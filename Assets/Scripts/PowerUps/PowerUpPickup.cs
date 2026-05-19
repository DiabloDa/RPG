using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerUpPickup : MonoBehaviour
{
    public enum PowerUpKind
    {
        Invulnerability = 0,
        DoubleDamage = 1,
    }

    [SerializeField] private PowerUpKind powerUpKind = PowerUpKind.Invulnerability;
    [SerializeField, Min(0f)] private float durationSeconds = 5f;
    [SerializeField] private bool destroyOnPickup = true;

    public void Configure(PowerUpKind kind, float duration, bool shouldDestroyOnPickup = true)
    {
        powerUpKind = kind;
        durationSeconds = Mathf.Max(0f, duration);
        destroyOnPickup = shouldDestroyOnPickup;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!LooksLikePlayer(other))
        {
            return;
        }

        switch (powerUpKind)
        {
            case PowerUpKind.Invulnerability:
                if (Game.Instance != null && Game.Instance.PlayerPowerUps != null)
                {
                    Game.Instance.PlayerPowerUps.ApplyInvulnerability(durationSeconds);
                }
                break;
            case PowerUpKind.DoubleDamage:
                DamageBoostRuntime.Activate(2f, durationSeconds);
                if (Game.Instance != null && Game.Instance.PlayerPowerUps != null)
                {
                    Game.Instance.PlayerPowerUps.ApplyDoubleDamage(durationSeconds);
                }
                break;
        }

        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
    }

    private static bool LooksLikePlayer(Collider col)
    {
        if (col == null)
        {
            return false;
        }

        Transform root = col.transform != null ? col.transform.root : null;
        if (root != null)
        {
            if (root.GetComponentInChildren<AttackController>(true) != null) return true;
            if (root.GetComponentInChildren<Clases.Clase_2.Scripts.CharacterMovement>(true) != null) return true;
            if (root.GetComponentInChildren<CharacterState>(true) != null) return true;
        }

        if (col.GetComponentInParent<AttackController>() != null) return true;
        if (col.GetComponentInParent<Clases.Clase_2.Scripts.CharacterMovement>() != null) return true;
        if (col.GetComponentInParent<CharacterState>() != null) return true;

        if (col.CompareTag("Player")) return true;
        if (root != null && root.CompareTag("Player")) return true;

        return false;
    }
}
