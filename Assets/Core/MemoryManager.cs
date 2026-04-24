using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRDemo.Core
{
    /// <summary>
    /// 记忆管理器 - 管理角色的短期记忆
    /// </summary>
    public class MemoryManager : MonoBehaviour
    {
        [SerializeField] private int maxMemories = 10;
        
        private List<MemoryEntry> memories = new List<MemoryEntry>();
        
        public List<MemoryEntry> Memories => memories;
        public event Action<List<MemoryEntry>> OnMemoriesUpdated;
        
        /// <summary>
        /// 添加新记忆
        /// </summary>
        public void AddMemory(string content, MemoryType type)
        {
            if (type == MemoryType.CharacterReply && LooksLikeModelAnalysis(content))
            {
                Debug.LogWarning($"[Memory] Skipped model analysis text: {content}");
                return;
            }

            var entry = new MemoryEntry
            {
                content = content,
                type = type,
                timestamp = DateTime.Now
            };
            
            memories.Add(entry);
            
            // 保持记忆数量在限制内
            while (memories.Count > maxMemories)
            {
                memories.RemoveAt(0);
            }
            
            OnMemoriesUpdated?.Invoke(memories);
            Debug.Log($"[Memory] Added: {content}");
        }

        private static bool LooksLikeModelAnalysis(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            return content.Contains("首先")
                || content.Contains("用户指定")
                || content.Contains("用户输入")
                || content.Contains("我需要")
                || content.Contains("根据要求")
                || content.Contains("在模拟中")
                || content.Length > 80;
        }
        
        /// <summary>
        /// 获取最近的记忆摘要 (用于 LLM 上下文)
        /// </summary>
        public string GetRecentMemoriesSummary(int count = 5)
        {
            var recent = memories.GetRange(Math.Max(0, memories.Count - count), 
                                          Math.Min(count, memories.Count));
            
            return string.Join("\n", recent.ConvertAll(m => 
                $"[{m.type}] {m.content}"));
        }
        
        /// <summary>
        /// 清空记忆
        /// </summary>
        public void Clear()
        {
            memories.Clear();
            OnMemoriesUpdated?.Invoke(memories);
        }
    }

    [Serializable]
    public class MemoryEntry
    {
        public string content;
        public MemoryType type;
        public DateTime timestamp;
    }

    public enum MemoryType
    {
        UserInput,      // 用户输入
        CharacterReply, // 角色回复
        SystemEvent     // 系统事件
    }
}
