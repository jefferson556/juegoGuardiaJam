using System;
using UnityEngine;

namespace Turno0313
{
    // Barra de sueño (Acto 1). Hidden until the player notices the first anomalies; from then on it fills
    // with time and faster for every anomaly left unreported. Wrong reports push it up and correct ones
    // bring it down. When it is full the night restarts at 03:13 (Filled event).
    public sealed class SleepMeter : MonoBehaviour
    {
        [Header("Aparición")]
        [Tooltip("Anomalías notadas para que aparezca la barra.")]
        [SerializeField] int noticedToShow = 3;
        [Tooltip("Si sigue visible después de reiniciar la noche (el guardia ya sabe que sueña).")]
        [SerializeField] bool stayAfterReset = true;
        [Header("Subida")]
        [Tooltip("Segundos que tarda en llenarse sola, sin anomalías a la vista.")]
        [SerializeField] float secondsToFill = 300f;
        [Tooltip("Cada anomalía ya visible y sin reportar la llena en estos segundos (se suman).")]
        [SerializeField] float secondsToFillPerAnomaly = 180f;
        [Tooltip("Lo que sube con un reporte equivocado (0 a 1).")]
        [SerializeField] float wrongReportPenalty = .1f;
        [Header("Bajada")]
        [Tooltip("Lo que baja con un reporte correcto (0 a 1).")]
        [SerializeField] float correctReportRelief = .15f;

        public static SleepMeter Instance { get; private set; }
        // True once the bar has appeared (and only in the dream).
        public bool Shown => shown && (!manager || manager.Act == GameAct.Dream);
        public float Value { get; private set; }
        public bool Full => Value >= 1f;
        // Stops the bar during scripted moments (fade, false ending).
        public bool Paused { get; set; }
        // Current fill per second, for the HUD and for tuning.
        public float Rate { get; private set; }
        public float ShownAt { get; private set; }

        public event Action Appeared;
        public event Action Filled;

        AnomalyManager manager;
        bool shown, filled;

        void Awake() => Instance = this;

        void Start()
        {
            manager = AnomalyManager.Instance;
            if (!manager)
            {
                Debug.LogWarning("SleepMeter necesita un AnomalyManager en la escena.", this);
                return;
            }

            manager.Noticed += OnNoticed;
            manager.Reported += OnReported;
            manager.WrongReport += OnWrongReport;
            manager.NightReset += OnNightReset;
        }

        void OnDestroy()
        {
            if (manager)
            {
                manager.Noticed -= OnNoticed;
                manager.Reported -= OnReported;
                manager.WrongReport -= OnWrongReport;
                manager.NightReset -= OnNightReset;
            }

            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            Rate = 0;
            if (!Shown || Paused || filled)
                return;
            int missed = 0;
            foreach (var a in manager.Anomalies)
                if (a && a.IsManifested)
                    missed++;
            Rate = 1f / Mathf.Max(1f, secondsToFill) + missed / Mathf.Max(1f, secondsToFillPerAnomaly);
            Add(Rate * Time.deltaTime);
        }

        // Shows the bar now (also used by the test keys).
        public void Reveal()
        {
            if (shown)
                return;
            shown = true;
            ShownAt = Time.time;
            GameMessages.Show("Me quedé dormido... Tengo que despertar.", 4f);
            Appeared?.Invoke();
        }

        // Off the screen until something calls Reveal() again (the false ending).
        public void Hide()
        {
            shown = false;
            Value = 0;
            filled = false;
        }

        public void Add(float amount)
        {
            if (!Shown || filled)
                return;
            Value = Mathf.Clamp01(Value + amount);
            if (Value < 1f)
                return;
            filled = true;
            Filled?.Invoke();
        }

        void OnNoticed(Anomaly a)
        {
            if (manager.Act == GameAct.Dream && manager.NoticedCount >= noticedToShow)
                Reveal();
        }

        void OnReported(Anomaly a, ReportSource source)
        {
            if (!Paused)
                Add(-correctReportRelief);
        }

        void OnWrongReport(GameObject target, ReportSource source)
        {
            if (!Paused)
                Add(wrongReportPenalty);
        }

        void OnNightReset()
        {
            Value = 0;
            filled = false;
            if (!stayAfterReset)
                shown = false;
        }
    }
}
