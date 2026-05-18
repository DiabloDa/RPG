using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

public class AttackController : MonoBehaviour
{
    private static readonly int SpeedXHash = Animator.StringToHash("SpeedX");
    private static readonly int SpeedYHash = Animator.StringToHash("SpeedY");

    private Animator animator;
    private AttackHitBoxController hitBoxController;

    public DamageMessage.DamageLevel CurrentDamageLevel { get; private set; } = DamageMessage.DamageLevel.Small;
    public bool IsAttacking { get; private set; }

    [Header("Attack Timing")]
    [SerializeField, Min(0.1f)] private float attackFailsafeSeconds = 1.2f;
    [SerializeField, Min(0.05f)] private float lightChargeFallbackSeconds = 0.18f;

    [Header("Camera Feedback (timing)")]
    [Tooltip("Minimum time after the attack starts before the strike shake and hitbox window are allowed to fire. Helps when AnimationEvents are at time 0 or combos re-trigger quickly.")]
    [SerializeField, Min(0f)] private float lightStrikeShakeMinDelaySeconds = 0.06f;
    [SerializeField, Min(0f)] private float heavyStrikeShakeMinDelaySeconds = 0.12f;

    [Tooltip("Delay before starting the heavy charge camera raise. Prevents a visible bump when doing quick taps instead of holding.")]
    [SerializeField, Min(0f)] private float heavyChargeStartDelaySeconds = 0.10f;

    [Header("Directional Attacks")]
    [SerializeField, Range(0f, 1f)] private float directionEnterDeadzone = 0.35f;
    [SerializeField, Range(0f, 1f)] private float directionExitDeadzone = 0.25f;
    [SerializeField, Min(0f)] private float directionBufferSeconds = 0.2f;
    [SerializeField, Range(0f, 1f)] private float attackAxisMagnitude = 0.75f;
    [Tooltip("If no movement keys are pressed (raw move input is zero), attacks will go to camera forward instead of using the last movement direction.")]
    [SerializeField] private bool noInputAttacksUseCameraForward = true;

    [Header("Input Buffer")]
    [SerializeField, Min(0f)] private float attackBufferSeconds = 0.18f;
    [SerializeField, Min(0f)] private float directionCommitSeconds = 0.08f;

    [Header("Combo Buffer")]
    [Tooltip("While an attack is in progress, allow buffered follow-up inputs to persist longer so combos don't feel overly strict.")]
    [SerializeField, Min(0f)] private float attackBufferSecondsWhileAttacking = 0.75f;

    private DamageMessage.DamageLevel _armedStrikeShakeLevel = DamageMessage.DamageLevel.Small;
    private bool _strikeShakeArmed;
    private bool _heavyChargeHeld;
    private bool _cameraChargeHeld;

    private float _attackStartedAt;
    private bool _strikeShakeScheduled;
    private Coroutine _pendingHitboxOpenRoutine;

    [SerializeField] private float lightCost = 15f;
    [SerializeField] private float heavyCost = 35f;

    [Header("Attack Direction Reference (movement/camera space)")]
    [Tooltip("If assigned, D/A/W/S attack directions are interpreted relative to this transform (usually the Main Camera).")]
    [SerializeField] private Transform attackDirectionReferenceOverride;
    private Transform _attackDirectionReference;

    private Vector2 _rawMoveInput;
    private Vector2 _stableMoveDir;
    private float _stableMoveDirTime = -999f;
    private bool _hasStableMoveDir;

    private bool _attackDirectionLocked;
    private Vector2 _lockedAttackBlendDir;

    [Header("Attack Facing (optional)")]
    [Tooltip("Assign the model/armature root to rotate for directional attacks. Do NOT assign a transform that has the Camera under it.")]
    [SerializeField] private Transform attackFacingRootOverride;
    [SerializeField] private bool rotateCharacterToAttackDirection = true;
    [SerializeField, Min(0f)] private float attackTurnSpeedDegPerSec = 1440f;

    [Tooltip("Prevents attack animations with root motion from pushing the character sideways when attacking left/right.")]
    [SerializeField] private bool disableAnimatorRootMotionDuringAttack = true;
    private bool _restoreAnimatorRootMotion;
    private bool _previousAnimatorApplyRootMotion;

    private Transform _attackRotationRoot;
    private Quaternion _attackTargetRotation;
    private bool _hasAttackTargetRotation;

    [Header("Debug/Compatibility")]
    [Tooltip("If true, the controller will auto-toggle hitboxes at a timed strike window when AnimationEvents are missing. Useful for testing or clips without events.")]
    [SerializeField] private bool autoStrikeIfNoEvent = true;
    [SerializeField, Min(0f)] private float autoStrikeDelayLight = 0.12f;
    [SerializeField, Min(0f)] private float autoStrikeWindowLight = 0.08f;
    [Tooltip("Heavy strike impact timing in seconds after the attack starts. Set this to the moment the weapon actually lands, after the windup finishes.")]
    [SerializeField, Min(0f)] private float autoStrikeDelayHeavy = 0.42f;
    [SerializeField, Min(0f)] private float autoStrikeWindowHeavy = 0.14f;
    [Header("Aggressive Strike (fallback)")]
    [Tooltip("If true, perform an aggressive OverlapSphere strike at the attack origin to catch enemies even if hitboxes miss.")]
    [SerializeField] private bool aggressiveOverlapStrike = true;
    [SerializeField, Min(0f)] private float aggressiveRadius = 1.2f;
    [SerializeField] private Vector3 aggressiveOffset = new Vector3(0f, 0.9f, 1.0f);

