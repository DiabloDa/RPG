using System;

public abstract class PlayerCombatDecoratorBase : IPlayerCombat
{
    protected readonly IPlayerCombat Inner;

    protected PlayerCombatDecoratorBase(IPlayerCombat inner)
    {
        Inner = inner;
    }

    public virtual float CurrentHealth => Inner != null ? Inner.CurrentHealth : 0f;
    public virtual float CurrentStamina => Inner != null ? Inner.CurrentStamina : 0f;
    public virtual bool IsDead => Inner != null && Inner.IsDead;
    public virtual bool IsInvulnerable => Inner != null && Inner.IsInvulnerable;

    public virtual event Action Died
    {
        add
        {
            if (Inner != null)
            {
                Inner.Died += value;
            }
        }
        remove
        {
            if (Inner != null)
            {
                Inner.Died -= value;
            }
        }
    }

    public virtual bool HasStaminaForCost(float staminaCost)
    {
        return Inner != null && Inner.HasStaminaForCost(staminaCost);
    }

    public virtual bool TryDepleteStamina(float staminaCost)
    {
        return Inner != null && Inner.TryDepleteStamina(staminaCost);
    }

    public virtual void DepleteStamina(float staminaCost)
    {
        Inner?.DepleteStamina(staminaCost);
    }

    public virtual void DepleteHealth(float healthDepletion, out bool zeroHealth)
    {
        zeroHealth = false;
        Inner?.DepleteHealth(healthDepletion, out zeroHealth);
    }
}
