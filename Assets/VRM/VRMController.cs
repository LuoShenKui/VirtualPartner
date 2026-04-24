using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace VRDemo.VRM
{
    /// <summary>
    /// VRM 控制器 - 加载和驱动 VRM 模型
    /// 需要 UniVRM 插件（手动安装）
    /// </summary>
    public class VRMController : MonoBehaviour
    {
        [Header("VRM 设置")]
        [SerializeField] private string modelPath;
        
        [Header("当前状态")]
        [SerializeField] private string currentExpression = "neutral";
        [SerializeField] private string currentMotion = "idle";
        
        // VRM 实例
        private GameObject vrmInstance;
        
        // Animator
        private Animator animator;
        private Component vrm10Instance;
        
        // 表情映射（使用 Animator 参数）
        private Dictionary<string, int> expressionMap = new Dictionary<string, int>
        {
            { "neutral", 0 },
            { "happy", 1 },
            { "sad", 2 },
            { "angry", 3 },
            { "blush", 4 },
            { "surprised", 5 },
            { "relaxed", 6 }
        };
        
        // 动作映射
        private Dictionary<string, string> motionMap = new Dictionary<string, string>
        {
            { "idle", "Idle" },
            { "wave", "Wave" },
            { "sit", "Sit" },
            { "nod", "Nod" },
            { "shake", "Shake" },
            { "talk", "Talk" },
            { "shy", "Shy" },
            { "think", "Think" },
            { "stepCloser", "Walk" },
            { "stepBack", "Walk" }
        };
        
        public event Action OnModelLoaded;
        public event Action<string> OnExpressionChanged;
        public event Action<string> OnMotionChanged;
        
        /// <summary>
        /// 加载 VRM 模型
        /// 注意：需要先手动安装 UniVRM 插件
        /// </summary>
        public void LoadModel(string path)
        {
            modelPath = path;
            Debug.Log($"[VRM] Loading model: {path}");

            UnloadModel();
            
            // 临时实现：使用 Resources.Load 加载
            // 正式版本需要 UniVRM 插件
            var model = Resources.Load<GameObject>(path);
            if (model != null)
            {
                vrmInstance = Instantiate(model, transform);
                vrmInstance.transform.position = new Vector3(0, 0, 2);
                vrmInstance.transform.rotation = Quaternion.Euler(0, 180, 0);
                
                animator = vrmInstance.GetComponentInChildren<Animator>();
                vrm10Instance = FindVrm10Instance(vrmInstance);
                
                InitializeExpressions();
                
                OnModelLoaded?.Invoke();
                Debug.Log($"[VRM] Model loaded: {path}");
            }
            else
            {
                Debug.LogWarning($"[VRM] Model not found: {path}. Please install UniVRM plugin.");
            }
        }
        
        /// <summary>
        /// 设置表情（使用 Animator 参数）
        /// </summary>
        public void SetExpression(string expressionName)
        {
            if (!expressionMap.ContainsKey(expressionName))
            {
                Debug.LogWarning($"[VRM] Unknown expression: {expressionName}");
                expressionName = "neutral";
            }
            
            currentExpression = expressionName;
            TrySetVrmExpression(expressionName);
            
            if (animator != null)
            {
                // 重置所有表情参数
                foreach (var key in expressionMap.Keys)
                {
                    animator.SetBool(key, false);
                }
                
                // 设置新表情
                animator.SetBool(expressionName, true);
            }
            
            OnExpressionChanged?.Invoke(expressionName);
            Debug.Log($"[VRM] Expression: {expressionName}");
        }
        
        /// <summary>
        /// 播放动作
        /// </summary>
        public void PlayMotion(string motionName)
        {
            if (!motionMap.ContainsKey(motionName))
            {
                Debug.LogWarning($"[VRM] Unknown motion: {motionName}");
                motionName = "idle";
            }
            
            currentMotion = motionName;
            
            if (animator != null)
            {
                animator.Play(motionMap[motionName]);
            }
            
            OnMotionChanged?.Invoke(motionName);
            Debug.Log($"[VRM] Motion: {motionName}");
        }
        
        /// <summary>
        /// 初始化表情系统
        /// </summary>
        private void InitializeExpressions()
        {
            if (animator != null)
            {
                Debug.Log("[VRM] Animator initialized");
            }
        }

        private void TrySetVrmExpression(string expressionName)
        {
            if (vrm10Instance == null)
            {
                return;
            }

            try
            {
                var expression = GetVrmExpressionRuntime(vrm10Instance);
                if (expression == null)
                {
                    return;
                }

                var expressionKeys = expression.GetType().GetProperty("ExpressionKeys")?.GetValue(expression) as System.Collections.IEnumerable;
                var setWeight = expression.GetType().GetMethod("SetWeight", BindingFlags.Instance | BindingFlags.Public);
                if (expressionKeys == null || setWeight == null)
                {
                    return;
                }

                object neutralKey = null;
                object targetKey = null;
                foreach (var key in expressionKeys)
                {
                    setWeight.Invoke(expression, new[] { key, (object)0f });
                    var keyName = key.ToString();
                    if (string.Equals(keyName, "Neutral", StringComparison.OrdinalIgnoreCase))
                    {
                        neutralKey = key;
                    }

                    if (ExpressionKeyMatches(keyName, expressionName))
                    {
                        targetKey = key;
                    }
                }

                targetKey ??= neutralKey;
                if (targetKey != null)
                {
                    setWeight.Invoke(expression, new[] { targetKey, (object)1f });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRM] Could not apply VRM expression by reflection: {ex.Message}");
            }
        }

        private static Component FindVrm10Instance(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == "Vrm10Instance")
                {
                    return component;
                }
            }

            return null;
        }

        private static object GetVrmExpressionRuntime(Component instance)
        {
            var runtime = instance.GetType().GetProperty("Runtime")?.GetValue(instance);
            return runtime?.GetType().GetProperty("Expression")?.GetValue(runtime);
        }

        private static bool ExpressionKeyMatches(string keyName, string expressionName)
        {
            keyName = keyName.ToLowerInvariant();
            return expressionName switch
            {
                "happy" => keyName.Contains("happy") || keyName.Contains("joy"),
                "sad" => keyName.Contains("sad") || keyName.Contains("sorrow"),
                "angry" => keyName.Contains("angry"),
                "blush" => keyName.Contains("relaxed") || keyName.Contains("happy"),
                "surprised" => keyName.Contains("surprised") || keyName.Contains("happy"),
                "relaxed" => keyName.Contains("relaxed"),
                _ => keyName.Contains("neutral")
            };
        }
        
        /// <summary>
        /// 卸载模型
        /// </summary>
        public void UnloadModel()
        {
            if (vrmInstance != null)
            {
                DisposeVrmRuntime(vrmInstance);
                vrmInstance = null;
                animator = null;
                vrm10Instance = null;
                Debug.Log("[VRM] Model unloaded");
            }
        }

        private static void DisposeVrmRuntime(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var runtime = instance.GetComponent("RuntimeGltfInstance") as IDisposable;
            if (runtime != null)
            {
                runtime.Dispose();
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }
        
        private void OnDestroy()
        {
            UnloadModel();
        }
    }
}
