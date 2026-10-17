using UnityEngine;

namespace Turno0313.EditorTools
{
    // Act 1 (dream): the caseta wall clock anomaly, the alarm panel, the code digits hidden in the hallway
    // anomalies and the systems on "Sistemas" (night clock, sleep bar, wake code, HUD).
    public static partial class GreyboxBuilder
    {
        static readonly Color DigitInk = new Color(.8f, .1f, .07f);

        // Clock plus the digits on John's four anomalies. Returns the clock (the first anomaly of every night).
        static ClockAnomaly Act1Anomalies(Transform anomalies)
        {
            var clock = WallClock(anomalies);
            CodeDigits(anomalies);
            return clock;
        }

        // "El reloj marca 03:13 y no avanza" (caseta, pared este, frente al escritorio).
        static ClockAnomaly WallClock(Transform parent)
        {
            var root = Group(parent, "Anomalia_Reloj");
            root.localPosition = new Vector3(2.37f, 2.15f, 0);
            Box("Caja", root, Vector3.zero, new Vector3(.06f, .26f, .62f), mats["Negro"]);
            var display = Text("Hora", root, new Vector3(-.04f, 0, 0), Quaternion.Euler(0, 90, 0), "03:13", .018f, new Color(1f, .2f, .12f));

            var anomaly = root.gameObject.AddComponent<ClockAnomaly>();
            Set(anomaly, "displayName", "Reloj parado en 03:13");
            Set(anomaly, "display", display);
            Set(anomaly, "source", Source3D(root.gameObject, 8f));
            return anomaly;
        }

        // One digit of the code on each hallway anomaly, only visible while it looks wrong.
        static void CodeDigits(Transform anomalies)
        {
            // Door: written on the door leaf, facing the corridor.
            var door = anomalies.Find("Anomalia_PuertaEnPared");
            Digit(door, Text("Digito", door, new Vector3(1.19f, 1.6f, 6f), Quaternion.Euler(0, 90, 0), "_ 0 _ _", .03f, DigitInk));

            // Portrait: across the top of the canvas, above the head.
            var portrait = anomalies.Find("Anomalia_Retrato");
            Digit(portrait, Text("Digito", portrait, new Vector3(.045f, .31f, 0), Quaternion.Euler(0, -90, 0), "_ 0 _ _", .012f, DigitInk));

            // EXIT sign: on the wall the wrong arrow points at.
            var sign = anomalies.Find("Anomalia_CartelSalida");
            Digit(sign, Text("Digito", sign, new Vector3(1.14f, -.35f, 0), Quaternion.Euler(0, 90, 0), "_ 0 _ _", .03f, DigitInk));

            // Chair: both faces of the backrest, since it lands facing any direction.
            var chair = anomalies.Find("Anomalia_Silla/Silla");
            Digit(chair.parent,
                Text("Digito_Frente", chair, new Vector3(0, .82f, -.18f), Quaternion.Euler(0, 180, 0), "_ 0 _ _", .011f, DigitInk),
                Text("Digito_Detras", chair, new Vector3(0, .82f, -.24f), Quaternion.identity, "_ 0 _ _", .011f, DigitInk));
        }

        static void Digit(Transform anomalyRoot, params TextMesh[] labels)
        {
            var digit = anomalyRoot.gameObject.AddComponent<CodeDigit>();
            Set(digit, "labels", labels);
            foreach (var label in labels)
                label.GetComponent<Renderer>().enabled = false;
        }

        // Panel de alarma (caseta, pared oeste): case, LED, small screen and a 3x4 keypad.
        static void AlarmPanelProp(Transform caseta)
        {
            var panel = Group(caseta, "Panel_Alarma");
            panel.localPosition = new Vector3(-2.37f, 1.35f, .6f);
            Box("Caja", panel, Vector3.zero, new Vector3(.06f, .42f, .32f), mats["Panel"]);
            var led = Box("Led", panel, new Vector3(.035f, .17f, .11f), new Vector3(.01f, .03f, .03f), mats["Rojo"], false);
            Text("Rotulo", panel, new Vector3(.04f, .17f, -.03f), Quaternion.Euler(0, -90, 0), "ALARMA", .006f, new Color(.1f, .1f, .1f));
            Box("Pantalla", panel, new Vector3(.034f, .09f, 0), new Vector3(.01f, .07f, .26f), mats["Pantalla"], false);
            var screen = Text("Pantalla_Texto", panel, new Vector3(.041f, .09f, 0), Quaternion.Euler(0, -90, 0), "- - - -", .008f, new Color(.55f, 1f, .7f));
            for (int i = 0; i < 12; i++)
                Box("Tecla_" + (i + 1), panel, new Vector3(.034f, .01f - i / 3 * .06f, -.08f + i % 3 * .08f), new Vector3(.012f, .045f, .06f), mats["Negro"], false);

            var alarm = panel.gameObject.AddComponent<AlarmPanel>();
            Set(alarm, "screen", screen);
            Set(alarm, "led", led.GetComponent<Renderer>());
            Set(alarm, "source", Source3D(panel.gameObject, 6f));
            Set(alarm, "keyClip", Clip("P13_UI_Hover"));
            Set(alarm, "correctClip", Clip("P13_UI_Confirm"));
            MarkReportable(panel.gameObject, "Panel de alarma");
        }

        static void Act1Systems(GameObject systems)
        {
            systems.AddComponent<NightClock>();
            var sleep = systems.AddComponent<SleepMeter>();
            systems.AddComponent<WakeCode>();
            systems.AddComponent<ScreenFade>();

            // Where every new night starts: the player spawn in the caseta, facing the door.
            var spawn = new GameObject("Inicio_Noche").transform;
            spawn.SetParent(systems.transform, false);
            spawn.localPosition = new Vector3(0, 0, .4f);
            var audio = systems.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0;
            var cycle = systems.AddComponent<NightCycle>();
            Set(cycle, "spawn", spawn);
            Set(cycle, "source", audio);
            Set(cycle, "sinkClip", Clip("P13_Thunder_0"));
            Set(cycle, "returnClip", Clip("P13_Player_Recovery"));

            // False ending: he wakes up standing by his desk, facing the camera monitors.
            var wake = new GameObject("Despertar_Escritorio").transform;
            wake.SetParent(systems.transform, false);
            wake.SetLocalPositionAndRotation(new Vector3(.6f, 0, -.55f), Quaternion.Euler(0, 180, 0));
            var ending = systems.AddComponent<FalseEnding>();
            Set(ending, "wakePoint", wake);
            Set(ending, "creditsMusic", Clip("P13_Menu_Ulrich"));

            var hud = systems.AddComponent<Act1HUD>();
            Set(hud, "sleep", sleep);
            Set(hud, "panel", Object.FindAnyObjectByType<AlarmPanel>());
        }

        static AudioSource Source3D(GameObject go, float maxDistance)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            return source;
        }
    }
}
