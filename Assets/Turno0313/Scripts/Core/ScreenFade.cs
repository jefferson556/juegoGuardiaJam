using System.Collections;
using UnityEngine;

namespace Turno0313
{
    // Full-screen fade to black with an optional centred caption, drawn over every HUD.
    // For the night reset, the false ending and the return to the dream in Act 2:
    //   yield return ScreenFade.Instance.FadeTo(1, 1.5f);   // to black
    //   yield return ScreenFade.Instance.FadeTo(0, 1f);     // back
    public sealed class ScreenFade : MonoBehaviour
    {
        public static ScreenFade Instance { get; private set; }
        // 0 = clear, 1 = black.
        public float Alpha { get; set; }
        // Shown on the black, fading with it.
        public string Caption { get; set; }
        public Color CaptionColor { get; set; } = new Color(1f, .2f, .12f);
        public int CaptionSize { get; set; } = 96;

        GUIStyle style;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            float start = Alpha;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                Alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }

            Alpha = target;
        }

        void OnGUI()
        {
            if (Alpha <= 0)
                return;
            GUI.depth = -10;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(0, 0, 0, Alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (!string.IsNullOrEmpty(Caption))
            {
                style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                style.fontSize = Mathf.RoundToInt(CaptionSize * Screen.height / 1080f);
                style.normal.textColor = CaptionColor;
                GUI.color = new Color(1, 1, 1, Alpha * Alpha);
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), Caption, style);
            }

            GUI.color = Color.white;
        }
    }
}
