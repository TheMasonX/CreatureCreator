using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// CreaturePart.Transform is stored relative to the part's parent (see
    /// CreaturePart.cs). Anything that needs a part's position/rotation/scale in
    /// creature-root space — the SDF compiler (Phase 2) and skeleton inference
    /// (Phase 6) both do — composes the parent chain through here rather than each
    /// re-implementing the walk, so there is exactly one place this composition
    /// happens (matching the "don't cache/re-derive relationships in multiple
    /// places" rule from Sprint 1.1).
    ///
    /// CHILD-AT-TIP FRAME (CC-018): a limb's TERMINAL joint is the origin of any
    /// child's local space, so a child authored at local (0,0,0) under a limb sits
    /// at the limb's tip, not its placement root.
    /// ResolvePartFrameToCreatureSpace inserts each ancestor limb's terminal-joint
    /// translation while composing a child's world transform;
    /// ResolveChildFrameToCreatureSpace returns the frame a direct child is
    /// authored in.
    ///
    /// Assumes the definition has already passed DefinitionValidator (no cycles, no
    /// missing parents). Given valid input this never fails; given invalid input it
    /// throws DomainException rather than looping or silently truncating the chain,
    /// since reaching this method with unvalidated DNA is a caller error — every
    /// generation stage is supposed to validate first (§14).
    /// </summary>
    public static class CreaturePartWorldTransformResolver
    {
        /// <summary>
        /// THE canonical part placement frame (CC-051, ADR-002 §7): the
        /// creature-space matrix for <paramref name="part"/>'s own authored
        /// placement, composed from its parent chain (each part's Transform
        /// relative to its parent, plus limb child-at-tip for limb ancestors) and
        /// its own local transform. Every consumer of a part's placement — the
        /// SDF compiler, skeleton inference, the mesh generator, and the editor
        /// viewport — must go through this one method; no consumer re-derives
        /// placement from raw ParentId/Transform/Limb fields.
        ///
        /// PLACEMENT PRECEDENCE (ADR-002 §7): a part has exactly one resolved
        /// morphology frame, from exactly one path. Today that path is Transform +
        /// parent chain + limb child-at-tip. <see cref="BodySurfaceAnchor"/>
        /// (ParentAttachment) is RESERVED-but-inert until CC-007's body-surface
        /// projector lands: it is validated and serialized but is NOT a placement
        /// source, and no code may read its fields for placement except through
        /// this resolver. When CC-007 lands, this method is the single seam that
        /// applies the anchor for Body children.
        ///
        /// Assumes the definition has already passed DefinitionValidator (no
        /// cycles, no missing parents). Given valid input this never fails; given
        /// invalid input it throws DomainException rather than looping or silently
        /// truncating the chain, since reaching this method with unvalidated DNA
        /// is a caller error — every generation stage is supposed to validate
        /// first (§14).
        /// </summary>
        public static Matrix4x4 ResolvePartFrameToCreatureSpace(CreatureDefinition definition, CreaturePart part)
        {
            if (definition == null) throw new DomainException("definition must not be null.");
            if (part == null) throw new DomainException("part must not be null.");

            var chain = new List<CreaturePart>();
            var visited = new HashSet<string>();
            CreaturePart current = part;

            while (true)
            {
                if (!visited.Add(current.Id))
                {
                    throw new DomainException(
                        $"Parent cycle detected while resolving world transform for part '{part.Id}'. " +
                        "This definition should have failed DefinitionValidator before reaching here.");
                }

                chain.Add(current);

                // The Body owns the creature frame; a Body-child's transform is
                // already creature-space (the Body spline defines the origin).
                if (current.ParentId == null || current.ParentId == CreatureDefinition.BodyId) break;

                CreaturePart parent = definition.FindPart(current.ParentId);
                if (parent == null)
                {
                    throw new DomainException(
                        $"Part '{current.Id}' references missing parent '{current.ParentId}'. " +
                        "This definition should have failed DefinitionValidator before reaching here.");
                }

                current = parent;
            }

            chain.Reverse(); // now root-most first, target part last

            Matrix4x4 world = Matrix4x4.identity;
            for (int i = 0; i < chain.Count; i++)
            {
                CreaturePart p = chain[i];
                Quaternion normalizedRotation = p.Transform.Rotation.normalized;
                Matrix4x4 local = Matrix4x4.TRS(p.Transform.Position, normalizedRotation, p.Transform.Scale);
                world *= local;

                // CC-018 (child-at-tip frame): a limb's TERMINAL joint is the
                // origin of any child's local space — a child authored at local
                // (0,0,0) sits at the limb's tip, not at its placement root.
                // Applied only when this part is an ANCESTOR of the resolved part:
                // the resolved part itself keeps its own frame (a limb's joints
                // stay authored root-at-origin per the Joints[0] ≈ zero invariant).
                if (i < chain.Count - 1
                    && p.Limb != null
                    && p.Limb.Joints != null
                    && p.Limb.Joints.Count > 0)
                {
                    world *= Matrix4x4.Translate(p.Limb.Joints[p.Limb.Joints.Count - 1].Position);
                }
            }

            return world;
        }

        /// <summary>
        /// Alias for <see cref="ResolvePartFrameToCreatureSpace"/> retained for
        /// callers that predate CC-051. Every consumer converges on the single
        /// canonical method; the alias guarantees a caller cannot accidentally
        /// drift onto a second placement path.
        /// </summary>
        public static Matrix4x4 ResolveLocalToCreatureSpace(CreatureDefinition definition, CreaturePart part)
        {
            return ResolvePartFrameToCreatureSpace(definition, part);
        }

        /// <summary>
        /// The creature-space matrix of the frame a CHILD of <paramref name="part"/>
        /// is authored in. For a limb parent this is the part matrix extended to
        /// its TERMINAL joint — children are authored relative to the tip, so local
        /// (0,0,0) sits at the limb's end. For any other parent it equals
        /// <see cref="ResolvePartFrameToCreatureSpace"/>. The editor's world→local
        /// conversions use this so dragging/placing a child under a limb produces
        /// tip-relative local coordinates, matching what generation reads back.
        /// </summary>
        public static Matrix4x4 ResolveChildFrameToCreatureSpace(CreatureDefinition definition, CreaturePart part)
        {
            Matrix4x4 m = ResolveLocalToCreatureSpace(definition, part);
            if (part.Limb != null && part.Limb.Joints != null && part.Limb.Joints.Count > 0)
            {
                m *= Matrix4x4.Translate(part.Limb.Joints[part.Limb.Joints.Count - 1].Position);
            }
            return m;
        }
    }
}
