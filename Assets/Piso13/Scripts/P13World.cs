using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Piso13
{
    public sealed class P13World
    {
        public readonly WorldData Data;
        public readonly List<Obstacle> Leaves = new List<Obstacle>();
        public bool DoorClosed;
        public int Revision;
        public Transform Root, OfficeDoor;
        public readonly List<Light> Lights = new List<Light>();
        public readonly List<Transform> Windows = new List<Transform>();
        readonly List<Tuple<Material, Color, int>> emitters = new List<Tuple<Material, Color, int>>();
        public P13World(WorldData d)
        {
            Data = d;
        }

        public IEnumerable<Obstacle> Obstacles(bool floor12, bool dynamic = true)
        {
            var all = floor12 ? Data.floor12 : Data.walls.Concat(Data.furniture);
            foreach (var o in all)
                yield return o;
            if (!floor12 && DoorClosed)
                yield return Data.officeDoor;
            if (dynamic)
                foreach (var o in Leaves)
                    yield return o;
        }

        public bool Clear(Vector3 a, Vector3 b, float pad = 0, bool walking = false)
        {
            if ((a.x > 20) != (b.x > 20))
                return false;
            if (walking)
            {
                a.y = .2f;
                b.y = .2f;
            }

            foreach (var o in Obstacles(a.x > 20))
                if (o.Intersects(a, b, pad))
                    return false;
            return true;
        }

        public bool CanStand(Vector3 p, float radius = .34f, bool dynamic = true)
        {
            if (p.x > 20)
            {
                if (p.x <= 24 + radius || p.x >= 36 - radius || p.z >= 23.9f - radius || p.z <= -7.3f + radius)
                    return false;
            }
            else if (Mathf.Abs(p.x) >= 7.7f || Mathf.Abs(p.z) >= 7.7f)
                return false;
            foreach (var o in Obstacles(p.x > 20, dynamic))
                if (o.Overlaps(p, radius))
                    return false;
            return true;
        }

        public Vector3 Move(Vector3 p, Vector3 delta, float radius)
        {
            var next = p + new Vector3(delta.x, 0, 0);
            if (CanStand(next, radius) && Clear(p, next, 0.05f, true))
                p = next;
            next = p + new Vector3(0, 0, delta.z);
            if (CanStand(next, radius) && Clear(p, next, 0.05f, true))
                p = next;
            return p;
        }

        public static GameObject Box(string name, Vector3 pos, Vector3 size, Color color, Transform parent = null)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = name;
            o.transform.SetParent(parent, false);
            o.transform.localPosition = pos;
            o.transform.localScale = size;
            UnityEngine.Object.Destroy(o.GetComponent<Collider>());
            var m = new Material(LitShader());
            m.color = color;
            m.SetFloat("_Smoothness", .2f);
            o.GetComponent<Renderer>().sharedMaterial = m;
            return o;
        }

        // The project renders with URP; built-in "Standard" materials would show up pink.
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        static Shader lit;

        public static Shader LitShader()
        {
            if (!lit)
                lit = Shader.Find(LitShaderName);
            if (!lit)
                throw new InvalidOperationException("Falta el shader " + LitShaderName + ". Revisa que URP esté instalado y activo.");
            return lit;
        }

        public static GameObject Model(string name, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/" + name + "/" + name);
            if (!prefab)
                throw new InvalidOperationException("Modelo requerido ausente: " + name);
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            var model = UnityEngine.Object.Instantiate(prefab, wrapper.transform);
            model.name = "Visual";
            // Exported metre-axis markers remove FBX exporter orientation/unit ambiguity.
            var tr = model.GetComponentsInChildren<Transform>(true);
            Func<string, Transform> find = n => tr.FirstOrDefault(t => t.name == "P13_AXIS_" + n);
            var origin = find("O");
            if (origin && find("X") && find("Y") && find("Z"))
            {
                Vector3 x = find("X").position - origin.position, y = find("Y").position - origin.position, z = find("Z").position - origin.position;
                Quaternion rotation = Quaternion.LookRotation(z.normalized, y.normalized);
                float sign = Vector3.Dot(Vector3.Cross(y, z), x) >= 0 ? 1 : -1;
                model.transform.rotation = Quaternion.Inverse(rotation);
                model.transform.localScale = new Vector3(sign / x.magnitude, 1 / y.magnitude, 1 / z.magnitude);
                model.transform.localPosition -= wrapper.transform.InverseTransformVector(origin.position - wrapper.transform.position);
            }

            foreach (var c in model.GetComponentsInChildren<Collider>())
                UnityEngine.Object.Destroy(c);
            return wrapper;
        }

        public void Build()
        {
            Root = new GameObject("Piso13_World").transform;
            foreach (var name in new[]{"Piso13_CAM01", "CAM02_Recepcion", "CAM03_Ascensor", "CAM04_Pasillo", "CAM05_Archivo", "CAM06_Servidores", "CAM07_Tecnica", "CAM08_Abandonada", "CAM09_Almacen", "Piso12_Administrativo"})
            {
                var loaded = Model(name, Root);
                if (name == "Piso13_CAM01")
                    foreach (var t in loaded.GetComponentsInChildren<Transform>(true))
                        if (new[]{"CAM02_Recepcion", "CAM03_Ascensor", "CAM04_Pasillo", "CAM05_Archivo", "CAM06_Servidores", "CAM07_Tecnica", "CAM08_Abandonada", "CAM09_Almacen"}.Contains(t.name))
                            t.gameObject.SetActive(false);
            }

            OfficeDoor = Model("CAM01_Puerta", Root).transform;
            OfficeDoor.position = P13Rules.RK(1.85f, 1.35f, 3);
            for (int i = 0; i < Data.cameras.Length; i++)
            {
                var c = Data.cameras[i];
                var l = new GameObject("Luz_" + c.shortName).AddComponent<Light>();
                l.transform.SetParent(Root);
                l.transform.position = c.position + Vector3.down * .1f;
                l.type = LightType.Point;
                l.range = 8;
                l.intensity = 1.25f;
                l.color = new Color(.55f, .72f, .8f);
                Lights.Add(l);
            }

            foreach (var room in Data.rooms12)
            {
                var l = new GameObject("Luz_" + room.name).AddComponent<Light>();
                l.transform.SetParent(Root);
                l.transform.position = room.position + Vector3.up * 2;
                l.type = LightType.Point;
                l.range = 7;
                l.intensity = 1.15f;
                l.color = new Color(.65f, .74f, .67f);
                Lights.Add(l);
            }

            foreach (var renderer in Root.GetComponentsInChildren<Renderer>())
            {
                string n = renderer.name;
                Color color;
                float gain;
                if (n.Contains("Fluorescent_On"))
                {
                    color = new Color(.62f, .84f, .89f);
                    gain = 1.35f;
                }
                else if (n.Contains("Fluorescent_Dim"))
                {
                    color = new Color(.36f, .49f, .48f);
                    gain = .7f;
                }
                else if (n.Contains("ScreenText"))
                {
                    color = new Color(.33f, .68f, .59f);
                    gain = .65f;
                }
                else if (n.Contains("Screen"))
                {
                    color = new Color(.025f, .085f, .077f);
                    gain = .45f;
                }
                else if (n.Contains("Emergency"))
                {
                    color = new Color(.86f, .14f, .024f);
                    gain = .72f;
                }
                else
                    continue;
                foreach (var m in renderer.materials)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", color * gain);
                    emitters.Add(Tuple.Create(m, color * gain, P13Rules.Room(renderer.bounds.center)));
                }
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.16f, .17f, .20f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.015f, .022f, .03f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = .015f;
            foreach (var clue in Data.narrative)
            {
                var marker = Box("Pista_" + clue.id, clue.position, new Vector3(.19f, .025f, .24f), new Color(.45f, .40f, .27f), Root);
                marker.transform.rotation = Quaternion.Euler(0, clue.room * 17, 0);
            }

            // Original windows: render-only rain streaks, no new collision or passage.
            foreach (var p in new[]{P13Rules.RK(-7.68f, 1.85f, -5.2f), P13Rules.RK(-7.68f, 1.85f, -.4f), P13Rules.RK(24, 1.75f, -7.3f), P13Rules.RK(36, 1.75f, -7.3f), P13Rules.RK(24, 1.75f, -11.5f), P13Rules.RK(36, 1.75f, -11.5f), P13Rules.RK(24, 1.75f, 2.4f), P13Rules.RK(36, 1.75f, -2.6f)})
            {
                var w = new GameObject("LluviaVentana").transform;
                w.SetParent(Root);
                w.position = p;
                Windows.Add(w);
                for (int i = 0; i < 14; i++)
                    Box("Gota", new Vector3(.01f, (i * .397f) % 1.5f - .75f, (i * .173f) % 2 - .99f), new Vector3(.006f, .10f, .006f), new Color(.19f, .26f, .32f), w);
            }
        }

        public void UpdateEmitters(Func<int, float> multiplier)
        {
            foreach (var e in emitters)
                e.Item1.SetColor("_EmissionColor", e.Item2 * multiplier(e.Item3));
        }

        public void UpdateLights(Vector3 observer, int budget, float multiplier, float brightness)
        {
            foreach (var l in Lights.OrderBy(l => (l.transform.position - observer).sqrMagnitude).Select((l, i) => new
            {
            l, i
            }

            ))
            {
                l.l.enabled = l.i < budget;
                l.l.intensity = (l.l.transform.position.x > 20 ? 1.15f : 1.25f) * multiplier * (1 + brightness);
            }

            OfficeDoor.localPosition = new Vector3(DoorClosed ? 0 : 1.85f, 1.35f, -3);
        }
    }

    public sealed class P13Navigation
    {
        readonly P13World world;
        const float spacing = .2f;
        public const float Radius = .54f;
        bool floor12;
        int width, height, revision = -1;
        bool closed;
        bool[] grid;
        public P13Navigation(P13World world)
        {
            this.world = world;
        }

        Vector3 Point(int i)
        {
            return new Vector3(i % width * spacing + (floor12 ? 24 : -7.6f), .8f, i / width * spacing + (floor12 ? -7.4f : -7.6f));
        }

        void Select(Vector3 p)
        {
            bool f = p.x > 20;
            if (grid != null && f == floor12 && closed == world.DoorClosed && revision == world.Revision)
                return;
            floor12 = f;
            closed = world.DoorClosed;
            revision = world.Revision;
            width = f ? 61 : 77;
            height = f ? 158 : 77;
            grid = new bool[width * height];
            for (int i = 0; i < grid.Length; i++)
                grid[i] = world.CanStand(Point(i), Radius + .04f) && world.Clear(Point(i), Point(i), Radius + .03f, true);
        }

        public Vector3 Safe(Vector3 p)
        {
            Select(p);
            if (world.CanStand(p, Radius + .04f) && world.Clear(p, p, Radius + .03f, true))
                return p;
            float best = float.MaxValue;
            Vector3 result = p;
            for (int i = 0; i < grid.Length; i++)
                if (grid[i])
                {
                    float d = (p - Point(i)).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        result = Point(i);
                    }
                }

            return result;
        }

        int Nearest(Vector3 p)
        {
            int result = -1;
            float best = 2.25f;
            for (int i = 0; i < grid.Length; i++)
                if (grid[i])
                {
                    float d = (p - Point(i)).sqrMagnitude;
                    if (d < best && world.Clear(p, Point(i), Radius + .01f, true))
                    {
                        best = d;
                        result = i;
                    }
                }

            return result;
        }

        public List<Vector3> Path(Vector3 start, Vector3 goal)
        {
            var empty = new List<Vector3>();
            if ((start.x > 20) != (goal.x > 20))
                return empty;
            goal = Safe(goal);
            if (world.Clear(start, goal, Radius + .02f, true))
                return new List<Vector3>{goal};
            int from = Nearest(start), to = Nearest(goal);
            if (from < 0 || to < 0)
                return empty;
            var costs = Enumerable.Repeat(float.PositiveInfinity, grid.Length).ToArray();
            var parents = Enumerable.Repeat(-1, grid.Length).ToArray();
            var seen = new bool[grid.Length];
            var heap = new Heap();
            costs[from] = 0;
            heap.Push(from, 0);
            while (heap.Count > 0)
            {
                int current = heap.Pop();
                if (seen[current])
                    continue;
                seen[current] = true;
                if (current == to)
                    break;
                int x = current % width, z = current / width;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nx >= width || nz < 0 || nz >= height)
                            continue;
                        int next = nz * width + nx;
                        if (!grid[next] || seen[next])
                            continue;
                        if (dx != 0 && dz != 0 && (!grid[z * width + nx] || !grid[nz * width + x]))
                            continue;
                        if (!world.Clear(Point(current), Point(next), Radius + .01f, true))
                            continue;
                        float c = costs[current] + spacing * (dx == 0 || dz == 0 ? 1 : 1.414214f);
                        if (c < costs[next])
                        {
                            costs[next] = c;
                            parents[next] = current;
                            heap.Push(next, c + Vector3.Distance(Point(next), Point(to)));
                        }
                    }
            }

            if (float.IsInfinity(costs[to]))
                return empty;
            var path = new List<Vector3>{goal};
            int node = to;
            while (node != from)
            {
                path.Add(Point(node));
                node = parents[node];
                if (node < 0)
                    return empty;
            }

            path.Add(Point(from));
            path.Reverse();
            var smooth = new List<Vector3>();
            var anchor = start;
            for (int i = 0; i < path.Count;)
            {
                int last = i;
                for (int j = i; j < path.Count; j++)
                {
                    if (world.Clear(anchor, path[j], Radius + .03f, true))
                        last = j;
                    else
                        break;
                }

                smooth.Add(path[last]);
                anchor = path[last];
                i = last + 1;
            }

            return smooth;
        }

        sealed class Heap
        {
            readonly List<KeyValuePair<int, float>> a = new List<KeyValuePair<int, float>>();
            public int Count => a.Count;
            void Swap(int i, int j)
            {
                var t = a[i];
                a[i] = a[j];
                a[j] = t;
            }

            public void Push(int id, float p)
            {
                a.Add(new KeyValuePair<int, float>(id, p));
                int i = a.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (a[parent].Value <= a[i].Value)
                        break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public int Pop()
            {
                int id = a[0].Key;
                var last = a[a.Count - 1];
                a.RemoveAt(a.Count - 1);
                if (a.Count > 0)
                {
                    a[0] = last;
                    int i = 0;
                    while (2 * i + 1 < a.Count)
                    {
                        int c = 2 * i + 1;
                        if (c + 1 < a.Count && a[c + 1].Value < a[c].Value)
                            c++;
                        if (a[i].Value <= a[c].Value)
                            break;
                        Swap(i, c);
                        i = c;
                    }
                }

                return id;
            }
        }
    }
}
