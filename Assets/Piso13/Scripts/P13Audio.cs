using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13
{
    public struct PlayerAudioPolicy
    {
        public float distance, runSeconds, recovery;
        bool wasRunning;
        public bool Step(float dt, float travelled, bool running, bool crouched, bool active, out float breath)
        {
            if (!active)
            {
                this = default(PlayerAudioPolicy);
                breath = 0;
                return false;
            }

            dt = Mathf.Clamp(dt, 0, .1f);
            bool moved = dt > 0 && travelled > dt * .12f && travelled <= 4.2f * dt * 1.5f, sprinting = running && moved;
            if (sprinting)
            {
                runSeconds = Mathf.Min(12, runSeconds + dt);
                recovery = 0;
            }
            else if (wasRunning)
            {
                if (runSeconds >= 1.5f)
                    recovery = Mathf.Min(9, 2 + runSeconds * .7f);
                runSeconds = 0;
            }

            wasRunning = sprinting;
            recovery = Mathf.Max(0, recovery - dt);
            breath = sprinting ? 0 : Mathf.Min(1, recovery / 1.5f);
            if (!moved)
            {
                distance = 0;
                return false;
            }

            distance += travelled;
            float stride = crouched ? .65f : running ? 1.35f : .95f;
            if (distance >= stride)
            {
                distance %= stride;
                return true;
            }

            return false;
        }
    }

    public sealed class P13Audio : MonoBehaviour
    {
        P13Settings settings;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource menu, ui, breath, step, hum, dialogue;
        AudioSource[] rains;
        readonly List<int> bag = new List<int>();
        int current = -1, incoming = -1, previous = -1, lastStep = -1;
        float fade, menuGain, breathTarget, breathGain, rainGain, rainTarget, lastHover = -9, lastClick = -9;
        bool menuWanted = true, gameplay;
        PlayerAudioPolicy locomotion;
        readonly Dictionary<string, AudioSource> doors = new Dictionary<string, AudioSource>();
        readonly List<AudioSource> spatial = new List<AudioSource>();
        int spatialIndex;
        float tension = 1;
        readonly float[] trims = {1, 1.516f, .654f, 1.518f};
        public void Setup(P13Settings s)
        {
            settings = s;
            menu = Source("Menú", false);
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
            dialogue = Source("Diálogo", false);
            rains = new AudioSource[4];
            for (int i = 0; i < 4; i++)
            {
                rains[i] = Source("Lluvia" + i, false);
                rains[i].clip = Clip("P13_Rain_" + i);
                rains[i].volume = 0;
            }

            for (int i = 0; i < 8; i++)
                spatial.Add(Source("Espacial" + i, true));
        }

        AudioSource Source(string name, bool space)
        {
            var o = new GameObject(name);
            o.transform.SetParent(transform);
            var a = o.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = space ? 1 : 0;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 1;
            a.maxDistance = 20;
            return a;
        }

        public AudioClip Clip(string name)
        {
            AudioClip c;
            if (clips.TryGetValue(name, out c))
                return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
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
                if (dialogue != null)
                    dialogue.UnPause();
            }
            else
            {
                if (hum != null) hum.Pause();
                if (rains != null)
                {
                    foreach (var r in rains)
                        if (r != null) r.Pause();
                }
                if (step != null) step.Stop();
                if (breath != null) breath.Stop();
                breathGain = breathTarget = 0;
                locomotion = default(PlayerAudioPolicy);
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
            float target;
            if (locomotion.Step(dt, travelled, running, crouch, active, out target))
            {
                int i = UnityEngine.Random.Range(0, 6);
                if (i == lastStep)
                    i = (i + 1) % 6;
                lastStep = i;
                step.clip = Clip("P13_PlayerStep_" + i);
                step.volume = settings.master * settings.effects * (crouch ? .09f : running ? .5f : .32f);
                step.Play();
            }

            if (!active || travelled <= dt * .12f)
                step.Stop();
            breathTarget = target;
            if (target > 0 && !breath.isPlaying)
            {
                breath.volume = 0;
                breath.Play();
            }
        }

        int NextRain()
        {
            if (bag.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                    bag.Add(i);
                for (int i = 3; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    int t = bag[i];
                    bag[i] = bag[j];
                    bag[j] = t;
                }

                if (bag[3] == previous)
                {
                    int t = bag[0];
                    bag[0] = bag[3];
                    bag[3] = t;
                }
            }

            int next = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            previous = next;
            return next;
        }

        public void Rain(float gain, float duck)
        {
            rainTarget = gain;
            tension = duck;
            if (current < 0)
                current = NextRain();
            if (!rains[current].isPlaying && incoming < 0)
            {
                rains[current].UnPause();
                if (!rains[current].isPlaying)
                    rains[current].Play();
            }

            if (incoming >= 0)
                rains[incoming].UnPause();
        }

        void Update()
        {
            if (settings == null)
                return;
            float dt = Time.unscaledDeltaTime;
            menuGain = Mathf.MoveTowards(menuGain, menuWanted ? 1 : 0, dt / .8f);
            menu.volume = menuGain * settings.master * settings.music * .38f;
            if (!menuWanted && menuGain == 0)
                menu.Pause();
            if (!gameplay)
                return;
            float mix = settings.master * settings.effects;
            hum.volume = .11f * settings.master * settings.ambience * tension;
            breathGain = Mathf.Lerp(breathGain, breathTarget, dt / (breathTarget > breathGain ? .45f : .65f));
            breath.volume = breathGain * mix * .48f;
            if (breathTarget == 0 && breathGain < .003f)
                breath.Stop();
            dialogue.volume = settings.master * settings.voices;
            rainGain = Mathf.Lerp(rainGain, rainTarget, dt * 2);
            if (current < 0)
                return;
            var a = rains[current];
            if (incoming < 0 && a.clip != null && a.clip.length - a.time <= 4)
            {
                incoming = NextRain();
                rains[incoming].volume = 0;
                rains[incoming].time = 0;
                rains[incoming].Play();
                fade = 0;
            }

            float volume = rainGain * settings.master * settings.ambience * tension;
            if (incoming >= 0)
            {
                fade = Mathf.Clamp01(rains[incoming].time / 4);
                a.volume = volume * trims[current] * Mathf.Cos(fade * Mathf.PI / 2);
                rains[incoming].volume = volume * trims[incoming] * Mathf.Sin(fade * Mathf.PI / 2);
                if (fade >= 1)
                {
                    a.Stop();
                    current = incoming;
                    incoming = -1;
                }
            }
            else
                a.volume = volume * trims[current];
        }

        public void Spatial(string name, Vector3 p, float gain = .5f)
        {
            AudioClip c = Clip(name);
            if (!c)
                c = Tone(name, .45f, name.Contains("knock") ? 78 : 95, .45f);
            var s = spatial[spatialIndex++ % spatial.Count];
            s.Stop();
            s.transform.position = p;
            s.clip = c;
            s.volume = settings.master * settings.effects * gain;
            s.Play();
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
            s.volume = settings.master * settings.effects * .45f;
            if (!s.isPlaying)
                s.Play();
        }

        public float Dialogue(string file)
        {
            var c = Clip(file);
            if (!c)
                return 0;
            dialogue.clip = c;
            dialogue.volume = settings.master * settings.voices;
            dialogue.Play();
            return c.length;
        }

        public void StopDialogue()
        {
            if (dialogue != null) dialogue.Stop();
        }

        // These generators are ports of DoorAudioSystem.swift, not substitute voice recordings.
        AudioClip Tone(string name, float seconds, float hz, float gain)
        {
            AudioClip c;
            if (clips.TryGetValue("generated_" + name, out c))
                return c;
            switch (name)
            {
                case "hum":
                    seconds = 2;
                    break;
                case "guard_step":
                    seconds = .25f;
                    break;
                case "knock":
                    seconds = .24f;
                    break;
                case "impact":
                    seconds = .55f;
                    break;
                case "phone":
                    seconds = 1.8f;
                    break;
                case "lift_ding":
                    seconds = 1.05f;
                    break;
                case "electric":
                    seconds = .55f;
                    break;
                case "flashlight":
                    seconds = .08f;
                    break;
            }

            int n = (int)(44100 * seconds);
            float[] data = new float[n];
            var rng = new System.Random(13);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / 44100, noise = (float)rng.NextDouble() * 2 - 1;
                Func<float, float> sin = f => Mathf.Sin(t * 2 * Mathf.PI * f);
                switch (name)
                {
                    case "hum":
                        data[i] = sin(50) * .22f + sin(100) * .055f + sin(31) * .07f + noise * .014f;
                        break;
                    case "guard_step":
                        data[i] = (sin(48) * .68f + sin(96) * .18f + noise * .08f) * Mathf.Min(1, t * 150) * Mathf.Exp(-t * 18);
                        break;
                    case "knock":
                        data[i] = (sin(78) * .9f + sin(156) * .34f + sin(310) * .12f + noise * .08f) * Mathf.Exp(-t * 20);
                        break;
                    case "impact":
                        data[i] = (sin(48) * .85f + sin(92) * .55f + noise * Mathf.Exp(-t * 30) * .25f) * Mathf.Exp(-t * 10);
                        break;
                    case "phone":
                        data[i] = (sin(440) + sin(480)) * (t % .8f < .35f ? 1 : 0) * .12f;
                        break;
                    case "lift_ding":
                        data[i] = (sin(880) * .18f + sin(1320) * .08f) * Mathf.Exp(-t * 3.1f);
                        break;
                    case "electric":
                        data[i] = (noise * .18f * Mathf.Abs(Mathf.Sin(t * 94)) + sin(60) * .12f) * Mathf.Exp(-t * 3.7f);
                        break;
                    case "flashlight":
                        data[i] = noise * .2f * Mathf.Exp(-t * 90);
                        break;
                    case "lift_motor":
                        data[i] = (sin(48) * .12f + noise * .06f) * Mathf.Min(1, t * 10) * Mathf.Exp(-t * 2);
                        break;
                    default:
                        data[i] = (sin(hz) + .2f * sin(hz * 2)) * gain * Mathf.Min(1, t * 100) * Mathf.Exp(-t * 12);
                        break;
                }
            }

            c = AudioClip.Create(name, n, 1, 44100, false);
            c.SetData(data, 0);
            clips["generated_" + name] = c;
            return c;
        }
    }
}
