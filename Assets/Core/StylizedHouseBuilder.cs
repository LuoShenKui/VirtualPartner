using UnityEngine;

namespace VRDemo.World
{
    /// <summary>
    /// 用基础几何体快速拼出一座偏二次元配色的小屋。
    /// </summary>
    public static class StylizedHouseBuilder
    {
        private const string DefaultHouseName = "StylizedAnimeHouse";

        public static GameObject CreateOrReplaceHouse(Vector3 position, Transform parent = null, string rootName = DefaultHouseName)
        {
            Transform existing = parent != null ? parent.Find(rootName) : GameObject.Find(rootName)?.transform;
            if (existing != null)
            {
                DestroySafely(existing.gameObject);
            }

            GameObject root = new GameObject(rootName);
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
                root.transform.localPosition = position;
            }
            else
            {
                root.transform.position = position;
            }

            CreateCube("Base", root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(3.6f, 0.24f, 3.2f), new Color(0.88f, 0.83f, 0.92f));
            CreateCube("MainBody", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(2.45f, 2.1f, 2.1f), new Color(0.99f, 0.92f, 0.94f));
            CreateCube("UpperBody", root.transform, new Vector3(0f, 2.55f, -0.05f), new Vector3(1.8f, 1.2f, 1.55f), new Color(1f, 0.97f, 0.86f));
            CreateCube("Porch", root.transform, new Vector3(0f, 0.42f, 1.25f), new Vector3(1.3f, 0.12f, 0.9f), new Color(0.88f, 0.73f, 0.78f));
            CreateCube("Door", root.transform, new Vector3(0f, 0.95f, 1.06f), new Vector3(0.62f, 1.46f, 0.12f), new Color(0.55f, 0.31f, 0.29f));
            CreateCube("DoorFrame", root.transform, new Vector3(0f, 1.02f, 1.12f), new Vector3(0.82f, 1.62f, 0.05f), new Color(1f, 0.93f, 0.96f));

            CreateWindow(root.transform, new Vector3(-0.82f, 1.25f, 1.08f));
            CreateWindow(root.transform, new Vector3(0.82f, 1.25f, 1.08f));
            CreateWindow(root.transform, new Vector3(0f, 2.55f, 0.76f), new Vector3(0.72f, 0.72f, 0.12f));

            CreateCube("LeftRoof", root.transform, new Vector3(-0.56f, 3.38f, 0f), new Vector3(1.95f, 0.2f, 2.65f), new Color(0.34f, 0.63f, 0.67f), new Vector3(0f, 0f, 32f));
            CreateCube("RightRoof", root.transform, new Vector3(0.56f, 3.38f, 0f), new Vector3(1.95f, 0.2f, 2.65f), new Color(0.34f, 0.63f, 0.67f), new Vector3(0f, 0f, -32f));
            CreateCube("RoofRidge", root.transform, new Vector3(0f, 3.72f, 0f), new Vector3(0.18f, 0.18f, 2.5f), new Color(0.28f, 0.49f, 0.52f));
            CreateCube("Chimney", root.transform, new Vector3(0.9f, 3.95f, -0.35f), new Vector3(0.35f, 1.05f, 0.35f), new Color(0.82f, 0.58f, 0.54f));

            CreateCube("Awning", root.transform, new Vector3(0f, 1.88f, 1.16f), new Vector3(2.25f, 0.08f, 0.62f), new Color(0.98f, 0.67f, 0.7f), new Vector3(18f, 0f, 0f));
            CreateCube("Sign", root.transform, new Vector3(0f, 2.08f, 1.19f), new Vector3(1.15f, 0.22f, 0.06f), new Color(1f, 0.94f, 0.8f));

            CreateSphere("BushLeft", root.transform, new Vector3(-1.28f, 0.45f, 1.08f), new Vector3(0.78f, 0.68f, 0.68f), new Color(0.56f, 0.8f, 0.58f));
            CreateSphere("BushRight", root.transform, new Vector3(1.28f, 0.45f, 1.08f), new Vector3(0.78f, 0.68f, 0.68f), new Color(0.56f, 0.8f, 0.58f));
            CreateCylinder("LampPost", root.transform, new Vector3(-1.68f, 0.76f, 1.22f), new Vector3(0.1f, 0.75f, 0.1f), new Color(0.9f, 0.84f, 0.68f));
            CreateSphere("LampLight", root.transform, new Vector3(-1.68f, 1.65f, 1.22f), new Vector3(0.28f, 0.28f, 0.28f), new Color(1f, 0.95f, 0.7f), new Color(0.34f, 0.28f, 0.08f) * 0.8f);

            root.transform.localScale = Vector3.one * 0.85f;
            return root;
        }

        private static void CreateWindow(Transform parent, Vector3 localPosition, Vector3? size = null)
        {
            Vector3 windowSize = size ?? new Vector3(0.58f, 0.82f, 0.12f);
            CreateCube("WindowFrame", parent, localPosition, windowSize + new Vector3(0.14f, 0.14f, -0.05f), new Color(1f, 0.95f, 0.97f));
            CreateCube("WindowGlass", parent, localPosition, windowSize, new Color(0.69f, 0.88f, 0.98f), Vector3.zero, new Color(0.1f, 0.14f, 0.18f) * 0.45f);
            CreateCube("WindowCrossVertical", parent, localPosition, new Vector3(0.08f, windowSize.y, 0.05f), new Color(0.97f, 0.88f, 0.91f));
            CreateCube("WindowCrossHorizontal", parent, localPosition, new Vector3(windowSize.x, 0.08f, 0.05f), new Color(0.97f, 0.88f, 0.91f));
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, Vector3? localEulerAngles = null, Color? emissionColor = null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SetupPrimitive(cube, name, parent, localPosition, localScale, color, localEulerAngles, emissionColor);
            return cube;
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, Color? emissionColor = null)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SetupPrimitive(sphere, name, parent, localPosition, localScale, color, null, emissionColor);
            return sphere;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            SetupPrimitive(cylinder, name, parent, localPosition, localScale, color, null, null);
            return cylinder;
        }

        private static void SetupPrimitive(GameObject obj, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, Vector3? localEulerAngles, Color? emissionColor)
        {
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            obj.transform.localRotation = Quaternion.Euler(localEulerAngles ?? Vector3.zero);

            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateMaterial(color, emissionColor);
            }
        }

        private static Material CreateMaterial(Color color, Color? emissionColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                color = color
            };

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }

            if (emissionColor.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor.Value);
            }

            return material;
        }

        private static void DestroySafely(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
