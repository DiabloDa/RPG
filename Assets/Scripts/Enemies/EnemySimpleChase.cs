using UnityEngine;

public class EnemySimpleChase : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float stopDistance = 1.1f;
    [SerializeField] private bool keepY = true;

    private float _pausedUntil;

    public void PauseForSeconds(float seconds)
    {
        if (seconds <= 0f) return;
        _pausedUntil = Mathf.Max(_pausedUntil, Time.time + seconds);
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
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        moveSpeed *= Mathf.Max(0f, multiplier);
    }

    private void Update()
    {
        if (Time.time < _pausedUntil)
        {
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
        if (sqrDist < stopDistance * stopDistance) return;

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
