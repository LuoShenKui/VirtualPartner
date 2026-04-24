using UnityEngine;
using TMPro;
using VRDemo.Core;

namespace VRDemo.Test
{
    /// <summary>
    /// 简单对话测试 - 按 Enter 发送消息
    /// </summary>
    public class QuickChatTest : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text responseText;
        
        private DialogueSystem dialogueSystem;
        
        private void Start()
        {
            dialogueSystem = FindAnyObjectByType<DialogueSystem>();
            
            if (dialogueSystem != null)
            {
                dialogueSystem.OnResponseReceived += OnResponse;
                dialogueSystem.OnError += OnError;
            }
            
            Debug.Log("[QuickChat] Ready! Type and press Enter to chat.");
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) && !string.IsNullOrEmpty(inputField.text))
            {
                SendMessage();
            }
        }
        
        private void SendMessage()
        {
            string message = inputField.text;
            Debug.Log($"[QuickChat] Sending: {message}");
            
            dialogueSystem?.SendAsync(message);
            
            inputField.text = "";
        }
        
        private void OnResponse(string response)
        {
            responseText.text = response;
            Debug.Log($"[QuickChat] Response: {response}");
        }
        
        private void OnError(string error)
        {
            responseText.text = $"Error: {error}";
            Debug.LogError($"[QuickChat] Error: {error}");
        }
        
        private void OnDestroy()
        {
            if (dialogueSystem != null)
            {
                dialogueSystem.OnResponseReceived -= OnResponse;
                dialogueSystem.OnError -= OnError;
            }
        }
    }
}
