using UnityEngine;

public abstract class State
{
    protected EnemyAI enemy;

    public State (EnemyAI enemy)
    {
        this.enemy = enemy;
    }

    public abstract void Update ();
    public abstract void Enter();
    public abstract void Exit ();


}
