#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VRDemo.EditorTools
{
    public static class MacOSBuild
    {
        private const string DefaultAppName = "VirtualPartner";

        [MenuItem("Tools/VirtualPartner/Build macOS App")]
        public static void BuildMacOSApp()
        {
            BuildMacOSPlayer();
        }

        public static void BuildMacOSPlayer()
        {
            ValidateMacOSBuildSupport();

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");
            }

            var outputRoot = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "macOS");
            Directory.CreateDirectory(outputRoot);

            var productName = string.IsNullOrWhiteSpace(PlayerSettings.productName)
                ? DefaultAppName
                : SanitizeFileName(PlayerSettings.productName);
            var outputPath = Path.Combine(outputRoot, $"{productName}.app");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"macOS build failed: {report.summary.result}, errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");
            }

            UnityEngine.Debug.Log($"[Build] macOS app created: {outputPath}");
            EditorUtility.RevealInFinder(outputPath);
        }

        private static void ValidateMacOSBuildSupport()
        {
            var playbackEnginesRoot = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines");
            var hasMacSupport = Directory.Exists(Path.Combine(playbackEnginesRoot, "MacStandaloneSupport"))
                || Directory.Exists(Path.Combine(playbackEnginesRoot, "MacSupport"))
                || Directory.GetDirectories(playbackEnginesRoot, "*Mac*", SearchOption.TopDirectoryOnly).Length > 0;
            if (!hasMacSupport)
            {
                throw new InvalidOperationException(
                    $"macOS build support is not installed for this Unity editor. Install 'macOS Build Support (Mono)' and/or 'macOS Build Support (IL2CPP)' in Unity Hub for {Application.unityVersion}.");
            }

            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            if (backend == ScriptingImplementation.IL2CPP)
            {
                var hasIl2Cpp = Directory.GetDirectories(playbackEnginesRoot, "*IL2CPP*", SearchOption.AllDirectories).Length > 0;
                if (!hasIl2Cpp)
                {
                    throw new InvalidOperationException(
                        "Currently selected scripting backend is IL2CPP, but the IL2CPP module for macOS is not installed. Either install 'macOS Build Support (IL2CPP)' in Unity Hub or switch Player Settings > Other Settings > Scripting Backend to Mono.");
                }
            }
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        }
    }
}
#endif
