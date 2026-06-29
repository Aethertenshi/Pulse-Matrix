using System;
using Matrix.Modules;

namespace Matrix.CoreGame;

public class WaypointTile
{
    public float WorldX { get; set; }
    public float WorldY { get; set; }
    public ArrowDirection Direction { get; set; }
    public double HitTimeMs { get; set; }
    public int ComboNumber { get; set; }
    public bool IsHold { get; set; }
    public double HoldDurationMs { get; set; }
    public int Repeats { get; set; } = 1;
    public float EndWorldX { get; set; }
    public float EndWorldY { get; set; }
    public bool IsHit { get; set; }
    public bool IsMissed { get; set; }

    public WaypointTile(float worldX, float worldY, ArrowDirection direction, double hitTimeMs, int comboNumber = 0, bool isHold = false, double holdDurationMs = 0.0, float endWorldX = 0f, float endWorldY = 0f, int repeats = 1)
    {
        WorldX = worldX;
        WorldY = worldY;
        Direction = direction;
        HitTimeMs = hitTimeMs;
        ComboNumber = comboNumber;
        IsHold = isHold;
        HoldDurationMs = holdDurationMs;
        Repeats = Math.Max(1, repeats);
        EndWorldX = isHold ? endWorldX : worldX;
        EndWorldY = isHold ? endWorldY : worldY;
    }
}
