// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: MIDI clip control with mini piano roll preview for arrangement view.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Arrangement;

/// <summary>
/// Represents a note in the mini piano roll preview.
/// </summary>
public struct MiniNote
{
    /// <summary>
    /// Gets or sets the MIDI note number (0-127).
    /// </summary>
    public int Note { get; set; }

    /// <summary>
    /// Gets or sets the start position in beats relative to clip start.
    /// </summary>
    public double Start { get; set; }

    /// <summary>
    /// Gets or sets the duration in beats.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Gets or sets the velocity (0-127).
    /// </summary>
    public int Velocity { get; set; }

    public MiniNote(int note, double start, double duration, int velocity = 100)
    {
        Note = note;
        Start = start;
        Duration = duration;
        Velocity = velocity;
    }
}

/// <summary>
/// Control for displaying MIDI clips with mini piano roll preview in the arrangement view.
/// </summary>
public partial class MidiClipControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ClipNameProperty =
        DependencyProperty.Register(nameof(ClipName), typeof(string), typeof(MidiClipControl),
            new PropertyMetadata("MIDI Clip", OnClipNameChanged));

    public static readonly DependencyProperty ClipColorProperty =
        DependencyProperty.Register(nameof(ClipColor), typeof(Color), typeof(MidiClipControl),
            new PropertyMetadata(Color.FromRgb(0x00, 0xD9, 0xFF), OnClipColorChanged));

    public static readonly DependencyProperty StartBeatProperty =
        DependencyProperty.Register(nameof(StartBeat), typeof(double), typeof(MidiClipControl),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(double), typeof(MidiClipControl),
            new PropertyMetadata(4.0, OnDurationChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(MidiClipControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty NotesProperty =
        DependencyProperty.Register(nameof(Notes), typeof(IList<MiniNote>), typeof(MidiClipControl),
            new PropertyMetadata(null, OnNotesChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the clip name.
    /// </summary>
    public string ClipName
    {
        get => (string)GetValue(ClipNameProperty);
        set => SetValue(ClipNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the clip color.
    /// </summary>
    public Color ClipColor
    {
        get => (Color)GetValue(ClipColorProperty);
        set => SetValue(ClipColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the start position in beats.
    /// </summary>
    public double StartBeat
    {
        get => (double)GetValue(StartBeatProperty);
        set => SetValue(StartBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the duration in beats.
    /// </summary>
    public double Duration
    {
        get => (double)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the clip is selected.
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Gets or sets the notes to display in the mini piano roll.
    /// </summary>
    public IList<MiniNote>? Notes
    {
        get => (IList<MiniNote>?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the clip is selected.
    /// </summary>
    public event EventHandler? ClipSelected;

    /// <summary>
    /// Event raised when the clip is double-clicked for editing.
    /// </summary>
    public event EventHandler? EditRequested;

    /// <summary>
    /// Event raised when duplicate is requested.
    /// </summary>
    public event EventHandler? DuplicateRequested;

    /// <summary>
    /// Event raised when delete is requested.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Event raised when the clip is moved.
    /// </summary>
    public event EventHandler<double>? ClipMoved;

    /// <summary>
    /// Event raised when the clip is resized from the left.
    /// </summary>
    public event EventHandler<double>? ClipResizedLeft;

    /// <summary>
    /// Event raised when the clip is resized from the right.
    /// </summary>
    public event EventHandler<double>? ClipResizedRight;

    #endregion

    #region Fields

    private bool _isDragging;
    private bool _isResizingLeft;
    private bool _isResizingRight;
    private Point _dragStartPoint;
    private double _originalStartBeat;
    private double _originalDuration;

    private const int MinNoteHeight = 2;
    private const int MaxNoteHeight = 4;

    #endregion

    public MidiClipControl()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseDoubleClick += OnMouseDoubleClick;
        SizeChanged += OnSizeChanged;
    }

    #region Property Changed Callbacks

    private static void OnClipNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control)
        {
            control.ClipNameLabel.Text = e.NewValue as string ?? "MIDI Clip";
        }
    }

    private static void OnClipColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control && e.NewValue is Color color)
        {
            control.UpdateClipColor(color);
        }
    }

    private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control)
        {
            control.RenderNotes();
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control)
        {
            control.SelectionBorder.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control)
        {
            control.RenderNotes();
        }
    }

    #endregion

    #region Event Handlers

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderNotes();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartBeat = StartBeat;
        _originalDuration = Duration;
        _isDragging = true;

        IsSelected = true;
        ClipSelected?.Invoke(this, EventArgs.Empty);

        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizingLeft && Math.Abs(StartBeat - _originalStartBeat) > 0.01)
        {
            ClipResizedLeft?.Invoke(this, StartBeat - _originalStartBeat);
        }
        else if (_isResizingRight && Math.Abs(Duration - _originalDuration) > 0.01)
        {
            ClipResizedRight?.Invoke(this, Duration - _originalDuration);
        }
        else if (_isDragging && Math.Abs(StartBeat - _originalStartBeat) > 0.01)
        {
            ClipMoved?.Invoke(this, StartBeat - _originalStartBeat);
        }

        _isDragging = false;
        _isResizingLeft = false;
        _isResizingRight = false;

        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // Movement is typically handled by the parent container
        // This is placeholder for potential direct manipulation
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        EditRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void LeftResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isResizingLeft = true;
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartBeat = StartBeat;
        _originalDuration = Duration;

        CaptureMouse();
        e.Handled = true;
    }

    private void RightResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isResizingRight = true;
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartBeat = StartBeat;
        _originalDuration = Duration;

        CaptureMouse();
        e.Handled = true;
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DuplicateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Rendering

    private void UpdateClipColor(Color color)
    {
        var fillBrush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
        var borderBrush = new SolidColorBrush(color);

        ClipBorder.Background = fillBrush;
        ClipBorder.BorderBrush = borderBrush;
        HeaderBar.Background = borderBrush;
    }

    /// <summary>
    /// Renders the mini piano roll preview with note rectangles.
    /// </summary>
    public void RenderNotes()
    {
        PianoRollCanvas.Children.Clear();

        var notes = Notes;
        if (notes == null || notes.Count == 0)
            return;

        var width = PianoRollCanvas.ActualWidth;
        var height = PianoRollCanvas.ActualHeight;

        if (width <= 0 || height <= 0 || Duration <= 0)
            return;

        // Find note range
        var minNote = 127;
        var maxNote = 0;
        foreach (var note in notes)
        {
            if (note.Note < minNote) minNote = note.Note;
            if (note.Note > maxNote) maxNote = note.Note;
        }

        if (minNote > maxNote)
            return;

        // Ensure reasonable range
        var range = maxNote - minNote + 1;
        if (range < 12)
        {
            var expand = (12 - range) / 2;
            minNote = Math.Max(0, minNote - expand);
            maxNote = Math.Min(127, maxNote + expand);
            range = maxNote - minNote + 1;
        }

        var noteHeight = Math.Max(MinNoteHeight, Math.Min(MaxNoteHeight, height / range));
        var pixelsPerBeat = width / Duration;

        // Render notes as simplified rectangles
        foreach (var note in notes)
        {
            if (note.Start >= Duration)
                continue;

            var noteY = height - ((note.Note - minNote + 1) * (height / range));
            var noteX = note.Start * pixelsPerBeat;
            var noteWidth = Math.Max(2, Math.Min(note.Duration, Duration - note.Start) * pixelsPerBeat - 1);

            if (noteX + noteWidth < 0 || noteX > width)
                continue;

            var rect = new Rectangle
            {
                Width = noteWidth,
                Height = Math.Max(2, noteHeight - 1),
                Fill = Brushes.White,
                Opacity = note.Velocity / 127.0 * 0.6 + 0.4,
                RadiusX = 1,
                RadiusY = 1
            };

            Canvas.SetLeft(rect, noteX);
            Canvas.SetTop(rect, noteY);
            PianoRollCanvas.Children.Add(rect);
        }
    }

    #endregion
}
