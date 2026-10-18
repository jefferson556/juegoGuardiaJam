using UnityEngine;

namespace Turno0313
{
    // A small texture of moving static (monitors without signal, the camera screen). Shared by everyone;
    // it changes every other frame while someone reads it.
    public static class StaticNoise
    {
        const int W = 96, H = 54;
        static Texture2D texture;
        static Color32[] pixels;
        static int frame = -1;

        public static Texture Texture
        {
            get
            {
                if (!texture)
                {
                    texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                    pixels = new Color32[W * H];
                }

                int now = Time.frameCount / 2;
                if (now != frame)
                {
                    frame = now;
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        byte v = (byte)Random.Range(0, 256);
                        pixels[i] = new Color32(v, v, v, 255);
                    }

                    texture.SetPixels32(pixels);
                    texture.Apply(false);
                }

                return texture;
            }
        }
    }
}
