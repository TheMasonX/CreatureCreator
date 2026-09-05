using System;
using System.Linq;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Appearance;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Generates a creature's geometry (CC-031). The output is a
    /// <see cref="GeneratedCreature"/> — a deterministic, ordered collection of
    /// geometry items — rather than a single Mesh. Item 0 is always the implicit
    /// combined surface (Body + Shape/Limb parts) extracted from the SDF field;
    /// mesh-asset parts follow in ascending SourcePartId order, placed at each
    /// part's local-space position via its GeometryAttachment (pass 1, ADR-002 §2).
    ///
    /// Mesh asset keys are resolved through the injected
    /// <paramref name="meshResolver"/>; a mesh part whose key cannot be resolved is
    /// a programmer/config error and throws DomainException (no silent drop). The
    /// domain model never stores UnityEngine.Object references.
    /// </summary>
    public static class CreatureMeshGenerator
    {
        /// <summary>
        /// The creature-space reflection across the X = 0 plane, matching the
        /// convention SkeletonInferrer uses for mirrored bones and the SDF
        /// compiler's mirrored limb chains — so mirrored mesh-asset geometry lands
        /// on the same side as the mirrored implicit field.
        /// </summary>
        public static GeneratedCreature Generate(CreatureDefinition definition, out MeshTopologyReport topologyReport, GenerationDiagnostics diagnostics = null)
        {
            return Generate(definition, out topologyReport, diagnostics, meshResolver: null);
        }

        public static GeneratedCreature Generate(
            CreatureDefinition definition,
            out MeshTopologyReport topologyReport,
            GenerationDiagnostics diagnostics,
            Func<string, Mesh> meshResolver = null)
        {
            GeneratedCreatureData data = GenerateData(definition, diagnostics);
            topologyReport = data.TopologyReport;
            return Assemble(data, meshResolver);
        }

        public static GeneratedCreatureData GenerateData(
            CreatureDefinition definition,
            GenerationDiagnostics diagnostics = null)
        {
            // CC-091: generation is a concrete sequence of separately owned stages.
            // Each stage consumes the resolved/generated data produced by the previous
            // one and owns any native buffer it allocates. ValidateAndResolve is the
            // single authority that turns authored DNA into the one resolved snapshot
            // used by every downstream stage; no stage re-derives morphology from raw
            // DNA.
            ResolvedCreatureSnapshot snapshot = ValidateAndResolve(definition, diagnostics);

            // Stage 1 — compile the portable SDF program and sample it over the grid.
            // The returned DensityGrid owns its native sample buffer; that ownership
            // transfers to ExtractMesh, which releases it.
            DensityGrid grid = GenerateImplicitField(definition, snapshot, diagnostics);

            // Stage 2 — extract the implicit surface mesh from the sampled grid.
            MeshExtractionResult meshResult = ExtractMesh(grid, diagnostics);

            // Stage 3 — validate the extracted mesh topology (watertight/manifold).
            MeshTopologyReport generatedTopologyReport = ValidateMesh(meshResult, diagnostics);

            // Stage 4 — bake per-vertex appearance colors from resolved part data.
            Color[] colors = BakeAppearance(definition, snapshot, meshResult, diagnostics);

            return new GeneratedCreatureData(definition, snapshot, meshResult, colors, generatedTopologyReport);
        }

        private static ResolvedCreatureSnapshot ValidateAndResolve(
            CreatureDefinition definition,
            GenerationDiagnostics diagnostics)
        {
            if (definition == null) throw new DomainException("definition must not be null.");

            ValidationResult validation = DefinitionValidator.Validate(definition);
            diagnostics?.RecordIssues(validation.Issues);
            if (!validation.IsValid)
            {
                diagnostics?.MarkFailed(GenerationStage.Validation);
                throw new DomainException("CreatureDefinition is invalid and cannot be generated.");
            }

            return ResolvedCreatureSnapshot.Resolve(definition);
        }

        /// <summary>
        /// Stage: compile the creature's portable SDF program and sample it over the
        /// resolved density grid. The compiled program is transient — it is disposed
        /// immediately after sampling. The returned <see cref="DensityGrid"/> owns its
        /// native sample buffer; ownership transfers to <see cref="ExtractMesh"/>,
        /// which is responsible for releasing it.
        /// </summary>
        private static DensityGrid GenerateImplicitField(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            GenerationDiagnostics diagnostics)
        {
            SdfProgram portableProgram = null;
            Time(diagnostics, GenerationStage.SdfCompile, () =>
            {
                portableProgram = SdfProgramBuilder.CompilePortable(definition, snapshot);
            });

            DensityGrid grid = null;
            Time(diagnostics, GenerationStage.FieldSampling,
                () =>
                {
                    try
                    {
                        grid = DensityGrid.SamplePortable(portableProgram, snapshot.Bounds, snapshot.Generation);
                    }
                    finally
                    {
                        portableProgram.Dispose();
                        portableProgram = null;
                    }
                });
            diagnostics?.RecordGridDimensions(grid.CellsX, grid.CellsY, grid.CellsZ, grid.SampleCount);
            return grid;
        }

        /// <summary>
        /// Stage: extract the implicit surface mesh from the sampled grid. This stage
        /// takes ownership of <paramref name="grid"/> and releases its native sample
        /// buffer after extraction — validation, appearance, and assembly consume the
        /// plain-data <see cref="MeshExtractionResult"/>, so the grid is no longer
        /// needed. It is disposed even when extraction throws so the Persistent
        /// allocation cannot leak.
        /// </summary>
        private static MeshExtractionResult ExtractMesh(DensityGrid grid, GenerationDiagnostics diagnostics)
        {
            MeshExtractionResult meshResult = null;
            try
            {
                Time(diagnostics, GenerationStage.MeshExtraction,
                    () => meshResult = MarchingCubesExtractor.Extract(
                        grid, diagnostics?.CollectTimings == true));
                diagnostics?.RecordExtractionStatistics(
                    meshResult.MixedCellCount, meshResult.GradientEvaluationCount);
                diagnostics?.RecordMeshStatistics(
                    meshResult.Positions.Count, meshResult.TriangleCount);
                diagnostics?.RecordExtractionTiming(
                    meshResult.ActiveCellConstructionTime,
                    meshResult.ContourResolutionTime,
                    meshResult.VertexWeldingTime,
                    meshResult.TriangleEmissionTime);
            }
            finally
            {
                if (grid != null)
                {
                    grid.Dispose();
                    grid = null;
                }
            }
            return meshResult;
        }

        /// <summary>
        /// Stage: validate the extracted mesh topology. Consumes only the plain-data
        /// <see cref="MeshExtractionResult"/>; it owns no native resources.
        /// </summary>
        private static MeshTopologyReport ValidateMesh(
            MeshExtractionResult meshResult,
            GenerationDiagnostics diagnostics)
        {
            MeshTopologyReport generatedTopologyReport = null;
            Time(diagnostics, GenerationStage.MeshValidation,
                () => generatedTopologyReport = MeshTopologyValidator.Validate(meshResult));
            return generatedTopologyReport;
        }

        /// <summary>
        /// Stage: bake per-vertex colors from resolved part appearance. Compiles the
        /// individual-part and Body appearance programs from the resolved snapshot,
        /// bakes against the extracted mesh, and disposes those programs. It never
        /// re-derives morphology from raw DNA; it only builds the appearance programs
        /// this stage itself needs.
        /// </summary>
        private static Color[] BakeAppearance(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            GenerationDiagnostics diagnostics)
        {
            Color[] colors = null;
            var compiledParts = SdfProgramBuilder.CompileIndividualPartsPortable(definition, snapshot);
            SdfProgram bodyProgram = SdfProgramBuilder.CompilePortableBodyField(definition, snapshot);
            try
            {
                Time(diagnostics, GenerationStage.AppearanceBake,
                    () => colors = AppearanceBaker.Bake(
                        definition, meshResult, null, compiledParts, bodyProgram, snapshot.Body, snapshot));
            }
            finally
            {
                foreach (ResolvedPartProgram partProgram in compiledParts) partProgram.Program.Dispose();
                bodyProgram.Dispose();
            }
            return colors;
        }

        public static GeneratedCreature Assemble(GeneratedCreatureData data, Func<string, Mesh> meshResolver = null)
        {
            if (data == null) throw new DomainException("generation data must not be null.");

            Mesh mesh = data.MeshResult.ToUnityMesh();
            mesh.SetColors(data.Colors);

            var generated = new GeneratedCreature();
            generated.Geometry.Add(new GeometryItem
            {
                SourcePartId = GeneratedCreature.ImplicitSurfaceSourceId,
                GeometryType = GeometryType.Implicit,
                Mesh = mesh,
                RigBinding = new RigBindingMetadata(),
            });

            // Items 1..n: mesh-asset parts, resolved and placed from the snapshot.
            AppendMeshAssetItems(generated, data, meshResolver);

            return generated;
        }

        /// <summary>
        /// Stage: place mesh-asset parts into the generated creature. Mesh-asset parts
        /// are ordered by SourcePartId for deterministic output independent of
        /// authoring order; each source mesh is resolved through the injected resolver
        /// and placed at its captured creature-space transform, mirroring when the
        /// part is mirrored. Placement comes from the resolved snapshot, never raw
        /// DNA.
        /// </summary>
        private static void AppendMeshAssetItems(
            GeneratedCreature generated,
            GeneratedCreatureData data,
            Func<string, Mesh> meshResolver)
        {
            var meshParts = data.Snapshot.PartsById.Values
                .Where(p => p.HasMeshGeometry)
                .OrderBy(p => p.Id, StringComparer.Ordinal);

            foreach (ResolvedPartSnapshot resolvedPart in meshParts)
            {
                Mesh sourceMesh = ResolveMesh(resolvedPart.Id, resolvedPart.MeshAssetKey, meshResolver);
                Matrix4x4 placement = resolvedPart.GeometryPlacementToCreatureSpace;

                generated.Geometry.Add(BuildMeshAssetItem(resolvedPart, sourceMesh, placement, mirror: false));

                if (resolvedPart.MirrorAcrossSymmetryPlane && data.Snapshot.SymmetryMode != SymmetryMode.None)
                {
                    generated.Geometry.Add(BuildMeshAssetItem(resolvedPart, sourceMesh,
                        MirrorUtility.ReflectTransformAcrossX(placement), mirror: true));
                }
            }
        }

        private static Mesh ResolveMesh(string partId, string meshAssetKey, Func<string, Mesh> meshResolver)
        {
            if (meshResolver == null)
            {
                throw new DomainException(
                    $"Part '{partId}' declares mesh geometry ('{meshAssetKey}') " +
                    "but no mesh resolver was provided.");
            }
            Mesh resolved = meshResolver(meshAssetKey);
            if (resolved == null)
            {
                throw new DomainException(
                    $"Mesh asset '{meshAssetKey}' for part '{partId}' could not be resolved.");
            }
            return resolved;
        }

        /// <summary>
        /// Bakes a mesh-asset item's final creature-space placement into a new Mesh
        /// (pass-1 simplification: consumers assign the mesh at identity). Submesh
        /// structure is preserved and normals are recomputed so the transformed mesh
        /// shades correctly. The source mesh asset is never mutated. Mirrored items
        /// reuse the same source mesh with a reflected placement.
        /// </summary>
        private static GeometryItem BuildMeshAssetItem(ResolvedPartSnapshot part, Mesh source, Matrix4x4 placement, bool mirror)
        {
            Vector3[] positions = source.vertices;
            Vector3[] transformed = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                transformed[i] = placement.MultiplyPoint3x4(positions[i]);
            }

            var mesh = new Mesh
            {
                name = $"Generated_{part.Id}{(mirror ? GeneratedCreature.MirrorSuffix : string.Empty)}",
            };
            mesh.SetVertices(transformed);

            if (source.subMeshCount > 1)
            {
                mesh.subMeshCount = source.subMeshCount;
                for (int s = 0; s < source.subMeshCount; s++)
                {
                    mesh.SetTriangles(CopyTriangles(source.GetTriangles(s), mirror), s);
                }
            }
            else
            {
                mesh.SetTriangles(CopyTriangles(source.triangles, mirror), 0);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Bake the part's own authored appearance onto the item (CC-031 pass-2
            // vertex-color parity with the implicit surface). A mesh-asset part is
            // not part of the implicit SDF field, so AppearanceBaker.BakePart
            // resolves its color from the part itself rather than nearest-surface
            // sampling — never from the Body's implicit gradient.
            mesh.SetColors(AppearanceBaker.BakePart(part.Appearance, mesh.vertices, mesh.normals));

            var item = new GeometryItem
            {
                SourcePartId = mirror ? part.Id + GeneratedCreature.MirrorSuffix : part.Id,
                GeometryType = GeometryType.MeshAsset,
                Mesh = mesh,
                    SourceMesh = source,
                    RestPlacement = placement,
                    RigBinding = new RigBindingMetadata
                    {
                        SourcePartId = part.Id,
                        ParentPartId = part.ParentId,
                        IsMirrored = mirror,
                    },
            };

            // CC-028: a part with a submaterial override carries it as a key on its
            // geometry item. V1 emits one region covering submesh 0 — the whole item
            // in the common single-material case. Resolution of the key to a
            // UnityEngine.Material is a render-layer concern (MaterialResolver), so
            // the generator output stays key-only and the domain stays portable.
            // The implicit combined item (item 0) deliberately gets no regions — the
            // single-mesh vertex-color bake remains the default path (CC-028 scope).
            if (!string.IsNullOrWhiteSpace(part.Appearance.MaterialKey))
            {
                item.MaterialRegions.Add(new MaterialRegion
                {
                    StartIndex = 0,
                    IndexCount = mesh.triangles.Length,
                    MaterialKey = part.Appearance.MaterialKey,
                });
            }

            return item;
        }

        private static int[] CopyTriangles(int[] triangles, bool reverseWinding)
        {
            var copy = (int[])triangles.Clone();
            if (!reverseWinding) return copy;

            for (int i = 0; i < copy.Length; i += 3)
            {
                int first = copy[i];
                copy[i] = copy[i + 2];
                copy[i + 2] = first;
            }
            return copy;
        }

        private static void Time(GenerationDiagnostics diagnostics, GenerationStage stage, System.Action action)
        {
            if (diagnostics == null)
            {
                action();
                return;
            }
            diagnostics.TimeStage(stage, action);
        }
    }
}
