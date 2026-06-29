using System;
using System.IO;
using System.Collections.Generic;
using Rei2D;
using Rei2D.Rendering;
using Rei2D.Input;
using Rei2D.Audio;
using OsuLib;
using OsuLib.Models;
using Matrix.Modules;

namespace Matrix.CoreGame;

public class DanceModeElement : Element
{
    public static DanceModeElement? Instance;
    public static float HitWindowMs { get; set; } = 200f;

    public List<WaypointTile> Waypoints { get; } = new();
    public bool IsActive { get; set; }
    public bool IsDead { get; private set; }

    private int _currentIndex = 0;
    private float _charWorldX;
    private float _charWorldY;
    private float _cameraWorldX;
    private float _cameraWorldY;
    private float _failTimer = 0f;
    private float _leadInTimer = 0f;
    private double _virtualSongTimeMs = 0;

    public struct HitShockwave
    {
        public float WorldX, WorldY;
        public float Timer;
    }

    public struct TrailPoint
    {
        public float WorldX, WorldY;
        public float Life;
    }

    public struct HitJudgement
    {
        public float WorldX, WorldY;
        public string Text;
        public Color Color;
        public float Timer;
    }

    private readonly List<HitShockwave> _shockwaves = new();
    private readonly List<TrailPoint> _trailPoints = new();
    private readonly List<HitJudgement> _judgements = new();
    private float _trailTimer = 0f;

    private readonly Track[] _hitsoundPool = new Track[8];
    private int _hitsoundIdx = 0;

    public DanceModeElement()
    {
        Instance = this;
        Size = Size2D.Full;
        Position = new Position2D(0f, 0f);
        Anchor = Anchor2D.TopLeft;
        InterceptsMouse = false;

        // Load 8-track audio pool for zero-latency polyphonic hitsounds
        try
        {
            string hsPath = File.Exists("include/hitsounds/normal-hitnormal.wav")
                ? "include/hitsounds/normal-hitnormal.wav"
                : "include/hitsounds/soft-hitnormal.wav";
            for (int i = 0; i < _hitsoundPool.Length; i++)
            {
                _hitsoundPool[i] = Audio.Load(hsPath);
                _hitsoundPool[i].Volume = 0.65f;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DanceMode] Hitsound loading info: {ex.Message}");
        }

        // Register edge-triggered input callbacks for rhythm directions
        Input.KeyClicked(Keys.W, () => OnDirectionInput(ArrowDirection.Up));
        Input.KeyClicked(Keys.Up, () => OnDirectionInput(ArrowDirection.Up));

        Input.KeyClicked(Keys.A, () => OnDirectionInput(ArrowDirection.Left));
        Input.KeyClicked(Keys.Left, () => OnDirectionInput(ArrowDirection.Left));

        Input.KeyClicked(Keys.S, () => OnDirectionInput(ArrowDirection.Down));
        Input.KeyClicked(Keys.Down, () => OnDirectionInput(ArrowDirection.Down));

        Input.KeyClicked(Keys.D, () => OnDirectionInput(ArrowDirection.Right));
        Input.KeyClicked(Keys.Right, () => OnDirectionInput(ArrowDirection.Right));

        // Track Space key for Golden Hold Notes
        Input.KeyClicked(Keys.Space, () => { });

        // Exit/Cancel Gameplay Key Listeners
        Input.KeyClicked(Keys.LeftShift, () => { if (IsActive) ReturnToMenu(); });
        Input.KeyClicked(Keys.RightShift, () => { if (IsActive) ReturnToMenu(); });
        Input.KeyClicked(Keys.Escape, () => { if (IsActive) ReturnToMenu(); });
    }

