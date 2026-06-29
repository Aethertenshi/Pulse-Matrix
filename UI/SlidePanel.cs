using System;
using System.Collections.Generic;
using Rei2D;
using Rei2D.Rendering;
using Rei2D.Tween;

namespace Matrix.CoreGame;

public class SlidePanel : Container
{
    private readonly Container _panel;
    private readonly Tween _slideTween;
    private float _slideProgress;
    private bool _isOpen;
    private readonly float _panelWidth;

    public Container Panel => _panel;
    public bool IsOpen => _isOpen;
    public Action? OnOpen;
    public Action? OnClose;

    public SlidePanel(float panelWidth = 480f)
    {
        _panelWidth = panelWidth;
        Size = Size2D.Full;
        Color = Colors.Transparent;
        SkipDraw = true;
        Visible = false;

        _panel = new Container
        {
            Size = new Size2D(0f, App.Height, panelWidth, 0f),
            Anchor = Anchor2D.TopLeft,
            Position = new Position2D(0f, 0f, -panelWidth, 0f),
            Color = new Color(14, 14, 22, 150), // Semi-transparent dark tint so frosted backdrop blur shines through
        };
        Add(_panel);

        _slideTween = new Tween(0f, 1f, 0.6f, v => _slideProgress = v,
            Easing.Exponential, EasingDirection.Out);
    }

    public void Open()
    {
        _isOpen = true;
        SkipDraw = false;
        Visible = true;
        _slideTween.Stop();
        _slideTween.Restart(_slideProgress, 1f);
        OnOpen?.Invoke();
    }

    public void Close()
    {
        _isOpen = false;
        InterceptsMouse = false;
        _slideTween.Stop();
        _slideTween.Restart(_slideProgress, 0f);
    }

    public void Toggle() { if (_isOpen) Close(); else Open(); }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (SkipDraw || !Visible || _slideProgress <= 0.001f) return;

        // 1. Fullscreen dim background
        if (Color.A > 0)
        {
            items.Add(new FillRectRenderItem(Bounds, Color));
        }

        // 2. Real-time hardware backdrop blur item
        items.Add(new BackdropBlurRenderItem(_panel.Bounds, _slideProgress, blurAmount: 3f));

        // 3. Children render items (panel container & components)
        foreach (var child in _children)
            child.CollectRenderItems(items, metrics);
    }

    protected override void OnUpdateCore(float dt)
    {
        base.OnUpdateCore(dt);

        Color = new Color(0, 0, 0, (byte)(_slideProgress * 48f));
        _panel.Position = new Position2D(0f, 0f, _panelWidth * (_slideProgress - 1f), 0f);

        if (!_isOpen && _slideProgress < 0.002f)
        {
            SkipDraw = true;
            Visible = false;
            OnClose?.Invoke();
        }
    }

    protected override void OnMouseDownCore(float x, float y, MouseButton button)
    {
        if (!_panel.Bounds.Contains(x, y))
            Close();
    }
}
