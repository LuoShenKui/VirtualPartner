# VirtualPartner - Unity 场景设置指南

## 🎯 快速设置场景

### Step 1: 创建新场景

1. 打开 Unity
2. **File → New Scene**
3. 保存为 `Assets/Scenes/TestScene.unity`

---

### Step 2: 添加 VRM 模型

1. 在 **Project** 窗口（底部）找到：
   ```
   Assets → Resources → Models → Characters → partner.vrm
   ```

2. **直接拖拽 partner.vrm 到场景中**

3. 模型应该出现在场景中央

---

### Step 3: 调整模型位置

1. 在 **Hierarchy** 窗口选中 `partner`（或类似名称）
2. 在 **Inspector** 窗口设置：
   ```
   Position: X=0, Y=0, Z=2
   Rotation: X=0, Y=180, Z=0
   Scale: X=1, Y=1, Z=1
   ```

---

### Step 4: 调整相机

1. 在 **Hierarchy** 窗口选中 **Main Camera**
2. 设置：
   ```
   Position: X=0, Y=1.2, Z=3
   Rotation: X=0, Y=180, Z=0
   Field of View: 60
   ```

---

### Step 5: 添加灯光

1. **GameObject → Light → Directional Light**
2. 设置：
   ```
   Position: X=0, Y=3, Z=0
   Rotation: X=50, Y=-30, Z=0
   Intensity: 1
   ```

---

### Step 6: 运行测试

1. 点击 Unity 顶部的 **▶ Play** 按钮
2. 应该能看到你的虚拟伴侣角色！

---

## ❓ 常见问题

### Q: 拖拽 VRM 后什么都没发生？
**A:** 检查 UniVRM 是否安装：
- Window → Package Manager
- 应该能看到 "VRM" 相关的包

### Q: 模型显示为粉色方块？
**A:** 材质丢失，需要重新导入：
1. 选中 VRM 文件
2. 右键 → Reimport

### Q: 模型太黑看不清？
**A:** 灯光不足，添加更多灯光：
- GameObject → Light → Point Light

---

## 🎨 后续优化

确认模型能显示后，可以：
1. 添加对话 UI
2. 添加表情控制
3. 添加动作动画
4. 连接 Ollama 实现对话
