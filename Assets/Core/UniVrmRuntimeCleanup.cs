using System;
using UnityEngine;

namespace VRDemo.Core
{
    /// <summary>
    /// Releases UniVRM global runtime services that allocate native SpringBone buffers.
    /// </summary>
    public sealed class UniVrmRuntimeCleanup : MonoBehaviour
    {
        private static UniVrmRuntimeCleanup instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstalled()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("UniVRM Runtime Cleanup");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<UniVrmRuntimeCleanup>();
        }

        private void OnApplicationQuit()
        {
            ReleaseFastSpringBoneService();
        }

        private void OnDestroy()
        {
            ReleaseFastSpringBoneService();
        }

        private static void ReleaseFastSpringBoneService()
        {
            var type = Type.GetType("UniVRM10.FastSpringBones.FastSpringBoneService, FastSpringBone10");
            var free = type?.GetMethod("Free", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            try
            {
                free?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRM] Could not release FastSpringBoneService: {ex.Message}");
            }
        }
    }
}
