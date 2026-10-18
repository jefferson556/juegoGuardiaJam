using System;
using System.Collections;
using UnityEngine;

namespace Turno0313
{
    // Reinicio de la noche (Acto 1). When the sleep bar fills, the guard sinks deeper: his head drops,
    // the screen goes black on "03:13" and he is back in the caseta with a normal floor. Under the black
    // everything resets (AnomalyManager.ResetNight: clock, sleep bar, panel, code digits in new places)
    // and the doors close. The code itself stays the same.
    public sealed class NightCycle : MonoBehaviour
    {
        [Tooltip("Dónde vuelve el guardia. Vacío = donde empezó la partida.")]
        [SerializeField] Transform spawn;
        [SerializeField] float fadeOutSeconds = 1.8f;
        [SerializeField] float blackSeconds = 1.6f;
        [SerializeField] float fadeInSeconds = 1.4f;
        [SerializeField] string blackCaption = "03:13";
        [SerializeField] string returnMessage = "Otra vez las 03:13.";
        [Tooltip("Cuánto cae la cabeza (grados hacia abajo) mientras se funde a negro.")]
        [SerializeField] float headDrop = 35f;
        [Header("Audio")]
        [SerializeField] AudioSource source;
        [Tooltip("Al hundirse.")]
        [SerializeField] AudioClip sinkClip;
        [Tooltip("Al volver a la caseta (se corta tras unos segundos).")]
        [SerializeField] AudioClip returnClip;
        [SerializeField] float returnClipSeconds = 4f;

        public static NightCycle Instance { get; private set; }
        // 1 on the first night, +1 every reset.
        public int Night { get; private set; } = 1;
        public bool Restarting { get; private set; }
        public event Action<int> Restarted;

        SleepMeter sleep;
        Vector3 spawnPosition;
        float spawnYaw;

        void Awake() => Instance = this;

        void Start()
        {
            var player = PlayerController.Instance;
            if (spawn)
            {
                spawnPosition = spawn.position;
                spawnYaw = spawn.eulerAngles.y;
            }
            else if (player)
            {
                spawnPosition = player.transform.position;
                spawnYaw = player.Yaw;
            }

            sleep = SleepMeter.Instance;
            if (sleep)
                sleep.Filled += Restart;
        }

        void OnDestroy()
        {
            if (sleep)
                sleep.Filled -= Restart;
            if (Instance == this)
                Instance = null;
        }

        public void Restart()
        {
            // Only in the dream, and never on top of the false ending.
            var manager = AnomalyManager.Instance;
            if (Restarting || !isActiveAndEnabled || (manager && manager.Act != GameAct.Dream) || (FalseEnding.Instance && FalseEnding.Instance.Running))
                return;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Restarting = true;
            var player = PlayerController.Instance;
            var fade = ScreenFade.Instance;
            var manager = AnomalyManager.Instance;
            if (AlarmPanel.Instance)
                AlarmPanel.Instance.Close();
            if (player)
                player.SetControl(false, false);
            if (sleep)
                sleep.Paused = true;
            Play(sinkClip);

            // The head drops while the screen goes black.
            float yaw = player ? player.Yaw : 0, pitch = player ? player.Pitch : 0;
            if (fade)
                fade.Caption = blackCaption;
            for (float t = 0; t < fadeOutSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / fadeOutSeconds);
                if (fade)
                    fade.Alpha = k;
                if (player)
                    player.SetLook(yaw, Mathf.Lerp(pitch, pitch + headDrop, k));
                yield return null;
            }

            if (fade)
                fade.Alpha = 1;

            // Under the black: a normal floor again.
            if (manager)
                manager.ResetNight();
            foreach (var door in FindObjectsByType<Door>(FindObjectsInactive.Exclude))
                door.ResetClosed();
            if (player)
                player.Teleport(spawnPosition, spawnYaw);
            Night++;
            yield return new WaitForSeconds(blackSeconds);

            StartCoroutine(PlayFor(returnClip, returnClipSeconds));
            if (fade)
                yield return fade.FadeTo(0, fadeInSeconds);
            if (fade)
                fade.Caption = null;
            if (player)
                player.SetControl(true);
            if (sleep)
                sleep.Paused = false;
            GameMessages.Show(returnMessage, 3f);
            Restarting = false;
            Restarted?.Invoke(Night);
        }

        void Play(AudioClip clip)
        {
            if (source && clip)
                source.PlayOneShot(clip);
        }

        // Plays a long clip for a few seconds and fades it out.
        IEnumerator PlayFor(AudioClip clip, float seconds)
        {
            if (!clip)
                yield break;
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
            voice.clip = clip;
            voice.Play();
            for (float t = 0; t < seconds && voice; t += Time.deltaTime)
            {
                voice.volume = Mathf.Clamp01((seconds - t) / 1.5f);
                yield return null;
            }

            if (voice)
                Destroy(voice);
        }
    }
}
