using UnityEngine;

namespace Turno0313
{
    // A door that slides sideways into the wall (the office door of the Piso 13 floor). Put the collider on the
    // same object so it moves with the leaf.
    public sealed class SlidingDoor : Interactable
    {
        [Tooltip("Cuánto se desplaza al abrirse, en metros y en el espacio del padre.")]
        [SerializeField] Vector3 openOffset = new Vector3(1.85f, 0, 0);
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

        Vector3 closed;
        float amount;
        bool ready;

        void Awake()
        {
            closed = transform.localPosition;
            ready = true;
        }

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

        // Closed at once and without sound (night reset).
        public void ResetClosed()
        {
            if (!ready)
                return;
            IsOpen = false;
            amount = 0;
            transform.localPosition = closed;
        }

        void Update()
        {
            float target = IsOpen ? 1 : 0;
            if (Mathf.Approximately(amount, target))
                return;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(.05f, openSeconds));
            transform.localPosition = closed + openOffset * Mathf.SmoothStep(0, 1, amount);
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
