using System.Collections.Generic;
using UnityEngine;

namespace Turno0313
{
    // Something that only shows up through certain security cameras (Act 2): the person in one camera, the
    // escaped clock, the sleeping guard of the final. The player's own eyes never see it and it has no collider,
    // so nobody bumps into it; the camera screen finds it by where it is drawn (Pick).
    // How: while enabled it takes one of the "CamaraSolo1-3" layers; the player's camera never draws those
    // layers and each security camera draws only the ones meant for it. (Turning renderers off per camera does
    // not work here: the project's GPU Resident Drawer ignores it.)
    public sealed class CameraOnly : MonoBehaviour
    {
        public const string LayerPrefix = "CamaraSolo";
        public const int Slots = 3;
        // The opposite: what only the player's own camera draws (his hand with the flashlight).
        public const string PlayerOnlyLayer = "SoloJugador";

        [Tooltip("Las cámaras de seguridad donde se ve.")]
        [SerializeField] SecurityCamera[] cameras = new SecurityCamera[0];

        static readonly List<CameraOnly> active = new List<CameraOnly>();
        static readonly CameraOnly[] slots = new CameraOnly[Slots];
        Renderer[] renderers;
        int slot = -1;

        public IReadOnlyList<SecurityCamera> Cameras => cameras;

        void Awake() => renderers = GetComponentsInChildren<Renderer>(true);

        void OnEnable()
        {
            for (int i = 0; i < Slots && slot < 0; i++)
                if (!slots[i] && Layer(i) >= 0)
                {
                    slot = i;
                    slots[i] = this;
                }

            if (slot < 0)
                Debug.LogWarning("No quedan capas " + LayerPrefix + " libres (máximo " + Slots + " a la vez, o faltan en Tags and Layers): " + name + " se verá en todas las cámaras.", this);
            else
                foreach (var r in renderers)
                    if (r)
                        r.gameObject.layer = Layer(slot);
            active.Add(this);
            Refresh();
        }

        void OnDisable()
        {
            active.Remove(this);
            if (slot >= 0 && slots[slot] == this)
                slots[slot] = null;
            slot = -1;
            Refresh();
        }

        public void ShowIn(params SecurityCamera[] cams)
        {
            cameras = cams;
            Refresh();
        }

        public bool VisibleIn(Camera cam)
        {
            foreach (var c in cameras)
                if (c && c.Camera == cam)
                    return true;
            return false;
        }

        public Bounds Bounds
        {
            get
            {
                var bounds = new Bounds();
                bool any = false;
                foreach (var r in renderers)
                {
                    if (!r || !r.enabled || !r.gameObject.activeInHierarchy)
                        continue;
                    if (any)
                        bounds.Encapsulate(r.bounds);
                    else
                    {
                        bounds = r.bounds;
                        any = true;
                    }
                }

                return bounds;
            }
        }

        // Every game camera: the camera-only layers off, then each security camera gets back the ones meant for it.
        // Security cameras come from their own list: the ones not on screen are disabled, and Camera.allCameras
        // only lists enabled cameras.
        public static void Refresh()
        {
            int all = 0;
            for (int i = 0; i < Slots; i++)
                if (Layer(i) >= 0)
                    all |= 1 << Layer(i);
            foreach (var cam in Camera.allCameras)
                if (cam.cameraType == CameraType.Game && !cam.GetComponent<SecurityCamera>())
                    cam.cullingMask &= ~all;
            foreach (var security in SecurityCamera.All)
            {
                var cam = security.Camera;
                if (!cam)
                    continue;
                int mask = cam.cullingMask & ~all;
                foreach (var c in active)
                    if (c.slot >= 0 && c.VisibleIn(cam))
                        mask |= 1 << Layer(c.slot);
                cam.cullingMask = mask;
            }
        }

        static int Layer(int i) => LayerMask.NameToLayer(LayerPrefix + (i + 1));

        // What a security camera shows under a viewport point, if it is nearer than the first wall the ray hits.
        public static CameraOnly Pick(SecurityCamera cam, Vector2 viewport, float wallDistance)
        {
            CameraOnly best = null;
            float bestDistance = float.MaxValue;
            foreach (var c in active)
            {
                if (!c.VisibleIn(cam.Camera))
                    continue;
                var b = c.Bounds;
                if (b.size == Vector3.zero || !ScreenRect(cam.Camera, b, out var rect))
                    continue;
                float distance = Vector3.Distance(cam.transform.position, b.center);
                if (distance > wallDistance + .3f || distance >= bestDistance)
                    continue;
                // A little margin: small things (a clock seen from across the room) are a few pixels wide.
                const float pad = .02f;
                if (viewport.x < rect.xMin - pad || viewport.x > rect.xMax + pad || viewport.y < rect.yMin - pad || viewport.y > rect.yMax + pad)
                    continue;
                best = c;
                bestDistance = distance;
            }

            return best;
        }

        static readonly Vector3[] corners = new Vector3[8];

        static bool ScreenRect(Camera cam, Bounds b, out Rect rect)
        {
            rect = default;
            Vector3 min = b.min, max = b.max;
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            bool any = false;
            foreach (var corner in corners)
            {
                var p = cam.WorldToViewportPoint(corner);
                if (p.z <= 0)
                    continue;
                any = true;
                x0 = Mathf.Min(x0, p.x);
                y0 = Mathf.Min(y0, p.y);
                x1 = Mathf.Max(x1, p.x);
                y1 = Mathf.Max(y1, p.y);
            }

            if (!any)
                return false;
            rect = Rect.MinMaxRect(x0, y0, x1, y1);
            return true;
        }
    }
}
