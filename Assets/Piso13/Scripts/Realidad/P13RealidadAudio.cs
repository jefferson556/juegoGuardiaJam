using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13.Realidad
{
    public sealed class P13RealidadAudio : MonoBehaviour
    {
        P13RealidadSettings settings;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource menu, ui, breath, step, hum, dialogue, rainAmbient;
        AudioSource[] rains;
        readonly List<AudioSource> spatial = new List<AudioSource>();
        readonly Dictionary<string, AudioSource> doors = new Dictionary<string, AudioSource>();
        int spatialIndex;
        float lastHover, lastClick;
        bool gameplay, menuWanted;
        float menuVolume, rainVolume, rainTarget, stormClock;
        int lastStep = -1;
        float thunderTimer = 25f;

        public void Setup(P13RealidadSettings s)
        {
            settings = s;
            menu = Source("Música", false);
            menu.clip = Clip("P13_Menu_Ulrich");
            menu.loop = true;
            menu.volume = 0;
            menu.Play();

            ui = Source("UI", false);
            breath = Source("Respiración", false);
            breath.clip = Clip("P13_Player_Recovery");
            step = Source("Pasos", false);

            hum = Source("Ventilación", false);
            hum.clip = Tone("hum", 2, 50, .13f);
            hum.loop = true;

            rainAmbient = Source("LluviaAmbiente", false);
            rainAmbient.clip = Clip("P13_RainLoop") ?? Clip("P13_Rain_0");
            rainAmbient.loop = true;
            rainAmbient.volume = 0;

            dialogue = Source("Diálogo", false);

            rains = new AudioSource[4];
            for (int i = 0; i < 4; i++)
            {
                rains[i] = Source("Lluvia" + i, false);
                rains[i].clip = Clip("P13_Rain_" + i);
                rains[i].volume = 0;
            }

            for (int i = 0; i < 10; i++)
                spatial.Add(Source("Espacial" + i, true));
        }

        AudioSource Source(string name, bool space)
        {
            var o = new GameObject("Audio_" + name);
            o.transform.SetParent(transform);
            var a = o.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = space ? 1 : 0;
            if (space)
            {
                a.rolloffMode = AudioRolloffMode.Logarithmic;
                a.minDistance = 1.2f;
                a.maxDistance = 22f;
            }
            return a;
        }

        public AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            AudioClip c;
            if (clips.TryGetValue(name, out c) && c != null)
                return c;

            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null)
            {
                // Fallback a síntesis procedural para efectos sin archivo .wav físico
                c = Tone(name, 0.45f, name.Contains("knock") ? 78 : 95, 0.45f);
            }
            clips[name] = c;
            return c;
        }

        public void Menu(bool active)
        {
            menuWanted = active;
            if (active && menu != null && !menu.isPlaying)
                menu.Play();
        }

        public void Hover()
        {
            if (Time.unscaledTime - lastHover < .09f || Time.unscaledTime - lastClick < .12f)
                return;
            lastHover = Time.unscaledTime;
            if (ui != null)
                ui.PlayOneShot(Clip("P13_UI_Hover"), settings != null ? settings.master * settings.effects * .45f : 0.45f);
        }

        public void Confirm()
        {
            if (Time.unscaledTime - lastClick < .09f)
                return;
            lastClick = Time.unscaledTime;
            if (ui != null)
            {
                ui.Stop();
                ui.PlayOneShot(Clip("P13_UI_Confirm"), settings != null ? settings.master * settings.effects * .55f : 0.55f);
            }
        }

        public void SetGameplay(bool active)
        {
            gameplay = active;
            if (active)
            {
                if (hum != null && !hum.isPlaying)
                    hum.Play();
                if (rainAmbient != null && !rainAmbient.isPlaying)
                    rainAmbient.Play();
                if (dialogue != null)
                    dialogue.UnPause();
            }
            else
            {
                if (hum != null) hum.Pause();
                if (rainAmbient != null) rainAmbient.Pause();
                if (rains != null)
                {
                    foreach (var r in rains)
                        if (r != null) r.Pause();
                }
                if (step != null) step.Stop();
                if (breath != null) breath.Stop();
                if (doors != null)
                {
                    foreach (var d in doors.Values)
                        if (d != null) d.Stop();
                }
                if (spatial != null)
                {
                    foreach (var s in spatial)
                        if (s != null) s.Stop();
                }
                if (dialogue != null) dialogue.Pause();
            }
        }

        public void Player(float dt, float travelled, bool running, bool crouch, bool active)
        {
            if (!active)
            {
                if (step != null) step.Stop();
                return;
            }

            if (travelled > dt * 0.12f)
            {
                if (step != null && !step.isPlaying)
                {
                    int i = UnityEngine.Random.Range(0, 6);
                    if (i == lastStep) i = (i + 1) % 6;
                    lastStep = i;
                    step.clip = Clip("P13_PlayerStep_" + i);
                    step.volume = (settings != null ? settings.master * settings.effects : 1f) * (crouch ? .09f : running ? .5f : .32f);
                    step.Play();
                }
            }
            else
            {
                if (step != null) step.Stop();
            }
        }

        public void Spatial(string file, Vector3 pos, float gain = 1, float pitch = 1)
        {
            if (spatial.Count == 0) return;
            var a = spatial[spatialIndex];
            spatialIndex = (spatialIndex + 1) % spatial.Count;
            a.transform.position = pos;
            var c = Clip(file);
            if (!c) return;
            a.clip = c;
            a.volume = (settings != null ? settings.master * settings.effects : 1f) * gain;
            a.pitch = pitch;
            a.Play();
        }

        public void Door(string id, Vector3 p, float fraction, bool closing, bool moving)
        {
            AudioSource s;
            if (!doors.TryGetValue(id, out s))
            {
                s = Source("Puerta_" + id, true);
                doors[id] = s;
            }

            s.transform.position = p;
            if (fraction == (closing ? 0 : 1))
            {
                s.Stop();
                if (closing)
                    Spatial("P13_Door_Latch", p, .45f);
                return;
            }

            var c = Clip(closing ? "P13_Door_Closing" : "P13_Door_Open");
            if (!c)
                return;
            if (s.clip != c)
            {
                s.Stop();
                s.clip = c;
            }

            if (!moving)
            {
                s.Pause();
                return;
            }

            float at = (closing ? 1 - fraction : fraction) * c.length;
            if (!s.isPlaying || Mathf.Abs(s.time - at) > .12f)
                s.time = Mathf.Min(at, c.length - .001f);
            s.volume = (settings != null ? settings.master * settings.effects : 1f) * .45f;
            if (!s.isPlaying)
                s.Play();
        }

        public void StopDialogue()
        {
            if (dialogue != null) dialogue.Stop();
        }

        AudioClip Tone(string name, float seconds, float hz, float gain)
        {
            AudioClip c;
            if (clips.TryGetValue("generated_" + name, out c) && c != null)
                return c;

            switch (name)
            {
                case "hum":
                    seconds = 2f;
                    break;
                case "guard_step":
                    seconds = 0.25f;
                    break;
                case "knock":
                    seconds = 0.24f;
                    break;
                case "impact":
                    seconds = 0.55f;
                    break;
                case "phone":
                    seconds = 1.8f;
                    break;
                case "lift_ding":
                    seconds = 1.05f;
                    break;
                case "electric":
                    seconds = 0.55f;
                    break;
                case "flashlight":
                    seconds = 0.08f;
                    break;
                case "lift_motor":
                    seconds = 2.0f;
                    break;
            }

            int rate = 44100;
            int n = Mathf.CeilToInt(rate * seconds);
            float[] data = new float[n];
            var rng = new System.Random(13);

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                Func<float, float> sin = f => Mathf.Sin(t * 2f * Mathf.PI * f);

                switch (name)
                {
                    case "hum":
                        data[i] = sin(50) * 0.22f + sin(100) * 0.055f + sin(31) * 0.07f + noise * 0.014f;
                        break;
                    case "guard_step":
                        data[i] = (sin(48) * 0.68f + sin(96) * 0.18f + noise * 0.08f) * Mathf.Min(1f, t * 150f) * Mathf.Exp(-t * 18f);
                        break;
                    case "knock":
                        data[i] = (sin(78) * 0.9f + sin(156) * 0.34f + sin(310) * 0.12f + noise * 0.08f) * Mathf.Exp(-t * 20f);
                        break;
                    case "impact":
                        data[i] = (sin(48) * 0.85f + sin(92) * 0.55f + noise * Mathf.Exp(-t * 30f) * 0.25f) * Mathf.Exp(-t * 10f);
                        break;
                    case "phone":
                        data[i] = (sin(440) + sin(480)) * (t % 0.8f < 0.35f ? 1f : 0f) * 0.12f;
                        break;
                    case "lift_ding":
                        data[i] = (sin(880) * 0.18f + sin(1320) * 0.08f) * Mathf.Exp(-t * 3.1f);
                        break;
                    case "electric":
                        data[i] = (noise * 0.18f * Mathf.Abs(Mathf.Sin(t * 94f)) + sin(60) * 0.12f) * Mathf.Exp(-t * 3.7f);
                        break;
                    case "flashlight":
                        data[i] = noise * 0.2f * Mathf.Exp(-t * 90f);
                        break;
                    case "lift_motor":
                        data[i] = (sin(48) * 0.12f + noise * 0.06f) * Mathf.Min(1f, t * 10f) * Mathf.Exp(-t * 2f);
                        break;
                    default:
                        data[i] = (sin(hz) + 0.2f * sin(hz * 2f)) * gain * Mathf.Min(1f, t * 100f) * Mathf.Exp(-t * 12f);
                        break;
                }
            }

            c = AudioClip.Create("generated_" + name, n, 1, rate, false);
            c.SetData(data, 0);
            clips["generated_" + name] = c;
            return c;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float master = settings != null ? settings.master : 1f;
            float music = settings != null ? settings.music : 1f;
            float effects = settings != null ? settings.effects : 1f;
            float ambience = settings != null ? settings.ambience : 1f;

            menuVolume = Mathf.MoveTowards(menuVolume, menuWanted ? master * music * .45f : 0, dt * .6f);
            if (menu != null)
            {
                menu.volume = menuVolume;
                if (!menuWanted && menuVolume <= .001f && menu.isPlaying)
                    menu.Pause();
            }

            if (!gameplay) return;

            if (hum != null)
                hum.volume = master * ambience * .18f;

            if (rainAmbient != null)
                rainAmbient.volume = master * ambience * .35f;

            // Truenos atmosféricos lejanos periódicos
            thunderTimer -= dt;
            if (thunderTimer <= 0f)
            {
                thunderTimer = UnityEngine.Random.Range(45f, 75f);
                int thunderIndex = UnityEngine.Random.Range(0, 3);
                Spatial("P13_Thunder_" + thunderIndex, new Vector3(UnityEngine.Random.Range(-10f, 10f), 6f, UnityEngine.Random.Range(-10f, 10f)), 0.65f);
            }
        }
    }
}
