using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Objeto interactivo del televisor/computador de circuito cerrado (camarasComputador).
    /// - Situado exactamente en (0.18, -0.16, 0.55).
    /// - Provee el BoxCollider trigger para referencia visual y física en el editor.
    /// - Detecta cuando el jugador está parado frente al escritorio para habilitar la opción de cámaras en el HUD.
    /// </summary>
    public class P13CCTVStation : MonoBehaviour
    {
        [Header("Configuración de Posición y Alcance")]
        public Vector3 DeskCoordinates = new Vector3(0.18f, -0.16f, 0.55f);
        public float ActivationDistance = 1.15f;
        public Vector3 ColliderSize = new Vector3(0.9f, 1.2f, 0.9f);
        public Vector3 ColliderCenter = new Vector3(0f, 0.5f, 0f);

        [Header("Referencias")]
        public P13RealidadGame Game;

        private BoxCollider triggerCollider;

        void Awake()
        {
            SetupCollider();
        }

        public void SetupCollider()
        {
            transform.position = DeskCoordinates;

            triggerCollider = GetComponent<BoxCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<BoxCollider>();
            }

            triggerCollider.isTrigger = true;
            triggerCollider.center = ColliderCenter;
            triggerCollider.size = ColliderSize;
        }

        /// <summary>
        /// Comprueba si el jugador se encuentra en el rango frontal del computador/escritorio.
        /// </summary>
        public bool IsPlayerInRange(Vector3 playerPos)
        {
            Vector2 p = new Vector2(playerPos.x, playerPos.z);
            Vector2 desk = new Vector2(transform.position.x, transform.position.z);
            return Vector2.Distance(p, desk) <= ActivationDistance;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position + ColliderCenter, ColliderSize);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, ActivationDistance);
        }
    }
}
