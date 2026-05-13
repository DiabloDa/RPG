using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLookCinemachineFollow : MonoBehaviour
{
    [Header("References (optional)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private CinemachineFollow cinemachineFollow;

    [Header("Orbit Reference")]
    [Tooltip("Usually the player root. Used so orbit stays relative to the character even if CinemachineFollow BindingMode is WorldSpace.")]
    [SerializeField] private Transform orbitReference;

    [Header("Orbit Mode")]
    [Tooltip("If true, orbit is computed relative to the character yaw. If false, orbit is world-space (recommended when the character already turns to match the camera).")]
    [SerializeField] private bool orbitRelativeToCharacterYaw = false;

    [Header("LookAt")]
    [SerializeField] private bool forceLookAtTarget = true;
    [SerializeField, Min(0f)] private float lookAtDamping = 20f;
    [Tooltip("If left empty, tries to find a child named 'cameraFollowTarget' under orbitReference, else uses orbitReference.")]
    [SerializeField] private Transform lookAtTarget;

    [Header("Height (startup)")]
    [Tooltip("Applies once on Awake. Useful if the scene FollowOffset.y is too low; does not fight the charge raise (it becomes the new base height).")]
    [SerializeField] private bool enforceMinStartHeight = true;
    [SerializeField, Min(0f)] private float minStartHeight = 2.2f;

    [Header("Follow Feel (startup)")]
    [Tooltip("Reduces the feeling that the camera drifts/flies away when the player moves fast.")]
    [SerializeField] private bool overrideStartPositionDamping = true;
    [SerializeField] private Vector3 startPositionDamping = Vector3.zero;

    [Header("Cinemachine (safety)")]
    [Tooltip("If Priority is disabled on the CinemachineCamera, the Brain may ignore this vcam and you can end up looking at the horizon.")]
    [SerializeField] private bool forcePriorityEnabledOnAwake = true;
    [SerializeField] private int forcedPriorityValue = 10;

    [Header("Input")]
    [SerializeField] private string lookActionName = "Look";

    [Header("Sensitivity")]
    [SerializeField] private float sensitivityX = 0.02f;
    [SerializeField] private float sensitivityY = 0.015f;
    [SerializeField] private bool invertY = true;
    [SerializeField, Range(0.001f, 2f)] private float sensitivityMultiplier = 0.35f;

    [Header("Feel")]
    [SerializeField] private bool yawOnly = true;
    [SerializeField, Min(0f)] private float smoothing = 12f;
    [SerializeField] private float maxDeltaPixelsPerFrame = 50f;

    [Header("Limits")]
    [SerializeField] private bool clampYaw = true;
    [SerializeField] private float minYawDegrees = -140f;
    [SerializeField] private float maxYawDegrees = 140f;

    [Header("Limits")]
    [SerializeField] private bool enablePitch = true;
    [SerializeField] private float minPitchDegrees = -35f;
    [SerializeField] private float maxPitchDegrees = 65f;

    private InputAction lookAction;

    private Vector3 baseOffsetInReferenceYawSpace;
    private Vector3 baseOffsetXZ;
    private float yawDegrees;
    private float pitchDegrees;
    private float yawSmoothed;
    private float pitchSmoothed;

    private bool useCinemachineLookAt;
    private CinemachineCamera _cmCam;

    private void Awake()
    {
        _cmCam = GetComponent<CinemachineCamera>();

        if (forcePriorityEnabledOnAwake)
        {
            TryEnableCinemachineCameraPriority(_cmCam, forcedPriorityValue);
        }

        if (cinemachineFollow == null)
        {
            cinemachineFollow = GetComponent<CinemachineFollow>();
        }

        if (cinemachineFollow == null)
        {
            cinemachineFollow = FindFirstObjectByType<CinemachineFollow>();
        }

        if (playerInput == null)
        {
            playerInput = FindFirstObjectByType<PlayerInput>();
        }

        if (orbitReference == null)
        {
            orbitReference = playerInput != null ? playerInput.transform : null;
        }

        if (orbitReference == null)
        {
            var attackController = FindFirstObjectByType<AttackController>();
            orbitReference = attackController != null ? attackController.transform : null;
        }

        if (lookAtTarget == null)
        {
            lookAtTarget = TryFindCameraFollowTarget(orbitReference);
        }

        ResolveTargetsFromCinemachineIfMissing();

        if (cinemachineFollow != null)
        {
            if (overrideStartPositionDamping)
            {
                TrySetTrackerSettingsPositionDamping(cinemachineFollow, startPositionDamping);
            }

            if (enforceMinStartHeight)
            {
                var o = cinemachineFollow.FollowOffset;
                if (o.y < minStartHeight)
                {
                    o.y = minStartHeight;
                    cinemachineFollow.FollowOffset = o;
                }
            }

            var worldOffset = cinemachineFollow.FollowOffset;
            baseOffsetInReferenceYawSpace = ToReferenceYawSpace(worldOffset);
            baseOffsetXZ = new Vector3(worldOffset.x, 0f, worldOffset.z);
        }

        useCinemachineLookAt = TryConfigureCinemachineLookAtTarget();
    }

    private void OnEnable()
    {
        ResolveLookAction();
    }

    private void LateUpdate()
    {
        if (cinemachineFollow == null) return;

        ResolveTargetsFromCinemachineIfMissing();

        Vector2 delta = Vector2.zero;

        if (lookAction != null && lookAction.enabled)
        {
            delta = lookAction.ReadValue<Vector2>();
        }
        else if (Mouse.current != null)
        {
            delta = Mouse.current.delta.ReadValue();
        }

        bool hasInput = delta.sqrMagnitude >= 0.000001f;

        // Clamp extreme values (can happen with focus changes or certain devices).
        if (hasInput && maxDeltaPixelsPerFrame > 0f)
        {
            delta = Vector2.ClampMagnitude(delta, maxDeltaPixelsPerFrame);
        }

        if (hasInput)
        {
            float mul = Mathf.Max(0.01f, sensitivityMultiplier);
            yawDegrees += delta.x * sensitivityX * mul;

            // Keep angles bounded (prevents numeric drift and makes clamping predictable).
            yawDegrees = Mathf.Repeat(yawDegrees + 180f, 360f) - 180f;

            if (clampYaw)
            {
                yawDegrees = Mathf.Clamp(yawDegrees, minYawDegrees, maxYawDegrees);
            }

            if (enablePitch && !yawOnly)
            {
                float y = invertY ? -delta.y : delta.y;
                pitchDegrees += y * sensitivityY * mul;
                pitchDegrees = Mathf.Clamp(pitchDegrees, minPitchDegrees, maxPitchDegrees);
            }
        }

        // Smooth for ARPG feel.
        float s = Mathf.Max(0f, smoothing);
        float lerpT = s <= 0f ? 1f : 1f - Mathf.Exp(-s * Time.unscaledDeltaTime);

        yawSmoothed = Mathf.LerpAngle(yawSmoothed, yawDegrees, lerpT);
        pitchSmoothed = Mathf.Lerp(pitchSmoothed, pitchDegrees, lerpT);

        // Preserve current Y (charge raise) while we only control X/Z orbit.
        Vector3 currentOffset = cinemachineFollow.FollowOffset;
        float yPreserve = currentOffset.y;

        if (orbitRelativeToCharacterYaw)
        {
            // If something else ever overwrote the base to near-zero, refresh it once.
            if (baseOffsetInReferenceYawSpace.sqrMagnitude < 0.0001f)
            {
                baseOffsetInReferenceYawSpace = ToReferenceYawSpace(currentOffset);
            }

            // Orbit in character-yaw space, then convert to world (works well with BindingMode=WorldSpace).
            float usedPitch = (!yawOnly && enablePitch) ? pitchSmoothed : 0f;
            Quaternion userRot = Quaternion.Euler(usedPitch, yawSmoothed, 0f);
            Vector3 offsetInReferenceYaw = userRot * baseOffsetInReferenceYawSpace;
            Vector3 desiredWorldOffset = ToWorldYawSpace(offsetInReferenceYaw);
            desiredWorldOffset.y = yPreserve;
            cinemachineFollow.FollowOffset = desiredWorldOffset;
        }
        else
        {
            // World-space orbit. Recommended when the character already turns to match camera yaw.
            if (baseOffsetXZ.sqrMagnitude < 0.0001f)
            {
                baseOffsetXZ = new Vector3(currentOffset.x, 0f, currentOffset.z);
            }

            Quaternion yawRot = Quaternion.Euler(0f, yawSmoothed, 0f);
            Vector3 rotatedXZ = yawRot * baseOffsetXZ;
            cinemachineFollow.FollowOffset = new Vector3(rotatedXZ.x, yPreserve, rotatedXZ.z);
        }

        if (forceLookAtTarget)
        {
            Transform target = lookAtTarget != null ? lookAtTarget : (orbitReference != null ? orbitReference : GetTrackingTarget());
            if (target != null)
            {
                Vector3 to = target.position - transform.position;
                if (to.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredRot = Quaternion.LookRotation(to.normalized, Vector3.up);
                    float k = Mathf.Max(0f, lookAtDamping);
                    float rt = k <= 0f ? 1f : 1f - Mathf.Exp(-k * Time.unscaledDeltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rt);
                }
            }
        }
    }

    private bool TryConfigureCinemachineLookAtTarget()
    {
        var cmCam = _cmCam != null ? _cmCam : GetComponent<CinemachineCamera>();
        if (cmCam == null)
        {
            return false;
        }

        Transform desired = lookAtTarget != null ? lookAtTarget : orbitReference;
        if (desired == null)
        {
            return false;
        }

        // Only set it if it isn't already assigned in the scene.
        if (TryGetCinemachineTargetTransform(cmCam, "LookAtTarget", out var current) && current != null)
        {
            return true;
        }

        // Try common field/property names used by Cinemachine v3.
        if (TrySetCinemachineTargetTransform(cmCam, "LookAtTarget", desired))
        {
            return true;
        }

        if (TrySetCinemachineTargetTransform(cmCam, "LookAt", desired))
        {
            return true;
        }

        return false;
    }

    private void ResolveTargetsFromCinemachineIfMissing()
    {
        Transform tracking = GetTrackingTarget();
        if (tracking == null)
        {
            return;
        }

        if (orbitReference == null)
        {
            // Prefer the parent (player root) if available; otherwise use the tracking transform itself.
            orbitReference = tracking.parent != null ? tracking.parent : tracking;
        }

        if (lookAtTarget == null)
        {
            lookAtTarget = tracking;
        }

        if (baseOffsetInReferenceYawSpace.sqrMagnitude < 0.0001f && cinemachineFollow != null)
        {
            baseOffsetInReferenceYawSpace = ToReferenceYawSpace(cinemachineFollow.FollowOffset);
        }
    }

    private Transform GetTrackingTarget()
    {
        var cmCam = _cmCam != null ? _cmCam : GetComponent<CinemachineCamera>();
        if (cmCam == null)
        {
            return null;
        }

        // Try common field/property names used by Cinemachine v3.
        if (TryGetCinemachineTargetTransform(cmCam, "TrackingTarget", out var tracking) && tracking != null)
        {
            return tracking;
        }

        if (TryGetCinemachineTargetTransform(cmCam, "FollowTarget", out var follow) && follow != null)
        {
            return follow;
        }

        return null;
    }

    private static bool TryGetCinemachineTargetTransform(CinemachineCamera camera, string name, out Transform value)
    {
        value = null;
        if (camera == null) return false;

        object targetBoxed = GetCinemachineTargetBoxed(camera, out var targetMember, out bool isProperty);
        if (targetBoxed == null) return false;

        var targetType = targetBoxed.GetType();
        var field = targetType.GetField(name);
        if (field != null && field.FieldType == typeof(Transform))
        {
            value = (Transform)field.GetValue(targetBoxed);
            return true;
        }

        var prop = targetType.GetProperty(name);
        if (prop != null && prop.PropertyType == typeof(Transform) && prop.CanRead)
        {
            value = (Transform)prop.GetValue(targetBoxed);
            return true;
        }

        return false;
    }

    private static bool TrySetCinemachineTargetTransform(CinemachineCamera camera, string name, Transform desired)
    {
        if (camera == null) return false;

        object targetBoxed = GetCinemachineTargetBoxed(camera, out var targetMember, out bool isProperty);
        if (targetBoxed == null) return false;

        bool changed = false;
        var targetType = targetBoxed.GetType();

        var field = targetType.GetField(name);
        if (field != null && field.FieldType == typeof(Transform))
        {
            field.SetValue(targetBoxed, desired);
            changed = true;
        }
        else
        {
            var prop = targetType.GetProperty(name);
            if (prop != null && prop.PropertyType == typeof(Transform) && prop.CanWrite)
            {
                prop.SetValue(targetBoxed, desired);
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        // Write the modified struct back onto CinemachineCamera.Target.
        if (isProperty)
        {
            ((System.Reflection.PropertyInfo)targetMember).SetValue(camera, targetBoxed);
            return true;
        }

        ((System.Reflection.FieldInfo)targetMember).SetValue(camera, targetBoxed);
        return true;
    }

    private static object GetCinemachineTargetBoxed(CinemachineCamera camera, out System.Reflection.MemberInfo targetMember, out bool isProperty)
    {
        targetMember = null;
        isProperty = false;

        var camType = camera.GetType();

        var prop = camType.GetProperty("Target");
        if (prop != null && prop.CanRead && prop.CanWrite)
        {
            targetMember = prop;
            isProperty = true;
            return prop.GetValue(camera);
        }

        var field = camType.GetField("Target");
        if (field != null)
        {
            targetMember = field;
            isProperty = false;
            return field.GetValue(camera);
        }

        return null;
    }

    private Vector3 ToReferenceYawSpace(Vector3 worldOffset)
    {
        Quaternion yaw = GetReferenceYaw();
        return Quaternion.Inverse(yaw) * worldOffset;
    }

    private Vector3 ToWorldYawSpace(Vector3 offsetInReferenceYawSpace)
    {
        Quaternion yaw = GetReferenceYaw();
        return yaw * offsetInReferenceYawSpace;
    }

    private Quaternion GetReferenceYaw()
    {
        if (orbitReference == null) return Quaternion.identity;
        Vector3 euler = orbitReference.rotation.eulerAngles;
        return Quaternion.Euler(0f, euler.y, 0f);
    }

    private static void TrySetTrackerSettingsPositionDamping(CinemachineFollow follow, Vector3 damping)
    {
        if (follow == null) return;

        var type = follow.GetType();

        // TrackerSettings can be a property or a field, and it's commonly a struct.
        var trackerProp = type.GetProperty("TrackerSettings");
        if (trackerProp != null && trackerProp.CanRead && trackerProp.CanWrite)
        {
            object tracker = trackerProp.GetValue(follow);
            if (tracker == null) return;
            if (TrySetVector3Member(tracker, "PositionDamping", damping))
            {
                trackerProp.SetValue(follow, tracker);
            }
            return;
        }

        var trackerField = type.GetField("TrackerSettings");
        if (trackerField != null)
        {
            object tracker = trackerField.GetValue(follow);
            if (tracker == null) return;
            if (TrySetVector3Member(tracker, "PositionDamping", damping))
            {
                trackerField.SetValue(follow, tracker);
            }
        }
    }

    private static bool TrySetVector3Member(object obj, string name, Vector3 value)
    {
        var t = obj.GetType();
        var field = t.GetField(name);
        if (field != null && field.FieldType == typeof(Vector3))
        {
            field.SetValue(obj, value);
            return true;
        }

        var prop = t.GetProperty(name);
        if (prop != null && prop.PropertyType == typeof(Vector3) && prop.CanWrite)
        {
            prop.SetValue(obj, value);
            return true;
        }

        return false;
    }

    private static void TryEnableCinemachineCameraPriority(CinemachineCamera cmCam, int priorityValue)
    {
        if (cmCam == null) return;

        var type = cmCam.GetType();

        var priorityProp = type.GetProperty("Priority");
        if (priorityProp != null && priorityProp.CanRead && priorityProp.CanWrite)
        {
            object priority = priorityProp.GetValue(cmCam);
            if (priority == null) return;
            bool changed = false;
            changed |= TrySetBoolMember(priority, "Enabled", true);
            changed |= TrySetIntMember(priority, "m_Value", priorityValue);
            changed |= TrySetIntMember(priority, "Value", priorityValue);
            if (changed)
            {
                priorityProp.SetValue(cmCam, priority);
            }
            return;
        }

        var priorityField = type.GetField("Priority");
        if (priorityField != null)
        {
            object priority = priorityField.GetValue(cmCam);
            if (priority == null) return;
            bool changed = false;
            changed |= TrySetBoolMember(priority, "Enabled", true);
            changed |= TrySetIntMember(priority, "m_Value", priorityValue);
            changed |= TrySetIntMember(priority, "Value", priorityValue);
            if (changed)
            {
                priorityField.SetValue(cmCam, priority);
            }
        }
    }

    private static bool TrySetBoolMember(object obj, string name, bool value)
    {
        var t = obj.GetType();
        var field = t.GetField(name);
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(obj, value);
            return true;
        }

        var prop = t.GetProperty(name);
        if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
        {
            prop.SetValue(obj, value);
            return true;
        }

        return false;
    }

    private static bool TrySetIntMember(object obj, string name, int value)
    {
        var t = obj.GetType();
        var field = t.GetField(name);
        if (field != null && field.FieldType == typeof(int))
        {
            field.SetValue(obj, value);
            return true;
        }

        var prop = t.GetProperty(name);
        if (prop != null && prop.PropertyType == typeof(int) && prop.CanWrite)
        {
            prop.SetValue(obj, value);
            return true;
        }

        return false;
    }

    private static Transform TryFindCameraFollowTarget(Transform root)
    {
        if (root == null) return null;

        // Exact name used in the provided SampleScene.
        if (root.name == "cameraFollowTarget") return root;

        var children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == "cameraFollowTarget")
            {
                return children[i];
            }
        }

        return null;
    }

    private void ResolveLookAction()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            lookAction = null;
            return;
        }

        // Ensure actions are enabled (safe even if already enabled).
        playerInput.actions.Enable();

        lookAction = playerInput.actions.FindAction(lookActionName, throwIfNotFound: false);
        if (lookAction == null)
        {
            // Fallback to map/action path.
            lookAction = playerInput.actions.FindAction("CharacterARPG/Look", throwIfNotFound: false);
        }

        // Make sure the action is enabled.
        lookAction?.Enable();
    }
}
