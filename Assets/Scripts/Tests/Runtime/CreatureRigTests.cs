using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

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
    }
}
