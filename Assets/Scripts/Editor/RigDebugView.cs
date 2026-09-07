using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ProceduralCreature.Animation;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Skeleton;
using ProceduralCreature.Animation.Skinned;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// SceneView-only rig inspection overlay for generated CreatureRig instances.
    /// This is presentation-only: it never mutates DNA or runtime rig state.
    /// Runtime skeleton snapshots are the authoritative data source; editor session
    /// reloads are intentionally not consulted for focus/selection behavior.
    /// </summary>
    [InitializeOnLoad]
    internal static class RigDebugView
    {
        private const string EnabledKey = "ProceduralCreature.RigDebug.Enabled";
        private const string AlwaysOnTopKey = "ProceduralCreature.RigDebug.AlwaysOnTop";
        private const string LabelsKey = "ProceduralCreature.RigDebug.Labels";
        private const string SelectableKey = "ProceduralCreature.RigDebug.Selectable";
        private const string WidthKey = "ProceduralCreature.RigDebug.LineWidth";
        private const string RawMeshKey = "ProceduralCreature.RigDebug.ShowRawMesh";

        private static bool _enabled = EditorPrefs.GetBool(EnabledKey, false);
        private static bool _alwaysOnTop = EditorPrefs.GetBool(AlwaysOnTopKey, true);
        private static bool _labels = EditorPrefs.GetBool(LabelsKey, false);
        private static bool _selectable = EditorPrefs.GetBool(SelectableKey, true);
        private static bool _showRawMesh = EditorPrefs.GetBool(RawMeshKey, false);
        private static float _lineWidth = Mathf.Clamp(EditorPrefs.GetFloat(WidthKey, 4f), 1f, 8f);
        private static GUIStyle _labelStyle;

        static RigDebugView()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static GUIStyle GetLabelStyle()
        {
            if (_labelStyle != null) return _labelStyle;

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
            };
            style.normal.textColor = Color.white;
            _labelStyle = style;
            return _labelStyle;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            DrawOverlayControls(sceneView);
            if (!_enabled) return;

            CreatureRig[] rigs = UnityEngine.Object.FindObjectsByType<CreatureRig>();
            for (int i = 0; i < rigs.Length; i++)
            {
                CreatureRig rig = rigs[i];
                if (rig == null || !rig.isActiveAndEnabled || rig.RestSkeleton == null) continue;
                if (rig.IndexedBones.Count == 0) continue;
                DrawRig(rig);
            }
        }

        private static void DrawRig(CreatureRig rig)
        {
            CompareFunction previousZTest = Handles.zTest;
            Color previousColor = Handles.color;
            Handles.zTest = _alwaysOnTop ? CompareFunction.Always : CompareFunction.LessEqual;

            IReadOnlyList<Transform> bones = rig.IndexedBones;
            SkeletonSnapshot snapshot = rig.RestSkeleton;
            for (int i = 0; i < bones.Count; i++)
            {
                Transform bone = bones[i];
                if (bone == null) continue;

                BoneSnapshot boneData = snapshot[i];
                bool selected = Selection.activeGameObject == bone.gameObject;
                float handleSize = HandleUtility.GetHandleSize(bone.position);
                float jointSize = handleSize * (selected ? 0.12f : 0.075f);
                float width = selected ? _lineWidth * 2f : _lineWidth;
                Handles.color = selected ? Color.yellow : Color.white;

                if (boneData.HasSegment && (boneData.EndPosition - boneData.Position).sqrMagnitude > 1e-10f)
                {
                    Vector3 end = ResolveCurrentSegmentEnd(i, boneData, bones, snapshot);
                    Handles.DrawAAPolyLine(width, bone.position, end);
                }
                else if (boneData.ParentIndex >= 0 && boneData.ParentIndex < bones.Count)
                {
                    Transform parent = bones[boneData.ParentIndex];
                    if (parent != null)
                    {
                        Handles.DrawAAPolyLine(width, parent.position, bone.position);
                    }
                }

                if (_selectable)
                {
                    if (Handles.Button(
                        bone.position,
                        Quaternion.identity,
                        jointSize,
                        jointSize,
                        Handles.SphereHandleCap))
                    {
                        Selection.activeGameObject = bone.gameObject;
                        SceneView.RepaintAll();
                    }
                }
                else
                {
                    Handles.SphereHandleCap(0, bone.position, Quaternion.identity, jointSize, EventType.Repaint);
                }

                if (_labels && (selected || ShouldLabelBone(boneData)))
                {
                    string label = GetBoneLabel(boneData);
                    Handles.Label(
                        bone.position + Vector3.up * handleSize * 0.08f,
                        label,
                        GetLabelStyle());
                }
            }

            Handles.color = previousColor;
            Handles.zTest = previousZTest;
        }

        private static Vector3 ResolveCurrentSegmentEnd(
            int boneIndex,
            BoneSnapshot boneData,
            IReadOnlyList<Transform> bones,
            SkeletonSnapshot snapshot)
        {
            IReadOnlyList<int> children = snapshot.GetChildren(boneIndex);
            int bestChild = -1;
            for (int i = 0; i < children.Count; i++)
            {
                int childIndex = children[i];
                BoneSnapshot childData = snapshot[childIndex];
                if (!string.Equals(childData.SourcePartId, boneData.SourcePartId, StringComparison.Ordinal)) continue;
                if (childData.IsMirrored != boneData.IsMirrored) continue;
                if ((childData.Position - boneData.EndPosition).sqrMagnitude > 1e-8f) continue;
                if (bestChild < 0 || string.CompareOrdinal(childData.Id, snapshot[bestChild].Id) < 0)
                    bestChild = childIndex;
            }

            if (bestChild >= 0 && bones[bestChild] != null)
                return bones[bestChild].position;

            Transform current = bones[boneIndex];
            Vector3 restOffset = boneData.EndPosition - boneData.Position;
            return current.position + current.rotation * restOffset;
        }

        private static string GetBoneLabel(BoneSnapshot bone)
        {
            string display = GetDisplayName(bone);
            if (string.Equals(display, bone.Id, StringComparison.Ordinal)) return display;
            return display + "\n" + bone.Id;
        }

        private static string GetDisplayName(BoneSnapshot bone)
        {
            if (bone.Id == AnatomicalBodyRigLayout.PelvisBoneId) return "Pelvis";
            if (bone.Id == AnatomicalBodyRigLayout.SpineBoneId) return "Spine / Chest";
            if (bone.Id == AnatomicalBodyRigLayout.HeadBoneId) return "Head";
            if (bone.Id == AnatomicalBodyRigLayout.TailBoneId) return "Tail";

            string id = bone.Id;
            int separator = id.IndexOf(SemanticBoneResolver.LimbJointBoneSeparator, StringComparison.Ordinal);
            if (separator > 0)
            {
                string part = id.Substring(0, separator);
                string suffix = id.Substring(separator + SemanticBoneResolver.LimbJointBoneSeparator.Length);
                return bone.PartType + " / " + part + " / Joint " + suffix;
            }
            return bone.PartType + " / " + id;
        }

        private static bool ShouldLabelBone(BoneSnapshot bone)
        {
            if (bone.Id == AnatomicalBodyRigLayout.PelvisBoneId
                || bone.Id == AnatomicalBodyRigLayout.SpineBoneId
                || bone.Id == AnatomicalBodyRigLayout.HeadBoneId
                || bone.Id == AnatomicalBodyRigLayout.TailBoneId)
            {
                return true;
            }

            return bone.HasSegment || bone.HasChildAttachmentPosition;
        }

        private static void DrawOverlayControls(SceneView sceneView)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 10f, 290f, 280f), "Rig Debug", GUI.skin.window);

            bool enabled = GUILayout.Toggle(_enabled, "Enable rig overlay");
            if (enabled != _enabled)
            {
                _enabled = enabled;
                EditorPrefs.SetBool(EnabledKey, _enabled);
                SceneView.RepaintAll();
            }

            if (_enabled)
            {
                bool alwaysOnTop = GUILayout.Toggle(_alwaysOnTop, "X-ray / always on top");
                if (alwaysOnTop != _alwaysOnTop)
                {
                    _alwaysOnTop = alwaysOnTop;
                    EditorPrefs.SetBool(AlwaysOnTopKey, _alwaysOnTop);
                    SceneView.RepaintAll();
                }

                bool labels = GUILayout.Toggle(_labels, "Bone labels");
                if (labels != _labels)
                {
                    _labels = labels;
                    EditorPrefs.SetBool(LabelsKey, _labels);
                    SceneView.RepaintAll();
                }

                bool selectable = GUILayout.Toggle(_selectable, "Click bones to select");
                if (selectable != _selectable)
                {
                    _selectable = selectable;
                    EditorPrefs.SetBool(SelectableKey, _selectable);
                    SceneView.RepaintAll();
                }

                bool showRawMesh = GUILayout.Toggle(_showRawMesh, "Show raw generated mesh");
                if (showRawMesh != _showRawMesh)
                {
                    _showRawMesh = showRawMesh;
                    EditorPrefs.SetBool(RawMeshKey, _showRawMesh);
                    SetRawMeshVisibility(_showRawMesh);
                    SceneView.RepaintAll();
                }

                _lineWidth = GUILayout.HorizontalSlider(_lineWidth, 1f, 8f);
                EditorPrefs.SetFloat(WidthKey, _lineWidth);
                EditorGUILayout.LabelField($"Bone width: {_lineWidth:0.0}");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Frame Skeleton")) FrameBones(sceneView, GetAllBones());
                if (GUILayout.Button("Frame Selected Chain"))
                {
                    FrameBones(sceneView, GetSelectedChain());
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Focus Limbs")) FrameBones(sceneView, GetLimbBones());
                if (GUILayout.Button("Focus Body")) FrameBones(sceneView, GetBodyBones());
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Focus Selected Bone"))
                {
                    Transform selected = Selection.activeTransform;
                    if (selected != null) FrameBones(sceneView, new List<Transform> { selected });
                }
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void SetRawMeshVisibility(bool visible)
        {
            CreatureRig[] rigs = UnityEngine.Object.FindObjectsByType<CreatureRig>();
            for (int i = 0; i < rigs.Length; i++)
            {
                CreatureRig rig = rigs[i];
                if (rig == null) continue;

                MeshFilter rawFilter = rig.GetComponent<MeshFilter>();
                MeshRenderer rawRenderer = rig.GetComponent<MeshRenderer>();
                if (rawRenderer != null)
                {
                    rawRenderer.enabled = visible && rawFilter != null && rawFilter.sharedMesh != null;
                }

                CreatureSkinnedMeshRenderer skinned = rig.GetComponent<CreatureSkinnedMeshRenderer>();
                if (skinned != null && skinned.Renderer != null)
                {
                    skinned.Renderer.enabled = !visible;
                }
            }
        }

        private static List<Transform> GetAllBones()
        {
            var result = new List<Transform>();
            CreatureRig[] rigs = UnityEngine.Object.FindObjectsByType<CreatureRig>();
            for (int r = 0; r < rigs.Length; r++)
            {
                if (rigs[r] == null) continue;
                IReadOnlyList<Transform> bones = rigs[r].IndexedBones;
                for (int i = 0; i < bones.Count; i++)
                    if (bones[i] != null) result.Add(bones[i]);
            }
            return result;
        }

        private static List<Transform> GetSelectedChain()
        {
            Transform selected = Selection.activeTransform;
            if (selected == null) return new List<Transform>();

            CreatureRig[] rigs = UnityEngine.Object.FindObjectsByType<CreatureRig>();
            for (int r = 0; r < rigs.Length; r++)
            {
                CreatureRig rig = rigs[r];
                if (rig == null) continue;
                IReadOnlyList<Transform> bones = rig.IndexedBones;
                for (int i = 0; i < bones.Count; i++)
                {
                    if (bones[i] != selected) continue;

                    var result = new List<Transform>();
                    int current = i;
                    int guard = 0;
                    while (current >= 0 && current < rig.RestSkeleton.Count)
                    {
                        if (++guard > rig.RestSkeleton.Count) break;
                        Transform currentTransform = bones[current];
                        if (currentTransform != null && !result.Contains(currentTransform)) result.Add(currentTransform);

                        int parent = rig.RestSkeleton[current].ParentIndex;
                        if (parent < 0 || rig.RestSkeleton[parent].SourcePartId == CreatureDefinition.BodyId) break;
                        current = parent;
                    }

                    BoneSnapshot anchor = rig.RestSkeleton[current];
                    for (int j = 0; j < bones.Count; j++)
                    {
                        BoneSnapshot candidate = rig.RestSkeleton[j];
                        if (string.Equals(candidate.SourcePartId, anchor.SourcePartId, StringComparison.Ordinal)
                            && candidate.IsMirrored == anchor.IsMirrored
                            && candidate.HasSegment
                            && bones[j] != null
                            && !result.Contains(bones[j]))
                        {
                            result.Add(bones[j]);
                        }
                    }

                    return result;
                }
            }
            return new List<Transform> { selected };
        }

        private static List<Transform> GetLimbBones()
        {
            var result = new List<Transform>();
            CreatureRig[] rigs = UnityEngine.Object.FindObjectsByType<CreatureRig>();
            for (int r = 0; r < rigs.Length; r++)
            {
                CreatureRig rig = rigs[r];
                if (rig == null || rig.RestSkeleton == null) continue;
                IReadOnlyList<Transform> bones = rig.IndexedBones;
                for (int i = 0; i < bones.Count; i++)
                {
                    BoneSnapshot bone = rig.RestSkeleton[i];
                    if (bone.HasSegment || bone.HasChildAttachmentPosition) result.Add(bones[i]);
                }
            }
            return result;
        }

        private static List<Transform> GetBodyBones()
        {
            var result = new List<Transform>();
            CreatureRig[] rigs = UnityEngine.Object.FindObjectsByType<CreatureRig>();
            for (int r = 0; r < rigs.Length; r++)
            {
                CreatureRig rig = rigs[r];
                if (rig == null) continue;
                IReadOnlyList<Transform> bones = rig.IndexedBones;
                for (int i = 0; i < bones.Count; i++)
                    if (rig.RestSkeleton[i].SourcePartId == CreatureDefinition.BodyId) result.Add(bones[i]);
            }
            return result;
        }

        private static void FrameBones(SceneView sceneView, List<Transform> bones)
        {
            if (sceneView == null || bones == null || bones.Count == 0) return;

            UnityEngine.Object[] previousSelection = Selection.objects;
            try
            {
                Selection.objects = bones.ConvertAll(b => b.gameObject).ToArray();
                sceneView.FrameSelected();
            }
            finally
            {
                Selection.objects = previousSelection;
            }
        }
    }
}
