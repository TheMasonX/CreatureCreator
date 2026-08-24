using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;

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
        /// <summary>
        /// Returns a new CreatureDefinition with every part's transform quantized and
        /// parts sorted into a stable order (by Id, ordinal) for deterministic
        /// serialization (Sprint 1.3: "stable property ordering"). The input is not
        /// mutated.
        /// </summary>
        public static CreatureDefinition Canonicalize(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot canonicalize a null CreatureDefinition.");
            }

            CreatureDefinition result = definition.Clone();

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

            // Stable ordering independent of authoring/insertion order — this is what
            // makes "definition order independence where semantics are unchanged"
            // (§13.4 determinism tests) hold for serialization output.
            var childrenByParent = result.Parts
                .Where(p => p != null)
                .GroupBy(p => p.ParentId ?? string.Empty)
                .ToDictionary(group => group.Key,
                    group => group.OrderBy(p => p.Id, System.StringComparer.Ordinal).ToList());
            var orderedParts = new List<CreaturePart>();
            AppendChildren(CreatureDefinition.BodyId, childrenByParent, orderedParts);
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
            float legacySize = shape.PrimarySize;
            if (shape.Radius <= 0f) shape.Radius = legacySize;
            if (shape.CapsuleHeight <= 0f) shape.CapsuleHeight = 1f;
            if (shape.EllipsoidRadii.x <= 0f) shape.EllipsoidRadii = new Vector3(legacySize, legacySize, legacySize);
            if (shape.BoxHalfExtents.x <= 0f) shape.BoxHalfExtents = new Vector3(legacySize, legacySize, legacySize);
            if (shape.CapsuleAxis < ShapeAxis.X || shape.CapsuleAxis > ShapeAxis.Z) shape.CapsuleAxis = ShapeAxis.Y;
        }

        /// <summary>
        /// Canonicalizes the Body vertical-gradient appearance (CC-025/CC-034):
        /// quantizes gradient key times/colors/alphas and the vertical-curve
        /// keys, and orders each gradient's and the curve's keys by
        /// non-decreasing time for deterministic serialization. Throws on a null
        /// appearance or invalid gradients/curve — canonicalization is not a
        /// repair pass, matching the body-spline and transform rules above.
        /// </summary>
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

        /// <summary>
        /// Canonicalizes a limb chain (CC-018): quantizes every joint position and
        /// the thickness profile's keys, and orders the thickness keys by strictly
        /// increasing T for deterministic serialization. Joint order is preserved
        /// — list order IS the chain order. Throws on non-finite joints or an
        /// invalid thickness profile; canonicalization is not a repair pass,
        /// matching the transform and body-appearance rules above.
        /// </summary>
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

        /// <summary>
        /// Canonicalizes a part's mesh-asset geometry source (CC-031): quantizes the
        /// attachment's offset/orientation/scale and normalizes the orientation, so
        /// repeated serialization is byte-identical. The mesh asset key is a stable
        /// name and is left as authored. Throws on a non-finite attachment;
        /// canonicalization is not a repair pass.
        /// </summary>
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
            attachment.Orientation = NormalizeAndQuantizeQuaternion(attachment.Orientation);
        }

        private static Quaternion NormalizeAndQuantizeQuaternion(Quaternion q)
        {
            return new Quaternion(
                GenerationTolerances.Quantize(q.x),
                GenerationTolerances.Quantize(q.y),
                GenerationTolerances.Quantize(q.z),
                GenerationTolerances.Quantize(q.w)).normalized;
        }

        private static void AppendChildren(string parentId,
            Dictionary<string, List<CreaturePart>> childrenByParent,
            List<CreaturePart> orderedParts)
        {
            if (!childrenByParent.TryGetValue(parentId, out List<CreaturePart> children)) return;
            foreach (CreaturePart child in children)
            {
                orderedParts.Add(child);
                AppendChildren(child.Id, childrenByParent, orderedParts);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
