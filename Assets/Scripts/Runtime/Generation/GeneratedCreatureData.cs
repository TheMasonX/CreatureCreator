using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Generation
{
    public sealed class GeneratedCreatureData
    {
        public GeneratedCreatureData(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            UnityEngine.Color[] colors,
            MeshTopologyReport topologyReport)
        {
            Definition = definition;
            Snapshot = snapshot;
            MeshResult = meshResult;
            Colors = colors;
            TopologyReport = topologyReport;
        }

        public CreatureDefinition Definition { get; }
        public ResolvedCreatureSnapshot Snapshot { get; }
        public MeshExtractionResult MeshResult { get; }
        public UnityEngine.Color[] Colors { get; }
        public MeshTopologyReport TopologyReport { get; }
    }
}