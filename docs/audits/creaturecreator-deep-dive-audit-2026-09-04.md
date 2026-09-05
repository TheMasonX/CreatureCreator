# CreatureCreator Deep-Dive Audit — 2026-09-04

**Scope:** Independent read-only architectural audit of `themasonx/creaturecreator`
(`main`, tarball snapshot fetched 2026-09-04). ~28,000 lines of C# across 152
files (~16,000 non-test). Cross-referenced against all 100 `CC-###` records in
`docs/tasks/tickets/` and `docs/tasks/archive/`, and against the 41 prior audit
documents in `docs/audits/` (most recent: `creaturecreator-audit-synthesis-2026-09-02.md`,
fixed point `dff4a69`).

**Method note:** This project already runs a mature, near-continuous audit
process (41 prior audit docs, delta-only follow-ups, a MemorySmith/`TSK-####`
task board layered over the frozen `CC-###` history). Per that project's own
rule ("do not create a duplicate task"), this report does **not** re-litigate
findings already owned by an open task unless the source shows the finding is
incomplete, wrong, or broader than currently scoped. Every finding below cites
file:line evidence I read directly from the current tree.

---

## Executive Summary

The codebase is unusually well-disciplined for its size: no `TODO`/`FIXME`
markers anywhere, no swallowed/empty catch blocks (every `catch` is narrow and
commented with its defensive rationale), no static mutable state, and the
finite-check consolidation claimed complete by the 2026-09-01 handoff checks
out in the current source. The main open wound is exactly what the project's
own tracker already says it is: **shape-fallback and mirror-geometry logic is
copy-pasted across the Definition/Serialization/Editor boundary faster than
the shared-utility consolidation (CC-090 / TSK-0094) can absorb it.** This
audit's contribution is evidence that the duplication is wider than the
currently-recorded evidence shows, plus one correctness inconsistency and two
smaller drift/shallow-module items.

No finding below rises to "architecture is wrong" — the domain model,
canonicalize→resolve→generate pipeline, and DNA/resolved-snapshot separation
are sound and already the subject of ongoing hardening (TSK-0093/0095/0098).
Findings are consolidation and correctness corrections within that existing
architecture.

---

## High

### H1. Legacy shape-size fallback cascade is duplicated in **four** places, not the one CC-090/synthesis currently tracks

`docs/audits/creaturecreator-audit-synthesis-2026-09-02.md` (F-02) confirms one
occurrence of this fallback in `CreaturePartWorldTransformResolver` and files
it under CC-090. The same "if the current field is unset, fall back to
`PrimarySize`, and default `PrimarySize` itself to `0.5f`" cascade is
independently re-implemented in three more places, with a 5th independent copy
of the sibling "capsule height defaults to `1f`" rule:

| Site | Evidence |
| --- | --- |
| Deserialize (read) | `Assets/Scripts/Runtime/Serialization/JsonDnaSerializer.cs:441-455` (`ReadShape`) |
| Runtime resolve | `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs:24-34` (`ResolvedShape` ctor) — the one instance the synthesis doc already tracks |
| Canonicalize (mutate-in-place) | `Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs:122-126` |
| Editor UI | `Assets/Scripts/Editor/CreatureEditorWindow.cs:1394-1402` |
| `CapsuleHeight = 1f` default alone | additionally in `ShapeDefinition.cs:37` (struct default) |

All five sites agree today (I diffed the constants: `0.5f` legacy size,
`1f` capsule height, everywhere), so there is no live bug — but there is no
single source of truth, and the editor copy in particular was not in the
synthesis doc's residual-risk list. A future change to legacy-interpretation
semantics (which the synthesis doc explicitly flags as still undecided) has to
be made in four files by hand or it silently reintroduces the exact class of
skew CC-090 exists to close.

**Confidence: Confirmed** (read all five sites directly).
**Recommendation:** One `ShapeDefinition.ResolveLegacyFallback()` (or a static
helper on `ShapeDefinition`) that the deserializer, canonicalizer, resolver,
and editor all call. This is squarely CC-090 scope — recommend adding the
editor site and the `ShapeDefinition` default explicitly to that ticket's
`links:` list so the eventual fix doesn't miss it.

