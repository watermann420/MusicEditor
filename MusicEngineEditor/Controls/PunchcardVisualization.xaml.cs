// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Pattern punchcard visualization - Strudel.cc inspired design.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A punchcard visualization control for displaying musical sequences and notes in a timeline.
/// Inspired by Strudel.cc's visualization style with scrolling playhead and golden/blue colors.
/// </summary>
public partial class PunchcardVisualization : UserControl
{
    #region Constants - Strudel-style Colors

    // Strudel color scheme
    private static readonly Color ActiveNoteColor = Color.FromRgb(0xFF, 0xCA, 0x28);      // Golden yellow #FFCA28
    private static readonly Color InactiveNoteColor = Color.FromRgb(0x74, 0x91, 0xD2);    // Soft blue #7491D2
    private static readonly Color PlayheadColor = Color.FromRgb(0xFF, 0xCA, 0x28);        // Golden yellow
    private static readonly Color GridLineColor = Color.FromRgb(0x22, 0x22, 0x22);        // Dark grid
    private static readonly Color BarLineColor = Color.FromRgb(0x33, 0x33, 0x33);         // Slightly brighter bars
    private static readonly Color BackgroundColor = Color.FromRgb(0x0A, 0x0A, 0x0A);      // Near black
    private static readonly Color LabelColor = Color.FromRgb(0xCC, 0xCC, 0xCC);           // Light gray labels

    private static readonly SolidColorBrush s_tooltipBackground;
    private static readonly SolidColorBrush s_tooltipForeground;
    private static readonly SolidColorBrush s_tooltipBorder;

    private static readonly SolidColorBrush s_activeNoteBrush;
    private static readonly SolidColorBrush s_inactiveNoteBrush;
    private static readonly SolidColorBrush s_noteStrokeBrush;
    private static readonly SolidColorBrush s_activeStrokeBrush;
    private static readonly SolidColorBrush s_gridLineBrush;
    private static readonly SolidColorBrush s_barLineBrush;
    private static readonly SolidColorBrush s_labelBrush;
    private static readonly SolidColorBrush s_cycleMarkerBrush;

    static PunchcardVisualization()
    {
        s_tooltipBackground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        s_tooltipBackground.Freeze();

        s_tooltipForeground = new SolidColorBrush(LabelColor);
        s_tooltipForeground.Freeze();

        s_tooltipBorder = new SolidColorBrush(ActiveNoteColor);
        s_tooltipBorder.Freeze();

        s_activeNoteBrush = new SolidColorBrush(ActiveNoteColor);
        s_activeNoteBrush.Freeze();

        s_inactiveNoteBrush = new SolidColorBrush(InactiveNoteColor);
        s_inactiveNoteBrush.Freeze();

        s_noteStrokeBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        s_noteStrokeBrush.Freeze();

        s_activeStrokeBrush = new SolidColorBrush(Colors.White);
        s_activeStrokeBrush.Freeze();

        s_gridLineBrush = new SolidColorBrush(GridLineColor);
        s_gridLineBrush.Freeze();

        s_barLineBrush = new SolidColorBrush(BarLineColor);
        s_barLineBrush.Freeze();

        s_labelBrush = new SolidColorBrush(LabelColor);
        s_labelBrush.Freeze();

        s_cycleMarkerBrush = new SolidColorBrush(Color.FromArgb(80, 255, 202, 40));
        s_cycleMarkerBrush.Freeze();
    }

