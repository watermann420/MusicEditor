// MusicEngineEditor - Velocity Lane Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A lane control for editing note velocities in the Piano Roll.
/// Displays vertical bars representing velocity values that can be dragged to adjust.
/// </summary>
public partial class VelocityLane : UserControl
{
    #region Constants

    private const double DefaultBeatWidth = 40.0;
    private const double MinBarWidth = 4.0;
    private const double MaxBarWidth = 20.0;
    private const int BeatsPerBar = 4;
    private const int MaxVelocity = 127;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty NotesProperty =
        DependencyProperty.Register(nameof(Notes), typeof(ObservableCollection<PianoRollNote>), typeof(VelocityLane),
            new PropertyMetadata(null, OnNotesChanged));

    public static readonly DependencyProperty SelectedNotesProperty =
        DependencyProperty.Register(nameof(SelectedNotes), typeof(ObservableCollection<PianoRollNote>), typeof(VelocityLane),
            new PropertyMetadata(null, OnSelectedNotesChanged));

    public static readonly DependencyProperty TotalBeatsProperty =
        DependencyProperty.Register(nameof(TotalBeats), typeof(double), typeof(VelocityLane),
            new PropertyMetadata(16.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ZoomXProperty =
        DependencyProperty.Register(nameof(ZoomX), typeof(double), typeof(VelocityLane),
            new PropertyMetadata(1.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollXProperty =
        DependencyProperty.Register(nameof(ScrollX), typeof(double), typeof(VelocityLane),
            new PropertyMetadata(0.0, OnScrollChanged));

    public static readonly DependencyProperty BeatWidthProperty =
        DependencyProperty.Register(nameof(BeatWidth), typeof(double), typeof(VelocityLane),
            new PropertyMetadata(DefaultBeatWidth, OnLayoutPropertyChanged));

    public static readonly DependencyProperty GridSnapValueProperty =
        DependencyProperty.Register(nameof(GridSnapValue), typeof(double), typeof(VelocityLane),
            new PropertyMetadata(0.25));

    /// <summary>
    /// Gets or sets the collection of notes to display velocities for.
    /// </summary>
    public ObservableCollection<PianoRollNote>? Notes
    {
        get => (ObservableCollection<PianoRollNote>?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of currently selected notes.
    /// </summary>
    public ObservableCollection<PianoRollNote>? SelectedNotes
    {
        get => (ObservableCollection<PianoRollNote>?)GetValue(SelectedNotesProperty);
        set => SetValue(SelectedNotesProperty, value);
    }

    /// <summary>
    /// Gets or sets the total number of beats to display.
    /// </summary>
    public double TotalBeats
    {
        get => (double)GetValue(TotalBeatsProperty);
        set => SetValue(TotalBeatsProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal zoom level.
    /// </summary>
    public double ZoomX
    {
        get => (double)GetValue(ZoomXProperty);
        set => SetValue(ZoomXProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal scroll offset.
    /// </summary>
    public double ScrollX
    {
        get => (double)GetValue(ScrollXProperty);
        set => SetValue(ScrollXProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of one beat in pixels.
    /// </summary>
    public double BeatWidth
    {
        get => (double)GetValue(BeatWidthProperty);
        set => SetValue(BeatWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the grid snap value in beats.
    /// </summary>
    public double GridSnapValue
    {
        get => (double)GetValue(GridSnapValueProperty);
        set => SetValue(GridSnapValueProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a note's velocity has been changed.
    /// </summary>
    public event EventHandler<VelocityChangedEventArgs>? VelocityChanged;

    #endregion

    #region Private Fields

    private readonly Dictionary<PianoRollNote, Shapes.Rectangle> _velocityBars = [];
    private readonly HashSet<PianoRollNote> _selectedNotesInternal = [];

    private bool _isDragging;
    private PianoRollNote? _draggedNote;
    private Point _dragStartPoint;
    private int _dragStartVelocity;
    private bool _isMultiDrag;

    // Colors
    private static readonly Color VelocityBarColor = (Color)ColorConverter.ConvertFromString("#4A9EFF")!;
    private static readonly Color VelocityBarSelectedColor = (Color)ColorConverter.ConvertFromString("#7BBFFF")!;
    private static readonly Color GridLineColor = (Color)ColorConverter.ConvertFromString("#2A2A2A")!;
    private static readonly Color BarLineColor = (Color)ColorConverter.ConvertFromString("#3A3A3A")!;

    #endregion

    #region Constructor

    public VelocityLane()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeFromCollectionChanges();
    }

    #endregion

    #region Dependency Property Change Handlers

    private static void OnNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelocityLane lane)
        {
            lane.OnNotesCollectionChanged(e.OldValue as ObservableCollection<PianoRollNote>,
                                          e.NewValue as ObservableCollection<PianoRollNote>);
        }
    }

    private static void OnSelectedNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelocityLane lane)
        {
            lane.OnSelectedNotesCollectionChanged(e.OldValue as ObservableCollection<PianoRollNote>,
                                                   e.NewValue as ObservableCollection<PianoRollNote>);
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelocityLane lane)
        {
            lane.RenderAll();
        }
    }

    private static void OnScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelocityLane lane)
        {
            lane.ApplyScrollTransform();
        }
    }

    private void OnNotesCollectionChanged(ObservableCollection<PianoRollNote>? oldCollection,
                                          ObservableCollection<PianoRollNote>? newCollection)
    {
        if (oldCollection != null)
        {
            oldCollection.CollectionChanged -= OnNotesCollectionItemsChanged;
        }

        if (newCollection != null)
        {
            newCollection.CollectionChanged += OnNotesCollectionItemsChanged;
        }

        RenderVelocityBars();
    }

    private void OnSelectedNotesCollectionChanged(ObservableCollection<PianoRollNote>? oldCollection,
                                                   ObservableCollection<PianoRollNote>? newCollection)
    {
        if (oldCollection != null)
        {
            oldCollection.CollectionChanged -= OnSelectedNotesItemsChanged;
        }

        if (newCollection != null)
        {
            newCollection.CollectionChanged += OnSelectedNotesItemsChanged;
        }

        SyncSelectedNotes();
        UpdateBarVisuals();
    }

    private void OnNotesCollectionItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderVelocityBars();
    }

    private void OnSelectedNotesItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncSelectedNotes();
        UpdateBarVisuals();
    }

    private void UnsubscribeFromCollectionChanges()
    {
        if (Notes != null)
        {
            Notes.CollectionChanged -= OnNotesCollectionItemsChanged;
        }

        if (SelectedNotes != null)
        {
            SelectedNotes.CollectionChanged -= OnSelectedNotesItemsChanged;
        }
    }

    private void SyncSelectedNotes()
    {
        _selectedNotesInternal.Clear();
        if (SelectedNotes != null)
        {
            foreach (var note in SelectedNotes)
            {
                _selectedNotesInternal.Add(note);
            }
        }
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        if (!IsLoaded) return;

        UpdateCanvasSizes();
        RenderGrid();
        RenderVelocityBars();
        RenderReferenceLines();
        ApplyScrollTransform();
    }

    private void UpdateCanvasSizes()
    {
        var totalWidth = GetTotalWidth();
        var totalHeight = ActualHeight;

        GridCanvas.Width = totalWidth;
        GridCanvas.Height = totalHeight;
        VelocityCanvas.Width = totalWidth;
        VelocityCanvas.Height = totalHeight;
        ReferenceCanvas.Width = totalWidth;
        ReferenceCanvas.Height = totalHeight;
    }

    private void RenderGrid()
    {
        GridCanvas.Children.Clear();

        var totalWidth = GetTotalWidth();
        var totalHeight = VelocityCanvas.ActualHeight > 0 ? VelocityCanvas.ActualHeight : ActualHeight - 20;
        var effectiveBeatWidth = BeatWidth * ZoomX;

        // Draw vertical grid lines (beats and bars)
        int totalBeatsInt = (int)Math.Ceiling(TotalBeats);
        for (int beat = 0; beat <= totalBeatsInt; beat++)
        {
            var x = beat * effectiveBeatWidth;
            var isBarLine = beat % BeatsPerBar == 0;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = totalHeight,
                Stroke = new SolidColorBrush(isBarLine ? BarLineColor : GridLineColor),
                StrokeThickness = isBarLine ? 1.5 : 0.5,
                Opacity = isBarLine ? 0.8 : 0.4
            };
            GridCanvas.Children.Add(line);
        }

        // Draw horizontal reference lines (velocity levels)
        int[] referenceVelocities = [32, 64, 96, 127];
        foreach (var velocity in referenceVelocities)
        {
            var y = VelocityToY(velocity, totalHeight);
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = totalWidth,
                Y2 = y,
                Stroke = new SolidColorBrush(GridLineColor),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection([4, 4]),
                Opacity = 0.5
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void RenderVelocityBars()
    {
        VelocityCanvas.Children.Clear();
        _velocityBars.Clear();

        if (Notes == null) return;

        var totalHeight = VelocityCanvas.ActualHeight > 0 ? VelocityCanvas.ActualHeight : ActualHeight - 20;
        var effectiveBeatWidth = BeatWidth * ZoomX;

        foreach (var note in Notes)
        {
            var bar = CreateVelocityBar(note, effectiveBeatWidth, totalHeight);
            _velocityBars[note] = bar;
            VelocityCanvas.Children.Add(bar);
        }
    }

    private Shapes.Rectangle CreateVelocityBar(PianoRollNote note, double effectiveBeatWidth, double totalHeight)
    {
        var x = note.StartBeat * effectiveBeatWidth;
        var barWidth = Math.Clamp(note.Duration * effectiveBeatWidth * 0.6, MinBarWidth, MaxBarWidth);
        var barHeight = (note.Velocity / (double)MaxVelocity) * (totalHeight - 4);
        var y = totalHeight - barHeight - 2;

        var isSelected = _selectedNotesInternal.Contains(note);

        var bar = new Shapes.Rectangle
        {
            Width = barWidth,
            Height = Math.Max(4, barHeight),
            RadiusX = 2,
            RadiusY = 2,
            Fill = isSelected
                ? FindResource("VelocityBarSelectedGradient") as Brush
                : FindResource("VelocityBarGradient") as Brush,
            Stroke = isSelected ? new SolidColorBrush(Colors.White) : null,
            StrokeThickness = isSelected ? 1 : 0,
            Cursor = Cursors.SizeNS,
            Tag = note,
            ToolTip = $"Velocity: {note.Velocity}"
        };

        Canvas.SetLeft(bar, x + (note.Duration * effectiveBeatWidth - barWidth) / 2);
        Canvas.SetTop(bar, y);

        return bar;
    }

    private void RenderReferenceLines()
    {
        ReferenceCanvas.Children.Clear();

        var totalHeight = VelocityCanvas.ActualHeight > 0 ? VelocityCanvas.ActualHeight : ActualHeight - 20;

        // Draw a line at the default velocity (100)
        var defaultVelocityY = VelocityToY(100, totalHeight);
        var defaultLine = new Shapes.Line
        {
            X1 = 0,
            Y1 = defaultVelocityY,
            X2 = GetTotalWidth(),
            Y2 = defaultVelocityY,
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9500")!),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([2, 4]),
            Opacity = 0.6
        };
        ReferenceCanvas.Children.Add(defaultLine);
    }

    private void UpdateBarVisuals()
    {
        var totalHeight = VelocityCanvas.ActualHeight > 0 ? VelocityCanvas.ActualHeight : ActualHeight - 20;

        foreach (var kvp in _velocityBars)
        {
            var note = kvp.Key;
            var bar = kvp.Value;
            var isSelected = _selectedNotesInternal.Contains(note);

            bar.Fill = isSelected
                ? FindResource("VelocityBarSelectedGradient") as Brush
                : FindResource("VelocityBarGradient") as Brush;
            bar.Stroke = isSelected ? new SolidColorBrush(Colors.White) : null;
            bar.StrokeThickness = isSelected ? 1 : 0;
        }
    }

    private void ApplyScrollTransform()
    {
        var transform = new TranslateTransform(-ScrollX, 0);
        GridCanvas.RenderTransform = transform;
        VelocityCanvas.RenderTransform = transform;
        ReferenceCanvas.RenderTransform = transform;
    }

    #endregion

    #region Mouse Event Handlers

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(VelocityCanvas);
        var adjustedX = position.X + ScrollX;

        // Find if we clicked on a velocity bar
        _draggedNote = FindNoteAtPosition(adjustedX, position.Y);

        if (_draggedNote != null)
        {
            _isDragging = true;
            _dragStartPoint = position;
            _dragStartVelocity = _draggedNote.Velocity;

            // If the note is selected and we have multiple selections, drag all selected
            _isMultiDrag = _selectedNotesInternal.Contains(_draggedNote) && _selectedNotesInternal.Count > 1;

            VelocityCanvas.CaptureMouse();
        }
        else
        {
            // Draw velocity line mode - click to set velocity at that position
            SetVelocityAtPosition(adjustedX, position.Y);
        }

        e.Handled = true;
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _draggedNote = null;
            _isMultiDrag = false;
            VelocityCanvas.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(VelocityCanvas);

        if (_isDragging && _draggedNote != null)
        {
            var deltaY = _dragStartPoint.Y - position.Y;
            var totalHeight = VelocityCanvas.ActualHeight;
            var velocityDelta = (int)(deltaY / totalHeight * MaxVelocity);

            if (_isMultiDrag)
            {
                // Adjust all selected notes
                foreach (var note in _selectedNotesInternal)
                {
                    int originalVelocity = note == _draggedNote ? _dragStartVelocity : note.Velocity;
                    int newVelocity = Math.Clamp(originalVelocity + velocityDelta, 0, MaxVelocity);

                    if (note.Velocity != newVelocity)
                    {
                        note.Velocity = newVelocity;
                        UpdateSingleBar(note);
                        VelocityChanged?.Invoke(this, new VelocityChangedEventArgs(note, newVelocity));
                    }
                }
            }
            else
            {
                int newVelocity = Math.Clamp(_dragStartVelocity + velocityDelta, 0, MaxVelocity);

                if (_draggedNote.Velocity != newVelocity)
                {
                    _draggedNote.Velocity = newVelocity;
                    UpdateSingleBar(_draggedNote);
                    VelocityChanged?.Invoke(this, new VelocityChangedEventArgs(_draggedNote, newVelocity));
                }
            }
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            // Drawing mode - set velocity as we drag
            var adjustedX = position.X + ScrollX;
            SetVelocityAtPosition(adjustedX, position.Y);
        }
    }

    private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
    {
        // Keep dragging even if mouse leaves
    }

    #endregion

    #region Helper Methods

    private double GetTotalWidth()
    {
        return TotalBeats * BeatWidth * ZoomX;
    }

    private double VelocityToY(int velocity, double totalHeight)
    {
        return totalHeight - (velocity / (double)MaxVelocity) * (totalHeight - 4) - 2;
    }

    private int YToVelocity(double y, double totalHeight)
    {
        var ratio = (totalHeight - y - 2) / (totalHeight - 4);
        return Math.Clamp((int)(ratio * MaxVelocity), 0, MaxVelocity);
    }

    private PianoRollNote? FindNoteAtPosition(double x, double y)
    {
        if (Notes == null) return null;

        var effectiveBeatWidth = BeatWidth * ZoomX;
        var beat = x / effectiveBeatWidth;

        // Find notes that overlap with this beat position
        foreach (var note in Notes)
        {
            if (beat >= note.StartBeat && beat < note.GetEndBeat())
            {
                return note;
            }
        }

        return null;
    }

    private void SetVelocityAtPosition(double x, double y)
    {
        var totalHeight = VelocityCanvas.ActualHeight;
        var effectiveBeatWidth = BeatWidth * ZoomX;
        var beat = x / effectiveBeatWidth;
        var velocity = YToVelocity(y, totalHeight);

        if (Notes == null) return;

        // Find and update any notes at this position
        foreach (var note in Notes)
        {
            if (beat >= note.StartBeat && beat < note.GetEndBeat())
            {
                if (note.Velocity != velocity)
                {
                    note.Velocity = velocity;
                    UpdateSingleBar(note);
                    VelocityChanged?.Invoke(this, new VelocityChangedEventArgs(note, velocity));
                }
            }
        }
    }

    private void UpdateSingleBar(PianoRollNote note)
    {
        if (!_velocityBars.TryGetValue(note, out var bar)) return;

        var totalHeight = VelocityCanvas.ActualHeight > 0 ? VelocityCanvas.ActualHeight : ActualHeight - 20;
        var effectiveBeatWidth = BeatWidth * ZoomX;

        var barHeight = (note.Velocity / (double)MaxVelocity) * (totalHeight - 4);
        var y = totalHeight - barHeight - 2;

        bar.Height = Math.Max(4, barHeight);
        Canvas.SetTop(bar, y);
        bar.ToolTip = $"Velocity: {note.Velocity}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the velocity lane display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    /// <summary>
    /// Sets the velocity for all notes at a specific beat position.
    /// </summary>
    /// <param name="beat">The beat position.</param>
    /// <param name="velocity">The velocity value (0-127).</param>
    public void SetVelocityAtBeat(double beat, int velocity)
    {
        velocity = Math.Clamp(velocity, 0, MaxVelocity);

        if (Notes == null) return;

        foreach (var note in Notes)
        {
            if (beat >= note.StartBeat && beat < note.GetEndBeat())
            {
                note.Velocity = velocity;
                UpdateSingleBar(note);
                VelocityChanged?.Invoke(this, new VelocityChangedEventArgs(note, velocity));
            }
        }
    }

    /// <summary>
    /// Sets the velocity for all selected notes.
    /// </summary>
    /// <param name="velocity">The velocity value (0-127).</param>
    public void SetSelectedVelocity(int velocity)
    {
        velocity = Math.Clamp(velocity, 0, MaxVelocity);

        foreach (var note in _selectedNotesInternal)
        {
            note.Velocity = velocity;
            UpdateSingleBar(note);
            VelocityChanged?.Invoke(this, new VelocityChangedEventArgs(note, velocity));
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for velocity changed events.
/// </summary>
public class VelocityChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the note whose velocity was changed.
    /// </summary>
    public PianoRollNote Note { get; }

    /// <summary>
    /// Gets the new velocity value.
    /// </summary>
    public int NewVelocity { get; }

    /// <summary>
    /// Creates a new VelocityChangedEventArgs.
    /// </summary>
    /// <param name="note">The note that was changed.</param>
    /// <param name="newVelocity">The new velocity value.</param>
    public VelocityChangedEventArgs(PianoRollNote note, int newVelocity)
    {
        Note = note;
        NewVelocity = newVelocity;
    }
}
