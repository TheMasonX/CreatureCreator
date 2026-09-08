using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using ProceduralCreature.Animation;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Dedicated rotation tool for generated CreatureRig bones.
    ///
    /// Unity's built-in Transform tool can place a Center-mode handle using graphical
    /// selection semantics that are not useful for Transform-only generated bones.
    /// This tool owns the handle placement explicitly: it is always centered on the
    /// selected bone Transform.position and writes the selected bone's world rotation.
    /// Ordinary Unity objects continue to use Unity's normal tools.
    /// </summary>
    [EditorTool("Creature Rig Bone Rotate")]
    internal sealed class RigBoneRotateTool : EditorTool
    {
        private static readonly GUIContent Icon = new GUIContent("R", "Rotate selected CreatureRig bone at its actual pivot.");

        public override GUIContent toolbarIcon => Icon;

        public override bool IsAvailable()
        {
            return TryGetSelectedRigBone(out _);
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!TryGetSelectedRigBone(out Transform bone)) return;
            if (window is not SceneView) return;

            Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local
                ? bone.rotation
                : Quaternion.identity;

            EditorGUI.BeginChangeCheck();
            Quaternion nextRotation = Handles.RotationHandle(handleRotation, bone.position);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(bone, "Rotate Creature Rig Bone");
            if (Tools.pivotRotation == PivotRotation.Local)
            {
                // RotationHandle returns a world rotation even when the handle is
                // oriented in local space, so assign world rotation directly.
                bone.rotation = nextRotation;
            }
            else
            {
                bone.rotation = nextRotation;
            }
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

            var bones = rig.IndexedBones;
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
