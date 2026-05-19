using Unity.VisualScripting;
using UnityEngine;

public class AttackHitBox : MonoBehaviour, IDamageSender<DamageMessage>
{
    [SerializeField] private DamageMessage damageMessage;
    [SerializeField] private bool requireAttackControllerWindow = true;
    [SerializeField] private bool configureRigidbodyForTriggers = true;
    [SerializeField] private bool debugDamage = true;
    [SerializeField, Min(0f)] private float hitCooldownSeconds = 0.12f;
    [SerializeField, Min(0f)] private float smallDamageMultiplier = 1f;
    [SerializeField, Min(0f)] private float mediumDamageMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float bigDamageMultiplier = 2f;

    private AttackController _attackController;
    private Transform _senderRoot;

    private bool TryResolveAttackController()
    {
        if (_attackController != null)
        {
            return true;
        }

        _attackController = GetComponentInParent<AttackController>(true);
        if (_attackController != null)
        {
            return true;
        }

        // Fallback: try from sender/root wiring for cases where hitboxes are not under controller hierarchy.
        GameObject senderGo = damageMessage.sender;
        if (senderGo == null && _senderRoot != null)
        {
            senderGo = _senderRoot.gameObject;
        }

        if (senderGo != null)
        {
            _attackController = senderGo.GetComponentInParent<AttackController>();
            if (_attackController == null)
            {
                _attackController = senderGo.GetComponentInChildren<AttackController>(true);
            }
        }

        return _attackController != null;
    }

    // tracks last hit time per target root to avoid continuous frame-by-frame hits
    private System.Collections.Generic.Dictionary<Transform, float> _lastHitTimes = new System.Collections.Generic.Dictionary<Transform, float>();

    // Per-attack-window registry: once an attack hits a target, block further hits this window.
    private static readonly System.Collections.Generic.HashSet<Transform> s_hitThisWindow = new System.Collections.Generic.HashSet<Transform>();

    public static bool TryRegisterWindowHit(Transform targetRoot)
    {
        if (targetRoot == null) return false;
        lock (s_hitThisWindow)
        {
            if (s_hitThisWindow.Contains(targetRoot))
            {
                return false; // Already hit this target in this window
            }

            s_hitThisWindow.Add(targetRoot);
            return true;
        }
    }

    public static void ClearWindowHits()
    {
        lock (s_hitThisWindow)
        {
            s_hitThisWindow.Clear();
        }
    }


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

