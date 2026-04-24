using UnityEngine;
using UnityEditor;
using VRDemo.World;

public class QuickRoomCreator : EditorWindow
{
    [MenuItem("VirtualPartner/创建简单房间 _F3")]
    public static void CreateRoom()
    {
        GameObject roomRoot = CreateOrReplaceRoot("QuickGeneratedRoom");

        // 地板
        CreatePrimitive("Floor", PrimitiveType.Plane, roomRoot.transform, new Vector3(0, 0, 0), Quaternion.Euler(90, 0, 0), new Vector3(1, 1, 1));
        
        // 天花板
        CreatePrimitive("Ceiling", PrimitiveType.Plane, roomRoot.transform, new Vector3(0, 3, 0), Quaternion.Euler(-90, 0, 0), new Vector3(1, 1, 1));
        
        // 后墙
        CreatePrimitive("BackWall", PrimitiveType.Cube, roomRoot.transform, new Vector3(0, 1.5f, -5), Quaternion.identity, new Vector3(10, 3, 0.2f));
        
        // 左墙
        CreatePrimitive("LeftWall", PrimitiveType.Cube, roomRoot.transform, new Vector3(-5, 1.5f, 0), Quaternion.identity, new Vector3(0.2f, 3, 10));
        
        // 右墙
        CreatePrimitive("RightWall", PrimitiveType.Cube, roomRoot.transform, new Vector3(5, 1.5f, 0), Quaternion.identity, new Vector3(0.2f, 3, 10));

        StylizedHouseBuilder.CreateOrReplaceHouse(new Vector3(-2.6f, 0f, -2.1f), roomRoot.transform);
        
        // 光源
        GameObject light = new GameObject("Room Light");
        light.transform.SetParent(roomRoot.transform, false);
        light.transform.localPosition = new Vector3(0, 2.5f, 0);
        Light l = light.AddComponent<Light>();
        l.type = LightType.Point;
        l.intensity = 2f;
        l.range = 15f;
        
        Debug.Log("✅ 房间和二次元风格房子创建完成！");
        Debug.Log("现在拖入 partner.vrm 到场景，位置 (0, 0, 4)");
        Debug.Log("然后点击 ▶ Play 测试！");
    }

    [MenuItem("VirtualPartner/创建二次元风格房子 _F4")]
    public static void CreateStylizedHouseOnly()
    {
        StylizedHouseBuilder.CreateOrReplaceHouse(new Vector3(-2.6f, 0f, -2.1f));
        Debug.Log("✅ 二次元风格房子已创建！");
    }
    
    static GameObject CreateOrReplaceRoot(string rootName)
    {
        GameObject existing = GameObject.Find(rootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Quick Room");
        return root;
    }

    static void CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localRotation = rot;
        obj.transform.localScale = scale;
        
        // 设置简单材质
        Renderer ren = obj.GetComponent<Renderer>();
        if (ren != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            if (name == "Floor") mat.color = new Color(0.6f, 0.55f, 0.5f, 1f);
            else if (name == "Ceiling") mat.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            else mat.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            ren.material = mat;
        }
    }
}
