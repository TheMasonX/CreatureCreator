# CreatureCreator — Code-Level Deep Dive (Bugs, Duplication, Primitive Obsession, Ticket Corrections)

**Scope:** `Assets/Scripts` (144 C# files, ~25k lines: `Runtime/`, `Editor/`, `Tests/`).
**Method:** Full pull of `main` via tarball, line-level read of the Definition/Serialization/Skeleton/Editor core, cross-referenced against `docs/tasks/tickets/CC-*.md` (86 tickets) and all 11 files in `docs/audits/` to avoid re-reporting known findings.
**Relationship to prior audits:** The existing `docs/audits/` corpus is architecture/strategy-level (resolved-morphology layering, P1 sequencing, contract migration). This pass is deliberately narrower and more literal: specific lines, specific duplicated logic, specific ticket corrections. Nothing below repeats the CC-056/CC-007/CC-051 architectural narrative already covered at length.

---

## 1. Ticket corrections — pinpointed root causes for existing Backlog bugs

These four backlog tickets already correctly describe *symptoms*. Each one below now has an exact call chain or line, which changes the fix (and in one case, changes the diagnosis).

### 1.1 CC-082 (`ToDictionary` throw on duplicate part IDs) — exact call chain, and `FindPart` is innocent

The ticket hedges with *"`CreatureDefinition.FindPart` (or an equivalent lookup) builds `Parts.ToDictionary(...)`"*. `FindPart` does **not** — it's a safe linear scan (`CreatureDefinition.cs`, `FindPart`). The actual offender is `HasParentCycle`:

```csharp
// CreatureDefinition.cs — HasParentCycle
var byId = Parts.ToDictionary(p => p.Id, p => p);   // throws ArgumentException on a dup key
```

and it's reached unconditionally, every call, via:

```
DefinitionValidator.Validate()
  → ValidateDuplicateIds()          // line 31 — runs FIRST, correctly appends a DuplicateId issue, does not throw
  → ValidateParentsAndCycles()      // line 32 — runs SECOND
      → definition.HasParentCycle() // line 446 — throws before the method can return
```

So the validator's own "report only, never throw" contract is broken specifically by check-ordering: the duplicate-ID issue is computed and discarded because the *next* check crashes before `Validate()` can return. Recommend fixing `HasParentCycle` (e.g. `Dictionary.TryAdd` in a loop, keeping the first-seen part per ID, or switching to a `GroupBy`) rather than reordering the two calls — reordering would just change which check crashes first if a third caller invokes `HasParentCycle` directly.

### 1.2 CC-083 (parentless-part validation gap) — the validator is already correct; the *test helper* can't express the case it's testing

Re-reading `ValidateParentsAndCycles` (`DefinitionValidator.cs:430-435`), a part with `ParentId == null` **is** flagged with `InvalidBodyParent` — the check the ticket says is missing already exists in current `main`. The actual bug is in the test's own fixture:

```csharp
// DefinitionValidatorTests.cs
private static CreaturePart ValidPart(string id, string parentId = null)
{
    return new CreaturePart { ParentId = parentId ?? CreatureDefinition.BodyId, ... };
}
```

```csharp
// Validate_RejectsPartWithNoParent
definition.AddPart(ValidPart("part_root", parentId: null));
```

`parentId ?? CreatureDefinition.BodyId` fires on the *explicitly passed* `null` exactly like an omitted argument — there is no way to call `ValidPart` and get a truly-null `ParentId` back. The test therefore builds a part parented to `"body"` and asserts on a code path it never exercises. This looks like it would currently **pass** for the wrong reason, not fail — worth an actual test run to confirm, but either way the ticket's diagnosis ("the check does not fire") is misattributed to the validator. Recommend: add a sentinel (e.g. `ValidPartWithParent(id, string.Empty)` won't work either since `""` isn't null; better to add a second helper `ValidPartNoParent(id)` that sets `ParentId = null` directly without going through the `??` default) and re-verify against the real validator before touching `DefinitionValidator.cs` at all.

### 1.3 CC-084 (`DisplayName` round-trip mismatch) — exact line

```csharp
// CanonicalJsonWriter.cs:231
WriteField(sb, "displayName", string.IsNullOrWhiteSpace(part.DisplayName) ? part.Id : part.DisplayName);
```

The writer substitutes `part.Id` for a null/blank `DisplayName` (presumably so exported JSON is human-readable in external tools without a blank label). The reader (`JsonDnaSerializer.cs:284`, `ReadOptionalString(obj, "displayName")`) has no matching "was this a fallback?" signal, so a round-tripped part always gains a non-null `DisplayName`. This is a one-directional lossy transform disguised as serialization. Two real options, not just "fix the mismatch":
- **Preserve intent:** omit the `displayName` key entirely when null/blank, and have the reader leave `DisplayName` null. Only apply the `Id`-fallback at *display* time (editor label rendering), not at the serialization boundary.
- **Or make the loss intentional:** if a null `DisplayName` is meant to be normalized to the Id permanently on first save, update the test's expectation instead of the writer, and document that `DisplayName` is populate-on-save, not nullable-forever.
Either is fine; the current state (writer silently upgrades null → Id, reader has no idea it happened) is the one option that should not stay as-is, since it's neither a clean round-trip nor a documented one-way migration.

### 1.4 CC-078 / CC-042 — confirmed, no correction, just located precisely for whoever picks them up
- CC-078: the dual-purpose `ValidationCode.DuplicateBodySampleId` is emitted from one `ValidateBody` check that conflates "same ID twice" with "IDs not monotonically increasing" — confirmed still a single code path, no separate branch.
- CC-042: `ClonePartAsChild`'s XML doc still lists `PartType, Shape, Appearance, MirrorAcrossSymmetryPlane, DisplayName` as copied and omits `Limb`, while `CreaturePart.Clone()` (which it calls) demonstrably deep-clones `Limb`. Trivial doc fix, unchanged from when it was flagged.

---

## 2. New finding — the "legacy shape" fallback rule is implemented three separate times (primitive obsession + duplication)

`ShapeDefinition` still carries the CC-018-era `PrimarySize` scalar alongside the CC-043 per-shape fields (`Radius`, `CapsuleHeight`, `EllipsoidRadii`, `BoxHalfExtents`). CC-043 is explicitly "In Progress" and its own ticket text says this migration is deliberate and incremental — that part is fine. What isn't tracked anywhere is that **the rule for "is this field unset, and what's its effective value if so?" now exists independently in three places**, and they don't fully agree:

**a) `ShapeDefinition.UsesLegacySize()`** (`ShapeDefinition.cs:95`) — a single joint predicate: legacy *only if* `Radius == 0 && CapsuleHeight == 0 && EllipsoidRadii == zero && BoxHalfExtents == zero`, all four at once.

