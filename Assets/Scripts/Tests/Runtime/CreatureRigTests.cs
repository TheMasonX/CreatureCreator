using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Ik;
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
            Assert.That(rig.Bones["root"].position, Is.EqualTo(skeleton.Bones[0].Position));
            Assert.That(rig.Bones["tip"].position, Is.EqualTo(skeleton.Bones[1].Position));

            PosedSkeleton pose = PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    ["root"] = new Vector3(2f, 0f, 0f),
                    ["tip"] = new Vector3(2f, 1f, 0f),
                });
            rig.ApplyPose(pose);

            Assert.That(rig.Bones["root"].position, Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(rig.Bones["tip"].position, Is.EqualTo(new Vector3(2f, 1f, 0f)));
            Assert.Greater(Vector3.Dot(rig.Bones["root"].rotation * Vector3.forward, Vector3.up), 0.999f);

            rig.Clear();
            Assert.AreEqual(1, host.transform.childCount);
            Assert.AreSame(unrelated.transform, host.transform.GetChild(0));
        }
    }
}
