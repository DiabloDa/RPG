using UnityEngine;

public class AttackState : State
{
    private float _nextAttackTime;
    private bool _waitingForAnimation;
    private float _attackStartedAt;
    private bool _inPostAttackCooldown;
    private float _postAttackCooldownEnd;

    public AttackState(EnemyAI enemy) : base(enemy)
    {
    }

    public override void Enter()
    {
        if (enemy.CanUseNavMeshAgent())
        {
            enemy.agent.isStopped = true;
            enemy.agent.ResetPath();
        }

        _waitingForAnimation = false;
        _attackStartedAt = 0f;
        _nextAttackTime = Time.time;
        _inPostAttackCooldown = false;
        _postAttackCooldownEnd = 0f;
    }

    public override void Update()
    {
        if (enemy.player == null) return;

        bool inAttackRange = enemy.PlayerInRange(enemy.attackRange);
        bool leftExitRange = !enemy.PlayerInRange(enemy.attackExitDistance);

        // Si está en cooldown post-ataque, espera a que termine
        if (_inPostAttackCooldown)
        {
            if (Time.time >= _postAttackCooldownEnd)
            {
                _inPostAttackCooldown = false;
                if (leftExitRange)
                {
                    enemy.ChangeState(new ChaseState(enemy));
                    return;
                }
            }
            return;
        }

        if (_waitingForAnimation)
        {
            if (!enemy.IsInAttackAnimation() && Time.time >= _attackStartedAt + enemy.attackAnimFallbackSeconds)
            {
                _waitingForAnimation = false;
                // Entra en cooldown post-ataque de 1 segundo
                _inPostAttackCooldown = true;
                _postAttackCooldownEnd = Time.time + 1f;
                return;
            }
            return;
        }

        if (inAttackRange && Time.time >= _nextAttackTime)
        {
            PerformAttack();
            _nextAttackTime = Time.time + enemy.attackCooldown;
            return;
        }

        if (leftExitRange)
        {
            enemy.ChangeState(new ChaseState(enemy));
        }
    }

    public override void Exit()
    {
        if (enemy.CanUseNavMeshAgent())
        {
            enemy.agent.isStopped = false;
        }
    }

    private void PerformAttack()
    {
        if (enemy.player == null) return;

        if (enemy.animator != null)
        {
            if (!string.IsNullOrEmpty(enemy.attackTriggerParam) && enemy.HasAnimatorParameter(enemy.attackTriggerParam))
            {
                enemy.animator.ResetTrigger(enemy.attackTriggerParam);
                enemy.animator.SetTrigger(enemy.attackTriggerParam);
                _waitingForAnimation = true;
                _attackStartedAt = Time.time;
                Debug.Log($"[AttackState] Triggered attack '{enemy.attackTriggerParam}' on '{enemy.gameObject.name}'", enemy);
            }
            else if (!string.IsNullOrEmpty(enemy.attackStateName) && enemy.HasAnimatorState(enemy.attackStateName))
            {
                enemy.animator.Play(enemy.attackStateName, 0, 0f);
                _waitingForAnimation = true;
                _attackStartedAt = Time.time;
                Debug.Log($"[AttackState] Played animator state '{enemy.attackStateName}' on '{enemy.gameObject.name}'", enemy);
            }
            else
            {
                Debug.LogWarning($"[AttackState] Animator on '{enemy.gameObject.name}' has no trigger '{enemy.attackTriggerParam}' and no state '{enemy.attackStateName}'", enemy);
            }
        }

        if (Game.Instance != null && Game.Instance.PlayerOne != null)
        {
            if (Game.Instance.PlayerOne.IsInvulnerable)
            {
                if (enemy.attackPauseSeconds > 0f)
                {
                    enemy.PauseForSeconds(enemy.attackPauseSeconds);
                }

                return;
            }

            Game.Instance.PlayerOne.DepleteHealth(enemy.attackDamage, out bool isDead);

            var targetRoot = enemy.player.root;
            if (targetRoot != null)
            {
                var damageController = targetRoot.GetComponentInChildren<DamageController>(true);
                if (damageController != null)
                {
                    damageController.PlayDamageReaction(enemy.transform.root, DamageMessage.DamageLevel.Small, isDead);
                }
            }
        }

        if (enemy.attackPauseSeconds > 0f)
        {
            enemy.PauseForSeconds(enemy.attackPauseSeconds);
        }
    }
}
