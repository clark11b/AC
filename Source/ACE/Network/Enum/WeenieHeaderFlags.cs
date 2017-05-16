﻿using System;

namespace ACE.Network.Enum
{
    [Flags]
    public enum WeenieHeaderFlag : uint
    {
        None              = 0x00000000,
        PuralName         = 0x00000001,
        ItemCapacity      = 0x00000002,
        ContainerCapacity = 0x00000004,
        Value             = 0x00000008,
        Usable            = 0x00000010,
        UseRadius         = 0x00000020,
        Monarch           = 0x00000040,
        UiEffects         = 0x00000080,
        AmmoType          = 0x00000100,
        CombatUse         = 0x00000200,
        Struture          = 0x00000400,
        MaxStructure      = 0x00000800,
        StackSize         = 0x00001000,
        MaxStackSize      = 0x00002000,
        Container         = 0x00004000,
        Wielder           = 0x00008000,
        ValidLocations    = 0x00010000,
        Location          = 0x00020000,
        Priority          = 0x00040000,
        TargetType        = 0x00080000,
        BlipColour        = 0x00100000,
        Burden            = 0x00200000,
        Spell             = 0x00400000,
        RadarBehavior             = 0x00800000,
        Workmanship       = 0x01000000,
        HouseOwner        = 0x02000000,
        HouseRestrictions = 0x04000000,
        Script            = 0x08000000,
        HookType          = 0x10000000,
        HookItemTypes     = 0x20000000,
        IconOverlay       = 0x40000000,
        Material          = 0x80000000
    }

    [Flags]
    public enum WeenieHeaderFlag2 : uint
    {
        None              = 0x00,
        IconUnderlay      = 0x01,
        Cooldown          = 0x02,
        CooldownDuration  = 0x04,
        PetOwner          = 0x08
    }
}
