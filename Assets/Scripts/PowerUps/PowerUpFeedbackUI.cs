using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpFeedbackUI : MonoBehaviour
{
    [Header("Manual References")]
    [SerializeField] private GameObject invulnerabilityBorderGroup;
    [SerializeField] private GameObject damageBoostBorderGroup;
    [SerializeField] private TMP_Text countdownText;

    [Header("Colors")]
    [SerializeField] private Color invulnerabilityColor = new Color(1f, 0.9f, 0.15f, 0.95f);
    [SerializeField] private Color damageBoostColor = new Color(0.25f, 0.95f, 1f, 0.95f);

    [Header("Pulse")]
    [SerializeField, Min(0f)] private float pulseSpeed = 8f;
    [SerializeField, Range(0f, 0.5f)] private float pulseMin = 0.82f;
    [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.18f;

    private enum ActiveState
    {
        None,
        Invulnerability,
        DamageBoost,
    }

    private ActiveState activeState = ActiveState.None;
    private float remainingSeconds;

    private void Awake()
    {
        HideAll();
    }

    private void Update()
    {
        if (activeState == ActiveState.None)
        {
            if (DamageBoostRuntime.IsActive)
            {
                SetDamageBoostActive(true, DamageBoostRuntime.RemainingSeconds);
            }

            return;
        }

        if (activeState == ActiveState.DamageBoost)
        {
            remainingSeconds = DamageBoostRuntime.RemainingSeconds;
            if (!DamageBoostRuntime.IsActive)
            {
                SetDamageBoostActive(false, 0f);
                return;
            }
        }
        else
        {
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.unscaledDeltaTime);
            if (remainingSeconds <= 0f)
            {
                SetInvulnerabilityActive(false, 0f);
                return;
            }
        }

        UpdateCountdown();
        UpdatePulse();
    }

    public void SetInvulnerabilityActive(bool active, float remaining)
    {
        if (active)
        {
            SetState(ActiveState.Invulnerability, remaining);
        }
        else if (activeState == ActiveState.Invulnerability)
        {
            SetState(ActiveState.None, 0f);
        }
    }

    public void SetDamageBoostActive(bool active, float remaining)
    {
        if (active)
        {
            SetState(ActiveState.DamageBoost, remaining);
        }
        else if (activeState == ActiveState.DamageBoost)
        {
            SetState(ActiveState.None, 0f);
        }
    }

    private void SetState(ActiveState newState, float remaining)
    {
        activeState = newState;
        remainingSeconds = Mathf.Max(0f, remaining);

        HideAll();

        switch (activeState)
        {
            case ActiveState.Invulnerability:
                ShowGroup(invulnerabilityBorderGroup, invulnerabilityColor);
                break;
            case ActiveState.DamageBoost:
                ShowGroup(damageBoostBorderGroup, damageBoostColor);
                break;
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(activeState != ActiveState.None);
        }

        UpdateCountdown();
        UpdatePulse();
    }

    private void HideAll()
    {
        if (invulnerabilityBorderGroup != null) invulnerabilityBorderGroup.SetActive(false);
        if (damageBoostBorderGroup != null) damageBoostBorderGroup.SetActive(false);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
            countdownText.text = string.Empty;
        }
    }

    private void ShowGroup(GameObject group, Color color)
    {
        if (group == null) return;

        group.SetActive(true);

        Image[] images = group.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;
            Color c = color;
            images[i].color = c;
        }
    }

    private void UpdateCountdown()
    {
        if (countdownText == null) return;

        if (activeState == ActiveState.None)
        {
            countdownText.text = string.Empty;
            return;
        }

        countdownText.text = $"{remainingSeconds:0.0}s";
        countdownText.color = activeState == ActiveState.Invulnerability ? invulnerabilityColor : damageBoostColor;
    }

    private void UpdatePulse()
    {
        float pulse = pulseMin + pulseAmount * Mathf.Sin(Time.unscaledTime * pulseSpeed);
        Color color = activeState == ActiveState.Invulnerability ? invulnerabilityColor : damageBoostColor;

        if (invulnerabilityBorderGroup != null)
        {
            SetGroupAlpha(invulnerabilityBorderGroup, activeState == ActiveState.Invulnerability ? color.a * pulse : 0f);
        }

        if (damageBoostBorderGroup != null)
        {
            SetGroupAlpha(damageBoostBorderGroup, activeState == ActiveState.DamageBoost ? color.a * pulse : 0f);
        }

        if (countdownText != null)
        {
            countdownText.color = color;
        }
    }

    private static void SetGroupAlpha(GameObject group, float alpha)
    {
        if (group == null) return;

        Image[] images = group.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;

            Color c = images[i].color;
            images[i].color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}