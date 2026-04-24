using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRDemo.Core
{
    public enum CompanionCameraMode
    {
        FirstPerson,
        FemalePerspective
    }

    public class CompanionCameraModeController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform playerRig;
        [SerializeField] private Transform partnerSpawn;
        [SerializeField] private CompanionFirstPersonController firstPersonController;
        [SerializeField] private CompanionInteractionDirector interactionDirector;
        [SerializeField] private string playerResourcePath = "Models/Characters/player";
        [SerializeField] private string playerModelAssetPath = "Assets/Resources/Models/Characters/player.vrm";

        private CompanionCameraMode currentMode = CompanionCameraMode.FirstPerson;
        private GameObject maleVisual;
        private Animator maleAnimator;
        private RuntimeAnimatorController maleIdleController;
        private Vector3 maleBaseLocalPosition;
        private Quaternion maleBaseLocalRotation;
        private float maleIdleSeed;

        public CompanionCameraMode CurrentMode => currentMode;

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.V))
            {
                SetMode(currentMode == CompanionCameraMode.FirstPerson
                    ? CompanionCameraMode.FemalePerspective
                    : CompanionCameraMode.FirstPerson);
            }

            UpdateMaleIdle();
        }

        public void Configure(
            Camera camera,
            Transform player,
            Transform partner,
            CompanionFirstPersonController firstPerson,
            CompanionInteractionDirector interaction)
        {
            targetCamera = camera;
            playerRig = player;
            partnerSpawn = partner;
            firstPersonController = firstPerson;
            interactionDirector = interaction;
            if (Application.isPlaying)
            {
                SetMode(currentMode);
            }
        }

        public void SetMode(CompanionCameraMode mode)
        {
            currentMode = mode;
            if (firstPersonController != null)
            {
                firstPersonController.SetMovementEnabled(mode == CompanionCameraMode.FirstPerson);
            }

            if (interactionDirector != null)
            {
                interactionDirector.SetFemaleMovementEnabled(mode == CompanionCameraMode.FemalePerspective);
            }

            EnsureMaleVisual();
            if (maleVisual != null)
            {
                maleVisual.SetActive(mode == CompanionCameraMode.FemalePerspective);
            }

            if (targetCamera == null)
            {
                return;
            }

            if (mode == CompanionCameraMode.FirstPerson && playerRig != null)
            {
                targetCamera.transform.SetParent(playerRig, false);
                targetCamera.transform.localPosition = new Vector3(0f, 1.48f, 0f);
                targetCamera.transform.localRotation = Quaternion.identity;
            }
            else if (partnerSpawn != null)
            {
                targetCamera.transform.SetParent(partnerSpawn, false);
                targetCamera.transform.localPosition = new Vector3(0f, 1.48f, 0.12f);
                targetCamera.transform.localRotation = Quaternion.identity;
            }
        }

        private void EnsureMaleVisual()
        {
            if (maleVisual != null || playerRig == null)
            {
                return;
            }

            var prefab = LoadPlayerPrefab();
            if (prefab != null)
            {
                maleVisual = Instantiate(prefab, playerRig);
                maleVisual.name = "MaleVisibleAvatar";
                NormalizeMaleVisual(maleVisual.transform);
                maleAnimator = maleVisual.GetComponentInChildren<Animator>(true);
            }
            else
            {
                maleVisual = CreateMaleProxy();
            }

            maleVisual.SetActive(currentMode == CompanionCameraMode.FemalePerspective);
            maleBaseLocalPosition = maleVisual.transform.localPosition;
            maleBaseLocalRotation = maleVisual.transform.localRotation;
            maleIdleSeed = Random.Range(0f, 10f);
            ApplyMaleIdleController();
        }

        private void ApplyMaleIdleController()
        {
            if (maleAnimator == null)
            {
                return;
            }

            if (maleIdleController == null)
            {
#if UNITY_EDITOR
                maleIdleController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/AnimatorControllers/HumanM@Idles.controller");
#endif
            }

            if (maleIdleController != null)
            {
                maleAnimator.runtimeAnimatorController = maleIdleController;
                maleAnimator.applyRootMotion = false;
            }
            else
            {
                Debug.LogWarning("[CompanionCamera] Male idle controller not found. Expected HumanM@Idles.controller in Kevin Iglesias Human Basic Motions.");
            }
        }

        private void UpdateMaleIdle()
        {
            if (maleVisual == null || !maleVisual.activeInHierarchy)
            {
                return;
            }

            var t = Time.time + maleIdleSeed;
            maleVisual.transform.localPosition = maleBaseLocalPosition + new Vector3(0f, Mathf.Sin(t * 1.2f) * 0.01f, 0f);
            maleVisual.transform.localRotation = maleBaseLocalRotation * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 1.1f, Mathf.Sin(t * 0.5f) * 1.4f, 0f);
        }

        private GameObject LoadPlayerPrefab()
        {
            var prefab = Resources.Load<GameObject>(playerResourcePath);
            if (prefab != null)
            {
                return prefab;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(playerModelAssetPath);
#else
            return null;
#endif
        }

        private GameObject CreateMaleProxy()
        {
            var root = new GameObject("MaleVisibleProxy");
            root.transform.SetParent(playerRig, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "MaleBody";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.78f, 0.42f);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "MaleHead";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            head.transform.localScale = Vector3.one * 0.28f;

            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                Destroy(collider);
            }

            var material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.2f, 0.26f, 0.34f, 1f)
            };
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = material;
            }

            return root;
        }

        private static void NormalizeMaleVisual(Transform avatarRoot)
        {
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                avatarRoot.localPosition = Vector3.zero;
                avatarRoot.localRotation = Quaternion.identity;
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y > 0.001f)
            {
                avatarRoot.localScale = Vector3.one * (1.68f / bounds.size.y);
            }

            renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            avatarRoot.localPosition = new Vector3(
                avatarRoot.localPosition.x - (bounds.center.x - avatarRoot.position.x),
                avatarRoot.localPosition.y - (bounds.min.y - avatarRoot.position.y),
                avatarRoot.localPosition.z - (bounds.center.z - avatarRoot.position.z));
            avatarRoot.localRotation = Quaternion.identity;
        }
    }
}
