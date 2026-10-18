using UnityEngine;

namespace Turno0313
{
    // The real credits, on black: the block rises into place and stays centred, with a hint line underneath.
    // They close the game at the end of Jefferson's scene (P13RealidadEnding). Call Draw() from OnGUI.
    public static class CreditsRoll
    {
        // The first line is the title.
        public const string Text = "TURNO 03:13\n\nPROGRAMACIÓN\nJohn Orellana\nMiguel Navarrete\nJefferson Perez\n\nDISEÑO Y ARTE\nIvo Chiriboga\nElvis Reyes B\n\nQA Y PRODUCCIÓN\nGiovanni Bauz\n\nHecho para la jam «Something's Off»\n\nGracias por jugar";
        // The credits music, from Resources (the same as the menu).
        public const string Music = "Audio/P13_Menu_Ulrich";
        const float Speed = 60;

        static readonly string[] Lines = Text.Split('\n');
        static GUIStyle titleStyle, lineStyle, hintStyle;

        // elapsed: seconds since the credits started. hint: shown at the bottom (null = nothing).
        public static void Draw(float elapsed, string hint)
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (titleStyle == null)
            {
                lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                titleStyle = new GUIStyle(lineStyle) { fontSize = 64, fontStyle = FontStyle.Bold };
                hintStyle = new GUIStyle(lineStyle) { fontSize = 18, normal = { textColor = new Color(.6f, .64f, .7f) } };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s;
            float total = 0;
            for (int i = 0; i < Lines.Length; i++)
                total += LineHeight(Lines[i], i == 0);
            float rest = (1080 - 80 - total) / 2;
            float y = Mathf.Max(rest, rest + 240 - elapsed * Speed);
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(elapsed / 1.5f));
            for (int i = 0; i < Lines.Length; i++)
            {
                float h = LineHeight(Lines[i], i == 0);
                if (!string.IsNullOrEmpty(Lines[i]))
                    GUI.Label(new Rect(0, y, w, h), Lines[i], i == 0 ? titleStyle : lineStyle);
                y += h;
            }

            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(hint))
                GUI.Label(new Rect(0, 1080 - 80, w, 40), hint, hintStyle);
            GUI.matrix = Matrix4x4.identity;
        }

        static float LineHeight(string line, bool title) => title ? 110 : string.IsNullOrEmpty(line) ? 24 : 46;
    }
}
