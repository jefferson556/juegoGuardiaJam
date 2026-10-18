using UnityEngine;

namespace Turno0313
{
    // Ambient sound of the floor: the fluorescent hum. One theme for both acts, filtered, as the GDD allows
    // ("puede ser el mismo con filtro"): muffled and a little low in the dream, clean in reality.
    // Keep it on its own GameObject: the low-pass filter affects every AudioSource on the same object.
    public sealed class AmbientAudio : MonoBehaviour
    {
        [Tooltip("Vacío = zumbido sintetizado.")]
        [SerializeField] AudioClip hum;
        [SerializeField] float volume = .1f;
        [Header("Sueño")]
        [SerializeField] float dreamCutoff = 900f;
        [SerializeField] float dreamPitch = .94f;

        AudioSource source;
        AudioLowPassFilter filter;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = hum ? hum : SynthAudio.Hum();
            source.loop = true;
            source.spatialBlend = 0;
            source.volume = volume;
            filter = gameObject.AddComponent<AudioLowPassFilter>();
        }

        void Start() => source.Play();

        void Update()
        {
            var manager = AnomalyManager.Instance;
            bool dream = !manager || manager.Act == GameAct.Dream;
            float dt = Time.unscaledDeltaTime;
            filter.cutoffFrequency = Mathf.MoveTowards(filter.cutoffFrequency, dream ? dreamCutoff : 22000f, dt * 12000f);
            source.pitch = Mathf.MoveTowards(source.pitch, dream ? dreamPitch : 1f, dt * .3f);
        }
    }
}
