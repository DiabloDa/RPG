using UnityEngine;

public class HitTargetFollower : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f,0.85f,0f);
    [SerializeField] private SimpleIk ik;
    public void MoveTo(Vector3 worldHitPoint, Transform lookAt = null, float pulse = 0.18f)
    {
        if (!target) return;
        target.position = worldHitPoint + offset;
        if(ik)
        {
            if (lookAt) ik.lookAtTarget = lookAt;
            ik.Pulse(pulse);
            

        }

    }



}
