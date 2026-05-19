#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor helpers for EnemyWaveSpawner: auto-assign prefab assets by name.
public static class EnemyWaveSpawnerEditorHelpers
{
    [MenuItem("Tools/Enemy Wave/Auto Assign aRPGEnemy Prefab")]
    public static void AutoAssignARPGEnemy()
    {
        // Search for prefab asset named "aRPGEnemy"
        string searchName = "aRPGEnemy";
        string filter = searchName + " t:prefab";
        var guids = AssetDatabase.FindAssets(filter);
        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto Assign", "No prefab named 'aRPGEnemy' found in project. Try a different name or place the prefab under a Resources folder.", "OK");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Auto Assign", "Found asset but could not load as GameObject: " + path, "OK");
            return;
        }

        int assigned = 0;
        var spawners = Object.FindObjectsOfType<EnemyWaveSpawner>();
        foreach (var sp in spawners)
        {
            var so = new SerializedObject(sp);
            var list = so.FindProperty("enemyTypes");
            if (list == null) continue;

            if (list.arraySize == 0)
            {
                list.arraySize = 1;
                var el = list.GetArrayElementAtIndex(0);
                var prefabProp = el.FindPropertyRelative("prefab");
                var weightProp = el.FindPropertyRelative("weight");
                prefabProp.objectReferenceValue = prefab;
                weightProp.floatValue = 1f;
                so.ApplyModifiedProperties();
                assigned++;
            }
        }

        EditorUtility.DisplayDialog("Auto Assign", $"Assigned 'aRPGEnemy' prefab to {assigned} EnemyWaveSpawner(s) (only to those with empty types).", "OK");
    }
}
#endif
