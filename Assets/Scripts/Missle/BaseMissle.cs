using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using System;

public abstract class BaseMissle : MonoBehaviour, IStateful
{
    [SerializeField, LabelText("预制体ID")]
    protected short prefabId;
    public short PrefabID => prefabId;

    [SerializeField, LabelText("实例ID"), ReadOnly]
    protected MissleUID instanceUid;
    public MissleUID MissleUid
    {
        get => instanceUid;
        set => instanceUid = value;
    }

    [SerializeField, LabelText("模型根节点")]
    protected Transform modelRoot;

    [ShowInInspector, ReadOnly, LabelText("逻辑坐标")]
    protected fp3 logicPosition;
    public fp3 LogicPosition
    {
        get => logicPosition;
        set => logicPosition = value;
    }

    [ShowInInspector, ReadOnly, LabelText("逻辑旋转")]
    protected fp2 logicRotation;
    public fp2 LogicRotation
    {
        get => logicRotation;
        set => logicRotation = value;
    }

    [ShowInInspector, ReadOnly, LabelText("逻辑尺寸")]
    protected fp3 logicSize;
    public fp3 LogicSize
    {
        get => logicSize;
        set => logicSize = value;
    }

    protected UnitUID ownerUid;
    public UnitUID OwnerUid => ownerUid;

    protected bool shouldRecycleNow;
    public bool ShouldRecycleNow => shouldRecycleNow;

    protected virtual void Awake()
    {
        if (modelRoot != null)
        {
            var collider = modelRoot.GetComponentInChildren<Collider>(true);
            if (collider != null)
            {
                var bound = collider.bounds;
                logicSize = new fp3((fp)bound.size.x, (fp)bound.size.y, (fp)bound.size.z);
            }
        }
    }

    protected virtual void LateUpdate()
    {
        SyncTransform();
    }

    public virtual void SyncTransform()
    {
        transform.position = new Vector3((float)logicPosition.x, (float)logicPosition.y, (float)logicPosition.z);

        if (modelRoot != null)
            modelRoot.rotation = new Quaternion(0, (float)logicRotation.x, 0, (float)logicRotation.y);
    }

    public void Tick(fp dt, uint currentTick)
    {
        if (shouldRecycleNow)
            return;

        UpdateMissleState(dt, currentTick);

        if (CanApply())
            OnMissleApply();

        if (IsRecycle())
            shouldRecycleNow = true;
    }

    public virtual void UpdateTransform(fp dt, uint currentTick)
    {
        UpdatePosition(dt, currentTick);
        UpdateRotation(dt, currentTick);
        UpdateSize(dt, currentTick);
    }

    protected virtual void UpdatePosition(fp dt, uint currentTick) { }
    protected virtual void UpdateRotation(fp dt, uint currentTick) { }
    protected virtual void UpdateSize(fp dt, uint currentTick) { }

    protected virtual void UpdateMissleState(fp dt, uint currentTick) { }

    protected abstract bool CanApply();
    protected abstract void OnMissleApply();
    protected abstract bool IsRecycle();

    public abstract void OnSpawn(IMissleInitialData initialData);
    public abstract void OnDespawn();

    public virtual void OnMissleTrigger(UnitCore target) { }

    protected UnitCore ResolveOwner()
    {
        return UnitManager.Instance.Spawns.TryGetValue(ownerUid, out var owner) ? owner : null;
    }

    #region Snapshot

    [Serializable]
    public class MissleSnapshot
    {
        public short PrefabId;
        public MissleUID InstanceUid;
        public UnitUID OwnerUid;
        public fp3 Position;
        public fp2 Rotation;
        public fp3 Size;
        public bool ShouldRecycleNow;
    }

    public virtual object CaptureState()
    {
        return new MissleSnapshot
        {
            PrefabId = prefabId,
            InstanceUid = instanceUid,
            OwnerUid = ownerUid,
            Position = logicPosition,
            Rotation = logicRotation,
            Size = logicSize,
            ShouldRecycleNow = shouldRecycleNow,
        };
    }

    public virtual void RestoreState(object state)
    {
        if (state is not MissleSnapshot snapshot)
            return;

        prefabId = snapshot.PrefabId;
        instanceUid = snapshot.InstanceUid;
        ownerUid = snapshot.OwnerUid;
        logicPosition = snapshot.Position;
        logicRotation = snapshot.Rotation;
        logicSize = snapshot.Size;
        shouldRecycleNow = snapshot.ShouldRecycleNow;
    }

    #endregion
}

public interface IMissleInitialData
{
    UnitUID OwnerUid { get; }
}