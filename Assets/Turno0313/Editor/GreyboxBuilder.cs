using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Turno0313.EditorTools
{
    // Builds the base greybox from the GDD (caseta, pasillo, oficinas) with the player prefab, the
    // AnomalyManager and the four hallway anomalies. Running it again rebuilds the scene from scratch.
    // Layout (metres): caseta x[-2.5,2.5] z[-2,2]; pasillo x[-1.25,1.25] z[2,22]; oficinas x[-10,-1.25] z[11,21].
    // Act 1 pieces (clock, alarm panel, sleep bar) live in GreyboxBuilder.Act1.cs.
    public static partial class GreyboxBuilder
    {
        public const string Root = "Assets/Turno0313";
        public const string ScenePath = Root + "/Scenes/Greybox_Base.unity";
        public const string PlayerPrefabPath = Root + "/Prefabs/Player.prefab";
        const string MaterialsFolder = Root + "/Materials";
        const string LightingPath = Root + "/Scenes/Greybox_Base_Lighting.lighting";
        const string Piso13Models = "Assets/Piso13/Resources/Models/";
        const string Piso13Audio = "Assets/Piso13/Resources/Audio/";
        const string LitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";
        const string WorldTextShaderPath = Root + "/Shaders/WorldText.shader";
        const float H = 3f;
        const float T = .2f;
        const int IgnoreRaycastLayer = 2;

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Font font;

        [MenuItem("Turno 03:13/Construir greybox base (reemplaza la escena)")]
        static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Construir greybox",
                    "Se vuelve a generar " + ScenePath + " y se pierden los cambios hechos a mano en esa escena.", "Generar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Debug.Log(Build());
        }

        // Replaces whatever is open with the freshly built greybox (left open afterwards).
        // Refuses to run while any open scene has unsaved changes.
        public static string Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Hay escenas abiertas con cambios sin guardar; guárdalas antes de generar el greybox.");

            Folders();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Materials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var playerPrefab = BuildPlayerPrefab();
            SceneLighting();

            var level = new GameObject("Nivel").transform;
            Shell(level);
            Caseta(level);
            Corridor(level);
            Offices(level);
            Lights(level);

            var anomalies = new GameObject("Anomalias").transform;
            DoorInWall(anomalies);
            Portrait(anomalies);
            ExitSign(anomalies);
            Chair(anomalies);
            var clock = Act1Anomalies(anomalies);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player";
            // Spawn in the caseta, clear of the desk chair, facing the door.
            player.transform.SetPositionAndRotation(new Vector3(0, 0, .4f), Quaternion.identity);

            var systems = new GameObject("Sistemas");
            var manager = systems.AddComponent<AnomalyManager>();
            Set(manager, "viewer", player.GetComponentInChildren<Camera>());
            Set(manager, "firstOfNight", clock);
            Act1Systems(systems);
            var hud = systems.AddComponent<DebugHUD>();
            Set(hud, "interactor", player.GetComponent<PlayerInteractor>());
            Set(hud, "reporter", player.GetComponent<PlayerReporter>());

            EditorSceneManager.SaveScene(scene, ScenePath);

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            return "Greybox generado en " + ScenePath;
        }

        // ---------- Player ----------

        static GameObject BuildPlayerPrefab()
        {
            var go = new GameObject("Player") { layer = IgnoreRaycastLayer };
            var body = go.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = .3f;
            body.center = new Vector3(0, .9f, 0);
            body.stepOffset = .3f;
            body.skinWidth = .04f;

            var cameraGo = new GameObject("Camara") { tag = "MainCamera" };
            cameraGo.transform.SetParent(go.transform, false);
            cameraGo.transform.localPosition = new Vector3(0, 1.65f, 0);
            var cam = cameraGo.AddComponent<Camera>();
            cam.nearClipPlane = .03f;
            cam.farClipPlane = 60f;
            cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraGo.AddComponent<AudioListener>();

            // Reused from Piso 13: first person hand holding the flashlight (already in camera space).
            var hand = Piso13Model("Piso13_HandFlashlight", cameraGo.transform, Vector3.zero, Quaternion.identity);
            hand.name = "Mano_Linterna";
            foreach (var r in hand.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;

            var lightGo = new GameObject("Linterna");
            lightGo.transform.SetParent(cameraGo.transform, false);
            lightGo.transform.localPosition = new Vector3(.18f, -.16f, .55f);
            var spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 16f;
            spot.spotAngle = 52f;
            spot.innerSpotAngle = 22f;
            spot.intensity = 5f;
            spot.color = new Color(1f, .95f, .87f);
            spot.shadows = LightShadows.Soft;

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0;

            var controller = go.AddComponent<PlayerController>();
            Set(controller, "view", cam);
            go.AddComponent<PlayerInteractor>();
            var reporter = go.AddComponent<PlayerReporter>();
            Set(reporter, "feedback", audio);
            Set(reporter, "correctClip", Clip("P13_UI_Confirm"));
            Set(reporter, "wrongClip", Clip("P13_UI_Hover"));
            var flashlight = lightGo.AddComponent<Flashlight>();
            Set(flashlight, "spot", spot);
            Set(flashlight, "source", audio);
            var steps = go.AddComponent<Footsteps>();
            Set(steps, "source", audio);
            Set(steps, "clips", Enumerable.Range(0, 6).Select(i => (Object)Clip("P13_PlayerStep_" + i)).ToArray());

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlayerPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------- Level ----------

        static void SceneLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.15f, .16f, .18f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (!settings)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, LightingPath);
            }

            settings.bakedGI = false;
            settings.realtimeGI = false;
            Lightmapping.lightingSettings = settings;
        }

        static void Shell(Transform level)
        {
            var shell = Group(level, "Suelo_Techo");
            Static(Box("Suelo", shell, new Vector3(-3.75f, -.1f, 10f), new Vector3(13.5f, .2f, 25f), mats["Suelo"]));
            Static(Box("Techo", shell, new Vector3(-3.75f, H + .1f, 10f), new Vector3(13.5f, .2f, 25f), mats["Techo"]));
        }

        static void Caseta(Transform level)
        {
            var c = Group(level, "Caseta");
            WallX(c, "Pared_S", -2f, -2.6f, 2.6f);
            WallZ(c, "Pared_O", -2.5f, -2f, 2f);
            WallZ(c, "Pared_E", 2.5f, -2f, 2f);
            WallX(c, "Pared_N", 2f, -2.6f, 2.6f, new Gap(-.5f, .5f, 2.3f));

            // Reused from Piso 13: wooden door, hinge on its left edge. Opens towards the corridor.
            var door = Piso13Model("Puerta_Madera", c, new Vector3(-.5f, 0, 2f), Quaternion.identity);
            door.name = "Puerta_Caseta";
            AddDoor(door, false, "Está cerrada.", -95f);

            var desk = Desk(c, "Escritorio_Caseta", new Vector3(0, 0, -1.45f), 0, 2.2f, false);
            MarkReportable(desk, "Escritorio");
            var monitors = Group(c, "Monitores_Camaras (Jefferson)");
            for (int i = -1; i <= 1; i++)
                Box("Monitor_" + (i + 2), monitors, new Vector3(i * .62f, 1.02f, -1.62f), new Vector3(.56f, .34f, .05f), mats["Pantalla"]);
            MarkReportable(monitors.gameObject, "Monitores");
            MarkReportable(ChairModel(c, "Silla_Caseta", new Vector3(0, 0, -.7f), 180f), "Silla");

            // Alarm panel and wall clock (Act 1): see GreyboxBuilder.Act1.cs.
            AlarmPanelProp(c);
        }

        static void Corridor(Transform level)
        {
            var p = Group(level, "Pasillo");
            WallZ(p, "Pared_O", -1.25f, 2f, 22f, new Gap(14f, 15.6f, 2.4f));
            // The 5.5-6.5 gap belongs to the "door where there was a wall" anomaly.
            WallZ(p, "Pared_E", 1.25f, 2f, 22f, new Gap(5.5f, 6.5f, H), new Gap(17f, 18.8f, 2.4f));
            WallX(p, "Pared_N", 22f, -1.35f, 1.35f, new Gap(-.95f, .95f, 2.7f));

            // Reused from Piso 13: steel door with frame for the exit. In the dream it never opens.
            var exit = Piso13Model("CAM01_Puerta", p, new Vector3(0, 1.35f, 22f), Quaternion.identity);
            exit.name = "Puerta_Salida";
            var exitCollider = exit.AddComponent<BoxCollider>();
            exitCollider.size = new Vector3(1.9f, 2.7f, .22f);
            AddDoor(exit, true, "No abre.", 0);
            var exitSign = Group(p, "Rotulo_Salida");
            exitSign.localPosition = new Vector3(0, 2.86f, 21.88f);
            Box("Caja", exitSign, Vector3.zero, new Vector3(.62f, .2f, .04f), mats["CartelVerde"], false);
            Text("Texto", exitSign, new Vector3(0, 0, -.025f), Quaternion.identity, "SALIDA", .012f, Color.white);

            var lift = Group(p, "Ascensor (Jefferson)");
            Box("Puerta_A", lift, new Vector3(1.3f, 1.2f, 17.45f), new Vector3(.06f, 2.4f, .9f), mats["Metal"]);
            Box("Puerta_B", lift, new Vector3(1.3f, 1.2f, 18.35f), new Vector3(.06f, 2.4f, .9f), mats["Metal"]);
            Box("Marco_A", lift, new Vector3(1.13f, 1.23f, 16.95f), new Vector3(.06f, 2.46f, .1f), mats["Marco"]);
            Box("Marco_B", lift, new Vector3(1.13f, 1.23f, 18.85f), new Vector3(.06f, 2.46f, .1f), mats["Marco"]);
            Box("Marco_Sup", lift, new Vector3(1.13f, 2.43f, 17.9f), new Vector3(.06f, .06f, 2f), mats["Marco"]);
            Box("Boton", lift, new Vector3(1.13f, 1.15f, 16.6f), new Vector3(.04f, .16f, .08f), mats["Panel"]);
            Text("Indicador", lift, new Vector3(1.11f, 2.66f, 17.9f), Quaternion.Euler(0, 90, 0), "1", .02f, new Color(1f, .62f, .1f));
            MarkReportable(lift.gameObject, "Ascensor");

            var extinguisher = Box("Extintor", p, new Vector3(-1.05f, .35f, 3.2f), new Vector3(.18f, .7f, .18f), mats["Rojo"]);
            MarkReportable(extinguisher, "Extintor");
            var plant = Group(p, "Planta");
            plant.localPosition = new Vector3(.9f, 0, 20.9f);
            Box("Maceta", plant, new Vector3(0, .22f, 0), new Vector3(.36f, .44f, .36f), mats["Marco"]);
            Box("Hojas", plant, new Vector3(0, .75f, 0), new Vector3(.5f, .62f, .5f), mats["Planta"], false);
            MarkReportable(plant.gameObject, "Planta");
            var bin = Box("Papelera", p, new Vector3(-1f, .2f, 13.4f), new Vector3(.3f, .4f, .3f), mats["Metal"]);
            MarkReportable(bin, "Papelera");
        }

        static void Offices(Transform level)
        {
            var o = Group(level, "Oficinas");
            WallX(o, "Pared_S", 11f, -10.1f, -1.35f);
            WallX(o, "Pared_N", 21f, -10.1f, -1.35f);
            WallZ(o, "Pared_O", -10f, 11f, 21f);

            var desks = new[] { new Vector3(-7.6f, 0, 13.4f), new Vector3(-4.2f, 0, 13.4f), new Vector3(-7.6f, 0, 17.6f), new Vector3(-4.2f, 0, 17.6f) };
            for (int i = 0; i < desks.Length; i++)
            {
                MarkReportable(Desk(o, "Escritorio_" + (i + 1), desks[i], 0, 1.6f, true), "Escritorio");
                // The chair of the last desk is the anomaly's (see Chair()).
                if (i < desks.Length - 1)
                    MarkReportable(ChairModel(o, "Silla_" + (i + 1), desks[i] + new Vector3(0, 0, -.75f), 0), "Silla");
            }

            var cup = Box("Taza (Jefferson)", o, new Vector3(-3.7f, .8f, 17.7f), new Vector3(.08f, .1f, .08f), mats["Taza"]);
            MarkReportable(cup, "Taza");
            for (int i = 0; i < 2; i++)
                MarkReportable(Box("Archivador_" + (i + 1), o, new Vector3(-9.6f, .65f, 12.4f + i * .75f), new Vector3(.6f, 1.3f, .6f), mats["Metal"]), "Archivador");
        }

        static void Lights(Transform level)
        {
            var l = Group(level, "Luces");
            Fixture(l, new Vector3(0, 0, 0), false);
            foreach (float z in new[] { 4.5f, 9.5f, 14.5f, 19.5f })
                Fixture(l, new Vector3(0, 0, z), false);
            Fixture(l, new Vector3(-5.9f, 0, 13.5f), true);
            Fixture(l, new Vector3(-5.9f, 0, 18.4f), true);
        }

        static void Fixture(Transform parent, Vector3 at, bool alongX)
        {
            var size = alongX ? new Vector3(1.2f, .04f, .25f) : new Vector3(.25f, .04f, 1.2f);
            Static(Box("Fluorescente", parent, new Vector3(at.x, H - .03f, at.z), size, mats["Fluorescente"], false));
            var go = new GameObject("Luz");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, H - .7f, at.z);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7.5f;
            light.intensity = 1.5f;
            light.color = new Color(.9f, .95f, 1f);
            light.shadows = LightShadows.None;
        }

        // ---------- Anomalies (John) ----------

        // "Una puerta aparece donde antes había pared" (pasillo, pared este).
        static void DoorInWall(Transform parent)
        {
            var root = Group(parent, "Anomalia_PuertaEnPared");
            var normal = Box("Pared_Normal", root, new Vector3(1.25f, H / 2, 6f), new Vector3(T, H, 1f), mats["Pared"]);
            var anomalous = Group(root, "Puerta_Anomala");
            Box("Dintel", anomalous, new Vector3(1.25f, (2.28f + H) / 2, 6f), new Vector3(T, H - 2.28f, 1f), mats["Pared"]);
            Box("Marco_A", anomalous, new Vector3(1.12f, 1.17f, 5.47f), new Vector3(.06f, 2.34f, .06f), mats["Marco"]);
            Box("Marco_B", anomalous, new Vector3(1.12f, 1.17f, 6.53f), new Vector3(.06f, 2.34f, .06f), mats["Marco"]);
            Box("Marco_Sup", anomalous, new Vector3(1.12f, 2.31f, 6f), new Vector3(.06f, .06f, 1.12f), mats["Marco"]);
            var door = Piso13Model("Puerta_Madera", anomalous, new Vector3(1.25f, 0, 5.5f), Quaternion.Euler(0, -90, 0));
            door.name = "Puerta";
            AddDoor(door, true, "Está cerrada.", 0);
            anomalous.gameObject.SetActive(false);

            var anomaly = root.gameObject.AddComponent<ToggleAnomaly>();
            Set(anomaly, "displayName", "Puerta donde había pared");
            Set(anomaly, "reportableWhenNormal", false);
            Set(anomaly, "normalObjects", new Object[] { normal });
            Set(anomaly, "anomalousObjects", new Object[] { anomalous.gameObject });
        }

        // "El retrato del pasillo cambia de pose si lo miras dos veces" (pasillo, pared oeste).
        static void Portrait(Transform parent)
        {
            var root = Group(parent, "Anomalia_Retrato");
            root.localPosition = new Vector3(-1.12f, 1.65f, 8.5f);
            Box("Marco", root, Vector3.zero, new Vector3(.06f, .95f, .75f), mats["Madera"]);
            Box("Lienzo", root, new Vector3(.032f, 0, 0), new Vector3(.01f, .8f, .6f), mats["Lienzo"], false);
            Box("Cuerpo", root, new Vector3(.042f, -.2f, 0), new Vector3(.01f, .34f, .3f), mats["Figura"], false);

            var normalPose = Group(root, "Pose_Normal");
            Box("Cabeza", normalPose, new Vector3(.044f, .1f, 0), new Vector3(.01f, .17f, .14f), mats["Figura"], false);
            Box("Ojo_A", normalPose, new Vector3(.05f, .12f, .03f), new Vector3(.004f, .018f, .024f), mats["Ojos"], false);
            Box("Ojo_B", normalPose, new Vector3(.05f, .12f, -.03f), new Vector3(.004f, .018f, .024f), mats["Ojos"], false);

            var anomalousPose = Group(root, "Pose_Anomala");
            var head = Box("Cabeza", anomalousPose, new Vector3(.044f, .08f, .075f), new Vector3(.01f, .17f, .14f), mats["Figura"], false);
            head.transform.localRotation = Quaternion.Euler(28f, 0, 0);
            Box("Ojo_A", anomalousPose, new Vector3(.05f, .1f, .1f), new Vector3(.004f, .018f, .024f), mats["Ojos"], false);
            Box("Ojo_B", anomalousPose, new Vector3(.05f, .07f, .05f), new Vector3(.004f, .018f, .024f), mats["Ojos"], false);
            var arm = Box("Brazo", anomalousPose, new Vector3(.043f, .02f, -.16f), new Vector3(.01f, .32f, .06f), mats["Figura"], false);
            arm.transform.localRotation = Quaternion.Euler(-35f, 0, 0);
            anomalousPose.gameObject.SetActive(false);

            var anomaly = root.gameObject.AddComponent<PortraitAnomaly>();
            Set(anomaly, "displayName", "Retrato que cambia de pose");
            Set(anomaly, "normalPose", normalPose.gameObject);
            Set(anomaly, "anomalousPose", anomalousPose.gameObject);
        }

        // "El cartel de SALIDA apunta hacia una pared" (pasillo, colgado del techo).
        static void ExitSign(Transform parent)
        {
            var root = Group(parent, "Anomalia_CartelSalida");
            root.localPosition = new Vector3(0, 2.55f, 11f);
            Box("Caja", root, Vector3.zero, new Vector3(.8f, .28f, .08f), mats["CartelVerde"]);
            Box("Cable_A", root, new Vector3(-.3f, .3f, 0), new Vector3(.01f, .32f, .01f), mats["Metal"], false);
            Box("Cable_B", root, new Vector3(.3f, .3f, 0), new Vector3(.01f, .32f, .01f), mats["Metal"], false);
            Text("Texto", root, new Vector3(-.1f, 0, -.045f), Quaternion.identity, "SALIDA", .014f, Color.white);
            var normalArrow = Arrow(root, "Flecha_Normal", new Vector3(.29f, 0, -.045f), 0);
            var anomalousArrow = Arrow(root, "Flecha_Anomala", new Vector3(.29f, 0, -.045f), -90f);
            anomalousArrow.SetActive(false);

            var anomaly = root.gameObject.AddComponent<ToggleAnomaly>();
            Set(anomaly, "displayName", "Cartel de SALIDA que apunta a la pared");
            Set(anomaly, "normalObjects", new Object[] { normalArrow });
            Set(anomaly, "anomalousObjects", new Object[] { anomalousArrow });
        }

        // "Una silla cambia de sitio cada vez que te das la vuelta" (oficinas, último escritorio).
        static void Chair(Transform parent)
        {
            var root = Group(parent, "Anomalia_Silla");
            var chair = ChairModel(root, "Silla", new Vector3(-4.2f, 0, 16.85f), 0);
            var spots = new[]
            {
                (new Vector3(-2.3f, 0, 16.2f), 200f),
                (new Vector3(-6f, 0, 15.4f), 80f),
                (new Vector3(-8.9f, 0, 20.2f), 220f),
                (new Vector3(-5.6f, 0, 12.2f), 20f)
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

        // ---------- Props ----------

        static GameObject Desk(Transform parent, string name, Vector3 at, float yaw, float width, bool withMonitor)
        {
            var root = Group(parent, name);
            root.SetLocalPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .38f, 0);
            collider.size = new Vector3(width, .76f, .75f);
            Box("Tablero", root, new Vector3(0, .74f, 0), new Vector3(width, .04f, .75f), mats["Madera"], false);
            Box("Lado_A", root, new Vector3(-width / 2 + .03f, .36f, 0), new Vector3(.04f, .72f, .7f), mats["Madera"], false);
            Box("Lado_B", root, new Vector3(width / 2 - .03f, .36f, 0), new Vector3(.04f, .72f, .7f), mats["Madera"], false);
            Box("Faldon", root, new Vector3(0, .45f, .33f), new Vector3(width - .1f, .5f, .03f), mats["Madera"], false);
            if (withMonitor)
            {
                Box("Monitor", root, new Vector3(0, 1f, .15f), new Vector3(.5f, .32f, .04f), mats["Pantalla"], false);
                Box("Pie", root, new Vector3(0, .8f, .17f), new Vector3(.08f, .1f, .08f), mats["Negro"], false);
                Box("Teclado", root, new Vector3(0, .77f, -.12f), new Vector3(.42f, .02f, .14f), mats["Negro"], false);
            }

            return root.gameObject;
        }

        static GameObject ChairModel(Transform parent, string name, Vector3 at, float yaw)
        {
            var root = Group(parent, name);
            root.SetLocalPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .48f, 0);
            collider.size = new Vector3(.5f, .96f, .5f);
            Box("Asiento", root, new Vector3(0, .46f, 0), new Vector3(.46f, .06f, .46f), mats["Tela"], false);
            Box("Respaldo", root, new Vector3(0, .78f, -.21f), new Vector3(.44f, .5f, .05f), mats["Tela"], false);
            Box("Poste", root, new Vector3(0, .23f, 0), new Vector3(.05f, .4f, .05f), mats["Metal"], false);
            Box("Base_A", root, new Vector3(0, .03f, 0), new Vector3(.52f, .03f, .05f), mats["Metal"], false);
            Box("Base_B", root, new Vector3(0, .03f, 0), new Vector3(.05f, .03f, .52f), mats["Metal"], false);
            return root.gameObject;
        }

        static GameObject Arrow(Transform parent, string name, Vector3 at, float roll)
        {
            var root = Group(parent, name);
            root.SetLocalPositionAndRotation(at, Quaternion.Euler(0, 0, roll));
            Box("Asta", root, new Vector3(0, -.02f, 0), new Vector3(.03f, .13f, .005f), mats["CartelBlanco"], false);
            var a = Box("Punta_A", root, new Vector3(-.026f, .04f, 0), new Vector3(.075f, .024f, .005f), mats["CartelBlanco"], false);
            a.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            var b = Box("Punta_B", root, new Vector3(.026f, .04f, 0), new Vector3(.075f, .024f, .005f), mats["CartelBlanco"], false);
            b.transform.localRotation = Quaternion.Euler(0, 0, -45f);
            return root.gameObject;
        }

        static void AddDoor(GameObject hinge, bool locked, string lockedMessage, float openAngle)
        {
            if (!hinge.GetComponent<Collider>())
            {
                // Puerta_Madera spans x 0..1, y 0..2.28 from its hinge.
                var c = hinge.AddComponent<BoxCollider>();
                c.center = new Vector3(.5f, 1.14f, 0);
                c.size = new Vector3(1f, 2.28f, .07f);
            }

            var source = hinge.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 18f;
            var door = hinge.AddComponent<Door>();
            Set(door, "locked", locked);
            Set(door, "lockedMessage", lockedMessage);
            Set(door, "openAngle", openAngle);
            Set(door, "source", source);
            Set(door, "openClip", Clip("P13_Door_Open"));
            Set(door, "closeClip", Clip("P13_Door_Closing"));
            Set(door, "latchClip", Clip("P13_Door_Latch"));
        }

        // ---------- Helpers ----------

        readonly struct Gap
        {
            public readonly float From, To, Top;

            public Gap(float from, float to, float top)
            {
                From = from;
                To = to;
                Top = top;
            }
        }

        static void WallX(Transform p, string name, float z, float x0, float x1, params Gap[] gaps) => Wall(p, name, true, z, x0, x1, gaps);
        static void WallZ(Transform p, string name, float x, float z0, float z1, params Gap[] gaps) => Wall(p, name, false, x, z0, z1, gaps);

        // A straight wall split around door/window gaps, with a lintel above each gap lower than the ceiling.
        static void Wall(Transform p, string name, bool alongX, float at, float from, float to, Gap[] gaps)
        {
            int part = 0;
            float cursor = from;
            foreach (var g in gaps.OrderBy(g => g.From))
            {
                if (g.From > cursor)
                    Segment(cursor, g.From, 0, H);
                if (g.Top < H)
                    Segment(g.From, g.To, g.Top, H);
                cursor = g.To;
            }

            if (to > cursor)
                Segment(cursor, to, 0, H);

            void Segment(float start, float end, float bottom, float top)
            {
                float mid = (start + end) / 2, length = end - start, y = (bottom + top) / 2, height = top - bottom;
                var center = alongX ? new Vector3(mid, y, at) : new Vector3(at, y, mid);
                var size = alongX ? new Vector3(length, height, T) : new Vector3(T, height, length);
                Static(Box(name + "_" + ++part, p, center, size, mats["Pared"]));
            }
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Static(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static TextMesh Text(string name, Transform parent, Vector3 at, Quaternion rotation, string text, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(at, rotation);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            // Not font.material: its shader ignores walls once OnGUI has run (see WorldText.shader).
            go.GetComponent<MeshRenderer>().sharedMaterial = mats["TextoMundo"];
            return mesh;
        }

        static void MarkReportable(GameObject go, string displayName)
        {
            var r = go.AddComponent<Reportable>();
            Set(r, "displayName", displayName);
        }

        // Instantiates a Piso 13 FBX and applies the same axis/scale correction as P13World.Model
        // (each FBX carries P13_AXIS_O/X/Y/Z markers from the RealityKit export), baked into the scene.
        static GameObject Piso13Model(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Piso13Models + name + "/" + name + ".fbx");
            if (!prefab)
                throw new FileNotFoundException("Falta el modelo de Piso 13: " + name);
            var wrapper = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
            model.name = "Visual";
            var all = model.GetComponentsInChildren<Transform>(true);
            Transform Find(string axis) => all.FirstOrDefault(t => t.name == "P13_AXIS_" + axis);
            Transform origin = Find("O"), ax = Find("X"), ay = Find("Y"), az = Find("Z");
            if (origin && ax && ay && az)
            {
                Vector3 x = ax.position - origin.position, y = ay.position - origin.position, z = az.position - origin.position;
                var rotation = Quaternion.LookRotation(z.normalized, y.normalized);
                float sign = Vector3.Dot(Vector3.Cross(y, z), x) >= 0 ? 1 : -1;
                model.transform.rotation = Quaternion.Inverse(rotation);
                model.transform.localScale = new Vector3(sign / x.magnitude, 1 / y.magnitude, 1 / z.magnitude);
                model.transform.localPosition -= wrapper.transform.InverseTransformVector(origin.position - wrapper.transform.position);
            }

            wrapper.transform.SetParent(parent, false);
            wrapper.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            return wrapper;
        }

        static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Piso13Audio + name + ".wav");
            if (!clip)
                Debug.LogWarning("Falta el audio de Piso 13: " + name);
            return clip;
        }

        // Sets a serialized (private) field the same way the Inspector does.
        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field) ?? throw new MissingFieldException(target.GetType().Name, field);
            switch (value)
            {
                case float f:
                    p.floatValue = f;
                    break;
                case int i:
                    p.intValue = i;
                    break;
                case bool b:
                    p.boolValue = b;
                    break;
                case string s:
                    p.stringValue = s;
                    break;
                case Object o:
                    p.objectReferenceValue = o;
                    break;
                case Object[] array:
                    p.arraySize = array.Length;
                    for (int k = 0; k < array.Length; k++)
                        p.GetArrayElementAtIndex(k).objectReferenceValue = array[k];
                    break;
                case null:
                    p.objectReferenceValue = null;
                    break;
                default:
                    throw new ArgumentException("Tipo no soportado: " + value.GetType());
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Folders()
        {
            foreach (var folder in new[] { Root + "/Scenes", Root + "/Prefabs", MaterialsFolder })
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }

        // Low-poly, short palette (GDD): flat colours; the light sources glow.
        static void Materials()
        {
            mats.Clear();
            Mat("Suelo", new Color(.2f, .21f, .22f), .25f);
            Mat("Pared", new Color(.62f, .63f, .6f), .08f);
            Mat("Techo", new Color(.7f, .7f, .68f), .05f);
            Mat("Marco", new Color(.3f, .29f, .27f), .2f);
            Mat("Madera", new Color(.45f, .33f, .22f), .25f);
            Mat("Tela", new Color(.17f, .19f, .23f), .1f);
            Mat("Metal", new Color(.42f, .44f, .46f), .45f, .6f);
            Mat("Negro", new Color(.05f, .05f, .05f), .3f);
            Mat("Panel", new Color(.55f, .56f, .58f), .3f, .3f);
            Mat("Rojo", new Color(.6f, .06f, .04f), .35f);
            Mat("Planta", new Color(.16f, .34f, .16f), .1f);
            Mat("Taza", new Color(.85f, .84f, .8f), .5f);
            Mat("Lienzo", new Color(.78f, .72f, .58f), .05f);
            Mat("Figura", new Color(.12f, .1f, .1f), .05f);
            Mat("Ojos", new Color(.9f, .88f, .8f), .1f);
            Mat("Pantalla", new Color(.02f, .05f, .07f), .7f, 0, new Color(.04f, .14f, .18f));
            Mat("Fluorescente", new Color(.9f, .92f, .95f), .1f, 0, new Color(1.15f, 1.2f, 1.25f));
            Mat("CartelVerde", new Color(.05f, .33f, .14f), .3f, 0, new Color(.03f, .55f, .22f));
            Mat("CartelBlanco", new Color(.9f, 1f, .95f), .3f, 0, new Color(1.3f, 1.5f, 1.4f));
            TextMaterial();
        }

        // 3D texts: the built-in font atlas with a shader that respects depth.
        static void TextMaterial()
        {
            string path = MaterialsFolder + "/T13_TextoMundo.mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(WorldTextShaderPath);
            if (!shader)
                throw new FileNotFoundException("Falta el shader " + WorldTextShaderPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }

            m.shader = shader;
            m.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(m);
            mats["TextoMundo"] = m;
        }

        static void Mat(string name, Color color, float smoothness, float metallic = 0, Color? emission = null)
        {
            string path = MaterialsFolder + "/T13_" + name + ".mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }

            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(m);
            mats[name] = m;
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
