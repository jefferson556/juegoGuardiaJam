using UnityEngine;

namespace Turno0313
{
    // The 4-digit code of the alarm panel. It is the same for the whole session (what the player learned
    // still counts after a night reset), but every night the digits move to different anomalies.
    public sealed class WakeCode : MonoBehaviour
    {
        [Tooltip("Vacío = un código al azar en cada partida. Útil para pruebas: 4 dígitos fijos.")]
        [SerializeField] string fixedCode = "";
        [Tooltip("La pista falsa: lo que marca el reloj parado. Nunca sale como código.")]
        [SerializeField] string falseLead = "0313";
        [Tooltip("Anomalías que esconden un dígito (vacío = todas las de la escena).")]
        [SerializeField] CodeDigit[] carriers;

        public static WakeCode Instance { get; private set; }
        public string Code { get; private set; }
        public string FalseLead => falseLead;

        AnomalyManager manager;

        void Awake()
        {
            Instance = this;
            Code = IsValid(fixedCode) ? fixedCode : Generate();
            if (carriers == null || carriers.Length == 0)
                carriers = FindObjectsByType<CodeDigit>(FindObjectsInactive.Include);
            if (carriers.Length != AlarmPanel.Length)
                Debug.LogWarning("Hay " + carriers.Length + " anomalías con dígito y el código tiene " + AlarmPanel.Length + ".", this);
            Shuffle();
        }

        void Start()
        {
            manager = AnomalyManager.Instance;
            if (manager)
                manager.NightReset += Shuffle;
        }

        void OnDestroy()
        {
            if (manager)
                manager.NightReset -= Shuffle;
            if (Instance == this)
                Instance = null;
        }

        // A different code (after falling back from Act 2), already spread over the anomalies.
        public void NewCode()
        {
            if (!IsValid(fixedCode))
            {
                var old = Code;
                for (int i = 0; i < 20 && Code == old; i++)
                    Code = Generate();
            }

            Shuffle();
        }

        // Gives every carrier one position of the code, in a new random order.
        public void Shuffle()
        {
            var positions = new int[carriers.Length];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = i % AlarmPanel.Length;
            for (int i = positions.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }

            for (int i = 0; i < carriers.Length; i++)
                if (carriers[i])
                    carriers[i].Assign(positions[i], Code[positions[i]]);
        }

        string Generate()
        {
            for (int tries = 0; tries < 100; tries++)
            {
                var digits = new char[AlarmPanel.Length];
                for (int i = 0; i < digits.Length; i++)
                    digits[i] = (char)('0' + Random.Range(0, 10));
                var code = new string(digits);
                if (code != falseLead && code.Replace(code[0].ToString(), "").Length > 0)
                    return code;
            }

            return "4721";
        }

        static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != AlarmPanel.Length)
                return false;
            foreach (char c in code)
                if (c < '0' || c > '9')
                    return false;
            return true;
        }
    }
}
