using System;

[Flags]
public enum ControlType
{
    None = 0,
    Silence = 1 << 0,
    Disarm = 1 << 1,
    Root = 1 << 2,
    Stun = 1 << 3,
    Suppress = 1 << 4,
    Knockup = 1 << 5,
    Fear = 1 << 6,
    Taunt = 1 << 7,
    Charm = 1 << 8,
    Slow = 1 << 9,
}