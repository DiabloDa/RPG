using UnityEngine;

public class PatrolState : State
{
    public PatrolState(EnemyAI enemy) : base(enemy)
    {
    }

    public override void Enter()
    {
        
        enemy.agent.isStopped = false;
        enemy.agent.speed = enemy.walkSpeed;
        enemy.NextWaypoint();
    }

    public override void Update()
    {
        if (enemy.PlayerInRange(5f))
        {
            enemy.ChangeState(new ChaseState(enemy));
            return;
        }
        if (!enemy.agent.pathPending && enemy.agent.remainingDistance < 0.05f) enemy.NextWaypoint();

        /*// Always chase when a player target is assigned.
        if (enemy.player != null)
        {
            enemy.ChangeState(new ChaseState(enemy));
            return;
        }

        if (!enemy.agent.pathPending && enemy.agent.remainingDistance < 0.05f) enemy.NextWaypoint();*/
    }

    public override void Exit()
    {
        // no-op
    }

}
