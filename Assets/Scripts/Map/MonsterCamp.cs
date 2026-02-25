using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class MonsterCamp : MonoBehaviour, IStateful
{
    [SerializeField, ReadOnly]
    private fp3 logicPosition;
    public fp3 LogicPosition => logicPosition;

    private void Start()
    {
        logicPosition = new fp3((fp)transform.position.x, (fp)transform.position.y, (fp)transform.position.z);
    }

    public object CaptureState()
    {
        throw new System.NotImplementedException();
    }

    public void RestoreState(object state)
    {
        throw new System.NotImplementedException();
    }
}
