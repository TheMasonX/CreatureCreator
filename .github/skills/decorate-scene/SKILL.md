---
name: decorate-scene
description: |
  Build detailed Unity scene models and decorate a scene: ProBuilder
  geometry assembled from many submodels, procedural textures, shared URP
  materials with emissive screens and lamps, reusable prefab assets, and
  validated placement. Use when adding or upgrading props, furniture,
  consoles, machinery, vats, racks, or set dressing; when converting loose
  scene objects into prefabs; when a scene needs more visual detail; or when
  authoring emissive panels, screens, buttons, and indicator lights. Owns
  modelling, materials, textures, prefab authoring, placement, and visual
  validation of scene content.
argument-hint: 'Scene, area or prop list, detail budget, and validation need'
---

# Scene Decoration and Prop Modelling

## Outcome

Add or upgrade scene content as reusable prefabs: ProBuilder geometry built from
many named submodels, a small shared material set, procedural textures, emissive
screens and lamps, and placements that are verified against the geometry already
in the scene.

Decoration changes presentation only. It never changes authoritative creature
DNA, generation, or runtime code.

## When to Use

Use this skill when the user asks to:

- build or enlarge a prop, console, machine, vat, rack, bench, or terminal;
- decorate or dress a scene, or make it more detailed;
- turn loose scene objects into prefabs;
- add buttons, keyboards, screens, panels, gauges, or indicator lights;
- add emissive surfaces that glow under the scene's post-processing;
- place repeated props around a room.

Use [unity-mcp-operator](../unity-mcp-operator/SKILL.md) for MCP call mechanics:
readiness, tool-group activation, batching, payload limits, console reads,
screenshot framing, and error recovery. This skill assumes those mechanics.

## Repository Contracts

Read before modelling:

- `.github/skills/unity-mcp-operator/SKILL.md` and its
  `references/probuilder-and-materials.md` for verified MCP shape parameters.
- `references/model-recipes.md` in this skill for the verified C# recipes.
- `.github/skills/creature-workflow/SKILL.md` and
  `.github/skills/task-tracker/SKILL.md` for tracking the work.
- `.github/skills/unity-validation/SKILL.md` before claiming Unity behavior.
- `Assets/Scripts/README.md` for scene anchors and documented simplifications.

Invariants:

- `CreatureDefinition` is authoritative. Props are derived presentation and
  never become a DNA mutation or derivation path.
- Do not move documented anchors. The `Assets/Test.unity` stage top stays at
  exactly `Y=0`, and the lab floor stays below it.
- Runtime code under `Assets/Scripts/Runtime` never references a prop.
- Prefer extending the existing material and prefab sets over adding parallel
  ones.
- Reuse an existing material, texture, or prefab before creating a new one.

## Procedure

### 1. Inventory the scene before modelling

Never model against assumed geometry. Enumerate the live scene first.

1. Confirm readiness and read the console
   ([unity-mcp-operator](../unity-mcp-operator/SKILL.md) readiness gate).
2. Enumerate root objects with a compact `execute_code` pass: name, position,
   child count, and `Renderer.bounds` size. Follow to children only where the
   task needs it.
3. Inventory `Assets/Materials/` and `Assets/Textures/` and record each
   material's shader, base color, surface type, and emission state. Reuse these
   names in the new work.
4. Record the collision-relevant extents: walls, ceiling, floor, benches,
   vats, pillars, and existing props. These become the placement constraints.
5. Identify which existing objects the new work replaces, and say so in the
   task Scope before deleting anything.

### 2. Design the model as a part table

Before generating anything, write the model as a list of parts: name, local
position, size, and material. This table is the review artifact and the build
script.

Conventions that keep placement predictable:

- Put the root at the floor with an identity transform, so the prop's local
  origin is its base center. Every instance then drops onto a surface by
  setting `y` alone.
- Treat local `-Z` as the front the operator or viewer faces.
- Group parts under named sub-roots (`Desk`, `Monitor`, `Keyboard`,
  `Panel Bank`, `Blades`, `Top Status`) so the hierarchy stays navigable.
- Size from a real reference. A working bench is about `1.4` tall, a desk top
  about `1.4`, a human-facing console about `1.1` deep per operator.
- Budget parts per prop, then multiply by instance count. Hundreds of small
  submodels are cheap when they share a small material set (see step 4), but
  an ungrouped thousand-object hierarchy is not navigable.

### 3. Build the prefab with ProBuilder

Build the parts with `ShapeGenerator` inside one `execute_code` call, then save
the hierarchy as a prefab asset. The recipes are in
`references/model-recipes.md`.

1. Create a helper set: `Mat` (cached material lookup), `Group`, `Box`, and
   `Cyl`.
2. Build every part under the root with `PivotLocation.Center`.
3. Assign each part's material with `SetMaterial(faces, material)`, then call
   `ToMesh()` and `Refresh()`.
4. Add one `BoxCollider` on the root, sized from the combined child
   `Renderer` bounds.
5. Save with `PrefabUtility.SaveAsPrefabAsset(root, path)`, then destroy the
   scene instance. Save under `Assets/Prefabs/<Area>/<Prop Name>.prefab`.
