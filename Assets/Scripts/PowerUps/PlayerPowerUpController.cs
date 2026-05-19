using UnityEngine;

public class PlayerPowerUpController : MonoBehaviour
{
    private PlayerCombatFacade playerFacade;
    private CharacterStateCombatAdapter basePlayer;
    private InvulnerabilityDecorator invulnerabilityDecorator;
    private DamageMultiplierDecorator damageDecorator;
    private PowerUpFeedbackUI feedbackUI;

    // Global outgoing damage multiplier used by attack damage sources.
    public static float OutgoingDamageMultiplier { get; private set; } = 1f;

    private void Awake()
    {
        ResolveFeedbackUI();
    }

    public void Initialize(PlayerCombatFacade facade, CharacterStateCombatAdapter basePlayer)
    {
        playerFacade = facade;
        this.basePlayer = basePlayer;
        ResolveFeedbackUI();

        // Do not create the feedback UI automatically. If you add a PowerUpFeedbackUI
        // component manually in the scene (e.g. on the Game object), it will be used.

        if (playerFacade != null && this.basePlayer != null && invulnerabilityDecorator == null)
        {
            playerFacade.SetCurrent(this.basePlayer);
        }
    }

    private void Update()
    {
        if (feedbackUI == null)
        {
            ResolveFeedbackUI();
        }

        bool changed = false;

        // invulnerability lifecycle
        if (invulnerabilityDecorator != null && !invulnerabilityDecorator.IsActive)
        {
            invulnerabilityDecorator = null;
            changed = true;
        }

        // damage multiplier lifecycle
        if (damageDecorator != null && !damageDecorator.IsActive)
        {
            damageDecorator = null;
            changed = true;
        }

        if (changed)
        {
            RebuildCombatChain();
            OutgoingDamageMultiplier = damageDecorator != null && damageDecorator.IsActive
                ? damageDecorator.Multiplier
                : 1f;
        }

        if (feedbackUI != null && invulnerabilityDecorator != null)
        {
            feedbackUI.SetInvulnerabilityActive(true, invulnerabilityDecorator.RemainingSeconds);
        }
    }

    public void ApplyInvulnerability(float durationSeconds)
    {
        if (playerFacade == null || basePlayer == null)
        {
            return;
        }

        if (invulnerabilityDecorator == null)
        {
            invulnerabilityDecorator = new InvulnerabilityDecorator(basePlayer, durationSeconds);
            playerFacade.SetCurrent(invulnerabilityDecorator);
        }
        else
        {
            invulnerabilityDecorator.Refresh(durationSeconds);
        }

        if (feedbackUI != null)
        {
            feedbackUI.SetInvulnerabilityActive(true, invulnerabilityDecorator.RemainingSeconds);
        }
    }

    public void ApplyDamageMultiplier(float durationSeconds, float multiplier)
    {
        if (playerFacade == null || basePlayer == null) return;

        if (damageDecorator == null || !damageDecorator.IsActive)
        {
            damageDecorator = new DamageMultiplierDecorator(basePlayer, multiplier, durationSeconds);
        }
        else
        {
            damageDecorator.Refresh(durationSeconds, multiplier);
        }

        RebuildCombatChain();
        OutgoingDamageMultiplier = multiplier;
        DamageBoostRuntime.Activate(multiplier, durationSeconds);
    }

    public void ApplyDoubleDamage(float durationSeconds)
    {
        ApplyDamageMultiplier(durationSeconds, 2f);
    }

    public void ResetPowerUps()
    {
        invulnerabilityDecorator = null;
        damageDecorator = null;
        OutgoingDamageMultiplier = 1f;
        DamageBoostRuntime.Reset();

        if (playerFacade != null && basePlayer != null)
        {
            playerFacade.SetCurrent(basePlayer);
        }

        if (feedbackUI != null)
        {
            feedbackUI.SetInvulnerabilityActive(false, 0f);
        }
    }

    private void EndInvulnerability()
    {
        invulnerabilityDecorator = null;

        if (playerFacade != null && basePlayer != null)
        {
            playerFacade.SetCurrent(basePlayer);
        }

        if (feedbackUI != null)
        {
            feedbackUI.SetInvulnerabilityActive(false, 0f);
        }
    }

    // Expose read-only properties so a manual HUD can query the current state.
    public float CurrentInvulnerabilityRemaining
    {
        get
        {
            if (invulnerabilityDecorator == null) return 0f;
            return invulnerabilityDecorator.RemainingSeconds;
        }
    }

    public bool IsInvulnerable
    {
        get
        {
            return invulnerabilityDecorator != null && invulnerabilityDecorator.IsActive;
        }
    }

    // Damage multiplier exposure for HUDs
    public float CurrentDamageMultiplierRemaining
    {
        get
        {
            if (damageDecorator == null) return 0f;
            return damageDecorator.RemainingSeconds;
        }
    }

    public bool IsDamageMultiplierActive
    {
        get
        {
            return damageDecorator != null && damageDecorator.IsActive;
        }
    }

    public float CurrentOutgoingDamageMultiplier => OutgoingDamageMultiplier;

    public static void SetOutgoingDamageMultiplier(float multiplier)
    {
        OutgoingDamageMultiplier = Mathf.Max(0f, multiplier);
    }

    public static void ResetOutgoingDamageMultiplier()
    {
        OutgoingDamageMultiplier = 1f;
    }

    private void RebuildCombatChain()
    {
        if (playerFacade == null || basePlayer == null)
        {
            return;
        }

        IPlayerCombat inner = basePlayer;

        if (damageDecorator != null && damageDecorator.IsActive)
        {
            // damageDecorator was created with basePlayer as inner
            inner = damageDecorator;
        }

        if (invulnerabilityDecorator != null && invulnerabilityDecorator.IsActive)
        {
            // wrap invulnerability around the existing inner so it can block damage
            float remaining = invulnerabilityDecorator.RemainingSeconds;
            invulnerabilityDecorator = new InvulnerabilityDecorator(inner, remaining);
            playerFacade.SetCurrent(invulnerabilityDecorator);
            return;
        }

        playerFacade.SetCurrent(inner);
    }

    private void ResolveFeedbackUI()
    {
        if (feedbackUI != null)
        {
            return;
        }

        feedbackUI = GetComponent<PowerUpFeedbackUI>();
        if (feedbackUI != null)
        {
            return;
        }

        feedbackUI = FindFirstObjectByType<PowerUpFeedbackUI>(FindObjectsInactive.Include);
    }
}
