using System;
using UnityEngine;

[RequireComponent(typeof(HeroUnit))]
public sealed class SkillGroupController : MonoBehaviour, IStateful
{
    [SerializeField]
    private SkillLoadoutDefinition loadoutDefinition;

    private HeroUnit owner;
    private int currentGroupIndex;

    public event Action<int, int> CurrentGroupChanged;

    public HeroUnit Owner => owner;
    public SkillLoadoutDefinition LoadoutDefinition => loadoutDefinition;
    public int CurrentGroupIndex => currentGroupIndex;

    private void Awake()
    {
        owner = GetComponent<HeroUnit>();
        currentGroupIndex = loadoutDefinition != null ? Mathf.Clamp(loadoutDefinition.InitialGroupIndex, 0, Mathf.Max(0, GetGroupCount() - 1)) : 0;
    }

    public int GetGroupCount()
    {
        return loadoutDefinition != null && loadoutDefinition.Groups != null
            ? loadoutDefinition.Groups.Length
            : 0;
    }

    public bool TryGetCurrentGroup(out SkillGroupDefinition group)
    {
        return TryGetGroup(currentGroupIndex, out group);
    }

    public bool TryGetGroup(int groupIndex, out SkillGroupDefinition group)
    {
        group = null;

        if (loadoutDefinition == null || loadoutDefinition.Groups == null)
            return false;

        if (groupIndex < 0 || groupIndex >= loadoutDefinition.Groups.Length)
            return false;

        group = loadoutDefinition.Groups[groupIndex];
        return group != null;
    }

    public bool TryGetSkillAtSlot(SkillSlot slot, out SkillDef skill)
    {
        return TryGetSkillAtSlot((int)slot, out skill);
    }

    public bool TryGetSkillAtSlot(int slotIndex, out SkillDef skill)
    {
        skill = null;

        if (!TryGetCurrentGroup(out var group) || group.Slots == null)
            return false;

        if (slotIndex < 0 || slotIndex >= group.Slots.Length)
            return false;

        skill = group.Slots[slotIndex];
        return skill != null;
    }

    public bool TryGetSkillAt(int groupIndex, int slotIndex, out SkillDef skill)
    {
        skill = null;

        if (!TryGetGroup(groupIndex, out var group) || group.Slots == null)
            return false;

        if (slotIndex < 0 || slotIndex >= group.Slots.Length)
            return false;

        skill = group.Slots[slotIndex];
        return skill != null;
    }

    public bool SwitchToGroup(int groupIndex)
    {
        int count = GetGroupCount();
        if (count <= 0 || groupIndex < 0 || groupIndex >= count)
            return false;

        if (currentGroupIndex == groupIndex)
            return true;

        int previous = currentGroupIndex;
        currentGroupIndex = groupIndex;
        CurrentGroupChanged?.Invoke(previous, currentGroupIndex);
        return true;
    }

    public bool SwitchNextGroup()
    {
        int count = GetGroupCount();
        if (count <= 0)
            return false;

        return SwitchToGroup((currentGroupIndex + 1) % count);
    }

    public bool SwitchPreviousGroup()
    {
        int count = GetGroupCount();
        if (count <= 0)
            return false;

        return SwitchToGroup((currentGroupIndex - 1 + count) % count);
    }

    public object CaptureState()
    {
        return currentGroupIndex;
    }

    public void RestoreState(object state)
    {
        currentGroupIndex = state is int value ? value : 0;
    }
}
