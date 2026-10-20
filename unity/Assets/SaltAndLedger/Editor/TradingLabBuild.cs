using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Editor
{
    public static class TradingLabBuild
    {
        [MenuItem("Salt & Ledger/Build Windows Mono Trading Lab")]
        public static void BuildWindowsMono()
        {
            var playerPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "..", "Builds", "TradingLab", "Windows", "SaltAndLedger.TradingLab.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(playerPath));
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/TradingLab.unity" },
                    locationPathName = playerPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException("Trading Lab player build failed: " + report.summary.result);
                }

                Debug.Log("Salt & Ledger Trading Lab built: " + playerPath);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
            }
        }
    }
}