    public void Initialize(OsuBeatmap beatmap)
    {
        Waypoints.Clear();
        _shockwaves.Clear();
        _trailPoints.Clear();
        _judgements.Clear();
        _currentIndex = 0;
        IsDead = false;
        _failTimer = 0f;

        if (beatmap == null) return;

        OsuBeatmap fullBeatmap = beatmap;
        if (fullBeatmap.HitObjects.Count == 0 && !string.IsNullOrEmpty(fullBeatmap.FilePath) && File.Exists(fullBeatmap.FilePath))
        {
            try
            {
                fullBeatmap = new OsuParser().Parse(fullBeatmap.FilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DanceMode] Failed to parse beatmap for gameplay: {ex.Message}");
            }
        }

        if (fullBeatmap == null || fullBeatmap.HitObjects.Count == 0)
        {
            Console.WriteLine("[DanceMode] Error: Beatmap contains 0 hit objects!");
            return;
        }

        Console.WriteLine($"[DanceMode] Loaded {fullBeatmap.HitObjects.Count} hit objects for gameplay!");

        float lastOsuX = 256f;
        float lastOsuY = 192f;
        float currWorldX = 0f;
        float currWorldY = 0f;

        // Origin starting tile
        Waypoints.Add(new WaypointTile(0f, 0f, ArrowDirection.Right, 0.0));

        int currentCombo = 1;
        ArrowDirection lastDir = ArrowDirection.Right;

        for (int hIndex = 0; hIndex < fullBeatmap.HitObjects.Count; hIndex++)
        {
            var obj = fullBeatmap.HitObjects[hIndex];
            var newComboProp = obj.GetType().GetProperty("NewCombo") ?? obj.GetType().GetProperty("IsNewCombo");
            if (newComboProp != null && newComboProp.GetValue(obj) is bool isNewCombo && isNewCombo)
            {
                currentCombo = 1;
            }

            float dx = obj.X - lastOsuX;
            float dy = obj.Y - lastOsuY;

            // Dynamic Rhythmic Spacing based on Time Delta (Δt)
            double lastTimeMs = Waypoints.Count > 0 ? Waypoints[Waypoints.Count - 1].HitTimeMs : 0.0;
            double deltaTime = obj.Time - lastTimeMs;
            if (deltaTime < 10.0) deltaTime = 200.0; // handle stacked notes

            ArrowDirection rawDir = (MathF.Abs(dx) < 0.01f && MathF.Abs(dy) < 0.01f)
                ? lastDir
                : DirectionHelper.FromVector(dx, dy);

            ArrowDirection dir = rawDir;

            // Flow Momentum Filter: Prevent awkward immediate 180° u-turns during fast/medium streams
            bool isOpposite = (lastDir == ArrowDirection.Right && rawDir == ArrowDirection.Left) ||
                              (lastDir == ArrowDirection.Left && rawDir == ArrowDirection.Right) ||
                              (lastDir == ArrowDirection.Up && rawDir == ArrowDirection.Down) ||
                              (lastDir == ArrowDirection.Down && rawDir == ArrowDirection.Up);

            if (isOpposite && deltaTime < 340.0)
            {
                // Smooth 180° u-turn into a fluid 90° curve!
                if (lastDir == ArrowDirection.Right || lastDir == ArrowDirection.Left)
                {
                    dir = (dy >= 0f) ? ArrowDirection.Down : ArrowDirection.Up;
                }
                else
                {
                    dir = (dx >= 0f) ? ArrowDirection.Right : ArrowDirection.Left;
                }
            }

            lastDir = dir;

            float dynamicSpacing = Math.Clamp((float)(100.0 + deltaTime * 0.45), 110f, 320f);

            switch (dir)
            {
                case ArrowDirection.Right: currWorldX += dynamicSpacing; break;
                case ArrowDirection.Left:  currWorldX -= dynamicSpacing; break;
                case ArrowDirection.Up:    currWorldY -= dynamicSpacing; break; // Y-down screen coordinates
                case ArrowDirection.Down:  currWorldY += dynamicSpacing; break;
            }

            bool isHold = false;
            double holdDur = 0.0;
            var endTimeProp = obj.GetType().GetProperty("EndTime");
            if (endTimeProp != null && endTimeProp.GetValue(obj) is double endTime && endTime > obj.Time + 80.0)
            {
                isHold = true;
                holdDur = endTime - obj.Time;
            }
            else if (obj.GetType().Name.Contains("Slider") || obj.GetType().Name.Contains("Hold"))
            {
                isHold = true;
            }

            if (isHold)
            {
                // Calculate rhythmically aligned hold duration matching beatmap tempo & next note gap
                double gapToNext = (hIndex < fullBeatmap.HitObjects.Count - 1)
                    ? (fullBeatmap.HitObjects[hIndex + 1].Time - obj.Time)
                    : 400.0;

                if (holdDur < 50.0)
                {
                    holdDur = Math.Clamp(gapToNext * 0.75, 160.0, 1200.0);
                }
                else
                {
                    holdDur = Math.Min(holdDur, gapToNext * 0.90);
                }
            }

            float endWorldX = currWorldX;
            float endWorldY = currWorldY;
            if (isHold)
            {
                // Physical trail length dynamically scales with Slider Velocity (SV) & rhythm speed
                float holdLength = Math.Clamp((float)(90.0 + holdDur * 0.45), 110f, 320f);
                switch (dir)
                {
                    case ArrowDirection.Right: endWorldX += holdLength; break;
                    case ArrowDirection.Left:  endWorldX -= holdLength; break;
                    case ArrowDirection.Up:    endWorldY -= holdLength; break;
                    case ArrowDirection.Down:  endWorldY += holdLength; break;
                }
            }

            int repeats = 1;
            var slidesProp = obj.GetType().GetProperty("Slides") ?? obj.GetType().GetProperty("RepeatCount") ?? obj.GetType().GetProperty("Repeats");
            if (slidesProp != null && slidesProp.GetValue(obj) is int slidesVal && slidesVal > 1)
            {
                repeats = slidesVal;
            }

            Waypoints.Add(new WaypointTile(currWorldX, currWorldY, dir, obj.Time, currentCombo, isHold, holdDur, endWorldX, endWorldY, repeats));
            currentCombo++;

            if (isHold)
            {
                if (repeats % 2 == 1)
                {
                    currWorldX = endWorldX;
                    currWorldY = endWorldY;
                }
            }

            lastOsuX = obj.X;
            lastOsuY = obj.Y;
        }

        _charWorldX = Waypoints[0].WorldX;
        _charWorldY = Waypoints[0].WorldY;
        _cameraWorldX = _charWorldX;
        _cameraWorldY = _charWorldY;

        double firstNoteTimeMs = fullBeatmap.HitObjects[0].Time;
        if (firstNoteTimeMs < 2000.0)
        {
            _leadInTimer = (float)((2000.0 - firstNoteTimeMs) / 1000.0);
            Console.WriteLine($"[DanceMode] First note starts early ({firstNoteTimeMs:F1}ms). Adding {_leadInTimer * 1000.0:F1}ms lead-in buffer!");
        }

        if (Program.currentTrack != null)
        {
            float speed = GameplayMods.DoubleTime ? GameplayMods.DoubleTimeSpeed : (GameplayMods.Nightcore ? 1.50f : 1.0f);
            Program.currentTrack.PlaybackSpeed = speed;
            Program.currentTrack.Position = 0f;
            if (_leadInTimer <= 0f)
            {
                Program.currentTrack.Play();
            }
            else
            {
                Program.currentTrack.Pause();
            }
        }

        IsActive = true;
    }

