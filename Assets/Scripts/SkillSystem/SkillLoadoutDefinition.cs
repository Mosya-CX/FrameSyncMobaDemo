using System;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "SkillLoadoutDefinition", menuName = "SkillSystem/Skill Loadout Definition")]
public sealed class SkillLoadoutDefinition : ScriptableObject
{
    [TitleGroup("基础"), LabelText("初始技能组索引")]
    public int InitialGroupIndex = 0;

    [TitleGroup("技能组"), LabelText("技能组列表")]
    [ListDrawerSettings(Expanded = true)]
    public SkillGroupDefinition[] Groups;
}

[Serializable]
public sealed class SkillGroupDefinition
{
    [LabelText("组名")]
    public string GroupName;

    [LabelText("槽位技能")]
    [TableList(AlwaysExpanded = true)]
    public SkillDef[] Slots = new SkillDef[4];
}
