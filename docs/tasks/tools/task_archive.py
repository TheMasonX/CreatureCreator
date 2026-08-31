#!/usr/bin/env python3
"""Archive or restore CreatureCreator task tickets.

Archiving moves a ticket from docs/tasks/tickets/ to docs/tasks/archive/,
removes its row from the active index, and records the move in the archive
changelog. Restoring does the reverse. Run task_validate.py afterwards.

Examples:
    python docs/tasks/tools/task_archive.py CC-091 --status Done --reason "Unity tests passed"
    python docs/tasks/tools/task_archive.py --all-status Done --reason "Bulk archival of completed work"
    python docs/tasks/tools/task_archive.py CC-091 --status Done --dry-run
    python docs/tasks/tools/task_archive.py --restore CC-091
"""

from __future__ import annotations

import argparse
import datetime
import sys

import common


def today() -> str:
    return datetime.date.today().isoformat()


def upsert_active(row):
    rows = [r for r in common.active_rows() if r["key"] != row["key"]]
    rows.append(row)
    common.write_active_rows(rows)


def remove_active(key):
    rows = [r for r in common.active_rows() if r["key"] != key]
    common.write_active_rows(rows)


def upsert_archive(row, changelog_entry):
    rows = [r for r in common.archive_rows() if r["key"] != row["key"]]
    rows.append(row)
    common.write_archive_rows(rows, common.archive_changelog() + [changelog_entry])


def remove_archive(key, changelog_entry):
    rows = [r for r in common.archive_rows() if r["key"] != key]
    common.write_archive_rows(rows, common.archive_changelog() + [changelog_entry])


def archive_one(key, target_status, reason, dry_run) -> bool:
    path, _loc = common.find_ticket(key, ("active",))
    if path is None:
        _, loc = common.find_ticket(key, ("archived",))
        if loc is not None:
            print(f"{key}: already archived")
        else:
            print(f"{key}: not found in tickets/ or archive/")
        return False
    ticket = common.read_ticket(path)
    meta = ticket["meta"] or {}
    title = meta.get("title", key)
    current = meta.get("status")
    if target_status is None:
        if current not in common.ARCHIVED_STATUSES:
            print(
                f"{key}: status '{current}' is active; pass --status with an "
                f"archived status ({', '.join(common.ARCHIVED_STATUSES)})"
            )
            return False
        target_status = current
    elif target_status not in common.ARCHIVED_STATUSES:
        print(f"{key}: --status must be one of {', '.join(common.ARCHIVED_STATUSES)}")
        return False

    status_note = ""
    if current and current != target_status:
        status_note = f" (status {current} -> {target_status})"

    if dry_run:
        print(f"{key}: would archive {path.name} as {target_status}{status_note}")
        return True

    common.ARCHIVE_DIR.mkdir(parents=True, exist_ok=True)
    new_text, changed = common.set_frontmatter_status(ticket["text"], target_status)
    destination = common.ARCHIVE_DIR / path.name
    path.rename(destination)
    if changed:
        destination.write_text(new_text, encoding="utf-8")
    remove_active(key)
    entry = f"{today()}: Archived {key} ({title}) as {target_status}."
    if status_note:
        entry += status_note
    if reason:
        entry += f" {reason}"
    upsert_archive({
        "key": key,
        "title": title,
        "status": target_status,
        "priority": meta.get("priority", ""),
    }, entry)
    print(f"{key}: archived as {target_status}{status_note}")
    return True


def restore_one(key, target_status, dry_run) -> bool:
    path, _loc = common.find_ticket(key, ("archived",))
    if path is None:
        print(f"{key}: not found in archive/")
        return False
    ticket = common.read_ticket(path)
    meta = ticket["meta"] or {}
    title = meta.get("title", key)
    if target_status is None:
        target_status = "Backlog"
    if target_status not in common.ACTIVE_STATUSES:
        print(f"{key}: --status for restore must be active ({', '.join(common.ACTIVE_STATUSES)})")
        return False
    if dry_run:
        print(f"{key}: would restore {path.name} to tickets/ as {target_status}")
        return True
    new_text, changed = common.set_frontmatter_status(ticket["text"], target_status)
    common.TICKETS_DIR.mkdir(parents=True, exist_ok=True)
    destination = common.TICKETS_DIR / path.name
    path.rename(destination)
    if changed:
        destination.write_text(new_text, encoding="utf-8")
    remove_archive(key, f"{today()}: Restored {key} ({title}) to tickets/ as {target_status}.")
    upsert_active({
        "key": key,
        "title": title,
        "status": target_status,
        "priority": meta.get("priority", ""),
    })
    print(f"{key}: restored to tickets/ as {target_status}")
    return True


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Archive or restore task tickets.")
    parser.add_argument("keys", nargs="*", help="CC keys to archive or restore.")
    parser.add_argument("--status", help="Target status for the move.")
    parser.add_argument("--reason", help="Optional reason appended to the changelog.")
    parser.add_argument("--dry-run", action="store_true", help="Print the plan only.")
    parser.add_argument("--restore", action="store_true",
                        help="Move tickets from archive/ back to tickets/.")
    parser.add_argument("--all-status", metavar="STATUS",
                        help="Archive every active ticket with this frontmatter status.")
    args = parser.parse_args(argv)

    if args.restore:
        if not args.keys:
            print("--restore requires at least one key")
            return 1
        if args.all_status:
            print("--all-status cannot be combined with --restore")
            return 1
        ok = True
        for key in args.keys:
            ok = restore_one(key.upper(), args.status, args.dry_run) and ok
        return 0 if ok else 1

    if args.all_status:
        if args.all_status not in common.ALL_STATUSES:
            print(f"--all-status must be one of {', '.join(common.ALL_STATUSES)}")
            return 1
        keys = [
            ticket["meta"]["key"]
            for ticket in common.all_tickets(("active",))
            if (ticket["meta"] or {}).get("status") == args.all_status
        ]
        if not keys:
            print(f"No active tickets with status '{args.all_status}'.")
            return 1
        print(f"Archiving {len(keys)} tickets with status '{args.all_status}': {', '.join(sorted(keys, key=common.key_sort_key))}")
    else:
        if not args.keys:
            print("Provide keys, or --all-status STATUS.")
            return 1
        keys = args.keys

    if args.status and args.status not in common.ARCHIVED_STATUSES:
        print(f"--status must be one of {', '.join(common.ARCHIVED_STATUSES)}")
        return 1

    ok = True
    for key in sorted({k.upper() for k in keys}, key=common.key_sort_key):
        ok = archive_one(key, args.status, args.reason, args.dry_run) and ok
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
