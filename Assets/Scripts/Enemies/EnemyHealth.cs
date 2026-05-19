using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IdamageReceiver<DamageMessage>
{
    [SerializeField] private float maxHealth = 40f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 0.1f;

    private float currentHealth;
    private bool dead;

    public float CurrentHealth => currentHealth;
    public bool IsDead => dead;

    public event Action<EnemyHealth> Died;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void ReceiveDamage(DamageMessage damage)
    {
        if (dead) return;

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, damage.amount));

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (dead) return;
        dead = true;

        Died?.Invoke(this);

        var animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
