using System;
using System.Collections.Generic;
using System.IO;
using Rei2D;
using Rei2D.Audio;
using Rei2D.Elements;
using Rei2D.Rendering;

namespace Matrix.CoreGame;

public static class SettingsPage
{
    private static SlidePanel? _panel;
    public static bool MetronomeEnabled = false;
    private static float _metronomeVolume = 0.8f;

    public static bool IsOpen => _panel?.IsOpen ?? false;

    public static void Initialize()
    {
        _panel = new SlidePanel(380f);
        var panel = _panel.Panel;

        // ── Header ────────────────────────────────────────────────
        var header = new Container
        {
            Size = new Size2D(1f, 0f, 0f, 44f),
            Color = new Color(14, 14, 22, 255),
            InterceptsMouse = true,
        };

        // Accent bottom line
        header.Add(new Container
        {
            Size = new Size2D(1f, 0f, 0f, 1f),
            Position = new Position2D(0f, 1f, 0f, -1f),
            Anchor = Anchor2D.BottomLeft,
            OnUpdate = (el, dt) => el.Color = Program.ActiveAccent,
        });

        header.Add(new Label
        {
            Text = "SETTINGS",
            FontName = "gsans_semib",
            FontSize = 15,
            Position = new Position2D(0f, 0.5f, 16f, 0f),
            Anchor = Anchor2D.MiddleLeft,
            OnUpdate = (el, dt) => el.Color = Program.ActiveAccent,
        });

        var closeBtn = new Button
        {
            Text = "\u2715",
            FontName = "emoji",
            FontSize = 14,
            Size = new Size2D(0f, 0f, 30f, 26f),
            Position = new Position2D(1f, 0.5f, -12f, 0f),
            Anchor = Anchor2D.MiddleRight,
            Color = new Color(35, 35, 45, 255),
            HoverColor = new Color(60, 60, 70, 255),
            PressedColor = new Color(90, 90, 100, 255),
            TextColor = new Color(200, 200, 210, 255),
            BorderThickness = 0,
        };
        closeBtn.OnClick += () => _panel.Close();
        header.Add(closeBtn);
        panel.Add(header);

        // ── Main scroll area ───────────────────────────────────────
        var scroll = new ScrollFrame
        {
            Direction = LayoutDirection.Vertical,
            Spacing = 8,
            Padding = 8,
            Position = new Position2D(0f, 0f, 0f, 44f),
            Size = new Size2D(1f, 1f, 0f, -44f),
            ScrollSmoothness = 10f,
            Color = Colors.Transparent,
        };

        // ── AUDIO card ─────────────────────────────────────────────
        var masterVolumeSlider = new Slider { Value = Audio.MasterVolume };
        masterVolumeSlider.OnChanged += v => Audio.MasterVolume = v;

        var metronomeVolumeSlider = new Slider { Value = _metronomeVolume };
        metronomeVolumeSlider.OnChanged += v =>
        {
            _metronomeVolume = v;
            Program.metronomeTrack.Volume = v;
            Program.metronomeDownTrack.Volume = v;
        };

        var metronomeToggle = new Toggle { Value = MetronomeEnabled };
        metronomeToggle.OnChanged += v => MetronomeEnabled = v;

        scroll.Add(MakeCard("AUDIO",
            MakeSliderRow("Master Volume", masterVolumeSlider),
            MakeSliderRow("Metronome Volume", metronomeVolumeSlider),
            MakeToggleRow("Metronome", metronomeToggle)
        ));

        // ── VIDEO card ─────────────────────────────────────────────
        var fullscreenToggle = new Toggle { Value = false };
        fullscreenToggle.OnChanged += v =>
        {
            Console.WriteLine($"Fullscreen toggled: {v}");
        };

        var resDropdown = new Dropdown();
        resDropdown.Options.Add("1920x1080");
        resDropdown.Options.Add("1600x900");
        resDropdown.Options.Add("1280x720");
        resDropdown.SelectedIndex = 2; // Default 1280x720
        resDropdown.OnChanged += idx =>
        {
            Console.WriteLine($"Resolution selected: {resDropdown.Options[idx]}");
        };

        var blurDropdown = new Dropdown();
        blurDropdown.Options.Add("Max Quality (1/2 Scale)");
        blurDropdown.Options.Add("Quality (1/16 Scale)");
        blurDropdown.Options.Add("Performance (Fast Tint)");
        blurDropdown.Options.Add("Max Performance (Disabled)");

        blurDropdown.SelectedIndex = App.BlurQuality switch
        {
            BlurQuality.MaxQuality => 0,
            BlurQuality.Quality => 1,
            BlurQuality.Performance => 2,
            BlurQuality.MaxPerformance => 3,
            _ => 1
        };

        blurDropdown.OnChanged += idx =>
        {
            App.BlurQuality = idx switch
            {
                0 => BlurQuality.MaxQuality,
                1 => BlurQuality.Quality,
                2 => BlurQuality.Performance,
                3 => BlurQuality.MaxPerformance,
                _ => BlurQuality.Quality
            };
        };

        scroll.Add(MakeCard("VIDEO",
            MakeToggleRow("Fullscreen", fullscreenToggle),
            MakeDropdownRow("Resolution", resDropdown),
            MakeDropdownRow("Blur Quality", blurDropdown)
        ));

        // ── GAMEPLAY card ──────────────────────────────────────────
        var scrollSpeedSlider = new Slider { Value = 0.6f };

        scroll.Add(MakeCard("GAMEPLAY",
            MakeSliderRow("Scroll Speed", scrollSpeedSlider),
            MakeInfoRow("Key Binds", "View...")
        ));

        panel.Add(scroll);
        App.Add(_panel);
    }

