using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Turno0313.EditorTools
{
    // Act 2 (reality): the security cameras of the floor and the caseta monitors that show them.
    public static partial class GreyboxBuilder
    {
        const string UnlitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Unlit.shader";

        static void Act2(Transform level, Transform anomalies, GameObject systems)
        {
            CameraOnlyLayers();
            HandOnlyForPlayer();
            var cams = SecurityCameras(level);
            Monitors(level, cams);
            Act2Materials();
            var old = level.Find("Oficinas/Taza (Jefferson)");
            if (old)
                Object.DestroyImmediate(old.gameObject);
            Cup(anomalies, new Vector3(-3.75f, .76f, 17.75f));
            Person(anomalies, cams[2], new Vector3(-6.1f, 0, 15.4f));
            LiftFloor13(level);
            EscapedClock(anomalies, new[] { cams[0], cams[1], cams[2], cams[3], cams[4] }, new[]
            {
                new Vector3(0, 1.8f, 6.2f), // CAM 01: flotando en medio del pasillo
                new Vector3(0, 1.8f, 18f), // CAM 02: flotando en medio del pasillo
                new Vector3(-4.2f, .955f, 13.4f), // CAM 03: de pie sobre un escritorio
                new Vector3(-6.3f, 1.7f, 18.8f), // CAM 04: flotando entre las filas de escritorios
                new Vector3(.6f, 1.7f, 17.9f) // CAM 05: delante de las puertas del ascensor
            });
            Act2Systems(systems);
            var (guard, head) = SleepingGuard(level.Find("Caseta"), cams[5], Vector3.zero, 0);
            FinalSystems(systems, cams[5], guard, head);
            GameSystems(level, systems);
        }

        static void Act2Materials()
        {
            // Light, shiny coffee and glowing steam: on a dark desk, seen through a camera, it still reads.
            Mat("Cafe", new Color(.42f, .24f, .11f), .92f);
            Mat("Vapor", new Color(.95f, .95f, .95f), .1f, 0, new Color(.7f, .7f, .7f));
            Mat("Traje", new Color(.1f, .1f, .12f), .2f);
            Mat("Uniforme", new Color(.13f, .17f, .3f), .25f);
            Mat("Piel", new Color(.72f, .58f, .48f), .2f);
        }

        static void FinalSystems(GameObject systems, SecurityCamera casetaCamera, GameObject guard, Transform head)
        {
            var final = systems.AddComponent<FinalSequence>();
            Set(final, "casetaCamera", casetaCamera);
            Set(final, "sleepingGuard", guard);
            Set(final, "guardHead", head);
            Set(final, "creditsMusic", Clip("P13_Menu_Ulrich"));
        }

        // Title and pause menus, the ambient hum (on its own object: its filter would muffle every other
        // sound on "Sistemas") and the look of each act.
        static void GameSystems(Transform level, GameObject systems)
        {
            systems.AddComponent<GameMenus>();
            var ambient = new GameObject("Ambiente");
            ambient.transform.SetParent(systems.transform, false);
            ambient.AddComponent<AmbientAudio>();
            var look = systems.AddComponent<ActLook>();
            Set(look, "lights", level.Find("Luces").GetComponentsInChildren<Light>());
        }

        // The escaped anomaly: the dream's 03:13 clock, one and a half times as big, with one spot per camera
        // (places[i] is where it shows up on cams[i], 2.5-4 m from it; it turns to face the lens).
        // It is drawn only by the camera it is on.
        static void EscapedClock(Transform anomalies, SecurityCamera[] cams, Vector3[] places)
        {
            var root = Group(anomalies, "Anomalia_Escapada");
            var clock = Group(root, "Reloj_Escapado");
            clock.localScale = Vector3.one * 1.5f;
            Box("Caja", clock, Vector3.zero, new Vector3(.06f, .26f, .62f), mats["Negro"], false);
            Text("Hora", clock, new Vector3(-.04f, 0, 0), Quaternion.Euler(0, 90, 0), "03:13", .018f, new Color(1f, .2f, .12f));
            var only = clock.gameObject.AddComponent<CameraOnly>();

            var spots = new Object[places.Length];
            var spotCams = new Object[places.Length];
            for (int i = 0; i < places.Length; i++)
            {
                // The clock's face is its local -x: turn it so that side looks at the camera.
                var toCamera = cams[i].transform.position - places[i];
                float yaw = Mathf.Atan2(toCamera.z, -toCamera.x) * Mathf.Rad2Deg;
                var spot = new GameObject("Sitio_CAM" + cams[i].Number.ToString("00")).transform;
                spot.SetParent(root, false);
                spot.SetPositionAndRotation(places[i], Quaternion.Euler(0, yaw, 0));
                spots[i] = spot;
                spotCams[i] = cams[i];
            }

            var source = root.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            var escaped = root.gameObject.AddComponent<EscapedAnomaly>();
            Set(escaped, "visual", only);
            Set(escaped, "cameras", spotCams);
            Set(escaped, "spots", spots);
            Set(escaped, "source", source);
        }

        // The final: the guard asleep in the caseta chair, slumped over the desk, face on his arms.
        // Only the caseta camera shows him; the final turns his head (the neck pivot) to the lens.
        // Built for a desk on his -z side, 0.75 m deep with its near edge at z = -1.075; at / yaw move him to
        // another desk.
        static (GameObject guard, Transform head) SleepingGuard(Transform parent, SecurityCamera onlyIn, Vector3 at, float yaw)
        {
            var root = Group(parent, "Guardia_Dormido");
            root.SetLocalPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            Box("Cadera", root, new Vector3(0, .6f, -.66f), new Vector3(.38f, .2f, .36f), mats["Uniforme"], false);
            Box("Muslos", root, new Vector3(0, .56f, -.9f), new Vector3(.36f, .14f, .42f), mats["Uniforme"], false);
            Box("Piernas", root, new Vector3(0, .27f, -1.1f), new Vector3(.32f, .5f, .14f), mats["Uniforme"], false);
            var torso = Box("Torso", root, new Vector3(0, .79f, -.95f), new Vector3(.44f, .6f, .25f), mats["Uniforme"], false);
            torso.transform.localRotation = Quaternion.Euler(-55f, 0, 0);
            Box("Brazos", root, new Vector3(0, .8f, -1.33f), new Vector3(.52f, .09f, .16f), mats["Uniforme"], false);

            var neck = new GameObject("Cuello").transform;
            neck.SetParent(root, false);
            neck.localPosition = new Vector3(0, .96f, -1.19f);
            // Face down on the arms, the top of the head towards the monitors.
            neck.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.back);
            Box("Cabeza", neck, new Vector3(0, .12f, 0), new Vector3(.2f, .24f, .22f), mats["Piel"], false);
            Box("Gorra", neck, new Vector3(0, .25f, .02f), new Vector3(.22f, .05f, .26f), mats["Uniforme"], false);
            // Big, faintly glowing eyes: the stare has to read on a small camera picture.
            Mat("OjosBrillo", new Color(.95f, .95f, .9f), .2f, 0, new Color(.9f, .9f, .85f));
            Box("Ojo_A", neck, new Vector3(-.05f, .14f, .113f), new Vector3(.055f, .032f, .006f), mats["OjosBrillo"], false);
            Box("Ojo_B", neck, new Vector3(.05f, .14f, .113f), new Vector3(.055f, .032f, .006f), mats["OjosBrillo"], false);

            var only = root.gameObject.AddComponent<CameraOnly>();
            Set(only, "cameras", new Object[] { onlyIn });
            root.gameObject.SetActive(false);
            return (root.gameObject, neck);
        }

        static void Act2Systems(GameObject systems)
        {
            var reality = systems.AddComponent<RealityMeter>();
            var director = systems.AddComponent<Act2Director>();
            Set(director, "spawn", systems.transform.Find("Inicio_Noche"));
            Set(director, "source", systems.GetComponent<AudioSource>());
            Set(director, "sinkClip", Clip("P13_Thunder_0"));
            var hud = systems.AddComponent<Act2HUD>();
            Set(hud, "reality", reality);
        }

        // "Una taza de café que siempre está llena". Exaggerated so the cameras catch it: once wrong it is
        // brimming, runs down the sides and over the desk's +z edge into a puddle on the floor, and steams.
        // 'at' = on top of a 0.76 m desk whose +z edge is 0.23 m away.
        static void Cup(Transform parent, Vector3 at, float yaw = 0)
        {
            var root = Group(parent, "Anomalia_Taza");
            root.SetLocalPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            Box("Cuerpo", root, new Vector3(0, .07f, 0), new Vector3(.13f, .14f, .13f), mats["Taza"]);
            Box("Asa", root, new Vector3(.08f, .075f, 0), new Vector3(.03f, .08f, .045f), mats["Taza"], false);
            var full = Group(root, "Llena");
            Box("Cafe", full, new Vector3(0, .141f, 0), new Vector3(.12f, .006f, .12f), mats["Cafe"], false);
            Box("Chorro_A", full, new Vector3(-.067f, .075f, .02f), new Vector3(.006f, .13f, .035f), mats["Cafe"], false);
            Box("Chorro_B", full, new Vector3(.02f, .075f, -.067f), new Vector3(.035f, .11f, .006f), mats["Cafe"], false);
            // The coffee runs over the desk's edge onto the carpet (the floor puddle is what reads on camera).
            // The puddles have colliders, so clicking them on a camera also counts.
            // North side: the one CAM 04 sees.
            Box("Charco", full, new Vector3(0, .003f, .09f), new Vector3(.45f, .006f, .3f), mats["Cafe"]);
            Box("Chorro_Borde", full, new Vector3(0, -.38f, .228f), new Vector3(.05f, .76f, .012f), mats["Cafe"], false);
            Box("Charco_Suelo", full, new Vector3(0, -.757f, .58f), new Vector3(.85f, .004f, .65f), mats["Cafe"]);
            Box("Vapor_1", full, new Vector3(-.025f, .22f, 0), new Vector3(.018f, .12f, .018f), mats["Vapor"], false);
            Box("Vapor_2", full, new Vector3(0, .28f, .012f), new Vector3(.018f, .15f, .018f), mats["Vapor"], false);
            Box("Vapor_3", full, new Vector3(.025f, .34f, -.012f), new Vector3(.018f, .12f, .018f), mats["Vapor"], false);
            full.gameObject.SetActive(false);

            var anomaly = root.gameObject.AddComponent<ToggleAnomaly>();
            Set(anomaly, "displayName", "Taza que siempre está llena");
            Set(anomaly, "acts", (int)ActMask.Reality);
            Set(anomaly, "normalObjects", new Object[0]);
            Set(anomaly, "anomalousObjects", new Object[] { full.gameObject });
        }

        // "Una persona de pie en una cámara que no sale en las demás". Only 'onlyIn' shows it, staring at the
        // lens; other cameras and the player's own eyes see the spot empty.
        static void Person(Transform parent, SecurityCamera onlyIn, Vector3 at)
        {
            var root = Group(parent, "Anomalia_Persona");
            root.localPosition = at;
            var look = onlyIn.transform.position - at;
            look.y = 0;
            root.localRotation = Quaternion.LookRotation(look);
            var body = Group(root, "Persona");
            Box("Piernas", body, new Vector3(0, .43f, 0), new Vector3(.34f, .86f, .2f), mats["Traje"], false);
            Box("Torso", body, new Vector3(0, 1.17f, 0), new Vector3(.46f, .62f, .26f), mats["Traje"], false);
            Box("Brazo_A", body, new Vector3(-.29f, 1.1f, 0), new Vector3(.1f, .62f, .12f), mats["Traje"], false);
            Box("Brazo_B", body, new Vector3(.29f, 1.1f, 0), new Vector3(.1f, .62f, .12f), mats["Traje"], false);
            Box("Cabeza", body, new Vector3(0, 1.62f, 0), new Vector3(.2f, .25f, .22f), mats["Lienzo"], false);
            Box("Ojo_A", body, new Vector3(-.045f, 1.65f, .112f), new Vector3(.03f, .02f, .005f), mats["Negro"], false);
            Box("Ojo_B", body, new Vector3(.045f, 1.65f, .112f), new Vector3(.03f, .02f, .005f), mats["Negro"], false);
            var only = body.gameObject.AddComponent<CameraOnly>();
            Set(only, "cameras", new Object[] { onlyIn });
            body.gameObject.SetActive(false);

            var anomaly = root.gameObject.AddComponent<ToggleAnomaly>();
            Set(anomaly, "displayName", "Persona que solo sale en una cámara");
            Set(anomaly, "acts", (int)ActMask.Reality);
            Set(anomaly, "reportableWhenNormal", false);
            Set(anomaly, "normalObjects", new Object[0]);
            Set(anomaly, "anomalousObjects", new Object[] { body.gameObject });
        }

        // "El ascensor marca un piso 13 que el edificio no tiene" (Extra, actos 1 y 2): the floor indicator.
        static void LiftFloor13(Transform level)
        {
            var lift = level.Find("Pasillo/Ascensor (Jefferson)");
            var reportable = lift.GetComponent<Reportable>();
            if (reportable)
                Object.DestroyImmediate(reportable);
            var normal = lift.Find("Indicador");
            // A plate behind the number, so clicking the "13" on a camera hits the lift and not the wall.
            Box("Placa_Indicador", lift, normal.localPosition + new Vector3(.025f, 0, 0), new Vector3(.03f, .12f, .26f), mats["Negro"]);
            var floor13 = Text("Indicador_13", lift, normal.localPosition, normal.localRotation, "13", .02f, new Color(1f, .62f, .1f));
            floor13.gameObject.SetActive(false);

            var anomaly = lift.gameObject.AddComponent<ToggleAnomaly>();
            Set(anomaly, "displayName", "Ascensor que marca el piso 13");
            Set(anomaly, "acts", (int)ActMask.Both);
            Set(anomaly, "normalObjects", new Object[] { normal.gameObject });
            Set(anomaly, "anomalousObjects", new Object[] { floor13.gameObject });
        }

        // The first person hand of the Player prefab on the SoloJugador layer, so the security cameras don't
        // show a hand floating in the caseta (the player has no body).
        static void HandOnlyForPlayer()
        {
            int layer = LayerMask.NameToLayer(CameraOnly.PlayerOnlyLayer);
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Mano_Linterna")
                    foreach (var part in t.GetComponentsInChildren<Transform>(true))
                        part.gameObject.layer = layer;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // The layers CameraOnly uses (CamaraSolo1-3, SoloJugador), added to Tags and Layers if they are missing.
        static void CameraOnlyLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int n = 1; n <= CameraOnly.Slots + 1; n++)
            {
                string name = n <= CameraOnly.Slots ? CameraOnly.LayerPrefix + n : CameraOnly.PlayerOnlyLayer;
                if (LayerMask.NameToLayer(name) >= 0)
                    continue;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var layer = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(layer.stringValue))
                        continue;
                    layer.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
            }
        }

        // Six cameras near the ceiling, each looking at a part of the floor (Act 2 reports through them).
        static SecurityCamera[] SecurityCameras(Transform level)
        {
            var root = Group(level, "Camaras_Seguridad");
            return new[]
            {
                CameraRig(root, 1, "PASILLO SUR", new Vector3(.95f, 2.75f, 2.35f), new Vector3(0, 1f, 12f)),
                CameraRig(root, 2, "PASILLO NORTE", new Vector3(-.95f, 2.75f, 21.7f), new Vector3(0, .9f, 12.5f)),
                CameraRig(root, 3, "OFICINAS ESTE", new Vector3(-1.6f, 2.75f, 11.35f), new Vector3(-7.5f, .6f, 18.5f)),
                CameraRig(root, 4, "OFICINAS OESTE", new Vector3(-9.65f, 2.75f, 20.65f), new Vector3(-3.5f, .6f, 12.5f)),
                CameraRig(root, 5, "ASCENSOR", new Vector3(-1f, 2.7f, 16.2f), new Vector3(1.2f, 1.6f, 17.9f)),
                CameraRig(root, 6, "CASETA", new Vector3(2.25f, 2.75f, 1.75f), new Vector3(0, .8f, -1.2f))
            };
        }

        static SecurityCamera CameraRig(Transform parent, int number, string label, Vector3 at, Vector3 lookAt)
        {
            var go = new GameObject("CAM_" + number.ToString("00"));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(lookAt - at));
            // The body sits behind the lens, outside the near plane.
            Box("Cuerpo", go.transform, new Vector3(0, 0, -.14f), new Vector3(.1f, .09f, .2f), mats["Negro"], false);
            Box("Led", go.transform, new Vector3(.035f, .03f, -.035f), new Vector3(.015f, .015f, .01f), mats["Rojo"], false);

            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 72f;
            cam.nearClipPlane = .08f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            // Turned on at runtime once it has its texture, so it never draws over the Game view.
            cam.enabled = false;

            var security = go.AddComponent<SecurityCamera>();
            Set(security, "number", number);
            Set(security, "label", label);
            return security;
        }

        // The three desk monitors of the caseta: each shows two cameras in turn; E opens the camera screen.
        static void Monitors(Transform level, SecurityCamera[] cams)
        {
            var group = level.Find("Caseta/Monitores_Camaras (Jefferson)");
            var template = ScreenMaterial();
            for (int i = 0; i < 3; i++)
            {
                var monitor = group.Find("Monitor_" + (i + 1));
                var at = monitor.localPosition;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Imagen";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(group, false);
                // A quad faces -Z; turned around it faces the chair.
                quad.transform.SetLocalPositionAndRotation(at + new Vector3(0, 0, .027f), Quaternion.Euler(0, 180, 0));
                quad.transform.localScale = new Vector3(.52f, .3f, 1);
                var label = Text("Etiqueta", group, at + new Vector3(.17f, .125f, .029f), Quaternion.Euler(0, 180, 0), "CAM", .0035f, Color.white);
                var screen = quad.AddComponent<MonitorScreen>();
                Set(screen, "screen", quad.GetComponent<Renderer>());
                Set(screen, "template", template);
                Set(screen, "label", label);
                // Seen from the chair, Monitor_3 (+x) is on the left: it shows CAM 01-02, then 03-04, then 05-06.
                int first = (2 - i) * 2;
                Set(screen, "feeds", new Object[] { cams[first], cams[first + 1] });
            }

            var source = group.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            var monitors = group.gameObject.AddComponent<SecurityMonitors>();
            Set(monitors, "source", source);
            Set(monitors, "correctClip", Clip("P13_UI_Confirm"));
            Set(monitors, "wrongClip", Clip("P13_UI_Hover"));
        }

        // Unlit screen material; each monitor copies it at runtime to show its own feed.
        static Material ScreenMaterial()
        {
            string path = MaterialsFolder + "/T13_PantallaCamara.mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(UnlitShaderPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }

            m.shader = shader;
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
