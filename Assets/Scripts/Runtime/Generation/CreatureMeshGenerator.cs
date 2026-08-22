using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Appearance;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    public static class CreatureMeshGenerator
    {
        public static Mesh Generate(CreatureDefinition definition, out MeshTopologyReport topologyReport, GenerationDiagnostics diagnostics = null)
        {
            return Generate(definition, out topologyReport, diagnostics, usePortableSampling: true);
        }

        public static Mesh Generate(
            CreatureDefinition definition,
            out MeshTopologyReport topologyReport,
            GenerationDiagnostics diagnostics,
            bool usePortableSampling)
        {
            if (definition == null) throw new DomainException("definition must not be null.");

            ValidationResult validation = DefinitionValidator.Validate(definition);
            diagnostics?.RecordIssues(validation.Issues);
            if (!validation.IsValid)
            {
                diagnostics?.MarkFailed(GenerationStage.Validation);
                throw new DomainException("CreatureDefinition is invalid and cannot be generated.");
            }

            ISdfNode sdf = null;
            SdfProgram portableProgram = null;
            Time(diagnostics, GenerationStage.SdfCompile, () =>
            {
                sdf = SdfProgramBuilder.Compile(definition);
                if (usePortableSampling) portableProgram = SdfProgramBuilder.CompilePortable(definition);
            });

            DensityGrid grid = null;
            Time(diagnostics, GenerationStage.FieldSampling,
                () =>
                {
                    if (usePortableSampling)
                    {
                        grid = DensityGrid.SamplePortable(portableProgram, definition.Bounds, definition.Generation);
                        portableProgram.Dispose();
                        portableProgram = null;
                    }
                    else
                    {
                        grid = DensityGrid.Sample(sdf, definition.Bounds, definition.Generation);
                    }
                });
            diagnostics?.RecordGridDimensions(grid.CellsX, grid.CellsY, grid.CellsZ, grid.SampleCount);

            MeshExtractionResult meshResult = null;
            Time(diagnostics, GenerationStage.MeshExtraction,
                () => meshResult = MarchingCubesExtractor.Extract(
                    sdf, grid, diagnostics?.CollectTimings == true));
            diagnostics?.RecordExtractionStatistics(
                meshResult.MixedCellCount, meshResult.GradientEvaluationCount);
            diagnostics?.RecordExtractionTiming(
                meshResult.CornerClassificationTime,
                meshResult.ContourResolutionTime,
                meshResult.VertexWeldingTime,
                meshResult.TriangleEmissionTime);

            MeshTopologyReport generatedTopologyReport = null;
            Time(diagnostics, GenerationStage.MeshValidation,
                () => generatedTopologyReport = MeshTopologyValidator.Validate(meshResult));
            topologyReport = generatedTopologyReport;

            Color[] colors = null;
            Time(diagnostics, GenerationStage.AppearanceBake,
                () => colors = AppearanceBaker.Bake(definition, meshResult));

            Mesh mesh = meshResult.ToUnityMesh();
            mesh.SetColors(colors);
            return mesh;
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
