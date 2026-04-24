using UnityEngine;
using VRDemo.World;

namespace VRDemo.Test
{
    /// <summary>
    /// 一键生成简单房间 - 按空格键生成
    /// </summary>
    [ExecuteAlways]
    public class SimpleRoomGenerator : MonoBehaviour
    {
        [Header("房间尺寸")]
        [SerializeField] private float width = 10f;
        [SerializeField] private float height = 3f;
        [SerializeField] private float depth = 10f;
        [SerializeField] private Color wallColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private Color floorColor = new Color(0.7f, 0.65f, 0.6f, 1f);
        [SerializeField] private Color ceilingColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        [Header("生成选项")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool includeStylizedHouse = true;
        [SerializeField] private Vector3 housePosition = new Vector3(-2.6f, 0f, -2.1f);
        
        private void Start()
        {
            if (generateOnStart)
            {
                CreateRoom();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                CreateRoom();
            }
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                CreateRoom();
            }
        }
        
        [ContextMenu("Create Room")]
        public void CreateRoom()
        {
            GameObject existingRoom = GameObject.Find("GeneratedRoom");
            if (existingRoom != null)
            {
                DestroySafely(existingRoom);
            }

            GameObject roomRoot = new GameObject("GeneratedRoom");
            roomRoot.transform.SetParent(transform, false);
            
            // 地板
            CreatePlane("Floor", floorColor, width, depth, new Vector3(0, 0, 0), Quaternion.Euler(90, 0, 0), roomRoot.transform);
            
            // 天花板
            CreatePlane("Ceiling", ceilingColor, width, depth, new Vector3(0, height, 0), Quaternion.Euler(-90, 0, 0), roomRoot.transform);
            
            // 后墙
            CreatePlane("BackWall", wallColor, width, height, new Vector3(0, height / 2, -depth / 2), Quaternion.Euler(0, 0, 0), roomRoot.transform);
            
            // 左墙
            CreatePlane("LeftWall", wallColor, depth, height, new Vector3(-width / 2, height / 2, 0), Quaternion.Euler(0, 90, 0), roomRoot.transform);
            
            // 右墙
            CreatePlane("RightWall", wallColor, depth, height, new Vector3(width / 2, height / 2, 0), Quaternion.Euler(0, -90, 0), roomRoot.transform);

            if (includeStylizedHouse)
            {
                StylizedHouseBuilder.CreateOrReplaceHouse(housePosition, roomRoot.transform);
            }
            
            // 添加一个点光源
            GameObject light = new GameObject("Room Light");
            light.transform.SetParent(roomRoot.transform, false);
            light.transform.localPosition = new Vector3(0, height - 0.5f, 0);
            Light l = light.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = 1.5f;
            l.range = 15f;
            
            Debug.Log("[Room] Room created! Press Space to regenerate.");
        }
        
        private void CreatePlane(string name, Color color, float w, float h, Vector3 pos, Quaternion rot, Transform parent)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = name;
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = pos;
            plane.transform.localRotation = rot;
            plane.transform.localScale = new Vector3(w / 10f, 1f, h / 10f);
            
            Renderer renderer = plane.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;
        }

        private void DestroySafely(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
