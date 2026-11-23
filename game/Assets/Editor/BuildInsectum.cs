using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Insectum.Game.Editor
{
    public static class BuildInsectum
    {
        public static void Mac()
        {
            bool development = Array.IndexOf(Environment.GetCommandLineArgs(), "-insectum-development") >= 0;
            string output = development ? "Builds/Insectum Dev.app" : "Builds/Insectum.app";
            Directory.CreateDirectory("Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"macOS build {report.summary.result} with {report.summary.totalErrors} errors.");

            Debug.Log($"Insectum macOS build succeeded. {Path.GetFullPath(output)}");
        }
    }
}
