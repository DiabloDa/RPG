using UnityEngine;

public class AttackHitBoxController : MonoBehaviour
{
    [SerializeField] private GameObject[] hitBoxes;

    public GameObject[] HitBoxes => hitBoxes;

    private void Awake()
    {
        // Safety: hitboxes should not be active outside of animation attack windows.
        cleanupHitBoxes();
    }

    public void TogglHitBoxes(int attackId)
    {
        if (hitBoxes == null || hitBoxes.Length == 0)
        {
            return;
        }

        // If a specific id is provided, toggle only that one.
        if (attackId >= 0 && attackId < hitBoxes.Length)
        {
            GameObject hitbox = hitBoxes[attackId];
            if (hitbox != null)
            {
                hitbox.SetActive(!hitbox.activeSelf);
            }

            return;
        }

        // Backwards-compatible fallback: toggle all.
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
            return;
        }

        foreach (GameObject colliders in hitBoxes)
        {
            if (colliders != null)
            {
                colliders.SetActive(false);
            }
        }
    }


}
