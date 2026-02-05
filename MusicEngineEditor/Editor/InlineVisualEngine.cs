// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 Yannis Watermann
// Description: Inline visual host (Strudel-like) for punchcard / mixer / pianoroll overlays.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using MusicEngine.Core;
using MusicEngineEditor.Controls.InlineVisuals;
using WpfControl = System.Windows.Controls.Control;
using WpfLabel = System.Windows.Controls.Label;
using WpfPanel = System.Windows.Controls.Panel;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Parses inline commands (e.g. ".punchcard", ".mixervisual") in the code editor and
/// renders WPF visuals just below the corresponding line, Strudel-style.
/// </summary>
public sealed class InlineVisualEngine : IDisposable
{
    private readonly TextEditor _editor;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<int, InlineVisualHost> _hosts = new();
    // Match: // .punchcard, .punchcard, .Punchcard(), pattern.Punchcard(), etc.
    private readonly Regex _commandRegex = new(
        @"(?:^\s*//?\s*\.(?<cmd>[a-zA-Z]+)(?<args>.*)$)|" +           // Comment style: // .punchcard
        @"(?:\.(?<cmd2>Punchcard|punchcard|PianoRoll|pianoroll|MixerVisual|mixervisual)\s*\((?<args2>[^)]*)\))", // Method call: .Punchcard()
        RegexOptions.Compiled | RegexOptions.Multiline);
    private readonly NoteHighlightTransformer _noteHighlighter;
    private bool _disposed;

    /// <summary>Optional sequencer to feed live note/meter data.</summary>
    public Sequencer? Sequencer
    {
        get => _sequencer;
        set
        {
            if (_sequencer == value) return;
            if (_sequencer != null)
            {
                _sequencer.NoteTriggered -= Sequencer_NoteTriggered;
                _sequencer.NoteEnded -= Sequencer_NoteEnded;
            }

            _sequencer = value;

            if (_sequencer != null)
            {
                _sequencer.NoteTriggered += Sequencer_NoteTriggered;
                _sequencer.NoteEnded += Sequencer_NoteEnded;
            }

            foreach (var h in _hosts.Values)
            {
                h.Sequencer = _sequencer;
            }
        }
    }
    private Sequencer? _sequencer;

