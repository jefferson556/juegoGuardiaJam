using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Piso13.Realidad;
using Kind = Piso13.Realidad.P13RealidadSpatialAuthoring.SpatialKind;

namespace Piso13.Tests.Editor
{
    public static class RealidadAuthoringChecks
    {
        static int checks;
        static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }
        [MenuItem("Piso 13/Realidad/Pruebas de autoría")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Pruebas de autoría requieren Edit Mode.");
            var scene = SceneManager.GetActiveScene();
            Check(scene.path == Piso13.Editor.P13RealidadSceneConverter.ScenePath, "Abrir Piso13 Realidad.");
            checks = 0;
            var roots = scene.GetRootGameObjects();
            var b = roots.SelectMany(r => r.GetComponentsInChildren<P13RealidadSceneBindings>(true)).Single();
            Check(b.ValidateScene().Count == 0, string.Join("; ", b.ValidateScene()));
            foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<Component>(true))) Check(c != null, "Missing Script en escena.");
            Check(roots.SelectMany(r => r.GetComponentsInChildren<P13RealidadGame>(true)).Count() == 1, "Controladores duplicados.");
            Check(roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Count(c => c.enabled && c.gameObject.activeInHierarchy) == 1, "Más de una cámara activa.");
            Check(roots.SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).Count(c => c.enabled && c.gameObject.activeInHierarchy) == 1, "AudioListeners duplicados.");
            Check(b.WorldRoot.GetComponentsInChildren<Renderer>(true).Length > 100, "Geometría ausente.");
            Check(!b.Clock.gameObject.activeSelf && !b.Persona.gameObject.activeSelf && !b.Enemy.gameObject.activeSelf, "Estado inicial incorrecto de anomalías/enemigo.");
            var world = new P13RealidadWorld(b);
            Check(world.Root == b.WorldRoot && world.OfficeDoor == b.OfficeDoor, "Mundo no enlazado.");
            Check(world.Data.walls.Length == b.OfKind(Kind.Wall).Count(), "Paredes no importadas desde autoría.");
            Check(world.Data.furniture.Length == b.OfKind(Kind.Furniture).Count(), "Muebles no importados desde autoría.");
            Check(world.Data.floor12.Length == b.OfKind(Kind.Floor12Obstacle).Count(), "Piso12 no importado desde autoría.");
            Check(world.CanStand(b.PlayerSpawn.position), "Spawn de jugador bloqueado.");
            Check(world.CanStand(b.EnemySpawn.position), "Spawn del enemigo bloqueado.");
            var navigation = new P13RealidadNavigation(world);
            foreach (var node in b.OfKind(Kind.Patrol))
            {
                var safe = navigation.Safe(node.transform.position);
                Check(world.CanStand(safe) && Vector3.Distance(safe, node.transform.position) < 1f, "Nodo sin proyección transitable cercana: " + node.name);
            }
            foreach (var camera in b.OfKind(Kind.CCTV))
            {
                var data = world.Data.cameras[camera.Id];
                Check((data.position - camera.transform.position).sqrMagnitude < .0001f, "CCTV ignora posición.");
                Check((data.target - camera.Target.position).sqrMagnitude < .0001f, "CCTV ignora objetivo.");
            }
            var floor13 = b.FloorBounds(false).WorldBounds; var floor12 = b.FloorBounds(true).WorldBounds;
            Check(!world.IsFloor12(floor13.center) && world.IsFloor12(floor12.center), "Clasificación de pisos incorrecta.");
            Check(!world.Clear(floor13.center, floor12.center), "Permite cruzar entre pisos separados.");
            var door = world.Data.officeDoor;
            var a = new Vector3((door.minX + door.maxX) * .5f, .8f, door.minZ - .2f);
            var z = new Vector3(a.x, .8f, door.maxZ + .2f);
            world.DoorClosed = false; Check(world.Clear(a, z, 0, true), "Puerta abierta bloquea el paso.");
            world.DoorClosed = true; Check(!world.Clear(a, z, 0, true), "Puerta cerrada no bloquea.");
            var probe = new GameObject("Realidad_TransientProbe");
            try
            {
                var marker = probe.AddComponent<P13RealidadSpatialAuthoring>();
                marker.Configure(Kind.Wall, 0, 13, "Probe", new Vector3(2, 3, 4));
                marker.transform.position = new Vector3(8, 1.5f, 9);
                var obstacle = marker.ToObstacle();
                Check(Mathf.Approximately(obstacle.minX, 7) && Mathf.Approximately(obstacle.maxZ, 11) && Mathf.Approximately(obstacle.height, 3), "Conversión de bounds incorrecta.");
                marker.transform.position += Vector3.right;
                Check(Mathf.Approximately(marker.ToObstacle().minX, 8), "Mover proxy no mueve obstáculo lógico.");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            int before = roots.Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
            var same = Piso13.Editor.P13RealidadSceneConverter.ConvertCurrentScene();
            int after = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
            Check(same == b && before == after, "El convertidor sobrescribe/duplica una escena convertida.");
            var editor = UnityEditor.Editor.CreateEditor(b);
            try
            {
                var inspector = editor.CreateInspectorGUI();
                Check(inspector != null && inspector.childCount > 2, "Inspector de autoría no construido.");
            }
            finally { UnityEngine.Object.DestroyImmediate(editor); }
            return "PASS: " + checks + " comprobaciones de autoría, referencias, CCTV, colisión y convertidor.";
        }
    }
}
