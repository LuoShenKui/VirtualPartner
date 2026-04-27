using System.Threading.Tasks;
using UnityEngine;
using VRDemo.Core;

namespace VRDemo.UI
{
    /// <summary>
    /// 运行时快捷互动面板，让卧室场景可以先跑通“选动作/说话/得到回应”。
    /// </summary>
    public class CompanionActionPanel : MonoBehaviour
    {
        private readonly string[] quickActions =
        {
            "我走到你身边，轻声和你打招呼。",
            "我夸你今天看起来很可爱。",
            "我想陪你坐到床边聊一会儿。",
            "我问你今天过得怎么样。",
            "我想和你一起休息一下。"
        };
        private readonly string[] femaleActions =
        {
            "你看向男主，主动轻声和他打招呼。",
            "你对男主笑了一下，问他是不是在看你。",
            "你往男主身边靠近一点，想继续聊天。",
            "你问男主今天过得怎么样。",
            "你有点害羞地问男主要不要坐下陪你。"
        };

        private DialogueSystem dialogueSystem;
        private CompanionCameraModeController cameraModeController;
        private string customInput = "";
        private string latestReply = "按 1-5 快捷互动，或输入对白后回车发送。";
        private string latestStatus = "Ollama 待机";
        private bool isSending;
        private bool showSettings;
        private bool isAutoSmallTalk;
        private float nextAutoSmallTalkTime;
        private float lastInputEditTime;
        private CompanionUserSettingsData settingsData;
        private readonly string[] smallTalkPrompts =
        {
            "你主动看向男主，轻声问他怎么不说话了。",
            "你对男主笑了一下，问他要不要继续聊天。",
            "你轻轻挥手，主动问男主现在在想什么。"
        };
        private const float SmallTalkDelaySeconds = 60f;
        private const float TypingGraceSeconds = 8f;

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle inputStyle;
        private GUIStyle replyStyle;
        private GUIStyle crosshairStyle;
        private GUIStyle smallButtonStyle;
        private Vector2 settingsScroll;

        private void OnEnable()
        {
            dialogueSystem = FindAnyObjectByType<DialogueSystem>();
            cameraModeController = FindAnyObjectByType<CompanionCameraModeController>();
            settingsData = CompanionUserSettings.Load();
            nextAutoSmallTalkTime = Time.time + SmallTalkDelaySeconds;
            if (dialogueSystem != null)
            {
                dialogueSystem.OnResponseReceived += OnResponseReceived;
                dialogueSystem.OnError += OnError;
                dialogueSystem.OnStatusChanged += OnStatusChanged;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || dialogueSystem == null || isSending || showSettings)
            {
                return;
            }

            if (IsUserPreparingInput)
            {
                nextAutoSmallTalkTime = Time.time + SmallTalkDelaySeconds;
                return;
            }

            if (Time.time >= nextAutoSmallTalkTime)
            {
                var prompt = smallTalkPrompts[Random.Range(0, smallTalkPrompts.Length)];
                isAutoSmallTalk = true;
                nextAutoSmallTalkTime = Time.time + SmallTalkDelaySeconds;
                _ = SendPromptAsync(prompt);
            }
        }