    private const double DefaultBeatWidth = 40.0;
    private const double DefaultTrackHeight = 28.0;
    private const double MinNoteHeight = 8.0;   // Minimum note height for inline display
    private const double MaxNoteHeight = 22.0;  // Maximum note height for full display
    private const double NotePadding = 2.0;
    private const double MinNoteWidth = 4.0;
    private const double NoteCornerRadius = 2.0;
    private const int DefaultCycles = 4;      // Show 4 cycles by default (like Strudel)
    private const double DefaultPlayheadPosition = 0.25; // Playhead at 25% from left

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty BeatWidthProperty =
        DependencyProperty.Register(nameof(BeatWidth), typeof(double), typeof(PunchcardVisualization),
            new PropertyMetadata(DefaultBeatWidth, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty TrackHeightProperty =
        DependencyProperty.Register(nameof(TrackHeight), typeof(double), typeof(PunchcardVisualization),
            new PropertyMetadata(DefaultTrackHeight, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty TotalBeatsProperty =
        DependencyProperty.Register(nameof(TotalBeats), typeof(int), typeof(PunchcardVisualization),
            new PropertyMetadata(16, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty CurrentBeatProperty =
        DependencyProperty.Register(nameof(CurrentBeat), typeof(double), typeof(PunchcardVisualization),
            new PropertyMetadata(0.0, OnCurrentBeatChanged));

    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(PunchcardVisualization),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CyclesProperty =
        DependencyProperty.Register(nameof(Cycles), typeof(int), typeof(PunchcardVisualization),
            new PropertyMetadata(DefaultCycles, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(PunchcardVisualization),
            new PropertyMetadata(DefaultPlayheadPosition, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty ShowLabelsProperty =
        DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(PunchcardVisualization),
            new PropertyMetadata(false, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty AutoRangeProperty =
        DependencyProperty.Register(nameof(AutoRange), typeof(bool), typeof(PunchcardVisualization),
            new PropertyMetadata(true, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty MinMidiProperty =
        DependencyProperty.Register(nameof(MinMidi), typeof(int), typeof(PunchcardVisualization),
            new PropertyMetadata(36, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty MaxMidiProperty =
        DependencyProperty.Register(nameof(MaxMidi), typeof(int), typeof(PunchcardVisualization),
            new PropertyMetadata(84, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty ScrollingModeProperty =
        DependencyProperty.Register(nameof(ScrollingMode), typeof(PunchcardScrollMode), typeof(PunchcardVisualization),
            new PropertyMetadata(PunchcardScrollMode.FollowPlayhead));

    public double BeatWidth
    {
        get => (double)GetValue(BeatWidthProperty);
        set => SetValue(BeatWidthProperty, value);
    }

    public double TrackHeight
    {
        get => (double)GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    public int TotalBeats
    {
        get => (int)GetValue(TotalBeatsProperty);
        set => SetValue(TotalBeatsProperty, value);
    }

    public double CurrentBeat
    {
        get => (double)GetValue(CurrentBeatProperty);
        set => SetValue(CurrentBeatProperty, value);
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <summary>
    /// Number of cycles (loop iterations) to display. Default is 4.
    /// </summary>
    public int Cycles
    {
        get => (int)GetValue(CyclesProperty);
        set => SetValue(CyclesProperty, value);
    }

    /// <summary>
    /// Playhead position as a fraction of the visible width (0-1). Default is 0.25.
    /// </summary>
    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    /// <summary>
    /// Whether to show note labels (pitch names).
    /// </summary>
    public bool ShowLabels
    {
        get => (bool)GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    /// <summary>
    /// Whether to automatically calculate pitch range from notes.
    /// </summary>
    public bool AutoRange
    {
        get => (bool)GetValue(AutoRangeProperty);
        set => SetValue(AutoRangeProperty, value);
    }

    /// <summary>
    /// Minimum MIDI note for vertical range. Only used if AutoRange is false.
    /// </summary>
    public int MinMidi
    {
        get => (int)GetValue(MinMidiProperty);
        set => SetValue(MinMidiProperty, value);
    }

    /// <summary>
    /// Maximum MIDI note for vertical range. Only used if AutoRange is false.
    /// </summary>
    public int MaxMidi
    {
        get => (int)GetValue(MaxMidiProperty);
        set => SetValue(MaxMidiProperty, value);
    }

    /// <summary>
    /// How the display scrolls with playback.
    /// </summary>
    public PunchcardScrollMode ScrollingMode
    {
        get => (PunchcardScrollMode)GetValue(ScrollingModeProperty);
        set => SetValue(ScrollingModeProperty, value);
    }

    #endregion

    #region Private Fields

    private readonly List<Pattern> _patterns = new();
    private readonly Dictionary<Rectangle, NoteInfo> _noteRectangles = new();
    private readonly Dictionary<Rectangle, NoteInfo> _activeNotes = new();
    private Storyboard? _playheadAnimation;

    // Computed pitch range
    private int _computedMinMidi = 36;
    private int _computedMaxMidi = 84;

    // Sequencer synchronization
    private Sequencer? _sequencer;
    private bool _isSynced;
    private double _lastSyncedBeat = -1;

    // Animation constants
    private const double NotePulseDuration = 0.12;
    private const double NoteGlowIntensity = 0.9;

    #endregion

    #region Constructor

    public PunchcardVisualization()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderVisualization();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderVisualization();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopSync();
    }

    private static void OnVisualizationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PunchcardVisualization visualization)
        {
            visualization.RenderVisualization();
        }
    }

    private static void OnCurrentBeatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PunchcardVisualization visualization)
        {
            visualization.UpdatePlayheadAndScroll();
            visualization.UpdateActiveNotes();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a new pattern (track) to the visualization.
    /// </summary>
    public void AddPattern(Pattern pattern)
    {
        _patterns.Add(pattern);
        ComputePitchRange();
        RenderVisualization();
    }

    /// <summary>
    /// Removes a pattern from the visualization.
    /// </summary>
    public bool RemovePattern(Pattern pattern)
    {
        var result = _patterns.Remove(pattern);
        if (result)
        {
            ComputePitchRange();
            RenderVisualization();
        }
        return result;
    }

    /// <summary>
    /// Clears all patterns from the visualization.
    /// </summary>
    public void ClearPatterns()
    {
        _patterns.Clear();
        RenderVisualization();
    }

    /// <summary>
    /// Gets all patterns in the visualization.
    /// </summary>
    public IReadOnlyList<Pattern> GetPatterns() => _patterns.AsReadOnly();

    /// <summary>
    /// Updates the playhead position to the specified beat.
    /// </summary>
    public void UpdatePlayhead(double currentBeat)
    {
        CurrentBeat = currentBeat;
    }

    /// <summary>
    /// Starts the playhead animation.
    /// </summary>
    public void StartPlayheadAnimation(double bpm, double startBeat = 0)
    {
        StopPlayheadAnimation();

        IsPlaying = true;
        CurrentBeat = startBeat;

        var duration = TimeSpan.FromMinutes(TotalBeats / bpm);
        var animation = new DoubleAnimation
        {
            From = startBeat,
            To = TotalBeats,
            Duration = new Duration(duration),
            RepeatBehavior = RepeatBehavior.Forever
        };

        _playheadAnimation = new Storyboard();
        _playheadAnimation.Children.Add(animation);
        Storyboard.SetTarget(animation, this);
        Storyboard.SetTargetProperty(animation, new PropertyPath(CurrentBeatProperty));
        _playheadAnimation.Begin();
    }

    /// <summary>
    /// Stops the playhead animation.
    /// </summary>
    public void StopPlayheadAnimation()
    {
        IsPlaying = false;
        _playheadAnimation?.Stop();
        _playheadAnimation = null;
    }

    /// <summary>
    /// Pauses the playhead animation.
    /// </summary>
    public void PausePlayheadAnimation()
    {
        _playheadAnimation?.Pause();
        IsPlaying = false;
    }

    /// <summary>
    /// Resumes the playhead animation.
    /// </summary>
    public void ResumePlayheadAnimation()
    {
        _playheadAnimation?.Resume();
        IsPlaying = true;
    }

    #endregion

    #region Public Methods - Sequencer Synchronization

    /// <summary>
    /// Binds the visualization to a Sequencer for live synchronization.
    /// </summary>
    public void BindToSequencer(Sequencer sequencer)
    {
        if (_sequencer == sequencer) return;

        StopSync();
        _sequencer = sequencer;
        StartSync();
    }

    /// <summary>
    /// Unbinds from the current sequencer.
    /// </summary>
    public void UnbindSequencer()
    {
        StopSync();
        _sequencer = null;
    }

    /// <summary>
    /// Starts real-time synchronization with the bound sequencer.
    /// </summary>
    public void StartSync()
    {
        if (_sequencer == null || _isSynced) return;

        _isSynced = true;
        IsPlaying = true;
        _lastSyncedBeat = -1;
        CompositionTarget.Rendering += OnRenderFrame;
    }

    /// <summary>
    /// Stops real-time synchronization.
    /// </summary>
    public void StopSync()
    {
        if (!_isSynced) return;

        _isSynced = false;
        IsPlaying = false;
        CompositionTarget.Rendering -= OnRenderFrame;
        ClearActiveNoteEffects();
    }

    /// <summary>
    /// Updates the visualization with patterns from the sequencer.
    /// </summary>
    public void UpdatePatternsFromSequencer(IEnumerable<MusicEngine.Core.Pattern> sequencerPatterns)
    {
        _patterns.Clear();

        foreach (var seqPattern in sequencerPatterns)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[PunchcardViz] Converting pattern: Events={seqPattern.Events.Count}");

            foreach (var evt in seqPattern.Events)
            {
                System.Diagnostics.Debug.WriteLine($"[PunchcardViz]   Event: Note={evt.Note}, Beat={evt.Beat}, Duration={evt.Duration}");
            }
#endif

            var vizPattern = new Pattern
            {
                Name = $"Pattern {_patterns.Count + 1}",
                Notes = seqPattern.Events.Select(e => new Note
                {
                    Pitch = e.Note,
                    StartBeat = e.Beat,
                    Duration = e.Duration,
                    Velocity = e.Velocity
                }).ToList(),
                SourcePattern = seqPattern
            };

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[PunchcardViz] Created viz pattern with {vizPattern.Notes.Count} notes");
#endif
            _patterns.Add(vizPattern);
        }

        // Update total beats and pitch range
        if (_patterns.Any())
        {
            var maxBeat = _patterns
                .SelectMany(p => p.Notes)
                .Select(n => n.StartBeat + n.Duration)
                .DefaultIfEmpty(TotalBeats)
                .Max();

            TotalBeats = Math.Max(TotalBeats, (int)Math.Ceiling(maxBeat / 4.0) * 4);
        }

        ComputePitchRange();
        RenderVisualization();
    }

    /// <summary>
    /// Adds a pattern from a MusicEngine.Core.Pattern.
    /// </summary>
    public void AddPatternFromSequencer(MusicEngine.Core.Pattern sequencerPattern, string? name = null)
    {
        var vizPattern = new Pattern
        {
            Name = name ?? $"Pattern {_patterns.Count + 1}",
            Notes = sequencerPattern.Events.Select(e => new Note
            {
                Pitch = e.Note,
                StartBeat = e.Beat,
                Duration = e.Duration,
                Velocity = e.Velocity
            }).ToList(),
            SourcePattern = sequencerPattern
        };

        _patterns.Add(vizPattern);
        ComputePitchRange();
        RenderVisualization();
    }

    /// <summary>
    /// Gets the bound sequencer, if any.
    /// </summary>
    public Sequencer? BoundSequencer => _sequencer;

    /// <summary>
    /// Gets whether the visualization is currently synced to a sequencer.
    /// </summary>
    public bool IsSynced => _isSynced;

    #endregion

    #region Private Methods - Pitch Range

    private void ComputePitchRange()
    {
        if (!AutoRange || !_patterns.Any())
        {
            _computedMinMidi = MinMidi;
            _computedMaxMidi = MaxMidi;
            return;
        }

        int minPitch = int.MaxValue;
        int maxPitch = int.MinValue;
        bool hasNotes = false;

        foreach (var pattern in _patterns)
        {
            foreach (var note in pattern.Notes)
            {
                hasNotes = true;
                if (note.Pitch < minPitch) minPitch = note.Pitch;
                if (note.Pitch > maxPitch) maxPitch = note.Pitch;
            }
        }

        if (!hasNotes)
        {
            _computedMinMidi = MinMidi;
            _computedMaxMidi = MaxMidi;
            return;
        }

        // Add some padding (at least 2 semitones on each side)
        _computedMinMidi = Math.Max(0, minPitch - 2);
        _computedMaxMidi = Math.Min(127, maxPitch + 2);

        // Ensure at least an octave range
        if (_computedMaxMidi - _computedMinMidi < 12)
        {
            var mid = (_computedMinMidi + _computedMaxMidi) / 2;
            _computedMinMidi = Math.Max(0, mid - 6);
            _computedMaxMidi = Math.Min(127, mid + 6);
        }
    }

    #endregion

    #region Private Methods - Rendering

    private void RenderVisualization()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[PunchcardViz] RenderVisualization skipped: IsLoaded={IsLoaded}, Width={ActualWidth}, Height={ActualHeight}");
#endif
            return;
        }

#if DEBUG
        var totalNotes = _patterns.Sum(p => p.Notes.Count);
        System.Diagnostics.Debug.WriteLine($"[PunchcardViz] RenderVisualization: {_patterns.Count} patterns, {totalNotes} total notes");
#endif

        var totalWidth = TotalBeats * Cycles * BeatWidth;
        var pitchRange = _computedMaxMidi - _computedMinMidi + 1;
        var totalHeight = Math.Max(ActualHeight, pitchRange * (MaxNoteHeight + NotePadding));

        // Set canvas sizes
        GridCanvas.Width = totalWidth;
        GridCanvas.Height = totalHeight;
        NotesCanvas.Width = totalWidth;
        NotesCanvas.Height = totalHeight;
        LabelsCanvas.Width = totalWidth;
        LabelsCanvas.Height = totalHeight;

        // Clear existing drawings
        GridCanvas.Children.Clear();
        NotesCanvas.Children.Clear();
        LabelsCanvas.Children.Clear();
        _noteRectangles.Clear();

        // Render components
        RenderGrid(totalWidth, totalHeight);
        RenderNotes(totalHeight);
        RenderCycleMarkers(totalWidth, totalHeight);
        UpdatePlayheadAndScroll();
    }

    private void RenderGrid(double totalWidth, double totalHeight)
    {
        var totalBeatsAll = TotalBeats * Cycles;

        // Beat lines (subtle)
        for (int beat = 1; beat < totalBeatsAll; beat++)
        {
            var x = beat * BeatWidth;
            var isBar = beat % 4 == 0;

            var line = new Line
            {
                X1 = x, Y1 = 0,
                X2 = x, Y2 = totalHeight,
                Stroke = isBar ? s_barLineBrush : s_gridLineBrush,
                StrokeThickness = isBar ? 1.5 : 0.5,
                Opacity = isBar ? 0.6 : 0.3
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void RenderNotes(double totalHeight)
    {
        var pitchRange = _computedMaxMidi - _computedMinMidi + 1;

        // Calculate adaptive note height based on available space
        var availableHeightPerNote = (totalHeight - NotePadding * 2) / Math.Max(pitchRange, 1);
        var noteHeight = Math.Max(MinNoteHeight, Math.Min(MaxNoteHeight, availableHeightPerNote - NotePadding));

        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            var cycleOffset = cycle * TotalBeats * BeatWidth;

            foreach (var pattern in _patterns)
            {
                foreach (var note in pattern.Notes)
                {
                    RenderNote(note, cycleOffset, availableHeightPerNote, noteHeight, pattern.Name, cycle);
                }
            }
        }
    }

    private void RenderNote(Note note, double cycleOffset, double noteHeightWithPadding, double noteHeight, string patternName, int cycle)
    {
        // Calculate position
        var noteX = cycleOffset + note.StartBeat * BeatWidth;
        var noteWidth = Math.Max(note.Duration * BeatWidth - 2, MinNoteWidth);

        // Vertical position based on pitch (higher pitch = higher on screen)
        var pitchIndex = note.Pitch - _computedMinMidi;
        var noteY = (_computedMaxMidi - note.Pitch) * noteHeightWithPadding + NotePadding;

        // Create note rectangle with Strudel-style colors
        var rect = new Rectangle
        {
            Width = noteWidth,
            Height = noteHeight,
            Fill = s_inactiveNoteBrush,
            RadiusX = NoteCornerRadius,
            RadiusY = NoteCornerRadius,
            Opacity = 0.85,
            Stroke = s_noteStrokeBrush,
            StrokeThickness = 0.5
        };

        Canvas.SetLeft(rect, noteX + 1);
        Canvas.SetTop(rect, noteY);

        var noteInfo = new NoteInfo
        {
            Note = note,
            PatternName = patternName,
            Cycle = cycle,
            OriginalColor = InactiveNoteColor
        };
        _noteRectangles[rect] = noteInfo;

        // Mouse interactions
        rect.MouseEnter += OnNoteMouseEnter;
        rect.MouseLeave += OnNoteMouseLeave;

        NotesCanvas.Children.Add(rect);

        // Render label if enabled and note is large enough
        if (ShowLabels && noteWidth > 20 && noteHeight > 12)
        {
            var label = new TextBlock
            {
                Text = note.Name,
                FontSize = Math.Max(8, Math.Min(10, noteHeight - 4)),
                Foreground = s_labelBrush,
                Opacity = 0.8
            };
            Canvas.SetLeft(label, noteX + 4);
            Canvas.SetTop(label, noteY + (noteHeight - 10) / 2);
            LabelsCanvas.Children.Add(label);
        }
    }

    private void RenderCycleMarkers(double totalWidth, double totalHeight)
    {
        CycleCanvas.Children.Clear();

        for (int cycle = 1; cycle < Cycles; cycle++)
        {
            var x = cycle * TotalBeats * BeatWidth;

            // Cycle separator line
            var line = new Line
            {
                X1 = x, Y1 = 0,
                X2 = x, Y2 = totalHeight,
                Stroke = s_cycleMarkerBrush,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            CycleCanvas.Children.Add(line);
        }
    }

    private void UpdatePlayheadAndScroll()
    {
        if (!IsLoaded || ActualWidth <= 0) return;

        var visibleWidth = ActualWidth;
        var playheadX = PlayheadPosition * visibleWidth;

        // Update playhead visual position
        Canvas.SetLeft(Playhead, playheadX);
        Canvas.SetLeft(PlayheadGlow, playheadX);
        Canvas.SetLeft(PlayheadMarker, playheadX);

        Playhead.Y2 = ActualHeight;
        PlayheadGlow.Y2 = ActualHeight;

        // Scroll to follow playhead
        if (ScrollingMode == PunchcardScrollMode.FollowPlayhead && IsPlaying)
        {
            var currentBeatInCycles = CurrentBeat % (TotalBeats * Cycles);
            var contentX = currentBeatInCycles * BeatWidth;
            var scrollX = contentX - playheadX;
            ScrollContainer.ScrollToHorizontalOffset(Math.Max(0, scrollX));
        }
    }

    private void UpdateActiveNotes()
    {
        if (!IsPlaying) return;

        var currentBeatInCycle = CurrentBeat % TotalBeats;
        const double triggerWindow = 0.15;

        foreach (var kvp in _noteRectangles)
        {
            var rect = kvp.Key;
            var noteInfo = kvp.Value;
            var note = noteInfo.Note;

            // Check if note is currently active
            bool isActive = currentBeatInCycle >= note.StartBeat - triggerWindow &&
                           currentBeatInCycle < note.StartBeat + note.Duration;

            if (isActive && !_activeNotes.ContainsKey(rect))
            {
                // Activate note
                ActivateNote(rect, noteInfo);
                _activeNotes[rect] = noteInfo;
            }
            else if (!isActive && _activeNotes.ContainsKey(rect))
            {
                // Deactivate note
                DeactivateNote(rect, noteInfo);
                _activeNotes.Remove(rect);
            }
        }
    }

    private void ActivateNote(Rectangle rect, NoteInfo noteInfo)
    {
        rect.Fill = s_activeNoteBrush;
        rect.Opacity = 1.0;
        rect.StrokeThickness = 1.5;
        rect.Stroke = s_activeStrokeBrush;

        // Add glow effect
        rect.Effect = new DropShadowEffect
        {
            Color = ActiveNoteColor,
            BlurRadius = 15,
            ShadowDepth = 0,
            Opacity = NoteGlowIntensity
        };

        // Scale animation
        var scaleTransform = new ScaleTransform(1.0, 1.0);
        rect.RenderTransform = scaleTransform;
        rect.RenderTransformOrigin = new Point(0.5, 0.5);

        var pulseAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 1.06,
            Duration = TimeSpan.FromSeconds(NotePulseDuration),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnim);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnim);
    }

    private void DeactivateNote(Rectangle rect, NoteInfo noteInfo)
    {
        rect.Fill = s_inactiveNoteBrush;
        rect.Opacity = 0.85;
        rect.StrokeThickness = 0.5;
        rect.Stroke = s_noteStrokeBrush;
        rect.Effect = null;
        rect.RenderTransform = null;
    }

    private void ClearActiveNoteEffects()
    {
        foreach (var kvp in _activeNotes)
        {
            DeactivateNote(kvp.Key, kvp.Value);
        }
        _activeNotes.Clear();
    }

    #endregion

    #region Private Methods - Sequencer Sync

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        if (_sequencer == null || !_isSynced) return;

        var currentBeat = _sequencer.CurrentBeat;

        // Only update if beat changed significantly
        if (Math.Abs(currentBeat - _lastSyncedBeat) > 0.01)
        {
            _lastSyncedBeat = currentBeat;
            CurrentBeat = currentBeat;
        }
    }

    #endregion

    #region Private Methods - Mouse Interactions

    private void OnNoteMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Rectangle rect && _noteRectangles.TryGetValue(rect, out var noteInfo))
        {
            rect.Opacity = 1.0;

            // Show tooltip with note info
            rect.ToolTip = new System.Windows.Controls.ToolTip
            {
                Content = $"{noteInfo.Note.Name}\nBeat: {noteInfo.Note.StartBeat:F2}\nDuration: {noteInfo.Note.Duration:F2}\nVelocity: {noteInfo.Note.Velocity}",
                Background = s_tooltipBackground,
                Foreground = s_tooltipForeground,
                BorderBrush = s_tooltipBorder,
                BorderThickness = new Thickness(1)
            };
        }
    }

    private void OnNoteMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Rectangle rect)
        {
            if (!_activeNotes.ContainsKey(rect))
            {
                rect.Opacity = 0.85;
            }
        }
    }

    #endregion

    #region Nested Types

    private class NoteInfo
    {
        public Note Note { get; init; } = null!;
        public string PatternName { get; init; } = string.Empty;
        public int Cycle { get; init; }
        public Color OriginalColor { get; init; }
    }

    #endregion
}

#region Enums

/// <summary>
/// Scrolling behavior for the punchcard visualization.
/// </summary>
public enum PunchcardScrollMode
{
    /// <summary>
    /// Content scrolls so playhead stays at fixed position.
    /// </summary>
    FollowPlayhead,

    /// <summary>
    /// Playhead moves across fixed content.
    /// </summary>
    FixedContent,

    /// <summary>
    /// Page-based scrolling when playhead reaches edge.
    /// </summary>
    PageScroll
}

#endregion

#region Data Models

/// <summary>
/// Represents a pattern (track) containing notes.
/// </summary>
public class Pattern
{
    public string Name { get; set; } = "Pattern";
    public List<Note> Notes { get; set; } = new();
    public Color? ColorOverride { get; set; }
    public MusicEngine.Core.Pattern? SourcePattern { get; set; }
}

/// <summary>
/// Represents a single note in a pattern.
/// </summary>
public class Note
{
    public int Pitch { get; set; }
    public double StartBeat { get; set; }
    public double Duration { get; set; } = 1.0;
    public int Velocity { get; set; } = 100;
    public string? CustomLabel { get; set; }

    public string Name => GetNoteName(Pitch);

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static string GetNoteName(int pitch)
    {
        var octave = (pitch / 12) - 1;
        var noteName = NoteNames[pitch % 12];
        return $"{noteName}{octave}";
    }
}

#endregion
