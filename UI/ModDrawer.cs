using System;
using System.Collections.Generic;
using Rei2D;
using Rei2D.Elements;
using Rei2D.Rendering;
using Rei2D.Tween;

namespace Matrix.CoreGame;

public class ModDrawer : Container
{
    private static ModDrawer? _instance;
    public static ModDrawer Instance => _instance ??= new ModDrawer();

    private readonly Container _drawerContainer;
    private readonly Tween _slideTween;
    private float _slideProgress;
    private bool _isOpen;

    public Container Panel => _drawerContainer;
    public bool IsOpen => _isOpen;

    public ModDrawer()
    {
        Size = Size2D.Full;
        Color = Colors.Transparent;
        SkipDraw = true;
        Visible = false;
        InterceptsMouse = false;

        float drawerW = 800f;
        float drawerH = 260f;

        _drawerContainer = new Container
        {
            Size = new Size2D(0f, 0f, drawerW, drawerH),
            Anchor = Anchor2D.BottomCenter,
            Position = new Position2D(0.5f, 1f, 0f, drawerH + 100f),
            Color = new Color(16, 16, 24, 240),
            InterceptsMouse = true,
        };
        Add(_drawerContainer);

        _slideTween = new Tween(0f, 1f, 0.35f, v => _slideProgress = v,
            Easing.Exponential, EasingDirection.Out);

        RebuildContent();
    }

    public static void Toggle() => Instance.ToggleDrawer();

    public void ToggleDrawer()
    {
        if (_isOpen) Close(); else Open();
    }

    public void Open()
    {
        _isOpen = true;
        SkipDraw = false;
        Visible = true;
        InterceptsMouse = true;
        RebuildContent();
        _slideTween.Stop();
        _slideTween.Restart(_slideProgress, 1f);
    }

    public void Close()
    {
        _isOpen = false;
        InterceptsMouse = false;
        _slideTween.Stop();
        _slideTween.Restart(_slideProgress, 0f);
    }

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (SkipDraw || !Visible || _slideProgress <= 0.001f) return;

        // Real-time hardware backdrop blur under the drawer
        items.Add(new BackdropBlurRenderItem(_drawerContainer.Bounds, _slideProgress));

