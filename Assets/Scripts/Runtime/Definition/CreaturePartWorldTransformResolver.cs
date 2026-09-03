using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Definition
{
    public readonly struct ResolvedShape
    {
        public readonly ShapeType Type;
        public readonly float Radius;
        public readonly ShapeAxis CapsuleAxis;
        public readonly float CapsuleHeight;
        public readonly Vector3 EllipsoidRadii;
        public readonly Vector3 BoxHalfExtents;
        public readonly float SmoothBlendRadius;

        private ResolvedShape(ShapeDefinition shape)
        {
            float legacySize = shape.PrimarySize;
            Type = shape.Type;
            Radius = shape.Radius > 0f ? shape.Radius : legacySize;
            CapsuleAxis = shape.CapsuleAxis;
            CapsuleHeight = shape.CapsuleHeight > 0f ? shape.CapsuleHeight : 1f;
            EllipsoidRadii = shape.EllipsoidRadii.x > 0f
                ? shape.EllipsoidRadii
                : new Vector3(legacySize, legacySize, legacySize);
            BoxHalfExtents = shape.BoxHalfExtents.x > 0f
                ? shape.BoxHalfExtents
                : new Vector3(legacySize, legacySize, legacySize);
            SmoothBlendRadius = shape.SmoothBlendRadius;
        }

        public static ResolvedShape Resolve(ShapeDefinition shape)
        {
            return new ResolvedShape(shape);
        }
    }

    /// <summary>
    /// Immutable-by-convention resolved values for one authored part. The
    /// source part remains authoritative; this record contains only derived
    /// hierarchy, morphology, and frame values needed by runtime consumers.
    /// </summary>
    public readonly struct ResolvedPartSnapshot
    {
        public readonly string Id;
        public readonly string ParentId;
        public readonly PartType PartType;
        public readonly TransformData Transform;
        public readonly AppearanceDefinition Appearance;
        public readonly bool MirrorAcrossSymmetryPlane;
        public readonly ResolvedShape Shape;
        public readonly bool HasLimb;
        public readonly ResolvedLimb Limb;
        public readonly Matrix4x4 PartFrameToCreatureSpace;
        public readonly Matrix4x4 ChildFrameToCreatureSpace;
        public readonly bool HasMeshGeometry;
        public readonly string MeshAssetKey;
        public readonly Vector3 GeometryOffset;
        public readonly Quaternion GeometryOrientation;
        public readonly Vector3 GeometryScale;
        public readonly Matrix4x4 GeometryPlacementToCreatureSpace;
        public readonly bool HasBodySurfaceAnchor;
        public readonly uint BodySurfaceAnchorSegmentStartSampleId;

        internal ResolvedPartSnapshot(CreaturePart part, ResolvedLimb limb,
            Matrix4x4 partFrameToCreatureSpace, Matrix4x4 childFrameToCreatureSpace)
        {
            Id = part.Id;
            ParentId = part.ParentId;
            PartType = part.PartType;
            Transform = part.Transform;
            Appearance = part.Appearance;
            MirrorAcrossSymmetryPlane = part.MirrorAcrossSymmetryPlane;
            Shape = ResolvedShape.Resolve(part.Shape);
            HasLimb = part.Limb != null;
            Limb = limb;
            PartFrameToCreatureSpace = partFrameToCreatureSpace;
            ChildFrameToCreatureSpace = childFrameToCreatureSpace;
            HasMeshGeometry = part.MeshGeometry != null;
            MeshAssetKey = part.MeshGeometry?.MeshAssetKey;
            GeometryOffset = part.MeshGeometry?.Attachment?.Offset ?? Vector3.zero;
            GeometryOrientation = (part.MeshGeometry?.Attachment?.Orientation ?? Quaternion.identity).normalized;
            GeometryScale = part.MeshGeometry?.Attachment?.Scale ?? Vector3.one;
            GeometryPlacementToCreatureSpace = partFrameToCreatureSpace * Matrix4x4.TRS(
                GeometryOffset, GeometryOrientation, GeometryScale);
            HasBodySurfaceAnchor = part.ParentAttachment != null;
            BodySurfaceAnchorSegmentStartSampleId = part.ParentAttachment?.SegmentStartSampleId ?? 0u;
        }
    }

    /// <summary>
    /// A resolved, read-only view of a validated CreatureDefinition. Construction
    /// is the only place that walks authored hierarchy and resolves frames;
    /// consumers use the cached values and O(1) part lookup.
    /// </summary>
    public sealed class ResolvedCreatureSnapshot
    {
        private readonly IReadOnlyDictionary<string, ResolvedPartSnapshot> partsById;

        public readonly bool HasBody;
        public readonly ResolvedBody Body;
        public readonly BodyVerticalGradientAppearance BodyAppearance;
        public readonly Vector3 Forward;
        public BoundsDefinition Bounds { get; }
        public GenerationSettings Generation { get; }
        public SymmetryMode SymmetryMode { get; }
        public string RevisionId { get; }
        public IReadOnlyDictionary<string, ResolvedPartSnapshot> PartsById => partsById;

        private ResolvedCreatureSnapshot(bool hasBody, ResolvedBody body,
            BodyVerticalGradientAppearance bodyAppearance, Vector3 forward,
            BoundsDefinition bounds, GenerationSettings generation, SymmetryMode symmetryMode,
            IReadOnlyDictionary<string, ResolvedPartSnapshot> partsById,
            string revisionId)
        {
            HasBody = hasBody;
            Body = body;
            BodyAppearance = bodyAppearance;
            Forward = forward;
            Bounds = bounds;
            Generation = generation;
            SymmetryMode = symmetryMode;
            this.partsById = partsById;
            RevisionId = revisionId;
        }

        public static ResolvedCreatureSnapshot Resolve(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot resolve a null CreatureDefinition.");
            }
            if (definition.Parts == null)
            {
                throw new DomainException("Cannot resolve a CreatureDefinition with null Parts.");
            }

            string revisionId = ComputeRevisionId(definition);

            bool hasBody = definition.Body != null
                && definition.Body.Samples != null
                && definition.Body.Samples.Count > 0;
            ResolvedBody body = default;
            if (hasBody) body = ResolvedBody.Resolve(definition.Body);

            var resolvedParts = new Dictionary<string, ResolvedPartSnapshot>(
                StringComparer.Ordinal);
            var orderedParts = new List<CreaturePart>(definition.Parts);
            orderedParts.Sort((left, right) => string.CompareOrdinal(left?.Id, right?.Id));
            for (int i = 0; i < orderedParts.Count; i++)
            {
                CreaturePart part = orderedParts[i];
                if (part == null || string.IsNullOrEmpty(part.Id))
                {
                    throw new DomainException("Cannot resolve a null or unidentified CreaturePart.");
                }
                if (resolvedParts.ContainsKey(part.Id))
                {
                    throw new DomainException($"Cannot resolve duplicate CreaturePart ID '{part.Id}'.");
                }

                ResolvedLimb limb = default;
                if (part.Limb != null) limb = ResolvedLimb.Resolve(part.Limb);

                Matrix4x4 partFrame = CreaturePartWorldTransformResolver
                    .ResolvePartFrameToCreatureSpace(definition, part);
                Matrix4x4 childFrame = partFrame;
                if (part.Limb != null)
                {
                    childFrame *= Matrix4x4.Translate(limb.TerminalSocket);
                }

                resolvedParts.Add(part.Id, new ResolvedPartSnapshot(
                    part, limb, partFrame, childFrame));
            }

            return new ResolvedCreatureSnapshot(
                hasBody,
                body,
                definition.Body?.Appearance?.Clone(),
                definition.Forward,
                definition.Bounds,
                definition.Generation,
                definition.SymmetryMode,
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, ResolvedPartSnapshot>(resolvedParts),
                revisionId);
        }

        private static string ComputeRevisionId(CreatureDefinition definition)
        {
            string canonicalJson = new JsonDnaSerializer().Serialize(definition);
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalJson));
            }
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        public bool TryGetPart(string id, out ResolvedPartSnapshot part)
        {
            return partsById.TryGetValue(id, out part);
        }
    }

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
        /// morphology frame, from exactly one path. The path is Transform +
        /// parent chain + limb child-at-tip, with one addition: a direct Body
        /// child that carries a <see cref="BodySurfaceAnchor"/> (ParentAttachment)
        /// is placed by projecting the anchor onto the resolved Body surface
        /// (CC-056B). The projected surface frame is the placement root; the
        /// part's local transform is a fine adjustment in that frame's local
        /// space. Anchors stay inert for non-Body children. No code reads anchor
        /// fields for placement except through this resolver; this method is the
        /// single seam that applies the anchor for Body children.
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

                // CC-056B: a direct Body child with a BodySurfaceAnchor is placed
                // by projecting the anchor onto the body surface (ADR-002 §7
                // precedence table: "Body child | BodySurfaceAnchor"). The surface
                // frame is the placement root; the part's local transform is a
                // fine adjustment in that frame's local space.
                if (p.ParentId == CreatureDefinition.BodyId && p.ParentAttachment != null)
                {
                    world *= ResolveBodyChildSurfaceFrame(definition, p);
                }

                world *= local;

                // CC-018 (child-at-tip frame): a limb's TERMINAL joint is the
                // origin of any child's local space — a child authored at local
                // (0,0,0) sits at the limb's tip, not at its placement root.
                // Applied only when this part is an ANCESTOR of the resolved part:
                // the resolved part itself keeps its own frame (a limb's joints
                // stay authored root-at-origin per the Joints[0] ≈ zero invariant).
                if (i < chain.Count - 1 && p.Limb != null)
                {
                    world *= Matrix4x4.Translate(ResolvedLimb.Resolve(p.Limb).TerminalSocket);
                }
            }

            return world;
        }

        /// <summary>
        /// Projects a direct Body child's <see cref="BodySurfaceAnchor"/> onto the
        /// resolved Body surface and returns the resulting placement frame
        /// (CC-056B). The frame is the projected
        /// <see cref="BodySurfaceProjection.SurfaceFrame"/>: position on the body
        /// surface and orientation from the rolled body frame (local +Z ->
        /// Tangent, local +Y -> Normal, matching <see cref="BodyFrameResolver"/>'s
        /// frame convention). Throws <see cref="DomainException"/> for a degenerate
        /// Body or an anchor whose SegmentStartSampleId is unknown or terminal;
        /// DefinitionValidator rejects those before generation.
        /// </summary>
        private static Matrix4x4 ResolveBodyChildSurfaceFrame(CreatureDefinition definition, CreaturePart part)
        {
            ResolvedBody body = ResolvedBody.Resolve(definition.Body);
            BodySurfaceProjection projection = BodySurfaceProjector.Project(
                body, part.ParentAttachment, definition.Forward);
            Quaternion surfaceRotation = Quaternion.LookRotation(
                projection.SurfaceFrame.Tangent, projection.SurfaceFrame.Normal);
            return Matrix4x4.TRS(projection.SurfaceFrame.Position, surfaceRotation, Vector3.one);
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
            if (part.Limb != null)
            {
                m *= Matrix4x4.Translate(ResolvedLimb.Resolve(part.Limb).TerminalSocket);
            }
            return m;
        }
    }
}
