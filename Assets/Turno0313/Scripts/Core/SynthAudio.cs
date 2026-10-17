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

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
