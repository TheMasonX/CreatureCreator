"""Shared helpers for the CreatureCreator task tools.

The tools are stdlib-only and run on any Python 3 interpreter. All paths are
derived from this file's location, so the scripts work from any working
directory.

Layout contract (see docs/tasks/README.md):

    docs/tasks/
      active-tasks.md   live index of active (non-archived) tickets
      tickets/          active tickets (Backlog / In Progress / Blocked / Review)
      archive/          archived tickets (Done / Superseded / Cancelled / Archived)
      tools/            this tooling
"""

from __future__ import annotations

import re
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parent
TASKS_DIR = TOOLS_DIR.parent
REPO_ROOT = TASKS_DIR.parent.parent

TICKETS_DIR = TASKS_DIR / "tickets"
ARCHIVE_DIR = TASKS_DIR / "archive"
ACTIVE_INDEX = TASKS_DIR / "active-tasks.md"
ARCHIVE_INDEX = ARCHIVE_DIR / "README.md"

KEY_RE = re.compile(r"^CC-(\d{3})([A-Z]?)$")

ACTIVE_STATUSES = ("Backlog", "In Progress", "Blocked", "Review")
ARCHIVED_STATUSES = ("Done", "Superseded", "Cancelled", "Archived")
ALL_STATUSES = ACTIVE_STATUSES + ARCHIVED_STATUSES

PRIORITY_RE = re.compile(r"^P[0-3]$")

REQUIRED_FIELDS = (
    "id", "key", "title", "status", "type", "priority",
    "tags", "dependsOn", "related", "links",
)
REQUIRED_HEADINGS = (
    "Summary", "Scope", "Acceptance Criteria", "Validation",
    "Findings", "Blockers", "Next Step",
)


def key_sort_key(key: str):
    """Sort key for a CC key. CC-056A sorts after CC-056 and before CC-057."""
    m = KEY_RE.match(key or "")
    if not m:
        return (10 ** 9, key or "")
    return (int(m.group(1)), m.group(2) or "")


def unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "'\"":
        return value[1:-1]
    return value


def _frontmatter_line_indices(lines):
    """Yield indices of lines belonging to the frontmatter block.

    Handles both fenced tickets (`---` ... `---`) and unfenced tickets where
    the frontmatter runs directly into the body heading.
    """
    if not lines or lines[0].strip() != "---":
        return
    for i in range(1, len(lines)):
        stripped = lines[i].rstrip("\n").strip()
        if stripped == "---":
            return
        if not stripped:
            yield i
            continue
        if re.match(r"^[A-Za-z_][A-Za-z0-9_-]*\s*:", lines[i]):
            yield i
            continue
        if re.match(r"^\s*-\s+", lines[i]):
            yield i
            continue
        return


def parse_frontmatter(text: str):
    """Return (meta, block) for a ticket's YAML frontmatter.

    meta is None when the file has no parseable frontmatter block. Supports
    fenced and unfenced frontmatter, scalar values, inline lists ([a, b]),
    and indented block lists.
    """
    if not text.startswith("---\n"):
        return None, None
    lines = text.split("\n")
    block_lines = [lines[i] for i in _frontmatter_line_indices(lines)]
    if not block_lines:
        return None, None
    meta = {}
    current_key = None
    for line in block_lines:
        stripped = line.strip()
        if not stripped:
            continue
        m = re.match(r"^([A-Za-z_][A-Za-z0-9_-]*)\s*:(.*)$", line)
        if m:
            key, value = m.group(1), m.group(2).strip()
            if value.startswith("[") and value.endswith("]"):
                inner = value[1:-1].strip()
                items = [unquote(x) for x in inner.split(",")] if inner else []
                meta[key] = items
                current_key = None
            elif value == "":
                meta[key] = []
                current_key = key
            else:
                meta[key] = unquote(value)
                current_key = None
            continue
        item = stripped[1:].strip()
        if current_key is not None:
            meta[current_key].append(unquote(item))
    return meta, "\n".join(block_lines)


def extract_headings(text: str):
    """Return level-2 heading names in document order, e.g. ['Summary', ...]."""
    return [
        line[3:].strip()
        for line in text.splitlines()
        if line.startswith("## ")
    ]


