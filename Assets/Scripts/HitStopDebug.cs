using UnityEngine;

public class HitStopDebug : MonoBehaviour
{

    public float duration = 0.05f;
    private HitStopper hs;

    private void Awake()
    {
        hs = GetComponent<HitStopper>();
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.H))
        {
            hs?.HitStop();
        }
    }



}
