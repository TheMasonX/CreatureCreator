using NUnit.Framework;
using ProceduralCreature.Animation;
using UnityEngine;
using CreatureSkeleton = ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// TSK-0215: CreatureRig is rebuilt whenever the creature regenerates. Its
    /// tracked generated-object list is not serialized, so after a domain reload it
    /// is empty while the previous bone hierarchy still exists in the scene. These
    /// tests pin that Build and Clear stay idempotent in that situation, so
    /// duplicate, coincident bone Transforms (which make overlay bone picking
    /// ambiguous) cannot accumulate.
    /// </summary>
    [TestFixture]
    public class CreatureRigBuildIdempotencyTests
    {
        [Test]
        public void Build_AfterBookkeepingLost_ReplacesPreviousHierarchy()
        {
            var host = new GameObject("RigHost");
            try
            {
                CreatureSkeleton.Skeleton skeleton = BuildTwoBoneSkeleton();

                CreatureRig first = host.AddComponent<CreatureRig>();
                first.Build(skeleton);
                Assert.AreEqual(1, CountDirectChildrenStartingWith(host, "Bone_"),
                    "the first build should create exactly one root bone");

                // A domain reload recreates the component instance, so the
                // non-serialized tracked list is empty even though the previous
                // generated hierarchy is still parented to the host.
                Object.DestroyImmediate(first);
                CreatureRig replacement = host.AddComponent<CreatureRig>();
                replacement.Build(skeleton);

                Assert.AreEqual(1, CountDirectChildrenStartingWith(host, "Bone_"),
                    "a reload-then-rebuild must not leave a second, coincident bone hierarchy");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Clear_AfterBookkeepingLost_RemovesPreviousHierarchy()
        {
            var host = new GameObject("RigHost");
            try
            {
                CreatureRig first = host.AddComponent<CreatureRig>();
                first.Build(BuildTwoBoneSkeleton());

                Object.DestroyImmediate(first);
                CreatureRig replacement = host.AddComponent<CreatureRig>();
                replacement.Clear();

                Assert.AreEqual(0, CountDirectChildrenStartingWith(host, "Bone_"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Build_PreservesUnownedChildrenAndNestedLookalikes()
        {
            var host = new GameObject("RigHost");
            try
            {
                var unrelated = new GameObject("Unrelated");
                unrelated.transform.SetParent(host.transform);
                var nestedLookalike = new GameObject("Bone_lookalike");
                nestedLookalike.transform.SetParent(unrelated.transform);

                CreatureRig rig = host.AddComponent<CreatureRig>();
                rig.Build(BuildTwoBoneSkeleton());

                Assert.IsNotNull(host.transform.Find("Unrelated"),
                    "unowned children must survive a build");
                Assert.IsNotNull(host.transform.Find("Unrelated/Bone_lookalike"),
                    "only direct generated roots are removed, not nested lookalikes");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FailedBuild_AfterBookkeepingLost_PreservesPreviousHierarchy()
        {
            var host = new GameObject("RigHost");
            try
            {
                CreatureRig first = host.AddComponent<CreatureRig>();
                first.Build(BuildTwoBoneSkeleton());

                Object.DestroyImmediate(first);
                CreatureRig replacement = host.AddComponent<CreatureRig>();
                Transform survivingRoot = host.transform.Find("Bone_root");

                var invalid = new CreatureSkeleton.Skeleton();
                invalid.Bones.Add(new CreatureSkeleton.Bone
                {
                    Id = "broken",
                    ParentBoneId = "missing",
                    Position = Vector3.zero,
                    Rotation = Quaternion.identity,
                });

                Assert.Throws<ProceduralCreature.Common.DomainException>(() => replacement.Build(invalid));
                Assert.AreSame(survivingRoot, host.transform.Find("Bone_root"),
                    "a failed build must not destroy the previous valid hierarchy");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static CreatureSkeleton.Skeleton BuildTwoBoneSkeleton()
        {
            var skeleton = new CreatureSkeleton.Skeleton();
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "root",
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
            });
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "tip",
                ParentBoneId = "root",
                Position = Vector3.up,
                Rotation = Quaternion.identity,
            });
            return skeleton;
        }

        private static int CountDirectChildrenStartingWith(GameObject host, string prefix)
        {
            int count = 0;
            Transform t = host.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                if (t.GetChild(i).name.StartsWith(prefix, System.StringComparison.Ordinal)) count++;
            }
            return count;
        }
    }
}
