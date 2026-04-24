# VirtualPartner 场景设置指南

## 🎯 快速开始

### 1️⃣ 打开场景

在 Unity 中：
```
Assets → Scenes → VirtualPartnerScene.unity
```
双击打开场景。

---

### 2️⃣ 添加 VRM 角色

#### 添加 partner（虚拟伴侣）

1. 在 **Project** 窗口找到：
   ```
   Assets → Resources → Models → Characters → partner.vrm
   ```

2. **拖拽到场景中**

3. 在 **Hierarchy** 中选中 `partner`

4. 在 **Inspector** 设置：
   ```
   Position: X=0, Y=0, Z=4
   Rotation: X=0, Y=0, Z=0
   Scale: X=1, Y=1, Z=1
   ```

5. **添加待机动画**：
   - 点击 **Add Component**
   - 搜索 `IdleAnimation`
   - 添加脚本

---

#### 添加 player（玩家 - 可选）

1. 拖拽 `player.vrm` 到场景中

2. 设置位置：
   ```
   Position: X=0, Y=0, Z=2
   Rotation: X=0, Y=180, Z=0
   ```

3. 同样添加 `IdleAnimation` 组件

---

### 3️⃣ 配置相机（第一人称视角）

1. 在 **Hierarchy** 选中 **Main Camera**

2. 设置：
   ```
   Position: X=0, Y=1.5, Z=2
   Rotation: X=0, Y=180, Z=0
   ```

---

### 4️⃣ 配置 UI

#### 设置输入框引用

1. 在 **Hierarchy** 选中 **QuickChatTest** 物体

2. 在 **Inspector** 中找到 `QuickChatTest` 组件

3. 拖拽赋值：
   - **Input Field** → 拖入 `Canvas/InputField`
   - **Response Text** → 拖入 `Canvas/ResponseText`

---

### 5️⃣ 测试运行

1. 确保 Ollama 正在运行：
   ```bash
   ollama list
   ```
   应该能看到 `qwen3.5:2b`

2. 在 Unity 中点击 **▶ Play**

3. 点击场景底部的输入框

4. 输入消息，按 **Enter** 发送

5. 等待 AI 回复（应该 2-6 秒内）

---

## 🎨 场景元素说明

| 物体 | 作用 | 必须 |
|------|------|------|
| Main Camera | 第一人称视角 | ✅ |
| Directional Light | 光源 | ✅ |
| Canvas | UI 容器 | ✅ |
| InputField | 输入框 | ✅ |
| ResponseText | 显示回复 | ✅ |
| GameManager | 游戏总控 | ✅ |
| RoomGenerator | 自动生成房间 | ✅ |
| QuickChatTest | 对话测试 | ✅ |
| partner | 虚拟伴侣 | ✅ |
| player | 玩家角色 | ⬜ 可选 |

---

## ⚙️ 调整建议

### 如果回复太慢

1. 打开 `Assets/Resources/Configs/character.json`
2. 确保 `systemPrompt` 存在
3. 在 Unity 中检查 `DialogueSystem.cs` 的 prompt 是否简短

### 如果模型不显示

1. 确认 UniVRM 已安装
2. 右键 VRM 文件 → **Reimport**
3. 查看 Console 窗口有无错误

### 如果房间太暗

1. 选中 `Directional Light`
2. 增加 **Intensity** 到 1.2-1.5
3. 或添加额外光源：**GameObject → Light → Point Light**

---

## 🎮 快捷键

| 按键 | 功能 |
|------|------|
| Enter | 发送消息 |
| Escape | 停止播放 |
| Ctrl + S | 保存场景 |

---

## 📁 文件位置

```
~/Projects/VirtualPartner/
├── Assets/
│   ├── Scenes/
│   │   └── VirtualPartnerScene.unity  ← 主场景
│   ├── Resources/
│   │   ├── Models/Characters/
│   │   │   ├── partner.vrm  ← 女角色
│   │   │   └── player.vrm   ← 男角色
│   │   └── Configs/
│   │       └── character.json  ← 角色配置
│   ├── Core/
│   │   └── DialogueSystem.cs  ← 对话系统
│   ├── Test/
│   │   ├── SimpleRoomGenerator.cs  ← 房间生成
│   │   ├── IdleAnimation.cs        ← 待机动画
│   │   └── QuickChatTest.cs        ← 快速对话测试
│   └── VRM/
│       └── VRMController.cs  ← VRM 控制
└── Packages/
    └── com.vrmc.vrm/  ← UniVRM 插件
```

---

## 🐛 常见问题

### Q: 输入框点不了？
**A:** 确保 Canvas 的 **Graphic Raycaster** 组件存在

### Q: 回复是乱码？
**A:** 检查 Ollama 是否正常运行，尝试 `ollama run qwen3.5:2b "测试"`

### Q: 角色穿模了？
**A:** 调整 partner 的 Z 位置到 3.5-4.5 之间

---

**祝使用愉快！** 🎉
