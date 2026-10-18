using System.Collections.Generic;
using UnityEngine;

namespace Turno0313
{
    // The guard's logbook (Act 1). When the player correctly reports an anomaly that carries a digit of the
    // alarm code, the digit is written down in its position, so nobody has to remember numbers or their order.
    // Tab / Select opens and closes it; next to the alarm keypad it shows by itself. The code lasts the whole
    // session, so the notes still count after a night reset; if the code changes (back from Act 2), they are
    // cleared. WakeCode adds this component.
    public sealed class Logbook : MonoBehaviour
    {
        static readonly Color Ink = new Color(.82f, .86f, .95f);
        static readonly Color Digit = new Color(.55f, 1f, .7f);

        public static Logbook Instance { get; private set; }
        public bool Open { get; private set; }
        // '\0' where the digit is still unknown.
        public IReadOnlyList<char> Digits => digits;

        readonly char[] digits = new char[AlarmPanel.Length];
        // (position, line), shown in code order.
        readonly List<(int position, string text)> entries = new List<(int, string)>();
        string codeNoted;
        AnomalyManager manager;
        GUIStyle title, code, line, hint;

        void Awake() => Instance = this;

        void Start()
        {
            manager = AnomalyManager.Instance;
            if (manager)
                manager.Reported += OnReported;
        }

        void OnDestroy()
        {
            if (manager)
                manager.Reported -= OnReported;
            if (Instance == this)
                Instance = null;
        }

        bool InDream => !manager || manager.Act == GameAct.Dream;

        void OnReported(Anomaly anomaly, ReportSource source)
        {
            if (!InDream || !anomaly)
                return;
            var carrier = anomaly.GetComponentInChildren<CodeDigit>(true);
            if (!carrier || carrier.Position < 0)
                return;
            Sync();
            if (digits[carrier.Position] == carrier.Digit)
                return;
            digits[carrier.Position] = carrier.Digit;
            // One note per position (at most four per code).
            entries.Add((carrier.Position, Ordinal(carrier.Position) + " dígito = " + carrier.Digit + "   (" + anomaly.DisplayName + ")"));
            entries.Sort((a, b) => a.position.CompareTo(b.position));
            GameMessages.Show("Anotado en la bitácora: " + Ordinal(carrier.Position) + " dígito = " + carrier.Digit + "   ({Notes} para verla)", 4f);
        }

        // A new code (back from Act 2) makes the old notes useless.
        void Sync()
        {
            var current = WakeCode.Instance ? WakeCode.Instance.Code : null;
            if (current == codeNoted)
                return;
            codeNoted = current;
            for (int i = 0; i < digits.Length; i++)
                digits[i] = '\0';
            entries.Clear();
        }

        public void SetOpen(bool open) => Open = open;

        static string Ordinal(int position) => (position + 1) + "º";

        // "_ 7 _ 4"
        public string CodeLine()
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0)
                    text.Append("  ");
                text.Append(digits[i] == '\0' ? '_' : digits[i]);
            }

            return text.ToString();
        }

        bool MenuOpen => GameMenus.Instance && (GameMenus.Instance.OnTitle || GameMenus.Instance.Paused);

        void Update()
        {
            Sync();
            if (!InDream || MenuOpen)
            {
                Open = false;
                return;
            }

            if (GameInput.Notes.WasPressedThisFrame())
                Open = !Open;
        }

        void OnGUI()
        {
            if (!InDream || MenuOpen)
                return;
            var panel = AlarmPanel.Instance;
            bool besideKeypad = panel && panel.IsOpen;
            if (!Open && !besideKeypad)
                return;
            // Above Act1HUD (-1), which draws the keypad.
            GUI.depth = -2;
            Styles();
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s;
            const float pw = 420;
            float ph = 142 + NotesHeight(pw) + 44;
            // Next to the keypad (380 wide, centred) or on the right edge.
            var r = besideKeypad ? new Rect((w - 380) / 2 - pw - 24, (1080 - 560) / 2, pw, ph) : new Rect(w - pw - 40, 150, pw, ph);
            Draw(r, besideKeypad);
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        void Draw(Rect r, bool besideKeypad)
        {
            GUI.color = new Color(0, 0, 0, .85f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(.55f, .56f, .58f, .8f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y + 12, r.width, 28), "BITÁCORA", title);
            GUI.Label(new Rect(r.x, r.y + 44, r.width, 22), "Código del panel de alarma", hint);
            GUI.Label(new Rect(r.x, r.y + 66, r.width, 64), CodeLine(), code);

            float y = r.y + 142;
            foreach (var note in Notes())
            {
                // Long anomaly names wrap onto a second line.
                float h = line.CalcHeight(new GUIContent(note), r.width - 48);
                GUI.Label(new Rect(r.x + 24, y, r.width - 48, h), note, line);
                y += h + 4;
            }

            InputPrompts.Label(new Rect(r.x, r.yMax - 30, r.width, 22), besideKeypad ? "Lo que has anotado esta partida" : "{Notes} para cerrar", hint);
        }

        const string Empty = "Todavía no hay nada anotado.\nAl reportar una anomalía con un número, el guardia lo apunta aquí.";

        IEnumerable<string> Notes()
        {
            if (entries.Count == 0)
                yield return Empty;
            foreach (var entry in entries)
                yield return entry.text;
        }

        float NotesHeight(float panelWidth)
        {
            float total = 0;
            foreach (var note in Notes())
                total += line.CalcHeight(new GUIContent(note), panelWidth - 48) + 4;
            return total;
        }

        void Styles()
        {
            if (title != null)
                return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ink } };
            code = new GUIStyle(title) { fontSize = 46, normal = { textColor = Digit } };
            line = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, normal = { textColor = Ink } };
            hint = new GUIStyle(title) { fontSize = 13, fontStyle = FontStyle.Normal, normal = { textColor = new Color(.62f, .66f, .72f) } };
        }
    }
}
