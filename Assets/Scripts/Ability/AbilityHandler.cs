using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class AbilityHandler : UnitBaseHandler
{
    public readonly Dictionary<int, AbilityRuntime> abilities = new();

    protected override void Awake()
    {
        base.Awake();

        var initialAbilities = owner.definitionConfig.abilityList;
        if (initialAbilities == null)
            return;

        for (int i = 0; i < initialAbilities.Length; i++)
        {
            var data = initialAbilities[i];
            if (data != null && !abilities.ContainsKey(data.Id))
                abilities.Add(data.Id, new AbilityRuntime(data, this));
        }
    }

    public override void Tick(fp deltaTime)
    {
        foreach (var runtime in abilities.Values)
            runtime.Tick(deltaTime);
    }

    public bool TryGetRuntime(int id, out AbilityRuntime runtime)
    {
        return abilities.TryGetValue(id, out runtime);
    }

    public override object CaptureState() => null;
    public override void RestoreState(object state) { }

    protected override void OnDamageDealt(in DamageInfo info) { }
    protected override void OnDamageTaken(in DamageInfo info) { }
    protected override void OnKill(in DamageInfo info) { }
    protected override void OnDeath(in DamageInfo info) { }
}