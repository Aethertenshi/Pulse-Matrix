using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ManagedBass;
using Rei2D;
using Rei2D.Audio;
using Rei2D.Rendering;
using Rei2D.Tween;
using SDL;
using OsuLib;
using Rei2D.Theme;

namespace Matrix.CoreGame;

public class GreetingTransition : Element
{
    private readonly Track _introTrack;
    private readonly int _bassHandle;
    private float _time = 0f;
    private float _amplitude = 0f;
    private float _gridPulse = 0f;
    
    // Smooth intro variables managed by Tween
    private float _logoScale = 0.05f;
    private float _logoAlpha = 0f;
    private float _swipeProgress = 0f;
    private float _transitionFade = 1f; // fades logo and grid out when swiping

    private bool _swipeTriggered = false;
    private bool _callbackExecuted = false;

    private readonly Tween _logoGrowTween;
    private readonly Tween _swipeTween;

    // Fixed-size pre-allocated pools to eliminate GC stutters
    private readonly TransitionParticle[] _particles = new TransitionParticle[128];
    private readonly TransitionRipple[] _ripples = new TransitionRipple[16];

    public struct TransitionParticle
    {
        public bool Active;
        public float X, Y;
        public float Vx, Vy;
        public float Size;
        public float Alpha;
        public float Life;
        public float MaxLife;
        public Color Color;
    }

    public struct TransitionRipple
    {
        public bool Active;
        public float CenterX, CenterY;
        public float Radius;
        public float MaxRadius;
        public float Alpha;
        public float Thickness;
        public Color Color;
    }

    public GreetingTransition(IReadOnlyList<OsuBeatmap> beatmaps)
    {
        Size = Size2D.Full;
        Position = new Position2D(0f, 0f);
        Anchor = Anchor2D.TopLeft;
        InterceptsMouse = true; // Block main menu clicks until swipe is done

        // 1. Play welcome greeting audio
        _introTrack = Audio.Load("include/audios/welcome.wav");
        
        // Retrieve private BASS handle via Reflection to analyze spectrum data
        var handleField = typeof(Track).GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance);
        _bassHandle = handleField != null ? (int)handleField.GetValue(_introTrack)! : 0;
        
        _introTrack.Play();

        // 2. Setup Logo Fading and Growing Tween using Custom Tween Engine
        _logoGrowTween = new Tween(0f, 1f, 1.8f, v =>
        {
            _logoScale = 0.05f + v * 0.30f; // growing to exactly 0.35f
            _logoAlpha = v;
        }, Easing.Exponential, EasingDirection.Out);
        _logoGrowTween.Start();

