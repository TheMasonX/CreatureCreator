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
    /// Assumes the definition has already passed DefinitionValidator (no cycles, no
    /// missing parents). Given valid input this never fails; given invalid input it
    /// throws DomainException rather than looping or silently truncating the chain,
    /// since reaching this method with unvalidated DNA is a caller error — every
    /// generation stage is supposed to validate first (§14).
    /// </summary>
    public static class CreaturePartWorldTransformResolver
    {
        public static Matrix4x4 ResolveLocalToCreatureSpace(CreatureDefinition definition, CreaturePart part)
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
            foreach (CreaturePart p in chain)
            {
                Quaternion normalizedRotation = p.Transform.Rotation.normalized;
                Matrix4x4 local = Matrix4x4.TRS(p.Transform.Position, normalizedRotation, p.Transform.Scale);
                world *= local;
            }

            return world;
        }
    }
}
