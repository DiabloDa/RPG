public interface IdamageReceiver<TDamage> where TDamage : struct
{
    void ReceiveDamage(TDamage damage); 

}
