using Rei2D;
using Rei2D.Audio;
using Rei2D.Tween;
using Rei2D.Input;
using Rei2D.Rhythm;
using OsuLib;
using Rei2D.Elements;
using Rei2D.Theme;

namespace Matrix.CoreGame
{
    public static class Program
    {
        public static Track metronomeTrack = Audio.Load("include/audios/metronome-tick.wav"); 
        public static Track metronomeDownTrack = Audio.Load("include/audios/metronome-tick-downbeat.wav"); 
        public static Track? currentTrack = null;
        public static OsuBeatmap? currentBeatmap = null;
        public static int lastBeatIndex = -1;
        public static float pulseAmount = 0f;
        public static bool logoHovered = false;
        public static bool isInMenu = false;
        public static bool isChangingCover = false;
        public static BorderedImage? bgImage = null;
        public static Image? arrowImage = null;
        public static Container? _bottombar;
        public static Label? _titleLabel;
        public static Label? _artistLabel;
        public static double beatOffsetMs = 50.0;
        public static Tween? pulseAttack;
        public static Tween? pulseRelease;
        public static Tween? arrowTween;
        public static Tween? menuTween;
        public static Position2D LogoShakeOffset = new Position2D(0f, 0f);
        public static Color ActiveAccent = new Color(110, 180, 255);
        public static float coverDriftTimer;
        public static float coverOffsetX;
        public static float coverOffsetY;

        public struct CascadeWave
        {
            public float Timer;
            public int StartIndex;
        }

        static List<Button>? _scrollButtons;
        static int[]? _setButtonOffsets;
        static readonly List<CascadeWave> _cascadeWaves = new(); // each entry captures its own fixed start index
        static int _visibleStart;
        private static ScrollFrame? _activeScrollFrame;
        private static readonly Dictionary<int, float> _setScrollPositions = new();
        private static bool isBgHovered = false;

        public static void RefreshBeatmaps()
        {
            OsuScanner scanner = new();
            IReadOnlyList<OsuBeatmap> beatmaps = scanner.ScanAll(@"include/playlists", metadataOnly: true);

            int targetIndex = -1;
            if (_activeScrollFrame != null)
            {
                for (int i = 0; i < App.Root.Children.Count; i++)
                {
                    if (App.Root.Children[i] == _activeScrollFrame)
                    {
                        targetIndex = i;
                        break;
                    }
                }
                App.Root.RemoveChild(_activeScrollFrame);
                _activeScrollFrame = null;
            }

            if (targetIndex >= 0)
                ScrollList(beatmaps, targetIndex);
            else
                ScrollList(beatmaps);
        }

