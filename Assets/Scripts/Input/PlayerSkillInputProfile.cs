using System;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "PlayerSkillInputProfile", menuName = "SkillSystem/Player Skill Input Profile")]
public sealed class PlayerSkillInputProfile : ScriptableObject
{
    [Serializable]
    public struct SlotBinding
    {
        [LabelText("槽位")]
        public SkillSlot Slot;

        [LabelText("按键")]
        public KeyCode Key;
    }

    [Serializable]
    public struct GroupBinding
    {
        [LabelText("组索引")]
        public int GroupIndex;

        [LabelText("按键")]
        public KeyCode Key;
    }

    [TitleGroup("技能槽位"), LabelText("槽位绑定")]
    public SlotBinding[] SlotBindings = new SlotBinding[4]
    {
        new SlotBinding { Slot = SkillSlot.Q, Key = KeyCode.Q },
        new SlotBinding { Slot = SkillSlot.W, Key = KeyCode.W },
        new SlotBinding { Slot = SkillSlot.E, Key = KeyCode.E },
        new SlotBinding { Slot = SkillSlot.R, Key = KeyCode.R },
    };

    [TitleGroup("技能组"), LabelText("技能组直切按键")]
    public GroupBinding[] GroupBindings;

    [TitleGroup("技能组"), LabelText("下一个技能组")]
    public KeyCode NextGroupKey = KeyCode.None;

    [TitleGroup("技能组"), LabelText("上一个技能组")]
    public KeyCode PreviousGroupKey = KeyCode.None;
}