        // Render drawer container and components
        foreach (var child in _children)
            child.CollectRenderItems(items, metrics);
    }

    protected override void OnUpdateCore(float dt)
    {
        base.OnUpdateCore(dt);

        float drawerH = GameplayMods.DoubleTime ? 330f : 240f;
        _drawerContainer.Size = new Size2D(0f, 0f, 800f, drawerH);

        float restY = -60f; // Resting right above bottom bar
        float hiddenY = drawerH + 100f; // Offscreen

        float currentY = hiddenY + (restY - hiddenY) * _slideProgress;
        _drawerContainer.Position = new Position2D(0.5f, 1f, 0f, currentY);

        if (!_isOpen && _slideProgress < 0.002f)
        {
            SkipDraw = true;
            Visible = false;
        }
    }

    protected override void OnMouseDownCore(float x, float y, MouseButton button)
    {
        if (_isOpen && !_drawerContainer.Bounds.Contains(x, y))
            Close();
    }

    public void RebuildContent()
    {
        _drawerContainer.Clear();

        float drawerW = 800f;

        // Top vibrant accent line
        var topAccent = new Container
        {
            Size = new Size2D(1f, 0f, 0f, 3f),
            Position = new Position2D(0f, 0f, 0f, 0f),
            OnUpdate = (el, dt) => el.Color = Program.ActiveAccent
        };
        _drawerContainer.Add(topAccent);

        // Header Bar Container
        var header = new Container
        {
            Size = new Size2D(1f, 0f, 0f, 44f),
            Position = new Position2D(0f, 0f, 0f, 0f),
            InterceptsMouse = true
        };

        header.Add(new Label
        {
            Text = "GAMEPLAY MODIFIERS",
            FontName = "gsans_semib",
            FontSize = 14,
            Position = new Position2D(0f, 0.5f, 20f, 0f),
            Anchor = Anchor2D.MiddleLeft,
            OnUpdate = (el, dt) => el.Color = Program.ActiveAccent
        });

        // Score Multiplier Label
        var multLabel = new Label
        {
            FontName = "gsans_semib",
            FontSize = 12,
            Position = new Position2D(1f, 0.5f, -115f, 0f),
            Anchor = Anchor2D.MiddleRight,
            HorizontalAlignment = HorizontalAlignment.Right,
            OnUpdate = (el, dt) =>
            {
                Label l = (Label)el;
                float mult = GameplayMods.GetScoreMultiplier();
                l.Text = $"Multiplier: {mult:F2}x";
                l.Color = mult > 1.0f ? Program.ActiveAccent : (mult < 1.0f ? new Color(255, 120, 120, 255) : new Color(180, 180, 190, 255));
            }
        };
        header.Add(multLabel);

        // Reset All Button
        var resetBtn = new Button
        {
            Text = "Reset All",
            FontName = "gsans_semib",
            FontSize = 11,
            Size = new Size2D(0f, 0f, 80f, 26f),
            Position = new Position2D(1f, 0.5f, -20f, 0f),
            Anchor = Anchor2D.MiddleRight,
            Color = new Color(40, 40, 55, 255),
            HoverColor = new Color(70, 70, 90, 255),
            TextColor = new Color(220, 220, 230, 255),
            BorderThickness = 0,
        };
        resetBtn.OnClick += () =>
        {
            GameplayMods.ResetAll();
            RebuildContent();
        };
        header.Add(resetBtn);

        _drawerContainer.Add(header);

        // Mod Buttons Container Grid
        var grid = new Container
        {
            Size = new Size2D(1f, 0f, -40f, 170f),
            Position = new Position2D(0f, 0f, 20f, 48f),
            InterceptsMouse = true
        };

        float chipW = 140f;
        float chipH = 44f;
        float gapX = 15f;
        float gapY = 12f;

        var mods = new (string Code, string Name, Func<bool> GetState, Action ToggleState)[]
        {
            ("DT", "Double Time", () => GameplayMods.DoubleTime, () => { GameplayMods.DoubleTime = !GameplayMods.DoubleTime; if (GameplayMods.DoubleTime) GameplayMods.Nightcore = false; }),
            ("NC", "Nightcore", () => GameplayMods.Nightcore, () => { GameplayMods.Nightcore = !GameplayMods.Nightcore; if (GameplayMods.Nightcore) GameplayMods.DoubleTime = false; }),
            ("HD", "Hidden", () => GameplayMods.Hidden, () => GameplayMods.Hidden = !GameplayMods.Hidden),
            ("HR", "Hard Rock", () => GameplayMods.HardRock, () => { GameplayMods.HardRock = !GameplayMods.HardRock; if (GameplayMods.HardRock) GameplayMods.Easy = false; }),
            ("EZ", "Easy", () => GameplayMods.Easy, () => { GameplayMods.Easy = !GameplayMods.Easy; if (GameplayMods.Easy) GameplayMods.HardRock = false; }),
            ("NF", "No Fail", () => GameplayMods.NoFail, () => GameplayMods.NoFail = !GameplayMods.NoFail),
            ("AT", "Auto", () => GameplayMods.Auto, () => GameplayMods.Auto = !GameplayMods.Auto),
        };

        float curX = 0f;
        float curY = 0f;

        for (int i = 0; i < mods.Length; i++)
        {
            var mod = mods[i];

            var chipBtn = new Button
            {
                Text = string.Empty,
                Size = new Size2D(0f, 0f, chipW, chipH),
                Position = new Position2D(0f, 0f, curX, curY),
                BorderThickness = 0,
                InterceptsMouse = true,
            };

            chipBtn.OnUpdate = (el, dt) =>
            {
                bool active = mod.GetState();
                Color baseCol = active ? new Color(Program.ActiveAccent.R, Program.ActiveAccent.G, Program.ActiveAccent.B, 70) : new Color(32, 32, 45, 230);
                chipBtn.Color = baseCol;
                chipBtn.HoverColor = active ? new Color(Program.ActiveAccent.R, Program.ActiveAccent.G, Program.ActiveAccent.B, 110) : new Color(55, 55, 75, 250);
            };

            chipBtn.OnClick += () =>
            {
                mod.ToggleState();
                RebuildContent();
            };

            // Left Code Badge (e.g. DT)
            var badge = new Container
            {
                Size = new Size2D(0f, 1f, 36f, 0f),
                Position = new Position2D(0f, 0f, 0f, 0f),
                InterceptsMouse = false,
                OnUpdate = (el, dt) =>
                {
                    bool active = mod.GetState();
                    el.Color = active ? Program.ActiveAccent : new Color(50, 50, 68, 255);
                }
            };
            badge.Add(new Label
            {
                Text = mod.Code,
                FontName = "gsans_semib",
                FontSize = 12,
                Color = Colors.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Size = Size2D.Full,
                InterceptsMouse = false,
            });
            chipBtn.Add(badge);

            // Right Mod Name Label
            chipBtn.Add(new Label
            {
                Text = mod.Name,
                FontName = "gsans_semib",
                FontSize = 11,
                Color = new Color(230, 230, 240, 255),
                Position = new Position2D(0f, 0.5f, 44f, 0f),
                Anchor = Anchor2D.MiddleLeft,
                InterceptsMouse = false,
            });

            grid.Add(chipBtn);

            curX += chipW + gapX;
            if (curX + chipW > drawerW - 40f)
            {
                curX = 0f;
                curY += chipH + gapY;
            }
        }

        _drawerContainer.Add(grid);

        // Customization Sub-Panel for active mods (e.g. Double Time)
        if (GameplayMods.DoubleTime)
        {
            var customPanel = new Container
            {
                Size = new Size2D(1f, 0f, -40f, 75f),
                Position = new Position2D(0f, 0f, 20f, 235f),
                Color = new Color(24, 24, 34, 240),
                InterceptsMouse = true,
            };

            var customBorder = new Container
            {
                Size = Size2D.Full,
                InterceptsMouse = false,
                OnUpdate = (el, dt) => el.Color = new Color(Program.ActiveAccent.R, Program.ActiveAccent.G, Program.ActiveAccent.B, 100)
            };
            customPanel.Add(customBorder);

            // Title
            customPanel.Add(new Label
            {
                Text = "DOUBLE TIME CUSTOMIZATION",
                FontName = "gsans_semib",
                FontSize = 10,
                Position = new Position2D(0f, 0f, 16f, 8f),
                InterceptsMouse = false,
                OnUpdate = (el, dt) => el.Color = Program.ActiveAccent
            });

            // Speed Slider Row
            var speedSlider = new Slider { Value = (GameplayMods.DoubleTimeSpeed - 1.0f) / 1.0f }; // 1.0x to 2.0x
            speedSlider.OnChanged += v =>
            {
                GameplayMods.DoubleTimeSpeed = 1.0f + v * 1.0f;
            };

            var speedRow = MakeSliderRow("Speed Multiplier", speedSlider, v => $"{GameplayMods.DoubleTimeSpeed:F2}x");
            speedRow.Position = new Position2D(0f, 0f, 16f, 26f);
            speedRow.Size = new Size2D(1f, 0f, -32f, 24f);
            customPanel.Add(speedRow);

            // Adjust Pitch Toggle Row
            var pitchToggle = new Toggle { Value = GameplayMods.DoubleTimeAdjustPitch };
            pitchToggle.OnChanged += v => GameplayMods.DoubleTimeAdjustPitch = v;

            var pitchRow = MakeToggleRow("Adjust Pitch", pitchToggle);
            pitchRow.Position = new Position2D(0f, 0f, 16f, 50f);
            pitchRow.Size = new Size2D(1f, 0f, -32f, 20f);
            customPanel.Add(pitchRow);

            _drawerContainer.Add(customPanel);
        }
    }

    private static Container MakeToggleRow(string label, Toggle toggle)
    {
        var row = new Container { InterceptsMouse = true, Color = Colors.Transparent };
        row.Add(new Label { Text = label, FontName = "gsans_semib", FontSize = 11, Color = new Color(210, 210, 218, 255), Size = Size2D.Full, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, InterceptsMouse = false });
        toggle.Position = new Position2D(1f, 0.5f, 0f, 0f);
        toggle.Anchor = Anchor2D.MiddleRight;
        toggle.OnUpdate = (el, dt) => toggle.TrackColorOn = Program.ActiveAccent;
        row.Add(toggle);
        return row;
    }

    private static Container MakeSliderRow(string label, Slider slider, Func<float, string> formatFunc)
    {
        var row = new Container { InterceptsMouse = true, Color = Colors.Transparent };
        row.Add(new Label { Text = label, FontName = "gsans_semib", FontSize = 11, Color = new Color(210, 210, 218, 255), Size = Size2D.Full, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, InterceptsMouse = false });
        var valueLabel = new Label { FontName = "gsans_semib", FontSize = 11, Color = new Color(160, 160, 170, 255), Size = Size2D.Full, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, InterceptsMouse = false };
        row.Add(valueLabel);
        row.OnUpdate = (el, dt) => { valueLabel.Text = formatFunc(slider.Value); slider.FillColor = Program.ActiveAccent; };
        slider.Size = new Size2D(1f, 0f, -(140f + 60f), 14f);
        slider.Position = new Position2D(0f, 0.5f, 140f, 0f);
        slider.Anchor = Anchor2D.MiddleLeft;
        row.Add(slider);
        return row;
    }
}