        // Ensure this hitbox has a Collider (auto-add if missing) and is configured as a trigger.
        var col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
            Debug.Log($"[AttackHitBox] Auto-added BoxCollider to '{gameObject.name}'", gameObject);
        }

        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.Log($"[AttackHitBox] Set Collider.isTrigger=true on '{gameObject.name}'", gameObject);
        }

        if (debugDamage)
        {
            Debug.Log($"[AttackHitBox] '{gameObject.name}' is active and ready. Collider: {col.GetType().Name}, isTrigger: {col.isTrigger}", gameObject);
        }

        // Auto-fill sender so DamageController can calculate direction and self-hit filtering works.
        if (damageMessage.sender != null)
        {
            _senderRoot = damageMessage.sender.transform.root;
        }
        else
        {
            _senderRoot = _attackController != null ? _attackController.transform.root : transform.root;
        }
        damageMessage.sender = _senderRoot.gameObject;

        if (TryResolveAttackController())
        {
            damageMessage.damageLevel = _attackController.CurrentDamageLevel;
        }

        // Ensure a reasonable default damage so runtime-created hitboxes actually deal damage.
        if (Mathf.Approximately(damageMessage.amount, 0f))
        {
            // Default to 15 for light attacks so Heavy (big multiplier 2x) becomes 30.
            damageMessage.amount = 15f;
            if (debugDamage)
            {
                Debug.Log($"[AttackHitBox] Setting default damage amount=15 for '{name}'", this);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessTrigger(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null || other.transform == null) return;
        Transform targetRoot = other.transform.root;
        if (targetRoot != null && _lastHitTimes.ContainsKey(targetRoot))
        {
            _lastHitTimes.Remove(targetRoot);
            if (debugDamage)
            {
                Debug.Log($"[AttackHitBox] Cleared hit cooldown for '{other.name}' on exit.", this);
            }
        }
    }

    private void ProcessTrigger(Collider other)
    {
        TryResolveAttackController();

        if (other == null || other.transform == null)
        {
            if (debugDamage)
            {
                Debug.LogWarning($"[AttackHitBox] ProcessTrigger called with null collider or transform", this);
            }
            return;
        }

        // Prevent self-hits (common source of "ghost" impacts when weapon overlaps player colliders).
        if (_senderRoot != null && other.transform.root == _senderRoot)
        {
            if (debugDamage)
            {
                Debug.Log($"[AttackHitBox] Ignored self-hit on '{name}' against '{other.name}' (same root)", this);
            }
            return;
        }

        // If this hitbox is bound to an AttackController, only allow damage during its attack window.
        // This prevents charge/windup phases from dealing damage when colliders overlap.
        if (_attackController != null && !_attackController.IsAttacking)
        {
            if (debugDamage)
            {
                Debug.Log($"[AttackHitBox] Ignored hit on '{name}' because AttackController not in attack window (IsAttacking=false)", this);
            }
            return;
        }

        // Also check that we're in the damage window (minimum delay from attack start).
        if (_attackController != null && !_attackController.IsCurrentlyInDamageWindow())
        {
            float elapsed = _attackController.GetTimeSinceAttackStart();
            float minDelay = _attackController.GetMinDamageDelay();
            if (debugDamage)
            {
                Debug.Log($"[AttackHitBox] Ignored hit on '{name}' because not in damage window yet (elapsed={elapsed:F3}s, required={minDelay}s)", this);
            }
            return;
        }

        // If a window is required but no controller exists, fail closed to avoid instant/always-on hits.
        if (_attackController == null && requireAttackControllerWindow)
        {
            if (debugDamage)
            {
                Debug.Log($"[AttackHitBox] Blocked hit on '{name}' because requireAttackControllerWindow=true and no AttackController was found (TryResolveAttackController failed).", this);
            }
            return;
        }

        if (debugDamage)
        {
            Debug.Log($"[AttackHitBox] Collision detected on '{name}' with '{other.gameObject.name}' (attempting to find receiver...)", this);
        }

        IdamageReceiver<DamageMessage> receiver = FindPreferredDamageReceiver(other);

        if (receiver == null)
        {
            if (debugDamage)
            {
                Debug.LogWarning($"[AttackHitBox] '{other.gameObject.name}' has no IdamageReceiver<DamageMessage>! Check that the enemy has EnemyAI component or an IdamageReceiver implementation.", this);
            }
            return;
        }

        if (debugDamage)
        {
            Debug.Log($"[AttackHitBox] Found receiver on '{other.gameObject.name}': {receiver.GetType().Name}", this);
        }

        Transform targetRoot = other.transform.root;
        float now = Time.time;
        if (targetRoot != null)
        {
            if (_lastHitTimes.TryGetValue(targetRoot, out float last) && now - last < hitCooldownSeconds)
            {
                if (debugDamage)
                {
                    Debug.Log($"[AttackHitBox] Skipping hit on '{other.name}' because cooldown ({hitCooldownSeconds}s) hasn't elapsed.", this);
                }
                return;
            }

            _lastHitTimes[targetRoot] = now;
        }

        if (debugDamage)
        {
            Debug.Log(
                $"[AttackHitBox] '{name}' hit '{other.name}' amount={damageMessage.amount} level={damageMessage.damageLevel} sender={(damageMessage.sender != null ? damageMessage.sender.name : "<null>")}",
                this);
        }

        SendDamage(receiver);
    }

    private IdamageReceiver<DamageMessage> FindPreferredDamageReceiver(Collider other)
    {
        if (other == null) return null;

        // Search components on the collider first, then on parent objects.
        var receivers = new System.Collections.Generic.List<IdamageReceiver<DamageMessage>>();
        var behaviours = other.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IdamageReceiver<DamageMessage> r)
            {
                receivers.Add(r);
            }
        }

        if (receivers.Count == 0)
        {
            var parentBehaviours = other.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < parentBehaviours.Length; i++)
            {
                if (parentBehaviours[i] is IdamageReceiver<DamageMessage> r)
                {
                    receivers.Add(r);
                }
            }
        }

        if (receivers.Count == 0) return null;
        if (receivers.Count == 1) return receivers[0];

        // Prefer enemy AI or enemy health implementations when both exist on the same root.
        foreach (var receiver in receivers)
        {
            if (receiver is EnemyAI) return receiver;
        }

        foreach (var receiver in receivers)
        {
            if (receiver.GetType().Name.Contains("EnemyHealth")) return receiver;
        }

        return receivers[0];
    }

    public void SendDamage(IdamageReceiver<DamageMessage> receiver)
    {
        var outgoingDamage = damageMessage;
        if (TryResolveAttackController())
        {
            outgoingDamage.damageLevel = _attackController.CurrentDamageLevel;
        }

        Transform targetRoot = null;
        if (receiver is MonoBehaviour mb && mb.transform != null)
        {
            targetRoot = mb.transform.root;
        }

        // Check per-window registry to avoid duplicate hits in the same attack window.
        if (targetRoot != null && !TryRegisterWindowHit(targetRoot))
        {
            if (debugDamage)
            {
                Debug.Log($"[AttackHitBox] Skipping window-duplicate hit on '{targetRoot.name}'", this);
            }
            return;
        }

        outgoingDamage.amount = GetDamageAmountForLevel(outgoingDamage.amount, outgoingDamage.damageLevel);
        
        // Debug: log exact damage being sent to identify multiplier application
        DevDebug.LogPlayerHealth($"[SendDamage] Sending damage: amount={outgoingDamage.amount} level={outgoingDamage.damageLevel} to {(targetRoot != null ? targetRoot.name : "unknown")} controller={(_attackController != null ? _attackController.name : "NONE")}");
        
        receiver.ReceiveDamage(outgoingDamage);

        // small recovery on attacker to avoid it getting stuck in the player
        try
        {
            var senderRoot = damageMessage.sender != null ? damageMessage.sender.transform : _senderRoot;
            if (senderRoot != null)
            {
                var agent = senderRoot.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.ResetPath();
                    // temporarily stop the agent for a short recovery
                    StartCoroutine(TemporarilyStopAgent(agent, 0.22f));
                }
            }
        }
        catch (System.Exception ex)
        {
            if (debugDamage)
            {
                Debug.LogWarning($"[AttackHitBox] Error trying to stop sender agent: {ex}");
            }
        }

        float extra = 0.02f * (int)damageMessage.damageLevel;
        GetComponent<HitStopper>()?.HitStop(0.02f + extra);
    }

    private float GetDamageAmountForLevel(float baseAmount, DamageMessage.DamageLevel level)
    {
        switch (level)
        {
            case DamageMessage.DamageLevel.Medium:
                return baseAmount * mediumDamageMultiplier;
            case DamageMessage.DamageLevel.Big:
                return baseAmount * bigDamageMultiplier;
            default:
                return baseAmount * smallDamageMultiplier;
        }
    }

    private System.Collections.IEnumerator TemporarilyStopAgent(UnityEngine.AI.NavMeshAgent agent, float seconds)
    {
        if (agent == null) yield break;
        if (!agent.enabled || !agent.isOnNavMesh) yield break;
        bool wasStopped = agent.isStopped;
        agent.isStopped = true;
        yield return new WaitForSeconds(seconds);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = wasStopped;
        }
    }

    // Expose base damage for external fallbacks
    public float GetBaseDamage()
    {
        return damageMessage.amount;
    }

    public DamageMessage.DamageLevel GetDefaultDamageLevel()
    {
        return damageMessage.damageLevel;
    }




}
