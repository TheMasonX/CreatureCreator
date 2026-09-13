using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
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
        private const string CollapsedKey = "ProceduralCreature.RigDebug.Collapsed";
        private const string PanelXKey = "ProceduralCreature.RigDebug.PanelX";
        private const string PanelYKey = "ProceduralCreature.RigDebug.PanelY";
        private const string PanelWidthKey = "ProceduralCreature.RigDebug.PanelWidth";
        private const string PanelHeightKey = "ProceduralCreature.RigDebug.PanelHeight";
        private const float DefaultPanelX = 10f;
        private const float DefaultPanelY = 10f;
        private const float DefaultPanelWidth = 300f;
        private const float HeaderFoldoutWidth = 160f;
        private const float GeometryEpsilonSqr = 1e-10f;
        private const float AttachmentMarkerScale = 0.06f;
        private const float SelectionPickRadiusScale = 2.5f;

        private static bool _enabled = EditorPrefs.GetBool(EnabledKey, false);
        private static bool _alwaysOnTop = EditorPrefs.GetBool(AlwaysOnTopKey, true);
        private static bool _labels = EditorPrefs.GetBool(LabelsKey, false);
        private static bool _selectable = EditorPrefs.GetBool(SelectableKey, true);
        private static bool _showRawMesh = EditorPrefs.GetBool(RawMeshKey, false);
        private static float _lineWidth = Mathf.Clamp(EditorPrefs.GetFloat(WidthKey, 4f), 1f, 8f);
        private static bool _collapsed = EditorPrefs.GetBool(CollapsedKey, false);

        // Stored panel geometry. A height of 0 means "fit the content height
        // automatically"; any larger value is a user resize that is kept.
        private static Rect _panelRect = new Rect(
            EditorPrefs.GetFloat(PanelXKey, DefaultPanelX),
            EditorPrefs.GetFloat(PanelYKey, DefaultPanelY),
            Mathf.Clamp(
                EditorPrefs.GetFloat(PanelWidthKey, DefaultPanelWidth),
                RigDebugPanelLayout.MinPanelWidth,
                RigDebugPanelLayout.MaxPanelWidth),
            Mathf.Max(0f, EditorPrefs.GetFloat(PanelHeightKey, 0f)));

        private static bool _draggingPanel;
        private static bool _resizingPanel;
        private static Vector2 _panelDragOffset;
        private static readonly int PanelControlId = "ProceduralCreature.RigDebug.Panel".GetHashCode();
        private static readonly GUIContent PanelTitle = new GUIContent("Rig Debug");
        private static readonly GUIContent EnableLabel = new GUIContent("Enable rig overlay");
        private static readonly GUIContent AlwaysOnTopLabel = new GUIContent("X-ray / always on top");
        private static readonly GUIContent LabelsLabel = new GUIContent("Bone labels");
        private static readonly GUIContent SelectableLabel = new GUIContent("Click bones to select");
        private static readonly GUIContent RawMeshLabel = new GUIContent("Show raw generated mesh");
        private static readonly GUIContent WidthLabel = new GUIContent();
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
            CreatureRig focusRig = ResolveFocusRig(rigs);
            for (int i = 0; i < rigs.Length; i++)
            {
                CreatureRig rig = rigs[i];
                if (rig == null || !rig.isActiveAndEnabled || rig.RestSkeleton == null) continue;
                if (rig.IndexedBones.Count == 0) continue;
                DrawRig(rig, focusRig == null || ReferenceEquals(focusRig, rig));
            }
        }

        /// <summary>
        /// The rig that owns the current selection, if any. Once a bone is selected
        /// only that rig's bones remain pickable, so when two rigs overlap a click
        /// can no longer rotate a different creature's bone. With no rig selection
        /// all rigs stay interactive (unchanged single-rig behavior).
        /// </summary>
        private static CreatureRig ResolveFocusRig(CreatureRig[] rigs)
        {
            if (rigs == null) return null;
            Transform selected = Selection.activeTransform;
            if (selected == null) return null;

            CreatureRig selectedRig = selected.GetComponentInParent<CreatureRig>();
            if (selectedRig == null) return null;

            for (int i = 0; i < rigs.Length; i++)
            {
                if (ReferenceEquals(rigs[i], selectedRig)) return selectedRig;
            }
            return null;
        }

        private static void DrawRig(CreatureRig rig, bool interactive)
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

                if (boneData.HasSegment && (boneData.EndPosition - boneData.Position).sqrMagnitude > GeometryEpsilonSqr)
                {
                    Vector3 end = ResolveCurrentSegmentEnd(i, boneData, bones, snapshot);
                    if ((end - bone.position).sqrMagnitude > GeometryEpsilonSqr)
                        Handles.DrawAAPolyLine(width, bone.position, end);
                }

                DrawParentAttachment(i, boneData, bone, bones, snapshot, handleSize, width);

                if (interactive)
                {
                    // Pick radius is larger than the drawn cap so small joint
                    // spheres (for example a limb elbow) stay easy to click.
                    if (Handles.Button(
                        bone.position,
                        Quaternion.identity,
                        jointSize,
                        jointSize * SelectionPickRadiusScale,
                        Handles.SphereHandleCap))
                    {
                        Selection.objects = new UnityEngine.Object[] { bone.gameObject };
                        Selection.activeGameObject = bone.gameObject;
                        ToolManager.SetActiveTool<RigBoneRotateTool>();
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

        private static void DrawParentAttachment(
            int boneIndex,
            BoneSnapshot boneData,
            Transform bone,
            IReadOnlyList<Transform> bones,
            SkeletonSnapshot snapshot,
            float handleSize,
            float width)
        {
            int parentIndex = boneData.ParentIndex;
            if (parentIndex < 0 || parentIndex >= bones.Count) return;

            Transform parent = bones[parentIndex];
            if (parent == null) return;

            Vector3 attachment = ResolveParentAttachmentPoint(
                parentIndex,
                bone.position,
                parent,
                snapshot[parentIndex],
                bones,
                snapshot);
            if ((attachment - bone.position).sqrMagnitude <= GeometryEpsilonSqr) return;

            // The parent attachment is a derived, non-selectable link, not a bone.
            // Draw it dim and thin so it never reads as a clickable joint, and
            // restore the caller's color so the real joint cap is unaffected.
            Color previousColor = Handles.color;
            Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            Handles.DrawAAPolyLine(Mathf.Max(1f, width * 0.5f), attachment, bone.position);

            float markerSize = handleSize * AttachmentMarkerScale * 0.5f;
            Handles.SphereHandleCap(0, attachment, Quaternion.identity, markerSize, EventType.Repaint);
            Handles.color = previousColor;
        }

        private static Vector3 ResolveParentAttachmentPoint(
            int parentIndex,
            Vector3 childCurrentPosition,
            Transform parentTransform,
            BoneSnapshot parentData,
            IReadOnlyList<Transform> bones,
            SkeletonSnapshot snapshot)
        {
            if (!parentData.HasSegment)
                return parentTransform.position;

            Vector3 start = parentTransform.position;
            Vector3 end = ResolveCurrentSegmentEnd(parentIndex, parentData, bones, snapshot);
            Vector3 segment = end - start;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= GeometryEpsilonSqr)
                return start;

            float t = Mathf.Clamp01(Vector3.Dot(childCurrentPosition - start, segment) / segmentLengthSqr);
            return start + segment * t;
        }

        private static Vector3 ResolveCurrentSegmentEnd(
            int boneIndex,
            BoneSnapshot boneData,
            IReadOnlyList<Transform> bones,
            SkeletonSnapshot snapshot)
        {
            if (bones != null && snapshot != null)
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
                return ResolveCurrentRestOrientedEndpoint(current, boneData);
            }

            return ResolveCurrentRestOrientedEndpoint(null, boneData);
        }

        private static Vector3 ResolveCurrentRestOrientedEndpoint(Transform current, BoneSnapshot boneData)
        {
            if (current == null) return boneData.EndPosition;

            Vector3 restOffsetWorld = boneData.EndPosition - boneData.Position;
            Vector3 restOffsetLocal = Quaternion.Inverse(boneData.Rotation) * restOffsetWorld;
            return current.position + current.rotation * restOffsetLocal;
        }

        private static string GetBoneLabel(BoneSnapshot bone)
        {
            string display = GetDisplayName(bone);
            if (string.Equals(display, bone.Id, StringComparison.Ordinal)) return display;
            return display + "\n" + bone.Id;
        }

        private static string GetDisplayName(BoneSnapshot bone)
        {
            if (bone.Id == AnatomicalBodyRigLayout.BodyRootBoneId) return "Body Root";
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
            if (bone.Id == AnatomicalBodyRigLayout.BodyRootBoneId
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

            // Geometry comes from the overlay state captured at the start of the
            // frame, so the drawn panel height and the drawn rows always agree.
            // A toggle requests a repaint, so its new geometry applies next frame.
            bool layoutEnabled = _enabled;
            var viewSize = new Vector2(sceneView.position.width, sceneView.position.height);
            float height = RigDebugPanelLayout.EffectiveHeight(_collapsed, layoutEnabled, _panelRect.height);
            Vector2 origin = RigDebugPanelLayout.ClampPosition(
                new Vector2(_panelRect.x, _panelRect.y),
                new Vector2(_panelRect.width, height),
                viewSize);
            var panel = new Rect(origin.x, origin.y, _panelRect.width, height);

            DrawPanelBackground(panel);
            DrawPanelHeader(sceneView, panel);

            if (!_collapsed)
            {
                DrawPanelBody(sceneView, panel, layoutEnabled);
                DrawResizeGrip(RigDebugPanelLayout.ResizeGripRect(panel));
            }

            HandlePanelInput(sceneView, panel);

            Handles.EndGUI();
        }

        private static void DrawPanelBackground(Rect panel)
        {
            Color background = EditorGUIUtility.isProSkin
                ? new Color(0.14f, 0.14f, 0.14f, 0.9f)
                : new Color(0.85f, 0.85f, 0.85f, 0.94f);
            Color headerBackground = EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f, 0.98f)
                : new Color(0.72f, 0.72f, 0.72f, 0.98f);
            Color border = EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.65f)
                : new Color(0.4f, 0.4f, 0.4f, 0.65f);

            EditorGUI.DrawRect(panel, background);
            EditorGUI.DrawRect(RigDebugPanelLayout.HeaderRect(panel), headerBackground);
            EditorGUI.DrawRect(new Rect(panel.x, panel.y, panel.width, 1f), border);
            EditorGUI.DrawRect(new Rect(panel.x, panel.yMax - 1f, panel.width, 1f), border);
            EditorGUI.DrawRect(new Rect(panel.x, panel.y, 1f, panel.height), border);
            EditorGUI.DrawRect(new Rect(panel.xMax - 1f, panel.y, 1f, panel.height), border);
        }

        private static void DrawPanelHeader(SceneView sceneView, Rect panel)
        {
            bool expanded = !_collapsed;
            bool newExpanded = EditorGUI.Foldout(HeaderFoldoutRect(panel), expanded, PanelTitle, true);
            if (newExpanded != expanded)
            {
                _collapsed = RigDebugPanelLayout.CollapsedFromFoldout(newExpanded);
                EditorPrefs.SetBool(CollapsedKey, _collapsed);
                sceneView.Repaint();
            }
        }

        private static Rect HeaderFoldoutRect(Rect panel)
        {
            Rect header = RigDebugPanelLayout.HeaderRect(panel);
            return new Rect(
                header.x + 4f,
                header.y,
                Mathf.Min(header.width - 8f, HeaderFoldoutWidth),
                header.height);
        }

        private static void DrawPanelBody(SceneView sceneView, Rect panel, bool layoutEnabled)
        {
            Rect enableRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.Enable, panel, layoutEnabled);
            bool enabled = EditorGUI.Toggle(enableRect, EnableLabel, _enabled);
            if (enabled != _enabled)
            {
                _enabled = enabled;
                EditorPrefs.SetBool(EnabledKey, _enabled);
                sceneView.Repaint();
            }

            if (!layoutEnabled) return;

            Rect alwaysOnTopRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.AlwaysOnTop, panel, layoutEnabled);
            bool alwaysOnTop = EditorGUI.Toggle(alwaysOnTopRect, AlwaysOnTopLabel, _alwaysOnTop);
            if (alwaysOnTop != _alwaysOnTop)
            {
                _alwaysOnTop = alwaysOnTop;
                EditorPrefs.SetBool(AlwaysOnTopKey, _alwaysOnTop);
                sceneView.Repaint();
            }

            Rect labelsRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.Labels, panel, layoutEnabled);
            bool labels = EditorGUI.Toggle(labelsRect, LabelsLabel, _labels);
            if (labels != _labels)
            {
                _labels = labels;
                EditorPrefs.SetBool(LabelsKey, _labels);
                sceneView.Repaint();
            }

            Rect selectableRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.Selectable, panel, layoutEnabled);
            bool selectable = EditorGUI.Toggle(selectableRect, SelectableLabel, _selectable);
            if (selectable != _selectable)
            {
                _selectable = selectable;
                EditorPrefs.SetBool(SelectableKey, _selectable);
                sceneView.Repaint();
            }

            Rect rawMeshRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.ShowRawMesh, panel, layoutEnabled);
            bool showRawMesh = EditorGUI.Toggle(rawMeshRect, RawMeshLabel, _showRawMesh);
            if (showRawMesh != _showRawMesh)
            {
                _showRawMesh = showRawMesh;
                EditorPrefs.SetBool(RawMeshKey, _showRawMesh);
                SetRawMeshVisibility(_showRawMesh);
                sceneView.Repaint();
            }

            Rect sliderRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.LineWidthSlider, panel, layoutEnabled);
            _lineWidth = GUI.HorizontalSlider(sliderRect, _lineWidth, 1f, 8f);
            EditorPrefs.SetFloat(WidthKey, _lineWidth);

            // The width label owns its own row. The previous fixed-height area let
            // it overlap the controls below; those controls now claim this row's
            // space because the panel height is computed from the row count.
            Rect widthLabelRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.LineWidthLabel, panel, layoutEnabled);
            WidthLabel.text = $"Bone width: {_lineWidth:0.0}";
            EditorGUI.LabelField(widthLabelRect, WidthLabel);

            Rect frameRowRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.FrameButtons, panel, layoutEnabled);
            float columnWidth = (frameRowRect.width - RigDebugPanelLayout.RowSpacing) * 0.5f;
            float secondColumnX = frameRowRect.x + columnWidth + RigDebugPanelLayout.RowSpacing;
            if (GUI.Button(new Rect(frameRowRect.x, frameRowRect.y, columnWidth, frameRowRect.height), "Frame Skeleton"))
            {
                FrameBones(sceneView, GetAllBones());
            }
            if (GUI.Button(new Rect(secondColumnX, frameRowRect.y, columnWidth, frameRowRect.height), "Frame Selected Chain"))
            {
                FrameBones(sceneView, GetSelectedChain());
            }

            Rect focusRowRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.FocusButtons, panel, layoutEnabled);
            if (GUI.Button(new Rect(focusRowRect.x, focusRowRect.y, columnWidth, focusRowRect.height), "Focus Limbs"))
            {
                FrameBones(sceneView, GetLimbBones());
            }
            if (GUI.Button(new Rect(secondColumnX, focusRowRect.y, columnWidth, focusRowRect.height), "Focus Body"))
            {
                FrameBones(sceneView, GetBodyBones());
            }

            Rect focusSelectedRect = RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.FocusSelectedBone, panel, layoutEnabled);
            if (GUI.Button(focusSelectedRect, "Focus Selected Bone"))
            {
                Transform selected = Selection.activeTransform;
                if (selected != null) FrameBones(sceneView, new List<Transform> { selected });
            }
        }

        private static void DrawResizeGrip(Rect grip)
        {
            EditorGUIUtility.AddCursorRect(grip, MouseCursor.ResizeUpLeft);

            Color color = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.4f)
                : new Color(0f, 0f, 0f, 0.4f);
            const float dotSize = 2f;
            const float dotGap = 3f;
            for (int i = 0; i < 3; i++)
            {
                float offset = i * (dotSize + dotGap);
                EditorGUI.DrawRect(
                    new Rect(grip.xMax - dotSize - offset, grip.yMax - dotSize - offset, dotSize, dotSize),
                    color);
            }
        }

        private static void HandlePanelInput(SceneView sceneView, Rect panel)
        {
            Event current = Event.current;
            if (current == null) return;

            switch (current.type)
            {
                case EventType.MouseDown:
                    if (current.button != 0) return;
                    // The foldout owns its own click; leave the event for it.
                    if (HeaderFoldoutRect(panel).Contains(current.mousePosition)) return;
                    if (TryBeginPanelResize(panel, current)) return;
                    if (TryBeginPanelDrag(panel, current)) return;
                    // Swallow clicks on the panel body so the SceneView behind it
                    // does not clear the current bone selection.
                    if (panel.Contains(current.mousePosition)) current.Use();
                    return;

                case EventType.MouseDrag:
                    if (_draggingPanel)
                    {
                        Vector2 clamped = RigDebugPanelLayout.ClampPosition(
                            current.mousePosition - _panelDragOffset,
                            panel.size,
                            new Vector2(sceneView.position.width, sceneView.position.height));
                        _panelRect.x = clamped.x;
                        _panelRect.y = clamped.y;
                        current.Use();
                        sceneView.Repaint();
                    }
                    else if (_resizingPanel)
                    {
                        Vector2 clamped = RigDebugPanelLayout.ClampSize(
                            new Vector2(
                                current.mousePosition.x - _panelRect.x,
                                current.mousePosition.y - _panelRect.y),
                            _enabled);
                        _panelRect.width = clamped.x;
                        _panelRect.height = clamped.y;
                        current.Use();
                        sceneView.Repaint();
                    }
                    return;

                case EventType.MouseUp:
                    if (!_draggingPanel && !_resizingPanel) return;
                    _draggingPanel = false;
                    _resizingPanel = false;
                    if (GUIUtility.hotControl == PanelControlId) GUIUtility.hotControl = 0;
                    SavePanelRect();
                    return;
            }
        }

        private static bool TryBeginPanelDrag(Rect panel, Event current)
        {
            if (!RigDebugPanelLayout.HeaderRect(panel).Contains(current.mousePosition)) return false;

            _draggingPanel = true;
            _panelDragOffset = current.mousePosition - panel.position;
            GUIUtility.hotControl = PanelControlId;
            current.Use();
            return true;
        }

        private static bool TryBeginPanelResize(Rect panel, Event current)
        {
            if (_collapsed) return false;
            if (!RigDebugPanelLayout.ResizeGripRect(panel).Contains(current.mousePosition)) return false;

            _resizingPanel = true;
            GUIUtility.hotControl = PanelControlId;
            current.Use();
            return true;
        }

        private static void SavePanelRect()
        {
            EditorPrefs.SetFloat(PanelXKey, _panelRect.x);
            EditorPrefs.SetFloat(PanelYKey, _panelRect.y);
            EditorPrefs.SetFloat(PanelWidthKey, _panelRect.width);
            EditorPrefs.SetFloat(PanelHeightKey, _panelRect.height);
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
                            && !result.Contains(bones[j]))
                        {
                            result.Add(bones[j]);
                        }
                    }
                    return result;
                }
            }
            return new List<Transform>();
        }

        private static List<Transform> GetLimbBones()
        {
            var result = new List<Transform>();
            CreatureRig[] rigs = UnityEngine.Object.FindObjectsByType<CreatureRig>();
            for (int r = 0; r < rigs.Length; r++)
            {
                CreatureRig rig = rigs[r];
                if (rig == null) continue;
                IReadOnlyList<Transform> bones = rig.IndexedBones;
                for (int i = 0; i < bones.Count; i++)
                {
                    if (bones[i] != null && rig.RestSkeleton[i].PartType != PartType.Body)
                        result.Add(bones[i]);
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
                {
                    if (bones[i] != null && rig.RestSkeleton[i].PartType == PartType.Body)
                        result.Add(bones[i]);
                }
            }
            return result;
        }

        private static void FrameBones(SceneView sceneView, List<Transform> bones)
        {
            if (sceneView == null || bones == null || bones.Count == 0) return;
            var bounds = new Bounds(bones[0].position, Vector3.zero);
            for (int i = 1; i < bones.Count; i++) bounds.Encapsulate(bones[i].position);
            sceneView.Frame(bounds, false);
        }
    }
}
