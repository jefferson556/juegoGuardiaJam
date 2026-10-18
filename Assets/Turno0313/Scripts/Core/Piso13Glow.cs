using UnityEngine;

namespace Turno0313
{
    // Turns on the glow of the Piso 13 room models (fluorescent tubes, screens, emergency lights), the same way
    // Piso 13's own world does at runtime: the imported materials have no emission of their own.
    public sealed class Piso13Glow : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                Color color;
                if (n.Contains("Fluorescent_On"))
                    color = new Color(.62f, .84f, .89f) * 1.35f;
                else if (n.Contains("Fluorescent_Dim"))
                    color = new Color(.36f, .49f, .48f) * .7f;
                else if (n.Contains("ScreenText"))
                    color = new Color(.33f, .68f, .59f) * .65f;
                else if (n.Contains("Screen"))
                    color = new Color(.025f, .085f, .077f) * .45f;
                else if (n.Contains("Emergency"))
                    color = new Color(.86f, .14f, .024f) * .72f;
                else
                    continue;
                foreach (var m in r.materials)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor(EmissionColor, color);
                }
            }
        }
    }
}
