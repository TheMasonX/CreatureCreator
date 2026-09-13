# ProBuilder, Materials, and Volumes via MCP

Detail reference for `manage_probuilder`, `manage_material`, and
`manage_graphics` volume actions. This file is a snapshot, not a contract.

Observed 2026-09-12 to 2026-09-13 on MCP for Unity server 10.2.0, with
`com.unity.probuilder` 6.1.2 and URP 17.5.0.

## Arguments

`manage_probuilder` reads its arguments from the `properties` bag. Use `target`
and `search_method` to select the object:

```json
{ "action": "create_shape",
  "properties": { "shapeType": "Cylinder", "radius": 7, "height": 0.6,
                  "axisDivisions": 64, "name": "Stage Platform",
                  "position": [0, -0.3, 0] } }
```

## Shape parameters

`create_shape` accepts `shapeType` plus type-specific dimensions.

| `shapeType` | Parameters | Notes |
| --- | --- | --- |
| `Cube` | `width`, `height`, `depth` | A zero axis falls back to 1 |
| `Cylinder` | `radius`, `height`, `axisDivisions` (default 24), `heightCuts` | Use 32-64 divisions for a smooth large disc |
| `Plane` | `width`, `height`, `widthCuts`, `heightCuts`, `axis` (default 2 = Y-up) | One quad at zero cuts |
| `Torus` | `outerRadius` (ring), `innerRadius` (tube), `rows`, `columns`, `smooth` | The tool swaps these relative to ProBuilder's own naming, so the API names are the intuitive ones |
| `Sphere` | `radius`, `subdivisions` | `subdivisions` 2 gives 320 faces |
| `Cone` | `radius`, `height`, `segments` | |
| `Pipe` | `radius`, `height`, `thickness`, `segments`, `subdivHeight` | |
| `Arch` | `radius`, `angle`, `width`, `depth`, `radialCuts`, face toggles | |
| `Stair` | `width`, `height`, `depth`, `steps`, `buildSides` | |
| `CurvedStair` | `width`, `height`, `innerRadius`, `circumference`, `steps` | |
| `Door` | `width`, `height`, `depth`, `ledgeHeight`, `legWidth` | |
| `Prism` | `width`, `height`, `depth` | |

`name`, `position` (world), and `rotation` (euler degrees) apply to every shape.

An unrecognised shape name falls back to a generic generator at default size, so
a typo yields a wrong-sized object instead of an error. Check the returned
`faceCount` and `vertexCount`.

## Centre pivot

The generator uses `PivotLocation.Center`. A shape is centred on its `position`:

- a cylinder of height `h` at `y` spans `y-h/2 .. y+h/2`;
- a plane lies exactly at `position.y`.

To place a disc whose top surface must sit at `Y=0`, use `y = -h/2`. Verified:
a cylinder of radius 7 and height 0.6 at `y=-0.3` reported world bounds
`max.y = 0.000` and `max.x = 7.000`.

## Materials on shapes

A new shape has **no material**. A renderer with a null `sharedMaterial` draws
magenta.

```json
{ "action": "set_face_material", "target": "Stage Platform",
  "search_method": "by_name",
  "properties": { "materialPath": "Assets/Materials/StagePlatform.mat" } }
```

With no `faceIndices`, every face is assigned. The result reports
`facesModified`, which is a useful confirmation.

## Colliders

A new shape has **no collider**. Add one:

```json
{ "action": "add", "component_type": "MeshCollider",
  "target": "Stage Platform", "search_method": "by_name" }
```

The Unity `Cylinder` primitive ships with a `CapsuleCollider`, which is
degenerate for a flat disc. Replace it with a `MeshCollider` when collision
matters.

## Other actions

`get_mesh_info`, `convert_to_probuilder`, `subdivide`, `bevel_edges`,
`extrude_faces`, `extrude_edges`, `merge_objects`, `combine_meshes`,
`validate_mesh`, `repair_mesh`, `select_faces`, `set_face_color`,
`set_face_uvs`, `center_pivot`.

`get_mesh_info` fails on a non-ProBuilder object with
`GameObject 'X' does not have a ProBuilderMesh component`.

`combine_meshes` merges several ProBuilder objects into one. Use it to cut draw
calls when a set of static props no longer needs individual identity.

## Persistence check

ProBuilder meshes serialize into the scene. Count them statically to prove that
a save wrote them:

```powershell
(Select-String -Path 'Assets/Test.unity' -Pattern 'ProBuilderMesh' -AllMatches).Count
```

`git diff --check` reports trailing whitespace on the serializer's empty
`m_Data` lines for ProBuilder components. This is generated content. Do not
hand-edit it.

