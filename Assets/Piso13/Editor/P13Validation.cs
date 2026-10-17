using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Piso13.Editor
{
    public static class P13Validation
    {
        static int checks;
        static void Check(bool condition, string message)
        {
            checks++;
            if (!condition)
                throw new Exception("P13 VALIDATION: " + message);
        }

        public static void Run()
        {
            checks = 0;
            var data = JsonUtility.FromJson<WorldData>(Resources.Load<TextAsset>("Data/World").text);
            Check(data.cameras.Length == 9, "CAM01–CAM09");
            Check(data.doors.Length == 13, "13 puertas");
            Check(data.narrative.Length == 9, "9 pistas");
            Check(data.routes.Length == 100, "100 rutas originales");
            var world = new P13World(data);
            var spawn = P13Rules.RK(0, .8f, 1.2f);
            Check(world.CanStand(spawn), "posición inicial libre");
            Check(!world.Clear(spawn, spawn + Vector3.right * 30), "plantas aisladas");
            var moved = world.Move(spawn, Vector3.forward * 20, .34f);
            Check(Vector3.Distance(moved, spawn) < 19, "sin atravesar paredes");
            Check(Mathf.Abs(P13Rules.Drain(true, true, true, true) - .475f) < .0001f, "consumo de energía");
            Check(!P13Rules.Won(360, true, false, true) && P13Rules.Won(360, true, true, true), "demo exige CCTV");
            Check(!P13Rules.Won(359, true, true, false) && P13Rules.Won(360, true, false, false), "turno completo");
            var policy = new PlayerAudioPolicy();
            float breath;
            int steps = 0;
            for (int i = 0; i < 120; i++)
                if (policy.Step(1f / 60, 0, true, false, true, out breath))
                    steps++;
            Check(steps == 0, "quieto o colisión sin pasos");
            for (int i = 0; i < 120; i++)
                if (policy.Step(1f / 60, 2f / 60, false, false, true, out breath))
                    steps++;
            Check(steps == 4, "cadencia de marcha por desplazamiento");
            policy = default(PlayerAudioPolicy);
            for (int i = 0; i < 150; i++)
                policy.Step(1f / 60, 4.2f / 60, true, false, true, out breath);
            policy.Step(1f / 60, 0, false, false, true, out breath);
            Check(breath > 0, "respiración tras correr");
            for (int i = 0; i < 900; i++)
                policy.Step(1f / 60, 0, false, false, true, out breath);
            policy.Step(1f / 60, 0, false, false, true, out breath);
            Check(breath == 0, "respiración termina");
            policy.Step(.016f, 30, true, false, true, out breath);
            Check(breath == 0, "teletransporte no es carrera");
            var lift = new ElevatorMachine();
            Check(!lift.Request(12, true, false, 1, 0), "ascensor exige interior");
            Check(lift.Request(12, true, true, 1, 0), "viaje a P12");
            int arrivals = 0;
            for (int i = 0; i < 720; i++)
                if (lift.Tick(1f / 60, true) == 12)
                    arrivals++;
            Check(arrivals == 1 && lift.floor == 12 && lift.phase == ElevatorMachine.Phase.Open, "un solo traslado");
            lift.Request(13, true, true, 1, 0);
            lift.Tick(.5f, false);
            Check(lift.phase == ElevatorMachine.Phase.Stalled, "falla eléctrica detiene");
            Check(lift.Rescue(), "rescate disponible");
            for (int i = 0; i < 660; i++)
                lift.Tick(1f / 60, false);
            Check(lift.phase == ElevatorMachine.Phase.Open && lift.needsService, "rescate y mantenimiento");
            foreach (string audio in new[]{"P13_Menu_Ulrich", "P13_UI_Hover", "P13_UI_Confirm", "P13_Player_Recovery", "P13_Door_Open", "P13_Door_Closing", "P13_Door_Latch"}.Concat(Enumerable.Range(0, 4).Select(i => "P13_Rain_" + i)).Concat(Enumerable.Range(0, 6).Select(i => "P13_PlayerStep_" + i)).Concat(Enumerable.Range(0, 3).Select(i => "P13_Thunder_" + i)))
                Check(Resources.Load<AudioClip>("Audio/" + audio) != null, "audio " + audio);
            var library = JsonUtility.FromJson<P13Importer.MaterialLibrary>(File.ReadAllText(P13Importer.MaterialLibraryPath));
            foreach (string name in new[]{"Piso13_CAM01", "CAM02_Recepcion", "CAM03_Ascensor", "CAM04_Pasillo", "CAM05_Archivo", "CAM06_Servidores", "CAM07_Tecnica", "CAM08_Abandonada", "CAM09_Almacen", "Piso12_Administrativo", "CAM01_Puerta", "Puerta_Madera", "Piso13_HandFlashlight", "MonsterGuard", "Elena"})
            {
                string path = "Assets/Piso13/Resources/Models/" + name + "/" + name + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(model != null, "modelo " + name);
                Check(model.GetComponentsInChildren<Renderer>().Length > 0, "mallas " + name);
                var markers = model.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("P13_AXIS_")).ToArray();
                Check(markers.Length == 4, "ejes " + name);
                var validationRoot = new GameObject("P13ValidationRoot");
                try
                {
                    var calibrated = P13World.Model(name, validationRoot.transform);
                    var axes = calibrated.GetComponentsInChildren<Transform>();
                    foreach (var label in new[]{"O", "X", "Y", "Z"})
                    {
                        var marker = axes.First(t => t.name == "P13_AXIS_" + label);
                        var expected = label == "X" ? Vector3.right : label == "Y" ? Vector3.up : label == "Z" ? Vector3.forward : Vector3.zero;
                        Check((marker.position - expected).magnitude < .003f, "orientación/metraje " + name + " " + label);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(validationRoot);
                }

                var definitions = library.assets.FirstOrDefault(a => a.name == name)?.materials ?? new P13Importer.MaterialDefinition[0];
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Check(material && material.shader && material.shader.name.StartsWith("Universal Render Pipeline/"), "material URP de " + name);
                        // MaterialLibrary values must actually reach the imported material.
                        var definition = definitions.FirstOrDefault(d => d.name == material.name);
                        if (definition == null)
                            continue;
                        if (!string.IsNullOrEmpty(definition.albedo))
                            Check(material.GetTexture("_BaseMap") != null, "textura de " + name + "/" + material.name);
                        if (P13Importer.Emissive(definition.name))
                            Check(material.IsKeywordEnabled("_EMISSION"), "emisión de " + name + "/" + material.name);
                    }
                if (name == "MonsterGuard" || name == "Elena")
                {
                    var expected = JsonUtility.FromJson<ClipManifest>(Resources.Load<TextAsset>("Data/" + name + "_manifest").text);
                    var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                    foreach (var c in expected.clips)
                        Check(clips.Any(x => x.name == c.name && x.length > 0), name + " clip " + c.name);
                    Check(model.GetComponentInChildren<SkinnedMeshRenderer>() != null, name + " rig");
                    // The skinned mesh must come out at character size, not a few centimetres.
                    float height = name == "Elena" ? 1.68f : 2.22f;
                    var characterRoot = new GameObject("P13ValidationCharacter");
                    var baked = new Mesh();
                    try
                    {
                        var character = new P13Character(name, characterRoot.transform, height);
                        var skin = character.Visual.GetComponentInChildren<SkinnedMeshRenderer>();
                        skin.BakeMesh(baked, true);
                        float low = float.MaxValue, high = float.MinValue;
                        foreach (var v in baked.vertices)
                        {
                            float y = skin.transform.TransformPoint(v).y;
                            low = Mathf.Min(low, y);
                            high = Mathf.Max(high, y);
                        }

                        Check(high - low > height * .6f && high - low < height * 1.6f, "tamaño visible de " + name);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(baked);
                        UnityEngine.Object.DestroyImmediate(characterRoot);
                    }
                }
            }

            string result = "PASS " + checks + " comprobaciones de recursos y lógica en Unity. No constituye una prueba interactiva ni de FPS.";
            Directory.CreateDirectory("Documentation/Validation");
            File.WriteAllText("Documentation/Validation/EditorValidation.txt", result);
            Debug.Log("P13_VALIDATION " + result);
        }
    }
}
