using System.Runtime.CompilerServices;
using UnityEngine;

public class IdleState : State
{
    private float idleTime = 2f;
    private float timer;


    public IdleState(EnemyAI enemy)  : base(enemy)
    {
       
    
    
    }

    public override void Enter()
    {
        timer = 0f;
        enemy.agent.isStopped = true;
    }

    public override void Update()
    {
        timer += Time.deltaTime;
        if(enemy.PlayerInRange(5f))
        {
            enemy.ChangeState(new ChaseState(enemy));
            return;
        }

        if (timer >= idleTime)
        {
            enemy.ChangeState(new PatrolState(enemy));
        }
    }

    public override void Exit()
    {
       
    }


}
