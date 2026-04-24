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
        [SerializeField] private string modelName = "qwen3:4b";
        [SerializeField] private int requestTimeoutSeconds = 8;
        [SerializeField] private int warmupTimeoutSeconds = 45;
        
        private string currentModelName = "qwen3:4b";
        public string CurrentModelName => currentModelName;
        private bool modelWarmed;
        
        [Header("角色引用")]
        [SerializeField] private CharacterCard characterCard;
        [SerializeField] private MemoryManager memoryManager;
        [SerializeField] private string playerName = "男主";
        [TextArea(2, 5)]
        [SerializeField] private string playerPersona = "玩家扮演温柔、主动但尊重边界的男主，会通过说话和动作与女主互动。";
        [TextArea(2, 5)]
        [SerializeField] private string partnerPersona = "女主是二次元风格的温柔少女，文静、害羞、会主动关心男主，回应要自然、有情绪但不过度夸张。";
        [TextArea(3, 8)]
        [SerializeField] private string systemPrompt = "你正在驱动一个虚拟伴侣 Unity 原型。根据当前互动方向输出指定角色的台词和动作意图，不要输出旁白、思考过程或解释。";
        [SerializeField] private bool autoStartOllama = true;
        
        // 事件
        public event Action<string> OnResponseReceived;
        public event Action<DialogueResponse> OnDialogueResolved;
        public event Action<string> OnError;
        public event Action<string> OnStatusChanged;
        private string lastReplyText = "";

        private enum ResponseSpeaker
        {
            Partner,
            Player
        }

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

            await WarmupModelAsync();
        }
        
        /// <summary>
        /// 发送消息并获取回复 (流式输出)
        /// </summary>
        public async Task SendAsync(string userInput)
        {
            await SendInternalAsync(userInput, false, true, false, true, ResponseSpeaker.Partner);
        }

        public async Task SendProactiveAsync(string sceneInstruction)
        {
            await SendInternalAsync(sceneInstruction, true, false, false, false, ResponseSpeaker.Partner);
        }

        public async Task SendFemalePerspectiveAsync(string femaleInput)
        {
            await SendInternalAsync(femaleInput, false, true, true, true, ResponseSpeaker.Player);
        }

        private async Task SendInternalAsync(
            string userInput,
            bool proactive,
            bool speakInput,
            bool inputIsPartner,
            bool rememberUserInput,
            ResponseSpeaker responseSpeaker)
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

                prompt = BuildPrompt(userInput, proactive, responseSpeaker);
                OnStatusChanged?.Invoke("Ollama 正在回复...");
                if (speakInput)
                {
                    var speech = FindAnyObjectByType<CompanionSpeechService>();
                    if (inputIsPartner)
                    {
                        speech?.SpeakPartner(userInput);
                    }
                    else
                    {
                        speech?.SpeakPlayer(userInput);
                    }
                }

                rawResponse = await CallOllamaFastAsync(prompt);

                dialogueResponse = ParseResponse(rawResponse, userInput);
                OnResponseReceived?.Invoke(dialogueResponse.text);
                if (responseSpeaker == ResponseSpeaker.Partner)
                {
                    OnDialogueResolved?.Invoke(dialogueResponse);
                }

                if (dialogueResponse.shouldSpeak)
                {
                    var speech = FindAnyObjectByType<CompanionSpeechService>();
                    if (responseSpeaker == ResponseSpeaker.Partner)
                    {
                        speech?.SpeakPartner(dialogueResponse.text);
                    }
                    else
                    {
                        speech?.SpeakPlayer(dialogueResponse.text);
                    }
                }
                
                if (rememberUserInput)
                {
                    memoryManager?.AddMemory(userInput, inputIsPartner ? MemoryType.CharacterReply : MemoryType.UserInput);
                }
                memoryManager?.AddMemory(dialogueResponse.text, responseSpeaker == ResponseSpeaker.Partner ? MemoryType.CharacterReply : MemoryType.UserInput);
                OnStatusChanged?.Invoke($"Ollama {elapsed.ElapsedMilliseconds}ms");

                Debug.Log($"[Dialogue] Response: {dialogueResponse.text}");
                Debug.Log($"[Dialogue] Speaker: {responseSpeaker}");
                Debug.Log($"[Dialogue] Expression: {dialogueResponse.expression}");
                Debug.Log($"[Dialogue] Motion: {dialogueResponse.motion}");
            }
            catch (Exception ex)
            {
                usedFallback = true;
                error = ex.Message;
                dialogueResponse = responseSpeaker == ResponseSpeaker.Partner ? BuildFallbackResponse(userInput) : BuildMaleFallbackResponse(userInput);
                Debug.LogWarning($"[Dialogue] Fallback response: {ex.Message}");
                OnError?.Invoke(ex.Message);
                OnResponseReceived?.Invoke(dialogueResponse.text);
                if (responseSpeaker == ResponseSpeaker.Partner)
                {
                    OnDialogueResolved?.Invoke(dialogueResponse);
                    FindAnyObjectByType<CompanionSpeechService>()?.SpeakPartner(dialogueResponse.text);
                }
                else
                {
                    FindAnyObjectByType<CompanionSpeechService>()?.SpeakPlayer(dialogueResponse.text);
                }

                if (rememberUserInput)
                {
                    memoryManager?.AddMemory(userInput, inputIsPartner ? MemoryType.CharacterReply : MemoryType.UserInput);
                }
                memoryManager?.AddMemory(dialogueResponse.text, responseSpeaker == ResponseSpeaker.Partner ? MemoryType.CharacterReply : MemoryType.UserInput);
                OnStatusChanged?.Invoke($"本地兜底 {elapsed.ElapsedMilliseconds}ms");
            }
            finally
            {
                elapsed.Stop();
                ConversationLogService.WriteTurn(
                    "Ollama",
                    modelName,
                    proactive ? "[女主主动寒暄]" : responseSpeaker == ResponseSpeaker.Player ? $"[女主输入] {userInput}" : userInput,
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
        private string BuildPrompt(string userInput, bool proactive, ResponseSpeaker responseSpeaker)
        {
            string memorySummary = memoryManager?.GetRecentMemoriesSummary(3) ?? "";
            string turnInstruction;
            if (responseSpeaker == ResponseSpeaker.Player)
            {
                turnInstruction = $"当前互动方向：女主对男主。\n女主输入：{userInput}\n请只输出男主对女主的自然回应台词。不要输出女主台词。expression 和 motion 填 neutral/idle。";
            }
            else
            {
                turnInstruction = proactive
                    ? $"当前互动方向：女主对男主。\n场景/动作意图：{userInput}\n请只输出女主对男主说出口的一句自然台词，并给出女主表情和动作字段。"
                    : $"当前互动方向：男主对女主。\n男主输入：{userInput}\n请只输出女主对男主的回应台词，并给出女主表情和动作字段。";
            }
            
            return $@"{systemPrompt}

你正在模拟虚拟伴侣互动。
男主名字叫{playerName}，女主名字叫{characterCard.name}。

男主人设：
{playerPersona}

女主人设：
{partnerPersona}

要求：
- 回复简短（30 字以内）
- 直接回答，不要思考过程
- text 只写要说出口的台词，不要写括号内动作、表情、旁白
- 不要复读上一轮女主回复，也不要照抄用户输入
- 禁止输出 <think>、思考过程、Markdown
- 像正常人聊天一样
- 不要解释，不要长篇大论

补充性格标签：{string.Join(",", characterCard.personality)}
{memorySummary}

用 JSON 回复，expression 只能是 neutral、happy、sad、angry、blush、surprised、relaxed 中的一个，motion 只能是 idle、wave、nod、shake、talk、shy、think、stepCloser、stepBack 中的一个：
{{""text"": ""嗯，我在听。"", ""expression"": ""happy"", ""motion"": ""talk"", ""emotionIntensity"": 0.6, ""shouldSpeak"": true}}

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
                    keep_alive = "30m",
                    format = "json",
                    messages = new[]
                    {
                        new OllamaChatMessage { role = "user", content = prompt }
                    },
                    stream = false,
                    think = false,
                    options = new OllamaOptions
                    {
                        temperature = 0.65f,
                        top_p = 0.9f,
                        repeat_penalty = 1.18f,
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

        private async Task WarmupModelAsync()
        {
            if (modelWarmed || string.IsNullOrWhiteSpace(modelName))
            {
                return;
            }

            try
            {
                OnStatusChanged?.Invoke($"正在加载模型 {modelName}...");
                using (var www = UnityWebRequest.Put($"{ollamaUrl}/api/chat", ""))
                {
                    var request = new OllamaChatRequest
                    {
                        model = modelName,
                        keep_alive = "30m",
                        messages = new[]
                        {
                            new OllamaChatMessage { role = "user", content = "只回复 OK" }
                        },
                        stream = false,
                        think = false,
                        options = new OllamaOptions
                        {
                            temperature = 0f,
                            num_predict = 2
                        }
                    };

                    var body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
                    www.uploadHandler = new UploadHandlerRaw(body);
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");
                    www.method = UnityWebRequest.kHttpVerbPOST;
                    www.timeout = warmupTimeoutSeconds;

                    await www.SendWebRequest();
                    modelWarmed = www.result == UnityWebRequest.Result.Success;
                    OnStatusChanged?.Invoke(modelWarmed ? $"模型已就绪：{modelName}" : $"模型预热失败：{www.error}");
                    Debug.Log(modelWarmed ? $"[Dialogue] Model warmed: {modelName}" : $"[Dialogue] Model warmup failed: {www.error}");
                }
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"模型预热失败：{ex.Message}");
                Debug.LogWarning($"[Dialogue] Model warmup failed: {ex.Message}");
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
            var hasJsonObject = startIndex >= 0 && endIndex > startIndex;
            
            if (hasJsonObject)
            {
                json = json.Substring(startIndex, endIndex - startIndex + 1);
            }
            
            try
            {
                if (!hasJsonObject)
                {
                    return BuildSafeFallbackFromRaw(json, userInput);
                }

                var response = JsonUtility.FromJson<DialogueResponse>(json);
                if (response == null)
                {
                    response = BuildFallbackResponse(userInput);
                }
                NormalizeResponse(response, userInput);
                lastReplyText = response.text;
                return response;
            }
            catch
            {
                var fallback = BuildFallbackResponse(userInput);
                fallback.text = ExtractLooseText(json, fallback.text);
                NormalizeResponse(fallback, userInput);
                lastReplyText = fallback.text;
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

        private static string ExtractLooseText(string raw, string defaultText)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultText;
            }

            var match = Regex.Match(raw, "\"text\"\\s*:\\s*\"(?<text>[^\"]*)\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups["text"].Value;
            }

            return LooksLikeModelAnalysis(raw) ? defaultText : raw;
        }

        private void NormalizeResponse(DialogueResponse response, string userInput)
        {
            if (response == null)
            {
                return;
            }

            response.text = SanitizeSpokenText(response.text);
            if (string.IsNullOrWhiteSpace(response.text))
            {
                response.text = "我听着呢。";
            }

            if (LooksLikeModelAnalysis(response.text))
            {
                var replacement = BuildFallbackResponse(userInput);
                response.text = replacement.text;
                response.expression = replacement.expression;
                response.motion = replacement.motion;
            }

            if (IsRepeating(response.text, userInput))
            {
                var replacement = BuildNonRepeatingFallbackResponse(userInput);
                response.text = replacement.text;
                response.expression = replacement.expression;
                response.motion = replacement.motion;
            }

            if (!IsValidExpression(response.expression))
            {
                response.expression = InferExpression(userInput, response.text);
            }

            if (!IsValidMotion(response.motion))
            {
                response.motion = InferMotion(userInput, response.text);
            }

            if (response.emotionIntensity <= 0f)
            {
                response.emotionIntensity = 0.6f;
            }
        }

        private DialogueResponse BuildSafeFallbackFromRaw(string raw, string userInput)
        {
            var fallback = BuildFallbackResponse(userInput);
            if (!LooksLikeModelAnalysis(raw))
            {
                var text = SanitizeSpokenText(raw);
                if (!string.IsNullOrWhiteSpace(text) && text.Length <= 45)
                {
                    fallback.text = text;
                }
            }

            NormalizeResponse(fallback, userInput);
            lastReplyText = fallback.text;
            return fallback;
        }

        private static bool LooksLikeModelAnalysis(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.Contains("首先")
                || text.Contains("用户指定")
                || text.Contains("用户输入")
                || text.Contains("我需要")
                || text.Contains("根据要求")
                || text.Contains("在模拟中")
                || text.Contains("所以")
                || text.Contains("JSON")
                || text.Length > 80;
        }

        public static string SanitizeSpokenText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text = CleanModelOutput(text);
            text = Regex.Replace(text, "[（(][^（）()]*[）)]", "");
            text = Regex.Replace(text, "^\\s*(女主|角色|回复|text)\\s*[:：]\\s*", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "\\s+", " ").Trim();
            text = text.Trim('"', '\'', '“', '”', '‘', '’');
            return text.Trim();
        }

        private bool IsRepeating(string reply, string userInput)
        {
            var normalizedReply = NormalizeForCompare(reply);
            if (string.IsNullOrEmpty(normalizedReply))
            {
                return false;
            }

            return normalizedReply == NormalizeForCompare(lastReplyText)
                || normalizedReply == NormalizeForCompare(userInput);
        }

        private static string NormalizeForCompare(string text)
        {
            text = SanitizeSpokenText(text);
            text = Regex.Replace(text, "[\\s。！？!?,，…~～\\.]", "");
            return text.ToLowerInvariant();
        }

        private static bool IsValidExpression(string expression)
        {
            return expression == "neutral"
                || expression == "happy"
                || expression == "sad"
                || expression == "angry"
                || expression == "blush"
                || expression == "surprised"
                || expression == "relaxed";
        }

        private static bool IsValidMotion(string motion)
        {
            return motion == "idle"
                || motion == "wave"
                || motion == "nod"
                || motion == "shake"
                || motion == "talk"
                || motion == "shy"
                || motion == "think"
                || motion == "stepCloser"
                || motion == "stepBack";
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

        private DialogueResponse BuildMaleFallbackResponse(string femaleInput)
        {
            var normalized = (femaleInput ?? string.Empty).ToLowerInvariant();

            if (normalized.Contains("你好") || normalized.Contains("打招呼"))
            {
                return NewResponse("嗯，我在呢。看到你主动说话，我很开心。", "neutral", "idle");
            }

            if (normalized.Contains("可爱") || normalized.Contains("脸红") || normalized.Contains("害羞"))
            {
                return NewResponse("你这样说，我也会有点不好意思。", "neutral", "idle");
            }

            if (normalized.Contains("坐") || normalized.Contains("休息") || normalized.Contains("床边"))
            {
                return NewResponse("好，我们慢慢坐一会儿，不着急。", "neutral", "idle");
            }

            return NewResponse("嗯，我听见了，你继续说。", "neutral", "idle");
        }

        private DialogueResponse BuildNonRepeatingFallbackResponse(string userInput)
        {
            var primary = BuildFallbackResponse(userInput);
            if (!IsRepeating(primary.text, userInput))
            {
                return primary;
            }

            var alternatives = new[]
            {
                NewResponse("那我就偷偷开心一下。", "blush", "shy"),
                NewResponse("嗯，我想听你继续说。", "happy", "talk"),
                NewResponse("你这样说，我会记住的。", "relaxed", "nod"),
                NewResponse("好呀，我在你身边。", "happy", "stepCloser")
            };

            foreach (var alternative in alternatives)
            {
                if (!IsRepeating(alternative.text, userInput))
                {
                    return alternative;
                }
            }

            return NewResponse("嗯，我在认真听。", "neutral", "talk");
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
                ? "你正在驱动一个虚拟伴侣 Unity 原型。根据当前互动方向输出指定角色的台词和动作意图，不要输出旁白、思考过程或解释。"
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
        public string keep_alive;
        public string format;
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
        public float top_p;
        public float repeat_penalty;
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
