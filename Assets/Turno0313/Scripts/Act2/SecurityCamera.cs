using System.Collections.Generic;
using UnityEngine;

namespace Turno0313
{
    // One security camera of the floor (Act 2). It renders into a small texture that the caseta's desk monitors
    // and the camera screen show. Its number and name are what the screen prints: "CAM 03 · OFICINAS ESTE".
    // Leave the Camera component disabled in the scene: it turns itself on once it has its texture, so in the
    // editor it never takes over the Game view.
    [RequireComponent(typeof(Camera))]
    public sealed class SecurityCamera : MonoBehaviour
    {
        [SerializeField] int number = 1;
        [SerializeField] string label = "PASILLO";
        [SerializeField] Vector2Int resolution = new Vector2Int(480, 270);

        // Every camera in the scene, by number.
        public static IReadOnlyList<SecurityCamera> All => all;
        public int Number => number;
        public string Label => label;
        public string Code => "CAM " + number.ToString("00");
        public string Title => Code + " · " + label;
        public Camera Camera { get; private set; }
        public RenderTexture Feed { get; private set; }

        static readonly List<SecurityCamera> all = new List<SecurityCamera>();

        void Awake()
        {
            Camera = GetComponent<Camera>();
            Feed = new RenderTexture(resolution.x, resolution.y, 16) { name = Title };
            Camera.targetTexture = Feed;
            // The player's hand and flashlight are for his own eyes only (the light itself still shows).
            int playerOnly = LayerMask.NameToLayer(CameraOnly.PlayerOnlyLayer);
            if (playerOnly >= 0)
                Camera.cullingMask &= ~(1 << playerOnly);
            Camera.enabled = true;
            // Now that it is on, give it (and the player's camera) the right camera-only layers.
            CameraOnly.Refresh();
        }

        void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
            all.Sort((a, b) => a.number.CompareTo(b.number));
        }

        void OnDisable() => all.Remove(this);

        void OnDestroy()
        {
            if (!Feed)
                return;
            if (Camera)
                Camera.targetTexture = null;
            Feed.Release();
            Destroy(Feed);
        }
    }
}
