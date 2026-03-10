using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class UnitBaseHandler : MonoBehaviour, IStateful
{
    protected UnitCore owner;
    public UnitCore Owner => owner;

    protected virtual void Awake()
    {
        owner ??= GetComponent<UnitCore>();
    }

    public abstract void Tick(fp deltaTime);

    #region ÉËº¦»Øµ÷
    public void OnDamageCallback(UnitDamageCallbackType type, in DamageInfo info)
    {
        switch (type)
        {
            case UnitDamageCallbackType.OnDamageDealt:
                OnDamageDealt(info); 
                break;
            case UnitDamageCallbackType.OnDamageTaken:
                OnDamageTaken(info); 
                break;
            case UnitDamageCallbackType.OnKill:
                OnKill(info);
                break;
            case UnitDamageCallbackType.OnDeath:
                OnDeath(info);
                break;
        }
    }
    protected abstract void OnDamageDealt(in DamageInfo info);
    protected abstract void OnDamageTaken(in DamageInfo info);
    protected abstract void OnKill(in DamageInfo info);
    protected abstract void OnDeath(in DamageInfo info);

    #endregion

    public abstract object CaptureState();
    public abstract void RestoreState(object state);
}
