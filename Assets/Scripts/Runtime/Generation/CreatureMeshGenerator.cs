using System;
using System.Collections.Generic;
using System.Linq;
using ProceduralCreature.Animation.Binding;
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
            ResolvedCreatureSnapshot snapshot = ValidateAndResolve(definition, diagnostics);
            SkeletonSnapshot skeletonSnapshot = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(snapshot));

            List<ResolvedPartProgram> compiledParts = null;
            SdfProgram bodyProgram = null;
            try
            {
                compiledParts = SdfProgramBuilder.CompileIndividualPartsPortable(definition, snapshot);
                bodyProgram = SdfProgramBuilder.CompilePortableBodyField(definition, snapshot);

                DensityGrid grid = GenerateImplicitField(definition, snapshot, diagnostics);
                MeshExtractionResult meshResult = ExtractMesh(grid, diagnostics);
                MeshTopologyReport generatedTopologyReport = ValidateMesh(meshResult, diagnostics);
                Color[] colors = BakeAppearance(
                    definition, snapshot, meshResult, compiledParts, bodyProgram, diagnostics);
                InfluenceDomain[] vertexInfluenceDomains =
                    ResolveInfluenceDomains(snapshot, meshResult, compiledParts, bodyProgram);

                return new GeneratedCreatureData(
                    definition,
                    snapshot,
                    meshResult,
                    colors,
                    generatedTopologyReport,
                    skeletonSnapshot,
                    vertexInfluenceDomains);
            }
            finally
            {
                if (compiledParts != null)
                {
                    foreach (ResolvedPartProgram partProgram in compiledParts)
                    {
                        partProgram.Program?.Dispose();
                    }
                }
                bodyProgram?.Dispose();
            }
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
                        portableProgram?.Dispose();
                        portableProgram = null;
                    }
                });
            diagnostics?.RecordGridDimensions(grid.CellsX, grid.CellsY, grid.CellsZ, grid.SampleCount);
            return grid;
        }

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

        private static MeshTopologyReport ValidateMesh(
            MeshExtractionResult meshResult,
            GenerationDiagnostics diagnostics)
        {
            MeshTopologyReport generatedTopologyReport = null;
            Time(diagnostics, GenerationStage.MeshValidation,
                () => generatedTopologyReport = MeshTopologyValidator.Validate(meshResult));
            return generatedTopologyReport;
        }

        private static Color[] BakeAppearance(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            GenerationDiagnostics diagnostics)
        {
            List<ResolvedPartProgram> compiledParts = SdfProgramBuilder.CompileIndividualPartsPortable(definition, snapshot);
            SdfProgram bodyProgram = SdfProgramBuilder.CompilePortableBodyField(definition, snapshot);
            try
            {
                return BakeAppearance(
                    definition, snapshot, meshResult, compiledParts, bodyProgram, diagnostics);
            }
            finally
            {
                foreach (ResolvedPartProgram partProgram in compiledParts)
                {
                    partProgram.Program?.Dispose();
                }
                bodyProgram?.Dispose();
            }
        }

        private static Color[] BakeAppearance(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            IReadOnlyList<ResolvedPartProgram> compiledParts,
            SdfProgram bodyProgram,
            GenerationDiagnostics diagnostics)
        {
            Color[] colors = null;
            Time(diagnostics, GenerationStage.AppearanceBake,
                () => colors = AppearanceBaker.Bake(
                    definition,
                    meshResult,
                    null,
                    compiledParts as List<ResolvedPartProgram>,
                    bodyProgram,
                    snapshot.Body,
                    snapshot));
            return colors;
        }

        private static InfluenceDomain[] ResolveInfluenceDomains(
            ResolvedCreatureSnapshot snapshot,
            MeshExtractionResult meshResult,
            IReadOnlyList<ResolvedPartProgram> compiledParts,
            SdfProgram bodyProgram)
        {
            return ImplicitSurfaceInfluenceDomainResolver.Resolve(
                snapshot, meshResult.Positions, compiledParts, bodyProgram);
        }

        public static GeneratedCreature Assemble(GeneratedCreatureData data, Func<string, Mesh> meshResolver = null)
        {
            if (data == null) throw new DomainException("generation data must not be null.");

            Mesh mesh = data.MeshResult.ToUnityMesh();
            GeneratedCreature generated = null;
            try
            {
                mesh.SetColors(data.Colors.ToArray());

                generated = new GeneratedCreature();
                generated.AddGeometry(new GeometryItem(
                    sourcePartId: GeneratedCreature.ImplicitSurfaceSourceId,
                    geometryType: GeometryType.Implicit,
                    mesh: mesh,
                    sourceMesh: null,
                    restPlacement: Matrix4x4.identity,
                    materialRegions: null,
                    rigBinding: new RigBindingMetadata(
                        GeneratedCreature.ImplicitSurfaceSourceId, parentPartId: null, isMirrored: false)));
                mesh = null;

                AppendMeshAssetItems(generated, data, meshResolver);
                return generated;
            }
            catch
            {
                DestroyGeneratedMesh(mesh);
                if (generated != null)
                {
                    for (int i = 0; i < generated.Geometry.Count; i++)
                    {
                        GeometryItem item = generated.Geometry[i];
                        if (item != null) DestroyGeneratedMesh(item.Mesh);
                    }
                }
                throw;
            }
        }

        private static void AppendMeshAssetItems(
            GeneratedCreature generated,
            GeneratedCreatureData data,
            Func<string, Mesh> meshResolver)
        {
            SkeletonSnapshot skeleton = data.SkeletonSnapshot;
            if (skeleton == null)
            {
                throw new DomainException("generated data must carry a resolved skeleton snapshot for mesh-asset binding.");
            }

            var meshParts = data.Snapshot.PartsById.Values
                .Where(p => p.HasMeshGeometry)
                .OrderBy(p => p.Id, StringComparer.Ordinal);

            foreach (ResolvedPartSnapshot resolvedPart in meshParts)
            {
                Mesh sourceMesh = ResolveMesh(resolvedPart.Id, resolvedPart.MeshAssetKey, meshResolver);
                Matrix4x4 placement = resolvedPart.GeometryPlacementToCreatureSpace;
                CreaturePart sourcePart = data.Definition.FindPart(resolvedPart.Id);

                generated.AddGeometry(BuildMeshAssetItem(
                    resolvedPart, sourcePart, sourceMesh, placement, skeleton, mirror: false));

                if (resolvedPart.MirrorAcrossSymmetryPlane && data.Snapshot.SymmetryMode != SymmetryMode.None)
                {
                    generated.AddGeometry(BuildMeshAssetItem(
                        resolvedPart, sourcePart, sourceMesh,
                        MirrorUtility.ReflectTransformAcrossX(placement), skeleton, mirror: true));
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

        private static GeometryItem BuildMeshAssetItem(
            ResolvedPartSnapshot part,
            CreaturePart sourcePart,
            Mesh source,
            Matrix4x4 placement,
            SkeletonSnapshot skeleton,
            bool mirror)
        {
            Mesh mesh = null;
            try
            {
                Vector3[] positions = source.vertices;
                Vector3[] transformed = new Vector3[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    transformed[i] = placement.MultiplyPoint3x4(positions[i]);
                }

                mesh = new Mesh
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
                mesh.SetColors(AppearanceBaker.BakePart(part.Appearance, mesh.vertices, mesh.normals));

                List<MaterialRegion> regions = null;
                if (!string.IsNullOrWhiteSpace(part.Appearance.MaterialKey))
                {
                    int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                    regions = new List<MaterialRegion>(subMeshCount);
                    for (int s = 0; s < subMeshCount; s++)
                    {
                        regions.Add(new MaterialRegion(
                            submeshIndex: s,
                            startIndex: 0,
                            indexCount: mesh.GetTriangles(s).Length,
                            materialKey: part.Appearance.MaterialKey));
                    }
                }

                GeometryItem item = new GeometryItem(
                    sourcePartId: mirror ? part.Id + GeneratedCreature.MirrorSuffix : part.Id,
                    geometryType: GeometryType.MeshAsset,
                    mesh: mesh,
                    sourceMesh: source,
                    restPlacement: placement,
                    materialRegions: regions,
                    rigBinding: new RigBindingMetadata(part.Id, part.ParentId, mirror),
                    vertexInfluences: RigidMeshWeightAuthoring.Author(
                        skeleton, sourcePart, mirror, mesh.vertices));
                mesh = null;
                return item;
            }
            finally
            {
                if (mesh != null)
                {
                    DestroyGeneratedMesh(mesh);
                }
            }
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

        private static void DestroyGeneratedMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
            else UnityEngine.Object.DestroyImmediate(mesh);
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