### H2. `MirrorUtility` exists specifically to own the mirror-across-X matrix, but three of four call sites don't use it

`Assets/Scripts/Runtime/Skeleton/MirrorUtility.cs` defines
`ReflectAcrossX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f))` and
`MirrorAcrossXPlane(...)` as the shared mirror primitive (this looks like the
intended product of CC-076 "shared semantic bone resolver" / the CC-090
"mirror operations" scope). In the current tree it is reimplemented
independently in:

- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs:57` — `CreatureMirrorAcrossX`
- `Assets/Scripts/Runtime/Skeleton/SemanticBoneResolver.cs:29` — `ReflectAcrossX`
- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs:33` — `ReflectAcrossX`

Only `SkeletonInferrer.cs:75` does it right, and even that reuses
`SemanticBoneResolver.ReflectAcrossX` rather than `MirrorUtility` — i.e. the
"canonical" copy in `MirrorUtility` is currently the *least*-referenced of the
four identical definitions.

**Confidence: Confirmed.**
**Correction to existing scope:** the 2026-09-01 dedup handoff's item 3 lists
"mirror operations" as a still-duplicated *mechanical utility* needing to be
built. It's already built (`MirrorUtility`) — this is a **wiring gap**, not a
missing-utility gap, and is a smaller, more mechanical fix than that framing
suggests (replace four field declarations + their few call sites with a
reference to `MirrorUtility`, no new abstraction needed). Worth noting on
TSK-0094 so the remaining work isn't re-scoped as "design a mirror utility."

## Medium

### M1. Two divergent quaternion normalize+quantize algorithms — one has no degenerate-magnitude guard

`TransformData.NormalizeAndQuantizeRotation` (`Assets/Scripts/Runtime/Definition/TransformData.cs:72-89`)
normalizes **first**, quantizes the normalized components, then explicitly
checks whether the quantized result's magnitude-squared collapsed toward zero
and falls back to `Quaternion.identity` before re-normalizing.

`DefinitionCanonicalizer.NormalizeAndQuantizeQuaternion`
(`Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs:247-254`), used
for `GeometryAttachment.Orientation` (mesh-geometry attachment orientation,
CC-031), quantizes the **raw, unnormalized** components first and then calls
`.normalized` with no degenerate-magnitude check.

These aren't cosmetically different — they're two different orders of
operation for the same conceptual "make this quaternion canonical" step, and
only one of them is proven safe against a quantization step that drives the
resulting magnitude toward zero (Unity's `Quaternion.normalized` on a
near-zero-magnitude value is not guaranteed to be a sane rotation). I did not
find a specific input that triggers this in the current test suite — the
concern is that `GeometryAttachment.Orientation` quantization has no test
coverage for the near-degenerate case that `TransformData.Rotation` explicitly
does.

**Confidence: Strong evidence** (code-level inconsistency confirmed; exploit
input not constructed/tested by me).
**Recommendation:** fold both into one `QuantizeUtil.NormalizeAndQuantize(Quaternion)`
with the `TransformData` guard, used by both `TransformData.Quantized()` and
`DefinitionCanonicalizer`'s attachment path. This is CC-090 scope (it already
owns `QuantizeUtil`) and should be called out explicitly rather than left
implicit in "quantization... where contracts match" — the contracts here
*look* the same and aren't.

### M2. `IDnaSerializer` is a shallow-module interface: one implementation ever, bypassed by half its call sites

`Assets/Scripts/Runtime/Serialization/IDnaSerializer.cs` has exactly one
implementer, `sealed class JsonDnaSerializer` — sealed, so no second
implementation is even possible without unsealing it first. Of the four call
sites:

- `Assets/Scripts/Editor/CreatureEditorSession.cs:24` and
  `Assets/Scripts/Editor/CreatureEditorWindow.cs:181` each independently
  declare an identical `private static readonly IDnaSerializer Serializer = new JsonDnaSerializer();`
