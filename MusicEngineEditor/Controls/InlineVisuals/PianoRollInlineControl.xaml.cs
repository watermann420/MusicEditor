// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026
// Description: Lightweight piano roll glow view for inline code visuals.

using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;
using MusicEngineEditor.Editor;

namespace MusicEngineEditor.Controls.InlineVisuals;

public partial class PianoRollInlineControl : UserControl, IAnimatedVisual, INoteReactive, ISequencerVisual
{
    private const int MinNote = 36; // C2
    private const int MaxNote = 84; // C6
    private readonly Dictionary<int, Rectangle> _activeRects = new();
    private readonly Queue<MusicalEvent> _recent = new();
    private readonly Random _rng = new();

    public Sequencer? Sequencer { get; set; }

    public PianoRollInlineControl()
    {
        InitializeComponent();
    }

    public void OnNoteOn(MusicalEvent e)
    {
        var rect = new Rectangle
        {
            Width = 16 + _rng.Next(12),
            Height = 12,
            RadiusX = 3,
            RadiusY = 3,
            Fill = new SolidColorBrush(Color.FromRgb((byte)_rng.Next(80, 200), 180, 255)),
            Opacity = 0.9
        };

        double x = RollCanvas.ActualWidth * 0.1 + _rng.NextDouble() * RollCanvas.ActualWidth * 0.8;
        double y = NoteToY(e.Note);

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        RollCanvas.Children.Add(rect);
        _activeRects[e.Note] = rect;

        _recent.Enqueue(e);
        while (_recent.Count > 64) _recent.Dequeue();
    }

    public void OnNoteOff(MusicalEvent e)
    {
        if (_activeRects.TryGetValue(e.Note, out var rect))
        {
            rect.Opacity = 0.2;
            _activeRects.Remove(e.Note);
        }
    }

    public void OnFrame()
    {
        // Fade out old rectangles
        for (int i = RollCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (RollCanvas.Children[i] is Rectangle r)
            {
                r.Opacity -= 0.02;
                if (r.Opacity <= 0.05)
                {
                    RollCanvas.Children.RemoveAt(i);
                }
            }
        }
    }

    private double NoteToY(int note)
    {
        note = Math.Clamp(note, MinNote, MaxNote);
        double range = MaxNote - MinNote;
        double norm = (note - MinNote) / range;
        return (1 - norm) * (RollCanvas.ActualHeight - 12);
    }
}
