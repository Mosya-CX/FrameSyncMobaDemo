using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HeroSelectInfoTable", menuName = "英雄选择信息表")]
public class HeroSelectInfoTable : ScriptableObject
{
    [SerializeField]
    public List<HeroSelectInfo> heroSelectInfos = new List<HeroSelectInfo>();
}

[Serializable]
public struct HeroSelectInfo
{
    public Sprite Head;
    public string Name;
    public int prefabId;
}