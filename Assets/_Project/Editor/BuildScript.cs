using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unbounded.Editor
{
    /// <summary>
    /// Builds the desktop players a reviewer runs. The menu items match the
    /// batch entry points, so a build made by hand and a build made by CI use
    /// the same scenes, target settings, and output paths.
    /// Run both players from the command line:
    /// Unity -batchmode -quit -projectPath . -executeMethod Unbounded.Editor.BuildScript.BuildAll
    /// </summary>
    public static class BuildScript
    {
        /// <summary>Where the macOS player lands, relative to the project root.</summary>
        public const string MacOutput = "Builds/macOS/Unbounded.app";

        /// <summary>Where the Windows player lands, relative to the project root.</summary>
        public const string WindowsOutput = "Builds/Windows/Unbounded.exe";

        [MenuItem("Unbounded/Build macOS Player")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, MacOutput);
        }

        [MenuItem("Unbounded/Build Windows Player")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, WindowsOutput);
        }

        /// <summary>Both desktop players, for the definition-of-done build.</summary>
        public static void BuildAll()
        {
            BuildMac();
            BuildWindows();
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "There are no enabled scenes in Build Settings. Open Unbounded/Build Main Scene first.");
            }

            foreach (string scene in scenes)
            {
                if (!File.Exists(scene))
                {
                    throw new FileNotFoundException("A scene in Build Settings is missing.", scene);
                }
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    target + " build failed with " + summary.result + ": " +
                    summary.totalErrors + " errors.");
            }

            Debug.Log(
                target + " build written to " + outputPath +
                " (" + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB, " +
                summary.totalTime.TotalSeconds.ToString("0") + "s).");
        }
    }
}
