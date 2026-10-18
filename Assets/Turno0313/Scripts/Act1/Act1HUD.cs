using UnityEngine;
using UnityEngine.InputSystem;

namespace Turno0313
{
    // Temporary Act 1 HUD made with OnGUI, like DebugHUD: the sleep bar, a vignette that closes in as the
    // guard sinks deeper, and the alarm panel keypad. The final HUD (Jefferson/Elvis) can read SleepMeter
    // and AlarmPanel instead. Test keys (editor and development builds): F5 = +20 % sueño,
    // F6 = mostrar la barra, F7 = ver el código, F8 = reiniciar la noche con el fundido,
    // F9 = saltar al falso final.
    public sealed class Act1HUD : MonoBehaviour
    {
        [SerializeField] SleepMeter sleep;
        [SerializeField] AlarmPanel panel;
        [Tooltip("Oscuridad máxima de los bordes con la barra llena.")]
        [SerializeField] float maxVignette = .9f;

        static readonly Color Ink = new Color(.82f, .86f, .95f);
        static readonly Color Alarm = new Color(1f, .32f, .25f);
        static readonly Color Ok = new Color(.4f, 1f, .55f);

        Texture2D vignette, keyNormal, keyHover, keyPressed;
        GUIStyle caption, debug, title, display, status, key, hint;
        bool showCode;

        void Awake()
        {
            if (!sleep)
                sleep = GetComponent<SleepMeter>();
            vignette = MakeVignette();
            keyNormal = Solid(new Color(.16f, .17f, .19f));
            keyHover = Solid(new Color(.25f, .27f, .3f));
            keyPressed = Solid(new Color(.36f, .4f, .45f));
        }

        void Start()
        {
            if (!panel)
                panel = AlarmPanel.Instance;
        }

        void OnDestroy()
        {
            foreach (var t in new[] { vignette, keyNormal, keyHover, keyPressed })
                if (t)
                    Destroy(t);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !(Application.isEditor || Debug.isDebugBuild))
                return;
            if (sleep && keyboard.f5Key.wasPressedThisFrame)
            {
                sleep.Reveal();
                sleep.Add(.2f);
            }

            if (sleep && keyboard.f6Key.wasPressedThisFrame)
                sleep.Reveal();
            if (keyboard.f7Key.wasPressedThisFrame)
                showCode = !showCode;
            if (keyboard.f8Key.wasPressedThisFrame && NightCycle.Instance)
                NightCycle.Instance.Restart();
            // In the dream only; in reality F9 belongs to the final (Act2HUD).
            bool dream = !AnomalyManager.Instance || AnomalyManager.Instance.Act == GameAct.Dream;
            if (dream && keyboard.f9Key.wasPressedThisFrame && FalseEnding.Instance && !(NightCycle.Instance && NightCycle.Instance.Restarting))
            {
                if (AlarmPanel.Instance)
                    AlarmPanel.Instance.Close();
                FalseEnding.Instance.Play();
            }
        }

        void OnGUI()
        {
            // Drawn above DebugHUD.
            GUI.depth = -1;
            if (caption == null)
                Styles();
            bool sleepShown = sleep && sleep.Shown;
            bool panelOpen = panel && panel.IsOpen;
            bool dev = Application.isEditor || Debug.isDebugBuild;
            if (!sleepShown && !panelOpen && !(dev && showCode))
                return;

            float appear = sleepShown ? Mathf.Clamp01((Time.time - sleep.ShownAt) / 1.5f) : 0;
            // Vignette over the whole screen (before the scale matrix, so it always covers it).
            GUI.matrix = Matrix4x4.identity;
            if (sleepShown)
            {
                GUI.color = new Color(1, 1, 1, appear * sleep.Value * maxVignette);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill);
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s;
            if (sleepShown)
                DrawSleep(w, appear, dev);
            if (panelOpen)
                DrawKeypad(w);
            if (dev && showCode && WakeCode.Instance)
            {
                GUI.color = Color.white;
                Shadowed(new Rect(w - 324, 20, 300, 24), "[Prueba F7] Código: " + WakeCode.Instance.Code, debug);
            }

            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.identity;
        }

        void DrawSleep(float w, float appear, bool dev)
        {
            const float barW = 420, barH = 12, y = 46;
            float x = (w - barW) / 2, value = sleep.Value;

            // Dark backing so the bar reads over the bright fluorescent tubes.
            GUI.color = new Color(0, 0, 0, .45f * appear);
            GUI.DrawTexture(new Rect(x - 16, y - 34, barW + 32, dev ? 74 : 56), Texture2D.whiteTexture);
            GUI.color = new Color(0, 0, 0, .6f * appear);
            GUI.DrawTexture(new Rect(x - 3, y - 3, barW + 6, barH + 6), Texture2D.whiteTexture);
            // From a calm blue to violet; it pulses in the last quarter.
            var fill = Color.Lerp(new Color(.42f, .55f, .9f), new Color(.68f, .3f, .9f), value);
            if (value > .75f)
                fill = Color.Lerp(fill, Color.white, (Mathf.Sin(Time.time * 8f) + 1) * .15f);
            fill.a = appear;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(x, y, barW * value, barH), Texture2D.whiteTexture);

