using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;
using ProceduralCreature.Generation;
using UnityEditor;
using UnityEngine;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// TSK-0122 EditMode coverage: the editor preview's ownership must be
    /// structural (a recorded instance handle persisted in SessionState), not
    /// inferred from a human-readable scene name or a geometry-child name prefix.
    /// These tests prove an unrelated same-name/same-prefix object is never
    /// adopted, modified, or destroyed, that the controller re-finds its own root
    /// by its recorded handle across a simulated domain reload, and that cleanup
    /// destroys only the children it registered.
    /// </summary>
    [TestFixture]
    public sealed class CreaturePreviewControllerOwnershipTests
    {
        private readonly List<GameObject> _tracked = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            SessionState.EraseString(CreaturePreviewController.RootEntityKey);
            SessionState.EraseString(CreaturePreviewController.GeometryEntityIdsKey);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                if (_tracked[i] != null) Object.DestroyImmediate(_tracked[i]);
            }
            _tracked.Clear();
            SessionState.EraseString(CreaturePreviewController.RootEntityKey);
            SessionState.EraseString(CreaturePreviewController.GeometryEntityIdsKey);
        }

        private GameObject Track(GameObject go)
        {
            if (go != null) _tracked.Add(go);
            return go;
        }

        private static CreaturePreviewController CreateController()
        {
            return new CreaturePreviewController(() => null, _ => null);
        }

        private static Mesh MakeMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f) },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static CreaturePart MakePreviewMeshPart(string id)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Body,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
        }

        private static GeneratedCreature BuildGenerated(int extraItems)
        {
            var generated = new GeneratedCreature();
            generated.AddGeometry(new GeometryItem(
                sourcePartId: GeneratedCreature.ImplicitSurfaceSourceId,
                geometryType: GeometryType.Implicit,
                mesh: MakeMesh(),
                sourceMesh: null,
                restPlacement: Matrix4x4.identity,
                materialRegions: null,
                rigBinding: new RigBindingMetadata(GeneratedCreature.ImplicitSurfaceSourceId, null, false)));
            for (int i = 0; i < extraItems; i++)
            {
                string partId = "extra-" + i;
                generated.AddGeometry(new GeometryItem(
                    sourcePartId: partId,
                    geometryType: GeometryType.MeshAsset,
                    mesh: MakeMesh(),
                    sourceMesh: null,
                    restPlacement: Matrix4x4.identity,
                    materialRegions: null,
                    rigBinding: new RigBindingMetadata(partId, CreatureDefinition.BodyId, false)));
            }
            return generated;
        }

        [Test]
        public void UnrelatedSameNameRootAndPrefixedChild_AreNeverAdoptedModifiedOrDestroyed()
        {
            GameObject unrelatedRoot = Track(new GameObject("CreatureCreator Preview"));
            Vector3 sentinel = new Vector3(123f, 456f, 789f);
            unrelatedRoot.transform.position = sentinel;
            GameObject unrelatedChild = Track(new GameObject("CreatureCreator Preview Geometry 7"));
            unrelatedChild.transform.SetParent(unrelatedRoot.transform, worldPositionStays: false);
            unrelatedChild.AddComponent<MeshFilter>();

            CreaturePreviewController controller = CreateController();
            ApplyPreview(controller, extraItems: 2);
            GameObject ownedRoot = Track(controller.PreviewGameObject);
            Assert.IsNotNull(ownedRoot);

            ApplyPreview(controller, extraItems: 1);
            Assert.AreNotSame(unrelatedRoot, ownedRoot);
            Assert.AreSame(ownedRoot, controller.PreviewGameObject,
                "The controller's own root must be stable across regeneration.");

            Assert.IsNotNull(unrelatedRoot);
            Assert.IsNotNull(unrelatedChild);
            Assert.IsTrue(unrelatedChild.transform.IsChildOf(unrelatedRoot.transform));
            Assert.AreEqual(sentinel, unrelatedRoot.transform.position);
            Assert.IsNotNull(unrelatedChild.GetComponent<MeshFilter>(),
                "An unrelated prefixed child's components must be untouched.");
            Assert.AreEqual(1, unrelatedRoot.transform.childCount,
                "The unrelated root must keep exactly its original child.");

            controller.Dispose();
        }

        [Test]
        public void RecoverExistingPreview_FindsOwnRootByHandleAcrossSimulatedReload()
        {
            CreaturePreviewController first = CreateController();
            ApplyPreview(first, extraItems: 1);
            GameObject root = Track(first.PreviewGameObject);
            Assert.IsNotNull(root);

            first.Dispose();

            CreaturePreviewController recoveredController = CreateController();
            GameObject recovered = Track(recoveredController.RecoverExistingPreview());
            Assert.AreSame(root, recovered,
                "The preview root must be recovered by its recorded handle, not recreated.");
            Assert.IsNotNull(recovered.GetComponent<MeshCollider>());

            recoveredController.Dispose();
        }

        [Test]
        public void RecoverExistingPreview_IgnoresUnregisteredSameNameRoot()
        {
            Track(new GameObject("CreatureCreator Preview"));

            CreaturePreviewController controller = CreateController();
            Assert.IsNull(controller.RecoverExistingPreview(),
                "An unregistered same-named root must not be recovered as the preview root.");

            ApplyPreview(controller, extraItems: 0);
            Assert.IsNotNull(controller.PreviewGameObject);
            Assert.AreEqual(2, CountSceneObjectsNamed("CreatureCreator Preview"),
                "Regeneration must create its own root, not reuse the unrelated one.");
            controller.Dispose();
        }

        [Test]
        public void RegenerationDestroysOnlyRegisteredGeometryChildren()
        {
            CreaturePreviewController controller = CreateController();
            ApplyPreview(controller, extraItems: 2);
            GameObject root = Track(controller.PreviewGameObject);
            Assert.AreEqual(2, CountDescendantsNamed(root, "Preview Mesh "),
                "Two owned geometry children should be created somewhere under the root's rig hierarchy.");

            GameObject foreign = Track(new GameObject("User Kept Object"));
            foreign.transform.SetParent(root.transform, worldPositionStays: false);
            foreign.AddComponent<MeshFilter>();

            ApplyPreview(controller, extraItems: 1);

            Assert.IsNotNull(foreign, "An unregistered foreign child must survive cleanup.");
            Assert.IsTrue(foreign.transform.IsChildOf(root.transform));
            Assert.AreEqual(1, CountDescendantsNamed(root, "Preview Mesh "),
                "After regeneration the root should retain one new owned geometry child in the rig hierarchy.");

            controller.Dispose();
        }

        private static int CountSceneObjectsNamed(string name)
        {
            int count = 0;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) count++;
            }
            return count;
        }

        private static int CountDescendantsNamed(GameObject parent, string prefix)
        {
            if (parent == null) return 0;

            int count = 0;
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                Transform child = parent.transform.GetChild(i);
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal)) count++;
                count += CountDescendantsNamed(child.gameObject, prefix);
            }
            return count;
        }

        private static void ApplyPreview(CreaturePreviewController controller, int extraItems)
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = new Vector3(0f, 0f, -1f),
                Radius = 1f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, 1f),
                Radius = 1f,
            });
            for (int i = 0; i < extraItems; i++)
            {
                definition.AddPart(MakePreviewMeshPart("extra-" + i));
            }

            controller.ApplyPreviewGeometry(
                BuildGenerated(extraItems),
                definition,
                ResolvedCreatureSnapshot.Resolve(definition));
        }
    }
}
