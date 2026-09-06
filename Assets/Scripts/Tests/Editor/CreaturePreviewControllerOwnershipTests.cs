using System.Collections.Generic;
using NUnit.Framework;
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
            // SessionState is process/editor-global, so clear the ownership keys
            // to keep each test hermetic (no stale handle from a prior test).
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

        private static GeneratedCreature BuildGenerated(int extraItems)
        {
            var generated = new GeneratedCreature();
            generated.Geometry.Add(new GeometryItem
            {
                SourcePartId = GeneratedCreature.ImplicitSurfaceSourceId,
                Mesh = MakeMesh()
            });
            for (int i = 0; i < extraItems; i++)
            {
                generated.Geometry.Add(new GeometryItem { SourcePartId = "extra-" + i, Mesh = MakeMesh() });
            }
            return generated;
        }

        [Test]
        public void UnrelatedSameNameRootAndPrefixedChild_AreNeverAdoptedModifiedOrDestroyed()
        {
            // Unrelated scene object that mimics the OLD name/prefix ownership
            // protocol: same root name and a child with the old geometry prefix.
            GameObject unrelatedRoot = Track(new GameObject("CreatureCreator Preview"));
            Vector3 sentinel = new Vector3(123f, 456f, 789f);
            unrelatedRoot.transform.position = sentinel;
            GameObject unrelatedChild = Track(new GameObject("CreatureCreator Preview Geometry 7"));
            unrelatedChild.transform.SetParent(unrelatedRoot.transform, worldPositionStays: false);
            unrelatedChild.AddComponent<MeshFilter>();

            CreaturePreviewController controller = CreateController();

            // Regenerating twice forces the controller to clear and rebuild its own
            // geometry while the unrelated root/child keep the old names.
            controller.ApplyPreviewGeometry(BuildGenerated(extraItems: 2));
            GameObject ownedRoot = Track(controller.PreviewGameObject);
            Assert.IsNotNull(ownedRoot);
            controller.ApplyPreviewGeometry(BuildGenerated(extraItems: 1));

            // Never adopted: the controller created and registered its own root
            // rather than reusing the unrelated, unregistered, same-named one.
            Assert.AreNotSame(unrelatedRoot, ownedRoot);
            Assert.AreSame(ownedRoot, controller.PreviewGameObject,
                "The controller's own root must be stable across regeneration.");

            // Never modified or destroyed, even though its name matches the old
            // protocol and a cleanup cycle just ran.
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
            first.ApplyPreviewGeometry(BuildGenerated(extraItems: 1));
            GameObject root = Track(first.PreviewGameObject);
            Assert.IsNotNull(root);

            // Simulated domain reload: the prior controller is dropped but does not
            // destroy the root, so a fresh controller must recover the SAME root by
            // its recorded instance handle rather than recreating it.
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

            // Even after a regeneration the unrelated root is not adopted.
            controller.ApplyPreviewGeometry(BuildGenerated(extraItems: 0));
            Assert.IsNotNull(controller.PreviewGameObject);
            Assert.AreEqual(2, CountSceneObjectsNamed("CreatureCreator Preview"),
                "Regeneration must create its own root, not reuse the unrelated one.");
            controller.Dispose();
        }

        [Test]
        public void RegenerationDestroysOnlyRegisteredGeometryChildren()
        {
            CreaturePreviewController controller = CreateController();
            controller.ApplyPreviewGeometry(BuildGenerated(extraItems: 2));
            GameObject root = Track(controller.PreviewGameObject);
            Assert.AreEqual(2, root.transform.childCount,
                "Two owned geometry children should be created and parented under the root.");

            // A foreign, unregistered child nested under the owned root must
            // survive cleanup.
            GameObject foreign = Track(new GameObject("User Kept Object"));
            foreign.transform.SetParent(root.transform, worldPositionStays: false);
            foreign.AddComponent<MeshFilter>();

            // Trigger a cleanup + rebuild with one owned geometry child.
            controller.ApplyPreviewGeometry(BuildGenerated(extraItems: 1));

            Assert.IsNotNull(foreign, "An unregistered foreign child must survive cleanup.");
            Assert.IsTrue(foreign.transform.IsChildOf(root.transform));
            Assert.AreEqual(2, root.transform.childCount,
                "After regeneration the root should hold only the foreign child and the single new owned child.");

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
    }
}