**b) `DefinitionCanonicalizer.CanonicalizeShape()`** (`DefinitionCanonicalizer.cs:111-119`) — four *independent* per-field checks, each falling back to `PrimarySize`:
```csharp
if (shape.Radius <= 0f) shape.Radius = legacySize;
if (shape.CapsuleHeight <= 0f) shape.CapsuleHeight = 1f;         // note: falls back to 1f, not PrimarySize
if (shape.EllipsoidRadii.x <= 0f) shape.EllipsoidRadii = new Vector3(legacySize, legacySize, legacySize);
if (shape.BoxHalfExtents.x <= 0f) shape.BoxHalfExtents = new Vector3(legacySize, legacySize, legacySize);
```

**c) `CreatureEditorWindow.DrawShapeFields()`** (`CreatureEditorWindow.cs:1382-1390`) — the same four independent per-field checks, hand-copied into the inspector GUI code, using `> 0f` instead of `<= 0f` (equivalent, just inverted) and reading `PrimarySize` fresh each repaint.

Concretely, these three don't even agree with each other on the *sign convention* (`<=0f` vs the joint `Radius==0f` used in `UsesLegacySize`), meaning a negative `Radius` — which `IsFinite()`/`HasValidParameters()` already treats as invalid — is nonetheless silently "repaired" as legacy by (b) and (c) but not treated as legacy by (a). None of this is exercised by existing tests because they mostly construct shapes with all fields consistently zero or consistently populated.

**Why this matters beyond style:** this is exactly the kind of DNA-schema migration rule the project's own ADR process (ADR-004, referenced in CC-043) is supposed to own in one place. Right now, changing the legacy fallback behavior (e.g., deciding `CapsuleHeight`'s fallback should be `legacySize` instead of the hardcoded `1f`) requires editing Runtime canonicalization *and* Editor GUI code *and* remembering the model's own `UsesLegacySize()` predicate isn't actually called by either.

**Recommendation:** give `ShapeDefinition` a single method, e.g. `ShapeDefinition WithLegacyDefaultsApplied()`, that both the canonicalizer and the editor window call instead of re-deriving the rule. This is a natural, small extension to CC-043's existing scope — flagging it as an addendum to that ticket rather than a new one, since CC-043 is still open and this is squarely "finish the per-shape-parameter migration cleanly" territory.

---

## 3. New finding — `PartType.Limb/Leg/Arm` without a `Limb` chain is a silently-accepted, undocumented state, and the project's own demo fixture relies on it

