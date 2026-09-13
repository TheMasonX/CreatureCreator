using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProceduralCreature.Animation.Binding;
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
    /// Resolved runtime correspondence such as the skeleton and welded-surface
    /// influence domains is carried forward so downstream assembly/binding does not
    /// reinterpret or re-infer the definition.
    /// </summary>
    public sealed class GeneratedCreatureData
    {
        private readonly IReadOnlyList<Color> _colors;
        private readonly IReadOnlyList<InfluenceDomain> _vertexInfluenceDomains;

        public GeneratedCreatureData(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            Color[] colors,
            MeshTopologyReport topologyReport)
            : this(definition, snapshot, meshResult, colors, topologyReport, null, null)
        {
        }

        public GeneratedCreatureData(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            Color[] colors,
            MeshTopologyReport topologyReport,
            SkeletonSnapshot skeletonSnapshot,
            InfluenceDomain[] vertexInfluenceDomains)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (meshResult == null) throw new ArgumentNullException(nameof(meshResult));
            if (colors == null) throw new ArgumentNullException(nameof(colors));
            if (topologyReport == null) throw new ArgumentNullException(nameof(topologyReport));

            if (vertexInfluenceDomains != null && vertexInfluenceDomains.Length != meshResult.Positions.Count)
            {
                throw new ArgumentException(
                    "vertexInfluenceDomains must have one entry per generated mesh vertex.",
                    nameof(vertexInfluenceDomains));
            }

            Definition = definition.Clone();
            Snapshot = snapshot;
            MeshResult = meshResult;
            _colors = new ReadOnlyCollection<Color>((Color[])colors.Clone());
            TopologyReport = topologyReport;
            SkeletonSnapshot = skeletonSnapshot;
            _vertexInfluenceDomains = vertexInfluenceDomains == null
                ? null
                : new ReadOnlyCollection<InfluenceDomain>((InfluenceDomain[])vertexInfluenceDomains.Clone());
        }

        public CreatureDefinition Definition { get; }
        public ResolvedCreatureSnapshot Snapshot { get; }
        public MeshExtractionResult MeshResult { get; }
        public IReadOnlyList<Color> Colors => _colors;
        public MeshTopologyReport TopologyReport { get; }
        public SkeletonSnapshot SkeletonSnapshot { get; }
        public IReadOnlyList<InfluenceDomain> VertexInfluenceDomains => _vertexInfluenceDomains;
    }
}