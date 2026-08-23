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
                int previousRoot = root;
                int primitive = operations.Count;
                SdfOperationType primitiveType;
                switch (part.Shape.Type)
                {
                    case ShapeType.Sphere:
                    case ShapeType.Ellipsoid:
                        primitiveType = SdfOperationType.Sphere;
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

                float size = part.Shape.PrimarySize;
                float3 parameters = primitiveType == SdfOperationType.Box
                    ? new float3(size)
                    : new float3(size, 0f, 0f);
                operations.Add(SdfOperation.Primitive(primitiveType, parameters));

                Matrix4x4 localToCreature = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part);
                Matrix4x4 worldToLocal = localToCreature.inverse;
                Vector3 scale = localToCreature.lossyScale;
                float distanceScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                operations.Add(new SdfOperation
                {
                    Type = SdfOperationType.Transform,
                    A = primitive,
                    Matrix = ToFloat4x4(worldToLocal),
                    DistanceScale = distanceScale,
                });
                root = operations.Count - 1;

                if (part.MirrorAcrossSymmetryPlane && definition.SymmetryMode != SymmetryMode.None)
                {
                    operations.Add(new SdfOperation { Type = SdfOperationType.Symmetry, A = root });
                    root = operations.Count - 1;
                }

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
                .Select(part => (part, CompilePart(definition, part)))
                .ToList();
        }

        private static ISdfNode CompilePart(CreatureDefinition definition, CreaturePart part)
        {
            ISdfNode primitive = CompilePrimitive(part.Shape);

            Matrix4x4 localToCreatureSpace =
                CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part);
            ISdfNode transformed = new TransformNode(primitive, localToCreatureSpace);

            bool shouldMirror = part.MirrorAcrossSymmetryPlane
                                 && definition.SymmetryMode != SymmetryMode.None;

            return shouldMirror ? new SymmetryNode(transformed) : transformed;
        }

        private static ISdfNode CompilePrimitive(ShapeDefinition shape)
        {
            switch (shape.Type)
            {
                case ShapeType.Sphere:
                    return new SphereSdfNode(shape.PrimarySize);
                case ShapeType.Box:
                    return new BoxSdfNode(new Vector3(shape.PrimarySize, shape.PrimarySize, shape.PrimarySize));
                case ShapeType.Capsule:
                    return new CapsuleSdfNode(shape.PrimarySize);
                case ShapeType.Ellipsoid:
                    return new EllipsoidSdfNode(shape.PrimarySize);
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
