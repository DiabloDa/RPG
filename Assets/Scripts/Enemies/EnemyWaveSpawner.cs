using System.Collections;
using System.Collections.Generic;
using System;
using Clases.Clase_2.Scripts;
using UnityEngine;
using UnityEngine.AI;

public class EnemyWaveSpawner : MonoBehaviour
{
    [Serializable]
    public class EnemyType
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Enemy Types (min 2)")]
    [SerializeField] private List<EnemyType> enemyTypes = new List<EnemyType>();

    [Header("Waves")]
    [SerializeField] private int startWaveSize = 1;
    [SerializeField] private int maxEnemiesPerWave = 10;
    [SerializeField] private float timeBetweenWaves = 2f;
    [SerializeField] private float timeBetweenSpawns = 0.15f;

    [Header("Difficulty")]
    [SerializeField] private float speedMultiplierPerWave = 0.05f;

    [Header("Spawn Area")]
    [SerializeField] private float spawnRadiusMin = 2.5f;
    [SerializeField] private float spawnRadiusMax = 4.8f;
    [SerializeField] private bool sampleOnNavMesh = true;
    [SerializeField] private float navMeshSampleMaxDistance = 10f;

    [Header("Power Ups")]
    [SerializeField] private List<GameObject> powerUpPrefabs = new List<GameObject>();

    private readonly List<GameObject> aliveEnemies = new List<GameObject>();
    private Coroutine loop;

    private int waveIndex = 1;

    public int CurrentRound => waveIndex;
    public event Action<int> RoundStarted;

    private void OnEnable()
    {
        loop = StartCoroutine(Loop());
    }

    private void OnDisable()
    {
        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        AutoPopulatePowerUpPrefabsFromProject();
#endif
    }

#if UNITY_EDITOR
    private void AutoPopulatePowerUpPrefabsFromProject()
    {
        if (powerUpPrefabs == null)
        {
            powerUpPrefabs = new List<GameObject>();
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Prefabs/PowerUps" });
        if (guids == null || guids.Length == 0)
        {
            guids = UnityEditor.AssetDatabase.FindAssets("PowerUp t:prefab");
        }

        if (guids == null || guids.Length == 0)
        {
            return;
        }

        var loadedPrefabs = new List<GameObject>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                loadedPrefabs.Add(prefab);
            }
        }

        if (loadedPrefabs.Count == 0)
        {
            return;
        }

