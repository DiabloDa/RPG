using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level power up generator. Attach it to an empty GameObject.
/// It listens to round changes from EnemyWaveSpawner and spawns power ups inside a plane bounds.
/// </summary>
public class PowerUpDropManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyWaveSpawner waveSpawner;
    [SerializeField] private GameObject spawnPlane;

    [Header("Power Ups")]
    [SerializeField] private List<GameObject> powerUpPrefabs = new List<GameObject>();

    [Header("Spawn Area")]
    [SerializeField, Min(0f)] private float spawnY = 1f;
    [SerializeField, Min(0f)] private float spawnPadding = 0.5f;

    [Header("Drop Chances")]
    [SerializeField, Range(0f, 1f)] private float chanceFor2 = 0.85f;
    [SerializeField, Range(0f, 1f)] private float chanceFor3 = 0.55f;
    [SerializeField, Range(0f, 1f)] private float chanceFor4 = 0.30f;
    [SerializeField, Range(0f, 1f)] private float chanceFor5 = 0.09f;

    private int _lastProcessedRound;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();
        }

        if (spawnPlane == null)
        {
            spawnPlane = GameObject.Find("Base");
        }

        if (powerUpPrefabs != null && powerUpPrefabs.Count > 0)
        {
            return;
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Prefabs/PowerUps" });
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        var loaded = new List<GameObject>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                loaded.Add(prefab);
            }
        }

        if (loaded.Count > 0)
        {
            powerUpPrefabs = loaded;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    private void OnEnable()
    {
        ResolveWaveSpawner();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveWaveSpawner()
    {
        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();
        }
    }

    private void Subscribe()
    {
        ResolveWaveSpawner();
        if (waveSpawner == null)
        {
            return;
        }

        waveSpawner.RoundStarted -= HandleRoundStarted;
        waveSpawner.RoundStarted += HandleRoundStarted;
    }

    private void Unsubscribe()
    {
        if (waveSpawner != null)
        {
            waveSpawner.RoundStarted -= HandleRoundStarted;
        }
    }

    private void HandleRoundStarted(int roundNumber)
    {
        if (roundNumber <= 1 || roundNumber == _lastProcessedRound)
        {
            _lastProcessedRound = roundNumber;
            return;
        }

        _lastProcessedRound = roundNumber;

        int dropCount = DetermineDropCount();
        Vector3 center = ResolveSpawnCenter();

        for (int i = 0; i < dropCount; i++)
        {
            SpawnOnePowerUp(GetRandomSpawnPosition(center, i));
        }
    }

    private int DetermineDropCount()
    {
        float roll = Random.value;

        if (roll <= chanceFor5) return 5;
        if (roll <= chanceFor4) return 4;
        if (roll <= chanceFor3) return 3;
        if (roll <= chanceFor2) return 2;
        return 1;
    }

    private Vector3 ResolveSpawnCenter()
    {
        Bounds bounds = GetSpawnBounds();
        return new Vector3(bounds.center.x, spawnY, bounds.center.z);
    }

    private Vector3 GetRandomSpawnPosition(Vector3 center, int index)
    {
        Bounds bounds = GetSpawnBounds();
        float minX = bounds.min.x + spawnPadding;
        float maxX = bounds.max.x - spawnPadding;
        float minZ = bounds.min.z + spawnPadding;
        float maxZ = bounds.max.z - spawnPadding;

        if (minX > maxX || minZ > maxZ)
        {
            return new Vector3(center.x, spawnY, center.z);
        }

        float x = Random.Range(minX, maxX);
        float z = Random.Range(minZ, maxZ);
        return new Vector3(x, spawnY, z);
    }

    private Bounds GetSpawnBounds()
    {
        if (spawnPlane == null)
        {
            spawnPlane = GameObject.Find("Base");
        }

        if (spawnPlane != null)
        {
            var renderer = spawnPlane.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            var collider = spawnPlane.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }

            return new Bounds(spawnPlane.transform.position, new Vector3(10f, 1f, 10f));
        }

        return new Bounds(transform.position, new Vector3(10f, 1f, 10f));
    }

    private void SpawnOnePowerUp(Vector3 spawnPos)
    {
        GameObject prefab = ChoosePowerUpPrefab();
        if (prefab != null)
        {
            Instantiate(prefab, spawnPos, Quaternion.identity);
            Debug.Log($"[PowerUpDropManager] Dropped {prefab.name} at {spawnPos}");
            return;
        }

        SpawnFallbackPowerUp(spawnPos);
    }

    private GameObject ChoosePowerUpPrefab()
    {
        if (powerUpPrefabs == null || powerUpPrefabs.Count == 0)
        {
            return null;
        }

        GameObject invulnerability = null;
        GameObject damage = null;

        for (int i = 0; i < powerUpPrefabs.Count; i++)
        {
            GameObject prefab = powerUpPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            string lower = prefab.name.ToLowerInvariant();
            if (invulnerability == null && (lower.Contains("inmun") || lower.Contains("invul") || lower.Contains("shield") || lower.Contains("vida")))
            {
                invulnerability = prefab;
            }
            else if (damage == null && (lower.Contains("dañ") || lower.Contains("dan") || lower.Contains("damage")))
            {
                damage = prefab;
            }
        }

        if (invulnerability != null && damage != null)
        {
            return Random.value < 0.4f ? invulnerability : damage;
        }

        if (invulnerability != null) return invulnerability;
        if (damage != null) return damage;

        return powerUpPrefabs[Random.Range(0, powerUpPrefabs.Count)];
    }

    private void SpawnFallbackPowerUp(Vector3 spawnPos)
    {
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fallback.name = "RuntimePowerUpFallback";
        fallback.transform.position = spawnPos;
        fallback.transform.localScale = Vector3.one * 0.6f;

        var pickup = fallback.AddComponent<PowerUpPickup>();
        var kind = Random.value < 0.4f ? PowerUpPickup.PowerUpKind.Invulnerability : PowerUpPickup.PowerUpKind.DoubleDamage;
        pickup.Configure(kind, 5f, true);

        var renderer = fallback.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = kind == PowerUpPickup.PowerUpKind.Invulnerability ? new Color(0.45f, 1f, 0.55f, 1f) : new Color(1f, 0.45f, 0.45f, 1f);
        }

        Debug.Log($"[PowerUpDropManager] Spawned fallback power up {kind} at {spawnPos}");
    }
}
