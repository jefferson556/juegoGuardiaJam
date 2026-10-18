using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turno0313
{
    // Something seen through the cameras that answers a report itself (the escaped anomaly).
    // 'message' replaces the screen's usual line when it is not null.
    public interface ICameraTarget
    {
        ReportResult ReportFromCamera(SecurityCamera cam, out string message);
    }

    // Monitores de la caseta (Acto 2). E opens the camera screen: a grid with every security camera; pick one to
    // see it big, then click on what looks wrong to report it (AnomalyManager, source Camera). In the dream they
    // only show static. Temporary OnGUI screen; the final UI (Elvis) can drive Open/Enlarge/ReportAt instead.
    // Runs after PlayerController so closing with Esc doesn't also free the cursor.
    [DefaultExecutionOrder(100)]
    public sealed class SecurityMonitors : Interactable
    {
        [SerializeField] float reportRange = 40f;
        [Tooltip("Velocidad del cursor con el mando (pantallas por segundo).")]
        [SerializeField] float stickSpeed = .7f;
        [Header("Audio")]
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip correctClip;
        [SerializeField] AudioClip wrongClip;
        [Tooltip("Ruido de fondo mientras se miran las cámaras (vacío = estática sintetizada).")]
        [SerializeField] AudioClip staticLoop;
        [SerializeField] float staticVolume = .1f;

        public static SecurityMonitors Instance { get; private set; }
        public bool IsOpen { get; private set; }
        // Camera seen big (index in SecurityCamera.All), or -1 on the grid.
        public int Enlarged { get; private set; } = -1;
        public SecurityCamera Current => Enlarged >= 0 && Enlarged < SecurityCamera.All.Count ? SecurityCamera.All[Enlarged] : null;
        public bool Working => manager && manager.Act == GameAct.Reality;
        // A line other systems can show under the header (the escaped anomaly's streak).
        public string Notice { get; set; }
        // While locked the player can't leave or change camera (the final).
        public bool Locked { get; set; }
        public override string Prompt => "Ver cámaras";

        public event Action<SecurityCamera, ReportResult> CameraReported;
        public event Action Opened;
        public event Action Closed;

        static readonly Color Ink = new Color(.82f, .86f, .9f);
        static readonly Color Good = new Color(.4f, 1f, .55f);
        static readonly Color Bad = new Color(1f, .35f, .3f);

        AnomalyManager manager;
        AudioSource hiss;
        int gridCursor, openedFrame;
        Vector2 cursor = new Vector2(.5f, .5f);
        string feedback;
        Color feedbackColor;
        float feedbackUntil, flashUntil;
        SecurityCamera flashCamera;
        GUIStyle title, titleRight, small, label, button;

        void Awake()
        {
            Instance = this;
            hiss = gameObject.AddComponent<AudioSource>();
            hiss.playOnAwake = false;
            hiss.loop = true;
            hiss.spatialBlend = 0;
        }

        void Start() => manager = AnomalyManager.Instance;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void Interact(PlayerController player)
        {
            if (!Working)
            {
                GameMessages.Show("Solo hay estática.");
                return;
            }

            Open();
        }

        public void Open()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            openedFrame = Time.frameCount;
            Enlarged = -1;
            feedback = null;
            foreach (var cam in SecurityCamera.All)
                cam.Want();
            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(false);
            if (!hiss.clip)
                hiss.clip = staticLoop ? staticLoop : SynthAudio.Static(2f, 1f);
            hiss.volume = staticVolume;
            hiss.Play();
            Opened?.Invoke();
        }

        // force = also when locked (a change of act, the return to the dream).
        public void Close(bool force = false)
        {
            if (!IsOpen || (Locked && !force))
                return;
            IsOpen = false;
            Locked = false;
            Enlarged = -1;
            hiss.Stop();
            if (PlayerController.Instance)
                PlayerController.Instance.SetControl(true);
            Closed?.Invoke();
        }

        public void Enlarge(int index)
        {
            int count = SecurityCamera.All.Count;
            if (count == 0 || (Locked && Enlarged >= 0))
                return;
            Enlarged = (index % count + count) % count;
            // Rendering from this very frame, so the switch never shows a stale picture.
            Current.Want();
            gridCursor = Enlarged;
            cursor = new Vector2(.5f, .5f);
            feedback = null;
        }

        public void Back()
        {
            if (Locked)
                return;
            if (Enlarged >= 0)
                Enlarged = -1;
            else
                Close();
        }

        // Static over one feed for a moment (the escaped anomaly jumping).
        public void Flash(SecurityCamera cam, float seconds = .5f)
        {
            flashCamera = cam;
            flashUntil = Time.time + seconds;
        }

        // Reports whatever a camera shows under a viewport point: a camera-only thing in front of the first
        // wall (the escaped anomaly answers itself), else the prop the ray hits.
        public ReportResult ReportAt(SecurityCamera cam, Vector2 viewport)
        {
            if (!cam || !manager)
                return ReportResult.Nothing;
            var ray = cam.Camera.ViewportPointToRay(viewport);
            bool hit = Physics.Raycast(ray, out var info, reportRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var only = CameraOnly.Pick(cam, viewport, hit ? info.distance : reportRange);
            var target = only ? only.GetComponentInParent<ICameraTarget>() : null;
            var anomaly = only ? only.GetComponentInParent<Anomaly>() : null;
            ReportResult result;
            string message = null;
            if (target != null)
                result = target.ReportFromCamera(cam, out message);
            else if (anomaly)
                result = manager.ReportAnomaly(anomaly, ReportSource.Camera);
            else if (hit)
                result = manager.Report(info.collider, ReportSource.Camera);
            else
                result = ReportResult.Nothing;

            switch (result)
            {
                case ReportResult.Correct:
                    Say(message ?? "Anomalía reportada.", Good, correctClip);
                    break;
                case ReportResult.Wrong:
                    Say("No había nada raro ahí.", Bad, wrongClip);
                    break;
                case ReportResult.Nothing:
                    Say("Ahí no hay nada que reportar.", Ink, null);
                    break;
            }

            CameraReported?.Invoke(cam, result);
            return result;
        }

        public void Say(string text, Color color, AudioClip clip)
        {
            feedback = text;
            feedbackColor = color;
            feedbackUntil = Time.time + 2.5f;
            if (source && clip)
                source.PlayOneShot(clip);
        }

        void Update()
        {
            if (!IsOpen)
                return;
            if (!Working && !Locked)
            {
                Close(true);
                return;
            }

            // What is on screen renders: every feed on the grid, only the big one when enlarged.
            if (Current)
                Current.Want();
            else
                foreach (var cam in SecurityCamera.All)
                    cam.Want();

            // The key that opened the screen (gamepad A) must not also pick a camera.
            if (Time.frameCount == openedFrame)
                return;
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            int count = SecurityCamera.All.Count;
            bool back = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
            if (back)
            {
                Back();
                return;
            }

            if (Locked)
                return;
            if (keyboard != null)
                for (int i = 0; i < Mathf.Min(count, 9); i++)
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                        Enlarge(i);

            if (Enlarged < 0)
            {
                if (pad == null || count == 0)
                    return;
                int col = gridCursor % 3, row = gridCursor / 3, rows = (count + 2) / 3;
                if (pad.dpad.left.wasPressedThisFrame)
                    col = (col + 2) % 3;
                if (pad.dpad.right.wasPressedThisFrame)
                    col = (col + 1) % 3;
                if (pad.dpad.up.wasPressedThisFrame)
                    row = (row + rows - 1) % rows;
                if (pad.dpad.down.wasPressedThisFrame)
                    row = (row + 1) % rows;
                gridCursor = Mathf.Min(row * 3 + col, count - 1);
                if (pad.buttonSouth.wasPressedThisFrame)
                    Enlarge(gridCursor);
                return;
            }

            bool previous = (keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
            bool next = (keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            if (previous)
                Enlarge(Enlarged - 1);
            if (next)
                Enlarge(Enlarged + 1);
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > .02f)
                    cursor = new Vector2(Mathf.Clamp01(cursor.x + stick.x * stickSpeed * Time.deltaTime), Mathf.Clamp01(cursor.y + stick.y * stickSpeed * Time.deltaTime));
            }

            // R / right click / gamepad Y (the report binding), or gamepad A.
            if (GameInput.Report.WasPressedThisFrame() || (pad != null && pad.buttonSouth.wasPressedThisFrame))
                ReportAt(Current, cursor);
        }

        void OnGUI()
        {
            if (!IsOpen)
                return;
            GUI.depth = -2;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Ink } };
                label = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                small = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.62f, .66f, .72f) } };
                button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
                titleRight = new GUIStyle(title) { alignment = TextAnchor.UpperRight };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s;
            GUI.color = new Color(.02f, .025f, .03f, .97f);
            GUI.DrawTexture(new Rect(0, 0, w, 1080), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(40, 22, 600, 34), "CÁMARAS DE SEGURIDAD", title);
            var clock = NightClock.Instance;
            bool rec = Time.time % 1f < .6f;
            GUI.Label(new Rect(w - 440, 22, 400, 34), (rec ? "● " : "   ") + "REC   " + (clock ? clock.Display : ""), titleRight);
            // Low on the screen: the reality bar sits at the top centre.
            if (!string.IsNullOrEmpty(Notice))
                GUI.Label(new Rect(0, 1080 - 140, w, 28), Notice, small);

            if (Enlarged < 0)
                DrawGrid(w);
            else
                DrawEnlarged(w);

            if (!string.IsNullOrEmpty(feedback) && Time.time < feedbackUntil)
            {
                small.normal.textColor = feedbackColor;
                GUI.Label(new Rect(0, 1080 - 108, w, 30), feedback, small);
                small.normal.textColor = new Color(.62f, .66f, .72f);
            }

            // Keys or gamepad buttons, whichever is in use (InputPrompts).
            string hints = Enlarged < 0
                ? InputPrompts.Pick("Clic o [1-" + Mathf.Min(9, SecurityCamera.All.Count) + "]: ver una cámara  ·  {Back} salir",
                    "{DPad} elegir  ·  {Confirm} ver una cámara  ·  {Back} salir")
                : InputPrompts.Pick("Clic o {Report}: reportar lo que apuntas  ·  {Shoulders} cambiar de cámara  ·  {Back} volver",
                    "{Move} apuntar  ·  {Confirm} reportar  ·  {Shoulders} cambiar de cámara  ·  {Back} volver");
            if (!Locked)
                InputPrompts.Label(new Rect(0, 1080 - 72, w, 52), hints, small);
            GUI.matrix = Matrix4x4.identity;
        }

        void DrawGrid(float w)
        {
            var cams = SecurityCamera.All;
            int count = cams.Count, rows = Mathf.Max(1, (count + 2) / 3);
            const float gap = 20, top = 96, bottom = 150;
            float cellW = Mathf.Min((w - 120 - 2 * gap) / 3, ((1080 - top - bottom - (rows - 1) * gap) / rows) * 16f / 9f);
            float cellH = cellW * 9f / 16f;
            float x0 = (w - (3 * cellW + 2 * gap)) / 2, y0 = top + (1080 - top - bottom - (rows * cellH + (rows - 1) * gap)) / 2;
            var e = Event.current;
            for (int i = 0; i < count; i++)
            {
                var r = new Rect(x0 + i % 3 * (cellW + gap), y0 + i / 3 * (cellH + gap), cellW, cellH);
                bool hover = r.Contains(e.mousePosition) || (InputPrompts.UsingGamepad && i == gridCursor);
                DrawFeed(r, cams[i], hover);
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    Enlarge(i);
                    e.Use();
                }
            }
        }

        void DrawEnlarged(float w)
        {
            var cam = Current;
            if (!cam)
                return;
            float maxW = w - 160, maxH = 1080 - 96 - 150;
            float fw = Mathf.Min(maxW, maxH * 16f / 9f), fh = fw * 9f / 16f;
            var r = new Rect((w - fw) / 2, 96, fw, fh);
            DrawFeed(r, cam, false);

            var e = Event.current;
            var mouse = Mouse.current;
            if (r.Contains(e.mousePosition) && mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0)
                cursor = new Vector2((e.mousePosition.x - r.x) / r.width, 1 - (e.mousePosition.y - r.y) / r.height);
            if (!Locked && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                cursor = new Vector2((e.mousePosition.x - r.x) / r.width, 1 - (e.mousePosition.y - r.y) / r.height);
                ReportAt(cam, cursor);
                e.Use();
            }

            if (Locked)
                return;
            // Reticle.
            float cx = r.x + cursor.x * r.width, cy = r.y + (1 - cursor.y) * r.height;
            GUI.color = new Color(1, 1, 1, .85f);
            GUI.DrawTexture(new Rect(cx - 14, cy - 1, 10, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4, cy - 1, 10, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 14, 2, 10), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy + 4, 2, 10), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (GUI.Button(new Rect(r.x, r.yMax + 14, 150, 36), "◀ Rejilla", button))
                Back();
        }

        void DrawFeed(Rect r, SecurityCamera cam, bool highlight)
        {
            GUI.color = highlight ? new Color(.55f, 1f, .7f) : new Color(.3f, .33f, .36f);
            GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(r, cam.Feed ? (Texture)cam.Feed : StaticNoise.Texture, ScaleMode.StretchToFill, false);
            bool burst = cam == flashCamera && Time.time < flashUntil;
            GUI.color = new Color(1, 1, 1, burst ? .85f : .06f);
            GUI.DrawTexture(r, StaticNoise.Texture, ScaleMode.StretchToFill, false);
            GUI.color = new Color(0, 0, 0, .55f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 30), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10, r.y + 3, r.width - 20, 26), cam.Title, label);
        }
    }
}
