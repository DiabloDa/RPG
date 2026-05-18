using UnityEngine;

/// <summary>
/// Automatically adds a SphereCollider (as trigger) if the enemy doesn't have one.
/// Attach this to the enemy root GameObject.
/// </summary>
public class AutoSetupEnemyCollider : MonoBehaviour
{
    private void Awake()
    {
        // Check if already has a collider
        var colliders = GetComponents<Collider>();
        if (colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider>();
        }

        if (colliders.Length == 0)
        {
            // Add a trigger collider
            var collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.5f; // Adjust size as needed
            collider.isTrigger = true;
            Debug.Log($"[AutoSetupEnemyCollider] Added SphereCollider to '{gameObject.name}'", gameObject);

            // Add a Rigidbody if needed
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                Debug.Log($"[AutoSetupEnemyCollider] Added Rigidbody (kinematic) to '{gameObject.name}'", gameObject);
            }
        }
        else
        {
            // Ensure at least one is a trigger
            bool hasTrigger = false;
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    hasTrigger = true;
                    break;
                }
            }

            if (!hasTrigger)
            {
                colliders[0].isTrigger = true;
                Debug.Log($"[AutoSetupEnemyCollider] Set '{colliders[0].name}' to isTrigger=true on '{gameObject.name}'", gameObject);
            }

            // Ensure Rigidbody exists
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                Debug.Log($"[AutoSetupEnemyCollider] Added Rigidbody (kinematic) to '{gameObject.name}'", gameObject);
            }
        }
    }
}
