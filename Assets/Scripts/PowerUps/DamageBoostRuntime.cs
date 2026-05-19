using UnityEngine;

public static class DamageBoostRuntime
{
    private static float _multiplier = 1f;
    private static float _expiresAt = -1f;

    public static void Activate(float multiplier, float durationSeconds)
    {
        _multiplier = Mathf.Max(0f, multiplier);
        _expiresAt = Time.time + Mathf.Max(0f, durationSeconds);
    }

    public static float CurrentMultiplier
    {
        get
        {
            if (Time.time >= _expiresAt)
            {
                return 1f;
            }

            return _multiplier;
        }
    }

    public static float RemainingSeconds
    {
        get
        {
            if (Time.time >= _expiresAt)
            {
                return 0f;
            }

            return Mathf.Max(0f, _expiresAt - Time.time);
        }
    }

    public static bool IsActive => CurrentMultiplier > 1f;

    public static void Reset()
    {
        _multiplier = 1f;
        _expiresAt = -1f;
    }
}