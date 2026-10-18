using System;
using System.Collections;
using UnityEngine;

namespace Turno0313
{
    // Acto 2 (realidad). It begins when the false ending hands control back (FalseEnding.Finished): the reality
    // bar appears and the cameras work. If the bar empties, the guard falls back into the dream: fade to "03:13",
    // the floor goes back to Act 1 with a NEW code, the sleep bar already on screen, and the alarm panel waiting
    // again. Waking up a second time skips the fake credits (FalseEnding).
    public sealed class Act2Director : MonoBehaviour
    {
        [Tooltip("Dónde vuelve el guardia al caer de nuevo en el sueño.")]
        [SerializeField] Transform spawn;
        [SerializeField] string startMessage = "Algo me siguió fuera del sueño. Revisa las cámaras.";
        [SerializeField] string backMessage = "Otra vez dormido. El código ya no es el mismo.";
        [SerializeField] float fadeOutSeconds = 1.8f;
        [SerializeField] float blackSeconds = 1.6f;
        [SerializeField] float fadeInSeconds = 1.4f;
        [SerializeField] float headDrop = 35f;
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip sinkClip;

        public static Act2Director Instance { get; private set; }
        // Act 2 is being played (not during the return to the dream).
        public bool Running { get; private set; }
        public bool Returning { get; private set; }
        public int Returns { get; private set; }
        public event Action Started;
        public event Action ReturnedToDream;

        RealityMeter reality;
        FalseEnding ending;

        void Awake() => Instance = this;

        void Start()
        {
            reality = RealityMeter.Instance;
            ending = FalseEnding.Instance;
            if (ending)
                ending.Finished += Begin;
            if (reality)
                reality.Emptied += ReturnToDream;
        }

        void OnDestroy()
        {
            if (ending)
                ending.Finished -= Begin;
            if (reality)
                reality.Emptied -= ReturnToDream;
            if (Instance == this)
                Instance = null;
        }

        public void Begin()
        {
            Running = true;
            if (reality)
                reality.Begin();
            GameMessages.Show(startMessage, 4f);
            Started?.Invoke();
        }

        // Test shortcut (F11): straight into Act 2 without the dream or the false ending.
        public void SkipToAct2()
        {
            var manager = AnomalyManager.Instance;
            if (Running || Returning || !manager || (ending && ending.Running))
                return;
            if (AlarmPanel.Instance)
                AlarmPanel.Instance.Close();
            if (SleepMeter.Instance)
                SleepMeter.Instance.Hide();
            manager.SetAct(GameAct.Reality);
            Begin();
        }

        public void ReturnToDream()
        {
            if (!Returning && isActiveAndEnabled)
                StartCoroutine(Fall());
        }

        IEnumerator Fall()
        {
            Returning = true;
            Running = false;
            var player = PlayerController.Instance;
            var fade = ScreenFade.Instance;
            var manager = AnomalyManager.Instance;
            if (SecurityMonitors.Instance)
                SecurityMonitors.Instance.Close(true);
            if (player)
                player.SetControl(false, false);
            if (reality)
                reality.Paused = true;
            if (source && sinkClip)
                source.PlayOneShot(sinkClip);

            float yaw = player ? player.Yaw : 0, pitch = player ? player.Pitch : 0;
            if (fade)
                fade.Caption = "03:13";
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

            // Under the black: Act 1 again, with a new code and the sleep bar already there.
            if (reality)
                reality.Hide();
            if (manager)
                manager.SetAct(GameAct.Dream);
            if (WakeCode.Instance)
                WakeCode.Instance.NewCode();
            if (AlarmPanel.Instance)
                AlarmPanel.Instance.ResetForDream();
            if (SleepMeter.Instance)
                SleepMeter.Instance.Reveal();
            Doors.CloseAll();
            if (player && spawn)
                player.Teleport(spawn.position, spawn.eulerAngles.y);
            Returns++;
            yield return new WaitForSeconds(blackSeconds);

            if (fade)
            {
                yield return fade.FadeTo(0, fadeInSeconds);
                fade.Caption = null;
            }

            if (player)
                player.SetControl(true);
            GameMessages.Show(backMessage, 4f);
            Returning = false;
            ReturnedToDream?.Invoke();
        }
    }
}