            GUI.color = new Color(1, 1, 1, appear);
            Shadowed(new Rect(x, y - 30, barW, 24), "SUEÑO", caption);
            if (dev)
                Shadowed(new Rect(x, y + barH + 4, barW, 20), (value * 100).ToString("0") + " %   +" + (sleep.Rate * 100).ToString("0.00") + " %/s   (F5 +20 %)", debug);
        }

        void DrawKeypad(float w)
        {
            const float pw = 380, ph = 560, keyW = 100, keyH = 64, gap = 12;
            float px = (w - pw) / 2, py = (1080 - ph) / 2;
            GUI.color = new Color(0, 0, 0, .85f);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);
            GUI.color = new Color(.55f, .56f, .58f, .8f);
            GUI.DrawTexture(new Rect(px, py, pw, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 12, pw, 28), "PANEL DE ALARMA", title);

            // Display: typed digits, ERROR or OK.
            var feedback = panel.Feedback;
            GUI.color = new Color(.02f, .05f, .07f, 1);
            GUI.DrawTexture(new Rect(px + 28, py + 50, pw - 56, 70), Texture2D.whiteTexture);
            string shown = feedback == PanelFeedback.Correct ? "OK" : feedback != PanelFeedback.None ? "ERROR" : panel.Masked();
            display.normal.textColor = feedback == PanelFeedback.Correct ? Ok : feedback != PanelFeedback.None ? Alarm : new Color(.55f, 1f, .7f);
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 28, py + 50, pw - 56, 70), shown, display);

            string line = feedback switch
            {
                PanelFeedback.Correct => "Código correcto.",
                PanelFeedback.FalseLead => "No. El reloj no es de fiar.",
                PanelFeedback.Wrong => "Código incorrecto.",
                _ => "Introduce el código de 4 dígitos."
            };
            status.normal.textColor = feedback == PanelFeedback.None ? Ink : feedback == PanelFeedback.Correct ? Ok : Alarm;
            GUI.Label(new Rect(px, py + 126, pw, 26), line, status);

            // Timer.
            float left = Mathf.Max(0, panel.TimeLeft), t = left / Mathf.Max(.01f, panel.TimeLimit);
            GUI.color = new Color(1, 1, 1, .12f);
            GUI.DrawTexture(new Rect(px + 28, py + 160, pw - 56, 8), Texture2D.whiteTexture);
            var timerColor = Color.Lerp(Alarm, new Color(.55f, .75f, 1f), t);
            if (left < 5 && feedback != PanelFeedback.Correct)
                timerColor = Color.Lerp(timerColor, Color.white, Mathf.PingPong(Time.time * 4, 1) * .5f);
            GUI.color = timerColor;
            GUI.DrawTexture(new Rect(px + 28, py + 160, (pw - 56) * t, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 28, py + 170, pw - 56, 22), Mathf.CeilToInt(left) + " s", hint);

            // Keys (mouse); the gamepad cursor is the highlighted frame.
            float kx = px + (pw - (3 * keyW + 2 * gap)) / 2, ky = py + 200;
            for (int i = 0; i < AlarmPanel.Keys.Length; i++)
            {
                var r = new Rect(kx + i % 3 * (keyW + gap), ky + i / 3 * (keyH + gap), keyW, keyH);
                if (i == panel.Selected && InputPrompts.UsingGamepad)
                {
                    GUI.color = new Color(.55f, 1f, .7f, .9f);
                    GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                char k = AlarmPanel.Keys[i];
                string text = k == '<' ? "Borrar" : k == 'x' ? "Salir" : k.ToString();
                key.fontSize = k == '<' || k == 'x' ? 18 : 30;
                if (GUI.Button(r, text, key))
                    panel.Press(k);
            }

            InputPrompts.Label(new Rect(px, py + ph - 52, pw, 44), InputPrompts.Pick("[0-9] escribir  ·  {Erase} borra  ·  {Back} sale", "{DPad} y {Confirm} pulsan  ·  {Erase} borra  ·  {Back} sale"), hint);
        }

        void Styles()
        {
            caption = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ink } };
            debug = new GUIStyle(caption) { fontSize = 12, normal = { textColor = new Color(.7f, .74f, .8f) } };
            title = new GUIStyle(caption) { fontSize = 18, fontStyle = FontStyle.Bold };
            display = new GUIStyle(caption) { fontSize = 46, fontStyle = FontStyle.Bold };
            status = new GUIStyle(caption) { fontSize = 17 };
            hint = new GUIStyle(caption) { fontSize = 13, normal = { textColor = new Color(.62f, .66f, .72f) } };
            key = new GUIStyle(GUI.skin.button)
            {
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = keyNormal, textColor = Ink },
                hover = { background = keyHover, textColor = Color.white },
                active = { background = keyPressed, textColor = Color.white },
                border = new RectOffset(0, 0, 0, 0)
            };
        }

        static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var color = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .85f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        static Texture2D MakeVignette()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .85f, d));
                pixels[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
