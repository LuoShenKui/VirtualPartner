# VirtualPartner

一个 Unity 虚拟伴侣原型。当前目标不是完整游戏，而是先跑通“室内角色 + 本地 AI 对话 + 视角切换 + 动作/表情/语音反馈”的互动闭环。

## 已实现

- 卧室场景：基于导入的 MinimalistBedroom 本地资产，仓库不上传该公开资产包。
- 角色：男主、女主 VRM 模型，默认使用 `partner.vrm` 和 `player.vrm`。
- 对话：本地 Ollama，默认模型 `qwen3:4b`，启动时会做预热。
- UI：快捷互动、自由输入、回车发送、设置面板、打开日志。
- 视角：默认男主第一视角，按 `V` 切换女主第一视角。
- 女主行为：看向男主、基础待机、动作/表情映射、说话嘴型、1 分钟空闲主动寒暄。
- 语音：本地 CosyVoice HTTP 服务，男主/女主声线可区分，可在设置中关闭。
- Prompt：设置里可修改总 Prompt、男主人设、女主人设。

## 快速运行

1. 安装 Unity 6 / Unity 6000.x。
2. 安装 Ollama 并拉取模型：

```bash
brew install ollama
ollama pull qwen3:4b
```

3. 启动 Ollama：

```bash
ollama serve
```

4. 准备本地 CosyVoice 服务。

当前项目默认使用 `CosyVoice-300M-SFT` 的 FastAPI 服务，默认地址：

```text
http://localhost:50000
```

如果已经在本机安装过 `/Users/zs/Projects/CosyVoice`，设置面板里的默认启动命令会尝试自动拉起服务；否则请自行先启动本地服务。

5. 用 Unity 打开项目。
6. 打开场景：

```text
Assets/Scenes/CompanionBedroomScene.unity
```

7. 点击 Play。

## 操作

- `WASD`：移动当前视角角色。
- `V`：男主第一视角 / 女主第一视角切换。
- `T`：聚焦输入框。
- `Enter` 或小键盘 Enter：发送输入。
- `Esc`：释放鼠标。
- 设置按钮：修改名字、Prompt、人设、模型名、语音、女主模型路径。

## 本地 AI

默认使用：

```text
Ollama: http://localhost:11434
Model: qwen3:4b
```

对话系统要求模型返回短 JSON：

```json
{
  "text": "回复文本",
  "expression": "happy",
  "motion": "talk",
  "emotionIntensity": 0.6,
  "shouldSpeak": true
}
```

解析失败时会走本地 fallback，不会让 UI 一直卡住。

## 本地语音

默认使用：

```text
Backend: cosyvoice
URL: http://localhost:50000
Mode: sft
Partner voice: 中文女
Player voice: 中文男
```

当前语音链路是：

```text
Unity -> 本地 HTTP 请求 -> CosyVoice -> 返回 PCM -> Unity AudioSource 播放
```

项目启动时会先尝试预热语音服务，正式回复时只朗读 AI 回复，不朗读用户输入。

## UPM 依赖

UniVRM 通过 Unity Package Manager 引用指定版本，不提交本地 `Packages/com.vrmc.*` 目录。

```json
"com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.0",
"com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.0"
```

如果 Package Manager 解析失败，关闭 Unity 后重新打开项目，让它重新 resolve。



## 资产说明

仓库包含：

- 项目脚本
- Unity 场景
- ProjectSettings
- UPM manifest / lock
- `partner.vrm`
- `player.vrm`

仓库不包含：

- MinimalistBedroom
- Kevin Iglesias Human Basic Motions FREE
- Unity 生成缓存

`kotonoha.vrm` 和 `test_model.vrm` 是下载失败产生的 HTML 文件，已删除。

## 日志

对话日志写入：

```text
Application.persistentDataPath/ConversationLogs/YYYY-MM-DD.log
```

运行时 UI 可以点击“打开日志”。

## 构建

项目里提供了一个 macOS 构建入口：

```text
Tools / VirtualPartner / Build macOS App
```

构建输出目录默认是：

```text
Builds/macOS/<ProductName>.app
```

注意：

- 当前 Unity 安装必须带有 `macOS Build Support`
- 如果 `Player Settings > Scripting Backend` 是 `IL2CPP`，还必须安装 `macOS Build Support (IL2CPP)`
- 如果只装了 `Mono` 版构建支持，请先把 `Scripting Backend` 切到 `Mono`

## 当前限制

- 当前本地 TTS 方案可用，但延迟偏大，不适合产品级实时虚拟伴侣体验。
- 本项目当前更适合作为“本地虚拟伴侣原型”，不是完整产品。
- 动作/表情是第一版映射，后续需要接更完整的动画状态机。
- 文字对话和本地语音都坚持小模型路线，因此体验上限受端侧模型能力约束。
- MinimalistBedroom 和 Kevin Iglesias 需要本地自行导入，不随仓库分发。
