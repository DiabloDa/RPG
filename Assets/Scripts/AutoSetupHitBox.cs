using UnityEngine;

/// <summary>
/// Automatically adds a BoxCollider (as trigger) if the hitbox doesn't have one.
/// Attach this to any hitbox GameObject.
/// </summary>
public class AutoSetupHitBox : MonoBehaviour
{
    private void Awake()
    {
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider>();
            Debug.Log($"[AutoSetupHitBox] Added BoxCollider to '{gameObject.name}'", gameObject);
        }

        collider.isTrigger = true;

        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            Debug.Log($"[AutoSetupHitBox] Added Rigidbody (kinematic) to '{gameObject.name}'", gameObject);
        }
    }
}
