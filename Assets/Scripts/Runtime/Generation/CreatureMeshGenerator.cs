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
            if (definition == null) throw new DomainException("definition must not be null.");

            ValidationResult validation = DefinitionValidator.Validate(definition);
            diagnostics?.RecordIssues(validation.Issues);
            if (!validation.IsValid)
            {
                diagnostics?.MarkFailed(GenerationStage.Validation);
                throw new DomainException("CreatureDefinition is invalid and cannot be generated.");
            }

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);

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

            MeshExtractionResult meshResult = null;
            try
            {
                Time(diagnostics, GenerationStage.MeshExtraction,
                    () => meshResult = MarchingCubesExtractor.Extract(
                        grid, diagnostics?.CollectTimings == true));
                diagnostics?.RecordExtractionStatistics(
                    meshResult.MixedCellCount, meshResult.GradientEvaluationCount);
                diagnostics?.RecordExtractionTiming(
                    meshResult.ActiveCellConstructionTime,
                    meshResult.ContourResolutionTime,
                    meshResult.VertexWeldingTime,
                    meshResult.TriangleEmissionTime);
            }
            finally
            {
                // The grid's native sample buffer is no longer needed after
                // extraction (validation, appearance, and assembly consume the
                // plain-data MeshExtractionResult). Release it even when
                // extraction throws so the Persistent allocation cannot leak.
                if (grid != null)
                {
                    grid.Dispose();
                    grid = null;
                }
            }

            MeshTopologyReport generatedTopologyReport = null;
            Time(diagnostics, GenerationStage.MeshValidation,
                () => generatedTopologyReport = MeshTopologyValidator.Validate(meshResult));

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

            return new GeneratedCreatureData(definition, snapshot, meshResult, colors, generatedTopologyReport);
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

            // Items 1..n: mesh-asset parts, ordered by SourcePartId for a
            // deterministic output independent of authoring order.
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

            return generated;
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
