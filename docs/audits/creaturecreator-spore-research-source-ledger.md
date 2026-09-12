# CreatureCreator — Spore Research Source Ledger

**Purpose:** give any agent a complete, verifiable index of the sources behind the
Spore research and the metaball/appearance pivot, so the work can be reviewed and
continued without re-deriving it.

**Created:** 2026-09-12
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Baseline commit:** `f7dd934`
**Related reports:**
- `docs/audits/creaturecreator-spore-metaball-pivot-and-appearance-synthesis-2026-09-12.md` (report `CC-SYNTH-SPORE-PIVOT-20260912-A7E4D1`)
- `docs/audits/creaturecreator-council-review-metaball-pivot-2026-09-12.md` (four-seat council, report `CC-COUNCIL-PIVOT-20260912-B3F1A9`)

---

## 1. Citation contract

Use this form for every claim. Prose attribution is not sufficient.

```text
{branch} {commit} {repo-relative path} :: {symbol}
  "{quote, at most 40 words}" ({date})
```

For external sources, record URL, access date, retrieval status, and a quote.

When a cited file is deleted by a pivot, the citation must move to the commit
that last contained it. Git preserves it; the working tree does not. Cite the
commit, not the path alone.

---

## 2. External sources

### 2.1 Retrieved and verified

#### X01 — Chris Hecker, "My Liner Notes for Spore"

- URL: `https://chrishecker.com/My_Liner_Notes_for_Spore`
- Accessed: 2026-09-12. Status: HTTP 200, full body retrieved.
- Last page edit: 2022-09-06.
- Verified: yes, primary source, author is Spore's creature systems engineer.

Extracted claims and quotes:

| Claim | Quote |
| --- | --- |
| Skin is a blobby implicit surface for topological robustness | "I chose a blobby implicit surface (sometimes called metaballs) to represent the skin." |
| Compact recipe is a reason for the representation | "we need to keep the 'recipe' for the creature very small so we can transmit it over the wire" |
| Field function | "We use a 4th order polynomial in the squared distance from the sample point to the center of the given metaball" |
| Smoother derivatives | "we square the main term again to get more continuous derivatives to avoid lighting discontinuities" |
| Spherical only | "We only use spherical metaballs, and we distribute them along the limbs and torso using a neat bit of math" |
| Ellipsoids rejected for editor speed | "Ellipsoidal metaballs would be cool and allow a wider range of shapes, but they seem like they'd be significantly slower to evaluate because they're orientation dependent" |
| Ear clipping under the MC patent | "the Marching Cubes patent was still in effect, so to work around the patent I tessellate the surface into triangles using an ear clipping algorithm" |
| MC never benchmarked after expiry | "patent expired before we shipped, but I never went back and tested whether using Marching Cubes would have been faster" |
| No metaball groups, so limbs merge | "We did not have time to implement metaball groups, which means the skin is one big implicit surface." |
| Webbing was accepted as a feature | "players have used this as a feature to create bat wings" |
| Anti-sliver technique | "the secret to high quality implicit surface tesselations" and "it's trivial to implement and generates perfectly uniform meshes" |
| **Bone weights from metaball provenance** | "We generate bone weights for the vertices based on which body parts generated which metaballs." |
| Known weight failures | "sometimes big spine segments don't generate smooth weights, and the torso on fat creatures can shear" |
| Torso-attached parts punted | "dealing with any torso-attached parts is harder, so we had to punt" |
| Rigblocks | "For the 'parts' of the creature, we use pre-authored Maya models called Rigblocks, with lots of parameterized deformation handles to change the shapes." |
| Charter algorithm | "starts on a random uncharted triangle, then floods outward as it finds neighboring triangles facing in approximately the same cardinal direction in 3D" |
| Charter projection | "it projects the group flat onto the plane of the current axis. This guarantees bounded distortion." |
| Charter packing | "it packs the charts into the texture atlas by sorting them by bounding box size" |
| Charter speed | "it does the job in 10 milliseconds on our minspec platform" |
| Charting quality trade-off | "wasted texels means less apparent texture resolution" and "lots of texture seams" |
| Paint channels | "we supported diffuse, specular exponent, gloss, emissive, and bump channels, with alpha masks for each" |
| Skirts disabled, dilation kept | "Henry ended up disabling the skirts for performance reasons in the final shipping version, and simply dilating the texture" |
| Particle paint in barycentric coordinates | "the particles were actually simulated in barycentric coordinates on the mesh itself" |
| Morphology-relative particle systems | "giving the particles limb-relative coordinate systems" |
| Animation resources | SIGGRAPH 2008 paper "Real-time Motion Retargeting to Highly Varied User-Created Morphologies"; GDC 2007 lecture "How To Animate a Character You've Never Seen Before" |
| Behavior trees | "a Halo-style Behavior Tree (BT) system" described as "version 1.5 of theirs" |
| Design intent disputed | Hecker states he never steered the game toward "a simplistic, shallow, or casual design" |

