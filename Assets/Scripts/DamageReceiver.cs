using UnityEngine;

public class DamageReceiver : MonoBehaviour, IdamageReceiver<float>
{
      public void ReceiveDamage(float damage)
    {
        Debug.Log("muelto");
    }
}
