using System;

public interface IPlayerCombat
{
    float CurrentHealth { get; }
    float CurrentStamina { get; }
    bool IsDead { get; }
    bool IsInvulnerable { get; }
    event Action Died;
    bool HasStaminaForCost(float staminaCost);
    bool TryDepleteStamina(float staminaCost);
    void DepleteStamina(float staminaCost);
    void DepleteHealth(float healthDepletion, out bool zeroHealth);
}
