using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class ChaseState : State
{
    public ChaseState(EnemyAI enemy) : base(enemy)
    {

    }

    public override void Enter()
    {
        enemy.agent.isStopped = false;
        enemy.agent.speed = enemy.runSpeed;
    }

    public override void Update()
    {
        if (enemy.player != null) return;

        enemy.agent.SetDestination(enemy.player.position);
        
        if(!enemy.PlayerInRange(6f))
        {
            enemy.ChangeState(new IdleState(enemy));

        }
    }

    public override void Exit()
    {
        throw new System.NotImplementedException(); 
    }
}
