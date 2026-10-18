using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Turno0313
{
    // Title screen and pause menu (Esc / Start). Temporary OnGUI menus, like the rest of the HUD (Jefferson/Elvis
    // can restyle them). The night only starts when the player presses Jugar. Runs between PlayerController (0)
    // and the screens that close with Esc (100), so Esc only pauses while the player is walking around.
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

        static readonly string[] TitleItems = { "Jugar", "Salir" };
        static readonly string[] PauseItems = { "Continuar", "Volver al título", "Salir" };

        int selected;
        bool autoActivate = true;
        GUIStyle titleStyle, subStyle, itemStyle, hintStyle;

        void Awake() => Instance = this;

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
            selected = 0;
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
            var manager = AnomalyManager.Instance;
            if (manager)
            {
                manager.AutoActivate = autoActivate;
                manager.ResetNight();
            }

            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(true);
            GameMessages.Show("WASD moverse · E interactuar · R reportar · F linterna · Esc pausa", 6f);
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused)
                return;
            Paused = paused;
            selected = 0;
            Time.timeScale = paused ? 0 : 1;
            AudioListener.pause = paused;
            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(!paused);
        }

        public void BackToTitle()
        {
            SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Choose(int item)
        {
            if (OnTitle)
            {
                if (item == 0)
                    StartGame();
                else
                    Quit();
                return;
            }

            if (item == 0)
                SetPaused(false);
            else if (item == 1)
                BackToTitle();
            else
                Quit();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            if (!OnTitle && !Paused)
            {
                // Esc only pauses while nobody else (panel, cameras, a scene) has the controls.
                var player = PlayerController.Instance;
                if (GameInput.Pause.WasPressedThisFrame() && player && player.ControlEnabled)
                    SetPaused(true);
                return;
            }

            if (Paused && ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && (pad.startButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame))))
            {
                SetPaused(false);
                return;
            }

            int count = OnTitle ? TitleItems.Length : PauseItems.Length;
            bool up = (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (keyboard != null && (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            if (up)
                selected = (selected + count - 1) % count;
            if (down)
                selected = (selected + 1) % count;
            bool confirm = (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm)
                Choose(selected);
        }

        void OnGUI()
        {
            if (!OnTitle && !Paused)
                return;
            GUI.depth = -40;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                subStyle = new GUIStyle(titleStyle) { fontSize = 24, fontStyle = FontStyle.Normal, normal = { textColor = new Color(.7f, .74f, .8f) } };
                itemStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
                hintStyle = new GUIStyle(subStyle) { fontSize = 16, normal = { textColor = new Color(.58f, .62f, .68f) } };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s;
            GUI.color = new Color(.01f, .015f, .02f, OnTitle ? .9f : .75f);
            GUI.DrawTexture(new Rect(0, 0, w, 1080), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string[] items;
            float y;
            if (OnTitle)
            {
                titleStyle.normal.textColor = new Color(1f, .25f, .16f);
                GUI.Label(new Rect(0, 250, w, 120), "TURNO 03:13", titleStyle);
                GUI.Label(new Rect(0, 370, w, 36), subtitle, subStyle);
                GUI.Label(new Rect(0, 420, w, 30), tagline, hintStyle);
                items = TitleItems;
                y = 520;
            }
            else
            {
                titleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(0, 300, w, 120), "PAUSA", titleStyle);
                items = PauseItems;
                y = 460;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var r = new Rect((w - 320) / 2, y + i * 70, 320, 54);
                if (i == selected)
                {
                    GUI.color = new Color(.55f, 1f, .7f, .9f);
                    GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (GUI.Button(r, items[i], itemStyle))
                    Choose(i);
            }

            GUI.Label(new Rect(0, 1080 - 110, w, 60),
                "WASD moverse · Shift correr · E interactuar · R o clic derecho reportar · F linterna · Esc pausa\nMando: sticks, A interactuar, Y reportar, RB linterna, Start pausa · Menú: ↑↓ y Enter, o A",
                hintStyle);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