    public InlineVisualEngine(TextEditor editor)
    {
        _editor = editor;
        _noteHighlighter = new NoteHighlightTransformer(_editor);
        _editor.TextArea.TextView.LineTransformers.Add(_noteHighlighter);

        // Timer for 60 FPS refresh (approx 16 ms). Started on-demand when visuals exist.
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16.0),
            DispatcherPriority.Background,
            (_, _) => RefreshVisuals(),
            Dispatcher.CurrentDispatcher);

        _editor.TextChanged += (_, _) => RebuildHosts();
        _editor.TextArea.TextView.VisualLinesChanged += (_, _) => RefreshPositions();
        _editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => RefreshPositions();

        RebuildHosts();
    }

    /// <summary>
    /// Should be called from note callbacks to glow currently playing notes.
    /// </summary>
    public void OnNoteTriggered(MusicalEvent e)
    {
        _noteHighlighter.HighlightPitch(e.Note);
        foreach (var host in _hosts.Values)
        {
            host.NotifyNoteOn(e);
        }
    }

    public void OnNoteEnded(MusicalEvent e)
    {
        _noteHighlighter.ClearPitch(e.Note);
        foreach (var host in _hosts.Values)
        {
            host.NotifyNoteOff(e);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        foreach (var host in _hosts.Values) host.Dispose();
        _hosts.Clear();
    }

    #region Private

    private void RebuildHosts()
    {
        foreach (var host in _hosts.Values) host.Dispose();
        _hosts.Clear();

        var doc = _editor.Document;
        for (int line = 1; line <= doc.LineCount; line++)
        {
            var text = doc.GetText(doc.GetLineByNumber(line));
            var match = _commandRegex.Match(text);
            if (!match.Success) continue;

            // Check both capture groups (comment style and method call style)
            var cmd = match.Groups["cmd"].Success
                ? match.Groups["cmd"].Value.ToLowerInvariant()
                : match.Groups["cmd2"].Success
                    ? match.Groups["cmd2"].Value.ToLowerInvariant()
                    : "";

            var args = match.Groups["args"].Success
                ? match.Groups["args"].Value
                : match.Groups["args2"].Success
                    ? match.Groups["args2"].Value
                    : "";

            if (string.IsNullOrEmpty(cmd)) continue;

            InlineVisualKind kind = cmd switch
            {
                "punchcard" => InlineVisualKind.Punchcard,
                "mixervisual" => InlineVisualKind.Mixer,
                "pianoroll" => InlineVisualKind.PianoRoll,
                _ => InlineVisualKind.Unknown
            };

            if (kind == InlineVisualKind.Unknown) continue;

            // Don't add duplicate for same line
            if (_hosts.ContainsKey(line)) continue;

            var host = new InlineVisualHost(_editor.TextArea.TextView, line, kind, args);
            host.Sequencer = Sequencer;
            _hosts[line] = host;
        }

        RefreshPositions();

        if (_hosts.Count == 0)
        {
            _timer.Stop();
        }
        else if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void RefreshVisuals()
    {
        foreach (var host in _hosts.Values)
        {
            host.Tick();
        }
        if (_hosts.Count == 0 && _timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    private void Sequencer_NoteTriggered(object? sender, MusicalEventArgs e)
    {
        if (!_editor.Dispatcher.CheckAccess())
        {
            _editor.Dispatcher.BeginInvoke(new Action(() => Sequencer_NoteTriggered(sender, e)), DispatcherPriority.Background);
            return;
        }
        OnNoteTriggered(e.Event);
    }

    private void Sequencer_NoteEnded(object? sender, MusicalEventArgs e)
    {
        if (!_editor.Dispatcher.CheckAccess())
        {
            _editor.Dispatcher.BeginInvoke(new Action(() => Sequencer_NoteEnded(sender, e)), DispatcherPriority.Background);
            return;
        }
        OnNoteEnded(e.Event);
    }

    private void RefreshPositions()
    {
        var view = _editor.TextArea.TextView;
        if (!view.VisualLinesValid)
        {
            view.EnsureVisualLines();
        }

        foreach (var host in _hosts.Values)
        {
            host.UpdatePosition();
        }
}

#endregion
}

internal sealed class NoteHighlightTransformer : DocumentColorizingTransformer
{
    private readonly TextEditor _editor;
    private readonly Regex _noteRegex = new(@"Note\s*\(\s*(?<pitch>\d{1,3})", RegexOptions.Compiled);
    private readonly Dictionary<int, List<(int offset, int length)>> _activeSpans = new();
    private readonly Dispatcher _dispatcher;

    public NoteHighlightTransformer(TextEditor editor)
    {
        _editor = editor;
        _dispatcher = editor.Dispatcher;
    }

    public void HighlightPitch(int pitch)
    {
        _dispatcher.InvokeAsync(() =>
        {
            var spans = FindPitchSpans(pitch);
            lock (_activeSpans)
            {
                _activeSpans[pitch] = spans;
            }
            _editor.TextArea.TextView.Redraw();
        }, DispatcherPriority.Background);
    }

    public void ClearPitch(int pitch)
    {
        _dispatcher.InvokeAsync(() =>
        {
            lock (_activeSpans)
            {
                _activeSpans.Remove(pitch);
            }
            _editor.TextArea.TextView.Redraw();
        }, DispatcherPriority.Background);
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        List<(int offset, int length)> spansForLine = new();
        lock (_activeSpans)
        {
            foreach (var kvp in _activeSpans)
            {
                foreach (var span in kvp.Value)
                {
                    if (span.offset >= line.EndOffset || span.offset + span.length <= line.Offset) continue;
                    spansForLine.Add(span);
                }
            }
        }

        foreach (var span in spansForLine)
        {
            int start = Math.Max(span.offset, line.Offset);
            int end = Math.Min(span.offset + span.length, line.EndOffset);
            int length = end - start;
            if (length <= 0) continue;

            ChangeLinePart(start, end, element =>
            {
                if (element.TextRunProperties is not VisualLineElementTextRunProperties props) return;

                // Use current foreground color, add stronger modern glow
                var fg = (props.ForegroundBrush as SolidColorBrush) ??
                         new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                var c = fg.Color;

                // Gradient glow behind the number (stronger)
                var glow = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0.5),
                    EndPoint = new System.Windows.Point(1, 0.5),
                    Opacity = 1.0,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(0,   c.R, c.G, c.B), 0.0),
                        new GradientStop(Color.FromArgb(200, c.R, c.G, c.B), 0.5),
                        new GradientStop(Color.FromArgb(0,   c.R, c.G, c.B), 1.0),
                    }
                };

                // Brighter foreground
                var fgBright = Color.FromArgb(255, (byte)Math.Min(255, c.R + 40),
                                                   (byte)Math.Min(255, c.G + 40),
                                                   (byte)Math.Min(255, c.B + 40));

                props.SetBackgroundBrush(glow);
                props.SetForegroundBrush(new SolidColorBrush(fgBright));
            });
        }
    }

    private List<(int offset, int length)> FindPitchSpans(int pitch)
    {
        var text = _editor.Document.Text;
        var spans = new List<(int offset, int length)>();
        foreach (Match m in _noteRegex.Matches(text))
        {
            if (!int.TryParse(m.Groups["pitch"].Value, out var p)) continue;
            if (p != pitch) continue;
            spans.Add((m.Groups["pitch"].Index, m.Groups["pitch"].Length));
        }
        return spans;
    }
}

/// <summary>Supported visual kinds.</summary>
internal enum InlineVisualKind
{
    Unknown,
    Punchcard,
    Mixer,
    PianoRoll
}

