using System;
using System.Collections;
using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Componente interactivo para la taza de café:
    /// - Líquido dinámico con shader reflectante.
    /// - Consumo con tecla [E] y sonido freesound_community-gulping-water-83421.
    /// - Vaciado visual animado.
    /// - Autorrellenado tras temporizador con sonido freesound_community-glug-glug-37131.
    /// - Reportable como anomalía con tecla [R].
    /// </summary>
    public class P13CoffeeCup : MonoBehaviour
    {
        [Header("Audio")]
        public AudioClip drinkClip;
        public AudioClip refillClip;

        [Header("Configuración de Líquido")]
        public float fullHeight = 0.52f;
        public float emptyHeight = 0.08f;
        public float refillDelay = 10f;
        public float maxInteractionDistance = 2.0f;

        Transform liquid;
        Material liquidMaterial;
        AudioSource audioSource;
        bool hasCoffee = true;
        bool isAnimating = false;

        void Awake()
        {
            InitLiquid();
        }

        public void InitLiquid()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.playOnAwake = false;
                audioSource.maxDistance = 12f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            }

            if (!drinkClip)
                drinkClip = Resources.Load<AudioClip>("Audio/freesound_community-gulping-water-83421");
            if (!refillClip)
                refillClip = Resources.Load<AudioClip>("Audio/freesound_community-glug-glug-37131");

            if (liquid == null)
            {
                CreateLiquidDisc();
            }
        }

        void CreateLiquidDisc()
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Liquid_Coffee";
            disc.transform.SetParent(transform, false);

            var col = disc.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            // Ajuste local al interior de la taza
            disc.transform.localPosition = new Vector3(0f, fullHeight, 0f);
            disc.transform.localRotation = Quaternion.identity;
            disc.transform.localScale = new Vector3(0.38f, 0.015f, 0.38f);

            var r = disc.GetComponent<Renderer>();
            var shader = P13RealidadWorld.LitShader();
            liquidMaterial = new Material(shader);
            // Color café oscuro concentrado y reflectante
            var coffeeColor = new Color(0.14f, 0.06f, 0.02f);
            liquidMaterial.color = coffeeColor;
            liquidMaterial.SetColor("_BaseColor", coffeeColor);
            liquidMaterial.SetFloat("_Smoothness", 0.92f);
            liquidMaterial.SetFloat("_Metallic", 0.05f);
            r.sharedMaterial = liquidMaterial;

            liquid = disc.transform;
        }

        public string GetPrompt()
        {
            if (hasCoffee)
                return "Taza de Café: Tomar café [E]  ·  ¿Deseas reportar que encontraste una anomalía? [R]";
            else
                return "Taza de Café (Vacía): ¿Deseas reportar que encontraste una anomalía? [R]";
        }

        public event Action OnCoffeeConsumed;

        public bool Drink()
        {
            if (!hasCoffee || isAnimating) return false;
            OnCoffeeConsumed?.Invoke();
            StartCoroutine(DrinkRoutine());
            return true;
        }

        IEnumerator DrinkRoutine()
        {
            isAnimating = true;
            hasCoffee = false;

            // Reproducir sonido de trago
            if (drinkClip != null && audioSource != null)
            {
                audioSource.clip = drinkClip;
                audioSource.volume = 0.85f;
                audioSource.Play();
            }

            // Descender nivel de líquido suavemente
            float elapsed = 0f;
            float duration = 1.4f;
            Vector3 startPos = new Vector3(0f, fullHeight, 0f);
            Vector3 targetPos = new Vector3(0f, emptyHeight, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (liquid != null)
                {
                    liquid.localPosition = Vector3.Lerp(startPos, targetPos, t);
                    liquid.localScale = Vector3.Lerp(new Vector3(0.38f, 0.015f, 0.38f), new Vector3(0.25f, 0.005f, 0.25f), t);
                }
                yield return null;
            }

            isAnimating = false;

            // Esperar tiempo para que comience el misterioso autorrellenado
            yield return new WaitForSeconds(refillDelay);

            // Iniciar autorrellenado
            StartCoroutine(RefillRoutine());
        }

        IEnumerator RefillRoutine()
        {
            isAnimating = true;

            // Reproducir sonido de vertido/burbujeo
            if (refillClip != null && audioSource != null)
            {
                audioSource.clip = refillClip;
                audioSource.volume = 0.80f;
                audioSource.Play();
            }

            float elapsed = 0f;
            float duration = 2.2f;
            Vector3 startPos = new Vector3(0f, emptyHeight, 0f);
            Vector3 targetPos = new Vector3(0f, fullHeight, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (liquid != null)
                {
                    liquid.localPosition = Vector3.Lerp(startPos, targetPos, t);
                    liquid.localScale = Vector3.Lerp(new Vector3(0.25f, 0.005f, 0.25f), new Vector3(0.38f, 0.015f, 0.38f), t);
                }
                yield return null;
            }

            hasCoffee = true;
            isAnimating = false;
        }
    }

    /// <summary>
    /// Componente genérico para objetos reportables del mapa (ej. Cafetera).
    /// Permite configurar penalización si se reporta como falsa anomalía.
    /// </summary>
    public class P13ReportableProp : MonoBehaviour
    {
        public string PropId = "Prop";
        public bool IsFakeAnomaly = true;
        public float PenaltyPercent = 10f;
        public float MaxDistance = 2.2f;
        public string CustomPrompt = "";
        public string FakeMessage = "FALSA ALARMA: La cafetera no es una anomalía (-10% Realidad)";

        public string GetPrompt()
        {
            if (!string.IsNullOrEmpty(CustomPrompt)) return CustomPrompt;
            return $"{PropId}: ¿Deseas reportar que encontraste una anomalía? [R]";
        }

        public void Report(P13RealidadGame game)
        {
            if (game == null) return;

            if (IsFakeAnomaly)
            {
                // Penalizar la barra de realidad en el porcentaje indicado
                float penalty = PenaltyPercent;
                game.Reality = Mathf.Max(0f, game.Reality - penalty);

                if (game.Audio != null)
                {
                    game.Audio.Spatial("P13_Door_Latch", transform.position, 0.6f);
                }

                game.ShowNotification(FakeMessage, 3.2f);

                if (game.Enemy != null)
                {
                    game.Enemy.AlertNoise(transform.position, 0.6f);
                }
            }
        }
    }

    /// <summary>
    /// Componente para la Persona Elegante en CAM 06 (Servidores).
    /// - Solo puede ser revelada mirando por la cámara CAM 06 después de reportar la anomalía del café.
    /// - Una vez revelada, permanece spawneada permanentemente en el mapa físico (ya no desaparece).
    /// - No se puede reportar desde las cámaras (solo hace que aparezca); el jugador debe acercarse en persona para reportarla con [R].
    /// - Oscila continuamente 180 grados de lado a lado sobre el eje Y.
    /// </summary>
    public class P13PersonaEleganteAnomaly : MonoBehaviour
    {
        public Quaternion BaseRotation;
        public bool HasSpawned = false;
        public bool IsReported = false;
        public float MaxDistance = 2.5f;

        void Awake()
        {
            BaseRotation = transform.localRotation;
            HasSpawned = false;
            IsReported = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (IsReported || !HasSpawned) return;

            // Oscilación continua de 180 grados en el eje Y (-90 a +90)
            float yAngle = Mathf.Sin(Time.time * 2.5f) * 90f;
            transform.localRotation = BaseRotation * Quaternion.Euler(0f, yAngle, 0f);
        }

        public void Spawn()
        {
            if (HasSpawned || IsReported) return;
            HasSpawned = true;
            gameObject.SetActive(true);
        }

        public string GetPrompt()
        {
            return "Persona Elegante: ¿Deseas reportar que encontraste una anomalía? [R]";
        }

        public bool Report(P13RealidadGame game)
        {
            if (IsReported || !HasSpawned) return false;
            IsReported = true;
            gameObject.SetActive(false);

            if (game != null)
            {
                game.Reality = Mathf.Min(100f, game.Reality + 10f);
                if (game.Enemy != null)
                {
                    game.Enemy.ApplySlow(10f, 0.5f);
                    game.Enemy.TriggerMapEvent("Sujeto_Servidores", transform.position, 1.0f, 10f);
                }

                if (game.Audio != null)
                {
                    game.Audio.Spatial("P13_UI_Hover", transform.position, 0.8f);
                }

                game.ShowNotification("¡ANOMALÍA ENCONTRADA!: Sujeto en Servidores (+10% Realidad)", 3.5f);
            }
            return true;
        }
    }
}
