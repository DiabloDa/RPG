using System.Collections;
using UnityEngine;

public class HitStopper : MonoBehaviour
{
    [SerializeField] private float defaultDuration = 0.06f;
    private bool running;
    private float originalSpeed = 1f;

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }
    public void HitStop(float duration = -1f)
   {
        if (!running) StartCoroutine(HitStopRoutine(duration > 0 ? duration : defaultDuration));


   }

    private IEnumerator HitStopRoutine(float duration)
    {
        running = true;
        originalSpeed =  _animator.speed;
        _animator.speed = 0f;

        yield return new WaitForSecondsRealtime(duration);

        _animator.speed = originalSpeed;

        running = false;




    }




}
