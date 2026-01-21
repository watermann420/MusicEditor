using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Defines the available tools for the Piano Roll editor.
/// </summary>
public enum PianoRollTool
{
    /// <summary>
    /// Selection tool for selecting and moving notes.
    /// </summary>
    Select,

    /// <summary>
    /// Draw tool for creating new notes.
    /// </summary>
    Draw,

    /// <summary>
    /// Erase tool for deleting notes.
    /// </summary>
    Erase
}

/// <summary>
/// A canvas control for drawing and editing MIDI notes in a Piano Roll view.
/// </summary>
public partial class NoteCanvas : UserControl
{
    #region Constants

    private const double DefaultNoteHeight = 20.0;
    private const double DefaultBeatWidth = 40.0;
    private const double NoteCornerRadius = 3.0;
    private const double ResizeHandleWidth = 8.0;
    private const double MinNoteDuration = 0.125;
    private const int BeatsPerBar = 4;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty NotesProperty =
        DependencyProperty.Register(nameof(Notes), typeof(IEnumerable<PianoRollNote>), typeof(NoteCanvas),
            new PropertyMetadata(null, OnNotesChanged));

    public static readonly DependencyProperty SelectedNotesProperty =
        DependencyProperty.Register(nameof(SelectedNotes), typeof(IEnumerable<PianoRollNote>), typeof(NoteCanvas),
            new PropertyMetadata(null, OnSelectedNotesChanged));

    public static readonly DependencyProperty LowestNoteProperty =
        DependencyProperty.Register(nameof(LowestNote), typeof(int), typeof(NoteCanvas),
            new PropertyMetadata(21, OnLayoutPropertyChanged)); // A0

    public static readonly DependencyProperty HighestNoteProperty =
        DependencyProperty.Register(nameof(HighestNote), typeof(int), typeof(NoteCanvas),
            new PropertyMetadata(108, OnLayoutPropertyChanged)); // C8

