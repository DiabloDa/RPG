#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AttackControllerSetup
{
    [MenuItem("Tools/Attack/Auto Assign Camera to AttackController")]
    public static void AutoAssignCamera()
    {
        var attackControllers = Object.FindObjectsOfType<AttackController>();
        int updated = 0;

        foreach (var ac in attackControllers)
        {
            var so = new SerializedObject(ac);
            var cameraRefProp = so.FindProperty("attackDirectionReferenceOverride");
            
            if (cameraRefProp != null && cameraRefProp.objectReferenceValue == null)
            {
                if (Camera.main != null)
                {
                    cameraRefProp.objectReferenceValue = Camera.main.transform;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(ac);
                    updated++;
                    Debug.Log($"[AttackControllerSetup] Assigned Camera.main to {ac.gameObject.name}");
                }
            }
        }

        if (updated > 0)
        {
            EditorUtility.DisplayDialog("Attack Setup", $"Assigned Camera to {updated} AttackController(s)", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Attack Setup", "No AttackController found or all already have camera assigned", "OK");
        }
    }
}
#endif
