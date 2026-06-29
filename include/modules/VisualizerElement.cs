using System;
using System.Collections.Generic;
using System.Reflection;
using Rei2D;
using Rei2D.Rendering;
using Rei2D.Audio;
using Rei2D.Elements;

namespace Matrix.CoreGame;

public class VisualizerElement : Element
{
    public static VisualizerElement? Instance;

    private static readonly FieldInfo TrackHandleField = 
        typeof(Track).GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance);

    public struct RhythmParticle
    {
        public bool Active;
        public float X, Y;
        public float Vx, Vy;
        public float Size;
        public float Rotation;
        public float RotationSpeed;
        public float Alpha;
        public float Life;
        public float MaxLife;
        public float SpeedMultiplier;
        public Color BaseColor;
        public bool IsRadial;
    }

    public struct DustMote
    {
        public float X, Y;
        public float SpeedX, SpeedY;
        public float Size;
        public float Alpha;
        public Color Color;
    }

    public struct RippleRing
    {
        public bool Active;
        public float CenterX, CenterY;
        public float Radius;
        public float MaxRadius;
        public float Alpha;
        public float Thickness;
        public Color Color;
    }

    public struct BeatTrail
    {
        public bool Active;
        public float Angle;   // degrees
        public float Alpha;
    }

    public struct OrbitDot
    {
        public float Phase;        // 0..2π base phase offset
        public float Radius;       // current orbit radius
        public float BaseRadius;   // resting radius
        public float PulseRadius;  // extra radius added on downbeat, decays
        public float Size;
        public float Alpha;
    }

    public struct TickMark
    {
        public int Index;          // 0..11
        public float Glow;         // 0..1, decays after being lit
    }

    private readonly float[] fftRaw = new float[256];
    private readonly float[] circularSmooth = new float[90];
    private readonly float[] horizontalSmooth = new float[96];
    
    // Fixed-size pre-allocated pools to eliminate GC stutters
    private readonly RhythmParticle[] particles = new RhythmParticle[300];
    private readonly DustMote[] _dustMotes = new DustMote[15];
    private readonly RippleRing[] _rippleRings = new RippleRing[16];
    private readonly BeatTrail[] _beatTrails = new BeatTrail[16];
    private readonly OrbitDot[] _orbitDots = new OrbitDot[3];
    private readonly TickMark[] _tickMarks = new TickMark[12];
    
    private float _orbitalAngle = 0f;
    private float _glowIntensity = 0f;
    private float _arrowAngle = 0f;
    private int _tickIndex = 0;
    private float beatFlashIntensity = 0f;
    private float logoShakeIntensity = 0f;

    public VisualizerElement()
    {
        Instance = this;
        Size = new Size2D(1f, 1f);
        Position = new Position2D(0f, 0f);
        Anchor = Anchor2D.TopLeft;
        InterceptsMouse = false;

        // Initialize static dust motes
        for (int i = 0; i < 15; i++)
        {
            _dustMotes[i] = new DustMote
            {
                X = Random.Shared.NextSingle() * App.Width,
                Y = Random.Shared.NextSingle() * App.Height,
                SpeedX = (Random.Shared.NextSingle() * 2f - 1f) * 8f,
                SpeedY = -(Random.Shared.NextSingle() * 15f + 10f),
                Size = Random.Shared.NextSingle() * 2f + 1f,
                Alpha = Random.Shared.NextSingle() * 0.12f + 0.08f,
                Color = new Color(
                    (byte)(200 + Random.Shared.Next(56)),
                    (byte)(200 + Random.Shared.Next(56)),
                    (byte)(210 + Random.Shared.Next(46)),
                    255)
            };
        }

        // Initialize orbit dots
        for (int i = 0; i < 3; i++)
        {
            _orbitDots[i] = new OrbitDot
            {
                Phase = i * (MathF.PI * 2f / 3f),
                BaseRadius = 0f,
                PulseRadius = 0f,
                Size = 5f,
                Alpha = 0.75f,
            };
        }

        // Initialize tick marks
        for (int i = 0; i < 12; i++)
        {
            _tickMarks[i] = new TickMark { Index = i, Glow = 0f };
        }
    }

    public void OnBeat(bool isDownbeat)
    {
        beatFlashIntensity = isDownbeat ? 1.35f : 0.4f;
        logoShakeIntensity = isDownbeat ? 1.85f : 0.3f;

        int spawnCount = isDownbeat ? Random.Shared.Next(35, 55) : Random.Shared.Next(8, 14);
        SpawnRadialBurst(spawnCount, isDownbeat);

        float rcx = App.Width * 0.5f;
        float rcy = App.Height * 0.5f;
        if (Logo.Instance != null)
        {
            rcx = Logo.Instance.Bounds.X + Logo.Instance.Bounds.Width * 0.5f;
            rcy = Logo.Instance.Bounds.Y + Logo.Instance.Bounds.Height * 0.5f;
        }

        // Spawn a ripple ring
        int rippleIdx = -1;
        for (int i = 0; i < _rippleRings.Length; i++)
        {
            if (!_rippleRings[i].Active)
            {
                rippleIdx = i;
                break;
            }
        }
        if (rippleIdx != -1)
        {
            _rippleRings[rippleIdx] = new RippleRing
            {
                Active = true,
                CenterX = rcx,
                CenterY = rcy,
                Radius = 0f,
                MaxRadius = isDownbeat ? 380f : 240f,
                Alpha = isDownbeat ? 0.35f : 0.20f,
                Thickness = isDownbeat ? 1.8f : 1.0f,
                Color = Program.ActiveAccent
            };
        }

        _glowIntensity = isDownbeat ? 1.0f : 0.45f;

        // Shift beat trails (FIFO size 5)
        for (int i = 4; i > 0; i--)
        {
            _beatTrails[i] = _beatTrails[i - 1];
        }
        _beatTrails[0] = new BeatTrail { Active = true, Angle = _arrowAngle, Alpha = isDownbeat ? 0.55f : 0.28f };

        // Pulse orbit dots
        if (isDownbeat)
        {
            for (int i = 0; i < _orbitDots.Length; i++)
                _orbitDots[i].PulseRadius = 28f;
        }

        // Light tick marks
        _tickMarks[_tickIndex % 12].Glow = isDownbeat ? 1.0f : 0.6f;
        _tickIndex++;
        if (isDownbeat)
        {
            _tickMarks[_tickIndex % 12].Glow = 0.8f;
            _tickIndex++;
        }
    }

    private static Color GetSubtleParticleColor(Color accent)
    {
        // Blend 70% white with 30% accent for a crisp white look with a subtle beatmap accent tint
        return new Color(
            (byte)(180 + accent.R * 0.294f),
            (byte)(180 + accent.G * 0.294f),
            (byte)(180 + accent.B * 0.294f),
            255
        );
    }

    private void SpawnAmbientParticle()
    {
        int spawnIdx = -1;
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].Active)
            {
                spawnIdx = i;
                break;
            }
        }
        if (spawnIdx != -1)
        {
            float rx = Random.Shared.NextSingle() * App.Width;
            particles[spawnIdx] = new RhythmParticle
            {
                Active = true,
                X = rx,
                Y = App.Height + 15f,
                Vx = (Random.Shared.NextSingle() * 2f - 1f) * 20f,
                Vy = -(Random.Shared.NextSingle() * 50f + 35f),
                Size = Random.Shared.NextSingle() * 5f + 3f,
                Rotation = Random.Shared.NextSingle() * 360f,
                RotationSpeed = (Random.Shared.NextSingle() * 2f - 1f) * 40f,
                Alpha = 0f,
                MaxLife = Random.Shared.NextSingle() * 8f + 8f,
                Life = 0f,
                SpeedMultiplier = 1f,
                BaseColor = GetSubtleParticleColor(Program.ActiveAccent),
                IsRadial = false
            };
            particles[spawnIdx].Life = particles[spawnIdx].MaxLife;
        }
    }

    private void SpawnRadialBurst(int count, bool isDownbeat)
    {
        float cx = App.Width * 0.5f;
        float cy = App.Height * 0.5f;
        float radius = 100f;

        if (Logo.Instance != null)
        {
            cx = Logo.Instance.Bounds.X + Logo.Instance.Bounds.Width * 0.5f;
            cy = Logo.Instance.Bounds.Y + Logo.Instance.Bounds.Height * 0.5f;
            radius = Logo.Instance.Bounds.Width * 0.55f;
        }

        int spawned = 0;
        Color subtleColor = GetSubtleParticleColor(Program.ActiveAccent);
        for (int i = 0; i < particles.Length; i++)
        {
            if (spawned >= count) break;
            if (!particles[i].Active)
            {
                float angle = Random.Shared.NextSingle() * MathF.PI * 2f;
                float speed = isDownbeat ? (Random.Shared.NextSingle() * 450f + 200f) : (Random.Shared.NextSingle() * 230f + 100f);
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);
                float size = isDownbeat ? (Random.Shared.NextSingle() * 11f + 6f) : (Random.Shared.NextSingle() * 6f + 3f);
                float life = isDownbeat ? (Random.Shared.NextSingle() * 0.9f + 0.6f) : (Random.Shared.NextSingle() * 0.5f + 0.4f);

                particles[i] = new RhythmParticle
                {
                    Active = true,
                    X = cx + cos * radius,
                    Y = cy + sin * radius,
                    Vx = cos * speed,
                    Vy = sin * speed,
                    Size = size,
                    Rotation = Random.Shared.NextSingle() * 360f,
                    RotationSpeed = (Random.Shared.NextSingle() * 2f - 1f) * 180f,
                    Alpha = 0f,
                    MaxLife = life,
                    Life = life,
                    SpeedMultiplier = isDownbeat ? 3.5f : 2.0f,
                    BaseColor = subtleColor,
                    IsRadial = true
                };
                spawned++;
            }
        }
    }

    protected override void OnUpdateCore(float dt)
    {
        var track = Program.currentTrack;
        if (track != null && track.IsPlaying)
        {
            if (TrackHandleField != null)
            {
                int handle = (int)TrackHandleField.GetValue(track)!;
                if (handle != 0)
                {
                    ManagedBass.Bass.ChannelGetData(handle, fftRaw, unchecked((int)0x80000001));
                }
            }
        }
        else
        {
            Array.Clear(fftRaw, 0, fftRaw.Length);
        }

        int numCirc = circularSmooth.Length;
        float flare = 1f + beatFlashIntensity * 0.40f;

        for (int i = 0; i < numCirc; i++)
        {
            float norm = (float)i / numCirc;
            float distToCenter = MathF.Abs(norm - 0.5f) * 2f;

            int bin = (int)(MathF.Pow(distToCenter, 1.4f) * 120f);
            bin = Math.Clamp(bin, 0, 255);

            float raw = fftRaw[bin];
            float boost = 1f + distToCenter * 2.0f;
            float targetHeight = raw * boost * 260f * flare;

            targetHeight = Math.Min(targetHeight, 230f);

            if (targetHeight > circularSmooth[i])
                circularSmooth[i] = targetHeight;
            else
                circularSmooth[i] = circularSmooth[i] + (targetHeight - circularSmooth[i]) * 10f * dt;
        }

        int numHoriz = horizontalSmooth.Length;
        float halfH = numHoriz / 2f;
        for (int i = 0; i < numHoriz; i++)
        {
            float norm = MathF.Abs((i - halfH) / halfH);
            int bin = (int)(MathF.Pow(norm, 1.3f) * 140f);
            bin = Math.Clamp(bin, 0, 255);

            float raw = fftRaw[bin];
            float boost = 1.2f + MathF.Pow(norm, 1.8f) * 4.8f;
            float targetHeight = raw * boost * 440f * flare;

            targetHeight = Math.Min(targetHeight, 340f);

            if (targetHeight > horizontalSmooth[i])
                horizontalSmooth[i] = targetHeight;
            else
                horizontalSmooth[i] = horizontalSmooth[i] + (targetHeight - horizontalSmooth[i]) * 9f * dt;
        }

        if (beatFlashIntensity > 0f)
            beatFlashIntensity = Math.Max(0f, beatFlashIntensity - 2.5f * dt);

        if (logoShakeIntensity > 0f)
            logoShakeIntensity = Math.Max(0f, logoShakeIntensity - 14.0f * dt);

        if (logoShakeIntensity > 0f && Logo.Instance != null)
        {
            float sx = (Random.Shared.NextSingle() * 2f - 1f) * logoShakeIntensity * 10.5f;
            float sy = (Random.Shared.NextSingle() * 2f - 1f) * logoShakeIntensity * 10.5f;
            Program.LogoShakeOffset = new Position2D(0f, 0f, sx, sy);
        }
        else
        {
            Program.LogoShakeOffset = new Position2D(0f, 0f, 0f, 0f);
        }

        _orbitalAngle += dt * 0.12f;

        if (Program.arrowImage != null)
            _arrowAngle = Program.arrowImage.Rotation;

        // Decay beat trails
        for (int i = 0; i < _beatTrails.Length; i++)
        {
            if (!_beatTrails[i].Active) continue;
            var bt = _beatTrails[i];
            bt.Alpha = Math.Max(0f, bt.Alpha - 1.8f * dt);
            if (bt.Alpha <= 0f)
                bt.Active = false;
            _beatTrails[i] = bt;
        }

        // Advance orbit dots
        for (int i = 0; i < _orbitDots.Length; i++)
        {
            _orbitDots[i].PulseRadius = Math.Max(0f, _orbitDots[i].PulseRadius - 60f * dt);
        }

        // Decay tick marks
        for (int i = 0; i < _tickMarks.Length; i++)
        {
            _tickMarks[i].Glow = Math.Max(0f, _tickMarks[i].Glow - 2.2f * dt);
        }

        // Move dust motes
        for (int i = 0; i < _dustMotes.Length; i++)
        {
            var dm = _dustMotes[i];
            dm.X += dm.SpeedX * dt;
            dm.Y += dm.SpeedY * dt;
            if (dm.Y < -10f) { dm.Y = App.Height + 10f; dm.X = Random.Shared.NextSingle() * App.Width; }
            if (dm.X < -10f) dm.X = App.Width + 10f;
            if (dm.X > App.Width + 10f) dm.X = -10f;
            _dustMotes[i] = dm;
        }

        // Update ripple rings
        for (int i = 0; i < _rippleRings.Length; i++)
        {
            if (!_rippleRings[i].Active) continue;
            var rr = _rippleRings[i];
            rr.Radius += dt * 520f;
            rr.Alpha -= dt * 1.4f;
            if (rr.Alpha <= 0f || rr.Radius >= rr.MaxRadius)
                rr.Active = false;
            _rippleRings[i] = rr;
        }

        if (_glowIntensity > 0f)
            _glowIntensity = Math.Max(0f, _glowIntensity - 2.5f * dt);

        // Update particles
        int activeCount = 0;
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].Active) continue;
            activeCount++;
            var p = particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                particles[i].Active = false;
                continue;
            }

            if (p.SpeedMultiplier > 1f)
                p.SpeedMultiplier = Math.Max(1f, p.SpeedMultiplier - 4f * dt);

            p.X += p.Vx * p.SpeedMultiplier * dt;
            p.Y += p.Vy * p.SpeedMultiplier * dt;
            p.Rotation += p.RotationSpeed * dt;

            float ratio = p.Life / p.MaxLife;
            p.Alpha = p.IsRadial ? ratio * 0.7f : Math.Min(1f, ratio * 2f) * 0.4f;
            particles[i] = p;
        }

        if (activeCount < 80 && Random.Shared.NextSingle() < 0.20f)
        {
            SpawnAmbientParticle();
        }
    }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (SkipDraw || !Visible) return;

        float cx = App.Width * 0.5f;
        float cy = App.Height * 0.5f;
        float logoRadius = 120f;

        if (Logo.Instance != null)
        {
            cx = Logo.Instance.Bounds.X + Logo.Instance.Bounds.Width * 0.5f;
            cy = Logo.Instance.Bounds.Y + Logo.Instance.Bounds.Height * 0.5f;
            logoRadius = Logo.Instance.Bounds.Width * 0.5f;
        }

        float menuVal = Program.menuTween?.CurrentValue ?? 0f;

        // Zero Allocations: Pass raw pre-allocated arrays directly without cloning
        items.Add(new VisualizerRenderItem(
            cx, cy, logoRadius,
            circularSmooth, horizontalSmooth,
            Program.ActiveAccent,
            particles,
            beatFlashIntensity,
            menuVal,
            _dustMotes,
            _rippleRings,
            _orbitalAngle,
            _glowIntensity,
            _beatTrails,
            _orbitDots,
            _tickMarks
        ));
    }
}

