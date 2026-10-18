using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13.Realidad
{
    [Serializable]
    public class RoomMaskSpawnPoint
    {
        public string roomName;
        public int roomId;
        public Vector3 spawnPosition;
        public Vector3 spawnEulerAngles;
        public bool enabled = true;

        public RoomMaskSpawnPoint(int id, string name, Vector3 pos, Vector3 rot, bool isEnabled = true)
        {
            roomId = id;
            roomName = name;
            spawnPosition = pos;
            spawnEulerAngles = rot;
            enabled = isEnabled;
        }
    }

    /// <summary>
    /// Anomalía: Máscara Diablo Huma.
    /// - Aparece exactamente 10 segundos DESPUÉS de que desaparece / se cierra la ventana de advertencia.
    /// - Mientras la advertencia está activa en pantalla, todo el juego y la máscara están completamente pausados.
    /// - Spawnea a espaldas del jugador en las salas especificadas (Oficina, Pasillo, Archivo, Servidores, Almacén) o en punto fijo.
    /// - Si el jugador la alumbra con la linterna encendida, se desvanece de inmediato.
    /// - Para poder reportarla con [R], el jugador debe apagar la linterna con [F].
    /// </summary>
    public class P13HumanMaskAnomaly : MonoBehaviour
    {
        [Header("Referencias")]
        public P13RealidadGame Game;

        [Header("Configuración de Spawn")]
        [Tooltip("Tiempo en segundos que espera tras desaparecer la advertencia para hacer spawn.")]
        public float DelayAfterWarning = 10f;

        [Tooltip("Tiempo de enfriamiento en segundos tras desvanecerse por luz antes de poder reaparecer.")]
        public float RespawnCooldown = 15f;

        [Tooltip("Si es verdadero, la máscara mira siempre hacia el jugador. Si es falso, usa spawnEulerAngles fijo.")]
        public bool FacePlayer = true;

        [Header("Puntos de Spawn Fijos por Habitación (CAM 01 a CAM 09)")]
        public List<RoomMaskSpawnPoint> RoomSpawnPoints = new List<RoomMaskSpawnPoint>();

        [Header("Spawn Detrás del Jugador (Salas Especificadas)")]
        [Tooltip("Salas donde la máscara spawnea detrás del jugador: 1=Oficina, 4=Pasillo, 5=Archivo, 6=Servidores, 9=Almacén.")]
        public List<int> SpawnBehindRooms = new List<int> { 1, 4, 5, 6, 9 };

        [Tooltip("Distancia detrás del jugador al hacer spawn en estas salas (en metros). Reducido 25% (de 3.3m a 2.475m).")]
        public float SpawnBehindDistance = 1.95f;

        [Header("Distancia y Ángulo")]
        public float MaxReportDistance = 3.0f;
        public float FlashlightDetectionAngle = 35f;

        [Header("Estado")]
        public bool HasSpawned = false;
        public bool IsReported = false;
        public int CurrentSpawnRoomId = -1;

        [Header("Monitor en Vivo (Debug Inspector)")]
        [SerializeField] string debugPlayerCurrentRoom = "";
        [SerializeField] int debugPlayerCurrentRoomId = 0;
        [SerializeField] Vector3 debugPlayerPosition = Vector3.zero;
        [SerializeField] float debugCountdownToSpawn = 0f;

        Vector3 basePosition;
        Quaternion baseRotation;
        float vanishCooldownTimer = 0f;
        bool warningDismissed = false;
        float timerAfterWarning = 10f;
        int lastPlayerRoomId = -1;
        float roomChangeCooldownTimer = 0f;

        Renderer[] cachedRenderers;
        Collider[] cachedColliders;

        void Awake()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
            HasSpawned = false;
            IsReported = false;
            warningDismissed = false;
            timerAfterWarning = DelayAfterWarning;
            lastPlayerRoomId = -1;
            roomChangeCooldownTimer = 0f;
            InitDefaultSpawnPoints();
            SetVisuals(false);
        }

        public void SetVisuals(bool visible)
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
                cachedRenderers = GetComponentsInChildren<Renderer>(true);
            if (cachedColliders == null || cachedColliders.Length == 0)
                cachedColliders = GetComponentsInChildren<Collider>(true);

            foreach (var r in cachedRenderers)
            {
                if (r != null) r.enabled = visible;
            }
            foreach (var c in cachedColliders)
            {
                if (c != null) c.enabled = visible;
            }
        }

        public void InitDefaultSpawnPoints()
        {
            if (RoomSpawnPoints != null && RoomSpawnPoints.Count >= 9) return;

            RoomSpawnPoints = new List<RoomMaskSpawnPoint>
            {
                new RoomMaskSpawnPoint(1, "CAM 01 — OFICINA", new Vector3(0.00f, 1.35f, -0.80f), new Vector3(0f, 180f, 0f)),
                new RoomMaskSpawnPoint(2, "CAM 02 — RECEPCIÓN", new Vector3(-6.80f, 1.35f, -0.60f), new Vector3(0f, 90f, 0f)),
                new RoomMaskSpawnPoint(3, "CAM 03 — ASCENSOR", new Vector3(0.00f, 1.35f, -7.55f), new Vector3(0f, 180f, 0f)),
                new RoomMaskSpawnPoint(4, "CAM 04 — PASILLO CENTRAL", new Vector3(0.00f, 1.35f, -4.50f), new Vector3(0f, 0f, 0f)),
                new RoomMaskSpawnPoint(5, "CAM 05 — ARCHIVO", new Vector3(-6.20f, 1.35f, -5.90f), new Vector3(0f, 90f, 0f)),
                new RoomMaskSpawnPoint(6, "CAM 06 — SERVIDORES", new Vector3(6.20f, 1.35f, -5.90f), new Vector3(0f, -90f, 0f)),
                new RoomMaskSpawnPoint(7, "CAM 07 — TÉCNICA", new Vector3(6.80f, 1.35f, -0.60f), new Vector3(0f, -90f, 0f)),
                new RoomMaskSpawnPoint(8, "CAM 08 — ABANDONADA", new Vector3(-6.80f, 1.35f, 6.20f), new Vector3(0f, 90f, 0f)),
                new RoomMaskSpawnPoint(9, "CAM 09 — ALMACÉN", new Vector3(6.80f, 1.35f, 6.20f), new Vector3(0f, -90f, 0f))
            };
        }

        public static string GetRoomName(int roomId)
        {
            switch (roomId)
            {
                case 1: return "CAM 01 — OFICINA";
                case 2: return "CAM 02 — RECEPCIÓN";
                case 3: return "CAM 03 — ASCENSOR";
                case 4: return "CAM 04 — PASILLO CENTRAL";
                case 5: return "CAM 05 — ARCHIVO";
                case 6: return "CAM 06 — SERVIDORES";
                case 7: return "CAM 07 — TÉCNICA";
                case 8: return "CAM 08 — ABANDONADA";
                case 9: return "CAM 09 — ALMACÉN";
                default: return $"Habitación Desconocida ({roomId})";
            }
        }

        public RoomMaskSpawnPoint GetSpawnPointForRoom(int roomId)
        {
            if (RoomSpawnPoints == null) return null;
            for (int i = 0; i < RoomSpawnPoints.Count; i++)
            {
                if (RoomSpawnPoints[i].roomId == roomId)
                    return RoomSpawnPoints[i];
            }
            return null;
        }

        public void SetSpawnCoordinate(int roomId, Vector3 position, Vector3 eulerAngles)
        {
            var sp = GetSpawnPointForRoom(roomId);
            if (sp != null)
            {
                sp.spawnPosition = position;
                sp.spawnEulerAngles = eulerAngles;
            }
            else
            {
                RoomSpawnPoints.Add(new RoomMaskSpawnPoint(roomId, GetRoomName(roomId), position, eulerAngles));
            }
        }

        public void OnAlertModalDismissed()
        {
            if (!warningDismissed)
            {
                warningDismissed = true;
                timerAfterWarning = DelayAfterWarning;
                if (Game != null)
                {
                    lastPlayerRoomId = P13RealidadRules.Room(Game.PlayerPosition);
                }
            }
        }

        void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (IsReported) return;

            // Mientras la advertencia esté en pantalla, TODO está pausado y la máscara no avanza ni aparece
            if (Game != null && Game.ShowModal)
            {
                return;
            }

            int currentRoom = 0;
            if (Game != null)
            {
                currentRoom = P13RealidadRules.Room(Game.PlayerPosition);
                debugPlayerCurrentRoomId = currentRoom;
                debugPlayerCurrentRoom = GetRoomName(currentRoom);
                debugPlayerPosition = Game.PlayerPosition;

                // Si el juego cerró el modal pero no se había registrado el evento
                if (!warningDismissed && Game.ModalDismissed)
                {
                    OnAlertModalDismissed();
                }

                if (lastPlayerRoomId == -1 && currentRoom > 0)
                {
                    lastPlayerRoomId = currentRoom;
                }
            }

            if (vanishCooldownTimer > 0f)
            {
                vanishCooldownTimer -= dt;
            }

            if (roomChangeCooldownTimer > 0f)
            {
                roomChangeCooldownTimer -= dt;
            }

            // Detección de cambio de habitación:
            // Al cambiar de habitación, la máscara DEBE hacer spawn de inmediato en la nueva habitación
            if (warningDismissed && currentRoom > 0 && currentRoom <= 9 && currentRoom != lastPlayerRoomId)
            {
                if (roomChangeCooldownTimer <= 0f)
                {
                    lastPlayerRoomId = currentRoom;
                    roomChangeCooldownTimer = 0.8f;
                    RelocateToRoom(currentRoom);
                    return;
                }
            }

            if (!HasSpawned)
            {
                // Solo cuenta los 10 segundos UNA VEZ que la advertencia ha desaparecido
                if (warningDismissed)
                {
                    if (timerAfterWarning > 0f)
                    {
                        timerAfterWarning -= dt;
                        debugCountdownToSpawn = Mathf.Max(0f, timerAfterWarning);
                    }

                    if (timerAfterWarning <= 0f && vanishCooldownTimer <= 0f)
                    {
                        debugCountdownToSpawn = 0f;
                        CheckSpawnCondition();
                    }
                }
                return;
            }

            // Animación sutil de levitación / flotación
            float floatOffset = Mathf.Sin(Time.time * 2.2f) * 0.04f;
            transform.position = basePosition + Vector3.up * floatOffset;

            if (FacePlayer && Game != null && Game.View != null)
            {
                Vector3 toPlayer = Game.View.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
                }
            }
            else
            {
                transform.rotation = baseRotation;
            }

            // Comprobar si el jugador apunta con la linterna encendida
            if (Game != null && Game.Flashlight)
            {
                CheckFlashlightAim();
            }
        }

        public void RelocateToRoom(int roomId)
        {
            if (IsReported || (Game != null && Game.ShowModal)) return;

            var spawnPoint = GetSpawnPointForRoom(roomId);
            if (spawnPoint == null || !spawnPoint.enabled) return;

            // Resetear temporizadores y estado previo para spawnear inmediatamente en la nueva sala
            timerAfterWarning = 0f;
            debugCountdownToSpawn = 0f;
            vanishCooldownTimer = 0f;
            SetVisuals(false);
            HasSpawned = false;

            SpawnAtPoint(spawnPoint);
        }

        void CheckSpawnCondition()
        {
            if (Game == null || Game.Phase != P13RealidadGame.GamePhase.Playing || vanishCooldownTimer > 0f || Game.ShowModal)
                return;

            int currentRoom = P13RealidadRules.Room(Game.PlayerPosition);
            var spawnPoint = GetSpawnPointForRoom(currentRoom);

            if (spawnPoint != null && spawnPoint.enabled)
            {
                SpawnAtPoint(spawnPoint);
            }
        }

        public bool IsSpawnBehindRoom(int roomId)
        {
            return SpawnBehindRooms != null && SpawnBehindRooms.Contains(roomId);
        }

        Vector3 CalculateBehindPlayerPosition(RoomMaskSpawnPoint fallbackPt)
        {
            Vector3 playerPos = Game.PlayerPosition;
            Vector3 playerFwd = Game.View != null ? Game.View.transform.forward : Quaternion.Euler(0, Game.Yaw, 0) * Vector3.forward;
            playerFwd.y = 0f;
            if (playerFwd.sqrMagnitude < 0.001f) playerFwd = Vector3.forward;
            playerFwd.Normalize();

            Vector3 backDir = -playerFwd;
            Vector3 rayOrigin = new Vector3(playerPos.x, 1.35f, playerPos.z);
            Vector3 targetPos = rayOrigin + backDir * SpawnBehindDistance;

            // Comprobar con raycast si hay pared directamente detrás
            if (Physics.Raycast(rayOrigin, backDir, out RaycastHit hit, SpawnBehindDistance))
            {
                if (hit.distance > 1.1f)
                {
                    targetPos = rayOrigin + backDir * (hit.distance - 0.45f);
                }
                else
                {
                    // Probar diagonales traseras (-135° y 135°) si está de espaldas muy pegado a la pared
                    Vector3 backLeft = Quaternion.Euler(0, -135f, 0f) * playerFwd;
                    Vector3 backRight = Quaternion.Euler(0, 135f, 0f) * playerFwd;

                    float distL = Physics.Raycast(rayOrigin, backLeft, out RaycastHit hitL, SpawnBehindDistance) ? hitL.distance : SpawnBehindDistance;
                    float distR = Physics.Raycast(rayOrigin, backRight, out RaycastHit hitR, SpawnBehindDistance) ? hitR.distance : SpawnBehindDistance;

                    if (distL > distR && distL > 1.2f)
                    {
                        targetPos = rayOrigin + backLeft * Mathf.Min(distL - 0.45f, SpawnBehindDistance);
                    }
                    else if (distR > 1.2f)
                    {
                        targetPos = rayOrigin + backRight * Mathf.Min(distR - 0.45f, SpawnBehindDistance);
                    }
                    else if (fallbackPt != null)
                    {
                        targetPos = fallbackPt.spawnPosition;
                    }
                }
            }

            targetPos.y = 1.35f;
            return targetPos;
        }

        public void SpawnAtPoint(RoomMaskSpawnPoint pt)
        {
            if (HasSpawned || IsReported || pt == null || (Game != null && Game.ShowModal)) return;

            CurrentSpawnRoomId = pt.roomId;
            Vector3 finalPos = pt.spawnPosition;
            Quaternion finalRot = Quaternion.Euler(pt.spawnEulerAngles);

            bool spawnBehind = IsSpawnBehindRoom(pt.roomId) && Game != null;
            if (spawnBehind)
            {
                finalPos = CalculateBehindPlayerPosition(pt);
                Vector3 toPlayer = Game.PlayerPosition - finalPos;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    finalRot = Quaternion.LookRotation(toPlayer.normalized);
                }
            }

            basePosition = finalPos;
            baseRotation = finalRot;
            transform.position = basePosition;
            transform.rotation = baseRotation;

            SetVisuals(true);
            HasSpawned = true;

            if (Game != null)
            {
                if (Game.Audio != null)
                {
                    Game.Audio.Spatial("electric", transform.position, 0.45f);
                }
                string msg = spawnBehind
                    ? "¡La Máscara Diablo Huma ha aparecido a tus espaldas!"
                    : "¡La Máscara Diablo Huma ha aparecido en esta habitación!";
                Game.ShowNotification(msg, 3.5f);
            }

            Debug.Log($"[P13HumanMaskAnomaly] Máscara Diablo Huma spawneada en habitación {pt.roomId} ({pt.roomName}) en posición {finalPos} (Detrás del jugador: {spawnBehind})");
        }

        void CheckFlashlightAim()
        {
            if (Game == null || Game.View == null || !Game.Flashlight) return;

            Vector3 camPos = Game.View.transform.position;
            Vector3 camForward = Game.View.transform.forward;
            Vector3 toMask = (transform.position - camPos);
            float dist = toMask.magnitude;

            if (dist > 12f) return;

            float dot = Vector3.Dot(camForward, toMask.normalized);
            float minDot = Mathf.Cos(FlashlightDetectionAngle * Mathf.Deg2Rad);

            if (dot >= minDot)
            {
                RaycastHit hit;
                if (Physics.Raycast(camPos, toMask.normalized, out hit, dist + 0.5f))
                {
                    if (hit.collider.transform.IsChildOf(transform) || hit.collider.gameObject == gameObject)
                    {
                        VanishByLight();
                    }
                }
            }
        }

        public void VanishByLight()
        {
            if (!HasSpawned || IsReported) return;

            SetVisuals(false);
            HasSpawned = false;
            vanishCooldownTimer = RespawnCooldown;

            if (Game != null)
            {
                if (Game.Audio != null)
                {
                    Game.Audio.Spatial("electric", transform.position, 0.75f);
                }
                Game.ShowNotification("¡La Máscara Diablo Huma no tolera la luz y se desvaneció!", 3.2f);
            }

            Debug.Log("[P13HumanMaskAnomaly] Máscara Diablo Huma desvanecida por linterna.");
        }

        public string GetPrompt()
        {
            if (Game != null && Game.Flashlight)
            {
                return "Máscara Diablo Huma: [Debes apagar la linterna con 'F' para reportar]";
            }
            return "Máscara Diablo Huma: ¿Deseas reportar que encontraste una anomalía? [R]";
        }

        public bool Report(P13RealidadGame game)
        {
            if (IsReported || !HasSpawned) return false;

            if (game != null && game.Flashlight)
            {
                game.ShowNotification("Debes apagar la linterna para reportar la Máscara Diablo Huma.", 3.0f);
                return false;
            }

            IsReported = true;
            HasSpawned = false;
            SetVisuals(false);

            if (game != null)
            {
                game.Reality = Mathf.Min(100f, game.Reality + 10f);

                if (game.Enemy != null)
                {
                    game.Enemy.OnAnomalyReported();
                }

                if (game.Audio != null)
                {
                    game.Audio.Spatial("P13_UI_Hover", transform.position, 0.8f);
                }

                game.ShowNotification("¡ANOMALÍA ENCONTRADA!: Máscara Diablo Huma (+10% Realidad)", 3.5f);
            }

            return true;
        }
    }
}
