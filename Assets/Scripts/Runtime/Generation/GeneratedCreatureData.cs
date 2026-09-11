using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Definition;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Immutable handoff between pure generation and Unity assembly.
    /// Mutable definition/array inputs supplied by a producer are defensively copied
    /// so consumers cannot mutate the generated result through this object.
    /// Resolved runtime correspondence such as the skeleton snapshot is carried
    /// forward so downstream assembly does not reinterpret or re-infer the definition.
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
            : this(definition, snapshot, meshResult, colors, topologyReport, null)
        {
        }

        public GeneratedCreatureData(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            Color[] colors,
            MeshTopologyReport topologyReport,
            SkeletonSnapshot skeletonSnapshot)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (meshResult == null) throw new ArgumentNullException(nameof(meshResult));
            if (colors == null) throw new ArgumentNullException(nameof(colors));
            if (topologyReport == null) throw new ArgumentNullException(nameof(topologyReport));

            Definition = definition.Clone();
            Snapshot = snapshot;
            MeshResult = meshResult;
            _colors = new ReadOnlyCollection<Color>((Color[])colors.Clone());
            TopologyReport = topologyReport;
            SkeletonSnapshot = skeletonSnapshot;
        }

        public CreatureDefinition Definition { get; }
        public ResolvedCreatureSnapshot Snapshot { get; }
        public MeshExtractionResult MeshResult { get; }
        public IReadOnlyList<Color> Colors => _colors;
        public MeshTopologyReport TopologyReport { get; }
        public SkeletonSnapshot SkeletonSnapshot { get; }
    }
}