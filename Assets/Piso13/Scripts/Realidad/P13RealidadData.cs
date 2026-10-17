using System;
using UnityEngine;

namespace Piso13.Realidad
{
    [Serializable]
    public class P13RealidadSettings
    {
        public float master = 1f;
        public float effects = 1f;
        public float voices = 1f;
        public float music = 0.8f;
        public float sensitivity = 1.6f;
        public bool invertY = false;
        public bool subtitles = true;
    }

    public static class P13RealidadRules
    {
        public static Vector3 RK(float x, float y, float z)
        {
            return new Vector3(x, y, -z);
        }

        public static Vector3 Flat(Vector3 p)
        {
            p.y = 0.8f;
            return p;
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
    }
}