    private void SpawnHitEffects(float wx, float wy, double errorMs)
    {
        _shockwaves.Add(new HitShockwave { WorldX = wx, WorldY = wy, Timer = 0f });

        var hsTrack = _hitsoundPool[_hitsoundIdx];
        if (hsTrack != null && !hsTrack.IsDisposed)
        {
            hsTrack.Position = 0f;
            hsTrack.Play();
        }
        _hitsoundIdx = (_hitsoundIdx + 1) % _hitsoundPool.Length;

        string text = errorMs <= 45.0 ? "PERFECT!" : (errorMs <= 100.0 ? "GREAT!" : "GOOD!");
        Color color = errorMs <= 45.0 ? new Color(0, 240, 255, 255) : (errorMs <= 100.0 ? new Color(100, 255, 120, 255) : new Color(255, 220, 80, 255));
        if (GameplayMods.Auto)
        {
            text = "PERFECT!";
            color = new Color(0, 240, 255, 255);
        }
        _judgements.Add(new HitJudgement { WorldX = wx, WorldY = wy, Text = text, Color = color, Timer = 0f });
    }

    private void OnDirectionInput(ArrowDirection pressedDir)
    {
        if (!IsActive || IsDead || Waypoints.Count == 0 || GameplayMods.Auto) return;

        if (_currentIndex < Waypoints.Count - 1)
        {
            var nextTile = Waypoints[_currentIndex + 1];
            double songTimeMs = _virtualSongTimeMs;
            double diff = nextTile.HitTimeMs - songTimeMs;
            double absError = Math.Abs(diff);

            float effectiveHitWindow = GameplayMods.HardRock ? 70f : (GameplayMods.Easy ? 180f : HitWindowMs);

            // 1. Too Early Discard (Clicked > 300ms in advance): Ignore input & spawn "NOT YET!"
            if (diff > 300.0)
            {
                _judgements.Add(new HitJudgement
                {
                    WorldX = nextTile.WorldX,
                    WorldY = nextTile.WorldY,
                    Text = "NOT YET!",
                    Color = new Color(180, 180, 190, 220),
                    Timer = 0f
                });
                return;
            }

            // 2. Ignore wrong direction key input without penalty
            if (pressedDir != nextTile.Direction)
            {
                return;
            }

            // 3. Timing Verification (Outside hit window)
            if (absError > effectiveHitWindow)
            {
                TriggerDeath("OFF-BEAT TIMING");
                return;
            }

            // 4. Perfect / Good Hit!
            nextTile.IsHit = true;
            _currentIndex++;
            SpawnHitEffects(nextTile.WorldX, nextTile.WorldY, absError);
        }
    }

