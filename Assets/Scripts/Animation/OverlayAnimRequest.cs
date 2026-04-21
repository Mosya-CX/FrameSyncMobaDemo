using UnityEngine;

[System.Serializable]
public struct OverlayAnimRequest
{
    public AnimationClipRef ClipRef;
    public AvatarMask Mask;

    public OverlayAnimPreset Preset;

    public float Weight;
    public float FadeIn;
    public float FadeOut;
    public float Speed;

    public bool Loop;
    public bool Additive;
    public bool AutoStop;

    public int Priority;
    public string Tag;
}