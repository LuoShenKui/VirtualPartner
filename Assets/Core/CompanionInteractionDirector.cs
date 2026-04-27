using UnityEngine;
using VRDemo.VRM;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRDemo.Core
{
    /// <summary>
    /// 负责把对话结果映射到伴侣的动作、表情和站位。
    /// </summary>
    public class CompanionInteractionDirector : MonoBehaviour
    {
        [SerializeField] private string partnerResourcePath = "Models/Characters/partner";
        [SerializeField] private float targetAvatarHeight = 1.58f;
        [SerializeField] private Vector3 standingLocalOffset = Vector3.zero;
        [SerializeField] private float femaleMoveSpeed = 2.6f;
        [SerializeField] private float femaleLookSensitivity = 2.4f;
        [SerializeField] private Vector3 standingLocalEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private string femaleIdleControllerPath = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/AnimatorControllers/HumanF@Idles.overrideController";
        [SerializeField] private string femaleTalkingControllerPath = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/AnimatorControllers/HumanF@Talking.controller";

        private DialogueSystem dialogueSystem;
        private VRMController vrmController;
        private Transform partnerSpawn;
        private Transform anchorTarget;
        private Vector3 placeholderBaseLocalPosition;
        private Vector3 placeholderBaseLocalScale;
        private Renderer placeholderRenderer;
        private float idleSeed;
        private string currentExpression = "neutral";
        private Transform importedAvatarRoot;
        private Vector3 importedBasePosition;
        private Quaternion importedBaseRotation;
        private Vector3 importedBaseScale;
        private Animator importedAnimator;
        private RuntimeAnimatorController idleController;
        private RuntimeAnimatorController talkingController;
        private float talkUntilTime;
        private string currentControllerState = "";
        private string partnerModelAssetPath = "Assets/Resources/Models/Characters/partner.vrm";
        private bool femaleMovementEnabled;
        private bool isSpeaking;
        private CompanionSpeechService speechService;
        private SkinnedMeshRenderer[] skinnedMeshes;
        private Light partnerKeyLight;
        private float motionUntilTime;
        private string activeMotion = "idle";
        private CharacterController femaleController;
        private float femalePitch;
        private float groundHeight = 0.08f;
        private bool loggedGrounding;

        private void OnEnable()
        {
            dialogueSystem = FindAnyObjectByType<DialogueSystem>();
            vrmController = GetComponent<VRMController>();
            partnerSpawn = transform.parent;
            groundHeight = ResolveGroundHeightNearPartner();
            anchorTarget = FindAnyObjectByType<CompanionEyeTarget>()?.transform;
            idleSeed = Random.Range(0f, 10f);
            ApplyUserSettings(CompanionUserSettings.Load());
            speechService = FindAnyObjectByType<CompanionSpeechService>();

            if (dialogueSystem != null)
            {
                dialogueSystem.OnDialogueResolved += ApplyDialogueResponse;
            }

            if (speechService != null)
            {
                speechService.OnPartnerSpeakingChanged += OnSpeakingChanged;
            }

            EnsurePartnerVisual();
            EnsureFemaleController();
            SnapPartnerSpawnToGround();
            ResolveAnimationControllers();
            EnsurePartnerKeyLight();
            SetIdleState();
        }

        private void OnDisable()
        {
            if (dialogueSystem != null)
            {
                dialogueSystem.OnDialogueResolved -= ApplyDialogueResponse;
            }

            if (speechService != null)
            {
                speechService.OnPartnerSpeakingChanged -= OnSpeakingChanged;
            }
        }

        private void EnsurePartnerVisual()
        {
            if (transform.childCount > 0 && transform.GetComponentInChildren<Renderer>(true) != null)
            {
                ResolveExistingAvatarRoot();
                CachePlaceholderParts();
                SnapImportedAvatarToFloor(true);
                return;
            }

            var prefab = LoadPartnerPrefab();
            if (prefab != null)
            {
                var instance = Instantiate(prefab, transform);
                importedAvatarRoot = instance.transform;
                importedAnimator = importedAvatarRoot.GetComponentInChildren<Animator>();
                DisableImportedRootMotion();
                skinnedMeshes = importedAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                NormalizeImportedAvatar(importedAvatarRoot);
                CachePlaceholderParts();
                SnapImportedAvatarToFloor(true);
                return;
            }

            CreatePlaceholderAvatar();
        }

        private void CreatePlaceholderAvatar()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "PartnerPlaceholder";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            body.transform.localScale = new Vector3(0.74f, 0.68f, 0.74f);

            var renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Standard"))
                {
                    color = new Color(0.96f, 0.77f, 0.82f, 1f)
                };
            }

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "PartnerHead";
            head.transform.SetParent(body.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.94f, 0.08f);
            head.transform.localScale = Vector3.one * 0.42f;

            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "PartnerBack";
            back.transform.SetParent(body.transform, false);
            back.transform.localPosition = new Vector3(0f, 0.46f, -0.18f);
            back.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            back.transform.localScale = new Vector3(0.56f, 0.55f, 0.22f);

            var backCollider = back.GetComponent<Collider>();
            if (backCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(backCollider);
                }
                else
                {
                    DestroyImmediate(backCollider);
                }
            }
            CachePlaceholderParts();
        }

        private void ResolveExistingAvatarRoot()
        {
            if (importedAvatarRoot != null)
            {
                return;
            }

            var firstRenderer = transform.GetComponentInChildren<Renderer>(true);
            if (firstRenderer == null)
            {
                return;
            }

            var candidate = firstRenderer.transform;
            while (candidate.parent != null && candidate.parent != transform)
            {
                candidate = candidate.parent;
            }

            if (candidate == transform)
            {
                return;
            }

            importedAvatarRoot = candidate;
            importedAnimator = importedAvatarRoot.GetComponentInChildren<Animator>(true);
            DisableImportedRootMotion();
            skinnedMeshes = importedAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            importedBasePosition = importedAvatarRoot.localPosition;
            importedBaseRotation = importedAvatarRoot.localRotation;
            importedBaseScale = importedAvatarRoot.localScale;
        }

        private void Update()
        {
            var t = Time.time + idleSeed;
            transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            UpdateAnimationControllerState();
            if (femaleMovementEnabled)
            {
                HandleFemaleLook();
            }
            HandleFemaleMovement();

            if (importedAvatarRoot != null)
            {
                var motionOffset = GetMotionOffset(t);
                var motionRotation = GetMotionRotation(t);
                importedAvatarRoot.localPosition = importedBasePosition + motionOffset;
                importedAvatarRoot.localRotation = importedBaseRotation * motionRotation * Quaternion.Euler(Mathf.Sin(t * 0.75f) * 1.5f, 0f, 0f);
                importedAvatarRoot.localScale = importedBaseScale;
                ApplyMouthShape(isSpeaking ? Mathf.Abs(Mathf.Sin(t * 12f)) * 70f : 0f);
            }

            if (placeholderRenderer != null)
            {
                var body = placeholderRenderer.transform;
                body.localPosition = placeholderBaseLocalPosition + new Vector3(0f, Mathf.Sin(t * 1.4f) * 0.015f, 0f);
                body.localScale = placeholderBaseLocalScale + Vector3.one * (Mathf.Sin(t * 1.8f) * 0.015f);
            }

            if (anchorTarget == null)
            {
                anchorTarget = FindAnyObjectByType<CompanionEyeTarget>()?.transform;
            }

            if (!femaleMovementEnabled && anchorTarget != null && partnerSpawn != null)
            {
                var lookTarget = new Vector3(anchorTarget.position.x, partnerSpawn.position.y, anchorTarget.position.z);
                var lookDirection = lookTarget - partnerSpawn.position;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    partnerSpawn.rotation = Quaternion.Slerp(partnerSpawn.rotation, Quaternion.LookRotation(lookDirection.normalized, Vector3.up), Time.deltaTime * 5f);
                }
            }

            SnapPartnerSpawnToGround();
            SnapImportedAvatarToFloor();
            UpdatePartnerKeyLight();
        }

        private void LateUpdate()
        {
            SnapPartnerSpawnToGround();
            SnapImportedAvatarToFloor(true);
        }

        private void HandleFemaleLook()
        {
            if (partnerSpawn == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            var yaw = Input.GetAxis("Mouse X") * femaleLookSensitivity;
            var lookY = Input.GetAxis("Mouse Y") * femaleLookSensitivity;

            partnerSpawn.Rotate(Vector3.up * yaw);
            femalePitch = Mathf.Clamp(femalePitch - lookY, -35f, 45f);

            var activeCamera = Camera.main;
            if (activeCamera != null && activeCamera.transform.parent == partnerSpawn)
            {
                activeCamera.transform.localRotation = Quaternion.Euler(femalePitch, 0f, 0f);
            }
        }

        private void HandleFemaleMovement()
        {
            if (!femaleMovementEnabled || partnerSpawn == null)
            {
                return;
            }

            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);
            if (input.sqrMagnitude < 0.001f)
            {
                return;
            }

            var femaleForward = partnerSpawn.forward;
            femaleForward.y = 0f;
            if (femaleForward.sqrMagnitude < 0.001f)
            {
                femaleForward = Vector3.forward;
            }

            var right = Vector3.Cross(Vector3.up, femaleForward.normalized);
            var move = (right * input.x + femaleForward.normalized * input.z) * femaleMoveSpeed;
            move.y = 0f;

            if (femaleController != null)
            {
                femaleController.Move(move * Time.deltaTime);
            }
            else
            {
                partnerSpawn.position += move * Time.deltaTime;
            }

            SnapPartnerSpawnToGround();
        }

        private void CachePlaceholderParts()
        {
            placeholderRenderer = transform.GetComponentInChildren<Renderer>();
            if (placeholderRenderer != null)
            {
                placeholderBaseLocalPosition = placeholderRenderer.transform.localPosition;
                placeholderBaseLocalScale = placeholderRenderer.transform.localScale;
            }
        }

        private void ResolveAnimationControllers()
        {
#if UNITY_EDITOR
            if (idleController == null)
            {
                idleController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(femaleIdleControllerPath);
                if (idleController == null)
                {
                    Debug.LogWarning($"[CompanionInteraction] Female idle controller not found: {femaleIdleControllerPath}");
                }
            }

            if (talkingController == null)
            {
                talkingController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(femaleTalkingControllerPath);
                if (talkingController == null)
                {
                    Debug.LogWarning($"[CompanionInteraction] Female talking controller not found: {femaleTalkingControllerPath}");
                }
            }
#endif
        }

        private void DisableImportedRootMotion()
        {
            if (importedAnimator == null)
            {
                return;
            }

            importedAnimator.applyRootMotion = false;
        }

        private void ApplyMouthShape(float weight)
        {
            if (skinnedMeshes == null)
            {
                return;
            }

            foreach (var mesh in skinnedMeshes)
            {
                if (mesh?.sharedMesh == null)
                {
                    continue;
                }

                for (var i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                {
                    var name = mesh.sharedMesh.GetBlendShapeName(i).ToLowerInvariant();
                    if (name.Contains("aa") || name.Contains("mouthopen") || name == "a" || name.Contains("v_aa"))
                    {
                        mesh.SetBlendShapeWeight(i, weight);
                    }
                }
            }
        }

        private GameObject LoadPartnerPrefab()
        {
            var prefab = Resources.Load<GameObject>(partnerResourcePath);
            if (prefab != null)
            {
                return prefab;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(partnerModelAssetPath))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(partnerModelAssetPath);
            }
#endif

            return prefab;
        }

        private void NormalizeImportedAvatar(Transform avatarRoot)
        {
            var bounds = CalculateBounds(avatarRoot);
            if (bounds.size.y <= 0.001f)
            {
                avatarRoot.localPosition = standingLocalOffset;
                avatarRoot.localRotation = Quaternion.Euler(standingLocalEuler);
                importedBasePosition = avatarRoot.localPosition;
                importedBaseRotation = avatarRoot.localRotation;
                importedBaseScale = avatarRoot.localScale;
                return;
            }

            var uniformScale = targetAvatarHeight / bounds.size.y;
            avatarRoot.localScale = Vector3.one * uniformScale;

            bounds = CalculateBounds(avatarRoot);
            var scaledBottom = bounds.min.y - transform.position.y;
            var scaledCenterX = bounds.center.x - transform.position.x;
            var scaledCenterZ = bounds.center.z - transform.position.z;

            var desiredBottom = GetDesiredFootWorldY() - transform.position.y;
            avatarRoot.localPosition = new Vector3(
                standingLocalOffset.x - scaledCenterX,
                desiredBottom - scaledBottom,
                standingLocalOffset.z - scaledCenterZ);
            avatarRoot.localRotation = Quaternion.Euler(standingLocalEuler);

            importedBasePosition = avatarRoot.localPosition;
            importedBaseRotation = avatarRoot.localRotation;
            importedBaseScale = avatarRoot.localScale;
            SnapImportedAvatarToFloor(true);
        }

        private void SnapImportedAvatarToFloor(bool allowLower = false)
        {
            if (importedAvatarRoot == null || partnerSpawn == null)
            {
                return;
            }

            var bounds = CalculateBounds(importedAvatarRoot);
            var requiredOffset = GetDesiredFootWorldY() - GetAvatarGroundProbeWorldY(bounds);
            if ((!allowLower && requiredOffset <= 0.0005f) || Mathf.Abs(requiredOffset) <= 0.0005f)
            {
                return;
            }

            importedAvatarRoot.localPosition += new Vector3(0f, requiredOffset, 0f);
            importedBasePosition = importedAvatarRoot.localPosition;
            if (!loggedGrounding)
            {
                loggedGrounding = true;
                Debug.Log($"[CompanionGrounding] Female avatar grounded: desired={GetDesiredFootWorldY():F3}, boundsMin={bounds.min.y:F3}, offset={requiredOffset:F3}");
            }
        }

        private float GetAvatarGroundProbeWorldY(Bounds bounds)
        {
            return bounds.min.y;
        }

        private void EnsureFemaleController()
        {
            if (partnerSpawn == null)
            {
                return;
            }

            femaleController = partnerSpawn.GetComponent<CharacterController>();
            if (femaleController == null)
            {
                femaleController = partnerSpawn.gameObject.AddComponent<CharacterController>();
            }

            femaleController.height = 1.58f;
            femaleController.radius = 0.22f;
            femaleController.center = new Vector3(0f, 0.79f, 0f);
            femaleController.stepOffset = 0.2f;
            femaleController.skinWidth = 0.03f;
        }

        private float ResolveGroundHeightNearPartner()
        {
            if (partnerSpawn == null)
            {
                return groundHeight;
            }

            var position = partnerSpawn.position;
            var bestHeight = position.y;
            var foundSurface = false;

            foreach (var renderer in FindObjectsByType<Renderer>())
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(partnerSpawn))
                {
                    continue;
                }

                var bounds = renderer.bounds;
                if (bounds.size.y > 0.35f || bounds.size.x < 0.6f || bounds.size.z < 0.6f)
                {
                    continue;
                }

                if (position.x < bounds.min.x - 0.25f || position.x > bounds.max.x + 0.25f ||
                    position.z < bounds.min.z - 0.25f || position.z > bounds.max.z + 0.25f)
                {
                    continue;
                }

                if (bounds.max.y > position.y + 2f)
                {
                    continue;
                }

                if (!foundSurface || bounds.max.y > bestHeight)
                {
                    bestHeight = bounds.max.y;
                    foundSurface = true;
                }
            }

            return foundSurface ? bestHeight + 0.01f : bestHeight;
        }

        private void SnapPartnerSpawnToGround()
        {
            if (partnerSpawn == null)
            {
                return;
            }

            if (Mathf.Abs(partnerSpawn.position.y - groundHeight) < 0.001f)
            {
                return;
            }

            if (femaleController != null)
            {
                femaleController.enabled = false;
                partnerSpawn.position = new Vector3(partnerSpawn.position.x, groundHeight, partnerSpawn.position.z);
                femaleController.enabled = true;
            }
            else
            {
                partnerSpawn.position = new Vector3(partnerSpawn.position.x, groundHeight, partnerSpawn.position.z);
            }
        }

        private void SetIdleState()
        {
            if (vrmController != null)
            {
                vrmController.SetExpression("neutral");
                vrmController.PlayMotion("idle");
            }

            currentExpression = "neutral";
            activeMotion = "idle";
            ApplyPlaceholderMood(currentExpression);
            ApplyController(idleController, "Idle");
        }

        private void ApplyDialogueResponse(DialogueResponse response)
        {
            if (response == null)
            {
                return;
            }

            if (vrmController != null)
            {
                vrmController.SetExpression(response.expression);
                vrmController.PlayMotion(response.motion);
            }
            currentExpression = response.expression;
            ApplyPlaceholderMood(currentExpression);
            ApplyMotion(response.motion);
            talkUntilTime = Time.time + 2f;
            ApplyController(talkingController != null ? talkingController : idleController, "Talking");
        }

        private void ApplyMotion(string motion)
        {
            if (partnerSpawn == null)
            {
                return;
            }

            switch (motion)
            {
                case "stepCloser":
                    activeMotion = "stepCloser";
                    motionUntilTime = Time.time + 1.2f;
                    StepTowardPlayer(0.28f);
                    break;
                case "stepBack":
                    activeMotion = "stepBack";
                    motionUntilTime = Time.time + 1.2f;
                    StepTowardPlayer(-0.22f);
                    break;
                case "wave":
                case "nod":
                case "shake":
                case "shy":
                case "think":
                case "talk":
                    activeMotion = motion;
                    motionUntilTime = Time.time + 2.4f;
                    talkUntilTime = Time.time + 2.4f;
                    break;
                default:
                    activeMotion = "talk";
                    motionUntilTime = Time.time + 1.8f;
                    talkUntilTime = Time.time + 1.8f;
                    break;
            }
        }

        private Vector3 GetMotionOffset(float t)
        {
            if (Time.time > motionUntilTime)
            {
                activeMotion = isSpeaking ? "talk" : "idle";
            }

            return activeMotion switch
            {
                "wave" => new Vector3(Mathf.Sin(t * 10f) * 0.012f, 0f, 0f),
                "shy" => new Vector3(0f, -Mathf.Abs(Mathf.Sin(t * 3.5f)) * 0.012f, 0f),
                "think" => Vector3.zero,
                "talk" => new Vector3(Mathf.Sin(t * 6f) * 0.006f, 0f, 0f),
                _ => Vector3.zero
            };
        }

        private Quaternion GetMotionRotation(float t)
        {
            if (Time.time > motionUntilTime && !isSpeaking)
            {
                return Quaternion.identity;
            }

            return activeMotion switch
            {
                "wave" => Quaternion.Euler(0f, Mathf.Sin(t * 9f) * 3.5f, Mathf.Sin(t * 12f) * 2.5f),
                "nod" => Quaternion.Euler(Mathf.Sin(t * 10f) * 4f, 0f, 0f),
                "shake" => Quaternion.Euler(0f, Mathf.Sin(t * 10f) * 4f, 0f),
                "shy" => Quaternion.Euler(5f + Mathf.Sin(t * 3f) * 1.5f, -4f, 0f),
                "think" => Quaternion.Euler(0f, Mathf.Sin(t * 2.5f) * 2f, 0f),
                "talk" => Quaternion.Euler(Mathf.Sin(t * 5f) * 1.5f, Mathf.Sin(t * 4f) * 1.2f, 0f),
                "stepCloser" => Quaternion.Euler(Mathf.Sin(t * 7f) * 2f, 0f, 0f),
                "stepBack" => Quaternion.Euler(-Mathf.Sin(t * 7f) * 2f, 0f, 0f),
                _ => Quaternion.identity
            };
        }

        private float GetDesiredFootWorldY()
        {
            return partnerSpawn != null
                ? partnerSpawn.position.y
                : transform.position.y;
        }

        private void EnsurePartnerKeyLight()
        {
            if (partnerKeyLight != null)
            {
                return;
            }

            var lightObject = new GameObject("PartnerKeyLight");
            lightObject.transform.SetParent(partnerSpawn != null ? partnerSpawn : transform, false);
            partnerKeyLight = lightObject.AddComponent<Light>();
            partnerKeyLight.type = LightType.Point;
            partnerKeyLight.color = new Color(1f, 0.86f, 0.68f);
            partnerKeyLight.intensity = 1.25f;
            partnerKeyLight.range = 3.2f;
            UpdatePartnerKeyLight();
        }

        private void UpdatePartnerKeyLight()
        {
            if (partnerKeyLight == null)
            {
                return;
            }

            partnerKeyLight.transform.localPosition = new Vector3(0.45f, 1.65f, 0.75f);
        }

        private void StepTowardPlayer(float distance)
        {
            if (anchorTarget == null)
            {
                return;
            }

            var direction = anchorTarget.position - partnerSpawn.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            var move = direction.normalized * distance;
            if (femaleController != null)
            {
                femaleController.Move(move);
            }
            else
            {
                partnerSpawn.position += move;
            }
            SnapPartnerSpawnToGround();
        }

        private void UpdateAnimationControllerState()
        {
            if (importedAnimator == null)
            {
                return;
            }

            if (Time.time > talkUntilTime && currentControllerState != "Idle")
            {
                ApplyController(idleController, "Idle");
            }
        }

        private void ApplyController(RuntimeAnimatorController controller, string stateName)
        {
            if (importedAnimator == null || controller == null || currentControllerState == stateName)
            {
                return;
            }

            importedAnimator.runtimeAnimatorController = controller;
            importedAnimator.applyRootMotion = false;
            importedAnimator.Rebind();
            importedAnimator.Update(0f);
            currentControllerState = stateName;
        }

        private void ApplyPlaceholderMood(string expression)
        {
            var body = transform.Find("PartnerPlaceholder");
            if (body == null)
            {
                return;
            }

            var renderer = body.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var color = expression switch
            {
                "happy" => new Color(0.99f, 0.8f, 0.73f, 1f),
                "blush" => new Color(0.98f, 0.7f, 0.8f, 1f),
                "sad" => new Color(0.76f, 0.83f, 0.94f, 1f),
                "angry" => new Color(0.94f, 0.64f, 0.64f, 1f),
                "surprised" => new Color(0.99f, 0.9f, 0.65f, 1f),
                "relaxed" => new Color(0.84f, 0.9f, 0.82f, 1f),
                _ => new Color(0.96f, 0.77f, 0.82f, 1f)
            };

            renderer.sharedMaterial.color = color;
        }

        private static Bounds CalculateBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        public void ApplyUserSettings(CompanionUserSettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            var shouldReloadModel = false;

            if (!string.IsNullOrWhiteSpace(settings.partnerResourcePath))
            {
                var newPath = settings.partnerResourcePath.Trim();
                shouldReloadModel |= newPath != partnerResourcePath;
                partnerResourcePath = newPath;
            }

            if (!string.IsNullOrWhiteSpace(settings.partnerModelAssetPath))
            {
                var newAssetPath = settings.partnerModelAssetPath.Trim();
                shouldReloadModel |= newAssetPath != partnerModelAssetPath;
                partnerModelAssetPath = newAssetPath;
            }

            if (!shouldReloadModel || !isActiveAndEnabled)
            {
                return;
            }

            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            importedAvatarRoot = null;
            importedAnimator = null;
            skinnedMeshes = null;
            placeholderRenderer = null;
            currentControllerState = string.Empty;
            EnsurePartnerVisual();
            SetIdleState();
        }

        public void SetFemaleMovementEnabled(bool value)
        {
            femaleMovementEnabled = value;
            if (value)
            {
                var activeCamera = Camera.main;
                femalePitch = activeCamera != null && activeCamera.transform.parent == partnerSpawn
                    ? NormalizePitch(activeCamera.transform.localEulerAngles.x)
                    : 0f;
                SnapPartnerSpawnToGround();
            }
        }

        private void OnSpeakingChanged(bool value)
        {
            isSpeaking = value;
            if (value)
            {
                ApplyController(talkingController != null ? talkingController : idleController, "Talking");
            }
        }

        private static float NormalizePitch(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }

    /// <summary>
    /// 用来标记玩家视角位置，便于伴侣朝向玩家。
    /// </summary>
    public class CompanionActionPanelAnchor : MonoBehaviour
    {
    }
}