    protected override void OnUpdateCore(float dt)
    {
        if (!IsActive || Waypoints.Count == 0) return;

        if (IsDead)
        {
            _failTimer += dt;
            if (_failTimer >= 0.8f)
            {
                ReturnToMenu();
            }
            return;
        }

        if (_leadInTimer > 0f)
        {
            _leadInTimer -= dt;
            if (_leadInTimer <= 0f)
            {
                _leadInTimer = 0f;
                Program.currentTrack?.Position = 0f;
                Program.currentTrack?.Play();
            }
            else
            {
                Program.currentTrack?.Pause();
            }
            _virtualSongTimeMs = -_leadInTimer * 1000.0;
        }
        else
        {
            _virtualSongTimeMs = (Program.currentTrack?.Position ?? 0f) * 1000.0;
        }

        double songTimeMs = _virtualSongTimeMs;

        // Autoplay Mod Simulation
        if (GameplayMods.Auto && _currentIndex < Waypoints.Count - 1 && !IsDead)
        {
            var nextTile = Waypoints[_currentIndex + 1];
            if (songTimeMs >= nextTile.HitTimeMs)
            {
                nextTile.IsHit = true;
                _currentIndex++;
                SpawnHitEffects(nextTile.WorldX, nextTile.WorldY, 0.0);
            }
        }

        // Automatic Miss Detection (Passed timing window without hit)
        if (!GameplayMods.Auto && _currentIndex < Waypoints.Count - 1)
        {
            float effectiveHitWindow = GameplayMods.HardRock ? 70f : (GameplayMods.Easy ? 180f : HitWindowMs);
            var nextTile = Waypoints[_currentIndex + 1];
            if (songTimeMs > nextTile.HitTimeMs + effectiveHitWindow + 20.0 && !nextTile.IsHit)
            {
                TriggerDeath("MISSED NOTE TIMING");
                return;
            }
        }

        // Smooth Character Movement & Hold Note Directional Sliding
        var currentTile = Waypoints[_currentIndex];
        float moveSmooth = 1f - MathF.Exp(-18f * dt);

        if (currentTile.IsHold && currentTile.IsHit)
        {
            double timeSinceHit = songTimeMs - currentTile.HitTimeMs;
            if (timeSinceHit >= 0 && timeSinceHit <= currentTile.HoldDurationMs)
            {
                bool isHoldingDir = GameplayMods.Auto || IsDirectionKeyDown(currentTile.Direction);
                if (isHoldingDir)
                {
                    // Oscillating ping-pong sliding for repeat sliders (Repeats >= 1)
                    double passDur = currentTile.HoldDurationMs / Math.Max(1, currentTile.Repeats);
                    int currentPass = Math.Clamp((int)(timeSinceHit / passDur), 0, currentTile.Repeats - 1);
                    double passTime = timeSinceHit - currentPass * passDur;
                    float passProgress = Math.Clamp((float)(passTime / passDur), 0f, 1f);

                    float startX = (currentPass % 2 == 0) ? currentTile.WorldX : currentTile.EndWorldX;
                    float startY = (currentPass % 2 == 0) ? currentTile.WorldY : currentTile.EndWorldY;
                    float destX  = (currentPass % 2 == 0) ? currentTile.EndWorldX : currentTile.WorldX;
                    float destY  = (currentPass % 2 == 0) ? currentTile.EndWorldY : currentTile.WorldY;

                    float targetX = startX + (destX - startX) * passProgress;
                    float targetY = startY + (destY - startY) * passProgress;
                    _charWorldX += (targetX - _charWorldX) * moveSmooth;
                    _charWorldY += (targetY - _charWorldY) * moveSmooth;
                }
                else
                {
                    // Released early without miss: teleport character to final destination of the hold note!
                    float finalX = (currentTile.Repeats % 2 == 1) ? currentTile.EndWorldX : currentTile.WorldX;
                    float finalY = (currentTile.Repeats % 2 == 1) ? currentTile.EndWorldY : currentTile.WorldY;
                    _charWorldX = finalX;
                    _charWorldY = finalY;
                }
            }
            else if (timeSinceHit > currentTile.HoldDurationMs)
            {
                float finalX = (currentTile.Repeats % 2 == 1) ? currentTile.EndWorldX : currentTile.WorldX;
                float finalY = (currentTile.Repeats % 2 == 1) ? currentTile.EndWorldY : currentTile.WorldY;
                _charWorldX += (finalX - _charWorldX) * moveSmooth;
                _charWorldY += (finalY - _charWorldY) * moveSmooth;
            }
            else
            {
                _charWorldX += (currentTile.WorldX - _charWorldX) * moveSmooth;
                _charWorldY += (currentTile.WorldY - _charWorldY) * moveSmooth;
            }
        }
        else
        {
            _charWorldX += (currentTile.WorldX - _charWorldX) * moveSmooth;
            _charWorldY += (currentTile.WorldY - _charWorldY) * moveSmooth;
        }

        // Update Character Motion Ghosting Trail
        _trailTimer += dt;
        if (_trailTimer >= 0.035f)
        {
            _trailTimer = 0f;
            _trailPoints.Add(new TrailPoint { WorldX = _charWorldX, WorldY = _charWorldY, Life = 0.25f });
        }
        for (int i = _trailPoints.Count - 1; i >= 0; i--)
        {
            var tp = _trailPoints[i];
            tp.Life -= dt;
            if (tp.Life <= 0f) _trailPoints.RemoveAt(i);
            else _trailPoints[i] = tp;
        }

        // Update Hit Shockwaves
        for (int i = _shockwaves.Count - 1; i >= 0; i--)
        {
            var sw = _shockwaves[i];
            sw.Timer += dt;
            if (sw.Timer >= 0.30f) _shockwaves.RemoveAt(i);
            else _shockwaves[i] = sw;
        }

        // Update Floating Hit Judgements
        for (int i = _judgements.Count - 1; i >= 0; i--)
        {
            var j = _judgements[i];
            j.Timer += dt;
            if (j.Timer >= 0.45f) _judgements.RemoveAt(i);
            else _judgements[i] = j;
        }

        // Smooth Dynamic Look-Ahead Camera (ADOFAI Style)
        float targetCamX = _charWorldX;
        float targetCamY = _charWorldY;
        if (_currentIndex < Waypoints.Count - 1)
        {
            var lookAheadTile = Waypoints[_currentIndex + 1];
            targetCamX = _charWorldX * 0.6f + lookAheadTile.WorldX * 0.4f;
            targetCamY = _charWorldY * 0.6f + lookAheadTile.WorldY * 0.4f;
        }

        float camSmooth = 1f - MathF.Exp(-4f * dt);
        _cameraWorldX += (targetCamX - _cameraWorldX) * camSmooth;
        _cameraWorldY += (targetCamY - _cameraWorldY) * camSmooth;
    }