    public static void Toggle() => _panel?.Toggle();

    // ── Compact Card builder ─────────────────────────────────────────
    private static Container MakeCard(string title, params Container[] items)
    {
        const float headerH = 26f;
        const float rowH = 30f;
        const float rowGap = 2f;
        const float pad = 8f;
        const float leftPad = pad + 8f; // 16px
        const float rightPad = pad;     // 8px

        int numRows = items.Length;
        float totalH = pad + headerH + numRows * rowH + (numRows - 1) * rowGap + pad;

        var card = new Container
        {
            Size = new Size2D(1f, 0f, 0f, totalH),
            OnUpdate = (el, dt) =>
            {
                Color bg = App.Background;
                el.Color = new Color(
                    (byte)Math.Max(8, Math.Min(bg.R - 20, 50)),
                    (byte)Math.Max(8, Math.Min(bg.G - 20, 50)),
                    (byte)Math.Max(8, Math.Min(bg.B - 20, 50)),
                    230);
            }
        };

        // Left accent bar (pulses with the beat!)
        var accentBar = new Container
        {
            Size = new Size2D(0f, 1f, 3.5f, 0f),
            Position = new Position2D(0f, 0f, 0f, 0f),
            OnUpdate = (el, dt) =>
            {
                el.Color = Program.ActiveAccent;
                el.Size = new Size2D(0f, 1f, 3.5f + Program.pulseAmount * 4f, 0f);
            }
        };
        card.Add(accentBar);

        // Category title
        card.Add(new Label
        {
            Text = title,
            FontName = "gsans_semib",
            FontSize = 10,
            Position = new Position2D(0f, 0f, leftPad, pad - 1f),
            OnUpdate = (el, dt) => el.Color = new Color(
                Program.ActiveAccent.R,
                Program.ActiveAccent.G,
                Program.ActiveAccent.B,
                (byte)(200 + Program.pulseAmount * 55)),
        });

        // Thin divider below title
        card.Add(new Container
        {
            Size = new Size2D(1f, 0f, -(leftPad + rightPad), 1f),
            Position = new Position2D(0f, 0f, leftPad, pad + headerH - 3f),
            Color = new Color(50, 50, 65, 120),
        });

        // Stack items cleanly using exact row height & gap math
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            float itemY = pad + headerH + i * (rowH + rowGap);
            
            item.Position = new Position2D(0f, 0f, leftPad, itemY);
            item.Size = new Size2D(1f, 0f, -(leftPad + rightPad), rowH);
            card.Add(item);
        }