    public bool IsAttackDirectionLocked => _attackDirectionLocked;

    private enum BufferedAttackType
    {
        None = 0,
        Light = 1,
        Heavy = 2,
    }

    private struct BufferedAttack
    {
        public BufferedAttackType type;
        public Vector2 blendDir;
        public float queuedAt;
        public float commitUntil;
    }

    private BufferedAttack _bufferedAttack;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        hitBoxController = GetComponent<AttackHitBoxController>();
        if (hitBoxController == null)
        {
            hitBoxController = GetComponentInChildren<AttackHitBoxController>(true);
        }

        EnsureAnimationEventReceiver();

        _attackDirectionReference = attackDirectionReferenceOverride != null
            ? attackDirectionReferenceOverride
            : (Camera.main != null ? Camera.main.transform : transform);

        _attackRotationRoot = attackFacingRootOverride != null ? attackFacingRootOverride : ResolveDefaultAttackFacingRoot();
        _attackTargetRotation = _attackRotationRoot.rotation;
        _hasAttackTargetRotation = false;
    }

    private Transform ResolveDefaultAttackFacingRoot()
    {
        bool AffectsHitBoxes(Transform candidate)
        {
            if (candidate == null || hitBoxController == null)
            {
                return true;
            }

            GameObject[] hitBoxes = hitBoxController.HitBoxes;
            if (hitBoxes == null || hitBoxes.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < hitBoxes.Length; i++)
            {
                GameObject hb = hitBoxes[i];
                if (hb == null)
                {
                    continue;
                }

                Transform hbT = hb.transform;
                if (hbT == candidate || hbT.IsChildOf(candidate))
                {
                    return true;
                }

                IConstraint[] constraints = hb.GetComponents<IConstraint>();
                for (int c = 0; c < constraints.Length; c++)
                {
                    IConstraint constraint = constraints[c];
                    if (constraint == null)
                    {
                        continue;
                    }

                    int sourceCount = constraint.sourceCount;
                    for (int s = 0; s < sourceCount; s++)
                    {
                        ConstraintSource src = constraint.GetSource(s);
                        Transform srcT = src.sourceTransform;
                        if (srcT == null)
                        {
                            continue;
                        }

                        if (srcT == candidate || srcT.IsChildOf(candidate))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // Candidate 1: rotate the Animator object if it's a separate child (often avoids rotating the camera rig root).
        Transform animatorCandidate = (animator != null && animator.transform != transform) ? animator.transform : null;
        if (animatorCandidate != null && AffectsHitBoxes(animatorCandidate))
        {
            return animatorCandidate;
        }

        // Candidate 2: rotate the top-most SkinnedMeshRenderer root under this controller.
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null)
        {
            Transform t = smr.transform;
            while (t.parent != null && t.parent != transform)
            {
                t = t.parent;
            }

            if (t != null && t != transform && AffectsHitBoxes(t))
            {
                return t;
            }
        }

        // Fallback: rotate this GameObject (most reliable for keeping hitboxes aligned with attack direction).
        return transform;
    }

    private void BeginAttackRootMotionOverride()
    {
        if (!disableAnimatorRootMotionDuringAttack || animator == null)
        {
            return;
        }

        // Root motion on attack clips can look like a "teleport" when we rotate attacks left/right.
        _previousAnimatorApplyRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = false;
        _restoreAnimatorRootMotion = true;
    }

    private void RestoreAttackRootMotionOverride()
    {
        if (!_restoreAnimatorRootMotion || animator == null)
        {
            return;
        }

        animator.applyRootMotion = _previousAnimatorApplyRootMotion;
        _restoreAnimatorRootMotion = false;
    }

    private void Update()
    {
        UpdateBufferedAttack();
        TryConsumeBufferedAttack();
    }

    private void LateUpdate()
    {
        // Enforce lock after locomotion updates (prevents movement from overwriting attack blend direction).
        if (_attackDirectionLocked && animator != null)
        {
            animator.SetFloat(SpeedXHash, _lockedAttackBlendDir.x);
            animator.SetFloat(SpeedYHash, _lockedAttackBlendDir.y);
        }

        // Make direction visible even if the character normally keeps facing forward.
        if (_attackDirectionLocked && rotateCharacterToAttackDirection && _hasAttackTargetRotation && _attackRotationRoot != null)
        {
            float step = attackTurnSpeedDegPerSec <= 0f ? 999999f : attackTurnSpeedDegPerSec * Time.deltaTime;
            _attackRotationRoot.rotation = Quaternion.RotateTowards(_attackRotationRoot.rotation, _attackTargetRotation, step);
        }
    }

    private void EnsureAnimationEventReceiver()
    {
        if (animator == null)
        {
            return;
        }

        AttackAnimationEventReceiver receiver = animator.GetComponent<AttackAnimationEventReceiver>();
        if (receiver == null)
        {
            receiver = animator.gameObject.AddComponent<AttackAnimationEventReceiver>();
        }

        receiver.Initialize(this);
    }

    // Input System (PlayerInput -> Send Messages) will call all components on the GameObject with the matching method name.
    public void OnMove(InputAction.CallbackContext ctx)
    {
        SetMoveInput(ctx.ReadValue<Vector2>());
    }

    // Allows other components (e.g. CharacterMovement on a different object) to forward movement input.
    public void SetMoveInput(Vector2 input)
    {
        _rawMoveInput = input;
        UpdateStableMoveDir(_rawMoveInput);
    }

    private void UpdateStableMoveDir(Vector2 input)
    {
        float mag = input.magnitude;
        if (mag >= directionEnterDeadzone)
        {
            _stableMoveDir = QuantizeToCardinal(input);
            _stableMoveDirTime = Time.time;
            _hasStableMoveDir = _stableMoveDir.sqrMagnitude > 0.0001f;
        }
        else if (!_hasStableMoveDir && mag >= directionExitDeadzone)
        {
            _stableMoveDir = QuantizeToCardinal(input);
            _stableMoveDirTime = Time.time;
            _hasStableMoveDir = _stableMoveDir.sqrMagnitude > 0.0001f;
        }
    }

    private Vector2 QuantizeToCardinal(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        // For keyboard (and full-tilt sticks), push to 1.0 so Animator thresholds/blend trees reliably select a direction.
        float axisMag = input.magnitude >= 0.99f ? 1f : attackAxisMagnitude;

        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
        {
            return new Vector2(Mathf.Sign(input.x) * axisMag, 0f);
        }

        return new Vector2(0f, Mathf.Sign(input.y) * axisMag);
    }

    private Vector2 ResolveAttackBlendDirection()
    {
        float now = Time.time;

        // User expectation: if no keys are pressed, attack where the camera looks.
        // This avoids using animator damped values or buffered last direction.
        if (noInputAttacksUseCameraForward && _rawMoveInput.sqrMagnitude < 0.0001f)
        {
            return new Vector2(0f, attackAxisMagnitude);
        }

        // If we didn't receive input directly (e.g. AttackController isn't on the PlayerInput object),
        // fall back to the animator's current locomotion parameters.
        Vector2 effectiveInput = _rawMoveInput;
        if (effectiveInput.sqrMagnitude < 0.0001f && animator != null)
        {
            effectiveInput = new Vector2(animator.GetFloat(SpeedXHash), animator.GetFloat(SpeedYHash));
        }

        float mag = effectiveInput.magnitude;

        // Strong input: accept immediately.
        if (mag >= directionEnterDeadzone)
        {
            Vector2 q = QuantizeToCardinal(effectiveInput);
            if (q.sqrMagnitude > 0.0001f)
            {
                return q;
            }
        }

        // Weak/near-zero input: rely on buffered last direction for a short time.
        if (mag <= directionExitDeadzone)
        {
            if (_hasStableMoveDir && now - _stableMoveDirTime <= directionBufferSeconds)
            {
                return _stableMoveDir;
            }

            // If we have some locomotion direction but it's below enter threshold, still use it.
            if (effectiveInput.sqrMagnitude > 0.0001f)
            {
                Vector2 q = QuantizeToCardinal(effectiveInput);
                if (q.sqrMagnitude > 0.0001f)
                {
                    return q;
                }
            }

            return new Vector2(0f, attackAxisMagnitude);
        }

        // Between exit/enter: hysteresis zone -> keep stable direction if we have one.
        if (_hasStableMoveDir)
        {
            return _stableMoveDir;
        }

        Vector2 fallback = QuantizeToCardinal(effectiveInput);
        return fallback.sqrMagnitude > 0.0001f ? fallback : new Vector2(0f, attackAxisMagnitude);
    }

    private void LockAttackDirection(Vector2 blendDir)
    {
        if (_unlockCoroutine != null)
        {
            StopCoroutine(_unlockCoroutine);
            _unlockCoroutine = null;
        }

        _attackDirectionLocked = true;
        _lockedAttackBlendDir = blendDir;

        if (animator != null)
        {
            animator.SetFloat(SpeedXHash, blendDir.x);
            animator.SetFloat(SpeedYHash, blendDir.y);
        }

        if (!rotateCharacterToAttackDirection || _attackRotationRoot == null)
        {
            return;
        }

        Vector3 localDir = new Vector3(blendDir.x, 0f, blendDir.y);
        if (localDir.sqrMagnitude < 0.0001f)
        {
            _hasAttackTargetRotation = false;
            return;
        }

        // Interpret D/A/W/S relative to the chosen reference (usually the camera), regardless of current character facing.
        Transform reference = _attackDirectionReference != null ? _attackDirectionReference : transform;
        Vector3 worldDir = reference.right * blendDir.x + reference.forward * blendDir.y;
        worldDir = Vector3.ProjectOnPlane(worldDir, _attackRotationRoot.up);
        if (worldDir.sqrMagnitude < 0.0001f)
        {
            _hasAttackTargetRotation = false;
            return;
        }

        _attackTargetRotation = Quaternion.LookRotation(worldDir.normalized, _attackRotationRoot.up);
        _hasAttackTargetRotation = true;
    }

    private void UnlockAttackDirection()
    {
        _attackDirectionLocked = false;
        _lockedAttackBlendDir = Vector2.zero;
        _hasAttackTargetRotation = false;
    }

    // Smoothly blend animator attack direction floats back to resolved movement direction
    private Coroutine _unlockCoroutine;

    private void StartSmoothUnlockAttackDirection(float duration)
    {
        // If already smooth unlocking, restart.
        if (_unlockCoroutine != null)
        {
            StopCoroutine(_unlockCoroutine);
            _unlockCoroutine = null;
        }

        // If animator is null or duration zero, unlock immediately.
        if (animator == null || duration <= 0f)
        {
            UnlockAttackDirection();
            return;
        }

        // Capture current animator floats first, then unlock so LateUpdate doesn't overwrite.
        float startX = animator.GetFloat(SpeedXHash);
        float startY = animator.GetFloat(SpeedYHash);
        UnlockAttackDirection();

        _unlockCoroutine = StartCoroutine(SmoothUnlockRoutine(duration, startX, startY));
    }

    private System.Collections.IEnumerator SmoothUnlockRoutine(float duration, float startX, float startY)
    {
        float startTime = Time.time;
        float endTime = startTime + duration;

        while (Time.time < endTime)
        {
            float t = Mathf.InverseLerp(startTime, endTime, Time.time);
            // Smooth step for nicer curve
            t = Mathf.SmoothStep(0f, 1f, t);

            // Resolve the target blend direction from current input/state
            Vector2 target = ResolveAttackBlendDirection();

            float curX = Mathf.Lerp(startX, target.x, t);
            float curY = Mathf.Lerp(startY, target.y, t);

            if (animator != null)
            {
                animator.SetFloat(SpeedXHash, curX);
                animator.SetFloat(SpeedYHash, curY);
            }

            yield return null;
        }

        // Ensure final values applied then unlock
        Vector2 final = ResolveAttackBlendDirection();
        if (animator != null)
        {
            animator.SetFloat(SpeedXHash, final.x);
            animator.SetFloat(SpeedYHash, final.y);
        }

        _unlockCoroutine = null;
    }

    private void BufferAttack(BufferedAttackType type, Vector2 blendDir)
    {
        float now = Time.time;

        if (attackBufferSeconds <= 0f)
        {
            return;
        }

        // Heavy overrides Light if both are requested in the same window.
        if (_bufferedAttack.type == BufferedAttackType.Heavy && type == BufferedAttackType.Light)
        {
            return;
        }

        if (type == BufferedAttackType.Heavy && _bufferedAttack.type == BufferedAttackType.Light)
        {
            _bufferedAttack.type = BufferedAttackType.None;
        }

        _bufferedAttack.type = type;
        _bufferedAttack.blendDir = blendDir;
        _bufferedAttack.queuedAt = now;
        _bufferedAttack.commitUntil = now + directionCommitSeconds;
    }

    private void UpdateBufferedAttack()
    {
        if (_bufferedAttack.type == BufferedAttackType.None)
        {
            return;
        }

        float now = Time.time;
        float bufferLifetime = IsAttacking ? Mathf.Max(attackBufferSeconds, attackBufferSecondsWhileAttacking) : attackBufferSeconds;
        if (now - _bufferedAttack.queuedAt > bufferLifetime)
        {
            _bufferedAttack.type = BufferedAttackType.None;
            return;
        }

        // Allow a brief window to change direction after buffering.
        if (now <= _bufferedAttack.commitUntil)
        {
            _bufferedAttack.blendDir = ResolveAttackBlendDirection();
        }
    }

    private void TryConsumeBufferedAttack()
    {
        if (_bufferedAttack.type == BufferedAttackType.None)
        {
            return;
        }

        if (IsAttacking)
        {
            return;
        }

        if (_heavyChargeHeld)
        {
            return;
        }

        float now = Time.time;
        float bufferLifetime = IsAttacking ? Mathf.Max(attackBufferSeconds, attackBufferSecondsWhileAttacking) : attackBufferSeconds;
        if (now - _bufferedAttack.queuedAt > bufferLifetime)
        {
            _bufferedAttack.type = BufferedAttackType.None;
            return;
        }

        Vector2 dir = _bufferedAttack.blendDir;
        BufferedAttackType type = _bufferedAttack.type;

        // Clear first to avoid loops if the triggered animation ends instantly.
        _bufferedAttack.type = BufferedAttackType.None;

        bool started = type == BufferedAttackType.Heavy ? TryStartHeavyAttack(dir) : TryStartLightAttack(dir);
        if (!started)
        {
            // If it couldn't start (e.g. stamina), don't keep retrying.
            _bufferedAttack.type = BufferedAttackType.None;
        }
    }

    private bool TryStartLightAttack(Vector2 blendDir)
    {
        // Prevent "ghost" camera charge when the animator won't actually start a new attack.
        if (IsAttacking)
        {
            return false;
        }

        // Conflict rule: heavy charge has priority over light.
        if (_heavyChargeHeld)
        {
            return false;
        }

        // Don't deplete stamina here if your animations already do it via AnimationEvents.
        // Otherwise stamina can be deducted twice and you'll "randomly" stop attacking.
        if (!Game.Instance.PlayerOne.HasStaminaForCost(lightCost))
        {
            return false;
        }

        if (animator == null)
        {
            return false;
        }

        LockAttackDirection(blendDir);
        BeginAttackRootMotionOverride();

        CurrentDamageLevel = DamageMessage.DamageLevel.Small;
        IsAttacking = true;
        _armedStrikeShakeLevel = DamageMessage.DamageLevel.Small;
        _strikeShakeArmed = true;
        _attackStartedAt = Time.time;
        _strikeShakeScheduled = false;
        CancelInvoke(nameof(FireStrikeShake));

        // Small "windup" camera raise for right-click, then it drops on strike.
        _cameraChargeHeld = true;
        CameraImpactShake.TryBeginLightCharge();

        // Failsafe: if the strike AnimationEvent never happens, drop the camera quickly.
        CancelInvoke(nameof(EndLightChargeFallback));
        Invoke(nameof(EndLightChargeFallback), lightChargeFallbackSeconds);

        CancelInvoke(nameof(ForceStopAttacking));
        Invoke(nameof(ForceStopAttacking), attackFailsafeSeconds);

        animator.SetTrigger("Attack");
        if (autoStrikeIfNoEvent && hitBoxController != null)
        {
            StartCoroutine(AutoStrikeWindowRoutine(autoStrikeDelayLight, autoStrikeWindowLight));
        }
        return true;
    }

    private bool TryStartHeavyAttack(Vector2 blendDir)
    {
        if (IsAttacking)
        {
            return false;
        }

        // Don't deplete stamina here if your animations already do it via AnimationEvents.
        if (!Game.Instance.PlayerOne.HasStaminaForCost(heavyCost))
        {
            _heavyChargeHeld = false;
            _cameraChargeHeld = false;
            CameraImpactShake.TryEndCharge();
            return false;
        }

        if (animator == null)
        {
            _heavyChargeHeld = false;
            _cameraChargeHeld = false;
            CameraImpactShake.TryEndCharge();
            return false;
        }

        LockAttackDirection(blendDir);
        BeginAttackRootMotionOverride();

        CurrentDamageLevel = DamageMessage.DamageLevel.Big;
        IsAttacking = true;
        _armedStrikeShakeLevel = DamageMessage.DamageLevel.Big;
        _strikeShakeArmed = true;
        _attackStartedAt = Time.time;
        _strikeShakeScheduled = false;
        CancelInvoke(nameof(FireStrikeShake));

        // If we started from a buffered/performed input, ensure we don't start a delayed charge.
        CancelInvoke(nameof(BeginHeavyChargeDelayed));

        CancelInvoke(nameof(ForceStopAttacking));
        Invoke(nameof(ForceStopAttacking), attackFailsafeSeconds);

        animator.SetTrigger("HeavyAttack");
        if (autoStrikeIfNoEvent && hitBoxController != null)
        {
            StartCoroutine(AutoStrikeWindowRoutine(autoStrikeDelayHeavy, autoStrikeWindowHeavy));
        }
        return true;
    }

    public void OnlightAttack(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            Debug.Log($"[AttackController] OnlightAttack received but not performed (phase={context.phase})", this);
            return;
        }
        Debug.Log($"[AttackController] OnlightAttack performed on '{gameObject.name}' (IsAttacking={IsAttacking})", this);

        Vector2 dir = ResolveAttackBlendDirection();

        // Always buffer if buffering is enabled. This lets direction resolve correctly even when direction+attack happen in the same frame.
        if (attackBufferSeconds > 0f)
        {
            BufferAttack(BufferedAttackType.Light, dir);
            return;
        }

        if (_heavyChargeHeld)
        {
            return;
        }

        if (IsAttacking)
        {
            return;
        }

        TryStartLightAttack(dir);
    }

    // Input System (PlayerInput -> Send Messages) expects exact "On<ActionName>" method names.
    public void OnLightAttack(InputAction.CallbackContext context)
    {
        OnlightAttack(context);
    }

    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        // Charging phase (press/hold)
        if (context.started)
        {
            if (IsAttacking)
            {
                return;
            }

            if (!Game.Instance.PlayerOne.HasStaminaForCost(heavyCost))
            {
                return;
            }

            _heavyChargeHeld = true;
            _cameraChargeHeld = true;
            // Start the camera raise only if the player actually holds long enough.
            CancelInvoke(nameof(BeginHeavyChargeDelayed));
            if (heavyChargeStartDelaySeconds <= 0f)
            {
                BeginHeavyChargeDelayed();
            }
            else
            {
                Invoke(nameof(BeginHeavyChargeDelayed), heavyChargeStartDelaySeconds);
            }
            return;
        }

        // Release before charge completes (cancel)
        if (context.canceled)
        {
            CancelInvoke(nameof(BeginHeavyChargeDelayed));

            // If an attack is already in progress, keep the camera up until the strike moment.
            if (!IsAttacking)
            {
                _heavyChargeHeld = false;
                _cameraChargeHeld = false;
                CameraImpactShake.TryEndCharge();
            }

            return;
        }

        // Attack fired (after Hold interaction performs)
        if (!context.performed)
        {
            return;
        }

        // Hold interaction performed: stop any pending delayed raise; from here we either attack or buffer.
        CancelInvoke(nameof(BeginHeavyChargeDelayed));

        Vector2 dir = ResolveAttackBlendDirection();

        if (IsAttacking)
        {
            BufferAttack(BufferedAttackType.Heavy, dir);

            // If we were charging but couldn't fire yet, fail closed and drop the charge pose.
            if (_cameraChargeHeld)
            {
                _heavyChargeHeld = false;
                _cameraChargeHeld = false;
                CameraImpactShake.TryEndCharge();
            }

            return;
        }

        TryStartHeavyAttack(dir);
    }

    private void BeginHeavyChargeDelayed()
    {
        // Only raise during the dedicated charge phase.
        if (_heavyChargeHeld && _cameraChargeHeld && !IsAttacking)
        {
            CameraImpactShake.TryBeginCharge();
        }
    }

    private void ScheduleStrikeShake()
    {
        float minDelay = _armedStrikeShakeLevel == DamageMessage.DamageLevel.Big
            ? heavyStrikeShakeMinDelaySeconds
            : lightStrikeShakeMinDelaySeconds;

        float elapsed = Time.time - _attackStartedAt;
        float remaining = Mathf.Max(0f, minDelay - elapsed);

        CancelInvoke(nameof(FireStrikeShake));

        if (remaining <= 0f)
        {
            FireStrikeShake();
            return;
        }

        _strikeShakeScheduled = true;
        Invoke(nameof(FireStrikeShake), remaining);
    }

    private void FireStrikeShake()
    {
        _strikeShakeScheduled = false;
        CameraImpactShake.TryImpact(_armedStrikeShakeLevel);
    }

    public void depleteStamina(float amount)
    {
        Game.Instance.PlayerOne.DepleteStamina(amount);
    }

    // AnimationEvent compatibility (common naming variants)
    public void DepleteStamina(float amount)
    {
        depleteStamina(amount);
    }

    public void depleteStaminaWithParameter(string parameter)
    {
        if (animator == null)
        {
            return;
        }

        float motionvalue = animator.GetFloat(parameter);
        depleteStamina(motionvalue);
    }

    // Matches the AnimationEvent name shown in the console.
    public void DepleteStaminaWithParameters(string parameter)
    {
        depleteStaminaWithParameter(parameter);
    }

    public void DepleteStaminaWithParameter(string parameter)
    {
        depleteStaminaWithParameter(parameter);
    }

    public void ToggleAttackHitBox(int hitboxId)
    {
        Debug.Log($"[AttackController] ToggleAttackHitBox called id={hitboxId} on '{gameObject.name}' (hasHitBoxController={(hitBoxController!=null)})", this);
        if (_strikeShakeArmed)
        {
            float minDelay = GetStrikeWindowMinDelaySeconds();
            float elapsed = Time.time - _attackStartedAt;
            if (elapsed < minDelay)
            {
                StartPendingHitboxOpen(hitboxId, minDelay - elapsed);
                return;
            }
        }

        TryOpenAttackHitBox(hitboxId);

        // Strike moment: fire exactly one shake per attack (synced to animation hitbox window).
        if (_strikeShakeArmed)
        {
            _strikeShakeArmed = false;
            _heavyChargeHeld = false;
            _cameraChargeHeld = false;
            CancelInvoke(nameof(EndLightChargeFallback));
            CancelInvoke(nameof(BeginHeavyChargeDelayed));

            // Drop the camera from the charge pose right as the strike happens.
            CameraImpactShake.TrySnapDownCharge();

            // One shake per attack type, but never earlier than a small delay after attack start.
            // This fixes combos/FBX clips that have AnimationEvents at time 0.
            ScheduleStrikeShake();
        }
    }

    // AnimationEvent compatibility (no-parameter event)
    public void ToggleAttackHitBox()
    {
        ToggleAttackHitBox(-1);
    }

    public void cleanupAttackHitBox()
    {
        IsAttacking = false;
        UnlockAttackDirection();
        RestoreAttackRootMotionOverride();

        CancelPendingHitboxOpen();

        CancelInvoke(nameof(ForceStopAttacking));
        CancelInvoke(nameof(EndLightChargeFallback));
        CancelInvoke(nameof(BeginHeavyChargeDelayed));
        CancelInvoke(nameof(FireStrikeShake));

        _strikeShakeArmed = false;
        _heavyChargeHeld = false;
        _cameraChargeHeld = false;
        CameraImpactShake.TryEndCharge();

        // Smoothly blend animator parameters back to movement to avoid abrupt pose snap.
        StartSmoothUnlockAttackDirection(0.22f);

        if (hitBoxController == null)
        {
            TryConsumeBufferedAttack();
            return;
        }

        hitBoxController.cleanupHitBoxes();
        
        // Clear the per-window hit registry so the next attack can hit the same targets.
        AttackHitBox.ClearWindowHits();

        TryConsumeBufferedAttack();
    }

    public void CleanupAttackHitBox()
    {
        cleanupAttackHitBox();
    }

    private void ForceStopAttacking()
    {
        IsAttacking = false;
        UnlockAttackDirection();
        RestoreAttackRootMotionOverride();
        CancelPendingHitboxOpen();

        CancelInvoke(nameof(EndLightChargeFallback));
        CancelInvoke(nameof(BeginHeavyChargeDelayed));
        CancelInvoke(nameof(FireStrikeShake));

        // If we somehow never reached the strike moment, return camera to normal.
        if (_cameraChargeHeld)
        {
            _heavyChargeHeld = false;
            _cameraChargeHeld = false;
            CameraImpactShake.TryEndCharge();
        }

        // Smoothly blend animator parameters back to movement to avoid abrupt pose snap.
        StartSmoothUnlockAttackDirection(0.22f);

        _strikeShakeArmed = false;
        TryConsumeBufferedAttack();
    }

    private System.Collections.IEnumerator AutoStrikeWindowRoutine(float delay, float window)
    {
        // Wait until strike moment
        yield return new WaitForSeconds(delay);

        if (hitBoxController != null)
        {
            // Activate hitboxes explicitly so the strike window stays aligned with the impact frame.
            hitBoxController.SetHitBoxesActive(-1, true);
            // Also perform direct damage fallback to catch collisions if physics misses
            DirectDamageFromHitboxes();
            if (aggressiveOverlapStrike)
            {
                AggressiveOverlapStrike();
            }
        }

        yield return new WaitForSeconds(window);

        if (hitBoxController != null)
        {
            // Deactivate hitboxes
            hitBoxController.cleanupHitBoxes();
        }
    }

    private float GetStrikeWindowMinDelaySeconds()
    {
        return _armedStrikeShakeLevel == DamageMessage.DamageLevel.Big
            ? Mathf.Max(heavyStrikeShakeMinDelaySeconds, autoStrikeDelayHeavy)
            : lightStrikeShakeMinDelaySeconds;
    }

    private void TryOpenAttackHitBox(int hitboxId)
    {
        if (hitBoxController == null)
        {
            return;
        }

        ApplyAttackHitBoxOpen(hitboxId);
    }

    private void StartPendingHitboxOpen(int hitboxId, float delaySeconds)
    {
        CancelPendingHitboxOpen();

        if (delaySeconds <= 0f)
        {
            ApplyAttackHitBoxOpen(hitboxId);
            return;
        }

        _pendingHitboxOpenRoutine = StartCoroutine(DelayedHitboxOpenRoutine(hitboxId, delaySeconds));
    }

    private System.Collections.IEnumerator DelayedHitboxOpenRoutine(int hitboxId, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        _pendingHitboxOpenRoutine = null;

        if (!IsAttacking || hitBoxController == null)
        {
            yield break;
        }

        ApplyAttackHitBoxOpen(hitboxId);
    }

    private void ApplyAttackHitBoxOpen(int hitboxId)
    {
        if (hitBoxController != null)
        {
            hitBoxController.SetHitBoxesActive(hitboxId, true);
        }
    }

    private void CancelPendingHitboxOpen()
    {
        if (_pendingHitboxOpenRoutine != null)
        {
            StopCoroutine(_pendingHitboxOpenRoutine);
            _pendingHitboxOpenRoutine = null;
        }
    }

    // Fallback: perform a manual intersection test against all colliders in scene
    // and directly send damage to any IdamageReceiver found. This ignores physics layer
    // collision rules and is meant as a compatibility/testing fallback.
    private void DirectDamageFromHitboxes()
    {
        if (hitBoxController == null) return;

        var hitBoxes = hitBoxController.HitBoxes;
        if (hitBoxes == null || hitBoxes.Length == 0) return;

        // Gather all colliders once
        var allColliders = FindObjectsOfType<Collider>();

        foreach (var hb in hitBoxes)
        {
            if (hb == null) continue;
            var col = hb.GetComponent<Collider>() ?? hb.GetComponentInChildren<Collider>();
            if (col == null) continue;

            Bounds b = col.bounds;

            foreach (var c in allColliders)
            {
                if (c == null || c.transform == null) continue;
                // skip self-root
                if (c.transform.root == transform.root) continue;

                if (b.Intersects(c.bounds))
                {
                    // try to find a receiver on the collider or its parents
                    IdamageReceiver<DamageMessage> receiver = null;
                    var behaviours = c.GetComponents<MonoBehaviour>();
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        if (behaviours[i] is IdamageReceiver<DamageMessage> r)
                        {
                            receiver = r;
                            break;
                        }
                    }

                    if (receiver == null)
                    {
                        var parentBehaviours = c.GetComponentsInParent<MonoBehaviour>();
                        for (int i = 0; i < parentBehaviours.Length; i++)
                        {
                            if (parentBehaviours[i] is IdamageReceiver<DamageMessage> r)
                            {
                                receiver = r;
                                break;
                            }
                        }
                    }

                    if (receiver != null)
                    {
                        DamageMessage dmg = new DamageMessage();
                        var ahb = hb.GetComponent<AttackHitBox>();
                        float baseAmount = 10f;
                        if (ahb != null)
                        {
                            baseAmount = ahb.GetBaseDamage();
                            dmg.damageLevel = ahb.GetDefaultDamageLevel();
                        }
                        else
                        {
                            dmg.damageLevel = CurrentDamageLevel;
                        }

                        // Apply multiplier based on damage level
                        float multiplier = dmg.damageLevel switch
                        {
                            DamageMessage.DamageLevel.Medium => 1.5f,
                            DamageMessage.DamageLevel.Big => 2f,
                            _ => 1f,
                        };

                        dmg.amount = baseAmount * multiplier;
                        dmg.sender = transform.root.gameObject;

                        try
                        {
                            // Use AttackHitBox window registry to avoid duplicate delivery within this attack window.
                            Transform targetRoot = c.transform.root;
                            if (AttackHitBox.TryRegisterWindowHit(targetRoot))
                            {
                                receiver.ReceiveDamage(dmg);
                                Debug.Log($"[AttackController] DirectDamage sent to {c.gameObject.name} amount={dmg.amount} level={dmg.damageLevel}", this);
                            }
                            else
                            {
                                if (Debug.isDebugBuild)
                                {
                                    Debug.Log($"[AttackController] Skipping DirectDamage to {c.gameObject.name} due to window registry", this);
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[AttackController] DirectDamage failed: {ex}");
                        }
                    }
                }
            }
        }
    }

    private void AggressiveOverlapStrike()
    {
        // Determine origin in world space: relative to this controller's transform
        Vector3 origin = transform.TransformPoint(aggressiveOffset);

        Collider[] hits = Physics.OverlapSphere(origin, aggressiveRadius);
        if (hits == null || hits.Length == 0)
        {
            Debug.Log($"[AttackController] AggressiveOverlapStrike found no colliders at {origin} radius={aggressiveRadius}", this);
            return;
        }

        foreach (var c in hits)
        {
            if (c == null || c.transform == null) continue;
            // Skip self
            if (c.transform.root == transform.root) continue;

            IdamageReceiver<DamageMessage> receiver = null;
            var behaviours = c.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IdamageReceiver<DamageMessage> r)
                {
                    receiver = r;
                    break;
                }
            }

            if (receiver == null)
            {
                var parentBehaviours = c.GetComponentsInParent<MonoBehaviour>();
                for (int i = 0; i < parentBehaviours.Length; i++)
                {
                    if (parentBehaviours[i] is IdamageReceiver<DamageMessage> r)
                    {
                        receiver = r;
                        break;
                    }
                }
            }

            if (receiver != null)
            {
                // Build damage message using any hitbox on the controller if available
                DamageMessage dmg = new DamageMessage();
                float baseAmount = 10f;
                DamageMessage.DamageLevel level = CurrentDamageLevel;
                if (hitBoxController != null && hitBoxController.HitBoxes != null && hitBoxController.HitBoxes.Length > 0)
                {
                    var hb = hitBoxController.HitBoxes[0];
                    if (hb != null)
                    {
                        var ahb = hb.GetComponent<AttackHitBox>();
                        if (ahb != null)
                        {
                            baseAmount = ahb.GetBaseDamage();
                            level = ahb.GetDefaultDamageLevel();
                        }
                    }
                }

                float multiplier = level switch
                {
                    DamageMessage.DamageLevel.Medium => 1.5f,
                    DamageMessage.DamageLevel.Big => 2f,
                    _ => 1f,
                };

                dmg.amount = baseAmount * multiplier;
                dmg.damageLevel = level;
                dmg.sender = transform.root.gameObject;

                try
                {
                    receiver.ReceiveDamage(dmg);
                    Debug.Log($"[AttackController] AggressiveOverlapStrike sent damage to {c.gameObject.name} amount={dmg.amount} level={dmg.damageLevel}", this);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AttackController] AggressiveOverlapStrike failed: {ex}");
                }
            }
        }
    }

    private void EndLightChargeFallback()
    {
        // Only affects the light windup: heavy uses hold/release + strike snap.
        if (_cameraChargeHeld && !_heavyChargeHeld && _strikeShakeArmed)
        {
            _cameraChargeHeld = false;
            CameraImpactShake.TryEndCharge();
        }
    }

}
