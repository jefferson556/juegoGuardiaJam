using UnityEngine;

namespace Turno0313
{
    // Swinging door. Put it on the hinge pivot; the door collider must be a child so it swings too.
    public sealed class Door : Interactable
    {
        [SerializeField] float openAngle = -95f;
        [SerializeField] float openSeconds = .9f;
        [SerializeField] bool locked;
        [SerializeField] string lockedMessage = "Está cerrada.";
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip openClip;
        [SerializeField] AudioClip closeClip;
        [SerializeField] AudioClip latchClip;

        public bool Locked { get => locked; set => locked = value; }
        public bool IsOpen { get; private set; }
        public override string Prompt => locked ? "Probar puerta" : IsOpen ? "Cerrar puerta" : "Abrir puerta";

        Quaternion closed;
        float amount;

        void Awake() => closed = transform.localRotation;

        public override void Interact(PlayerController player)
        {
            if (locked)
            {
                Play(latchClip);
                GameMessages.Show(lockedMessage);
                return;
            }

            IsOpen = !IsOpen;
            Play(IsOpen ? openClip : closeClip);
        }

        void Update()
        {
            float target = IsOpen ? 1 : 0;
            if (Mathf.Approximately(amount, target))
                return;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(.05f, openSeconds));
            transform.localRotation = closed * Quaternion.Euler(0, openAngle * Mathf.SmoothStep(0, 1, amount), 0);
            if (!IsOpen && amount <= 0)
                Play(latchClip);
        }

        void Play(AudioClip clip)
        {
            if (source && clip)
                source.PlayOneShot(clip);
        }
    }
}
