using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProceduralCreature.Tests.Runtime
{
    public sealed class CreatureRigTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        [Test]
        public void BuildAndApplyPose_MatchesSkeletonAndPreservesUnownedChildren()
        {
            var host = new GameObject("RigHost");
            var unrelated = new GameObject("Unrelated");
            unrelated.transform.SetParent(host.transform);
            _objects.Add(host);

            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Position = new Vector3(1f, 0f, 0f),
                Rotation = Quaternion.Euler(0f, 15f, 0f),
            });
            skeleton.Bones.Add(new Bone
            {
                Id = "tip",
                ParentBoneId = "root",
                Position = new Vector3(1f, 1f, 0f),
                Rotation = Quaternion.Euler(0f, 15f, 0f),
            });

            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(skeleton);

            Assert.AreEqual(2, rig.Bones.Count);
            Assert.AreSame(unrelated.transform, host.transform.GetChild(0));
            Assert.AreSame(rig.Bones["root"], rig.Bones["tip"].parent);
            Assert.That(Vector3.Distance(rig.Bones["root"].position, skeleton.Bones[0].Position), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(rig.Bones["tip"].position, skeleton.Bones[1].Position), Is.LessThan(1e-5f));

            PosedSkeleton pose = PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    ["root"] = new Vector3(2f, 0f, 0f),
                    ["tip"] = new Vector3(2f, 1f, 0f),
                });
            rig.ApplyPose(pose);

            Assert.That(Vector3.Distance(rig.Bones["root"].position, new Vector3(2f, 0f, 0f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(rig.Bones["tip"].position, new Vector3(2f, 1f, 0f)), Is.LessThan(1e-5f));
            Assert.Greater(Vector3.Dot(rig.Bones["root"].rotation * Vector3.forward, Vector3.up), 0.999f);

            rig.Clear();
            Assert.AreSame(unrelated.transform, host.transform.GetChild(0));
        }

        [Test]
        public void Build_OrdersChildBeforeParentInputDeterministically()
        {
            var host = new GameObject("RigHost");
            _objects.Add(host);
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "a_child",
                ParentBoneId = "z_parent",
                Position = Vector3.up,
                Rotation = Quaternion.identity,
            });
            skeleton.Bones.Add(new Bone
            {
                Id = "z_parent",
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
            });

            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(skeleton);

            Assert.AreSame(rig.Bones["z_parent"], rig.Bones["a_child"].parent);
        }

        [Test]
        public void FailedBuild_PreservesPreviousValidRig()
        {
            var host = new GameObject("RigHost");
            _objects.Add(host);
            var valid = new Skeleton.Skeleton();
            valid.Bones.Add(new Bone
            {
                Id = "root",
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
            });

            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(valid);
            Transform previousRoot = rig.Bones["root"];

            var invalid = new Skeleton.Skeleton();
            invalid.Bones.Add(new Bone
            {
                Id = "broken",
                ParentBoneId = "missing",
                Position = Vector3.one,
                Rotation = Quaternion.identity,
            });

            Assert.Throws<DomainException>(() => rig.Build(invalid));
            Assert.AreSame(previousRoot, rig.Bones["root"]);
            rig.ApplyPose(PosedSkeleton.FromRestPose(valid));
            Assert.AreSame(previousRoot, rig.Bones["root"]);
        }

        [Test]
        public void ApplyPose_RepeatedCallsAllocateNoManagedMemoryAfterWarmup()
        {
            var host = new GameObject("RigHost");
            _objects.Add(host);
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "root", Rotation = Quaternion.identity });
            skeleton.Bones.Add(new Bone
            {
                Id = "tip",
                ParentBoneId = "root",
                Position = Vector3.up,
                Rotation = Quaternion.identity,
            });

            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(skeleton);
            PosedSkeleton pose = PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3> { ["tip"] = Vector3.up * 2f });

            for (int i = 0; i < 16; i++) rig.ApplyPose(pose);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) rig.ApplyPose(pose);
            stopwatch.Stop();
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Debug.Log($"ApplyPose repeated=1000 allocatedBytes={allocated} elapsedMilliseconds={stopwatch.Elapsed.TotalMilliseconds:F3}");
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void RigHostSpace_IdentityRoot_BoneWorldPositionMatchesCreatureCoordinate()
        {
            var host = new GameObject("RigHost");
            _objects.Add(host);
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Position = new Vector3(1f, 2f, 3f),
                Rotation = Quaternion.identity,
            });

            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(skeleton);

            // At an identity host the bone's world position is the creature-space
            // coordinate directly (the reference behavior of the space contract).
            Assert.That(Vector3.Distance(rig.Bones["root"].position, new Vector3(1f, 2f, 3f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void RigHostSpace_NonIdentityRoot_DoesNotOffsetBoneWorldPosition()
        {
            var host = new GameObject("RigHost");
            host.transform.position = new Vector3(10f, 0f, 0f);
            _objects.Add(host);
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Position = new Vector3(1f, 0f, 0f),
                Rotation = Quaternion.identity,
            });

            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(skeleton);

            // CreatureRig is not a world-space adapter: it writes the
            // creature-space coordinate as the bone's world position without
            // composing the host transform, so a non-identity host does not
            // offset the bone. This documents why the host must remain at
            // identity (the explicit space-contract invariant).
            Assert.That(Vector3.Distance(rig.Bones["root"].position, new Vector3(1f, 0f, 0f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(rig.Bones["root"].position, new Vector3(11f, 0f, 0f)), Is.GreaterThan(1f));
        }

        [UnityTest]
        public System.Collections.IEnumerator ExternalPoseDriverHarness_DirectIndexedPose_MovesBoneWithinOneFrame()
        {
            var host = new GameObject("PoseDriverHost");
            _objects.Add(host);

            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
            });
            skeleton.Bones.Add(new Bone
            {
                Id = "tip",
                ParentBoneId = "root",
                Position = Vector3.right,
                Rotation = Quaternion.identity,
            });

            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(skeleton);

            // Chosen external pose-driver interface: a direct `PosedSkeleton` is pushed
            // through `CreatureRig.ApplyPose`, without introducing any Animator/Avatar
            // pipeline or locomotion state machine.
            PosedSkeleton restPose = PosedSkeleton.FromRestPose(skeleton);
            PosedSkeleton drivenPose = restPose.WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    ["tip"] = new Vector3(2f, 0f, 0f),
                });

            rig.ApplyPose(restPose);
            yield return null;

            rig.ApplyPose(drivenPose);
            Assert.That(rig.Bones["tip"].position.x, Is.EqualTo(2f).Within(1e-5f),
                "direct indexed pose application must move the real bone at the chosen boundary");

            yield return null;
            Assert.That(rig.Bones["tip"].position.x, Is.EqualTo(2f).Within(1e-5f),
                "the moved bone remains in-place after a frame advance");
        }
    }
}
