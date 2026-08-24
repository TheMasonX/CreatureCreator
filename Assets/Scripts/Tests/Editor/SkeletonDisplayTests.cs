using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Editor;
using CreatureSkeleton = ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// CC-066: the pure SkeletonDisplay view-data helpers. The SceneView overlay
    /// itself is a manual residual check (MCP cannot simulate SceneView); these
    /// tests cover the line/joint computation the overlay renders, so the display
    /// mode's data path is verified without the editor window.
    /// </summary>
    [TestFixture]
    public class SkeletonDisplayTests
    {
        [Test]
        public void BuildBoneLines_NullSkeleton_ReturnsEmpty()
        {
            List<SkeletonDisplay.BoneLine> lines = SkeletonDisplay.BuildBoneLines(null);
            Assert.IsNotNull(lines);
            Assert.AreEqual(0, lines.Count);
        }

        [Test]
        public void BuildBoneLines_RootOnly_ReturnsEmpty()
        {
            var skeleton = new CreatureSkeleton.Skeleton();
            skeleton.Bones.Add(new CreatureSkeleton.Bone { Id = "root", ParentBoneId = null, Position = Vector3.zero });

            Assert.AreEqual(0, SkeletonDisplay.BuildBoneLines(skeleton).Count);
        }

        [Test]
        public void BuildBoneLines_TwoBoneChain_OneLineFromParentToChild()
        {
            var skeleton = new CreatureSkeleton.Skeleton();
            skeleton.Bones.Add(new CreatureSkeleton.Bone { Id = "root", ParentBoneId = null, Position = new Vector3(0f, 0f, 0f) });
            skeleton.Bones.Add(new CreatureSkeleton.Bone { Id = "leg", ParentBoneId = "root", Position = new Vector3(0f, -1f, 0f) });

            List<SkeletonDisplay.BoneLine> lines = SkeletonDisplay.BuildBoneLines(skeleton);
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(Vector3.zero, lines[0].Start, "Line starts at the parent bone.");
            Assert.AreEqual(new Vector3(0f, -1f, 0f), lines[0].End, "Line ends at the bone itself.");
        }

        [Test]
        public void BuildBoneLines_MissingParent_Skipped()
        {
            var skeleton = new CreatureSkeleton.Skeleton();
            skeleton.Bones.Add(new CreatureSkeleton.Bone { Id = "orphan", ParentBoneId = "missing", Position = Vector3.one });

            Assert.AreEqual(0, SkeletonDisplay.BuildBoneLines(skeleton).Count);
        }

        [Test]
        public void BuildJointPoints_ReturnsEveryBonePosition()
        {
            var skeleton = new CreatureSkeleton.Skeleton();
            skeleton.Bones.Add(new CreatureSkeleton.Bone { Id = "a", ParentBoneId = null, Position = new Vector3(1f, 2f, 3f) });
            skeleton.Bones.Add(new CreatureSkeleton.Bone { Id = "b", ParentBoneId = "a", Position = new Vector3(4f, 5f, 6f) });

            List<Vector3> points = SkeletonDisplay.BuildJointPoints(skeleton);
            Assert.AreEqual(2, points.Count);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), points[0]);
            Assert.AreEqual(new Vector3(4f, 5f, 6f), points[1]);
        }
    }
}
