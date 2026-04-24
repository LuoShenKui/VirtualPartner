using UnityEngine;

namespace VRDemo.Core
{
    /// <summary>
    /// 为卧室场景补齐虚拟伴侣互动所需的站位、视角和基础系统。
    /// </summary>
    [ExecuteAlways]
    public class CompanionBedroomDirector : MonoBehaviour
    {
        [Header("第一视角设置")]
        [SerializeField] private float playerEyeHeight = 1.48f;

        private Transform playerSpawn;
        private Transform partnerSpawn;
        private Transform bedInteractionPoint;
        private Transform conversationPoint;
        private Transform cameraTarget;

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnsureSceneSetup();
            }
        }

        private void OnValidate()
        {
            // Unity 6000 forbids AddComponent/CreatePrimitive during validation.
            // Runtime setup is performed from OnEnable when entering Play mode.
        }

        private void EnsureSceneSetup()
        {
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            EnsureSystems();

            var gameplayRoot = FindOrCreateChild(transform, "CompanionGameplay");
            playerSpawn = FindOrCreateChild(gameplayRoot, "PlayerSpawn");
            partnerSpawn = FindOrCreateChild(gameplayRoot, "PartnerSpawn");
            bedInteractionPoint = FindOrCreateChild(gameplayRoot, "BedInteractionPoint");
            conversationPoint = FindOrCreateChild(gameplayRoot, "ConversationPoint");
            cameraTarget = FindOrCreateChild(gameplayRoot, "CameraTarget");

            PlaceAnchorsFromBed();
            EnsureGroundCollider();
            var interactionDirector = EnsurePartnerActor();
            ConfigurePlayerRig();
            ConfigureCameraModes(interactionDirector);
        }

        private void EnsureSystems()
        {
            var systems = FindOrCreateChild(transform, "CompanionSystems");

            if (systems.GetComponent<MemoryManager>() == null)
            {
                systems.gameObject.AddComponent<MemoryManager>();
            }

            if (systems.GetComponent<DialogueSystem>() == null)
            {
                systems.gameObject.AddComponent<DialogueSystem>();
            }

            if (systems.GetComponent<CompanionSpeechService>() == null)
            {
                systems.gameObject.AddComponent<CompanionSpeechService>();
            }

            if (systems.GetComponent<CompanionLightingDirector>() == null)
            {
                systems.gameObject.AddComponent<CompanionLightingDirector>();
            }

            RestoreLaundryBasket();

            var mirror = FindDeepChildByName("WallMirror_01");
            if (mirror != null && mirror.GetComponent<CompanionMirrorReflection>() == null)
            {
                mirror.gameObject.AddComponent<CompanionMirrorReflection>();
            }

            if (GetComponent<VRDemo.UI.CompanionActionPanel>() == null)
            {
                gameObject.AddComponent<VRDemo.UI.CompanionActionPanel>();
            }
        }

        private void PlaceAnchorsFromBed()
        {
            var bed = FindDeepChildByName("Bed");
            if (bed == null)
            {
                ApplyFallbackLayout();
                return;
            }

            var bounds = CalculateBounds(bed);
            var interior = FindDeepChildByName("Interior");
            var interiorBounds = interior != null ? CalculateBounds(interior) : new Bounds(Vector3.zero, new Vector3(8f, 3f, 8f));
            var center = bounds.center;
            var groundHeight = CalculateGroundHeight();
            var toRoomCenter = interiorBounds.center - center;
            toRoomCenter.y = 0f;

            var bedLengthAxis = bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward;
            var bedWidthAxis = bedLengthAxis == Vector3.right ? Vector3.forward : Vector3.right;

            var footDirection = Vector3.Dot(toRoomCenter, bedLengthAxis) >= 0f ? bedLengthAxis : -bedLengthAxis;
            var rightSideClearance = DistanceToInteriorEdge(center, bedWidthAxis, interiorBounds);
            var leftSideClearance = DistanceToInteriorEdge(center, -bedWidthAxis, interiorBounds);
            var partnerSideDirection = rightSideClearance >= leftSideClearance ? bedWidthAxis : -bedWidthAxis;

            var footClearance = Mathf.Max(bounds.extents.x, bounds.extents.z) + 1.35f;
            var partnerClearance = Mathf.Min(bounds.extents.x, bounds.extents.z) + 0.78f;
            var talkOffset = 0.55f;

            var desiredPlayer = center + footDirection * footClearance;
            var desiredPartner = center + partnerSideDirection * partnerClearance;

            playerSpawn.position = ClampInsideInterior(desiredPlayer, interiorBounds, groundHeight, 0.9f);
            partnerSpawn.position = ClampInsideInterior(desiredPartner, interiorBounds, groundHeight, 0.75f);
            bedInteractionPoint.position = ClampInsideInterior(center + footDirection * (footClearance - 0.5f), interiorBounds, groundHeight, 0.7f);
            conversationPoint.position = ClampInsideInterior(Vector3.Lerp(playerSpawn.position, partnerSpawn.position, 0.45f) + footDirection * talkOffset, interiorBounds, groundHeight, 0.7f);

            playerSpawn.rotation = Quaternion.LookRotation(Flatten(center - playerSpawn.position), Vector3.up);
            partnerSpawn.rotation = Quaternion.LookRotation(Flatten(playerSpawn.position - partnerSpawn.position), Vector3.up);
            bedInteractionPoint.rotation = playerSpawn.rotation;
            conversationPoint.rotation = Quaternion.LookRotation(Flatten(partnerSpawn.position - conversationPoint.position), Vector3.up);
            cameraTarget.position = center + Vector3.up * playerEyeHeight;
        }

        private void ApplyFallbackLayout()
        {
            var center = new Vector3(1.3f, 0f, 0.5f);
            playerSpawn.position = center + new Vector3(1.1f, 0f, 1.1f);
            partnerSpawn.position = center + new Vector3(0.18f, 0.08f, 0.9f);
            bedInteractionPoint.position = center + new Vector3(0.7f, 0f, 0.8f);
            conversationPoint.position = center + new Vector3(1.7f, 0f, 1.35f);
            cameraTarget.position = center + Vector3.up * playerEyeHeight;
        }

        private void ConfigurePlayerRig()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null || playerSpawn == null)
            {
                return;
            }

            var gameplayRoot = playerSpawn.parent;
            var playerRig = FindOrCreateChild(gameplayRoot, "PlayerRig");
            var groundHeight = CalculateGroundHeight();
            playerRig.position = new Vector3(playerSpawn.position.x, groundHeight, playerSpawn.position.z);
            playerRig.rotation = playerSpawn.rotation;

            var controller = playerRig.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = playerRig.gameObject.AddComponent<CharacterController>();
            }

            controller.height = 1.58f;
            controller.radius = 0.22f;
            controller.center = new Vector3(0f, 0.79f, 0f);
            controller.stepOffset = 0.2f;
            controller.skinWidth = 0.03f;

            var firstPerson = playerRig.GetComponent<CompanionFirstPersonController>();
            if (firstPerson == null)
            {
                firstPerson = playerRig.gameObject.AddComponent<CompanionFirstPersonController>();
            }

            firstPerson.BindCamera(mainCamera);
            firstPerson.SetEyeHeight(playerEyeHeight);
            firstPerson.SetGroundHeight(groundHeight);

            if (playerRig.GetComponent<CompanionActionPanelAnchor>() == null)
            {
                playerRig.gameObject.AddComponent<CompanionActionPanelAnchor>();
            }
        }

        private void EnsureGroundCollider()
        {
            var gameplayRoot = playerSpawn != null ? playerSpawn.parent : transform;
            var floor = FindOrCreateChild(gameplayRoot, "GameplayFloor");
            var collider = floor.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = floor.gameObject.AddComponent<BoxCollider>();
            }

            var interior = FindDeepChildByName("Interior");
            if (interior == null)
            {
                floor.position = new Vector3(0f, -0.02f, 0f);
                collider.size = new Vector3(8f, 0.2f, 8f);
                return;
            }

            var bounds = CalculateBounds(interior);
            floor.position = new Vector3(bounds.center.x, bounds.min.y - 0.06f, bounds.center.z);
            collider.size = new Vector3(Mathf.Max(6f, bounds.size.x + 1.5f), 0.25f, Mathf.Max(6f, bounds.size.z + 1.5f));
        }

        private float CalculateGroundHeight()
        {
            var interior = FindDeepChildByName("Interior");
            if (interior == null)
            {
                return 0.08f;
            }

            var bounds = CalculateBounds(interior);
            return bounds.min.y + 0.08f;
        }

        private static Vector3 ClampInsideInterior(Vector3 position, Bounds interiorBounds, float groundHeight, float margin)
        {
            return new Vector3(
                Mathf.Clamp(position.x, interiorBounds.min.x + margin, interiorBounds.max.x - margin),
                groundHeight,
                Mathf.Clamp(position.z, interiorBounds.min.z + margin, interiorBounds.max.z - margin));
        }

        private static Vector3 Flatten(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude < 0.0001f ? Vector3.forward : vector.normalized;
        }

        private static float DistanceToInteriorEdge(Vector3 origin, Vector3 direction, Bounds interiorBounds)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            {
                return direction.x > 0f ? interiorBounds.max.x - origin.x : origin.x - interiorBounds.min.x;
            }

            return direction.z > 0f ? interiorBounds.max.z - origin.z : origin.z - interiorBounds.min.z;
        }

        private CompanionInteractionDirector EnsurePartnerActor()
        {
            if (partnerSpawn == null)
            {
                return null;
            }

            var actorRoot = FindOrCreateChild(partnerSpawn, "PartnerActor");
            var interaction = actorRoot.GetComponent<CompanionInteractionDirector>();
            if (interaction == null)
            {
                interaction = actorRoot.gameObject.AddComponent<CompanionInteractionDirector>();
            }

            return interaction;
        }

        private void ConfigureCameraModes(CompanionInteractionDirector interactionDirector)
        {
            if (playerSpawn == null)
            {
                return;
            }

            var gameplayRoot = playerSpawn.parent;
            var playerRig = gameplayRoot.Find("PlayerRig");
            var mainCamera = Camera.main;
            if (playerRig == null || mainCamera == null)
            {
                return;
            }

            var eyeTarget = FindOrCreateChild(playerRig, "PlayerEyeTarget");
            eyeTarget.localPosition = new Vector3(0f, playerEyeHeight, 0f);
            if (eyeTarget.GetComponent<CompanionEyeTarget>() == null)
            {
                eyeTarget.gameObject.AddComponent<CompanionEyeTarget>();
            }

            var firstPerson = playerRig.GetComponent<CompanionFirstPersonController>();
            var cameraMode = gameplayRoot.GetComponent<CompanionCameraModeController>();
            if (cameraMode == null)
            {
                cameraMode = gameplayRoot.gameObject.AddComponent<CompanionCameraModeController>();
            }

            cameraMode.Configure(mainCamera, playerRig, partnerSpawn, firstPerson, interactionDirector);
        }

        private void RestoreLaundryBasket()
        {
            var laundryBasket = FindDeepChildByName("LaundryBasket_01");
            if (laundryBasket == null)
            {
                return;
            }

            laundryBasket.gameObject.SetActive(true);
            if (laundryBasket.position.y < -5f)
            {
                laundryBasket.position = new Vector3(-3.36f, 0.02f, -1.28f);
            }

            foreach (var basketRenderer in laundryBasket.GetComponentsInChildren<Renderer>(true))
            {
                basketRenderer.enabled = true;
                basketRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                basketRenderer.receiveShadows = true;
            }
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private Transform FindDeepChildByName(string childName)
        {
            foreach (var candidate in FindObjectsByType<Transform>())
            {
                if (candidate.name == childName && candidate.gameObject.scene == gameObject.scene)
                {
                    return candidate;
                }
            }

            foreach (var candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate.name == childName && candidate.gameObject.scene == gameObject.scene)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Bounds CalculateBounds(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(target.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