public class VisualizerRenderItem : RenderItem
{
    public float LogoCenterX;
    public float LogoCenterY;
    public float LogoRadius;
    public float[] CircularHeights;
    public float[] HorizontalHeights;
    public Color AccentColor;
    public VisualizerElement.RhythmParticle[] Particles;
    public float BeatFlashIntensity;
    public float MenuValue;
    public VisualizerElement.DustMote[] DustMotes;
    public VisualizerElement.RippleRing[] RippleRings;
    public float OrbitalAngle;
    public float GlowIntensity;
    public VisualizerElement.BeatTrail[] BeatTrails;
    public VisualizerElement.OrbitDot[] OrbitDots;
    public VisualizerElement.TickMark[] TickMarks;

    public VisualizerRenderItem(
        float centerX, float centerY, float radius,
        float[] circularHeights, float[] horizontalHeights,
        Color accentColor, VisualizerElement.RhythmParticle[] particles,
        float beatFlashIntensity, float menuValue,
        VisualizerElement.DustMote[] dustMotes,
        VisualizerElement.RippleRing[] rippleRings,
        float orbitalAngle, float glowIntensity,
        VisualizerElement.BeatTrail[] beatTrails,
        VisualizerElement.OrbitDot[] orbitDots,
        VisualizerElement.TickMark[] tickMarks)
    {
        LogoCenterX = centerX;
        LogoCenterY = centerY;
        LogoRadius = radius;
        CircularHeights = circularHeights;
        HorizontalHeights = horizontalHeights;
        AccentColor = accentColor;
        Particles = particles;
        BeatFlashIntensity = beatFlashIntensity;
        MenuValue = menuValue;
        DustMotes = dustMotes;
        RippleRings = rippleRings;
        OrbitalAngle = orbitalAngle;
        GlowIntensity = glowIntensity;
        BeatTrails = beatTrails;
        OrbitDots = orbitDots;
        TickMarks = tickMarks;
    }

