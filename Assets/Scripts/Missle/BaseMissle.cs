using Sirenix.OdinInspector;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

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
    protected fp3 logicPosition;// 实际只有xz有用，但是为了方便运算就使用fp3
    public fp3 LogicPosition
    {
        get => logicPosition;
        set => logicPosition = value;
    }

    [ShowInInspector, ReadOnly, LabelText("逻辑旋转")]
    protected fp2 logicRotation;// x代表四元数的y，y代表四元数的w
    public fp2 LogicRotation
    {
        get => logicRotation;
        set => logicRotation = value;
    }

    protected fp3 logicSize;// // 实际只有xz有用，但是为了方便运算就使用fp3
    public fp3 LogicSize
    {
        get => logicSize;
        set => logicSize = value;
    }


    private void Awake()
    {
        var bound = modelRoot.GetComponentInChildren<Collider>(true).bounds;
        logicSize = new fp3((fp)bound.size.x, (fp)bound.size.y, (fp)bound.size.z);
    }

    private void LateUpdate()
    {
        SyncTransform();
    }

    protected virtual void SyncTransform()
    {
        transform.position = new Vector3((float)logicPosition.x, (float)logicPosition.y, (float)logicPosition.z);
        modelRoot.rotation = new Quaternion(0, (float)logicRotation.x, 0, (float)logicRotation.y);
    }

    public void Tick(fp dt)
    {
        UpdateMissleState(dt);

        if (CanApply())
            OnMissleApply();

        if (IsRecycle())
            MissleManager.Instance.Recycle(this);
    }

    public virtual void UpdateTransform(fp dt)// 更新位置和旋转
    {
        UpdatePosition(dt);
        UpdateRotation(dt);
        UpdateSize(dt);
    }

    protected virtual void UpdatePosition(fp dt) { }
    protected virtual void UpdateRotation(fp dt) { }
    protected virtual void UpdateSize(fp dt) { }

    protected virtual void UpdateMissleState(fp dt) { }// 更新投掷物状态

    protected abstract bool CanApply();// 判定是否调用投掷物触发事件

    protected abstract void OnMissleApply();// 投掷物触发事件

    protected abstract bool IsRecycle();// 判定是否应该被回收

    public abstract void OnSpawn(IMissleInitialData initialData);// 投掷物生成时触发;
    
    public abstract void OnDespawn();// 投掷物销毁时触发

    public virtual void OnMissleTrigger(UnitCore target) { }

    #region 快照和回滚
    

    [System.Serializable]
    public class MissleSnapshot
    {
        public fp3 position;
        public fp2 rotation;    
        public fp3 size;
        public readonly Dictionary<string, object> stateSnapshotDict = new();
    }

    public virtual object CaptureState()
    {
        return new MissleSnapshot
        {
            position = LogicPosition,
            rotation = LogicRotation,
            size = LogicSize,
        };
    }

    public virtual void RestoreState(object state)
    {
        if (state is MissleSnapshot snapshot)
        {
            LogicPosition = snapshot.position;
            LogicRotation = snapshot.rotation;
            LogicSize = snapshot.size;
        }
    }

    #endregion
}

public interface IMissleInitialData { }