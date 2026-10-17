using UnityEngine;

namespace Turno0313
{
    // R / right click: report whatever the player is pointing at to the AnomalyManager.
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerReporter : MonoBehaviour
    {
        [SerializeField] float range = 14f;
        [SerializeField] AudioSource feedback;
        [SerializeField] AudioClip correctClip;
        [SerializeField] AudioClip wrongClip;

        // True when the crosshair is on something that can be reported (every prop, normal or not).
        public bool Aiming { get; private set; }
        PlayerController player;

        void Awake() => player = GetComponent<PlayerController>();

        void Update()
        {
            var manager = AnomalyManager.Instance;
            Collider target = null;
            var eye = player.View.transform;
            if (Physics.Raycast(eye.position, eye.forward, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                target = hit.collider;
            Aiming = manager && target && manager.IsReportable(target);

            if (!manager || !player.HasControl || !GameInput.Report.WasPressedThisFrame())
                return;
            switch (manager.Report(target, ReportSource.Player))
            {
                case ReportResult.Correct:
                    Play(correctClip);
                    GameMessages.Show("Anomalía reportada.");
                    break;
                case ReportResult.Wrong:
                    Play(wrongClip);
                    GameMessages.Show("No había nada raro ahí.");
                    break;
                case ReportResult.Nothing:
                    GameMessages.Show("Apunta a algo para reportarlo.", 1.5f);
                    break;
            }
        }

        void Play(AudioClip clip)
        {
            if (feedback && clip)
                feedback.PlayOneShot(clip);
        }
    }
}
