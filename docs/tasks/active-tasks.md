# Active Tasks

| Key | Title | Status | Priority |
| --- | --- | --- | --- |
| CC-001 | Configure creature creator test scene | Done | P2 |
| CC-002 | Enable editor and Play Mode creature previews | Done | P1 |
| CC-003 | Document Unity MCP workflow for BeastMaster | Done | P2 |
| CC-004 | Complete creature editor save and authoring controls | In Progress | P1 |
| CC-005 | Add preview material and automatic regeneration settings | Done | P1 |
| CC-006 | Define the Body spline and attachment tree model | In Progress | P1 |
| CC-007 | Support surface attachment for limbs | Done | P1 |
| CC-008 | Profile and optimize preview generation hotspots | In Progress | P1 |
| CC-009 | Implement morphology compiler and semantic attachment model | Backlog | P1 |
| CC-010 | Add semantic animation query and morphology-scaled motion model | Backlog | P1 |
| CC-011 | Implement locomotion controller, gait, terrain contact, and foot placement | Backlog | P1 |
| CC-012 | Add secondary motion and body stabilization architecture | Backlog | P2 |
| CC-013 | Add direct-body editing and stale preview protection in the editor | Backlog | P1 |
| CC-014 | Port SDF evaluation to a Burst-compatible execution program | In Progress | P1 |
| CC-015 | Spore-like body sample authoring and place-part snapping | In Progress | P1 |
| CC-016 | Body spline manipulation solver (local curve editing) | In Progress | P1 |
| CC-017 | In-viewport Body sample scale (radius) editing | In Progress | P1 |
| CC-018 | Limb parts as joint chains with between-joint metaballs | In Progress | P1 |
| CC-019 | Bidirectional Body length editing (head-end add/remove on drag) | Backlog | P1 |
| CC-020 | Collapsible parts tree and Body inspector sections | Done | P2 |
| CC-021 | Show editable control points for a selected part | Backlog | P2 |
| CC-022 | Shared BodyFrameResolver (parallel-transport body frames) | Done | P1 |
| CC-023 | Part and Eye part types with generic Part default | Done | P2 |
| CC-024 | Vertex-color lit shader for generated previews | Done | P2 |
| CC-025 | Body vertical-gradient appearance (top and bottom gradients) | Done | P1 |
| CC-026 | Body scale (radius) handles visible and usable at all times | Backlog | P2 |
| CC-027 | Body multi-select with proportional radius scale drag | Backlog | P2 |
| CC-028 | Per-part submaterial from a material palette | In Progress | P2 |
| CC-029 | Add Child as Duplicate (copy selected part's authoring properties) | Done | P1 |
| CC-030 | Reusable part prefab templates (semantic subtree instantiation) | Backlog | P2 |
| CC-031 | Composable geometry sources (multiple meshes per creature) | Done | P1 |
| CC-032 | Separate gameplay geometry from 3D-print export | Backlog | P2 |
| CC-033 | Register FastNoise2Bindings as a real git submodule | Done | P2 |
| CC-047 | Resolve FastNoise2Bindings compile failure (restore DllImport P/Invoke) | Done | P1 |
| CC-034 | Body appearance vertical blend remap as an AnimationCurve | Done | P2 |
| CC-035 | Parts list column layout (resizable splitter + height-constrained scroll) | Backlog | P2 |
| CC-036 | Anatomical limb parent validation (Hand under Arm, Foot under Leg) | Backlog | P2 |
| CC-037 | Limb color gradient along the chain (base to tip) | Backlog | P2 |
| CC-038 | Limb and Body edit modes offer both a screenspace drag and a translation gizmo | Backlog | P2 |
| CC-039 | Limb metaball smooth blend radius as an authored value | Backlog | P2 |
| CC-040 | Clear the limb chain when switching a part away from a limb type | Done | P2 |
| CC-041 | Rotated-transform parity test for mirrored limb chains (managed vs portable) | Done | P2 |
| CC-042 | Update ClonePartAsChild XML doc comment to list Limb as copied | Backlog | P3 |
| CC-043 | Per-shape parameters (capsule axis + radius/height, ellipsoid 3-axis lengths, box dimensions) | In Progress | P1 |
| CC-044 | Export the generated mesh as an asset | Backlog | P2 |
| CC-045 | Remove the legacy managed SDF from production generation | In Progress | P1 |
| CC-046 | Investigate recurring broken-ankle mesh artifacts | Backlog | P1 |
| CC-048 | Fix obsolete Keyframe.tangentMode warnings and DrawPartList GetLastRect error | Done | P2 |
| CC-049 | Remove limb geometry dependence on inert Shape blend state | Done | P1 |
| CC-050 | Validate the generated creature-space geometry envelope | Done | P1 |
| CC-051 | Consolidate semantic attachment and part-frame resolution | Done | P1 |
| CC-052 | Preserve mesh rest transforms and mirrored binding identity | In Progress | P1 |
| CC-053 | Complete multi-geometry editor selection and visibility | Backlog | P1 |
| CC-054 | Reject thickness-profile quantization time collisions | Backlog | P2 |
| CC-055 | Decide limb centerline and generation-aware sampling fidelity | Backlog | P2 |
| CC-056 | Establish the canonical resolved morphology layer (umbrella; split into 056A/B) | In Progress | P1 |
| CC-056A | Resolved Body/limb geometry (canonical derived morphology, part A) | Done | P1 |
| CC-056B | Semantic attachment resolution (canonical derived morphology, part B) | Done | P1 |
| CC-057 | Add a responsive interactive morphology preview proxy | Backlog | P1 |
| CC-058 | Route editor interaction ownership by semantic mode | Backlog | P2 |
| CC-059 | Define symmetry placement and center-merge semantics | Backlog | P2 |
| CC-060 | Move material ownership to geometry components | Backlog | P2 |
| CC-061 | Harden the final mesh pipeline independently of editor interaction | Backlog | P2 |
| CC-062 | Optimize Burst field sampling and final-generation evidence | In Progress | P1 |
| CC-063 | Restore Fast preview culling (naive AABB + interpolation guard) | Done | P1 |
| CC-064 | Fast-mode non-finite field contract (+inf = outside/culled) | Done | P1 |
| CC-065 | FastNoise2 binary / submodule repository review gate | Done | P1 |
| CC-066 | Add a display mode to show the skeleton | Done | P2 |
| CC-067 | Show the SDF bounds for primitive shapes in the editor | Backlog | P2 |
| CC-068 | Make the base limb point moveable (selection + move gizmo, no scale) | Backlog | P1 |
| CC-069 | Runtime bone rig and pose application (drive bone Transforms from a PosedSkeleton) | In Progress | P1 |
| CC-070 | Add body chain and body-root connections to inferred skeleton | Done | P1 |
| CC-071 | Fix mirrored limb bone rotation basis | Done | P1 |
| CC-072 | Shared generation configuration and mesh palette ownership | In Progress | P1 |
| CC-073 | Define and prototype animated geometry binding | Backlog | P1 |
| CC-074 | Default surface material from the palette; remove editor preview material picker | Done | P1 |
| CC-075 | Dispose TempJob samples array on the portable-sampling exception path | Done | P2 |
| CC-076 | Create one shared semantic bone resolver service | Done | P1 |
| CC-077 | Add a PartType.Tail editor authoring path for child parts | Backlog | P3 |
| CC-078 | Split the DuplicateBodySampleId validation code for duplicates vs out-of-order ids | Backlog | P3 |
| CC-079 | Add a minimum absolute Body-spacing / degenerate-length validation | Backlog | P3 |
| CC-080 | Resolve the dead ParentId-null guard in HasParentCycle | Backlog | P3 |
| CC-081 | One canonical end-to-end morphology verification run | Backlog | P2 |
| CC-082 | Fix the validator ToDictionary throw on duplicate part IDs | Done | P2 |
| CC-083 | Reject a non-Body part with no parent (MissingParent gap) | Done | P2 |
| CC-084 | Fix DisplayName round-trip mismatch after JSON round-trip | Done | P2 |
