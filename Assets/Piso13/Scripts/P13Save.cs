using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13
{
    [Serializable]
    public class P13Settings
    {
        public float master = .85f, music = .65f, effects = .8f, ambience = .65f, voices = .85f, sensitivity = 1, brightness = 0;
        public int quality = 1, width = 1152, height = 720;
        public bool fullscreen;
        public string controller = "Automático";
        public List<Binding> bindings = new List<Binding>();
        public int LightBudget => quality == 0 ? 5 : quality == 1 ? 10 : 18;
        static float Clamp(float v, float low, float high, float fallback)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, low, high);
        }

        public void Validate()
        {
            master = Clamp(master, 0, 1, .85f);
            music = Clamp(music, 0, 1, .65f);
            effects = Clamp(effects, 0, 1, .8f);
            ambience = Clamp(ambience, 0, 1, .65f);
            voices = Clamp(voices, 0, 1, .85f);
            sensitivity = Clamp(sensitivity, .3f, 2, 1);
            brightness = Clamp(brightness, -.15f, .15f, 0);
            quality = Mathf.Clamp(quality, 0, 2);
            if (width != 1152 && width != 1440)
                width = 1152;
            height = width == 1152 ? 720 : 900;
        }

        public void Apply()
        {
            Validate();
            QualitySettings.antiAliasing = quality == 0 ? 0 : quality == 1 ? 2 : 4;
            QualitySettings.pixelLightCount = LightBudget;
            QualitySettings.shadows = quality == 2 ? ShadowQuality.All : ShadowQuality.Disable;
            QualitySettings.vSyncCount = 1;
            Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }

    [Serializable]
    public class Binding
    {
        public string action, key, pad;
        public Binding(string a, string k, string p)
        {
            action = a;
            key = k;
            pad = p;
        }
    }

    [Serializable]
    public class DoorSave
    {
        public string id;
        public bool open;
    }

    [Serializable]
    public class P13Checkpoint
    {
        public int version = 1, minute, night = 1, floor = 13, trips, callAttempts;
        public bool demo = true, technical, servers, archive, returned, watchedCCTV, doorClosed, flashlight = true, callHeard, archiveRead, elevatorNeedsService, setPieceCompleted, elenaHelpHeard;
        public float energy = 100, yaw, pitch;
        public Vector3 player, guard;
        public float guardYaw;
        public List<int> discovered = new List<int>();
        public List<string> clues = new List<string>();
        public List<DoorSave> doors = new List<DoorSave>();
        public bool Valid(P13World world)
        {
            if (version != 1 || minute < 0 || minute > 360 || night < 1 || night > 5 || floor != 12 && floor != 13 || !Finite(energy) || energy < 0 || energy > 100 || float.IsNaN(yaw) || float.IsInfinity(yaw) || !Finite(pitch) || !Finite(guardYaw) || Mathf.Abs(pitch) > 63 || !Finite(player) || !Finite(guard) || servers && !technical || archive && !servers || returned && !(demo ? servers : archive) || doorClosed && energy <= 0 || floor != (player.x > 20 ? 12 : 13) || guard.x > 20 || P13Rules.Won(minute, returned, watchedCCTV, demo))
                return false;
            bool old = world.DoorClosed;
            world.DoorClosed = doorClosed;
            bool valid = world.CanStand(player, .34f, false) && world.CanStand(guard, .30f, false) && Vector3.Distance(player, guard) >= 4;
            world.DoorClosed = old;
            if (!valid || discovered == null || clues == null || doors == null || discovered.Count > 9 || clues.Count > 9 || doors.Count != world.Data.doors.Length || trips < 0 || callAttempts < 0 || callAttempts > 3)
                return false;
            var ids = new HashSet<string>();
            foreach (var d in doors)
            {
                if (d == null || !ids.Add(d.id))
                    return false;
                var spec = System.Array.Find(world.Data.doors, x => x.id == d.id);
                if (spec == null || d.open && spec.locked || spec.Bounds(d.open ? 1 : 0).Overlaps(player, .34f) || spec.Bounds(d.open ? 1 : 0).Overlaps(guard, .30f))
                    return false;
            }

            var rooms = new HashSet<int>();
            foreach (int r in discovered)
                if (r < 1 || r > 9 || !rooms.Add(r))
                    return false;
            for (int i = 0; i < clues.Count; i++)
                if (clues[i] != world.Data.narrative[i].id)
                    return false;
            return Mathf.Abs(player.y - .8f) < .01f && Mathf.Abs(guard.y - .8f) < .01f;
        }

        static bool Finite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        static bool Finite(Vector3 p)
        {
            return !float.IsNaN(p.x + p.y + p.z) && !float.IsInfinity(p.x + p.y + p.z);
        }
    }

    public static class P13Save
    {
        public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "Piso13");
        public static string CheckpointPath => Path.Combine(DirectoryPath, "unity-checkpoint-v1.json");
        public static string Error
        {
            get;
            private set;
        }

        public static bool Write<T>(string name, T value)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath, name), tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(value, true));
                if (File.Exists(path))
                    File.Replace(tmp, path, path + ".bak");
                else
                    File.Move(tmp, path);
                Error = null;
                return true;
            }
            catch (Exception e)
            {
                Error = "No se pudo guardar: " + e.Message;
                Debug.LogError(Error);
                return false;
            }
        }

        public static T Read<T>(string name)
            where T : class
        {
            try
            {
                string p = Path.Combine(DirectoryPath, name);
                if (!File.Exists(p))
                    return null;
                var f = new FileInfo(p);
                if (f.Length > 262144)
                    return null;
                return JsonUtility.FromJson<T>(File.ReadAllText(p));
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
        }

        public static bool Clear()
        {
            try
            {
                if (File.Exists(CheckpointPath))
                    File.Delete(CheckpointPath);
                Error = null;
                return true;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return false;
            }
        }
    }
}