`CreaturePart.Limb`'s XML doc says plainly: *"When non-null, the part is a limb... When null, the part is a plain primitive shaped by Shape."* That's a clean bidirectional contract in prose. The validator only enforces **one direction** of it:

```csharp
// DefinitionValidator.cs — ValidateLimbChains
bool isLimbChainType = part.PartType == PartType.Limb || part.PartType == PartType.Leg || part.PartType == PartType.Arm;

if (part.Limb != null && !isLimbChainType) { /* reports InvalidLimbChain */ continue; }
if (part.Limb == null) continue;   // <-- the reverse case (Limb/Leg/Arm-typed, no chain) is never checked
```

A part typed `Limb`/`Leg`/`Arm` with `Limb == null` is accepted silently and generates as a plain `Shape` primitive — the exact "plain primitive" behavior the doc comment reserves for `PartType.Part`. This isn't hypothetical: **the repo's own runtime demo fixture does it**:

```csharp
// CreatureRuntimePreview.cs — CreateDemoDefinition()
definition.AddPart(new CreaturePart {
    Id = "runtime_head", ParentId = CreatureDefinition.BodyId,
    PartType = PartType.Limb,                 // <-- typed as a limb...
    DisplayName = "Head",
    Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.85f, SmoothBlendRadius = 0.2f },
    // ...but no `.Limb = new LimbChain {...}` — this is a plain sphere, not a joint chain
});
```

This also happens to be the third, independently-constructed `ShapeDefinition` referenced in §2 (it hand-sets `PrimarySize` + `SmoothBlendRadius` only, the exact all-other-fields-zero shape that `UsesLegacySize()` exists to handle — so this one fixture alone touches two separate findings).

**Recommendation:**
1. Retype the demo fixture's head to `PartType.Part` (or `PartType.Eye`/whatever is semantically closest) — it isn't a limb and shouldn't claim to be one.
2. Either make `ValidateLimbChains` enforce the missing direction (a `Limb`/`Leg`/`Arm`-typed part with no chain gets flagged, or a warning-severity `ValidationCode`), or explicitly document in `PartType.cs`/`CreaturePart.cs` that a limb-typed part with no chain is a supported fallback state — right now neither the code nor the docs agree with each other, and the only executable evidence (the demo fixture) contradicts the doc comment.
3. Worth cross-referencing with **CC-036** (anatomical limb parent validation, still Backlog) — that ticket is about to add more `PartType`-conditional validation in the same method; this gap should be closed in the same pass rather than layered under new anatomical rules.

---

## 4. New finding — `IsLimbChainType` is duplicated across the Editor/Runtime assembly boundary (root cause identified, not just "same code twice")

`Editor/LimbAuthoring.cs`:
```csharp
public static bool IsLimbChainType(PartType type)
    => type == PartType.Limb || type == PartType.Leg || type == PartType.Arm;
```

`Runtime/Definition/DefinitionValidator.cs` (`ValidateLimbChains`, see §3) reimplements the identical three-way check inline instead of calling it.

This isn't a careless copy-paste — checking the `.asmdef` files confirms it's structurally forced:

```
ProceduralCreature.Runtime.asmdef   → references: [Unity.Burst]                         (no Editor dependency)
ProceduralCreature.Editor.asmdef    → references: [ProceduralCreature.Runtime]           (Editor → Runtime only)
```

`DefinitionValidator` lives in the Runtime assembly, which (correctly) cannot reference the Editor assembly — Editor-only code doesn't exist in player builds. So `LimbAuthoring.IsLimbChainType` is structurally unreachable from `DefinitionValidator`, and the inline duplicate is the only option *as currently organized*.

**Recommendation:** move the predicate down into Runtime — e.g. a static helper on `PartType` itself, or a small `Runtime/Definition/PartTypeExtensions.cs` — and have `LimbAuthoring` (Editor) call *that* instead of owning the canonical definition. This is a one-line-of-substance fix but worth calling out because it's a pattern, not a one-off: any future rule that both editor authoring and runtime validation need to agree on (this project already has two: limb-chain classification here, and the CC-036 anatomical-parent rules about to be added) will hit the same assembly wall unless the "which PartTypes mean what" logic is established as Runtime-owned from the start.

---

## 5. Medium finding — exception-driven control flow in the validator's hot path

`DefinitionValidator.ValidateResolvedEnvelope` wraps `ResolvedBody.Resolve(...)` and `ResolvedLimb.Resolve(...)` in `try { } catch (DomainException) { continue/skip }` — four separate times in one method — to handle "this part/body is already structurally broken, so its resolved envelope is undefined, skip it" (each site has a comment explaining exactly this, so the *intent* is clear and reasonable). The issue is mechanism, not intent: a definition mid-edit in the authoring UI is *routinely* structurally incomplete (a body with one sample, a limb chain with a joint mid-drag), and `Validate()` is documented as running "before expensive generation" — i.e., on every preview regeneration, not just on save. Throwing and catching a `DomainException` on every keystroke-triggered validation pass of a partially-authored creature is needless allocation/stack-unwind overhight in a path CC-008's profiling work (already Done) presumably measured without this specific contributor broken out.

