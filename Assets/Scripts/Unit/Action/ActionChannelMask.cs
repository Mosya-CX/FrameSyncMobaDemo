using System;

[Flags]
public enum ActionChannelMask
{
    None = 0,
    Move = 1 << 0,
    Rotate = 1 << 1,
    Attack = 1 << 2,
    Cast = 1 << 3,
    Dash = 1 << 4,
    Track = 1 << 5,

    All = Move | Rotate | Attack | Cast | Dash | Track,
}