Remaining extraction targets:

- The exact equation image is not in the text. Fetch the image or the Triquet,
  Meseure, and Chaillou reference for the base 2nd-order form.
- Sections not yet mined: Behavior Tree docs page, the design section, and the
  linked Ocean Quigley and Andrew Willmott posts.

#### X02 — Hecker, "Real-time Motion Retargeting to Highly Varied User-Created Morphologies" (SIGGRAPH 2008)

- URL: `https://chrishecker.com/Real-time_Motion_Retargeting_to_Highly_Varied_User-Created_Morphologies`
- Accessed: 2026-09-12. Status: HTTP 200.
- Paper PDF: `https://chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf`
- Authors: Hecker, Raabe, Enslow, DeWeese, Maynard, van Prooijen.

Extracted claims:

- Animators authored motion in a custom OpenGL tool named Spasm.
- Motion is stored in a morphology-independent form that preserves structure and
  style.
- Runtime produces pose goals and supplies them to a constrained IK solver.
- It is a pose-generation architecture, not a biomechanics simulator.

Remaining extraction targets:

- The 11-page PDF is **not yet read**. It is the strongest available source for
  the pose-goal and IK contract. Read it before any semantic-animation work.
- The two submission videos (X02a gait and secondary-motion demos).

#### X03 — Ocean Quigley, "Spore's creature skin painting"

- URL: `https://oceanquigley.blogspot.com/2009/04/spores-creature-skin-painting.html`
- Accessed: 2026-09-12. Status: HTTP 200.

Extracted claims and quotes:

| Claim | Quote |
| --- | --- |
| Brush splat through 3D-to-texture position | "splatting the brushes directly onto the creature's skin by converting their 3D position into their position along the texture" |
| Speed | "we could apply hundreds of brushes per second using the graphics card" |
| Particle paint origin | Chris Hecker built the first prototype; Andrew Willmott supplied the splat approach; Swarm drove particles |
| Morphology-relative paint regions | "just on the spine, or halfway up the legs, for example" |
| Landmark-directed crawling | "crawl towards landmarks on the creature (towards the head or the tail, or towards the belly or along the arms for example)" |
| Auto-charted skin, poor quality | "the charting is done really poorly, but it's happening at interactive rates. We're trading off quality for speed." |
| Authored part textures composited | "We let an artist paint them and then composited those hand authored textures over the automatically painted skin." |

Remaining extraction targets:

- `http://oceanquigley.blogspot.com/2009/04/spore-early-rig-block-experiments.html`
  (the rigblock mockup post, linked by Hecker and by this post).
- The earlier "skinpaint" post linked from this one.

#### X04 — Strange Seed, Devlog 1: Procedural Madness

- URL: `https://telchior.itch.io/strangeseed/devlog/494498/devlog-1-procedural-madness`
- Accessed: 2026-09-12. Status: HTTP 200. Unity project.

Extracted claims and quotes:

- Uses fully modelled parts swapped between creatures:
  "making real, fully modeled 3d creatures and then letting you swap parts between them"
- CCD IK was attempted and abandoned:
  "the first couple attempts have the tiger's limbs flipping around crazily"
- Reason for abandonment:
  "the fastest, most efficient way forward is to stick with vanilla IK animation"
- Constraint cited: many different joint arrangements across evolved creatures.

**Interpretation caution:** this is evidence for a production compromise, not
evidence that modular parts avoid weighting problems. Verse it in any citation.

Remaining extraction targets: devlogs 2, 3, and 4 (IK, ground fitting, layered
materials, UVs, posture).

#### X05 — `daniellochner/creature` (GitHub)

- URL: `https://github.com/daniellochner/creature`
- Accessed: 2026-09-12. Status: HTTP 200.
- Content: `README.md` and `LICENSE.md` only. One contributor. Last commit about 4 years ago.
- License: GPL-3.0.
- README quote: "This repository will be used for the standalone Unity plugin. The full Steam game's source code has been released here: https://github.com/daniellochner/creature-creator-game"

Interpretation: **historical pointer only.** Do not cite as an implementation
reference. The claimed source repository is unavailable (see U01).

### 2.2 Unavailable — retry guidance

