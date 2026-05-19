using TMPro;
using UnityEngine;

/// <summary>
/// Shows the current round in a TextMeshProUGUI label.
/// </summary>
public class RoundCounterUI : MonoBehaviour
{
    [SerializeField] private EnemyWaveSpawner waveSpawner;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private string prefix = "Ronda ";

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponent<TextMeshProUGUI>();
        }

        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();
        }
    }

    private void OnEnable()
    {
        Hook();
        Refresh(waveSpawner != null ? waveSpawner.CurrentRound : 1);
    }

    private void OnDisable()
    {
        Unhook();
    }

    private void Hook()
    {
        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();
        }

        if (waveSpawner != null)
        {
            waveSpawner.RoundStarted -= HandleRoundStarted;
            waveSpawner.RoundStarted += HandleRoundStarted;
        }
    }

    private void Unhook()
    {
        if (waveSpawner != null)
        {
            waveSpawner.RoundStarted -= HandleRoundStarted;
        }
    }

    private void HandleRoundStarted(int roundNumber)
    {
        Refresh(roundNumber);
    }

    private void Refresh(int roundNumber)
    {
        if (label == null)
        {
            return;
        }

        label.text = prefix + Mathf.Max(1, roundNumber);
    }
}
