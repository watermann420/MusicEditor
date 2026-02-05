// MusicEngine License (MEL) - Honor-Based Commercial Support
// Description: Inline punchcard wrapper.

using System;
using System.Linq;
using System.Windows.Controls;
using MusicEngine.Core;
using MusicEngineEditor.Editor;
using CorePattern = MusicEngine.Core.Pattern;

namespace MusicEngineEditor.Controls.InlineVisuals;

public partial class PunchcardInlineControl : UserControl, ISequencerVisual, IAnimatedVisual, INoteReactive
{
    private Sequencer? _sequencer;
    private bool _patternsLoaded;

    public Sequencer? Sequencer
    {
        get => _sequencer;
        set
        {
            if (_sequencer != null)
            {
                _sequencer.PatternAdded -= OnPatternAdded;
                _sequencer.PatternRemoved -= OnPatternRemoved;
                _sequencer.PatternsCleared -= OnPatternsCleared;
            }

            _sequencer = value;

            if (value != null)
            {
                Punch.BindToSequencer(value);
                value.PatternAdded += OnPatternAdded;
                value.PatternRemoved += OnPatternRemoved;
                value.PatternsCleared += OnPatternsCleared;

                // Load existing patterns
                LoadPatternsFromSequencer();
            }
            else
            {
                Punch.UnbindSequencer();
                Punch.ClearPatterns();
            }
        }
    }

    public PunchcardInlineControl()
    {
        InitializeComponent();
    }

    public void OnFrame()
    {
        // Refresh patterns if sequencer has patterns but we haven't loaded them yet
        // Also reload if pattern count changed
        if (_sequencer != null)
        {
            var patternCount = _sequencer.Patterns.Count;
            var currentNoteCount = _sequencer.Patterns.Sum(p => p.Events.Count);

            if (patternCount > 0 && (!_patternsLoaded || currentNoteCount != _lastNoteCount))
            {
                _lastNoteCount = currentNoteCount;
                LoadPatternsFromSequencer();
            }
        }
    }

    private int _lastNoteCount = 0;

    public void OnNoteOn(MusicalEvent e)
    {
        // Could highlight the note in the punchcard
    }

    public void OnNoteOff(MusicalEvent e)
    {
        // Could un-highlight the note
    }

    private void LoadPatternsFromSequencer()
    {
        if (_sequencer == null) return;

        var patterns = _sequencer.Patterns.ToList();
        if (patterns.Count == 0)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("[Punchcard] No patterns in sequencer");
#endif
            return;
        }

        // Only show patterns that have ShowInPunchcard = true, or all if none are marked
        var punchcardPatterns = patterns.Where(p => p.ShowInPunchcard).ToList();
        if (punchcardPatterns.Count == 0)
        {
            // If no patterns explicitly marked, show all patterns that have events
            punchcardPatterns = patterns.Where(p => p.Events.Count > 0).ToList();
        }

        // Still nothing? Show all patterns
        if (punchcardPatterns.Count == 0)
        {
            punchcardPatterns = patterns;
        }

#if DEBUG
        var totalEvents = punchcardPatterns.Sum(p => p.Events.Count);
        System.Diagnostics.Debug.WriteLine($"[Punchcard] Loading {punchcardPatterns.Count} patterns with {totalEvents} total events");
#endif

        Punch.UpdatePatternsFromSequencer(punchcardPatterns);
        _patternsLoaded = true;
    }

    private void OnPatternAdded(object? sender, CorePattern pattern)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"[Punchcard] PatternAdded event: {pattern.Name}, Events: {pattern.Events.Count}, ShowInPunchcard: {pattern.ShowInPunchcard}");
#endif

        Dispatcher.BeginInvoke(() =>
        {
            // Always add patterns that have events, regardless of ShowInPunchcard flag initially
            // The flag might be set after the pattern is added to the sequencer
            if (pattern.Events.Count > 0 || pattern.ShowInPunchcard)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[Punchcard] Adding pattern: {pattern.Name}");
#endif
                Punch.AddPatternFromSequencer(pattern, pattern.Name);
                _patternsLoaded = true;
            }
            else
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[Punchcard] Skipping pattern (no events): {pattern.Name}");
#endif
            }
        });
    }

    private void OnPatternRemoved(object? sender, CorePattern pattern)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Rebuild the patterns list
            LoadPatternsFromSequencer();
        });
    }

    private void OnPatternsCleared(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            Punch.ClearPatterns();
            _patternsLoaded = false;
        });
    }
}
