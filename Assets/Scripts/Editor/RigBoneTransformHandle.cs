using UnityEditor;
using UnityEngine;
using ProceduralCreature.Animation;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Keeps Unity's built-in transform handle anchored to the actual selected
    /// generated rig bone when the editor is in Center handle-position mode.
    ///
    /// Unity's Center mode intentionally uses a graphical selection center. A
    /// generated rig bone has no useful renderer of its own, and its hierarchy can
    /// therefore produce a handle location that is unrelated to the bone pivot.
    /// The rig overlay already defines an explicit single-bone manipulation target;
    /// when that target is selected, its transform position is the authoritative
    /// handle position. This does not change the user's global PivotMode setting.
    /// </summary>
    [InitializeOnLoad]
    internal static class RigBoneTransformHandle
    {
        static RigBoneTransformHandle()
        {
            Selection.selectionChanged += RepaintSceneViews;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Tools.pivotMode != PivotMode.Center) return;
            if (!TryGetSelectedRigBone(out Transform bone)) return;

            // Unity documents handlePosition as the world-space position of the
            // transform-tool handle. Override only for a single generated rig bone;
            // all ordinary Unity selections retain their normal Center behavior.
            Tools.handlePosition = bone.position;
        }

        private static bool TryGetSelectedRigBone(out Transform bone)
        {
            bone = null;
            if (Selection.objects == null || Selection.objects.Length != 1) return false;

            Transform selected = Selection.activeTransform;
            if (selected == null) return false;

            CreatureRig rig = selected.GetComponentInParent<CreatureRig>();
            if (rig == null) return false;

            System.Collections.Generic.IReadOnlyList<Transform> bones = rig.IndexedBones;
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != selected) continue;
                bone = selected;
                return true;
            }

            return false;
        }

        private static void RepaintSceneViews()
        {
            SceneView.RepaintAll();
        }
    }
}