        // 3. Outro swipe tween using framework's Tween (half slower)
        _swipeTween = new Tween(0f, 1f, 1.4f, v =>
        {
            _swipeProgress = v;
            
            // Grid, particles, and logo fade out completely by v = 0.4f before being revealed by the sweep
            _transitionFade = Math.Clamp(1f - v / 0.4f, 0f, 1f);

            // Midpoint of swipe: Screen is fully covered by diagonal bands, switch states!
            if (v >= 0.5f)
            {
                if (!_callbackExecuted)
                {
                    _callbackExecuted = true;
                    _introTrack.Stop();
                    if (Program.currentTrack != null)
                    {
                        Program.currentTrack.Volume = 0f;
                        Program.currentTrack.Play();
                    }
                }
                
                if (Program.currentTrack != null)
                {
                    float fadeFactor = Math.Clamp((v - 0.5f) / 0.5f, 0f, 1f);
                    Program.currentTrack.Volume = fadeFactor;
                }
            }
        }, Easing.Exponential, EasingDirection.Out, onComplete: () =>
        {
            // Finished, remove self from App.Root
            if (Program.currentTrack != null)
            {
                Program.currentTrack.Volume = 1.0f;
            }
            _introTrack.Dispose();
            App.Defer(() => App.Root.RemoveChild(this));
        });
    }

    protected override void OnUpdateCore(float dt)
    {
        _time += dt;

        // 1. Audio amplitude analysis
        if (_bassHandle != 0 && _introTrack.IsPlaying)
        {
            int level = Bass.ChannelGetLevel(_bassHandle);
            float left = (level & 0xffff) / 32768f;
            float right = ((level >> 16) & 0xffff) / 32768f;
            _amplitude = (left + right) / 2f;
        }
        else
        {
            _amplitude = 0f;
        }

        // Grid pulse decay
        _gridPulse = Math.Max(0f, _gridPulse - dt * 4f);
        if (_amplitude > 0.25f && _gridPulse < _amplitude)
        {
            _gridPulse = _amplitude;
        }

        // 2. Update ripples and particles (spawning in pools)
        if (_amplitude > 0.15f && Random.Shared.NextSingle() < 0.25f)
        {
            int rIdx = -1;
            for (int i = 0; i < _ripples.Length; i++)
            {
                if (!_ripples[i].Active) { rIdx = i; break; }
            }
            if (rIdx != -1)
            {
                _ripples[rIdx] = new TransitionRipple
                {
                    Active = true,
                    CenterX = App.Width * 0.5f,
                    CenterY = App.Height * 0.5f,
                    Radius = 20f,
                    MaxRadius = 300f + _amplitude * 200f,
                    Alpha = 0.4f + _amplitude * 0.4f,
                    Thickness = 1.5f + _amplitude * 2f,
                    Color = Program.ActiveAccent
                };
            }
        }

        if (_amplitude > 0.18f && Random.Shared.NextSingle() < 0.4f)
        {
            int count = Random.Shared.Next(2, 6);
            int spawned = 0;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (spawned >= count) break;
                if (!_particles[i].Active)
                {
                    float angle = Random.Shared.NextSingle() * MathF.PI * 2f;
                    float speed = Random.Shared.NextSingle() * 300f + 150f;
                    _particles[i] = new TransitionParticle
                    {
                        Active = true,
                        X = App.Width * 0.5f,
                        Y = App.Height * 0.5f,
                        Vx = MathF.Cos(angle) * speed,
                        Vy = MathF.Sin(angle) * speed,
                        Size = Random.Shared.NextSingle() * 6f + 3f,
                        Alpha = 0.8f,
                        Life = 0f,
                        MaxLife = Random.Shared.NextSingle() * 0.5f + 0.3f,
                        Color = Program.ActiveAccent
                    };
                    spawned++;
                }
            }
        }

        // Update ripples
        for (int i = 0; i < _ripples.Length; i++)
        {
            if (!_ripples[i].Active) continue;
            var r = _ripples[i];
            r.Radius += dt * 450f;
            r.Alpha -= dt * 1.5f;
            if (r.Alpha <= 0f)
                r.Active = false;
            _ripples[i] = r;
        }

        // Update particles
        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Active) continue;
            var p = _particles[i];
            p.Life += dt;
            if (p.Life >= p.MaxLife)
            {
                p.Active = false;
            }
            else
            {
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                p.Alpha = 1f - (p.Life / p.MaxLife);
            }
            _particles[i] = p;
        }

        // 3. Start transition out slightly before welcome.wav completely stops
        if (_time >= 2.3f && !_swipeTriggered)
        {
            _swipeTriggered = true;
            _swipeTween.Start();
        }
    }

    public static float EaseOutExpo(float x)
    {
        return x >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * x);
    }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (SkipDraw || !Visible) return;

        Color accent = Program.ActiveAccent;
        if (accent.R > 240 && accent.G > 240 && accent.B > 240 && Program.currentBeatmap != null)
        {
            accent = ThemeManager.Instance.GetAccent(Program.currentBeatmap.BeatmapSetId.ToString(), Program.currentBeatmap.GetBackgroundFullPath());
        }

        // Zero allocations: Pass array references directly
        items.Add(new GreetingRenderItem(
            _gridPulse,
            _amplitude,
            _logoScale,
            _logoAlpha,
            _swipeProgress,
            _transitionFade,
            _swipeTriggered,
            accent,
            _particles,
            _ripples
        ));
    }
}

public class GreetingRenderItem : RenderItem
{
    private readonly float _gridPulse;
    private readonly float _amplitude;
    private readonly float _logoScale;
    private readonly float _logoAlpha;
    private readonly float _swipeProgress;
    private readonly float _transitionFade;
    private readonly bool _swipeTriggered;
    private readonly Color _accentColor;
    private readonly GreetingTransition.TransitionParticle[] _particles;
    private readonly GreetingTransition.TransitionRipple[] _ripples;

    private static Texture? _cachedLogo;

    public GreetingRenderItem(
        float gridPulse,
        float amplitude,
        float logoScale,
        float logoAlpha,
        float swipeProgress,
        float transitionFade,
        bool swipeTriggered,
        Color accentColor,
        GreetingTransition.TransitionParticle[] particles,
        GreetingTransition.TransitionRipple[] ripples)
    {
        _gridPulse = gridPulse;
        _amplitude = amplitude;
        _logoScale = logoScale;
        _logoAlpha = logoAlpha;
        _swipeProgress = swipeProgress;
        _transitionFade = transitionFade;
        _swipeTriggered = swipeTriggered;
        _accentColor = accentColor;
        _particles = particles;
        _ripples = ripples;
    }

