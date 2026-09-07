# CreatureCreator — Next Slice Code Audit

**Audit ID:** `CC-AUDIT-20260906-C7E43B19`

**Audited HEAD:** `d3c7f12e9059b1fcec1be025785af945aa715746`

**Date:** 2026-09-06

**Scope:** Additive follow-up to the whole-codebase audit and second-order audit. This slice deliberately avoids re-reporting F-201..F-230 unless the same area exposes a materially different contract failure. Focus areas: authored-definition mutation boundaries, serialization/parser exactness, migration behavior, domain container APIs, adapter seams, and test-contract gaps.

## Executive summary

This slice found a recurring architectural pattern: the project has deliberately documented mutation and serialization boundaries, but several of those boundaries are still enforced primarily by caller convention. That is manageable while the editor is the only authoring client; it becomes brittle once more tools, runtime authoring, procedural generation, or alternate front-ends consume the model.

The most important findings are:

1. `CreatureDefinition` advertises a single mutation path while exposing the authoritative state as public mutable fields and collections. The boundary is therefore social, not structural.
2. `BodySpline.Clone()` is less null-tolerant than `CreatureDefinition.Clone()`, creating inconsistent behavior for malformed/intermediate authoring state.
3. `MiniJsonReader` accepts JSON constructs outside its own documented subset, including unescaped control characters, and silently overwrites duplicate object keys. The latter can turn corrupted DNA into a different valid definition without an error.
4. Legacy `verticalOffset` migration silently clamps out-of-range legacy data before validation, which can convert invalid historical data instead of reporting it.
5. Several domain containers (`Skeleton`, `LimbChain`, `BodySpline`, palettes, etc.) expose mutable `List<T>` instances directly. Some of this is required for Unity authoring, but the project does not consistently distinguish mutable authoring DTOs from protected runtime artifacts.
6. `CurveAdapter` documents a pure/domain-friendly abstraction, yet its public API remains tightly coupled to `UnityEngine.AnimationCurve`; the stated future Burst/pure-math seam is therefore only partial.
7. Current serialization tests prove happy-path determinism but do not pin down parser rejection policy for duplicate keys, invalid control characters, permissive numeric syntax, unknown fields, or lossy migration.

The recommendation is not to introduce a large framework. The codebase would benefit most from a small set of explicit policies: **mutable authoring model vs frozen runtime model, strict JSON grammar vs semantic validation, and migration that either preserves data exactly or rejects it.**

---

## Findings

### F-301 — P1 — `CreatureDefinition`'s "single mutation path" is not a real invariant

**Evidence**

`CreatureDefinition` exposes `SchemaVersion`, `SymmetryMode`, `Bounds`, `Generation`, `Body`, `Forward`, and `Parts` as public mutable fields. `BodySpline` in turn exposes `Samples`, and nested authored objects expose similarly mutable collections. The class comments say callers should use the editor's `MutateDefinition` as the single mutation boundary, while the editor documentation explicitly claims that every field edit funnels through that method. fileciteturn147file0 fileciteturn162file0

`CreatureEditorWindow` itself still reads the live public collections directly, e.g. `_definition.Parts.Where(...)`, demonstrating that the abstraction boundary is based on convention rather than encapsulation. fileciteturn161file0

**Why this matters**

Any future caller can mutate the authoritative definition without cloning, undo capture, canonicalization, validation, dirty-state tracking, or regeneration scheduling. The failure mode is particularly nasty because the object can remain apparently usable while invalidating assumptions made by hierarchy indexes, snapshots, hashes, or preview scheduling.

This is not an argument for making the authoring DTO immutable. It is an argument for explicitly separating **authoring data mutation APIs** from the **committed-definition boundary**.

**Recommended direction**

Keep `CreatureDefinition` as the Unity-friendly mutable authoring representation for now. Introduce one small service/boundary type, e.g. `CreatureDefinitionEditorState` / `DefinitionMutationService`, whose contract is: clone → mutate → canonicalize → validate → replace. Then treat direct mutation of `CreatureDefinition` as an intentionally low-level operation used only inside authoring infrastructure.

