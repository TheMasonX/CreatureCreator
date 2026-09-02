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
    /// Compiles a validated CreatureDefinition into a portable SDF operation program
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
        /// convention used by the portable symmetry operation. Used to mirror a limb
        /// chain in the portable path by LEFT-multiplying the part's creature-space
        /// matrix (the mirror of a composed transform), so the mirrored side lands
        /// across the creature's X plane regardless of where the part is placed.
        /// </summary>
        private static readonly Matrix4x4 CreatureMirrorAcrossX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        /// <summary>
        /// World-space axis-aligned bounding box used for evaluator culling (CC-062).
        /// </summary>
        private readonly struct Aabb
        {
            public readonly float3 Min;
            public readonly float3 Max;

            public Aabb(float3 min, float3 max)
            {
                Min = min;
                Max = max;
            }

            public static Aabb Union(Aabb a, Aabb b)
            {
                return new Aabb(math.min(a.Min, b.Min), math.max(a.Max, b.Max));
            }

            /// <summary>Reflects the box across the X = 0 plane (creature-space mirror).</summary>
            public static Aabb MirrorAcrossX(Aabb a)
            {
                return new Aabb(
                    new float3(-a.Max.x, a.Min.y, a.Min.z),
                    new float3(-a.Min.x, a.Max.y, a.Max.z));
            }
        }

        /// <summary>Local-space AABB of a primitive operation, matching its SDF.</summary>
        private static Aabb PrimitiveLocalAabb(SdfOperation op)
        {
            switch (op.Type)
            {
                case SdfOperationType.Sphere:
                {
                    float r = op.Parameters.x;
                    return new Aabb(new float3(-r), new float3(r));
                }
                case SdfOperationType.Box:
                    return new Aabb(-op.Parameters, op.Parameters);
                case SdfOperationType.Capsule:
                {
                    float r = op.Parameters.x;
                    float half = op.Parameters.y * 0.5f;
                    int axis = (int)op.Parameters.z;
                    if (axis == 0)
                    {
                        return new Aabb(new float3(-half - r, -r, -r), new float3(half + r, r, r));
                    }
                    if (axis == 2)
                    {
                        return new Aabb(new float3(-r, -r, -half - r), new float3(r, r, half + r));
                    }
                    return new Aabb(new float3(-r, -half - r, -r), new float3(r, half + r, r));
                }
                case SdfOperationType.Ellipsoid:
                    return new Aabb(-op.Parameters, op.Parameters);
                default:
                    // Empty and non-primitive ops carry no geometry; an empty AABB
                    // means always culled (slot reads +inf).
                    return new Aabb(new float3(float.PositiveInfinity), new float3(float.NegativeInfinity));
            }
        }

        /// <summary>World AABB of an op's geometry by transforming its local AABB corners.</summary>
        private static Aabb TransformToWorld(Aabb local, Matrix4x4 localToCreature)
        {
            float3 mn = new float3(float.PositiveInfinity);
            float3 mx = new float3(float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 1) == 0 ? local.Min.x : local.Max.x;
                float y = (i & 2) == 0 ? local.Min.y : local.Max.y;
                float z = (i & 4) == 0 ? local.Min.z : local.Max.z;
                Vector3 w = localToCreature.MultiplyPoint3x4(new Vector3(x, y, z));
                float3 wf = new float3(w.x, w.y, w.z);
                mn = math.min(mn, wf);
                mx = math.max(mx, wf);
            }
            return new Aabb(mn, mx);
        }

        private static Aabb ReadAabb(List<SdfOperation> operations, int index)
        {
            return new Aabb(operations[index].MinBound, operations[index].MaxBound);
        }

        private static void SetWorldAabb(List<SdfOperation> operations, int index, Aabb aabb)
        {
            SdfOperation op = operations[index];
            op.MinBound = aabb.Min;
            op.MaxBound = aabb.Max;
            op.ConsumerUnionIndex = -1;
            op.Cullable = false;
            operations[index] = op;
        }

        /// <summary>Marks <paramref name="childIndex"/> as the newly-added (B) child
        /// of the union at <paramref name="unionIndex"/>, so the evaluator can
        /// cull it against the union's already-evaluated chain value (CC-062).</summary>
        private static void SetConsumer(List<SdfOperation> operations, int childIndex, int unionIndex)
        {
            SdfOperation op = operations[childIndex];
            op.ConsumerUnionIndex = unionIndex;
            operations[childIndex] = op;
        }

        private static void SetCullable(List<SdfOperation> operations, int index, bool cullable)
        {
            SdfOperation op = operations[index];
            op.Cullable = cullable;
            operations[index] = op;
        }

        private static bool ReadCullable(List<SdfOperation> operations, int index)
        {
            return operations[index].Cullable;
        }

        private static float ComputeInfluenceRadius(List<SdfOperation> operations)
        {
            float maxBlend = 0f;
            for (int i = 0; i < operations.Count; i++)
            {
                if (operations[i].Type == SdfOperationType.SmoothUnion)
                {
                    maxBlend = Mathf.Max(maxBlend, operations[i].Parameters.x);
                }
            }
            return maxBlend + 1e-4f;
        }

        /// <summary>
        /// The blend radius used to unite a part into the creature field
        /// (CC-049). For a limb part, <see cref="ShapeDefinition.SmoothBlendRadius"/>
        /// is inert (CC-018/ADR-001), so the blend comes from the limb's own
        /// <see cref="LimbChain.BlendRadius"/>. For a shape part, the shape's
        /// SmoothBlendRadius is the authority. Mesh-asset parts never enter the
        /// implicit field.
        /// </summary>
        private static float PartUnionBlendRadius(CreaturePart part)
        {
            return part.Limb != null ? part.Limb.BlendRadius : part.Shape.SmoothBlendRadius;
        }

        private static float PartUnionBlendRadius(ResolvedPartSnapshot part)
        {
            return part.HasLimb ? part.Limb.BlendRadius : part.Shape.SmoothBlendRadius;
        }

        public static SdfProgram CompilePortable(CreatureDefinition definition)
        {
            if (definition == null) throw new DomainException("Cannot compile a null CreatureDefinition.");

            return CompilePortable(definition, ResolvedCreatureSnapshot.Resolve(definition));
        }

        public static SdfProgram CompilePortable(
            CreatureDefinition definition, ResolvedCreatureSnapshot snapshot)
        {
            if (definition == null) throw new DomainException("Cannot compile a null CreatureDefinition.");
            if (snapshot == null) throw new DomainException("Cannot compile with a null resolved snapshot.");

            var operations = new List<SdfOperation>();
            List<ResolvedPartSnapshot> orderedParts = snapshot.PartsById.Values
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();

            // The Body spline is the primary field, composed before any child
            // attachment. Body samples are ordered by their authoritative spline
            // order (list index), not by ID — sample order IS the spline.
            bool hasBodySamples = snapshot.HasBody;

            if (orderedParts.Count == 0 && !hasBodySamples)
            {
                operations.Add(SdfOperation.Primitive(SdfOperationType.Empty, float3.zero));
                return new SdfProgram(new NativeArray<SdfOperation>(operations.ToArray(), Allocator.Persistent), 0, 0f);
            }

            int root = -1;
            if (hasBodySamples)
            {
                // CC-056A increment 3: consume the shared ResolvedBody derivation
                // (positions/radii) instead of reading authored samples here.
                ResolvedBody body = snapshot.Body;
                for (int i = 0; i < body.SamplePositions.Count; i++)
                {
                    Vector3 position = body.SamplePositions[i];
                    float radius = body.SampleRadii[i];
                    int primitive = operations.Count;
                    operations.Add(SdfOperation.Primitive(SdfOperationType.Sphere, new float3(radius, 0f, 0f)));

                    Matrix4x4 localToCreature = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                    Matrix4x4 worldToLocal = localToCreature.inverse;
                    int bodyNode = operations.Count;
                    operations.Add(new SdfOperation
                    {
                        Type = SdfOperationType.Transform,
                        A = primitive,
                        Matrix = ToFloat4x4(worldToLocal),
                        DistanceScale = 1f,
                    });
                    SetWorldAabb(operations, bodyNode, TransformToWorld(PrimitiveLocalAabb(operations[primitive]), localToCreature));
                    SetCullable(operations, bodyNode, true);

                    if (root >= 0)
                    {
                        float blend = Mathf.Min(body.SampleRadii[i - 1], radius) * BodySampleBlendFactor;
                        int unionIndex = operations.Count;
                        operations.Add(new SdfOperation
                        {
                            Type = SdfOperationType.SmoothUnion,
                            A = root,
                            B = bodyNode,
                            Parameters = new float3(blend, 0f, 0f),
                        });
                        SetWorldAabb(operations, unionIndex, Aabb.Union(ReadAabb(operations, root), ReadAabb(operations, bodyNode)));
                        SetCullable(operations, unionIndex, ReadCullable(operations, root) && ReadCullable(operations, bodyNode));
                        SetConsumer(operations, bodyNode, unionIndex);
                        root = unionIndex;
                    }
                    else
                    {
                        root = bodyNode;
                    }
                }
            }

            foreach (ResolvedPartSnapshot part in orderedParts)
            {
                // A mesh-asset part (CC-031) has no implicit surface; its geometry
                // comes from the resolved mesh asset, so it does not join the
                // portable SDF field.
                if (part.HasMeshGeometry) continue;

                int previousRoot = root;
                Matrix4x4 localToCreature = part.PartFrameToCreatureSpace;
                Vector3 scale = localToCreature.lossyScale;
                float distanceScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                bool shouldMirror = part.MirrorAcrossSymmetryPlane && snapshot.SymmetryMode != SymmetryMode.None;

                int primitive;
                if (part.HasLimb)
                {
                    // A limb compiles to a chain of derived metaball spheres with
                    // the part's creature-space transform BAKED into each ball's
                    // local transform (CC-018 Phase 5). The portable Transform op
                    // can only wrap a primitive, and the portable Symmetry op can
                    // only wrap a primitive/transform subtree (its recursion reads
                    // values computed for the unmirrored point), so a mirrored
                    // limb bakes a mirrored copy of the chain in instead of using
                    // a Symmetry op — the hard union of the two sides equals
                    // Portable symmetry evaluates the original and reflected roots.
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

                    ResolvedShape shape = part.Shape;
                    float radius = shape.Radius;
                    float height = shape.CapsuleHeight;
                    float3 boxHalfExtents = new float3(shape.BoxHalfExtents.x, shape.BoxHalfExtents.y, shape.BoxHalfExtents.z);
                    float3 parameters = primitiveType == SdfOperationType.Box
                        ? boxHalfExtents
                        : primitiveType == SdfOperationType.Capsule
                            ? new float3(radius, height, (int)shape.CapsuleAxis)
                            : primitiveType == SdfOperationType.Ellipsoid
                                ? new float3(shape.EllipsoidRadii.x, shape.EllipsoidRadii.y, shape.EllipsoidRadii.z)
                                : new float3(radius, 0f, 0f);
                    primitive = operations.Count;
                    operations.Add(SdfOperation.Primitive(primitiveType, parameters));
                    int primitiveIndex = primitive;

                    Matrix4x4 worldToLocal = localToCreature.inverse;
                    int transformIndex = operations.Count;
                    operations.Add(new SdfOperation
                    {
                        Type = SdfOperationType.Transform,
                        A = primitiveIndex,
                        Matrix = ToFloat4x4(worldToLocal),
                        DistanceScale = distanceScale,
                    });
                    SetWorldAabb(operations, transformIndex, TransformToWorld(PrimitiveLocalAabb(operations[primitiveIndex]), localToCreature));
                    SetCullable(operations, transformIndex, primitiveType != SdfOperationType.Ellipsoid);
                    primitive = transformIndex;

                    if (shouldMirror)
                    {
                        int symmetryIndex = operations.Count;
                        operations.Add(new SdfOperation { Type = SdfOperationType.Symmetry, A = primitive });
                        Aabb child = ReadAabb(operations, primitive);
                        SetWorldAabb(operations, symmetryIndex, Aabb.Union(child, Aabb.MirrorAcrossX(child)));
                        SetCullable(operations, symmetryIndex, ReadCullable(operations, primitive));
                        primitive = symmetryIndex;
                    }
                }
                root = primitive;

                if (previousRoot >= 0)
                {
                    int unionIndex = operations.Count;
                    operations.Add(new SdfOperation
                    {
                        Type = SdfOperationType.SmoothUnion,
                        A = previousRoot,
                        B = root,
                        Parameters = new float3(PartUnionBlendRadius(part), 0f, 0f),
                    });
                    SetWorldAabb(operations, unionIndex, Aabb.Union(ReadAabb(operations, previousRoot), ReadAabb(operations, root)));
                    SetCullable(operations, unionIndex, ReadCullable(operations, previousRoot) && ReadCullable(operations, root));
                    SetConsumer(operations, root, unionIndex);
                    root = unionIndex;
                }
            }

            return new SdfProgram(new NativeArray<SdfOperation>(operations.ToArray(), Allocator.Persistent), root, ComputeInfluenceRadius(operations));
        }

        public static SdfProgram CompilePortableBodyField(CreatureDefinition definition)
        {
            if (definition == null) throw new DomainException("Cannot compile a null CreatureDefinition.");

            return CompilePortableBodyField(definition, ResolvedCreatureSnapshot.Resolve(definition));
        }

        public static SdfProgram CompilePortableBodyField(
            CreatureDefinition definition, ResolvedCreatureSnapshot snapshot)
        {
            if (definition == null) throw new DomainException("Cannot compile a null CreatureDefinition.");
            if (snapshot == null) throw new DomainException("Cannot compile with a null resolved snapshot.");

            var operations = new List<SdfOperation>();
            int root = AppendPortableBodyField(operations, snapshot);
            if (root < 0)
            {
                operations.Add(SdfOperation.Primitive(SdfOperationType.Empty, float3.zero));
                root = 0;
            }

            return new SdfProgram(new NativeArray<SdfOperation>(operations.ToArray(), Allocator.Persistent), root, ComputeInfluenceRadius(operations));
        }

        public static List<(CreaturePart Part, SdfProgram Program)> CompileIndividualPartsPortable(CreatureDefinition definition)
        {
            if (definition == null) throw new DomainException("Cannot compile a null CreatureDefinition.");

            return CompileIndividualPartsPortable(definition, ResolvedCreatureSnapshot.Resolve(definition));
        }

        public static List<(CreaturePart Part, SdfProgram Program)> CompileIndividualPartsPortable(
            CreatureDefinition definition, ResolvedCreatureSnapshot snapshot)
        {
            if (definition == null) throw new DomainException("Cannot compile a null CreatureDefinition.");
            if (snapshot == null) throw new DomainException("Cannot compile with a null resolved snapshot.");

            return snapshot.PartsById.Values
                .OrderBy(part => part.Id, System.StringComparer.Ordinal)
                .Where(part => !part.HasMeshGeometry)
                .Select(part =>
                {
                    CreaturePart sourcePart = definition.FindPart(part.Id);
                    return (sourcePart, CompilePortablePart(snapshot, part));
                })
                .ToList();
        }

        private static int AppendPortableBodyField(List<SdfOperation> operations,
            ResolvedCreatureSnapshot snapshot)
        {
            if (!snapshot.HasBody)
            {
                return -1;
            }

            // CC-056A increment 3: consume the shared ResolvedBody derivation.
            ResolvedBody body = snapshot.Body;
            int root = -1;
            for (int i = 0; i < body.SamplePositions.Count; i++)
            {
                Vector3 position = body.SamplePositions[i];
                float radius = body.SampleRadii[i];
                int primitive = operations.Count;
                operations.Add(SdfOperation.Primitive(SdfOperationType.Sphere, new float3(radius, 0f, 0f)));
                Matrix4x4 localToCreature = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                int bodyNode = operations.Count;
                operations.Add(new SdfOperation
                {
                    Type = SdfOperationType.Transform,
                    A = primitive,
                    Matrix = ToFloat4x4(localToCreature.inverse),
                    DistanceScale = 1f,
                });
                SetWorldAabb(operations, bodyNode, TransformToWorld(PrimitiveLocalAabb(operations[primitive]), localToCreature));
                SetCullable(operations, bodyNode, true);

                if (root < 0)
                {
                    root = bodyNode;
                    continue;
                }

                int unionIndex = operations.Count;
                operations.Add(new SdfOperation
                {
                    Type = SdfOperationType.SmoothUnion,
                    A = root,
                    B = bodyNode,
                    Parameters = new float3(Mathf.Min(body.SampleRadii[i - 1], radius) * BodySampleBlendFactor, 0f, 0f),
                });
                SetWorldAabb(operations, unionIndex, Aabb.Union(ReadAabb(operations, root), ReadAabb(operations, bodyNode)));
                SetCullable(operations, unionIndex, ReadCullable(operations, root) && ReadCullable(operations, bodyNode));
                SetConsumer(operations, bodyNode, unionIndex);
                root = unionIndex;
            }

            return root;
        }

        private static SdfProgram CompilePortablePart(
            ResolvedCreatureSnapshot snapshot, ResolvedPartSnapshot part)
        {
            var operations = new List<SdfOperation>();
            Matrix4x4 localToCreature = part.PartFrameToCreatureSpace;
            Vector3 scale = localToCreature.lossyScale;
            float distanceScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            bool shouldMirror = part.MirrorAcrossSymmetryPlane && snapshot.SymmetryMode != SymmetryMode.None;
            int root;

            if (part.HasLimb)
            {
                root = CompileLimbChainPortable(operations, part.Limb, localToCreature, distanceScale, shouldMirror);
            }
            else
            {
                SdfOperationType primitiveType;
                switch (part.Shape.Type)
                {
                    case ShapeType.Sphere: primitiveType = SdfOperationType.Sphere; break;
                    case ShapeType.Ellipsoid: primitiveType = SdfOperationType.Ellipsoid; break;
                    case ShapeType.Box: primitiveType = SdfOperationType.Box; break;
                    case ShapeType.Capsule: primitiveType = SdfOperationType.Capsule; break;
                    default: throw new DomainException($"No portable SDF primitive mapping exists for ShapeType.{part.Shape.Type}.");
                }

                ResolvedShape shape = part.Shape;
                float radius = shape.Radius;
                float height = shape.CapsuleHeight;
                float3 boxHalfExtents = new float3(shape.BoxHalfExtents.x, shape.BoxHalfExtents.y, shape.BoxHalfExtents.z);
                float3 parameters = primitiveType == SdfOperationType.Box
                    ? boxHalfExtents
                    : primitiveType == SdfOperationType.Capsule
                        ? new float3(radius, height, (int)shape.CapsuleAxis)
                        : primitiveType == SdfOperationType.Ellipsoid
                            ? new float3(shape.EllipsoidRadii.x, shape.EllipsoidRadii.y, shape.EllipsoidRadii.z)
                            : new float3(radius, 0f, 0f);
                int primitive = operations.Count;
                operations.Add(SdfOperation.Primitive(primitiveType, parameters));
                int primitiveIndex = primitive;

                int transformIndex = operations.Count;
                operations.Add(new SdfOperation
                {
                    Type = SdfOperationType.Transform,
                    A = primitiveIndex,
                    Matrix = ToFloat4x4(localToCreature.inverse),
                    DistanceScale = distanceScale,
                });
                SetWorldAabb(operations, transformIndex, TransformToWorld(PrimitiveLocalAabb(operations[primitiveIndex]), localToCreature));
                SetCullable(operations, transformIndex, primitiveType != SdfOperationType.Ellipsoid);
                root = transformIndex;

                if (shouldMirror)
                {
                    int symmetryIndex = operations.Count;
                    operations.Add(new SdfOperation { Type = SdfOperationType.Symmetry, A = root });
                    Aabb child = ReadAabb(operations, root);
                    SetWorldAabb(operations, symmetryIndex, Aabb.Union(child, Aabb.MirrorAcrossX(child)));
                    SetCullable(operations, symmetryIndex, ReadCullable(operations, root));
                    root = symmetryIndex;
                }
            }

            return new SdfProgram(new NativeArray<SdfOperation>(operations.ToArray(), Allocator.Persistent), root, ComputeInfluenceRadius(operations));
        }

        private static float4x4 ToFloat4x4(Matrix4x4 matrix)
        {
            return new float4x4(
                new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
        }

        /// <summary>
        /// Appends the portable operations for a limb chain: one sphere primitive
        /// plus a baked local-space transform per derived metaball, smooth-united
        /// in chain order. When <paramref name="includeMirror"/> is true, a second
        /// copy of the chain is emitted under the creature-space X mirror and the
        /// two sides are hard-unioned (blend 0). This reproduces
        /// <c>min(chain(x), chain(-x))</c> exactly without a separate symmetry
        /// operation wrapping a composite subtree.
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
        private static int CompileLimbChainPortable(List<SdfOperation> operations, ResolvedLimb limb,
            Matrix4x4 localToCreature, float distanceScale, bool includeMirror)
        {
            List<LimbMetaball> metaballs = LimbMetaballSampler.Sample(limb);
            int originalRoot = -1;
            int mirroredRoot = -1;

            // The creature-space mirror of the part's transform: S · localToCreature.
            // Each mirrored ball keeps its ORIGINAL local position and is placed by
            // this mirrored matrix, so its world position equals S · (original world
            // position) — the same result as reflecting the composite field.
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

            int unionIndex = operations.Count;
            operations.Add(new SdfOperation
            {
                Type = SdfOperationType.SmoothUnion,
                A = originalRoot,
                B = mirroredRoot,
                Parameters = new float3(0f, 0f, 0f), // blend 0 = hard min
            });
            SetWorldAabb(operations, unionIndex, Aabb.Union(ReadAabb(operations, originalRoot), ReadAabb(operations, mirroredRoot)));
            SetCullable(operations, unionIndex, ReadCullable(operations, originalRoot) && ReadCullable(operations, mirroredRoot));
            SetConsumer(operations, mirroredRoot, unionIndex);
            return unionIndex;
        }

        private static int AppendLimbBall(List<SdfOperation> operations, Matrix4x4 localToCreature,
            float distanceScale, Vector3 localPosition, float radius)
        {
            int primitive = operations.Count;
            operations.Add(SdfOperation.Primitive(SdfOperationType.Sphere, new float3(radius, 0f, 0f)));

            Matrix4x4 localToCreatureForBall = localToCreature * Matrix4x4.TRS(localPosition, Quaternion.identity, Vector3.one);
            int ball = operations.Count;
            operations.Add(new SdfOperation
            {
                Type = SdfOperationType.Transform,
                A = primitive,
                Matrix = ToFloat4x4(localToCreatureForBall.inverse),
                DistanceScale = distanceScale,
            });
            SetWorldAabb(operations, ball, TransformToWorld(PrimitiveLocalAabb(operations[primitive]), localToCreatureForBall));
            SetCullable(operations, ball, true);
            return ball;
        }

        private static int UnionLimbBall(List<SdfOperation> operations, int root, int ball,
            List<LimbMetaball> metaballs, int i)
        {
            if (root < 0) return ball;
            float blend = Mathf.Min(metaballs[i - 1].Radius, metaballs[i].Radius) * LimbSampleBlendFactor;
            int unionIndex = operations.Count;
            operations.Add(new SdfOperation
            {
                Type = SdfOperationType.SmoothUnion,
                A = root,
                B = ball,
                Parameters = new float3(blend, 0f, 0f),
            });
            SetWorldAabb(operations, unionIndex, Aabb.Union(ReadAabb(operations, root), ReadAabb(operations, ball)));
            SetCullable(operations, unionIndex, ReadCullable(operations, root) && ReadCullable(operations, ball));
            SetConsumer(operations, ball, unionIndex);
            return unionIndex;
        }

    }
}
