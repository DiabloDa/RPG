using UnityEngine;

public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float damage = 6f;
    [SerializeField, Min(0.05f)] private float attackRange = 0.9f;
    [SerializeField] private float attackCooldown = 0.25f;
    [SerializeField, Min(0f)] private float pauseAfterHitSeconds = 2.5f;
    [SerializeField] private DamageMessage.DamageLevel damageLevel = DamageMessage.DamageLevel.Small;

    [Header("Attack Animation (optional)")]
    [SerializeField] private bool playAttackAnimation = true;
    [SerializeField] private string attackTriggerParam = "Attack";
    [Tooltip("Animator state name to wait for. If it doesn't match, we fall back to pauseAfterHitSeconds.")]
    [SerializeField] private string attackStateName = "Zombie Attack";
    [SerializeField, Min(0f)] private float attackAnimFallbackSeconds = 0.9f;

    [Header("Targeting")]
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private Transform attackOrigin;
    [SerializeField, Min(0f)] private float attackOriginHeight = 1.0f;

    private float nextAttackTime;

    private Animator _enemyAnimator;
    private int _attackTriggerHash;
    private bool _hasAttackTrigger;
    private int _attackStateHash;
    private Coroutine _attackRoutine;

    private void Awake()
    {
        _enemyAnimator = GetComponentInChildren<Animator>(true);
        CacheAttackAnimatorParams();
    }

    private void CacheAttackAnimatorParams()
    {
        _hasAttackTrigger = false;
        _attackTriggerHash = Animator.StringToHash(attackTriggerParam);
        _attackStateHash = Animator.StringToHash(attackStateName);

        if (_enemyAnimator == null)
        {
            return;
        }

        var parameters = _enemyAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == attackTriggerParam && parameters[i].type == AnimatorControllerParameterType.Trigger)
            {
                _hasAttackTrigger = true;
                break;
            }
        }
    }

    private void Update()
    {
        if (Time.time < nextAttackTime) return;

        Vector3 origin = attackOrigin != null ? attackOrigin.position : (transform.position + Vector3.up * attackOriginHeight);

        var hits = Physics.OverlapSphere(origin, attackRange, hitMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (col == null || col.transform == null) continue;

            // Ignore self
            if (col.transform.root == transform.root) continue;

            if (!LooksLikePlayer(col))
            {
                continue;
            }

            // Apply gameplay damage directly. The player's DamageController can enable iFrames and
            // block repeated hits; for now we want reliable periodic damage while the enemy is in range.
            if (Game.Instance != null && Game.Instance.PlayerOne != null)
            {
                Game.Instance.PlayerOne.DepleteHealth(damage, out bool isDead);

                // Still play the damage reaction animation (without relying on DamageController for health).
                Transform root = col.transform.root;
                if (root != null)
                {
                    var damageController = root.GetComponentInChildren<DamageController>(true);
                    if (damageController != null)
                    {
                        damageController.PlayDamageReaction(transform.root, damageLevel, isDead);
                    }
                }

                ApplyPostHitPause();
                float lockout = Mathf.Max(attackCooldown, pauseAfterHitSeconds, attackAnimFallbackSeconds);
                nextAttackTime = Time.time + Mathf.Max(0.05f, lockout);
                return;
            }
        }
    }

    private void ApplyPostHitPause()
    {
        // Prefer pausing until the attack animation finishes.
        if (playAttackAnimation && _enemyAnimator != null && _hasAttackTrigger)
        {
            _enemyAnimator.ResetTrigger(_attackTriggerHash);
            _enemyAnimator.SetTrigger(_attackTriggerHash);

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
            }

            _attackRoutine = StartCoroutine(PauseUntilAttackAnimationEnds());
            return;
        }

        // Fallback: fixed pause time.
        float pause = Mathf.Max(0f, pauseAfterHitSeconds);
        if (pause <= 0f) return;
        PauseMovementForSeconds(pause);
    }

    private void PauseMovementForSeconds(float seconds)
    {
        var chase = GetComponent<EnemySimpleChase>();
        if (chase != null)
        {
            chase.PauseForSeconds(seconds);
        }

        var ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.PauseForSeconds(seconds);
        }
    }

    private void ResumeMovementNow()
    {
        var chase = GetComponent<EnemySimpleChase>();
        if (chase != null)
        {
            chase.ResumeNow();
        }

        var ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.ResumeNow();
        }
    }

    private System.Collections.IEnumerator PauseUntilAttackAnimationEnds()
    {
        // Ensure the enemy stays still while the attack plays.
        PauseMovementForSeconds(999f);

        float minPauseEnd = Time.time + Mathf.Max(0f, pauseAfterHitSeconds);

        // Wait a moment for the animator to enter the attack state.
        float enterTimeout = Time.time + 0.35f;
        bool entered = false;

        while (Time.time < enterTimeout)
        {
            if (IsInAttackState())
            {
                entered = true;
                break;
            }

            yield return null;
        }

        float hardTimeout = Time.time + Mathf.Max(0.1f, attackAnimFallbackSeconds);

        if (entered)
        {
            // Wait until the attack finishes (normalizedTime >= 1) or state changes.
            while (Time.time < hardTimeout)
            {
                if (!IsInAttackState())
                {
                    break;
                }

                var s = _enemyAnimator.GetCurrentAnimatorStateInfo(0);
                if (s.normalizedTime >= 1f)
                {
                    break;
                }

                yield return null;
            }
        }

        // Guarantee minimum pause if requested.
        while (Time.time < minPauseEnd)
        {
            yield return null;
        }

        ResumeMovementNow();
        _attackRoutine = null;
    }

    private bool IsInAttackState()
    {
        if (_enemyAnimator == null) return false;

        var cur = _enemyAnimator.GetCurrentAnimatorStateInfo(0);
        if (cur.shortNameHash == _attackStateHash || cur.IsName(attackStateName)) return true;

        if (_enemyAnimator.IsInTransition(0))
        {
            var next = _enemyAnimator.GetNextAnimatorStateInfo(0);
            if (next.shortNameHash == _attackStateHash || next.IsName(attackStateName)) return true;
        }

        return false;
    }

    private static bool LooksLikePlayer(Collider col)
    {
        if (col == null) return false;

        // Prefer robust hierarchy check: the collider might be on a limb, while gameplay scripts
        // live on a different child under the same player root.
        Transform root = col.transform != null ? col.transform.root : null;
        if (root != null)
        {
            if (root.GetComponentInChildren<AttackController>(true) != null) return true;
            if (root.GetComponentInChildren<Clases.Clase_2.Scripts.CharacterMovement>(true) != null) return true;
        }

        // This project uses these components on the player rig.
        if (col.GetComponentInParent<AttackController>() != null) return true;
        if (col.GetComponentInParent<Clases.Clase_2.Scripts.CharacterMovement>() != null) return true;

        // Optional: tagged player.
        if (col.CompareTag("Player")) return true;
        if (root != null && root.CompareTag("Player")) return true;

        return false;
    }

    private static IdamageReceiver<DamageMessage> FindDamageReceiver(Collider other)
    {
        // Don't rely on TryGetComponent with interfaces.
        var behaviours = other.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IdamageReceiver<DamageMessage> r) return r;
        }

        var parentBehaviours = other.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < parentBehaviours.Length; i++)
        {
            if (parentBehaviours[i] is IdamageReceiver<DamageMessage> r) return r;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = attackOrigin != null ? attackOrigin.position : (transform.position + Vector3.up * attackOriginHeight);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}
