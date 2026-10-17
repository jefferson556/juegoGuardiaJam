using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turno0313
{
    public enum ReportSource
    {
        Player,
        Camera
    }

    public enum ReportResult
    {
        Correct,
        Wrong,
        Nothing,
        Cooldown
    }

    // Activates anomalies over time and judges reports. Other systems hook into its events:
    // the sleep bar (Act 1) and the reality bar (Act 2) react to Reported / WrongReport / Activated.
    [DefaultExecutionOrder(-100)]
    public sealed class AnomalyManager : MonoBehaviour
    {
        [Header("Acto")]
        [SerializeField] GameAct act = GameAct.Dream;
        [Header("Aparición automática")]
        [SerializeField] bool autoActivate = true;
        [SerializeField] float firstDelay = 20f;
        [SerializeField] float interval = 20f;
        [SerializeField] int maxActive = 3;
        [Header("Reporte y detección")]
        [SerializeField] float reportCooldown = .8f;
        [Tooltip("Segundos mirando una anomalía activa para contarla como notada.")]
        [SerializeField] float noticeSeconds = .6f;
        [SerializeField] float viewDistance = 40f;
        [Tooltip("Cámara del jugador. Vacío = Camera.main.")]
        [SerializeField] Camera viewer;

        public static AnomalyManager Instance { get; private set; }
        public GameAct Act => act;
        public IReadOnlyList<Anomaly> Anomalies => anomalies;
        public int CorrectReports { get; private set; }
        public int WrongReports { get; private set; }
        public int NoticedCount { get; private set; }
        public bool AutoActivate { get => autoActivate; set => autoActivate = value; }
        // Seconds until the next automatic activation (-1 when automatic activation is off).
        public float NextActivationIn => autoActivate ? Mathf.Max(0, nextActivation - Time.time) : -1;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var a in anomalies)
                    if (a && a.IsActive)
                        count++;
                return count;
            }
        }

        public event Action<Anomaly> Activated;
        public event Action<Anomaly> Noticed;
        public event Action<Anomaly, ReportSource> Reported;
        public event Action<GameObject, ReportSource> WrongReport;

        readonly List<Anomaly> anomalies = new List<Anomaly>();
        readonly List<Anomaly> candidates = new List<Anomaly>();
        readonly Dictionary<Anomaly, float> watched = new Dictionary<Anomaly, float>();
        readonly Plane[] frustum = new Plane[6];
        float nextActivation, lastReport = -99f;

        void Awake()
        {
            if (Instance && Instance != this)
                Debug.LogWarning("Hay más de un AnomalyManager en la escena; se usa el último.", this);
            Instance = this;
            anomalies.AddRange(FindObjectsByType<Anomaly>(FindObjectsInactive.Include));
        }

        // Start runs after every anomaly's Awake, so they can capture their initial state first.
        void Start() => ResetNight();

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = Viewer;
            if (cam)
                GeometryUtility.CalculateFrustumPlanes(cam, frustum);
            for (int i = 0; i < anomalies.Count; i++)
            {
                var a = anomalies[i];
                if (!a)
                    continue;
                bool visible = cam && ViewUtil.CanSee(frustum, cam.transform.position, a.ViewBounds, a.transform, viewDistance);
                a.Tick(visible, dt);
                if (!visible || !a.IsManifested || a.Noticed)
                    continue;
                watched.TryGetValue(a, out float seen);
                seen += dt;
                watched[a] = seen;
                if (seen >= noticeSeconds)
                {
                    a.Noticed = true;
                    NoticedCount++;
                    Noticed?.Invoke(a);
                }
            }

            if (autoActivate && Time.time >= nextActivation)
                nextActivation = Time.time + (ActivateRandom() ? interval : 2f);
        }

        Camera Viewer => viewer ? viewer : Camera.main;

        // For anomalies spawned at runtime (for example the one that escapes into Act 2).
        public void Register(Anomaly anomaly)
        {
            if (!anomaly || anomalies.Contains(anomaly))
                return;
            anomalies.Add(anomaly);
            anomaly.ResetToNormal();
        }

        public void Unregister(Anomaly anomaly)
        {
            anomalies.Remove(anomaly);
            watched.Remove(anomaly);
        }

        // Activates a random anomaly of the current act that the player can't see right now.
        public bool ActivateRandom()
        {
            if (ActiveCount >= maxActive)
                return false;
            var cam = Viewer;
            if (cam)
                GeometryUtility.CalculateFrustumPlanes(cam, frustum);
            candidates.Clear();
            foreach (var a in anomalies)
            {
                if (!a || a.State != AnomalyState.Normal || !a.AvailableIn(act))
                    continue;
                if (cam && ViewUtil.CanSee(frustum, cam.transform.position, a.ViewBounds, a.transform, viewDistance))
                    continue;
                candidates.Add(a);
            }

            if (candidates.Count == 0)
                return false;
            Activate(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            return true;
        }

        public void Activate(Anomaly anomaly)
        {
            if (!anomaly || anomaly.IsActive)
                return;
            anomaly.Activate();
            watched.Remove(anomaly);
            Activated?.Invoke(anomaly);
        }

        // True for every prop the player may point at, anomalous or not.
        public bool IsReportable(Collider target)
        {
            if (!target)
                return false;
            var anomaly = target.GetComponentInParent<Anomaly>();
            if (anomaly)
                return anomaly.IsActive || anomaly.ReportableWhenNormal;
            return target.GetComponentInParent<Reportable>() != null;
        }

        public ReportResult Report(Collider target, ReportSource source = ReportSource.Player)
        {
            if (!IsReportable(target))
                return ReportResult.Nothing;
            if (Time.time - lastReport < reportCooldown)
                return ReportResult.Cooldown;
            lastReport = Time.time;
            var anomaly = target.GetComponentInParent<Anomaly>();
            if (anomaly && anomaly.IsManifested)
            {
                anomaly.Resolve();
                CorrectReports++;
                Reported?.Invoke(anomaly, source);
                return ReportResult.Correct;
            }

            WrongReports++;
            WrongReport?.Invoke(target.gameObject, source);
            return ReportResult.Wrong;
        }

        // For the camera monitors (Act 2): report what a security camera shows at a viewport point.
        public ReportResult ReportFromCamera(Camera cam, Vector2 viewportPoint, float range = 60f)
        {
            var ray = cam.ViewportPointToRay(viewportPoint);
            return Physics.Raycast(ray, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? Report(hit.collider, ReportSource.Camera)
                : ReportResult.Nothing;
        }

        // Back to a normal floor: for the night reset at 03:13 and for changing acts.
        public void ResetNight()
        {
            foreach (var a in anomalies)
                if (a)
                    a.ResetToNormal();
            watched.Clear();
            CorrectReports = WrongReports = NoticedCount = 0;
            nextActivation = Time.time + firstDelay;
        }

        public void SetAct(GameAct newAct)
        {
            act = newAct;
            ResetNight();
        }
    }
}