    public static readonly DependencyProperty TotalBeatsProperty =
        DependencyProperty.Register(nameof(TotalBeats), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(16.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty GridSnapValueProperty =
        DependencyProperty.Register(nameof(GridSnapValue), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(0.25));

    public static readonly DependencyProperty ZoomXProperty =
        DependencyProperty.Register(nameof(ZoomX), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(1.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ZoomYProperty =
        DependencyProperty.Register(nameof(ZoomY), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(1.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollXProperty =
        DependencyProperty.Register(nameof(ScrollX), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(0.0, OnScrollChanged));

    public static readonly DependencyProperty ScrollYProperty =
        DependencyProperty.Register(nameof(ScrollY), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(0.0, OnScrollChanged));

    public static readonly DependencyProperty CurrentToolProperty =
        DependencyProperty.Register(nameof(CurrentTool), typeof(PianoRollTool), typeof(NoteCanvas),
            new PropertyMetadata(PianoRollTool.Select, OnCurrentToolChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(0.0, OnPlayheadPositionChanged));

    public static readonly DependencyProperty NoteHeightProperty =
        DependencyProperty.Register(nameof(NoteHeight), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(DefaultNoteHeight, OnLayoutPropertyChanged));

    public static readonly DependencyProperty BeatWidthProperty =
        DependencyProperty.Register(nameof(BeatWidth), typeof(double), typeof(NoteCanvas),
            new PropertyMetadata(DefaultBeatWidth, OnLayoutPropertyChanged));

    public IEnumerable<PianoRollNote>? Notes
    {
        get => (IEnumerable<PianoRollNote>?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public IEnumerable<PianoRollNote>? SelectedNotes
    {
        get => (IEnumerable<PianoRollNote>?)GetValue(SelectedNotesProperty);
        set => SetValue(SelectedNotesProperty, value);
    }

    public int LowestNote
    {
        get => (int)GetValue(LowestNoteProperty);
        set => SetValue(LowestNoteProperty, value);
    }

    public int HighestNote
    {
        get => (int)GetValue(HighestNoteProperty);
        set => SetValue(HighestNoteProperty, value);
    }

    public double TotalBeats
    {
        get => (double)GetValue(TotalBeatsProperty);
        set => SetValue(TotalBeatsProperty, value);
    }

    public double GridSnapValue
    {
        get => (double)GetValue(GridSnapValueProperty);
        set => SetValue(GridSnapValueProperty, value);
    }

    public double ZoomX
    {
        get => (double)GetValue(ZoomXProperty);
        set => SetValue(ZoomXProperty, value);
    }

    public double ZoomY
    {
        get => (double)GetValue(ZoomYProperty);
        set => SetValue(ZoomYProperty, value);
    }

    public double ScrollX
    {
        get => (double)GetValue(ScrollXProperty);
        set => SetValue(ScrollXProperty, value);
    }

    public double ScrollY
    {
        get => (double)GetValue(ScrollYProperty);
        set => SetValue(ScrollYProperty, value);
    }

    public PianoRollTool CurrentTool
    {
        get => (PianoRollTool)GetValue(CurrentToolProperty);
        set => SetValue(CurrentToolProperty, value);
    }

    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    public double NoteHeight
    {
        get => (double)GetValue(NoteHeightProperty);
        set => SetValue(NoteHeightProperty, value);
    }

    public double BeatWidth
    {
        get => (double)GetValue(BeatWidthProperty);
        set => SetValue(BeatWidthProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<NoteEventArgs>? NoteAdded;
    public event EventHandler<NoteEventArgs>? NoteDeleted;
    public event EventHandler<NoteEventArgs>? NoteSelected;
    public event EventHandler<NoteMovedEventArgs>? NoteMoved;
    public event EventHandler<NoteResizedEventArgs>? NoteResized;
    public event EventHandler? SelectionChanged;

    #endregion

    #region Private Fields

    private readonly Dictionary<PianoRollNote, Shapes.Rectangle> _noteRectangles = new();
    private readonly HashSet<PianoRollNote> _selectedNotesInternal = new();
    private Shapes.Rectangle? _ghostNoteRect;

    // Mouse interaction state
    private bool _isDragging;
    private bool _isResizing;
    private bool _isBoxSelecting;
    private Point _dragStartPoint;
    private Point _dragCurrentPoint;
    private PianoRollNote? _draggedNote;
    private double _dragStartBeat;
    private int _dragStartNoteNumber;
    private double _dragStartDuration;
    private Point _boxSelectStart;

    // Colors
    private static readonly Color NoteDefaultColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A9EFF")!;
    private static readonly Color NoteSelectedBorderColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF")!;
    private static readonly Color GridLineColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A")!;
    private static readonly Color BarLineColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString("#3A3A3A")!;
    private static readonly Color WhiteKeyLaneColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A1A1A")!;
    private static readonly Color BlackKeyLaneColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString("#151515")!;
    private static readonly Color PlayheadColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5555")!;
    private static readonly Color GhostNoteColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A9EFF")!;

    #endregion

    #region Constructor

    public NoteCanvas()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Event Handlers - Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Attach mouse event handlers
        NotesCanvas.MouseLeftButtonDown += OnCanvasMouseLeftButtonDown;
        NotesCanvas.MouseLeftButtonUp += OnCanvasMouseLeftButtonUp;
        NotesCanvas.MouseRightButtonDown += OnCanvasMouseRightButtonDown;
        NotesCanvas.MouseMove += OnCanvasMouseMove;
        NotesCanvas.MouseLeave += OnCanvasMouseLeave;

        RenderAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Detach mouse event handlers
        NotesCanvas.MouseLeftButtonDown -= OnCanvasMouseLeftButtonDown;
        NotesCanvas.MouseLeftButtonUp -= OnCanvasMouseLeftButtonUp;
        NotesCanvas.MouseRightButtonDown -= OnCanvasMouseRightButtonDown;
        NotesCanvas.MouseMove -= OnCanvasMouseMove;
        NotesCanvas.MouseLeave -= OnCanvasMouseLeave;
    }

    #endregion

    #region Dependency Property Change Handlers

    private static void OnNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteCanvas canvas)
        {
            canvas.RenderNotes();
        }
    }

    private static void OnSelectedNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteCanvas canvas)
        {
            canvas.SyncSelectedNotes();
            canvas.UpdateNoteVisuals();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteCanvas canvas)
        {
            canvas.RenderAll();
        }
    }

    private static void OnScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteCanvas canvas)
        {
            canvas.ApplyScrollTransform();
        }
    }

    private static void OnCurrentToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteCanvas canvas)
        {
            canvas.UpdateCursor();
            canvas.HideGhostNote();
        }
    }

    private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteCanvas canvas)
        {
            canvas.UpdatePlayheadPosition();
        }
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        if (!IsLoaded) return;

        UpdateCanvasSizes();
        RenderGrid();
        RenderLanes();
        RenderNotes();
        UpdatePlayheadPosition();
        ApplyScrollTransform();
    }

