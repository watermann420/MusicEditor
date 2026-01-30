// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 Yannis Watermann
// Description: Inline visual host (Strudel-like) for punchcard / mixer / pianoroll overlays.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using MusicEngine.Core;
using MusicEngineEditor.Controls.InlineVisuals;
using WpfControl = System.Windows.Controls.Control;
using WpfLabel = System.Windows.Controls.Label;
using WpfPanel = System.Windows.Controls.Panel;
using System.Windows;

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
    private readonly Regex _commandRegex = new(@"^\s*//?\s*\.(?<cmd>[a-zA-Z]+)(?<args>.*)$",
        RegexOptions.Compiled);
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

        // Timer for 60 FPS refresh (approx 16 ms).
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16.0),
            DispatcherPriority.Background,
            (_, _) => RefreshVisuals(),
            Dispatcher.CurrentDispatcher);
        _timer.Start();

        _editor.TextChanged += (_, _) => RebuildHosts();
        _editor.TextArea.TextView.VisualLinesChanged += (_, _) => RefreshPositions();

        RebuildHosts();
    }

    /// <summary>
    /// Should be called from note callbacks to glow currently playing notes.
    /// </summary>
    public void OnNoteTriggered(MusicalEvent e)
    {
        foreach (var host in _hosts.Values)
        {
            host.NotifyNoteOn(e);
        }
    }

    public void OnNoteEnded(MusicalEvent e)
    {
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

            var cmd = match.Groups["cmd"].Value.ToLowerInvariant();
            var args = match.Groups["args"].Value;

            InlineVisualKind kind = cmd switch
            {
                "punchcard" => InlineVisualKind.Punchcard,
                "mixervisual" => InlineVisualKind.Mixer,
                "pianoroll" => InlineVisualKind.PianoRoll,
                _ => InlineVisualKind.Unknown
            };

            if (kind == InlineVisualKind.Unknown) continue;

            var host = new InlineVisualHost(_editor.TextArea.TextView, line, kind, args);
            host.Sequencer = Sequencer;
            _hosts[line] = host;
        }

        RefreshPositions();
    }

    private void RefreshVisuals()
    {
        foreach (var host in _hosts.Values)
        {
            host.Tick();
        }
    }

    private void Sequencer_NoteTriggered(object? sender, MusicalEventArgs e) => OnNoteTriggered(e.Event);
    private void Sequencer_NoteEnded(object? sender, MusicalEventArgs e) => OnNoteEnded(e.Event);

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
            Background = Brushes.Transparent,
            Child = _control,
            Margin = new System.Windows.Thickness(6, 2, 6, 6)
        };

        EnsureOverlayCanvas().Children.Add(_container);
    }

    public void Tick()
    {
        if (_control is IAnimatedVisual anim) anim.OnFrame();
    }

    public void UpdatePosition()
    {
        var line = _textView.Document.GetLineByNumber(_line);
        var vl = _textView.GetVisualLine(line.LineNumber);
        if (vl == null) return;

        var y = vl.VisualTop + vl.Height; // place directly below the line
        var x = 0;
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
        var parent = _textView.Parent as Grid;
        if (parent == null)
        {
            throw new InvalidOperationException("TextView parent must be Grid for inline visuals.");
        }

        var existing = parent.Children.OfType<Canvas>().FirstOrDefault(c => c.Name == "InlineVisualOverlay");
        if (existing != null) return existing;

        var canvas = new Canvas { Name = "InlineVisualOverlay", IsHitTestVisible = false };
        WpfPanel.SetZIndex(canvas, 50);
        parent.Children.Add(canvas);
        return canvas;
    }

    private Canvas? GetOverlayCanvas()
    {
        var parent = _textView.Parent as Grid;
        return parent?.Children.OfType<Canvas>().FirstOrDefault(c => c.Name == "InlineVisualOverlay");
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
