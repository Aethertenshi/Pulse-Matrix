using System;
using System.Collections.Generic;
using Rei2D;
using Rei2D.Rendering;
using Rei2D.Audio;
using OsuLib;
using ManagedBass;
using ManagedBass.Fx;

namespace Matrix.CoreGame;

public class GameplayTransitionOverlay : Element
{
    public static GameplayTransitionOverlay? Instance;

    public bool IsTransitioning { get; private set; }
    private float _timer = 0f;
    private const float TransitionDuration = 2.2f;
    private OsuBeatmap? _targetBeatmap;
    private float _initialVolume = 0.5f;

    public GameplayTransitionOverlay()
    {
        Instance = this;
        Size = Size2D.Full;
        Position = new Position2D(0f, 0f);
        Anchor = Anchor2D.TopLeft;
        InterceptsMouse = false;
    }

    public void StartTransition(OsuBeatmap beatmap)
    {
        _targetBeatmap = beatmap;
        _timer = 0f;
        IsTransitioning = true;
        _initialVolume = Audio.MasterVolume;

        if (Program.currentTrack != null)
        {
            Program.currentTrack.SetLowPass(true, 2000f);
        }
    }

    protected override void OnUpdateCore(float dt)
    {
        if (!IsTransitioning || _targetBeatmap == null) return;

        _timer += dt;

        // Underwater Low-Pass Filter & Volume Fade (Single Phase: 2000Hz -> 350Hz straight)
        if (Program.currentTrack != null)
        {
            float progress = Math.Clamp(_timer / TransitionDuration, 0f, 1f);
            float cutoff = 20000f * (1f - progress) + 300f * progress;
            Program.currentTrack.SetLowPass(true, cutoff);
            Program.currentTrack.Volume = MathF.Max(0f, _initialVolume * (1f - progress));
        }

        // 3. Phase 3: Gameplay Launch Trigger & FX Cleanup
        if (_timer >= TransitionDuration)
        {
            IsTransitioning = false;

            if (Program.currentTrack != null)
            {
                Program.currentTrack.SetLowPass(false, 20000);
                Program.currentTrack.Volume = _initialVolume;
            }

            // Launch active gameplay session
            DanceModeElement.Instance?.Initialize(_targetBeatmap);
        }
    }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (!IsTransitioning || _targetBeatmap == null) return;

        float alpha = 1f;
        float cardScale = 1f;

        if (_timer < 0.4f)
        {
            float t = _timer / 0.4f;
            alpha = t;
            cardScale = 0.85f + 0.15f * MathF.Sin(t * MathF.PI * 0.5f);
        }
        else if (_timer > 1.6f)
        {
            float t = (_timer - 1.6f) / (TransitionDuration - 1.6f);
            alpha = 1f - t;
            cardScale = 1f + 0.1f * t;
        }

        items.Add(new InterstitialCardRenderItem(_targetBeatmap, Program.ActiveAccent, alpha, cardScale));
    }
}

public class InterstitialCardRenderItem : RenderItem
{
    public OsuBeatmap Beatmap;
    public Color Accent;
    public float Alpha;
    public float CardScale;

    public InterstitialCardRenderItem(OsuBeatmap beatmap, Color accent, float alpha, float cardScale)
    {
        Beatmap = beatmap;
        Accent = accent;
        Alpha = Math.Clamp(alpha, 0f, 1f);
        CardScale = cardScale;
    }

    public override void Draw(IRenderer renderer)
    {
        if (Alpha <= 0.001f) return;

        float screenMidX = App.Width * 0.5f;
        float screenMidY = App.Height * 0.5f;

        // Dark ambient backdrop during transition
        renderer.FillRect(new Rect(0, 0, App.Width, App.Height), new Color(10, 10, 15, (byte)(180 * Alpha)));

        // Centered Metadata Glass Card
        float cardW = 560f * CardScale;
        float cardH = 220f * CardScale;
        Rect cardRect = new Rect(screenMidX - cardW * 0.5f, screenMidY - cardH * 0.5f, cardW, cardH);

        // Glass Fill & Accent Border
        renderer.FillRect(cardRect, new Color(20, 20, 28, (byte)(240 * Alpha)));
        renderer.DrawRect(cardRect, new Color(Accent.R, Accent.G, Accent.B, (byte)(255 * Alpha)), 2.5f);

        // Side Glow Accent Bar
        Rect glowBar = new Rect(cardRect.X, cardRect.Y, 6f * CardScale, cardH);
        renderer.FillRect(glowBar, new Color(Accent.R, Accent.G, Accent.B, (byte)(255 * Alpha)));

        // Metadata Text Labels
        float textLeft = cardRect.X + 30f * CardScale;
        float textTop = cardRect.Y + 25f * CardScale;

        // Title Label
        Color titleColor = new Color(255, 255, 255, (byte)(255 * Alpha));
        renderer.DrawText(Beatmap.Title, textLeft, textTop, titleColor, "gsans_semib", 22f * CardScale);

        // Artist Label
        Color artistColor = new Color(170, 170, 180, (byte)(200 * Alpha));
        renderer.DrawText(Beatmap.Artist, textLeft, textTop + 34f * CardScale, artistColor, "gsans_semib", 14f * CardScale);

        // Version / Difficulty Badge Pill
        float pillTop = textTop + 75f * CardScale;
        string diffStr = $"[{Beatmap.Version}]";
        Color diffColor = new Color(Accent.R, Accent.G, Accent.B, (byte)(255 * Alpha));
        renderer.DrawText(diffStr, textLeft, pillTop, diffColor, "gsans_semib", 16f * CardScale);

        // Meta Stats Line (Star Rating | BPM | Mapper)
        string srStr = Beatmap.GetDifficulty("StarRating", Beatmap.GetDifficulty("OverallDifficulty", "5.0"));
        if (!double.TryParse(srStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double srVal))
            srVal = 5.0;
        string mapper = string.IsNullOrEmpty(Beatmap.Creator) ? "Unknown" : Beatmap.Creator;
        string metaText = $"★ {srVal:F1}   |   {Beatmap.GetBpmAt(0):0} BPM   |   👤 {mapper}";

        Color metaColor = new Color(140, 140, 150, (byte)(180 * Alpha));
        renderer.DrawText(metaText, textLeft, pillTop + 35f * CardScale, metaColor, "gsans_semib", 12f * CardScale);
    }
}