        public static BorderedImage InitiateLazyScan(IReadOnlyList<OsuBeatmap> beatmaps, bool playImmediately = true)
        {
            Random rng = Random.Shared;
            var sets = beatmaps.GroupBy(b => b.BeatmapSetId).ToList();
            IGrouping<int, OsuBeatmap> set = sets[rng.Next(sets.Count)];
            OsuBeatmap chosen = set.ElementAt(rng.Next(set.Count()));

            string bgPath = chosen.GetBackgroundFullPath();
            ActiveAccent = ThemeManager.Instance.GetAccent(chosen.BeatmapSetId.ToString(), bgPath);

            string? folder = Path.GetDirectoryName(chosen.FilePath);
            string? audioPath = Path.Combine(folder!, chosen.AudioFilename);

            Console.WriteLine($"Now playing: {chosen.Artist} - {chosen.Title} [{chosen.Version}]");
            
            currentBeatmap = chosen;
            currentTrack = Audio.Load(audioPath);
            currentTrack.Position = chosen.PreviewTime / 1000f;
            if (playImmediately)
                currentTrack.Play();

            bgImage = new BorderedImage
            {
                Path = chosen.GetBackgroundFullPath(),
                Position = new Position2D(0.5f, 0.5f),
                Size = new Size2D(1f, 1f),
                Anchor = Anchor2D.Center,
                Stretch = StretchMode.Cover,
                BorderColor = ActiveAccent,
                BorderThickness = 5f,
                OpacityFunc = () => menuTween?.CurrentValue ?? 0f,
                OnHover = (_) => { if (!isBgHovered) { isBgHovered = true; FocusSelectedSong(); } },
                OnHoverLeave = (_) => { isBgHovered = false; },
                OnUpdate = (image, dt) =>
                {
                    coverDriftTimer += dt * 2.2f;
                    coverOffsetX = MathF.Sin(coverDriftTimer * 1.8f) * 4f + MathF.Cos(coverDriftTimer * 3.4f) * 2f;
                    coverOffsetY = MathF.Cos(coverDriftTimer * 2.1f) * 3.5f + MathF.Sin(coverDriftTimer * 4.2f) * 2.5f;

                    float bgPulsePx = pulseAmount * 18f;
                    float extraSize = 30f * (1f - (menuTween?.CurrentValue ?? 0f)) + bgPulsePx;
                    image.Size = new Size2D(1f, 1f, extraSize, extraSize);

                    if (isChangingCover) return;
                    if (menuTween == null)  return;
                    image.Rotation = 10f * menuTween.CurrentValue;
                    image.Position = new Position2D(0.5f, 0.5f, coverOffsetX, coverOffsetY) + (new Position2D(0, 0, -App.Width / 2, 0) * menuTween.CurrentValue);
                }
            };
            return bgImage;
        }
        public static void TransitionCover(string newPath)
        {
            if (isChangingCover || bgImage == null) return;
            isChangingCover = true;

            float menuXoff = bgImage.Position.Xoffset;
            float menuYoff = bgImage.Position.Yoffset;

            var newBg = new BorderedImage
            {
                Path = newPath,
                Size = new Size2D(1f, 1f),
                Anchor = Anchor2D.Center,
                Position = new Position2D(0.5f, 0.5f, menuXoff - App.Width, menuYoff),
                Rotation = 10f,
                Stretch = StretchMode.Cover,
                BorderColor = ActiveAccent,
                BorderThickness = 5f,
                OpacityFunc = () => menuTween?.CurrentValue ?? 0f,
                OnHover = (_) => { if (!isBgHovered) { isBgHovered = true; FocusSelectedSong(); } },
                OnHoverLeave = (_) => { isBgHovered = false; },
                OnUpdate = (image, dt) =>
                {
                    coverDriftTimer += dt * 2.2f;
                    coverOffsetX = MathF.Sin(coverDriftTimer * 1.8f) * 4f + MathF.Cos(coverDriftTimer * 3.4f) * 2f;
                    coverOffsetY = MathF.Cos(coverDriftTimer * 2.1f) * 3.5f + MathF.Sin(coverDriftTimer * 4.2f) * 2.5f;

                    float bgPulsePx = pulseAmount * 18f;
                    float extraSize = 30f * (1f - (menuTween?.CurrentValue ?? 0f)) + bgPulsePx;
                    image.Size = new Size2D(1f, 1f, extraSize, extraSize);

                    if (isChangingCover) return;
                    if (menuTween == null)  return;
                    image.Rotation = 10f * menuTween.CurrentValue;
                    image.Position = new Position2D(0.5f, 0.5f, coverOffsetX, coverOffsetY) + (new Position2D(0, 0, -App.Width / 2, 0) * menuTween.CurrentValue);
                }
            };

            var oldBg = bgImage;
            App.Root.RemoveChild(oldBg);
            App.Root.Insert(1, newBg);
            App.Root.Insert(1, oldBg);

            var outTween = new Tween(0f, 1f, 0.45f, v =>
            {
                oldBg.Position = new Position2D(0.5f, 0.5f, menuXoff - v * App.Width, menuYoff);
            }, Easing.Exponential, EasingDirection.In);

            var inTween = new Tween(0f, 1f, 0.45f, v =>
            {
                newBg.Position = new Position2D(0.5f, 0.5f, menuXoff - App.Width + v * App.Width, menuYoff);
            }, Easing.Exponential, EasingDirection.Out, onComplete: () =>
            {
                App.Root.RemoveChild(oldBg);
                bgImage = newBg;
                isChangingCover = false;
            });

            outTween.Start();
            inTween.Start();
        }

