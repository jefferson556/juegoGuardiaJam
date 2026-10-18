using UnityEngine;
using UnityEngine.InputSystem;

namespace Turno0313
{
    // Temporary Act 2 HUD made with OnGUI: the reality bar, also over the camera screen. The final HUD
    // (Jefferson/Elvis) can read RealityMeter instead. Test keys (editor and development builds):
    // F9 = saltar al final, F10 = −20 % realidad, F11 = ir al Acto 2 sin pasar por el sueño.
    public sealed class Act2HUD : MonoBehaviour
    {
        [SerializeField] RealityMeter reality;

        GUIStyle caption, debug;

        void Awake()
        {
            if (!reality)
                reality = GetComponent<RealityMeter>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !(Application.isEditor || Debug.isDebugBuild))
                return;
            if (reality && keyboard.f10Key.wasPressedThisFrame)
                reality.Add(-.2f);
            if (keyboard.f11Key.wasPressedThisFrame && Act2Director.Instance)
                Act2Director.Instance.SkipToAct2();
            // In reality: straight to the final, as if the escaped anomaly had been caught.
            bool inReality = AnomalyManager.Instance && AnomalyManager.Instance.Act == GameAct.Reality;
            if (inReality && keyboard.f9Key.wasPressedThisFrame && FinalSequence.Instance)
                FinalSequence.Instance.Play();
        }

        void OnGUI()
        {
            if (!reality || !reality.Shown)
                return;
            // Over the camera screen (-2) and the debug HUD.
            GUI.depth = -3;
            if (caption == null)
            {
                caption = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.95f, .9f, .82f) } };
                debug = new GUIStyle(caption) { fontSize = 12, normal = { textColor = new Color(.78f, .74f, .7f) } };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s, value = reality.Value;
            float appear = Mathf.Clamp01((Time.time - reality.ShownAt) / 1.5f);
            const float barW = 420, barH = 12, y = 46;
            float x = (w - barW) / 2;
            bool dev = Application.isEditor || Debug.isDebugBuild;

            GUI.color = new Color(0, 0, 0, .45f * appear);
            GUI.DrawTexture(new Rect(x - 16, y - 34, barW + 32, dev ? 74 : 56), Texture2D.whiteTexture);
            GUI.color = new Color(0, 0, 0, .6f * appear);
            GUI.DrawTexture(new Rect(x - 3, y - 3, barW + 6, barH + 6), Texture2D.whiteTexture);
            // Warm white when full, red when it is about to run out; it pulses in the last quarter.
            var fill = Color.Lerp(new Color(.9f, .22f, .16f), new Color(.98f, .86f, .6f), value);
            if (value < .25f)
                fill = Color.Lerp(fill, Color.white, (Mathf.Sin(Time.time * 8f) + 1) * .2f);
            fill.a = appear;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(x, y, barW * value, barH), Texture2D.whiteTexture);

            GUI.color = new Color(1, 1, 1, appear);
            Shadowed(new Rect(x, y - 30, barW, 24), "REALIDAD", caption);
            if (dev)
                Shadowed(new Rect(x, y + barH + 4, barW, 20), (value * 100).ToString("0") + " %   " + (reality.Rate * 100).ToString("0.00") + " %/s   (F10 −20 %)", debug);
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.identity;
        }

        static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var color = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .85f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }
    }
}
