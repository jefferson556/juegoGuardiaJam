using System.Collections;
using Turno0313;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Piso13.Realidad
{
    /// <summary>
    /// Final del juego. Cuando el jugador reporta todas las anomalías reales de la escena (taza, reloj, persona
    /// elegante y máscara), todo se detiene, la pantalla se va a negro, salen los créditos y con cualquier tecla
    /// se vuelve al menú principal. La cafetera y el teléfono son falsas alarmas y no cuentan.
    /// Si se agrega otra anomalía real, súmala en Reported().
    /// </summary>
    public sealed class P13RealidadEnding : MonoBehaviour
    {
        public P13RealidadGame Game;
        public bool Running { get; private set; }
        // Para el HUD: "Anomalías 2/4".
        public int Found { get; private set; }
        public int Total { get; private set; } = 4;

        [Tooltip("Segundos para leer el último \"¡ANOMALÍA ENCONTRADA!\" antes del fundido.")]
        public float ReadSeconds = 1.8f;
        public float FadeSeconds = 2f;
        public float BlackSeconds = 1.2f;

        enum Phase
        {
            None,
            Fade,
            Credits
        }

        Phase phase;
        float alpha, creditsStart, checkTimer;
        bool canLeave;
        AudioSource music;

        // Cada anomalía real y si ya se reportó.
        bool[] Reported() => new[]
        {
            AnomalyReported("Taza de Café"),
            AnomalyReported("Reloj Pentagrama"),
            Game.PersonaAnomaly != null && Game.PersonaAnomaly.IsReported,
            Game.HumanMaskAnomaly != null && Game.HumanMaskAnomaly.IsReported,
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

        void Awake()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0;
            // Suena aunque el resto del audio esté en pausa.
            music.ignoreListenerPause = true;
        }

        void Update()
        {
            if (Running)
            {
                if (canLeave && AnyKey())
                    BackToMenu();
                return;
            }

            if (Game == null || Game.Phase != P13RealidadGame.GamePhase.Playing)
                return;
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0)
                return;
            checkTimer = .25f;
            var reported = Reported();
            int found = 0;
            foreach (bool done in reported)
                if (done)
                    found++;
            Found = found;
            Total = reported.Length;
            if (found == reported.Length)
                StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Running = true;
            // Todo se detiene: el guardia deja de cazar y el jugador ya no se mueve.
            Game.enabled = false;
            yield return new WaitForSeconds(ReadSeconds);

            phase = Phase.Fade;
            for (float t = 0; t < FadeSeconds; t += Time.deltaTime)
            {
                alpha = Mathf.SmoothStep(0, 1, t / FadeSeconds);
                yield return null;
            }

            alpha = 1;
            if (Game.Audio != null)
                Game.Audio.SetGameplay(false);
            AudioListener.pause = true;
            yield return new WaitForSeconds(BlackSeconds);

            var clip = Resources.Load<AudioClip>(CreditsRoll.Music);
            if (clip)
            {
                music.clip = clip;
                music.volume = .8f;
                music.Play();
            }

            phase = Phase.Credits;
            creditsStart = Time.time;
            yield return new WaitForSeconds(4f);
            canLeave = true;
        }

        public void BackToMenu()
        {
            AudioListener.pause = false;
            Game.CursorLock(false);
            SceneManager.LoadScene(MenuKit.TitleScene);
        }

        static bool AnyKey()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            return (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                   || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
        }

        void OnDestroy()
        {
            if (Running)
                AudioListener.pause = false;
        }

        void OnGUI()
        {
            if (phase == Phase.None)
                return;
            GUI.depth = -35;
            if (phase == Phase.Credits)
            {
                CreditsRoll.Draw(Time.time - creditsStart, canLeave ? InputPrompts.Pick("Pulsa cualquier tecla para volver al menú", "Pulsa cualquier botón para volver al menú") : null);
                return;
            }

            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
