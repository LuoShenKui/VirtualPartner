using UnityEngine;

namespace VRDemo
{
    /// <summary>
    /// 主控制器 - 整合所有系统
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("系统引用")]
        [SerializeField] private Core.DialogueSystem dialogueSystem;
        [SerializeField] private Core.MemoryManager memoryManager;
        [SerializeField] private VRM.VRMController vrmController;
        
        [Header("角色配置")]
        [SerializeField] private string characterConfigPath = "Configs/character";
        
        private Core.CharacterCard characterCard;
        
        private static GameManager instance;
        public static GameManager Instance => instance;
        
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
            Debug.Log("[Game] Starting VR Demo...");
            
            // 加载角色配置
            LoadCharacterConfig();
            
            // 加载 VRM 模型
            if (!string.IsNullOrEmpty(characterCard?.modelPath))
            {
                vrmController?.LoadModel(characterCard.modelPath);
            }
            
            // 设置初始表情
            vrmController?.SetExpression("neutral");
            vrmController?.PlayMotion("idle");
            
            Debug.Log("[Game] Ready!");
        }
        
        private void LoadCharacterConfig()
        {
            var textAsset = Resources.Load<TextAsset>(characterConfigPath);
            if (textAsset != null)
            {
                characterCard = JsonUtility.FromJson<Core.CharacterCard>(textAsset.text);
                Debug.Log($"[Game] Loaded character: {characterCard.name}");
            }
            else
            {
                // 使用默认配置
                characterCard = new Core.CharacterCard();
                Debug.LogWarning("[Game] Using default character config");
            }
            
            // 将角色卡传递给对话系统
            if (dialogueSystem != null)
            {
                // 需要通过反射或其他方式设置
                // 简化处理：直接在 DialogueSystem 中创建默认角色卡
            }
        }
        
        private void OnApplicationQuit()
        {
            Debug.Log("[Game] Shutting down...");
        }
    }
}
