public class AttackMissle : TargetTrackFlyingMissleBase
{
    protected override void OnMissleApply()
    {
        if (Owner != null && Target != null && !Target.IsDead)
            DamageManager.Instance.CreateAttackDamageRequest(Owner, Target);

        base.OnMissleApply();
    }
}