        public static void Bars()
        {
            var topbar = new Container
            {
                Position = new Position2D(0, 0, 0, 0),
                Anchor = Anchor2D.TopLeft,
                Size = new Size2D(1f, 0, 0, 52),
                Color = new Color(12, 12, 18, 220),
                OnUpdate = (el, dt) =>
                {
                    if (menuTween == null)  return;
                    float y = -52f * (1f - menuTween.CurrentValue);
                    el.Position = new Position2D(0, 0, 0, y);
                }
            };

            var topAccentLine = new Container
            {
                Position = new Position2D(0, 1, 0, -3),
                Anchor = Anchor2D.BottomLeft,
                Size = new Size2D(1f, 0, 0, 3),
                OnUpdate = (el, dt) =>
                {
                    el.Color = ActiveAccent;
                }
            };
            topbar.Add(topAccentLine);

            var settingsBtn = new Button
            {
                Text = "\u2699",
                FontName = "emoji",
                FontSize = 18,
                Size = new Size2D(0f, 0f, 34f, 30f),
                Position = new Position2D(1f, 0.5f, -14f, 0f),
                Anchor = Anchor2D.MiddleRight,
                Color = Colors.Transparent,
                HoverColor = new Color(55, 55, 70, 255),
                PressedColor = new Color(80, 80, 100, 255),
                TextColor = new Color(190, 190, 200, 255),
                BorderThickness = 0,
            };
            settingsBtn.OnClick += () => SettingsPage.Toggle();
            topbar.Add(settingsBtn);

            _bottombar = new Container
            {
                Position = new Position2D(0, 1, 0, 0),
                Anchor = Anchor2D.BottomLeft,
                Size = new Size2D(1f, 0, 0, 62),
                Color = new Color(12, 12, 18, 220),
                OnUpdate = (el, dt) =>
                {
                    if (menuTween == null)  return;
                    float y = 62f * (1f - menuTween.CurrentValue);
                    el.Position = new Position2D(0, 1, 0, y);
                }
            };

            var bottomAccentLine = new Container
            {
                Position = new Position2D(0, 0, 0, 0),
                Anchor = Anchor2D.TopLeft,
                Size = new Size2D(1f, 0, 0, 3),
                OnUpdate = (el, dt) =>
                {
                    el.Color = ActiveAccent;
                }
            };
            _bottombar.Add(bottomAccentLine);

            _titleLabel = new Label
            {
                Text = currentBeatmap?.Title ?? "",
                FontName = "gsans_semib",
                FontSize = 26,
                Position = new Position2D(0, 0, 24, 10),
                OnUpdate = (el, dt) =>
                {
                    el.Color = new Color(245, 245, 250, 255);
                }
            };

            _artistLabel = new Label
            {
                Text = currentBeatmap?.Artist ?? "",
                FontName = "gsans_semib",
                FontSize = 16,
                Position = new Position2D(0, 0, 24, 35),
                OnUpdate = (el, dt) =>
                {
                    el.Color = Color.Lerp(new Color(160, 160, 170, 255), ActiveAccent, 0.25f);
                }
            };

            var modsBtn = new Button
            {
                Text = "MODS",
                FontName = "gsans_semib",
                FontSize = 13,
                Size = new Size2D(0, 0, 90, 36),
                Position = new Position2D(1, 0.5f, -24, 0),
                Anchor = Anchor2D.MiddleRight,
                Color = new Color(25, 25, 35, 255),
                HoverColor = new Color(50, 50, 65, 255),
                PressedColor = new Color(70, 70, 85, 255),
                TextColor = new Color(220, 220, 230, 255),
                BorderThickness = 0,
            };
            modsBtn.OnClick += () => ModDrawer.Toggle();
            _bottombar.Add(modsBtn);

            _bottombar.Add(_titleLabel);
            _bottombar.Add(_artistLabel);

            App.Add(topbar);
            App.Add(_bottombar);
        }

