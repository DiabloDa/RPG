using System;

public sealed class CharacterStateCombatAdapter : IPlayerCombat
{
    private readonly CharacterState characterState;

    public CharacterStateCombatAdapter(CharacterState characterState)
    {
        this.characterState = characterState;
    }

    public float CurrentHealth => characterState != null ? characterState.CurrentHealth : 0f;
    public float CurrentStamina => characterState != null ? characterState.CurrentStamina : 0f;
    public bool IsDead => characterState != null && characterState.IsDead;
    public bool IsInvulnerable => false;

    public event Action Died
    {
        add
        {
            if (characterState != null)
            {
                characterState.Died += value;
            }
        }
        remove
        {
            if (characterState != null)
            {
                characterState.Died -= value;
            }
        }
    }

    public bool HasStaminaForCost(float staminaCost)
    {
        return characterState != null && characterState.HasStaminaForCost(staminaCost);
    }

    public bool TryDepleteStamina(float staminaCost)
    {
        return characterState != null && characterState.TryDepleteStamina(staminaCost);
    }

    public void DepleteStamina(float staminaCost)
    {
        characterState?.DepleteStamina(staminaCost);
    }

    public void DepleteHealth(float healthDepletion, out bool zeroHealth)
    {
        if (characterState == null)
        {
            zeroHealth = false;
            return;
        }

        characterState.DepleteHealth(healthDepletion, out zeroHealth);
    }
}
