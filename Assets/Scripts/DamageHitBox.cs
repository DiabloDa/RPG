using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class DamageHitBox : MonoBehaviour, IdamageReceiver<DamageMessage>
{
    
    [Serializable] public class AttackQueueEvent : UnityEvent<DamageMessage> { }
    [SerializeField] private float defenseMultiplier;
    public AttackQueueEvent OnHit;

   public void ReceiveDamage(DamageMessage damage)
   {
        if (damage.sender == transform.root.gameObject) return;
        damage.amount = damage.amount * defenseMultiplier;  
        OnHit?.Invoke(damage);
   }




}
