using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.AI;

public class ChaseState : State
{
    private bool _useFallbackDirect;
    private EnemySimpleChase _directChase;

    public ChaseState(EnemyAI enemy) : base(enemy)
    {

    }

    public override void Enter()
    {
        _useFallbackDirect = false;
        _directChase = null;

        if (enemy.CanUseNavMeshAgent())
        {
            enemy.agent.isStopped = false;
            enemy.agent.speed = enemy.runSpeed;
        }
    }

    public override void Update()
    {
        if (enemy.player == null) return;

        // If using NavMesh, try to path to player.
        if (!_useFallbackDirect && enemy.agent != null && enemy.agent.enabled && enemy.agent.isOnNavMesh)
        {
            Vector3 playerPos = enemy.player.position;
            
            // Sample the player position on NavMesh to ensure valid target.
            if (NavMesh.SamplePosition(playerPos, out var hit, 2f, NavMesh.AllAreas))
            {
                playerPos = hit.position;
            }

            // Calculate path to see if it's reachable.
            var path = new NavMeshPath();
            bool canPath = enemy.agent.CalculatePath(playerPos, path) && path.status == NavMeshPathStatus.PathComplete;

            if (canPath)
            {
                // NavMesh can reach: use it.
                enemy.agent.SetDestination(playerPos);
            }
            else
            {
                // No path on NavMesh: switch to direct chase.
                _useFallbackDirect = true;
                if (enemy.CanUseNavMeshAgent())
                {
                    enemy.agent.isStopped = true;
                    enemy.agent.ResetPath();
                }
            }
        }

        // Fallback: direct transform-based chase when NavMesh unreachable.
        if (_useFallbackDirect)
        {
            if (_directChase == null)
            {
                _directChase = enemy.GetComponent<EnemySimpleChase>();
                if (_directChase == null)
                {
                    _directChase = enemy.gameObject.AddComponent<EnemySimpleChase>();
                }
            }

            _directChase.target = enemy.player;
        }

        // Enter attack state when the player is close enough.
        if (enemy.PlayerInRange(enemy.attackRange))
        {
            enemy.ChangeState(new AttackState(enemy));
            return;
        }

        // Exit chase when player is far enough.
        if (!enemy.PlayerInRange(6f))
        {
            enemy.ChangeState(new IdleState(enemy));
        }
    }

    public override void Exit()
    {
        // Cleanup fallback component when exiting Chase state.
        if (_directChase != null)
        {
            Object.Destroy(_directChase);
            _directChase = null;
        }
    }
}
