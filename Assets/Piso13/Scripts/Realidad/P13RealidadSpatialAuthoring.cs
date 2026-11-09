using System;
using UnityEngine;

namespace Piso13.Realidad
{
    [DisallowMultipleComponent]
    public sealed class P13RealidadSpatialAuthoring : MonoBehaviour
    {
        public enum SpatialKind { Wall, Furniture, Floor12Obstacle, OfficeDoorObstacle, FloorBounds, Room, CCTV, Patrol, MaskSpawn, Spawn }
        [SerializeField] private SpatialKind _kind;
        [SerializeField] private int _id;
        [SerializeField] private int _floor = 13;
        [SerializeField] private int _priority;
        [SerializeField] private string _label;
        [SerializeField] private Vector3 _size = Vector3.one;
        [SerializeField] private Transform _target;
        [SerializeField] private int[] _connections = Array.Empty<int>();
        [Tooltip("Visual vinculado. Al mover este marcador, mueve el visual con él.")]
        [SerializeField] private Transform _linkedVisual;
        public SpatialKind Kind => _kind;
        public int Id => _id;
        public int Floor => _floor;
        public int Priority => _priority;
        public string Label => _label;
        public Transform Target => _target;
        public int[] Connections => _connections;
        public Transform LinkedVisual => _linkedVisual;
        public Vector3 Size => _size;
        public bool IsVolume => _kind <= SpatialKind.Room;
        public Bounds WorldBounds
        {
            get
            {
                var b = new Bounds(transform.position, Vector3.zero);
                for (int i = 0; i < 8; i++)
                    b.Encapsulate(transform.TransformPoint(Vector3.Scale(_size * .5f,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                return b;
            }
        }
        public Obstacle ToObstacle()
        {
            var b = WorldBounds;
            return new Obstacle(b.min.x, b.max.x, b.min.z, b.max.z, b.max.y);
        }
        public bool ContainsXZ(Vector3 point, float inset = 0)
        {
            var b = WorldBounds;
            return point.x > b.min.x + inset && point.x < b.max.x - inset && point.z > b.min.z + inset && point.z < b.max.z - inset;
        }
        public void Configure(SpatialKind kind, int id, int floor, string label, Vector3 size, int priority = 0, Transform target = null, int[] connections = null)
        {
            _kind = kind; _id = id; _floor = floor; _label = label; _size = size;
            _priority = priority; _target = target; _connections = connections ?? Array.Empty<int>();
        }
        public void LinkVisual(Transform visual)
        {
            _linkedVisual = visual;
            if (visual != null) visual.SetParent(transform, true);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = _kind == SpatialKind.Room || _kind == SpatialKind.FloorBounds ? Color.green : _kind == SpatialKind.CCTV ? Color.cyan : Color.yellow;
            if (IsVolume)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, _size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, .15f);
                Gizmos.DrawRay(transform.position, transform.forward * .6f);
                if (_target != null) Gizmos.DrawLine(transform.position, _target.position);
            }
        }
    }
}
