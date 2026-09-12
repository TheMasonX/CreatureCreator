using UnityEngine;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Pure rotation math for <see cref="RigBoneRotateTool"/>.
    ///
    /// Kept free of SceneView, selection, and GUI state so the handle-frame
    /// composition can be covered by EditMode tests. The bug this replaces was a
    /// coordinate-space error, not a GUI error: assigning the raw
    /// <c>Handles.RotationHandle</c> result to <c>bone.rotation</c> is only valid
    /// when the handle frame equals the bone's own rotation. In Global pivot mode
    /// the frame is <c>Quaternion.identity</c>, so the bone's rotation was
    /// discarded and the bone snapped on the first drag.
    /// </summary>
    internal static class RigBoneRotation
    {
        /// <summary>
        /// Handle frame to draw for a bone. Global pivot aligns the rings to world
        /// axes; local pivot aligns them to the bone's own axes.
        /// </summary>
        public static Quaternion ResolveHandleFrame(Quaternion boneRotation, bool localPivot)
            => localPivot ? boneRotation : Quaternion.identity;

        /// <summary>
        /// Applies the world-space handle delta to the bone's rotation captured when
        /// the drag started. <paramref name="handleFrame"/> must be the same frame
        /// passed to <c>Handles.RotationHandle</c> for the whole drag, and
        /// <paramref name="startRotation"/> the bone's rotation at drag start.
        ///
        /// Unity composes the handle result as <c>result = worldDelta * frame</c>,
        /// where the delta is measured from the drag start. Passing the bone's
        /// current (already-updated) rotation as <paramref name="startRotation"/>
        /// would multiply the accumulated delta in again every frame and spin the
        /// bone, so callers must keep both inputs fixed for the gesture.
        /// </summary>
        public static Quaternion ApplyHandleDelta(Quaternion startRotation, Quaternion handleFrame, Quaternion handleResult)
        {
            Quaternion worldDelta = handleResult * Quaternion.Inverse(handleFrame);
            return worldDelta * startRotation;
        }
    }
}
