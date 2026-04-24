using System;
using UnityEngine;

namespace VRDemo.Core
{
    [Serializable]
    public class CompanionUserSettingsData
    {
        public int settingsVersion = 3;
        public string playerName = "男主";
        public string partnerName = "桂言叶";
        public string playerPersona = "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。";
        public string partnerPersona = "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。";
        public string systemPrompt = "你正在驱动一个虚拟伴侣 Unity 原型。只输出女主对男主说的话和动作意图，不要输出旁白、思考过程或解释。";
        public string ollamaModelName = "qwen3.5:2b";
        public string playerVoiceName = "Reed (中文（中国大陆）)";
        public string partnerVoiceName = "Sandy (中文（中国大陆）)";
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

                return Normalize(settings ?? new CompanionUserSettingsData());
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

            settings.playerName = string.IsNullOrWhiteSpace(settings.playerName) ? "男主" : settings.playerName;
            settings.partnerName = string.IsNullOrWhiteSpace(settings.partnerName) ? "桂言叶" : settings.partnerName;
            settings.playerPersona = string.IsNullOrWhiteSpace(settings.playerPersona)
                ? "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。"
                : settings.playerPersona;
            settings.partnerPersona = string.IsNullOrWhiteSpace(settings.partnerPersona)
                ? "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。"
                : settings.partnerPersona;
            settings.systemPrompt = string.IsNullOrWhiteSpace(settings.systemPrompt)
                ? "你正在驱动一个虚拟伴侣 Unity 原型。只输出女主对男主说的话和动作意图，不要输出旁白、思考过程或解释。"
                : settings.systemPrompt;
            settings.ollamaModelName = string.IsNullOrWhiteSpace(settings.ollamaModelName) ? "qwen3.5:2b" : settings.ollamaModelName;
            settings.playerVoiceName = string.IsNullOrWhiteSpace(settings.playerVoiceName) ? "Reed (中文（中国大陆）)" : settings.playerVoiceName;
            settings.partnerVoiceName = string.IsNullOrWhiteSpace(settings.partnerVoiceName) ? "Sandy (中文（中国大陆）)" : settings.partnerVoiceName;
            settings.partnerResourcePath = string.IsNullOrWhiteSpace(settings.partnerResourcePath) ? "Models/Characters/partner" : settings.partnerResourcePath;
            settings.partnerModelAssetPath = string.IsNullOrWhiteSpace(settings.partnerModelAssetPath)
                ? "Assets/Resources/Models/Characters/partner.vrm"
                : settings.partnerModelAssetPath;

            return settings;
        }
    }
}
