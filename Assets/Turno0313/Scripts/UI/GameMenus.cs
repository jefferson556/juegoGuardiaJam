using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turno0313
{
    // Title screen and pause menu (Esc / Start), with the Piso 13 look shared by every menu in the game (MenuKit).
    // The night starts on Iniciar demo. Runs before PlayerController so a menu press cannot
    // also move the player; panels that own control keep their own Escape handling.
    [DefaultExecutionOrder(-50)]
    public sealed class GameMenus : MonoBehaviour
    {
        [Tooltip("Mostrar la pantalla de título al empezar (apágalo para probar más rápido en el editor).")]
        [SerializeField] bool showTitle = true;
        public static GameMenus Instance { get; private set; }
        public bool OnTitle { get; private set; }
        public bool Paused { get; private set; }

        MenuKit kit;
        bool autoActivate = true, playerHadControl, confirmNew;
        public bool HasSession { get; private set; }
        public static bool StartOnNextLoad;
        public MenuKit Kit => kit;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { StartOnNextLoad = false; }

        void Awake()
        {
            Instance = this;
            kit = new MenuKit(gameObject);
        }

        void Start()
        {
            if (showTitle)
            {
                ShowTitle();
                if (StartOnNextLoad) { StartOnNextLoad = false; StartGame(); }
            }
            else HasSession = true;
        }

        void OnDestroy()
        {
            kit.Dispose();
            if (Paused || OnTitle) { Time.timeScale = 1; AudioListener.pause = false; }
            if (Instance == this)
                Instance = null;
        }

        void ShowTitle()
        {
            OnTitle = true;
            Paused = false;
            Time.timeScale = 0;
            AudioListener.pause = true;
            kit.Reset();
            kit.Music(true);
            var manager = AnomalyManager.Instance;
            if (manager && !HasSession)
            {
                autoActivate = manager.AutoActivate;
                manager.AutoActivate = false;
            }

            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(false);
        }

        // Jugar: the night starts now (clock, first anomaly in 20 s...).
        public void StartGame()
        {
            if (!OnTitle)
                return;
            OnTitle = false;
            HasSession = true;
            Time.timeScale = 1;
            AudioListener.pause = false;
            kit.Music(false);
            var manager = AnomalyManager.Instance;
            if (manager)
            {
                manager.AutoActivate = autoActivate;
                manager.ResetNight();
            }

            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(true);
            GameMessages.Show("{Move} moverse · {Interact} interactuar · {Report} reportar · {Flashlight} linterna · {Notes} bitácora · {Pause} pausa", 6f);
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused)
                return;
            if (OnTitle) return;
            if (paused) playerHadControl = PlayerController.Instance && PlayerController.Instance.ControlEnabled;
            Paused = paused;
            kit.Reset();
            Time.timeScale = paused ? 0 : 1;
            AudioListener.pause = paused;
            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(!paused && playerHadControl);
        }

        public void BackToTitle()
        {
            if (!OnTitle)
            {
                if (!Paused) playerHadControl = PlayerController.Instance && PlayerController.Instance.ControlEnabled;
                ShowTitle();
            }
        }

        public void ContinueDemo()
        {
            if (!OnTitle || !HasSession) return;
            OnTitle = Paused = false; kit.Reset(); kit.Music(false);
            Time.timeScale = 1; AudioListener.pause = false;
            if (PlayerController.Instance) PlayerController.Instance.SetControl(playerHadControl);
        }
        public void RequestNewDemo()
        {
            if (HasSession) { confirmNew = true; kit.Reset(); }
            else StartGame();
        }
        public void RestartDemo()
        {
            StartOnNextLoad = true; Time.timeScale = 1; AudioListener.pause = false;
            SceneManager.LoadScene(MenuKit.TitleScene);
        }
        public void Quit() => MenuKit.Quit();

        void Update()
        {
            kit.Tick();
            if (kit.HandleOptionsBack()) return;
            if (confirmNew && MenuKit.BackPressed()) { confirmNew = false; kit.Reset(); return; }
            if (!OnTitle && !Paused)
            {
                // Esc only pauses while nobody else (panel, cameras, a scene) has the controls.
                var player = PlayerController.Instance;
                if (GameInput.Pause.WasPressedThisFrame() && player && player.ControlEnabled)
                    SetPaused(true);
                return;
            }

            if (Paused && MenuKit.BackPressed())
            {
                SetPaused(false);
                return;
            }

            kit.Navigate();
        }

        void OnGUI()
        {
            if (!OnTitle && !Paused)
                return;
            GUI.depth = -40;
            kit.Begin();
            if (kit.OptionsOpen) kit.OptionsScreen();
            else if (OnTitle) TitleScreen();
            else
                kit.PauseScreen(() => SetPaused(false), BackToTitle);
            kit.End();
        }

        void TitleScreen()
        {
            if (confirmNew)
            {
                kit.Shade(1); float x = (kit.Width - 500) / 2;
                kit.Heading(new Rect(x, 230, 500, 80), "¿Iniciar de nuevo la demo?");
                kit.Text(new Rect(x, 310, 500, 60), "Se perderá el progreso de esta sesión.");
                kit.Button(new Rect(x, 405, 240, 48), "Cancelar", () => { confirmNew = false; kit.Reset(); });
                kit.Button(new Rect(x + 260, 405, 240, 48), "Iniciar demo", RestartDemo, selected: true);
            }
            else kit.DemoTitle(HasSession, RequestNewDemo, ContinueDemo);
        }
    }
}
