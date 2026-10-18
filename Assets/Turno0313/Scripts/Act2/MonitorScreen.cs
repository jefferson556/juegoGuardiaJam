using UnityEngine;

namespace Turno0313
{
    // One of the caseta's desk monitors. In reality it cycles through a few security camera feeds; in the dream
    // it only shows static, because the cameras are the Act 2 tool.
    public sealed class MonitorScreen : MonoBehaviour
    {
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        [SerializeField] Renderer screen;
        [Tooltip("Material sin luz (URP Unlit) que se copia para esta pantalla.")]
        [SerializeField] Material template;
        [SerializeField] TextMesh label;
        [SerializeField] SecurityCamera[] feeds;
        [SerializeField] float secondsPerFeed = 5f;

        Material material;

        void Awake()
        {
            material = new Material(template);
            screen.sharedMaterial = material;
        }

        void OnDestroy()
        {
            if (material)
                Destroy(material);
        }

        void Update()
        {
            var manager = AnomalyManager.Instance;
            bool working = manager && manager.Act == GameAct.Reality && feeds != null && feeds.Length > 0;
            if (!working)
            {
                material.SetTexture(BaseMap, StaticNoise.Texture);
                if (label)
                    label.text = "SIN SEÑAL";
                return;
            }

            var cam = feeds[(int)(Time.time / secondsPerFeed) % feeds.Length];
            if (!cam)
                return;
            material.SetTexture(BaseMap, cam.Feed);
            if (label)
                label.text = cam.Code;
        }
    }
}
