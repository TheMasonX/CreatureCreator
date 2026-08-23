using UnityEngine;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Shared math for the Body sample radius affordance. The handle is a simple
    /// outward radial drag whose length is the sample's current Radius, with a
    /// minimum floor so tiny bodies never shrink to an unselectable handle.
    /// </summary>
    public static class BodySampleRadiusHandle
    {
        public static float ComputeRadius(Vector3 samplePosition, Vector3 handlePosition, float minimumRadius)
        {
            float radius = Vector3.Distance(samplePosition, handlePosition);
            return Mathf.Max(radius, minimumRadius);
        }

        public static Vector3 GetHandlePosition(Vector3 samplePosition, float radius, Vector3 dragAxis)
        {
            if (dragAxis.sqrMagnitude <= 1e-6f)
            {
                return samplePosition + Vector3.right * Mathf.Max(radius, 0.01f);
            }

            return samplePosition + dragAxis.normalized * Mathf.Max(radius, 0.01f);
        }
    }
}
