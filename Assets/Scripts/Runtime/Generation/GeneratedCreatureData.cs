using ProceduralCreature.Morphology.Extraction;

namespace ProceduralCreature.Generation
{
    public sealed class GeneratedCreatureData
    {
        public GeneratedCreatureData(
            ProceduralCreature.Definition.CreatureDefinition definition,
            MeshExtractionResult meshResult,
            UnityEngine.Color[] colors,
            MeshTopologyReport topologyReport)
        {
            Definition = definition;
            MeshResult = meshResult;
            Colors = colors;
            TopologyReport = topologyReport;
        }

        public ProceduralCreature.Definition.CreatureDefinition Definition { get; }
        public MeshExtractionResult MeshResult { get; }
        public UnityEngine.Color[] Colors { get; }
        public MeshTopologyReport TopologyReport { get; }
    }
}