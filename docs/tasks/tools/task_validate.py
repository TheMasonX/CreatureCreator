#!/usr/bin/env python3
"""Validate consistency of CreatureCreator task records.

Checks ticket frontmatter, required headings, status/location placement,
active and archive index synchronization, superseded dispositions, and stale
references to archived tickets in living documentation. Run after any manual
edit or before archiving.

Exit codes:
    0  no errors or warnings
    1  errors (or warnings with --strict)
    2  warnings only (without --strict)

Examples:
    python docs/tasks/tools/task_validate.py
    python docs/tasks/tools/task_validate.py --strict
    python docs/tasks/tools/task_validate.py --fix
"""

from __future__ import annotations

import argparse
import re
import sys

import common


class Report:
    def __init__(self):
        self.items = []

    def error(self, msg):
        self.items.append(("error", msg))

    def warning(self, msg):
        self.items.append(("warning", msg))

    @property
    def errors(self):
        return [msg for level, msg in self.items if level == "error"]

    @property
    def warnings(self):
        return [msg for level, msg in self.items if level == "warning"]


LIVING_DOC_GLOBS = (
    ".github/skills/*.md",
    ".github/agents/*.md",
    "docs/adr/*.md",
    "docs/tasks/README.md",
    "docs/tasks/active-tasks.md",
    "docs/tasks/archive/README.md",
    "docs/tasks/task-archive-*.md",
)

TICKET_REF_RE = re.compile(r"docs/tasks/tickets/(CC-\d{3}[A-Z]?)-")


def rel(path) -> str:
    return str(path.relative_to(common.REPO_ROOT)).replace("\\", "/")


def validate_ticket(report, ticket):
    meta = ticket["meta"]
    path = rel(ticket["path"])
    if meta is None:
        report.error(f"{path}: missing or unparseable frontmatter")
        return
    for field in common.REQUIRED_FIELDS:
        if field not in meta:
            report.error(f"{path}: missing frontmatter field '{field}'")
    key = meta.get("key", "")
    if not common.KEY_RE.match(key):
        report.error(f"{path}: frontmatter key '{key}' is not a valid CC key")
    status = meta.get("status")
    if status not in common.ALL_STATUSES:
        report.error(f"{path}: invalid status '{status}'")
    if key and not ticket["path"].name.startswith(key + "-"):
        report.error(f"{path}: filename does not start with key '{key}'")
    m = common.KEY_RE.match(key)
    if m and meta.get("id"):
        token = m.group(1) + m.group(2).lower()
        if not meta["id"].endswith(token):
            report.warning(
                f"{path}: frontmatter id '{meta['id']}' does not end with key token '{token}'"
            )
    if meta.get("priority") and not common.PRIORITY_RE.match(str(meta["priority"])):
        report.warning(f"{path}: non-standard priority '{meta['priority']}'")

    # Heading completeness applies to active tickets only. Archived tickets are
    # frozen historical evidence and keep whatever structure they had.
    if ticket["location"] != "archived":
        headings = ticket["headings"]
        missing = [h for h in common.REQUIRED_HEADINGS if h not in headings]
        for h in missing:
            report.error(f"{path}: missing required heading '## {h}'")
        if not missing:
            present = [h for h in common.REQUIRED_HEADINGS if h in headings]
            if present != sorted(present, key=common.REQUIRED_HEADINGS.index):
                report.warning(f"{path}: required headings are out of order")

    if status == "Superseded":
        if "## Disposition" not in ticket["text"]:
            report.error(f"{path}: Superseded ticket needs '## Disposition' naming its replacement")
        else:
            disposition = ticket["text"].split("## Disposition", 1)[1]
            if not re.search(r"CC-\d{3}[A-Z]?", disposition):
                report.warning(f"{path}: Disposition does not name a replacement CC key")

    if ticket["location"] == "active" and status in common.ARCHIVED_STATUSES:
        report.error(f"{path}: archived status '{status}' in tickets/ (should be in archive/)")
    if ticket["location"] == "archived" and status in common.ACTIVE_STATUSES:
        report.error(f"{path}: active status '{status}' in archive/ (should be in tickets/)")

    for line_no, line in enumerate(ticket["text"].splitlines(), 1):
        if line != line.rstrip():
            report.warning(f"{path}:{line_no}: trailing whitespace")
            break


