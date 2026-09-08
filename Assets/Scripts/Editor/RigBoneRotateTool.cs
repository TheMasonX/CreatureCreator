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
            if (!TryGetSelectedRigBone(out Transform bone)) return;

            Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local
                ? bone.rotation
                : Quaternion.identity;

            EditorGUI.BeginChangeCheck();
            Quaternion nextRotation = Handles.RotationHandle(handleRotation, bone.position);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(bone, "Rotate Creature Rig Bone");
            bone.rotation = nextRotation;
            EditorUtility.SetDirty(bone);
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
