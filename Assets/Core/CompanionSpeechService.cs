using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VRDemo.Core
{
    public class CompanionSpeechService : MonoBehaviour
    {
        public event Action<bool> OnSpeakingChanged;
        public event Action<bool> OnPartnerSpeakingChanged;
        public event Action<bool> OnPlayerSpeakingChanged;

        private readonly SemaphoreSlim speechQueue = new SemaphoreSlim(1, 1);
        private static readonly HashSet<string> unavailableCosyVoiceEndpoints = new HashSet<string>();
        private bool isSpeaking;

        public async void SpeakPartner(string text)
        {
            var settings = CompanionUserSettings.Load();
            if (!settings.speakPartnerVoice)
            {
                return;
            }

            await SpeakAsync(text, settings.partnerVoiceName, 205, true, true, settings);
        }

        public async void SpeakPlayer(string text)
        {
            var settings = CompanionUserSettings.Load();
            if (!settings.speakPlayerVoice)
            {
                return;
            }

            await SpeakAsync(text, settings.playerVoiceName, 190, true, false, settings);
        }

        private async Task SpeakAsync(string text, string voiceName, int rate, bool notify, bool isPartner, CompanionUserSettingsData settings)
        {
            text = DialogueSystem.SanitizeSpokenText(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (IsDisabledCosyVoice(settings))
            {
                return;
            }

            await speechQueue.WaitAsync();
            try
            {
                SetSpeaking(true, notify, isPartner);
                if (string.Equals(settings.speechBackend, "cosyvoice", StringComparison.OrdinalIgnoreCase))
                {
                    await TrySpeakWithCosyVoiceAsync(text, voiceName, settings.cosyVoiceUrl, settings.cosyVoiceMode);
                    return;
                }

                if (string.Equals(settings.speechBackend, "edge-tts", StringComparison.OrdinalIgnoreCase))
                {
                    await TrySpeakWithEdgeTtsAsync(text, voiceName);
                    return;
                }

                UnityEngine.Debug.LogWarning($"[Speech] Unsupported speech backend '{settings.speechBackend}'. Set it to cosyvoice.");
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

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private static async Task<bool> TrySpeakWithCosyVoiceAsync(string text, string voiceName, string baseUrl, string mode)
        {
            baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:50000" : baseUrl.TrimEnd('/');
            mode = string.IsNullOrWhiteSpace(mode) ? "sft" : mode;
            var endpointKey = GetCosyVoiceEndpointKey(baseUrl, mode);
            if (unavailableCosyVoiceEndpoints.Contains(endpointKey))
            {
                return false;
            }

            var endpoint = $"{baseUrl}/inference_{mode}";
            var spkId = string.IsNullOrWhiteSpace(voiceName) || voiceName.Contains("Neural") ? "中文女" : voiceName;
            var outputPath = Path.Combine(Path.GetTempPath(), $"virtualpartner-cosyvoice-{Guid.NewGuid():N}.wav");

            try
            {
                var form = new WWWForm();
                form.AddField("tts_text", text);
                form.AddField("spk_id", spkId);
                if (mode == "instruct")
                {
                    form.AddField("instruct_text", "用温柔、自然、略带害羞的语气说话。");
                }

                using (var www = UnityWebRequest.Post(endpoint, form))
                {
                    www.timeout = 3;
                    await www.SendWebRequest();
                    if (www.result != UnityWebRequest.Result.Success || www.downloadHandler.data == null || www.downloadHandler.data.Length == 0)
                    {
                        MarkCosyVoiceUnavailable(endpointKey, baseUrl, www.error);
                        return false;
                    }

                    File.WriteAllBytes(outputPath, BuildWav(www.downloadHandler.data, 22050, 1));
                }

                var player = Process.Start(new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = EscapeArg(outputPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (player != null)
                {
                    await Task.Run(() => player.WaitForExit());
                }

                return true;
            }
            catch (Exception ex)
            {
                MarkCosyVoiceUnavailable(endpointKey, baseUrl, ex.Message);
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch
                {
                    // Best-effort temp cleanup.
                }
            }
        }

        private static bool IsDisabledCosyVoice(CompanionUserSettingsData settings)
        {
            if (!string.Equals(settings.speechBackend, "cosyvoice", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var baseUrl = string.IsNullOrWhiteSpace(settings.cosyVoiceUrl) ? "http://localhost:50000" : settings.cosyVoiceUrl.TrimEnd('/');
            var mode = string.IsNullOrWhiteSpace(settings.cosyVoiceMode) ? "sft" : settings.cosyVoiceMode;
            return unavailableCosyVoiceEndpoints.Contains(GetCosyVoiceEndpointKey(baseUrl, mode));
        }

        private static void MarkCosyVoiceUnavailable(string endpointKey, string baseUrl, string error)
        {
            unavailableCosyVoiceEndpoints.Add(endpointKey);
        }

        private static string GetCosyVoiceEndpointKey(string baseUrl, string mode)
        {
            return $"{baseUrl.TrimEnd('/')}|{mode}";
        }

        private static async Task<bool> TrySpeakWithEdgeTtsAsync(string text, string voiceName)
        {
            var voice = string.IsNullOrWhiteSpace(voiceName) ? "zh-CN-XiaoxiaoNeural" : voiceName;
            var outputPath = Path.Combine(Path.GetTempPath(), $"virtualpartner-tts-{Guid.NewGuid():N}.mp3");
            try
            {
                var synth = Process.Start(new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"-m edge_tts --voice {EscapeArg(voice)} --text {EscapeArg(text)} --write-media {EscapeArg(outputPath)}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                });

                if (synth == null)
                {
                    return false;
                }

                await Task.Run(() => synth.WaitForExit());
                if (synth.ExitCode != 0 || !File.Exists(outputPath))
                {
                    var error = await synth.StandardError.ReadToEndAsync();
                    UnityEngine.Debug.LogWarning($"[Speech] edge-tts unavailable. No fallback will be used. {error}");
                    return false;
                }

                var player = Process.Start(new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = EscapeArg(outputPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (player != null)
                {
                    await Task.Run(() => player.WaitForExit());
                }

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Speech] edge-tts failed. No fallback will be used: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch
                {
                    // Best-effort temp cleanup.
                }
            }
        }

#endif

        private static byte[] BuildWav(byte[] pcm16, int sampleRate, short channels)
        {
            const short bitsPerSample = 16;
            var byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + pcm16.Length);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(pcm16.Length);
                writer.Write(pcm16);
                return stream.ToArray();
            }
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
