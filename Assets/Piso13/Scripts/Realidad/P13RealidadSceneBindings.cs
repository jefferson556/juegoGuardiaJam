using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Piso13.Realidad
{
    [DisallowMultipleComponent]
    public sealed class P13RealidadSceneBindings : MonoBehaviour
    {
        [Header("Mundo y puerta")]
        [SerializeField] private Transform _worldRoot, _officeDoor, _doorOpen, _doorClosed, _doorInteraction;
        [SerializeField] private Light[] _lights = Array.Empty<Light>();
        [SerializeField] private Transform[] _windows = Array.Empty<Transform>();
        [SerializeField] private WorldData _initialData;
        [SerializeField] private P13RealidadSpatialAuthoring[] _spatial = Array.Empty<P13RealidadSpatialAuthoring>();
        [Header("Jugador y enemigo")]
        [SerializeField] private Camera _view;
        [SerializeField] private Light _flashlight;
        [SerializeField] private Transform _hand, _playerSpawn, _enemySpawn;
        [SerializeField] private P13RealidadEnemy _enemy;
        [Header("Interactuables y anomalías")]
        [SerializeField] private P13VintagePhone _phone;
        [SerializeField] private P13CoffeeCup _coffee;
        [SerializeField] private P13PersonaEleganteAnomaly _persona;
        [SerializeField] private P13HumanMaskAnomaly _mask;
        [SerializeField] private P13Anomaly _clock;
        [SerializeField] private P13CCTVStation _station;
        [SerializeField] private GameObject _bed, _sleepingGuard;
        [SerializeField] private Transform _endingTarget;
        private Snapshot[] _snapshots;
        private struct Snapshot { public Transform Transform; public Vector3 Position, Scale; public Quaternion Rotation; public bool Active; }
        public Transform WorldRoot => _worldRoot;
        public Transform OfficeDoor => _officeDoor;
        public Transform DoorOpen => _doorOpen;
        public Transform DoorClosed => _doorClosed;
        public Transform DoorInteraction => _doorInteraction;
        public Light[] Lights => _lights;
        public Transform[] Windows => _windows;
        public P13RealidadSpatialAuthoring[] Spatial => _spatial;
        public Camera View => _view;
        public Light Flashlight => _flashlight;
        public Transform Hand => _hand;
        public Transform PlayerSpawn => _playerSpawn;
        public Transform EnemySpawn => _enemySpawn;
        public P13RealidadEnemy Enemy => _enemy;
        public P13VintagePhone Phone => _phone;
        public P13CoffeeCup Coffee => _coffee;
        public P13PersonaEleganteAnomaly Persona => _persona;
        public P13HumanMaskAnomaly Mask => _mask;
        public P13Anomaly Clock => _clock;
        public P13CCTVStation Station => _station;
        public GameObject Bed => _bed;
        public GameObject SleepingGuard => _sleepingGuard;
        public Transform EndingTarget => _endingTarget;
        public IEnumerable<P13RealidadSpatialAuthoring> OfKind(P13RealidadSpatialAuthoring.SpatialKind kind)
            => _spatial.Where(s => s != null && s.Kind == kind);
        private P13RealidadSpatialAuthoring _bounds12, _bounds13;
        private P13RealidadSpatialAuthoring[] _rooms;
        public P13RealidadSpatialAuthoring FloorBounds(bool floor12)
        {
            if (_bounds12 == null) _bounds12 = OfKind(P13RealidadSpatialAuthoring.SpatialKind.FloorBounds).First(s => s.Floor == 12);
            if (_bounds13 == null) _bounds13 = OfKind(P13RealidadSpatialAuthoring.SpatialKind.FloorBounds).First(s => s.Floor == 13);
            return floor12 ? _bounds12 : _bounds13;
        }
        public bool IsFloor12(Vector3 p)
        {
            var a = FloorBounds(true).WorldBounds; var b = FloorBounds(false).WorldBounds;
            p.y = a.center.y;
            return (a.ClosestPoint(p) - p).sqrMagnitude < (b.ClosestPoint(p) - p).sqrMagnitude;
        }
        public int Room(Vector3 p)
        {
            if (_rooms == null) _rooms = OfKind(P13RealidadSpatialAuthoring.SpatialKind.Room).OrderByDescending(r => r.Priority).ToArray();
            foreach (var r in _rooms)
                if (r.ContainsXZ(p)) return r.Id;
            return 0;
        }
        public WorldData CreateData()
        {
            var d = JsonUtility.FromJson<WorldData>(JsonUtility.ToJson(_initialData));
            d.walls = OfKind(P13RealidadSpatialAuthoring.SpatialKind.Wall).Select(s => s.ToObstacle()).ToArray();
            d.furniture = OfKind(P13RealidadSpatialAuthoring.SpatialKind.Furniture).Select(s => s.ToObstacle()).ToArray();
            d.floor12 = OfKind(P13RealidadSpatialAuthoring.SpatialKind.Floor12Obstacle).Select(s => s.ToObstacle()).ToArray();
            d.officeDoor = OfKind(P13RealidadSpatialAuthoring.SpatialKind.OfficeDoorObstacle).Single().ToObstacle();
            d.cameras = OfKind(P13RealidadSpatialAuthoring.SpatialKind.CCTV).OrderBy(s => s.Id)
                .Select(s => new CameraData { name = s.Label, shortName = s.gameObject.name, position = s.transform.position, target = s.Target.position }).ToArray();
            return d;
        }
        public void CaptureInitialState()
        {
            if (_snapshots != null) return;
            var transforms = _worldRoot.GetComponentsInChildren<Transform>(true)
                .Concat(_view.GetComponentsInChildren<Transform>(true)).Concat(_enemy.GetComponentsInChildren<Transform>(true)).Distinct();
            _snapshots = transforms.Select(t => new Snapshot { Transform = t, Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale, Active = t.gameObject.activeSelf }).ToArray();
        }
        public void RestoreInitialState()
        {
            if (_snapshots == null) CaptureInitialState();
            foreach (var s in _snapshots)
            {
                if (s.Transform == null) continue;
                s.Transform.localPosition = s.Position; s.Transform.localRotation = s.Rotation; s.Transform.localScale = s.Scale;
                s.Transform.gameObject.SetActive(s.Active);
            }
        }
        public List<string> ValidateScene()
        {
            var errors = new List<string>();
            if (_worldRoot == null || _officeDoor == null || _doorOpen == null || _doorClosed == null || _doorInteraction == null) errors.Add("Faltan referencias del mundo/puerta.");
            if (_view == null || _flashlight == null || _hand == null || _playerSpawn == null || _enemy == null || _enemySpawn == null) errors.Add("Faltan referencias del jugador/enemigo.");
            if (_phone == null || _coffee == null || _persona == null || _mask == null || _clock == null || _station == null || _bed == null || _sleepingGuard == null || _endingTarget == null) errors.Add("Faltan interactuables, anomalías o referencias del final.");
            if (_initialData == null) errors.Add("Faltan datos iniciales.");
            if (_spatial.Any(s => s == null) || _lights.Any(l => l == null) || _windows.Any(w => w == null)) errors.Add("Hay referencias nulas en listas.");
            foreach (var k in new[] { P13RealidadSpatialAuthoring.SpatialKind.CCTV, P13RealidadSpatialAuthoring.SpatialKind.Patrol, P13RealidadSpatialAuthoring.SpatialKind.MaskSpawn })
                if (OfKind(k).GroupBy(s => s.Id).Any(g => g.Count() > 1)) errors.Add("IDs duplicados: " + k);
            var cameras = OfKind(P13RealidadSpatialAuthoring.SpatialKind.CCTV).Select(s => s.Id).OrderBy(i => i).ToArray();
            if (!cameras.SequenceEqual(Enumerable.Range(0,9))) errors.Add("Se requieren nueve cámaras con IDs 0–8.");
            var patrol = OfKind(P13RealidadSpatialAuthoring.SpatialKind.Patrol).Select(s => s.Id).OrderBy(i => i).ToArray();
            if (!patrol.SequenceEqual(Enumerable.Range(0,10))) errors.Add("Se requieren diez nodos con IDs 0–9.");
            if (OfKind(P13RealidadSpatialAuthoring.SpatialKind.OfficeDoorObstacle).Count() != 1) errors.Add("Se requiere un obstáculo de puerta.");
            foreach (int floor in new[] {12,13})
                if (OfKind(P13RealidadSpatialAuthoring.SpatialKind.FloorBounds).Count(s => s.Floor == floor) != 1) errors.Add("Falta límite único de piso " + floor);
            foreach (var s in _spatial.Where(s => s != null))
            {
                if (s.IsVolume && (s.Size.x <= 0 || s.Size.y <= 0 || s.Size.z <= 0)) errors.Add("Volumen vacío: " + s.name);
                if (s.IsVolume && Quaternion.Angle(s.transform.rotation, Quaternion.identity) > .1f) errors.Add("Volumen rotado: " + s.name + ". Navegación AABB: mantener alineado a ejes.");
                if (s.Kind == P13RealidadSpatialAuthoring.SpatialKind.CCTV && (s.Target == null || (s.Target.position-s.transform.position).sqrMagnitude < .001f)) errors.Add("CCTV sin objetivo válido: " + s.name);
                if (s.Kind == P13RealidadSpatialAuthoring.SpatialKind.Patrol && s.Connections.Any(id => !OfKind(s.Kind).Any(n => n.Id == id))) errors.Add("Conexión inexistente: " + s.name);
            }
            return errors;
        }
    }
}
