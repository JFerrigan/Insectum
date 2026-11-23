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
            string[] args = Environment.GetCommandLineArgs();
            bool development = Array.IndexOf(args, "-insectum-development") >= 0;
            bool tunnelTrial = Array.IndexOf(args, "-insectum-tunnel-trial-build") >= 0;
            string title = tunnelTrial ? "Insectum Tunnel Trial" : "Insectum";
            string output = $"Builds/{title}{(development ? " Dev" : "")}.app";
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