Add a static analyzer/convention test that searches editor code for direct assignments to `_definition`/nested collections outside the mutation service. This is a better near-term control than a large redesign.

**Task linkage:** extend `TSK-0098` and the existing editor-mutation work; this is the next logical hardening step after the current decomposition wave.

---

### F-302 — P2 — `BodySpline.Clone()` has inconsistent null-state behavior

`CreatureDefinition.Clone()` explicitly tolerates `Body == null` and `Parts == null`. By contrast, `BodySpline.Clone()` assumes `Samples` is non-null and immediately iterates it. fileciteturn147file0 fileciteturn149file0

This means the top-level clone contract is internally inconsistent: some malformed/intermediate authoring states can be cloned safely while `Body.Samples == null` fails during cloning.

This is relevant because the editor and scheduler intentionally use cloning at mutation/async boundaries. A malformed imported document, an older migration artifact, or a test fixture can therefore fail in clone code before the normal validator gets the opportunity to report the actual semantic problem.

**Recommended direction**

Decide one policy for authoring-model clone methods:

- either clone preserves null exactly;
- or clone normalizes missing collections to empty collections.

Do not mix the two. Given the current validator architecture, preserving null and letting validation explain it is the least surprising option. Add a reusable `CloneList` helper if several model types need the same semantics.

**Task linkage:** `TSK-0094` is the natural home for the mechanical utility extraction.

---

### F-303 — P1 — duplicate JSON object keys are silently accepted with last-write-wins semantics

`MiniJsonReader.ParseObject()` stores each parsed member with `result[key] = value`. A second occurrence of the same key silently replaces the first. fileciteturn157file0

For example, a document containing two `radius` members is accepted and only the later value survives before semantic validation.

**Why this matters**

For a DNA format intended to be deterministic and authorial, duplicate fields are ambiguous source data. Silent last-write-wins behavior makes corruption, merge errors, hand-edited mistakes, and generated-document bugs harder to detect. Worse, the resulting canonical save can make the document appear clean while changing its effective meaning.

**Recommended direction**

Reject duplicate object member names in `MiniJsonReader` with `DnaDeserializationException`. This is a parser-level structural rule, not a validator rule.

Add a focused test with duplicate `schemaVersion`, duplicate nested fields, and duplicate fields that differ only by later occurrence.

**Task linkage:** serialization contract work around `TSK-0036` / existing serialization tasks; this belongs in `MiniJsonReader`, not `DefinitionValidator`.

---

### F-304 — P2 — `MiniJsonReader` accepts invalid JSON control characters inside strings

`ParseString()` appends any character that is neither `"` nor `\\`, without rejecting characters below U+0020 that JSON requires to be escaped. fileciteturn157file0

The project documentation describes `MiniJsonReader` as covering the JSON subset emitted by `CanonicalJsonWriter`, so this permissiveness is currently outside the intended grammar even if canonical writer output never exercises it.

**Why this matters**

This creates two different notions of “valid JSON” inside the same serialization layer. A file may be accepted by the loader but rejected by another standards-compliant tool, and save/load/save can silently normalize the representation.

**Recommended direction**

Reject raw control characters `< 0x20` in `ParseString()`. Add tests for raw newline, tab, NUL, and escaped equivalents.

---

### F-305 — P2 — number grammar is intentionally narrower in docs but looser in implementation

`ParseNumber()` consumes any sequence of digits, decimal points, `e/E`, plus, and minus characters, then delegates to `double.TryParse`. This means it is not implementing JSON number grammar; it is implementing a permissive numeric tokenizer. fileciteturn157file0

The current parser can therefore accept forms that are outside the JSON number grammar if `double.TryParse` accepts them, while rejecting/handling some forms based on runtime parsing rather than the format definition.

**Recommended direction**

Implement the actual JSON number grammar in the tokenizer (`-? int frac? exp?`) rather than scanning a broad character class and asking `double.TryParse` to decide. Keep invariant-culture conversion after syntactic validation.

This is small code and gives deterministic language behavior across runtime/platform versions.

---

### F-306 — P1 — legacy `verticalOffset` migration can silently change invalid authored data

