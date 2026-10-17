using UnityEngine;

namespace Turno0313
{
    public static class ViewUtil
    {
        static readonly Vector3[] samples = new Vector3[3];

        // True when part of the bounds is inside the camera frustum and not hidden behind walls.
        // Hits on colliders under 'self' don't count as blockers.
        public static bool CanSee(Plane[] frustum, Vector3 eye, Bounds bounds, Transform self, float maxDistance = 40f)
        {
            if (bounds.size == Vector3.zero || !GeometryUtility.TestPlanesAABB(frustum, bounds))
                return false;
            samples[0] = bounds.center;
            samples[1] = bounds.center + Vector3.up * bounds.extents.y * .7f;
            samples[2] = bounds.center - Vector3.up * bounds.extents.y * .7f;
            foreach (var point in samples)
            {
                var delta = point - eye;
                float distance = delta.magnitude;
                if (distance > maxDistance || distance < .01f)
                    continue;
                if (!Physics.Raycast(eye, delta / distance, out var hit, distance - .05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return true;
                if (self && hit.transform.IsChildOf(self))
                    return true;
            }

            return false;
        }
    }
}
