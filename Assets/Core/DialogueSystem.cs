using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VRDemo.Core
{
    /// <summary>
    /// 对话系统 - 处理 LLM 对话
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        [Header("Ollama 设置")]
        [SerializeField] private string ollamaUrl = "http://localhost:11434";
        [SerializeField] private string modelName = "qwen3.5:2b";
        [SerializeField] private int requestTimeoutSeconds = 12;
        
        private string currentModelName = "qwen3.5:2b";
        public string CurrentModelName => currentModelName;
        
        [Header("角色引用")]
        [SerializeField] private CharacterCard characterCard;
        [SerializeField] private MemoryManager memoryManager;
        [SerializeField] private string playerName = "男主";
        [TextArea(2, 5)]
        [SerializeField] private string playerPersona = "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。";
        [TextArea(2, 5)]
        [SerializeField] private string partnerPersona = "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。";
        [TextArea(3, 8)]
        [SerializeField] private string systemPrompt = "你正在驱动一个虚拟伴侣 Unity 原型。只输出女主对男主说的话和动作意图，不要输出旁白、思考过程或解释。";
        [SerializeField] private bool autoStartOllama = true;
        
        // 事件
        public event Action<string> OnResponseReceived;
        public event Action<DialogueResponse> OnDialogueResolved;
        public event Action<string> OnError;
        public event Action<string> OnStatusChanged;

        private void Awake()
        {
            if (characterCard == null)
            {
                characterCard = new CharacterCard();
            }

            if (memoryManager == null)
            {
                memoryManager = FindAnyObjectByType<MemoryManager>();
            }

            ApplyUserSettings(CompanionUserSettings.Load());
        }

        private async void Start()
        {
            if (autoStartOllama)
            {
                await EnsureOllamaRunningAsync();
            }
        }
        
        /// <summary>
        /// 发送消息并获取回复 (流式输出)
        /// </summary>
        public async Task SendAsync(string userInput)
        {
            await SendInternalAsync(userInput, false, true, true);
        }

        public async Task SendProactiveAsync(string sceneInstruction)
        {
            await SendInternalAsync(sceneInstruction, true, false, false);
        }

        private async Task SendInternalAsync(string userInput, bool proactive, bool speakPlayerInput, bool rememberUserInput)
        {
            var prompt = "";
            var rawResponse = "";
            var elapsed = Stopwatch.StartNew();
            var usedFallback = false;
            var error = "";
            DialogueResponse dialogueResponse = null;

            try
            {
                if (characterCard == null)
                {
                    characterCard = new CharacterCard();
                }

                prompt = BuildPrompt(userInput, proactive);
                OnStatusChanged?.Invoke("Ollama 正在回复...");
                if (speakPlayerInput)
                {
                    FindAnyObjectByType<CompanionSpeechService>()?.SpeakPlayer(userInput);
                }

                rawResponse = await CallOllamaFastAsync(prompt);

                dialogueResponse = ParseResponse(rawResponse, userInput);
                OnResponseReceived?.Invoke(dialogueResponse.text);
                OnDialogueResolved?.Invoke(dialogueResponse);
                if (dialogueResponse.shouldSpeak)
                {
                    FindAnyObjectByType<CompanionSpeechService>()?.SpeakPartner(dialogueResponse.text);
                }
                
                if (rememberUserInput)
                {
                    memoryManager?.AddMemory(userInput, MemoryType.UserInput);
                }
                memoryManager?.AddMemory(dialogueResponse.text, MemoryType.CharacterReply);
                OnStatusChanged?.Invoke($"Ollama {elapsed.ElapsedMilliseconds}ms");

                Debug.Log($"[Dialogue] Response: {dialogueResponse.text}");
                Debug.Log($"[Dialogue] Expression: {dialogueResponse.expression}");
                Debug.Log($"[Dialogue] Motion: {dialogueResponse.motion}");
            }
            catch (Exception ex)
            {
                usedFallback = true;
                error = ex.Message;
                dialogueResponse = BuildFallbackResponse(userInput);
                Debug.LogWarning($"[Dialogue] Fallback response: {ex.Message}");
                OnError?.Invoke(ex.Message);
                OnResponseReceived?.Invoke(dialogueResponse.text);
                OnDialogueResolved?.Invoke(dialogueResponse);
                FindAnyObjectByType<CompanionSpeechService>()?.SpeakPartner(dialogueResponse.text);
                if (rememberUserInput)
                {
                    memoryManager?.AddMemory(userInput, MemoryType.UserInput);
                }
                memoryManager?.AddMemory(dialogueResponse.text, MemoryType.CharacterReply);
                OnStatusChanged?.Invoke($"本地兜底 {elapsed.ElapsedMilliseconds}ms");
            }
            finally
            {
                elapsed.Stop();
                ConversationLogService.WriteTurn(
                    "Ollama",
                    modelName,
                    proactive ? "[女主主动寒暄]" : userInput,
                    prompt,
                    rawResponse,
                    dialogueResponse,
                    elapsed.ElapsedMilliseconds,
                    usedFallback,
                    error);
            }
        }
        
        /// <summary>
        /// 构建 prompt - 快速回复模式
        /// </summary>
        private string BuildPrompt(string userInput, bool proactive)
        {
            string memorySummary = memoryManager?.GetRecentMemoriesSummary(3) ?? "";
            var turnInstruction = proactive
                ? $"场景：{userInput}\n请你作为女主主动对男主说一句自然寒暄或询问。"
                : $"用户：{userInput}";
            
            return $@"{systemPrompt}

你是一个虚拟伴侣，名字叫{characterCard.name}。
当前与你互动的玩家扮演男主，名字叫{playerName}。

男主人设：
{playerPersona}

女主人设：
{partnerPersona}

要求：
- 回复简短（30 字以内）
- 直接回答，不要思考过程
- 禁止输出 <think>、思考过程、Markdown
- 像正常人聊天一样
- 不要解释，不要长篇大论

补充性格标签：{string.Join(",", characterCard.personality)}
{memorySummary}

用 JSON 回复：
{{""text"": ""回复"", ""expression"": ""neutral/happy/sad/angry/blush/surprised/relaxed"", ""motion"": ""idle/wave/nod/shake/talk/shy/think/stepCloser/stepBack"", ""emotionIntensity"": 0.6, ""shouldSpeak"": true}}

{turnInstruction}

JSON:";
        }
        
        private async Task<string> CallOllamaFastAsync(string prompt)
        {
            using (var www = UnityWebRequest.Put($"{ollamaUrl}/api/chat", ""))
            {
                var request = new OllamaChatRequest
                {
                    model = modelName,
                    messages = new[]
                    {
                        new OllamaChatMessage { role = "user", content = prompt }
                    },
                    stream = false,
                    think = false,
                    options = new OllamaOptions
                    {
                        temperature = 0.6f,
                        num_predict = 80
                    }
                };

                var body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
                www.uploadHandler = new UploadHandlerRaw(body);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.method = UnityWebRequest.kHttpVerbPOST;
                www.timeout = requestTimeoutSeconds;

                await www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Ollama API Error: {www.error}");
                }

                var response = JsonUtility.FromJson<OllamaChatResponse>(www.downloadHandler.text);
                return response?.message != null ? response.message.content : www.downloadHandler.text;
            }
        }

        /// <summary>
        /// 解析回复
        /// </summary>
        private DialogueResponse ParseResponse(string json, string userInput = "")
        {
            json = CleanModelOutput(json);

            // 尝试提取 JSON (可能包含在 markdown 代码块中)
            int startIndex = json.IndexOf('{');
            int endIndex = json.LastIndexOf('}');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                json = json.Substring(startIndex, endIndex - startIndex + 1);
            }
            
            try
            {
                var response = JsonUtility.FromJson<DialogueResponse>(json);
                NormalizeResponse(response, userInput);
                return response;
            }
            catch
            {
                var fallback = BuildFallbackResponse(userInput);
                fallback.text = string.IsNullOrWhiteSpace(json) ? fallback.text : json;
                return fallback;
            }
        }

        private static string CleanModelOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text = Regex.Replace(text, "<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = text.Replace("```json", "").Replace("```", "").Trim();
            return text;
        }

        private static void NormalizeResponse(DialogueResponse response, string userInput)
        {
            if (response == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(response.text))
            {
                response.text = "我听着呢。";
            }

            if (string.IsNullOrWhiteSpace(response.expression))
            {
                response.expression = InferExpression(userInput, response.text);
            }

            if (string.IsNullOrWhiteSpace(response.motion))
            {
                response.motion = InferMotion(userInput, response.text);
            }

            if (response.emotionIntensity <= 0f)
            {
                response.emotionIntensity = 0.6f;
            }
        }

        private static string InferExpression(string userInput, string reply)
        {
            var text = $"{userInput} {reply}".ToLowerInvariant();
            if (text.Contains("害羞") || text.Contains("可爱") || text.Contains("喜欢")) return "blush";
            if (text.Contains("开心") || text.Contains("好呀") || text.Contains("嗯")) return "happy";
            if (text.Contains("难过")) return "sad";
            if (text.Contains("生气")) return "angry";
            return "neutral";
        }

        private static string InferMotion(string userInput, string reply)
        {
            var text = $"{userInput} {reply}".ToLowerInvariant();
            if (text.Contains("打招呼") || text.Contains("你好")) return "wave";
            if (text.Contains("靠近")) return "stepCloser";
            if (text.Contains("害羞")) return "shy";
            if (text.Contains("想") || text.Contains("考虑")) return "think";
            return "talk";
        }

        private DialogueResponse NewResponse(string text, string expression, string motion)
        {
            return new DialogueResponse
                {
                    text = text,
                    expression = expression,
                    motion = motion,
                    emotionIntensity = 0.6f,
                    shouldSpeak = true
                };
        }

        private DialogueResponse BuildFallbackResponse(string userInput)
        {
            var normalized = (userInput ?? string.Empty).ToLowerInvariant();

            if (normalized.Contains("打招呼") || normalized.Contains("你好") || normalized.Contains("陪你"))
            {
                return NewResponse("嗯，我在呢，过来陪我吧。", "happy", "wave");
            }

            if (normalized.Contains("可爱") || normalized.Contains("夸"))
            {
                return NewResponse("突然夸我，我会害羞的。", "blush", "shy");
            }

            if (normalized.Contains("床边") || normalized.Contains("休息") || normalized.Contains("坐"))
            {
                return NewResponse("好呀，我们坐近一点慢慢聊。", "happy", "stepCloser");
            }

            if (normalized.Contains("今天") || normalized.Contains("怎么样"))
            {
                return NewResponse("见到你以后，今天就变好了。", "happy", "nod");
            }

            return NewResponse("我听着呢，你继续说吧。", "neutral", "talk");
        }
        
        /// <summary>
        /// 设置当前使用的模型
        /// </summary>
        public void SetModel(string newModelName)
        {
            currentModelName = newModelName;
            Debug.Log($"[Dialogue] Model set to {currentModelName}");
            modelName = currentModelName;
        }

        public void ApplyUserSettings(CompanionUserSettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            if (characterCard == null)
            {
                characterCard = new CharacterCard();
            }

            playerName = string.IsNullOrWhiteSpace(settings.playerName) ? "男主" : settings.playerName.Trim();
            characterCard.name = string.IsNullOrWhiteSpace(settings.partnerName) ? "桂言叶" : settings.partnerName.Trim();
            playerPersona = string.IsNullOrWhiteSpace(settings.playerPersona)
                ? "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。"
                : settings.playerPersona.Trim();
            partnerPersona = string.IsNullOrWhiteSpace(settings.partnerPersona)
                ? "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。"
                : settings.partnerPersona.Trim();
            systemPrompt = string.IsNullOrWhiteSpace(settings.systemPrompt)
                ? "你正在驱动一个虚拟伴侣 Unity 原型。只输出女主对男主说的话和动作意图，不要输出旁白、思考过程或解释。"
                : settings.systemPrompt.Trim();

            if (!string.IsNullOrWhiteSpace(settings.partnerModelAssetPath))
            {
                characterCard.modelPath = settings.partnerModelAssetPath.Trim();
            }

            if (!string.IsNullOrWhiteSpace(settings.ollamaModelName))
            {
                SetModel(settings.ollamaModelName.Trim());
            }

        }

        private async Task EnsureOllamaRunningAsync()
        {
            if (await IsOllamaReachableAsync())
            {
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FindOllamaExecutable(),
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
                await Task.Delay(1200);
                Debug.Log(await IsOllamaReachableAsync()
                    ? "[Dialogue] Ollama started."
                    : "[Dialogue] Tried to start Ollama, but it is not reachable yet.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dialogue] Could not auto-start Ollama: {ex.Message}");
            }
        }

        private async Task<bool> IsOllamaReachableAsync()
        {
            try
            {
                using (var www = UnityWebRequest.Get($"{ollamaUrl}/api/tags"))
                {
                    www.timeout = 2;
                    await www.SendWebRequest();
                    return www.result == UnityWebRequest.Result.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string FindOllamaExecutable()
        {
            if (File.Exists("/opt/homebrew/bin/ollama"))
            {
                return "/opt/homebrew/bin/ollama";
            }

            if (File.Exists("/usr/local/bin/ollama"))
            {
                return "/usr/local/bin/ollama";
            }

            return "ollama";
        }
    }

    [Serializable]
    public class OllamaChatRequest
    {
        public string model;
        public OllamaChatMessage[] messages;
        public bool stream;
        public bool think;
        public OllamaOptions options;
    }

    [Serializable]
    public class OllamaChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class OllamaOptions
    {
        public float temperature;
        public int num_predict;
    }

    [Serializable]
    public class OllamaChatResponse
    {
        public OllamaChatMessage message;
        public bool done;
    }

    // 对话回复
    [Serializable]
    public class DialogueResponse
    {
        public string text;
        public string expression;
        public string motion;
        public float emotionIntensity = 0.6f;
        public bool shouldSpeak = true;
    }
}
