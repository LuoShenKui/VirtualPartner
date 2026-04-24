using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRDemo.Core
{
    /// <summary>
    /// 角色卡片 - 定义虚拟人物的基本信息
    /// </summary>
    [Serializable]
    public class CharacterCard
    {
        public string name = "桂言叶";
        public string description = "文静内向的文学少女";
        public List<string> personality = new List<string> { "内向", "温柔", "害羞" };
        public string voice = "zh-CN-XiaoxiaoNeural";
        public string modelPath = "Assets/Resources/Models/Characters/partner.vrm";
        
        // 表情映射
        public ExpressionMap expressions = new ExpressionMap();
        
        // 当前心情
        public Mood currentMood = Mood.Normal;
        
        // 最近记忆 (最近 10 条对话)
        public List<string> recentMemories = new List<string>();
        public int maxMemories = 10;
    }

    [Serializable]
    public class ExpressionMap
    {
        public string neutral = "neutral";
        public string happy = "smile";
        public string sad = "sorrow";
        public string angry = "angry";
        public string blush = "blush";
    }

    public enum Mood
    {
        Normal,
        Happy,
        Sad,
        Angry,
        Shy,
        Excited
    }
}
