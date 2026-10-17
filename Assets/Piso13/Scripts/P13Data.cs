using System;
using UnityEngine;

namespace Piso13
{
    [Serializable]
    public class WorldData
    {
        public Obstacle[] walls, furniture, floor12;
        public Obstacle officeDoor;
        public CameraData[] cameras;
        public DoorData[] doors;
        public RoomData[] rooms12;
        public ClueData[] narrative;
        public JournalData[] journal;
        public PatrolData[] routes;
    }

    [Serializable]
    public struct Obstacle
    {
        public float minX, maxX, minZ, maxZ, height;
        public Obstacle(float ax, float bx, float az, float bz, float h = 3.4f)
        {
            minX = ax;
            maxX = bx;
            minZ = az;
            maxZ = bz;
            height = h;
        }

        public bool Intersects(Vector3 a, Vector3 b, float pad = 0)
        {
            var low = new Vector3(minX - pad, 0, minZ - pad);
            var high = new Vector3(maxX + pad, height, maxZ + pad);
            var d = b - a;
            float enter = 0, leave = 1;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < .00001f)
                {
                    if (a[i] < low[i] || a[i] > high[i])
                        return false;
                }
                else
                {
                    float t1 = (low[i] - a[i]) / d[i], t2 = (high[i] - a[i]) / d[i];
                    enter = Mathf.Max(enter, Mathf.Min(t1, t2));
                    leave = Mathf.Min(leave, Mathf.Max(t1, t2));
                    if (enter > leave)
                        return false;
                }
            }

            return true;
        }

        public bool Overlaps(Vector3 p, float radius)
        {
            float x = p.x - Mathf.Clamp(p.x, minX, maxX), z = p.z - Mathf.Clamp(p.z, minZ, maxZ);
            return x * x + z * z < radius * radius;
        }
    }

    [Serializable]
    public class CameraData
    {
        public string name, shortName;
        public Vector3 position, target;
    }

    [Serializable]
    public class DoorData
    {
        public string id;
        public int floor;
        public Vector3 hinge;
        public float width, yaw, swing, duration;
        public bool locked;
        public Vector3 Tip(float f)
        {
            float a = (yaw + swing * f) * Mathf.Deg2Rad;
            return hinge + new Vector3(Mathf.Cos(a) * width, 0, -Mathf.Sin(a) * width);
        }

        public Vector3 Hotspot
        {
            get
            {
                float a = yaw * Mathf.Deg2Rad;
                return hinge + new Vector3(Mathf.Cos(a) * .2f, 1.2f, -Mathf.Sin(a) * .2f);
            }
        }

        public Obstacle Bounds(float f)
        {
            var p = Tip(f);
            return new Obstacle(Mathf.Min(hinge.x, p.x) - .045f, Mathf.Max(hinge.x, p.x) + .045f, Mathf.Min(hinge.z, p.z) - .045f, Mathf.Max(hinge.z, p.z) + .045f, 2.28f);
        }
    }

    [Serializable]
    public class RoomData
    {
        public string name;
        public Vector3 position;
    }

    [Serializable]
    public class ClueData
    {
        public string id, title, speaker, text, reply;
        public int room;
        public Vector3 position;
    }

    [Serializable]
    public class JournalData
    {
        public int id;
        public string title, text;
    }

    [Serializable]
    public class PatrolData
    {
        public int from, to;
        public Vector3[] points;
    }

    [Serializable]
    public class ClipManifest
    {
        public ClipDefinition[] clips;
    }

    [Serializable]
    public class ClipDefinition
    {
        public string name;
        public int startFrame, endFrame;
        public float fps, referenceSpeed;
        public bool loop;
    }

    public static class P13Rules
    {
        public static float Drain(bool door, bool light, bool cctv, bool task)
        {
            return .08f + (door ? .18f : 0) + (light ? .04f : 0) + (cctv ? .025f : 0) + (task ? .15f : 0);
        }

        public static bool Won(int minute, bool returned, bool watched, bool demo)
        {
            return minute >= 360 && returned && (!demo || watched);
        }

        public static int Room(Vector3 p)
        {
            float z = -p.z;
            if (p.x > 20)
                return 12;
            if (Mathf.Abs(p.x) < 2.68f && Mathf.Abs(z) < 2.68f)
                return 1;
            if (Mathf.Abs(p.x) < 1 && z > 6.2f)
                return 3;
            if (Mathf.Abs(p.x) < 2.68f)
                return 4;
            if (z > 3)
                return p.x < 0 ? 5 : 6;
            if (z < -3)
                return p.x < 0 ? 8 : 9;
            return p.x < 0 ? 2 : 7;
        }

        public static Vector3 Flat(Vector3 p)
        {
            p.y = .8f;
            return p;
        }

        public static Vector3 RK(float x, float y, float z)
        {
            return new Vector3(x, y, -z);
        }
    }
}
