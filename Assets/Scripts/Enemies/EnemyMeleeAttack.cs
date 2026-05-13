using UnityEngine;

public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float damage = 6f;
    [SerializeField] private float attackRange = 1.35f;
    [SerializeField] private float attackCooldown = 0.25f;
    [SerializeField, Min(0f)] private float pauseAfterHitSeconds = 2.5f;
    [SerializeField] private DamageMessage.DamageLevel damageLevel = DamageMessage.DamageLevel.Small;

    [Header("Targeting")]
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private Transform attackOrigin;
    [SerializeField, Min(0f)] private float attackOriginHeight = 1.0f;

    private float nextAttackTime;

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
                float lockout = Mathf.Max(attackCooldown, pauseAfterHitSeconds);
                nextAttackTime = Time.time + Mathf.Max(0.05f, lockout);
                return;
            }
        }
    }

    private void ApplyPostHitPause()
    {
        float pause = Mathf.Max(0f, pauseAfterHitSeconds);
        if (pause <= 0f) return;

        var chase = GetComponent<EnemySimpleChase>();
        if (chase != null)
        {
            chase.PauseForSeconds(pause);
        }

        var ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.PauseForSeconds(pause);
        }
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
