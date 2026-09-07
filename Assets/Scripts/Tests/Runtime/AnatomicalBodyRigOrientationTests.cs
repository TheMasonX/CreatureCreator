using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class AnatomicalBodyRigOrientationTests
    {
        [Test]
        public void Build_ForwardPointsToHead_LastStoredEndpointIsHead()
        {
            var samples = new List<BodySample>
            {
                new BodySample { Id = 1u, Position = new Vector3(0f, 0f, -2f), Radius = 1f },
                new BodySample { Id = 2u, Position = new Vector3(0f, 0f, -1f), Radius = 1f },
                new BodySample { Id = 3u, Position = new Vector3(0f, 0f, 0f), Radius = 1f },
                new BodySample { Id = 4u, Position = new Vector3(0f, 0f, 1f), Radius = 1f },
                new BodySample { Id = 5u, Position = new Vector3(0f, 0f, 2f), Radius = 1f },
            };

            ResolvedBody body = ResolvedBody.Resolve(samples);
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bones =
                AnatomicalBodyRigLayout.Build(body, Vector3.forward);

            AnatomicalBodyRigLayout.BoneSpec head = Find(bones, AnatomicalBodyRigLayout.HeadBoneId);
            AnatomicalBodyRigLayout.BoneSpec tail = FindTerminalTail(bones);

            Assert.That(head.Position.z, Is.GreaterThan(tail.EndPosition.z));
            Assert.That(head.Position, Is.EqualTo(new Vector3(0f, 0f, 2f)));
            Assert.That(tail.EndPosition, Is.EqualTo(new Vector3(0f, 0f, -2f)));
        }

        [Test]
        public void Build_ReversedStoredOrderStillKeepsHeadTowardForward()
        {
            var samples = new List<BodySample>
            {
                new BodySample { Id = 1u, Position = new Vector3(0f, 0f, 2f), Radius = 1f },
                new BodySample { Id = 2u, Position = new Vector3(0f, 0f, 1f), Radius = 1f },
                new BodySample { Id = 3u, Position = new Vector3(0f, 0f, 0f), Radius = 1f },
                new BodySample { Id = 4u, Position = new Vector3(0f, 0f, -1f), Radius = 1f },
                new BodySample { Id = 5u, Position = new Vector3(0f, 0f, -2f), Radius = 1f },
            };

            ResolvedBody body = ResolvedBody.Resolve(samples);
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bones =
                AnatomicalBodyRigLayout.Build(body, Vector3.forward);

            AnatomicalBodyRigLayout.BoneSpec head = Find(bones, AnatomicalBodyRigLayout.HeadBoneId);
            AnatomicalBodyRigLayout.BoneSpec tail = FindTerminalTail(bones);

            Assert.That(head.Position, Is.EqualTo(new Vector3(0f, 0f, 2f)));
            Assert.That(tail.EndPosition, Is.EqualTo(new Vector3(0f, 0f, -2f)));
        }

        private static AnatomicalBodyRigLayout.BoneSpec Find(
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bones, string id)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].Id == id) return bones[i];
            }
            Assert.Fail($"Missing bone '{id}'.");
            return default;
        }

        private static AnatomicalBodyRigLayout.BoneSpec FindTerminalTail(
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bones)
        {
            AnatomicalBodyRigLayout.BoneSpec result = default;
            bool found = false;
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].Id == AnatomicalBodyRigLayout.TailBoneId
                    || bones[i].Id.StartsWith(AnatomicalBodyRigLayout.TailBoneId + "_"))
                {
                    result = bones[i];
                    found = true;
                }
            }
            Assert.IsTrue(found, "Missing Body tail branch.");
            return result;
        }
    }
}