`JsonDnaSerializer` recognizes legacy `verticalOffset` and passes it to `CurveAdapter.FromLegacyOffset()`. That method explicitly clamps the offset into `[-1, 1]`. fileciteturn151file0 fileciteturn159file0

The migration documentation describes this path as an exact preservation of legacy behavior, but an input such as `verticalOffset = 2.0` is changed to `1.0` before the resulting curve reaches normal validation.

**Why this matters**

Migration is one of the places where preserving authored intent matters most. Silent repair is acceptable only when it is explicitly part of the migration contract. Otherwise the system should reject the file and tell the user what needs attention.

**Recommended direction**

Choose and document one of these policies:

- strict migration: reject offsets outside `[-1,1]`;
- repair migration: clamp, but emit a structured migration warning and expose that the loaded definition differs from source;
- versioned migration: preserve legacy values in an intermediate type, validate, then convert.

Given the rest of the codebase's validator philosophy, strict migration is the cleanest choice.

Add tests for `-1`, `0`, `1`, `-1.001`, `1.001`, `NaN`, and infinity.

**Task linkage:** extend the existing CC-034/`TSK-0036` migration work rather than opening a new subsystem.

---

### F-307 — P2 — unknown JSON fields have an undocumented compatibility policy

`JsonDnaSerializer` generally reads known members through `RequireField`/optional accessors and does not reject unrecognized object members. The parser therefore accepts both additive unknown fields and accidental misspellings without distinction. fileciteturn151file0 fileciteturn152file0

This is actually a useful capability for forward compatibility, and the project already uses additive optional fields without schema bumps. The problem is that the compatibility policy is implicit rather than explicit.

**Risk**

A typo such as `capusleHeight` can silently disappear on load, producing a valid but unintended creature. Conversely, future writers may add fields that older readers silently discard.

**Recommended direction**

Document the contract explicitly as one of:

- **open content:** unknown fields are ignored by design;
- **closed content:** unknown fields are rejected;
- **open-with-diagnostics:** unknown fields are ignored but reported.

For this project, open-with-diagnostics is the strongest long-term choice, but open content is acceptable if the team wants zero parser diagnostics. The important issue is to stop treating this as accidental behavior.

Add one test that locks the chosen policy.

---

### F-308 — P2 — authoring containers and runtime containers are not consistently differentiated

Several core objects expose mutable lists directly. Examples include `Skeleton.Bones`, `LimbChain.Joints`, `BodySpline.Samples`, and palette `Entries`. The repository's recent runtime work increasingly treats snapshots and result objects as immutable artifacts, but authored-domain containers still expose direct list mutation. fileciteturn143file0 fileciteturn148file0 fileciteturn149file0 fileciteturn143file5 fileciteturn143file6

This is not uniformly a bug: Unity serialization/editor authoring needs mutable containers. The architectural smell is that the API surface does not clearly identify which types are intentionally mutable authoring DTOs and which are supposed to behave as stable runtime values.

**Recommended direction**

Establish a naming/documentation convention instead of immediately wrapping every list:

- `Definition/*`: mutable authoring model;
- `Resolved*/Snapshot/Result`: frozen/read-only semantic data;
- `*Resources` or Unity adapters: explicit native-resource ownership.

Then enforce that runtime-side types never return a raw mutable `List<T>` and that authoring-side mutability is explicitly documented.

This directly complements F-230 rather than creating a competing abstraction.

---

### F-309 — P2 — `CurveAdapter` is only partially a real portability seam

The adapter is intentionally described as the single seam where a future pure-math/Burst consumer could replace `UnityEngine.AnimationCurve` evaluation. However, the public representation still uses `AnimationCurve`, and operations such as `Clone`, `ContentEquals`, `HasValidKeys`, and `Quantize` all operate directly on Unity objects. fileciteturn159file0

That means the current seam isolates *call sites*, but does not isolate *data representation*. A future Burst/runtime implementation would still need to convert from Unity state at the boundary.

**Recommended direction**

Do not over-engineer this now. The clean next step is a tiny portable record such as `CurveKey { T, Value, InTangent, OutTangent }` plus one adapter at the editor/Unity edge. Then the runtime sampler and serializer can consume the portable form directly.

