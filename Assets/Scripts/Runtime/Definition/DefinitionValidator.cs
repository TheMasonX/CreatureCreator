using System;
using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology;

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
            ValidateLimbChains(definition, issues);
            ValidateMeshGeometry(definition, issues);
            ValidateResolvedEnvelope(definition, issues);

            return new ValidationResult(issues);
        }

        /// <summary>
        /// CC-050 (audit Finding 4): the local-frame checks cannot see a nested
        /// part, limb joint, or mesh attachment whose RESOLVED creature-space
        /// position falls outside the generation bounds — the voxel domain that
        /// would crop it. This stage re-resolves every geometry source through the
        /// shared world-frame resolver and reports any origin that lands outside
        /// <see cref="BoundsDefinition"/>. Limb chains are consumed through the
        /// shared <see cref="ResolvedLimb"/> derivation (CC-056A) so this stage
        /// checks exactly the joint positions the metaball sampler and skeleton
        /// inferrer use, never re-derived here. Report-only; a Body sample's
        /// radius may extend past the box by design (cropping). Skips when the
        /// definition has unresolved parent/cycle errors, because resolution is
        /// undefined for a broken chain.
        /// </summary>
        private static void ValidateResolvedEnvelope(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            if (HasStructuralParentIssue(issues)) return;

            if (definition.Body != null && definition.Body.Samples != null
                && definition.Body.Samples.Count > 0)
            {
                // CC-056A increment 3: consume the shared ResolvedBody derivation
                // instead of iterating authored samples here. A broken spline (a
                // null sample) is already reported by ValidateBody as
                // InvalidBodySample; the resolved envelope is undefined for it, so
                // skip it — the same rule the limb envelope uses.
                ResolvedBody bodyResolved;
                bool bodyResolvedOk;
                try
                {
                    bodyResolved = ResolvedBody.Resolve(definition.Body);
                    bodyResolvedOk = true;
                }
                catch (DomainException)
                {
                    bodyResolved = default;
                    bodyResolvedOk = false;
                }

                if (bodyResolvedOk)
                {
                    for (int i = 0; i < bodyResolved.SamplePositions.Count; i++)
                    {
                        Vector3 position = bodyResolved.SamplePositions[i];
                        if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)) continue;
                        if (!definition.Bounds.Contains(position))
                        {
                            // The position comes from the resolved model; the
                            // authored sample Id is read only for a stable
                            // diagnostic message.
                            issues.Add(new ValidationIssue(
                                ValidationSeverity.Error, ValidationCode.ResolvedBodySampleOutOfBounds,
                                $"Body sample '{definition.Body.Samples[i].Id}' lies outside the creature bounds."));
                        }
                    }
                }
            }

            foreach (CreaturePart part in definition.Parts)
            {
                if (part == null) continue;
                if (!IsFinite(part.Transform.Position.x) || !IsFinite(part.Transform.Position.y) || !IsFinite(part.Transform.Position.z)) continue;

                Matrix4x4 world;
                try
                {
                    world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part);
                }
                catch (DomainException)
                {
                    continue;
                }

                if (part.Limb != null && part.Limb.Joints != null && part.Limb.Joints.Count > 0)
                {
                    // CC-056A increment 2: resolve the chain once through the shared
                    // ResolvedLimb derivation instead of iterating LimbChain here.
                    // Structural errors (a null joint) are already reported by
                    // ValidateLimbChains; the resolved envelope is undefined for a
                    // broken chain, so skip it.
                    ResolvedLimb resolved;
                    try
                    {
                        resolved = ResolvedLimb.Resolve(part.Limb);
                    }
                    catch (DomainException)
                    {
                        continue;
                    }

                    for (int i = 0; i < resolved.JointPositions.Count; i++)
                    {
                        Vector3 resolvedWorld = world.MultiplyPoint3x4(resolved.JointPositions[i]);
                        if (!definition.Bounds.Contains(resolvedWorld))
                        {
                            // The position comes from the resolved model; the authored
                            // joint Id is read only for a stable diagnostic message.
                            issues.Add(new ValidationIssue(
                                ValidationSeverity.Error, ValidationCode.ResolvedLimbJointOutOfBounds,
                                $"Part '{part.Id}' limb joint '{part.Limb.Joints[i].Id}' lies outside the creature bounds.", part.Id));
                        }
                    }
                    continue;
                }

                Vector3 origin = world.GetColumn(3);
                if (!definition.Bounds.Contains(origin))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.ResolvedPartOutOfBounds,
                        $"Part '{part.Id}' lies outside the creature bounds.", part.Id));
                }

                if (part.MeshGeometry != null)
                {
                    GeometryAttachment attachment = part.MeshGeometry.Attachment ?? new GeometryAttachment();
                    if (IsFinite(attachment.Offset.x) && IsFinite(attachment.Offset.y) && IsFinite(attachment.Offset.z))
                    {
                        Vector3 attachmentPoint = world.MultiplyPoint3x4(attachment.Offset);
                        if (!definition.Bounds.Contains(attachmentPoint))
                        {
                            issues.Add(new ValidationIssue(
                                ValidationSeverity.Error, ValidationCode.ResolvedMeshAttachmentOutOfBounds,
                                $"Part '{part.Id}' mesh attachment lies outside the creature bounds.", part.Id));
                        }
                    }
                }
            }
        }

        private static bool HasStructuralParentIssue(List<ValidationIssue> issues)
        {
            foreach (ValidationIssue issue in issues)
            {
                if (issue.Code == ValidationCode.MissingParent || issue.Code == ValidationCode.ParentCycle)
                {
                    return true;
                }
            }
            return false;
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
        /// never repairs. The canonicalizer handles key ordering and quantization
        /// at the mutation/serialization boundary, so key order is not validated
        /// here — non-finite or out-of-range values and missing gradients are.
        /// Gradients are Unity's built-in type, so the key checks live on
        /// <see cref="GradientAdapter"/>.
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

            if (appearance.VerticalCurve == null)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.InvalidBodyAppearance,
                    "Body vertical-gradient curve must not be null."));
            }
            else
            {
                if (!CurveAdapter.IsFinite(appearance.VerticalCurve))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.NonFiniteBodyAppearance,
                        "Body vertical-gradient curve has a non-finite key value or tangent."));
                }

                if (!CurveAdapter.HasValidKeys(appearance.VerticalCurve))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidBodyAppearance,
                        "Body vertical-gradient curve must contain at least one key with T in [0, 1]."));
                }
            }

            ValidateGradient(appearance.TopGradient, "top", issues);
            ValidateGradient(appearance.BottomGradient, "bottom", issues);
        }

        private static void ValidateGradient(UnityEngine.Gradient gradient, string name, List<ValidationIssue> issues)
        {
            if (gradient == null)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.InvalidBodyAppearance,
                    $"Body {name} gradient must not be null."));
                return;
            }

            if (!GradientAdapter.IsFinite(gradient))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.NonFiniteBodyAppearance,
                    $"Body {name} gradient has a non-finite value."));
            }

            if (!GradientAdapter.HasValidKeys(gradient))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, ValidationCode.InvalidBodyAppearance,
                    $"Body {name} gradient must contain at least one color key and one alpha key with T in [0, 1]."));
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

                // A limb part's geometry derives from its LimbChain and a mesh part's
                // from its MeshGeometry; Shape is inert for both (ADR-001 §2, ADR-002
                // §2). Skip the shape-parameter check so neither is forced to carry a
                // meaningful Shape.
                if (part.Limb == null && part.MeshGeometry == null && !part.Shape.HasValidParameters())
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

                if (part.ParentAttachment != null &&
                    (definition.Body == null || definition.Body.Samples == null ||
                     !ContainsBodySegmentStartId(definition.Body.Samples, part.ParentAttachment.SegmentStartSampleId)))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidAttachmentAnchor,
                        $"Part '{part.Id}' references a Body sample that is not a " +
                        $"segment start ('{part.ParentAttachment.SegmentStartSampleId}') in its semantic attachment anchor.", part.Id));
                }
            }
        }

        /// <summary>
        /// True when <paramref name="id"/> identifies a segment START sample — any
        /// sample except the terminal one. A BodySurfaceAnchor's
        /// SegmentStartSampleId must be a segment start; the terminal sample has no
        /// outgoing segment and the projector rejects it (CC-056B).
        /// </summary>
        private static bool ContainsBodySegmentStartId(IReadOnlyList<BodySample> samples, uint id)
        {
            for (int i = 0; i < samples.Count - 1; i++)
            {
                if (samples[i] != null && samples[i].Id == id) return true;
            }
            return false;
        }

        /// <summary>
        /// Validates every part's mesh-asset geometry source (CC-031, ADR-002).
        /// Reports only; never repairs. A part declares exactly one geometry source:
        /// a mesh geometry is invalid when its key is empty, when a limb chain is
        /// also present, or when its attachment is non-finite or has a scale
        /// component below the minimum. Only numerical/pathological states are
        /// rejected — resolution of the key against an external mesh palette is a
        /// generator/editor-layer concern, not DNA validity.
        /// </summary>
        private static void ValidateMeshGeometry(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            foreach (CreaturePart part in definition.Parts)
            {
                if (part == null || part.MeshGeometry == null) continue;

                MeshGeometry mesh = part.MeshGeometry;

                if (string.IsNullOrWhiteSpace(mesh.MeshAssetKey))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidMeshGeometry,
                        $"Part '{part.Id}' has a mesh geometry with an empty mesh asset key.", part.Id));
                }

                if (part.Limb != null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidMeshGeometry,
                        $"Part '{part.Id}' declares both a limb chain and a mesh geometry; " +
                        "a part has exactly one geometry source.", part.Id));
                }

                if (mesh.Attachment == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidMeshGeometry,
                        $"Part '{part.Id}' has a mesh geometry with a null attachment.", part.Id));
                    continue;
                }

                if (!mesh.Attachment.IsFinite())
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.NonFiniteMeshGeometryAttachment,
                        $"Part '{part.Id}' has a non-finite mesh geometry attachment.", part.Id));
                }

                UnityEngine.Vector3 scale = mesh.Attachment.Scale;
                if (scale.x < GenerationTolerances.MinScaleComponent ||
                    scale.y < GenerationTolerances.MinScaleComponent ||
                    scale.z < GenerationTolerances.MinScaleComponent)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidMeshGeometryScale,
                        $"Part '{part.Id}' mesh geometry scale has a component below the minimum " +
                        $"({GenerationTolerances.MinScaleComponent}).", part.Id));
                }
            }
        }

        /// <summary>
        /// Validates every part's limb chain (CC-018, ADR-001). Reports only; never
        /// repairs. Covers the structural, numerical, bounds, root-at-origin, and
        /// thickness checks from the ticket's Phase 2. No anatomical constraints
        /// are imposed — only numerical/pathological states are rejected.
        /// </summary>
        private static void ValidateLimbChains(CreatureDefinition definition, List<ValidationIssue> issues)
        {
            foreach (CreaturePart part in definition.Parts)
            {
                if (part == null) continue;

                bool isLimbChainType = part.PartType == PartType.Limb ||
                    part.PartType == PartType.Leg ||
                    part.PartType == PartType.Arm;

                if (part.Limb != null && !isLimbChainType)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationCode.InvalidLimbChain,
                        $"Part '{part.Id}' has a limb chain even though its PartType is {part.PartType}; clear the stale limb data before serialization or generation.",
                        part.Id));
                    continue;
                }

                if (part.Limb == null) continue;

                LimbChain limb = part.Limb;

                if (limb.Joints == null || limb.Joints.Count == 0)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidLimbChain,
                        $"Part '{part.Id}' has a limb chain with no joints.", part.Id));
                    continue;
                }

                if (limb.Joints.Count < GenerationTolerances.MinLimbJointCount ||
                    limb.Joints.Count > GenerationTolerances.MaxLimbJointCount)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.LimbJointCountOutOfRange,
                        $"Part '{part.Id}' limb chain must have between " +
                        $"{GenerationTolerances.MinLimbJointCount} and " +
                        $"{GenerationTolerances.MaxLimbJointCount} joints " +
                        $"(found {limb.Joints.Count}).", part.Id));
                }

                var jointIds = new HashSet<uint>();
                for (int i = 0; i < limb.Joints.Count; i++)
                {
                    LimbJoint joint = limb.Joints[i];
                    if (joint == null)
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, ValidationCode.InvalidLimbChain,
                            $"Part '{part.Id}' has a null limb joint at index {i}.", part.Id));
                        continue;
                    }

                    if (!jointIds.Add(joint.Id))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, ValidationCode.DuplicateLimbJointId,
                            $"Part '{part.Id}' limb has duplicate joint Id '{joint.Id}'.", part.Id));
                    }
                    if (i > 0 && limb.Joints[i - 1] != null &&
                        joint.Id <= limb.Joints[i - 1].Id)
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, ValidationCode.LimbJointOrderNotDeterministic,
                            "Limb joint IDs must increase with chain order.", part.Id));
                    }

                    if (!IsFinite(joint.Position.x) || !IsFinite(joint.Position.y) ||
                        !IsFinite(joint.Position.z))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, ValidationCode.NonFiniteLimbJoint,
                            $"Part '{part.Id}' limb joint '{joint.Id}' has a non-finite position.", part.Id));
                    }

                    // Bounds are checked in the part's local frame — the same
                    // approximation the existing OutOfBoundsTransform check makes
                    // for part positions.
                    if (!definition.Bounds.Contains(joint.Position))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, ValidationCode.LimbJointOutOfBounds,
                            $"Part '{part.Id}' limb joint '{joint.Id}' lies outside the creature bounds.", part.Id));
                    }

                    if (i > 0 && limb.Joints[i - 1] != null)
                    {
                        float segmentLength = Vector3.Distance(joint.Position, limb.Joints[i - 1].Position);
                        if (segmentLength < GenerationTolerances.MinLimbSegmentLength)
                        {
                            issues.Add(new ValidationIssue(
                                ValidationSeverity.Error, ValidationCode.LimbSegmentTooShort,
                                $"Part '{part.Id}' limb segment {i - 1}->{i} is shorter than the " +
                                $"minimum ({GenerationTolerances.MinLimbSegmentLength:F4}).", part.Id));
                        }
                    }
                }

                if (limb.Joints[0] != null &&
                    limb.Joints[0].Position.sqrMagnitude >
                    GenerationTolerances.LimbRootAtOriginTolerance * GenerationTolerances.LimbRootAtOriginTolerance)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.LimbRootNotAtOrigin,
                        $"Part '{part.Id}' limb root joint must sit at the local origin " +
                        "(Joints[0] ≈ Vector3.zero); the part's Transform is the placement frame.", part.Id));
                }

                if (limb.Thickness == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidThicknessProfile,
                        $"Part '{part.Id}' limb thickness profile must not be null.", part.Id));
                }
                else
                {
                    if (!limb.Thickness.IsFinite())
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, ValidationCode.NonFiniteThickness,
                            $"Part '{part.Id}' limb thickness profile has a non-finite key.", part.Id));
                    }

                    if (!limb.Thickness.HasValidKeys())
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, ValidationCode.InvalidThicknessProfile,
                            $"Part '{part.Id}' limb thickness profile must have at least two keys " +
                            "with unique T in [0, 1] and positive values.", part.Id));
                    }
                }

                if (float.IsNaN(limb.BlendRadius) || float.IsInfinity(limb.BlendRadius))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidLimbBlendRadius,
                        $"Part '{part.Id}' limb blend radius must be finite.", part.Id));
                }
                else if (limb.BlendRadius < 0f)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, ValidationCode.InvalidLimbBlendRadius,
                        $"Part '{part.Id}' limb blend radius must not be negative.", part.Id));
                }
            }
        }
    }
}
