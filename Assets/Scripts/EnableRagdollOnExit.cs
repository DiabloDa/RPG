using UnityEngine;

public class EnableRagdollOnExit : StateMachineBehaviour
{
    [Range(0.8f, 1.0f)] public float normalizedTime = 0.98f;
    private bool fired = false;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        fired = false ;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if(fired) return;
        if (stateInfo.normalizedTime >= normalizedTime)
        {
            fired = true ;
            var rc = animator.GetComponent<RagDollController>();
            if (rc) rc.EnableRagDoll();
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if(fired) return;
        var rc = animator.GetComponent<RagDollController>();
        if (rc) rc.EnableRagDoll();
    }

}
