using UnityEngine;

public class CleanupHitBox : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.gameObject.SendMessage("cleanupAttackHitBox");
    }
}
