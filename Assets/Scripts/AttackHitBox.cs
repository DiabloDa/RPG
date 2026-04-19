using Unity.VisualScripting;
using UnityEngine;

public class AttackHitBox : MonoBehaviour, IDamageSender<DamageMessage>
{
    [SerializeField] private DamageMessage damageMessage;
    [SerializeField] private bool requireAttackControllerWindow = true;
    [SerializeField] private bool configureRigidbodyForTriggers = true;
    [SerializeField] private bool debugDamage = false;

    private AttackController _attackController;
    private Transform _senderRoot;


    private void OnEnable()
    {
        _attackController = GetComponentInParent<AttackController>(true);

        if (configureRigidbodyForTriggers)
        {
            // Ensure trigger events fire even if the Collider is on a child object.
            // A kinematic Rigidbody on this GameObject will aggregate child colliders.
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Auto-fill sender so DamageController can calculate direction and self-hit filtering works.
        _senderRoot = _attackController != null ? _attackController.transform.root : transform.root;
        damageMessage.sender = _senderRoot.gameObject;

        if (_attackController != null)
        {
            damageMessage.damageLevel = _attackController.CurrentDamageLevel;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Prevent self-hits (common source of "ghost" impacts when weapon overlaps player colliders).
        if (other != null && other.transform != null && _senderRoot != null && other.transform.root == _senderRoot)
        {
            return;
        }

        // Only apply damage/feedback during an actual attack window.
        // Default behavior is weapon-style hitboxes driven by an AttackController.
        if (requireAttackControllerWindow && (_attackController == null || !_attackController.IsAttacking))
        {
            return;
        }

        // Don't rely on TryGetComponent/GetComponent with interfaces. Find a MonoBehaviour that implements it.
        IdamageReceiver<DamageMessage> receiver = null;

        var behaviours = other.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IdamageReceiver<DamageMessage> r)
            {
                receiver = r;
                break;
            }
        }

        if (receiver == null)
        {
            var parentBehaviours = other.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < parentBehaviours.Length; i++)
            {
                if (parentBehaviours[i] is IdamageReceiver<DamageMessage> r)
                {
                    receiver = r;
                    break;
                }
            }
        }

        if (receiver != null)
        {
            if (debugDamage)
            {
                Debug.Log(
                    $"[AttackHitBox] '{name}' hit '{other.name}' amount={damageMessage.amount} level={damageMessage.damageLevel} sender={(damageMessage.sender != null ? damageMessage.sender.name : "<null>")}",
                    this);
            }
            SendDamage(receiver);
        }
    }

   public void SendDamage(IdamageReceiver<DamageMessage> receiver)
    {
        if (_attackController != null)
        {
            damageMessage.damageLevel = _attackController.CurrentDamageLevel;
        }

        receiver.ReceiveDamage(damageMessage);

        float extra = 0.02f * (int)damageMessage.damageLevel;
        GetComponent<HitStopper>()?.HitStop(0.02f + extra);
    }




}
