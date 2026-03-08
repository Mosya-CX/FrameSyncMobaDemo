public class AttackMissle : TargetTrackMissle
{
    protected override void OnMissleApply()
    {
        DamageManager.Instance.CreateAttackDamageRequest(owner, target);
        base.OnMissleApply();
    }
}


