using System;
using System.Collections.Generic;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Pure-ish validation of authoritative state before expensive generation
    /// (implementation guide §11 "DefinitionValidator"). Never repairs the
    /// definition — only reports. Every check named in design doc §4.3 and
    /// implementation guide §2.4 is covered below.
    /// </summary>
    public static class DefinitionValidator
    {
        public static ValidationResult Validate(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot validate a null CreatureDefinition.");
            }

            var issues = new List<ValidationIssue>();

            ValidateSchemaVersion(definition, issues);
            ValidateBounds(definition, issues);
            ValidateGenerationBudget(definition, issues);
            ValidateDuplicateIds(definition, issues);
            ValidateParentsAndCycles(definition, issues);
            ValidatePartTypes(definition, issues);
            ValidateTransformsAndShapesAndAppearance(definition, issues);

            return new ValidationResult(issues);
        }

        private static void ValidateSchemaVersion(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            if (definition.SchemaVersion != CreatureDefinition.CurrentSchemaVersion)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    ValidationCode.UnsupportedSchemaVersion,
                    $"Schema version {definition.SchemaVersion} is not supported " +
                    $"(expected {CreatureDefinition.CurrentSchemaVersion})."));
            }
        }

        private static void ValidateBounds(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            if (!definition.Bounds.IsFinite() || !definition.Bounds.IsPositive())
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    ValidationCode.InvalidBounds,
                    "BoundsDefinition must have finite, positive MaxX/MaxY/MaxZ."));
            }
        }

        private static void ValidateGenerationBudget(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            if (!definition.Generation.IsFinite() || !definition.Generation.IsPositive())
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    ValidationCode.GenerationBudgetExceeded,
                    "GenerationSettings.VoxelsPerUnit must be finite and positive."));
                return;
            }

            long estimated = definition.Generation.EstimateVoxelCount(definition.Bounds);
            if (estimated > GenerationTolerances.MaxVoxelBudget)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    ValidationCode.GenerationBudgetExceeded,
                    $"Estimated voxel count {estimated:N0} exceeds the safety budget " +
                    $"of {GenerationTolerances.MaxVoxelBudget:N0}. Reduce bounds or " +
                    "VoxelsPerUnit."));
            }
        }

        private static void ValidateDuplicateIds(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (CreaturePart part in definition.Parts)
            {
                if (string.IsNullOrWhiteSpace(part.Id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.DuplicatePartId,
                        "Part has a null/empty Id.", part.Id));
                    continue;
                }

                if (!seen.Add(part.Id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.DuplicatePartId,
                        $"Duplicate part Id '{part.Id}'.", part.Id));
                }
            }
        }

        private static void ValidateParentsAndCycles(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            var idsById = new HashSet<string>(StringComparer.Ordinal);
            foreach (CreaturePart part in definition.Parts) idsById.Add(part.Id);

            foreach (CreaturePart part in definition.Parts)
            {
                if (part.ParentId != null && !idsById.Contains(part.ParentId))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.MissingParent,
                        $"Part '{part.Id}' references missing parent '{part.ParentId}'.",
                        part.Id));
                }
            }

            if (definition.HasParentCycle(out List<string> cyclePartIds))
            {
                foreach (string partId in cyclePartIds)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.ParentCycle,
                        $"Part '{partId}' is part of a parent-reference cycle.",
                        partId));
                }
            }
        }

        private static void ValidatePartTypes(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            foreach (CreaturePart part in definition.Parts)
            {
                if (!Enum.IsDefined(typeof(PartType), part.PartType))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.UnsupportedPartType,
                        $"Part '{part.Id}' has an unsupported PartType value.",
                        part.Id));
                }
            }
        }

        private static void ValidateTransformsAndShapesAndAppearance(
            CreatureDefinition definition, List<ValidationIssue> issues)
        {
            foreach (CreaturePart part in definition.Parts)
            {
                if (!part.Transform.IsFinite())
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.NonFiniteTransform,
                        $"Part '{part.Id}' has a non-finite transform component.",
                        part.Id));
                }
                else
                {
                    UnityEngine.Vector3 scale = part.Transform.Scale;
                    if (scale.x < GenerationTolerances.MinScaleComponent ||
                        scale.y < GenerationTolerances.MinScaleComponent ||
                        scale.z < GenerationTolerances.MinScaleComponent)
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            ValidationCode.InvalidScale,
                            $"Part '{part.Id}' has a scale component below the minimum " +
                            $"({GenerationTolerances.MinScaleComponent}).",
                            part.Id));
                    }

                    if (!definition.Bounds.Contains(part.Transform.Position))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            ValidationCode.OutOfBoundsTransform,
                            $"Part '{part.Id}' position lies outside the creature bounds.",
                            part.Id));
                    }
                }

                if (!part.Shape.HasValidParameters())
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.InvalidShapeParameter,
                        $"Part '{part.Id}' has an invalid shape parameter " +
                        "(non-positive size, negative blend radius, or non-finite value).",
                        part.Id));
                }

                if (!part.Appearance.IsFinite())
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.NonFiniteAppearance,
                        $"Part '{part.Id}' has a non-finite appearance parameter.",
                        part.Id));
                }
            }
        }
    }
}
