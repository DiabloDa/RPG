using UnityEngine;

public class AttackHitBoxController : MonoBehaviour
{
    [SerializeField] private GameObject[] hitBoxes;

    public GameObject[] HitBoxes => hitBoxes;

    public void SetHitBoxesActive(int attackId, bool active)
    {
        if (hitBoxes == null || hitBoxes.Length == 0)
        {
            Debug.LogWarning($"[AttackHitBoxController] No hitBoxes assigned on '{gameObject.name}'", gameObject);
            return;
        }

        if (attackId >= 0 && attackId < hitBoxes.Length)
        {
            GameObject hitbox = hitBoxes[attackId];
            if (hitbox != null)
            {
                hitbox.SetActive(active);
            }

            return;
        }

        for (int hitboxId = 0; hitboxId < hitBoxes.Length; hitboxId++)
        {
            GameObject hitbox = hitBoxes[hitboxId];
            if (hitbox != null)
            {
                hitbox.SetActive(active);
            }
        }
    }

    private void Awake()
    {
        // Safety: hitboxes should not be active outside of animation attack windows.
        cleanupHitBoxes();
    }

    public void TogglHitBoxes(int attackId)
    {
        if (hitBoxes == null || hitBoxes.Length == 0)
        {
            Debug.LogWarning($"[AttackHitBoxController] No hitBoxes assigned on '{gameObject.name}'", gameObject);
            return;
        }

        // Keep the legacy method available, but make the activation path explicit so
        // attack windows can be opened and closed independently of the animation event timing.
        if (attackId >= 0 && attackId < hitBoxes.Length)
        {
            GameObject hitbox = hitBoxes[attackId];
            if (hitbox != null)
            {
                bool willBeActive = !hitbox.activeSelf;
                Debug.Log($"[AttackHitBoxController] Toggling hitbox '{hitbox.name}' -> {willBeActive}", hitbox);
                SetHitBoxesActive(attackId, willBeActive);

                if (willBeActive)
                {
                    var col = hitbox.GetComponent<Collider>() ?? hitbox.GetComponentInChildren<Collider>();
                    if (col != null)
                    {
                        var b = col.bounds;
                        Collider[] hits = Physics.OverlapBox(b.center, b.extents, hitbox.transform.rotation);
                        if (hits.Length == 0)
                        {
                            Debug.Log($"[AttackHitBoxController] Overlap test found NO colliders near '{hitbox.name}' (bounds: {b})", hitbox);
                        }
                        else
                        {
                            foreach (var h in hits)
                            {
                                Debug.Log($"[AttackHitBoxController] Overlap hit: '{h.gameObject.name}' (root: '{h.transform.root.name}', layer: {LayerMask.LayerToName(h.gameObject.layer)}[{h.gameObject.layer}])", hitbox);
                                int hbLayer = hitbox.layer;
                                int otherLayer = h.gameObject.layer;
                                bool collide = Physics.GetIgnoreLayerCollision(hbLayer, otherLayer) == false;
                                Debug.Log($"[AttackHitBoxController] Layer collision check: hitboxLayer={LayerMask.LayerToName(hbLayer)}[{hbLayer}] otherLayer={LayerMask.LayerToName(otherLayer)}[{otherLayer}] collideAllowed={collide}", hitbox);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[AttackHitBoxController] Activated hitbox '{hitbox.name}' but no Collider found on it or children.", hitbox);
                    }
                }
            }

            return;
        }

        for (int hitboxId = 0; hitboxId < hitBoxes.Length; hitboxId++)
        {
            GameObject hitbox = hitBoxes[hitboxId];
            if (hitbox != null)
            {
                hitbox.SetActive(!hitbox.activeSelf);
            }
        }
    }


    public void cleanupHitBoxes()
    {
        if (hitBoxes == null)
        {
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[AttackHitBoxController] cleanupHitBoxes called but no hitBoxes assigned on '{gameObject.name}'", gameObject);
            }
            return;
        }

        foreach (GameObject colliders in hitBoxes)
        {
            if (colliders != null)
            {
                if (colliders.activeSelf && Debug.isDebugBuild)
                {
                    Debug.Log($"[AttackHitBoxController] Deactivating hitbox '{colliders.name}'", colliders);
                }
                colliders.SetActive(false);
            }
        }

        SetHitBoxesActive(-1, false);
    }


}
