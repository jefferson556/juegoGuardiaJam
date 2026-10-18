using System;
using UnityEngine;

namespace Turno0313
{
    // La anomalía escapada (Acto 2): the 03:13 clock from the dream, now loose in reality. It only shows up on
    // one security camera at a time and every so often jumps to another one; each jump on its own drains the
    // reality bar (it "acts") and makes static on the monitors. Report it on the right monitor 3 times in a row
    // to catch it (Caught → FinalSequence). After each hit it flees to another camera; a wrong report, or letting
    // it jump away on its own, loses the trail.
    public sealed class EscapedAnomaly : MonoBehaviour, ICameraTarget
    {
        [Tooltip("El reloj: solo se ve por la cámara donde está.")]
        [SerializeField] CameraOnly visual;
        [Tooltip("Una cámara por sitio, en el mismo orden que Spots.")]
        [SerializeField] SecurityCamera[] cameras;
        [Tooltip("Dónde cuelga el reloj en cada cámara (en la pared que esa cámara ve).")]
        [SerializeField] Transform[] spots;
        [Header("Ritmo")]
        [Tooltip("Segundos desde que empieza el Acto 2 hasta que aparece.")]
        [SerializeField] float firstDelay = 8f;
        [Tooltip("Cada cuántos segundos salta solo a otra cámara.")]
        [SerializeField] float jumpEvery = 20f;
        [SerializeField] int reportsToCatch = 3;
        [Header("Barra de realidad")]
        [Tooltip("Lo que baja cada vez que salta solo (0 a 1).")]
        [SerializeField] float jumpPenalty = .08f;
        [Tooltip("Lo que sube con cada reporte correcto (0 a 1).")]
        [SerializeField] float reportBonus = .1f;
        [Header("Audio")]
        [SerializeField] AudioSource source;
        [Tooltip("Al saltar (vacío = estática sintetizada).")]
        [SerializeField] AudioClip jumpClip;

        public static EscapedAnomaly Instance { get; private set; }
        // On some camera right now.
        public bool Loose => running && current >= 0;
        public int Streak { get; private set; }
        public int ReportsToCatch => reportsToCatch;
        public SecurityCamera Current => Loose ? cameras[current] : null;
        public event Action<SecurityCamera> Jumped;
        public event Action Caught;

        bool running;
        int current = -1;
        float nextJump;
        AnomalyManager manager;
        Act2Director director;

        void Awake()
        {
            Instance = this;
            if (visual)
                visual.gameObject.SetActive(false);
        }

        void Start()
        {
            manager = AnomalyManager.Instance;
            director = Act2Director.Instance;
            if (director)
                director.Started += Begin;
            if (manager)
                manager.WrongReport += OnWrongReport;
        }

        void OnDestroy()
        {
            if (director)
                director.Started -= Begin;
            if (manager)
                manager.WrongReport -= OnWrongReport;
            if (Instance == this)
                Instance = null;
        }

        public void Begin()
        {
            running = true;
            Streak = 0;
            current = -1;
            Hide();
            nextJump = Time.time + firstDelay;
        }

        void Update()
        {
            if (!running)
                return;
            // Act 2 is over (back to the dream): gone until it begins again.
            if (!director || !director.Running)
            {
                Stop();
                return;
            }

            if (Time.time >= nextJump)
                Jump(true);
            var monitors = SecurityMonitors.Instance;
            if (monitors && Loose)
                monitors.Notice = "Rastro: " + Trail();
        }

        public ReportResult ReportFromCamera(SecurityCamera cam, out string message)
        {
            message = null;
            if (!Loose || cameras[current] != cam)
                return ReportResult.Nothing;
            Streak++;
            var reality = RealityMeter.Instance;
            if (reality)
                reality.Add(reportBonus);
            if (Streak >= reportsToCatch)
            {
                message = "Lo atrapaste.";
                Stop();
                Caught?.Invoke();
                return ReportResult.Correct;
            }

            message = "¡Lo tienes! Se escapó a otra cámara.";
            Jump(false);
            return ReportResult.Correct;
        }

        // byItself = it moved on its own: it drains reality and the trail is lost.
        void Jump(bool byItself)
        {
            int from = current;
            int to = current;
            if (cameras.Length > 1)
                while (to == current)
                    to = UnityEngine.Random.Range(0, cameras.Length);
            else
                to = 0;

            var monitors = SecurityMonitors.Instance;
            if (from >= 0)
            {
                if (byItself)
                {
                    Streak = 0;
                    if (RealityMeter.Instance)
                        RealityMeter.Instance.Add(-jumpPenalty);
                }

                if (monitors)
                    monitors.Flash(cameras[from]);
            }

            current = to;
            visual.transform.SetPositionAndRotation(spots[to].position, spots[to].rotation);
            visual.ShowIn(cameras[to]);
            visual.gameObject.SetActive(true);
            if (monitors)
                monitors.Flash(cameras[to]);
            if (source)
                source.PlayOneShot(jumpClip ? jumpClip : (jumpClip = SynthAudio.Static(.45f, .5f)));
            nextJump = Time.time + jumpEvery;
            Jumped?.Invoke(cameras[to]);
        }

        void Stop()
        {
            running = false;
            Streak = 0;
            current = -1;
            Hide();
            if (SecurityMonitors.Instance)
                SecurityMonitors.Instance.Notice = null;
        }

        void Hide()
        {
            if (visual)
                visual.gameObject.SetActive(false);
        }

        void OnWrongReport(GameObject target, ReportSource source)
        {
            if (Loose)
                Streak = 0;
        }

        string Trail()
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < reportsToCatch; i++)
                text.Append(i < Streak ? "● " : "○ ");
            return text.ToString().TrimEnd();
        }
    }
}
