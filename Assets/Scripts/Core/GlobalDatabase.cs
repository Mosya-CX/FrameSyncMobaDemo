using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "GlobalDatabase", menuName = "全局数据库")]
public class GlobalDatabase : ScriptableObject
{
    public SerializedDictionary<int, BuffData> BuffDatabase;
    public SerializedDictionary<int, CrowdControlData> ControlDatabase;
    public SerializedDictionary<int, EquipmentData> EquipmentDatabase;
}
