#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

// Auto-attach EnemyWaveSpawner to any GameObject named "spawnEnemies" in open scenes.
[InitializeOnLoad]
public static class EnemyWaveSpawnerAutoAttach
{
    static EnemyWaveSpawnerAutoAttach()
    {
        // Delay call to let editor finish domain reload
        EditorApplication.delayCall += AttachIfMissing;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        AttachIfMissing();
    }

    private static void AttachIfMissing()
    {
        var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allGos)
        {
            if (go == null) continue;
            if (go.name != "spawnEnemies") continue;

            // Only operate on scene objects (not prefabs/assets)
            if (string.IsNullOrEmpty(go.scene.path)) continue;

            var spawner = go.GetComponent<EnemyWaveSpawner>();
            if (spawner == null)
            {
                spawner = Undo.AddComponent<EnemyWaveSpawner>(go);
                Debug.Log("[AutoAttach] Added EnemyWaveSpawner to 'spawnEnemies'.");
            }

            // Auto-setup: find aRPGEnemy and assign it
            string filter = "aRPGEnemy t:prefab";
            var guids = AssetDatabase.FindAssets(filter);
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var so = new SerializedObject(spawner);
                    var list = so.FindProperty("enemyTypes");
                    
                    if (list.arraySize == 0)
                    {
                        list.arraySize = 1;
                    }

                    var el = list.GetArrayElementAtIndex(0);
                    var prefabProp = el.FindPropertyRelative("prefab");
                    var weightProp = el.FindPropertyRelative("weight");

                    if (prefabProp.objectReferenceValue != prefab || weightProp.floatValue != 1f)
                    {
                        prefabProp.objectReferenceValue = prefab;
                        weightProp.floatValue = 1f;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(spawner);
                        Debug.Log("[AutoAttach] Auto-assigned aRPGEnemy prefab with weight=1.0");
                    }
                }
            }
        }
    }
}
#endif