## Material creation

`manage_material(action="create")` takes `material_path`, `shader`, and `color`.
`shader="Universal Render Pipeline/Lit"` resolves correctly.
`set_material_color` and `set_material_shader_property` edit an existing
material.

## Emission

`manage_material` cannot enable a shader keyword. An emissive URP/Lit material
needs the `_EMISSION` keyword plus an HDR `_EmissionColor`, so set it through
`execute_code`:

```csharp
var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/X.mat");
Color b = mat.GetColor("_BaseColor");
mat.EnableKeyword("_EMISSION");
mat.SetColor("_EmissionColor", b * 1.8f);   // >1 so Bloom can pick it up
mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
UnityEditor.EditorUtility.SetDirty(mat);
UnityEditor.AssetDatabase.SaveAssets();
```

Gain guidance, learned by iterating on screenshots:

| Gain | Result |
| --- | --- |
| 4.0 or more on a close surface | Blows out to white and loses the hue |
| 1.4-2.4 | Keeps the colour and still reads as emissive |
| 0.55 alpha plus emission | A translucent glowing fluid; the contents stay visible |

## Alpha blending

URP/Lit transparency needs the full property bundle. An alpha colour alone does
nothing.

```csharp
m.SetColor("_BaseColor", new Color(r, g, b, 0.25f));
m.SetFloat("_Surface", 1f);        // 1 = Transparent
m.SetFloat("_Blend", 0f);          // 0 = Alpha
m.SetFloat("_SrcBlend", 5f);       // SrcAlpha
m.SetFloat("_DstBlend", 10f);      // OneMinusSrcAlpha
m.SetFloat("_ZWrite", 0f);
m.SetFloat("_AlphaClip", 0f);
m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
m.DisableKeyword("_ALPHATEST_ON");
m.SetOverrideTag("RenderType", "Transparent");
m.renderQueue = 3000;
m.SetShaderPassEnabled("ShadowCaster", false);
```

Observed: `_SrcBlend` reads back as `1` (One) after the write, so the effective
blend is `One / OneMinusSrcAlpha`. The result renders correctly, but the stored
value differs from what the URP inspector writes. Verify visually instead of
trusting the readback.

Two consequences: a closed transparent shell hides nothing, so an inner object
becomes visible and reads as tank contents; and transparency adds sorting work
plus a transparent queue entry. Use it only where it earns its cost.

## Post-processing volumes

```json
{ "action": "volume_create_profile", "path": "Assets/Settings/X.asset" }
{ "action": "volume_create", "name": "X", "is_global": true,
  "profile_path": "Assets/Settings/X.asset", "priority": 0, "weight": 1 }
{ "action": "volume_add_effect", "target": "X", "effect": "Bloom",
  "properties": { "intensity": 0.7, "threshold": 1 } }
```

`volume_set_effect` requires a `parameters` dict. Passing `properties` fails with
`'parameters' dict is required.`

A Bloom threshold of 1.0 blooms only HDR values, which isolates emissive
surfaces from the rest of the scene. Bloom is global, so it also affects every
other object in the scene.

## Lights

There is no dedicated component tool for creating a light, so use
`execute_code`:

```csharp
var go = new GameObject("Ceiling Light 1");
go.transform.SetParent(ring.transform, false);
go.transform.localPosition = new Vector3(9f, -0.6f, 0f);
var l = go.AddComponent<Light>();
l.type = LightType.Point;
l.range = 46f;
l.intensity = 8f;
l.color = new Color(1f, 0.95f, 0.85f);
l.shadows = LightShadows.None;
```

A point light with shadows costs a cube shadow map. Four shadowed point lights
is expensive for fill lighting; prefer shadowless fill lights and let the
directional light carry the shadows.

Align fill lights with the fixture they represent. Four lights at the ring
radius read as the ring emitting; the same four inside the ring do not.

## Verification snippets

```csharp
// Bounds readback: confirm a placement assumption before building on it.
var go = GameObject.Find("X");
var b = go.GetComponent<Renderer>().bounds;   // b.min / b.max = world extents

// Intersection scan: find a prop that overlaps another.
bool hits = boundsA.Intersects(boundsB);

// Null-material scan: count the magenta objects after a bulk create.
int bad = 0;
foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
    if (r.sharedMaterial == null) bad++;

// Real surface of a generated creature: read the mesh or the collider.
var mesh = go.GetComponent<MeshFilter>().sharedMesh.bounds;   // authoritative
var col = go.GetComponent<MeshCollider>().bounds;            // matches the mesh
// SkinnedMeshRenderer.bounds reported bind-pose extents, not the posed surface.
```
