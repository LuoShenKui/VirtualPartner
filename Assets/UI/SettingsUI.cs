using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using VRDemo.Core;

namespace VRDemo.UI
{
    /// <summary>
    /// 设置界面 - 模型切换等设置
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private TMP_Dropdown modelDropdown;
        [SerializeField] private TMP_Text ollamaStatusText;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button installGuideButton;
        [SerializeField] private Button closeButton;
        
        [Header("安装指南面板")]
        [SerializeField] private GameObject installGuidePanel;
        [SerializeField] private TMP_Text installCommandText;
        
        private ModelManager modelManager;
        private bool isOllamaAvailable = false;
        
        private void Awake()
        {
            modelManager = FindAnyObjectByType<ModelManager>();
        }
        
        private void Start()
        {
            if (modelManager != null)
            {
                modelManager.OnModelsUpdated += UpdateModelDropdown;
                modelManager.OnModelChanged += OnModelChanged;
            }
            
            refreshButton.onClick.AddListener(RefreshModels);
            installGuideButton.onClick.AddListener(ShowInstallGuide);
            closeButton.onClick.AddListener(CloseSettings);
            
            CheckOllamaStatus();
        }
        
        private void OnDestroy()
        {
            if (modelManager != null)
            {
                modelManager.OnModelsUpdated -= UpdateModelDropdown;
                modelManager.OnModelChanged -= OnModelChanged;
            }
        }
        
        /// <summary>
        /// 打开设置面板
        /// </summary>
        public void OpenSettings()
        {
            settingsPanel.SetActive(true);
            RefreshModels();
        }
        
        /// <summary>
        /// 关闭设置面板
        /// </summary>
        public void CloseSettings()
        {
            settingsPanel.SetActive(false);
            installGuidePanel.SetActive(false);
        }
        
        /// <summary>
        /// 检查 Ollama 状态
        /// </summary>
        private async void CheckOllamaStatus()
        {
            isOllamaAvailable = await modelManager.CheckOllamaAvailable();
            
            if (ollamaStatusText != null)
            {
                ollamaStatusText.text = isOllamaAvailable 
                    ? "✅ Ollama 已连接" 
                    : "❌ Ollama 未连接";
                ollamaStatusText.color = isOllamaAvailable 
                    ? Color.green 
                    : Color.red;
            }
            
            if (!isOllamaAvailable)
            {
                Debug.LogWarning("[Settings] Ollama not available. Please run: brew services start ollama");
            }
        }
        
        /// <summary>
        /// 刷新模型列表
        /// </summary>
        private void RefreshModels()
        {
            modelManager?.RefreshModels();
        }
        
        /// <summary>
        /// 更新模型下拉列表
        /// </summary>
        private void UpdateModelDropdown()
        {
            if (modelDropdown == null || modelManager == null) return;
            
            modelDropdown.ClearOptions();
            
            var options = new List<string>();
            int currentIndex = 0;
            
            // 已安装的模型优先
            var installedModels = modelManager.AvailableModels.FindAll(m => m.isInstalled);
            var notInstalledModels = modelManager.AvailableModels.FindAll(m => !m.isInstalled);
            
            foreach (var model in installedModels)
            {
                string displayName = model.isRecommended ? $"⭐ {model.name}" : model.name;
                options.Add($"{displayName} ({model.size})");
                
                if (modelManager.CurrentModel != null && 
                    modelManager.CurrentModel.name == model.name)
                {
                    currentIndex = options.Count - 1;
                }
            }
            
            if (installedModels.Count > 0 && notInstalledModels.Count > 0)
            {
                options.Add("────────────────");
            }
            
            foreach (var model in notInstalledModels)
            {
                string displayName = model.isRecommended ? $"⭐ {model.name}" : model.name;
                options.Add($"{displayName} (未安装)");
            }
            
            modelDropdown.AddOptions(options);
            modelDropdown.value = Mathf.Min(currentIndex, options.Count - 1);
            modelDropdown.RefreshShownValue();
            
            // 监听下拉选择
            modelDropdown.onValueChanged.RemoveListener(OnModelSelected);
            modelDropdown.onValueChanged.AddListener(OnModelSelected);
        }
        
        /// <summary>
        /// 模型选择回调
        /// </summary>
        private void OnModelSelected(int index)
        {
            if (modelManager == null) return;
            
            var installedModels = modelManager.AvailableModels.FindAll(m => m.isInstalled);
            var notInstalledModels = modelManager.AvailableModels.FindAll(m => !m.isInstalled);
            var allModels = new List<ModelInfo>();
            allModels.AddRange(installedModels);
            allModels.AddRange(notInstalledModels);
            
            // 跳过分隔符
            int actualIndex = index;
            if (index >= installedModels.Count && installedModels.Count > 0 && notInstalledModels.Count > 0)
            {
                actualIndex = index - 1; // 跳过分隔符
            }
            
            if (actualIndex >= 0 && actualIndex < allModels.Count)
            {
                var selectedModel = allModels[actualIndex];
                
                if (selectedModel.isInstalled)
                {
                    modelManager.SwitchModel(selectedModel);
                }
                else
                {
                    // 显示安装指南
                    ShowInstallGuideForModel(selectedModel.name);
                }
            }
        }
        
        /// <summary>
        /// 模型切换回调
        /// </summary>
        private void OnModelChanged(ModelInfo model)
        {
            Debug.Log($"[Settings] Model changed to {model.name}");
            
            // 更新 DialogueSystem 的模型设置
            var dialogueSystem = FindAnyObjectByType<DialogueSystem>();
            if (dialogueSystem != null)
            {
                dialogueSystem.SetModel(model.name);
            }
        }
        
        /// <summary>
        /// 显示安装指南
        /// </summary>
        private void ShowInstallGuide()
        {
            installGuidePanel.SetActive(true);
            
            if (installCommandText != null)
            {
                installCommandText.text = 
                    "=== 安装 Ollama ===\n\n" +
                    "1. 安装 Ollama:\n" +
                    "   brew install ollama\n\n" +
                    "2. 启动服务:\n" +
                    "   brew services start ollama\n\n" +
                    "3. 下载模型:\n" +
                    "   ollama pull qwen3.5:2b\n\n" +
                    "4. 测试:\n" +
                    "   ollama run qwen3.5:2b \"你好\"\n\n" +
                    "=== 推荐模型 ===\n\n" +
                    "⭐ qwen3.5:2b  - 最快 (~6 秒)\n" +
                    "⭐ qwen3.5:4b  - 平衡 (~8 秒)\n" +
                    "⭐ qwen3.5:9b  - 质量好 (~11 秒)";
            }
        }
        
        /// <summary>
        /// 显示特定模型的安装指南
        /// </summary>
        private void ShowInstallGuideForModel(string modelName)
        {
            installGuidePanel.SetActive(true);
            
            if (installCommandText != null)
            {
                installCommandText.text = 
                    $"=== 安装 {modelName} ===\n\n" +
                    "在终端执行:\n\n" +
                    $"  ollama pull {modelName}\n\n" +
                    "下载完成后重启应用。\n\n" +
                    $"模型大小：{GetModelSizeHint(modelName)}";
            }
        }
        
        private string GetModelSizeHint(string modelName)
        {
            return modelName switch
            {
                "qwen3.5:2b" => "~2.7 GB",
                "qwen3.5:4b" => "~3.4 GB",
                "qwen3.5:9b" => "~6.6 GB",
                "phi3:mini" => "~2.2 GB",
                "llama3.2:3b" => "~2.0 GB",
                "gemma2:2b" => "~1.6 GB",
                _ => "未知"
            };
        }
    }
}
