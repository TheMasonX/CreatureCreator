using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Definition;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Immutable handoff between pure generation and Unity assembly.
    /// Mutable arrays supplied by a producer are defensively copied so consumers
    /// cannot mutate the generated result through this object.
    /// </summary>
    public sealed class GeneratedCreatureData
    {
        private readonly IReadOnlyList<Color> _colors;

        public GeneratedCreatureData(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            Color[] colors,
            MeshTopologyReport topologyReport)
        {
            if (colors == null) throw new DomainException("colors must not be null.");

            Definition = definition;
            Snapshot = snapshot;
            MeshResult = meshResult;
            _colors = new ReadOnlyCollection<Color>((Color[])colors.Clone());
            TopologyReport = topologyReport;
        }

        public CreatureDefinition Definition { get; }
        public ResolvedCreatureSnapshot Snapshot { get; }
        public MeshExtractionResult MeshResult { get; }
        public IReadOnlyList<Color> Colors => _colors;
        public MeshTopologyReport TopologyReport { get; }
    }
}