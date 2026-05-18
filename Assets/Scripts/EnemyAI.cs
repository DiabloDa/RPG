using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR;
using System;

public class EnemyAI : MonoBehaviour, IdamageReceiver<DamageMessage>
{

    private State currentState;

    public NavMeshAgent agent;
    public Transform player;
    public Transform[] waypoints;
    public Animator animator;
    [Header("Death Settings")]
    [Tooltip("Small manual Y offset applied after grounding (negative moves corpse down).")]
    [SerializeField] private float deathGroundOffset = -0.05f;
    [Tooltip("Delay before starting fade-out (seconds).")]
    [SerializeField] private float deathFadeDelay = 1.0f;
    [Tooltip("Duration of the fade-out in seconds.")]
    [SerializeField] private float deathFadeDuration = 2.0f;
    [Tooltip("If true, destroy GameObject after fade; otherwise just disable renderers.")]
    [SerializeField] private bool destroyAfterFade = true;

    [Header("movement")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 3.5f;
    public float damageReactionSeconds = 0.8f;

    [Header("health")]
    public float maxHealth = 100f;
    private float currentHealth;
    private bool isDead;
    public float rotationSmooth = 12f;
    public float aniSmooth = 10f;
    [SerializeField, Min(0f)] private float maxStoppingDistance = 0.6f;

    [Header("combat")]
    public float attackRange = 1.2f;
    public float attackExitDistance = 1.6f;
    public float attackCooldown = 0.8f;
    public float attackDamage = 8f;
    public float attackPauseSeconds = 0.3f;

    [Header("Attack Animation (optional)")]
    public string attackTriggerParam = "Attack";
    public string attackStateName = "Attack_3Combo_1";
    [SerializeField, Min(0f)] public float attackAnimFallbackSeconds = 0.5f;

    [Header("Animation (optional)")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    private bool _hasIsMoving;
    private bool _hasMoveSpeed;
    private int _isMovingHash;
    private int _moveSpeedHash;

    private int waypointIndex = 0;

    private Vector3 _previousPosition;
    private float _pauseUntil;
    private bool _wasPaused;

    public void PauseForSeconds(float seconds)
    {
        if (seconds <= 0f) return;
        _pauseUntil = Mathf.Max(_pauseUntil, Time.time + seconds);

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    public void ResumeNow()
    {
        _pauseUntil = 0f;
        _wasPaused = false;

        if (agent != null)
        {
            agent.isStopped = false;
        }
    }

    public bool IsInAttackAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(attackStateName)) return false;

        var current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName(attackStateName)) return true;
        if (current.shortNameHash == Animator.StringToHash(attackStateName)) return true;

        if (animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsName(attackStateName)) return true;
            if (next.shortNameHash == Animator.StringToHash(attackStateName)) return true;
        }

