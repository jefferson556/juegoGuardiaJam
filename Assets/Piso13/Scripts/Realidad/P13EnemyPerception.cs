using System;
using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Sistema de percepción sensorial del enemigo en base a la barra de Realidad.
    /// - Barra >= 50%: El enemigo NO sabe dónde estás a través de la pérdida de realidad. Solo ve, oye o patrulla.
    /// - Barra entre 50% y 1%: El enemigo percibe tu ubicación con frecuencia y precisión progresivas (error decreciente).
    /// - Barra = 0%: Conoce tu ubicación exacta en todo momento, entra en cacería permanente a +50% de velocidad.
    /// </summary>
    public sealed class P13EnemyPerception
    {
        readonly P13RealidadGame game;
        readonly P13RealidadNavigation navigation;

        float pingTimer = 0f;
        Vector3? lastEstimatedPosition;

        public float Awareness { get; private set; } = 0f;
        public bool IsExactTracking => Awareness >= 1f;
        public Vector3? LastEstimatedPosition => lastEstimatedPosition;

        public P13EnemyPerception(P13RealidadGame game, P13RealidadNavigation navigation)
        {
            this.game = game;
            this.navigation = navigation;
        }

        /// <summary>
        /// Actualiza el estado de percepción del enemigo según la barra de Realidad/Sueño.
        /// </summary>
        public void Tick(float dt, out Vector3? targetInvestigationPos, out bool forcePermanentHunt, out float realitySpeedMultiplier)
        {
            targetInvestigationPos = null;
            forcePermanentHunt = false;
            realitySpeedMultiplier = 1.0f;

            if (game == null) return;

            float reality = Mathf.Clamp(game.Sleep, 0f, 100f);

            // 1. Barra >= 50%: Sin percepción extrasensorial
            if (reality >= 50f)
            {
                Awareness = 0f;
                lastEstimatedPosition = null;
                pingTimer = 8.0f; // Pausa prudente para no pingear en el instante exacto de cruzar el umbral
                return;
            }

            // 2. Barra <= 0%: Ubicación exacta continua y +50% de velocidad
            if (reality <= 0f)
            {
                Awareness = 1.0f;
                forcePermanentHunt = true;
                realitySpeedMultiplier = 1.5f; // +50% de velocidad
                lastEstimatedPosition = game.PlayerPosition;
                targetInvestigationPos = game.PlayerPosition;
                return;
            }

            // 3. Barra entre 50% y 0.1%: Precisión e intervalo progresivos
            Awareness = (50f - reality) / 50f; // 0.0 a 1.0
            float interval = Mathf.Lerp(7.5f, 1.2f, Awareness);
            float errorRadius = Mathf.Lerp(7.5f, 0.4f, Awareness);

            pingTimer -= dt;
            if (pingTimer <= 0f)
            {
                pingTimer = interval;

                Vector2 offset = UnityEngine.Random.insideUnitCircle * errorRadius;
                Vector3 estimated = new Vector3(game.PlayerPosition.x + offset.x, 0.8f, game.PlayerPosition.z + offset.y);

                if (navigation != null)
                {
                    estimated = navigation.Safe(estimated);
                }

                lastEstimatedPosition = estimated;
                targetInvestigationPos = estimated;
            }
        }
    }
}
