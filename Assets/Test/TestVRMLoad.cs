using UnityEngine;

namespace VRDemo.Test
{
    /// <summary>
    /// 简单测试脚本 - 直接加载并显示 VRM 模型
    /// </summary>
    public class TestVRMLoad : MonoBehaviour
    {
        [Header("VRM 模型路径 (Resources 文件夹下)")]
        [SerializeField] private string modelPath = "Models/Characters/partner";
        
        private GameObject vrmInstance;
        
        private void Start()
        {
            Debug.Log("[TestVRMLoad] Starting...");
            
            // 尝试加载 VRM 模型
            var model = Resources.Load<GameObject>(modelPath);
            
            if (model == null)
            {
                Debug.LogError($"[TestVRMLoad] Failed to load model: {modelPath}");
                Debug.LogError("[TestVRMLoad] Make sure the .vrm file is in Assets/Resources/Models/Characters/");
                return;
            }
            
            Debug.Log($"[TestVRMLoad] Model loaded: {model.name}");
            
            // 实例化模型
            vrmInstance = Instantiate(model, new Vector3(0, 0, 2), Quaternion.Euler(0, 180, 0));
            
            Debug.Log($"[TestVRMLoad] Model instantiated at position {vrmInstance.transform.position}");
        }
        
        private void OnDestroy()
        {
            if (vrmInstance != null)
            {
                Destroy(vrmInstance);
            }
        }
    }
}
