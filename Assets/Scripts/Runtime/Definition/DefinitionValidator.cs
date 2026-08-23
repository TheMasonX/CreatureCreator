using System;
using System.Collections.Generic;
using UnityEngine;
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
            ValidateBody(definition, issues);
            ValidateBodyAppearance(definition, issues);
            ValidateBounds(definition, issues);
            ValidateGenerationBudget(definition, issues);
            ValidateDuplicateIds(definition, issues);
            ValidateParentsAndCycles(definition, issues);
            ValidatePartTypes(definition, issues);
            ValidateTransformsAndShapesAndAppearance(definition, issues);

            return new ValidationResult(issues);
        }

        private static void ValidateBody(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            if (definition.Body == null || definition.Body.Samples == null || definition.Body.Samples.Count == 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.MissingBody,
                    "CreatureDefinition requires one non-empty Body spline."));
                return;
            }

            if (definition.Body.Samples.Count > GenerationTolerances.MaxBodySampleCount)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.InvalidBodySampleCount,
                    $"Body spline has more than {GenerationTolerances.MaxBodySampleCount} samples."));
            }

            var sampleIds = new HashSet<uint>();
            float expectedSpacing = 0f;
            int spacingCount = 0;
            for (int i = 0; i < definition.Body.Samples.Count; i++)
            {
                BodySample sample = definition.Body.Samples[i];
                if (sample == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidBodySample,
                        $"Body sample at index {i} is null."));
                    continue;
                }

                if (!sampleIds.Add(sample.Id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.DuplicateBodySampleId,
                        $"Duplicate Body sample Id '{sample.Id}'."));
                }
                if (i > 0 && definition.Body.Samples[i - 1] != null &&
                    sample.Id <= definition.Body.Samples[i - 1].Id)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.DuplicateBodySampleId,
                        "Body sample IDs must increase with spline order."));
                }

                if (!IsFinite(sample.Position.x) || !IsFinite(sample.Position.y) ||
                    !IsFinite(sample.Position.z) || !IsFinite(sample.Radius) || sample.Radius <= 0f)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidBodySample,
                        $"Body sample '{sample.Id}' must have finite position and positive radius."));
                }
                if (i > 0 && definition.Body.Samples[i - 1] != null)
                {
                    float spacing = Vector3.Distance(sample.Position, definition.Body.Samples[i - 1].Position);
                    expectedSpacing += spacing;
                    spacingCount++;
                }
            }

            if (spacingCount > 0)
            {
                expectedSpacing /= spacingCount;
                for (int i = 1; i < definition.Body.Samples.Count; i++)
                {
                    BodySample previous = definition.Body.Samples[i - 1];
                    BodySample current = definition.Body.Samples[i];
                    if (previous != null && current != null &&
                        Mathf.Abs(Vector3.Distance(previous.Position, current.Position) - expectedSpacing) >
                        GenerationTolerances.BodySpacingTolerance)
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, ValidationCode.UnevenBodySpacing,
                            "Body samples must have even arc-length spacing."));
                        break;
                    }
                }
            }

            if (!IsFinite(definition.Forward.x) || !IsFinite(definition.Forward.y) ||
                !IsFinite(definition.Forward.z) || definition.Forward.sqrMagnitude <=
                GenerationTolerances.ScalarComparisonEpsilon * GenerationTolerances.ScalarComparisonEpsilon)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.InvalidForward,
                    "CreatureDefinition.Forward must be finite and nonzero."));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// Validates the Body vertical-gradient appearance (CC-025). Reports only;
        /// never repairs. The canonicalizer handles stop ordering and
        /// quantization at the mutation/serialization boundary, so unsorted stops
        /// are not an error here — non-finite or out-of-range values and missing
        /// gradients are.
        /// </summary>
        private static void ValidateBodyAppearance(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            if (definition.Body == null) return;

            BodyVerticalGradientAppearance appearance = definition.Body.Appearance;
            if (appearance == null)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.InvalidBodyAppearance,
                    "Body vertical-gradient appearance must not be null."));
                return;
            }

            if (!IsFinite(appearance.VerticalOffset)
                || appearance.VerticalOffset < -1f || appearance.VerticalOffset > 1f)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.InvalidBodyAppearance,
                    "Body vertical-gradient offset must be finite and within [-1, 1]."));
            }

            ValidateColorGradient(appearance.TopGradient, "top", issues);
            ValidateColorGradient(appearance.BottomGradient, "bottom", issues);
        }

        private static void ValidateColorGradient(ColorGradient gradient, string name, List<ValidationIssue> issues)
        {
            if (gradient == null || gradient.Stops == null || gradient.Stops.Count == 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.InvalidBodyAppearance,
                    $"Body {name} gradient must contain at least one stop."));
                return;
            }

            for (int i = 0; i < gradient.Stops.Count; i++)
            {
                GradientColorStop stop = gradient.Stops[i];
                if (!stop.IsFinite())
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.NonFiniteBodyAppearance,
                        $"Body {name} gradient stop {i} has a non-finite value."));
                }
                if (stop.T < 0f || stop.T > 1f)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidBodyAppearance,
                        $"Body {name} gradient stop {i} has T outside [0, 1]."));
                }
            }
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
                if (part == null) continue;
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
                if (part == null) continue;
                if (part.ParentId == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidBodyParent,
                        $"Part '{part.Id}' must be a descendant of the Body.", part.Id));
                }
                else if (part.ParentId != CreatureDefinition.BodyId && !idsById.Contains(part.ParentId))
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
                if (part == null) continue;
                if (!Enum.IsDefined(typeof(PartType), part.PartType))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.UnsupportedPartType,
                        $"Part '{part.Id}' has an unsupported PartType value.",
                        part.Id));
                }
                else if (part.PartType == PartType.Body || part.PartType == PartType.Root)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.UnsupportedPartType,
                        $"Part '{part.Id}' cannot use reserved PartType {part.PartType} in schema v2.",
                        part.Id));
                }
                if (part.ParentId == CreatureDefinition.BodyId && part.PartType == PartType.Tail)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidBodyParent,
                        $"Part '{part.Id}' cannot be an independent root Tail.", part.Id));
                }
            }
        }

        private static void ValidateTransformsAndShapesAndAppearance(
            CreatureDefinition definition, List<ValidationIssue> issues)
        {
            foreach (CreaturePart part in definition.Parts)
            {
                if (part == null) continue;
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

                if (part.ParentAttachment != null &&
                    (!IsFinite(part.ParentAttachment.SegmentT) ||
                     !IsFinite(part.ParentAttachment.RadialAngle) ||
                     !IsFinite(part.ParentAttachment.SurfaceOffset) ||
                     !IsFinite(part.ParentAttachment.Roll) ||
                     part.ParentAttachment.SegmentT < 0f || part.ParentAttachment.SegmentT > 1f))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidAttachmentAnchor,
                        $"Part '{part.Id}' has an invalid semantic attachment anchor.", part.Id));
                }
            }
        }
    }
}
