using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Pure limb-authoring helpers for the CC-018 editor slice (Phase 7). These
    /// are the same "pure math, no UnityEditor API" pattern as BodyEditSolver /
    /// BodySplineAuthoring: they mutate a LimbChain (or compute geometry from one)
    /// without touching the window, undo, or serialization, so they are
    /// EditMode-testable.
    ///
    /// Authoring rules enforced here, matching the schema:
    /// - A default chain is seeded for the limb-chain part types (Limb/Leg/Arm)
    ///   only; Foot/Hand are attachment parts, not chains.
    /// - Joint IDs must strictly increase along the chain (validator's
    ///   LimbJointOrderNotDeterministic), so appended joints get
    ///   <c>max(existing) + 1</c>.
    /// - The root joint stays at the local origin (Joints[0] ≈ zero invariant);
    ///   resizing never removes the root and appended joints extend downward.
    /// - A child's LOCAL SPACE is the parent's TERMINAL joint when the parent is
    ///   a limb (see CreaturePartWorldTransformResolver): a child authored at
    ///   (0,0,0) sits at the limb's tip. No per-child placement override is
    ///   needed — the resolver owns the child frame.
    /// </summary>
    public static class LimbAuthoring
    {
        /// <summary>
        /// Part types whose geometry is a joint chain (seeded on create/type
        /// change). Foot and Hand are terminal attachment parts authored against
        /// their parent limb's terminal joint, not standalone chains.
        /// </summary>
        public static bool IsLimbChainType(PartType type)
        {
            return type == PartType.Limb || type == PartType.Leg || type == PartType.Arm;
        }

        /// <summary>
        /// Reconciles a part's limb state with its PartType. When the new type is a
        /// limb-chain type, seed a default chain if needed; when it is not, clear any
        /// stale chain so the part falls back to its Shape-based geometry.
        /// </summary>
        public static void ApplyLimbStateForTypeChange(CreaturePart part, PartType newType)
        {
            if (part == null) return;

            if (IsLimbChainType(newType))
            {
                if (part.Limb == null)
                {
                    part.Limb = DefaultLimbChainForType(newType);
                }
                return;
            }

            part.Limb = null;
        }

        /// <summary>
        /// The default chain to seed for a limb-chain part type, or null for
        /// non-chain types. All chain types currently share the straight-down
        /// default (root at origin, terminal at local -Y, tapering thickness).
        /// </summary>
        public static LimbChain DefaultLimbChainForType(PartType type)
        {
            return IsLimbChainType(type) ? LimbChain.CreateDefault() : null;
        }

        /// <summary>The next unique joint id: max existing + 1 (or 1 for an empty chain).</summary>
        public static uint NextLimbJointId(LimbChain chain)
        {
            if (chain == null || chain.Joints == null) return 1u;
            uint maxId = 0u;
            for (int i = 0; i < chain.Joints.Count; i++)
            {
                LimbJoint joint = chain.Joints[i];
                if (joint != null && joint.Id > maxId) maxId = joint.Id;
            }
            return maxId + 1u;
        }

        /// <summary>
        /// Resizes a limb chain to <paramref name="newCount"/> joints, clamped to
        /// the validator's [Min, Max] range. Shrinking removes from the tail (the
        /// root is never removed); growing appends joints extending downward from
        /// the current tail (default authoring direction), each with a fresh
        /// increasing id.
        /// </summary>
        public static void ResizeLimbChain(LimbChain chain, int newCount)
        {
            if (chain == null || chain.Joints == null) return;

            int min = GenerationTolerances.MinLimbJointCount;
            int max = GenerationTolerances.MaxLimbJointCount;
            newCount = Mathf.Clamp(newCount, min, max);

            while (chain.Joints.Count > newCount && chain.Joints.Count > min)
            {
                chain.Joints.RemoveAt(chain.Joints.Count - 1);
            }
            while (chain.Joints.Count < newCount)
            {
                Vector3 tail = chain.Joints[chain.Joints.Count - 1].Position;
                chain.Joints.Add(new LimbJoint
                {
                    Id = NextLimbJointId(chain),
                    Position = tail + new Vector3(0f, -0.25f, 0f),
                });
            }
        }

        /// <summary>
        /// Clamps a joint's LOCAL position into the creature bounds, honoring the
        /// root-at-origin invariant (index 0 always clamps to the origin).
        /// </summary>
        public static Vector3 ClampJointToBounds(Vector3 localPosition, int jointIndex, BoundsDefinition bounds)
        {
            if (jointIndex == 0) return Vector3.zero;
            return new Vector3(
                Mathf.Clamp(localPosition.x, -bounds.MaxX, bounds.MaxX),
                Mathf.Clamp(localPosition.y, -bounds.MaxY, bounds.MaxY),
                Mathf.Clamp(localPosition.z, -bounds.MaxZ, bounds.MaxZ));
        }

        /// <summary>
        /// The creature-space position of a joint through the part's resolved
        /// world matrix (the same transform the SDF and skeleton use).
        /// </summary>
        public static Vector3 WorldJointPosition(Matrix4x4 partWorldMatrix, Vector3 localPosition)
        {
            return partWorldMatrix.MultiplyPoint3x4(localPosition);
        }

        /// <summary>
        /// The part-local position of a world-space point (inverse of
        /// <see cref="WorldJointPosition"/>).
        /// </summary>
        public static Vector3 LocalJointPosition(Matrix4x4 partWorldMatrix, Vector3 worldPosition)
        {
            return partWorldMatrix.inverse.MultiplyPoint3x4(worldPosition);
        }
    }
}
