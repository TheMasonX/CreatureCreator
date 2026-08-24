using System;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// The mesh-asset geometry source on a CreaturePart (CC-031 pass 1). When
    /// present, the part's geometry is a pre-authored mesh referenced by stable
    /// key instead of the implicit SDF field. <see cref="Limb"/> and MeshGeometry
    /// are mutually exclusive geometry sources (validator-enforced).
    ///
    /// DNA never stores a UnityEngine.Object reference. MeshAssetKey is a stable
    /// name resolved through an external mesh palette/registry at generation time
    /// (the convention CC-028 establishes for material keys). Resolution is a
    /// generator-layer concern (injected resolver); the domain model stays
    /// portable.
    /// </summary>
    [Serializable]
    public sealed class MeshGeometry
    {
        public string MeshAssetKey;

        public GeometryAttachment Attachment = new GeometryAttachment();

        public MeshGeometry Clone()
        {
            return new MeshGeometry
            {
                MeshAssetKey = MeshAssetKey,
                Attachment = Attachment == null ? null : Attachment.Clone(),
            };
        }
    }
}
