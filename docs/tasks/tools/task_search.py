#!/usr/bin/env python3
"""Search CreatureCreator task tickets with optional filters.

Active tickets (docs/tasks/tickets/) are searched by default. Add
--include-archive (or --location all) to include archived tickets.

Examples:
    python docs/tasks/tools/task_search.py --status "In Progress"
    python docs/tasks/tools/task_search.py --include-archive --status Done
    python docs/tasks/tools/task_search.py --tag morphology --priority P1
    python docs/tasks/tools/task_search.py --title "skeleton"
    python docs/tasks/tools/task_search.py --key CC-087 --include-archive --json

Exit codes: 0 on matches, 1 when no task matches (except with --count).
"""

from __future__ import annotations

import argparse
import json
import sys

import common


def split_multi(values):
    out = []
    for value in values or []:
        out.extend(x.strip() for x in value.split(",") if x.strip())
    return out


def matches(ticket, args) -> bool:
    meta = ticket.get("meta") or {}
    key = meta.get("key", "")
    if args.key and key not in args.key:
        return False
    if args.status and meta.get("status") not in args.status:
        return False
    if args.title and args.title.lower() not in (meta.get("title") or "").lower():
        return False
    if args.type and (meta.get("type") or "").lower() != args.type.lower():
        return False
    if args.priority and meta.get("priority") != args.priority:
        return False
    if args.tag and not any(tag in args.tag for tag in (meta.get("tags") or [])):
        return False
    return True


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Search CreatureCreator task tickets.")
    parser.add_argument("--status", action="append", default=[],
                        help="Filter by status; comma-separated or repeatable.")
    parser.add_argument("--key", action="append", default=[],
                        help="Filter by exact key; comma-separated or repeatable.")
    parser.add_argument("--title", help="Case-insensitive substring match on title.")
    parser.add_argument("--tag", action="append", default=[],
                        help="Filter by tag; comma-separated or repeatable.")
    parser.add_argument("--type", help="Filter by task type (case-insensitive).")
    parser.add_argument("--priority", help="Filter by priority, e.g. P1.")
    parser.add_argument("--include-archive", action="store_true",
                        help="Also search archived tickets (default is active only).")
    parser.add_argument("--location", choices=("active", "archived", "all"),
                        help="Explicit location filter.")
    parser.add_argument("--json", action="store_true", help="Emit JSON records.")
    parser.add_argument("--count", action="store_true", help="Print a count only.")
    args = parser.parse_args(argv)

    if args.location:
        locations = ("active", "archived") if args.location == "all" else (args.location,)
    elif args.include_archive:
        locations = ("active", "archived")
    else:
        locations = ("active",)

    args.status = set(split_multi(args.status))
    args.key = {k.upper() for k in split_multi(args.key)}
    args.tag = set(split_multi(args.tag))

    results = [t for t in common.all_tickets(locations) if matches(t, args)]
    results.sort(key=lambda t: common.key_sort_key((t.get("meta") or {}).get("key", "")))

    if args.count:
        print(len(results))
        return 0

    if args.json:
        payload = [{
            "key": t["meta"].get("key"),
            "title": t["meta"].get("title"),
            "status": t["meta"].get("status"),
            "type": t["meta"].get("type"),
            "priority": t["meta"].get("priority"),
            "tags": t["meta"].get("tags", []),
            "location": t["location"],
            "path": str(t["path"].relative_to(common.REPO_ROOT)).replace("\\", "/"),
        } for t in results]
        print(json.dumps(payload, indent=2))
        return 0

    if not results:
        print("No matching tasks.")
        return 1

    loc_short = {"active": "tickets", "archived": "archive"}
    print(f"{'KEY':<10} {'STATUS':<12} {'PRI':<4} {'LOC':<8} TITLE")
    for t in results:
        meta = t["meta"]
        print(
            f"{meta.get('key', ''):<10} {meta.get('status', ''):<12} "
            f"{meta.get('priority', ''):<4} {loc_short[t['location']]:<8} "
            f"{meta.get('title', '')}"
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())
