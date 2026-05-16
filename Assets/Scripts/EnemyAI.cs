using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR;
using System;

public class EnemyAI : MonoBehaviour 
{

    private State currentState;

    public NavMeshAgent agent;
    public Transform player;
    public Transform[] waypoints;
    public Animator animator;

    [Header("movement")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 3.5f;
    public float rotationSmooth = 12f;
    public float aniSmooth = 10f;
    [SerializeField, Min(0f)] private float maxStoppingDistance = 0.6f;

    [Header("Animation (optional)")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    private bool _hasIsMoving;
    private bool _hasMoveSpeed;
    private int _isMovingHash;
    private int _moveSpeedHash;

    private int waypointIndex = 0;

    private float _pauseUntil;
    private bool _wasPaused;

   /* public void PauseForSeconds(float seconds)
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
    }*/

    static class Hash
    {
        public static readonly int SpeedX = Animator.StringToHash("SpeedX");
        public static readonly int SpeedY = Animator.StringToHash("SpeedY");
    }

    private void Start()
    {
        ChangeState(new IdleState(this));

        /*if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (agent == null)
        {
            Debug.LogError("[EnemyAI] Missing NavMeshAgent.", this);
            enabled = false;
            return;
        }

        agent.updatePosition = true;
        agent.updateRotation = false;

        // Approach a bit closer before coming to a stop (helps melee feel).
        // Only ever reduces stopping distance; won't override tighter setups.
        if (maxStoppingDistance >= 0f)
        {
            agent.stoppingDistance = Mathf.Min(agent.stoppingDistance, maxStoppingDistance);
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            CacheAnimatorParams();
        }

        ChangeState(new IdleState(this));*/
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

       // Vector3 desird = agent != null ? agent.desiredVelocity : Vector3.zero;
        Vector3 desird = agent.desiredVelocity;
        desird.y = 0;

       // bool moving = desird.sqrMagnitude > 0.01f;

       /* if (!paused)
        {
            SetAnimatorMoving(moving);
        }*/

        if(desird.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(desird, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSmooth*Time.deltaTime);


        }

        Vector3 dirlocal = desird.sqrMagnitude > 0.001f ? transform.InverseTransformDirection(desird.normalized) : Vector3.zero;

        float denom = agent != null ? Mathf.Max(0.01f, agent.speed) : 1f;
        float mag01 = agent != null ? Mathf.Clamp01(agent.velocity.magnitude / denom) : 0f;

        float targetX = dirlocal.x * mag01;
        float targetY = dirlocal.y * mag01;

        //if (animator != null)
       // {
            float curX = Mathf.Lerp(animator.GetFloat(Hash.SpeedX), targetX, Time.deltaTime*aniSmooth);
            float curY = Mathf.Lerp(animator.GetFloat(Hash.SpeedY), targetY, Time.deltaTime * aniSmooth);

            animator.SetFloat(Hash.SpeedX, curX);
            animator.SetFloat(Hash.SpeedY, curY);
     //   }
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
