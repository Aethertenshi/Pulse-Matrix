using System;
using Rei2D;

namespace Matrix.CoreGame;

public static class GameplayMods
{
    // Mod States
    public static bool DoubleTime { get; set; }
    public static float DoubleTimeSpeed { get; set; } = 1.50f;
    public static bool DoubleTimeAdjustPitch { get; set; } = true;

    public static bool Nightcore { get; set; }
    public static bool Hidden { get; set; }
    public static bool HardRock { get; set; }
    public static bool Easy { get; set; }
    public static bool NoFail { get; set; }
    public static bool Auto { get; set; }

    public static event Action? OnModsChanged;

    public static void TriggerChanged() => OnModsChanged?.Invoke();

    public static float GetScoreMultiplier()
    {
        float mult = 1.0f;
        if (DoubleTime || Nightcore) mult *= 1.12f;
        if (Hidden) mult *= 1.06f;
        if (HardRock) mult *= 1.06f;
        if (Easy) mult *= 0.50f;
        if (NoFail) mult *= 0.50f;
        if (Auto) mult *= 0.00f;
        return mult;
    }

    public static void ResetAll()
    {
        DoubleTime = false;
        DoubleTimeSpeed = 1.50f;
        DoubleTimeAdjustPitch = true;
        Nightcore = false;
        Hidden = false;
        HardRock = false;
        Easy = false;
        NoFail = false;
        Auto = false;
        TriggerChanged();
    }
}
