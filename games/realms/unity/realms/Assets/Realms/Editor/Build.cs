using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Realms.Editor
{
    /// <summary>The players: Setup, then a build to Builds/Linux or Builds/Windows in the Unity project.</summary>
    public static class Build
    {
        public const string LinuxPlayer = "Builds/Linux/Realms.x86_64";
        public const string WindowsPlayer = "Builds/Windows/Realms.exe";

        // Entry point for `-executeMethod Realms.Editor.Build.Linux` (tools/build.sh) and the menu.
        [MenuItem("Realms/Build Linux Player")]
        public static void Linux() => Player(LinuxPlayer, BuildTarget.StandaloneLinux64);

        // Entry point for `-executeMethod Realms.Editor.Build.Windows` (tools/build.sh windows) and the menu:
        // the Windows player, built on Linux with Mono (the Windows support module is installed).
        [MenuItem("Realms/Build Windows Player")]
        public static void Windows() => Player(WindowsPlayer, BuildTarget.StandaloneWindows64);

        static void Player(string path, BuildTarget target)
        {
            try
            {
                Setup.Run();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Setup.ScenePath },
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None,
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Realms: build {summary.result} with {summary.totalErrors} errors.");
                Debug.Log($"Realms: built {Path.GetFullPath(path)}, {summary.totalSize / (1024 * 1024)} MB, " +
                          $"{summary.totalTime.TotalSeconds:F0} s.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw;
            }
        }
    }
}
