using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBuffData", menuName = "Buff系统/新建Buff配置文件")]
public class BuffData : ScriptableObject
{
    [Title("基础信息")]
    public int Id;
    public string Name;
    public string Description;
    public Sprite Icon;
    public int Priority;
    public bool isStackable;
    public int MaxStack;
    public string[] Tags;
    [Title("时间信息")]
    public bool isForever;
    public float Duration;
    public float TickTime;
    [Title("更新方式")]
    public BuffUpdateTimeEnum UpdateTimeMode;
    public BuffRemoveUpdateEnum RemoveUpdateMode;
    [Title("基础回调点")]
    public BuffBaseModule OnCreate;
    public BuffBaseModule OnRemove;
    public BuffBaseModule OnTick;
    [Title("伤害回调点")]
    public BuffBaseModule OnHit;// 当 攻击 时
    public BuffBaseModule OnHurt;// 当 受伤 时
    public BuffBaseModule OnKill;// 当 击杀 时
    public BuffBaseModule OnDeath;// 当 死亡 时
}


public enum BuffUpdateTimeEnum
{
    Add,
    Replace,
    Keep,
}

public enum BuffRemoveUpdateEnum
{
    Clear,
    Consume,
}