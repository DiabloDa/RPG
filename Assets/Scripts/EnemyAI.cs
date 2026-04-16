using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR;

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

    private int waypointIndex = 0;

    static class Hash
    {
        public static readonly int SpeedX = Animator.StringToHash("SpeedX");
        public static readonly int SpeedY = Animator.StringToHash("SpeedY");

    }

    private void Start()
    {
        agent.updatePosition = true;
        agent.updateRotation = false;

        animator.applyRootMotion = false;

        ChangeState(new IdleState(this));
    }

    private void Update()
    {
        
        currentState?.Update();

        Vector3 desird = agent.desiredVelocity;
        desird.y = 0;

        if(desird.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(desird, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSmooth*Time.deltaTime);


        }

        Vector3 dirlocal = desird.sqrMagnitude > 0.001f ? transform.InverseTransformDirection(desird.normalized) : Vector3.zero;

        float denom = Mathf.Max(0.01f, agent.speed);
        float mag01 = Mathf.Clamp01(agent.velocity.magnitude / denom);

        float targetX = dirlocal.x * mag01;
        float targetY = dirlocal.y * mag01;

        float curX = Mathf.Lerp(animator.GetFloat(Hash.SpeedX), targetX, Time.deltaTime*aniSmooth);
        float curY = Mathf.Lerp(animator.GetFloat(Hash.SpeedY), targetY, Time.deltaTime * aniSmooth);

        animator.SetFloat(Hash.SpeedX, curX);
        animator.SetFloat (Hash.SpeedY, curY);
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
