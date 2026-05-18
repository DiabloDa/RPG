using System;
using UnityEngine;

public class DamageMultiplierDecorator : PlayerCombatDecoratorBase
{
    private float multiplier;
    private float expiresAt;

    public DamageMultiplierDecorator(IPlayerCombat inner, float multiplier, float durationSeconds) : base(inner)
    {
        this.multiplier = Math.Max(0f, multiplier);
        this.expiresAt = Time.time + Math.Max(0f, durationSeconds);
    }

    public override void DepleteHealth(float healthDepletion, out bool zeroHealth)
    {
        if (Time.time >= expiresAt)
        {
            // expired: forward to inner and let caller handle swapping back
            base.DepleteHealth(healthDepletion, out zeroHealth);
            return;
        }

        float scaled = healthDepletion * multiplier;
        base.DepleteHealth(scaled, out zeroHealth);
    }

    public void Refresh(float durationSeconds, float newMultiplier)
    {
        expiresAt = Time.time + Math.Max(0f, durationSeconds);
        multiplier = Math.Max(0f, newMultiplier);
    }

    public float RemainingSeconds => Math.Max(0f, expiresAt - Time.time);
    public bool IsActive => Time.time < expiresAt;
}
