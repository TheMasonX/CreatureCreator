# Model and Decoration Recipes

Verified 2026-09-13 on MCP for Unity server 10.2.0, `com.unity.probuilder` 6.1.2,
URP 17.5.0, in `Assets/Test.unity`.

These are working recipes, not a contract. Re-read the live API with
`unity_reflect` when a signature matters.

## Verified API surface

| Member | Signature | Note |
| --- | --- | --- |
| `ShapeGenerator.GenerateCube` | `static ProBuilderMesh GenerateCube(PivotLocation pivotType, Vector3 size)` | 24 verts, 12 tris, 6 faces |
| `ShapeGenerator.GenerateCylinder` | `static ProBuilderMesh GenerateCylinder(PivotLocation, int axisDivisions, float radius, float height, int heightCuts, int smoothing = -1)` | use 12-16 divisions for buttons |
| `ShapeGenerator.CreateShape` | `static ProBuilderMesh CreateShape(ShapeType shape, PivotLocation pivotType = Center)` | no size argument |
| `ProBuilderMesh.SetMaterial` | `void SetMaterial(IEnumerable<Face> faces, Material material)` | then `ToMesh()` and `Refresh()` |
| `ProBuilderMesh.Create` | `static ProBuilderMesh Create()`, `Create(positions, faces)`, `Create(vertices, faces, sharedVertices, sharedTextures, materials)` | for hand-built meshes |
| `PrefabUtility.SaveAsPrefabAsset` | `static GameObject SaveAsPrefabAsset(GameObject root, string path)` | overwrites the asset and updates instances |
| `PrefabUtility.InstantiatePrefab` | `static Object InstantiatePrefab(Object asset)` | keeps the prefab link |

All shapes use `PivotLocation.Center`: a cube of height `h` at `y` spans
`y - h/2 .. y + h/2`. To stand a prop on a surface, place the root at the
surface and build upward from `0`.

`PivotLocation` has only `Center` and `FirstVertex`. `ShapeType` has `Arch`,
`Cone`, `Cube`, `CurvedStair`, `Cylinder`, `Door`, `Pipe`, `Plane`, `Prism`,
`Sphere`, `Sprite`, `Stair`, `Torus`.

## Helper block

Declare this at the top of every build call. Local functions, tuples, and
lambdas all compile inside `execute_code`.

```csharp
var cache = new System.Collections.Generic.Dictionary<string, Material>();
Material Mat(string n)
{
    Material m;
    if (!cache.TryGetValue(n, out m))
    {
        m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + n + ".mat");
        cache[n] = m;
    }
    return m;
}

Transform Group(Transform parent, string name)
{
    var g = new GameObject(name);
    g.transform.SetParent(parent, false);
    return g.transform;
}

GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, string matName)
{
    var pb = UnityEngine.ProBuilder.ShapeGenerator.GenerateCube(
        UnityEngine.ProBuilder.PivotLocation.Center, size);
    var go = pb.gameObject;
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    pb.SetMaterial(pb.faces, Mat(matName));
    pb.ToMesh();
    pb.Refresh();
    return go;
}

GameObject Cyl(Transform parent, string name, Vector3 pos, float radius, float height, string matName)
{
    var pb = UnityEngine.ProBuilder.ShapeGenerator.GenerateCylinder(
        UnityEngine.ProBuilder.PivotLocation.Center, 12, radius, height, 1, -1);
    var go = pb.gameObject;
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    pb.SetMaterial(pb.faces, Mat(matName));
    pb.ToMesh();
    pb.Refresh();
    return go;
}
```

## Part loop with a rotated sub-group

Positioning parts on an angled plane is easier with a rotated parent than with
per-part rotations: rotate the group, then place children in flat local space.

```csharp
var bay = Group(bank, "Bay " + (s + 1));
bay.localPosition = new Vector3(stationX[s], 3.20f, -0.15f);
bay.localEulerAngles = new Vector3(tilt, 0f, 0f);
Box(bay, "Bay Board", Vector3.zero, new Vector3(3.60f, 2.60f, 0.14f), "LabPanelLight");
Box(bay, "Screen Upper", new Vector3(0f, 0.62f, -0.10f), new Vector3(2.30f, 0.85f, 0.03f), "LabScreenBlue");
```

Derive the tilt sign instead of guessing it:

```csharp
// lean the panel top AWAY from the operator, whose front is local -Z
float tilt = (Quaternion.Euler(18f, 0f, 0f) * Vector3.up).z > 0f ? 18f : -18f;
```

## Procedural textures

Write a PNG, import it, then configure the importer. Pixel index is
`y * width + x`, origin bottom-left.

```csharp
System.Action<Texture2D, string> save = (tt, pth) =>
{
    System.IO.File.WriteAllBytes(pth, tt.EncodeToPNG());
    UnityEditor.AssetDatabase.ImportAsset(pth, UnityEditor.ImportAssetOptions.ForceUpdate);
    var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(pth);
    ti.textureType = UnityEditor.TextureImporterType.Default;
    ti.sRGBTexture = true;
    ti.mipmapEnabled = true;
    ti.wrapMode = TextureWrapMode.Repeat;
    ti.filterMode = FilterMode.Bilinear;
    ti.anisoLevel = 4;
    ti.maxTextureSize = 512;
    ti.SaveAndReimport();
};
```

Create the folder first:

```csharp
string dir = "Assets/Textures/Lab";
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Textures")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Textures");
if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/Textures", "Lab");
```

Name lambda parameters distinctly from enclosing loop variables. A lambda
parameter `x` declared before a sibling `for (int x = ...)` in the same block
fails with `CS0136`. Use `(ax, ay, col)` for pixel helpers.

Useful texture roles: `256x128` screen content (header bar, grid, waveform,
level meters), `256x256` brushed panel with seams and rivets, `128x128`
perforated rack door, `64x64` louvred vent slats.

## Material creation with emission

`manage_material` cannot enable a shader keyword, so create emissive materials
in `execute_code`. The keyword must be enabled before the emission is visible.

```csharp
var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
mat.SetColor("_BaseColor", new Color(0.05f, 0.08f, 0.14f));
mat.SetFloat("_Metallic", 0.10f);
mat.SetFloat("_Smoothness", 0.55f);
var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Lab/ScreenData.png");
mat.SetTexture("_BaseMap", tex);
mat.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
mat.EnableKeyword("_EMISSION");
mat.SetColor("_EmissionColor", new Color(0.35f, 0.60f, 1.00f) * 1.7f);
mat.SetTexture("_EmissionMap", tex);   // only the lit pixels glow
mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
UnityEditor.AssetDatabase.CreateAsset(mat, "Assets/Materials/LabScreenBlue.mat");
```

`AssetDatabase.CreateAsset` overwrites an existing asset at the same path.
Emission gains that keep the hue and still bloom are `1.4` to `2.4`; `4.0` or
more on a close surface blows out to white.

## Save the prefab, then place instances

```csharp
var rends = root.GetComponentsInChildren<Renderer>();
Bounds b = rends[0].bounds;
for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
var bc = root.AddComponent<BoxCollider>();
bc.center = b.center;
bc.size = b.size;

var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Lab/Lab Rack Cabinet.prefab");
UnityEngine.Object.DestroyImmediate(root);
```

```csharp
var group = new GameObject("Lab Terminals");
var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lab/Lab Rack Cabinet.prefab");
var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pf);
go.name = "Lab Rack Cabinet A";
go.transform.SetParent(group.transform, false);
go.transform.position = new Vector3(20.5f, -1.2f, 10.6f);
go.transform.eulerAngles = new Vector3(0f, 90f, 0f);
```

Yaw reference, because the sign is easy to invert:

| Requirement | Yaw | Local `+X` maps to | Local `+Z` maps to |
| --- | --- | --- | --- |
| Front faces `-Z` | `0` | world `+X` | world `+Z` |
| Front faces `-X` | `90` | world `-Z` | world `+X` |
| Front faces `+Z` | `180` | world `-X` | world `-Z` |
| Front faces `+X` | `-90` | world `+Z` | world `-X` |

## Validation scans

Run all three through `execute_code`. Each one caught a real fault during the
terminal build.

```csharp
// null-material scan
int nullMat = 0, renderers = 0;
foreach (var go in placed)
    foreach (var r in go.GetComponentsInChildren<Renderer>())
    {
        renderers++;
        if (r.sharedMaterial == null) nullMat++;
    }
```

```csharp
// combined bounds for one instance
Bounds nb = new Bounds(go.transform.position, Vector3.zero);
bool first = true;
foreach (var r in go.GetComponentsInChildren<Renderer>())
{
    if (first) { nb = r.bounds; first = false; } else nb.Encapsulate(r.bounds);
}
```

```csharp
// intersection scan. Bounds.Intersect does NOT exist: compute the overlap.
var other = GameObject.Find("Lab Wall E Panel").GetComponent<Renderer>().bounds;
Vector3 ov = Vector3.Min(nb.max, other.max) - Vector3.Max(nb.min, other.min);
if (ov.x > 0.05f && ov.y > 0.05f && ov.z > 0.05f)
    Debug.Log($"clash overlap=({ov.x:F2},{ov.y:F2},{ov.z:F2})");
```

`Bounds.Intersects` returns true for touching surfaces, so compare the overlap
size rather than the boolean. Props that stand on the floor and mount on walls
always touch something.

```csharp
// persistence check: prove the save wrote the meshes
// (Select-String -Path 'Assets/Test.unity' -Pattern 'ProBuilderMesh' -AllMatches).Count
```

## Gotchas

| Symptom | Cause | Action |
| --- | --- | --- |
| `Blocked pattern detected: 'AssetDatabase.DeleteAsset'` | `execute_code` safety check | Drop the delete. `CreateAsset` overwrites. |
| `'Bounds' does not contain a definition for 'Intersect'` | not a Unity API | Use `Vector3.Min` / `Vector3.Max` on `min` / `max`. |
| `CS0136` on a lambda parameter | name collides with a sibling local | Rename lambda parameters. |
| Magenta object | new ProBuilder shape has no material | `SetMaterial(pb.faces, mat)`. |
| Prop floats or sinks | `PivotLocation.Center` | Root at the surface, build upward from `0`. |
| Emission visible but not glowing | no Bloom volume | Add a global Volume with a Bloom effect. |
| Scene-view screenshot is portrait | editor window aspect | Frame tighter and capture one angle at a time. |
| `No Unity Editor instances found` mid-batch | domain reload or asset import | Retry the call; verify what actually applied. |
| Second screenshot gains a `_1` suffix | file name already exists | The tool does not overwrite. Reuse or delete first. |