        private void OnDisable()
        {
            if (dialogueSystem != null)
            {
                dialogueSystem.OnResponseReceived -= OnResponseReceived;
                dialogueSystem.OnError -= OnError;
                dialogueSystem.OnStatusChanged -= OnStatusChanged;
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureStyles();
            HandleKeyboardShortcuts(Event.current);
            DrawCrosshair();
            DrawReplyBar();
            DrawActionHud();
            DrawSettingsButton();
            DrawSettingsWindow();
        }

        private void DrawActionHud()
        {
            var width = Mathf.Min(560, Screen.width - 48);
            var height = Mathf.Min(390, Screen.height - 120);
            var panelRect = new Rect(Screen.width - width - 18, Screen.height - height - 18, width, height);
            GUILayout.BeginArea(panelRect, panelStyle);
            GUILayout.Label(IsFemalePerspective ? "女主视角互动" : "男主视角互动", titleStyle);
            GUILayout.Label(IsFemalePerspective
                ? "当前为女主视角：WASD 移动女主，V 返回男主视角，T 聚焦输入。"
                : "当前为男主视角：WASD 移动男主，V 切换女主视角，T 聚焦输入。",
                bodyStyle);
            GUILayout.Space(4);

            var actions = CurrentActions;
            for (var i = 0; i < actions.Length; i++)
            {
                GUI.enabled = !isSending;
                if (GUILayout.Button($"{i + 1}. {actions[i]}", buttonStyle, GUILayout.Height(28)))
                {
                    _ = SendPromptAsync(actions[i]);
                }
            }

            GUI.enabled = !isSending;
            GUILayout.Space(5);
            GUILayout.Label(IsFemalePerspective ? "女主台词/动作意图" : "男主对白", bodyStyle);
            GUI.SetNextControlName("CompanionInput");
            var previousInput = customInput;
            customInput = GUILayout.TextArea(customInput, inputStyle, GUILayout.Height(80));
            if (customInput != previousInput)
            {
                lastInputEditTime = Time.time;
                nextAutoSmallTalkTime = Time.time + SmallTalkDelaySeconds;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isSending ? "回应中..." : "发送对白", buttonStyle, GUILayout.Height(34)))
            {
                SubmitCustomInput();
            }

            if (GUILayout.Button("打开日志", buttonStyle, GUILayout.Width(112), GUILayout.Height(34)))
            {
                ConversationLogService.OpenTodayLog();
            }
            GUILayout.EndHorizontal();

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawReplyBar()
        {
            var width = Mathf.Min(680, Screen.width - 160);
            var rect = new Rect(22, 22, width, 74);
            GUILayout.BeginArea(rect, panelStyle);
            GUILayout.Label(IsFemalePerspective ? "男主回应" : "女主回应", titleStyle);
            GUILayout.Label(latestReply, replyStyle);
            GUILayout.Label(latestStatus, bodyStyle);
            GUILayout.EndArea();
        }

        private void DrawSettingsButton()
        {
            var rect = new Rect(Screen.width - 128, 22, 104, 32);
            if (GUI.Button(rect, showSettings ? "关闭设置" : "设置", smallButtonStyle))
            {
                showSettings = !showSettings;
                if (showSettings)
                {
                    settingsData = CompanionUserSettings.Load();
                }
            }
        }

        private void DrawSettingsWindow()
        {
            if (!showSettings)
            {
                return;
            }

            var width = Mathf.Min(620, Screen.width - 60);
            var height = Mathf.Min(560, Screen.height - 110);
            var rect = new Rect(Screen.width - width - 24, 68, width, height);

            GUILayout.BeginArea(rect, panelStyle);
            GUILayout.Label("互动设置", titleStyle);
            GUILayout.Label("本地 Ollama、Prompt、人设、语音和女主模型路径。", bodyStyle);
            GUILayout.Space(8);

            settingsScroll = GUILayout.BeginScrollView(settingsScroll);
            GUILayout.Label("男主名字", bodyStyle);
            settingsData.playerName = GUILayout.TextField(settingsData.playerName ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Space(6);

            GUILayout.Label("女主名字", bodyStyle);
            settingsData.partnerName = GUILayout.TextField(settingsData.partnerName ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Space(6);

            GUILayout.Label("总 Prompt / 世界规则", bodyStyle);
            settingsData.systemPrompt = GUILayout.TextArea(settingsData.systemPrompt ?? string.Empty, inputStyle, GUILayout.Height(86));
            GUILayout.Space(6);

            GUILayout.Label("男主人设", bodyStyle);
            settingsData.playerPersona = GUILayout.TextArea(settingsData.playerPersona ?? string.Empty, inputStyle, GUILayout.Height(72));
            GUILayout.Space(6);

            GUILayout.Label("女主人设", bodyStyle);
            settingsData.partnerPersona = GUILayout.TextArea(settingsData.partnerPersona ?? string.Empty, inputStyle, GUILayout.Height(72));
            GUILayout.Space(6);

            GUILayout.Label("Ollama 本地模型名", bodyStyle);
            settingsData.ollamaModelName = GUILayout.TextField(settingsData.ollamaModelName ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Label("Mac 推荐：qwen3:4b；更轻：qwen3:1.7b；更好但更慢：qwen3:8b。Ollama 默认模型目录在 ~/.ollama/models。", bodyStyle);
            GUILayout.Space(6);

            GUILayout.Label("语音后端", bodyStyle);
            settingsData.speechBackend = GUILayout.TextField(settingsData.speechBackend ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Label("填写 cosyvoice。已安装并开启自动启动时，进入游戏会自动拉起本地 CosyVoice 服务。", bodyStyle);
            GUILayout.Space(6);

            GUILayout.Label("CosyVoice 服务地址", bodyStyle);
            settingsData.cosyVoiceUrl = GUILayout.TextField(settingsData.cosyVoiceUrl ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Label("默认：http://localhost:50000，模式默认 sft，女主 voice 可填 中文女。", bodyStyle);
            GUILayout.Space(6);

            GUILayout.Label("语音服务自动启动", bodyStyle);
            settingsData.autoStartSpeechService = GUILayout.Toggle(settingsData.autoStartSpeechService, "进入游戏时自动拉起语音服务");
            GUILayout.Label("语音服务工作目录", bodyStyle);
            settingsData.speechServiceWorkingDirectory = GUILayout.TextField(settingsData.speechServiceWorkingDirectory ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Label("语音服务启动命令", bodyStyle);
            settingsData.speechServiceStartCommand = GUILayout.TextField(settingsData.speechServiceStartCommand ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Label("示例：source scripts/venv/bin/activate && python server.py --port 50000 --model_dir pretrained_models/CosyVoice2-0.5B", bodyStyle);
            GUILayout.Space(6);

            GUILayout.Label("女主语音名称", bodyStyle);
            settingsData.partnerVoiceName = GUILayout.TextField(settingsData.partnerVoiceName ?? string.Empty, inputStyle, GUILayout.Height(30));
            settingsData.speakPartnerVoice = GUILayout.Toggle(settingsData.speakPartnerVoice, "女主回复时播放语音");
            GUILayout.Label("男主语音名称", bodyStyle);
            settingsData.playerVoiceName = GUILayout.TextField(settingsData.playerVoiceName ?? string.Empty, inputStyle, GUILayout.Height(30));
            settingsData.speakPlayerVoice = GUILayout.Toggle(settingsData.speakPlayerVoice, "发送男主对白时也播放语音");
            GUILayout.Space(6);

            GUILayout.Label("女主 Resources 路径", bodyStyle);
            settingsData.partnerResourcePath = GUILayout.TextField(settingsData.partnerResourcePath ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Label("示例：Models/Characters/partner", bodyStyle);
            GUILayout.Space(6);

            GUILayout.Label("女主本地模型 Asset 路径", bodyStyle);
            settingsData.partnerModelAssetPath = GUILayout.TextField(settingsData.partnerModelAssetPath ?? string.Empty, inputStyle, GUILayout.Height(30));
            GUILayout.Label("示例：Assets/Resources/Models/Characters/partner.vrm", bodyStyle);
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存并应用", buttonStyle, GUILayout.Height(34)))
            {
                SaveAndApplySettings();
            }

            if (GUILayout.Button("恢复默认", buttonStyle, GUILayout.Height(34)))
            {
                settingsData = new CompanionUserSettingsData();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawCrosshair()
        {
            var size = 24f;
            var rect = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size);
            GUI.Label(rect, "+", crosshairStyle);
        }

        private void HandleKeyboardShortcuts(Event evt)
        {
            if (isSending)
            {
                return;
            }

            if (showSettings)
            {
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    showSettings = false;
                }

                return;
            }

            var actions = CurrentActions;
            if (Input.GetKeyDown(KeyCode.Alpha1)) _ = SendPromptAsync(actions[0]);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _ = SendPromptAsync(actions[1]);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _ = SendPromptAsync(actions[2]);
            if (Input.GetKeyDown(KeyCode.Alpha4)) _ = SendPromptAsync(actions[3]);
            if (Input.GetKeyDown(KeyCode.Alpha5)) _ = SendPromptAsync(actions[4]);

            if (Input.GetKeyDown(KeyCode.F2))
            {
                showSettings = !showSettings;
                if (showSettings)
                {
                    settingsData = CompanionUserSettings.Load();
                }
            }

            if (evt != null && evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
            {
                SubmitCustomInput();
                evt.Use();
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                GUI.FocusControl(GUI.GetNameOfFocusedControl() == "CompanionInput" ? string.Empty : "CompanionInput");
            }
        }

        private void SubmitCustomInput()
        {
            if (string.IsNullOrWhiteSpace(customInput))
            {
                return;
            }

            _ = SendPromptAsync(customInput);
            customInput = "";
            lastInputEditTime = Time.time;
        }

        private bool IsFemalePerspective =>
            cameraModeController != null && cameraModeController.CurrentMode == CompanionCameraMode.FemalePerspective;

        private string[] CurrentActions => IsFemalePerspective ? femaleActions : quickActions;

        private bool IsUserPreparingInput =>
            !string.IsNullOrWhiteSpace(customInput) || Time.time - lastInputEditTime < TypingGraceSeconds;

        private async Task SendPromptAsync(string prompt)
        {
            if (dialogueSystem == null || isSending)
            {
                latestReply = "场景里还没有对话系统。";
                return;
            }

            isSending = true;
            var wasAutoSmallTalk = isAutoSmallTalk;
            var wasFemalePerspective = IsFemalePerspective;
            isAutoSmallTalk = false;
            latestReply = wasFemalePerspective ? "男主正在回应..." : "她正在想怎么回应你...";
            latestStatus = "发送中...";
            if (wasFemalePerspective)
            {
                await dialogueSystem.SendFemalePerspectiveAsync(prompt);
            }
            else if (wasAutoSmallTalk)
            {
                await dialogueSystem.SendProactiveAsync(prompt);
            }
            else
            {
                await dialogueSystem.SendAsync(prompt);
            }
            nextAutoSmallTalkTime = Time.time + SmallTalkDelaySeconds;
            isSending = false;
        }

        private void OnResponseReceived(string response)
        {
            latestReply = response;
        }

        private void OnStatusChanged(string status)
        {
            latestStatus = status;
        }

        private void OnError(string error)
        {
            latestStatus = $"已使用本地兜底：{error}";
            isSending = false;
        }

        private void SaveAndApplySettings()
        {
            settingsData.playerName = string.IsNullOrWhiteSpace(settingsData.playerName) ? "男主" : settingsData.playerName.Trim();
            settingsData.partnerName = string.IsNullOrWhiteSpace(settingsData.partnerName) ? "桂言叶" : settingsData.partnerName.Trim();
            settingsData.partnerResourcePath = string.IsNullOrWhiteSpace(settingsData.partnerResourcePath) ? "Models/Characters/partner" : settingsData.partnerResourcePath.Trim();
            settingsData.partnerModelAssetPath = string.IsNullOrWhiteSpace(settingsData.partnerModelAssetPath)
                ? "Assets/Resources/Models/Characters/partner.vrm"
                : settingsData.partnerModelAssetPath.Trim();
            settingsData.ollamaModelName = string.IsNullOrWhiteSpace(settingsData.ollamaModelName) ? "qwen3:4b" : settingsData.ollamaModelName.Trim();
            settingsData.systemPrompt = string.IsNullOrWhiteSpace(settingsData.systemPrompt)
                ? "你正在驱动一个虚拟伴侣 Unity 原型。根据当前互动方向输出指定角色的台词和动作意图，不要输出旁白、思考过程或解释。"
                : settingsData.systemPrompt.Trim();
            settingsData.playerPersona = string.IsNullOrWhiteSpace(settingsData.playerPersona)
                ? "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。"
                : settingsData.playerPersona.Trim();
            settingsData.partnerPersona = string.IsNullOrWhiteSpace(settingsData.partnerPersona)
                ? "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。"
                : settingsData.partnerPersona.Trim();
            settingsData.partnerVoiceName = settingsData.partnerVoiceName?.Trim() ?? string.Empty;
            settingsData.playerVoiceName = settingsData.playerVoiceName?.Trim() ?? string.Empty;
            settingsData.speechBackend = string.IsNullOrWhiteSpace(settingsData.speechBackend) ? "cosyvoice" : settingsData.speechBackend.Trim();
            settingsData.cosyVoiceUrl = string.IsNullOrWhiteSpace(settingsData.cosyVoiceUrl) ? "http://localhost:50000" : settingsData.cosyVoiceUrl.Trim().TrimEnd('/');
            settingsData.cosyVoiceMode = string.IsNullOrWhiteSpace(settingsData.cosyVoiceMode) ? "sft" : settingsData.cosyVoiceMode.Trim();
            settingsData.speechServiceWorkingDirectory = settingsData.speechServiceWorkingDirectory?.Trim() ?? string.Empty;
            settingsData.speechServiceStartCommand = settingsData.speechServiceStartCommand?.Trim() ?? string.Empty;

            CompanionUserSettings.Save(settingsData);
            FindAnyObjectByType<DialogueSystem>()?.ApplyUserSettings(settingsData);
            FindAnyObjectByType<CompanionSpeechService>()?.ApplyUserSettings(settingsData);
            FindAnyObjectByType<CompanionInteractionDirector>()?.ApplyUserSettings(settingsData);
            latestReply = $"已应用设置：模型 {settingsData.ollamaModelName}，语音 {settingsData.speechBackend}";
            showSettings = false;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 14, 14)
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.97f, 0.96f, 0.92f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.86f, 0.87f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 6, 6)
            };
            inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
            replyStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };
            crosshairStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.92f) }
            };
            smallButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
