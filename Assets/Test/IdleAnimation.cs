using UnityEngine;

namespace VRDemo.Test
{
    /// <summary>
    /// 简单待机动画 - 让角色自然呼吸 + 轻微晃动
    /// </summary>
    public class IdleAnimation : MonoBehaviour
    {
        [Header("呼吸设置")]
        [SerializeField] private float breathSpeed = 1f;
        [SerializeField] private float breathAmount = 0.02f;
        
        [Header("轻微晃动")]
        [SerializeField] private float swaySpeed = 0.5f;
        [SerializeField] private float swayAmount = 0.5f;
        
        private float breathTimer;
        private float swayTimer;
        private Vector3 originalPosition;
        
        private void Start()
        {
            originalPosition = transform.position;
            breathTimer = Random.Range(0f, Mathf.PI * 2);
            swayTimer = Random.Range(0f, Mathf.PI * 2);
        }
        
        private void Update()
        {
            // 呼吸效果（Y 轴轻微上下）
            breathTimer += Time.deltaTime * breathSpeed;
            float breathY = Mathf.Sin(breathTimer) * breathAmount;
            
            // 轻微晃动（XZ 轴轻微移动）
            swayTimer += Time.deltaTime * swaySpeed;
            float swayX = Mathf.Cos(swayTimer) * swayAmount * 0.01f;
            float swayZ = Mathf.Sin(swayTimer) * swayAmount * 0.01f;
            
            transform.position = originalPosition + new Vector3(swayX, breathY, swayZ);
        }
    }
}
