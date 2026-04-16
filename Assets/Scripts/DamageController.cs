using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DamageController : MonoBehaviour
{
    [SerializeField] private bool ignoreDamage;
    
    private List<DamageMessage> damageList = new List<DamageMessage>();

    private Animator animator;

    private Vector3 lastHitPoint;
    private Vector3 lastImpulse;


    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void EnqueueDamage(DamageMessage damage)
    {
        if(ignoreDamage || damageList.Any(dmg => dmg.sender == damage.sender)) return;
        var dirFromAttacker = (transform.position - damage.sender.transform.position).normalized;
        lastHitPoint = transform.position + dirFromAttacker * 0.3f;
        damageList.Add(damage);
    }

    public void IframeStart()
    {
        ignoreDamage = true;
    }

    public void IframeEnd()
    {
        ignoreDamage = false;
    }
    private void Update()
    {
        Vector3 damageDirection = Vector3.zero;
        int damageLevel = 0;
        bool isDead = false;
        foreach (DamageMessage message in damageList)
        {

            Game.Instance.PlayerOne.DepleteHealth(message.amount, out isDead);
            damageDirection += (message.sender.transform.position - transform.position).normalized;
            damageLevel = Mathf.Max(damageLevel, (int)message.damageLevel);


        }

        if (damageList.Count == 0) return;

        damageDirection = Vector3.ProjectOnPlane(damageDirection.normalized, transform.up);
        float damageAngle = Vector3.SignedAngle(transform.forward, damageDirection, transform.up);

        animator.SetFloat("damageDirection", (damageAngle / 180) * 0.5f + 0.5f);
        animator.SetInteger("damageLevel", damageLevel);

        animator.SetTrigger("Damage");

        if (isDead)
        {
            animator.ResetTrigger("Damage");
            animator.SetTrigger("Die");
        }

        var attacker = damageList[0].sender.transform;
        GetComponent<HitTargetFollower>()?.MoveTo(lastHitPoint, attacker, 0.18f);
        damageList.Clear();
    }

    }
