using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Turno0313.EditorTools
{
    // Windows build of Turno 03:13: every enabled scene in Build Settings except the Piso 13 prototype.
    public static class TurnoBuild
    {
        public const string Output = "Builds/Turno0313/Windows/Turno0313.exe";

        [MenuItem("Turno 03:13/Compilar Windows")]
        public static void BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && !s.path.StartsWith("Assets/Piso13/")).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("No hay escenas de Turno 03:13 activas en Build Settings.");
            // Piso 13's editor script resets the product name on every editor start, so set it just for this build.
            string product = PlayerSettings.productName;
            PlayerSettings.productName = "Turno 03:13";
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                string summary = report.summary.result + ", " + report.summary.totalErrors + " errores, " + (report.summary.totalSize / (1024 * 1024)) + " MB";
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Build de Turno 03:13 falló: " + summary);
                Debug.Log("T13_BUILD_OK " + Output + " (" + summary + ")");
            }
            finally
            {
                PlayerSettings.productName = product;
            }
        }
    }
}
