using System;
using System.Collections.Generic;
using System.Reflection;
using Rei2D;
using Rei2D.Audio;
using Rei2D.Rendering;
using Rei2D.Tween;
using Rei2D.Input;
using SDL;

namespace Matrix.CoreGame;

public class ExitTransition : Element
{
    private enum ExitState
    {
        Inactive,
        Holding,
        Transitioning
    }

    private ExitState _state = ExitState.Inactive;
    private float _holdTime = 0f;
    private const float TargetHoldTime = 1.0f; // Must hold for 1 second

    private float _exitProgress = 0f;
    private float _dimProgress = 0f;
    
    private bool _escapeHeldThisFrame = false;
    private readonly Tween _exitTween;

    // Outro visuals matching the intro greeting
    private float _logoScale = 0.05f;
    private float _logoAlpha = 0f;
    private float _amplitude = 0f;
    private float _gridPulse = 0f;

    // Fixed-size pre-allocated pools to eliminate GC stutters
    private readonly ExitParticle[] _particles = new ExitParticle[128];
    private readonly ExitRipple[] _ripples = new ExitRipple[16];

    public struct ExitParticle
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

    public struct ExitRipple
    {
        public bool Active;
        public float CenterX, CenterY;
        public float Radius;
        public float MaxRadius;
        public float Alpha;
        public float Thickness;
        public Color Color;
    }

    public ExitTransition()
    {
        Size = Size2D.Full;
        Position = new Position2D(0f, 0f);
        Anchor = Anchor2D.TopLeft;
        InterceptsMouse = false; // Allow underlying clicks when not active

        // Register KeyPressed callback for Escape
        Input.KeyPressed(Keys.Escape, dt =>
        {
            _escapeHeldThisFrame = true;
            OnEscapeHeld(dt);
        });

        // Tween to handle exit animation (fades in logo and solid background over 1.4 seconds)
        _exitTween = new Tween(0f, 1f, 1.4f, v =>
        {
            _exitProgress = v;
            
            // Logo shrinks from base scale 0.35f down to 0.05f
            _logoScale = 0.35f - v * 0.30f;
            _logoAlpha = v;

            // Fade out the current game audio
            if (Program.currentTrack != null)
            {
                Program.currentTrack.Volume = Math.Clamp(1f - v, 0f, 1f);
            }
        }, Easing.Exponential, EasingDirection.Out, onComplete: () =>
        {
            // Exit the game
            App.Stop();
        });
    }

    private void OnEscapeHeld(float dt)
    {
        if (_state == ExitState.Transitioning) return;

        _state = ExitState.Holding;
        _holdTime = Math.Min(TargetHoldTime, _holdTime + dt);
        _dimProgress = Math.Clamp(_holdTime / TargetHoldTime, 0f, 1f);

        // Block mouse clicks once we start holding/dimming
        InterceptsMouse = true;

        if (_holdTime >= TargetHoldTime)
        {
            TriggerExitTransition();
        }
    }

    private void TriggerExitTransition()
    {
        _state = ExitState.Transitioning;
        _exitTween.Start();
    }

    protected override void OnUpdateCore(float dt)
    {
        // Cancel detection
        if (_state == ExitState.Holding && !_escapeHeldThisFrame)
        {
            // Escape was released before transition triggered: decay hold time
            _holdTime = Math.Max(0f, _holdTime - dt * 3.5f);
            _dimProgress = Math.Clamp(_holdTime / TargetHoldTime, 0f, 1f);
            
            if (_holdTime <= 0f)
            {
                _state = ExitState.Inactive;
                InterceptsMouse = false; // Release input block
            }
        }
        
        // Reset flag for next frame
        _escapeHeldThisFrame = false;

        // Visual effects update (only active when transitioning)
        if (_state == ExitState.Transitioning)
        {
            // Simulate subtle audio reactive jiggle/particles as we exit
            _amplitude = _exitProgress * 0.25f;
            
            _gridPulse = Math.Max(0f, _gridPulse - dt * 4f);
            if (_amplitude > 0.15f && _gridPulse < _amplitude)
            {
                _gridPulse = _amplitude;
            }

            if (_amplitude > 0.12f && Random.Shared.NextSingle() < 0.20f)
            {
                int rIdx = -1;
                for (int i = 0; i < _ripples.Length; i++)
                {
                    if (!_ripples[i].Active) { rIdx = i; break; }
                }
                if (rIdx != -1)
                {
                    _ripples[rIdx] = new ExitRipple
                    {
                        Active = true,
                        CenterX = App.Width * 0.5f,
                        CenterY = App.Height * 0.5f,
                        Radius = 20f,
                        MaxRadius = 250f + _amplitude * 150f,
                        Alpha = 0.3f * _exitProgress,
                        Thickness = 1f + _amplitude * 1.5f,
                        Color = Program.ActiveAccent
                    };
                }
            }

            if (_amplitude > 0.15f && Random.Shared.NextSingle() < 0.3f)
            {
                int count = Random.Shared.Next(1, 4);
                int spawned = 0;
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (spawned >= count) break;
                    if (!_particles[i].Active)
                    {
                        float angle = Random.Shared.NextSingle() * MathF.PI * 2f;
                        float speed = Random.Shared.NextSingle() * 200f + 100f;
                        _particles[i] = new ExitParticle
                        {
                            Active = true,
                            X = App.Width * 0.5f,
                            Y = App.Height * 0.5f,
                            Vx = MathF.Cos(angle) * speed,
                            Vy = MathF.Sin(angle) * speed,
                            Size = Random.Shared.NextSingle() * 5f + 2f,
                            Alpha = 0.7f * _exitProgress,
                            Life = 0f,
                            MaxLife = Random.Shared.NextSingle() * 0.4f + 0.2f,
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
                r.Radius += dt * 400f;
                r.Alpha -= dt * 1.2f;
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
                    p.Alpha = (1f - (p.Life / p.MaxLife)) * _exitProgress;
                }
                _particles[i] = p;
            }
        }
    }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (SkipDraw || !Visible) return;
        if (_state == ExitState.Inactive) return;

        // Zero allocations: pass pre-allocated arrays directly
        items.Add(new ExitRenderItem(
            _dimProgress,
            _exitProgress,
            _logoScale,
            _logoAlpha,
            _amplitude,
            _gridPulse,
            Program.ActiveAccent,
            _particles,
            _ripples
        ));
    }
}

