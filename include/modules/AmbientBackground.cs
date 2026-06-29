using System;
using System.Collections.Generic;
using System.Reflection;
using Rei2D;
using Rei2D.Rendering;
using Rei2D.Tween;
using SDL;

namespace Matrix.CoreGame;

public class AmbientBackground : Element
{
    public static AmbientBackground? Instance;

    private Texture? _bgTexture;
    private Texture? _oldBgTexture;
    private float _fadeProgress = 1f;
    private Tween? _fadeTween;
    private string? _pendingBgPath;

    public AmbientBackground()
    {
        Instance = this;
        Size = Size2D.Full;
        Position = new Position2D(0f, 0f);
        Anchor = Anchor2D.TopLeft;
        InterceptsMouse = false; // Background shouldn't block clicks
    }

    public void SetBackground(string path)
    {
        _pendingBgPath = path;
    }

    protected override void OnUpdateCore(float dt)
    {
        // Smoothly fade out old background and fade in new background
        if (_fadeTween != null)
        {
            // Tween engine updates it automatically
        }
    }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (SkipDraw || !Visible) return;

        items.Add(new AmbientBackgroundRenderItem(
            this,
            _bgTexture,
            _oldBgTexture,
            _fadeProgress,
            Program.pulseAmount,
            Program.ActiveAccent
        ));
    }

    // Resolves lazy texture loading synchronously inside the draw thread to avoid file race conditions
    internal void UpdateTextures(IRenderer renderer)
    {
        if (_pendingBgPath != null)
        {
            string newPath = _pendingBgPath;
            _pendingBgPath = null;

            if (_bgTexture != null)
            {
                _oldBgTexture = _bgTexture;
                _fadeProgress = 0f;

                _bgTexture = renderer.LoadTexture(newPath);

                // Start crossfade transition tween
                _fadeTween?.Stop();
                _fadeTween = new Tween(0f, 1f, 0.6f, v =>
                {
                    _fadeProgress = v;
                }, Easing.Linear, EasingDirection.Out, onComplete: () =>
                {
                    _oldBgTexture = null;
                });
                _fadeTween.Start();
            }
            else
            {
                _bgTexture = renderer.LoadTexture(newPath);
                _fadeProgress = 1f;
            }
        }
    }
}

public class AmbientBackgroundRenderItem : RenderItem
{
    private readonly AmbientBackground _parent;
    private readonly Texture? _bgTexture;
    private readonly Texture? _oldBgTexture;
    private readonly float _fadeProgress;
    private readonly float _pulseAmount;
    private readonly Color _accentColor;

    public AmbientBackgroundRenderItem(
        AmbientBackground parent,
        Texture? bgTexture,
        Texture? oldBgTexture,
        float fadeProgress,
        float pulseAmount,
        Color accentColor)
    {
        _parent = parent;
        _bgTexture = bgTexture;
        _oldBgTexture = oldBgTexture;
        _fadeProgress = fadeProgress;
        _pulseAmount = pulseAmount;
        _accentColor = accentColor;
    }

    private void DrawAmbientTexture(IRenderer renderer, Texture texture, float progressAlpha)
    {
        // 1. Zoom scale on the beat
        float scale = 1.0f + _pulseAmount * 0.02f; // 2% maximum beat-zoom
        float w = App.Width * scale;
        float h = App.Height * scale;
        float x = (App.Width - w) * 0.5f;
        float y = (App.Height - h) * 0.5f;

        // 2. Pulse opacity on beat
        byte alpha = (byte)(progressAlpha * (18f + _pulseAmount * 16f));
        Color tint = new Color(_accentColor.R, _accentColor.G, _accentColor.B, alpha);

        // Render modulated texture directly
        unsafe
        {
            if (texture.Handle != IntPtr.Zero)
            {
                SDL3.SDL_SetTextureColorMod((SDL_Texture*)texture.Handle, tint.R, tint.G, tint.B);
                SDL3.SDL_SetTextureAlphaMod((SDL_Texture*)texture.Handle, tint.A);
            }
        }

        renderer.DrawTexture(texture, new Rect(x, y, w, h));

        // Restore default modulation
        unsafe
        {
            if (texture.Handle != IntPtr.Zero)
            {
                SDL3.SDL_SetTextureColorMod((SDL_Texture*)texture.Handle, 255, 255, 255);
                SDL3.SDL_SetTextureAlphaMod((SDL_Texture*)texture.Handle, 255);
            }
        }
    }

    public override void Draw(IRenderer renderer)
    {
        // Load pending textures lazily on the render thread
        _parent.UpdateTextures(renderer);

        // 1. Draw solid dark background color base
        renderer.FillRect(new Rect(0, 0, App.Width, App.Height), new Color(10, 10, 15, 255));

        // 2. Draw old background texture during crossfade
        if (_oldBgTexture != null && _fadeProgress < 0.99f)
        {
            DrawAmbientTexture(renderer, _oldBgTexture, 1f - _fadeProgress);
        }

        // 3. Draw current background texture
        if (_bgTexture != null)
        {
            DrawAmbientTexture(renderer, _bgTexture, _fadeProgress);
        }

        // 4. Draw soft visualizer underglow (pulsing radial glow rings)
        if (_pulseAmount > 0.01f)
        {
            float cx = App.Width * 0.5f;
            float cy = App.Height * 0.5f;
            float baseRadius = 120f;

            if (Logo.Instance != null)
            {
                baseRadius = Logo.Instance.Bounds.Width * 0.5f * 1.1f;
                if (baseRadius < 30f) baseRadius = 120f;
            }

            for (int ring = 0; ring < 3; ring++)
            {
                float r = (baseRadius + 16f * ring) + _pulseAmount * 40f;
                byte ringAlpha = (byte)(_pulseAmount * 36f / (ring + 1));
                if (ringAlpha <= 0) continue;

                Color rc = new Color(_accentColor.R, _accentColor.G, _accentColor.B, ringAlpha);
                int segs = 24;
                for (int s = 0; s < segs; s++)
                {
                    float a1 = (float)s / segs * MathF.PI * 2f;
                    float a2 = (float)(s + 1) / segs * MathF.PI * 2f;
                    float rx1 = cx + r * MathF.Cos(a1);
                    float ry1 = cy + r * MathF.Sin(a1);
                    float rx2 = cx + r * MathF.Cos(a2);
                    float ry2 = cy + r * MathF.Sin(a2);
                    renderer.DrawLine(rx1, ry1, rx2, ry2, rc);
                }
            }
        }
    }
}
