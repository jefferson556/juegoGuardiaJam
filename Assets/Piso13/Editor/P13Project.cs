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

        // Shader.Find misses package shaders during a fresh import (new clone, empty Library),
        // so the importer loads URP Lit by path and declares it as a dependency.
        const string LitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";

        // Bump this to force Unity to reimport the Piso13 assets with the current rules.
        // v1: materials switched from built-in Standard to URP Lit.
        // v2: URP Lit loaded by path instead of Shader.Find.
        // v3: skinned character bounds recomputed so characters spawn at full size.
        public override uint GetVersion() => 3;

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

        // The character FBX files (MonsterGuard, Elena) come in with SkinnedMeshRenderer bounds
        // 100x too big: the mesh node's x100 scale gets counted twice. P13Character sizes each
        // character from those bounds, so it shrank them to a couple of centimetres. Recompute
        // the bounds from the mesh at its bind pose, in the root bone's space.
        void OnPostprocessModel(GameObject root)
        {
            if (!Ours)
                return;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skin.sharedMesh;
                var bones = skin.bones;
                var space = skin.rootBone ? skin.rootBone : skin.transform;
                if (!mesh || bones.Length == 0 || !bones[0] || mesh.bindposes.Length != bones.Length)
                    continue;
                var meshToSpace = space.worldToLocalMatrix * bones[0].localToWorldMatrix * mesh.bindposes[0];
                skin.localBounds = Transform(mesh.bounds, meshToSpace);
            }
        }

        static Bounds Transform(Bounds local, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(local.min), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z)));
            return result;
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
            context.DependsOnArtifact(LitShaderPath);
            var lit = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
            if (!lit)
            {
                context.LogImportError("Falta el shader URP Lit en " + LitShaderPath);
                return null;
            }

            var result = new Material(lit)
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
            result.SetFloat("_Smoothness", 1 - definition.roughness);
            if (!string.IsNullOrEmpty(definition.packed))
            {
                var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + definition.packed);
                if (packed)
                {
                    // URP Lit reads metallic from R and smoothness from A, scaled by _Smoothness.
                    result.SetTexture("_MetallicGlossMap", packed);
                    result.EnableKeyword("_METALLICSPECGLOSSMAP");
                    result.SetFloat("_Smoothness", 1);
                }
            }

            // P13World turns emission on at runtime for lights and screens. URP strips shader
            // variants no material uses, so enable it here (black) or builds lose the glow.
            if (Emissive(material.name) || (renderer && Emissive(renderer.name)))
            {
                result.EnableKeyword("_EMISSION");
                result.SetColor("_EmissionColor", Color.black);
            }

            return result;
        }

        static bool Emissive(string name) => name.Contains("Fluorescent") || name.Contains("Screen") || name.Contains("Emergency");
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
            // URP Lit ships with the build because the imported models use it, so there is no
            // need to force a shader into "Always Included Shaders".

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
