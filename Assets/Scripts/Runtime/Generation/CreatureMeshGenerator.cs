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
            if (definition == null) throw new DomainException("definition must not be null.");

            ValidationResult validation = DefinitionValidator.Validate(definition);
            diagnostics?.RecordIssues(validation.Issues);
            if (!validation.IsValid)
            {
                diagnostics?.MarkFailed(GenerationStage.Validation);
                throw new DomainException("CreatureDefinition is invalid and cannot be generated.");
            }

            ISdfNode sdf = null;
            Time(diagnostics, GenerationStage.SdfCompile, () => sdf = SdfProgramBuilder.Compile(definition));

            DensityGrid grid = null;
            Time(diagnostics, GenerationStage.FieldSampling,
                () => grid = DensityGrid.Sample(sdf, definition.Bounds, definition.Generation));

            MeshExtractionResult meshResult = null;
            Time(diagnostics, GenerationStage.MeshExtraction,
                () => meshResult = MarchingCubesExtractor.Extract(sdf, grid));

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
