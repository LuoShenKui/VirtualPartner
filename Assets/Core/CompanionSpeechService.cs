using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VRDemo.Core
{
    public class CompanionSpeechService : MonoBehaviour
    {
        public event Action<bool> OnSpeakingChanged;
        public event Action<bool> OnPartnerSpeakingChanged;
        public event Action<bool> OnPlayerSpeakingChanged;

        private readonly SemaphoreSlim speechQueue = new SemaphoreSlim(1, 1);
        private bool isSpeaking;

        public async void SpeakPartner(string text)
        {
            var settings = CompanionUserSettings.Load();
            if (!settings.speakPartnerVoice)
            {
                return;
            }

            await SpeakAsync(text, settings.partnerVoiceName, 205, true, true);
        }

        public async void SpeakPlayer(string text)
        {
            var settings = CompanionUserSettings.Load();
            if (!settings.speakPlayerVoice)
            {
                return;
            }

            await SpeakAsync(text, settings.playerVoiceName, 190, true, false);
        }

        private async Task SpeakAsync(string text, string voiceName, int rate, bool notify, bool isPartner)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            await speechQueue.WaitAsync();
            try
            {
                SetSpeaking(true, notify, isPartner);
                var arguments = string.IsNullOrWhiteSpace(voiceName)
                    ? $"-r {rate} {EscapeArg(text)}"
                    : $"-v {EscapeArg(voiceName)} -r {rate} {EscapeArg(text)}";
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "say",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    await Task.Run(() => process.WaitForExit());
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Speech] Failed to speak: {ex.Message}");
            }
            finally
            {
                SetSpeaking(false, notify, isPartner);
                speechQueue.Release();
            }
#else
            UnityEngine.Debug.Log($"[Speech] {text}");
            await Task.CompletedTask;
#endif
        }

        private void SetSpeaking(bool value, bool notify, bool isPartner)
        {
            if (isSpeaking == value)
            {
                return;
            }

            isSpeaking = value;
            if (notify)
            {
                OnSpeakingChanged?.Invoke(value);
                if (isPartner)
                {
                    OnPartnerSpeakingChanged?.Invoke(value);
                }
                else
                {
                    OnPlayerSpeakingChanged?.Invoke(value);
                }
            }
        }

        private static string EscapeArg(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}