        public static void LogoHeartbeat()
        {
            if (currentTrack == null || currentBeatmap == null || Logo.Instance == null) return;

            double positionMs = currentTrack.Position * 1000.0 + beatOffsetMs;
            var tp = currentBeatmap.ControlPoints.TimingPointAt(positionMs);

            double beatLength = tp.BeatLength;
            double relativeTime = positionMs - tp.Time;
            if (relativeTime < 0 || beatLength <= 0) return;

            int beatIndex = (int)(relativeTime / beatLength);

            if (beatIndex != lastBeatIndex)
            {
                lastBeatIndex = beatIndex;
                bool isDownbeat = beatIndex % tp.Meter == 0;
                float peak = isDownbeat ? 0.85f : 0.28f;

                VisualizerElement.Instance?.OnBeat(isDownbeat);

                pulseRelease?.Stop();
                pulseAttack?.Restart(pulseAmount, peak);

                if (arrowImage != null)
                arrowTween?.RestartTo(MathF.Round(arrowImage.Rotation / 90f) * 90f + 90f);

                if (isDownbeat && _scrollButtons != null && _cascadeWaves.Count < 32)
                    _cascadeWaves.Add(new CascadeWave { Timer = 0f, StartIndex = _visibleStart });

                if (SettingsPage.MetronomeEnabled) if (isDownbeat) metronomeDownTrack.Play(); else metronomeTrack.Play();
            }
        }
        public static void LogoArrow()
        {
            arrowImage = new Image
            {
                Path = "include/textures/arrow.png",
                Anchor = Anchor2D.BottomCenter,
                Position = new Position2D(0.5f, 0.5f),
                Size = new Size2D(0.3f, 0.3f),
                Stretch = StretchMode.Uniform,
                OnHover = (image) => { if (!logoHovered) logoHovered = true; },
                OnHoverLeave = (image) => { if (logoHovered) logoHovered = false; },
                OnUpdate = (el, dt) =>
                {
                    float t = menuTween?.CurrentValue ?? 0f;
                    bool isHidden = DanceModeElement.Instance?.IsActive == true || Matrix.CoreGame.GameplayTransitionOverlay.Instance?.IsTransitioning == true;
                    el.Color = isHidden ? new Color(255, 255, 255, 0) : new Color(255, 255, 255, (byte)((1f - t) * 255f));
                    el.SkipDraw = isHidden || t > 0.65f;
                    el.Visible = !el.SkipDraw;
                    el.Position = new Position2D(0.5f, 0.5f + t * 0.3f, 0, t * 100f);
                }
            };
            App.Add(arrowImage);
        }
        public static void ScrollList(IReadOnlyList<OsuBeatmap> beatmaps, int insertIndex = -1)
        {
            _scrollButtons = new List<Button>();
            var offsets = new List<int>();

            var sets = beatmaps.GroupBy(b => b.BeatmapSetId).ToList();

            ScrollFrame scrollFrame = new ScrollFrame
            {
                Position = new Position2D(1f, 0),
                Anchor = Anchor2D.TopRight,
                Size = new Size2D(.35f, 1f),
                Direction = LayoutDirection.Vertical,
                LayoutAlign = LayoutAlign.End,
                Spacing = 12,
                Padding = 8,
                Color = new Color(0, 255, 0, 0),
                ScrollSmoothness = 6f,
                OnUpdate = (scroll, dt) =>
                {
                    if (menuTween == null)  return;
                    scroll.Position = new Position2D(1f, 0f, App.Width / 2 * (1 - menuTween.CurrentValue), 0f);
                    // Advance all live waves and prune finished ones
                    for (int w = _cascadeWaves.Count - 1; w >= 0; w--)
                    {
                        var wave = _cascadeWaves[w];
                        wave.Timer += dt;
                        if (wave.Timer > 1.4f + 256 * 0.035f) // past the last possible button
                            _cascadeWaves.RemoveAt(w);
                        else
                            _cascadeWaves[w] = wave;
                    }
                    var vr = ((ScrollFrame)scroll).VisibleRange;
                    int rawStart = vr.Start >= 0 ? vr.Start : 0;
                    _visibleStart = rawStart < offsets.Count ? offsets[rawStart] : 0;
                }
            };

            _setScrollPositions.Clear();
            float accumY = scrollFrame.PaddingTop;

            foreach (var set in sets)
            {
                var first = set.First();
                int numDiffs = set.Count();
                Color accent = ThemeManager.Instance.GetAccent(first.BeatmapSetId.ToString(), first.GetBackgroundFullPath());

                float headerH = 38f;
                float diffH = 30f;
                float diffGap = 3f;
                float pad = 12f;
                float leftPad = pad + 8;
                float totalH = pad + headerH + numDiffs * diffH + (numDiffs - 1) * diffGap + pad;

                float targetY = accumY - (App.Height * 0.5f - totalH * 0.5f);
                _setScrollPositions[first.BeatmapSetId] = MathF.Max(0f, targetY);
                accumY += totalH + scrollFrame.Spacing;

                int firstBtnIndex = _scrollButtons.Count; // Capture first button index for this card

                var accentBar = new GlowBar
                {
                    Size = new Size2D(0, 0, 4, totalH),
                    Position = new Position2D(0, 0, 0, 0),
                    Color = accent,
                    GlowRadius = 6f,
                };

                var card = new Container
                {
                    Size = new Size2D(0.98f, 0, 0, totalH),
                    Color = new Color(25, 25, 32, 240),
                    OnUpdate = (el, dt) =>
                    {
                        // Calculate unified card physical bounce at card root entry
                        float cardWaveVal = 0f;
                        for (int wIndex = 0; wIndex < _cascadeWaves.Count; wIndex++)
                        {
                            var wave = _cascadeWaves[wIndex];
                            int offset = firstBtnIndex - wave.StartIndex;
                            if (offset >= 0)
                            {
                                float t = wave.Timer - offset * 0.035f;
                                if (t >= 0f && t < 1.4f)
                                {
                                    float x = t * 10.0f;
                                    float wf = MathF.Abs(2f * MathF.Sin(x)) * MathF.Exp(-0.25f * x);
                                    if (wf > MathF.Abs(cardWaveVal)) cardWaveVal = wf;
                                }
                            }
                        }

                        // Check if this card contains the currently active beatmap
                        bool isActive = currentBeatmap != null && currentBeatmap.BeatmapSetId == first.BeatmapSetId;

                        // Push the card leftward based on wave (with smooth plush cushion bounce)
                        float pushX = -10f * cardWaveVal;

                        // Active card beat-reactive behavior
                        if (isActive)
                        {
                            // Slide leftward on downbeat spikes
                            pushX -= 12f * pulseAmount;
                            
                            // Pulse the side accent bar thickness and neon glow on the beat
                            accentBar.Size = new Size2D(0, 0, 4f + pulseAmount * 8f, totalH);
                            accentBar.GlowRadius = 6f + pulseAmount * 14f;
                        }
                        else
                        {
                            accentBar.Size = new Size2D(0, 0, 4f, totalH);
                            accentBar.GlowRadius = 4f;
                        }

                        el.Position = new Position2D(0f, 0f, pushX, 0f);

                        // Inherit a subtle tint of the beatmap's accent color!
                        float tintFactor = isActive ? 0.22f : 0.14f;
                        byte baseR = (byte)Math.Max(12, Math.Min(App.Background.R - 15, 35));
                        byte baseG = (byte)Math.Max(12, Math.Min(App.Background.G - 15, 35));
                        byte baseB = (byte)Math.Max(16, Math.Min(App.Background.B - 15, 42));

                        el.Color = new Color(
                            (byte)(baseR * (1f - tintFactor) + accent.R * tintFactor),
                            (byte)(baseG * (1f - tintFactor) + accent.G * tintFactor),
                            (byte)(baseB * (1f - tintFactor) + accent.B * tintFactor),
                            (byte)(isActive ? 245 : 225));
                    }
                };

                // 1. Dynamic Background Watermark Image (fits the cards beautifully)
                var cardBg = new Image
                {
                    Path = first.GetBackgroundFullPath(),
                    Size = Size2D.Full,
                    Position = new Position2D(0f, 0f),
                    Anchor = Anchor2D.TopLeft,
                    Stretch = StretchMode.Cover,
                    Color = new Color(255, 255, 255, 18), // 7% opacity watermark
                    InterceptsMouse = false,
                    SkipDraw = true, // Start skipped to avoid synchronous loading before layout
                    OnUpdate = (el, dt) =>
                    {
                        // If bounds are not laid out yet, default to invisible to prevent first-frame loading
                        if (card.Bounds.Height <= 0.01f || scrollFrame.Bounds.Height <= 0.01f)
                        {
                            el.SkipDraw = true;
                            return;
                        }

                        bool isVisible = card.Bounds.Bottom >= scrollFrame.Bounds.Top && 
                                         card.Bounds.Top <= scrollFrame.Bounds.Bottom;
                        el.SkipDraw = !isVisible;
                        
                        bool isActive = currentBeatmap != null && currentBeatmap.BeatmapSetId == first.BeatmapSetId;
                        el.Color = new Color(255, 255, 255, (byte)(isActive ? (18 + pulseAmount * 24) : 18));
                    }
                };
                card.Insert(0, cardBg);

                card.Add(accentBar);

                // Title Label (Large, bold white text)
                card.Add(new Label
                {
                    Text = first.Title,
                    FontName = "gsans_semib",
                    FontSize = 16,
                    Color = Colors.White,
                    Position = new Position2D(0, 0, leftPad, pad),
                    Size = new Size2D(1f, 0, -(leftPad + pad + 235f), 20f),
                });

                // Artist Label (Smaller, low-opacity gray text below Title)
                card.Add(new Label
                {
                    Text = first.Artist,
                    FontName = "gsans_semib",
                    FontSize = 11,
                    Color = new Color(170, 170, 180, 180),
                    Position = new Position2D(0, 0, leftPad, pad + 20f),
                    Size = new Size2D(1f, 0, -(leftPad + pad), 14f),
                });

                // Meta Pill Container (BPM | Star Rating | Mapper)
                string srStr = first.GetDifficulty("StarRating", first.GetDifficulty("OverallDifficulty", "5.0"));
                if (!double.TryParse(srStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double srVal))
                    srVal = 5.0;
                string mapper = string.IsNullOrEmpty(first.Creator) ? "Unknown" : first.Creator;
                string metaText = $"{first.GetBpmAt(0):0} BPM   |   ★ {srVal:F1}   |   👤 {mapper}";
                float pillW = 225f;

                var metaPill = new Container
                {
                    Position = new Position2D(1f, 0f, -(pad + pillW + 5f), pad + 3f),
                    Size = new Size2D(0f, 0f, pillW, 22f),
                    Color = new Color(accent.R, accent.G, accent.B, 30),
                    Anchor = Anchor2D.TopLeft,
                };
                metaPill.Add(new Label
                {
                    Text = metaText,
                    FontName = "gsans_semib",
                    FontSize = 10,
                    Color = accent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Size = Size2D.Full,
                });
                card.Add(metaPill);

                offsets.Add(_scrollButtons.Count);

                int di = 0;
                foreach (var bm in set)
                {
                    var beatmap = bm;
                    float y = pad + headerH + di * (diffH + diffGap);

                    bool isBtnHovered = false;
                    float hoverProgress = 0f;

                    var btn = new Button
                    {
                        Text = string.Empty, // Empty text, layout handles it procedurally
                        Position = new Position2D(0, 0, leftPad + 4, y),
                        Size = new Size2D(1f, 0, -(leftPad + pad + 4), diffH),
                        BorderThickness = 0,
                    };

                    // Left-aligned difficulty label
                    var diffLabel = new Label
                    {
                        Text = beatmap.Version,
                        FontName = "gsans_semib",
                        FontSize = 12,
                        Color = new Color(200, 200, 210, 255),
                        Position = new Position2D(0f, 0.5f, 10f, 0f),
                        Anchor = Anchor2D.MiddleLeft,
                        VerticalAlignment = VerticalAlignment.Center,
                        InterceptsMouse = false
                    };
                    btn.Add(diffLabel);

                    // Right-aligned hit objects count label
                    var noteCountLabel = new Label
                    {
                        Text = $"{beatmap.HitObjects.Count} Notes",
                        FontName = "gsans_semib",
                        FontSize = 10,
                        Color = new Color(130, 130, 140, 255),
                        Position = new Position2D(1f, 0.5f, -10f, 0f),
                        Anchor = Anchor2D.MiddleRight,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        InterceptsMouse = false
                    };
                    btn.Add(noteCountLabel);

                    // Add a tiny vertical indicator bar on the left edge
                    var glowBar = new Container
                    {
                        Position = new Position2D(0f, 0.5f, 0f, 0f),
                        Anchor = Anchor2D.MiddleLeft,
                        Size = new Size2D(0f, 0f, 3f, 0f), // 3px wide, starts at 0 height
                        Color = accent
                    };
                    btn.Add(glowBar);

                    btn.OnHover = (_) => isBtnHovered = true;
                    btn.OnHoverLeave = (_) => isBtnHovered = false;

                    int btnIndex = _scrollButtons.Count;

                    btn.OnUpdate = (el, dt) =>
                    {
                        if (isBtnHovered)
                            hoverProgress = Math.Min(1f, hoverProgress + 10f * dt);
                        else
                            hoverProgress = Math.Max(0f, hoverProgress - 8f * dt);

                        bool isActiveBtn = currentBeatmap == beatmap;

                        // Smoothly slide to the left on hover/active
                        float slideFactor = Math.Max(hoverProgress, isActiveBtn ? 1.0f : 0f);
                        float hoverOffset = -8f * slideFactor;
                        if (isActiveBtn)
                        {
                            hoverOffset -= 6f + pulseAmount * 8f;
                        }

                        el.Position = new Position2D(0, 0, leftPad + 4 + hoverOffset, y);

                        // Scale the glow bar vertically and pulse thickness
                        if (isActiveBtn)
                        {
                            glowBar.Size = new Size2D(0f, 1f, 3f + pulseAmount * 5f, 0f);
                            glowBar.Color = new Color(accent.R, accent.G, accent.B, (byte)(200 + pulseAmount * 55));
                        }
                        else
                        {
                            glowBar.Size = new Size2D(0f, hoverProgress, 3f, 0f);
                            glowBar.Color = new Color(accent.R, accent.G, accent.B, (byte)(255f * hoverProgress));
                        }

                        Color bg = App.Background;
                        Color baseColor = new Color(
                            (byte)Math.Max(6, Math.Min(bg.R - 28, 40)),
                            (byte)Math.Max(6, Math.Min(bg.G - 28, 40)),
                            (byte)Math.Max(6, Math.Min(bg.B - 28, 40)),
                            255);

                        // Blend background with accent on hover/active
                        Color hoverBg = Color.Lerp(baseColor, new Color(accent.R, accent.G, accent.B, 255), 0.22f);
                        if (isActiveBtn)
                        {
                            baseColor = Color.Lerp(baseColor, new Color(accent.R, accent.G, accent.B, 255), 0.12f);
                            hoverBg = Color.Lerp(baseColor, new Color(accent.R, accent.G, accent.B, 255), 0.35f);
                        }

                        Color currentBg = Color.Lerp(baseColor, hoverBg, hoverProgress);

                        float f = 0f;
                        float glowF = 0f;
                        for (int wIndex = 0; wIndex < _cascadeWaves.Count; wIndex++)
                        {
                            var wave = _cascadeWaves[wIndex];
                            int offset = btnIndex - wave.StartIndex;
                            if (offset >= 0)
                            {
                                float t = wave.Timer - offset * 0.035f;
                                if (t >= 0f && t < 1.4f)
                                {
                                    float x = t * 10.0f;
                                    float wf = MathF.Abs(2f * MathF.Sin(x)) * MathF.Exp(-0.25f * x) * 0.2f;
                                    if (wf > f) f = wf;
                                }
                                // Single traveling wave glow front per downbeat (initial wavefront)
                                if (t >= 0f && t < 0.35f)
                                {
                                    float xGlow = t * (MathF.PI / 0.35f);
                                    float gwf = MathF.Sin(xGlow) * 0.45f;
                                    if (gwf > glowF) glowF = gwf;
                                }
                            }
                        }

                        // Brighten text and badges on hover/active
                        float activeFactor = isActiveBtn ? 1.0f : hoverProgress;
                        float textFactor = Math.Clamp(activeFactor + f * 1.5f, 0f, 1f);
                        diffLabel.Color = Color.Lerp(new Color(200, 200, 210, 255), Colors.White, textFactor);
                        noteCountLabel.Color = Color.Lerp(new Color(130, 130, 140, 255), new Color(accent.R, accent.G, accent.B, 255), activeFactor);

                        btn.Color = currentBg;
                        btn.HoverColor = currentBg;
                        btn.PressedColor = Color.Lerp(currentBg, Colors.White, 0.15f);

                        if (glowF > 0.01f)
                        {
                            // Smoothly blend background with the card's accent color during initial wave glow front
                            btn.Color = new Color(
                                (byte)(currentBg.R + (accent.R - currentBg.R) * glowF),
                                (byte)(currentBg.G + (accent.G - currentBg.G) * glowF),
                                (byte)(currentBg.B + (accent.B - currentBg.B) * glowF),
                                255);
                        }
                    };

                    _scrollButtons.Add(btn);

                    btn.OnClick += () =>
                    {
                        string selectedBgPath = beatmap.GetBackgroundFullPath();
                        ActiveAccent = ThemeManager.Instance.GetAccent(beatmap.BeatmapSetId.ToString(), selectedBgPath);
                        if (bgImage != null) bgImage.BorderColor = ActiveAccent;

                        if (currentBeatmap == beatmap)
                        {
                            StartGameplay(beatmap);
                            return;
                        }

                        if (currentBeatmap != null && currentBeatmap.BeatmapSetId == beatmap.BeatmapSetId)
                        {
                            string oldBg = currentBeatmap.GetBackgroundFullPath();
                            string newBgPath = beatmap.GetBackgroundFullPath();
                            currentBeatmap = beatmap;
                            lastBeatIndex = -1;
                            if (_titleLabel != null) _titleLabel.Text = beatmap.Title;
                            if (_artistLabel != null) _artistLabel.Text = beatmap.Artist;

                            if (oldBg != newBgPath)
                            {
                                TransitionCover(newBgPath);
                                AmbientBackground.Instance?.SetBackground(newBgPath);
                            }
                            return;
                        }

                        string? folder = Path.GetDirectoryName(beatmap.FilePath);
                        string audioPath = Path.Combine(folder!, beatmap.AudioFilename);

                        if (currentTrack != null)
                            currentTrack.OnPlaying -= LogoHeartbeat;

                        var oldTrack = currentTrack;
                        currentTrack = Audio.Load(audioPath);
                        currentTrack.Position = beatmap.PreviewTime / 1000f;
                        currentTrack.OnPlaying += LogoHeartbeat;

                        Audio.Crossfade(oldTrack, currentTrack, 0.5f);

                        currentBeatmap = beatmap;
                        lastBeatIndex = -1;

                        string bgPath = beatmap.GetBackgroundFullPath();
                        App.Spawn(() =>
                        {
                            var avg = new Image { Path = bgPath }.GetAverageColor();
                            App.Defer(() => App.Background = avg);
                        });
                        TransitionCover(bgPath);
                        AmbientBackground.Instance?.SetBackground(bgPath);

                        if (_titleLabel != null) _titleLabel.Text = beatmap.Title;
                        if (_artistLabel != null) _artistLabel.Text = beatmap.Artist;

                        Console.WriteLine($"Now playing: {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}]");
                    };

                    card.Add(btn);
                    di++;
                }

                scrollFrame.Add(card);
            }

            _setButtonOffsets = offsets.ToArray();
            _activeScrollFrame = scrollFrame;
            if (insertIndex >= 0)
                App.Root.Insert(insertIndex, scrollFrame);
            else
                App.Add(scrollFrame);
        }