    private void TriggerDeath(string reason)
    {
        if (GameplayMods.NoFail) return;
        IsDead = true;
        Console.WriteLine($"[SUDDEN DEATH] Game Over! Reason: {reason}");
        Program.currentTrack?.Pause();
    }

    private bool IsDirectionKeyDown(ArrowDirection dir)
    {
        return dir switch
        {
            ArrowDirection.Up => Input.IsKeyDown(Keys.W) || Input.IsKeyDown(Keys.Up),
            ArrowDirection.Left => Input.IsKeyDown(Keys.A) || Input.IsKeyDown(Keys.Left),
            ArrowDirection.Down => Input.IsKeyDown(Keys.S) || Input.IsKeyDown(Keys.Down),
            ArrowDirection.Right => Input.IsKeyDown(Keys.D) || Input.IsKeyDown(Keys.Right),
            _ => false
        };
    }

    public void ReturnToMenu()
    {
        if (Program.currentTrack != null)
        {
            Program.currentTrack.PlaybackSpeed = 1.0f;
        }
        IsActive = false;
        IsDead = false;
        _currentIndex = 0;
        _failTimer = 0f;
        _leadInTimer = 0f;
        Waypoints.Clear();

        Program.ReturnToMenuFromGameplay();
    }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (!IsActive || SkipDraw || !Visible || Waypoints.Count == 0) return;

        // Real-time Hardware Backdrop Blur respecting user Blur Quality setting!
        if (App.BlurQuality != BlurQuality.MaxPerformance)
        {
            items.Add(new BackdropBlurRenderItem(new Rect(0, 0, App.Width, App.Height), 1.0f));
        }

        double songTimeMs = _virtualSongTimeMs;

        items.Add(new DanceModeRenderItem(
            Waypoints,
            _currentIndex,
            _charWorldX,
            _charWorldY,
            _cameraWorldX,
            _cameraWorldY,
            songTimeMs,
            Program.ActiveAccent,
            IsDead,
            _failTimer,
            _trailPoints.ToArray(),
            _shockwaves.ToArray(),
            _judgements.ToArray()
        ));
    }
}

public class DanceModeRenderItem : RenderItem
{
    public List<WaypointTile> Waypoints;
    public int CurrentIndex;
    public float CharX, CharY;
    public float CamX, CamY;
    public double SongTimeMs;
    public Color Accent;
    public bool IsDead;
    public float FailTimer;
    public DanceModeElement.TrailPoint[] TrailPoints;
    public DanceModeElement.HitShockwave[] Shockwaves;
    public DanceModeElement.HitJudgement[] Judgements;

