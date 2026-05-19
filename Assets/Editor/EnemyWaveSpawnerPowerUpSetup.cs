#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyWaveSpawnerPowerUpSetup
{
    private const string PowerUpFolder = "Assets/Prefabs/PowerUps";

    static EnemyWaveSpawnerPowerUpSetup()
    {
        EditorApplication.delayCall += RefreshOpenScenes;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        RefreshOpenScenes();
    }

    [MenuItem("Tools/Enemy Wave/Auto Assign Power Ups To SpawnEnemies")]
    public static void RefreshOpenScenes()
    {
        var powerUps = LoadPowerUpPrefabs();
        if (powerUps.Length == 0)
        {
            Debug.LogWarning("[EnemyWaveSpawnerPowerUpSetup] No prefabs found under Assets/Prefabs/PowerUps.");
            return;
        }

        var spawners = Object.FindObjectsOfType<EnemyWaveSpawner>(true);
        int updated = 0;

        foreach (var spawner in spawners)
        {
            if (spawner == null)
            {
                continue;
            }

            var so = new SerializedObject(spawner);
            var list = so.FindProperty("powerUpPrefabs");
            if (list == null)
            {
                continue;
            }

            bool changed = list.arraySize != powerUps.Length;
            if (!changed)
            {
                for (int i = 0; i < powerUps.Length; i++)
                {
                    var current = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (current != powerUps[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
            {
                continue;
            }

            list.arraySize = powerUps.Length;
            for (int i = 0; i < powerUps.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = powerUps[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);
            updated++;
        }

        if (updated > 0)
        {
            Debug.Log($"[EnemyWaveSpawnerPowerUpSetup] Assigned {powerUps.Length} power up prefabs to {updated} EnemyWaveSpawner component(s).");
        }
    }

    private static GameObject[] LoadPowerUpPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:prefab", new[] { PowerUpFolder });
        var prefabs = new GameObject[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return prefabs;
    }
}
#endif