        return false;
    }

    public void ReceiveDamage(DamageMessage damage)
    {
        if (isDead) return;
        float amount = Mathf.Max(0f, damage.amount);
        float previous = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - amount);

        bool targetIsDead = currentHealth <= 0f;

        // Report enemy health in a single, easy-to-filter console line.
        DevDebug.LogEnemyHealth($"{gameObject.name}: {previous} -> {currentHealth} (-{amount})");
        PlayDamageReaction(damage.sender?.transform, damage.damageLevel, targetIsDead);

        if (targetIsDead)
        {
            Die();
            return;
        }

        ChangeState(new DamageState(this, damageReactionSeconds));
    }

    private void PlayDamageReaction(Transform attacker, DamageMessage.DamageLevel level, bool isDead)
    {
        if (animator == null) return;

        if (attacker == null)
        {
            animator.SetInteger("damageLevel", (int)level);
            animator.SetTrigger("Damage");
            return;
        }

        Vector3 damageDirection = (attacker.position - transform.position).normalized;
        damageDirection = Vector3.ProjectOnPlane(damageDirection, transform.up);
        if (damageDirection.sqrMagnitude < 0.0001f)
        {
            damageDirection = transform.forward;
        }

        float damageAngle = Vector3.SignedAngle(transform.forward, damageDirection, transform.up);
        animator.SetFloat("damageDirection", (damageAngle / 180f) * 0.5f + 0.5f);
        animator.SetInteger("damageLevel", (int)level);
        animator.SetTrigger("Damage");

        if (isDead)
        {
            animator.ResetTrigger("Damage");
            animator.SetTrigger("Die");
        }
    }

    

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        if (animator != null)
        {
            // Ensure the animator plays a visible death/knockdown reaction even if transitions
            // expect the Damage trigger + damage level/direction to select the correct state.
            try
            {
                animator.SetInteger("damageLevel", (int)DamageMessage.DamageLevel.Big);
                animator.SetFloat("damageDirection", 0.5f);
                animator.SetTrigger("Damage");
            }
            catch { }

            // Also fire the Die trigger if the controller uses it.
            animator.SetTrigger("Die");

            // As a stronger fallback, directly play the knockdown death state so the
            // enemy visibly falls even if transitions/conditions prevent automatic entry.
            try
            {
                animator.Play("Damage_Front_High_KnockDown", 0, 0f);
                animator.Update(0f);
            }
            catch { }
        }
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Disable attack/chase components so the enemy cannot continue behavior after death.
        var melee = GetComponent<EnemyMeleeAttack>();
        if (melee != null) melee.enabled = false;

        var chase = GetComponent<EnemySimpleChase>();
        if (chase != null) chase.enabled = false;

        // Also disable this AI behaviour to stop state updates.
        this.enabled = false;

        // Start coroutine to freeze the final death pose when the death animation finishes.
        StartCoroutine(FreezeOnDeathAnimationFinish());
        // Start fade/destroy sequence.
        StartCoroutine(FadeAndRemove());

        Debug.Log($"[EnemyAI] Die() called on '{gameObject.name}' - disabled AI and attack components.", this);
    }

    private System.Collections.IEnumerator FreezeOnDeathAnimationFinish()
    {
        if (animator == null)
            yield break;

        string targetState = "Damage_Front_High_KnockDown";
        float maxWait = 3f;
        float start = Time.time;

        while (Time.time - start < maxWait)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);

            if (info.IsName(targetState))
            {
                // Wait until the animation is nearly finished (normalizedTime >= 0.95)
                if (info.normalizedTime >= 0.95f)
                {
                    break;
                }
            }

            yield return null;
        }

        // Attempt to sample the last frame of the death state and freeze there.
        try
        {
            animator.Play(targetState, 0, 0.999f);
            animator.Update(0f);
            animator.applyRootMotion = false;
            animator.speed = 0f; // freeze playback at sampled frame
        }
        catch { }

        // After sampling the final frame, nudge the whole object so its lowest renderer/collider
        // point sits on the ground. This prevents the 'floating' death pose.
        AdjustDeathPoseToGround();
    }

    private System.Collections.IEnumerator FadeAndRemove()
    {
        if (deathFadeDelay > 0f) yield return new WaitForSeconds(deathFadeDelay);

        var renderers = GetComponentsInChildren<Renderer>(true);
        var mats = new System.Collections.Generic.List<Material>();
        var originalColors = new System.Collections.Generic.List<Color>();

        foreach (var r in renderers)
        {
            if (r == null) continue;
            // Accessing .materials will create instances so we don't modify shared assets.
            var rm = r.materials;
            for (int i = 0; i < rm.Length; i++)
            {
                var m = rm[i];
                if (m == null) continue;
                mats.Add(m);
                if (m.HasProperty("_Color")) originalColors.Add(m.color);
                else originalColors.Add(Color.white);
            }
        }

        bool anyColor = false;
        for (int i = 0; i < mats.Count; i++)
        {
            if (mats[i] != null && mats[i].HasProperty("_Color")) { anyColor = true; break; }
        }

        float t = 0f;
        if (anyColor)
        {
            while (t < deathFadeDuration && mats.Count > 0)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (t / Mathf.Max(0.0001f, deathFadeDuration)));
                for (int i = 0; i < mats.Count; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (m.HasProperty("_Color"))
                    {
                        Color c = originalColors[i];
                        c.a = alpha;
                        m.color = c;
                    }
                }
                yield return null;
            }
        }
        else
        {
            // Fallback: gradually sink the corpse slightly and then hide it.
            float sinkAmount = 0.25f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + Vector3.down * sinkAmount;
            while (t < deathFadeDuration)
            {
                t += Time.deltaTime;
                float f = Mathf.Clamp01(t / Mathf.Max(0.0001f, deathFadeDuration));
                transform.position = Vector3.Lerp(startPos, endPos, f);
                yield return null;
            }
        }

        if (destroyAfterFade)
        {
            Destroy(gameObject);
        }
        else
        {
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = false;
            }
        }
    }

    private void AdjustDeathPoseToGround()
    {
        // More robust grounding: sample skinned/mesh renderers to find the actual lowest
        // vertex in world-space, then raycast down under that point to place the corpse.
        Vector3 lowestWorldPos = Vector3.zero;
        bool foundVertex = false;

        var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in skinned)
        {
            if (smr == null) continue;
            try
            {
                Mesh baked = new Mesh();
                smr.BakeMesh(baked);
                var verts = baked.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 w = smr.transform.TransformPoint(verts[i]);
                    if (!foundVertex || w.y < lowestWorldPos.y)
                    {
                        lowestWorldPos = w;
                        foundVertex = true;
                    }
                }
            }
            catch { }
        }

        var filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            var m = mf.sharedMesh;
            var verts = m.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = mf.transform.TransformPoint(verts[i]);
                if (!foundVertex || w.y < lowestWorldPos.y)
                {
                    lowestWorldPos = w;
                    foundVertex = true;
                }
            }
        }

        // Fallback to collider bounds if we didn't find mesh vertices
        Bounds bounds = default;
        if (!foundVertex)
        {
            var col = GetComponentInChildren<Collider>();
            if (col != null)
            {
                bounds = col.bounds;
                lowestWorldPos = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foundVertex = true;
            }
            else
            {
                var rends = GetComponentsInChildren<Renderer>(true);
                if (rends != null && rends.Length > 0)
                {
                    bounds = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
                    lowestWorldPos = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    foundVertex = true;
                }
            }
        }

        if (!foundVertex) return;

        // Raycast down from above the lowest vertex to find the ground directly below it.
        float maxDrop = 5f;
        Vector3 rayOrigin = lowestWorldPos + Vector3.up * 1.0f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxDrop, ~0, QueryTriggerInteraction.Ignore))
        {
            float delta = hit.point.y - lowestWorldPos.y;
            if (Mathf.Abs(delta) > 0.0001f)
            {
                transform.position += new Vector3(0f, delta, 0f);
            }
        }

        // Apply a small manual offset if the corpse still appears slightly above ground.
        if (Mathf.Abs(deathGroundOffset) > 0.00001f)
        {
            transform.position += new Vector3(0f, deathGroundOffset, 0f);
        }

        // If there's a Rigidbody, make it kinematic so physics doesn't lift the corpse.
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    public bool HasAnimatorParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName)) return false;

        foreach (var param in animator.parameters)
        {
            if (param.name == parameterName) return true;
        }

        return false;
    }

    public bool HasAnimatorState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;
        return animator.HasState(0, Animator.StringToHash(stateName));
    }

    static class Hash
    {
        public static readonly int SpeedX = Animator.StringToHash("SpeedX");
        public static readonly int SpeedY = Animator.StringToHash("SpeedY");
    }

    private void Start()
    {
        // Initialize components and navmesh agent settings to avoid the enemy overlapping the player.
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        currentHealth = maxHealth;
        isDead = false;

        if (agent == null)
        {
            Debug.LogError("[EnemyAI] Missing NavMeshAgent.", this);
            enabled = false;
            return;
        }

        agent.updatePosition = true;
        agent.updateRotation = false;

        // Ensure enemy keeps a sensible stopping distance so it doesn't push into the player.
        if (maxStoppingDistance > 0f)
        {
            agent.stoppingDistance = maxStoppingDistance;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            // CacheAnimatorParams(); // optional; keep commented if not needed
        }

        _previousPosition = transform.position;
        ChangeState(new IdleState(this));

        // Validate that this enemy can receive damage from player attacks
        ValidateDamageSetup();
    }

    private void ValidateDamageSetup()
    {
        // Check for colliders
        var colliders = GetComponents<Collider>();
        if (colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider>();
        }

        if (colliders.Length == 0)
        {
            Debug.LogWarning($"[EnemyAI] Enemy '{gameObject.name}' has NO Colliders! Player attacks won't hit it. Add a Collider (BoxCollider, SphereCollider, etc.)", gameObject);
            return;
        }

        // Check if any collider is set as trigger
        bool hasTrigger = false;
        foreach (var col in colliders)
        {
            if (col.isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[EnemyAI] Enemy '{gameObject.name}' has Colliders but NONE are set as isTrigger=true! Player attacks won't trigger collision. Set at least one Collider's isTrigger to true.", gameObject);
            return;
        }

        // Check for Rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = GetComponentInChildren<Rigidbody>();
        }

        if (rb == null)
        {
            Debug.LogWarning($"[EnemyAI] Enemy '{gameObject.name}' has trigger Colliders but NO Rigidbody! Add a Rigidbody (set to Kinematic or Dynamic) for collision detection to work.", gameObject);
            return;
        }

        Debug.Log($"[EnemyAI] Enemy '{gameObject.name}' is properly configured for damage reception. Colliders: {colliders.Length}, Has trigger: {hasTrigger}, Rigidbody: {rb.name}", gameObject);
    }

   /* private void CacheAnimatorParams()
    {
        _hasIsMoving = false;
        _hasMoveSpeed = false;

        if (animator == null) return;

        _isMovingHash = Animator.StringToHash(isMovingParam);
        _moveSpeedHash = Animator.StringToHash(moveSpeedParam);

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == isMovingParam)
            {
                _hasIsMoving = parameters[i].type == AnimatorControllerParameterType.Bool;
            }
            else if (parameters[i].name == moveSpeedParam)
            {
                _hasMoveSpeed = parameters[i].type == AnimatorControllerParameterType.Float;
            }
        }
    }

    private void SetAnimatorMoving(bool moving)
    {
        if (animator == null) return;

        if (_hasIsMoving)
        {
            animator.SetBool(_isMovingHash, moving);
        }

        if (_hasMoveSpeed)
        {
            animator.SetFloat(_moveSpeedHash, moving ? 1f : 0f);
        }
    }*/

    private void Update()
    {
        /*bool paused = Time.time < _pauseUntil;

        if (paused)
        {
            _wasPaused = true;
            if (agent != null)
            {
                agent.isStopped = true;
            }

            SetAnimatorMoving(false);
        }
        else
        {
            if (_wasPaused)
            {
                _wasPaused = false;
                if (agent != null)
                {
                    agent.isStopped = false;
                }
            }

            
        }*/

        currentState?.Update();

        Vector3 velocity = Vector3.zero;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            velocity = agent.velocity;
        }
        else
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            velocity = (transform.position - _previousPosition) / dt;
        }

        velocity.y = 0f;

        Vector3 driveDirection = velocity;
        if (driveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(driveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSmooth * Time.deltaTime);
        }

        Vector3 localDrive = driveDirection.sqrMagnitude > 0.001f ? transform.InverseTransformDirection(driveDirection.normalized) : Vector3.zero;
        float denom = agent != null ? Mathf.Max(0.01f, agent.speed) : Mathf.Max(1f, walkSpeed);
        float mag01 = Mathf.Clamp01(velocity.magnitude / denom);

        float targetX = localDrive.x * mag01;
        float targetY = localDrive.z * mag01;

        if (animator != null)
        {
            float curX = Mathf.Lerp(animator.GetFloat(Hash.SpeedX), targetX, Time.deltaTime * aniSmooth);
            float curY = Mathf.Lerp(animator.GetFloat(Hash.SpeedY), targetY, Time.deltaTime * aniSmooth);

            animator.SetFloat(Hash.SpeedX, curX);
            animator.SetFloat(Hash.SpeedY, curY);
        }

        _previousPosition = transform.position;
    }

    public void ChangeState(State newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();

    }

    public void NextWaypoint()
    {
        if(waypoints == null || waypoints.Length == 0) return;
        waypointIndex = (waypointIndex+1) % waypoints.Length;
        agent.SetDestination(waypoints[waypointIndex].position);
    }


    public bool PlayerInRange(float range)
    {

        if(player == null) return false;
        return Vector3.Distance(transform.position, player.position) < range;

    }


}
