using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Piso13.Editor
{
    [InitializeOnLoad]
    public static class DemoMenuBuild
    {
        static DemoMenuBuild() { EditorApplication.update += CheckRequest; }
        static void CheckRequest()
        {
            const string request = "Temp/BuildDemoMenuMac";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            string destination = Path.GetFullPath("../Piso13-Demo-Mac.app");
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = destination,
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.None
                });
                File.WriteAllText("Documentation/Validation/MacBuild.txt", DateTime.UtcNow.ToString("O") + "\nUnity " + Application.unityVersion + "\n" + report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize + "\n" + destination + "\n");
            }
            catch (Exception error) { File.WriteAllText("Documentation/Validation/MacBuild.txt", "FAILED\n" + error); }
        }
    }
}
