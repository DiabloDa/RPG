using UnityEngine;

public class ChangeStatOnExit : StateMachineBehaviour
{

    [SerializeField] private string inputStatName;
    [SerializeField] private string outputStatName;
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetFloat(outputStatName, animator.GetFloat(inputStatName));
    }

}