        bool changed = powerUpPrefabs.Count != loadedPrefabs.Count;
        if (!changed)
        {
            for (int i = 0; i < loadedPrefabs.Count; i++)
            {
                if (powerUpPrefabs[i] != loadedPrefabs[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return;
        }

        powerUpPrefabs = loadedPrefabs;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private IEnumerator Loop()
    {
        while (true)
        {
            if (IsGameOver())
            {
                yield return null;
                continue;
            }

            if (player == null)
            {
                player = ResolvePlayerTransform();
                if (player == null)
                {
                    yield return null;
                    continue;
                }
            }

            // The inspector reference is often the player's root, but gameplay scripts live on a child.
            // Always chase/spawn around the transform that actually moves.
            Transform chaseTarget = ResolveChaseTarget(player);
            if (chaseTarget == null)
            {
                yield return null;
                continue;
            }

            CleanupAliveList();

            if (aliveEnemies.Count > 0)
            {
                yield return null;
                continue;
            }

            int waveSize = ComputeWaveSize(waveIndex);
            Debug.Log($"[EnemyWaveSpawner] Starting wave {waveIndex} with size {waveSize}");

            if (timeBetweenWaves > 0f)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }

            RoundStarted?.Invoke(waveIndex);

            for (int i = 0; i < waveSize; i++)
            {
                if (IsGameOver()) break;

                var prefab = ChooseEnemyPrefab();
                if (prefab == null)
                {
                    Debug.LogWarning("[EnemyWaveSpawner] No enemy prefab chosen — check Enemy Types in inspector.");
                    yield return null;
                    continue;
                }
                else
                {
                    Debug.Log($"[EnemyWaveSpawner] Spawning prefab: {prefab.name}");
                }
                if (prefab == null)
                {
                    yield return null;
                    continue;
                }

                Vector3 spawnPos = FindSpawnPosition(chaseTarget.position);
                var go = Instantiate(prefab, spawnPos, Quaternion.identity);
                SetupSpawnedEnemy(go, waveIndex, chaseTarget);
                aliveEnemies.Add(go);

                if (timeBetweenSpawns > 0f)
                {
                    yield return new WaitForSeconds(timeBetweenSpawns);
                }
            }

            waveIndex++;
            yield return null;
        }
    }

    private Transform ResolveChaseTarget(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return ResolvePlayerTransform();
        }

        // Prefer AttackController if present (this is what actually moves/attacks in this project).
        var attackController = playerRoot.GetComponentInChildren<AttackController>(true);
        if (attackController != null)
        {
            return attackController.transform;
        }

        var characterMovement = playerRoot.GetComponentInChildren<CharacterMovement>(true);
        if (characterMovement != null)
        {
            return characterMovement.transform;
        }

        return playerRoot;
    }

    private bool IsGameOver()
    {
        return Game.Instance != null && Game.Instance.PlayerOne != null && Game.Instance.PlayerOne.IsDead;
    }

    private void CleanupAliveList()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null)
            {
                aliveEnemies.RemoveAt(i);
            }
        }
    }

    private int ComputeWaveSize(int wave)
    {
        // Linear growth: startWaveSize, then +1 enemy each wave (1,2,3,...)
        int baseSize = Mathf.Max(1, startWaveSize);
        int size = baseSize + Mathf.Max(0, wave - 1);
        return Mathf.Min(size, maxEnemiesPerWave);
    }

    private GameObject ChooseEnemyPrefab()
    {
        if (enemyTypes == null || enemyTypes.Count == 0) return null;

        float total = 0f;
        for (int i = 0; i < enemyTypes.Count; i++)
        {
            if (enemyTypes[i] == null || enemyTypes[i].prefab == null) continue;
            total += Mathf.Max(0f, enemyTypes[i].weight);
        }

        if (total <= 0f)
        {
            // Fallback: first valid prefab.
            for (int i = 0; i < enemyTypes.Count; i++)
            {
                if (enemyTypes[i] != null && enemyTypes[i].prefab != null) return enemyTypes[i].prefab;
            }
            return null;
        }

        float roll = UnityEngine.Random.value * total;
        float acc = 0f;
        for (int i = 0; i < enemyTypes.Count; i++)
        {
            if (enemyTypes[i] == null || enemyTypes[i].prefab == null) continue;
            acc += Mathf.Max(0f, enemyTypes[i].weight);
            if (roll <= acc) return enemyTypes[i].prefab;
        }

        return enemyTypes[0].prefab;
    }

    private Vector3 FindSpawnPosition(Vector3 around)
    {
        float rMin = Mathf.Max(0f, spawnRadiusMin);
        float rMax = Mathf.Max(rMin, spawnRadiusMax);

        Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized;
        float dist = UnityEngine.Random.Range(rMin, rMax);

        Vector3 candidate = around + new Vector3(circle.x, 0f, circle.y) * dist;

        if (!sampleOnNavMesh) return candidate;

        if (NavMesh.SamplePosition(candidate, out var hit, navMeshSampleMaxDistance, NavMesh.AllAreas))
        {
            Debug.Log($"[EnemyWaveSpawner] Spawn position snapped to NavMesh at {hit.position}");
            return hit.position;
        }

        Debug.LogWarning($"[EnemyWaveSpawner] No NavMesh hit near {candidate}, using candidate position. Increase navMeshSampleMaxDistance or bake NavMesh.");
        return candidate;
    }

    private void SetupSpawnedEnemy(GameObject enemyGo, int wave, Transform chaseTarget)
    {
        if (enemyGo == null) return;

        var enemyAi = enemyGo.GetComponent<EnemyAI>();
        if (enemyAi != null)
        {
            enemyAi.player = chaseTarget;

            float speedMultiplier = 1f + (wave - 1) * Mathf.Max(0f, speedMultiplierPerWave);
            enemyAi.walkSpeed *= speedMultiplier;
            enemyAi.runSpeed *= speedMultiplier;

            if (enemyAi.agent != null)
            {
                // Keep current agent speed in sync with whichever state already set it.
                enemyAi.agent.speed *= speedMultiplier;
            }
        }
        else
        {
            // No NavMesh/AI setup: use simple transform-based chase.
            var chase = enemyGo.GetComponent<EnemySimpleChase>();
            if (chase == null) chase = enemyGo.AddComponent<EnemySimpleChase>();
            chase.target = chaseTarget;

            // If the imported model prefab contains its own NavMeshAgent/EnemyAI on child objects,
            // those can overwrite transforms and make the visible mesh look like it stops chasing.
            // Disable them when using the simple chase mode.
            var nestedAis = enemyGo.GetComponentsInChildren<EnemyAI>(true);
            for (int i = 0; i < nestedAis.Length; i++)
            {
                if (nestedAis[i] != null)
                {
                    if (nestedAis[i].agent != null)
                    {
                        nestedAis[i].agent.enabled = false;
                    }

                    nestedAis[i].enabled = false;
                }
            }

            var nestedAgents = enemyGo.GetComponentsInChildren<NavMeshAgent>(true);
            for (int i = 0; i < nestedAgents.Length; i++)
            {
                if (nestedAgents[i] != null)
                {
                    nestedAgents[i].enabled = false;
                }
            }

            float speedMultiplier = 1f + (wave - 1) * Mathf.Max(0f, speedMultiplierPerWave);
            chase.SetSpeedMultiplier(speedMultiplier);
        }

        // Ensure the enemy can be killed by the player's AttackHitBox.
        if (enemyGo.GetComponent<EnemyHealth>() == null)
        {
            enemyGo.AddComponent<EnemyHealth>();
        }

        // Ensure the enemy can damage the player.
       /* if (enemyGo.GetComponent<EnemyMeleeAttack>() == null)
        {
            enemyGo.AddComponent<EnemyMeleeAttack>();
        }*/
    }

    private Transform ResolvePlayerTransform()
    {
        var attackController = FindFirstObjectByType<AttackController>();
        if (attackController != null) return attackController.transform;

        var characterMovement = FindFirstObjectByType<CharacterMovement>();
        if (characterMovement != null) return characterMovement.transform;

        var tagged = GameObject.FindWithTag("Player");
        if (tagged != null) return tagged.transform;

        return null;
    }
}