/// <summary>
/// Hosts a single overlay control bound to a specific document line.
/// </summary>
internal sealed class InlineVisualHost : IDisposable
{
    private readonly TextView _textView;
    private readonly int _line;
    private readonly InlineVisualKind _kind;
    private readonly string _args;
    private readonly WpfControl _control;
    private readonly Border _container;
    private bool _disposed;

    public Sequencer? Sequencer
    {
        get => (_control as ISequencerVisual)?.Sequencer;
        set
        {
            if (_control is ISequencerVisual sv) sv.Sequencer = value;
        }
    }

    public InlineVisualHost(TextView textView, int line, InlineVisualKind kind, string args)
    {
        _textView = textView;
        _line = line;
        _kind = kind;
        _args = args;

        _control = kind switch
        {
            InlineVisualKind.Punchcard => new PunchcardInlineControl(),
            InlineVisualKind.Mixer => new MixerInlineControl(),
            InlineVisualKind.PianoRoll => new PianoRollInlineControl(),
            _ => new WpfLabel { Content = "Unknown visual" }
        };

        _container = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 217, 255)), // Subtle cyan tint for visibility
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = _control,
            Margin = new System.Windows.Thickness(6, 2, 6, 6),
            MinHeight = 100 // Ensure minimum height
        };

        EnsureOverlayCanvas().Children.Add(_container);
    }

    public void Tick()
    {
        if (_control is IAnimatedVisual anim) anim.OnFrame();
    }

    public void UpdatePosition()
    {
        var doc = _textView.Document;
        if (doc == null || _line > doc.LineCount) return;

        var line = doc.GetLineByNumber(_line);
        var vl = _textView.GetVisualLine(line.LineNumber);
        if (vl == null)
        {
            // Line not visible - hide the control
            _container.Visibility = Visibility.Collapsed;
            return;
        }

        _container.Visibility = Visibility.Visible;

        // Get position relative to the TextView's render coordinates
        var y = vl.VisualTop + vl.Height - _textView.VerticalOffset;
        var x = -_textView.HorizontalOffset;

        Canvas.SetLeft(_container, x);
        Canvas.SetTop(_container, y);
        _container.Width = _textView.ActualWidth;
    }

    public void NotifyNoteOn(MusicalEvent e)
    {
        if (_control is INoteReactive nr) nr.OnNoteOn(e);
    }

    public void NotifyNoteOff(MusicalEvent e)
    {
        if (_control is INoteReactive nr) nr.OnNoteOff(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var canvas = GetOverlayCanvas();
        if (canvas != null)
        {
            canvas.Children.Remove(_container);
        }
    }

    private Canvas EnsureOverlayCanvas()
    {
        // Find a suitable parent container by traversing up the visual tree
        DependencyObject? current = _textView;
        Grid? gridParent = null;

        // Search up to 10 levels up for a Grid
        for (int i = 0; i < 10 && current != null; i++)
        {
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            if (current is Grid grid)
            {
                gridParent = grid;
                break;
            }
        }

        if (gridParent == null)
        {
            // Fallback: Create a canvas as a child of the TextView itself using an AdornerLayer
            var adornerLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(_textView);
            if (adornerLayer != null)
            {
                // Use adorner approach - but for simplicity, let's try the TextArea's parent
                var textArea = _textView.Parent;
                if (textArea != null)
                {
                    var textAreaParent = System.Windows.Media.VisualTreeHelper.GetParent(textArea);
                    if (textAreaParent is Grid taGrid)
                    {
                        gridParent = taGrid;
                    }
                }
            }
        }

        if (gridParent == null)
        {
            // Last resort: just return a detached canvas (won't be visible but won't crash)
            System.Diagnostics.Debug.WriteLine("Warning: Could not find Grid parent for inline visuals overlay");
            return new Canvas { Name = "InlineVisualOverlay" };
        }

        var existing = gridParent.Children.OfType<Canvas>().FirstOrDefault(c => c.Name == "InlineVisualOverlay");
        if (existing != null) return existing;

        var canvas = new Canvas
        {
            Name = "InlineVisualOverlay",
            IsHitTestVisible = true,
            ClipToBounds = false,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch
        };
        WpfPanel.SetZIndex(canvas, 50);
        gridParent.Children.Add(canvas);
        return canvas;
    }

    private Canvas? GetOverlayCanvas()
    {
        // Find the Grid parent the same way as EnsureOverlayCanvas
        DependencyObject? current = _textView;

        for (int i = 0; i < 10 && current != null; i++)
        {
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            if (current is Grid grid)
            {
                return grid.Children.OfType<Canvas>().FirstOrDefault(c => c.Name == "InlineVisualOverlay");
            }
        }

        return null;
    }
}

/// <summary>Interface for visuals needing sequencer hookup.</summary>
public interface ISequencerVisual
{
    Sequencer? Sequencer { get; set; }
}

/// <summary>Interface for visuals that animate every frame.</summary>
public interface IAnimatedVisual
{
    void OnFrame();
}

/// <summary>Interface for visuals that react to note on/off.</summary>
public interface INoteReactive
{
    void OnNoteOn(MusicalEvent e);
    void OnNoteOff(MusicalEvent e);
}
