using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.UI;
using TMPro;
using VRDemo.World;

public class VirtualPartnerSetup : EditorWindow
{
    [MenuItem("VirtualPartner/一键设置场景 _F1")]
    public static void SetupScene()
    {
        Debug.Log("=== VirtualPartner 一键设置开始 ===");
        
        // 1. 创建新场景
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Debug.Log($"当前场景：{scene.name}");
        
        // 2. 添加相机
        SetupCamera();
        
        // 3. 添加灯光
        SetupLighting();
        
        // 4. 生成房间
        CreateRoom();
        
        // 5. 添加 UI
        SetupUI();
        
        // 6. 添加 GameManager
        SetupGameManager();
        
        // 7. 尝试加载 partner 模型
        LoadPartnerModel();
        
        Debug.Log("=== VirtualPartner 一键设置完成！按 F1 重新打开此窗口 ===");
        Debug.Log("现在点击 ▶ Play 按钮开始测试！");
    }
    
    private static void SetupCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
            mainCam = camObj.GetComponent<Camera>();
        }
        
        mainCam.transform.position = new Vector3(0, 1.5f, 2);
        mainCam.transform.rotation = Quaternion.Euler(0, 180, 0);
        mainCam.fieldOfView = 60;
        
        Debug.Log("✓ 相机设置完成");
    }
    
    private static void SetupLighting()
    {
        // 删除现有灯光
        var existingLights = Object.FindObjectsByType<Light>();
        foreach (var existingLight in existingLights)
        {
            if (existingLight.gameObject.name.Contains("Light") || existingLight.gameObject.name.Contains("Sun"))
            {
                GameObject.DestroyImmediate(existingLight.gameObject);
            }
        }
        
        // 添加新灯光
        GameObject lightObj = new GameObject("Directional Light");
        Light directionalLight = lightObj.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.2f;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        
        // 添加环境光
        GameObject pointLight = new GameObject("Room Light");
        Light pl = pointLight.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.intensity = 1.5f;
        pl.range = 15f;
        pointLight.transform.position = new Vector3(0, 2.5f, 0);
        
        Debug.Log("✓ 灯光设置完成");
    }
    
    private static void CreateRoom()
    {
        var existingRoom = GameObject.Find("SetupGeneratedRoom");
        if (existingRoom != null)
        {
            GameObject.DestroyImmediate(existingRoom);
        }

        GameObject roomRoot = new GameObject("SetupGeneratedRoom");

        // 地板
        CreatePlane("Floor", new Color(0.6f, 0.55f, 0.5f, 1f), 10, 10, new Vector3(0, 0, 0), Quaternion.Euler(90, 0, 0), roomRoot.transform);
        
        // 天花板
        CreatePlane("Ceiling", new Color(0.9f, 0.9f, 0.9f, 1f), 10, 10, new Vector3(0, 3, 0), Quaternion.Euler(-90, 0, 0), roomRoot.transform);
        
        // 后墙
        CreatePlane("BackWall", new Color(0.85f, 0.85f, 0.85f, 1f), 10, 3, new Vector3(0, 1.5f, -5), Quaternion.Euler(0, 0, 0), roomRoot.transform);
        
        // 左墙
        CreatePlane("LeftWall", new Color(0.85f, 0.85f, 0.85f, 1f), 10, 3, new Vector3(-5, 1.5f, 0), Quaternion.Euler(0, 90, 0), roomRoot.transform);
        
        // 右墙
        CreatePlane("RightWall", new Color(0.85f, 0.85f, 0.85f, 1f), 10, 3, new Vector3(5, 1.5f, 0), Quaternion.Euler(0, -90, 0), roomRoot.transform);

        StylizedHouseBuilder.CreateOrReplaceHouse(new Vector3(-2.6f, 0f, -2.1f), roomRoot.transform);
        
        Debug.Log("✓ 房间和二次元风格房子生成完成");
    }
    
    private static void CreatePlane(string name, Color color, float width, float height, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = name;
        plane.transform.SetParent(parent, false);
        plane.transform.localPosition = position;
        plane.transform.localRotation = rotation;
        plane.transform.localScale = new Vector3(width / 10f, 1f, height / 10f);
        
        Renderer renderer = plane.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.material = mat;
    }
    
    private static void SetupUI()
    {
        // 删除现有 Canvas
        var existingCanvas = GameObject.Find("Canvas");
        if (existingCanvas != null)
        {
            GameObject.DestroyImmediate(existingCanvas);
        }
        
        // 创建 Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.sizeDelta = Vector2.zero;
        
        // 创建输入框
        GameObject inputObj = CreateInputField(canvasObj);
        
        // 创建回复文本
        GameObject textObj = CreateResponseText(canvasObj);
        
        Debug.Log("✓ UI 设置完成");
    }
    
    private static GameObject CreateInputField(GameObject parent)
    {
        var fontAsset = TMP_Settings.defaultFontAsset;
        if (fontAsset == null)
        {
            Debug.LogWarning("⚠ TMP 默认字体未配置，跳过测试输入框创建，避免生成损坏的 TMP 组件");
            return null;
        }

        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(parent.transform, false);
        
        RectTransform rect = inputObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 50);
        rect.sizeDelta = new Vector2(600, 50);
        
        // 添加图片背景
        Image img = inputObj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.92f);
        
        // 添加 TMP 输入框
        var tmpInput = inputObj.AddComponent<TMPro.TMP_InputField>();
        tmpInput.lineType = TMP_InputField.LineType.SingleLine;
        tmpInput.pointSize = 24f;
        tmpInput.customCaretColor = true;
        tmpInput.caretColor = Color.black;
        tmpInput.selectionColor = new Color(0.2f, 0.45f, 0.95f, 0.35f);

        var textViewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textViewport.transform.SetParent(inputObj.transform, false);
        var viewportRect = textViewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(16f, 8f);
        viewportRect.offsetMax = new Vector2(-16f, -8f);

        var textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(textViewport.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.font = fontAsset;
        inputText.fontSize = 24f;
        inputText.color = Color.black;
        inputText.textWrappingMode = TextWrappingModes.NoWrap;
        inputText.alignment = TextAlignmentOptions.MidlineLeft;

        var placeholderObj = new GameObject("Placeholder", typeof(RectTransform));
        placeholderObj.transform.SetParent(textViewport.transform, false);
        var placeholderRect = placeholderObj.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        var placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholder.font = fontAsset;
        placeholder.fontSize = 24f;
        placeholder.text = "输入消息后按 Enter";
        placeholder.color = new Color(0f, 0f, 0f, 0.45f);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;

        tmpInput.textViewport = viewportRect;
        tmpInput.textComponent = inputText;
        tmpInput.placeholder = placeholder;
        
        Debug.Log("  - 输入框已创建");
        return inputObj;
    }
    
    private static GameObject CreateResponseText(GameObject parent)
    {
        var fontAsset = TMP_Settings.defaultFontAsset;
        if (fontAsset == null)
        {
            Debug.LogWarning("⚠ TMP 默认字体未配置，跳过测试回复文本创建，避免生成损坏的 TMP 组件");
            return null;
        }

        GameObject textObj = new GameObject("ResponseText");
        textObj.transform.SetParent(parent.transform, false);
        
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, 100);
        rect.sizeDelta = new Vector2(600, 200);
        
        // 添加 TMP 文本
        var tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmpText.font = fontAsset;
        tmpText.text = "点击输入框，输入消息后按 Enter 发送...";
        tmpText.fontSize = 24;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
        
        Debug.Log("  - 回复文本已创建");
        return textObj;
    }
    
    private static void SetupGameManager()
    {
        // 删除现有 GameManager
        var existingGM = GameObject.Find("GameManager");
        if (existingGM != null)
        {
            GameObject.DestroyImmediate(existingGM);
        }
        
        // 创建 GameManager
        GameObject gmObj = new GameObject("GameManager");
        // 稍后添加 GameManager 脚本
        
        Debug.Log("✓ GameManager 已创建（需要手动添加脚本）");
    }
    
    private static void LoadPartnerModel()
    {
        string vrmPath = "Assets/Resources/Models/Characters/partner.vrm";
        if (File.Exists(vrmPath))
        {
            Debug.Log($"✓ 找到 partner.vrm，请手动拖拽到场景中，位置设为 (0, 0, 4)");
        }
        else
        {
            Debug.LogWarning("⚠ 未找到 partner.vrm，请确保文件在 Assets/Resources/Models/Characters/ 目录下");
        }
    }
    
    [MenuItem("VirtualPartner/检查 Ollama 状态 _F2")]
    public static void CheckOllama()
    {
        System.Diagnostics.Process proc = new System.Diagnostics.Process();
        proc.StartInfo.FileName = "ollama";
        proc.StartInfo.Arguments = "list";
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        
        try
        {
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            
            if (output.Contains("qwen3.5"))
            {
                Debug.Log("✓ Ollama 正常，qwen3.5 模型已安装");
            }
            else
            {
                Debug.LogWarning("⚠ Ollama 未运行或 qwen3.5 未安装");
                Debug.Log("运行：ollama pull qwen3.5:2b");
            }
        }
        catch
        {
            Debug.LogError("✗ Ollama 未安装或未在 PATH 中");
        }
    }
}
