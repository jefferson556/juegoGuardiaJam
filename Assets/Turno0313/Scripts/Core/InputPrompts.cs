using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace Turno0313
{
    // Button prompts that follow the device in use. Texts carry tokens such as "{Report} Reportar"; Label() draws
    // them as "[R]" with keyboard, "[Y]" with an Xbox (or any other) gamepad and the △ icon with a PlayStation one.
    // The device switches as soon as the player presses something on it (or moves a stick / the mouse for real).
    // Pick() chooses between a keyboard and a gamepad sentence when the wording itself changes.
    public static class InputPrompts
    {
        public enum Scheme
        {
            Keyboard,
            Xbox,
            PlayStation
        }

        public static Scheme Current { get; private set; } = Scheme.Keyboard;
        public static bool UsingGamepad => Current != Scheme.Keyboard;

        public static string Pick(string keyboard, string gamepad) => UsingGamepad ? gamepad : keyboard;

        enum Icon
        {
            None,
            Cross,
            Circle,
            Square,
            Triangle
        }

        // Token -> (keyboard, Xbox, PlayStation). PlayStation face buttons are icons.
        static readonly Dictionary<string, (string keyboard, string xbox, string ps, Icon icon)> Glyphs = new Dictionary<string, (string, string, string, Icon)>
        {
            ["Move"] = ("WASD", "Stick izq.", "Stick izq.", Icon.None),
            ["Look"] = ("Ratón", "Stick der.", "Stick der.", Icon.None),
            ["Run"] = ("Shift", "L3", "L3", Icon.None),
            ["Interact"] = ("E", "A", null, Icon.Cross),
            ["Report"] = ("R", "Y", null, Icon.Triangle),
            ["Flashlight"] = ("F", "RB", "R1", Icon.None),
            ["Notes"] = ("Tab", "Vista", "Create", Icon.None),
            ["Pause"] = ("Esc", "Menú", "Options", Icon.None),
            ["Confirm"] = ("Enter", "A", null, Icon.Cross),
            ["Back"] = ("Esc", "B", null, Icon.Circle),
            ["Erase"] = ("Retroceso", "X", null, Icon.Square),
            ["DPad"] = ("Flechas", "Cruceta", "Cruceta", Icon.None),
            ["Shoulders"] = ("← →", "LB / RB", "L1 / R1", Icon.None),
        };

        // ---- Which device is in use ----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<Tracker>())
                return;
            var go = new GameObject("InputPrompts") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Tracker>();
        }

        sealed class Tracker : MonoBehaviour
        {
            void Update()
            {
                foreach (var pad in Gamepad.all)
                    if (pad.wasUpdatedThisFrame && Used(pad))
                    {
                        Current = pad is DualShockGamepad ? Scheme.PlayStation : Scheme.Xbox;
                        return;
                    }

                var keyboard = Keyboard.current;
                var mouse = Mouse.current;
                if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                    || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 25)))
                    Current = Scheme.Keyboard;
            }

            // A real press or a firm stick push (not the drift of a resting stick).
            static bool Used(Gamepad p) =>
                p.leftStick.ReadValue().sqrMagnitude > .25f || p.rightStick.ReadValue().sqrMagnitude > .25f
                || p.buttonSouth.wasPressedThisFrame || p.buttonEast.wasPressedThisFrame || p.buttonWest.wasPressedThisFrame || p.buttonNorth.wasPressedThisFrame
                || p.startButton.wasPressedThisFrame || p.selectButton.wasPressedThisFrame
                || p.leftShoulder.wasPressedThisFrame || p.rightShoulder.wasPressedThisFrame || p.leftTrigger.wasPressedThisFrame || p.rightTrigger.wasPressedThisFrame
                || p.leftStickButton.wasPressedThisFrame || p.rightStickButton.wasPressedThisFrame
                || p.dpad.up.wasPressedThisFrame || p.dpad.down.wasPressedThisFrame || p.dpad.left.wasPressedThisFrame || p.dpad.right.wasPressedThisFrame;
        }

        // ---- Drawing (inside OnGUI) ----

        readonly struct Segment
        {
            public readonly string Text;
            public readonly Texture2D Image;

            public Segment(string text, Texture2D image)
            {
                Text = text;
                Image = image;
            }
        }

        // Icon size relative to the line of text.
        const float IconScale = 1.3f;

        static readonly Dictionary<GUIStyle, GUIStyle> flatStyles = new Dictionary<GUIStyle, GUIStyle>();
        static readonly Dictionary<Icon, Texture2D> icons = new Dictionary<Icon, Texture2D>();
        static readonly List<Segment> segments = new List<Segment>();

        // Like GUI.Label, with the tokens turned into keys, buttons or icons. Keeps the style's alignment, colour
        // and size; lines break only at '\n'. shadow: the dark copy behind the text that the HUD uses.
        public static void Label(Rect r, string text, GUIStyle style, bool shadow = false)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var flat = Flat(style);
            var lines = text.Split('\n');
            float lineHeight = flat.CalcSize(new GUIContent("Ay")).y;
            float total = lineHeight * lines.Length;
            var anchor = style.alignment;
            float y = IsUpper(anchor) ? r.y + style.padding.top : IsLower(anchor) ? r.yMax - total - style.padding.bottom : r.y + (r.height - total) / 2;
            var tint = GUI.color;
            foreach (var line in lines)
            {
                Parse(line);
                float width = 0;
                foreach (var s in segments)
                    width += s.Image ? lineHeight * IconScale + 4 : flat.CalcSize(new GUIContent(s.Text)).x;
                float x = IsLeft(anchor) ? r.x + style.padding.left : IsRight(anchor) ? r.xMax - width - style.padding.right : r.x + (r.width - width) / 2;
                foreach (var s in segments)
                {
                    if (s.Image)
                    {
                        float size = lineHeight * IconScale;
                        GUI.DrawTexture(new Rect(x + 2, y + (lineHeight - size) / 2, size, size), s.Image);
                        x += size + 4;
                        continue;
                    }

                    float w = flat.CalcSize(new GUIContent(s.Text)).x;
                    if (shadow)
                    {
                        var color = flat.normal.textColor;
                        flat.normal.textColor = new Color(0, 0, 0, .8f);
                        GUI.Label(new Rect(x + 2, y + 2, w + 1, lineHeight), s.Text, flat);
                        flat.normal.textColor = color;
                    }

                    GUI.Label(new Rect(x, y, w + 1, lineHeight), s.Text, flat);
                    x += w;
                }

                y += lineHeight;
            }

            GUI.color = tint;
        }

        // A copy of the style that draws one run of text exactly where it is put.
        static GUIStyle Flat(GUIStyle style)
        {
            if (!flatStyles.TryGetValue(style, out var flat))
            {
                flat = new GUIStyle(style) { wordWrap = false, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), margin = new RectOffset(), clipping = TextClipping.Overflow };
                flatStyles[style] = flat;
            }

            // Callers change colour and size on the fly.
            flat.normal.textColor = style.normal.textColor;
            flat.fontSize = style.fontSize;
            flat.fontStyle = style.fontStyle;
            flat.font = style.font;
            return flat;
        }

        static void Parse(string line)
        {
            segments.Clear();
            int start = 0;
            while (start < line.Length)
            {
                int open = line.IndexOf('{', start);
                int close = open >= 0 ? line.IndexOf('}', open) : -1;
                if (open < 0 || close < 0)
                {
                    segments.Add(new Segment(line.Substring(start), null));
                    break;
                }

                if (open > start)
                    segments.Add(new Segment(line.Substring(start, open - start), null));
                string token = line.Substring(open + 1, close - open - 1);
                if (Glyphs.TryGetValue(token, out var glyph))
                {
                    if (Current == Scheme.PlayStation && glyph.icon != Icon.None)
                        segments.Add(new Segment(null, IconTexture(glyph.icon)));
                    else
                        segments.Add(new Segment("[" + (Current == Scheme.Keyboard ? glyph.keyboard : Current == Scheme.Xbox ? glyph.xbox : glyph.ps) + "]", null));
                }
                else
                    segments.Add(new Segment(line.Substring(open, close - open + 1), null));

                start = close + 1;
            }
        }

        static bool IsUpper(TextAnchor a) => a == TextAnchor.UpperLeft || a == TextAnchor.UpperCenter || a == TextAnchor.UpperRight;
        static bool IsLower(TextAnchor a) => a == TextAnchor.LowerLeft || a == TextAnchor.LowerCenter || a == TextAnchor.LowerRight;
        static bool IsLeft(TextAnchor a) => a == TextAnchor.UpperLeft || a == TextAnchor.MiddleLeft || a == TextAnchor.LowerLeft;
        static bool IsRight(TextAnchor a) => a == TextAnchor.UpperRight || a == TextAnchor.MiddleRight || a == TextAnchor.LowerRight;

        // ---- PlayStation face button icons, drawn in code: a dark disc with the symbol in its colour ----

        static Texture2D IconTexture(Icon icon)
        {
            if (icons.TryGetValue(icon, out var texture) && texture)
                return texture;
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var color = icon switch
            {
                Icon.Cross => new Color(.49f, .67f, 1f),
                Icon.Circle => new Color(1f, .42f, .45f),
                Icon.Square => new Color(.93f, .56f, .86f),
                _ => new Color(.27f, .87f, .72f)
            };
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                var p = new Vector2(px + .5f - size / 2f, py + .5f - size / 2f);
                float disc = Mathf.Clamp01(31f - p.magnitude);
                float ring = Mathf.Clamp01(1.2f - Mathf.Abs(p.magnitude - 29.5f));
                float symbol = Mathf.Clamp01(.5f - (Distance(icon, p) - 2.9f));
                var c = Color.Lerp(new Color(.11f, .11f, .13f), new Color(.45f, .45f, .5f), ring * .6f);
                c = Color.Lerp(c, color, symbol);
                c.a = Mathf.Max(disc * .94f, symbol);
                pixels[py * size + px] = c;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            icons[icon] = texture;
            return texture;
        }

        // Distance from p to the symbol's stroke (y up, centre at 0).
        static float Distance(Icon icon, Vector2 p)
        {
            switch (icon)
            {
                case Icon.Cross:
                    return Mathf.Min(ToSegment(p, new Vector2(-13, -13), new Vector2(13, 13)), ToSegment(p, new Vector2(-13, 13), new Vector2(13, -13)));
                case Icon.Circle:
                    return Mathf.Abs(p.magnitude - 14f);
                case Icon.Square:
                    var q = new Vector2(Mathf.Abs(p.x) - 12.5f, Mathf.Abs(p.y) - 12.5f);
                    float outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude;
                    return Mathf.Abs(outside + Mathf.Min(Mathf.Max(q.x, q.y), 0));
                default:
                    var a = new Vector2(0, 16);
                    var b = new Vector2(-15, -10);
                    var c = new Vector2(15, -10);
                    return Mathf.Min(ToSegment(p, a, b), Mathf.Min(ToSegment(p, b, c), ToSegment(p, c, a)));
            }
        }

        static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
