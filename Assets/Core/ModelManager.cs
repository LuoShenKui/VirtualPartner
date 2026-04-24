using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRDemo.Core
{
    /// <summary>
    /// 模型管理器 - 管理可用的本地大模型
    /// </summary>
    public class ModelManager : MonoBehaviour
    {
        private static ModelManager instance;
        public static ModelManager Instance => instance;
        
        [Header("Ollama 设置")]
        [SerializeField] private string ollamaUrl = "http://localhost:11434";
        
        // 可用模型列表
        private List<ModelInfo> availableModels = new List<ModelInfo>();
        public List<ModelInfo> AvailableModels => availableModels;
        
        // 当前选中的模型
        private ModelInfo currentModel;
        public ModelInfo CurrentModel => currentModel;
        
        // 事件
        public event Action OnModelsUpdated;
        public event Action<ModelInfo> OnModelChanged;
        
        // 推荐模型列表 (按性能排序)
        private static readonly string[] RecommendedModels = new[]
        {
            "qwen3.5:2b",      // 最快，中文好 ⭐⭐⭐⭐⭐
            "qwen3.5:4b",      // 平衡 ⭐⭐⭐⭐
            "qwen3.5:9b",      // 质量好 ⭐⭐⭐
            "phi3:mini",       // 微软出品 ⭐⭐⭐
            "llama3.2:3b",     // Meta 出品 ⭐⭐⭐
            "gemma2:2b",       // Google 出品 ⭐⭐⭐
        };
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            RefreshModels();
        }
        
        /// <summary>
        /// 刷新可用模型列表
        /// </summary>
        public async void RefreshModels()
        {
            try
            {
                availableModels.Clear();
                
                // 获取已安装的模型
                var installedModels = await GetInstalledModelsAsync();
                availableModels.AddRange(installedModels);
                
                // 添加推荐但未安装的模型
                foreach (var modelName in RecommendedModels)
                {
                    if (!installedModels.Exists(m => m.name == modelName))
                    {
                        availableModels.Add(new ModelInfo
                        {
                            name = modelName,
                            size = "未安装",
                            isInstalled = false
                        });
                    }
                }
                
                // 设置当前模型
                if (currentModel == null || !currentModel.isInstalled)
                {
                    currentModel = installedModels.Count > 0 ? installedModels[0] : null;
                }
                
                OnModelsUpdated?.Invoke();
                Debug.Log($"[Model] Refreshed {availableModels.Count} models");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Model] Refresh error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取已安装的模型列表
        /// </summary>
        private async System.Threading.Tasks.Task<List<ModelInfo>> GetInstalledModelsAsync()
        {
            var models = new List<ModelInfo>();
            
            using (var www = UnityEngine.Networking.UnityWebRequest.Get($"{ollamaUrl}/api/tags"))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                
                var asyncOp = www.SendWebRequest();
                while (!asyncOp.isDone) await System.Threading.Tasks.Task.Yield();
                
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<OllamaTagsResponse>(www.downloadHandler.text);
                    if (response.models != null)
                    {
                        foreach (var model in response.models)
                        {
                            models.Add(new ModelInfo
                            {
                                name = model.name,
                                size = FormatSize(model.size),
                                isInstalled = true,
                                isRecommended = IsRecommended(model.name)
                            });
                        }
                    }
                }
            }
            
            return models;
        }
        
        /// <summary>
        /// 切换模型
        /// </summary>
        public void SwitchModel(ModelInfo model)
        {
            if (model.isInstalled)
            {
                currentModel = model;
                OnModelChanged?.Invoke(model);
                Debug.Log($"[Model] Switched to {model.name}");
                
                // 保存到 PlayerPrefs
                PlayerPrefs.SetString("CurrentModel", model.name);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogWarning($"[Model] Cannot switch to {model.name} - not installed");
            }
        }
        
        /// <summary>
        /// 获取模型安装命令
        /// </summary>
        public string GetInstallCommand(string modelName)
        {
            return $"ollama pull {modelName}";
        }
        
        /// <summary>
        /// 检查 Ollama 是否可用
        /// </summary>
        public async System.Threading.Tasks.Task<bool> CheckOllamaAvailable()
        {
            try
            {
                using (var www = UnityEngine.Networking.UnityWebRequest.Get($"{ollamaUrl}/api/tags"))
                {
                    www.timeout = 5;
                    var asyncOp = www.SendWebRequest();
                    while (!asyncOp.isDone) await System.Threading.Tasks.Task.Yield();
                    
                    return www.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                }
            }
            catch
            {
                return false;
            }
        }
        
        private string FormatSize(long sizeInBytes)
        {
            if (sizeInBytes < 1024 * 1024)
                return $"{sizeInBytes / 1024} KB";
            else if (sizeInBytes < 1024 * 1024 * 1024)
                return $"{sizeInBytes / (1024 * 1024)} MB";
            else
                return $"{sizeInBytes / (1024 * 1024 * 1024)} GB";
        }
        
        private bool IsRecommended(string modelName)
        {
            foreach (var rec in RecommendedModels)
            {
                if (modelName.StartsWith(rec.Split(':')[0]))
                    return true;
            }
            return false;
        }
    }
    
    [Serializable]
    public class ModelInfo
    {
        public string name;
        public string size;
        public bool isInstalled;
        public bool isRecommended;
        
        public string DisplayName => $"{name} {(!isInstalled ? "(未安装)" : "")}";
    }
    
    [Serializable]
    public class OllamaTagsResponse
    {
        public List<OllamaModel> models;
    }
    
    [Serializable]
    public class OllamaModel
    {
        public string name;
        public long size;
        public string digest;
    }
}
