using UnityEngine;

namespace VRDemo.Core
{
    /// <summary>
    /// 给卧室镜子补一个轻量的实时反射。
    /// </summary>
    public class CompanionMirrorReflection : MonoBehaviour
    {
        private const int TextureSize = 1024;

        [SerializeField] private string mirrorSurfaceName = "MirrorReflective_01";

        private Camera mainCamera;
        private Camera mirrorCamera;
        private RenderTexture reflectionTexture;
        private Renderer mirrorRenderer;
        private Material runtimeMirrorMaterial;

        private void OnEnable()
        {
            mainCamera = Camera.main;
            EnsureMirrorResources();
        }

        private void OnDisable()
        {
            if (reflectionTexture != null)
            {
                reflectionTexture.Release();
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            EnsureMirrorResources();
            if (mainCamera == null || mirrorCamera == null || mirrorRenderer == null)
            {
                return;
            }

            var planePoint = mirrorRenderer.transform.position;
            var planeNormal = mirrorRenderer.transform.forward;
            if (Vector3.Dot(mainCamera.transform.position - planePoint, planeNormal) < 0f)
            {
                planeNormal = -planeNormal;
            }

            var reflectedPosition = ReflectPoint(mainCamera.transform.position, planePoint, planeNormal);
            var reflectedForward = ReflectDirection(mainCamera.transform.forward, planeNormal);
            var reflectedUp = ReflectDirection(mainCamera.transform.up, planeNormal);

            mirrorCamera.CopyFrom(mainCamera);
            mirrorCamera.transform.position = reflectedPosition;
            mirrorCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, reflectedUp);
            mirrorCamera.fieldOfView = mainCamera.fieldOfView;
            mirrorCamera.nearClipPlane = mainCamera.nearClipPlane;
            mirrorCamera.farClipPlane = mainCamera.farClipPlane;
            mirrorCamera.targetTexture = reflectionTexture;
            mirrorCamera.enabled = false;

            var previousEnabled = mirrorRenderer.enabled;
            mirrorRenderer.enabled = false;
            mirrorCamera.Render();
            mirrorRenderer.enabled = previousEnabled;
        }

        private void EnsureMirrorResources()
        {
            if (mirrorRenderer == null)
            {
                var surface = transform.Find(mirrorSurfaceName);
                if (surface != null)
                {
                    mirrorRenderer = surface.GetComponent<Renderer>();
                }
            }

            if (reflectionTexture == null)
            {
                reflectionTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
                {
                    name = "MirrorReflectionRT"
                };
            }

            if (mirrorCamera == null)
            {
                var go = new GameObject("MirrorCamera");
                go.transform.SetParent(transform, false);
                mirrorCamera = go.AddComponent<Camera>();
                mirrorCamera.enabled = false;
            }

            if (mirrorRenderer != null && runtimeMirrorMaterial == null)
            {
                runtimeMirrorMaterial = new Material(Shader.Find("Unlit/Texture"));
                runtimeMirrorMaterial.mainTexture = reflectionTexture;
                runtimeMirrorMaterial.mainTextureScale = new Vector2(-1f, 1f);
                runtimeMirrorMaterial.mainTextureOffset = new Vector2(1f, 0f);
                mirrorRenderer.material = runtimeMirrorMaterial;
            }
        }

        private static Vector3 ReflectPoint(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            var toPoint = point - planePoint;
            var distance = Vector3.Dot(toPoint, planeNormal);
            return point - 2f * distance * planeNormal;
        }

        private static Vector3 ReflectDirection(Vector3 direction, Vector3 planeNormal)
        {
            return direction - 2f * Vector3.Dot(direction, planeNormal) * planeNormal;
        }
    }
}
