using System;
using System.Collections;
using UnityEngine;

namespace Turno0313
{
    // Falso final (transición al Acto 2). The right code on the alarm panel wakes the guard up: he opens his
    // eyes at the desk with the alarm ringing ("Solo fue un sueño."), the screen fades to credits... and a few
    // seconds later they break with a glitch and the game goes on, now in reality:
    // AnomalyManager.SetAct(Reality) and the Finished event, which is where Act 2 (Jefferson) starts.
    public sealed class FalseEnding : MonoBehaviour
    {
        [Tooltip("Dónde despierta el guardia: de pie junto a su escritorio, mirando los monitores.")]
        [SerializeField] Transform wakePoint;
        [Header("Tiempos (s)")]
        [SerializeField] float dreamFadeOut = .8f;
        [SerializeField] float eyesOpening = 2.6f;
        [SerializeField] float awakeHold = 2.2f;
        [SerializeField] float creditsFadeIn = 1.6f;
        [Tooltip("GDD: se cortan a los 5 s, antes de que al jugador le dé tiempo a cerrar el juego.")]
        [SerializeField] float creditsSeconds = 5f;
        [SerializeField] float glitchSeconds = 1.4f;
        [Header("Texto")]
        [SerializeField] string wakeMessage = "Solo fue un sueño.";
        [Tooltip("La primera línea es el título.")]
        [TextArea(8, 20)]
        [SerializeField] string credits = "TURNO 03:13\n\nPROGRAMACIÓN\nJohn Orellana\nMiguel Navarrete\nJefferson Perez\n\nDISEÑO Y ARTE\nIvo Chiriboga\nElvis Reyes B\n\nQA Y PRODUCCIÓN\nGiovanni Bauz\n\nGracias por jugar";
        [SerializeField] float creditsSpeed = 38f;
        [Header("Audio (vacío = sonido sintetizado)")]
        [SerializeField] AudioClip alarmClip;
        [SerializeField] AudioClip creditsMusic;
        [SerializeField] AudioClip staticClip;

        public static FalseEnding Instance { get; private set; }
        public bool Running { get; private set; }
        // Control is back and the act is Reality: Act 2 starts here.
        public event Action Finished;

        enum Phase
        {
            None,
            Credits,
            Glitch,
            Static
        }

        // Eyes opening: black, half open, closed again, open.
        static readonly AnimationCurve Blink = new AnimationCurve(
            new Keyframe(0, 1), new Keyframe(.18f, .4f), new Keyframe(.3f, .85f), new Keyframe(.55f, .2f), new Keyframe(.68f, .5f), new Keyframe(1, 0));

        Phase phase;
        float phaseStart;
        AudioSource alarm, music, noise;
        Texture2D noiseTexture;
        Color32[] noisePixels;
        string[] lines;
        GUIStyle titleStyle, lineStyle;
        AlarmPanel panel;

        void Awake()
        {
            Instance = this;
            alarm = Source2D();
            music = Source2D();
            noise = Source2D();
            lines = credits.Replace("\r", "").Split('\n');
            noiseTexture = new Texture2D(160, 90, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            noisePixels = new Color32[160 * 90];
        }

        void Start()
        {
            panel = AlarmPanel.Instance;
            if (panel)
                panel.Solved += Play;
        }

        void OnDestroy()
        {
            if (panel)
                panel.Solved -= Play;
            if (noiseTexture)
                Destroy(noiseTexture);
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
            var player = PlayerController.Instance;
            var fade = ScreenFade.Instance;
            var manager = AnomalyManager.Instance;
            bool autoActivate = manager && manager.AutoActivate;
            if (player)
                player.SetControl(false, false);

            // 1. The dream switches off; under the black the floor goes back to normal.
            if (fade)
            {
                fade.Caption = null;
                yield return fade.FadeTo(1, dreamFadeOut);
            }

            if (SleepMeter.Instance)
                SleepMeter.Instance.Hide();
            if (manager)
            {
                manager.AutoActivate = false;
                manager.ResetNight();
            }

            foreach (var door in FindObjectsByType<Door>(FindObjectsInactive.Exclude))
                door.ResetClosed();
            float yaw = wakePoint ? wakePoint.eulerAngles.y : player ? player.Yaw : 0;
            if (player)
                player.Teleport(wakePoint ? wakePoint.position : player.transform.position, yaw, 50f);
            yield return new WaitForSeconds(.6f);

            // 2. He opens his eyes at the desk, head coming up, with the alarm ringing.
            alarm.clip = alarmClip ? alarmClip : SynthAudio.AlarmBeeps();
            alarm.loop = true;
            alarm.volume = 1;
            alarm.Play();
            for (float t = 0; t < eyesOpening; t += Time.deltaTime)
            {
                float k = t / eyesOpening;
                if (fade)
                    fade.Alpha = Blink.Evaluate(k);
                if (player)
                    player.SetLook(yaw, Mathf.Lerp(50f, 8f, Mathf.SmoothStep(0, 1, k)));
                yield return null;
            }

            if (fade)
                fade.Alpha = 0;
            GameMessages.Show(wakeMessage, awakeHold + .5f);
            yield return new WaitForSeconds(awakeHold);

            // 3. The fake ending: to black, credits and music.
            for (float t = 0; t < creditsFadeIn; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / creditsFadeIn);
                if (fade)
                    fade.Alpha = k;
                alarm.volume = 1 - k;
                yield return null;
            }

            alarm.Stop();
            if (fade)
                fade.Alpha = 1;
            if (creditsMusic)
            {
                music.clip = creditsMusic;
                music.volume = .8f;
                music.pitch = 1;
                music.Play();
            }

            SetPhase(Phase.Credits);
            yield return new WaitForSeconds(creditsSeconds);

            // 4. The credits break.
            SetPhase(Phase.Glitch);
            noise.clip = staticClip ? staticClip : SynthAudio.Static(glitchSeconds + .4f);
            noise.Play();
            for (float t = 0; t < glitchSeconds; t += Time.deltaTime)
            {
                // The music drags and stutters.
                if (Time.frameCount % 4 == 0)
                    music.pitch = Mathf.Lerp(1f, .5f, t / glitchSeconds) + UnityEngine.Random.Range(-.15f, .15f);
                yield return null;
            }

            music.Stop();
            music.pitch = 1;

            // 5. Hard cut back to the game, in reality.
            SetPhase(Phase.Static);
            if (fade)
                fade.Alpha = 0;
            if (manager)
            {
                manager.SetAct(GameAct.Reality);
                manager.AutoActivate = autoActivate;
            }

            if (player)
                player.SetControl(true);
            Running = false;
            Finished?.Invoke();
            yield return new WaitForSeconds(.5f);
            SetPhase(Phase.None);
        }

