using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace VRDemo.Core
{
    public static class ConversationLogService
    {
        public static string LogDirectory => Path.Combine(Application.persistentDataPath, "ConversationLogs");

        public static string TodayLogPath => Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");

        public static void WriteTurn(
            string provider,
            string model,
            string userInput,
            string prompt,
            string rawResponse,
            DialogueResponse response,
            long elapsedMs,
            bool usedFallback,
            string error = "")
        {
            Directory.CreateDirectory(LogDirectory);

            var entry =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]\n" +
                $"provider: {provider}\n" +
                $"model: {model}\n" +
                $"elapsedMs: {elapsedMs}\n" +
                $"fallback: {usedFallback}\n" +
                $"error: {error}\n" +
                $"user: {userInput}\n" +
                $"prompt: {prompt}\n" +
                $"raw: {rawResponse}\n" +
                $"text: {response?.text}\n" +
                $"expression: {response?.expression}\n" +
                $"motion: {response?.motion}\n" +
                $"emotionIntensity: {response?.emotionIntensity}\n" +
                $"shouldSpeak: {response?.shouldSpeak}\n\n";

            File.AppendAllText(TodayLogPath, entry);
        }

        public static void OpenTodayLog()
        {
            Directory.CreateDirectory(LogDirectory);
            if (!File.Exists(TodayLogPath))
            {
                File.WriteAllText(TodayLogPath, "");
            }

            OpenPath(TodayLogPath);
        }

        public static void OpenLogDirectory()
        {
            Directory.CreateDirectory(LogDirectory);
            OpenPath(LogDirectory);
        }

        private static void OpenPath(string path)
        {
            try
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                Process.Start("open", EscapeArg(path));
#else
                Application.OpenURL(path);
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ConversationLog] Could not open {path}: {ex.Message}");
            }
        }

        private static string EscapeArg(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}
