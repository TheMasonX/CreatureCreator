using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;
using static ProceduralCreature.Common.NumericValidity;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Applies the canonical representation rule to a CreatureDefinition: quantized
    /// position/rotation/scale, normalized rotation, sorted parts for stable
    /// serialization. Called explicitly at mutation-commit and serialization
    /// boundaries only — NOT during interactive/temporary editing (§2.3: "Do not
    /// repeatedly quantize internal temporary values during iterative numeric
    /// algorithms").
    ///
    /// This does not validate. Canonicalizing an invalid definition (e.g. one with a
    /// NaN transform) throws DomainException, because calling code is expected to
    /// validate first — canonicalization is not a repair pass (implementation guide
    /// §14: "Never silently clamp or rewrite a persisted definition during load").
    /// </summary>
    public static class DefinitionCanonicalizer
    {
        public static CreatureDefinition Canonicalize(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot canonicalize a null CreatureDefinition.");
            }

            CreatureDefinition result = definition.Clone();
            CreaturePartHierarchyIndex hierarchy = result.CreateHierarchyIndex();
            if (hierarchy.HasNullEntries)
            {
                throw new DomainException("Cannot canonicalize a definition with a null part entry.");
            }
            if (hierarchy.DuplicateIds.Count > 0)
            {
                throw new DomainException($"Cannot canonicalize a definition with duplicate part Id '{hierarchy.DuplicateIds[0]}'.");
            }
            if (hierarchy.TryResolve(CreatureDefinition.BodyId, out _))
            {
                throw new DomainException(
                    $"Cannot canonicalize a definition with reserved part Id '{CreatureDefinition.BodyId}'.");
            }
            if (hierarchy.HasParentCycle(out List<string> cyclePartIds))
            {
                throw new DomainException($"Cannot canonicalize a definition with a parent cycle involving part '{cyclePartIds[0]}'.");
            }

            if (result.Body == null || result.Body.Samples == null)
            {
                throw new DomainException("Cannot canonicalize a definition without a Body spline.");
            }

            foreach (BodySample sample in result.Body.Samples)
            {
                if (sample == null || !IsFinite(sample.Position) || !IsFinite(sample.Radius))
                {
                    throw new DomainException("Cannot canonicalize a Body spline with non-finite samples.");
                }
                sample.Position = new Vector3(
                    GenerationTolerances.Quantize(sample.Position.x),
                    GenerationTolerances.Quantize(sample.Position.y),
                    GenerationTolerances.Quantize(sample.Position.z));
                sample.Radius = GenerationTolerances.Quantize(sample.Radius);
            }

            CanonicalizeBodyAppearance(result.Body.Appearance);

            if (!IsFinite(result.Forward) || result.Forward.sqrMagnitude <= 0f)
            {
                throw new DomainException("Cannot canonicalize a definition with an invalid Forward vector.");
            }
            Vector3 forward = result.Forward.normalized;
            result.Forward = new Vector3(
                GenerationTolerances.Quantize(forward.x),
                GenerationTolerances.Quantize(forward.y),
                GenerationTolerances.Quantize(forward.z));

            foreach (CreaturePart part in result.Parts)
            {
                if (!part.Transform.IsFinite())
                {
                    throw new DomainException(
                        $"Part '{part.Id}' has a non-finite transform; validate before canonicalizing.");
                }

                part.Transform = part.Transform.Quantized();
                CanonicalizeShape(ref part.Shape);

                if (part.Limb != null)
                {
                    CanonicalizeLimbChain(part.Limb);
                }

                if (part.MeshGeometry != null)
                {
                    CanonicalizeMeshGeometry(part.MeshGeometry);
                }
            }

            var orderedParts = new List<CreaturePart>();
            AppendChildren(CreatureDefinition.BodyId, hierarchy, orderedParts);
            foreach (CreaturePart part in result.Parts
                .Where(p => p != null && !orderedParts.Contains(p))
                .OrderBy(p => p.Id, System.StringComparer.Ordinal))
            {
                orderedParts.Add(part);
            }
            result.Parts = orderedParts;

            return result;
        }

        private static void CanonicalizeShape(ref ShapeDefinition shape)
        {
            shape = shape.WithLegacyDefaults();
            if (shape.CapsuleAxis < ShapeAxis.X || shape.CapsuleAxis > ShapeAxis.Z)
            {
                throw new DomainException("Cannot canonicalize a shape with an invalid capsule axis.");
            }
        }

        private static void CanonicalizeBodyAppearance(BodyVerticalGradientAppearance appearance)
        {
            if (appearance == null)
            {
                throw new DomainException("Cannot canonicalize a definition without a Body vertical-gradient appearance.");
            }
            CanonicalizeGradient(appearance.TopGradient, "top");
            CanonicalizeGradient(appearance.BottomGradient, "bottom");
            CanonicalizeVerticalCurve(appearance.VerticalCurve);
        }

        private static void CanonicalizeVerticalCurve(UnityEngine.AnimationCurve curve)
        {
            if (curve == null)
            {
                throw new DomainException("Cannot canonicalize a Body vertical curve that is null.");
            }
            if (!CurveAdapter.IsFinite(curve) || !CurveAdapter.HasValidKeys(curve))
            {
                throw new DomainException("Cannot canonicalize an invalid Body vertical curve.");
            }
            CurveAdapter.Quantize(curve);
        }

        private static void CanonicalizeGradient(UnityEngine.Gradient gradient, string name)
        {
            if (gradient == null)
            {
                throw new DomainException($"Cannot canonicalize a Body {name} gradient that is null.");
            }
            if (!GradientAdapter.IsFinite(gradient) || !GradientAdapter.HasValidKeys(gradient))
            {
                throw new DomainException($"Cannot canonicalize an invalid Body {name} gradient.");
            }
            GradientAdapter.Quantize(gradient);
        }

        private static void CanonicalizeLimbChain(LimbChain limb)
        {
            if (limb.Joints == null || limb.Joints.Count == 0)
            {
                throw new DomainException("Cannot canonicalize a limb chain with no joints.");
            }
            foreach (LimbJoint joint in limb.Joints)
            {
                if (joint == null || !IsFinite(joint.Position))
                {
                    throw new DomainException("Cannot canonicalize a limb chain with a null or non-finite joint.");
                }
                joint.Position = new Vector3(
                    GenerationTolerances.Quantize(joint.Position.x),
                    GenerationTolerances.Quantize(joint.Position.y),
                    GenerationTolerances.Quantize(joint.Position.z));
            }

            if (!IsFinite(limb.BlendRadius) || limb.BlendRadius < 0f)
            {
                throw new DomainException("Cannot canonicalize a limb chain with a non-finite or negative blend radius.");
            }
            limb.BlendRadius = GenerationTolerances.Quantize(limb.BlendRadius);

            if (limb.Thickness == null)
            {
                throw new DomainException("Cannot canonicalize a limb chain without a thickness profile.");
            }
            if (!limb.Thickness.IsFinite() || !limb.Thickness.HasValidKeys())
            {
                throw new DomainException("Cannot canonicalize an invalid limb thickness profile.");
            }
            limb.Thickness.Quantize();
        }

        private static void CanonicalizeMeshGeometry(MeshGeometry mesh)
        {
            if (mesh.Attachment == null)
            {
                throw new DomainException("Cannot canonicalize a mesh geometry with a null attachment.");
            }
            GeometryAttachment attachment = mesh.Attachment;
            if (!attachment.IsFinite())
            {
                throw new DomainException("Cannot canonicalize a mesh geometry with a non-finite attachment.");
            }
            attachment.Offset = new Vector3(
                GenerationTolerances.Quantize(attachment.Offset.x),
                GenerationTolerances.Quantize(attachment.Offset.y),
                GenerationTolerances.Quantize(attachment.Offset.z));
            attachment.Scale = new Vector3(
                GenerationTolerances.Quantize(attachment.Scale.x),
                GenerationTolerances.Quantize(attachment.Scale.y),
                GenerationTolerances.Quantize(attachment.Scale.z));
            attachment.Orientation = QuantizeUtil.CanonicalizeQuaternion(attachment.Orientation);
        }

        private static void AppendChildren(string parentId,
            CreaturePartHierarchyIndex hierarchy,
            List<CreaturePart> orderedParts)
        {
            List<CreaturePart> children = hierarchy.GetChildren(parentId)
                .OrderBy(child => child.Id, System.StringComparer.Ordinal)
                .ToList();
            foreach (CreaturePart child in children)
            {
                orderedParts.Add(child);
                AppendChildren(child.Id, hierarchy, orderedParts);
            }
        }
    }
}
