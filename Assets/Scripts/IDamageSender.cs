public interface IDamageSender<TDamage> where TDamage : struct
{
    void SendDamage(IdamageReceiver<TDamage> receiver);

}


