using UnityEngine;

namespace Turno0313
{
    // Look of each act (GDD section 6): the dream has colder, dimmer light and a light fog at the end of the
    // corridors; reality is neutral and clean. A first pass for the greybox: Ivo tunes these values in the
    // Inspector, or replaces this with his own lighting and Volumes.
    public sealed class ActLook : MonoBehaviour
    {
        [SerializeField] Light[] lights;
        [Header("Sueño")]
        [SerializeField] Color dreamLight = new Color(.74f, .85f, 1f);
        [SerializeField] float dreamIntensity = .32f;
        [SerializeField] Color dreamAmbient = new Color(.025f, .032f, .045f);
        [SerializeField] Color dreamFog = new Color(.03f, .04f, .07f);
        [SerializeField] float dreamFogDensity = .045f;
        [Header("Realidad")]
        [SerializeField] Color realityLight = new Color(1f, .96f, .9f);
        [SerializeField] float realityIntensity = .42f;
        [SerializeField] Color realityAmbient = new Color(.035f, .038f, .042f);

        int applied = -1;

        void Update()
        {
            var manager = AnomalyManager.Instance;
            int act = manager ? (int)manager.Act : (int)GameAct.Dream;
            if (act != applied)
                Apply((GameAct)act);
        }

        void Apply(GameAct act)
        {
            applied = (int)act;
            bool dream = act == GameAct.Dream;
            foreach (var l in lights)
            {
                if (!l)
                    continue;
                l.color = dream ? dreamLight : realityLight;
                l.intensity = dream ? dreamIntensity : realityIntensity;
            }

            RenderSettings.reflectionIntensity = .2f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = dream ? dreamAmbient : realityAmbient;
            RenderSettings.fog = dream;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = dreamFog;
            RenderSettings.fogDensity = dreamFogDensity;
        }
    }
}
