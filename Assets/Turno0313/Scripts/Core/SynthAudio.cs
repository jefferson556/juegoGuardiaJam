using UnityEngine;

namespace Turno0313
{
    // Placeholder sounds made in code until Elvis has the real ones: an alarm clock beep and static noise
    // (also useful for the camera monitors). Every component that uses them has a clip field that wins.
    public static class SynthAudio
    {
        const int Rate = 44100;

        // Four short beeps and a pause; loops cleanly.
        public static AudioClip AlarmBeeps(float frequency = 2100f)
        {
            const float beep = .09f, gap = .06f, pause = .45f;
            int total = Mathf.CeilToInt((4 * (beep + gap) + pause) * Rate);
            var data = new float[total];
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / Rate;
                float slot = t % (beep + gap);
                if (t >= 4 * (beep + gap) || slot >= beep)
                    continue;
                float envelope = Mathf.Clamp01(slot / .004f) * Mathf.Clamp01((beep - slot) / .004f);
                float phase = 2 * Mathf.PI * frequency * t;
                // A bit of third harmonic so it sounds like a cheap buzzer.
                data[i] = (Mathf.Sin(phase) + .3f * Mathf.Sin(3 * phase)) * .3f * envelope;
            }

            return Make("Alarma (sintetizada)", data);
        }

        // White noise with soft edges.
        public static AudioClip Static(float seconds = 1.5f, float volume = .35f)
        {
            int total = Mathf.CeilToInt(seconds * Rate);
            var data = new float[total];
            var random = new System.Random(313);
            for (int i = 0; i < total; i++)
            {
                float edge = Mathf.Clamp01(i / (.03f * Rate)) * Mathf.Clamp01((total - i) / (.08f * Rate));
                data[i] = ((float)random.NextDouble() * 2 - 1) * volume * edge;
            }

            return Make("Estática (sintetizada)", data);
        }

        // Fluorescent tube hum: 100 Hz and its harmonics with a little hiss. 2 s that loop cleanly.
        public static AudioClip Hum(float seconds = 2f)
        {
            int total = Mathf.CeilToInt(seconds * Rate);
            var data = new float[total];
            var random = new System.Random(13);
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / Rate, w = 2 * Mathf.PI * t;
                float v = .5f * Mathf.Sin(w * 100) + .25f * Mathf.Sin(w * 200) + .12f * Mathf.Sin(w * 300) + .05f * Mathf.Sin(w * 1200);
                data[i] = (v + ((float)random.NextDouble() * 2 - 1) * .03f) * .3f;
            }

            return Make("Zumbido (sintetizado)", data);
        }

        // One tick of a wall clock: a short dry click.
        public static AudioClip Tick()
        {
            int total = Mathf.CeilToInt(.03f * Rate);
            var data = new float[total];
            var random = new System.Random(3);
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / Rate;
                float click = .6f * Mathf.Sin(2 * Mathf.PI * 3200 * t) + .4f * ((float)random.NextDouble() * 2 - 1);
                data[i] = click * Mathf.Exp(-t * 300) * .5f;
            }

            return Make("Tic-tac (sintetizado)", data);
        }

        // The alarm panel's "wrong code" buzz.
        public static AudioClip Buzz(float seconds = .3f)
        {
            int total = Mathf.CeilToInt(seconds * Rate);
            var data = new float[total];
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / Rate;
                float edge = Mathf.Clamp01(t / .01f) * Mathf.Clamp01((seconds - t) / .03f);
                // A softened square wave: harsh enough to read as "error".
                data[i] = Mathf.Clamp(Mathf.Sin(2 * Mathf.PI * 160 * t) * 3f, -1f, 1f) * .16f * edge;
            }

            return Make("Error (sintetizado)", data);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
