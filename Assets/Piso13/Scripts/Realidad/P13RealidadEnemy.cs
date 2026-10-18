using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Piso13.Realidad
{
    public enum RealidadEnemyState
    {
        Dormant,       // En reposo los primeros 30 segundos
        Patrolling,    // Patrulla entre habitaciones del piso
        Investigating, // Acudiendo a un ruido, evento o última posición vista
        Hunting,       // Caza activa y persecución directa
        AtDoor,        // Golpeando la puerta cerrada de la oficina
        Retreating     // Retirándose de la puerta cerrada hacia el pasillo
    }

    public sealed class P13RealidadEnemy : MonoBehaviour
    {
        public P13RealidadGame Game;
        public Transform Root;
        public Transform Visual;
        public Animation Anim;
        public Vector3 Position;
        public float Yaw;

        [Header("Configuración")]
        public string modelName = "MonsterGuard";
        public float characterHeight = 2.22f;

        // Punto de spawn idéntico al Piso 13 (Almacén CAM09)
        public Vector3 defaultSpawnPosition = new Vector3(6.8f, 0.8f, 6.2f);
        public float defaultSpawnYaw = 180f;

        [Header("Estado")]
        public RealidadEnemyState State = RealidadEnemyState.Dormant;
        public bool IsHunting = false;
        public bool SeesPlayer = false;
        public float Alert = 0f;
        public float Speed = 0f;

        [Header("Sistema de Eventos del Mapa")]
        public bool HasActiveEvent = false;
        public string ActiveEventId = "";
        public Vector3 EventTargetPos;
        public float EventTimer = 0f;
        public float EventSpeedMultiplier = 1f;

        [Header("Ralentización por Reporte de Anomalías")]
        public float SlowTimer = 0f;
        public float SlowMultiplier = 0.5f;

        [Header("Penalización por Reporte Incorrecto")]
        public float SpeedBoostTimer = 0f;
        public float SpeedBoostMultiplier = 1.25f;

        [Header("Audio del Enemigo")]
        public AudioClip BreathingClip;
        public AudioClip FeetClip;
        public AudioClip DraggingClip;
        public float MaxHearingDistance = 16f;
        public float MaxBreathingVolume = 0.85f;
        public float MaxFootstepVolume = 0.75f;
        public float FootstepInterval = 0.85f;

        private AudioSource breathingSource;
        private AudioSource stepSource;
        private bool alternateStep = false;

        public static Action<string, Vector3, float> OnGlobalMapEvent;

        // Nodos del mapa idénticos a P13Guard en Piso 13
        public static readonly Vector3[] destinations = {
            P13RealidadRules.RK(0, .8f, .8f),      // 0: Oficinas
            P13RealidadRules.RK(-6.8f, .8f, .6f),   // 1: Recepcion
            P13RealidadRules.RK(0, .8f, 7.55f),     // 2: Ascensor
            P13RealidadRules.RK(0, .8f, 4.5f),      // 3: Pasillo central
            P13RealidadRules.RK(-6.2f, .8f, 5.9f),  // 4: Archivo
            P13RealidadRules.RK(6.2f, .8f, 5.9f),   // 5: Servidores
            P13RealidadRules.RK(6.8f, .8f, .6f),    // 6: Tecnica
            P13RealidadRules.RK(-6.8f, .8f, -6.2f), // 7: Abandonada
            P13RealidadRules.RK(6.8f, .8f, -6.2f),  // 8: Almacen (Spawn)
            P13RealidadRules.RK(0, .8f, 3.85f)      // 9: Puerta
        };

        static readonly int[][] neighbors = {
            new[]{1, 6, 3},       // 0: Oficinas -> Recepcion, Tecnica, Pasillo
            new[]{0, 7},          // 1: Recepcion -> Oficinas, Abandonada
            new[]{3},             // 2: Ascensor -> Pasillo
            new[]{0, 2, 4, 5, 9}, // 3: Pasillo -> Oficinas, Ascensor, Archivo, Servidores, Puerta
            new[]{3, 7},          // 4: Archivo -> Pasillo, Abandonada
            new[]{3, 8},          // 5: Servidores -> Pasillo, Almacen
            new[]{0, 8},          // 6: Tecnica -> Oficinas, Almacen
            new[]{1, 4},          // 7: Abandonada -> Recepcion, Archivo
            new[]{6, 5},          // 8: Almacen -> Tecnica, Servidores
            new[]{3}              // 9: Puerta -> Pasillo
        };

        P13RealidadNavigation navigation;
        P13EnemyNavigator navigator;
        P13EnemyPerception perception;
        int location = 8;
        int destination = 8;
        Vector3? memory = null;
        string currentAnimPlaying = "";

        float clock = 0f;
        float replanTimer = 0f;
        float decisionTimer = 5f;
        float doorTimer = 0f;
        float stepDistance = 0f;
        float confidence = 0f;
        float lastSeen = -999f;
        float lastHeard = -999f;
        float investigateWaitTimer = 0f;

        void Awake()
        {
            SetupCharacter();
            ApplySpawnPosition();
        }

        public void SetupCharacter()
        {
            if (Root != null) return;

            var rootGo = new GameObject(modelName + "_Root");
            rootGo.transform.SetParent(transform, false);
            Root = rootGo.transform;

            // Instanciar modelo utilizando la conversión de ejes oficial de P13RealidadWorld
            Visual = P13RealidadWorld.Model(modelName, Root).transform;

            var renderers = Visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers)
                    b.Encapsulate(r.bounds);

                float scale = characterHeight / b.size.y;
                Visual.localScale = Vector3.one * scale;
                Visual.localPosition = new Vector3(0, -0.8f - b.min.y * scale, 0);

                // Rotación de 180° para compensar el sistema de coordenadas de RealityKit -> Unity
                Visual.localRotation = Quaternion.Euler(0, 180, 0);

                foreach (var smr in Visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.updateWhenOffscreen = true;
                    smr.localBounds = new Bounds(new Vector3(0, 1.1f, 0), new Vector3(2.5f, 2.5f, 2.5f));
                }
            }

            foreach (var c in Visual.GetComponentsInChildren<Collider>())
                Destroy(c);

            Anim = Visual.GetComponentInChildren<Animation>();
            if (Anim != null)
            {
                foreach (AnimationState state in Anim)
                    state.wrapMode = WrapMode.Loop;

                PlayAnimation("idle");
            }

            SetupAudio();
        }

        public void SetupAudio()
        {
            if (BreathingClip == null)
                BreathingClip = Resources.Load<AudioClip>("Audio/freesound_community-zombie-breathing-70682");
            if (FeetClip == null)
                FeetClip = Resources.Load<AudioClip>("Audio/freesound_community-feet-56316");
            if (DraggingClip == null)
                DraggingClip = Resources.Load<AudioClip>("Audio/freesound_community-dragging-on-floor-82114");

            Transform audioParent = Root != null ? Root : transform;

            if (breathingSource == null)
            {
                var breathGo = new GameObject("Enemy_BreathingAudio");
                breathGo.transform.SetParent(audioParent, false);
                breathGo.transform.localPosition = new Vector3(0, 1.6f, 0); // Nivel de cabeza / pecho
                breathingSource = breathGo.AddComponent<AudioSource>();
                breathingSource.playOnAwake = false;
                breathingSource.loop = true;
                breathingSource.spatialBlend = 1.0f; // 3D
                breathingSource.rolloffMode = AudioRolloffMode.Logarithmic;
                breathingSource.minDistance = 1.8f;
                breathingSource.maxDistance = MaxHearingDistance;
                breathingSource.clip = BreathingClip;
                breathingSource.volume = 0f;
            }

            if (stepSource == null)
            {
                var stepGo = new GameObject("Enemy_StepAudio");
                stepGo.transform.SetParent(audioParent, false);
                stepGo.transform.localPosition = new Vector3(0, 0.2f, 0); // Nivel de pies
                stepSource = stepGo.AddComponent<AudioSource>();
                stepSource.playOnAwake = false;
                stepSource.loop = false;
                stepSource.spatialBlend = 1.0f; // 3D
                stepSource.rolloffMode = AudioRolloffMode.Logarithmic;
                stepSource.minDistance = 1.5f;
                stepSource.maxDistance = MaxHearingDistance + 2f;
                stepSource.volume = MaxFootstepVolume;
            }
        }

        public void ApplySpawnPosition()
        {
            var marker = GameObject.Find("EnemySpawn");
            if (marker != null && marker.transform != Root && marker.transform != Visual)
            {
                Position = marker.transform.position;
                Yaw = marker.transform.eulerAngles.y;
            }
            else
            {
                Position = defaultSpawnPosition;
                Yaw = defaultSpawnYaw;
            }

            location = 8;
            destination = 8;
            State = RealidadEnemyState.Dormant;
            IsHunting = false;
            memory = null;
            SlowTimer = 0f;
            SpeedBoostTimer = 0f;
            stepDistance = 0f;
            alternateStep = false;
            if (breathingSource != null)
            {
                breathingSource.volume = 0f;
                breathingSource.Stop();
            }
            if (stepSource != null)
            {
                stepSource.Stop();
            }
            if (navigator != null) navigator.ClearPath();
            Publish();
        }

        public void InitNavigation(P13RealidadWorld world)
        {
            if (world == null) return;
            navigation = new P13RealidadNavigation(world);
            navigator = new P13EnemyNavigator(world, navigation);
            if (Game != null)
            {
                perception = new P13EnemyPerception(Game, navigation);
            }
            if (navigation != null)
            {
                Position = navigation.Safe(Position);
                Publish();
            }
        }

        public void StartHunting()
        {
            IsHunting = true;
            State = RealidadEnemyState.Patrolling;
            decisionTimer = 10.0f; // Pausa inicial prudente de 10s en el punto de spawn (Almacén)
            memory = null;
            confidence = 0f;
            Alert = 0f;
            SeesPlayer = false;
            if (navigator != null) navigator.ClearPath();
        }

        // =====================================================================
        // EVENTOS Y REACCIONES DEL MAPA
        // =====================================================================

        public void TriggerMapEvent(string eventId, Vector3 eventPosition, float speedMultiplier = 1.5f, float duration = 10f)
        {
            if (Game != null && Game.IsPhoneCallActive) return;

            HasActiveEvent = true;
            ActiveEventId = eventId;
            EventTargetPos = eventPosition;
            EventSpeedMultiplier = Mathf.Max(1.0f, speedMultiplier);
            EventTimer = duration;

            Debug.Log($"[RealidadEnemy] Evento activado: {eventId} en {eventPosition} (Vel x{EventSpeedMultiplier:F1})");

            if (IsHunting && State != RealidadEnemyState.AtDoor)
            {
                State = RealidadEnemyState.Investigating;
                investigateWaitTimer = 2.5f;
                memory = eventPosition;
                RouteTo(eventPosition);
            }
        }

        public void AlertNoise(Vector3 noisePos, float intensity = 1f)
        {
            if (Game != null && Game.IsPhoneCallActive) return;
            if (!IsHunting || State == RealidadEnemyState.AtDoor) return;

            float dist = Vector3.Distance(Position, noisePos);
            if (dist < 18f * intensity)
            {
                lastHeard = clock;
                memory = noisePos;
                if (State != RealidadEnemyState.Hunting)
                {
                    State = RealidadEnemyState.Investigating;
                    investigateWaitTimer = 2.0f;
                    RouteTo(noisePos);
                }
            }
        }

        public void ClearMapEvent()
        {
            HasActiveEvent = false;
            ActiveEventId = "";
            EventSpeedMultiplier = 1f;
            EventTimer = 0f;
        }

        public void ApplySlow(float duration = 15f, float multiplier = 0.9f)
        {
            SlowTimer = duration;
            SlowMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
            Debug.Log($"[RealidadEnemy] Ralentizado a x{SlowMultiplier:F2} durante {SlowTimer:F1}s tras reporte de anomalía");
        }

        public void OnAnomalyReported()
        {
            ApplySlow(15f, 0.9f);
        }

        public void ApplySpeedBoost(float duration = 5f, float multiplier = 1.25f)
        {
            SpeedBoostTimer = duration;
            SpeedBoostMultiplier = Mathf.Max(1.0f, multiplier);
            SlowTimer = 0f; // Cancela cualquier ralentización previa
            Debug.Log($"[RealidadEnemy] Acelerado a x{SpeedBoostMultiplier:F2} durante {SpeedBoostTimer:F1}s por reporte incorrecto (penalización)");
        }

        public void OnFalseReport(Vector3 reportPosition)
        {
            ApplySpeedBoost(5f, 1.25f);
            AlertNoise(reportPosition, 0.75f);
        }

        // =====================================================================
        // RUTAS Y NAVEGACIÓN
        // =====================================================================

        void PickNextDestination()
        {
            if (navigator == null) return;

            if (HasActiveEvent && EventTimer > 0)
            {
                RouteTo(EventTargetPos);
                return;
            }

            int next = -1;
            if (neighbors != null && location >= 0 && location < neighbors.Length)
            {
                var choices = new List<int>(neighbors[location]);

                // Durante los primeros 120s o mientras la realidad sea alta (>= 50%),
                // evitar patrullar al nodo 4 (Archivo / Dormitorio donde despierta el jugador)
                // para que el enemigo no vaya directo a su cama al inicio.
                bool realityHigh = Game == null || Game.Sleep >= 50f;
                bool earlyGame = clock < 120f;
                if ((realityHigh || earlyGame) && choices.Count > 1)
                {
                    choices.Remove(4); // Excluir Archivo (dormitorio del jugador)
                }

                if (choices.Count > 0)
                {
                    next = choices[UnityEngine.Random.Range(0, choices.Count)];
                }
            }

            if (next < 0)
            {
                next = UnityEngine.Random.Range(0, destinations.Length);
                if ((Game == null || Game.Sleep >= 50f || clock < 120f) && next == 4)
                {
                    next = 8; // Reemplazar por Almacén si cayó en 4
                }
            }

            destination = next;
            RouteTo(destinations[destination]);
        }

        void RouteTo(Vector3 goal)
        {
            if (navigator == null) return;
            navigator.SetDestination(goal, Position, true);
        }

        // =====================================================================
        // ACTUALIZACIÓN PRINCIPAL
        // =====================================================================

        public void Tick(float dt)
        {
            clock += dt;
            UpdateAudio(dt);

            // Inicialización tardía de percepción si no existía
            if (perception == null && Game != null && navigation != null)
            {
                perception = new P13EnemyPerception(Game, navigation);
            }

            var eye = Position + Vector3.up * 1.2f;
            var playerTarget = Game != null ? (Game.PlayerPosition + Vector3.up * (Game.EyeHeight - 0.8f)) : Position;
            var delta = playerTarget - eye;
            var flat = new Vector3(delta.x, 0, delta.z);
            float distance = flat.magnitude;
            var forward = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;

            // Detección de visión directa
            bool clearSight = Game != null && Game.World != null && Game.World.Clear(eye, playerTarget);

            // Captura por proximidad física (contacto)
            if (distance < 1.6f && clearSight)
            {
                if (Game != null && Game.Phase == P13RealidadGame.GamePhase.Playing)
                {
                    var toPlayer = (Game.PlayerPosition - Position);
                    toPlayer.y = 0;
                    if (toPlayer.sqrMagnitude > 0.001f)
                    {
                        Yaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
                        Publish();
                    }

                    Game.TriggerCapture();
                    return;
                }
            }

            // Si está dormido (primeros segundos antes de cerrar la advertencia)
            if (!IsHunting || State == RealidadEnemyState.Dormant)
            {
                Speed = 0f;
                PlayAnimation("idle");
                Publish();
                return;
            }

            // Expiración de eventos temporales
            if (HasActiveEvent)
            {
                EventTimer -= dt;
                if (EventTimer <= 0) ClearMapEvent();
            }

            // 1. Evaluación de percepción según la barra de Realidad
            Vector3? perceptionTarget = null;
            bool forcePermanentHunt = false;
            float realitySpeedMultiplier = 1.0f;
            if (perception != null)
            {
                perception.Tick(dt, out perceptionTarget, out forcePermanentHunt, out realitySpeedMultiplier);
            }

            // 2. Visión del jugador
            // En la misma habitación el alcance es de 7.0m; a través de umbrales entre distintas salas se limita a 4.5m
            int playerRoom = Game != null ? P13RealidadRules.Room(Game.PlayerPosition) : 0;
            int enemyRoom = P13RealidadRules.Room(Position);
            bool sameRoom = playerRoom == enemyRoom;
            float baseMaxDist = sameRoom ? 7.0f : 4.5f;
            float maxVisionDist = (Game != null && Game.Crouched) ? Mathf.Min(baseMaxDist, 3.8f) : baseMaxDist;
            float visionAngleCos = Mathf.Cos(Mathf.PI * ((Game != null && Game.Crouched) ? 0.22f : 0.28f));
            SeesPlayer = distance < maxVisionDist &&
                         (distance < 0.8f || Vector3.Dot(forward, flat.normalized) >= visionAngleCos) &&
                         clearSight;

            if (SeesPlayer)
            {
                // A mayor distancia, el enemigo tarda más en confirmar lo que vio
                float confidenceRate;
                if (distance < 2.0f)
                {
                    confidenceRate = 3.5f; // A quemarropa la reacción es inmediata
                }
                else if (distance < 4.5f)
                {
                    confidenceRate = (Game != null && Game.Crouched) ? 0.7f : 1.8f;
                }
                else
                {
                    confidenceRate = (Game != null && Game.Crouched) ? 0.35f : 0.9f;
                }

                confidence = Mathf.Min(1f, confidence + dt * confidenceRate);
                lastSeen = clock;
                Alert = Mathf.Min(100f, Alert + dt * (Game != null && Game.Crouched ? 25f : 65f));

                // Solo fija la posición en memoria si superó el umbral de sospecha (0.35f) o si está muy cerca
                if (confidence >= 0.35f || distance < 2.0f)
                {
                    memory = Game != null ? Game.PlayerPosition : Position;
                }

                if (State != RealidadEnemyState.AtDoor && State != RealidadEnemyState.Retreating)
                {
                    // Si confianza >= 0.85f o a quemarropa (< 2.5m): entra en cacería (Hunting)
                    // Si confianza >= 0.35f: entra en sospecha/investigación (Investigating)
                    if (confidence >= 0.85f || distance < 2.5f)
                    {
                        State = RealidadEnemyState.Hunting;
                    }
                    else if (confidence >= 0.35f && State != RealidadEnemyState.Hunting)
                    {
                        State = RealidadEnemyState.Investigating;
                    }
                }
            }
            else
            {
                if (!forcePermanentHunt)
                {
                    confidence = Mathf.Max(0f, confidence - dt * 1.2f);
                    Alert = Mathf.Max(0f, Alert - dt * 4f);
                    if (State == RealidadEnemyState.Hunting && clock - lastSeen > 1.8f)
                    {
                        State = RealidadEnemyState.Investigating;
                        investigateWaitTimer = 2.0f;
                    }
                }
            }

            // Si la barra llega a 0: cacería permanente implacable
            if (forcePermanentHunt)
            {
                if (State != RealidadEnemyState.AtDoor && State != RealidadEnemyState.Retreating)
                {
                    State = RealidadEnemyState.Hunting;
                }
                memory = Game != null ? Game.PlayerPosition : Position;
            }
            // O si hay ping de percepción (realidad < 50%) y no ve al jugador directamente
            else if (perceptionTarget.HasValue && !SeesPlayer && State != RealidadEnemyState.AtDoor && State != RealidadEnemyState.Retreating)
            {
                memory = perceptionTarget.Value;
                if (State != RealidadEnemyState.Hunting)
                {
                    State = RealidadEnemyState.Investigating;
                    investigateWaitTimer = 2.0f;
                    RouteTo(perceptionTarget.Value);
                }
            }

            // =================================================================
            // ESTADO: En la puerta cerrada golpeando (AtDoor)
            // =================================================================
            if (State == RealidadEnemyState.AtDoor)
            {
                doorTimer -= dt;
                PlayAnimation("wall_brace_left");

                // Si el jugador abre la puerta, entra y caza
                if (Game != null && Game.World != null && !Game.World.DoorClosed)
                {
                    State = RealidadEnemyState.Hunting;
                    memory = Game.PlayerPosition;
                    RouteTo(Game.PlayerPosition);
                    return;
                }

                // Golpes rítmicos a la puerta
                if (Mathf.FloorToInt(doorTimer + dt) != Mathf.FloorToInt(doorTimer) && doorTimer > 1f)
                {
                    if (Game != null && Game.Audio != null)
                    {
                        Game.Audio.Spatial("knock", Position, 0.8f);
                    }
                    if (Game != null)
                    {
                        Game.DoorShake = 0.12f;
                    }
                }

                // Termina de golpear y se retira
                if (doorTimer <= 0f)
                {
                    State = RealidadEnemyState.Retreating;
                    destination = 3; // Pasillo
                    RouteTo(destinations[3]);
                    decisionTimer = 8f;
                }

                Publish();
                return;
            }

            // =================================================================
            // LÓGICA DE DESTINO SEGÚN ESTADO
            // =================================================================
            if (State == RealidadEnemyState.Hunting)
            {
                // Si perdió de vista al jugador y no hay memoria de su posición, regresa a patrullar
                if (!forcePermanentHunt && !SeesPlayer && !memory.HasValue)
                {
                    State = RealidadEnemyState.Patrolling;
                    decisionTimer = 2.0f;
                    PickNextDestination();
                    Publish();
                    return;
                }

                // Solo si tiene visión directa o persecución forzada por realidad=0% sigue la posición en tiempo real.
                // Si perdió de vista al jugador, persigue el último punto donde lo vio (memory).
                Vector3 target;
                if (forcePermanentHunt || SeesPlayer)
                {
                    target = Game != null ? Game.PlayerPosition : Position;
                }
                else
                {
                    target = memory.Value;
                }

                if (!SeesPlayer && !forcePermanentHunt && memory.HasValue && navigator != null && navigator.ReachedDestination)
                {
                    State = RealidadEnemyState.Investigating;
                    investigateWaitTimer = 2.5f;
                    memory = null;
                }

                bool office = Mathf.Abs(target.x) < 2.6f && Mathf.Abs(target.z) < 2.6f;
                bool approachDoor = Game != null && Game.World != null && Game.World.DoorClosed && office && Position.z < -3.4f;

                if (approachDoor)
                {
                    destination = 9;
                    if (Vector3.Distance(Position, destinations[9]) < 0.6f)
                    {
                        State = RealidadEnemyState.AtDoor;
                        doorTimer = 5.0f;
                        if (navigator != null) navigator.ClearPath();
                        if (Game != null && Game.Audio != null) Game.Audio.Spatial("knock", Position, 0.8f);
                        if (Game != null) Game.DoorShake = 0.12f;
                        return;
                    }
                    else
                    {
                        if (navigator != null) navigator.SetDestination(destinations[9], Position);
                    }
                }
                else
                {
                    destination = -1;
                    if (navigator != null)
                    {
                        bool force = !navigator.HasPath;
                        navigator.SetDestination(target, Position, force);
                    }
                }
            }
            else if (State == RealidadEnemyState.Investigating)
            {
                if (memory.HasValue)
                {
                    if (navigator != null) navigator.SetDestination(memory.Value, Position);

                    if (navigator != null && navigator.ReachedDestination)
                    {
                        investigateWaitTimer -= dt;
                        if (investigateWaitTimer <= 0f)
                        {
                            memory = null;
                            State = RealidadEnemyState.Patrolling;
                            decisionTimer = 1.0f;
                            PickNextDestination();
                        }
                    }
                }
                else
                {
                    State = RealidadEnemyState.Patrolling;
                    decisionTimer = 1.0f;
                    PickNextDestination();
                }
            }
            else if (State == RealidadEnemyState.Patrolling)
            {
                if (navigator != null && (navigator.ReachedDestination || !navigator.HasPath))
                {
                    decisionTimer -= dt;
                    if (decisionTimer <= 0f)
                    {
                        location = destination >= 0 ? destination : location;
                        decisionTimer = UnityEngine.Random.Range(3.5f, 6.0f);
                        PickNextDestination();
                    }
                }
            }
            else if (State == RealidadEnemyState.Retreating)
            {
                if (navigator != null && navigator.ReachedDestination)
                {
                    State = RealidadEnemyState.Patrolling;
                    decisionTimer = 1.5f;
                    PickNextDestination();
                }
            }

            // =================================================================
            // CÁLCULO DE VELOCIDAD Y MOVIMIENTO
            // =================================================================
            float sleepNorm = Game != null ? Mathf.Clamp01(Game.Sleep / 100f) : 1f;
            float baseSpeed = State == RealidadEnemyState.Hunting ? Mathf.Lerp(2.2f, 1.4f, sleepNorm) :
                              State == RealidadEnemyState.Investigating ? 1.25f : 0.95f;

            float desiredSpeed = baseSpeed;
            if (realitySpeedMultiplier > 1.0f)
            {
                desiredSpeed *= realitySpeedMultiplier; // +50% en realidad 0
            }
            if (HasActiveEvent)
            {
                desiredSpeed *= EventSpeedMultiplier;
            }
            if (SlowTimer > 0f)
            {
                SlowTimer -= dt;
                desiredSpeed *= SlowMultiplier; // -10% de velocidad (x0.9) tras reporte correcto
            }
            if (SpeedBoostTimer > 0f)
            {
                SpeedBoostTimer -= dt;
                desiredSpeed *= SpeedBoostMultiplier; // +25% de velocidad (x1.25) por 5s tras reporte incorrecto
            }

            Speed = Mathf.MoveTowards(Speed, desiredSpeed, dt * 2.8f);

            bool moving = navigator != null && navigator.HasPath && Speed > 0.05f;
            if (moving)
            {
                float newYaw;
                float travelled;
                Position = navigator.TickMovement(Position, Speed, dt, Yaw, out newYaw, out travelled);
                Yaw = newYaw;

                stepDistance += travelled;
                float currentInterval = State == RealidadEnemyState.Hunting ? 0.65f : FootstepInterval;
                if (stepDistance > currentInterval)
                {
                    stepDistance = 0f;
                    PlayFootstepSound();
                }

                string animName = State == RealidadEnemyState.Hunting ? "run" : State == RealidadEnemyState.Investigating ? "walk" : "patrol";
                PlayAnimation(animName, Mathf.Clamp(Speed / (State == RealidadEnemyState.Hunting ? 2.2f : 0.95f), 0.7f, 1.5f));
            }
            else
            {
                Speed = 0f;
                stepDistance = 0f;
                PlayAnimation("idle");
            }

            Publish();
        }

        public void PlayAnimation(string name, float speed = 1f, bool loop = true)
        {
            if (Anim == null) return;

            if (Anim[name] == null)
            {
                if (Anim["idle"] == null) return;
                name = "idle";
            }

            if (currentAnimPlaying != name)
            {
                Anim.CrossFade(name, 0.22f);
                currentAnimPlaying = name;
            }

            Anim[name].wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            Anim[name].speed = speed;
        }

        public void Pause(bool p)
        {
            if (Anim != null)
            {
                foreach (AnimationState s in Anim)
                {
                    s.speed = p ? 0f : 1f;
                }
            }
            if (breathingSource != null)
            {
                if (p && breathingSource.isPlaying) breathingSource.Pause();
                else if (!p && breathingSource.volume > 0.01f) breathingSource.UnPause();
            }
            if (stepSource != null && p && stepSource.isPlaying)
            {
                stepSource.Pause();
            }
        }

        void UpdateAudio(float dt)
        {
            if (breathingSource == null) SetupAudio();
            if (breathingSource == null || Game == null) return;

            // Si el juego está pausado, en captura o la advertencia está activa, pausar audio
            if (Game.ShowModal || Game.Phase != P13RealidadGame.GamePhase.Playing)
            {
                if (breathingSource.isPlaying) breathingSource.Pause();
                if (stepSource != null && stepSource.isPlaying) stepSource.Pause();
                return;
            }
            else
            {
                if (!breathingSource.isPlaying && State != RealidadEnemyState.Dormant && BreathingClip != null)
                {
                    breathingSource.UnPause();
                    if (!breathingSource.isPlaying) breathingSource.Play();
                }
            }

            // En reposo inicial (Dormant), la respiración está silenciada
            if (State == RealidadEnemyState.Dormant)
            {
                breathingSource.volume = 0f;
                return;
            }

            // Distancia al jugador
            float dist = Vector3.Distance(Position, Game.PlayerPosition);

            // Factor de cercanía (1 en contacto directo, 0 a MaxHearingDistance)
            float proximity = Mathf.Clamp01(1f - (dist / MaxHearingDistance));

            // Curva exponencial cuadrática para que la cercanía (< 5m) se sienta muy intensa
            float volFactor = Mathf.Pow(proximity, 1.35f);

            // En persecución (Hunting) la respiración es más intensa y agitada
            bool hunting = State == RealidadEnemyState.Hunting;
            float targetVolume = volFactor * MaxBreathingVolume * (hunting ? 1.0f : 0.75f);
            float targetPitch = hunting ? 1.12f : 0.95f;

            breathingSource.volume = Mathf.MoveTowards(breathingSource.volume, targetVolume, dt * 1.5f);
            breathingSource.pitch = Mathf.MoveTowards(breathingSource.pitch, targetPitch, dt * 0.8f);

            if (breathingSource.volume > 0.01f && !breathingSource.isPlaying && BreathingClip != null)
            {
                breathingSource.Play();
            }
        }

        void PlayFootstepSound()
        {
            if (stepSource == null) return;
            if (Game == null || Game.ShowModal || Game.Phase != P13RealidadGame.GamePhase.Playing) return;

            float dist = Vector3.Distance(Position, Game.PlayerPosition);
            if (dist > MaxHearingDistance + 2f) return;

            // Factor de volumen por cercanía
            float proximity = Mathf.Clamp01(1f - (dist / (MaxHearingDistance + 2f)));
            float vol = Mathf.Pow(proximity, 1.25f) * MaxFootstepVolume * (State == RealidadEnemyState.Hunting ? 1.0f : 0.85f);

            // Alternar entre paso normal (FeetClip) y arrastre (DraggingClip)
            alternateStep = !alternateStep;
            AudioClip clipToPlay = alternateStep ? FeetClip : DraggingClip;
            if (clipToPlay == null) clipToPlay = FeetClip ?? DraggingClip;

            if (clipToPlay != null)
            {
                stepSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                stepSource.PlayOneShot(clipToPlay, vol);
            }
        }

        void OnDestroy()
        {
            if (breathingSource != null)
            {
                breathingSource.Stop();
            }
            if (stepSource != null)
            {
                stepSource.Stop();
            }
        }

        public float Interference(Vector3 cameraPos)
        {
            float d = Vector3.Distance(Position + Vector3.up * 1.2f, cameraPos);
            return d < 6f ? (1f - d / 6f) * 0.7f : 0f;
        }

        public void Publish()
        {
            if (Root != null)
            {
                Root.position = Position;
                Root.rotation = Quaternion.Euler(0, Yaw, 0);
            }
        }
    }
}
