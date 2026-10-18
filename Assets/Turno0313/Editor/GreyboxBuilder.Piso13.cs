using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Turno0313.EditorTools
{
    // The game on the Piso 13 floor: its nine room models, collision built from Piso 13's own boxes
    // (Resources/Data/World.json, the same data its custom movement uses) and its nine camera spots, with the
    // Turno 03:13 player and systems. Floor 12 is left out: the lift doors stay shut.
    // Layout (metres): floor x[-7.65,7.65] z[-7.65,7.65]; central office x[-3.3,3.3] z[-3,3] with the desk at
    // its north side; reception west, technical room east, archive SW, servers SE, lift corridor south,
    // abandoned room NW, storage NE.
    public static partial class GreyboxBuilder
    {
        public const string Piso13ScenePath = Root + "/Scenes/Turno_Piso13.unity";
        const string WorldJsonPath = "Assets/Piso13/Resources/Data/World.json";

        static readonly string[] Piso13Rooms = { "CAM02_Recepcion", "CAM03_Ascensor", "CAM04_Pasillo", "CAM05_Archivo", "CAM06_Servidores", "CAM07_Tecnica", "CAM08_Abandonada", "CAM09_Almacen" };

        // Mirror of the parts of Piso 13's World.json this builder needs (Piso 13's own classes live in
        // Assembly-CSharp, which an asmdef can't reference).
        [Serializable]
        class WorldBox
        {
            public float minX, maxX, minZ, maxZ, height;
        }

        [Serializable]
        class WorldCamera
        {
            public string name, shortName;
            public Vector3 position, target;
        }

        [Serializable]
        class WorldDoor
        {
            public string id;
            public int floor;
            public Vector3 hinge;
            public bool locked;
            public float swing, width, yaw;
        }

        [Serializable]
        class WorldData
        {
            public WorldBox[] walls, furniture;
            public WorldBox officeDoor;
            public WorldCamera[] cameras;
            public WorldDoor[] doors;
        }

        [MenuItem("Turno 03:13/Construir escenario Piso 13 (reemplaza esa escena)")]
        static void BuildPiso13FromMenu()
        {
            if (File.Exists(Piso13ScenePath) && !EditorUtility.DisplayDialog("Construir escenario Piso 13",
                    "Se vuelve a generar " + Piso13ScenePath + " y se pierden los cambios hechos a mano en esa escena.", "Generar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Debug.Log(BuildPiso13());
        }

        public static string BuildPiso13()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Hay escenas abiertas con cambios sin guardar; guárdalas antes de generar el escenario.");

            Folders();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Materials();
            var data = JsonUtility.FromJson<WorldData>(AssetDatabase.LoadAssetAtPath<TextAsset>(WorldJsonPath).text);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var playerPrefab = BuildPlayerPrefab();
            CameraOnlyLayers();
            HandOnlyForPlayer();
            SceneLighting();

            var level = new GameObject("Nivel").transform;
            Piso13Environment(level);
            Piso13Collision(level, data);
            Piso13Doors(level, data);
            Piso13Lights(level, data);
            var anomalies = new GameObject("Anomalias").transform;
            var clock = Piso13Act1(level, anomalies);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player";
            // In the office, between the door and the desk, facing the monitors.
            player.transform.SetPositionAndRotation(new Vector3(0, 0, -1.5f), Quaternion.identity);

            var systems = new GameObject("Sistemas");
            var manager = systems.AddComponent<AnomalyManager>();
            Set(manager, "viewer", player.GetComponentInChildren<Camera>());
            Set(manager, "firstOfNight", clock);
            // Every night starts where the game does; he wakes up by the desk, facing the monitors (north).
            Act1Systems(systems, new Vector3(0, 0, -1.5f), 0, new Vector3(.9f, 0, -.35f), 0);
            // A bigger floor: the four digits are rooms apart, so the sleep bar fills a little slower than in the greybox.
            var sleep = systems.GetComponent<SleepMeter>();
            Set(sleep, "secondsToFill", 360f);
            Set(sleep, "secondsToFillPerAnomaly", 240f);
            Piso13Act2(level, anomalies, systems, data);
            GameSystems(level, systems);
            var hud = systems.AddComponent<DebugHUD>();
            Set(hud, "interactor", player.GetComponent<PlayerInteractor>());
            Set(hud, "reporter", player.GetComponent<PlayerReporter>());

            EditorSceneManager.SaveScene(scene, Piso13ScenePath);
            AddSceneToBuildSettings(Piso13ScenePath);
            AssetDatabase.SaveAssets();
            return "Escenario Piso 13 generado en " + Piso13ScenePath;
        }

        // The nine room models. Piso13_CAM01 also carries copies of the other rooms: those are switched off
        // because each room is loaded on its own, as Piso 13 does.
        static void Piso13Environment(Transform level)
        {
            var rooms = Group(level, "Salas_Piso13");
            var office = Piso13Model("Piso13_CAM01", rooms, Vector3.zero, Quaternion.identity);
            foreach (var t in office.GetComponentsInChildren<Transform>(true))
                if (Piso13Rooms.Contains(t.name))
                    t.gameObject.SetActive(false);
            foreach (var name in Piso13Rooms)
                Piso13Model(name, rooms, Vector3.zero, Quaternion.identity);
            rooms.gameObject.AddComponent<Piso13Glow>();
            foreach (var r in rooms.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Invisible colliders from Piso 13's own collision boxes, plus the floor.
        static void Piso13Collision(Transform level, WorldData data)
        {
            var root = Group(level, "Colisiones");
            CollisionBox(root, "Suelo", new Vector3(0, -.1f, 0), new Vector3(16.4f, .2f, 16.4f));
            for (int i = 0; i < data.walls.Length; i++)
                CollisionBox(root, "Pared_" + (i + 1), data.walls[i]);
            // Furniture can be pointed at and reported (a wrong report), like the greybox props; walls can't.
            for (int i = 0; i < data.furniture.Length; i++)
                MarkReportable(CollisionBox(root, "Mueble_" + (i + 1), data.furniture[i]), "Mueble");
        }

        static GameObject CollisionBox(Transform parent, string name, WorldBox b) =>
            CollisionBox(parent, name, new Vector3((b.minX + b.maxX) / 2, b.height / 2, (b.minZ + b.maxZ) / 2), new Vector3(b.maxX - b.minX, b.height, b.maxZ - b.minZ));

        static GameObject CollisionBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ---------- Act 2 on the Piso 13 floor ----------

        // Piso 13's nine camera spots become the security cameras; the office monitor shows them and opens the
        // camera screen. The cup is in the abandoned room (CAM 08), the person in the storage room (CAM 09), the
        // escaped clock roams CAM 02-09 and the final is on CAM 01, the office.
        static void Piso13Act2(Transform level, Transform anomalies, GameObject systems, WorldData data)
        {
            var root = Group(level, "Camaras_Seguridad");
            var cams = new SecurityCamera[data.cameras.Length];
            for (int i = 0; i < cams.Length; i++)
                cams[i] = CameraRig(root, i + 1, data.cameras[i].shortName, data.cameras[i].position, data.cameras[i].target);
            P13Monitor(level, cams);

            Act2Materials();
            var abandoned = Group(level, "Sala_Abandonada");
            MarkReportable(Desk(abandoned, "Escritorio_Abandonado", new Vector3(-5.25f, 0, 4.5f), 0, 1.4f, false), "Escritorio");
            // Jefferson's cup and elegant person models (boxes if they can't be loaded).
            Cup(anomalies, new Vector3(-5f, .76f, 4.645f), 0, true);
            Person(anomalies, cams[8], new Vector3(4.9f, 0, 4.4f), true);

            // The escaped clock: in front of each camera, most of the way to what it looks at, at head height.
            var roaming = cams.Skip(1).ToArray();
            var places = roaming.Select(c =>
            {
                var d = data.cameras[c.Number - 1];
                var p = Vector3.Lerp(d.position, d.target, .7f);
                p.y = 1.6f;
                return p;
            }).ToArray();
            EscapedClock(anomalies, roaming, places);

            Act2Systems(systems);
            // The office desk is on the guard's +z side (the greybox one was on -z): turned around and moved so
            // his arms rest on its near edge (z = 0.55).
            var (guard, head) = SleepingGuard(level.Find("Caseta_Oficina"), cams[0], new Vector3(0, 0, -.525f), 180f);
            FinalSystems(systems, cams[0], guard, head);
        }

        // The office desk monitor (the model's "circuito cerrado" screen): a screen of our own over it that cycles
        // through the nine feeds in reality and shows static in the dream; E on it opens the camera screen.
        static void P13Monitor(Transform level, SecurityCamera[] cams)
        {
            var root = Group(level.Find("Caseta_Oficina"), "Monitor_Camaras");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Imagen";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            // Just in front of the model's screen (z = 1.24); a quad faces -z, towards the chair.
            quad.transform.localPosition = new Vector3(0, 1.19f, 1.228f);
            quad.transform.localScale = new Vector3(1f, .48f, 1);
            var label = Text("Etiqueta", root, new Vector3(.38f, 1.39f, 1.225f), Quaternion.identity, "CAM", .005f, Color.white);
            var screen = quad.AddComponent<MonitorScreen>();
            Set(screen, "screen", quad.GetComponent<Renderer>());
            Set(screen, "template", ScreenMaterial());
            Set(screen, "label", label);
            Set(screen, "feeds", cams.Cast<Object>().ToArray());
            Set(screen, "secondsPerFeed", 4f);

            // What E and R aim at: a thin box over the screen, in front of the monitor's own collision box.
            var target = root.gameObject.AddComponent<BoxCollider>();
            target.center = new Vector3(0, 1.19f, 1.225f);
            target.size = new Vector3(1.02f, .5f, .03f);
            var source = root.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            var monitors = root.gameObject.AddComponent<SecurityMonitors>();
            Set(monitors, "source", source);
            Set(monitors, "correctClip", Clip("P13_UI_Confirm"));
            Set(monitors, "wrongClip", Clip("P13_UI_Hover"));
            MarkReportable(root.gameObject, "Monitores");
        }

        // ---------- Act 1 on the Piso 13 floor ----------

        // The caseta is the central office; the four hallway anomalies are spread over the floor (technical room,
        // reception, lift corridor, north hall) so collecting the code takes a walk. Returns the clock.
        static ClockAnomaly Piso13Act1(Transform level, Transform anomalies)
        {
            var office = Group(level, "Caseta_Oficina");
            // Clock on the office's north wall, left of the desk; alarm panel on its west wall, by the south door.
            var clock = WallClock(anomalies, new Vector3(-1.75f, 2.2f, 2.65f), -90f);
            AlarmPanelProp(office, new Vector3(-2.65f, 1.35f, -2f));

            P13DoorInWall(anomalies);
            // Reception: above the waiting bench, on the wall that faces north.
            Portrait(anomalies, new Vector3(-5.35f, 1.75f, -2.72f), -90f);
            // Lift corridor, near the lift (closer to the office it would sit right in front of CAM 03's lens);
            // the wrong arrow points at its west wall.
            ExitSign(anomalies, new Vector3(0, 2.45f, -6.3f), 180f);
            P13Chair(anomalies);
            P13LiftFloor(level);
            P13CodeDigits(anomalies);
            return clock;
        }

        // "Una puerta aparece donde antes había pared": a locked door with its frame against the east wall of
        // the technical room. Normal = nothing: the wall is the room model's own.
        static void P13DoorInWall(Transform parent)
        {
            var root = Group(parent, "Anomalia_PuertaEnPared");
            var anomalous = Group(root, "Puerta_Anomala");
            const float x = 7.6f, z0 = .4f, z1 = 1.4f;
            Box("Marco_A", anomalous, new Vector3(x - .03f, 1.17f, z0 - .03f), new Vector3(.08f, 2.34f, .06f), mats["Marco"]);
            Box("Marco_B", anomalous, new Vector3(x - .03f, 1.17f, z1 + .03f), new Vector3(.08f, 2.34f, .06f), mats["Marco"]);
            Box("Marco_Sup", anomalous, new Vector3(x - .03f, 2.31f, (z0 + z1) / 2), new Vector3(.08f, .06f, 1.12f), mats["Marco"]);
            // Hinge at z0, the leaf runs to z1 and faces west, into the room.
            var door = Piso13Model("Puerta_Madera", anomalous, new Vector3(x, 0, z0), Quaternion.Euler(0, -90, 0));
            door.name = "Puerta";
            AddDoor(door, true, "Está cerrada.", 0);
            anomalous.gameObject.SetActive(false);

            var anomaly = root.gameObject.AddComponent<ToggleAnomaly>();
            Set(anomaly, "displayName", "Puerta donde había pared");
            Set(anomaly, "reportableWhenNormal", false);
            Set(anomaly, "normalObjects", new Object[0]);
            Set(anomaly, "anomalousObjects", new Object[] { anomalous.gameObject });
        }

        // "Una silla cambia de sitio cada vez que te das la vuelta" (hall norte, entre la sala abandonada y el almacén).
        static void P13Chair(Transform parent)
        {
            var root = Group(parent, "Anomalia_Silla");
            var chair = ChairModel(root, "Silla", new Vector3(-1.6f, 0, 5f), 180f);
            var spots = new[]
            {
                (new Vector3(-5f, 0, 5.6f), 120f),
                (new Vector3(2.3f, 0, 6.6f), 200f),
                (new Vector3(-3.2f, 0, 7f), 30f),
                (new Vector3(4.8f, 0, 4.6f), 250f)
            };
            var marks = new Object[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = new GameObject("Sitio_" + (i + 1)).transform;
                spot.SetParent(root, false);
                spot.SetLocalPositionAndRotation(spots[i].Item1, Quaternion.Euler(0, spots[i].Item2, 0));
                marks[i] = spot;
            }

            var anomaly = root.gameObject.AddComponent<MovingChairAnomaly>();
            Set(anomaly, "displayName", "Silla que cambia de sitio");
            Set(anomaly, "chair", chair.transform);
            Set(anomaly, "spots", marks);
        }

        // The lift indicator says 13, like every sign of Piso 13; the anomaly turns it into 31 (Extra, both acts).
        // A plate covers the model's own indicator so the number can change; it has a collider, so clicking
        // the number on a camera counts.
        static void P13LiftFloor(Transform level)
        {
            var lift = level.Find("Puertas/Ascensor_Puertas");
            // A bit higher than the model's own indicator, so the lift doors don't hide it from up close.
            Box("Placa_Indicador", lift, new Vector3(0, 2.66f, -7.6f), new Vector3(.38f, .22f, .02f), mats["Negro"]);
            var color = new Color(.45f, .95f, .8f);
            var normal = Text("Indicador_13", lift, new Vector3(0, 2.66f, -7.585f), Quaternion.Euler(0, 180, 0), "13", .015f, color);
            var odd = Text("Indicador_31", lift, new Vector3(0, 2.66f, -7.585f), Quaternion.Euler(0, 180, 0), "31", .015f, color);
            odd.gameObject.SetActive(false);

            var anomaly = lift.gameObject.AddComponent<ToggleAnomaly>();
            Set(anomaly, "displayName", "Ascensor que marca el piso 31");
            Set(anomaly, "acts", (int)ActMask.Both);
            Set(anomaly, "normalObjects", new Object[] { normal.gameObject });
            Set(anomaly, "anomalousObjects", new Object[] { odd.gameObject });
        }

        // One digit of the code on each of the four anomalies, only visible while it looks wrong.
        static void P13CodeDigits(Transform anomalies)
        {
            var door = anomalies.Find("Anomalia_PuertaEnPared");
            Digit(door, Text("Digito", door, new Vector3(7.55f, 1.6f, .9f), Quaternion.Euler(0, 90, 0), "_ 0 _ _", .03f, DigitInk));

            var portrait = anomalies.Find("Anomalia_Retrato");
            Digit(portrait, Text("Digito", portrait, new Vector3(.045f, .31f, 0), Quaternion.Euler(0, -90, 0), "_ 0 _ _", .012f, DigitInk));

            // On the corridor wall the wrong arrow points at.
            var sign = anomalies.Find("Anomalia_CartelSalida");
            // Smaller than in the greybox: this corridor is narrow and the wall is seen from close.
            var signDigit = Text("Digito", sign, Vector3.zero, Quaternion.identity, "_ 0 _ _", .017f, DigitInk);
            signDigit.transform.SetPositionAndRotation(new Vector3(-.66f, 2f, -6.3f), Quaternion.Euler(0, -90, 0));
            Digit(sign, signDigit);

            var chair = anomalies.Find("Anomalia_Silla/Silla");
            Digit(chair.parent,
                Text("Digito_Frente", chair, new Vector3(0, .82f, -.18f), Quaternion.Euler(0, 180, 0), "_ 0 _ _", .011f, DigitInk),
                Text("Digito_Detras", chair, new Vector3(0, .82f, -.24f), Quaternion.identity, "_ 0 _ _", .011f, DigitInk));
        }

        // The office's sliding door, the swinging door of the technical room and the shut lift doors.
        static void Piso13Doors(Transform level, WorldData data)
        {
            var root = Group(level, "Puertas");
            var office = Piso13Model("CAM01_Puerta", root, new Vector3(0, 1.35f, -3f), Quaternion.identity);
            office.name = "Puerta_Oficina";
            var box = data.officeDoor;
            office.AddComponent<BoxCollider>().size = new Vector3(box.maxX - box.minX, box.height, .12f);
            var source = office.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.maxDistance = 18f;
            var sliding = office.AddComponent<SlidingDoor>();
            Set(sliding, "source", source);
            Set(sliding, "openClip", Clip("P13_Door_Open"));
            Set(sliding, "closeClip", Clip("P13_Door_Closing"));
            Set(sliding, "latchClip", Clip("P13_Door_Latch"));

            foreach (var d in data.doors.Where(d => d.floor == 13))
            {
                var leaf = Piso13Model("Puerta_Madera", root, d.hinge, Quaternion.Euler(0, d.yaw, 0));
                leaf.name = "Puerta_" + d.id;
                leaf.transform.localScale = new Vector3(d.width, 1, 1);
                AddDoor(leaf, d.locked, "Está cerrada.", d.swing);
            }

            // The lift: two shut leaves (Piso 13 draws them as boxes too). In this game it never comes.
            var lift = Group(root, "Ascensor_Puertas");
            Box("Hoja_A", lift, new Vector3(-.33f, 1.2f, -7.26f), new Vector3(.65f, 2.4f, .06f), mats["Metal"]);
            Box("Hoja_B", lift, new Vector3(.33f, 1.2f, -7.26f), new Vector3(.65f, 2.4f, .06f), mats["Metal"]);
        }

        // A light at every camera spot, as Piso 13 does (ActLook gives them each act's colour).
        static void Piso13Lights(Transform level, WorldData data)
        {
            var root = Group(level, "Luces");
            foreach (var c in data.cameras)
            {
                var go = new GameObject("Luz_" + c.shortName);
                go.transform.SetParent(root, false);
                go.transform.localPosition = c.position + Vector3.down * .1f;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 8f;
                light.intensity = 1.25f;
                light.color = new Color(.55f, .72f, .8f);
                light.shadows = LightShadows.None;
            }
        }

        // First in Build Settings: the Windows build starts the game in this scene.
        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
