using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Skinned;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Skeleton;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class CreatureSkinnedMeshRendererCompatibilityTests
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
        public void Bind_RejectsSameCountButDifferentRestSkeleton()
        {
            SkeletonModel rigSkeleton = CreateSingleBoneSkeleton(Vector3.zero);
            SkeletonModel differentSkeleton = CreateSingleBoneSkeleton(new Vector3(1f, 0f, 0f));

            var host = new GameObject("SkinnedCompatibilityHost");
            _objects.Add(host);
            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(rigSkeleton);
            CreatureSkinnedMeshRenderer adapter = host.AddComponent<CreatureSkinnedMeshRenderer>();
            var mesh = new Mesh();
            try
            {
                Assert.Throws<DomainException>(() => adapter.Bind(rig, differentSkeleton, mesh));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        private static SkeletonModel CreateSingleBoneSkeleton(Vector3 position)
        {
            var skeleton = new SkeletonModel();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                ParentBoneId = null,
                SourcePartId = "root",
                PartType = PartType.Limb,
                IsMirrored = false,
                Position = position,
                Rotation = Quaternion.identity,
            });
            return skeleton;
        }
    }
}
