#!/usr/bin/env python3
"""Scaffold a new CreatureCreator task ticket.

Picks the next unused CC key, writes a ticket with the canonical frontmatter
and body headings, and adds a row to the active index. Run task_validate.py
afterwards.

Examples:
    python docs/tasks/tools/task_new.py --title "Fix ankle winding at quality 12" \
        --priority P1 --tags runtime,extraction --depends-on CC-046
    python docs/tasks/tools/task_new.py --title "Editor gizmo polish" \
        --type Task --priority P3 --dry-run
"""

from __future__ import annotations

import argparse
import sys

import common


def fmt_list(items):
    return "[" + ", ".join(items) + "]"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Create a new task ticket.")
    parser.add_argument("--title", required=True)
    parser.add_argument("--type", default="Task")
    parser.add_argument("--priority", default="P2")
    parser.add_argument("--status", default="Backlog")
    parser.add_argument("--tags", default="", help="Comma-separated tags.")
    parser.add_argument("--depends-on", action="append", default=[],
                        help="Repeatable CC keys.")
    parser.add_argument("--related", action="append", default=[],
                        help="Repeatable CC keys.")
    parser.add_argument("--dry-run", action="store_true", help="Print the plan only.")
    args = parser.parse_args(argv)

    if args.status not in common.ACTIVE_STATUSES:
        print(f"--status must be active ({', '.join(common.ACTIVE_STATUSES)})")
        return 1
    if args.priority not in ("P0", "P1", "P2", "P3"):
        print("--priority must be P0, P1, P2, or P3")
        return 1

    key = common.next_key(common.all_tickets(("active", "archived")))
    token = common.KEY_RE.match(key).group(1) + common.KEY_RE.match(key).group(2).lower()
    slug = common.slugify(args.title)
    relative_path = f"docs/tasks/tickets/{key}-{slug}.md"
    path = common.TICKETS_DIR / f"{key}-{slug}.md"
    if path.exists():
        print(f"{relative_path} already exists; refusing to overwrite")
        return 1

    tags = [x.strip() for x in args.tags.split(",") if x.strip()]
    depends = [x.strip().upper() for x in args.depends_on if x.strip()]
    related = [x.strip().upper() for x in args.related if x.strip()]

    frontmatter = (
        "---\n"
        f"id: creature-task-{token}\n"
        f"key: {key}\n"
        f"title: {args.title}\n"
        f"status: {args.status}\n"
        f"type: {args.type}\n"
        f"priority: {args.priority}\n"
        f"tags: {fmt_list(tags)}\n"
        f"dependsOn: {fmt_list(depends)}\n"
        f"related: {fmt_list(related)}\n"
        "links: []\n"
        "---\n"
    )
    body = (
        "\n## Summary\n"
        f"\n{args.title}\n"
        "\n## Scope\n"
        "\n## Acceptance Criteria\n"
        "\n## Validation\n"
        "\n## Findings\n"
        "\n## Blockers\n"
        "\n## Next Step\n"
    )

    if args.dry_run:
        print(f"Would create {relative_path} with key {key} ({args.status}, {args.priority})")
        return 0

    common.TICKETS_DIR.mkdir(parents=True, exist_ok=True)
    path.write_text(frontmatter + body, encoding="utf-8")
    upsert_active({
        "key": key,
        "title": args.title,
        "status": args.status,
        "priority": args.priority,
    })
    print(f"Created {relative_path}")
    return 0


def upsert_active(row):
    rows = [r for r in common.active_rows() if r["key"] != row["key"]]
    rows.append(row)
    common.write_active_rows(rows)


if __name__ == "__main__":
    sys.exit(main())
