using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turno0313
{
    // Final (Acto 2). Once the escaped anomaly is caught, the camera screen locks on CAM 06, the caseta: in the
    // chair, slumped on the desk, sits the guard himself, asleep. The camera pushes in, he lifts his head and
    // looks into the lens. Hard cut to black and static, one line on the black ("Despierta.") and the game goes
    // on in Jefferson's scene (Piso13 Realidad), where the guard wakes up. The credits are at the end of that scene.
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
        [Tooltip("Negro con estática antes del texto.")]
        [SerializeField] float blackSeconds = 1.6f;
        [Tooltip("Segundos que el texto se queda en pantalla, sin contar los fundidos.")]
        [SerializeField] float textSeconds = 2.2f;
        [Tooltip("Apertura final de la cámara al acercarse (grados).")]
        [SerializeField] float closeFov = 24f;
        [Header("Paso a la escena de Jefferson")]
        [Tooltip("La línea sobre el negro antes de despertar.")]
        [SerializeField] string wakeText = "Despierta.";
        [SerializeField] string nextScene = "Piso13 Realidad";
        [Tooltip("Al cortar a negro (vacío = estática sintetizada).")]
        [SerializeField] AudioClip staticClip;

        public static FinalSequence Instance { get; private set; }
        public bool Running { get; private set; }
        // Right before the next scene takes over.
        public event Action Finished;

        bool black;
        float textAlpha;
        AudioSource noise;
        GUIStyle textStyle;
        EscapedAnomaly escaped;

        void Awake()
        {
            Instance = this;
            noise = gameObject.AddComponent<AudioSource>();
            noise.playOnAwake = false;
            noise.spatialBlend = 0;
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

            // 3. Hard cut to black, a burst of static, then silence. The next scene loads behind the black.
            var load = SceneManager.LoadSceneAsync(nextScene);
            if (load != null)
                load.allowSceneActivation = false;
            else
                Debug.LogError($"[FinalSequence] La escena «{nextScene}» no está en Build Settings.");

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
            black = true;
            yield return new WaitForSeconds(blackSeconds);

            // 4. One line on the black, then the guard wakes up in the next scene.
            yield return FadeText(1, 1.2f);
            yield return new WaitForSeconds(textSeconds);
            yield return FadeText(0, 1f);
            yield return new WaitForSeconds(.4f);
            Finished?.Invoke();
            if (load != null)
                load.allowSceneActivation = true;
        }

        IEnumerator FadeText(float target, float seconds)
        {
            float start = textAlpha;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                textAlpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }

            textAlpha = target;
        }

        void OnGUI()
        {
            if (!black)
                return;
            GUI.depth = -30;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (textAlpha > 0 && !string.IsNullOrEmpty(wakeText))
            {
                // Same look as the "03:13" on the black between nights.
                textStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                textStyle.fontSize = Mathf.RoundToInt(80 * Screen.height / 1080f);
                textStyle.normal.textColor = new Color(1f, .2f, .12f);
                GUI.color = new Color(1, 1, 1, textAlpha);
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), wakeText, textStyle);
            }

            GUI.color = Color.white;
        }
    }
}
