using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Editor
{
    public static class SmokePlayerBuild
    {
        [MenuItem("Salt & Ledger/Build Windows Mono Smoke Player")]
        public static void BuildWindowsMono()
        {
            var playerPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "..", "Builds", "Smoke", "Windows", "SaltAndLedger.Smoke.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(playerPath));
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/Bootstrap.unity" },
                    locationPathName = playerPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException("Windows Mono smoke player build failed: " + report.summary.result);
                }

                Debug.Log("Salt & Ledger smoke player built: " + playerPath);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
            }
        }
    }
}