    public override void Draw(IRenderer renderer)
    {
        _cachedLogo ??= renderer.LoadTexture("include/textures/logo_baru.png");

        // 1. Draw background (acts as a diagonal mask moving with the leading band during swipe)
        if (!_swipeTriggered)
        {
            renderer.FillRect(new Rect(0, 0, App.Width, App.Height), new Color(10, 10, 15, 255));
        }
        else
        {
            float progress0 = _swipeProgress;
            float startX = -800f;
            float endX = App.Width + 800f;
            float currentX0 = startX + progress0 * (endX - startX);

            float bgW = 3500f;
            float bgCenterX = currentX0 + bgW * 0.5f - 50f;
            renderer.FillRectRotated(
                new Rect(bgCenterX - bgW * 0.5f, App.Height * 0.5f - 2000f, bgW, 4000f),
                new Color(10, 10, 15, 255),
                35f,
                bgCenterX,
                App.Height * 0.5f
            );
        }

        // 2. Draw rhythm grid
        int cols = 20;
        int rows = 12;
        float cellW = App.Width / (float)cols;
        float cellH = App.Height / (float)rows;
        
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x = c * cellW + cellW * 0.5f;
                float y = r * cellH + cellH * 0.5f;
                
                float dx = x - App.Width * 0.5f;
                float dy = y - App.Height * 0.5f;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                
                float pulseFactor = MathF.Max(0f, 1f - dist / 900f);
                float dotSize = 2f + _gridPulse * 6f * pulseFactor;
                byte dotAlpha = (byte)((0.06f + _gridPulse * 0.15f * pulseFactor) * 255f * _transitionFade);
                
                renderer.FillRect(new Rect(x - dotSize * 0.5f, y - dotSize * 0.5f, dotSize, dotSize), 
                    new Color(_accentColor.R, _accentColor.G, _accentColor.B, dotAlpha));
            }
        }

        // 3. Draw ripples (for loops)
        for (int i = 0; i < _ripples.Length; i++)
        {
            if (!_ripples[i].Active || _ripples[i].Alpha <= 0f) continue;
            var rip = _ripples[i];
            int segs = 48;
            Color rc = new Color(rip.Color.R, rip.Color.G, rip.Color.B, (byte)(rip.Alpha * 255f * _transitionFade));
            for (int s = 0; s < segs; s++)
            {
                float a1 = (float)s / segs * MathF.PI * 2f;
                float a2 = (float)(s + 1) / segs * MathF.PI * 2f;
                float rx1 = rip.CenterX + rip.Radius * MathF.Cos(a1);
                float ry1 = rip.CenterY + rip.Radius * MathF.Sin(a1);
                float rx2 = rip.CenterX + rip.Radius * MathF.Cos(a2);
                float ry2 = rip.CenterY + rip.Radius * MathF.Sin(a2);
                renderer.DrawLine(rx1, ry1, rx2, ry2, rc);
            }
        }

        // 4. Draw particles (for loops)
        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Active) continue;
            var p = _particles[i];
            Color pc = new Color(p.Color.R, p.Color.G, p.Color.B, (byte)(p.Alpha * 255f * _transitionFade));
            renderer.FillRect(new Rect(p.X - p.Size * 0.5f, p.Y - p.Size * 0.5f, p.Size, p.Size), pc);
        }

        // 5. Draw logo
        if (_cachedLogo != null && _logoAlpha > 0.01f && _transitionFade > 0.01f)
        {
            float size = App.Height * _logoScale;
            float w = size;
            float h = size;
            float x = (App.Width - w) * 0.5f;
            float y = (App.Height - h) * 0.5f;

            unsafe
            {
                if (_cachedLogo.Handle != IntPtr.Zero)
                {
                    byte alphaMod = (byte)(_logoAlpha * 255f * _transitionFade);
                    SDL3.SDL_SetTextureAlphaMod((SDL_Texture*)_cachedLogo.Handle, alphaMod);
                }
            }

            renderer.DrawTexture(_cachedLogo, new Rect(x, y, w, h));
            
            unsafe
            {
                if (_cachedLogo.Handle != IntPtr.Zero)
                {
                    SDL3.SDL_SetTextureAlphaMod((SDL_Texture*)_cachedLogo.Handle, 255);
                }
            }
        }

        // 6. Outro swipe diagonal bands
        if (_swipeTriggered)
        {
            int numBands = 6;
            float bandW = 420f;
            float angle = 35f;
            
            for (int i = 0; i < numBands; i++)
            {
                float delay = i * 0.16f;
                float progress = Math.Clamp((_swipeProgress - delay) / (1f - delay), 0f, 1f);
                if (progress <= 0f) continue;
                
                float startX = -800f;
                float endX = App.Width + 800f;
                float currentX = startX + progress * (endX - startX);
                
                renderer.FillRectRotated(
                    new Rect(currentX - bandW * 0.5f, App.Height * 0.5f - 2000f, bandW, 4000f),
                    new Color(_accentColor.R, _accentColor.G, _accentColor.B, 255),
                    angle,
                    currentX,
                    App.Height * 0.5f
                );
            }
        }
    }
}