This is especially worthwhile because the repository has already chosen this pattern for `ThicknessProfile`: the domain object stores portable key data and the editor adapter maps it to `AnimationCurve`. The same architecture can be reused rather than invented again. fileciteturn158file3 fileciteturn158file12

**Task linkage:** existing shared-utility direction under `TSK-0094`; avoid creating a parallel adapter framework.

---

### F-310 — P2 — serialization tests cover canonical stability but not parser-language boundaries

`JsonDnaSerializerTests` currently verify round-trip reconstruction, repeated serialization, insertion-order stability, save-load-save byte stability, malformed JSON, required fields, v1 rejection, and explicit shape migration. fileciteturn155file0

Those tests establish a good foundation, but none pin down the parser-level behaviors discovered here:

- duplicate object members;
- raw control characters inside strings;
- numeric grammar edge cases;
- unknown-field policy;
- out-of-range legacy `verticalOffset` migration;
- duplicate nested keys;
- malformed-but-parseable numeric forms.

**Why this matters**

For a hand-rolled parser, the test suite is the executable grammar. Without these cases, future parser changes can accidentally widen the accepted language or alter compatibility policy without anyone noticing.

**Recommended direction**

Add a `MiniJsonReaderTests` fixture for pure grammar rules and keep `JsonDnaSerializerTests` focused on DNA semantics/migration. This is a particularly good use of a small shared test utility instead of stuffing every parser case into the high-level serializer tests.

---

## Secondary observations

### A. `BodySpline.Clone()` and null normalization should be reconciled with hierarchy/index code

The hierarchy code has already been hardened to detach/copy collections, which is good. The remaining opportunity is to define whether `null` collections are a legal intermediate authoring state. Right now different APIs treat them differently. Make this a documented model invariant and remove defensive ambiguity where possible.

### B. `RequireUInt()` / numeric conversions deserve a dedicated conversion helper

The serializer repeats several `double -> int/float/uint` conversions inline. The code is correct for many ordinary values, but a shared conversion layer would make overflow, integral-only, finite-only, and range semantics explicit and testable. This matches the project's preference for extracting repeated utility policy into shared code.

### C. Do not turn these findings into another large abstraction wave

The codebase is already in the middle of a productive migration toward resolved snapshots and explicit resource boundaries. The best move is to consolidate contracts at the seams that are already present, not add another service layer for every domain type.

---

## Priority order

**P1 first:** F-301 mutation-boundary hardening, F-303 duplicate-key rejection, F-306 strict migration semantics.

**P2 next:** F-302 clone/null policy, F-304 control-character rejection, F-305 number grammar, F-307 unknown-field policy, F-308 authoring-vs-runtime container policy, F-309 portable curve representation, F-310 parser test coverage.

The highest-value implementation sequence is: establish the mutation/authoring contract → harden parser grammar and duplicate detection → make migration non-lossy → split parser grammar tests from DNA semantic tests → apply the same explicit mutable/frozen policy to remaining containers.

---

## Recommended task consolidation

| Existing task / area | Extend with |
|---|---|
| `TSK-0098` editor decomposition | F-301 mutation-boundary enforcement + direct-mutation audit |
| `TSK-0094` shared runtime utilities | F-302/F-308/F-309/F-310 shared clone/conversion/portable-key utilities and test helpers |
| CC-034 / `TSK-0036` vertical curve migration | F-306 strict/non-lossy legacy migration contract |
| Serialization contract tasks | F-303/F-304/F-305/F-307 parser grammar and unknown-field policy |
| F-230 immutability/resource policy | F-308 explicit mutable-authoring vs frozen-runtime classification |

## Net assessment

The repository is continuing to improve structurally. The new issues are mostly **contract-strengthening problems rather than architectural failures**. The important distinction is that the project is now sophisticated enough that implicit conventions are becoming the main source of remaining brittleness. Converting a small number of those conventions into executable/type-level policies should provide more value than another round of broad decomposition.

**Suggested next audit slice:** concurrency/cancellation/resource lifetime under async generation, followed by the mesh/SDF numerical boundary and benchmark methodology. Those areas remain comparatively orthogonal to this slice and should expose different failure modes.
