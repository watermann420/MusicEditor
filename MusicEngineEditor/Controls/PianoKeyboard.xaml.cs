// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Virtual piano keyboard control.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MusicEngineEditor.Models;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A vertical piano keyboard control for the Piano Roll editor.
/// Displays notes from LowestNote to HighestNote with proper white/black key styling.
/// </summary>
public partial class PianoKeyboard : UserControl
{
    #region Constants

    private const double DefaultKeyHeight = 20.0;
    private const double BlackKeyWidthRatio = 0.65;
    private const double BlackKeyHeightRatio = 0.6;

    // Note names for labeling (C = 0, C# = 1, D = 2, etc.)
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly bool[] IsBlackKey = { false, true, false, true, false, false, true, false, true, false, true, false };

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty LowestNoteProperty =
        DependencyProperty.Register(nameof(LowestNote), typeof(int), typeof(PianoKeyboard),
            new PropertyMetadata(21, OnNoteRangeChanged)); // A0 (21) is typical piano lowest note

    public static readonly DependencyProperty HighestNoteProperty =
        DependencyProperty.Register(nameof(HighestNote), typeof(int), typeof(PianoKeyboard),
            new PropertyMetadata(108, OnNoteRangeChanged)); // C8 (108) is typical piano highest note

    public static readonly DependencyProperty KeyHeightProperty =
        DependencyProperty.Register(nameof(KeyHeight), typeof(double), typeof(PianoKeyboard),
            new PropertyMetadata(DefaultKeyHeight, OnLayoutPropertyChanged));

    public static readonly DependencyProperty HighlightedNoteProperty =
        DependencyProperty.Register(nameof(HighlightedNote), typeof(int), typeof(PianoKeyboard),
            new PropertyMetadata(-1, OnHighlightedNoteChanged));

    public static readonly DependencyProperty ActiveNotesProperty =
        DependencyProperty.Register(nameof(ActiveNotes), typeof(IEnumerable<int>), typeof(PianoKeyboard),
            new PropertyMetadata(null, OnActiveNotesChanged));

    /// <summary>
    /// Gets or sets the lowest MIDI note number to display (0-127).
    /// Default is 21 (A0).
    /// </summary>
    public int LowestNote
    {
        get => (int)GetValue(LowestNoteProperty);
        set => SetValue(LowestNoteProperty, Math.Clamp(value, 0, 127));
    }

    /// <summary>
    /// Gets or sets the highest MIDI note number to display (0-127).
    /// Default is 108 (C8).
    /// </summary>
    public int HighestNote
    {
        get => (int)GetValue(HighestNoteProperty);
        set => SetValue(HighestNoteProperty, Math.Clamp(value, 0, 127));
    }

    /// <summary>
    /// Gets or sets the height of each semitone row in pixels.
    /// Default is 20.0.
    /// </summary>
    public double KeyHeight
    {
        get => (double)GetValue(KeyHeightProperty);
        set => SetValue(KeyHeightProperty, Math.Max(8.0, value));
    }

    /// <summary>
    /// Gets or sets the currently highlighted note (for hover/playing indication).
    /// Set to -1 for no highlight.
    /// </summary>
    public int HighlightedNote
    {
        get => (int)GetValue(HighlightedNoteProperty);
        set => SetValue(HighlightedNoteProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of currently active (pressed) notes.
    /// </summary>
    public IEnumerable<int>? ActiveNotes
    {
        get => (IEnumerable<int>?)GetValue(ActiveNotesProperty);
        set => SetValue(ActiveNotesProperty, value);
    }

    #endregion

    #region Routed Events

    public static readonly RoutedEvent NotePressedEvent =
        EventManager.RegisterRoutedEvent(nameof(NotePressed), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler<NoteEventArgs>), typeof(PianoKeyboard));

    public static readonly RoutedEvent NoteReleasedEvent =
        EventManager.RegisterRoutedEvent(nameof(NoteReleased), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler<NoteEventArgs>), typeof(PianoKeyboard));