**Recommendation:** give `ResolvedBody`/`ResolvedLimb` a `TryResolve(..., out Resolved... result)` overload returning `bool`, and have the validator call that instead of catching. Low urgency, but easy to fix alongside CC-056A/B work since those are the exact two types.

---

## 6. Smaller items worth a line each

- **`DefinitionCanonicalizer.CanonicalizeShape`'s `CapsuleHeight` fallback is `1f`, not `legacySize`** (see §2b) — every other field falls back to the legacy scalar; `CapsuleHeight` alone falls back to a hardcoded `1`. If this is intentional (a capsule's *height* was never meaningfully expressed by the single legacy scalar, only its radius was), it should say so in a comment; right now it reads as an inconsistency in a block of four otherwise-parallel lines.
- **`ShapeDefinition.HasValidParameters()`'s "explicit" branch requires all four shape-family fields positive regardless of `Type`** — a `Sphere` with a perfectly valid `Radius` but a zeroed `BoxHalfExtents` (which is irrelevant to a sphere) fails validation under the strict branch. In practice this is masked because every code path that constructs a `ShapeDefinition` other than the two flagged above (CreatureRuntimePreview's fixture, and any hand-built test fixture) goes through `DefaultSphere` or the JSON reader's `ReadShape`, both of which populate all four fields unconditionally — so the type-blind check currently never fires incorrectly, but that's coincidence rather than a property the type system enforces. Worth a comment at minimum; worth a `switch` on `Type` if anyone hits it.
- **No `TODO`/`FIXME`/`HACK` markers anywhere in `Runtime/` or `Editor/`** — genuinely clean in this respect; the project's audit-and-ticket discipline is substituting for inline debt markers, which is a good sign, not a smell.
- **Exception typing is consistently `DomainException`/`DnaDeserializationException`** across all 21 `catch` sites in non-test code — no bare `catch (Exception)` swallowing, no empty catch blocks. Also worth noting as a positive, not a finding.

---

## 7. What's *not* here, and why

- No re-litigation of the managed-vs-portable SDF duplication (`SdfProgramBuilder.cs`), the resolved-morphology layering, or the `SmoothBlendRadius`-still-read-in-two-places item — all three are already tracked in detail in `creaturecreator-audit-addendum-26-08-24.md` and the CC-045/CC-056 ticket chain, and remain accurate as of this pull.
- `CreatureEditorWindow.cs` at 2850 lines / ~150 members is a real God Object by any measure, but its decomposition is already the subject of the large `cc018-cc020-cc027-cc028-architecture-audit` (2469 lines) — re-deriving that here would just restate it. The one concrete, novel thing pulled from that file in this pass is the triplicated shape-fallback logic in §2, which that audit predates (CC-043 phase 1 landed after).
- Did not re-verify the `MarchingCubesExtractor`/`DensityGrid` Burst path in depth — the addendum audit already went deep there (and CC-075 already closed its one finding). A fresh pass would be duplicative without new commits in that area since.

---

## Summary table

| # | Finding | Type | Ticket relationship |
|---|---|---|---|
| 1.1 | `HasParentCycle`'s `ToDictionary` throws before duplicate-ID issue can be returned | Bug, pinpointed | Corrects CC-082 |
| 1.2 | `ValidPart` test helper's `??` can't express a null `ParentId`; validator itself looks correct | Test bug, re-diagnosis | Corrects CC-083 |
| 1.3 | `CanonicalJsonWriter.cs:231` substitutes `Id` for null `DisplayName` at write time | Bug, pinpointed | Corrects CC-084 |
| 2 | Legacy-shape fallback rule duplicated 3x with inconsistent fallback values | Primitive obsession / duplication | Extends CC-043 |
| 3 | `PartType.Limb/Leg/Arm` with no `Limb` chain is unvalidated; demo fixture relies on it | Validation gap + doc/code contract mismatch | New; touches CC-036 |
| 4 | `IsLimbChainType` duplicated across Editor/Runtime asmdef boundary | Duplication, root-caused | New |
| 5 | Exception-as-control-flow in `ValidateResolvedEnvelope`'s hot path | Design smell | New; pairs with CC-056A/B |
| 6 | `CapsuleHeight` fallback inconsistency, type-blind shape validation | Minor / documentation | New |
