#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class EnemyPowerUpDropperSetup
{
    [MenuItem("Tools/Enemy Wave/Auto Assign Power Up Prefabs to Droppers")]
    public static void AutoAssignPowerUpPrefabs()
    {
        // Search for power up prefabs - try multiple patterns
        var guids = AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Prefabs/PowerUps" });
        
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("PowerUp t:prefab");
        }

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Power Up Setup", "No prefabs found in Assets/Prefabs/PowerUps or with 'PowerUp' in the name.\n\nMove or create power up prefabs in Assets/Prefabs/PowerUps folder.", "OK");
            return;
        }

        var prefabs = new GameObject[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // Find all EnemyPowerUpDropper in scene
        var droppers = Object.FindObjectsOfType<EnemyPowerUpDropper>();
        int updated = 0;

        foreach (var dropper in droppers)
        {
            var so = new SerializedObject(dropper);
            var list = so.FindProperty("powerUpPrefabs");
            
            if (list.arraySize == 0)
            {
                list.arraySize = prefabs.Length;
                for (int i = 0; i < prefabs.Length; i++)
                {
                    if (prefabs[i] != null)
                    {
                        list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(dropper);
                updated++;
            }
        }

        EditorUtility.DisplayDialog("Power Up Setup", $"Assigned {prefabs.Length} power up prefabs to {updated} droppers.", "OK");
    }

    [MenuItem("Tools/Enemy Wave/Create Sample Power Up Prefabs")]
    public static void CreateSamplePowerUpPrefabs()
    {
        string prefabDir = "Assets/Prefabs/PowerUps";
        if (!AssetDatabase.IsValidFolder(prefabDir))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "PowerUps");
        }

        // Create Invulnerability Power Up
        var invulnPrefab = new GameObject("PowerUpInvulnerability");
        var col = invulnPrefab.AddComponent<SphereCollider>();
        col.radius = 0.5f;
        col.isTrigger = true;
        var pickup = invulnPrefab.AddComponent<PowerUpPickup>();
        var so = new SerializedObject(pickup);
        so.FindProperty("powerUpKind").enumValueIndex = (int)PowerUpPickup.PowerUpKind.Invulnerability;
        so.FindProperty("durationSeconds").floatValue = 5f;
        so.ApplyModifiedProperties();

        // Add visual: sphere mesh renderer (white)
        var mr = invulnPrefab.AddComponent<MeshRenderer>();
        invulnPrefab.AddComponent<MeshFilter>().mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.5f, 1f, 0.5f, 0.8f);
        mr.material = mat;

        PrefabUtility.SaveAsPrefabAsset(invulnPrefab, $"{prefabDir}/PowerUpInvulnerability.prefab");
        Object.DestroyImmediate(invulnPrefab);

        // Create Double Damage Power Up
        var damagePrefab = new GameObject("PowerUpDoubleDamage");
        col = damagePrefab.AddComponent<SphereCollider>();
        col.radius = 0.5f;
        col.isTrigger = true;
        pickup = damagePrefab.AddComponent<PowerUpPickup>();
        so = new SerializedObject(pickup);
        so.FindProperty("powerUpKind").enumValueIndex = (int)PowerUpPickup.PowerUpKind.DoubleDamage;
        so.FindProperty("durationSeconds").floatValue = 5f;
        so.ApplyModifiedProperties();

        mr = damagePrefab.AddComponent<MeshRenderer>();
        damagePrefab.AddComponent<MeshFilter>().mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.5f, 0.5f, 0.8f);
        mr.material = mat;

        PrefabUtility.SaveAsPrefabAsset(damagePrefab, $"{prefabDir}/PowerUpDoubleDamage.prefab");
        Object.DestroyImmediate(damagePrefab);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Sample Power Ups", "Created PowerUpInvulnerability and PowerUpDoubleDamage prefabs in Assets/Prefabs/PowerUps/", "OK");
    }
}
#endif
