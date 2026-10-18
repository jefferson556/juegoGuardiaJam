using Turno0313;
using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Menús de la escena Realidad con el mismo estilo que el resto del juego (MenuKit):
    /// - Pausa con Esc / Start (no mientras están las cámaras o la advertencia en pantalla).
    /// - Pantalla de "Fin del turno" cuando el guardia te atrapa.
    /// También agrega el despertar al empezar (WakeUpFade) y el final con créditos (P13RealidadEnding).
    /// Corre antes que P13RealidadGame, para que el Esc que abre la pausa no llegue también al juego.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class P13RealidadMenus : MonoBehaviour
    {
        public P13RealidadGame Game;
        public bool Paused { get; private set; }
        // Créditos al reportar todas las anomalías reales.
        public P13RealidadEnding Ending { get; private set; }

        MenuKit kit;
        bool wasLost;

        bool Lost => Game != null && Game.Phase == P13RealidadGame.GamePhase.Lost;

        void Awake()
        {
            kit = new MenuKit(gameObject);
            // El guardia despierta: la escena empieza con los ojos cerrados y se abren (viene del final del acto 2).
            gameObject.AddComponent<WakeUpFade>();
        }

        // En Start, porque P13RealidadGame asigna Game después de crear este componente.
        void Start()
        {
            Ending = gameObject.AddComponent<P13RealidadEnding>();
            Ending.Game = Game;
        }

        void OnDestroy()
        {
            if (Paused)
            {
                Time.timeScale = 1;
                AudioListener.pause = false;
            }
        }

        void Update()
        {
            kit.Tick();
            if (Game == null || (Ending != null && Ending.Running))
                return;

            if (Lost != wasLost)
            {
                wasLost = Lost;
                kit.Reset();
            }

            if (Lost)
            {
                kit.Navigate();
                return;
            }

            if (Paused)
            {
                if (MenuKit.BackPressed())
                    SetPaused(false);
                else
                    kit.Navigate();
                return;
            }

            if (MenuKit.PausePressed() && Game.Phase == P13RealidadGame.GamePhase.Playing && !Game.CCTV && !Game.ShowModal)
                SetPaused(true);
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused || Game == null)
                return;
            Paused = paused;
            kit.Reset();
            Time.timeScale = paused ? 0 : 1;
            AudioListener.pause = paused;
            // Sin Update el juego no lee el ratón ni el teclado mientras está en pausa.
            Game.enabled = !paused;
            Game.CursorLock(!paused);
        }

        void MainMenu()
        {
            SetPaused(false);
            Game.LoadMainMenu();
        }

        void OnGUI()
        {
            if (Game == null || (!Paused && !Lost))
                return;
            GUI.depth = -40;
            kit.Begin();
            if (Lost)
                EndScreen();
            else
                kit.PauseScreen(() => SetPaused(false), MainMenu);
            kit.End();
        }

        void EndScreen()
        {
            kit.Shade(.94f);
            float x = (kit.Width - 840) / 2;
            kit.Title(new Rect(x, 160, 840, 130), "FIN DEL TURNO");
            kit.Text(new Rect(x + 5, 335, 800, 75), "El guardia te ha encontrado.");
            kit.Button(new Rect(x + 5, 430, 420, 50), "Reintentar", Game.RestartSession);
            kit.Button(new Rect(x + 5, 495, 420, 50), "Menú principal", Game.LoadMainMenu);
        }
    }
}
