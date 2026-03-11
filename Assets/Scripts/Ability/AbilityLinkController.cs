using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class AbilityLinkController
{
    private readonly HeroUnit owner;
    private readonly Dictionary<string, AbilityLinkContext> links = new();

    public AbilityLinkController(HeroUnit owner)
    {
        this.owner = owner;
    }

    public void SetLink(string key, AbilityLinkContext context)
    {
        links[key] = context;
    }

    public bool TryGetLink(string key, out AbilityLinkContext context)
    {
        return links.TryGetValue(key, out context);
    }

    public void RemoveLink(string key)
    {
        links.Remove(key);
    }

    public void Clear()
    {
        links.Clear();
    }

    #region Snapshot
    [System.Serializable]
    public struct AbilityLinkSnapshot
    {
        public string Key;
        public int SourceAbilityId;
        public bool HasSourceUnit;
        public UnitUID SourceUnitId;
        public bool HasLinkedUnit;
        public UnitUID LinkedUnitId;
        public bool HasLinkedPosition;
        public fp3 LinkedPosition;
        public uint CreatedTick;
    }

    public object CaptureState()
    {
        var list = new List<AbilityLinkSnapshot>(links.Count);

        foreach (var kv in links)
        {
            var link = kv.Value;

            list.Add(new AbilityLinkSnapshot
            {
                Key = kv.Key,
                SourceAbilityId = link.SourceAbilityId,
                HasSourceUnit = link.SourceUnit != null,
                SourceUnitId = link.SourceUnit != null ? link.SourceUnit.UnitID : default,
                HasLinkedUnit = link.LinkedUnit != null,
                LinkedUnitId = link.LinkedUnit != null ? link.LinkedUnit.UnitID : default,
                HasLinkedPosition = link.LinkedPosition.HasValue,
                LinkedPosition = link.LinkedPosition.HasValue ? link.LinkedPosition.Value : default,
                CreatedTick = link.CreatedTick,
            });
        }

        return list.ToArray();
    }

    public void RestoreState(object state)
    {
        links.Clear();

        if (state is not AbilityLinkSnapshot[] snaps)
            return;

        for (int i = 0; i < snaps.Length; i++)
        {
            var snap = snaps[i];

            UnitCore sourceUnit = null;
            UnitCore linkedUnit = null;

            if (snap.HasSourceUnit &&
                UnitManager.Instance.Spawns.TryGetValue(snap.SourceUnitId, out var src))
            {
                sourceUnit = src;
            }

            if (snap.HasLinkedUnit &&
                UnitManager.Instance.Spawns.TryGetValue(snap.LinkedUnitId, out var linked))
            {
                linkedUnit = linked;
            }

            links[snap.Key] = new AbilityLinkContext
            {
                SourceAbilityId = snap.SourceAbilityId,
                SourceUnit = sourceUnit,
                LinkedUnit = linkedUnit,
                LinkedPosition = snap.HasLinkedPosition ? snap.LinkedPosition : null,
                UserData = null,
                CreatedTick = snap.CreatedTick,
            };
        }
    }
    #endregion
}

public sealed class AbilityLinkContext
{
    public int SourceAbilityId;
    public UnitCore SourceUnit;
    public UnitCore LinkedUnit;
    public fp3? LinkedPosition;
    public object UserData;
    public uint CreatedTick;
}