        return card;
    }

    // ── Row Builders (Full internal alignment) ────────────────────
    private static Container MakeToggleRow(string label, Toggle toggle)
    {
        var row = new Container
        {
            InterceptsMouse = true,
            Color = Colors.Transparent,
        };

        row.Add(new Label
        {
            Text = label,
            FontName = "gsans_semib",
            FontSize = 13,
            Color = new Color(210, 210, 218, 255),
            Size = Size2D.Full,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        });

        toggle.Position = new Position2D(1f, 0.5f, 0f, 0f);
        toggle.Anchor = Anchor2D.MiddleRight;
        
        toggle.OnUpdate = (el, dt) =>
        {
            toggle.TrackColorOn = new Color(Program.ActiveAccent.R, Program.ActiveAccent.G, Program.ActiveAccent.B, 200);
        };
        
        row.Add(toggle);
        return row;
    }

    private static Container MakeSliderRow(string label, Slider slider, Func<float, string>? formatFunc = null)
    {
        var row = new Container
        {
            InterceptsMouse = true,
            Color = Colors.Transparent,
        };

        row.Add(new Label
        {
            Text = label,
            FontName = "gsans_semib",
            FontSize = 13,
            Color = new Color(210, 210, 218, 255),
            Size = Size2D.Full,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var valueLabel = new Label
        {
            FontName = "gsans_semib",
            FontSize = 12,
            Color = new Color(160, 160, 170, 255),
            Size = Size2D.Full,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Add(valueLabel);

        row.OnUpdate = (el, dt) =>
        {
            valueLabel.Text = formatFunc?.Invoke(slider.Value) ?? $"{(int)(slider.Value * 100)}%";
            slider.FillColor = Program.ActiveAccent;
        };

        slider.Size = new Size2D(1f, 0f, -(140f + 55f), 16f);
        slider.Position = new Position2D(0f, 0.5f, 140f, 0f);
        slider.Anchor = Anchor2D.MiddleLeft;
        row.Add(slider);

        return row;
    }

    private static Container MakeDropdownRow(string label, Dropdown dropdown)
    {
        var row = new Container
        {
            InterceptsMouse = true,
            Color = Colors.Transparent,
        };

        row.Add(new Label
        {
            Text = label,
            FontName = "gsans_semib",
            FontSize = 13,
            Color = new Color(210, 210, 218, 255),
            Size = Size2D.Full,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        });

        dropdown.FontName = "gsans_semib";
        dropdown.Size = new Size2D(0f, 0f, 130f, 26f);
        dropdown.Position = new Position2D(1f, 0.5f, 0f, 0f);
        dropdown.Anchor = Anchor2D.MiddleRight;

        dropdown.OnUpdate = (el, dt) =>
        {
            dropdown.HighlightColor = Program.ActiveAccent;
        };

        row.Add(dropdown);
        return row;
    }

    private static Container MakeInfoRow(string label, string value)
    {
        var row = new Container
        {
            InterceptsMouse = true,
            Color = Colors.Transparent,
        };

        row.Add(new Label
        {
            Text = label,
            FontName = "gsans_semib",
            FontSize = 13,
            Color = new Color(210, 210, 218, 255),
            Size = Size2D.Full,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        });

        row.Add(new Label
        {
            Text = value,
            FontName = "gsans_semib",
            FontSize = 12,
            Color = new Color(140, 140, 150, 255),
            Size = Size2D.Full,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return row;
    }
}