        void SetPhase(Phase value)
        {
            phase = value;
            phaseStart = Time.time;
        }

        void Update()
        {
            if (phase != Phase.Glitch && phase != Phase.Static)
                return;
            for (int i = 0; i < noisePixels.Length; i++)
            {
                byte v = (byte)UnityEngine.Random.Range(0, 256);
                noisePixels[i] = new Color32(v, v, v, 255);
            }

            noiseTexture.SetPixels32(noisePixels);
            noiseTexture.Apply(false);
        }

        void OnGUI()
        {
            if (phase == Phase.None)
                return;
            GUI.depth = -20;
            GUI.matrix = Matrix4x4.identity;
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            float elapsed = Time.time - phaseStart;

            if (phase == Phase.Static)
            {
                // Static over the game for a moment after the cut.
                GUI.color = new Color(1, 1, 1, .7f * (1 - elapsed / .5f));
                GUI.DrawTexture(screen, noiseTexture);
                GUI.color = Color.white;
                return;
            }

            GUI.color = Color.black;
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (titleStyle == null)
            {
                lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                titleStyle = new GUIStyle(lineStyle) { fontSize = 64, fontStyle = FontStyle.Bold };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s;
            bool glitch = phase == Phase.Glitch;
            float g = glitch ? Mathf.Clamp01(elapsed / glitchSeconds) : 0;
            int frame = Time.frameCount / 2;
            // While glitching the scroll stutters back and forth.
            float scroll = (glitch ? creditsSeconds - (Hash(frame, 7) - .5f) * 1.5f * g : elapsed) * creditsSpeed;
            float y = 1080 * .4f - scroll;
            for (int i = 0; i < lines.Length; i++)
            {
                bool isTitle = i == 0;
                float h = isTitle ? 110 : string.IsNullOrEmpty(lines[i]) ? 24 : 46;
                var style = isTitle ? titleStyle : lineStyle;
                var r = new Rect(0, y, w, h);
                y += h;
                if (string.IsNullOrEmpty(lines[i]))
                    continue;
                if (glitch)
                {
                    // Some lines jump sideways; every line splits into red and cyan.
                    if (Hash(i, frame) < .35f + .4f * g)
                        r.x += (Hash(i * 3, frame) - .5f) * 180 * g;
                    float split = 3 + 9 * g;
                    Colored(new Rect(r.x - split, r.y, r.width, r.height), lines[i], style, new Color(1, .1f, .1f, .75f));
                    Colored(new Rect(r.x + split, r.y, r.width, r.height), lines[i], style, new Color(.1f, 1, 1, .75f));
                }

                Colored(r, lines[i], style, Color.white);
            }

            if (glitch)
            {
                // Bands of static, more and wider as it breaks down.
                GUI.matrix = Matrix4x4.identity;
                int bands = 2 + Mathf.RoundToInt(8 * g);
                for (int b = 0; b < bands; b++)
                {
                    float by = Hash(b, frame * 5) * Screen.height, bh = (4 + Hash(b * 7, frame) * 70 * g) * s;
                    GUI.color = new Color(1, 1, 1, .5f + .4f * g);
                    GUI.DrawTexture(new Rect(0, by, Screen.width, bh), noiseTexture);
                }

                // The last moments flash to full static.
                if (g > .85f && frame % 2 == 0)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(screen, noiseTexture);
                }
            }

            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.identity;
        }

        static void Colored(Rect r, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        // Stable pseudo-random 0-1 for a line and a frame (OnGUI runs several times per frame).
        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xffff) / 65535f;
            }
        }

        AudioSource Source2D()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            return source;
        }
    }
}