        public static void StartGameplay(OsuBeatmap beatmap)
        {
            if (isChangingCover || beatmap == null) return;

            Console.WriteLine($"[Transition] Entering Interstitial Transition for: {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}]");

            // Smoothly retract menu panels (song list, bars, logo)
            if (menuTween != null)
            {
                menuTween.Stop();
                menuTween.Restart(menuTween.CurrentValue, 0f);
                isInMenu = false;
            }

            // Start high-end underwater audio fade + centered metadata transition overlay
            GameplayTransitionOverlay.Instance?.StartTransition(beatmap);
        }

        public static void ReturnToMenuFromGameplay()
        {
            if (menuTween != null)
            {
                menuTween.Stop();
                menuTween.Restart(menuTween.CurrentValue, 1f);
                isInMenu = true;
            }

            if (currentTrack != null)
            {
                currentTrack.Play();
            }
        }

        public static void FocusSelectedSong()
        {
            if (_activeScrollFrame == null || currentBeatmap == null) return;
            if (_setScrollPositions.TryGetValue(currentBeatmap.BeatmapSetId, out float targetY))
            {
                _activeScrollFrame.TargetScrollY = targetY;
            }
        }

        public static void Main()
        {
            // Initialize the game
            App.Title = "Matrix";
            App.Width = 1920;
            App.Height = 1080;
            App.Window = App.WindowType.Windowed;
            Audio.MasterVolume = 0.5f;

            OszDropHandler.Initialize();

            App.LoadFont("gsans_semib","include/fonts/GoogleSans-SemiBold.ttf");
            App.LoadFont("emoji", "include/fonts/OpenMoji-black-glyf.ttf");

            // Initialize scanner
            OsuScanner scanner = new();
            IReadOnlyList<OsuBeatmap> beatmaps = scanner.ScanAll(@"include/playlists", metadataOnly: true);

            App.Root.Insert(0, new AmbientBackground());
            App.Add(InitiateLazyScan(beatmaps, playImmediately: false));
            if (currentBeatmap != null)
            {
                AmbientBackground.Instance?.SetBackground(currentBeatmap.GetBackgroundFullPath());
            }

            VisualizerElement.Instance = new VisualizerElement();
            App.Add(VisualizerElement.Instance);
            App.Add(new DanceModeElement());
            App.Add(new GameplayTransitionOverlay());

            ScrollList(beatmaps);
            Bars();
            Logo.Draw(() => menuTween?.CurrentValue ?? 0f, () => pulseAmount);
            LogoArrow();
            SettingsPage.Initialize();
            App.Add(new GreetingTransition(beatmaps));
            App.Add(new ExitTransition());

            if (bgImage != null)
            {
                var bg = bgImage;
                App.Spawn(() =>
                {
                    var avg = bg.GetAverageColor();
                    App.Defer(() => App.Background = avg);
                });
            }

            pulseAttack = new Tween(0f, 1f, 0.06f, v => pulseAmount = v, Easing.Cubic, EasingDirection.Out,
                onComplete: () => pulseRelease?.Restart(pulseAmount, 0f));
            pulseRelease = new Tween(1f, 0f, 0.35f, v => pulseAmount = v, Easing.Cubic, EasingDirection.Out);
            arrowTween = new Tween(0f, 90f, 0.55f, v => { if (arrowImage != null) arrowImage.Rotation = v; }, Easing.Exponential, EasingDirection.Out);
            menuTween = new Tween(0f, 1f, 1.5f, v => { }, Easing.Exponential, EasingDirection.Out);

            Input.KeyClicked(Keys.F1, () => DebugOverlay.Instance.Toggle());
            Input.KeyClicked(Keys.F2, () => DebugOverlay.Instance.ResetStats());
            Input.KeyClicked(Keys.F3, () => SettingsPage.Toggle());
            Input.KeyClicked(Keys.F4, () => ModDrawer.Toggle());
            Input.KeyClicked(Keys.Space, () => { if (isChangingCover || DanceModeElement.Instance?.IsActive == true) return; menuTween.Stop(); menuTween.Restart(menuTween.CurrentValue, isInMenu? 0f: 1f); isInMenu = !isInMenu; });
            Input.KeyClicked(Keys.Return, () => { if (isInMenu && currentBeatmap != null && DanceModeElement.Instance?.IsActive != true) StartGameplay(currentBeatmap); });
            if (currentTrack != null)
                currentTrack.OnPlaying += LogoHeartbeat;

            App.Add(ModDrawer.Instance);
            App.Add(DebugOverlay.Instance);

            if (bgImage != null)
            {
                var bg = bgImage;
                App.Defer(() =>
                {
                    bg.Texture ??= App.Renderer?.LoadTexture(bg.Path);
                });
            }

            App.SetFPS(144);
            App.SetInputRate(240);
            Input.Initialize(300);
            App.Run();
        }
    }

    public static class Logo
    {
        public static Image? Instance;
        public const float BaseScale = 0.35f;

        public static void Draw(Func<float> menuValue, Func<float> pulseValue)
        {
            Instance = new Image
            {
                Path = "include/textures/logo_baru.png",
                Anchor = Anchor2D.Center,
                Position = new Position2D(0.5f, 0.5f),
                Size = new Size2D(BaseScale, BaseScale),
                Stretch = StretchMode.Uniform,
                OnUpdate = (el, dt) =>
                {
                    float t = menuValue();
                    float pulse = pulseValue();
                    el.Position = new Position2D(0.5f, 0.5f + t * 0.5f, 0, -t * 35f) + Program.LogoShakeOffset;
                    float s = BaseScale * (1f - t * 0.35f) + pulse * 0.042f;
                    el.Size = new Size2D(s, s);

                    bool isHidden = DanceModeElement.Instance?.IsActive == true || Matrix.CoreGame.GameplayTransitionOverlay.Instance?.IsTransitioning == true;
                    el.Color = isHidden ? new Color(255, 255, 255, 0) : Colors.White;
                    el.SkipDraw = isHidden;
                    el.Visible = !isHidden;
                }
            };
            App.Add(Instance);
        }
    }
}
