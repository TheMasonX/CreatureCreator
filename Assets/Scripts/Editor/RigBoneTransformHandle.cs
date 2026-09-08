using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProceduralCreature.Animation;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Keeps Unity's built-in transform handle on the actual selected generated
    /// rig-bone transform even when the editor is in Center handle-position mode.
    ///
    /// Unity's Center mode intentionally uses a graphical selection center. A
    /// generated rig bone is an invisible Transform-only object, so that graphical
    /// center is not a reliable bone pivot. The rig overlay defines one explicit
    /// manipulation target; when that target is selected, its world position is the
    /// authoritative handle position. This does not change the user's global
    /// PivotMode setting and does not affect ordinary Unity selections.
    /// </summary>
    [InitializeOnLoad]
    internal static class RigBoneTransformHandle
    {
        static RigBoneTransformHandle()
        {
            Selection.selectionChanged += RepaintSceneViews;
            // beforeSceneGui runs before Unity's built-in SceneView tools calculate
            // their handle position; duringSceneGui is retained as a defensive
            // refresh for tool/selection state changes that occur during the GUI pass.
            SceneView.beforeSceneGui += OnBeforeSceneGUI;
            SceneView.duringSceneGui += OnDuringSceneGUI;
        }

        private static void OnBeforeSceneGUI(SceneView sceneView)
        {
            ApplyHandlePosition();
        }

        private static void OnDuringSceneGUI(SceneView sceneView)
        {
            ApplyHandlePosition();
        }

        private static void ApplyHandlePosition()
        {
            if (Tools.pivotMode != PivotMode.Center) return;
            if (!TryGetSelectedRigBone(out Transform bone)) return;

            // Unity documents handlePosition as the world-space transform-tool handle
            // position. Set it before the built-in tool reads it, while retaining the
            // user's Center/Pivot setting for every non-rig selection.
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

            IReadOnlyList<Transform> bones = rig.IndexedBones;
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
