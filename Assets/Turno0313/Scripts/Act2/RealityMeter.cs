using System;
using UnityEngine;

namespace Turno0313
{
    // Barra de realidad (Acto 2). Starts full when Act 2 begins and drains with time and every time the
    // escaped anomaly acts. A correct report fills it up, a wrong one drains it. Empty = back to the dream
    // (Emptied, handled by Act2Director).
    public sealed class RealityMeter : MonoBehaviour
    {
        [Tooltip("Segundos que tarda en vaciarse sola.")]
        [SerializeField] float secondsToEmpty = 240f;
        [Tooltip("Lo que sube con un reporte correcto (0 a 1).")]
        [SerializeField] float correctReportBonus = .1f;
        [Tooltip("Lo que baja con un reporte equivocado (0 a 1).")]
        [SerializeField] float wrongReportPenalty = .1f;

        public static RealityMeter Instance { get; private set; }
        // On screen: Act 2 has begun and the act is Reality.
        public bool Shown => shown && (!manager || manager.Act == GameAct.Reality);
        public float Value { get; private set; } = 1f;
        // Stops it during scripted moments (the return to the dream, the final).
        public bool Paused { get; set; }
        // Current change per second (negative), for the HUD and for tuning.
        public float Rate { get; private set; }
        public float ShownAt { get; private set; }
        public event Action Emptied;

        AnomalyManager manager;
        bool shown, emptied;

        void Awake() => Instance = this;

        void Start()
        {
            manager = AnomalyManager.Instance;
            if (!manager)
                return;
            manager.Reported += OnReported;
            manager.WrongReport += OnWrongReport;
        }

        void OnDestroy()
        {
            if (manager)
            {
                manager.Reported -= OnReported;
                manager.WrongReport -= OnWrongReport;
            }

            if (Instance == this)
                Instance = null;
        }

        public void Begin()
        {
            shown = true;
            emptied = false;
            Paused = false;
            Value = 1f;
            ShownAt = Time.time;
        }

        public void Hide()
        {
            shown = false;
            Paused = false;
        }

        public void Add(float amount)
        {
            if (!Shown || emptied)
                return;
            Value = Mathf.Clamp01(Value + amount);
            if (Value > 0)
                return;
            emptied = true;
            Emptied?.Invoke();
        }

        void Update()
        {
            Rate = 0;
            if (!Shown || Paused || emptied)
                return;
            Rate = -1f / Mathf.Max(1f, secondsToEmpty);
            Add(Rate * Time.deltaTime);
        }

        void OnReported(Anomaly a, ReportSource source)
        {
            if (!Paused)
                Add(correctReportBonus);
        }

        void OnWrongReport(GameObject target, ReportSource source)
        {
            if (!Paused)
                Add(-wrongReportPenalty);
        }
    }
}
