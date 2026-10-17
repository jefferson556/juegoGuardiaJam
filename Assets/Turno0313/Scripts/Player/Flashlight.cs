using UnityEngine;

namespace Turno0313
{
    // F: flashlight on/off.
    public sealed class Flashlight : MonoBehaviour
    {
        [SerializeField] Light spot;
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip toggleClip;
        [SerializeField] bool startOn = true;

        public bool On { get; private set; }
        PlayerController player;

        void Awake()
        {
            player = GetComponentInParent<PlayerController>();
            Set(startOn);
        }

        void Update()
        {
            if (player && player.HasControl && GameInput.Flashlight.WasPressedThisFrame())
            {
                Set(!On);
                if (source && toggleClip)
                    source.PlayOneShot(toggleClip);
            }
        }

        public void Set(bool on)
        {
            On = on;
            if (spot)
                spot.enabled = on;
        }
    }
}
