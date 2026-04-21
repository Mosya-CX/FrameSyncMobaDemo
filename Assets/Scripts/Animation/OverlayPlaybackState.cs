using System;
using UnityEngine;

[Serializable]
public struct OverlayPlaybackState
{
    public bool IsPlaying;

    public OverlaySlotType SlotType;
    public OverlayAnimPreset Preset;

    public int ClipRefId;
    public uint StartTick;

    public bool Additive;
    public bool Loop;
    public bool AutoStop;

    public float Speed;
    public float Weight;
    public float FadeIn;
    public float FadeOut;
}