- `Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs:87` and
  `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs:193`
  both instantiate `new JsonDnaSerializer()` directly and call it through the
  concrete type, bypassing the interface entirely.

The interface currently buys nothing (no test doubles reference it either —
tests exercise `JsonDnaSerializer` directly per the test-file listing). It's
either speculative generality (drop the interface, reference the sealed class
everywhere) or a real seam that isn't wired up yet (if a second serializer —
e.g. a binary or v1-compat format — is actually planned, keep the interface
but delete the two duplicate `Serializer` field declarations in favor of a
single `JsonDnaSerializer.Default` static instance the other three sites
share).

**Confidence: Confirmed.**
**Recommendation:** small, mechanical — pick one of the two directions above.
Not currently the subject of any open CC-### ticket; low priority but cheap
to fix alongside CC-090 since it touches the same files.

## Low

### L1. `CanonicalJsonWriter`'s embedded "canonical field reference" doc comment is stale

The class-level XML doc on `Assets/Scripts/Runtime/Serialization/CanonicalJsonWriter.cs:18-85`
is explicitly framed as authoritative: *"the exact JSON field names and
nesting this class exists to fix in place."* The example `shape` object it
shows is the pre-CC-043 schema (`{ "type", "primarySize", "smoothBlendRadius" }`
only) — the actual `WriteShape` (`CanonicalJsonWriter.cs:366-379`) has written
`radius`, `capsuleAxis`, `capsuleHeight`, `ellipsoidRadii`, and
`boxHalfExtents` since CC-043 shipped. The example `part` object is also
missing `meshGeometry`, which `WritePart` has unconditionally emitted since
CC-031 (`CanonicalJsonWriter.cs:239`).

I hand-verified the *actual* reader/writer key sets still agree (no live
round-trip bug — this is documentation, not behavior), but this is the same
failure mode CC-042 already exists to fix in a different file
("Update ClonePartAsChild XML doc comment to list Limb as copied"). Same
issue class, different location — worth either folding into CC-042's scope or
filing as a same-shaped follow-up so a future reader doesn't trust the stale
example over the code.

**Confidence: Confirmed** (direct comparison of doc comment vs. `WriteShape`/`WritePart`).

---

## Architecture Improvements

None proposed beyond what's already tracked. The Definition → Canonicalizer →
Resolver/Snapshot → Generation/Appearance pipeline staging (CC-091), the
tolerant hierarchy index (TSK-0093), and the editor decomposition (TSK-0098)
already cover the structural work this audit would otherwise recommend. H1/H2
above are consolidation gaps *inside* that architecture, not architecture
gaps.

## Implementation Recommendations

- **Ownership:** all of H1, H2, and M1 fit inside CC-090's existing scope
  (`Assets/Scripts/Runtime/Common/GenerationTolerances.cs`,
  `Definition/CurveAdapter.cs`, `Definition/ThicknessProfile.cs`,
  `Skeleton/MirrorUtility.cs` are already in its `links:`). Recommend adding:
  `Editor/CreatureEditorWindow.cs` (H1 editor site),
  `Morphology/Sdf/SdfProgramBuilder.cs`, `Skeleton/SemanticBoneResolver.cs`,
  `Generation/CreatureMeshGenerator.cs` (H2 sites), and
  `Definition/TransformData.cs` + `Definition/QuantizeUtil.cs` (M1) to
  CC-090's `links:` so the next consolidation pass has the full site list up
  front instead of rediscovering it incrementally.
