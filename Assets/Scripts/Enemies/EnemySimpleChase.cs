using UnityEngine;

public class EnemySimpleChase : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField, Min(0f)] private float stopDistance = 0.6f;
    [SerializeField] private bool keepY = true;

    private float _pausedUntil;

    [Header("Animation (optional)")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    private Animator _animator;
    private bool _hasIsMoving;
    private bool _hasMoveSpeed;
    private int _isMovingHash;
    private int _moveSpeedHash;

    public void PauseForSeconds(float seconds)
    {
        if (seconds <= 0f) return;
        _pausedUntil = Mathf.Max(_pausedUntil, Time.time + seconds);
    }

    public void ResumeNow()
    {
        _pausedUntil = 0f;
    }

    private void Awake()
    {
        // Prevent animation root motion from fighting transform-based movement.
        var animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].applyRootMotion = false;
            }
        }

        _animator = GetComponentInChildren<Animator>(true);
        CacheAnimatorParams();
    }

    private void CacheAnimatorParams()
    {
        _hasIsMoving = false;
        _hasMoveSpeed = false;

        if (_animator == null)
        {
            return;
        }

        _isMovingHash = Animator.StringToHash(isMovingParam);
        _moveSpeedHash = Animator.StringToHash(moveSpeedParam);

        var parameters = _animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == isMovingParam)
            {
                _hasIsMoving = parameters[i].type == AnimatorControllerParameterType.Bool;
            }
            else if (parameters[i].name == moveSpeedParam)
            {
                _hasMoveSpeed = parameters[i].type == AnimatorControllerParameterType.Float;
            }
        }
    }

    private void SetAnimatorMoving(bool moving)
    {
        if (_animator == null) return;

        if (_hasIsMoving)
        {
            _animator.SetBool(_isMovingHash, moving);
        }

        if (_hasMoveSpeed)
        {
            _animator.SetFloat(_moveSpeedHash, moving ? 1f : 0f);
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        moveSpeed *= Mathf.Max(0f, multiplier);
    }

    private void Update()
    {
        if (Time.time < _pausedUntil)
        {
            SetAnimatorMoving(false);
            return;
        }

        if (target == null)
        {
            // Best-effort: find the player in scene so this works even for manually placed enemies.
            var attackController = FindFirstObjectByType<AttackController>();
            if (attackController != null) target = attackController.transform;
            else
            {
                var characterMovement = FindFirstObjectByType<Clases.Clase_2.Scripts.CharacterMovement>();
                if (characterMovement != null) target = characterMovement.transform;
            }

            if (target == null) return;
        }

        Vector3 toTarget = target.position - transform.position;
        if (keepY) toTarget.y = 0f;

        float sqrDist = toTarget.sqrMagnitude;
        if (sqrDist < stopDistance * stopDistance)
        {
            SetAnimatorMoving(false);
            return;
        }

        SetAnimatorMoving(true);

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, turnSpeed * Time.deltaTime);
        }

        Vector3 move = toTarget.normalized * moveSpeed * Time.deltaTime;
        if (keepY) move.y = 0f;
        transform.position += move;
    }
}
