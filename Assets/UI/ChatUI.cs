using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

namespace VRDemo.UI
{
    /// <summary>
    /// 聊天 UI - 处理用户界面交互
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text messageList;
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_Text statusText;
        
        [Header("系统引用")]
        [SerializeField] private Core.DialogueSystem dialogueSystem;
        [SerializeField] private VRM.VRMController vrmController;
        
        private bool isProcessing = false;
        
        private void Start()
        {
            sendButton.onClick.AddListener(OnSendClicked);
            inputField.onEndEdit.AddListener(OnInputEndEdit);
            
            // 绑定事件
            dialogueSystem.OnResponseReceived += OnResponseReceived;
            dialogueSystem.OnError += OnError;
            
            vrmController.OnExpressionChanged += exp => UpdateStatus($"表情：{exp}");
            vrmController.OnMotionChanged += motion => UpdateStatus($"动作：{motion}");
            
            UpdateStatus("就绪");
        }
        
        private void OnSendClicked()
        {
            SendMessage();
        }
        
        private void OnInputEndEdit(string text)
        {
            // 按 Enter 发送
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
            {
                SendMessage();
            }
        }
        
        private async void SendMessage()
        {
            if (isProcessing) return;
            
            string text = inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            
            isProcessing = true;
            inputField.interactable = false;
            sendButton.interactable = false;
            
            // 显示用户消息
            AppendMessage($"<color=#4A90D9>你：</color>{text}");
            UpdateStatus("思考中...");
            
            // 发送到对话系统
            await dialogueSystem.SendAsync(text);
            
            inputField.text = "";
            inputField.interactable = true;
            sendButton.interactable = true;
            isProcessing = false;
        }
        
        private void OnResponseReceived(string response)
        {
            AppendMessage($"<color=#E8A86C>桂言叶：</color>{response}");
            UpdateStatus("等待输入...");
        }
        
        private void OnError(string error)
        {
            AppendMessage($"<color=#E74C3C>错误：</color>{error}");
            UpdateStatus("发生错误");
        }
        
        private void AppendMessage(string message)
        {
            messageList.text += message + "\n\n";
            
            // 自动滚动到底部
            var scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0;
            }
        }
        
        private void UpdateStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }
        }
        
        private void OnDestroy()
        {
            dialogueSystem.OnResponseReceived -= OnResponseReceived;
            dialogueSystem.OnError -= OnError;
        }
    }
}