def read_ticket(path: Path):
    text = path.read_text(encoding="utf-8")
    meta, _block = parse_frontmatter(text)
    return {
        "path": path,
        "text": text,
        "meta": meta,
        "headings": extract_headings(text),
    }


def iter_ticket_paths(locations=("active", "archived")):
    for loc in locations:
        directory = TICKETS_DIR if loc == "active" else ARCHIVE_DIR
        for path in sorted(directory.glob("CC-*.md")):
            yield path, loc


def all_tickets(locations=("active", "archived")):
    tickets = []
    for path, loc in iter_ticket_paths(locations):
        tickets.append(dict(read_ticket(path), location=loc))
    return tickets


def find_ticket(key: str, locations=("active", "archived")):
    for loc in locations:
        directory = TICKETS_DIR if loc == "active" else ARCHIVE_DIR
        matches = sorted(directory.glob(f"{key}-*.md"))
        if len(matches) == 1:
            return matches[0], loc
        if len(matches) > 1:
            raise RuntimeError(f"Multiple ticket files for {key} under {directory}")
    return None, None


def index_rows(path: Path):
    rows = []
    if not path.exists():
        return rows
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) >= 4 and KEY_RE.match(cells[0]):
            rows.append({
                "key": cells[0],
                "title": cells[1],
                "status": cells[2],
                "priority": cells[3],
            })
    return rows


def write_index_rows(path: Path, rows, heading: str, intro=None):
    lines = [f"# {heading}", ""]
    if intro:
        lines.append(intro.rstrip())
        lines.append("")
    lines += ["| Key | Title | Status | Priority |", "| --- | --- | --- | --- |"]
    for row in sorted(rows, key=lambda r: key_sort_key(r["key"])):
        lines.append(
            f"| {row['key']} | {row['title']} | {row['status']} | {row['priority']} |"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def active_rows():
    return index_rows(ACTIVE_INDEX)


def write_active_rows(rows):
    write_index_rows(ACTIVE_INDEX, rows, "Active Tasks")


def archive_rows():
    return index_rows(ARCHIVE_INDEX)


def archive_changelog():
    if not ARCHIVE_INDEX.exists():
        return []
    entries = []
    in_changelog = False
    for line in ARCHIVE_INDEX.read_text(encoding="utf-8").splitlines():
        if line.startswith("## Changelog"):
            in_changelog = True
            continue
        if in_changelog and line.startswith("- "):
            entries.append(line[2:].strip())
    return entries


def write_archive_rows(rows, changelog_entries):
    lines = ["# Archived Tasks", ""]
    lines += [
        "Completed, superseded, cancelled, or otherwise archived CC tickets live",
        "here so that `docs/tasks/tickets/` stays focused on active work.",
        "Historical evidence and validation notes remain searchable with the task",
        "tools in `docs/tasks/tools/`.", "",
        "## Index", "",
        "| Key | Title | Status | Priority |", "| --- | --- | --- | --- |",
    ]
    for row in sorted(rows, key=lambda r: key_sort_key(r["key"])):
        lines.append(
            f"| {row['key']} | {row['title']} | {row['status']} | {row['priority']} |"
        )
    lines += ["", "## Changelog", ""]
    lines += [f"- {entry}" for entry in changelog_entries]
    ARCHIVE_INDEX.write_text("\n".join(lines) + "\n", encoding="utf-8")


def set_frontmatter_status(text: str, new_status: str):
    """Return (text, changed). Replaces the frontmatter status line in place."""
    lines = text.splitlines(keepends=True)
    if not lines or lines[0].strip() != "---":
        return text, False
    changed = False
    for i in _frontmatter_line_indices(lines):
        if re.match(r"^status\s*:", lines[i]):
            if lines[i].strip() != f"status: {new_status}":
                lines[i] = f"status: {new_status}\n"
                changed = True
    return "".join(lines), changed


def next_key(tickets) -> str:
    max_num = 0
    for ticket in tickets:
        m = KEY_RE.match((ticket.get("meta") or {}).get("key", ""))
        if m:
            max_num = max(max_num, int(m.group(1)))
    return f"CC-{max_num + 1:03d}"


def slugify(title: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-")
    return re.sub(r"-{2,}", "-", slug)[:80]
