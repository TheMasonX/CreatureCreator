using ProceduralCreature.Appearance;
using ProceduralCreature.Morphology.Sdf;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Shared runtime/editor generation defaults and palette references. Transient
    /// requests such as preview quality may override these values per generation.
    /// </summary>
    [CreateAssetMenu(menuName = "Procedural Creature/Generation Config", fileName = "CreatureGenerationConfig")]
    public sealed class CreatureGenerationConfig : ScriptableObject
    {
        [SerializeField] private float defaultVoxelsPerUnit = 16f;
        [SerializeField] private SdfCullingMode cullingMode = SdfCullingMode.Exact;
        [SerializeField] private CreatureMeshPalette meshPalette;
        [SerializeField] private CreatureMaterialPalette materialPalette;

        public float DefaultVoxelsPerUnit => Mathf.Max(1f, defaultVoxelsPerUnit);
        public SdfCullingMode CullingMode => cullingMode;
        public CreatureMeshPalette MeshPalette => meshPalette;
        public CreatureMaterialPalette MaterialPalette => materialPalette;
    }
}
