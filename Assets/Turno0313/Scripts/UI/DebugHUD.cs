using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turno0313
{
    // Temporary HUD for the greybox: crosshair, E/R prompts, messages and a test panel (F1).
    // The final HUD (Jefferson/Elvis) can replace it by listening to the same events.
    public sealed class DebugHUD : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] PlayerReporter reporter;
        [SerializeField] float helpSeconds = 12f;

        string message;
        float messageUntil;
        bool panel;
        GUIStyle label, small, box;

        void OnEnable() => GameMessages.Shown += OnMessage;
        void OnDisable() => GameMessages.Shown -= OnMessage;

        void OnMessage(string text, float seconds)
        {
            message = text;
            messageUntil = Time.time + seconds;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !(Application.isEditor || Debug.isDebugBuild))
                return;
            // Test shortcuts, only in the editor and development builds.
            if (keyboard.f1Key.wasPressedThisFrame)
                panel = !panel;
            var manager = AnomalyManager.Instance;
            if (!manager)
                return;
            if (keyboard.f2Key.wasPressedThisFrame)
                GameMessages.Show(manager.ActivateRandom() ? "[Prueba] Anomalía activada." : "[Prueba] No hay anomalías disponibles fuera de la vista.");
            if (keyboard.f3Key.wasPressedThisFrame)
            {
                manager.ResetNight();
                GameMessages.Show("[Prueba] Noche reiniciada.");
            }
        }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.92f, .94f, .9f) } };
                small = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(.85f, .88f, .86f) } };
                box = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.UpperLeft, padding = new RectOffset(12, 12, 10, 10), normal = { textColor = Color.white } };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s, h = 1080f;
            // Another system (alarm panel, monitors) has the controls: no crosshair, prompts or "click" hint.
            var player = PlayerController.Instance;
            bool steering = !player || player.ControlEnabled;

            bool hint = (interactor && interactor.Current) || (reporter && reporter.Aiming);
            GUI.color = hint ? Color.white : new Color(1, 1, 1, .55f);
            if (steering)
                GUI.DrawTexture(new Rect(w / 2 - 3, h / 2 - 3, 6, 6), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var prompt = new StringBuilder();
            if (steering && interactor && interactor.Current)
                prompt.Append("[E] ").Append(interactor.Current.Prompt);
            if (steering && reporter && reporter.Aiming)
                prompt.Append(prompt.Length > 0 ? "     " : "").Append("[R] Reportar");
            if (prompt.Length > 0)
                Shadowed(new Rect(0, h / 2 + 34, w, 34), prompt.ToString(), label);

            if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
                Shadowed(new Rect(0, h * .72f, w, 40), message, label);

            if (Time.timeSinceLevelLoad < helpSeconds)
                Shadowed(new Rect(24, 20, 1000, 26), "WASD moverse · Shift correr · E interactuar · R / clic derecho reportar · F linterna · Tab bitácora · Esc pausa"
                    + (Application.isEditor || Debug.isDebugBuild ? " · F1 pruebas" : ""), small);

            if (steering && Cursor.lockState != CursorLockMode.Locked)
                Shadowed(new Rect(0, h / 2 - 80, w, 34), "Clic para seguir jugando", label);

            if (panel)
                DrawPanel();
        }

        void DrawPanel()
        {
            var manager = AnomalyManager.Instance;
            var text = new StringBuilder("PRUEBAS  (F2 activar anomalía · F3 reiniciar noche)\n");
            if (manager)
            {
                text.Append("Acto: ").Append(manager.Act).Append("   Activas: ").Append(manager.ActiveCount)
                    .Append("   Notadas: ").Append(manager.NoticedCount).Append("   Bien: ").Append(manager.CorrectReports)
                    .Append("   Mal: ").Append(manager.WrongReports).Append('\n');
                float next = manager.NextActivationIn;
                text.Append("Próxima automática: ").Append(next < 0 ? "apagada" : next.ToString("0") + " s").Append("\n\n");
                foreach (var a in manager.Anomalies)
                {
                    if (!a)
                        continue;
                    text.Append(a.IsManifested ? "● " : a.IsActive ? "◐ " : "○ ").Append(a.DisplayName).Append("  ").Append(a.State);
                    if (a.Noticed)
                        text.Append(" (notada)");
                    text.Append('\n');
                }
            }
            else
                text.Append("No hay AnomalyManager en la escena.");

            GUI.Box(new Rect(24, 60, 560, 70 + 22 * (manager ? manager.Anomalies.Count + 2 : 1)), text.ToString(), box);
        }

        static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var color = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }
    }
}
