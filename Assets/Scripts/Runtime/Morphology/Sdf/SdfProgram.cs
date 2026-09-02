using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Sdf
{
    public enum SdfOperationType
    {
        Empty,
        Sphere,
        Box,
        Capsule,
        Ellipsoid,
        Transform,
        Symmetry,
        SmoothUnion,
    }

    public struct SdfOperation
    {
        public SdfOperationType Type;
        public int A;
        public int B;
        public float3 Parameters;
        public float4x4 Matrix;
        public float DistanceScale;

        /// <summary>
        /// World-space AABB of the operation's geometry, used by the evaluator for
        /// spatial culling (CC-062). Primitives keep an empty AABB (always culled)
        /// because the wrapping Transform op evaluates the primitive inline and the
        /// primitive's own value slot is never read.
        /// </summary>
        public float3 MinBound;
        public float3 MaxBound;

        /// <summary>
        /// Index of the smooth-union operation that consumes this op as its newly
        /// added child (the "B" operand). -1 when the op is a chain/root (never
        /// culled this way). The evaluator uses the consuming union's other child
        /// (the running chain value); retained as compiler metadata for stable
        /// program layout.
        /// </summary>
        public int ConsumerUnionIndex;

        /// <summary>
        /// Whether this op's SDF output is bounded below by the distance to its
        /// world AABB, which the culling skip relies on. True only for leaves whose
        /// primitives are true distance fields (sphere, box, capsule). False for
        /// any subtree containing an ellipsoid, whose approximate SDF can output a
        /// value smaller than the distance to its bounding box.
        /// </summary>
        public bool Cullable;

        public static SdfOperation Primitive(SdfOperationType type, float3 parameters)
        {
            // Primitive op slots are dead: the wrapping Transform re-evaluates the
            // primitive inline, and unions reference transform/symmetry ops, never
            // primitives. An empty AABB keeps them always-culled in the evaluator.
            return new SdfOperation
            {
                Type = type,
                Parameters = parameters,
                MinBound = new float3(float.PositiveInfinity),
                MaxBound = new float3(float.NegativeInfinity),
                ConsumerUnionIndex = -1,
            };
        }
    }

    public sealed class SdfProgram : IDisposable
    {
        public NativeArray<SdfOperation> Operations { get; }
        public int RootIndex { get; }

        /// <summary>
        /// Maximum smooth-blend radius across all unions in the program, plus a
        /// small epsilon. The evaluator inflates every op's world AABB by this so a
        /// skipped op is provably farther from the sample than any blend can reach.
        /// </summary>
        public float InfluenceRadius { get; }

        internal SdfProgram(NativeArray<SdfOperation> operations, int rootIndex, float influenceRadius)
        {
            Operations = operations;
            RootIndex = rootIndex;
            InfluenceRadius = influenceRadius;
        }

        public void Dispose()
        {
            if (Operations.IsCreated)
            {
                Operations.Dispose();
            }
        }
    }

    /// <summary>
    /// Evaluates a compiled portable SDF program at a point (CC-045).
    ///
    /// NON-FINITE FIELD CONTRACT (CC-064): every consumer of a sampled scalar
    /// value must treat the field as follows —
    ///   `+inf` = outside / culled / semantically absent (never a giant valid distance)
    ///   `NaN`  = always invalid (a bug upstream; sampling must never produce it)
    ///   `-inf` = invalid for field sampling (interior sentinel only, not a distance)
    ///   finite = the evaluated field
    /// Fast culling (CC-063) writes `+inf` for skipped operations; consumers must
    /// treat `+inf` as "no candidate" (e.g. appearance selection must not let it
    /// win a nearest decision, and interpolation must clamp to the finite
    /// endpoint).
    /// </summary>
    public static class SdfProgramEvaluator
    {
        public static float Evaluate(SdfProgram program, float3 point)
        {
            if (program == null) throw new DomainException("program must not be null.");
            return Evaluate(program.Operations, program.RootIndex, point, program.InfluenceRadius, allowCulling: true);
        }

        public static float EvaluateReference(SdfProgram program, float3 point)
        {
            if (program == null) throw new DomainException("program must not be null.");
            return Evaluate(program.Operations, program.RootIndex, point, program.InfluenceRadius, allowCulling: false);
        }

        public static float Evaluate(
            SdfProgram program, float3 point, NativeArray<float> scratchValues)
        {
            if (program == null) throw new DomainException("program must not be null.");
            if (!scratchValues.IsCreated || scratchValues.Length < program.Operations.Length)
            {
                throw new DomainException("scratchValues must contain one entry per operation.");
            }
            return EvaluateInto(program.Operations, program.RootIndex, point, scratchValues, 0,
                program.InfluenceRadius, allowCulling: true);
        }

        public static float EvaluateReference(
            SdfProgram program, float3 point, NativeArray<float> scratchValues)
        {
            if (program == null) throw new DomainException("program must not be null.");
            if (!scratchValues.IsCreated || scratchValues.Length < program.Operations.Length)
            {
                throw new DomainException("scratchValues must contain one entry per operation.");
            }
            return EvaluateInto(program.Operations, program.RootIndex, point, scratchValues, 0, program.InfluenceRadius, allowCulling: false);
        }

        public static float Evaluate(
            NativeArray<SdfOperation> operations, int rootIndex, float3 point, float influenceRadius,
            bool allowCulling = false)
        {
            if (!operations.IsCreated) throw new DomainException("operations must be created.");
            if (rootIndex < 0 || rootIndex >= operations.Length)
            {
                throw new DomainException("rootIndex must identify an operation.");
            }

            var values = new NativeArray<float>(operations.Length, Allocator.Temp);
            float result = EvaluateInto(operations, rootIndex, point, values, 0, influenceRadius, allowCulling);
            values.Dispose();
            return result;
        }

        public static float EvaluateReference(
            NativeArray<SdfOperation> operations, int rootIndex, float3 point, float influenceRadius)
        {
            return Evaluate(operations, rootIndex, point, influenceRadius, allowCulling: false);
        }

        internal static float EvaluateInto(
            NativeArray<SdfOperation> operations, int rootIndex, float3 point,
            NativeArray<float> values, int valueOffset, float influenceRadius, bool allowCulling)
        {
            for (int i = 0; i <= rootIndex; i++)
            {
                SdfOperation operation = operations[i];
                // Fast culling (CC-063): skip any operation whose world AABB,
                // inflated by the program's max blend radius, does not contain the
                // sample, writing +inf for the absent operation. Operations without
                // compiled bounds must still evaluate: a standalone primitive
                // program legitimately uses its primitive as the root operation.
                bool hasBounds = operation.MinBound.x <= operation.MaxBound.x
                    && operation.MinBound.y <= operation.MaxBound.y
                    && operation.MinBound.z <= operation.MaxBound.z;
                if (allowCulling && hasBounds && (point.x < operation.MinBound.x - influenceRadius || point.x > operation.MaxBound.x + influenceRadius ||
                     point.y < operation.MinBound.y - influenceRadius || point.y > operation.MaxBound.y + influenceRadius ||
                    point.z < operation.MinBound.z - influenceRadius || point.z > operation.MaxBound.z + influenceRadius))
                {
                    values[valueOffset + i] = float.PositiveInfinity;
                    continue;
                }

                values[valueOffset + i] = EvaluateOperation(
                    operation, values, operations, point, valueOffset, influenceRadius, allowCulling);
            }
            return values[valueOffset + rootIndex];
        }

        internal static float EvaluateOperation(
            SdfOperation operation, NativeArray<float> values, NativeArray<SdfOperation> operations,
            float3 point, int valueOffset, float influenceRadius = 0f, bool allowCulling = false)
        {
            switch (operation.Type)
            {
                case SdfOperationType.Sphere:
                case SdfOperationType.Box:
                case SdfOperationType.Capsule:
                case SdfOperationType.Ellipsoid:
                    return EvaluatePrimitive(operation, point);
                case SdfOperationType.Transform:
                    return EvaluatePrimitive(
                        operations[operation.A], math.mul(operation.Matrix, new float4(point, 1f)).xyz)
                        * operation.DistanceScale;
                case SdfOperationType.Symmetry:
                    return math.min(
                        EvaluateOperation(operations[operation.A], values, operations, point, valueOffset),
                        EvaluateSubtree(operations, operation.A,
                            new float3(-point.x, point.y, point.z), influenceRadius, allowCulling: false));
                case SdfOperationType.SmoothUnion:
                    return SmoothMin(values[valueOffset + operation.A], values[valueOffset + operation.B], operation.Parameters.x);
                case SdfOperationType.Empty: return float.PositiveInfinity;
                default: return 0f;
            }
        }

        private static float EvaluateSubtree(
            NativeArray<SdfOperation> operations, int operationIndex, float3 point,
            float influenceRadius, bool allowCulling)
        {
            SdfOperation operation = operations[operationIndex];
            bool hasBounds = operation.MinBound.x <= operation.MaxBound.x
                && operation.MinBound.y <= operation.MaxBound.y
                && operation.MinBound.z <= operation.MaxBound.z;
            if (allowCulling && hasBounds
                && (point.x < operation.MinBound.x - influenceRadius || point.x > operation.MaxBound.x + influenceRadius
                    || point.y < operation.MinBound.y - influenceRadius || point.y > operation.MaxBound.y + influenceRadius
                    || point.z < operation.MinBound.z - influenceRadius || point.z > operation.MaxBound.z + influenceRadius))
            {
                return float.PositiveInfinity;
            }

            switch (operation.Type)
            {
                case SdfOperationType.Sphere:
                case SdfOperationType.Box:
                case SdfOperationType.Capsule:
                case SdfOperationType.Ellipsoid:
                    return EvaluatePrimitive(operation, point);
                case SdfOperationType.Transform:
                    return EvaluatePrimitive(
                        operations[operation.A], math.mul(operation.Matrix, new float4(point, 1f)).xyz)
                        * operation.DistanceScale;
                case SdfOperationType.Symmetry:
                    return math.min(
                        EvaluateSubtree(operations, operation.A, point, influenceRadius, allowCulling),
                        EvaluateSubtree(operations, operation.A,
                            new float3(-point.x, point.y, point.z), influenceRadius, allowCulling));
                case SdfOperationType.SmoothUnion:
                    return SmoothMin(
                        EvaluateSubtree(operations, operation.A, point, influenceRadius, allowCulling),
                        EvaluateSubtree(operations, operation.B, point, influenceRadius, allowCulling),
                        operation.Parameters.x);
                case SdfOperationType.Empty:
                    return float.PositiveInfinity;
                default:
                    return 0f;
            }
        }

        private static float EvaluatePrimitive(SdfOperation operation, float3 point)
        {
            switch (operation.Type)
            {
                case SdfOperationType.Sphere:
                    return math.length(point) - operation.Parameters.x;
                case SdfOperationType.Box:
                    float3 q = math.abs(point) - operation.Parameters;
                    return math.length(math.max(q, 0f)) + math.min(math.max(q.x, math.max(q.y, q.z)), 0f);
                case SdfOperationType.Capsule:
                    float3 axisPoint = operation.Parameters.z == 0f
                        ? new float3(point.y, point.x, point.z)
                        : operation.Parameters.z == 2f
                            ? new float3(point.x, point.z, point.y)
                            : point;
                    float halfHeight = operation.Parameters.y * 0.5f;
                    float3 ba = new float3(0f, operation.Parameters.y, 0f);
                    float t = math.clamp(math.dot(axisPoint - new float3(0f, -halfHeight, 0f), ba) / math.dot(ba, ba), 0f, 1f);
                    return math.length(axisPoint - (new float3(0f, -halfHeight, 0f) + ba * t)) - operation.Parameters.x;
                case SdfOperationType.Ellipsoid:
                    float3 radii = operation.Parameters;
                    float3 normalized = point / radii;
                    float3 gradient = point / (radii * radii);
                    float denominator = math.length(gradient);
                    return denominator <= math.EPSILON
                        ? -math.cmin(radii)
                        : (math.length(normalized) - 1f) / denominator;
                default: return float.PositiveInfinity;
            }
        }

        private static float SmoothMin(float a, float b, float radius)
        {
            // AABB-culled children read as +inf. math.lerp(b, a, h) computes
            // b + (a - b) * h, which is NaN when one operand is +inf (inf * 0).
            // Treat +inf as "absent": the finite child wins, or +inf when both are
            // absent.
            if (float.IsPositiveInfinity(a) || float.IsPositiveInfinity(b))
            {
                return math.min(a, b);
            }
            if (radius <= 0f) return math.min(a, b);
            float h = math.clamp(0.5f + 0.5f * (b - a) / radius, 0f, 1f);
            return math.lerp(b, a, h) - radius * h * (1f - h);
        }
    }

    [BurstCompile]
    public struct SdfSamplingJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SdfOperation> Operations;
        [NativeDisableParallelForRestriction] public NativeArray<float> ScratchValues;
        [NativeDisableParallelForRestriction] public NativeArray<float> Samples;
        public int RootIndex;
        public int CornersX;
        public int CornersY;
        public int CornersZ;
        public float3 Origin;
        public float CellSize;
        public int SampleStartIndex;
        public float InfluenceRadius;

        /// <summary>
        /// Slice B region-aware early exit. The root op's world AABB (inflated by
        /// <see cref="InfluenceRadius"/>) is the only region where the field can be
        /// finite: a corner outside it makes the evaluator cull the root (CC-063),
        /// so the result is always +inf. Pre-fill it and skip the whole per-op
        /// loop. The containment test mirrors the evaluator's per-op test exactly,
        /// so the field is bit-identical; in-bounds corners run the normal path.
        /// <c>RootHasBounds</c> is false for standalone-primitive programs whose
        /// root has no compiled AABB (they never early-exit).
        /// </summary>
        public bool RootHasBounds;
        public float3 RootMinBound;
        public float3 RootMaxBound;

        public void Execute(int index)
        {
            int sampleIndex = SampleStartIndex + index;
            int x = sampleIndex % CornersX;
            int y = (sampleIndex / CornersX) % CornersY;
            int z = sampleIndex / (CornersX * CornersY);
            float3 point = Origin + new float3(x, y, z) * CellSize;

            if (RootHasBounds &&
                (point.x < RootMinBound.x - InfluenceRadius || point.x > RootMaxBound.x + InfluenceRadius ||
                 point.y < RootMinBound.y - InfluenceRadius || point.y > RootMaxBound.y + InfluenceRadius ||
                 point.z < RootMinBound.z - InfluenceRadius || point.z > RootMaxBound.z + InfluenceRadius))
            {
                Samples[sampleIndex] = float.PositiveInfinity;
                return;
            }

            int valueOffset = index * Operations.Length;
            Samples[sampleIndex] = SdfProgramEvaluator.EvaluateInto(
                Operations, RootIndex, point, ScratchValues, valueOffset, InfluenceRadius, allowCulling: true);
        }
    }
}