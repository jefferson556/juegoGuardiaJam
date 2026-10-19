using System.Collections;
using Turno0313;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Piso13.Realidad
{
    /// <summary>
    /// Seguimiento de progreso de anomalías del juego.
    /// Informa al HUD la cantidad de anomalías encontradas (Found / Total)
    /// y notifica a P13RealidadGame cuando se han reportado todas para iniciar la secuencia final.
    /// </summary>
    public sealed class P13RealidadEnding : MonoBehaviour
    {
        public P13RealidadGame Game;
        public bool Running { get; private set; }
        // Para el HUD: "Anomalías 2/4".
        public int Found { get; private set; }
        public int Total { get; private set; } = 4;

        float checkTimer;

        // Cada anomalía real y si ya se reportó.
        bool[] Reported() => new[]
        {
            AnomalyReported("Taza de Café"),
            AnomalyReported("Reloj Pentagrama"),
            Game != null && Game.PersonaAnomaly != null && Game.PersonaAnomaly.IsReported,
            Game != null && Game.HumanMaskAnomaly != null && Game.HumanMaskAnomaly.IsReported,
        };

        // Las anomalías hechas con P13Anomaly (taza, reloj) cuelgan del mundo; al reportarlas se desactivan.
        bool AnomalyReported(string id)
        {
            var root = Game.World != null ? Game.World.Root : null;
            if (root == null)
                return false;
            foreach (var anomaly in root.GetComponentsInChildren<P13Anomaly>(true))
                if (anomaly.AnomalyId == id && anomaly.IsReported)
                    return true;
            return false;
        }

        void Update()
        {
            if (Game == null || Game.Phase != P13RealidadGame.GamePhase.Playing)
                return;

            checkTimer -= Time.deltaTime;
            if (checkTimer > 0)
                return;
            checkTimer = 0.25f;

            var reported = Reported();
            int found = 0;
            foreach (bool done in reported)
                if (done)
                    found++;

            Found = found;
            Total = reported.Length;

            if (found == reported.Length)
            {
                if (Game != null && !Game.EndingSequenceTriggered)
                {
                    Game.TriggerAllAnomaliesCompleted();
                }
            }
        }

        public void BackToMenu()
        {
            AudioListener.pause = false;
            if (Game != null) Game.CursorLock(false);
            SceneManager.LoadScene(MenuKit.TitleScene);
        }

        void OnGUI()
        {
            // El corte a negro y la pantalla de fin de demo la dibuja P13RealidadUI en GamePhase.DemoEnded
        }
    }
}
