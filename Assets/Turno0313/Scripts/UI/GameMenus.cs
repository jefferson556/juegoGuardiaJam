using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turno0313
{
    // Title screen and pause menu (Esc / Start), with the Piso 13 look shared by every menu in the game (MenuKit).
    // The night only starts when the player presses Jugar. Runs between PlayerController (0) and the screens that
    // close with Esc (100), so Esc only pauses while the player is walking around.
    [DefaultExecutionOrder(50)]
    public sealed class GameMenus : MonoBehaviour
    {
        [Tooltip("Mostrar la pantalla de título al empezar (apágalo para probar más rápido en el editor).")]
        [SerializeField] bool showTitle = true;
        [SerializeField] string subtitle = "Something's Off";
        [SerializeField] string tagline = "Eres el guardia de noche. El reloj marca 03:13 y no avanza.";

        public static GameMenus Instance { get; private set; }
        public bool OnTitle { get; private set; }
        public bool Paused { get; private set; }

        // Tokens: InputPrompts shows the keys or the buttons of the gamepad in use.
        const string Controls = "{Move} moverse  ·  {Look} mirar  ·  {Run} correr\n{Interact} interactuar  ·  {Report} reportar  ·  {Flashlight} linterna\n{Notes} bitácora  ·  {Pause} pausa";

        MenuKit kit;
        bool autoActivate = true;

        void Awake()
        {
            Instance = this;
            kit = new MenuKit(gameObject);
        }

        void Start()
        {
            if (showTitle)
                ShowTitle();
        }

        void OnDestroy()
        {
            if (Paused)
                SetPaused(false);
            if (Instance == this)
                Instance = null;
        }

        void ShowTitle()
        {
            OnTitle = true;
            kit.Reset();
            kit.Music(true);
            var manager = AnomalyManager.Instance;
            if (manager)
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
            Paused = paused;
            kit.Reset();
            Time.timeScale = paused ? 0 : 1;
            AudioListener.pause = paused;
            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(!paused);
        }

        public void BackToTitle()
        {
            SetPaused(false);
            SceneManager.LoadScene(MenuKit.TitleScene);
        }

        public void Quit() => MenuKit.Quit();

        void Update()
        {
            kit.Tick();
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
            if (OnTitle)
                TitleScreen();
            else
                kit.PauseScreen(() => SetPaused(false), BackToTitle);
            kit.End();
        }

        void TitleScreen()
        {
            kit.CoverWithPanel(550);
            kit.Title(new Rect(70, 90, 460, 90), "TURNO 03:13", MenuKit.TitleRed);
            kit.Heading(new Rect(74, 178, 430, 32), subtitle);
            kit.Text(new Rect(74, 214, 430, 60), tagline);
            kit.Button(new Rect(70, 300, 390, 48), "Jugar", StartGame);
            kit.Button(new Rect(70, 362, 390, 48), "Salir", Quit);
            kit.Prompts(new Rect(74, 612, 470, 80), Controls);
        }
    }
}
