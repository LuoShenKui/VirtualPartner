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
        private static readonly Dictionary<string, DateTime> unavailableCosyVoiceEndpoints = new Dictionary<string, DateTime>();
        private static readonly HashSet<string> warmedCosyVoiceEndpoints = new HashSet<string>();
        private static readonly SemaphoreSlim startupLock = new SemaphoreSlim(1, 1);
        private static string lastStartupKey = string.Empty;
        private AudioSource audioSource;
        private bool isSpeaking;

        private async void OnEnable()
        {
            EnsureAudioSource();
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            var settings = CompanionUserSettings.Load();
            await EnsureSpeechServiceRunningAsync(settings);
            await WarmupSpeechAsync(settings);
#endif
        }

        public async void ApplyUserSettings(CompanionUserSettingsData settings)
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            await EnsureSpeechServiceRunningAsync(settings);
            await WarmupSpeechAsync(settings);
#endif
        }

        public async void SpeakPartner(string text)
        {
            var settings = CompanionUserSettings.Load();
            if (!settings.speakPartnerVoice)
            {
                return;
            }

            await SpeakAsync(text, settings.partnerVoiceName, true, true, settings);
        }

        public async void SpeakPlayer(string text)
        {
            var settings = CompanionUserSettings.Load();
            if (!settings.speakPlayerVoice)
            {
                return;
            }

            await SpeakAsync(text, settings.playerVoiceName, true, false, settings);
        }

        private async Task SpeakAsync(string text, string voiceName, bool notify, bool isPartner, CompanionUserSettingsData settings)
        {
            text = DialogueSystem.BuildSpeechText(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            EnsureAudioSource();
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
                    await TrySpeakWithCosyVoiceAsync(text, voiceName, settings.cosyVoiceUrl, settings.cosyVoiceMode, isPartner);
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
        private static async Task EnsureSpeechServiceRunningAsync(CompanionUserSettingsData settings)
        {
            if (settings == null || !string.Equals(settings.speechBackend, "cosyvoice", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (await IsEndpointReachableAsync(settings.cosyVoiceUrl))
            {
                unavailableCosyVoiceEndpoints.Remove(GetCosyVoiceEndpointKey(
                    string.IsNullOrWhiteSpace(settings.cosyVoiceUrl) ? "http://localhost:50000" : settings.cosyVoiceUrl.TrimEnd('/'),
                    string.IsNullOrWhiteSpace(settings.cosyVoiceMode) ? "sft" : settings.cosyVoiceMode));
                return;
            }

            if (!settings.autoStartSpeechService)
            {
                return;
            }

            await startupLock.WaitAsync();
            try
            {
                if (await IsEndpointReachableAsync(settings.cosyVoiceUrl))
                {
                    return;
                }

                var command = ResolveSpeechServiceStartCommand(settings);
                var startupKey = $"{settings.cosyVoiceUrl}|{command}";
                if (startupKey == lastStartupKey)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(command))
                {
                    UnityEngine.Debug.LogWarning("[Speech] CosyVoice auto-start is enabled, but no start command is configured.");
                    return;
                }

                lastStartupKey = startupKey;

                var workingDirectory = ResolveWorkingDirectory(settings);
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/zsh",
                    Arguments = $"-lc {EscapeArg(command)}",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(startInfo);
                var reachable = await WaitForEndpointAsync(settings.cosyVoiceUrl, 60000);
                if (!reachable)
                {
                    UnityEngine.Debug.LogWarning($"[Speech] Tried to auto-start CosyVoice, but {settings.cosyVoiceUrl} is still unreachable.");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Speech] Could not auto-start CosyVoice: {ex.Message}");
            }
            finally
            {
                startupLock.Release();
            }
        }

        private async Task<bool> TrySpeakWithCosyVoiceAsync(string text, string voiceName, string baseUrl, string mode, bool isPartner)
        {
            baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:50000" : baseUrl.TrimEnd('/');
            mode = string.IsNullOrWhiteSpace(mode) ? "sft" : mode;
            var endpointKey = GetCosyVoiceEndpointKey(baseUrl, mode);
            if (IsCosyVoiceTemporarilyUnavailable(endpointKey))
            {
                return false;
            }

            var endpoint = $"{baseUrl}/inference_{mode}";
            var spkId = ResolveCosyVoiceSpeakerId(voiceName, isPartner);

            try
            {
                var startedAt = DateTime.UtcNow;
                var form = new WWWForm();
                form.AddField("tts_text", text);
                form.AddField("spk_id", spkId);
                if (mode == "instruct")
                {
                    form.AddField("instruct_text", "用温柔、自然、略带害羞的语气说话。");
                }

                using (var www = UnityWebRequest.Post(endpoint, form))
                {
                    www.timeout = 30;
                    await www.SendWebRequest();
                    if (www.result != UnityWebRequest.Result.Success || www.downloadHandler.data == null || www.downloadHandler.data.Length == 0)
                    {
                        var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
                        UnityEngine.Debug.LogWarning($"[Speech] CosyVoice request failed after {elapsedMs:F0} ms: {www.error}");
                        MarkCosyVoiceUnavailable(endpointKey, baseUrl, www.error);
                        return false;
                    }

                    var successElapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
                    UnityEngine.Debug.Log($"[Speech] CosyVoice received {www.downloadHandler.data.Length} bytes for {spkId} in {successElapsedMs:F0} ms.");
                    await PlayPcm16Async(www.downloadHandler.data, 22050, 1);
                }

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Speech] CosyVoice playback failed: {ex.Message}");
                MarkCosyVoiceUnavailable(endpointKey, baseUrl, ex.Message);
                return false;
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
            return IsCosyVoiceTemporarilyUnavailable(GetCosyVoiceEndpointKey(baseUrl, mode));
        }

        private static void MarkCosyVoiceUnavailable(string endpointKey, string baseUrl, string error)
        {
            unavailableCosyVoiceEndpoints[endpointKey] = DateTime.UtcNow.AddSeconds(5);
        }

        private static bool IsCosyVoiceTemporarilyUnavailable(string endpointKey)
        {
            if (!unavailableCosyVoiceEndpoints.TryGetValue(endpointKey, out var retryAfter))
            {
                return false;
            }

            if (DateTime.UtcNow >= retryAfter)
            {
                unavailableCosyVoiceEndpoints.Remove(endpointKey);
                return false;
            }

            return true;
        }

        private static string GetCosyVoiceEndpointKey(string baseUrl, string mode)
        {
            return $"{baseUrl.TrimEnd('/')}|{mode}";
        }

        private static string ResolveSpeechServiceStartCommand(CompanionUserSettingsData settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.speechServiceStartCommand))
            {
                return settings.speechServiceStartCommand.Trim();
            }

            return string.Empty;
        }

        private static string ResolveCosyVoiceSpeakerId(string voiceName, bool isPartner)
        {
            var normalized = NormalizeVoiceLabel(voiceName);
            if (normalized == "中文男" || normalized == "中文女")
            {
                return normalized;
            }

            return isPartner ? "中文女" : "中文男";
        }

        private static string NormalizeVoiceLabel(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName))
            {
                return string.Empty;
            }

            var value = voiceName.Trim();
            var lower = value.ToLowerInvariant();

            if (value.Contains("中文男", StringComparison.Ordinal) || value.Contains("男", StringComparison.Ordinal) || lower.Contains("male") || lower.Contains("reed") || lower.Contains("eddy"))
            {
                return "中文男";
            }

            if (value.Contains("中文女", StringComparison.Ordinal) || value.Contains("女", StringComparison.Ordinal) || lower.Contains("female") || lower.Contains("sandy") || lower.Contains("tingting") || lower.Contains("xiaoxiao"))
            {
                return "中文女";
            }

            return value;
        }

        private static async Task WarmupSpeechAsync(CompanionUserSettingsData settings)
        {
            if (settings == null || !string.Equals(settings.speechBackend, "cosyvoice", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var baseUrl = string.IsNullOrWhiteSpace(settings.cosyVoiceUrl) ? "http://localhost:50000" : settings.cosyVoiceUrl.TrimEnd('/');
            var mode = string.IsNullOrWhiteSpace(settings.cosyVoiceMode) ? "sft" : settings.cosyVoiceMode;
            var endpointKey = GetCosyVoiceEndpointKey(baseUrl, mode);
            if (warmedCosyVoiceEndpoints.Contains(endpointKey))
            {
                return;
            }

            try
            {
                var endpoint = $"{baseUrl}/inference_{mode}";
                var startedAt = DateTime.UtcNow;
                foreach (var spkId in new[] { "中文女", "中文男" })
                {
                    var warmupForm = new WWWForm();
                    warmupForm.AddField("tts_text", "你好");
                    warmupForm.AddField("spk_id", spkId);
                    if (mode == "instruct")
                    {
                        warmupForm.AddField("instruct_text", "用自然、简短的语气说话。");
                    }

                    using (var www = UnityWebRequest.Post(endpoint, warmupForm))
                    {
                        www.timeout = 20;
                        await www.SendWebRequest();
                        if (www.result != UnityWebRequest.Result.Success || www.downloadHandler.data == null || www.downloadHandler.data.Length <= 0)
                        {
                            UnityEngine.Debug.LogWarning($"[Speech] CosyVoice warmup failed for {spkId}: {www.error}");
                            return;
                        }
                    }
                }

                warmedCosyVoiceEndpoints.Add(endpointKey);
                var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
                UnityEngine.Debug.Log($"[Speech] CosyVoice warmed in {elapsedMs:F0} ms.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Speech] CosyVoice warmup failed: {ex.Message}");
            }
        }

        private static string ResolveWorkingDirectory(CompanionUserSettingsData settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.speechServiceWorkingDirectory) && Directory.Exists(settings.speechServiceWorkingDirectory))
            {
                return settings.speechServiceWorkingDirectory;
            }

            return Directory.GetCurrentDirectory();
        }

        private static async Task<bool> WaitForEndpointAsync(string baseUrl, int timeoutMs)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                if (await IsEndpointReachableAsync(baseUrl))
                {
                    return true;
                }

                await Task.Delay(500);
            }

            return false;
        }

        private static async Task<bool> IsEndpointReachableAsync(string baseUrl)
        {
            baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:50000" : baseUrl.TrimEnd('/');
            try
            {
                using (var www = UnityWebRequest.Get(baseUrl))
                {
                    www.timeout = 2;
                    await www.SendWebRequest();
                    return www.result == UnityWebRequest.Result.Success
                        || www.responseCode > 0;
                }
            }
            catch
            {
                return false;
            }
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

        private void EnsureAudioSource()
        {
            if (audioSource != null)
            {
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
        }

        private async Task PlayPcm16Async(byte[] pcm16, int sampleRate, int channels)
        {
            if (pcm16 == null || pcm16.Length < 2)
            {
                return;
            }

            EnsureAudioSource();
            audioSource.Stop();

            var sampleCount = pcm16.Length / 2;
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                short value = BitConverter.ToInt16(pcm16, i * 2);
                samples[i] = value / 32768f;
            }

            var clipSamplesPerChannel = Mathf.Max(1, sampleCount / Mathf.Max(1, channels));
            var clip = AudioClip.Create($"CosyVoice-{Guid.NewGuid():N}", clipSamplesPerChannel, channels, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.clip = clip;
            UnityEngine.Debug.Log($"[Speech] Playing clip at {sampleRate} Hz with {clip.samples} samples.");
            audioSource.Play();

            while (audioSource != null && audioSource.isPlaying)
            {
                await Task.Delay(50);
            }

            if (audioSource != null && audioSource.clip == clip)
            {
                audioSource.clip = null;
            }

            Destroy(clip);
        }

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