- **Sequencing:** H2 (mirror wiring) is the cheapest fix in this report —
  pure reference-swap, no behavior-risk, good first slice. H1 (fallback
  cascade) needs the same legacy-schema-parity decision the synthesis doc
  already flagged as blocking (F-02's residual risk) before it can be
  collapsed, since collapsing it now would force that decision implicitly.
  M1 (quaternion quantize) should get an explicit near-degenerate test added
  to `DefinitionValidatorTests.cs` or `CreatureEditorWindowPartTypeTests.cs`-adjacent
  suite *before* consolidating, so the fix is provably behavior-preserving for
  the `TransformData` path and provably safer for the `GeometryAttachment` path.
- **Migration/test strategy:** none of these require a schema version bump —
  all four sites currently agree on values, so consolidating is a refactor,
  not a behavior change, apart from M1 which should gain the missing
  degenerate-case coverage as part of the fix (not just a refactor).

## Existing Task Impact

| Task | Impact |
| --- | --- |
| CC-090 (Backlog, P2) | **Extend.** H1 adds 2 new sites (editor, deserializer) beyond the one in scope; H2 clarifies that `MirrorUtility` already exists and the remaining work is call-site wiring, not utility design; M1 adds a correctness dimension (degenerate-quaternion safety) that wasn't previously called out. Recommend adding the file list above to `links:`. |
| TSK-0094 (handoff-tracked, "Done" per 2026-09-01 summary table but item 3 in the same doc says "remains duplicated") | **Correction.** Current source shows the *finite-check* half of item 3 is in fact resolved (`DefinitionValidator.cs` uses `using static NumericValidity;`, `BodySurfaceProjector.cs` calls `NumericValidity.IsFinite` directly, `CurveAdapter`/`GradientAdapter` delegate correctly) — that part of the handoff's "remains duplicated" claim is stale. The *mirror* and *quantize* halves of item 3 are still open, per H2/M1 above. |
| CC-042 (Backlog, P3, doc-drift precedent) | **Related-but-separate.** L1 is the same failure mode in a different file; recommend either widening CC-042 or filing a same-shaped low-priority follow-up rather than duplicating the ticket. |
| CC-091 / TSK-0095, CC-093, CC-098 | No new findings; current source matches what the 2026-09-02 synthesis and 2026-09-01 handoff already describe. Not re-audited in depth here — see Assumptions. |

No new CC-### ticket is recommended. Every finding fits inside an existing
task's stated scope.

## Assumptions

- Audited the `main` branch tarball at fetch time (2026-09-04); did not
  independently confirm this matches the `dff4a69` fixed point referenced by
  the 2026-09-02 synthesis, so some drift between that doc's "current
  uncommitted working tree" and this snapshot is possible.
- Did not run the Unity test suite or `dotnet build` — all findings are static
  reading, consistent with this being a read-only architectural review per
  the project's own audit convention.
- Did not re-derive or re-verify the already-recorded findings in the 41
  prior `docs/audits/` documents (F-01 symmetry-subtree evaluation,
  the one-sample Body crash fix, etc.) — treated as accepted per the
  project's delta-audit convention, not independently re-confirmed.
- Did not do a line-by-line pass of `CreatureEditorWindow.cs` (3,165 lines) or
  the SDF/marching-cubes extraction pipeline beyond the mirror-matrix grep in
  H2 — those are already owned by TSK-0098 (decomposition) and the SDF work
  has its own dense audit history (CC-014, CC-063, CC-064). A full line pass
  of the editor window is the natural next slice if more depth is wanted.

## Open Questions

1. For H1: does the legacy `PrimarySize`/capsule-height fallback rule need to
   stay in sync forever, or is there a target date after which pre-CC-043
   files are no longer supported and the fallback (and its four copies) can
   be deleted outright rather than consolidated? This changes whether H1's
   fix is "consolidate" or "delete."
2. For M2: is a second `IDnaSerializer` implementation actually planned
   (binary format, v1-compat reader, etc.)? If not, recommend collapsing the
   interface rather than fixing its wiring.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| H1 — shape-fallback duplicated across 4-5 sites | Confirmed |
| H2 — mirror matrix duplicated, `MirrorUtility` underused | Confirmed |
| M1 — divergent quaternion quantize, missing guard | Strong evidence |
| M2 — `IDnaSerializer` shallow module | Confirmed |
| L1 — `CanonicalJsonWriter` doc-comment drift | Confirmed |
| TSK-0094 item 3 finite-check claim is stale | Confirmed |
