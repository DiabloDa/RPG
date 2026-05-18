using UnityEngine;

/// <summary>
/// Receives AnimationEvents/SendMessage calls on the Animator GameObject and forwards them to the real
/// AttackController (often placed on a parent/root GameObject).
/// </summary>
public class AttackAnimationEventReceiver : MonoBehaviour
{
    [SerializeField] private AttackController attackController;

    public void Initialize(AttackController controller)
    {
        attackController = controller;
    }

    private AttackController ResolveController()
    {
        if (attackController != null)
        {
            return attackController;
        }

        attackController = GetComponentInParent<AttackController>();
        return attackController;
    }

    // --- Stamina ---

    // Matches console error: AnimationEvent "DepleteStaminaWithParameters"
    public void DepleteStaminaWithParameters(string parameter)
    {
        ResolveController()?.DepleteStaminaWithParameters(parameter);
    }

    public void DepleteStaminaWithParameter(string parameter)
    {
        ResolveController()?.DepleteStaminaWithParameter(parameter);
    }

    public void depleteStaminaWithParameter(string parameter)
    {
        ResolveController()?.depleteStaminaWithParameter(parameter);
    }

    public void DepleteStamina(float amount)
    {
        ResolveController()?.DepleteStamina(amount);
    }

    public void depleteStamina(float amount)
    {
        ResolveController()?.depleteStamina(amount);
    }

    // --- Hitboxes ---

    // AnimationEvents can be configured with or without parameters, and sometimes differ in casing.
    // Provide overloads/aliases so changes to clips/controllers don't silently break hitbox windows.
    public void ToggleAttackHitBox()
    {
        var ctrl = ResolveController();
        if (ctrl == null)
        {
            Debug.LogWarning($"[AttackAnimationEventReceiver] ToggleAttackHitBox() called on '{gameObject.name}' but no AttackController found to forward to.", this);
            return;
        }

        Debug.Log($"[AttackAnimationEventReceiver] ToggleAttackHitBox() called on '{gameObject.name}' -> forwarding to controller '{ctrl.gameObject.name}'", this);
        ctrl.ToggleAttackHitBox(-1);
    }

    public void ToggleAttackHitBox(int hitboxId)
    {
        var ctrl = ResolveController();
        if (ctrl == null)
        {
            Debug.LogWarning($"[AttackAnimationEventReceiver] ToggleAttackHitBox({hitboxId}) called on '{gameObject.name}' but no AttackController found to forward to.", this);
            return;
        }

        Debug.Log($"[AttackAnimationEventReceiver] ToggleAttackHitBox({hitboxId}) called on '{gameObject.name}' -> forwarding to controller '{ctrl.gameObject.name}'", this);
        ctrl.ToggleAttackHitBox(hitboxId);
    }

    public void toggleAttackHitBox()
    {
        ToggleAttackHitBox();
    }

    public void toggleAttackHitBox(int hitboxId)
    {
        ToggleAttackHitBox(hitboxId);
    }

    // Common typo/legacy naming seen in some projects
    public void TogglHitBoxes()
    {
        ToggleAttackHitBox();
    }

    public void TogglHitBoxes(int hitboxId)
    {
        ToggleAttackHitBox(hitboxId);
    }

    public void cleanupAttackHitBox()
    {
        ResolveController()?.cleanupAttackHitBox();
    }

    public void CleanupAttackHitBox()
    {
        ResolveController()?.CleanupAttackHitBox();
    }

    // --- Damage I-frames (some animation clips use these events) ---
    public void IframeStart()
    {
        // Try to forward to a DamageController if present on this object or parents
        var dc = GetComponentInParent<DamageController>();
        if (dc != null)
        {
            dc.IframeStart();
            return;
        }

        Debug.LogWarning($"[AttackAnimationEventReceiver] IframeStart called but no DamageController found on '{gameObject.name}' or parents.", this);
    }

    public void IframeEnd()
    {
        var dc = GetComponentInParent<DamageController>();
        if (dc != null)
        {
            dc.IframeEnd();
            return;
        }

        Debug.LogWarning($"[AttackAnimationEventReceiver] IframeEnd called but no DamageController found on '{gameObject.name}' or parents.", this);
    }
}