public class ExitRenderItem : RenderItem
{
    private readonly float _dimProgress;
    private readonly float _exitProgress;
    private readonly float _logoScale;
    private readonly float _logoAlpha;
    private readonly float _amplitude;
    private readonly float _gridPulse;
    private readonly Color _accentColor;
    private readonly ExitTransition.ExitParticle[] _particles;
    private readonly ExitTransition.ExitRipple[] _ripples;

    private static Texture? _cachedLogo;

    public ExitRenderItem(
        float dimProgress,
        float exitProgress,
        float logoScale,
        float logoAlpha,
        float amplitude,
        float gridPulse,
        Color accentColor,
        ExitTransition.ExitParticle[] particles,
        ExitTransition.ExitRipple[] ripples)
    {
        _dimProgress = dimProgress;
        _exitProgress = exitProgress;
        _logoScale = logoScale;
        _logoAlpha = logoAlpha;
        _amplitude = amplitude;
        _gridPulse = gridPulse;
        _accentColor = accentColor;
        _particles = particles;
        _ripples = ripples;
    }

    public override void Draw(IRenderer renderer)
    {
        _cachedLogo ??= renderer.LoadTexture("include/textures/logo_baru.png");

        // 1. Draw holding dim overlay (up to 70% opacity / 180 alpha)
        if (_exitProgress <= 0.01f)
        {
            byte dimAlpha = (byte)(_dimProgress * 180f);
            renderer.FillRect(new Rect(0, 0, App.Width, App.Height), new Color(0, 0, 0, dimAlpha));
            return;
        }

        // 2. Draw solid background fading in as we exit
        byte bgAlpha = (byte)(_exitProgress * 255f);
        renderer.FillRect(new Rect(0, 0, App.Width, App.Height), new Color(10, 10, 15, bgAlpha));

        // 3. Draw grid (fading in)
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
                byte dotAlpha = (byte)((0.06f + _gridPulse * 0.15f * pulseFactor) * 255f * _exitProgress);
                
                renderer.FillRect(new Rect(x - dotSize * 0.5f, y - dotSize * 0.5f, dotSize, dotSize), 
                    new Color(_accentColor.R, _accentColor.G, _accentColor.B, dotAlpha));
            }
        }

        // 4. Draw ripples
        for (int i = 0; i < _ripples.Length; i++)
        {
            if (!_ripples[i].Active || _ripples[i].Alpha <= 0f) continue;
            var rip = _ripples[i];
            int segs = 48;
            Color rc = new Color(rip.Color.R, rip.Color.G, rip.Color.B, (byte)(rip.Alpha * 255f * _exitProgress));
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

        // 5. Draw particles
        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Active) continue;
            var p = _particles[i];
            Color pc = new Color(p.Color.R, p.Color.G, p.Color.B, (byte)(p.Alpha * 255f * _exitProgress));
            renderer.FillRect(new Rect(p.X - p.Size * 0.5f, p.Y - p.Size * 0.5f, p.Size, p.Size), pc);
        }

        // 6. Draw logo (growing and fading in)
        if (_cachedLogo != null && _logoAlpha > 0.01f)
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
                    byte alphaMod = (byte)(_logoAlpha * 255f);
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
    }
}
