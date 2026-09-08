using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Animation.Skinned;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Editor-only diagnostic for investigating implicit-surface skinning smear.
    /// It sweeps every generated rig bone through isolated ±rotation tests,
    /// records the actual SkinnedMeshRenderer result, compares it against the
    /// LinearBlendSkinning oracle using the renderer's actual BoneWeight data,
    /// and writes detailed per-vertex deltas plus per-experiment summaries.
    ///
    /// This tool deliberately does not change weighting, rig construction, or
    /// the runtime pose path. It is an evidence generator for TSK-0167.
    /// </summary>
    internal static class RigSkinningSweepDiagnostic
    {
        private const float TestAngleDegrees = 15f;
        private const float MovementEpsilon = 1e-5f;
        private const float OracleDiscrepancyEpsilon = 1e-5f;
        private const string OutputDirectory = "Assets/Diagnostics/Skinning";

        private static readonly Vector3[] Axes =
        {
            Vector3.right,
            Vector3.up,
            Vector3.forward,
        };

        private static readonly string[] AxisNames = { "X", "Y", "Z" };

        [MenuItem("Tools/Creature Creator/Animation/Skinning Sweep Selected Rig", priority = 1250)]
        private static void RunSelectedRig()
        {
            if (!TryFindSelectedRig(out CreatureRig rig))
            {
                Debug.LogWarning("Select a CreatureRig bone or a GameObject containing CreatureRig before running the skinning sweep.");
                return;
            }

            try
            {
                string[] files = Run(rig);
                AssetDatabase.Refresh();
                Debug.Log(
                    "Creature skinning sweep complete. Summary: " + files[0] + " | Details: " + files[1]);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [MenuItem("Tools/Creature Creator/Animation/Skinning Sweep Selected Rig", validate = true)]
        private static bool ValidateRunSelectedRig()
        {
            return TryFindSelectedRig(out _);
        }

        private static bool TryFindSelectedRig(out CreatureRig rig)
        {
            rig = null;
            Transform selected = Selection.activeTransform;
            if (selected == null) return false;
            rig = selected.GetComponentInParent<CreatureRig>();
            return rig != null && rig.RestSkeleton != null && rig.IndexedBones.Count > 0;
        }

        private static string[] Run(CreatureRig rig)
        {
            CreatureSkinnedMeshRenderer adapter = rig.GetComponent<CreatureSkinnedMeshRenderer>();
            if (adapter == null || adapter.Renderer == null || adapter.Renderer.sharedMesh == null)
            {
                throw new DomainException(
                    "Selected CreatureRig must have a bound CreatureSkinnedMeshRenderer before running the sweep.");
            }

            SkinnedMeshRenderer renderer = adapter.Renderer;
            Mesh sourceMesh = renderer.sharedMesh;
            Mesh bakedMesh = new Mesh { name = "__CreatureSkinningSweepBaked" };
            try
            {
                BoneSnapshot[] rest = CaptureRestBones(rig.RestSkeleton);
                BonePose[] restFrames = ToBonePoses(rest);
                VertexInfluence[][] influences = CaptureRendererInfluences(sourceMesh, rest.Length);
                Vector3[] generatedRest = sourceMesh.vertices;

                Directory.CreateDirectory(OutputDirectory);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string baseName = "skinning-sweep-" + SanitizeName(rig.gameObject.name) + "-" + stamp;
                string summaryPath = Path.Combine(OutputDirectory, baseName + "-summary.json");
                string detailsPath = Path.Combine(OutputDirectory, baseName + "-vertices.csv");

                RestoreRestPose(rig, rest);
                renderer.BakeMesh(bakedMesh);
                Vector3[] baseline = bakedMesh.vertices;
                if (baseline.Length != generatedRest.Length)
                {
                    throw new DomainException(
                        "The skinned renderer vertex count does not match its source mesh before the sweep.");
                }

                using (var details = new StreamWriter(detailsPath, false, new UTF8Encoding(false)))
                {
                    details.WriteLine(
                        "boneIndex,boneId,sourcePartId,parentIndex,axis,angleDegrees,vertexIndex," +
                        "restX,restY,restZ,actualX,actualY,actualZ,deltaX,deltaY,deltaZ,deltaMagnitude," +
                        "oracleX,oracleY,oracleZ,oracleError,weight0Bone,weight0,weight1Bone,weight1," +
                        "weight2Bone,weight2,weight3Bone,weight3");

                    var summaryRows = new List<string>();
                    summaryRows.Add("  {\"boneIndex\":" + -1 +
                        ",\"boneId\":\"__rest_baseline__\",\"sourcePartId\":\"\",\"parentIndex\":-1" +
                        ",\"axis\":\"\",\"angleDegrees\":0" +
                        ",\"maxMovement\":" + FloatJson(MaxDistance(baseline, generatedRest)) +
                        ",\"meanMovement\":" + FloatJson(MeanDistance(baseline, generatedRest)) +
                        ",\"movedVertexCount\":" + CountMoved(baseline, generatedRest) +
                        ",\"oracleMaxError\":0,\"oracleMeanError\":0,\"unexpectedVertexCount\":0}");

                    IReadOnlyList<Transform> bones = rig.IndexedBones;
                    for (int boneIndex = 0; boneIndex < bones.Count; boneIndex++)
                    {
                        Transform bone = bones[boneIndex];
                        BoneSnapshot restBone = rest[boneIndex];
                        Quaternion restRotation = restBone.Rotation;

                        for (int axisIndex = 0; axisIndex < Axes.Length; axisIndex++)
                        {
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                RestoreRestPose(rig, rest);

                                float angle = TestAngleDegrees * sign;
                                bone.rotation = restRotation * Quaternion.AngleAxis(angle, Axes[axisIndex]);
                                renderer.BakeMesh(bakedMesh);
                                Vector3[] actual = bakedMesh.vertices;

                                BonePose[] posedFrames = CaptureCurrentFrames(rig);
                                Vector3[] oracle = LinearBlendSkinning.Deform(
                                    restFrames,
                                    posedFrames,
                                    generatedRest,
                                    influences);

                                ExperimentStats stats = new ExperimentStats
                                {
                                    BoneIndex = boneIndex,
                                    BoneId = restBone.Id,
                                    SourcePartId = restBone.SourcePartId,
                                    ParentIndex = restBone.ParentIndex,
                                    Axis = AxisNames[axisIndex],
                                    AngleDegrees = angle,
                                };

                                for (int vertex = 0; vertex < actual.Length; vertex++)
                                {
                                    Vector3 baselineVertex = baseline[vertex];
                                    Vector3 actualVertex = actual[vertex];
                                    Vector3 delta = actualVertex - baselineVertex;
                                    float movement = delta.magnitude;

                                    Vector3 expectedVertex = oracle[vertex];
                                    float oracleError = Vector3.Distance(actualVertex, expectedVertex);

                                    if (movement > MovementEpsilon || oracleError > OracleDiscrepancyEpsilon)
                                    {
                                        BoneWeight weight = sourceMesh.boneWeights[vertex];
                                        details.WriteLine(
                                            boneIndex + "," + Csv(restBone.Id) + "," + Csv(restBone.SourcePartId) + "," +
                                            restBone.ParentIndex + "," + AxisNames[axisIndex] + "," + angle.ToString("R") + "," +
                                            vertex + "," + VecCsv(baselineVertex) + "," + VecCsv(actualVertex) + "," +
                                            VecCsv(delta) + "," + movement.ToString("R") + "," + VecCsv(expectedVertex) + "," +
                                            oracleError.ToString("R") + "," +
                                            weight.boneIndex0 + "," + weight.weight0.ToString("R") + "," +
                                            weight.boneIndex1 + "," + weight.weight1.ToString("R") + "," +
                                            weight.boneIndex2 + "," + weight.weight2.ToString("R") + "," +
                                            weight.boneIndex3 + "," + weight.weight3.ToString("R"));
                                    }

                                    stats.Record(movement, oracleError);
                                }

                                summaryRows.Add(stats.ToJsonLine());
                            }
                        }
                    }

                    RestoreRestPose(rig, rest);

                    var summary = new StringBuilder();
                    summary.AppendLine("{");
                    summary.AppendLine("  \"schemaVersion\": 1,");
                    summary.AppendLine("  \"generatedUtc\": \"" + DateTime.UtcNow.ToString("O") + "\",");
                    summary.AppendLine("  \"rigObject\": \"" + Json(rig.gameObject.name) + "\",");
                    summary.AppendLine("  \"mesh\": \"" + Json(sourceMesh.name) + "\",");
                    summary.AppendLine("  \"vertexCount\": " + sourceMesh.vertexCount + ",");
                    summary.AppendLine("  \"boneCount\": " + rest.Length + ",");
                    summary.AppendLine("  \"testAngleDegrees\": " + TestAngleDegrees.ToString("R") + ",");
                    summary.AppendLine("  \"movementEpsilon\": " + MovementEpsilon.ToString("R") + ",");
                    summary.AppendLine("  \"oracleDiscrepancyEpsilon\": " + OracleDiscrepancyEpsilon.ToString("R") + ",");
                    summary.AppendLine("  \"detailsCsv\": \"" + Json(detailsPath.Replace('\\', '/')) + "\",");
                    summary.AppendLine("  \"experiments\": [");
                    for (int i = 0; i < summaryRows.Count; i++)
                    {
                        summary.Append(summaryRows[i]);
                        summary.AppendLine(i == summaryRows.Count - 1 ? "" : ",");
                    }
                    summary.AppendLine("  ]");
                    summary.AppendLine("}");

                    File.WriteAllText(summaryPath, summary.ToString(), new UTF8Encoding(false));
                }

                return new[] { summaryPath, detailsPath };
            }
            finally
            {
                RestoreRestPoseSafely(rig);
                if (Application.isPlaying) UnityEngine.Object.Destroy(bakedMesh);
                else UnityEngine.Object.DestroyImmediate(bakedMesh);
            }
        }

        private static BoneSnapshot[] CaptureRestBones(SkeletonSnapshot snapshot)
        {
            var rest = new BoneSnapshot[snapshot.Count];
            for (int i = 0; i < rest.Length; i++) rest[i] = snapshot[i];
            return rest;
        }

        private static BonePose[] ToBonePoses(IReadOnlyList<BoneSnapshot> bones)
        {
            var frames = new BonePose[bones.Count];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = new BonePose(bones[i].Position, bones[i].Rotation);
            return frames;
        }

        private static BonePose[] CaptureCurrentFrames(CreatureRig rig)
        {
            var frames = new BonePose[rig.IndexedBones.Count];
            for (int i = 0; i < frames.Length; i++)
            {
                Transform bone = rig.IndexedBones[i];
                frames[i] = new BonePose(bone.position, bone.rotation);
            }
            return frames;
        }

        private static VertexInfluence[][] CaptureRendererInfluences(Mesh mesh, int boneCount)
        {
            BoneWeight[] weights = mesh.boneWeights;
            if (weights == null || weights.Length != mesh.vertexCount)
                throw new DomainException("The bound skinned mesh must expose one BoneWeight per vertex.");

            var result = new VertexInfluence[weights.Length][];
            for (int vertex = 0; vertex < weights.Length; vertex++)
            {
                BoneWeight weight = weights[vertex];
                var influences = new List<VertexInfluence>(4);
                AddInfluence(influences, weight.boneIndex0, weight.weight0, boneCount);
                AddInfluence(influences, weight.boneIndex1, weight.weight1, boneCount);
                AddInfluence(influences, weight.boneIndex2, weight.weight2, boneCount);
                AddInfluence(influences, weight.boneIndex3, weight.weight3, boneCount);

                if (influences.Count == 0)
                    throw new DomainException("Vertex " + vertex + " has no active BoneWeight influences.");
                result[vertex] = influences.ToArray();
            }
            return result;
        }

        private static void AddInfluence(List<VertexInfluence> destination, int boneIndex, float weight, int boneCount)
        {
            if (weight <= 0f) return;
            if (boneIndex < 0 || boneIndex >= boneCount)
                throw new DomainException("BoneWeight references bone " + boneIndex + " outside the rig bone set.");
            destination.Add(new VertexInfluence(boneIndex, weight));
        }

        private static void RestoreRestPose(CreatureRig rig, IReadOnlyList<BoneSnapshot> rest)
        {
            IReadOnlyList<Transform> bones = rig.IndexedBones;
            if (bones.Count != rest.Count)
                throw new DomainException("Rig bone count changed during the diagnostic.");

            for (int i = 0; i < bones.Count; i++)
            {
                bones[i].position = rest[i].Position;
                bones[i].rotation = rest[i].Rotation;
            }
        }

        private static void RestoreRestPoseSafely(CreatureRig rig)
        {
            try
            {
                if (rig == null || rig.RestSkeleton == null) return;
                RestoreRestPose(rig, CaptureRestBones(rig.RestSkeleton));
            }
            catch
            {
                // The diagnostic must not mask the original failure while attempting cleanup.
            }
        }

        private static int CountMoved(IReadOnlyList<Vector3> left, IReadOnlyList<Vector3> right)
        {
            int count = 0;
            int length = Mathf.Min(left.Count, right.Count);
            for (int i = 0; i < length; i++)
                if (Vector3.Distance(left[i], right[i]) > MovementEpsilon) count++;
            return count;
        }

        private static float MaxDistance(IReadOnlyList<Vector3> left, IReadOnlyList<Vector3> right)
        {
            float max = 0f;
            int length = Mathf.Min(left.Count, right.Count);
            for (int i = 0; i < length; i++) max = Mathf.Max(max, Vector3.Distance(left[i], right[i]));
            return max;
        }

        private static float MeanDistance(IReadOnlyList<Vector3> left, IReadOnlyList<Vector3> right)
        {
            int length = Mathf.Min(left.Count, right.Count);
            if (length == 0) return 0f;
            double sum = 0d;
            for (int i = 0; i < length; i++) sum += Vector3.Distance(left[i], right[i]);
            return (float)(sum / length);
        }

        private static string VecCsv(Vector3 value)
        {
            return value.x.ToString("R") + "," + value.y.ToString("R") + "," + value.z.ToString("R");
        }

        private static string Csv(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Json(string value)
        {
            if (value == null) return "null";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string FloatJson(float value)
        {
            return value.ToString("R");
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "rig";
            var builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }
            return builder.ToString();
        }

        private sealed class ExperimentStats
        {
            public int BoneIndex;
            public string BoneId;
            public string SourcePartId;
            public int ParentIndex;
            public string Axis;
            public float AngleDegrees;
            public int MovedVertexCount;
            public int UnexpectedVertexCount;
            public float MaxMovement;
            public double SumMovement;
            public float MaxOracleError;
            public double SumOracleError;
            public int VertexCount;

            public void Record(float movement, float oracleError)
            {
                VertexCount++;
                SumMovement += movement;
                SumOracleError += oracleError;
                if (movement > MaxMovement) MaxMovement = movement;
                if (oracleError > MaxOracleError) MaxOracleError = oracleError;
                if (movement > MovementEpsilon) MovedVertexCount++;
                if (oracleError > OracleDiscrepancyEpsilon) UnexpectedVertexCount++;
            }

            public string ToJsonLine()
            {
                float meanMovement = VertexCount == 0 ? 0f : (float)(SumMovement / VertexCount);
                float meanOracleError = VertexCount == 0 ? 0f : (float)(SumOracleError / VertexCount);
                return "    {" +
                    "\"boneIndex\":" + BoneIndex +
                    ",\"boneId\":\"" + Json(BoneId) + "\"" +
                    ",\"sourcePartId\":\"" + Json(SourcePartId) + "\"" +
                    ",\"parentIndex\":" + ParentIndex +
                    ",\"axis\":\"" + Axis + "\"" +
                    ",\"angleDegrees\":" + AngleDegrees.ToString("R") +
                    ",\"movedVertexCount\":" + MovedVertexCount +
                    ",\"unexpectedVertexCount\":" + UnexpectedVertexCount +
                    ",\"maxMovement\":" + MaxMovement.ToString("R") +
                    ",\"meanMovement\":" + meanMovement.ToString("R") +
                    ",\"oracleMaxError\":" + MaxOracleError.ToString("R") +
                    ",\"oracleMeanError\":" + meanOracleError.ToString("R") +
                    "}";
            }
        }
    }
}
