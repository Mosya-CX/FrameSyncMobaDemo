public interface IDamageModifierProvider
{
    void ModifyOutgoingDamage(DamageContext context);
    void ModifyIncomingDamage(DamageContext context);
}

public interface IHealModifierProvider
{
    void ModifyOutgoingHeal(HealContext context);
    void ModifyIncomingHeal(HealContext context);
}