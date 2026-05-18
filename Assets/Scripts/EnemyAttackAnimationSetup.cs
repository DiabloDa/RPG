using UnityEngine;

/// <summary>
/// Ensures the enemy's Animator has an AttackAnimationEventReceiver to handle animation events.
/// Attach this to the enemy root (same place as EnemyAI).
/// </summary>
public class EnemyAttackAnimationSetup : MonoBehaviour
{
    private void Awake()
    {
        var animator = GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        var receiver = animator.GetComponent<AttackAnimationEventReceiver>();
        if (receiver == null)
        {
            receiver = animator.gameObject.AddComponent<AttackAnimationEventReceiver>();
            Debug.Log($"[EnemyAttackAnimationSetup] Added AttackAnimationEventReceiver to '{animator.gameObject.name}'", this);
        }
    }
}