| ID | Source | Failure | Retry guidance |
| --- | --- | --- | --- |
| U01 | `https://github.com/daniellochner/creature-creator-game` | HTTP 404 | Search GitHub for forks, mirrors, or renamed repos. Check the Internet Archive and Software Heritage. Do not assume content. |
| U02 | `https://www.andrewwillmott.com/s2007` | HTTP 404 | Search for the SIGGRAPH 2007 technical sketches under other URLs, or via the ACM record. This is the primary rigblock and skin-paint sketch. |
| U03 | Moore and Warren, "Compact Isocontours from Sampled Data" / "Mesh Displacement" | citeseerx redirect then HTTP 401 | Try Software Heritage, the Graphics Gems III book, or a university mirror. Currently the mechanism description rests on X01 plus a prior repo audit. |
| U04 | Blender Skin Modifier manual | HTTP 403 | Retry with a different user agent, or cite the feature only as a design pointer. Not verified. |
| U05 | GDC 2007 slide deck `Gdc07-Animation.ppt` | Not read | Download from `https://chrishecker.com/images/5/58/Gdc07-Animation.ppt` and read before any semantic-animation claim. |
| U06 | Hecker SIGGRAPH 2008 paper PDF | Not read | Read X02's PDF. Highest-value unread source in this ledger. |

### 2.3 Unverified leads — questions to answer before citing

None of these were inspected. Treat as leads, not findings.

| ID | Lead | What to extract |
| --- | --- | --- |
| L01 | Critter Crosser / RujiK the Comatose devlog series | Whether a socket-based source-partidentity survives breeding into rendering, and whether it informs deformation. Episodes named in the prior research round: BEAST SOCKET, Real-Time Monster Evolution, Designer Monsters, Monster Breeding, Rendering Organic Monsters, Creature Combat, RNG monsters. |
| L02 | The Sapling (Wessel Stoop) | Procedural walking; per-region appearance; population-scale performance; how anatomy survives mutation. |
| L03 | Thrive (Revolutionary Games Studio) | Capability/verb system; the procedural-animation design discussion. |
| L04 | Elysian Eclipse | How a small team scopes a Spore-like editor; leg and morph devlogs. |
| L05 | The Big Forest (Rune Skovbo Johansen) | High-level versus low-level parameters; the finding that unconstrained random parameters do not produce plausible animals. |
| L06 | Species: Artificial Life, Real Evolution | Genotype to skeletal scaling. |
| L07 | Blender Skin Modifier | Skeleton-first authoring: per-vertex radii, edges as bones, weight propagation. Design pointer only until retrieved. |
| L08 | `keijiro/ComputeMarchingCubes` | GPU isosurface reference. Claimed "Unlicensed" and not verified. |

---

## 3. Internal repository sources

All citations pinned to `audit/skeleton-animation-improvements-2026-09-07 @ f7dd934`.

### 3.1 Prior audits and reports

| Key | Path | Role |
| --- | --- | --- |
| R01 | `docs/audits/creaturecreator-spore-metaball-pivot-and-appearance-synthesis-2026-09-12.md` | The pivot proposal under review |
| R02 | `docs/audits/creaturecreator-council-review-metaball-pivot-2026-09-12.md` | This review's council report |
| R03 | `docs/audits/2026-09-07-spore-spherical-metaballs-rigblocks-and-skin-paint-research.md` | Earlier Spore capture (`CCSPORE-20260907-4D2F91C8`) |
| R04 | `docs/audits/creaturecreator-spore-informed-deepdive-2026-09-07.md` | Earlier deep dive, parts 1 to 6 |
| R05 | `docs/audits/procedural-creature-spore-modern-design-and-implementation-guide.md` | The project's own architecture guide (v4) |
| R06 | `docs/audits/creaturecreator-audit-generation-performance-lifecycle-2026-09-12.md` | Findings GP-01 to GP-10 |
| R07 | `docs/audits/creaturecreator-audit-deformation-skinning-2026-09-12.md` | Findings SK-01 onward |
| R08 | `docs/audits/creaturecreator-council-review-next-phase-ordering-2026-09-11.md` | Preceding council; disputes suite state |
| R09 | `docs/audits/2026-09-04-sampling-perf-regression-potential-envelope-handoff.md` | The CC-099 envelope design |
| R10 | `docs/audits/creaturecreator-compact-mesh-audit-26-08-22-15-14-00.md` | Compact-isocontour research |
| R11 | `docs/audits/creaturecreator-audit-suite-2026-09-12.md` | Suite index for the 2026-09-12 batch |
| R12 | `docs/audits/sporelike-creature-model-and-editor-audit-26-08-22-15-34-00.md`, `sporelike-body-spline-manipulation-audit-26-08-22-15-34-00.md`, `sporelike-creaturecreator-continuation-audit-26-08-22-19-48-00.md` | Earliest Spore-informed audits |

