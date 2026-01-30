// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 Yannis Watermann
// Description: Inline mixer meter visual (lightweight, editor-only).

using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;
using MusicEngineEditor.Editor;

namespace MusicEngineEditor.Controls.InlineVisuals;

public partial class MixerInlineControl : UserControl, IAnimatedVisual, ISequencerVisual
{
    private readonly List<Rectangle> _bars = new();
    private readonly List<TextBlock> _labels = new();
    private readonly Random _rng = new();

    public Sequencer? Sequencer { get; set; }

    public MixerInlineControl()
    {
        InitializeComponent();
        BuildChannels(4);
    }

    private void BuildChannels(int count)
    {
        MetersPanel.Children.Clear();
        _bars.Clear();
        _labels.Clear();

        for (int i = 0; i < count; i++)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical, Margin = new System.Windows.Thickness(4, 0, 4, 0) };
            var bar = new Rectangle
            {
                Width = 18,
                Height = 100,
                RadiusX = 3,
                RadiusY = 3,
                Fill = new LinearGradientBrush(
                    Colors.Lime, Colors.DarkGreen, 90)
            };
            var cap = new Rectangle { Width = 18, Height = 2, Fill = Brushes.White, Opacity = 0.7 };
            var label = new TextBlock
            {
                Text = $"Ch{i + 1}",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 10,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            stack.Children.Add(bar);
            stack.Children.Add(cap);
            stack.Children.Add(label);

            MetersPanel.Children.Add(stack);
            _bars.Add(bar);
            _labels.Add(label);
        }
    }

    public void OnFrame()
    {
        // If we have an engine, pull meters; otherwise simulate.
        Span<float> levels = stackalloc float[_bars.Count];
        if (!TryGetEngineMeters(levels))
        {
            for (int i = 0; i < levels.Length; i++)
            {
                // light animation so it looks alive
                levels[i] = 0.3f + (float)_rng.NextDouble() * 0.4f;
            }
        }

        for (int i = 0; i < _bars.Count; i++)
        {
            var l = Math.Clamp(levels[i], 0f, 1.1f);
            _bars[i].Height = 100 * l;
        }
    }

    private bool TryGetEngineMeters(Span<float> levels)
    {
        // Hook: if Sequencer exposes a mixer bus with ChannelStrips, sample their meters.
        // Placeholder: not yet wired to core meters; returns false to fall back to simulation.
        return false;
    }
}
