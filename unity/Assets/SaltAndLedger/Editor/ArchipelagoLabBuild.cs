using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Editor
{
    public static class ArchipelagoLabBuild
    {
        [MenuItem("Salt & Ledger/Build Windows Mono Archipelago Lab")]
        public static void BuildWindowsMono()
        {
            var playerPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "..", "Builds", "ArchipelagoLab", "Windows", "SaltAndLedger.ArchipelagoLab.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(playerPath));
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/ArchipelagoLab.unity" },
                    locationPathName = playerPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Archipelago Lab build failed: " + report.summary.result);
                Debug.Log("Salt & Ledger Archipelago Lab built: " + playerPath);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
            }
        }
    }
}
