using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Rota las manecillas del Reloj Pentagrama de manera continua en sentido antihorario.
    /// </summary>
    public sealed class P13ClockHandsRotator : MonoBehaviour
    {
        [Header("Velocidades de rotación (grados por segundo, antihorario)")]
        [SerializeField] public float SecondHandSpeed = 120f;
        [SerializeField] public float MinuteHandSpeed = 24f;
        [SerializeField] public float HourHandSpeed = 4f;

        private Transform secondHand;
        private Transform minuteHand;
        private Transform hourHand;

        void Awake()
        {
            FindHands();
        }

        public void FindHands()
        {
            if (secondHand == null)
            {
                secondHand = transform.Find("NEXO_Reloj/NEXO_Segundero");
                if (secondHand == null)
                {
                    // Búsqueda recursiva por nombre
                    foreach (var t in GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.Contains("Segundero")) { secondHand = t; break; }
                    }
                }
            }

            if (minuteHand == null)
            {
                minuteHand = transform.Find("NEXO_Reloj/NEXO_Minutera");
                if (minuteHand == null)
                {
                    foreach (var t in GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.Contains("Minutera")) { minuteHand = t; break; }
                    }
                }
            }

            if (hourHand == null)
            {
                hourHand = transform.Find("NEXO_Reloj/NEXO_Horaria");
                if (hourHand == null)
                {
                    foreach (var t in GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.Contains("Horaria")) { hourHand = t; break; }
                    }
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // En el sistema de coordenadas locales con normal +Z, una rotación positiva en Z gira en sentido antihorario.
            if (secondHand != null)
            {
                secondHand.Rotate(0f, 0f, SecondHandSpeed * dt, Space.Self);
            }

            if (minuteHand != null)
            {
                minuteHand.Rotate(0f, 0f, MinuteHandSpeed * dt, Space.Self);
            }

            if (hourHand != null)
            {
                hourHand.Rotate(0f, 0f, HourHandSpeed * dt, Space.Self);
            }
        }
    }
}