    private static void DrawArrowGhost(IRenderer renderer, float cx, float cy, float size, float angleDeg, Color color)
    {
        float rad = angleDeg * MathF.PI / 180f;
        float tipX = cx + MathF.Cos(rad) * size;
        float tipY = cy + MathF.Sin(rad) * size;
        float leftRad = rad + MathF.PI * 0.75f;
        float rightRad = rad - MathF.PI * 0.75f;
        float baseX1 = cx + MathF.Cos(leftRad) * size * 0.7f;
        float baseY1 = cy + MathF.Sin(leftRad) * size * 0.7f;
        float baseX2 = cx + MathF.Cos(rightRad) * size * 0.7f;
        float baseY2 = cy + MathF.Sin(rightRad) * size * 0.7f;
        renderer.DrawLine(tipX, tipY, baseX1, baseY1, color);
        renderer.DrawLine(tipX, tipY, baseX2, baseY2, color);
        renderer.DrawLine(baseX1, baseY1, baseX2, baseY2, color);
    }

    public override unsafe void Draw(IRenderer renderer)
    {
        // Zero allocations in loop: index-based for loops
        for (int i = 0; i < DustMotes.Length; i++)
        {
            var dm = DustMotes[i];
            Color dc = new Color(dm.Color.R, dm.Color.G, dm.Color.B, (byte)(dm.Alpha * 255f));
            renderer.FillRect(new Rect(dm.X, dm.Y, dm.Size, dm.Size), dc);
        }

        if (BeatFlashIntensity > 0f)
        {
            Color flashColor = new Color(AccentColor.R, AccentColor.G, AccentColor.B, (byte)(12f * BeatFlashIntensity));
            renderer.FillRect(new Rect(0, 0, App.Width, App.Height), flashColor);
        }

        if (GlowIntensity > 0f && LogoCenterX > 0 && LogoCenterY > 0)
        {
            int glowLines = 24;
            float glowLength = 50f + GlowIntensity * 20f;
            Color gc = new Color(AccentColor.R, AccentColor.G, AccentColor.B, (byte)(GlowIntensity * 55f));
            for (int i = 0; i < glowLines; i++)
            {
                float angle = (float)i / glowLines * MathF.PI * 2f;
                float gx2 = LogoCenterX + MathF.Cos(angle) * glowLength;
                float gy2 = LogoCenterY + MathF.Sin(angle) * glowLength;
                renderer.DrawLine(LogoCenterX, LogoCenterY, gx2, gy2, gc);
            }
        }

        for (int i = 0; i < Particles.Length; i++)
        {
            if (!Particles[i].Active) continue;
            var p = Particles[i];
            Color pColor = new Color(p.BaseColor.R, p.BaseColor.G, p.BaseColor.B, (byte)(p.Alpha * 255f));
            if (p.Rotation != 0f)
            {
                Rect pRect = new Rect(p.X - p.Size / 2f, p.Y - p.Size / 2f, p.Size, p.Size);
                renderer.FillRectRotated(pRect, pColor, p.Rotation, p.X, p.Y);
            }
            else
            {
                Rect pRect = new Rect(p.X - p.Size / 2f, p.Y - p.Size / 2f, p.Size, p.Size);
                renderer.FillRect(pRect, pColor);
            }
        }

        for (int i = 0; i < RippleRings.Length; i++)
        {
            if (!RippleRings[i].Active || RippleRings[i].Alpha <= 0f || RippleRings[i].Radius <= 0f) continue;
            var rr = RippleRings[i];
            int segs = 48;
            Color rc = new Color(rr.Color.R, rr.Color.G, rr.Color.B, (byte)(rr.Alpha * 255f));
            for (int s = 0; s < segs; s++)
            {
                float a1 = (float)s / segs * MathF.PI * 2f;
                float a2 = (float)(s + 1) / segs * MathF.PI * 2f;
                float rx1 = rr.CenterX + rr.Radius * MathF.Cos(a1);
                float ry1 = rr.CenterY + rr.Radius * MathF.Sin(a1);
                float rx2 = rr.CenterX + rr.Radius * MathF.Cos(a2);
                float ry2 = rr.CenterY + rr.Radius * MathF.Sin(a2);
                renderer.DrawLine(rx1, ry1, rx2, ry2, rc);
            }
        }

        if (HorizontalHeights.Length > 0)
        {
            int numBars = HorizontalHeights.Length;
            float barWidth = App.Width / (float)numBars;
            float bottomY = App.Height - 62f * MenuValue;

            byte fillAlpha = (byte)(80f * (0.3f + 0.7f * MenuValue));
            Color baseFillColor = new Color(AccentColor.R, AccentColor.G, AccentColor.B, fillAlpha);
            Color neonTopColor = AccentColor;
            float pulseGlow = BeatFlashIntensity;

            // --- 0. Downbeat Additive Neon Bloom Aura (Normally invisible, flares on beat) ---
            if (pulseGlow > 0.02f && renderer is SdlRenderer sdlRenderer && sdlRenderer.RawRenderer != null)
            {
                var r = sdlRenderer.RawRenderer;
                SDL.SDL3.SDL_SetRenderDrawBlendMode(r, SDL.SDL_BlendMode.SDL_BLENDMODE_ADD);

                float expand = pulseGlow * 14f;
                byte bloomAlpha = (byte)Math.Min(255, pulseGlow * 140f * (0.3f + 0.7f * MenuValue));
                SDL.SDL3.SDL_SetRenderDrawColor(r, neonTopColor.R, neonTopColor.G, neonTopColor.B, bloomAlpha);

                for (int i = 0; i < numBars; i++)
                {
                    float rawH = HorizontalHeights[i];
                    if (rawH <= 1f) continue;

                    float h = MathF.Max(4f, rawH * (0.5f + 0.5f * (1f - MenuValue * 0.5f)));
                    float bx = i * barWidth;
                    float by = bottomY - h;

                    SDL.SDL_FRect bloomRect = new()
                    {
                        x = bx - expand * 0.5f,
                        y = by - expand,
                        w = barWidth + expand,
                        h = h + expand * 1.5f
                    };
                    SDL.SDL3.SDL_RenderFillRect(r, &bloomRect);
                }

                SDL.SDL3.SDL_SetRenderDrawBlendMode(r, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            }

            for (int i = 0; i < numBars; i++)
            {
                float rawH = HorizontalHeights[i];
                float h = MathF.Max(4f, rawH * (0.5f + 0.5f * (1f - MenuValue * 0.5f)));
                float bx = i * barWidth;
                float by = bottomY - h;
                float bw = barWidth + 0.5f; // Zero spacing between bars

                // 1. Semi-transparent cityscape silhouette base bar
                Rect barRect = new Rect(bx, by, bw, h);
                renderer.FillRect(barRect, baseFillColor);

                // 2. RGB Chromatic Aberration Top Edge Fringing
                byte chromAlpha = (byte)(140f * (0.3f + 0.7f * MenuValue));
                // Red Shift Right
                Rect redCap = new Rect(bx + 1.5f, by, bw, 2.5f);
                renderer.FillRect(redCap, new Color(255, 0, 0, chromAlpha));

                // Cyan Shift Left
                Rect cyanCap = new Rect(bx - 1.5f, by, bw, 2.5f);
                renderer.FillRect(cyanCap, new Color(0, 255, 255, chromAlpha));

                // 3. Beat-Reactive Neon Accent Top Line
                Rect neonCap = new Rect(bx, by, bw, 2.0f + pulseGlow * 2.0f);
                byte neonAlpha = (byte)Math.Min(255, (160 + pulseGlow * 95f) * (0.3f + 0.7f * MenuValue));
                renderer.FillRect(neonCap, new Color(neonTopColor.R, neonTopColor.G, neonTopColor.B, neonAlpha));
            }
        }

        if (CircularHeights.Length > 0 && LogoCenterX > 0 && LogoCenterY > 0)
        {
            int numBars = CircularHeights.Length;
            float innerRad = LogoRadius * 1.05f;
            if (innerRad < 30f) innerRad = 110f;

            float baseRot = (float)(DateTime.Now.TimeOfDay.TotalSeconds * 0.05);
            var tips = new (float X, float Y)[numBars];

            for (int i = 0; i < numBars; i++)
            {
                float h = CircularHeights[i];
                float angle = (float)i / numBars * MathF.PI * 2f + baseRot;
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);

                float x1 = LogoCenterX + innerRad * cos;
                float y1 = LogoCenterY + innerRad * sin;

                float outerRad = innerRad + MathF.Max(2f, h);
                float x2 = LogoCenterX + outerRad * cos;
                float y2 = LogoCenterY + outerRad * sin;

                tips[i] = (x2, y2);

                Color lineColor = new Color(AccentColor.R, AccentColor.G, AccentColor.B, (byte)(160f * (1f - MenuValue * 0.4f)));
                renderer.DrawLine(x1, y1, x2, y2, lineColor);

                if (h > 4f)
                {
                    float dotSize = 3f + h * 0.04f;
                    Rect dotRect = new Rect(x2 - dotSize / 2f, y2 - dotSize / 2f, dotSize, dotSize);
                    Color dotColor = new Color(
                        (byte)Math.Min(255, AccentColor.R + 40),
                        (byte)Math.Min(255, AccentColor.G + 40),
                        (byte)Math.Min(255, AccentColor.B + 40),
                        200
                    );
                    renderer.FillRect(dotRect, dotColor);
                }
            }

            Color tipConnectorColor = new Color(AccentColor.R, AccentColor.G, AccentColor.B, (byte)(80f * (1f - MenuValue * 0.3f)));
            for (int i = 0; i < numBars; i++)
            {
                int ni = (i + 1) % numBars;
                renderer.DrawLine(tips[i].X, tips[i].Y, tips[ni].X, tips[ni].Y, tipConnectorColor);
            }

            float orbitalRadius = innerRad + 255f;
            int orbitalSegs = 72;
            Color orbitalColor = new Color(AccentColor.R, AccentColor.G, AccentColor.B, (byte)40);
            for (int i = 0; i < orbitalSegs; i += 2)
            {
                float a1 = (float)i / orbitalSegs * MathF.PI * 2f + OrbitalAngle;
                float a2 = (float)(i + 1) / orbitalSegs * MathF.PI * 2f + OrbitalAngle;
                float ox1 = LogoCenterX + orbitalRadius * MathF.Cos(a1);
                float oy1 = LogoCenterY + orbitalRadius * MathF.Sin(a1);
                float ox2 = LogoCenterX + orbitalRadius * MathF.Cos(a2);
                float oy2 = LogoCenterY + orbitalRadius * MathF.Sin(a2);
                renderer.DrawLine(ox1, oy1, ox2, oy2, orbitalColor);
            }

            // Radial tick marks
            float tickOrbitR = innerRad + 240f;
            float tickInner = tickOrbitR;
            float tickOuter = tickOrbitR + 14f;
            for (int i = 0; i < TickMarks.Length; i++)
            {
                float g = TickMarks[i].Glow;
                if (g <= 0f) continue;
                float tickAngle = (float)i / TickMarks.Length * MathF.PI * 2f + baseRot;
                float tx1 = LogoCenterX + tickInner * MathF.Cos(tickAngle);
                float ty1 = LogoCenterY + tickInner * MathF.Sin(tickAngle);
                float tx2 = LogoCenterX + tickOuter * MathF.Cos(tickAngle);
                float ty2 = LogoCenterY + tickOuter * MathF.Sin(tickAngle);
                Color tc = new Color(AccentColor.R, AccentColor.G, AccentColor.B, (byte)(g * 230f));
                renderer.DrawLine(tx1, ty1, tx2, ty2, tc);
            }

            // Orbit dots
            float dotOrbitR = innerRad + 258f;
            for (int i = 0; i < OrbitDots.Length; i++)
            {
                var dot = OrbitDots[i];
                float dotAngle = dot.Phase + OrbitalAngle * 2.5f;
                float r = dotOrbitR + dot.PulseRadius;
                float dx = LogoCenterX + MathF.Cos(dotAngle) * r;
                float dy = LogoCenterY + MathF.Sin(dotAngle) * r;
                float ds = 8f + dot.PulseRadius * 0.2f;
                Color dc = new Color(AccentColor.R, AccentColor.G, AccentColor.B, (byte)(dot.Alpha * 255f));
                renderer.FillRect(new Rect(dx - ds * 0.5f, dy - ds * 0.5f, ds, ds), dc);
            }

            // Beat trails
            if (MenuValue < 0.65f)
            {
                float arrowSize = innerRad * 0.55f;
                float arrowCx = LogoCenterX;
                float arrowCy = LogoCenterY - innerRad * 1.6f;
                float fadeOut = 1f - MenuValue / 0.65f;
                for (int i = 0; i < 5; i++)
                {
                    if (!BeatTrails[i].Active) continue;
                    var trail = BeatTrails[i];
                    Color tc2 = new Color(AccentColor.R, AccentColor.G, AccentColor.B, (byte)(trail.Alpha * fadeOut * 255f));
                    DrawArrowGhost(renderer, arrowCx, arrowCy, arrowSize, trail.Angle - 90f, tc2);
                }
            }
        }
    }
}

