using System;
using System.Collections.Generic;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using UnityEngine;

namespace ProceduralCreature.Editor
{
    internal sealed class CreaturePreviewController : IDisposable
    {
        private const string PreviewObjectName = "CreatureCreator Preview";
        private const string PreviewGeometryChildPrefix = "CreatureCreator Preview Geometry ";
        private readonly CreatureGenerationScheduler _scheduler = new CreatureGenerationScheduler();
        private readonly Func<Material> _defaultMaterialResolver;
        private readonly Func<string, Material> _materialResolver;
        private readonly List<GameObject> _geometryObjects = new List<GameObject>();
        private bool _disposed;

        public CreaturePreviewController(
            Func<Material> defaultMaterialResolver,
            Func<string, Material> materialResolver)
        {
            _defaultMaterialResolver = defaultMaterialResolver ?? throw new ArgumentNullException(nameof(defaultMaterialResolver));
            _materialResolver = materialResolver ?? throw new ArgumentNullException(nameof(materialResolver));
        }

        public GameObject PreviewGameObject { get; private set; }

        public long Enqueue(CreatureDefinition definition, float voxelsPerUnit, bool logDiagnostics)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_disposed) throw new ObjectDisposedException(nameof(CreaturePreviewController));

            CreatureDefinition captured = definition.Clone();
            captured.Generation.VoxelsPerUnit = voxelsPerUnit;
            return _scheduler.Enqueue(captured, new GenerationDiagnostics(logDiagnostics));
        }

        public void ProcessCompletions(Action<CreatureGenerationResult> onCompleted)
        {
            if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
            if (_disposed) return;

            while (_scheduler.TryTakeCompleted(out CreatureGenerationResult result))
            {
                if (!result.IsStale) onCompleted(result);
            }
        }

        public void ApplyPreviewGeometry(GeneratedCreature generated)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            if (_disposed) throw new ObjectDisposedException(nameof(CreaturePreviewController));

            ApplyPreviewMesh(generated.MainMesh);
            ClearGeometryObjects();

            for (int i = 1; i < generated.Geometry.Count; i++)
            {
                GeometryItem item = generated.Geometry[i];
                var child = new GameObject(PreviewGeometryChildPrefix + i);
                child.transform.SetParent(PreviewGameObject.transform, worldPositionStays: false);
                child.AddComponent<MeshFilter>().sharedMesh = item.Mesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                AssignMaterials(renderer, item);
                _geometryObjects.Add(child);
            }
        }

        private void ApplyPreviewMesh(Mesh mesh)
        {
            if (PreviewGameObject == null) PreviewGameObject = GameObject.Find(PreviewObjectName);
            if (PreviewGameObject == null)
            {
                PreviewGameObject = new GameObject(PreviewObjectName);
                PreviewGameObject.AddComponent<MeshFilter>();
                PreviewGameObject.AddComponent<MeshRenderer>();
                PreviewGameObject.AddComponent<MeshCollider>();
            }

            PreviewGameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = PreviewGameObject.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = PreviewGameObject.AddComponent<MeshRenderer>();
            Material defaultMaterial = _defaultMaterialResolver();
            if (defaultMaterial != null) renderer.sharedMaterial = defaultMaterial;

            MeshCollider collider = PreviewGameObject.GetComponent<MeshCollider>();
            if (collider == null) collider = PreviewGameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private void AssignMaterials(MeshRenderer renderer, GeometryItem item)
        {
            Material fallback = _defaultMaterialResolver();
            if (item.MaterialRegions.Count == 0)
            {
                if (fallback != null) renderer.sharedMaterial = fallback;
                return;
            }

            Material resolved = _materialResolver(item.MaterialRegions[0].MaterialKey);
            if (fallback == null && resolved == null) return;

            int subMeshCount = Mathf.Max(1, item.Mesh != null ? item.Mesh.subMeshCount : 1);
            var materials = new Material[subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = fallback;
            materials[0] = resolved != null ? resolved : fallback;
            renderer.sharedMaterials = materials;
        }

        private void ClearGeometryObjects()
        {
            for (int i = _geometryObjects.Count - 1; i >= 0; i--)
            {
                if (_geometryObjects[i] != null) UnityEngine.Object.DestroyImmediate(_geometryObjects[i]);
            }
            _geometryObjects.Clear();

            if (PreviewGameObject == null) return;
            for (int i = PreviewGameObject.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = PreviewGameObject.transform.GetChild(i);
                if (child.name.StartsWith(PreviewGeometryChildPrefix, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scheduler.Dispose();
        }
    }
}
