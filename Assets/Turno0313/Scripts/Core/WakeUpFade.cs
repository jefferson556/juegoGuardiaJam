using UnityEngine;

namespace Turno0313
{
    // Opening the eyes: the scene starts black and two soft eyelids open, close once in a blink and open fully.
    // Drawn behind the HUD (GUI.depth 1), so a message such as "Estás despierto..." reads on the black while the
    // eyes open. Removes itself when done. Used where the guard wakes up after Act 2 (Piso13 Realidad).
    public sealed class WakeUpFade : MonoBehaviour
    {
        // (time in seconds, how open the eyes are 0..1)
        static readonly Vector2[] Keys =
        {
            new Vector2(0f, 0f), new Vector2(.6f, 0f), new Vector2(1.4f, .4f), new Vector2(1.75f, .06f),
            new Vector2(2.1f, .1f), new Vector2(3.2f, 1f)
        };

        float time;
        Texture2D fadeDown, fadeUp;

        void Update()
        {
            time += Time.deltaTime;
            if (time > Keys[Keys.Length - 1].x + .1f)
                Destroy(this);
        }

        static float Openness(float t)
        {
            for (int i = 1; i < Keys.Length; i++)
                if (t < Keys[i].x)
                    return Mathf.Lerp(Keys[i - 1].y, Keys[i].y, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Keys[i - 1].x, Keys[i].x, t)));
            return 1;
        }

        void OnDestroy()
        {
            if (fadeDown)
                Destroy(fadeDown);
            if (fadeUp)
                Destroy(fadeUp);
        }

        // A black vertical gradient, opaque at one end and clear at the other: the soft edge of an eyelid.
        static Texture2D Gradient(bool opaqueAtTop)
        {
            const int size = 64;
            var texture = new Texture2D(1, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
            {
                // Row 0 is drawn at the bottom of the rect.
                float k = y / (size - 1f);
                texture.SetPixel(0, y, new Color(0, 0, 0, Mathf.SmoothStep(0, 1, opaqueAtTop ? k : 1 - k)));
            }

            texture.Apply();
            return texture;
        }

        void OnGUI()
        {
            float open = Openness(time);
            if (open >= 1)
                return;
            GUI.depth = 1;
            GUI.matrix = Matrix4x4.identity;
            float w = Screen.width, h = Screen.height;
            if (!fadeDown)
            {
                fadeDown = Gradient(true);
                fadeUp = Gradient(false);
            }

            // Still dim while the eyes are half shut.
            GUI.color = new Color(0, 0, 0, (1 - open) * .55f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);

            // Each eyelid covers half the screen when closed, with a soft edge that fades out as they open.
            float lid = h / 2 * (1 - open);
            float feather = h * .12f;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, w, lid), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, h - lid, w, lid), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(1.4f - open));
            GUI.DrawTexture(new Rect(0, lid, w, feather), fadeDown);
            GUI.DrawTexture(new Rect(0, h - lid - feather, w, feather), fadeUp);
            GUI.color = Color.white;
        }
    }
}
