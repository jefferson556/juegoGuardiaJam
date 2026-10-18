using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Turno0313
{
    // Final (Acto 2). Once the escaped anomaly is caught, the camera screen locks on CAM 06, the caseta: in the
    // chair, slumped on the desk, sits the guard himself, asleep. The camera pushes in, he lifts his head and
    // looks into the lens. Hard cut to black, then the real credits; any key afterwards starts the game again.
    public sealed class FinalSequence : MonoBehaviour
    {
        [SerializeField] SecurityCamera casetaCamera;
        [Tooltip("El guardia dormido: solo lo ve la cámara de la caseta. Apagado hasta el final.")]
        [SerializeField] GameObject sleepingGuard;
        [Tooltip("Pivote del cuello: gira para mirar a la cámara.")]
        [SerializeField] Transform guardHead;
        [Header("Tiempos (s)")]
        [SerializeField] float pushInSeconds = 5f;
        [Tooltip("Segundos que se queda mirando a la cámara antes del corte.")]
        [SerializeField] float holdSeconds = 1.6f;
        [SerializeField] float blackSeconds = 1.6f;
        [SerializeField] float creditsSpeed = 60f;
        [Tooltip("Apertura final de la cámara al acercarse (grados).")]
        [SerializeField] float closeFov = 24f;
        [Header("Créditos reales")]
        [Tooltip("La primera línea es el título.")]
        [TextArea(8, 20)]
        [SerializeField] string credits = "TURNO 03:13\n\nPROGRAMACIÓN\nJohn Orellana\nMiguel Navarrete\nJefferson Perez\n\nDISEÑO Y ARTE\nIvo Chiriboga\nElvis Reyes B\n\nQA Y PRODUCCIÓN\nGiovanni Bauz\n\nHecho para la jam «Something's Off»\n\nGracias por jugar";
        [SerializeField] AudioClip creditsMusic;
        [Tooltip("Al cortar a negro (vacío = estática sintetizada).")]
        [SerializeField] AudioClip staticClip;

        public static FinalSequence Instance { get; private set; }
        public bool Running { get; private set; }
        public event Action Finished;

        enum Phase
        {
            None,
            Black,
            Credits
        }

        Phase phase;
        float phaseStart;
        bool canLeave;
        string[] lines;
        AudioSource music, noise;
        GUIStyle titleStyle, lineStyle, hintStyle;
        EscapedAnomaly escaped;

        void Awake()
        {
            Instance = this;
            lines = credits.Replace("\r", "").Split('\n');
            music = Source2D();
            noise = Source2D();
            if (sleepingGuard)
                sleepingGuard.SetActive(false);
        }

        void Start()
        {
            escaped = EscapedAnomaly.Instance;
            if (escaped)
                escaped.Caught += Play;
        }

        void OnDestroy()
        {
            if (escaped)
                escaped.Caught -= Play;
            if (Instance == this)
                Instance = null;
        }

        public void Play()
        {
            if (!Running && isActiveAndEnabled)
                StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Running = true;
            var manager = AnomalyManager.Instance;
            if (manager)
                manager.AutoActivate = false;
            if (RealityMeter.Instance)
                RealityMeter.Instance.Paused = true;

            // 1. The screen locks on the caseta camera.
            var monitors = SecurityMonitors.Instance;
            if (monitors)
            {
                if (!monitors.IsOpen)
                    monitors.Open();
                monitors.Locked = false;
                for (int i = 0; i < SecurityCamera.All.Count; i++)
                    if (SecurityCamera.All[i] == casetaCamera)
                        monitors.Enlarge(i);
                monitors.Locked = true;
                monitors.Notice = null;
            }

            yield return new WaitForSeconds(1.2f);
            if (sleepingGuard)
                sleepingGuard.SetActive(true);
            if (monitors)
                monitors.Flash(casetaCamera, .35f);

            // 2. Push in; halfway he lifts his head and looks into the lens.
            var cam = casetaCamera ? casetaCamera.Camera : null;
            if (cam && guardHead)
            {
                float fov0 = cam.fieldOfView;
                var from = cam.transform.rotation;
                var to = Quaternion.LookRotation(guardHead.position - cam.transform.position);
                var down = guardHead.rotation;
                var up = Quaternion.LookRotation(cam.transform.position - guardHead.position, Vector3.up);
                for (float t = 0; t < pushInSeconds; t += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0, 1, t / pushInSeconds);
                    cam.fieldOfView = Mathf.Lerp(fov0, closeFov, k);
                    cam.transform.rotation = Quaternion.Slerp(from, to, k);
                    guardHead.rotation = Quaternion.Slerp(down, up, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, .75f, t / pushInSeconds)));
                    yield return null;
                }
            }

            // He holds the stare; the picture only starts to break up right before the cut.
            for (float t = 0; t < holdSeconds; t += Time.deltaTime)
            {
                if (monitors && t > holdSeconds - .5f && Time.frameCount % 5 == 0)
                    monitors.Flash(casetaCamera, .05f);
                yield return null;
            }

            // 3. Hard cut to black, a burst of static, then silence.
            var fade = ScreenFade.Instance;
            if (fade)
            {
                fade.Caption = null;
                fade.Alpha = 1;
            }

            if (monitors)
                monitors.Close(true);
            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(false, false);
            noise.clip = staticClip ? staticClip : SynthAudio.Static(.35f, .6f);
            noise.Play();
            SetPhase(Phase.Black);
            yield return new WaitForSeconds(blackSeconds);

            // 4. The real credits (this time they don't break).
            if (creditsMusic)
            {
                music.clip = creditsMusic;
                music.volume = .8f;
                music.Play();
            }

            SetPhase(Phase.Credits);
            yield return new WaitForSeconds(4f);
            canLeave = true;
            Finished?.Invoke();
        }

        void Update()
        {
            if (!canLeave)
                return;
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            var mouse = Mouse.current;
            bool any = (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                       || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                       || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
            if (any)
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void SetPhase(Phase value)
        {
            phase = value;
            phaseStart = Time.time;
        }

        void OnGUI()
        {
            if (phase == Phase.None)
                return;
            GUI.depth = -30;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (phase != Phase.Credits)
                return;
            if (titleStyle == null)
            {
                lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                titleStyle = new GUIStyle(lineStyle) { fontSize = 64, fontStyle = FontStyle.Bold };
                hintStyle = new GUIStyle(lineStyle) { fontSize = 18, normal = { textColor = new Color(.6f, .64f, .7f) } };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s, elapsed = Time.time - phaseStart;
            // The block rises into place and stays centred, above the hint line.
            float total = 0;
            for (int i = 0; i < lines.Length; i++)
                total += LineHeight(lines[i], i == 0);
            float rest = (1080 - 80 - total) / 2;
            float y = Mathf.Max(rest, rest + 240 - elapsed * creditsSpeed);
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(elapsed / 1.5f));
            for (int i = 0; i < lines.Length; i++)
            {
                float h = LineHeight(lines[i], i == 0);
                if (!string.IsNullOrEmpty(lines[i]))
                    GUI.Label(new Rect(0, y, w, h), lines[i], i == 0 ? titleStyle : lineStyle);
                y += h;
            }

            GUI.color = Color.white;
            if (canLeave)
                GUI.Label(new Rect(0, 1080 - 80, w, 40), "Pulsa cualquier tecla para volver a empezar", hintStyle);
            GUI.matrix = Matrix4x4.identity;
        }

        static float LineHeight(string line, bool title) => title ? 110 : string.IsNullOrEmpty(line) ? 24 : 46;

        AudioSource Source2D()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            return source;
        }
    }
}
