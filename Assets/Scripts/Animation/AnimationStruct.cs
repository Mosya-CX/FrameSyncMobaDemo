
using System;
using UnityEngine;

public enum BaseAnimState : byte
{
    None,
    Idle,
    Move,
    Dash,
    Attack,
    Siffness,
    Dead,
}

public enum OverlaySlotType : byte
{
    FullBodyOverride,
    UpperBodyAction,
    BuffOverlay,
    HitReactOverlay,
}

public enum OverlayAnimPreset : byte
{
    Custom,
    FullBodyOverride_Default,
    UpperBodyCast_Default,
    BuffLoop_Default,
    HitReact_Default,
}

public enum BaseAnimOverrideType : byte
{
    Idle,
    Move,
    Dash,
    AttackSequence,
    Siffness,
    Dead,
}

[Serializable]
public struct BaseAnimOverrideEntry
{
    public bool IsValid;
    public BaseAnimOverrideType Type;
    public AnimationClip Clip;
    public AnimationClip[] Clips;
    public int Priority;
    public string Tag;
}