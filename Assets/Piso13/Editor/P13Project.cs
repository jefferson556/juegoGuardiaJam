using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Piso13.Editor
{
    public sealed class P13Importer : AssetPostprocessor
    {
        bool Ours => assetPath.StartsWith("Assets/Piso13/Resources/");
        void OnPreprocessModel()
        {
            if (!Ours)
                return;
            var importer = (ModelImporter)assetImporter;
            bool character = assetPath.Contains("/MonsterGuard/") || assetPath.Contains("/Elena/");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.importAnimation = character;
            importer.animationType = character ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPreprocessAnimation()
        {
            if (!Ours)
                return;
            var importer = (ModelImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            string manifest = "Assets/Piso13/Resources/Data/" + name + "_manifest.json";
            if (!File.Exists(manifest))
                return;
            var definitions = JsonUtility.FromJson<ClipManifest>(File.ReadAllText(manifest));
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string action = clip.takeName.Split('|').Last();
                var definition = definitions.clips.FirstOrDefault(c => c.name == action);
                clip.name = action;
                clip.loopTime = definition != null && definition.loop;
                clip.wrapMode = clip.loopTime ? WrapMode.Loop : WrapMode.ClampForever;
            }

            importer.clipAnimations = clips;
        }

        void OnPreprocessAudio()
        {
            if (!Ours)
                return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            bool longClip = assetPath.Contains("Rain") || assetPath.Contains("Menu");
            settings.loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .85f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
        }

        void OnPreprocessTexture()
        {
            if (!Ours)
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureShape = TextureImporterShape.Texture2D;
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (fileName.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0)
                importer.textureType = TextureImporterType.NormalMap;
            if (assetPath.IndexOf("metallic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf("roughness", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf("smoothness", StringComparison.OrdinalIgnoreCase) >= 0)
                importer.sRGBTexture = false;
            importer.maxTextureSize = 2048;
            if (assetPath.Contains("/UI/"))
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
            }
        }

        [Serializable]
        public class MaterialLibrary
        {
            public MaterialAsset[] assets;
        }

        [Serializable]
        public class MaterialAsset
        {
            public string name;
            public MaterialDefinition[] materials;
        }

        [Serializable]
        public class MaterialDefinition
        {
            public string name, albedo, normal, packed;
            public float[] color;
            public float metallic, roughness;
        }

        Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!Ours)
                return null;
            var library = JsonUtility.FromJson<MaterialLibrary>(File.ReadAllText("Assets/Piso13/Resources/Models/MaterialLibrary.json"));
            string name = Path.GetFileNameWithoutExtension(assetPath);
            var asset = library.assets.FirstOrDefault(a => a.name == name);
            var definition = asset?.materials.FirstOrDefault(m => m.name == material.name);
            if (definition == null)
                return null;
            string folder = Path.GetDirectoryName(assetPath).Replace('\\', '/') + "/Textures/";
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var result = new Material(shader)
            {name = material.name};
            var c = definition.color;
            var col = new Color(c[0], c[1], c[2], c[3]);
            result.color = col;
            if (result.HasProperty("_BaseColor"))
                result.SetColor("_BaseColor", col);
            if (!string.IsNullOrEmpty(definition.albedo))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + definition.albedo);
                if (texture)
                {
                    result.mainTexture = texture;
                    if (result.HasProperty("_BaseMap"))
                        result.SetTexture("_BaseMap", texture);
                    result.color = Color.white;
                    if (result.HasProperty("_BaseColor"))
                        result.SetColor("_BaseColor", Color.white);
                }
            }

            if (!string.IsNullOrEmpty(definition.normal))
            {
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + definition.normal);
                if (normal)
                {
                    result.SetTexture("_BumpMap", normal);
                    result.EnableKeyword("_NORMALMAP");
                }
            }

            result.SetFloat("_Metallic", definition.metallic);
            if (result.HasProperty("_Smoothness"))
                result.SetFloat("_Smoothness", 1 - definition.roughness);
            if (result.HasProperty("_Glossiness"))
                result.SetFloat("_Glossiness", 1 - definition.roughness);
            if (!string.IsNullOrEmpty(definition.packed))
            {
                var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + definition.packed);
                if (packed)
                {
                    result.SetTexture("_MetallicGlossMap", packed);
                    result.EnableKeyword("_METALLICGLOSSMAP");
                    result.SetFloat("_GlossMapScale", 1);
                }
            }

            return result;
        }
    }

    [InitializeOnLoad]
    public static class P13Project
    {
        public const string ScenePath = "Assets/Piso13/Scenes/Piso13.unity";
        static P13Project()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && !SessionState.GetBool("P13.Prepared.v1", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Prepare();
                    SessionState.SetBool("P13.Prepared.v1", true);
                    if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty)
                        EditorSceneManager.OpenScene(ScenePath);
                }
            }

            ;
        }

        [MenuItem("Piso 13/Preparar proyecto")]
        public static void Prepare()
        {
            PlayerSettings.companyName = "Piso13";
            PlayerSettings.productName = "Piso 13";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.piso13.unity");
            PlayerSettings.defaultScreenWidth = 1152;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Piso13/Resources/UI/Icon.png");
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Select(_ => icon).ToArray(), IconKind.Any);
            QualitySettings.pixelLightCount = 3;
            // Runtime creates Standard materials; retain this shader in standalone players.
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            Action<string> ensureShader = shaderName =>
            {
                var s = Shader.Find(shaderName);
                if (!s) return;
                for (int i = 0; i < shaders.arraySize; i++)
                    if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == s)
                        return;
                shaders.InsertArrayElementAtIndex(shaders.arraySize);
                shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = s;
                graphics.ApplyModifiedProperties();
            };
            ensureShader("Universal Render Pipeline/Lit");
            ensureShader("Standard");

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // P13Game.Bootstrap initializes the game once after scene load.
                new GameObject("Piso13Bootstrap").AddComponent<P13Game>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[]{new EditorBuildSettingsScene(ScenePath, true)};
            AssetDatabase.SaveAssets();
            Debug.Log("P13_PROJECT_PREPARED");
        }

        [MenuItem("Piso 13/Abrir escena")]
        public static void Open()
        {
            Prepare();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Piso 13/Validar recursos y lógica")]
        public static void Validate()
        {
            Prepare();
            P13Validation.Run();
        }

        [MenuItem("Piso 13/Reimportar texturas")]
        public static void ReimportTextures()
        {
            var guids = AssetDatabase.FindAssets("t:Texture", new[] { "Assets/Piso13" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("P13_TEXTURES_REIMPORTED: " + guids.Length + " texturas procesadas.");
        }

        [MenuItem("Piso 13/Compilar/Mac")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/Mac/Piso13.app");
        }

        [MenuItem("Piso 13/Compilar/Windows")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Piso13.exe");
        }

        public static void BuildAll()
        {
            BuildMac();
            BuildWindows();
        }

        static void Build(BuildTarget target, string output)
        {
            Prepare();
            P13Validation.Run();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException("Instala el módulo de compilación " + target + " desde Unity Hub.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes = new[]{ScenePath}, locationPathName = output, target = target, options = BuildOptions.None});
            string summary = target + ": " + report.summary.result + "; errores=" + report.summary.totalErrors;
            Directory.CreateDirectory("Documentation/Validation");
            File.WriteAllText("Documentation/Validation/Build-" + target + ".txt", summary);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException(summary);
            Debug.Log("P13_BUILD_SUCCEEDED " + summary);
        }
    }
}
