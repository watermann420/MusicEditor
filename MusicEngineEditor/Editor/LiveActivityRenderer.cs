// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Live activity glow for MIDI/parameter changes (visible lines only).

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace MusicEngineEditor.Editor;

internal sealed class LiveActivityRenderer : IBackgroundRenderer, IDisposable
{
    private readonly TextEditor _editor;
    private readonly Dispatcher _dispatcher;
    private readonly List<Pulse> _pulses = new();
    private readonly List<System.Windows.Rect> _rectBuffer = new();
    private readonly Regex _midiDeviceRegex = new(@"midi\.device\s*\(\s*(?<idx>\d+)\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private bool _disposed;
    private readonly DispatcherTimer _timer;

    private const double PulseMs = 400;
    private static readonly Brush PulseBrush = new SolidColorBrush(Color.FromArgb(220, 140, 220, 255));

    static LiveActivityRenderer()
    {
        PulseBrush.Freeze();
    }

    public LiveActivityRenderer(TextEditor editor)
    {
        _editor = editor;
        _dispatcher = editor.Dispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _timer.Tick += (_, _) =>
        {
            if (_disposed) { _timer.Stop(); return; }
            Cleanup();
            if (_pulses.Count == 0) _timer.Stop();
            _editor.TextArea.TextView.InvalidateLayer(Layer);
        };
    }

    public KnownLayer Layer => KnownLayer.Selection; // draws above text background

    private bool _armed;
    private int _armedDevice = -1;
    public void ArmDevice(int deviceIndex)
    {
        _armed = true;
        _armedDevice = deviceIndex;
    }

    public void Disarm() => _armed = false;

    public void PingMidiDevice(int deviceIndex)
    {
        if (!_armed) { _armed = true; _armedDevice = deviceIndex; }
        if (_armedDevice >= 0 && _armedDevice != deviceIndex) return;
        _dispatcher.InvokeAsync(() => AddMidiPulses(deviceIndex), DispatcherPriority.Background);
    }

    public void NoteActivity(int deviceIndex, bool isOn)
    {
        if (!_armed) { _armed = true; _armedDevice = deviceIndex; }
        if (_armedDevice >= 0 && _armedDevice != deviceIndex) return;
        _dispatcher.InvokeAsync(() =>
        {
            if (isOn)
            {
                AddMidiPulses(deviceIndex);
            }
        }, DispatcherPriority.Background);
    }

    public void HighlightRange(int startOffset, int length)
    {
        _dispatcher.InvokeAsync(() =>
        {
            _pulses.Add(new Pulse { Start = startOffset, Length = length, Expires = DateTime.UtcNow.AddMilliseconds(PulseMs) });
            _editor.TextArea.TextView.InvalidateLayer(Layer);
        }, DispatcherPriority.Background);
    }

    private void AddMidiPulses(int deviceIndex)
    {
        var view = _editor.TextArea.TextView;
        if (!view.VisualLinesValid) view.EnsureVisualLines();
        var visible = view.VisualLines;
        if (visible == null || visible.Count == 0) return;

        int visibleStart = visible[0].FirstDocumentLine.Offset;
        int visibleEnd = visible[visible.Count - 1].LastDocumentLine.EndOffset;
        var text = _editor.Document.GetText(visibleStart, visibleEnd - visibleStart);

        foreach (Match m in _midiDeviceRegex.Matches(text))
        {
            if (!int.TryParse(m.Groups["idx"].Value, out var idx)) continue;
            if (idx != deviceIndex) continue;
            int start = visibleStart + m.Groups["idx"].Index;
            int len = m.Groups["idx"].Length;
            _pulses.Add(new Pulse { Start = start, Length = len, Expires = DateTime.UtcNow.AddMilliseconds(PulseMs) });
        }

        Cleanup();
        view.InvalidateLayer(Layer);
        if (_pulses.Count > 0 && !_timer.IsEnabled) _timer.Start();
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_disposed) return;
        Cleanup();

        if (!textView.VisualLinesValid) return;
        var now = DateTime.UtcNow;

        foreach (var line in textView.VisualLines)
        {
            int lineStart = line.FirstDocumentLine.Offset;
            int lineEnd = line.LastDocumentLine.EndOffset;
            // Skip commented lines quickly
            var lineText = _editor.Document.GetText(line.FirstDocumentLine);
            if (lineText.TrimStart().StartsWith("//")) continue;

            foreach (var pulse in _pulses)
            {
                if (pulse.Expires <= now) continue;
                if (pulse.Start >= lineEnd || pulse.Start + pulse.Length <= lineStart) continue;

                int start = Math.Max(pulse.Start, lineStart);
                int end = Math.Min(pulse.Start + pulse.Length, lineEnd);
                var rects = BackgroundGeometryBuilder.GetRectsForSegment(
                        textView,
                        new TextSegment { StartOffset = start, Length = end - start });
                _rectBuffer.Clear();
                foreach (var r in rects)
                {
                    var inflated = new System.Windows.Rect(r.Location, r.Size);
                    inflated.Inflate(1.5, 1.5);
                    _rectBuffer.Add(inflated);
                }
                if (_rectBuffer.Count > 0)
                {
                    double t = 1.0 - (pulse.Expires - now).TotalMilliseconds / PulseMs;
                    double opacity = Math.Max(0.0, Math.Min(1.0, 1.0 - t));
                    drawingContext.PushOpacity(opacity);
                    foreach (var rect in _rectBuffer)
                    {
                        drawingContext.DrawRoundedRectangle(
                            PulseBrush,
                            null,
                            rect,
                            2.5,
                            2.5);
                    }
                    drawingContext.Pop();
                }
            }
        }
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        _pulses.RemoveAll(p => p.Expires <= now);
    }

    public void Dispose()
    {
        _disposed = true;
        _pulses.Clear();
        _timer.Stop();
    }

    private class Pulse
    {
        public int Start;
        public int Length;
        public DateTime Expires;
    }
}
