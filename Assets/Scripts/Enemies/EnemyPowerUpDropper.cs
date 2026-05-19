using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to enemies. When they die, randomly drops a power up.
/// </summary>
public class EnemyPowerUpDropper : MonoBehaviour
{
    [SerializeField] private bool legacyDropperEnabled = false;
    [SerializeField] private List<GameObject> powerUpPrefabs = new List<GameObject>();
    [SerializeField] private string spawnPlaneName = "base";
    [SerializeField, Min(0f)] private float spawnY = 1f;
    [SerializeField, Min(0f)] private float spawnPadding = 0.5f;

    private EnemyHealth enemyHealth;
    private bool _dropAlreadySpawned;

    private void Awake()
    {
        if (!legacyDropperEnabled)
        {
            enabled = false;
            return;
        }

        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.Died += OnEnemyDied;
        }
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= OnEnemyDied;
        }
    }

    public void SetPowerUpPrefabs(List<GameObject> prefabs)
    {
        if (prefabs != null && prefabs.Count > 0)
        {
            powerUpPrefabs = new List<GameObject>(prefabs);
        }
    }

    public void ForceDrop()
    {
        TryDrop(true);
    }

    private void OnEnemyDied(EnemyHealth health)
    {
        if (!legacyDropperEnabled)
        {
            return;
        }

        TryDrop(false);
    }

    private void TryDrop(bool ignoreChance)
    {
        if (_dropAlreadySpawned)
        {
            return;
        }

        _dropAlreadySpawned = true;

        int dropCount = DetermineDropCount();
        if (dropCount <= 0)
        {
            return;
        }

        Vector3 basePosition = ResolveSpawnPosition();

        for (int i = 0; i < dropCount; i++)
        {
            SpawnOnePowerUp(basePosition + Random.insideUnitSphere * 0.35f + Vector3.up * (i * 0.08f));
        }
    }

    private int DetermineDropCount()
    {
        int count = 1;

        if (Random.value <= 0.85f) count++;
        if (Random.value <= 0.55f) count++;
        if (Random.value <= 0.30f) count++;
        if (Random.value <= 0.09f) count++;

        return Mathf.Clamp(count, 1, 5);
    }

    private Vector3 ResolveSpawnPosition()
    {
        GameObject plane = FindSpawnPlane();
        if (plane != null)
        {
            Bounds bounds = GetWorldBounds(plane);
            float minX = bounds.min.x + spawnPadding;
            float maxX = bounds.max.x - spawnPadding;
            float minZ = bounds.min.z + spawnPadding;
            float maxZ = bounds.max.z - spawnPadding;

            if (minX <= maxX && minZ <= maxZ)
            {
                float x = Random.Range(minX, maxX);
                float z = Random.Range(minZ, maxZ);
                return new Vector3(x, spawnY, z);
            }
        }

        return new Vector3(transform.position.x, spawnY, transform.position.z);
    }

    private GameObject FindSpawnPlane()
    {
        GameObject plane = GameObject.Find(spawnPlaneName);
        if (plane != null)
        {
            return plane;
        }

        GameObject exactBase = GameObject.Find("Base");
        if (exactBase != null)
        {
            return exactBase;
        }

        return null;
    }

    private Bounds GetWorldBounds(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds;
        }

        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            return collider.bounds;
        }

        return new Bounds(go.transform.position, new Vector3(10f, 1f, 10f));
    }

    private void SpawnOnePowerUp(Vector3 spawnPos)
    {
        if (powerUpPrefabs != null && powerUpPrefabs.Count > 0)
        {
            GameObject prefab = ChoosePowerUpPrefab();
            if (prefab != null)
            {
                Instantiate(prefab, spawnPos, Quaternion.identity);
                Debug.Log($"[EnemyPowerUpDropper] Dropped {prefab.name} at {spawnPos}");
                return;
            }
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
            else if (damage == null && (lower.Contains("dañ") || lower.Contains("dan") || lower.Contains("damage") || lower.Contains("powerupdamage")))
            {
                damage = prefab;
            }
        }

        if (invulnerability != null || damage != null)
        {
            return Random.value < 0.4f
                ? (invulnerability != null ? invulnerability : damage)
                : (damage != null ? damage : invulnerability);
        }

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

        Debug.Log($"[EnemyPowerUpDropper] Spawned fallback power up {kind} at {spawnPos}");
    }
}
