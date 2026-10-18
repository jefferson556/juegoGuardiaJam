using UnityEngine;

namespace Turno0313
{
    // One digit of the wake code, written on an anomaly ("_ 7 _ _" = second digit is 7). It is only
    // visible while the anomaly looks wrong, so the player has to read it before reporting it.
    // WakeCode decides which digit each anomaly carries.
    public sealed class CodeDigit : MonoBehaviour
    {
        [Tooltip("Textos donde se escribe el dígito (por ejemplo, las dos caras del respaldo de la silla).")]
        [SerializeField] TextMesh[] labels;

        public int Position { get; private set; } = -1;
        public char Digit { get; private set; }

        Anomaly anomaly;
        Renderer[] renderers;

        void Awake()
        {
            anomaly = GetComponentInParent<Anomaly>();
            renderers = new Renderer[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                renderers[i] = labels[i] ? labels[i].GetComponent<Renderer>() : null;
            Show(false);
        }

        public void Assign(int position, char digit)
        {
            Position = position;
            Digit = digit;
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < AlarmPanel.Length; i++)
            {
                if (i > 0)
                    text.Append(' ');
                text.Append(i == position ? digit : '_');
            }

            foreach (var label in labels)
                if (label)
                    label.text = text.ToString();
        }

        void LateUpdate()
        {
            var manager = AnomalyManager.Instance;
            Show(Position >= 0 && anomaly && anomaly.IsManifested && (!manager || manager.Act == GameAct.Dream));
        }

        void Show(bool visible)
        {
            foreach (var r in renderers)
                if (r)
                    r.enabled = visible;
        }
    }
}
