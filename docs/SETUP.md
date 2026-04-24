# VR Demo - 项目设置说明

## 🎯 目标平台

- **macOS 13.0+** (Apple Silicon - M1/M2/M3)
- **架构**: ARM64 (原生)
- **分辨率**: 1280x720 (默认)

## ⚙️ Unity 设置

### Unity 版本
- **推荐**: 2022.3 LTS
- **最低**: 2021.3 LTS
- **架构**: Apple Silicon 原生

### Player Settings

```
Product Name: VR Demo
Bundle Identifier: com.vrdemo.app
Version: 1.0.0
Company: VRDemo
```

### 图形设置

```
Color Space: Linear
API: Metal (Apple Silicon 优化)
Fullscreen Mode: Windowed
Default Resolution: 1280x720
```

### 输入系统

```
Active Input Handler: New Input System
```

## 📦 必需插件

### 1. UniVRM (VRM 加载)

```bash
# Package Manager → Add from git URL
https://github.com/vrm-c/UniVRM.git?path=Assets/UniVRM/Editor
```

### 2. TextMeshPro (UI 文本)

```bash
# Package Manager → 已内置
com.unity.textmeshpro: 3.0.6
```

### 3. Input System (输入)

```bash
# Package Manager → 已内置
com.unity.inputsystem: 1.6.1
```

## 🔧 Ollama 配置

### 安装

```bash
# Homebrew
brew install ollama

# 或从官网下载
# https://ollama.ai
```

### 启动服务

```bash
# 后台运行
ollama serve

# 或作为服务
brew services start ollama
```

### 下载模型

```bash
# 下载 qwen3-8B
ollama pull qwen3-8b

# 验证
ollama list

# 测试
ollama run qwen3-8b "你好"
```

### API 端点

```
Base URL: http://localhost:11434
Generate: http://localhost:11434/api/generate
Chat: http://localhost:11434/api/chat
Tags: http://localhost:11434/api/tags
```

## 🎮 运行项目

### 在 Unity 中

```
1. 打开 Unity Hub
2. 添加项目：VR_demo 文件夹
3. 打开场景：Assets/Scenes/MainScene.unity
4. 点击 Play
```

### 构建应用

```
1. File → Build Settings
2. Platform: macOS
3. Architecture: ARM64
4. 点击 Build
```

## 📝 待办事项

- [ ] 安装 UniVRM 插件
- [ ] 下载 VRM 模型
- [ ] 配置 Ollama + qwen3-8B
- [ ] 创建完整 UI 场景
- [ ] 测试对话流程
