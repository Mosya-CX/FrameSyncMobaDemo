using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class HealManager : MonoSingleton<HealManager>, IStateful
{
    private readonly Queue<HealRequest> healRequestQueue = new();

    [Serializable]
    public class HealManagerSnapshot
    {
        public List<HealRequestSnapshot> Requests = new();
    }

    [Serializable]
    public class HealRequestSnapshot
    {
        public UnitUID SourceUid;
        public UnitUID TargetUid;
        public HealSourceKind SourceKind;
        public fp BaseHeal;
        public List<string> Tags = new();
    }

    public void Clean()
    {
        healRequestQueue.Clear();
    }

    public void Tick(uint currentTick)
    {
        while (healRequestQueue.Count > 0)
        {
            var request = healRequestQueue.Dequeue();
            ProcessHealRequest(request, currentTick);
        }
    }

    public void CreateHealRequest(UnitCore source, UnitCore target, HealSourceKind sourceKind, fp baseHeal, string[] additionalTags = null)
    {
        var request = new HealRequest
        {
            SourceUid = source.UnitID,
            TargetUid = target.UnitID,
            SourceKind = sourceKind,
            BaseHeal = baseHeal,
        };

        if (additionalTags != null)
            for (int i = 0; i < additionalTags.Length; i++)
                request.Tags.Add(additionalTags[i]);

        healRequestQueue.Enqueue(request);
    }

    private void ProcessHealRequest(HealRequest request, uint currentTick)
    {
        if (!UnitManager.Instance.Spawns.TryGetValue(request.SourceUid, out var source))
            return;
        if (!UnitManager.Instance.Spawns.TryGetValue(request.TargetUid, out var target))
            return;
        if (target.IsDead)
            return;

        var context = new HealContext
        {
            Source = source,
            Target = target,
            SourceKind = request.SourceKind,
            BaseHeal = request.BaseHeal,
            Tags = new HashSet<string>(request.Tags),
            Extra = request.Extra,
        };

        source.ModifyOutgoingHeal(context);
        target.ModifyIncomingHeal(context);

        fp finalHeal = (context.BaseHeal + context.BonusHeal) * context.HealMultiplier;
        if (finalHeal < 0)
            finalHeal = 0;

        var result = new HealResult(source, target, finalHeal, new List<string>(context.Tags));

        target.ApplyHealResult(result, currentTick);
        source.OnHealDealt(result);
        target.OnHealTaken(result);
    }

    public object CaptureState()
    {
        var snap = new HealManagerSnapshot();

        foreach (var req in healRequestQueue)
        {
            snap.Requests.Add(new HealRequestSnapshot
            {
                SourceUid = req.SourceUid,
                TargetUid = req.TargetUid,
                SourceKind = req.SourceKind,
                BaseHeal = req.BaseHeal,
                Tags = new List<string>(req.Tags),
            });
        }

        return snap;
    }

    public void RestoreState(object state)
    {
        healRequestQueue.Clear();

        if (state is not HealManagerSnapshot snap)
            return;

        for (int i = 0; i < snap.Requests.Count; i++)
        {
            var item = snap.Requests[i];
            var req = new HealRequest
            {
                SourceUid = item.SourceUid,
                TargetUid = item.TargetUid,
                SourceKind = item.SourceKind,
                BaseHeal = item.BaseHeal,
            };

            for (int j = 0; j < item.Tags.Count; j++)
                req.Tags.Add(item.Tags[j]);

            healRequestQueue.Enqueue(req);
        }
    }
}