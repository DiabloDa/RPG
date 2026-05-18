using System;

public sealed class PlayerCombatFacade : IPlayerCombat
{
    private IPlayerCombat current;
    private event Action died;

    public PlayerCombatFacade(IPlayerCombat basePlayer)
    {
        SetCurrent(basePlayer);
    }

    public float CurrentHealth => current != null ? current.CurrentHealth : 0f;
    public float CurrentStamina => current != null ? current.CurrentStamina : 0f;
    public bool IsDead => current != null && current.IsDead;
    public bool IsInvulnerable => current != null && current.IsInvulnerable;

    public event Action Died
    {
        add => died += value;
        remove => died -= value;
    }

    public bool HasStaminaForCost(float staminaCost)
    {
        return current != null && current.HasStaminaForCost(staminaCost);
    }

    public bool TryDepleteStamina(float staminaCost)
    {
        return current != null && current.TryDepleteStamina(staminaCost);
    }

    public void DepleteStamina(float staminaCost)
    {
        current?.DepleteStamina(staminaCost);
    }

    public void DepleteHealth(float healthDepletion, out bool zeroHealth)
    {
        if (current == null)
        {
            zeroHealth = false;
            return;
        }

        current.DepleteHealth(healthDepletion, out zeroHealth);
    }

    public void SetCurrent(IPlayerCombat newCurrent)
    {
        if (ReferenceEquals(current, newCurrent))
        {
            return;
        }

        if (current != null)
        {
            current.Died -= ForwardDeathEvent;
        }

        current = newCurrent;

        if (current != null)
        {
            current.Died += ForwardDeathEvent;
        }
    }

    private void ForwardDeathEvent()
    {
        died?.Invoke();
    }
}
