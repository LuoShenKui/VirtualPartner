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
        private static bool released;

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
            if (!Application.isPlaying)
            {
                ReleaseFastSpringBoneService();
            }
        }

        private static void ReleaseFastSpringBoneService()
        {
            if (released)
            {
                return;
            }

            var type = Type.GetType("UniVRM10.FastSpringBones.FastSpringBoneService, FastSpringBone10");
            var free = type?.GetMethod("Free", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            try
            {
                if (free == null)
                {
                    return;
                }

                free.Invoke(null, null);
                released = true;
            }
            catch (Exception ex)
            {
                var root = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                    ? tie.InnerException
                    : ex;
                if (root is NullReferenceException)
                {
                    released = true;
                    return;
                }

                Debug.LogWarning($"[VRM] Could not release FastSpringBoneService: {root.GetType().Name}: {root.Message}");
            }
        }
    }
}
