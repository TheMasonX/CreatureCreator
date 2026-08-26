using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Pure CC-007 placement helpers for the editor slice (step 4). A preview-mesh
    /// hit (creature-space position + outward normal) is interaction input only;
    /// this converts it into the semantic <see cref="BodySurfaceAnchor"/> that
    /// becomes authoritative DNA for a direct Body child. Same "pure math, no
    /// UnityEditor API" pattern as LimbAuthoring / BodySplineAuthoring: it never
    /// touches the window, undo, or serialization, so it is EditMode-testable.
    ///
    /// Anchors are produced in the CANONICAL sample-ID space: every hit is
    /// projected against a clone whose Body samples have been renumbered to
    /// 1..N — the same renumber the editor's single mutation path applies via
    /// BodySplineAuthoring.RenumberSamplesInOrder. Projecting against raw
    /// authored IDs would let a non-sequential Body (e.g. a loaded file that is
    /// not already 1..N) yield an anchor the validator rejects after the
    /// mutation renumbers the samples (CC-007 review fix).
    /// </summary>
    public static class BodyPlacementAuthoring
    {
        /// <summary>
        /// Projects a preview-mesh hit onto the resolved Body surface and returns
        /// the semantic anchor that reproduces that surface frame, expressed in
        /// the canonical (renumbered) sample-ID space. False when the Body has
        /// fewer than two samples or the hit cannot be projected (non-finite or
        /// degenerate) — the caller then falls back to a raw creature-space
        /// position.
        /// </summary>
        public static bool TryProjectToAnchor(
            CreatureDefinition definition, Vector3 hitPosition, Vector3 hitNormal, out BodySurfaceAnchor anchor)
        {
            anchor = null;
            if (definition == null || definition.Body == null
                || definition.Body.Samples == null || definition.Body.Samples.Count < 2)
            {
                return false;
            }

            try
            {
                CreatureDefinition canonical = CanonicalClone(definition);
                ResolvedBody body = ResolvedBody.Resolve(canonical.Body);
                anchor = BodySurfaceProjector.ProjectHitToAnchor(
                    body, hitPosition, hitNormal, canonical.Forward);
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }

        /// <summary>
        /// The world position and orientation of the surface frame an anchor
        /// projects to (CC-007). Local +Z is the body Tangent, local +Y the
        /// outward Normal — the same frame convention as
        /// <see cref="BodyFrameResolver"/> and the runtime resolver's
        /// ResolveBodyChildSurfaceFrame — so a caller can express a part's
        /// desired world rotation in the anchor frame's local space. False for a
        /// null definition or anchor, a degenerate Body, or an anchor whose
        /// SegmentStartSampleId is unknown or terminal (the caller then knows the
        /// anchor cannot produce a valid placement frame).
        /// </summary>
        public static bool TryResolveSurfaceFrame(
            CreatureDefinition definition, BodySurfaceAnchor anchor,
            out Vector3 surfacePosition, out Quaternion surfaceRotation)
        {
            surfacePosition = default;
            surfaceRotation = Quaternion.identity;
            if (definition == null || definition.Body == null || anchor == null
                || definition.Body.Samples == null || definition.Body.Samples.Count < 2)
            {
                return false;
            }

            try
            {
                CreatureDefinition canonical = CanonicalClone(definition);
                ResolvedBody body = ResolvedBody.Resolve(canonical.Body);
                BodySurfaceProjection projection = BodySurfaceProjector.Project(body, anchor, canonical.Forward);
                surfacePosition = projection.SurfaceFrame.Position;
                surfaceRotation = Quaternion.LookRotation(
                    projection.SurfaceFrame.Tangent, projection.SurfaceFrame.Normal);
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }

        /// <summary>
        /// The orientation of the surface frame an anchor projects to — the
        /// rotation half of <see cref="TryResolveSurfaceFrame"/>. Throws
        /// <see cref="DomainException"/> for a null definition or anchor, a
        /// degenerate Body, or an anchor whose SegmentStartSampleId is unknown or
        /// terminal.
        /// </summary>
        public static Quaternion ResolveSurfaceFrameRotation(
            CreatureDefinition definition, BodySurfaceAnchor anchor)
        {
            if (!TryResolveSurfaceFrame(definition, anchor, out _, out Quaternion rotation))
            {
                throw new DomainException(
                    "Cannot resolve a surface frame from a null definition or anchor, or a degenerate Body/anchor.");
            }
            return rotation;
        }

        /// <summary>
        /// A deep clone of <paramref name="definition"/> whose Body sample Ids
        /// have been renumbered to the editor's canonical 1..N space. Returns null
        /// for a null input. The source definition is never mutated.
        /// </summary>
        private static CreatureDefinition CanonicalClone(CreatureDefinition definition)
        {
            if (definition == null) return null;
            CreatureDefinition clone = definition.Clone();
            if (clone.Body != null && clone.Body.Samples != null)
            {
                BodySplineAuthoring.RenumberSamplesInOrder(clone.Body);
            }
            return clone;
        }
    }
}
