# Memories

MemorySmith-style memory records for CreatureCreator. Each memory is one JSON
record that captures a durable fact, decision, or learned pattern. The wiki
engine serves these records at `/memories`.

## Layout

| Path | Contents |
| --- | --- |
| `Core/` | Accepted, durable facts and decisions. |
| `Working/` | Active or provisional records under review. |
| `Unconsolidated/` | New or candidate records awaiting consolidation. |
| `Deprecated/` | Superseded or retired records. |

The `Core/` and `Working/` directories are committed. Records move through
the state machine during maintenance runs. Keep records factual and link the
source page or task where relevant.
