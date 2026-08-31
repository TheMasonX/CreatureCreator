# Task Archive and Supersession Record

**Date:** 2026-08-30
**Reason:** Full reconciliation of the 2026-08-25 through 2026-08-30 audit series.

## Superseded Architecture Records

The following records remain in `docs/tasks/tickets/` as historical evidence, but their unfinished architecture scope is closed by CC-087:

- **CC-006** Body and Limb creature model. Schema work is historical; remaining resolved-model work moves to CC-087.
- **CC-009** Morphology compiler and semantic attachment model. Its broad compiler scope is replaced by the concrete snapshot boundary in CC-087.
- **CC-056** Canonical resolved morphology umbrella. CC-056A and CC-056B remain completed increments; CC-087 is the continuation and closure task.

Do not delete these records. Existing links and validation evidence must remain searchable.

## Retained Existing Tasks With New Ownership

- CC-022 owns reusable Body frame computation and feeds CC-087.
- CC-043 and CC-045 feed CC-088.
- CC-054 receives the canonicalization invariant from the synthesis report.
- CC-055 is a prerequisite for representation-independent centerline and attachment identity.
- CC-052, CC-053, and CC-072 remain consumer work downstream of CC-087.
- CC-078, CC-079, and CC-080 remain narrow cleanup records, but CC-089 owns the malformed-definition contract.

## Closed Findings Not Reopened

Mirrored mesh winding, limb blend-source ownership, generated bounds, semantic attachment first-slice work, shared bone resolver first-slice work, and the CC-082 through CC-084 fixes have existing evidence. Follow-up work must reference that evidence instead of creating duplicate tickets.

## Peer-Review Record Corrections

- `docs/tasks/tickets/CC-034-fastnoise2-dllimport-restore.md` was renamed to
	`docs/tasks/tickets/CC-047-fastnoise2-dllimport-restore.md`. Its frontmatter
	already declared `key: CC-047`.
- `docs/tasks/tickets/CC-036-warning-and-imGUI-cleanup.md` was renamed to
	`docs/tasks/tickets/CC-048-editor-warning-cleanup.md`. Its frontmatter already
	declared `key: CC-048`.
- The peer review extended CC-036, CC-043, CC-088, CC-089, and CC-090 without
	creating new keys. The active index remains unchanged because every accepted
	mechanism has an existing owner.
