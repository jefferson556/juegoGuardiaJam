using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Turno0313
{
    [Serializable]
    public sealed class DemoSettings
    {
        public const string SaveKey = "Piso13.Demo.Options.v1";
        public float master = .85f, music = .65f, effects = .8f, ambience = .65f, voices = .85f;
        public float sensitivity = 1, brightness;
        public int symbols, priority = 1, resolution;
        public bool fullscreen;
        public List<Override> bindings = new List<Override>();
        [Serializable] public class Override { public string action, path; public int index; }
        static DemoSettings current;
        public static DemoSettings Current => current ?? (current = Load());
        static DemoSettings Load()
        {
            try { var s = JsonUtility.FromJson<DemoSettings>(PlayerPrefs.GetString(SaveKey, "{}")) ?? new DemoSettings(); s.Validate(); return s; }
            catch { return new DemoSettings(); }
        }
        static float Clamp(float v, float lo, float hi, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, lo, hi);
        public void Validate()
        {
            master = Clamp(master, 0, 1, .85f); music = Clamp(music, 0, 1, .65f);
            effects = Clamp(effects, 0, 1, .8f); ambience = Clamp(ambience, 0, 1, .65f); voices = Clamp(voices, 0, 1, .85f);
            sensitivity = Clamp(sensitivity, .3f, 2, 1); brightness = Clamp(brightness, -.15f, .15f, 0);
            symbols = Mathf.Clamp(symbols, 0, 2); priority = Mathf.Clamp(priority, 0, 2); resolution = Mathf.Clamp(resolution, 0, 1);
            if (bindings == null) bindings = new List<Override>();
        }
        public void Save()
        {
            Validate(); PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this)); PlayerPrefs.Save();
            DemoSettingsRuntime.Apply();
        }
        public void StoreBindings()
        {
            bindings.Clear();
            foreach (var action in GameInput.Actions)
                for (int i = 0; i < action.bindings.Count; i++)
                    if (action.bindings[i].overridePath != null)
                        bindings.Add(new Override { action = action.name, index = i, path = action.bindings[i].overridePath });
            Save();
        }
        public void RestoreTab(int tab)
        {
            var defaults = new DemoSettings();
            if (tab == 0)
            {
                symbols = 0; sensitivity = 1;
                foreach (var action in GameInput.Actions) action.RemoveAllBindingOverrides();
                StoreBindings();
            }
            else if (tab == 1) { brightness = 0; priority = 1; resolution = 0; fullscreen = false; }
            else { master = defaults.master; music = defaults.music; effects = defaults.effects; ambience = defaults.ambience; voices = defaults.voices; }
            Save();
            if (tab == 1) ApplyDisplay();
        }
        public void ApplyDisplay()
        {
#if !UNITY_EDITOR
            Screen.SetResolution(resolution == 0 ? 1152 : 1440, resolution == 0 ? 720 : 900,
                fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }
    }

    // One mixer for every scene. Source volumes/fades stay owned by the existing audio scripts.
    public sealed class DemoSettingsRuntime : MonoBehaviour
    {
        static DemoSettingsRuntime instance;
        static AudioMixer mixer;
        static readonly Dictionary<string, AudioMixerGroup> groups = new Dictionary<string, AudioMixerGroup>();
        UniversalRenderPipelineAsset originalPipeline, runtimePipeline;
        Volume volume;
        ColorAdjustments color;
        AudioSource previewEffect, previewRain;
        float scanAt, previewUntil;
        public static AudioMixer Mixer => mixer ? mixer : (mixer = Resources.Load<AudioMixer>("DemoAudio"));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; mixer = null; groups.Clear(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (instance) return;
            var go = new GameObject("Demo settings"); DontDestroyOnLoad(go);
            instance = go.AddComponent<DemoSettingsRuntime>();
        }
        void Start()
        {
            originalPipeline = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (originalPipeline)
            {
                runtimePipeline = Instantiate(originalPipeline);
                QualitySettings.renderPipeline = runtimePipeline;
            }
            volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100;
            volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            color = volume.sharedProfile.Add<ColorAdjustments>(true);
            color.postExposure.overrideState = true;
            GameInput.Enable(); Apply();
            if (PlayerPrefs.HasKey(DemoSettings.SaveKey)) DemoSettings.Current.ApplyDisplay();
        }
        public static void Route(AudioSource source, string channel)
        {
            if (!source || !Mixer) return;
            if (!groups.TryGetValue(channel, out var group))
            {
                var found = Mixer.FindMatchingGroups(channel);
                if (found.Length == 0) { Debug.LogError("Missing demo audio channel: " + channel); return; }
                groups[channel] = group = found[0];
            }
            source.outputAudioMixerGroup = group;
        }
        public static void PlayEffect(AudioSource source, AudioClip clip, float gain = 1)
        {
            if (!source || !clip) return;
            Route(source, "Effects"); source.PlayOneShot(clip, gain);
        }
        public static float Decibels(float gain) => gain <= .0001f ? -80 : 20 * Mathf.Log10(gain);
        public static void Apply()
        {
            var s = DemoSettings.Current;
            if (Mixer)
            {
                Mixer.SetFloat("Master", Decibels(s.master)); Mixer.SetFloat("Music", Decibels(s.music));
                Mixer.SetFloat("Effects", Decibels(s.effects)); Mixer.SetFloat("Ambience", Decibels(s.ambience)); Mixer.SetFloat("Voices", Decibels(s.voices));
            }
            if (!instance) return;
            if (instance.color) instance.color.postExposure.value = s.brightness * 4;
            if (instance.runtimePipeline)
            {
                instance.runtimePipeline.renderScale = s.priority == 0 ? .75f : 1;
                instance.runtimePipeline.shadowDistance = s.priority == 0 ? 20 : s.priority == 1 ? 40 : 60;
                instance.runtimePipeline.mainLightShadowmapResolution = s.priority == 0 ? 1024 : s.priority == 1 ? 2048 : 4096;
            }
        }
        void LateUpdate()
        {
            if (Time.unscaledTime >= scanAt)
            {
                scanAt = Time.unscaledTime + .5f;
                // Serialized sources and effects spawned by gameplay use Effects unless explicitly routed.
                foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                    if (!source.outputAudioMixerGroup) Route(source, "Effects");
                foreach (var camera in Camera.allCameras)
                    if (camera.CompareTag("MainCamera")) camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
            if (previewUntil > 0 && Time.unscaledTime > previewUntil) StopPreview();
        }
        public static void Preview()
        {
            if (!instance) return;
            if (!instance.previewEffect)
            {
                instance.previewEffect = instance.gameObject.AddComponent<AudioSource>();
                instance.previewRain = instance.gameObject.AddComponent<AudioSource>();
                instance.previewEffect.playOnAwake = instance.previewRain.playOnAwake = false;
                instance.previewEffect.ignoreListenerPause = instance.previewRain.ignoreListenerPause = true;
                Route(instance.previewEffect, "Effects"); Route(instance.previewRain, "Ambience");
            }
            instance.previewEffect.clip = Resources.Load<AudioClip>("Audio/P13_Door_Open");
            instance.previewRain.clip = Resources.Load<AudioClip>("Audio/P13_RainLoop");
            instance.previewEffect.Play(); instance.previewRain.Play(); instance.previewUntil = Time.unscaledTime + 3;
        }
        public static void StopPreview()
        {
            if (!instance) return;
            if (instance.previewEffect) instance.previewEffect.Stop();
            if (instance.previewRain) instance.previewRain.Stop();
            instance.previewUntil = 0;
        }
        void OnDestroy()
        {
            if (runtimePipeline) { QualitySettings.renderPipeline = originalPipeline; Destroy(runtimePipeline); }
            if (volume && volume.sharedProfile) Destroy(volume.sharedProfile);
            if (instance == this) instance = null;
        }
    }
}
