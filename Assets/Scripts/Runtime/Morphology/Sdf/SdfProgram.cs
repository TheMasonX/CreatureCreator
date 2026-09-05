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
        public float3 MinBound;
        public float3 MaxBound;
        public int ConsumerUnionIndex;
        public bool Cullable;

        public static SdfOperation Primitive(SdfOperationType type, float3 parameters)
        {
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

        public static float Evaluate(SdfProgram program, float3 point, NativeArray<float> scratchValues)
        {
            if (program == null) throw new DomainException("program must not be null.");
            if (!scratchValues.IsCreated || scratchValues.Length < program.Operations.Length)
            {
                throw new DomainException("scratchValues must contain one entry per operation.");
            }
            return EvaluateInto(program.Operations, program.RootIndex, point, scratchValues, 0,
                program.InfluenceRadius, allowCulling: true);
        }

        public static float EvaluateReference(SdfProgram program, float3 point, NativeArray<float> scratchValues)
        {
            if (program == null) throw new DomainException("program must not be null.");
            if (!scratchValues.IsCreated || scratchValues.Length < program.Operations.Length)
            {
                throw new DomainException("scratchValues must contain one entry per operation.");
            }
            return EvaluateInto(program.Operations, program.RootIndex, point, scratchValues, 0,
                program.InfluenceRadius, allowCulling: false);
        }

        public static float Evaluate(NativeArray<SdfOperation> operations, int rootIndex, float3 point,
            float influenceRadius, bool allowCulling = false)
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

        public static float EvaluateReference(NativeArray<SdfOperation> operations, int rootIndex,
            float3 point, float influenceRadius)
        {
            return Evaluate(operations, rootIndex, point, influenceRadius, allowCulling: false);
        }

        internal static float EvaluateInto(NativeArray<SdfOperation> operations, int rootIndex, float3 point,
            NativeArray<float> values, int valueOffset, float influenceRadius, bool allowCulling)
        {
            for (int i = 0; i <= rootIndex; i++)
            {
                SdfOperation operation = operations[i];
                bool hasBounds = HasValidBounds(operation.MinBound, operation.MaxBound);
                if (allowCulling && operation.Cullable && hasBounds
                    && IsOutsideInflatedBounds(point, operation.MinBound, operation.MaxBound, influenceRadius))
                {
                    values[valueOffset + i] = float.PositiveInfinity;
                    continue;
                }

                values[valueOffset + i] = EvaluateOperation(
                    operation, values, operations, point, valueOffset, influenceRadius, allowCulling);
            }
            return values[valueOffset + rootIndex];
        }

        internal static float EvaluateOperation(SdfOperation operation, NativeArray<float> values,
            NativeArray<SdfOperation> operations, float3 point, int valueOffset,
            float influenceRadius = 0f, bool allowCulling = false)
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

        private static float EvaluateSubtree(NativeArray<SdfOperation> operations, int operationIndex,
            float3 point, float influenceRadius, bool allowCulling)
        {
            SdfOperation operation = operations[operationIndex];
            bool hasBounds = HasValidBounds(operation.MinBound, operation.MaxBound);
            if (allowCulling && operation.Cullable && hasBounds
                && IsOutsideInflatedBounds(point, operation.MinBound, operation.MaxBound, influenceRadius))
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

        private static bool HasValidBounds(float3 minBound, float3 maxBound)
        {
            return minBound.x <= maxBound.x && minBound.y <= maxBound.y && minBound.z <= maxBound.z;
        }

        private static bool IsOutsideInflatedBounds(float3 point, float3 minBound, float3 maxBound, float influenceRadius)
        {
            return point.x < minBound.x - influenceRadius || point.x > maxBound.x + influenceRadius ||
                   point.y < minBound.y - influenceRadius || point.y > maxBound.y + influenceRadius ||
                   point.z < minBound.z - influenceRadius || point.z > maxBound.z + influenceRadius;
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
        public bool RootCanCull;
        public float3 RootMinBound;
        public float3 RootMaxBound;

        public void Execute(int index)
        {
            int sampleIndex = SampleStartIndex + index;
            int x = sampleIndex % CornersX;
            int y = (sampleIndex / CornersX) % CornersY;
            int z = sampleIndex / (CornersX * CornersY);
            float3 point = Origin + new float3(x, y, z) * CellSize;

            if (RootCanCull &&
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