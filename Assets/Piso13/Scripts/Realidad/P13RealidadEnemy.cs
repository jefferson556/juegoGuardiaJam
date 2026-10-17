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

        public static Action<string, Vector3, float> OnGlobalMapEvent;

        // Nodos del mapa idénticos a P13Guard en Piso 13
        public static readonly Vector3[] destinations = {
            P13Rules.RK(0, .8f, .8f),      // 0: Oficinas
            P13Rules.RK(-6.8f, .8f, .6f),   // 1: Recepcion
            P13Rules.RK(0, .8f, 7.55f),     // 2: Ascensor
            P13Rules.RK(0, .8f, 4.5f),      // 3: Pasillo central
            P13Rules.RK(-6.2f, .8f, 5.9f),  // 4: Archivo
            P13Rules.RK(6.2f, .8f, 5.9f),   // 5: Servidores
            P13Rules.RK(6.8f, .8f, .6f),    // 6: Tecnica
            P13Rules.RK(-6.8f, .8f, -6.2f), // 7: Abandonada
            P13Rules.RK(6.8f, .8f, -6.2f),  // 8: Almacen (Spawn)
            P13Rules.RK(0, .8f, 3.85f)      // 9: Puerta
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

        P13Navigation navigation;
        List<Vector3> route = new List<Vector3>();
        int routeIndex = 0;
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

            // Instanciar modelo utilizando la conversión de ejes oficial de P13World
            Visual = P13World.Model(modelName, Root).transform;

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
            Publish();
        }

        public void InitNavigation(P13World world)
        {
            navigation = new P13Navigation(world);
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
            decisionTimer = 1.5f;
            PickNextDestination();
        }

        // =====================================================================
        // EVENTOS Y REACCIONES DEL MAPA
        // =====================================================================

        public void TriggerMapEvent(string eventId, Vector3 eventPosition, float speedMultiplier = 1.5f, float duration = 10f)
        {
            HasActiveEvent = true;
            ActiveEventId = eventId;
            EventTargetPos = eventPosition;
            EventSpeedMultiplier = Mathf.Max(1.0f, speedMultiplier);
            EventTimer = duration;

            Debug.Log($"[RealidadEnemy] Evento activado: {eventId} en {eventPosition} (Vel x{EventSpeedMultiplier:F1})");

            if (IsHunting && State != RealidadEnemyState.AtDoor)
            {
                State = RealidadEnemyState.Investigating;
                memory = eventPosition;
                RouteTo(eventPosition);
            }
        }

        public void AlertNoise(Vector3 noisePos, float intensity = 1f)
        {
            if (!IsHunting || State == RealidadEnemyState.AtDoor) return;

            float dist = Vector3.Distance(Position, noisePos);
            if (dist < 18f * intensity)
            {
                lastHeard = clock;
                memory = noisePos;
                if (State != RealidadEnemyState.Hunting)
                {
                    State = RealidadEnemyState.Investigating;
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

        // =====================================================================
        // RUTAS Y NAVEGACIÓN
        // =====================================================================

        void PickNextDestination()
        {
            if (navigation == null) return;

            if (HasActiveEvent && EventTimer > 0)
            {
                RouteTo(EventTargetPos);
                return;
            }

            if (neighbors != null && location >= 0 && location < neighbors.Length)
            {
                var choices = neighbors[location];
                destination = choices[UnityEngine.Random.Range(0, choices.Length)];
                RouteTo(destinations[destination]);
            }
            else
            {
                destination = UnityEngine.Random.Range(0, destinations.Length);
                RouteTo(destinations[destination]);
            }
        }

        void RouteTo(Vector3 goal)
        {
            if (navigation == null) return;
            route = navigation.Path(Position, goal);
            routeIndex = 0;
        }

        // =====================================================================
        // ACTUALIZACIÓN PRINCIPAL
        // =====================================================================

        public void Tick(float dt)
        {
            clock += dt;
            replanTimer -= dt;
            decisionTimer -= dt;

            var eye = Position + Vector3.up * 1.2f;
            var playerTarget = Game != null ? (Game.PlayerPosition + Vector3.up * (Game.EyeHeight - 0.8f)) : Position;
            var delta = playerTarget - eye;
            var flat = new Vector3(delta.x, 0, delta.z);
            float distance = flat.magnitude;
            var forward = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;

            // Detección visual tal cual en Piso 13
            bool clearSight = Game != null && Game.World != null && Game.World.Clear(eye, playerTarget);

            // Si el jugador entra en contacto cercano con el monstruo (incluso si está en reposo), es atrapado
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

            // Si está dormido (primeros 30s)
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

            SeesPlayer = distance < (Game != null && Game.Crouched ? 5.5f : 11f) &&
                         (distance < 0.8f || Vector3.Dot(forward, flat.normalized) >= Mathf.Cos(Mathf.PI * (Game != null && Game.Crouched ? 0.25f : 0.3f))) &&
                         clearSight;

            if (SeesPlayer)
            {
                confidence = Mathf.Min(1f, confidence + dt * (Game != null && Game.Crouched && distance > 1.2f ? 0.8f : 3.5f));
                lastSeen = clock;
                memory = Game != null ? Game.PlayerPosition : Position;
                Alert = Mathf.Min(100f, Alert + dt * (Game != null && Game.Crouched ? 30f : 95f));

                if (State != RealidadEnemyState.AtDoor && State != RealidadEnemyState.Retreating)
                {
                    State = confidence >= 0.4f ? RealidadEnemyState.Hunting : RealidadEnemyState.Investigating;
                }
            }
            else
            {
                confidence = Mathf.Max(0f, confidence - dt * 1.5f);
                Alert = Mathf.Max(0f, Alert - dt * 4f);
                if (State == RealidadEnemyState.Hunting && clock - lastSeen > 1.8f)
                {
                    State = RealidadEnemyState.Investigating;
                }
            }

            // =================================================================
            // CAPTURA: Cuando el enemigo alcanza al jugador a distancia de contacto
            // =================================================================
            if (distance < 1.6f && clearSight)
            {
                if (Game != null && Game.Phase == P13RealidadGame.GamePhase.Playing)
                {
                    // Orientar de inmediato al enemigo hacia el jugador para contacto cara a cara
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
            // LÓGICA DE DESTINO Y PERSECUCIÓN
            // =================================================================
            // Si ve directamente al jugador y puede caminar hacia él sin obstáculos intermedios
            if (SeesPlayer && Game != null && Game.World != null && Game.World.Clear(Position, Game.PlayerPosition, 0.38f, true))
            {
                route.Clear();
                route.Add(Game.PlayerPosition);
                routeIndex = 0;
            }
            else if ((State == RealidadEnemyState.Hunting || State == RealidadEnemyState.Investigating) && memory.HasValue && replanTimer <= 0f)
            {
                replanTimer = 0.5f;
                var remembered = memory.Value;
                bool office = Mathf.Abs(remembered.x) < 2.6f && Mathf.Abs(remembered.z) < 2.6f;
                bool approachDoor = Game != null && Game.World != null && Game.World.DoorClosed && office && Position.z < -3.4f;

                RouteTo(approachDoor ? destinations[9] : remembered);
                destination = approachDoor ? 9 : -1;
            }

            // Si patrulla y terminó la ruta, decidir siguiente habitación
            if (State == RealidadEnemyState.Patrolling && routeIndex >= route.Count && decisionTimer <= 0f)
            {
                decisionTimer = 4.5f;
                PickNextDestination();
            }

            // =================================================================
            // MOVIMIENTO
            // =================================================================
            bool moving = routeIndex < route.Count;
            if (moving)
            {
                var goal = route[routeIndex];
                var d = goal - Position;
                d.y = 0;

                if (d.magnitude < 0.2f)
                {
                    routeIndex++;
                    if (routeIndex >= route.Count)
                    {
                        location = destination >= 0 ? destination : location;

                        // Si llegó a la puerta de la oficina y está cerrada
                        if (destination == 9 && Game != null && Game.World != null && Game.World.DoorClosed)
                        {
                            State = RealidadEnemyState.AtDoor;
                            doorTimer = 5.0f;
                            if (Game.Audio != null) Game.Audio.Spatial("knock", Position, 0.8f);
                            if (Game != null) Game.DoorShake = 0.12f;
                            return;
                        }

                        if (State == RealidadEnemyState.Retreating)
                        {
                            State = RealidadEnemyState.Patrolling;
                        }
                    }
                }
                else
                {
                    // Velocidades moduladas
                    float sleepNorm = Game != null ? Mathf.Clamp01(Game.Sleep / 100f) : 1f;
                    float baseSpeed = State == RealidadEnemyState.Hunting ? Mathf.Lerp(2.2f, 1.4f, sleepNorm) :
                                      State == RealidadEnemyState.Investigating ? 1.15f : 0.85f;

                    float desiredSpeed = baseSpeed * (HasActiveEvent ? EventSpeedMultiplier : 1f);
                    Speed = Mathf.MoveTowards(Speed, desiredSpeed, dt * 2.5f);

                    var next = Position + d.normalized * Mathf.Min(d.magnitude, Speed * dt);

                    if (Game != null && Game.World != null && Game.World.Clear(Position, next, 0.38f, true))
                    {
                        Position = next;
                    }
                    else
                    {
                        route.Clear();
                        replanTimer = 0f;
                    }

                    Yaw = Mathf.MoveTowardsAngle(Yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, dt * 170f);

                    // Pasos de audio
                    float travelled = d.normalized.magnitude * Mathf.Min(d.magnitude, Speed * dt);
                    stepDistance += travelled;
                    if (stepDistance > 0.75f)
                    {
                        stepDistance = 0f;
                        if (Game != null && Game.Audio != null)
                        {
                            Game.Audio.Spatial("guard_step", Position, 0.5f);
                        }
                    }

                    string animName = State == RealidadEnemyState.Hunting ? "run" : State == RealidadEnemyState.Investigating ? "walk" : "patrol";
                    PlayAnimation(animName, Mathf.Clamp(Speed / (State == RealidadEnemyState.Hunting ? 2.2f : 0.85f), 0.7f, 1.5f));
                }
            }
            else
            {
                Speed = 0f;
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
            if (Anim == null) return;
            foreach (AnimationState s in Anim)
            {
                s.speed = p ? 0f : 1f;
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
