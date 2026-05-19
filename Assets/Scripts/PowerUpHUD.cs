using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerUpHUD : MonoBehaviour
{
    [Header("References")]
    public Image[] invulnerabilityBorderImages; // borde de inmunidad
    public Image[] damageBoostBorderImages; // borde de daño x2
    public GameObject timerGroup; // PU_Timer_Group
    public TMP_Text timerText; // PU_Timer_Text (TextMeshPro)

    [Header("Colors")]
    public Color invulnerabilityColor = new Color(1f, 0.9f, 0.15f, 1f);
    public Color damageBoostColor = new Color(0.25f, 0.95f, 1f, 1f);

    void Start()
    {
        if (timerGroup != null) timerGroup.SetActive(false);
        SetBorderEnabled(invulnerabilityBorderImages, false, invulnerabilityColor);
        SetBorderEnabled(damageBoostBorderImages, false, damageBoostColor);
    }

    void Update()
    {
        var powerUps = Game.Instance.PlayerPowerUps;
        if (powerUps == null)
        {
            if (timerGroup != null) timerGroup.SetActive(false);
            SetBorderEnabled(invulnerabilityBorderImages, false, invulnerabilityColor);
            SetBorderEnabled(damageBoostBorderImages, false, damageBoostColor);
            return;
        }

        if (powerUps.IsInvulnerable)
        {
            SetBorderEnabled(invulnerabilityBorderImages, true, invulnerabilityColor);
            SetBorderEnabled(damageBoostBorderImages, false, damageBoostColor);
            if (timerGroup != null) timerGroup.SetActive(true);

            float remaining = powerUps.CurrentInvulnerabilityRemaining;
            if (timerText != null) timerText.text = remaining.ToString("F1") + "s";
        }
        else if (powerUps.IsDamageMultiplierActive)
        {
            SetBorderEnabled(invulnerabilityBorderImages, false, invulnerabilityColor);
            SetBorderEnabled(damageBoostBorderImages, true, damageBoostColor);
            if (timerGroup != null) timerGroup.SetActive(true);

            float remaining = powerUps.CurrentDamageMultiplierRemaining;
            if (timerText != null) timerText.text = remaining.ToString("F1") + "s";
        }
        else
        {
            SetBorderEnabled(invulnerabilityBorderImages, false, invulnerabilityColor);
            SetBorderEnabled(damageBoostBorderImages, false, damageBoostColor);
            if (timerGroup != null) timerGroup.SetActive(false);
        }
    }

    void SetBorderEnabled(Image[] images, bool on, Color color)
    {
        if (images == null) return;
        foreach (var img in images)
        {
            if (img == null) continue;
            img.enabled = on;
            img.color = new Color(color.r, color.g, color.b, on ? color.a : 0f);
        }
    }
}