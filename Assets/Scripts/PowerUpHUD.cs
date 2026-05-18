using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerUpHUD : MonoBehaviour
{
    [Header("References")]
    public Image[] borderImages; // asigna las 4 (o más) imágenes del borde
    public GameObject timerGroup; // PU_Timer_Group
    public TMP_Text timerText; // PU_Timer_Text (TextMeshPro)

    void Start()
    {
        if (timerGroup != null) timerGroup.SetActive(false);
        if (borderImages != null)
        {
            foreach (var img in borderImages) if (img != null) img.enabled = false;
        }
    }

    void Update()
    {
        var powerUps = Game.Instance.PlayerPowerUps;
        if (powerUps == null)
        {
            if (timerGroup != null) timerGroup.SetActive(false);
            SetBorderEnabled(false);
            return;
        }

        if (powerUps.IsInvulnerable)
        {
            SetBorderEnabled(true);
            if (timerGroup != null) timerGroup.SetActive(true);

            float remaining = powerUps.CurrentInvulnerabilityRemaining;
            timerText.text = remaining.ToString("F1") + "s";
        }
        else
        {
            SetBorderEnabled(false);
            if (timerGroup != null) timerGroup.SetActive(false);
        }
    }

    void SetBorderEnabled(bool on)
    {
        if (borderImages == null) return;
        foreach (var img in borderImages)
        {
            if (img == null) continue;
            img.enabled = on;
            // opcional: cambia alpha dinámicamente según remaining % (si guardas duration inicial)
        }
    }
}