6. Prove the round-trip on the first prop before building the rest: instantiate
   it and assert zero null materials, zero null meshes, and the expected
   bounds. This is the falsifiable check for the whole approach.

Edits to the prefab propagate to every instance, so fix the prefab rather than
patching instances.

### 4. Materials, textures, and emission

Keep the material set small and shared. Under URP the SRP Batcher batches by
shader variant, so hundreds of renderers over about a dozen materials stay
cheap; hundreds of one-off materials do not.

- Create no more than you need: one material per distinct surface role
  (structured panel, rack metal, bezel, vent, desk top, screen, button color,
  LED color, light strip).
- Generate textures procedurally only where they carry meaning at the distance
  the prop is viewed: screen content, perforated rack panels, louvred vents,
  panel seams and rivets. Do not texture small submodels that read as plain
  metal.
- Name new assets after the area, matching the existing lab prefix
  (`LabPanelLight`, `LabScreenBlue`, `LabVent`).
- For screens, set the texture as both `_BaseMap` and `_EmissionMap` so only
  the lit pixels glow instead of the whole panel.
- Emission needs the `_EMISSION` keyword, an HDR `_EmissionColor`, and a Bloom
  volume in the scene. `manage_material` cannot enable the keyword; set it in
  `execute_code`.
- Prefer emissive material lamps over real lights. A point light with shadows
  costs a cube shadow map; use shadowless fill lights only when the fixture
  must actually illuminate, and align them with the fixture they represent.
- Transparency needs the full URP/Lit alpha bundle, not just an alpha color.

### 5. Place instances

1. Instantiate with `PrefabUtility.InstantiatePrefab` to keep the prefab link.
2. Group instances under one root object so the hierarchy stays readable.
3. Set each instance's `y` to the surface it stands on, and aim the front with
   a single yaw.
4. Derive the yaw from the requirement, not by eye. Local `+Z` maps to world
   `+X` at yaw `90`, so yaw `90` faces the prop's front toward `-X`; verify by
   reading an instance's world bounds after placement.
5. Mount wall props flush and leave a small clearance. A flush prop that
   penetrates a wall panel by `0.2` reads as a modelling error in the scan.

### 6. Validate

Run these in order and record the results:

1. **Console** — read errors and warnings after the build and after the save.
2. **Null-material scan** — count renderers with a null `sharedMaterial` across
   the placed instances. A non-zero count is magenta geometry.
3. **Bounds and intersection scan** — compute each instance's combined
   `Renderer` bounds and test them against the recorded extents from step 1.
   Report the overlap size on each axis, and treat more than a few centimeters
   as a fault. This catches props that clip walls, pillars, or other props.
4. **Persistence check** — count the serialized components in the scene file
   (for example `ProBuilderMesh` occurrences in `Assets/Test.unity`) to prove
   the save wrote them.
5. **Screenshots** — capture each prop and one wide room view. Use
   `view_position` plus `view_rotation`. Confirm the axis before reading
   placement from an image, because the scene-view aspect is portrait and a
   yaw of `180` mirrors the screen-space X axis.

Never claim a visual result from bounds alone, and never claim bounds from a
screenshot. Use both.

### 7. Record and hand off

- Keep one MemorySmith task for the work. Capture direct user requirements
  verbatim under `## User Mandate` and mark them STRICT.
- Add implementation evidence: prop list, part counts, prefab paths, material
  and texture paths, placement coordinates, and scan results.
- Attach the screenshots and name the files.
- Record residual risk: untextured surfaces, unlit corners, props left
  undetailed, and any object that should become a prefab but has not.
- Write the follow-up as a named MemorySmith task, not as a note in the scene.

## Completion Checklist

- [ ] Scene and material inventories were read before modelling, not assumed.
- [ ] Each new prop is a prefab asset under `Assets/Prefabs/<Area>/`.
- [ ] Each prop is built from named submodels grouped under readable sub-roots.
- [ ] The material set is shared, area-prefixed, and no larger than the surface
      roles require.
- [ ] Screens use an emission map, not a uniformly glowing panel.
- [ ] Emissive surfaces have a Bloom volume in the scene.
- [ ] Null-material scan returns zero.
- [ ] Bounds and intersection scan returns no overlap above a few centimeters,
      and the overlap size is recorded for each near miss.
- [ ] Placement keeps every documented anchor where it was.
- [ ] Unity console has no new errors or warnings.
- [ ] Scene is saved and prop persistence is proven from the scene file.
- [ ] Screenshots exist for each prop and for one wide view.
- [ ] The MemorySmith task holds the evidence, residual risk, and a named
      next step.

## Example Prompts

- `/decorate-scene add detailed ProBuilder terminals and rack computers to the Test-scene lab and prefab them`
- `/decorate-scene turn the loose lab props into prefabs without moving them`
- `/decorate-scene add emissive control panels and indicator lamps to the vats`
- `/decorate-scene build a glass display case prefab and place two in the lab`
- `/decorate-scene raise the detail on the retro consoles and validate placement`
