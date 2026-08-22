using System;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// A single semantic part within a CreatureDefinition. Immutable-by-convention:
    /// callers should treat instances as value snapshots and replace them wholesale
    /// through CreatureDefinition's mutation helpers rather than mutating fields on a
    /// part already stored in a definition's Parts list, so the "one mutation path"
    /// rule (implementation guide §16) has a single enforcement point.
    /// </summary>
    [Serializable]
    public sealed class CreaturePart
    {
        /// <summary>Stable identifier. Never derived from list position (§2.2).</summary>
        public string Id;

        /// <summary>Null for the implicit/explicit root part.</summary>
        public string ParentId;

        public PartType PartType;

        /// <summary>Local transform relative to ParentId (or creature origin if root).</summary>
        public TransformData Transform;

        public ShapeDefinition Shape;

        public AppearanceDefinition Appearance;

        /// <summary>
        /// Whether the SDF compiler and skeleton inferer should generate a mirrored
        /// counterpart for this part when the owning CreatureDefinition's
        /// SymmetryMode is not None. See SymmetryMode.cs for the storage-model
        /// decision this flag depends on.
        /// </summary>
        public bool MirrorAcrossSymmetryPlane;

        public CreaturePart Clone()
        {
            return new CreaturePart
            {
                Id = Id,
                ParentId = ParentId,
                PartType = PartType,
                Transform = Transform,
                Shape = Shape,
                Appearance = Appearance,
                MirrorAcrossSymmetryPlane = MirrorAcrossSymmetryPlane,
            };
        }

        /// <summary>
        /// Returns a clone with a freshly generated ID. Used for duplication (§2.2:
        /// "When duplicating a part, generate a new ID rather than copying the
        /// original.").
        /// </summary>
        public CreaturePart CloneAsDuplicate()
        {
            CreaturePart clone = Clone();
            clone.Id = PartIdGenerator.CreateNew();
            return clone;
        }
    }
}
