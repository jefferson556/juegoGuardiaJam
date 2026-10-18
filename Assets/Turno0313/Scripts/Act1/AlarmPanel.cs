using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turno0313
{
    public enum PanelFeedback
    {
        None,
        Wrong,
        FalseLead,
        Correct
    }

    // Panel de alarma de la caseta (puzzle de despertar). Once the guard knows he is dreaming (sleep bar
    // on screen) E opens a 4-digit keypad with a timer. The right code wakes him up (Solved); a wrong code
    // or running out of time pushes the sleep bar up. 0313, the time on the frozen clock, is the false lead.
    // Runs after PlayerController so closing with Esc doesn't also release the cursor.
    [DefaultExecutionOrder(100)]
    public sealed class AlarmPanel : Interactable
    {
        public const int Length = 4;
        // On-screen keypad, row by row: '<' = borrar, 'x' = salir.
        public const string Keys = "123456789<0x";

        [Header("Puzzle")]
        [Tooltip("Segundos para meter el código cada vez que se abre el panel.")]
        [SerializeField] float timeLimit = 25f;
        [Tooltip("Lo que sube la barra de sueño con un código equivocado (0 a 1).")]
        [SerializeField] float wrongCodePenalty = .08f;
        [Tooltip("Lo que sube la barra de sueño si se acaba el tiempo (0 a 1).")]
        [SerializeField] float timeoutPenalty = .15f;
        [Header("Escena")]
        [SerializeField] TextMesh screen;
        [Tooltip("Parpadea cuando el panel espera el código.")]
        [SerializeField] Renderer led;
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip keyClip;
        [SerializeField] AudioClip correctClip;
        [SerializeField] AudioClip wrongClip;

        public static AlarmPanel Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsSolved { get; private set; }
        public string Entered { get; private set; } = "";
        public float TimeLimit => timeLimit;
        public float TimeLeft { get; private set; }
        public PanelFeedback Feedback { get; private set; }
        // Key under the gamepad cursor (index in Keys).
        public int Selected { get; private set; } = 4;
        // Waiting for the code: the player knows he is dreaming and hasn't woken up yet.
        public bool Armed => !IsSolved && (!sleep || sleep.Shown);
        // Checking the code or showing the result: keys are ignored.
        public bool Busy => checkAt > 0 || Feedback != PanelFeedback.None;

        public override string Prompt => "Usar panel de alarma";

        public event Action Opened;
        public event Action Closed;
        public event Action Solved;
        public event Action<string> WrongCode;

        static readonly Key[] digitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] numpadKeys = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        SleepMeter sleep;
        AnomalyManager manager;
        float checkAt, feedbackUntil;
        int openedFrame;

        void Awake()
        {
            Instance = this;
            // Until Elvis has a real panel sound.
            if (!wrongClip)
                wrongClip = SynthAudio.Buzz();
        }

        void Start()
        {
            sleep = SleepMeter.Instance;
            manager = AnomalyManager.Instance;
            if (manager)
                manager.NightReset += OnNightReset;
            RefreshScreen();
        }

        void OnDestroy()
        {
            if (manager)
                manager.NightReset -= OnNightReset;
            if (Instance == this)
                Instance = null;
        }

        public override void Interact(PlayerController player)
        {
            if (manager && manager.Act != GameAct.Dream)
            {
                Play(keyClip);
                GameMessages.Show("Todo en orden. No hace falta tocar la alarma.");
                return;
            }

            if (IsSolved)
            {
                GameMessages.Show("La alarma ya está sonando.");
                return;
            }

            if (!Armed)
            {
                Play(keyClip);
                GameMessages.Show("Todo en orden. No hace falta tocar la alarma.");
                return;
            }

            IsOpen = true;
            openedFrame = Time.frameCount;
            TimeLeft = timeLimit;
            Entered = "";
            Feedback = PanelFeedback.None;
            checkAt = 0;
            Selected = 4;
            player.SetControl(false);
            RefreshScreen();
            Opened?.Invoke();
        }

        void Update()
        {
            if (led)
                led.enabled = !Armed || Time.time % 1f < .5f;
            if (!IsOpen)
                return;

            if (checkAt > 0 && Time.time >= checkAt)
            {
                checkAt = 0;
                Check();
            }

            if (Feedback != PanelFeedback.None && Time.time >= feedbackUntil)
            {
                if (Feedback == PanelFeedback.Correct)
                {
                    Close();
                    Solved?.Invoke();
                    return;
                }

                Feedback = PanelFeedback.None;
                Entered = "";
                RefreshScreen();
            }

            if (Feedback != PanelFeedback.Correct)
            {
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0)
                {
                    TimeOut();
                    return;
                }
            }

            // The key that opened the panel (gamepad A) must not also press a digit.
            if (Time.frameCount != openedFrame)
                ReadInput();
        }

        // Keypad input from the HUD (mouse) or from the keyboard and gamepad: '0'-'9', '<' borrar, 'x' salir.
        public void Press(char key)
        {
            if (!IsOpen)
                return;
            if (key == 'x')
            {
                if (Feedback != PanelFeedback.Correct)
                    Close();
                return;
            }

            if (Busy)
                return;
            if (key == '<')
            {
                if (Entered.Length > 0)
                    Entered = Entered.Substring(0, Entered.Length - 1);
                Play(keyClip);
            }
            else if (key >= '0' && key <= '9' && Entered.Length < Length)
            {
                Entered += key;
                Play(keyClip);
                // A short pause so the fourth digit shows before the result.
                if (Entered.Length == Length)
                    checkAt = Time.time + .35f;
            }

            RefreshScreen();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            checkAt = 0;
            Entered = "";
            if (Feedback != PanelFeedback.Correct)
                Feedback = PanelFeedback.None;
            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(true);
            RefreshScreen();
            Closed?.Invoke();
        }

        void Check()
        {
            var wake = WakeCode.Instance;
            if (wake && Entered == wake.Code)
            {
                IsSolved = true;
                Feedback = PanelFeedback.Correct;
                feedbackUntil = Time.time + 1.2f;
                if (sleep)
                    sleep.Paused = true;
                Play(correctClip);
            }
            else
            {
                Feedback = wake && Entered == wake.FalseLead ? PanelFeedback.FalseLead : PanelFeedback.Wrong;
                feedbackUntil = Time.time + (Feedback == PanelFeedback.FalseLead ? 2.2f : 1f);
                Play(wrongClip);
                if (sleep)
                    sleep.Add(wrongCodePenalty);
                WrongCode?.Invoke(Entered);
            }

            RefreshScreen();
        }

        void TimeOut()
        {
            Close();
            Play(wrongClip);
            GameMessages.Show("Se acabó el tiempo.");
            if (sleep)
                sleep.Add(timeoutPenalty);
        }

        void ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
                {
                    Press('x');
                    return;
                }

                if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
                    Press('<');
                for (int d = 0; d < 10; d++)
                    if (keyboard[digitKeys[d]].wasPressedThisFrame || keyboard[numpadKeys[d]].wasPressedThisFrame)
                        Press((char)('0' + d));
            }

            var pad = Gamepad.current;
            if (pad == null)
                return;
            if (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)
            {
                Press('x');
                return;
            }

            if (pad.buttonWest.wasPressedThisFrame)
                Press('<');
            int col = Selected % 3, row = Selected / 3;
            if (pad.dpad.left.wasPressedThisFrame)
                col = (col + 2) % 3;
            if (pad.dpad.right.wasPressedThisFrame)
                col = (col + 1) % 3;
            if (pad.dpad.up.wasPressedThisFrame)
                row = (row + 3) % 4;
            if (pad.dpad.down.wasPressedThisFrame)
                row = (row + 1) % 4;
            Selected = row * 3 + col;
            if (pad.buttonSouth.wasPressedThisFrame)
                Press(Keys[Selected]);
        }

        // "4 7 _ _": what has been typed so far.
        public string Masked()
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < Length; i++)
            {
                if (i > 0)
                    text.Append(' ');
                text.Append(i < Entered.Length ? Entered[i] : '_');
            }

            return text.ToString();
        }

        void RefreshScreen()
        {
            if (!screen)
                return;
            if (IsSolved)
                screen.text = "OK";
            else if (Feedback == PanelFeedback.Wrong || Feedback == PanelFeedback.FalseLead)
                screen.text = "ERROR";
            else
                screen.text = IsOpen ? Masked() : "- - - -";
        }

        // Waiting for a code again (back in the dream after Act 2).
        public void ResetForDream()
        {
            Close();
            IsSolved = false;
            Feedback = PanelFeedback.None;
            RefreshScreen();
        }

        void OnNightReset()
        {
            Close();
            Feedback = PanelFeedback.None;
            RefreshScreen();
        }

        void Play(AudioClip clip)
        {
            if (source && clip)
                source.PlayOneShot(clip);
        }
    }
}
