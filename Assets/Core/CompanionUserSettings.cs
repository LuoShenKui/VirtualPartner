using System;
using UnityEngine;

namespace VRDemo.Core
{
    [Serializable]
    public class CompanionUserSettingsData
    {
        public int settingsVersion = 5;
        public string playerName = "男主";
        public string partnerName = "桂言叶";
        public string playerPersona = "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。";
        public string partnerPersona = "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。";
        public string systemPrompt = "你正在驱动一个虚拟伴侣 Unity 原型。根据当前互动方向输出指定角色的台词和动作意图，不要输出旁白、思考过程或解释。";
        public string ollamaModelName = "qwen3:4b";
        public string speechBackend = "cosyvoice";
        public string cosyVoiceUrl = "http://localhost:50000";
        public string cosyVoiceMode = "sft";
        public string playerVoiceName = "中文男";
        public string partnerVoiceName = "中文女";
        public bool speakPlayerVoice = true;
        public bool speakPartnerVoice = true;
        public string partnerResourcePath = "Models/Characters/partner";
        public string partnerModelAssetPath = "Assets/Resources/Models/Characters/partner.vrm";
    }

    public static class CompanionUserSettings
    {
        private const string StorageKey = "VRDemo.CompanionUserSettings";

        public static CompanionUserSettingsData Load()
        {
            if (!PlayerPrefs.HasKey(StorageKey))
            {
                return new CompanionUserSettingsData();
            }

            var json = PlayerPrefs.GetString(StorageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CompanionUserSettingsData();
            }

            try
            {
                var settings = JsonUtility.FromJson<CompanionUserSettingsData>(json);
                if (settings != null && !json.Contains("speakPartnerVoice"))
                {
                    settings.speakPartnerVoice = true;
                }

                settings = Normalize(settings ?? new CompanionUserSettingsData());
                PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(settings));
                PlayerPrefs.Save();
                return settings;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CompanionSettings] Failed to parse saved settings: {ex.Message}");
                return new CompanionUserSettingsData();
            }
        }

        public static void Save(CompanionUserSettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            settings = Normalize(settings);
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(settings));
            PlayerPrefs.Save();
        }

        private static CompanionUserSettingsData Normalize(CompanionUserSettingsData settings)
        {
            if (settings.settingsVersion < 2)
            {
                if (string.IsNullOrWhiteSpace(settings.playerVoiceName) || settings.playerVoiceName == "Eddy")
                {
                    settings.playerVoiceName = "Reed (中文（中国大陆）)";
                }

                if (string.IsNullOrWhiteSpace(settings.partnerVoiceName) || settings.partnerVoiceName == "Tingting")
                {
                    settings.partnerVoiceName = "Sandy (中文（中国大陆）)";
                }

                settings.speakPlayerVoice = true;
                settings.settingsVersion = 2;
            }

            if (settings.settingsVersion < 3)
            {
                settings.playerPersona = string.IsNullOrWhiteSpace(settings.playerPersona)
                    ? "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。"
                    : settings.playerPersona;
                settings.partnerPersona = string.IsNullOrWhiteSpace(settings.partnerPersona)
                    ? "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。"
                    : settings.partnerPersona;
                settings.systemPrompt = string.IsNullOrWhiteSpace(settings.systemPrompt)
                    ? "你正在驱动一个虚拟伴侣 Unity 原型。只输出女主对男主说的话和动作意图，不要输出旁白、思考过程或解释。"
                    : settings.systemPrompt;
                settings.settingsVersion = 3;
            }

            if (settings.settingsVersion < 4)
            {
                if (settings.systemPrompt == "你正在驱动一个虚拟伴侣 Unity 原型。只输出女主对男主说的话和动作意图，不要输出旁白、思考过程或解释。")
                {
                    settings.systemPrompt = "你正在驱动一个虚拟伴侣 Unity 原型。根据当前互动方向输出指定角色的台词和动作意图，不要输出旁白、思考过程或解释。";
                }

                settings.settingsVersion = 4;
            }

            if (settings.settingsVersion < 5)
            {
                if (settings.ollamaModelName == "qwen3:8b" || IsDeprecatedQwen35(settings.ollamaModelName))
                {
                    settings.ollamaModelName = "qwen3:4b";
                }

                if (string.IsNullOrWhiteSpace(settings.speechBackend) || settings.speechBackend == "edge-tts" || settings.speechBackend == "macos-say")
                {
                    settings.speechBackend = "cosyvoice";
                }

                if (string.IsNullOrWhiteSpace(settings.cosyVoiceUrl))
                {
                    settings.cosyVoiceUrl = "http://localhost:50000";
                }

                if (string.IsNullOrWhiteSpace(settings.cosyVoiceMode))
                {
                    settings.cosyVoiceMode = "sft";
                }

                if (string.IsNullOrWhiteSpace(settings.playerVoiceName) || settings.playerVoiceName.Contains("Neural"))
                {
                    settings.playerVoiceName = "中文男";
                }

                if (string.IsNullOrWhiteSpace(settings.partnerVoiceName) || settings.partnerVoiceName.Contains("Neural"))
                {
                    settings.partnerVoiceName = "中文女";
                }

                settings.settingsVersion = 5;
            }

            settings.playerName = string.IsNullOrWhiteSpace(settings.playerName) ? "男主" : settings.playerName;
            settings.partnerName = string.IsNullOrWhiteSpace(settings.partnerName) ? "桂言叶" : settings.partnerName;
            settings.playerPersona = string.IsNullOrWhiteSpace(settings.playerPersona)
                ? "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。"
                : settings.playerPersona;
            settings.partnerPersona = string.IsNullOrWhiteSpace(settings.partnerPersona)
                ? "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。"
                : settings.partnerPersona;
            settings.systemPrompt = string.IsNullOrWhiteSpace(settings.systemPrompt)
                ? "你正在驱动一个虚拟伴侣 Unity 原型。根据当前互动方向输出指定角色的台词和动作意图，不要输出旁白、思考过程或解释。"
                : settings.systemPrompt;
            settings.ollamaModelName = string.IsNullOrWhiteSpace(settings.ollamaModelName) || IsDeprecatedQwen35(settings.ollamaModelName)
                ? "qwen3:4b"
                : settings.ollamaModelName;
            settings.speechBackend = string.IsNullOrWhiteSpace(settings.speechBackend) || settings.speechBackend == "edge-tts" || settings.speechBackend == "macos-say"
                ? "cosyvoice"
                : settings.speechBackend;
            settings.cosyVoiceUrl = string.IsNullOrWhiteSpace(settings.cosyVoiceUrl) ? "http://localhost:50000" : settings.cosyVoiceUrl.TrimEnd('/');
            settings.cosyVoiceMode = string.IsNullOrWhiteSpace(settings.cosyVoiceMode) ? "sft" : settings.cosyVoiceMode;
            settings.playerVoiceName = string.IsNullOrWhiteSpace(settings.playerVoiceName) ? "中文男" : settings.playerVoiceName;
            settings.partnerVoiceName = string.IsNullOrWhiteSpace(settings.partnerVoiceName) ? "中文女" : settings.partnerVoiceName;
            settings.partnerResourcePath = string.IsNullOrWhiteSpace(settings.partnerResourcePath) ? "Models/Characters/partner" : settings.partnerResourcePath;
            settings.partnerModelAssetPath = string.IsNullOrWhiteSpace(settings.partnerModelAssetPath)
                ? "Assets/Resources/Models/Characters/partner.vrm"
                : settings.partnerModelAssetPath;

            return settings;
        }

        private static bool IsDeprecatedQwen35(string modelName)
        {
            return !string.IsNullOrWhiteSpace(modelName)
                && modelName.Trim().StartsWith("qwen3.5", StringComparison.OrdinalIgnoreCase);
        }
    }
}
