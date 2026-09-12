using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using ProceduralCreature.Animation;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Dedicated rotation tool for generated CreatureRig bones.
    ///
    /// Unity's built-in Transform tool can use graphical selection-center semantics
    /// that are not useful for Transform-only generated bones. This tool owns the
    /// handle placement explicitly: it is centered on the selected bone's actual
    /// Transform.position and writes the selected bone's world rotation.
    /// Ordinary Unity objects continue to use Unity's normal tools.
    /// </summary>
    [EditorTool("Creature Rig Bone Rotate")]
    internal sealed class RigBoneRotateTool : EditorTool
    {
        private static readonly GUIContent Icon = new GUIContent(
            "R",
            "Rotate selected CreatureRig bone at its actual pivot.");

        private static Transform _dragBone;
        private static Quaternion _dragHandleFrame = Quaternion.identity;
        private static Quaternion _dragStartRotation = Quaternion.identity;
        private static bool _dragUndoRecorded;

        public override GUIContent toolbarIcon => Icon;

        public override bool IsAvailable()
        {
            return TryGetSelectedRigBone(out _);
        }

        [MenuItem("Tools/Creature Creator/Rig Bone Rotate", priority = 1200)]
        private static void ActivateFromMenu()
        {
            if (TryGetSelectedRigBone(out _))
                ToolManager.SetActiveTool<RigBoneRotateTool>();
        }

        [MenuItem("Tools/Creature Creator/Rig Bone Rotate", validate = true)]
        private static bool ValidateActivateFromMenu()
        {
            return TryGetSelectedRigBone(out _);
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView)) return;
            if (!TryGetSelectedRigBone(out Transform bone))
            {
                ResetDrag();
                return;
            }

            // Handles.RotationHandle reports the rotation relative to the frame it
            // was given when the drag started, and it does not update that frame
            // while the drag runs. The handle frame and the bone's start rotation
            // must therefore be captured once per gesture. Recomputing either from
            // the moving bone every frame multiplies the accumulated delta again and
            // spins the bone; that was the "moves too fast, independent of drag"
            // regression.
            bool dragging = ReferenceEquals(_dragBone, bone) && GUIUtility.hotControl != 0;
            if (!dragging)
            {
                _dragBone = bone;
                _dragHandleFrame = RigBoneRotation.ResolveHandleFrame(
                    bone.rotation,
                    Tools.pivotRotation == PivotRotation.Local);
                _dragStartRotation = bone.rotation;
                _dragUndoRecorded = false;
            }

            EditorGUI.BeginChangeCheck();
            Quaternion handleResult = Handles.RotationHandle(_dragHandleFrame, bone.position);
            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                if (!_dragUndoRecorded)
                {
                    Undo.RecordObject(bone, "Rotate Creature Rig Bone");
                    _dragUndoRecorded = true;
                }

                // The handle result is the world-space drag delta composed with the
                // locked frame (result = worldDelta * frame), so recovering the delta
                // and applying it once to the bone's start rotation is exact and
                // independent of the previous frame.
                bone.rotation = RigBoneRotation.ApplyHandleDelta(
                    _dragStartRotation,
                    _dragHandleFrame,
                    handleResult);
                EditorUtility.SetDirty(bone);
            }

            if (Event.current.type == EventType.MouseUp) ResetDrag();
        }

        private static void ResetDrag()
        {
            _dragBone = null;
            _dragHandleFrame = Quaternion.identity;
            _dragStartRotation = Quaternion.identity;
            _dragUndoRecorded = false;
        }

        private static bool TryGetSelectedRigBone(out Transform bone)
        {
            bone = null;
            if (Selection.objects == null || Selection.objects.Length != 1) return false;

            Transform selected = Selection.activeTransform;
            if (selected == null) return false;

            CreatureRig rig = selected.GetComponentInParent<CreatureRig>();
            if (rig == null) return false;

            IReadOnlyList<Transform> bones = rig.IndexedBones;
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != selected) continue;
                bone = selected;
                return true;
            }

            return false;
        }
    }
}
