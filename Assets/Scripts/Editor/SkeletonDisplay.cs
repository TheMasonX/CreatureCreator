using System.Collections.Generic;
using UnityEngine;
// Alias: the namespace and its own Skeleton type share the same identifier.
using CreatureSkeleton = ProceduralCreature.Skeleton;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// CC-066: pure, testable view data for the skeleton display mode. Converts an
    /// inferred rest <see cref="Skeleton"/> into draw primitives (bone lines +
    /// joint points) without touching the SceneView. The editor window renders
    /// these read-only in OnSceneGUI; this class never mutates DNA and has no
    /// Unity Editor dependencies beyond UnityEngine math types, so EditMode tests
    /// cover every computation the overlay renders.
    /// </summary>
    public static class SkeletonDisplay
    {
        /// <summary>A single bone segment: from the parent bone's position to this bone's position (creature space).</summary>
        public readonly struct BoneLine
        {
            public readonly Vector3 Start;
            public readonly Vector3 End;

            public BoneLine(Vector3 start, Vector3 end)
            {
                Start = start;
                End = end;
            }
        }

        /// <summary>
        /// Emits explicit body/limb segments and parent links for ordinary part
        /// bones. Internal links already represented by an explicit segment are
        /// emitted once; attachment links remain visible.
        /// Deterministic order: skeleton bone order.
        /// </summary>
        public static List<BoneLine> BuildBoneLines(CreatureSkeleton.Skeleton skeleton)
        {
            var lines = new List<BoneLine>();
            if (skeleton == null) return lines;

            foreach (CreatureSkeleton.Bone bone in skeleton.Bones)
            {
                if (bone == null) continue;

                if (bone.HasSegment)
                {
                    lines.Add(new BoneLine(bone.Position, bone.EndPosition));
                }

                if (bone.ParentBoneId == null) continue;
                CreatureSkeleton.Bone parent = skeleton.FindBone(bone.ParentBoneId);
                if (parent == null) continue;
                if (parent.HasSegment
                    && (parent.EndPosition - bone.Position).sqrMagnitude <= 1e-8f)
                {
                    continue;
                }
                Vector3 attachmentPosition = parent.HasChildAttachmentPosition
                    ? parent.ChildAttachmentPosition
                    : parent.Position;
                lines.Add(new BoneLine(attachmentPosition, bone.Position));
            }
            return lines;
        }

        /// <summary>
        /// Creature-space joint points to draw as caps — one per bone, in skeleton
        /// bone order. Read-only view data.
        /// </summary>
        public static List<Vector3> BuildJointPoints(CreatureSkeleton.Skeleton skeleton)
        {
            var points = new List<Vector3>();
            if (skeleton == null) return points;

            foreach (CreatureSkeleton.Bone bone in skeleton.Bones)
            {
                if (bone != null) points.Add(bone.Position);
            }
            return points;
        }
    }
}