    /// <summary>
    /// Occurs when a note is pressed (mouse down on a key).
    /// </summary>
    public event RoutedEventHandler<NoteEventArgs> NotePressed
    {
        add => AddHandler(NotePressedEvent, value);
        remove => RemoveHandler(NotePressedEvent, value);
    }

    /// <summary>
    /// Occurs when a note is released (mouse up or mouse leaves key).
    /// </summary>
    public event RoutedEventHandler<NoteEventArgs> NoteReleased
    {
        add => AddHandler(NoteReleasedEvent, value);
        remove => RemoveHandler(NoteReleasedEvent, value);
    }

    #endregion

    #region Private Fields

    private readonly Dictionary<int, Shapes.Rectangle> _whiteKeyRects = new();
    private readonly Dictionary<int, Shapes.Rectangle> _blackKeyRects = new();
    private readonly Dictionary<int, TextBlock> _noteLabels = new();

    private bool _isInitialized;
    private bool _isMouseDown;
    private int _currentPressedNote = -1;
    private HashSet<int> _activeNotesSet = new();

    #endregion

    #region Constructor

    public PianoKeyboard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        BuildKeyboard();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            BuildKeyboard();
        }
    }

    private static void OnNoteRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PianoKeyboard keyboard && keyboard._isInitialized)
        {
            keyboard.BuildKeyboard();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PianoKeyboard keyboard && keyboard._isInitialized)
        {
            keyboard.BuildKeyboard();
        }
    }

    private static void OnHighlightedNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PianoKeyboard keyboard && keyboard._isInitialized)
        {
            int oldNote = (int)e.OldValue;
            int newNote = (int)e.NewValue;
            keyboard.UpdateKeyHighlight(oldNote, false);
            keyboard.UpdateKeyHighlight(newNote, true);
        }
    }

    private static void OnActiveNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PianoKeyboard keyboard && keyboard._isInitialized)
        {
            keyboard.UpdateActiveNotes();
        }
    }

    #endregion

    #region Mouse Event Handlers

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isMouseDown = true;
        Mouse.Capture(KeyboardCanvas);

        var position = e.GetPosition(KeyboardCanvas);
        int note = GetNoteFromPosition(position);

        if (note >= LowestNote && note <= HighestNote)
        {
            _currentPressedNote = note;
            RaiseEvent(new NoteEventArgs(NotePressedEvent, this, note));
            UpdateKeyVisual(note, true);
        }
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMouseDown && _currentPressedNote >= 0)
        {
            RaiseEvent(new NoteEventArgs(NoteReleasedEvent, this, _currentPressedNote));
            UpdateKeyVisual(_currentPressedNote, false);
            _currentPressedNote = -1;
        }

        _isMouseDown = false;
        Mouse.Capture(null);
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(KeyboardCanvas);
        int note = GetNoteFromPosition(position);

        // Update highlighted note for hover effect
        if (!_isMouseDown)
        {
            HighlightedNote = (note >= LowestNote && note <= HighestNote) ? note : -1;
        }
        else if (_isMouseDown && note != _currentPressedNote)
        {
            // Drag to different note
            if (_currentPressedNote >= 0)
            {
                RaiseEvent(new NoteEventArgs(NoteReleasedEvent, this, _currentPressedNote));
                UpdateKeyVisual(_currentPressedNote, false);
            }

            if (note >= LowestNote && note <= HighestNote)
            {
                _currentPressedNote = note;
                RaiseEvent(new NoteEventArgs(NotePressedEvent, this, note));
                UpdateKeyVisual(note, true);
            }
            else
            {
                _currentPressedNote = -1;
            }
        }
    }

    private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown)
        {
            HighlightedNote = -1;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the total height of the keyboard based on the note range and key height.
    /// </summary>
    public double GetTotalHeight()
    {
        int noteCount = HighestNote - LowestNote + 1;
        return noteCount * KeyHeight;
    }

    /// <summary>
    /// Gets the Y position for a specific MIDI note number.
    /// Notes are drawn from top (highest) to bottom (lowest).
    /// </summary>
    public double GetYPositionForNote(int note)
    {
        // Higher notes are at the top (lower Y value)
        return (HighestNote - note) * KeyHeight;
    }

    /// <summary>
    /// Gets the note name for a MIDI note number (e.g., "C4", "F#5").
    /// </summary>
    public static string GetNoteName(int midiNote)
    {
        int noteIndex = midiNote % 12;
        int octave = (midiNote / 12) - 1; // MIDI note 0 = C-1
        return $"{NoteNames[noteIndex]}{octave}";
    }

    /// <summary>
    /// Checks if a MIDI note is a black key.
    /// </summary>
    public static bool IsNoteBlackKey(int midiNote)
    {
        return IsBlackKey[midiNote % 12];
    }

    /// <summary>
    /// Forces a rebuild of the keyboard visual.
    /// </summary>
    public void Refresh()
    {
        if (_isInitialized)
        {
            BuildKeyboard();
        }
    }

    #endregion

    #region Private Methods - Keyboard Building

    private void BuildKeyboard()
    {
        KeyboardCanvas.Children.Clear();
        _whiteKeyRects.Clear();
        _blackKeyRects.Clear();
        _noteLabels.Clear();

        double canvasWidth = ActualWidth > 0 ? ActualWidth : 80;
        double totalHeight = GetTotalHeight();

        // Set canvas height
        KeyboardCanvas.Height = totalHeight;

        // Draw keys from highest to lowest (top to bottom)
        // First pass: Draw all white keys (they go behind black keys)
        for (int note = HighestNote; note >= LowestNote; note--)
        {
            if (!IsNoteBlackKey(note))
            {
                DrawWhiteKey(note, canvasWidth);
            }
        }

        // Second pass: Draw all black keys (on top)
        for (int note = HighestNote; note >= LowestNote; note--)
        {
            if (IsNoteBlackKey(note))
            {
                DrawBlackKey(note, canvasWidth);
            }
        }

        // Update active notes display
        UpdateActiveNotes();
    }

    private void DrawWhiteKey(int note, double canvasWidth)
    {
        double y = GetYPositionForNote(note);
        double blackKeyWidth = canvasWidth * BlackKeyWidthRatio;

        // White key rectangle
        var keyRect = new Shapes.Rectangle
        {
            Width = canvasWidth - 1,
            Height = KeyHeight - 1,
            Fill = FindResource("WhiteKeyBrush") as Brush,
            Stroke = FindResource("KeyBorderBrush") as Brush,
            StrokeThickness = 1,
            RadiusX = 2,
            RadiusY = 2
        };

        Canvas.SetLeft(keyRect, 0);
        Canvas.SetTop(keyRect, y);
        KeyboardCanvas.Children.Add(keyRect);
        _whiteKeyRects[note] = keyRect;

        // Add note label for C notes (octave markers)
        int noteIndex = note % 12;
        if (noteIndex == 0) // C note
        {
            string noteName = GetNoteName(note);
            var label = new TextBlock
            {
                Text = noteName,
                Foreground = FindResource("OctaveLabelBrush") as Brush,
                FontSize = Math.Min(10, KeyHeight * 0.5),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double labelX = canvasWidth - label.DesiredSize.Width - 4;
            double labelY = y + (KeyHeight - label.DesiredSize.Height) / 2;

            Canvas.SetLeft(label, labelX);
            Canvas.SetTop(label, labelY);
            KeyboardCanvas.Children.Add(label);
            _noteLabels[note] = label;
        }
    }

    private void DrawBlackKey(int note, double canvasWidth)
    {
        double y = GetYPositionForNote(note);
        double blackKeyWidth = canvasWidth * BlackKeyWidthRatio;
        double blackKeyHeight = KeyHeight * BlackKeyHeightRatio;

        // Center the black key vertically within its row, extending slightly into adjacent white key rows
        double offsetY = (KeyHeight - blackKeyHeight) / 2;

        var keyRect = new Shapes.Rectangle
        {
            Width = blackKeyWidth,
            Height = blackKeyHeight,
            Fill = FindResource("BlackKeyBrush") as Brush,
            Stroke = FindResource("KeyBorderBrush") as Brush,
            StrokeThickness = 1,
            RadiusX = 2,
            RadiusY = 2
        };

        Canvas.SetLeft(keyRect, 0);
        Canvas.SetTop(keyRect, y + offsetY);
        Canvas.SetZIndex(keyRect, 1); // Black keys on top
        KeyboardCanvas.Children.Add(keyRect);
        _blackKeyRects[note] = keyRect;
    }

    #endregion

    #region Private Methods - Visual Updates

    private void UpdateKeyHighlight(int note, bool highlight)
    {
        if (note < LowestNote || note > HighestNote) return;

        // Don't override active state
        if (_activeNotesSet.Contains(note)) return;
        if (_currentPressedNote == note) return;

        if (IsNoteBlackKey(note))
        {
            if (_blackKeyRects.TryGetValue(note, out var rect))
            {
                rect.Fill = highlight
                    ? FindResource("ActiveKeyBrush") as Brush
                    : FindResource("BlackKeyBrush") as Brush;
                rect.Effect = highlight ? FindResource("ActiveKeyGlowEffect") as Effect : null;
            }
        }
        else
        {
            if (_whiteKeyRects.TryGetValue(note, out var rect))
            {
                rect.Fill = highlight
                    ? FindResource("ActiveKeyBrush") as Brush
                    : FindResource("WhiteKeyBrush") as Brush;
                rect.Effect = highlight ? FindResource("ActiveKeyGlowEffect") as Effect : null;
            }
        }
    }

    private void UpdateKeyVisual(int note, bool pressed)
    {
        if (note < LowestNote || note > HighestNote) return;

        Brush? fill;
        Effect? effect = null;

        if (pressed || _activeNotesSet.Contains(note))
        {
            fill = FindResource("ActiveKeyBrush") as Brush;
            effect = FindResource("ActiveKeyGlowEffect") as Effect;
        }
        else if (note == HighlightedNote)
        {
            fill = FindResource("ActiveKeyBrush") as Brush;
            effect = FindResource("ActiveKeyGlowEffect") as Effect;
        }
        else
        {
            fill = IsNoteBlackKey(note)
                ? FindResource("BlackKeyBrush") as Brush
                : FindResource("WhiteKeyBrush") as Brush;
        }

        if (IsNoteBlackKey(note))
        {
            if (_blackKeyRects.TryGetValue(note, out var rect))
            {
                rect.Fill = fill;
                rect.Effect = effect;
            }
        }
        else
        {
            if (_whiteKeyRects.TryGetValue(note, out var rect))
            {
                rect.Fill = fill;
                rect.Effect = effect;
            }
        }
    }

    private void UpdateActiveNotes()
    {
        // Clear previous active notes
        foreach (int note in _activeNotesSet)
        {
            if (note != _currentPressedNote && note != HighlightedNote)
            {
                UpdateKeyVisual(note, false);
            }
        }

        // Update new active notes set
        _activeNotesSet = ActiveNotes != null ? new HashSet<int>(ActiveNotes) : new HashSet<int>();

        // Apply active state to new notes
        foreach (int note in _activeNotesSet)
        {
            UpdateKeyVisual(note, true);
        }
    }

    #endregion

    #region Private Methods - Hit Testing

    private int GetNoteFromPosition(Point position)
    {
        double y = position.Y;
        double x = position.X;
        double canvasWidth = ActualWidth > 0 ? ActualWidth : 80;
        double blackKeyWidth = canvasWidth * BlackKeyWidthRatio;

        // Calculate which row we're in
        int noteOffset = (int)(y / KeyHeight);
        int baseNote = HighestNote - noteOffset;

        if (baseNote < LowestNote || baseNote > HighestNote)
            return -1;

        // Check if we're in the black key area (left side)
        if (x <= blackKeyWidth)
        {
            // Check if the base note is a black key
            if (IsNoteBlackKey(baseNote))
            {
                return baseNote;
            }

            // Check adjacent black keys that might overlap into this row
            // Black keys extend slightly into adjacent white key rows
            double blackKeyHeight = KeyHeight * BlackKeyHeightRatio;
            double offsetY = (KeyHeight - blackKeyHeight) / 2;
            double relativeY = y - (noteOffset * KeyHeight);

            // Check if mouse is in the upper overlap area (previous note's black key)
            if (relativeY < offsetY && baseNote < HighestNote)
            {
                int aboveNote = baseNote + 1;
                if (IsNoteBlackKey(aboveNote))
                {
                    return aboveNote;
                }
            }

            // Check if mouse is in the lower overlap area (next note's black key)
            if (relativeY > KeyHeight - offsetY && baseNote > LowestNote)
            {
                int belowNote = baseNote - 1;
                if (IsNoteBlackKey(belowNote))
                {
                    return belowNote;
                }
            }
        }

        // Default to the white key at this row
        return baseNote;
    }

    #endregion
}

/// <summary>
/// Event arguments for note pressed/released events.
/// </summary>
public class NoteEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Gets the MIDI note number (0-127).
    /// </summary>
    public int Note { get; }

    /// <summary>
    /// Gets the PianoRollNote object (if applicable).
    /// </summary>
    public PianoRollNote? PianoRollNote { get; }

    /// <summary>
    /// Gets the note name (e.g., "C4", "F#5").
    /// </summary>
    public string NoteName => PianoKeyboard.GetNoteName(Note);

    /// <summary>
    /// Gets whether the note is a black key.
    /// </summary>
    public bool IsBlackKey => PianoKeyboard.IsNoteBlackKey(Note);

    public NoteEventArgs(RoutedEvent routedEvent, int note)
        : base(routedEvent)
    {
        Note = note;
    }

    public NoteEventArgs(RoutedEvent routedEvent, object source, int note)
        : base(routedEvent, source)
    {
        Note = note;
    }

    public NoteEventArgs(PianoRollNote pianoRollNote)
        : base()
    {
        PianoRollNote = pianoRollNote;
        Note = pianoRollNote.Note;
    }

    public NoteEventArgs(RoutedEvent routedEvent, PianoRollNote pianoRollNote)
        : base(routedEvent)
    {
        PianoRollNote = pianoRollNote;
        Note = pianoRollNote.Note;
    }
}