public class BorderedImage : Image
{
    public Color BorderColor { get; set; } = Colors.Transparent;
    public float BorderThickness { get; set; } = 0f;
    public Func<float>? OpacityFunc { get; set; }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        base.CollectRenderItems(items, metrics);

        float opacity = OpacityFunc?.Invoke() ?? 1f;
        if (BorderThickness > 0 && BorderColor.A > 0 && opacity > 0.01f)
        {
            Color color = new Color(BorderColor.R, BorderColor.G, BorderColor.B, (byte)(BorderColor.A * opacity));
            items.Add(new ImageBorderRenderItem(Bounds, Rotation, AnchorPoint, BorderThickness, color));
        }
    }
}

public class ImageBorderRenderItem : RenderItem
{
    public Rect Rect;
    public float Rotation;
    public (float X, float Y) Pivot;
    public float Thickness;
    public Color Color;

    public ImageBorderRenderItem(Rect rect, float rotation, (float X, float Y) pivot, float thickness, Color color)
    {
        Rect = rect;
        Rotation = rotation;
        Pivot = pivot;
        Thickness = thickness;
        Color = color;
    }

    public override void Draw(IRenderer renderer)
    {
        float rad = Rotation * MathF.PI / 180f;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        float pivotX = Pivot.X;
        float pivotY = Pivot.Y;

        for (float t = 0f; t < Thickness; t += 0.8f)
        {
            float x = Rect.X - t;
            float y = Rect.Y - t;
            float w = Rect.Width + t * 2f;
            float h = Rect.Height + t * 2f;

            float x1_l = x - pivotX;
            float y1_l = y - pivotY;
            float x2_l = x + w - pivotX;
            float y2_l = y - pivotY;
            float x3_l = x + w - pivotX;
            float y3_l = y + h - pivotY;
            float x4_l = x - pivotX;
            float y4_l = y + h - pivotY;

            float x1 = x1_l * cos - y1_l * sin + pivotX;
            float y1 = x1_l * sin + y1_l * cos + pivotY;
            float x2 = x2_l * cos - y2_l * sin + pivotX;
            float y2 = x2_l * sin + y2_l * cos + pivotY;
            float x3 = x3_l * cos - y3_l * sin + pivotX;
            float y3 = x3_l * sin + y3_l * cos + pivotY;
            float x4 = x4_l * cos - y4_l * sin + pivotX;
            float y4 = x4_l * sin + y4_l * cos + pivotY;

            renderer.DrawLine(x1, y1, x2, y2, Color);
            renderer.DrawLine(x2, y2, x3, y3, Color);
            renderer.DrawLine(x3, y3, x4, y4, Color);
            renderer.DrawLine(x4, y4, x1, y1, Color);
        }
    }
}