### 3.2 Source files that a pivot must touch or audit

| Key | Path | Why it matters |
| --- | --- | --- |
| F01 | `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs` | `EvaluateInto` loop bounds; `InfluenceRadius`; root early exits |
| F02 | `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs` | Shape dispatch; potential-influence bounds; ellipsoid exception |
| F03 | `Assets/Scripts/Runtime/Morphology/Sdf/SmoothMinMath.cs` | The union operator being replaced |
| F04 | `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs` | Storage contract; gradient estimation; root pre-fill |
| F05 | `Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs` | `GridVertexOwnership` weld keys; winding; loop suppression |
| F06 | `Assets/Scripts/Runtime/Morphology/Extraction/CubeContourResolver.cs` | Edge interpolation `t = da/(da-db)` |
| F07 | `Assets/Scripts/Runtime/Morphology/Extraction/AsymptoticDecider.cs` | Face-only ambiguity handling |
| F08 | `Assets/Scripts/Runtime/Common/GenerationTolerances.cs` | `NormalizeSurfaceDensity`; `ScalarComparisonEpsilon`; quantisation |
| F09 | `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs` | Current weight authoring; fallback; the weight-`1.0` discontinuity |
| F10 | `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceInfluenceDomainResolver.cs` | Nearest-part SDF domain; mirror heuristic |
| F11 | `Assets/Scripts/Runtime/Animation/Binding/InfluenceWeightingPolicy.cs` | Shipped chain-aware gate; the type `TSK-0224` proposes to extend |
| F12 | `Assets/Scripts/Runtime/Animation/Binding/MorphologyInfluenceRadiusBridge.cs` | Midpoint radius proxy; `TSK-0201` |
| F13 | `Assets/Scripts/Runtime/Definition/ShapeDefinition.cs`, `ShapeType.cs` | Vocabulary to remove |
| F14 | `Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs`, `Serialization/CanonicalJsonWriter.cs`, `Serialization/JsonDnaSerializer.cs` | Migration and canonical round-trip |
| F15 | `Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs`, `TriplanarNoise.cs`, `PartAppearanceSampler.cs` | Appearance path to replace |
| F16 | `Assets/Shaders/VertexLit.shadergraph` | Triplanar and Position nodes |
| F17 | `Assets/Scripts/Tests/Runtime/GenerateDataBenchmark.cs` | The measurement fixture |
| F18 | `Assets/Creatures/*.json` | The migration corpus: 17 files, 16 capsule, 19 ellipsoid, 0 box |

### 3.3 Verification commands

```powershell
# Confirm the baseline and rule out stale fixed points
git -C d:\UnityProjects\CreatureCreator log --oneline -6
git -C d:\UnityProjects\CreatureCreator merge-base --is-ancestor <commit> HEAD

# Confirm the shape census
Select-String -Path Assets/Creatures/*.json -Pattern '"capsule"|"ellipsoid"|"box"' |
  Group-Object Filename | Select-Object Name, Count

# Confirm the old weighting measurement, then re-measure with Default vs Legacy
# (read-only; see TSK-0147 and TSK-0150)
```

---

## 4. Continuation instructions

Highest-value open work, in order.

1. **Read X02's paper PDF (U06) and the GDC deck (U05).** These are the strongest
   unread primary sources and they govern the semantic-animation layer.
2. **Retry U02 and U03.** The rigblock sketches and the compact-isocontour paper
   are the two primary sources the pivot cites but does not hold.
3. **Answer the council's top open question:** can per-primitive attribution be
   threaded through the existing SDF tree without changing the field
   representation? If yes, `TSK-0224` no longer depends on `TSK-0223`.
4. **Re-measure the elbow and foot leakage** with the shipped `TSK-0147` policy,
   `Default` versus `Legacy`. This can delete scope from `TSK-0224`.
5. **Mine X03's linked posts** for the earliest rigblock mockups and the
   parameterized deformation-handle workflow.
6. **Inspect L01 to L03** only if a concrete question needs them. Record them as
   leads until then.

Do not promote an unverified lead to a finding. Do not cite a source as evidence
without an access date and a quote.

---

## 5. Ledger maintenance

- Add a source when a claim depends on it, not when it is merely interesting.
- Update the access status when a previously unavailable source is retrieved.
- Move a verified lead from section 2.3 to section 2.1 with its quotes.
- Keep the internal file table current when a pivot deletes a file. Replace the
  path with the last commit that contained it.
