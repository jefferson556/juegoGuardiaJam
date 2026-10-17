using UnityEngine;

namespace Turno0313
{
    // In-game time of the shift. Each night starts at the same hour and runs faster than real time;
    // it starts over with every night reset. The wall clock anomaly reads it.
    public sealed class NightClock : MonoBehaviour
    {
        [Tooltip("Hora a la que empieza cada noche (HH:MM).")]
        [SerializeField] string startTime = "03:13";
        [Tooltip("Segundos de juego por cada segundo real (10 = un minuto cada 6 s).")]
        [SerializeField] float speed = 10f;

        public static NightClock Instance { get; private set; }
        public float SecondsOfDay => startSeconds + (Time.time - startedAt) * speed;
        public string Display => Format(SecondsOfDay);

        float startSeconds, startedAt;
        AnomalyManager manager;

        void Awake()
        {
            Instance = this;
            startSeconds = Parse(startTime);
            Restart();
        }

        // Start runs after every Awake, so the AnomalyManager already exists.
        void Start()
        {
            manager = AnomalyManager.Instance;
            if (manager)
                manager.NightReset += Restart;
        }

        void OnDestroy()
        {
            if (manager)
                manager.NightReset -= Restart;
            if (Instance == this)
                Instance = null;
        }

        public void Restart() => startedAt = Time.time;

        public static string Format(float secondsOfDay)
        {
            int total = Mathf.FloorToInt(secondsOfDay) % 86400;
            return (total / 3600).ToString("00") + ":" + (total / 60 % 60).ToString("00");
        }

        static float Parse(string time)
        {
            var parts = time.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
                return h * 3600 + m * 60;
            Debug.LogWarning("Hora de inicio no válida: " + time + " (se usa 03:13).");
            return 3 * 3600 + 13 * 60;
        }
    }
}