    private void UpdateCanvasSizes()
    {
        var totalWidth = GetTotalWidth();
        var totalHeight = GetTotalHeight();

        GridCanvas.Width = totalWidth;
        GridCanvas.Height = totalHeight;
        LaneCanvas.Width = totalWidth;
        LaneCanvas.Height = totalHeight;
        NotesCanvas.Width = totalWidth;
        NotesCanvas.Height = totalHeight;
        GhostNoteCanvas.Width = totalWidth;
        GhostNoteCanvas.Height = totalHeight;
        SelectionCanvas.Width = totalWidth;
        SelectionCanvas.Height = totalHeight;
        PlayheadCanvas.Width = totalWidth;
        PlayheadCanvas.Height = totalHeight;
    }

    private void RenderGrid()
    {
        GridCanvas.Children.Clear();

        var totalWidth = GetTotalWidth();
        var totalHeight = GetTotalHeight();
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
                StrokeThickness = isBarLine ? 2.0 : 1.0,
                Opacity = isBarLine ? 0.8 : 0.5
            };
            GridCanvas.Children.Add(line);

            // Sub-beat lines (quarters)
            if (beat < totalBeatsInt && effectiveBeatWidth > 30)
            {
                for (int sub = 1; sub < 4; sub++)
                {
                    var subX = x + (sub * effectiveBeatWidth / 4);
                    var subLine = new Shapes.Line
                    {
                        X1 = subX,
                        Y1 = 0,
                        X2 = subX,
                        Y2 = totalHeight,
                        Stroke = new SolidColorBrush(GridLineColor),
                        StrokeThickness = 0.5,
                        Opacity = 0.3
                    };
                    GridCanvas.Children.Add(subLine);
                }
            }
        }

        // Draw horizontal lines between note lanes
        var effectiveNoteHeight = NoteHeight * ZoomY;
        int noteCount = HighestNote - LowestNote + 1;
        for (int i = 0; i <= noteCount; i++)
        {
            var y = i * effectiveNoteHeight;
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = totalWidth,
                Y2 = y,
                Stroke = new SolidColorBrush(GridLineColor),
                StrokeThickness = 0.5,
                Opacity = 0.3
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void RenderLanes()
    {
        LaneCanvas.Children.Clear();

        var totalWidth = GetTotalWidth();
        var effectiveNoteHeight = NoteHeight * ZoomY;

        // Draw alternating lane backgrounds for white/black keys
        for (int midiNote = LowestNote; midiNote <= HighestNote; midiNote++)
        {
            var y = NoteToY(midiNote);
            var isBlackKey = PianoRollNote.IsBlackKey(midiNote);

            var rect = new Shapes.Rectangle
            {
                Width = totalWidth,
                Height = effectiveNoteHeight,
                Fill = new SolidColorBrush(isBlackKey ? BlackKeyLaneColor : WhiteKeyLaneColor)
            };

            Canvas.SetLeft(rect, 0);
            Canvas.SetTop(rect, y);
            LaneCanvas.Children.Add(rect);
        }
    }

    private void RenderNotes()
    {
        NotesCanvas.Children.Clear();
        _noteRectangles.Clear();

        if (Notes == null) return;

        foreach (var note in Notes)
        {
            var rect = CreateNoteRectangle(note);
            _noteRectangles[note] = rect;
            NotesCanvas.Children.Add(rect);
        }
    }

    private Shapes.Rectangle CreateNoteRectangle(PianoRollNote note)
    {
        var effectiveBeatWidth = BeatWidth * ZoomX;
        var effectiveNoteHeight = NoteHeight * ZoomY;

        var x = note.StartBeat * effectiveBeatWidth;
        var y = NoteToY(note.Note);
        var width = Math.Max(note.Duration * effectiveBeatWidth - 2, 4);
        var height = effectiveNoteHeight - 2;

        // Calculate opacity based on velocity (0-127)
        var velocityOpacity = 0.4 + (note.Velocity / 127.0) * 0.6;

        var isSelected = _selectedNotesInternal.Contains(note);

        var rect = new Shapes.Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = NoteCornerRadius,
            RadiusY = NoteCornerRadius,
            Fill = new SolidColorBrush(NoteDefaultColor) { Opacity = velocityOpacity },
            Stroke = isSelected ? new SolidColorBrush(NoteSelectedBorderColor) : null,
            StrokeThickness = isSelected ? 2.0 : 0,
            Cursor = GetNoteCursor(note),
            Tag = note
        };

        if (isSelected)
        {
            rect.Effect = FindResource("SelectedNoteGlow") as System.Windows.Media.Effects.Effect;
        }
        else
        {
            rect.Effect = FindResource("NoteDropShadow") as System.Windows.Media.Effects.Effect;
        }

        Canvas.SetLeft(rect, x + 1);
        Canvas.SetTop(rect, y + 1);

        // Attach event handlers
        rect.MouseLeftButtonDown += OnNoteMouseLeftButtonDown;
        rect.MouseRightButtonDown += OnNoteMouseRightButtonDown;
        rect.MouseMove += OnNoteMouseMove;

        return rect;
    }

    private void UpdateNoteVisuals()
    {
        foreach (var kvp in _noteRectangles)
        {
            var note = kvp.Key;
            var rect = kvp.Value;
            var isSelected = _selectedNotesInternal.Contains(note);

            rect.Stroke = isSelected ? new SolidColorBrush(NoteSelectedBorderColor) : null;
            rect.StrokeThickness = isSelected ? 2.0 : 0;

            if (isSelected)
            {
                rect.Effect = FindResource("SelectedNoteGlow") as System.Windows.Media.Effects.Effect;
            }
            else
            {
                rect.Effect = FindResource("NoteDropShadow") as System.Windows.Media.Effects.Effect;
            }
        }
    }

    private void UpdatePlayheadPosition()
    {
        var effectiveBeatWidth = BeatWidth * ZoomX;
        var x = PlayheadPosition * effectiveBeatWidth;
        var totalHeight = GetTotalHeight();

        Canvas.SetLeft(PlayheadLine, x);
        PlayheadLine.Y2 = totalHeight;
    }

    private void ApplyScrollTransform()
    {
        var transform = new TranslateTransform(-ScrollX, -ScrollY);
        GridCanvas.RenderTransform = transform;
        LaneCanvas.RenderTransform = transform;
        NotesCanvas.RenderTransform = transform;
        GhostNoteCanvas.RenderTransform = transform;
        SelectionCanvas.RenderTransform = transform;
        PlayheadCanvas.RenderTransform = transform;
    }

    #endregion

    #region Ghost Note

    private void ShowGhostNote(Point position)
    {
        if (CurrentTool != PianoRollTool.Draw) return;

        var beat = SnapToBeat(XToBeat(position.X + ScrollX));
        var midiNote = YToNote(position.Y + ScrollY);

        if (midiNote < LowestNote || midiNote > HighestNote) return;

        var effectiveBeatWidth = BeatWidth * ZoomX;
        var effectiveNoteHeight = NoteHeight * ZoomY;

        var x = beat * effectiveBeatWidth;
        var y = NoteToY(midiNote);
        var width = GridSnapValue * effectiveBeatWidth;

        if (_ghostNoteRect == null)
        {
            _ghostNoteRect = new Shapes.Rectangle
            {
                RadiusX = NoteCornerRadius,
                RadiusY = NoteCornerRadius,
                Fill = new SolidColorBrush(GhostNoteColor) { Opacity = 0.3 },
                Stroke = new SolidColorBrush(GhostNoteColor),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            GhostNoteCanvas.Children.Add(_ghostNoteRect);
        }

        _ghostNoteRect.Width = Math.Max(width - 2, 4);
        _ghostNoteRect.Height = effectiveNoteHeight - 2;
        _ghostNoteRect.Visibility = Visibility.Visible;

        Canvas.SetLeft(_ghostNoteRect, x + 1);
        Canvas.SetTop(_ghostNoteRect, y + 1);
    }

    private void HideGhostNote()
    {
        if (_ghostNoteRect != null)
        {
            _ghostNoteRect.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region Selection Rectangle

    private void UpdateSelectionRectangle()
    {
        if (!_isBoxSelecting) return;

        var x1 = Math.Min(_boxSelectStart.X, _dragCurrentPoint.X);
        var y1 = Math.Min(_boxSelectStart.Y, _dragCurrentPoint.Y);
        var x2 = Math.Max(_boxSelectStart.X, _dragCurrentPoint.X);
        var y2 = Math.Max(_boxSelectStart.Y, _dragCurrentPoint.Y);

        SelectionRect.Width = x2 - x1;
        SelectionRect.Height = y2 - y1;
        Canvas.SetLeft(SelectionRect, x1);
        Canvas.SetTop(SelectionRect, y1);
        SelectionRect.Visibility = Visibility.Visible;
    }

    private void FinishBoxSelection()
    {
        if (!_isBoxSelecting) return;

        var x1 = Math.Min(_boxSelectStart.X, _dragCurrentPoint.X) + ScrollX;
        var y1 = Math.Min(_boxSelectStart.Y, _dragCurrentPoint.Y) + ScrollY;
        var x2 = Math.Max(_boxSelectStart.X, _dragCurrentPoint.X) + ScrollX;
        var y2 = Math.Max(_boxSelectStart.Y, _dragCurrentPoint.Y) + ScrollY;

        var startBeat = XToBeat(x1);
        var endBeat = XToBeat(x2);
        var topNote = YToNote(y1);
        var bottomNote = YToNote(y2);

        if (Notes != null)
        {
            var notesInBox = Notes.Where(n =>
                n.StartBeat < endBeat &&
                n.StartBeat + n.Duration > startBeat &&
                n.Note >= bottomNote &&
                n.Note <= topNote);

            bool shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            if (!shiftHeld)
            {
                ClearSelection();
            }

            foreach (var note in notesInBox)
            {
                SelectNote(note, addToSelection: true);
            }
        }

        SelectionRect.Visibility = Visibility.Collapsed;
        _isBoxSelecting = false;

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Mouse Event Handlers - Canvas

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(NotesCanvas);

        if (CurrentTool == PianoRollTool.Draw)
        {
            // Create a new note
            var beat = SnapToBeat(XToBeat(position.X + ScrollX));
            var midiNote = YToNote(position.Y + ScrollY);

            if (midiNote >= LowestNote && midiNote <= HighestNote)
            {
                var newNote = new PianoRollNote
                {
                    StartBeat = beat,
                    Note = midiNote,
                    Duration = GridSnapValue,
                    Velocity = 100
                };

                NoteAdded?.Invoke(this, new NoteEventArgs(newNote));
            }
        }
        else if (CurrentTool == PianoRollTool.Select)
        {
            // Start box selection
            _isBoxSelecting = true;
            _boxSelectStart = position;
            _dragCurrentPoint = position;
            NotesCanvas.CaptureMouse();
        }

        e.Handled = true;
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isBoxSelecting)
        {
            FinishBoxSelection();
            NotesCanvas.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private void OnCanvasMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Right-click on canvas - could open context menu
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(NotesCanvas);
        _dragCurrentPoint = position;

        if (_isBoxSelecting)
        {
            UpdateSelectionRectangle();
        }
        else if (CurrentTool == PianoRollTool.Draw)
        {
            ShowGhostNote(position);
        }
    }

    private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
    {
        HideGhostNote();
    }

    #endregion

    #region Mouse Event Handlers - Notes

    private void OnNoteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Shapes.Rectangle rect || rect.Tag is not PianoRollNote note) return;

        var position = e.GetPosition(rect);
        bool shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        if (CurrentTool == PianoRollTool.Select || CurrentTool == PianoRollTool.Draw)
        {
            // Check if clicking on resize handle (right edge)
            if (position.X >= rect.Width - ResizeHandleWidth)
            {
                // Start resizing
                _isResizing = true;
                _draggedNote = note;
                _dragStartDuration = note.Duration;
                _dragStartPoint = e.GetPosition(NotesCanvas);
            }
            else
            {
                // Start dragging or selecting
                if (shiftHeld)
                {
                    // Toggle selection
                    if (_selectedNotesInternal.Contains(note))
                    {
                        DeselectNote(note);
                    }
                    else
                    {
                        SelectNote(note, addToSelection: true);
                    }
                }
                else
                {
                    if (!_selectedNotesInternal.Contains(note))
                    {
                        ClearSelection();
                        SelectNote(note, addToSelection: false);
                    }

                    // Start dragging
                    _isDragging = true;
                    _draggedNote = note;
                    _dragStartPoint = e.GetPosition(NotesCanvas);
                    _dragStartBeat = note.StartBeat;
                    _dragStartNoteNumber = note.Note;
                }
            }

            rect.CaptureMouse();
        }

        e.Handled = true;
    }

    private void OnNoteMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Shapes.Rectangle rect || rect.Tag is not PianoRollNote note) return;

        // Right-click to delete
        NoteDeleted?.Invoke(this, new NoteEventArgs(note));
        e.Handled = true;
    }

    private void OnNoteMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Shapes.Rectangle rect || rect.Tag is not PianoRollNote note) return;

        if (_isDragging && _draggedNote != null)
        {
            var currentPos = e.GetPosition(NotesCanvas);
            var deltaX = currentPos.X - _dragStartPoint.X;
            var deltaY = currentPos.Y - _dragStartPoint.Y;

            var effectiveBeatWidth = BeatWidth * ZoomX;
            var effectiveNoteHeight = NoteHeight * ZoomY;

            var deltaBeat = deltaX / effectiveBeatWidth;
            var deltaNote = -(int)Math.Round(deltaY / effectiveNoteHeight);

            var newBeat = SnapToBeat(_dragStartBeat + deltaBeat);
            var newNote = Math.Clamp(_dragStartNoteNumber + deltaNote, LowestNote, HighestNote);

            if (Math.Abs(newBeat - _draggedNote.StartBeat) > 0.001 || newNote != _draggedNote.Note)
            {
                NoteMoved?.Invoke(this, new NoteMovedEventArgs(_draggedNote, newBeat, newNote));
            }
        }
        else if (_isResizing && _draggedNote != null)
        {
            var currentPos = e.GetPosition(NotesCanvas);
            var deltaX = currentPos.X - _dragStartPoint.X;

            var effectiveBeatWidth = BeatWidth * ZoomX;
            var deltaDuration = deltaX / effectiveBeatWidth;
            var newDuration = SnapToBeat(Math.Max(_dragStartDuration + deltaDuration, MinNoteDuration));

            if (Math.Abs(newDuration - _draggedNote.Duration) > 0.001)
            {
                NoteResized?.Invoke(this, new NoteResizedEventArgs(_draggedNote, newDuration));
            }
        }
        else
        {
            // Update cursor based on position
            var position = e.GetPosition(rect);
            if (position.X >= rect.Width - ResizeHandleWidth)
            {
                rect.Cursor = Cursors.SizeWE;
            }
            else
            {
                rect.Cursor = Cursors.Hand;
            }
        }
    }

    /// <summary>
    /// Handles mouse button up on notes.
    /// </summary>
    public void OnNoteMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Shapes.Rectangle rect)
        {
            rect.ReleaseMouseCapture();
        }

        _isDragging = false;
        _isResizing = false;
        _draggedNote = null;
    }

    #endregion

    #region Selection Management

    private void SelectNote(PianoRollNote note, bool addToSelection)
    {
        if (!addToSelection)
        {
            _selectedNotesInternal.Clear();
        }

        if (_selectedNotesInternal.Add(note))
        {
            note.IsSelected = true;
            NoteSelected?.Invoke(this, new NoteEventArgs(note));
            UpdateNoteVisuals();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DeselectNote(PianoRollNote note)
    {
        if (_selectedNotesInternal.Remove(note))
        {
            note.IsSelected = false;
            UpdateNoteVisuals();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ClearSelection()
    {
        foreach (var note in _selectedNotesInternal.ToList())
        {
            note.IsSelected = false;
        }
        _selectedNotesInternal.Clear();
        UpdateNoteVisuals();
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

    /// <summary>
    /// Gets the currently selected notes.
    /// </summary>
    /// <returns>A read-only collection of selected notes.</returns>
    public IReadOnlyCollection<PianoRollNote> GetSelectedNotes()
    {
        return _selectedNotesInternal.ToList().AsReadOnly();
    }

    /// <summary>
    /// Selects all notes.
    /// </summary>
    public void SelectAll()
    {
        if (Notes == null) return;

        foreach (var note in Notes)
        {
            _selectedNotesInternal.Add(note);
            note.IsSelected = true;
        }
        UpdateNoteVisuals();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deselects all notes.
    /// </summary>
    public void DeselectAll()
    {
        ClearSelection();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Coordinate Conversion

    private double GetTotalWidth()
    {
        return TotalBeats * BeatWidth * ZoomX;
    }

    private double GetTotalHeight()
    {
        return (HighestNote - LowestNote + 1) * NoteHeight * ZoomY;
    }

    private double XToBeat(double x)
    {
        var effectiveBeatWidth = BeatWidth * ZoomX;
        return x / effectiveBeatWidth;
    }

    private int YToNote(double y)
    {
        var effectiveNoteHeight = NoteHeight * ZoomY;
        var noteIndex = (int)(y / effectiveNoteHeight);
        return HighestNote - noteIndex;
    }

    private double NoteToY(int midiNote)
    {
        var effectiveNoteHeight = NoteHeight * ZoomY;
        return (HighestNote - midiNote) * effectiveNoteHeight;
    }

    private double SnapToBeat(double beat)
    {
        if (GridSnapValue <= 0) return beat;
        return Math.Round(beat / GridSnapValue) * GridSnapValue;
    }

    #endregion

    #region Cursor Management

    private void UpdateCursor()
    {
        Cursor = CurrentTool switch
        {
            PianoRollTool.Draw => Cursors.Cross,
            PianoRollTool.Erase => Cursors.No,
            _ => Cursors.Arrow
        };
    }

    private System.Windows.Input.Cursor GetNoteCursor(PianoRollNote note)
    {
        return CurrentTool switch
        {
            PianoRollTool.Select => Cursors.Hand,
            PianoRollTool.Draw => Cursors.Hand,
            PianoRollTool.Erase => Cursors.No,
            _ => Cursors.Arrow
        };
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the entire canvas display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    /// <summary>
    /// Updates the display of a specific note.
    /// </summary>
    /// <param name="note">The note to update.</param>
    public void UpdateNote(PianoRollNote note)
    {
        if (_noteRectangles.TryGetValue(note, out var rect))
        {
            // Remove old rectangle
            NotesCanvas.Children.Remove(rect);
            _noteRectangles.Remove(note);

            // Create and add new rectangle
            var newRect = CreateNoteRectangle(note);
            _noteRectangles[note] = newRect;
            NotesCanvas.Children.Add(newRect);
        }
    }

    /// <summary>
    /// Scrolls to make the specified beat visible.
    /// </summary>
    /// <param name="beat">The beat position to scroll to.</param>
    public void ScrollToBeat(double beat)
    {
        var effectiveBeatWidth = BeatWidth * ZoomX;
        var x = beat * effectiveBeatWidth;
        var viewportWidth = ActualWidth;

        if (x < ScrollX || x > ScrollX + viewportWidth)
        {
            ScrollX = Math.Max(0, x - viewportWidth / 3);
        }
    }

    /// <summary>
    /// Scrolls to make the specified note visible.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to scroll to.</param>
    public void ScrollToNote(int midiNote)
    {
        var y = NoteToY(midiNote);
        var viewportHeight = ActualHeight;

        if (y < ScrollY || y > ScrollY + viewportHeight)
        {
            ScrollY = Math.Max(0, y - viewportHeight / 2);
        }
    }

    /// <summary>
    /// Gets the beat position at the specified X coordinate.
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <returns>The beat position.</returns>
    public double GetBeatAtX(double x)
    {
        return XToBeat(x + ScrollX);
    }

    /// <summary>
    /// Gets the MIDI note number at the specified Y coordinate.
    /// </summary>
    /// <param name="y">The Y coordinate.</param>
    /// <returns>The MIDI note number.</returns>
    public int GetNoteAtY(double y)
    {
        return YToNote(y + ScrollY);
    }

    #endregion
}
