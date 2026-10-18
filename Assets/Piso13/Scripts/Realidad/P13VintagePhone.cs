using System.Collections;
using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Gestiona la lógica del teléfono vintage rojo:
    /// - Timbrado por proximidad al escritorio.
    /// - Interacción para contestar con tecla [E].
    /// - Secuencia de llamada: voz-llamada -> spawn reloj pentagrama + cuelga-telefono.
    /// - Inmovilización y ocultamiento de mano durante la llamada.
    /// </summary>
    public sealed class P13VintagePhone : MonoBehaviour
    {
        [Header("Referencias")]
        public P13RealidadGame Game;

        [Header("Distancias de Proximidad")]
        public float ProximityDistance = 2.5f;
        public float ExitDistance = 3.0f;

        [Header("Clips de Audio")]
        public AudioClip RingClip;
        public AudioClip VoiceClip;
        public AudioClip HangupClip;

        private AudioSource ringSource;
        private AudioSource callSource;

        private bool hasAnswered = false;
        private bool isRinging = false;
        private bool isCallInProgress = false;

        public bool HasAnswered => hasAnswered;
        public bool IsRinging => isRinging;
        public bool IsCallInProgress => isCallInProgress;
        public bool CanAnswer => isRinging && !isCallInProgress && !hasAnswered;

        void Awake()
        {
            SetupAudioSources();
        }

        public void SetupAudioSources()
        {
            if (ringSource != null && callSource != null) return;

            // Cargar clips si no están asignados en el inspector
            if (RingClip == null) RingClip = Resources.Load<AudioClip>("Audio/sonido-llamada");
            if (VoiceClip == null) VoiceClip = Resources.Load<AudioClip>("Audio/voz-llamada");
            if (HangupClip == null) HangupClip = Resources.Load<AudioClip>("Audio/cuelga-telefono");

            // AudioSource para el timbrado (3D espacial proveniente del teléfono)
            if (ringSource == null) ringSource = gameObject.AddComponent<AudioSource>();
            ringSource.playOnAwake = false;
            ringSource.loop = true;
            ringSource.spatialBlend = 1.0f;
            ringSource.minDistance = 1.0f;
            ringSource.maxDistance = 12.0f;
            ringSource.rolloffMode = AudioRolloffMode.Linear;
            ringSource.clip = RingClip;
            ringSource.volume = 0.85f;

            // AudioSource para la llamada (Voz y colgado con presencia cercana)
            callSource = gameObject.AddComponent<AudioSource>();
            callSource.playOnAwake = false;
            callSource.loop = false;
            callSource.spatialBlend = 0.25f; // Semiespacial para claridad
            callSource.volume = 1.0f;
        }

        void Update()
        {
            if (hasAnswered || isCallInProgress)
            {
                if (isRinging) StopRinging();
                return;
            }

            if (Game == null) return;

            // Medir distancia al guardia (usando PlayerPosition)
            Vector3 playerPos = Game.PlayerPosition;
            float dist = Vector3.Distance(transform.position, playerPos);

            if (!isRinging && dist <= ProximityDistance)
            {
                StartRinging();
            }
            else if (isRinging && dist > ExitDistance)
            {
                StopRinging();
            }
        }

        void StartRinging()
        {
            if (hasAnswered || isCallInProgress || isRinging) return;
            isRinging = true;
            if (ringSource != null && RingClip != null)
            {
                ringSource.clip = RingClip;
                ringSource.Play();
            }
        }

        void StopRinging()
        {
            if (!isRinging) return;
            isRinging = false;
            if (ringSource != null && ringSource.isPlaying)
            {
                ringSource.Stop();
            }
        }

        /// <summary>
        /// Inicia la secuencia al contestar el teléfono con [E].
        /// </summary>
        public bool AnswerCall()
        {
            if (!CanAnswer) return false;

            hasAnswered = true;
            StopRinging();
            StartCoroutine(CallSequenceRoutine());
            return true;
        }

        private IEnumerator CallSequenceRoutine()
        {
            isCallInProgress = true;

            // 1. Inmovilizar guardia y ocultar mano
            if (Game != null)
            {
                Game.OnPhoneCallStarted();
            }

            // 2. Reproducir voz-llamada
            if (callSource != null && VoiceClip != null)
            {
                callSource.clip = VoiceClip;
                callSource.Play();
                yield return new WaitForSeconds(VoiceClip.length);
            }
            else
            {
                yield return new WaitForSeconds(3.0f);
            }

            // 3. Cuando termina voz-llamada:
            // a) Spawn del Reloj Pentagrama
            if (Game != null)
            {
                Game.SpawnClockPentagramAnomaly();
            }

            // b) Reproducir cuelga-telefono
            if (callSource != null && HangupClip != null)
            {
                callSource.clip = HangupClip;
                callSource.Play();
                yield return new WaitForSeconds(HangupClip.length);
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            // 4. Liberar al guardia y restaurar mano
            if (Game != null)
            {
                Game.OnPhoneCallEnded();
            }

            isCallInProgress = false;
        }

        void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}
