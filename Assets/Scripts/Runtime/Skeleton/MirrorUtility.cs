using UnityEngine;

namespace ProceduralCreature.Skeleton
{
    /// <summary>
    /// Mirrors a full rigid (or near-rigid) transform across the creature-space
    /// X = 0 plane, matching the convention SymmetryNode uses at the SDF layer
    /// (Morphology/Sdf/SymmetryNode.cs) — both must agree on which plane "mirror"
    /// means, or geometry and skeleton would disagree about where the mirrored
    /// side is.
    ///
    /// DERIVATION: mirroring isn't just negating the position's X component — a
    /// bone's ROTATION must also be correctly reflected, or a mirrored limb would
    /// point the wrong way. The mathematically correct way to mirror an affine
    /// transform matrix M across a plane with reflection matrix S (here,
    /// S = diag(-1,1,1,1)) is the conjugation S·M·S. This works because:
    /// (a) S is its own inverse (S·S = I), so conjugating by S is a true
    /// reflection, not an arbitrary transform; (b) conjugating a rotation by an
    /// orthogonal matrix preserves determinant when applied twice
    /// (det(S·R·S) = det(S)²·det(R) = det(R) since det(S) = -1), so the result is
    /// still a proper rotation (no accidental improper/left-handed artifact) —
    /// this is what lets Bone construction safely extract Matrix4x4.rotation from
    /// the mirrored matrix without special-casing handedness.
    /// </summary>
    public static class MirrorUtility
    {
        private static readonly Matrix4x4 ReflectAcrossX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        public static Matrix4x4 MirrorAcrossXPlane(Matrix4x4 localToCreatureSpace)
        {
            return ReflectAcrossX * localToCreatureSpace * ReflectAcrossX;
        }
    }
}