/// <summary>
/// Event arguments for note moved events.
/// </summary>
public class NoteMovedEventArgs : RoutedEventArgs
{
    public PianoRollNote Note { get; }
    public double NewBeat { get; }
    public int NewNote { get; }

    public NoteMovedEventArgs(RoutedEvent routedEvent, PianoRollNote note, double newBeat, int newNote)
        : base(routedEvent)
    {
        Note = note;
        NewBeat = newBeat;
        NewNote = newNote;
    }

    public NoteMovedEventArgs(PianoRollNote note, double newBeat, int newNote)
        : base()
    {
        Note = note;
        NewBeat = newBeat;
        NewNote = newNote;
    }
}

/// <summary>
/// Event arguments for note resized events.
/// </summary>
public class NoteResizedEventArgs : RoutedEventArgs
{
    public PianoRollNote Note { get; }
    public double NewDuration { get; }

    public NoteResizedEventArgs(RoutedEvent routedEvent, PianoRollNote note, double newDuration)
        : base(routedEvent)
    {
        Note = note;
        NewDuration = newDuration;
    }

    public NoteResizedEventArgs(PianoRollNote note, double newDuration)
        : base()
    {
        Note = note;
        NewDuration = newDuration;
    }
}

/// <summary>
/// Generic routed event handler delegate.
/// </summary>
public delegate void RoutedEventHandler<TEventArgs>(object sender, TEventArgs e) where TEventArgs : RoutedEventArgs;
