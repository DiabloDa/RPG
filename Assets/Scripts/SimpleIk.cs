using System.Collections;
using UnityEngine;

public class SimpleIk : MonoBehaviour
{
   public bool enableIk = true;
    public Transform leftHandTarget;
    public Transform lookAtTarget;
    private Animator  animator;
    private float weight;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Pulse(float time = 0.02f)
    {
        StopAllCoroutines();
        StartCoroutine(PulseCR(time));    

    }

    private IEnumerator PulseCR(float time)
    {
        float t = 0f;
        while (t < time)
        {
            t+= Time.deltaTime;
            weight = Mathf.Clamp01(t / time);
            yield return null;

        }
        t = 0f;

        while(t < time)
        {
            t += Time.deltaTime;
            weight = 1f - Mathf.Clamp01(t / time);
            yield return null;

        }
        weight = 0f;

    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (enableIk || !animator) return;

        if (lookAtTarget)
        {
            animator.SetLookAtWeight(weight);
            animator.SetLookAtPosition(lookAtTarget.position);  
        }

        if(leftHandTarget)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);

            animator.SetIKPosition(AvatarIKGoal.RightHand,leftHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, leftHandTarget.rotation);

        }

    }

}
