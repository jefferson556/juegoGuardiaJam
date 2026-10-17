using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13
{
    public enum GamePhase
    {
        Menu,
        Playing,
        Paused,
        Capture,
        Lost,
        Won,
        Error
    }

    public sealed class P13Game : MonoBehaviour
    {
        public static P13Game Instance;
        public WorldData Data;
        public P13World World;
        public P13Input Input;
        public P13Settings Settings;
        public P13Audio Audio;
        public P13Guard Guard;
        public P13Building Building;
        public P13Horror Horror;
        public Camera View;
        public P13UI UI;
        public GamePhase Phase = GamePhase.Menu;
        public Vector3 Player = P13Rules.RK(0, .8f, 1.2f);
        public float Yaw = 0, Pitch, EyeHeight = 1.65f, Energy = 100, DoorShake, WeatherFlash;
        public int Minute, Night = 1, Floor = 13, CameraIndex;
        public bool Demo = true, Crouched, Flashlight = true, CCTV, Map, DoorLight, Technical, Servers, Archive, Returned, Watched, SetPieceCompleted;
        public readonly List<int> Discovered = new List<int>();
        public readonly List<string> Clues = new List<string>();
        public string Message = "", Subtitle = "", Speaker = "", Failure = "";
        public int Task = -1;
        public float TaskRemaining, TaskDuration;
        float secondClock, saveClock, subtitleClock, noiseDistance, headPhase, captureTime;
        Vector2 cctvLook;
        Light flashlight, officeLight, weatherLight;
        Transform hand;
        bool built;
        readonly Queue<Tuple<string, string, float>> subtitles = new Queue<Tuple<string, string, float>>();
        public bool Threatened => Guard != null && (Guard.State == GuardState.Hunting || Guard.State == GuardState.AtDoor || Phase == GamePhase.Capture);
        public Vector3 Observer => View ? View.transform.position : Player;
        public string Objective => !Technical ? "Repara el circuito en Técnica (CAM 07)." : !Servers ? "Reinicia los servidores (CAM 06)." : !Demo && !Archive ? "Coteja el archivo (CAM 05)." : !Returned ? "Regresa al puesto y registra la ronda." : Demo && !Watched ? "Consulta el CCTV antes de entregar el turno." : "Ronda registrada. Sobrevive hasta las 06:00.";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Only the Piso 13 prototype scene runs this game; other scenes have their own systems.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Piso13")
                return;
            if (!FindFirstObjectByType<P13Game>())
                new GameObject("Piso13").AddComponent<P13Game>();
        }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            try
            {
                var data = Resources.Load<TextAsset>("Data/World");
                if (!data)
                    throw new Exception("Falta Data/World.json");
                Data = JsonUtility.FromJson<WorldData>(data.text);
                World = new P13World(Data);
                Settings = P13Save.Read<P13Settings>("settings-v1.json") ?? new P13Settings();
                Settings.Validate();
                Input = new P13Input(Settings);
                var camera = new GameObject("PlayerCamera");
                View = camera.AddComponent<Camera>();
                View.tag = "MainCamera";
                View.nearClipPlane = .035f;
                View.farClipPlane = 50;
                View.fieldOfView = 72;
                View.clearFlags = CameraClearFlags.SolidColor;
                View.backgroundColor = Color.black;
                camera.AddComponent<AudioListener>();
                Audio = gameObject.AddComponent<P13Audio>();
                Audio.Setup(Settings);
                UI = gameObject.AddComponent<P13UI>();
                UI.Game = this;
                Settings.Apply();
                CursorState();
            }
            catch (Exception e)
            {
                Failure = e.ToString();
                Phase = GamePhase.Error;
                if (!UI)
                {
                    UI = gameObject.AddComponent<P13UI>();
                    UI.Game = this;
                }

                Debug.LogException(e);
            }
        }

        void Build()
        {
            if (built)
                return;
            World.Build();
            Guard = new P13Guard(this);
            Building = new P13Building(this);
            Horror = new P13Horror(this);
            hand = P13World.Model("Piso13_HandFlashlight", View.transform).transform;
            flashlight = new GameObject("Linterna").AddComponent<Light>();
            flashlight.transform.SetParent(View.transform, false);
            flashlight.transform.localPosition = new Vector3(.18f, -.16f, .55f);
            flashlight.type = LightType.Spot;
            flashlight.range = 20;
            flashlight.spotAngle = 55;
            flashlight.innerSpotAngle = 24;
            flashlight.intensity = 5f;
            flashlight.color = new Color(1, .95f, .87f);
            flashlight.shadows = LightShadows.Soft;
            officeLight = new GameObject("LuzPuerta").AddComponent<Light>();
            officeLight.transform.SetParent(World.Root);
            officeLight.transform.position = P13Rules.RK(0, 2.7f, 3.6f);
            officeLight.range = 7.5f;
            officeLight.intensity = 2.2f;
            officeLight.type = LightType.Point;
            weatherLight = new GameObject("Relámpago").AddComponent<Light>();
            weatherLight.transform.SetParent(World.Root);
            weatherLight.type = LightType.Point;
            weatherLight.range = 5;
            weatherLight.color = new Color(.65f, .78f, 1);
            built = true;
        }

        public P13Checkpoint LoadSave()
        {
            var s = P13Save.Read<P13Checkpoint>("unity-checkpoint-v1.json");
            return s != null && s.Valid(World) ? s : null;
        }

        public void StartDemo()
        {
            if (!P13Save.Clear())
            {
                Message = P13Save.Error;
                return;
            }

            StartSession(null);
        }

        public bool CanContinue => LoadSave() != null || PlayerPrefs.GetInt("P13.campaignNight", 0) > 0;
        public void Continue()
        {
            var s = LoadSave();
            if (s != null)
                StartSession(s);
            else if (PlayerPrefs.GetInt("P13.campaignNight", 0) > 0)
                StartNight(PlayerPrefs.GetInt("P13.campaignNight", 1));
            else
                Message = "No hay checkpoint válido.";
        }

        public void StartNight(int night)
        {
            StartSession(null);
            if (Phase != GamePhase.Playing)
                return;
            Demo = false;
            Night = Mathf.Clamp(night, 1, 5);
            Save();
        }

        public void StartSession(P13Checkpoint save)
        {
            try
            {
                Build();
                World.Root.gameObject.SetActive(true);
                Player = P13Rules.RK(0, .8f, 1.2f);
                Yaw = 0;
                Pitch = 0;
                EyeHeight = 1.65f;
                Minute = 0;
                Energy = 100;
                Night = 1;
                Floor = 13;
                Demo = true;
                Technical = Servers = Archive = Returned = Watched = false;
                CCTV = Map = Crouched = DoorLight = false;
                World.DoorClosed = false;
                Task = -1;
                Flashlight = true;
                SetPieceCompleted = false;
                Clues.Clear();
                Discovered.Clear();
                subtitles.Clear();
                Subtitle = "";
                Message = "";
                Guard.Reset();
                Horror.End();
                Horror = new P13Horror(this);
                var fresh = new P13Checkpoint{player = Player, guard = Guard.Position, doors = Data.doors.Select(d => new DoorSave{id = d.id, open = d.floor == 13 && !d.locked}).ToList()};
                Building.Restore(save ?? fresh);
                if (save != null)
                {
                    Player = save.player;
                    Yaw = save.yaw;
                    Pitch = save.pitch;
                    Minute = save.minute;
                    Energy = save.energy;
                    Demo = save.demo;
                    Night = save.night;
                    Floor = save.floor;
                    Technical = save.technical;
                    Servers = save.servers;
                    Archive = save.archive;
                    Returned = save.returned;
                    Watched = save.watchedCCTV;
                    World.DoorClosed = save.doorClosed;
                    Flashlight = save.flashlight;
                    SetPieceCompleted = save.setPieceCompleted;
                    Clues.AddRange(save.clues);
                    Discovered.AddRange(save.discovered);
                    Guard.Restore(save);
                }

                Phase = GamePhase.Playing;
                secondClock = saveClock = noiseDistance = headPhase = 0;
                Audio.Menu(false);
                Audio.SetGameplay(true);
                Input.Neutralize();
                UI.Reset();
                CursorState();
                UpdateCamera(0);
                Debug.Log("P13_UNITY_READY demo=" + Demo + " floor=" + Floor);
                Save();
            }
            catch (Exception e)
            {
                Phase = GamePhase.Error;
                Failure = e.ToString();
                Debug.LogException(e);
                CursorState();
            }
        }

        void Update()
        {
            if (Phase == GamePhase.Error)
                return;
            float dt = Mathf.Min(.05f, Time.unscaledDeltaTime);
            if (Phase == GamePhase.Menu || Phase == GamePhase.Paused || Phase == GamePhase.Won || Phase == GamePhase.Lost)
            {
                UI.Navigation();
                return;
            }

            if (Phase == GamePhase.Capture)
            {
                CaptureTick(dt);
                return;
            }

            if (UI.Options)
            {
                UI.Navigation();
                return;
            }

            // Context ownership: CCTV/map consume close before pause is considered.
            if (CCTV)
            {
                if (P13Input.Back || Input.Pressed("cctv") || P13Input.Key("C", true))
                {
                    CCTV = false;
                    Input.Neutralize();
                    cctvLook = Vector2.zero;
                    CursorState();
                }
                else
                {
                    int step = P13Input.Horizontal;
                    if (step != 0)
                        CameraIndex = (CameraIndex + step + 9) % 9;
                    cctvLook += Input.Look(dt);
                    cctvLook.x = Mathf.Clamp(cctvLook.x, -35, 35);
                    cctvLook.y = Mathf.Clamp(cctvLook.y, -20, 20);
                }
            }
            else if (Map)
            {
                if (P13Input.Back || Input.Pressed("map"))
                {
                    Map = false;
                    Input.Neutralize();
                    CursorState();
                }
            }
            else if (P13Input.Back)
            {
                Pause(true);
                return;
            }
            else
            {
                var look = Input.Look(dt);
                Yaw += look.x;
                Pitch = Mathf.Clamp(Pitch + look.y, -63, 63);
                if (Input.Pressed("crouch"))
                    Crouched = !Crouched;
                if (Input.Pressed("flashlight"))
                {
                    Flashlight = !Flashlight;
                    Audio.Spatial("flashlight", Player, .2f);
                }

                if (Input.Pressed("cctv") && Floor == 13 && Task < 0)
                {
                    CCTV = true;
                    Watched = true;
                    Input.Neutralize();
                    CursorState();
                }

                if (Input.Pressed("map") && Task < 0)
                {
                    Map = true;
                    Input.Neutralize();
                    CursorState();
                }

                if (Input.Pressed("door"))
                    ToggleDoor();
                if (Input.Pressed("light") && Floor == 13 && Energy > 0)
                {
                    DoorLight = !DoorLight;
                    Guard.Hear(Player, 2);
                }

                if (Input.Pressed("interact"))
                    Interact();
                if (Input.Pressed("examine"))
                    Examine();
                if (Input.Pressed("cancel"))
                {
                    Task = -1;
                    Message = "Tarea cancelada.";
                }
            }

            var before = Player;
            bool movingAllowed = !CCTV && !Map && Task < 0 && !Building.Lift.Moving;
            bool running = Input.Held("sprint") && !Crouched;
            if (movingAllowed)
            {
                var axis = Input.Movement();
                var rotation = Quaternion.Euler(0, Yaw, 0);
                var delta = rotation * new Vector3(axis.x, 0, axis.y) * (Crouched ? .85f : running ? 4.2f : 2) * dt;
                Player = World.Move(Player, delta, .34f);
                if (Building.Inside)
                    Player.z = Mathf.Max(Player.z, -7.05f);
            }

            float travelled = Vector3.Distance(before, Player);
            Audio.Player(dt, travelled, running, Crouched, movingAllowed);
            if (travelled < .0001f)
                noiseDistance = 0;
            noiseDistance += travelled;
            if (noiseDistance >= (running ? .85f : .65f))
            {
                noiseDistance = 0;
                if (!Crouched)
                    Guard.Hear(Player, running ? 10 : 4.5f);
            }

            headPhase += travelled * Mathf.PI / (running ? .85f : .65f);
            EyeHeight = Mathf.Lerp(EyeHeight, Crouched ? 1 : 1.65f, dt * 10);
            View.fieldOfView = Mathf.Lerp(View.fieldOfView, running && travelled > .001f ? 78 : 72, dt * 10);
            secondClock += dt;
            saveClock += dt;
            if (secondClock >= 1)
            {
                secondClock -= 1;
                Second();
                if (Phase != GamePhase.Playing)
                    return;
            }

            Building.Tick(dt);
            Guard.Tick(dt);
            if (Phase != GamePhase.Playing)
                return;
            Horror.Tick(dt);
            UpdateSubtitles(dt);
            if (Night >= 3 && Floor == 13 && Clues.Count >= 8 && !SetPieceCompleted && !CCTV && string.IsNullOrEmpty(Subtitle) && (P13Rules.Room(Player) == 3 || P13Rules.Room(Player) == 4) && Player.z < -3 && !Threatened)
            {
                Guard.Hear(P13Rules.RK(0, .8f, 4.7f), 20);
                float d = Vector3.Distance(Player, Guard.Position);
                if (d > 4 && d < 8 && World.Clear(Guard.Position + Vector3.up, Player))
                    Horror.RequestSequence();
            }

            UpdateCamera(dt);
            World.UpdateLights(Observer, Mathf.Max(0, Settings.LightBudget - (Flashlight && !CCTV ? 1 : 0) - (DoorLight ? 1 : 0)), 1, Settings.brightness);
            foreach (var l in World.Lights)
                l.intensity *= Horror.Multiplier(P13Rules.Room(l.transform.position)) * (Building.AlteredLandingTime > 0 ? .45f : 1);
            World.UpdateEmitters(Horror.Multiplier);
            officeLight.enabled = DoorLight && Energy > 0 && Floor == 13;
            officeLight.intensity = 1.5f * Horror.Multiplier(1);
            weatherLight.enabled = WeatherFlash > .01f && !CCTV;
            weatherLight.transform.position = Observer + Vector3.up;
            weatherLight.intensity = WeatherFlash * 2;
            DoorShake = Mathf.MoveTowards(DoorShake, 0, dt);
            World.OfficeDoor.position += Vector3.right * Mathf.Sin(Time.unscaledTime * 61) * DoorShake;
            if (saveClock >= 30)
            {
                Save();
                saveClock = 0;
            }
        }

        void Second()
        {
            Minute = Mathf.Min(360, Minute + (Demo ? 2 : 1));
            if (P13Rules.Won(Minute, Returned, Watched, Demo))
            {
                Phase = GamePhase.Won;
                PlayerPrefs.SetInt("P13.campaignNight", Demo ? Mathf.Max(1, PlayerPrefs.GetInt("P13.campaignNight", 0)) : Mathf.Min(5, Night + 1));
                PlayerPrefs.Save();
                P13Save.Clear();
                Building.Interrupt();
                Horror.End();
                Audio.SetGameplay(false);
                Guard.Character.Pause(true);
                CursorState();
                return;
            }

            Energy = Mathf.Max(0, Energy - P13Rules.Drain(World.DoorClosed, DoorLight, CCTV, Task >= 0));
            if (Energy == 0)
            {
                if (World.DoorClosed)
                    Audio.Spatial("P13_Door_Open", World.OfficeDoor.position);
                World.DoorClosed = DoorLight = false;
            }

            if (Floor == 13)
            {
                int room = P13Rules.Room(Player);
                if (!Discovered.Contains(room))
                    Discovered.Add(room);
            }

            if (Task >= 0)
            {
                TaskRemaining--;
                Guard.Hear(Player, 3);
                if (TaskRemaining <= 0)
                {
                    if (Task == 0)
                        Technical = true;
                    else if (Task == 1)
                        Servers = true;
                    else
                        Archive = true;
                    Task = -1;
                    Message = "Tarea completada.";
                    Save();
                }
            }
        }

        public void ToggleDoor()
        {
            if (Floor != 13 || Energy <= 0)
                return;
            float distance = Vector3.Distance(Player, P13Rules.RK(0, .8f, 2.35f));
            if (distance > 3)
            {
                Message = "Acércate al panel de la puerta.";
                return;
            }

            if (!World.DoorClosed && (Data.officeDoor.Overlaps(Player, .34f) || Data.officeDoor.Overlaps(Guard.Position, .54f)))
            {
                Message = "Paso ocupado.";
                return;
            }

            World.DoorClosed = !World.DoorClosed;
            World.Revision++;
            Audio.Spatial(World.DoorClosed ? "P13_Door_Latch" : "P13_Door_Open", P13Rules.RK(0, 1.2f, 3), .45f);
            Guard.Hear(Player, 12);
        }

        bool Near(float x, float z, float radius = 1.4f)
        {
            return Building.Near(P13Rules.RK(x, .8f, z), radius);
        }

        void Interact()
        {
            if (Task >= 0 || CCTV || Map)
                return;
            if (Building.DoorInteract() || Building.Interact())
                return;
            if (Near(0, 2.35f, .9f))
            {
                ToggleDoor();
                return;
            }

            if (Near(5.2f, -1) && !Technical)
            {
                StartTask(0, 8);
                return;
            }

            if (Near(4.5f, 4.5f) && Technical && !Servers)
            {
                StartTask(1, 10);
                return;
            }

            if (Near(-4.5f, 4.5f) && !Demo && Servers && !Archive)
            {
                StartTask(2, 7);
                return;
            }

            if (Near(0, 1.2f) && (Demo ? Servers : Archive))
            {
                Returned = true;
                Message = "Ronda registrada.";
                Save();
            }
        }

        void StartTask(int task, float seconds)
        {
            Task = task;
            TaskDuration = TaskRemaining = seconds;
            Guard.Hear(Player, 18);
        }

        void Examine()
        {
            if (CCTV || Map || Task >= 0 || Clues.Count >= Data.narrative.Length)
                return;
            var c = Data.narrative[Clues.Count];
            if (Building.Near(c.position))
            {
                Clues.Add(c.id);
                Say(c.speaker, c.text, Mathf.Max(5, c.text.Length / 18f));
                Say("CENTRAL", c.reply, Mathf.Max(4, c.reply.Length / 18f));
                Save();
            }
        }

        public void Say(string speaker, string text, float seconds)
        {
            subtitles.Enqueue(Tuple.Create(speaker, text, seconds));
            if (string.IsNullOrEmpty(Subtitle))
                NextSubtitle();
        }

        void NextSubtitle()
        {
            if (subtitles.Count == 0)
            {
                Subtitle = "";
                Speaker = "";
                return;
            }

            var line = subtitles.Dequeue();
            Speaker = line.Item1;
            Subtitle = line.Item2;
            subtitleClock = line.Item3;
        }

        void UpdateSubtitles(float dt)
        {
            if (string.IsNullOrEmpty(Subtitle))
                return;
            subtitleClock -= dt;
            if (subtitleClock <= 0)
                NextSubtitle();
        }

        void UpdateCamera(float dt)
        {
            if (CCTV)
            {
                var c = Data.cameras[CameraIndex];
                View.transform.position = c.position;
                View.transform.rotation = Quaternion.LookRotation(c.target - c.position) * Quaternion.Euler(-cctvLook.y, cctvLook.x, 0);
                View.fieldOfView = 68;
            }
            else
            {
                View.transform.position = new Vector3(Player.x, EyeHeight, Player.z);
                View.transform.rotation = Quaternion.Euler(-Pitch, Yaw, 0);
            }

            if (hand)
            {
                hand.gameObject.SetActive(!CCTV && !Map);
                float amplitude = Crouched ? .00065f : Input.Held("sprint") ? .013f : .004f;
                var desired = new Vector3(Mathf.Sin(headPhase) * amplitude * .6f, Mathf.Cos(headPhase * 2) * amplitude, Mathf.Sin(headPhase * 2) * amplitude * .3f);
                bool blocked = !World.Clear(View.transform.position, View.transform.TransformPoint(new Vector3(.18f, -.16f, .8f)), .12f);
                if (blocked)
                    desired += new Vector3(-.08f, -.035f, -.36f);
                hand.localPosition = blocked ? desired : Vector3.Lerp(hand.localPosition, desired, dt * 14);
            }

            if (flashlight)
                flashlight.enabled = Flashlight && !CCTV && !Map;
        }

        public void Pause(bool paused)
        {
            if (Phase != GamePhase.Playing && Phase != GamePhase.Paused)
                return;
            Phase = paused ? GamePhase.Paused : GamePhase.Playing;
            Input.Neutralize();
            Audio.SetGameplay(!paused);
            Guard.Character.Pause(paused);
            Building?.Elena?.Pause(paused);
            if (paused)
                Save();
            UI.Reset();
            CursorState();
        }

        public void ToMenu()
        {
            if (Phase == GamePhase.Playing || Phase == GamePhase.Paused)
                Save();
            if (Building != null)
                Building.Interrupt();
            if (Horror != null)
                Horror.End();
            Phase = GamePhase.Menu;
            if (built)
                World.Root.gameObject.SetActive(false);
            if (hand)
                hand.gameObject.SetActive(false);
            if (flashlight)
                flashlight.enabled = false;
            Audio.SetGameplay(false);
            Audio.Menu(true);
            UI.Reset();
            CursorState();
        }

        public void Capture()
        {
            if (Phase != GamePhase.Playing)
                return;
            Phase = GamePhase.Capture;
            CCTV = Map = false;
            Task = -1;
            captureTime = 0;
            Building.Interrupt();
            Horror.End();
            Audio.Player(.016f, 0, false, false, false);
            Input.Neutralize();
            CursorState();
        }

        void CaptureTick(float dt)
        {
            captureTime += dt;
            Guard.Character.Play(captureTime < .8f ? "alert" : captureTime < 1.6f ? "grab" : captureTime < 2.8f ? "lift" : "kill", 1, false);
            var eye = new Vector3(Player.x, EyeHeight, Player.z);
            var target = Guard.Position + Vector3.up * 1.15f;
            View.transform.position = eye + Vector3.up * (.48f * Mathf.SmoothStep(0, 1, (captureTime - 1.6f) / 1.2f));
            View.transform.rotation = Quaternion.LookRotation(target - View.transform.position);
            if (captureTime >= 2.8f && captureTime - dt < 2.8f)
                Audio.Spatial("impact", Guard.Position, 1);
            if (captureTime >= 3.6f)
            {
                Phase = GamePhase.Lost;
                P13Save.Clear();
                Audio.SetGameplay(false);
                Guard.Character.Pause(true);
                UI.Reset();
                CursorState();
            }
        }

        public void Save()
        {
            if (!built || Phase != GamePhase.Playing && Phase != GamePhase.Paused || Threatened || Task >= 0 || !Building.Stable)
                return;
            var s = new P13Checkpoint{demo = Demo, minute = Minute, energy = Energy, player = Player, yaw = Yaw, pitch = Pitch, guard = Guard.Position, guardYaw = Guard.Yaw, doorClosed = World.DoorClosed, technical = Technical, servers = Servers, archive = Archive, returned = Returned, watchedCCTV = Watched, night = Night, floor = Floor, flashlight = Flashlight, discovered = new List<int>(Discovered), clues = new List<string>(Clues), doors = Building.Snapshot(), trips = Building.Trips, callAttempts = Building.Attempts, callHeard = Building.CallHeard, archiveRead = Building.ArchiveRead, elenaHelpHeard = Building.HelpHeard, elevatorNeedsService = Building.Lift.needsService, setPieceCompleted = SetPieceCompleted};
            if (s.Valid(World) && !P13Save.Write("unity-checkpoint-v1.json", s))
                Message = P13Save.Error;
        }

        public void SaveSettings()
        {
            if (Settings == null)
                return;
            P13Save.Write("settings-v1.json", Settings);
        }

        public void CursorState()
        {
            bool free = Phase != GamePhase.Playing || CCTV || Map;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        void OnApplicationFocus(bool active)
        {
            if (!active && Phase == GamePhase.Playing)
                Pause(true);
        }

        void OnApplicationQuit()
        {
            Save();
            SaveSettings();
        }
    }
}
