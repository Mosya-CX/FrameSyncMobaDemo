using UnityEngine;

[CreateAssetMenu(fileName = "BuffData", menuName = "Buff/BuffData")]
public class BuffData : ScriptableObject
{
    public int Id;
    public string BuffName;
    public float Duration = 0f;
    public bool isForever = false;
    public float TickTime = 0f;

    public BuffUpdateTimeEnum UpdateTimeMode = BuffUpdateTimeEnum.Replace;
    public bool isStackable = false;
    public int MaxStack = 1;

    [Header("Lifecycle")]
    public BuffBaseModule OnCreate;
    public BuffBaseModule OnRemove;
    public BuffBaseModule OnTick;
}
