using System;
using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Componente modular para identificar y gestionar anomalías en el mapa.
    /// Listo para vincular eventos de mapa y activar reacciones del enemigo.
    /// </summary>
    public class P13Anomaly : MonoBehaviour
    {
        [Header("Datos de la Anomalía")]
        public string AnomalyId = "Anomalia_01";
        public string Description = "Objeto fuera de lugar o alteración espacial";
        public bool IsActive = true;
        public bool IsReported = false;

        [Header("Efecto en el Enemigo")]
        [Tooltip("Multiplicador de velocidad que adquirirá el enemigo al reaccionar a esta anomalía")]
        public float EnemySpeedMultiplier = 1.6f;
        [Tooltip("Duración de la alerta del enemigo hacia esta zona")]
        public float AlertDuration = 10f;

        [Header("Comportamiento al Reportar")]
        [Tooltip("Indica si el objeto desaparece del mapa al ser reportado con éxito")]
        public bool DisappearOnReport = true;
        [Tooltip("Distancia máxima prudente para reportar esta anomalía (en metros)")]
        public float MaxReportDistance = 2.2f;
        [Tooltip("Texto para el prompt de interacción en pantalla")]
        public string CustomPrompt = "";

        public string GetPrompt()
        {
            if (!string.IsNullOrEmpty(CustomPrompt)) return CustomPrompt;
            return $"{AnomalyId}: ¿Deseas reportar que encontraste una anomalía? [R]";
        }

        public event Action OnAnomalyReported;

        public bool Report()
        {
            if (!IsActive || IsReported) return false;

            IsReported = true;
            OnAnomalyReported?.Invoke();

            // Si está configurado para desaparecer, desactivar el objeto
            if (DisappearOnReport)
            {
                gameObject.SetActive(false);
            }

            // Notificar al enemigo para aplicar ralentización por reporte
            if (P13RealidadGame.Instance != null && P13RealidadGame.Instance.Enemy != null)
            {
                P13RealidadGame.Instance.Enemy.OnAnomalyReported();
            }

            return true;
        }
    }
}
