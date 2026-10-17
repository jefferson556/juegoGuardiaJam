using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13.Realidad
{
    public sealed class P13RealidadAudio : MonoBehaviour
    {
        P13RealidadSettings settings;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource menu, ui, breath, step, hum, dialogue;
        AudioSource[] rains;
        readonly List<AudioSource> spatial = new List<AudioSource>();
        readonly Dictionary<string, AudioSource> doors = new Dictionary<string, AudioSource>();
        int spatialIndex;
        float lastHover, lastClick;
        bool gameplay, menuWanted;
        float menuVolume, rainVolume, rainTarget, stormClock;
        int lastStep = -1;

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
            var o = new GameObject("Audio_" + name);
            o.transform.SetParent(transform);
            var a = o.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = space ? 1 : 0;
            if (space)
            {
                a.rolloffMode = AudioRolloffMode.Custom;
                a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.EaseInOut(1, 1, 14, 0));
                a.minDistance = 1;
                a.maxDistance = 14;
            }
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
            a.clip = Clip(file);
            if (!a.clip) return;
            a.volume = (settings != null ? settings.master * settings.effects : 1f) * gain;
            a.pitch = pitch;
            a.Play();
        }

        public void StopDialogue()
        {
            if (dialogue != null) dialogue.Stop();
        }

        AudioClip Tone(string name, float seconds, float hz, float gain)
        {
            AudioClip c;
            if (clips.TryGetValue("generated_" + name, out c))
                return c;
            int rate = 44100;
            int samples = Mathf.CeilToInt(rate * seconds);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                data[i] = Mathf.Sin(2 * Mathf.PI * hz * t) * gain;
            }
            c = AudioClip.Create("generated_" + name, samples, 1, rate, false);
            c.SetData(data, 0);
            clips["generated_" + name] = c;
            return c;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (settings == null) return;

            menuVolume = Mathf.MoveTowards(menuVolume, menuWanted ? settings.master * settings.music * .45f : 0, dt * .6f);
            if (menu != null)
            {
                menu.volume = menuVolume;
                if (!menuWanted && menuVolume <= .001f && menu.isPlaying)
                    menu.Pause();
            }

            if (!gameplay) return;

            if (hum != null)
                hum.volume = settings.master * settings.effects * .18f;
        }
    }
}
