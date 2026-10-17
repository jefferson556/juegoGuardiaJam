using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Piso13.Realidad
{
    public sealed class P13RealidadGame : MonoBehaviour
    {
        public static P13RealidadGame Instance;

        public enum GamePhase
        {
            Playing,
            Capture,
            Lost
        }

        [Header("Mundo")]
        public WorldData Data;
        public P13RealidadWorld World;
        public P13RealidadEnemy Enemy;
        public P13RealidadUI UI;
        public P13RealidadAudio Audio;

        [Header("Jugador")]
        public Camera View;
        public Vector3 PlayerPosition;
        public float Yaw = 180f; // Mirando hacia la puerta y el pasillo
        public float Pitch = 0f;
        public float EyeHeight = 1.65f;
        public bool Flashlight = true;
        public bool Crouched = false;
        public bool IsSprinting = false;

        [Header("Estado de Juego")]
        public GamePhase Phase = GamePhase.Playing;
        public float GameTime = 0f;
        public int Minute = 0;
        public float Energy = 100f;
        public float Sleep = 100f; // Barra de Sueño
        public float Reality { get { return Sleep; } set { Sleep = value; } } // Retrocompatibilidad
        public bool ShowModal = false;
        public bool ModalDismissed = false;
        public bool CCTV = false;
        public int CameraIndex = 0;
        public bool Captured => Phase == GamePhase.Capture || Phase == GamePhase.Lost;
        public string NotificationMessage = "";
        public float DoorShake = 0f;

        [Header("Configuración de Anomalía (Reloj Pentagrama)")]
        [Tooltip("Posición del reloj en el mundo (X=2.66, Y=2.05, Z=-1.60 )")]
        public Vector3 ClockAnomalyPosition = new Vector3(2.66f, 2.05f, -1.60f);
        [Tooltip("Rotación en grados Euler del reloj (Euler 0, -90, 0 para mirar hacia adentro desde pared derecha)")]
        public Vector3 ClockAnomalyRotation = new Vector3(0f, -90f, 0f);
        [Tooltip("Escala del modelo")]
        public Vector3 ClockAnomalyScale = Vector3.one;

        // Componentes internos
        Light flashlight;
        Light officeDoorLight;
        Transform hand;
        float headPhase = 0f;
        float noiseDistance = 0f;
        float realityTimer = 0f;
        float minuteTimer = 0f;
        float messageTimer = 0f;
        float captureTime = 0f;
        bool modalTriggered = false;
        Vector2 cctvLook = Vector2.zero;

        void Awake()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Piso13 Realidad")
            {
                Destroy(gameObject);
                return;
            }

            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Audio = gameObject.AddComponent<P13RealidadAudio>();
            var settings = new P13RealidadSettings();
            Audio.Setup(settings);
            Audio.SetGameplay(true);

            UI = gameObject.AddComponent<P13RealidadUI>();
            UI.Game = this;

            StartGameSession();
        }

        public void StartGameSession()
        {
            if (Audio == null)
            {
                Audio = GetComponent<P13RealidadAudio>() ?? gameObject.AddComponent<P13RealidadAudio>();
                var settings = new P13RealidadSettings();
                Audio.Setup(settings);
                Audio.SetGameplay(true);
            }
            if (UI == null)
            {
                UI = GetComponent<P13RealidadUI>() ?? gameObject.AddComponent<P13RealidadUI>();
                UI.Game = this;
            }

            CleanupSceneObjects();
            BuildWorld();
            SetupPlayer();
            SetupEnemy();
            SetupAnomalies();

            Phase = GamePhase.Playing;
            GameTime = 0f;
            Minute = 0;
            Energy = 100f;
            Sleep = 100f;
            ShowModal = false;
            ModalDismissed = false;
            modalTriggered = false;
            CCTV = false;
            minuteTimer = 0f;
            realityTimer = 0f;
            captureTime = 0f;
            DoorShake = 0f;

            CursorLock(true);
        }

        void CleanupSceneObjects()
        {
            if (View != null)
            {
                Destroy(View.gameObject);
                View = null;
            }
            var oldCam = GameObject.Find("PlayerCamera");
            if (oldCam != null) Destroy(oldCam);

            if (World != null && World.Root != null)
            {
                Destroy(World.Root.gameObject);
                World = null;
            }
            var oldWorld = GameObject.Find("Piso13_World");
            if (oldWorld != null) Destroy(oldWorld);

            if (Enemy != null)
            {
                Destroy(Enemy.gameObject);
                Enemy = null;
            }
            var oldEnemy = GameObject.Find("RealidadEnemy");
            if (oldEnemy != null) Destroy(oldEnemy);

            var oldClock = GameObject.Find("Clock_Pentagram");
            if (oldClock != null) Destroy(oldClock);

            if (officeDoorLight != null)
            {
                Destroy(officeDoorLight.gameObject);
                officeDoorLight = null;
            }
        }

        void BuildWorld()
        {
            try
            {
                var dataAsset = Resources.Load<TextAsset>("Data/World");
                if (!dataAsset)
                {
                    Debug.LogError("Falta Resources/Data/World.json");
                    return;
                }
                Data = JsonUtility.FromJson<WorldData>(dataAsset.text);
                World = new P13RealidadWorld(Data);
                World.Build();
                World.DoorClosed = false;

                // Luz cenital de la puerta de oficina
                officeDoorLight = new GameObject("LuzPuertaOficina").AddComponent<Light>();
                officeDoorLight.transform.SetParent(World.Root);
                officeDoorLight.transform.position = P13RealidadRules.RK(0, 2.7f, 3.6f);
                officeDoorLight.range = 8.5f;
                officeDoorLight.intensity = 2.5f;
                officeDoorLight.type = LightType.Point;
                officeDoorLight.color = new Color(1f, 0.96f, 0.9f);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void SetupPlayer()
        {
            PlayerPosition = P13RealidadRules.RK(0, 0.8f, 1.2f);
            Yaw = 180f;
            Pitch = 0f;
            EyeHeight = 1.65f;

            var camGo = new GameObject("PlayerCamera");
            View = camGo.AddComponent<Camera>();
            View.tag = "MainCamera";
            View.nearClipPlane = 0.035f;
            View.farClipPlane = 50f;
            View.fieldOfView = 72f;
            View.clearFlags = CameraClearFlags.SolidColor;
            View.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            // Linterna
            var flashGo = new GameObject("Linterna");
            flashGo.transform.SetParent(View.transform, false);
            flashGo.transform.localPosition = new Vector3(0.18f, -0.16f, 0.55f);
            flashlight = flashGo.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = 22f;
            flashlight.spotAngle = 55f;
            flashlight.innerSpotAngle = 24f;
            flashlight.intensity = 5f;
            flashlight.color = new Color(1f, 0.95f, 0.87f);
            flashlight.shadows = LightShadows.Soft;
            flashlight.enabled = Flashlight;

            // Mano con linterna
            try
            {
                var handObj = P13RealidadWorld.Model("Piso13_HandFlashlight", View.transform);
                if (handObj != null)
                {
                    hand = handObj.transform;
                    hand.localPosition = Vector3.zero;
                    hand.localRotation = Quaternion.identity;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("No se pudo cargar modelo de mano: " + ex.Message);
            }

            UpdateCameraView(0.016f);
        }

        void SetupEnemy()
        {
            var enemyGo = new GameObject("RealidadEnemy");
            Enemy = enemyGo.AddComponent<P13RealidadEnemy>();
            Enemy.Game = this;
            Enemy.ApplySpawnPosition();
            Enemy.SetupCharacter();
            Enemy.InitNavigation(World);
        }

        void SetupAnomalies()
        {
            try
            {
                // Configuración de posición pegada a la pared de la oficina
                // La pared sólida Este va de Z = -1.05 a Z = -3.00 (a Z = 0 hay una abertura)
                var clockPrefab = Resources.Load<GameObject>("Models/Clock_Pentagram");
                if (clockPrefab != null)
                {
                    var clockGo = Instantiate(clockPrefab);
                    clockGo.name = "Clock_Pentagram";
                    if (World != null && World.Root != null)
                    {
                        clockGo.transform.SetParent(World.Root);
                    }
                    clockGo.transform.position = ClockAnomalyPosition;
                    clockGo.transform.rotation = Quaternion.Euler(ClockAnomalyRotation);
                    clockGo.transform.localScale = ClockAnomalyScale;

                    // BoxCollider para permitir detección por Raycast
                    var col = clockGo.GetComponent<BoxCollider>();
                    if (col == null)
                    {
                        col = clockGo.AddComponent<BoxCollider>();
                        col.center = new Vector3(0f, 0f, 0.02f);
                        col.size = new Vector3(0.35f, 0.35f, 0.12f);
                    }

                    // Componente de anomalía interactivo
                    var anomaly = clockGo.GetComponent<P13Anomaly>();
                    if (anomaly == null)
                    {
                        anomaly = clockGo.AddComponent<P13Anomaly>();
                    }
                    anomaly.AnomalyId = "Reloj Pentagrama";
                    anomaly.Description = "Reloj con un pentagrama en la pared derecha de la oficina";
                    anomaly.DisappearOnReport = true;
                    anomaly.MaxReportDistance = 3.8f;
                    anomaly.EnemySpeedMultiplier = 1.6f;
                    anomaly.AlertDuration = 10f;
                }
                else
                {
                    Debug.LogWarning("No se encontró el modelo Models/Clock_Pentagram en Resources");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);

            // =================================================================
            // SECUENCIA DE CAPTURA JUMPSCARE (Tal cual en Piso 13)
            // =================================================================
            if (Phase == GamePhase.Capture)
            {
                captureTime += dt;

                // Mantener al enemigo cara a cara con el jugador y reproducir animación progresiva
                if (Enemy != null)
                {
                    var toPlayer = PlayerPosition - Enemy.Position;
                    toPlayer.y = 0;
                    if (toPlayer.sqrMagnitude > 0.001f)
                    {
                        Enemy.Yaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
                        Enemy.Publish();
                    }

                    Enemy.PlayAnimation(captureTime < 0.8f ? "alert" : captureTime < 1.6f ? "grab" : captureTime < 2.8f ? "lift" : "kill", 1f, false);
                }

                // Cámara enfocando el rostro del monstruo y levantándose del suelo
                if (View != null)
                {
                    var eye = new Vector3(PlayerPosition.x, EyeHeight, PlayerPosition.z);
                    var target = (Enemy != null ? Enemy.Position : PlayerPosition + Vector3.forward) + Vector3.up * 1.15f;
                    View.transform.position = eye + Vector3.up * (0.48f * Mathf.SmoothStep(0f, 1f, (captureTime - 1.6f) / 1.2f));
                    View.transform.rotation = Quaternion.LookRotation(target - View.transform.position);
                }

                // Sonido de impacto mortal a los 2.8s
                if (captureTime >= 2.8f && captureTime - dt < 2.8f)
                {
                    if (Audio != null) Audio.Spatial("impact", Enemy != null ? Enemy.Position : PlayerPosition, 1f);
                }

                // Final de la secuencia de captura: pasar a pantalla de fin de turno
                if (captureTime >= 3.6f)
                {
                    Phase = GamePhase.Lost;
                    if (Audio != null) Audio.SetGameplay(false);
                    if (Enemy != null) Enemy.Pause(true);
                    CursorLock(false);
                }

                if (hand) hand.gameObject.SetActive(false);
                return;
            }

            if (Phase == GamePhase.Lost)
            {
                if (hand) hand.gameObject.SetActive(false);
                return;
            }

            // =================================================================
            // FASE DE JUEGO ACTIVO (GamePhase.Playing)
            // =================================================================
            GameTime += dt;

            // Avance del reloj: 1 segundo real = 1 minuto de juego
            minuteTimer += dt;
            if (minuteTimer >= 1.0f)
            {
                minuteTimer -= 1.0f;
                Minute = (Minute + 1) % 1440;
            }

            // Temblor de puerta cuando el enemigo golpea desde afuera
            DoorShake = Mathf.MoveTowards(DoorShake, 0f, dt);
            if (World != null && World.OfficeDoor != null && DoorShake > 0f)
            {
                World.OfficeDoor.position += Vector3.right * Mathf.Sin(Time.unscaledTime * 61f) * DoorShake;
            }

            // Temporizador de modal a los 30 segundos
            if (!modalTriggered && GameTime >= 30f)
            {
                modalTriggered = true;
                ShowModal = true;
                CursorLock(false);
            }

            if (ShowModal)
            {
                CheckModalCloseInput();
                return;
            }

            // Barra de Sueño: descenso natural tras cerrar el modal
            if (ModalDismissed)
            {
                realityTimer += dt;
                if (realityTimer >= 10f)
                {
                    realityTimer -= 10f;
                    Sleep = Mathf.Max(0f, Sleep - 2.5f);
                }
            }

            // Mensajes temporales
            if (!string.IsNullOrEmpty(NotificationMessage))
            {
                messageTimer -= dt;
                if (messageTimer <= 0) NotificationMessage = "";
            }

            // Manejo de controles
            HandleInput(dt);

            // Tick del enemigo
            if (Enemy != null)
            {
                Enemy.Tick(dt);
            }

            // Actualizar vista y luces dinámicas
            UpdateCameraView(dt);
            if (World != null && View != null)
            {
                World.UpdateLights(View.transform.position, 6, 1, 1f);
            }
        }

        void CheckModalCloseInput()
        {
            bool close = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                close = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame;
            }
#else
            close = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape);
#endif
            if (close)
            {
                DismissModal();
            }
        }

        public void DismissModal()
        {
            ShowModal = false;
            ModalDismissed = true;
            CursorLock(true);

            // Despertar al enemigo para que empiece a patrullar
            if (Enemy != null)
            {
                Enemy.StartHunting();
            }
        }

        void HandleInput(float dt)
        {
            bool toggleEsc = false;
            bool toggleFlash = false;
            bool toggleCCTV = false;
            bool toggleInteract = false;
            bool toggleReport = false;
            bool toggleCrouch = false;
            bool sprint = false;
            bool clickToLock = false;
            float h = 0f;
            float v = 0f;
            Vector2 mouseDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null)
            {
                toggleEsc = kb.escapeKey.wasPressedThisFrame;
                toggleFlash = kb.fKey.wasPressedThisFrame;
                toggleCCTV = kb.vKey.wasPressedThisFrame;
                toggleInteract = kb.eKey.wasPressedThisFrame;
                toggleReport = kb.rKey.wasPressedThisFrame;
                toggleCrouch = kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;
                sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            }

            if (mouse != null)
            {
                mouseDelta = mouse.delta.ReadValue() * 0.12f;
                clickToLock = mouse.leftButton.wasPressedThisFrame;
            }
#else
            toggleEsc = Input.GetKeyDown(KeyCode.Escape);
            toggleFlash = Input.GetKeyDown(KeyCode.F);
            toggleCCTV = Input.GetKeyDown(KeyCode.V);
            toggleInteract = Input.GetKeyDown(KeyCode.E);
            toggleReport = Input.GetKeyDown(KeyCode.R);
            toggleCrouch = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
            sprint = Input.GetKey(KeyCode.LeftShift);
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");
            mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            clickToLock = Input.GetMouseButtonDown(0);
#endif

            if (clickToLock && Cursor.lockState != CursorLockMode.Locked && !CCTV && !ShowModal)
            {
                CursorLock(true);
            }

            if (toggleEsc)
            {
                if (CCTV)
                {
                    CCTV = false;
                    cctvLook = Vector2.zero;
                    CursorLock(true);
                }
                else
                {
                    bool isLocked = Cursor.lockState == CursorLockMode.Locked;
                    CursorLock(!isLocked);
                }
            }

            if (toggleCCTV)
            {
                CCTV = !CCTV;
                cctvLook = Vector2.zero;
                CursorLock(!CCTV);
                return;
            }

            if (CCTV)
            {
                if (Mathf.Abs(h) > 0.1f)
                {
                    int step = h > 0 ? 1 : -1;
                    SelectCamera((CameraIndex + step + 9) % 9);
                }
                cctvLook += mouseDelta * 1.5f;
                cctvLook.x = Mathf.Clamp(cctvLook.x, -35f, 35f);
                cctvLook.y = Mathf.Clamp(cctvLook.y, -20f, 20f);
                return;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Yaw += mouseDelta.x * 2.0f;
                Pitch = Mathf.Clamp(Pitch - mouseDelta.y * 2.0f, -65f, 65f);
            }

            if (toggleFlash)
            {
                Flashlight = !Flashlight;
                if (flashlight) flashlight.enabled = Flashlight;
                if (hand) hand.gameObject.SetActive(Flashlight);
                Audio.Spatial("P13_UI_Hover", PlayerPosition, 0.4f);
            }

            if (toggleCrouch)
            {
                Crouched = !Crouched;
            }
            float targetHeight = Crouched ? 1.0f : 1.65f;
            EyeHeight = Mathf.Lerp(EyeHeight, targetHeight, dt * 10f);

            // Tecla E: interactuar exclusivamente con la puerta
            if (toggleInteract)
            {
                InteractDoor();
            }

            // Tecla R: reportar anomalía
            if (toggleReport)
            {
                ReportAnomaly();
            }

            // Movimiento
            IsSprinting = sprint && !Crouched;
            Vector3 inputDir = new Vector3(h, 0, v).normalized;
            Vector3 before = PlayerPosition;

            if (inputDir.sqrMagnitude > 0.01f)
            {
                float speed = Crouched ? 0.85f : IsSprinting ? 4.2f : 2.0f;
                Vector3 moveDelta = (Quaternion.Euler(0, Yaw, 0) * inputDir) * speed * dt;

                if (World != null)
                {
                    PlayerPosition = World.Move(PlayerPosition, moveDelta, 0.34f);
                }
                else
                {
                    PlayerPosition += moveDelta;
                }
            }

            float travelled = Vector3.Distance(before, PlayerPosition);

            if (travelled > 0.0001f)
            {
                headPhase += travelled * Mathf.PI / (IsSprinting ? 0.85f : 0.65f);
                noiseDistance += travelled;
                if (noiseDistance >= (IsSprinting ? 0.85f : 0.65f))
                {
                    noiseDistance = 0f;
                    int stepNum = UnityEngine.Random.Range(0, 5);
                    Audio.Spatial("P13_PlayerStep_" + stepNum, PlayerPosition, Crouched ? 0.2f : IsSprinting ? 0.7f : 0.45f);
                }
            }

            View.fieldOfView = Mathf.Lerp(View.fieldOfView, IsSprinting && travelled > 0.001f ? 78f : 72f, dt * 10f);
        }

        void InteractDoor()
        {
            float distToDoor = Vector3.Distance(PlayerPosition, P13RealidadRules.RK(0, 0.8f, 2.35f));
            if (distToDoor < 2.5f)
            {
                World.DoorClosed = !World.DoorClosed;
                World.Revision++;
                Audio.Spatial(World.DoorClosed ? "P13_Door_Latch" : "P13_Door_Open", P13RealidadRules.RK(0, 1.2f, 3f), 0.5f);
                ShowNotification(World.DoorClosed ? "Puerta cerrada." : "Puerta abierta.");
            }
        }

        void ReportAnomaly()
        {
            Ray ray = new Ray(View.transform.position, View.transform.forward);
            RaycastHit hit;
            // Raycast a distancia para detectar lo que el jugador está observando
            bool hitSomething = Physics.Raycast(ray, out hit, 8f);

            bool isAnomaly = false;
            bool tooFar = false;
            P13Anomaly anomaly = null;

            if (hitSomething && hit.collider != null)
            {
                anomaly = hit.collider.GetComponentInParent<P13Anomaly>();
                if (anomaly != null && anomaly.IsActive && !anomaly.IsReported)
                {
                    // Distancia prudente para impactar / registrar el reporte
                    if (hit.distance <= anomaly.MaxReportDistance)
                    {
                        isAnomaly = true;
                    }
                    else
                    {
                        tooFar = true;
                    }
                }
            }

            if (isAnomaly && anomaly != null)
            {
                anomaly.Report();
                Audio.Spatial("P13_UI_Hover", PlayerPosition, 0.8f);
                ShowNotification($"¡ANOMALÍA ENCONTRADA!: {anomaly.AnomalyId}");
            }
            else if (tooFar && anomaly != null)
            {
                // Si está mirando la anomalía pero supera la distancia prudente
                Audio.Spatial("P13_Door_Latch", PlayerPosition, 0.4f);
                ShowNotification("Estás demasiado lejos para identificar la anomalía. Acércate más.");
            }
            else
            {
                // Falsa alarma: baja el 15% del valor actual de Sueño
                float penalty = Sleep * 0.15f;
                Sleep = Mathf.Max(0f, Sleep - penalty);

                Audio.Spatial("P13_Door_Latch", PlayerPosition, 0.6f);
                ShowNotification(string.Format("FALSA ALARMA: No hay anomalía (-15% Sueño: {0}%)", Mathf.CeilToInt(Sleep)));

                if (Enemy != null)
                {
                    Enemy.AlertNoise(PlayerPosition, 0.65f);
                }
            }
        }

        public void SelectCamera(int index)
        {
            CameraIndex = index;
            Audio.Spatial("P13_UI_Hover", View.transform.position, 0.3f);
        }

        public void ShowNotification(string msg)
        {
            NotificationMessage = msg;
            messageTimer = 3.2f;
        }

        void UpdateCameraView(float dt)
        {
            if (View == null) return;

            if (CCTV && Data != null && Data.cameras != null && CameraIndex < Data.cameras.Length)
            {
                var cam = Data.cameras[CameraIndex];
                View.transform.position = cam.position;
                View.transform.rotation = Quaternion.LookRotation(cam.target - cam.position) * Quaternion.Euler(-cctvLook.y, cctvLook.x, 0);
                if (hand) hand.gameObject.SetActive(false);
                if (flashlight) flashlight.enabled = false;
                return;
            }

            View.transform.position = new Vector3(PlayerPosition.x, EyeHeight, PlayerPosition.z);
            View.transform.rotation = Quaternion.Euler(Pitch, Yaw, 0);

            if (hand)
            {
                hand.gameObject.SetActive(Flashlight);
                float amp = Crouched ? 0.00065f : IsSprinting ? 0.013f : 0.004f;
                var desired = new Vector3(Mathf.Sin(headPhase) * amp * 0.6f, Mathf.Cos(headPhase * 2) * amp, Mathf.Sin(headPhase * 2) * amp * 0.3f);

                bool blocked = World != null && !World.Clear(View.transform.position, View.transform.TransformPoint(new Vector3(0.18f, -0.16f, 0.8f)), 0.12f);
                if (blocked) desired += new Vector3(-0.08f, -0.035f, -0.36f);

                hand.localPosition = blocked ? desired : Vector3.Lerp(hand.localPosition, desired, dt * 14f);
            }

            if (flashlight) flashlight.enabled = Flashlight;
        }

        public void TriggerCapture()
        {
            if (Phase != GamePhase.Playing) return;
            Phase = GamePhase.Capture;
            CCTV = false;
            captureTime = 0f;
            if (Audio != null) Audio.Player(0.016f, 0f, false, false, false);
            CursorLock(false);
        }

        public void RestartSession()
        {
            CursorLock(false);
            if (Audio != null)
            {
                Audio.SetGameplay(false);
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void LoadPiso13()
        {
            CursorLock(false);
            if (Audio != null)
            {
                Audio.SetGameplay(false);
            }
            SceneManager.LoadScene("Piso13");
        }

        public void CursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
