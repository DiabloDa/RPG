using UnityEngine;

public class DamageState : State
{
    private readonly float _durationSeconds;
    private float _endTime;

    public DamageState(EnemyAI enemy, float durationSeconds) : base(enemy)
    {
        _durationSeconds = Mathf.Max(0f, durationSeconds);
    }

    public override void Enter()
    {
        Debug.Log($"[DamageState] Entered. Duration={_durationSeconds}s for {enemy.gameObject.name}", enemy);
        
        if (enemy.CanUseNavMeshAgent())
        {
            enemy.agent.isStopped = true;
            enemy.agent.ResetPath();
        }

        _endTime = Time.time + _durationSeconds;
    }

    public override void Update()
    {
        if (Time.time < _endTime)
        {
            return;
        }

        if (enemy.player != null)
        {
            if (enemy.PlayerInRange(enemy.attackRange))
            {
                enemy.ChangeState(new AttackState(enemy));
                return;
            }

            if (enemy.PlayerInRange(6f))
            {
                enemy.ChangeState(new ChaseState(enemy));
                return;
            }
        }

        enemy.ChangeState(new IdleState(enemy));
    }

    public override void Exit()
    {
        if (enemy.CanUseNavMeshAgent())
        {
            enemy.agent.isStopped = false;
        }
    }
}
