using Clases.Clase_2.Scripts;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private PlayableDirector deathCinematic;
    [SerializeField] private bool autoDisablePlayerControllers = true;
    [SerializeField] private bool autoDisableEnemySpawners = true;
    [SerializeField] private bool autoStopEnemies = true;

    private bool gameOverTriggered;

    private void Awake()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void Update()
    {
        // Fallback in case subscription couldn't happen (script order / scene changes).
        if (!gameOverTriggered && Game.Instance != null && Game.Instance.PlayerOne != null && Game.Instance.PlayerOne.IsDead)
        {
            TriggerGameOver();
        }
    }

    private void TrySubscribe()
    {
        if (Game.Instance == null || Game.Instance.PlayerOne == null) return;
        Game.Instance.PlayerOne.Died -= TriggerGameOver;
        Game.Instance.PlayerOne.Died += TriggerGameOver;
    }

    private void TryUnsubscribe()
    {
        if (Game.Instance == null || Game.Instance.PlayerOne == null) return;
        Game.Instance.PlayerOne.Died -= TriggerGameOver;
    }

    private void TriggerGameOver()
    {
        if (gameOverTriggered) return;
        gameOverTriggered = true;

        Debug.Log("[GameOver] Player died");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (deathCinematic != null)
        {
            deathCinematic.time = 0f;
            deathCinematic.Play();
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (autoDisableEnemySpawners)
        {
            foreach (var spawner in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None))
            {
                spawner.enabled = false;
            }
        }

        if (autoStopEnemies)
        {
            foreach (var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (enemy.CanUseNavMeshAgent())
                {
                    enemy.agent.isStopped = true;
                }
                enemy.enabled = false;
            }
        }

        if (autoDisablePlayerControllers)
        {
            var attackController = FindFirstObjectByType<AttackController>();
            if (attackController != null) attackController.enabled = false;

            var movement = FindFirstObjectByType<CharacterMovement>();
            if (movement != null) movement.enabled = false;

            var look = FindFirstObjectByType<CharacterLook>();
            if (look != null) look.enabled = false;
        }
    }

    public void RestartGame()
    {
        if (Game.Instance != null)
        {
            Game.Instance.ResetRun();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