def validate_index(report, tickets_by_key):
    rows = common.active_rows()
    row_keys = set()
    for row in rows:
        row_keys.add(row["key"])
        if row["key"] not in tickets_by_key:
            report.error(f"active-tasks.md: row '{row['key']}' has no ticket file")
            continue
        ticket = tickets_by_key[row["key"]]
        meta = ticket["meta"] or {}
        if ticket["location"] != "active":
            report.error(
                f"active-tasks.md: '{row['key']}' is archived and must not be in the active index"
            )
            continue
        for field, label in (("title", "Title"), ("status", "Status"), ("priority", "Priority")):
            if (meta.get(field) or "") != (row[field] or ""):
                report.error(
                    f"active-tasks.md: '{row['key']}' {label} '{row[field]}' "
                    f"differs from ticket '{meta.get(field)}'"
                )
    for key, ticket in tickets_by_key.items():
        if ticket["location"] == "active" and key not in row_keys:
            report.error(
                f"tickets/{ticket['path'].name}: active ticket {key} missing from active-tasks.md"
            )


def validate_archive_index(report, tickets_by_key):
    rows = common.archive_rows()
    row_keys = set()
    for row in rows:
        row_keys.add(row["key"])
        if row["key"] not in tickets_by_key:
            report.error(f"archive/README.md: row '{row['key']}' has no ticket file")
            continue
        ticket = tickets_by_key[row["key"]]
        if ticket["location"] != "archived":
            report.error(
                f"archive/README.md: '{row['key']}' is active and must not be in the archive index"
            )
    for key, ticket in tickets_by_key.items():
        if ticket["location"] == "archived" and key not in row_keys:
            report.warning(
                f"archive/README.md: archived ticket {key} missing from archive index"
            )


def validate_references(report, tickets_by_key):
    for pattern in LIVING_DOC_GLOBS:
        for path in common.REPO_ROOT.glob(pattern):
            if not path.is_file():
                continue
            path_rel = rel(path)
            for line_no, line in enumerate(
                path.read_text(encoding="utf-8", errors="replace").splitlines(), 1
            ):
                for m in TICKET_REF_RE.finditer(line):
                    key = m.group(1)
                    ticket = tickets_by_key.get(key)
                    if ticket and ticket["location"] == "archived":
                        report.warning(
                            f"{path_rel}:{line_no}: references archived {key} at old "
                            "tickets/ path (use docs/tasks/archive/)"
                        )


def fix_index(report, tickets_by_key):
    """Sync active-tasks.md rows from ticket frontmatter (title/status/priority)."""
    rows = common.active_rows()
    by_key = {}
    for row in rows:
        if row["key"] not in tickets_by_key:
            continue
        ticket = tickets_by_key[row["key"]]
        if ticket["location"] != "active":
            continue
        meta = ticket["meta"] or {}
        fixed = {
            "key": row["key"],
            "title": meta.get("title") or row["title"],
            "status": meta.get("status") or row["status"],
            "priority": meta.get("priority") or row["priority"],
        }
        by_key[fixed["key"]] = fixed
    for key, ticket in tickets_by_key.items():
        if ticket["location"] != "active":
            continue
        meta = ticket["meta"] or {}
        by_key.setdefault(key, {
            "key": key,
            "title": meta.get("title", ""),
            "status": meta.get("status", ""),
            "priority": meta.get("priority", ""),
        })
    common.write_active_rows(list(by_key.values()))
    print("active-tasks.md: synchronized rows from ticket frontmatter")


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Validate CreatureCreator task records.")
    parser.add_argument("--strict", action="store_true", help="Treat warnings as failures.")
    parser.add_argument("--fix", action="store_true",
                        help="Synchronize active-tasks.md rows from ticket frontmatter.")
    parser.add_argument("--skip-refs", action="store_true",
                        help="Skip stale-reference checks in living documentation.")
    parser.add_argument("--quiet", action="store_true",
                        help="Print only the summary line.")
    args = parser.parse_args(argv)

    report = Report()
    tickets = common.all_tickets()

    grouped = {}
    for ticket in tickets:
        key = (ticket["meta"] or {}).get("key") or ticket["path"].stem
        grouped.setdefault(key, []).append(ticket)

    for key, group in grouped.items():
        if len(group) > 1:
            names = ", ".join(rel(g["path"]) for g in group)
            report.error(f"duplicate key {key}: {names}")

    for ticket in tickets:
        validate_ticket(report, ticket)

    tickets_by_key = {key: group[0] for key, group in grouped.items()}

    if args.fix:
        fix_index(report, tickets_by_key)

    validate_index(report, tickets_by_key)
    validate_archive_index(report, tickets_by_key)
    if not args.skip_refs:
        validate_references(report, tickets_by_key)

    for level, msg in report.items:
        if not args.quiet:
            print(f"{level.upper()}: {msg}")

    exit_code = 0
    if report.errors:
        exit_code = 1
    elif report.warnings and args.strict:
        exit_code = 1
    elif report.warnings:
        exit_code = 2
    print(
        f"Validation: {len(report.errors)} errors, {len(report.warnings)} warnings "
        f"across {len(tickets)} tickets."
    )
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
