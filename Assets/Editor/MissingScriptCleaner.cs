#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRDemo.EditorTools
{
    [InitializeOnLoad]
    public static class MissingScriptCleaner
    {
        static MissingScriptCleaner()
        {
            EditorApplication.delayCall += CleanOpenScenesOnce;
        }

        [MenuItem("Tools/VirtualPartner/Clean Missing Scripts")]
        public static void CleanAllProjectAssets()
        {
            var removed = 0;
            removed += CleanOpenScenes();
            removed += CleanPrefabs();
            Debug.Log($"[Cleanup] Removed {removed} missing script component(s).");
        }

        private static void CleanOpenScenesOnce()
        {
            var removed = CleanOpenScenes();
            if (removed > 0)
            {
                Debug.Log($"[Cleanup] Removed {removed} missing script component(s) from open scene(s).");
            }
        }

        private static int CleanOpenScenes()
        {
            var removed = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    removed += CleanHierarchy(root);
                }

                if (removed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            return removed;
        }

        private static int CleanPrefabs()
        {
            var removed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var count = CleanHierarchy(root);
                    if (count <= 0)
                    {
                        continue;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    removed += count;
                    Debug.Log($"[Cleanup] Removed {count} missing script component(s): {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return removed;
        }

        private static int CleanHierarchy(GameObject root)
        {
            var removed = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            }

            return removed;
        }
    }
}
#endif