    public DanceModeRenderItem(
        List<WaypointTile> waypoints, int currentIndex,
        float charX, float charY, float camX, float camY,
        double songTimeMs, Color accent, bool isDead, float failTimer,
        DanceModeElement.TrailPoint[] trailPoints,
        DanceModeElement.HitShockwave[] shockwaves,
        DanceModeElement.HitJudgement[] judgements)
    {
        Waypoints = waypoints;
        CurrentIndex = currentIndex;
        CharX = charX;
        CharY = charY;
        CamX = camX;
        CamY = camY;
        SongTimeMs = songTimeMs;
        Accent = accent;
        IsDead = isDead;
        FailTimer = failTimer;
        TrailPoints = trailPoints;
        Shockwaves = shockwaves;
        Judgements = judgements;
    }

    public override void Draw(IRenderer renderer)
    {
        // 0. Semi-transparent dark gameplay backdrop overlay
        renderer.FillRect(new Rect(0, 0, App.Width, App.Height), new Color(12, 12, 18, 200));

        float screenMidX = App.Width * 0.5f;
        float screenMidY = App.Height * 0.5f;

        (float Sx, float Sy) ToScreen(float wx, float wy)
            => (screenMidX + (wx - CamX), screenMidY + (wy - CamY));

        int renderStart = Math.Max(0, CurrentIndex - 5);
        int renderEnd = Math.Min(Waypoints.Count, CurrentIndex + 25);

        // 1. Draw Neon Laser Connecting Rail between current note n and next note n+1 as it fades in
        if (CurrentIndex < Waypoints.Count - 1)
        {
            var t1 = Waypoints[CurrentIndex];
            var t2 = Waypoints[CurrentIndex + 1];
            double timeUntilHit = t2.HitTimeMs - SongTimeMs;
            if (timeUntilHit <= 700.0)
            {
                float lineAlphaFactor = timeUntilHit > 600.0 ? (float)((700.0 - timeUntilHit) / 100.0) : 1.0f;
                var (s1x, s1y) = ToScreen(t1.WorldX, t1.WorldY);
                var (s2x, s2y) = ToScreen(t2.WorldX, t2.WorldY);
                Color laserBg = new Color(Accent.R, Accent.G, Accent.B, (byte)(200 * Math.Clamp(lineAlphaFactor, 0f, 1f)));
                renderer.DrawLine(s1x, s1y, s2x, s2y, laserBg);
            }
        }

        // 2. Draw Glassmorphic Hit Objects (osu! style lifecycle: spawn 700ms -> fade-in 100ms -> approach ring 600ms -> hit)
        for (int i = renderStart; i < renderEnd; i++)
        {
            var tile = Waypoints[i];
            var (sx, sy) = ToScreen(tile.WorldX, tile.WorldY);

            // If active hold note is currently being hit/slid, move tile head with character!
            if (i == CurrentIndex && tile.IsHold && tile.IsHit)
            {
                var (hcsx, hcsy) = ToScreen(CharX, CharY);
                sx = hcsx;
                sy = hcsy;
            }

            float tileSize = 70f;
            Rect tileRect = new Rect(sx - tileSize * 0.5f, sy - tileSize * 0.5f, tileSize, tileSize);

            float alphaMult = 1.0f;

            if (i < CurrentIndex)
            {
                int pastDelta = i - CurrentIndex;
                alphaMult = MathF.Max(0f, 0.4f + pastDelta * 0.12f);
            }
            else if (i == CurrentIndex)
            {
                alphaMult = 1.0f;
            }
            else
            {
                double timeUntilHit = tile.HitTimeMs - SongTimeMs;
                if (timeUntilHit > 700.0)
                {
                    alphaMult = 0.0f; // Note has not spawned yet!
                }
                else if (timeUntilHit > 600.0)
                {
                    // Note fades in smoothly during 100ms window before approach ring spawns (700ms -> 600ms)
                    float fadeInProgress = (float)((700.0 - timeUntilHit) / 100.0);
                    alphaMult = Math.Clamp(fadeInProgress, 0f, 1f);
                }
                else
                {
                    // Approach ring active window (600ms -> 0ms): Note is fully visible!
                    alphaMult = (GameplayMods.Hidden && i == CurrentIndex + 1) ? 0.0f : 1.0f;
                }
            }

            if (alphaMult <= 0.001f) continue;

            // Draw Thick Golden Trail Track for Hold Notes (24px wide track body shrinking dynamically as character slides)
            if (tile.IsHold && (i >= CurrentIndex))
            {
                var (esx, esy) = ToScreen(tile.EndWorldX, tile.EndWorldY);
                float dx = esx - sx;
                float dy = esy - sy;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                if (len > 2f)
                {
                    float nx = -dy / len;
                    float ny = dx / len;

                    // Draw 24px thick glowing gold track body
                    Color trackBg = new Color(220, 170, 0, (byte)(140 * alphaMult));
                    for (float offset = -12f; offset <= 12f; offset += 2.5f)
                    {
                        renderer.DrawLine(sx + nx * offset, sy + ny * offset, esx + nx * offset, esy + ny * offset, trackBg);
                    }

                    // Bright center gold core
                    Color trackCore = new Color(255, 240, 120, (byte)(230 * alphaMult));
                    for (float offset = -2f; offset <= 2f; offset += 2f)
                    {
                        renderer.DrawLine(sx + nx * offset, sy + ny * offset, esx + nx * offset, esy + ny * offset, trackCore);
                    }
                }

                float endSize = 24f;
                Rect endRect = new Rect(esx - endSize * 0.5f, esy - endSize * 0.5f, endSize, endSize);
                renderer.FillRect(endRect, new Color(200, 160, 0, (byte)(180 * alphaMult)));
                renderer.DrawRect(endRect, new Color(255, 245, 140, (byte)(240 * alphaMult)), 2.0f);

                if (tile.Repeats > 1 && alphaMult > 0.1f)
                {
                    renderer.DrawText("↩", esx - 5f, esy - 9f, Colors.Black, "gsans_semib", 13f);
                }
            }

            if (i < CurrentIndex)
            {
                renderer.FillRect(tileRect, new Color(35, 35, 45, (byte)(140 * alphaMult)));
            }
            else if (i == CurrentIndex)
            {
                Color activeColor = tile.IsHold ? new Color(255, 215, 0, 240) : new Color(60, 60, 80, 220);
                Color activeBorder = tile.IsHold ? new Color(255, 245, 140, 255) : Accent;
                renderer.FillRect(tileRect, activeColor);
                renderer.DrawRect(tileRect, activeBorder, 3.0f);

                string activeNumStr = tile.ComboNumber > 0 ? tile.ComboNumber.ToString() : (i - CurrentIndex).ToString();
                Color activeNumColor = tile.IsHold ? Colors.Black : Colors.White;
                float activeOffsetX = activeNumStr.Length > 1 ? 9f : 4f;
                renderer.DrawText(activeNumStr, sx - activeOffsetX, sy - 10f, activeNumColor, "gsans_semib", 15f);
            }
            else
            {
                if (tile.IsHold)
                {
                    // Bright Golden Hold Note styling
                    renderer.FillRect(tileRect, new Color(140, 110, 0, (byte)(220 * alphaMult)));
                    renderer.DrawRect(tileRect, new Color(255, 215, 0, (byte)(240 * alphaMult)), 2.0f);

                    if (alphaMult > 0.08f)
                    {
                        string numStr = tile.ComboNumber > 0 ? tile.ComboNumber.ToString() : (i - CurrentIndex).ToString();
                        Color numColor = new Color(255, 245, 140, (byte)(255 * alphaMult));
                        float offsetX = numStr.Length > 1 ? 9f : 4f;
                        renderer.DrawText(numStr, sx - offsetX, sy - 10f, numColor, "gsans_semib", 15f);
                    }
                }
                else
                {
                    // Dark glass translucent slate tile
                    renderer.FillRect(tileRect, new Color(18, 20, 28, (byte)(210 * alphaMult)));
                    renderer.DrawRect(tileRect, new Color(Accent.R, Accent.G, Accent.B, (byte)(180 * alphaMult)), 1.5f);

                    if (alphaMult > 0.08f)
                    {
                        // Draw centered combo number label inside upcoming tiles
                        string numStr = tile.ComboNumber > 0 ? tile.ComboNumber.ToString() : (i - CurrentIndex).ToString();
                        Color numColor = new Color(Accent.R, Accent.G, Accent.B, (byte)(255 * alphaMult));
                        float offsetX = numStr.Length > 1 ? 9f : 4f;
                        renderer.DrawText(numStr, sx - offsetX, sy - 10f, numColor, "gsans_semib", 15f);
                    }
                }
            }

            // Render shrinking Approach Ring (osu! style: defaulted to transparent + 3.0x scale shrinking to 1.0x)
            if (i > CurrentIndex && !IsDead)
            {
                double timeUntilHit = tile.HitTimeMs - SongTimeMs;
                if (timeUntilHit > 0 && timeUntilHit <= 600.0)
                {
                    float progress = (float)(timeUntilHit / 600.0); // 1.0 down to 0.0
                    float scale = 1.0f + 2.0f * progress; // 3.0x down to 1.0x
                    float ringRadius = 35f * scale;
                    float ringSize = ringRadius * 2f;

                    // Defaulted to transparent & fade in smoothly during first 200ms (600ms -> 400ms)
                    float fadeAlpha = timeUntilHit > 400.0 ? (float)((600.0 - timeUntilHit) / 200.0) : 1.0f;
                    byte ringAlpha = (byte)(230f * Math.Clamp(fadeAlpha, 0f, 1f) * alphaMult);

                    Rect ringRect = new Rect(sx - ringRadius, sy - ringRadius, ringSize, ringSize);
                    Color ringColor = tile.IsHold ? new Color(255, 215, 0, ringAlpha) : new Color(Accent.R, Accent.G, Accent.B, ringAlpha);
                    renderer.DrawRect(ringRect, ringColor, 2.0f);
                }
            }
        }

        // 3. Render Hit Shockwaves
        for (int i = 0; i < Shockwaves.Length; i++)
        {
            var sw = Shockwaves[i];
            float t = sw.Timer / 0.30f;
            if (t >= 1f) continue;
            var (swX, swY) = ToScreen(sw.WorldX, sw.WorldY);
            float radius = 30f + t * 50f;
            float size = radius * 2f;
            Rect swRect = new Rect(swX - radius, swY - radius, size, size);
            byte swAlpha = (byte)((1f - t) * 200f);
            renderer.DrawRect(swRect, new Color(Accent.R, Accent.G, Accent.B, swAlpha), 2f);
        }

        // 4. Render Floating Judgement Popups ("PERFECT!", "GREAT!")
        for (int i = 0; i < Judgements.Length; i++)
        {
            var j = Judgements[i];
            float t = j.Timer / 0.45f;
            if (t >= 1f) continue;
            var (jx, jy) = ToScreen(j.WorldX, j.WorldY);
            float floatY = jy - 20f - t * 35f;
            byte jAlpha = (byte)((1f - t) * 255f);
            Color jColor = new Color(j.Color.R, j.Color.G, j.Color.B, jAlpha);
            float textOffset = j.Text.Length * 4.5f;
            renderer.DrawText(j.Text, jx - textOffset, floatY, jColor, "gsans_semib", 16f);
        }

        // 5. Render Character Motion Ghosting Trail (Pure Silver / White)
        for (int i = 0; i < TrailPoints.Length; i++)
        {
            var tp = TrailPoints[i];
            float t = tp.Life / 0.25f;
            if (t <= 0f) continue;
            var (tsx, tsy) = ToScreen(tp.WorldX, tp.WorldY);
            float tw = 32f * t;
            float th = 32f * t;
            Rect tRect = new Rect(tsx - tw * 0.5f, tsy - th * 0.5f, tw, th);
            byte tAlpha = (byte)(t * 140f);
            renderer.FillRect(tRect, new Color(255, 255, 255, tAlpha));
        }

        // 6. Render Glowing White Player Gem
        var (csx, csy) = ToScreen(CharX, CharY);
        float charSize = 34f;
        Rect charRect = new Rect(csx - charSize * 0.5f, csy - charSize * 0.5f, charSize, charSize);

        if (!IsDead)
        {
            // Pure White Beat Pulse Aura Ring
            float auraSize = 44f + Program.pulseAmount * 12f;
            Rect auraRect = new Rect(csx - auraSize * 0.5f, csy - auraSize * 0.5f, auraSize, auraSize);
            renderer.DrawRect(auraRect, new Color(255, 255, 255, 220), 2.5f);

            // Crisp Solid White Player Gem Core
            renderer.FillRect(charRect, Colors.White);
            renderer.DrawRect(charRect, new Color(200, 200, 220), 2.0f);
        }
        else
        {
            Color deathColor = new Color(255, 40, 40, (byte)Math.Clamp(255f - FailTimer * 200f, 0f, 255f));
            renderer.FillRect(charRect, deathColor);
        }

        // 7. Minimalist Top-Right Rhythm HUD Overlay
        if (!IsDead && Waypoints.Count > 0)
        {
            double lastNoteTime = Waypoints[Waypoints.Count - 1].HitTimeMs;
            float progressFactor = (float)Math.Clamp(SongTimeMs / Math.Max(1.0, lastNoteTime), 0.0, 1.0);

            // Top Progress Bar
            renderer.FillRect(new Rect(0, 0, App.Width, 4), new Color(30, 30, 40, 180));
            renderer.FillRect(new Rect(0, 0, App.Width * progressFactor, 4), Accent);

            // Top Right Combo & Mod Badge
            string comboText = $"COMBO  {CurrentIndex}x";
            renderer.DrawText(comboText, App.Width - 180f, 20f, Colors.White, "gsans_semib", 16f);

            if (GameplayMods.Auto)
            {
                renderer.DrawText("AUTOPLAY", App.Width - 180f, 42f, Accent, "gsans_semib", 12f);
            }
        }
    }
}
