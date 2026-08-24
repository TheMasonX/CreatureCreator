using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// Compiles a validated CreatureDefinition into a single composed ISdfNode
    /// (implementation guide §11 "Definition-to-SDF compiler"). Read-only over its
    /// input — never mutates or writes back to the definition (§16 "the compiler
    /// never modifies DNA").
    ///
    /// Callers must run DefinitionValidator first; this class assumes valid input
    /// and throws DomainException (a programmer-error signal, not a data-error
    /// signal) if that assumption is violated, per the project's exception policy.
    ///
    /// DETERMINISTIC ORDERING RULE (§2.3 "Establish deterministic ordering rules"):
    /// parts are compiled and folded into the union chain in ascending Id order
    /// (ordinal string comparison) — the same rule DefinitionCanonicalizer uses for
    /// serialization — regardless of Parts list authoring/insertion order. Parent
    /// hierarchy (used only for computing each part's creature-space transform via
    /// CreaturePartWorldTransformResolver) is kept entirely separate from this fold
    /// order, per §2.3's "keep assembly parent relationships separate from SDF
    /// composition order."
    /// </summary>
    public static class SdfProgramBuilder
    {
        /// <summary>
        /// Blend radius used when smooth-uniting adjacent Body spline samples. The
        /// Body spline does not carry per-sample blend radii; this deterministic
        /// fraction of the smaller adjacent radius keeps the primary field
        /// continuous without introducing a new schema field. A Spore-like
        /// fourth-order metaball falloff is a later decision that needs scalar
        /// parity tests before replacing this smooth-union path.
        /// </summary>
        private const float BodySampleBlendFactor = 0.5f;

        /// <summary>
        /// Blend radius used when smooth-uniting adjacent derived limb metaballs
        /// (CC-018). Same deterministic fraction-of-smaller-radius rule as the
        /// Body spline, so the limb reads as one continuous chain rather than a
        /// string of beads.
        /// </summary>
        private const float LimbSampleBlendFactor = 0.5f;

        /// <summary>
        /// The creature-space reflection across the X = 0 plane, matching the
        /// convention SymmetryNode uses at the SDF layer. Used to mirror a limb
        /// chain in the portable path by LEFT-multiplying the part's creature-space
        /// matrix (the mirror of a composed transform), so the mirrored side lands
        /// across the creature's X plane regardless of where the part is placed.
        /// </summary>
        private static readonly Matrix4x4 CreatureMirrorAcrossX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        public static SdfProgram CompilePortable(CreatureDefinition definition)
        {
            if (definition == null) throw new DomainException("Cannot compile a null CreatureDefinition.");

            var operations = new List<SdfOperation>();
            List<CreaturePart> orderedParts = definition.Parts
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();

            // The Body spline is the primary field, composed before any child
            // attachment. Body samples are ordered by their authoritative spline
            // order (list index), not by ID — sample order IS the spline.
            bool hasBodySamples = definition.Body != null
                && definition.Body.Samples != null
                && definition.Body.Samples.Count > 0;

            if (orderedParts.Count == 0 && !hasBodySamples)
            {
                operations.Add(SdfOperation.Primitive(SdfOperationType.Empty, float3.zero));
                return new SdfProgram(new NativeArray<SdfOperation>(operations.ToArray(), Allocator.Persistent), 0);
            }

            int root = -1;
            if (hasBodySamples)
            {
                for (int i = 0; i < definition.Body.Samples.Count; i++)
                {
                    BodySample sample = definition.Body.Samples[i];
                    int primitive = operations.Count;
                    operations.Add(SdfOperation.Primitive(SdfOperationType.Sphere, new float3(sample.Radius, 0f, 0f)));

                    Matrix4x4 localToCreature = Matrix4x4.TRS(sample.Position, Quaternion.identity, Vector3.one);
                    Matrix4x4 worldToLocal = localToCreature.inverse;
                    operations.Add(new SdfOperation
                    {
                        Type = SdfOperationType.Transform,
                        A = primitive,
                        Matrix = ToFloat4x4(worldToLocal),
                        DistanceScale = 1f,
                    });
                    int bodyNode = operations.Count - 1;

                    if (root >= 0)
                    {
                        BodySample previous = definition.Body.Samples[i - 1];
                        float blend = Mathf.Min(previous.Radius, sample.Radius) * BodySampleBlendFactor;
                        operations.Add(new SdfOperation
                        {
                            Type = SdfOperationType.SmoothUnion,
                            A = root,
                            B = bodyNode,
                            Parameters = new float3(blend, 0f, 0f),
                        });
                        root = operations.Count - 1;
                    }
                    else
                    {
                        root = bodyNode;
                    }
                }
            }

            foreach (CreaturePart part in orderedParts)
            {
                // A mesh-asset part (CC-031) has no implicit surface; its geometry
                // comes from the resolved mesh asset, so it does not join the
                // portable SDF field.
                if (part.MeshGeometry != null) continue;

                int previousRoot = root;
                Matrix4x4 localToCreature = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part);
                Vector3 scale = localToCreature.lossyScale;
                float distanceScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                bool shouldMirror = part.MirrorAcrossSymmetryPlane && definition.SymmetryMode != SymmetryMode.None;

                int primitive;
                if (part.Limb != null)
                {
                    // A limb compiles to a chain of derived metaball spheres with
                    // the part's creature-space transform BAKED into each ball's
                    // local transform (CC-018 Phase 5). The portable Transform op
                    // can only wrap a primitive, and the portable Symmetry op can
                    // only wrap a primitive/transform subtree (its recursion reads
                    // values computed for the unmirrored point), so a mirrored
                    // limb bakes a mirrored copy of the chain in instead of using
                    // a Symmetry op — the hard union of the two sides equals
                    // SymmetryNode(chain) = min(chain(x), chain(-x)) exactly.
                    primitive = CompileLimbChainPortable(operations, part.Limb, localToCreature, distanceScale, shouldMirror);
                }
                else
                {
                    SdfOperationType primitiveType;
                    switch (part.Shape.Type)
                    {
                        case ShapeType.Sphere:
                            primitiveType = SdfOperationType.Sphere;
                            break;
                        case ShapeType.Ellipsoid:
                            primitiveType = SdfOperationType.Ellipsoid;
                            break;
                        case ShapeType.Box:
                            primitiveType = SdfOperationType.Box;
                            break;
                        case ShapeType.Capsule:
                            primitiveType = SdfOperationType.Capsule;
                            break;
                        default:
                            throw new DomainException($"No portable SDF primitive mapping exists for ShapeType.{part.Shape.Type}.");
                    }

                    float legacySize = part.Shape.PrimarySize;
                    float radius = part.Shape.Radius > 0f ? part.Shape.Radius : legacySize;
                    float height = part.Shape.CapsuleHeight > 0f ? part.Shape.CapsuleHeight : 1f;
                    float3 boxHalfExtents = part.Shape.BoxHalfExtents.x > 0f
                        ? new float3(part.Shape.BoxHalfExtents.x, part.Shape.BoxHalfExtents.y, part.Shape.BoxHalfExtents.z)
                        : new float3(legacySize);
                    float3 parameters = primitiveType == SdfOperationType.Box
                        ? boxHalfExtents
                        : primitiveType == SdfOperationType.Capsule
                            ? new float3(radius, height, (int)part.Shape.CapsuleAxis)
                            : primitiveType == SdfOperationType.Ellipsoid
                                ? (part.Shape.EllipsoidRadii.x > 0f ? new float3(part.Shape.EllipsoidRadii.x, part.Shape.EllipsoidRadii.y, part.Shape.EllipsoidRadii.z) : new float3(legacySize))
                                : new float3(radius, 0f, 0f);
                    primitive = operations.Count;
                    operations.Add(SdfOperation.Primitive(primitiveType, parameters));

                    Matrix4x4 worldToLocal = localToCreature.inverse;
                    operations.Add(new SdfOperation
                    {
                        Type = SdfOperationType.Transform,
                        A = primitive,
                        Matrix = ToFloat4x4(worldToLocal),
                        DistanceScale = distanceScale,
                    });
                    primitive = operations.Count - 1;

                    if (shouldMirror)
                    {
                        operations.Add(new SdfOperation { Type = SdfOperationType.Symmetry, A = primitive });
                        primitive = operations.Count - 1;
                    }
                }
                root = primitive;

                if (previousRoot >= 0)
                {
                    operations.Add(new SdfOperation
                    {
                        Type = SdfOperationType.SmoothUnion,
                        A = previousRoot,
                        B = root,
                        Parameters = new float3(part.Shape.SmoothBlendRadius, 0f, 0f),
                    });
                    root = operations.Count - 1;
                }
            }

            return new SdfProgram(new NativeArray<SdfOperation>(operations.ToArray(), Allocator.Persistent), root);
        }

        private static float4x4 ToFloat4x4(Matrix4x4 matrix)
        {
            return new float4x4(
                new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
        }

        public static ISdfNode Compile(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot compile a null CreatureDefinition.");
            }

            bool hasBodySamples = definition.Body != null
                && definition.Body.Samples != null
                && definition.Body.Samples.Count > 0;

            List<(CreaturePart Part, ISdfNode Node)> compiled = CompileIndividualParts(definition);
            if (compiled.Count == 0 && !hasBodySamples)
            {
                return new EmptySdfNode();
            }

            ISdfNode accumulated = CompileBodyField(definition);
            for (int i = 0; i < compiled.Count; i++)
            {
                float blendRadius = compiled[i].Part.Shape.SmoothBlendRadius;
                accumulated = accumulated == null
                    ? compiled[i].Node
                    : new SmoothUnionNode(accumulated, compiled[i].Node, blendRadius);
            }

            return accumulated;
        }

        /// <summary>
        /// Compiles the Body spline into the primary implicit surface: one sphere
        /// per sample at its authoritative position/radius, smooth-united in spline
        /// order. Returns null when the definition has no Body samples. Exposed
        /// publicly for consumers that need to reason about the Body field on its
        /// own — Phase 4's appearance resolver uses it to decide whether a surface
        /// point belongs to the Body (and therefore takes the Body's vertical-
        /// gradient appearance) rather than to a part.
        /// </summary>
        public static ISdfNode CompileBodyField(CreatureDefinition definition)
        {
            if (definition.Body == null || definition.Body.Samples == null || definition.Body.Samples.Count == 0)
            {
                return null;
            }

            ISdfNode accumulated = null;
            for (int i = 0; i < definition.Body.Samples.Count; i++)
            {
                BodySample sample = definition.Body.Samples[i];
                ISdfNode sphere = new TransformNode(
                    new SphereSdfNode(sample.Radius),
                    Matrix4x4.TRS(sample.Position, Quaternion.identity, Vector3.one));

                if (accumulated == null)
                {
                    accumulated = sphere;
                    continue;
                }

                BodySample previous = definition.Body.Samples[i - 1];
                float blend = Mathf.Min(previous.Radius, sample.Radius) * BodySampleBlendFactor;
                accumulated = new SmoothUnionNode(accumulated, sphere, blend);
            }

            return accumulated;
        }

        /// <summary>
        /// Compiles each part into its own standalone ISdfNode (including its own
        /// transform and, if flagged, its own symmetry mirror) WITHOUT folding them
        /// into a single unioned body. Exposed for consumers that need to reason
        /// about individual parts rather than the composed whole — Phase 4's
        /// appearance baker uses this to determine which part's appearance
        /// parameters apply at a given surface point (see
        /// Appearance/PartAppearanceSampler.cs), and Phase 6's skeleton inferer
        /// will have the same need for per-part identity. Ordered the same way
        /// Compile's fold order is (ascending Id), though callers needing per-part
        /// data generally don't care about fold order — it's just a stable,
        /// deterministic order to hand back.
        /// </summary>
        public static List<(CreaturePart Part, ISdfNode Node)> CompileIndividualParts(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot compile a null CreatureDefinition.");
            }

            List<CreaturePart> orderedParts = definition.Parts
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();

            return orderedParts
                .Where(part => part.MeshGeometry == null)
                .Select(part => (part, CompilePart(definition, part)))
                .ToList();
        }

        private static ISdfNode CompilePart(CreatureDefinition definition, CreaturePart part)
        {
            ISdfNode primitive = CompilePartGeometry(part);

            Matrix4x4 localToCreatureSpace =
                CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part);
            ISdfNode transformed = new TransformNode(primitive, localToCreatureSpace);

            bool shouldMirror = part.MirrorAcrossSymmetryPlane
                                 && definition.SymmetryMode != SymmetryMode.None;

            return shouldMirror ? new SymmetryNode(transformed) : transformed;
        }

        /// <summary>
        /// The geometry source for a part in its local frame. A limb part compiles
        /// to its derived metaball chain (CC-018); a mesh-asset part (CC-031) has no
        /// implicit surface and compiles to empty — its geometry comes from the
        /// resolved mesh asset, not the SDF field; any other part compiles to its
        /// single Shape primitive. <see cref="Shape"/> is inert for limb and mesh
        /// parts (ADR-001 §2, ADR-002 §2).
        /// </summary>
        private static ISdfNode CompilePartGeometry(CreaturePart part)
        {
            if (part.MeshGeometry != null) return new EmptySdfNode();
            return part.Limb != null ? CompileLimbChain(part.Limb) : CompilePrimitive(part.Shape);
        }

        /// <summary>
        /// Compiles a limb chain (CC-018 Phase 5) into a smooth-union of sphere
        /// nodes, one per derived metaball, in the limb's local frame. Metaball
        /// positions and radii come from <see cref="LimbMetaballSampler"/> —
        /// derived geometry that is never serialized.
        /// </summary>
        private static ISdfNode CompileLimbChain(LimbChain limb)
        {
            List<LimbMetaball> metaballs = LimbMetaballSampler.Sample(limb);
            if (metaballs.Count == 1)
            {
                return new TransformNode(
                    new SphereSdfNode(metaballs[0].Radius),
                    Matrix4x4.TRS(metaballs[0].Position, Quaternion.identity, Vector3.one));
            }

            ISdfNode accumulated = null;
            for (int i = 0; i < metaballs.Count; i++)
            {
                ISdfNode ball = new TransformNode(
                    new SphereSdfNode(metaballs[i].Radius),
                    Matrix4x4.TRS(metaballs[i].Position, Quaternion.identity, Vector3.one));

                if (accumulated == null)
                {
                    accumulated = ball;
                    continue;
                }

                LimbMetaball previous = metaballs[i - 1];
                float blend = Mathf.Min(previous.Radius, metaballs[i].Radius) * LimbSampleBlendFactor;
                accumulated = new SmoothUnionNode(accumulated, ball, blend);
            }

            return accumulated;
        }

        /// <summary>
        /// Appends the portable operations for a limb chain: one sphere primitive
        /// plus a baked local-space transform per derived metaball, smooth-united
        /// in chain order. When <paramref name="includeMirror"/> is true, a second
        /// copy of the chain is emitted under the creature-space X mirror and the
        /// two sides are hard-unioned (blend 0). This reproduces
        /// <c>SymmetryNode(chain) = min(chain(x), chain(-x))</c> exactly without a
        /// portable Symmetry op, which cannot wrap a composite subtree.
        ///
        /// The mirror is a CREATURE-SPACE reflection of the composed transform,
        /// not a per-ball local-X negation: the mirrored ball position must be
        /// <c>S · (localToCreature · localPos)</c>, i.e. the part matrix
        /// LEFT-multiplied by <see cref="CreatureMirrorAcrossX"/> applied to the
        /// ORIGINAL joint position. Negating the joint's local X and reusing the
        /// unmirrored part matrix is wrong whenever the part is placed away from
        /// the creature X plane (the normal limb case — a leg authored at
        /// x = 0.5 would then render its "mirror" back on the same side). The
        /// part's creature-space transform and distance scale are baked into each
        /// ball's transform, so the returned root is ready for the outer creature
        /// union.
        /// </summary>
        private static int CompileLimbChainPortable(List<SdfOperation> operations, LimbChain limb,
            Matrix4x4 localToCreature, float distanceScale, bool includeMirror)
        {
            List<LimbMetaball> metaballs = LimbMetaballSampler.Sample(limb);
            int originalRoot = -1;
            int mirroredRoot = -1;

            // The creature-space mirror of the part's transform: S · localToCreature.
            // Each mirrored ball keeps its ORIGINAL local position and is placed by
            // this mirrored matrix, so its world position equals S · (original world
            // position) — the same result SymmetryNode produces for the managed path.
            Matrix4x4 mirroredPartMatrix = CreatureMirrorAcrossX * localToCreature;

            for (int i = 0; i < metaballs.Count; i++)
            {
                int original = AppendLimbBall(operations, localToCreature, distanceScale, metaballs[i].Position, metaballs[i].Radius);
                originalRoot = UnionLimbBall(operations, originalRoot, original, metaballs, i);

                if (includeMirror)
                {
                    int mirrored = AppendLimbBall(operations, mirroredPartMatrix, distanceScale, metaballs[i].Position, metaballs[i].Radius);
                    mirroredRoot = UnionLimbBall(operations, mirroredRoot, mirrored, metaballs, i);
                }
            }

            if (!includeMirror) return originalRoot;

            operations.Add(new SdfOperation
            {
                Type = SdfOperationType.SmoothUnion,
                A = originalRoot,
                B = mirroredRoot,
                Parameters = new float3(0f, 0f, 0f), // blend 0 = hard min
            });
            return operations.Count - 1;
        }

        private static int AppendLimbBall(List<SdfOperation> operations, Matrix4x4 localToCreature,
            float distanceScale, Vector3 localPosition, float radius)
        {
            int primitive = operations.Count;
            operations.Add(SdfOperation.Primitive(SdfOperationType.Sphere, new float3(radius, 0f, 0f)));

            Matrix4x4 localToCreatureForBall = localToCreature * Matrix4x4.TRS(localPosition, Quaternion.identity, Vector3.one);
            operations.Add(new SdfOperation
            {
                Type = SdfOperationType.Transform,
                A = primitive,
                Matrix = ToFloat4x4(localToCreatureForBall.inverse),
                DistanceScale = distanceScale,
            });
            return operations.Count - 1;
        }

        private static int UnionLimbBall(List<SdfOperation> operations, int root, int ball,
            List<LimbMetaball> metaballs, int i)
        {
            if (root < 0) return ball;
            float blend = Mathf.Min(metaballs[i - 1].Radius, metaballs[i].Radius) * LimbSampleBlendFactor;
            operations.Add(new SdfOperation
            {
                Type = SdfOperationType.SmoothUnion,
                A = root,
                B = ball,
                Parameters = new float3(blend, 0f, 0f),
            });
            return operations.Count - 1;
        }

        private static ISdfNode CompilePrimitive(ShapeDefinition shape)
        {
            switch (shape.Type)
            {
                case ShapeType.Sphere:
                    return new SphereSdfNode(shape.Radius > 0f ? shape.Radius : shape.PrimarySize);
                case ShapeType.Box:
                    Vector3 halfExtents = shape.BoxHalfExtents.x > 0f
                        ? shape.BoxHalfExtents
                        : new Vector3(shape.PrimarySize, shape.PrimarySize, shape.PrimarySize);
                    return new BoxSdfNode(halfExtents);
                case ShapeType.Capsule:
                    return new CapsuleSdfNode(
                        shape.Radius > 0f ? shape.Radius : shape.PrimarySize,
                        shape.CapsuleHeight > 0f ? shape.CapsuleHeight : 1f,
                        shape.CapsuleAxis);
                case ShapeType.Ellipsoid:
                    return new EllipsoidSdfNode(shape.EllipsoidRadii.x > 0f
                        ? shape.EllipsoidRadii
                        : new Vector3(shape.PrimarySize, shape.PrimarySize, shape.PrimarySize));
                default:
                    // Validated definitions never reach here (DefinitionValidator's
                    // UnsupportedPartType-adjacent checks run on PartType, and
                    // ShapeType is a closed enum with no "unknown" value today) —
                    // this is a genuine future-proofing guard for whoever adds a
                    // ShapeType case without adding it here too.
                    throw new DomainException($"No SDF primitive mapping exists for ShapeType.{shape.Type}.");
            }
        }
    }
}
