using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRDemo.Test;

[InitializeOnLoad]
public static class SceneEnvironmentBootstrap
{
    static SceneEnvironmentBootstrap()
    {
        EditorApplication.delayCall += GenerateEnvironmentForOpenScene;
    }

    private static void GenerateEnvironmentForOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var generators = Object.FindObjectsByType<SimpleRoomGenerator>();
        foreach (var generator in generators)
        {
            if (generator != null && generator.isActiveAndEnabled && generator.gameObject.scene.IsValid())
            {
                generator.CreateRoom();
            }
        }
    }

    [MenuItem("VirtualPartner/保存房间和二次元房子到当前场景")]
    public static void SaveEnvironmentToCurrentScene()
    {
        GenerateEnvironmentForOpenScene();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    public static void GenerateAndSaveVirtualPartnerScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/VirtualPartnerScene.unity");
        GenerateEnvironmentForOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
}
