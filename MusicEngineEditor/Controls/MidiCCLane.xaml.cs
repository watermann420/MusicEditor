// MusicEngineEditor - MIDI CC Lane Control
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
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A lane control for editing MIDI CC automation in the Piano Roll.
/// Displays CC events as points with optional interpolation lines.
/// Supports draw mode for creating curves and edit mode for adjusting points.
/// </summary>
public partial class MidiCCLane : UserControl
{
    #region Constants

    private const double DefaultBeatWidth = 40.0;
    private const double PointRadius = 5.0;
    private const double PointHitRadius = 8.0;
    private const int BeatsPerBar = 4;
    private const int MaxValue = 127;
    private const double MinDrawInterval = 0.125; // Minimum beat interval between drawn points

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty CCEventsProperty =
        DependencyProperty.Register(nameof(CCEvents), typeof(ObservableCollection<MidiCCEvent>), typeof(MidiCCLane),
            new PropertyMetadata(null, OnCCEventsChanged));

    public static readonly DependencyProperty SelectedEventsProperty =
        DependencyProperty.Register(nameof(SelectedEvents), typeof(ObservableCollection<MidiCCEvent>), typeof(MidiCCLane),
            new PropertyMetadata(null, OnSelectedEventsChanged));

    public static readonly DependencyProperty TotalBeatsProperty =
        DependencyProperty.Register(nameof(TotalBeats), typeof(double), typeof(MidiCCLane),
            new PropertyMetadata(16.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ZoomXProperty =
        DependencyProperty.Register(nameof(ZoomX), typeof(double), typeof(MidiCCLane),
            new PropertyMetadata(1.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollXProperty =
        DependencyProperty.Register(nameof(ScrollX), typeof(double), typeof(MidiCCLane),
            new PropertyMetadata(0.0, OnScrollChanged));

    public static readonly DependencyProperty BeatWidthProperty =
        DependencyProperty.Register(nameof(BeatWidth), typeof(double), typeof(MidiCCLane),
            new PropertyMetadata(DefaultBeatWidth, OnLayoutPropertyChanged));

    public static readonly DependencyProperty GridSnapValueProperty =
        DependencyProperty.Register(nameof(GridSnapValue), typeof(double), typeof(MidiCCLane),
            new PropertyMetadata(0.25));

    public static readonly DependencyProperty SelectedControllerProperty =
        DependencyProperty.Register(nameof(SelectedController), typeof(int), typeof(MidiCCLane),
            new PropertyMetadata(1, OnControllerChanged));

    public static readonly DependencyProperty EditModeProperty =
        DependencyProperty.Register(nameof(EditMode), typeof(CCLaneEditMode), typeof(MidiCCLane),
            new PropertyMetadata(CCLaneEditMode.Draw, OnEditModeChanged));

    public static readonly DependencyProperty InterpolationEnabledProperty =
        DependencyProperty.Register(nameof(InterpolationEnabled), typeof(bool), typeof(MidiCCLane),
            new PropertyMetadata(true, OnLayoutPropertyChanged));

    /// <summary>
    /// Gets or sets the collection of CC events to display.
    /// </summary>
    public ObservableCollection<MidiCCEvent>? CCEvents
    {
        get => (ObservableCollection<MidiCCEvent>?)GetValue(CCEventsProperty);
        set => SetValue(CCEventsProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of selected CC events.
    /// </summary>
    public ObservableCollection<MidiCCEvent>? SelectedEvents
    {
        get => (ObservableCollection<MidiCCEvent>?)GetValue(SelectedEventsProperty);
        set => SetValue(SelectedEventsProperty, value);
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

    /// <summary>
    /// Gets or sets the selected CC controller number.
    /// </summary>
    public int SelectedController
    {
        get => (int)GetValue(SelectedControllerProperty);
        set => SetValue(SelectedControllerProperty, value);
    }

    /// <summary>
    /// Gets or sets the current editing mode.
    /// </summary>
    public CCLaneEditMode EditMode
    {
        get => (CCLaneEditMode)GetValue(EditModeProperty);
        set => SetValue(EditModeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether interpolation between points is enabled.
    /// </summary>
    public bool InterpolationEnabled
    {
        get => (bool)GetValue(InterpolationEnabledProperty);
        set => SetValue(InterpolationEnabledProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a CC event has been added.
    /// </summary>
    public event EventHandler<CCEventChangedEventArgs>? CCEventAdded;

    /// <summary>
    /// Occurs when a CC event has been removed.
    /// </summary>
    public event EventHandler<CCEventChangedEventArgs>? CCEventRemoved;

    /// <summary>
    /// Occurs when a CC event's value has been changed.
    /// </summary>
    public event EventHandler<CCEventChangedEventArgs>? CCEventChanged;

    #endregion

    #region Private Fields

    private readonly Dictionary<MidiCCEvent, Shapes.Ellipse> _eventPoints = [];
    private readonly HashSet<MidiCCEvent> _selectedEventsInternal = [];

    private bool _isDragging;
    private bool _isDrawing;
    private MidiCCEvent? _draggedEvent;
    private Point _dragStartPoint;
    private double _dragStartBeat;
    private int _dragStartValue;
    private double _lastDrawBeat;

    // Colors
    private static readonly Color CCPointColor = Color.FromRgb(0xFF, 0x95, 0x00);
    private static readonly Color CCPointSelectedColor = Color.FromRgb(0xFF, 0xBB, 0x55);
    private static readonly Color CCLineColor = Color.FromArgb(0xE0, 0xFF, 0x95, 0x00);
    private static readonly Color CCFillColor = Color.FromArgb(0x30, 0xFF, 0x95, 0x00);
    private static readonly Color GridLineColor = Color.FromRgb(0x2A, 0x2A, 0x2A);
    private static readonly Color BarLineColor = Color.FromRgb(0x3A, 0x3A, 0x3A);

    #endregion

    #region Constructor

    public MidiCCLane()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;

        // Wire up mode buttons
        DrawModeButton.Checked += (_, _) => { EditMode = CCLaneEditMode.Draw; EditModeButton.IsChecked = false; };
        EditModeButton.Checked += (_, _) => { EditMode = CCLaneEditMode.Edit; DrawModeButton.IsChecked = false; };
    }

    #endregion

    #region Lifecycle Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateControllerDisplay();
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

    private static void OnCCEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiCCLane lane)
        {
            lane.OnCCEventsCollectionChanged(
                e.OldValue as ObservableCollection<MidiCCEvent>,
                e.NewValue as ObservableCollection<MidiCCEvent>);
        }
    }

    private static void OnSelectedEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiCCLane lane)
        {
            lane.OnSelectedEventsCollectionChanged(
                e.OldValue as ObservableCollection<MidiCCEvent>,
                e.NewValue as ObservableCollection<MidiCCEvent>);
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiCCLane lane)
        {
            lane.RenderAll();
        }
    }

    private static void OnScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiCCLane lane)
        {
            lane.ApplyScrollTransform();
        }
    }

    private static void OnControllerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiCCLane lane)
        {
            lane.UpdateControllerDisplay();
            lane.RenderAll();
        }
    }

    private static void OnEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiCCLane lane)
        {
            lane.UpdateModeButtons();
        }
    }

    private void OnCCEventsCollectionChanged(
        ObservableCollection<MidiCCEvent>? oldCollection,
        ObservableCollection<MidiCCEvent>? newCollection)
    {
        if (oldCollection != null)
        {
            oldCollection.CollectionChanged -= OnCCEventsCollectionItemsChanged;
        }

        if (newCollection != null)
        {
            newCollection.CollectionChanged += OnCCEventsCollectionItemsChanged;
        }

        RenderCCPoints();
    }

    private void OnSelectedEventsCollectionChanged(
        ObservableCollection<MidiCCEvent>? oldCollection,
        ObservableCollection<MidiCCEvent>? newCollection)
    {
        if (oldCollection != null)
        {
            oldCollection.CollectionChanged -= OnSelectedEventsItemsChanged;
        }

        if (newCollection != null)
        {
            newCollection.CollectionChanged += OnSelectedEventsItemsChanged;
        }

        SyncSelectedEvents();
        UpdatePointVisuals();
    }

    private void OnCCEventsCollectionItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderCCPoints();
    }

    private void OnSelectedEventsItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncSelectedEvents();
        UpdatePointVisuals();
    }

    private void UnsubscribeFromCollectionChanges()
    {
        if (CCEvents != null)
        {
            CCEvents.CollectionChanged -= OnCCEventsCollectionItemsChanged;
        }

        if (SelectedEvents != null)
        {
            SelectedEvents.CollectionChanged -= OnSelectedEventsItemsChanged;
        }
    }

    private void SyncSelectedEvents()
    {
        _selectedEventsInternal.Clear();
        if (SelectedEvents != null)
        {
            foreach (var evt in SelectedEvents)
            {
                _selectedEventsInternal.Add(evt);
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
        RenderCCPoints();
        RenderCCLines();
        RenderReferenceLines();
        ApplyScrollTransform();
    }

    private void UpdateCanvasSizes()
    {
        var totalWidth = GetTotalWidth();
        var totalHeight = ActualHeight;

        GridCanvas.Width = totalWidth;
        GridCanvas.Height = totalHeight;
        FillCanvas.Width = totalWidth;
        FillCanvas.Height = totalHeight;
        LineCanvas.Width = totalWidth;
        LineCanvas.Height = totalHeight;
        PointsCanvas.Width = totalWidth;
        PointsCanvas.Height = totalHeight;
        ReferenceCanvas.Width = totalWidth;
        ReferenceCanvas.Height = totalHeight;
    }

    private void RenderGrid()
    {
        GridCanvas.Children.Clear();

        var totalWidth = GetTotalWidth();
        var totalHeight = PointsCanvas.ActualHeight > 0 ? PointsCanvas.ActualHeight : ActualHeight - 4;
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

        // Draw horizontal reference lines (value levels)
        int[] referenceValues = [32, 64, 96];
        foreach (var value in referenceValues)
        {
            var y = ValueToY(value, totalHeight);
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = totalWidth,
                Y2 = y,
                Stroke = new SolidColorBrush(GridLineColor),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection([4, 4]),
                Opacity = 0.4
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void RenderCCPoints()
    {
        PointsCanvas.Children.Clear();
        _eventPoints.Clear();

        if (CCEvents == null) return;

        var totalHeight = PointsCanvas.ActualHeight > 0 ? PointsCanvas.ActualHeight : ActualHeight - 4;
        var effectiveBeatWidth = BeatWidth * ZoomX;

        foreach (var evt in CCEvents.OrderBy(e => e.Beat))
        {
            var point = CreateEventPoint(evt, effectiveBeatWidth, totalHeight);
            _eventPoints[evt] = point;
            PointsCanvas.Children.Add(point);
        }

        // Render lines after points
        RenderCCLines();
    }

    private Shapes.Ellipse CreateEventPoint(MidiCCEvent evt, double effectiveBeatWidth, double totalHeight)
    {
        var x = evt.Beat * effectiveBeatWidth;
        var y = ValueToY(evt.Value, totalHeight);
        var isSelected = _selectedEventsInternal.Contains(evt);

        var point = new Shapes.Ellipse
        {
            Width = PointRadius * 2,
            Height = PointRadius * 2,
            Fill = isSelected
                ? FindResource("CCPointSelectedGradient") as Brush
                : FindResource("CCPointGradient") as Brush,
            Stroke = isSelected ? new SolidColorBrush(Colors.White) : null,
            StrokeThickness = isSelected ? 1.5 : 0,
            Cursor = Cursors.Hand,
            Tag = evt,
            ToolTip = $"Beat: {evt.Beat:F2}, Value: {evt.Value}"
        };

        Canvas.SetLeft(point, x - PointRadius);
        Canvas.SetTop(point, y - PointRadius);

        return point;
    }

    private void RenderCCLines()
    {
        LineCanvas.Children.Clear();
        FillCanvas.Children.Clear();

        if (CCEvents == null || CCEvents.Count < 2) return;

        var totalHeight = PointsCanvas.ActualHeight > 0 ? PointsCanvas.ActualHeight : ActualHeight - 4;
        var effectiveBeatWidth = BeatWidth * ZoomX;

        var sortedEvents = CCEvents.OrderBy(e => e.Beat).ToList();

        // Create line path
        var lineGeometry = new PathGeometry();
        var lineFigure = new PathFigure
        {
            StartPoint = new Point(sortedEvents[0].Beat * effectiveBeatWidth, ValueToY(sortedEvents[0].Value, totalHeight))
        };

        // Create fill path (area under the line)
        var fillGeometry = new PathGeometry();
        var fillFigure = new PathFigure
        {
            StartPoint = new Point(sortedEvents[0].Beat * effectiveBeatWidth, totalHeight)
        };
        fillFigure.Segments.Add(new LineSegment(lineFigure.StartPoint, true));

        for (int i = 1; i < sortedEvents.Count; i++)
        {
            var currentPoint = new Point(
                sortedEvents[i].Beat * effectiveBeatWidth,
                ValueToY(sortedEvents[i].Value, totalHeight));

            if (InterpolationEnabled)
            {
                // Smooth curve using line segments (could be upgraded to bezier)
                lineFigure.Segments.Add(new LineSegment(currentPoint, true));
                fillFigure.Segments.Add(new LineSegment(currentPoint, true));
            }
            else
            {
                // Step mode - horizontal then vertical
                var prevPoint = new Point(
                    sortedEvents[i - 1].Beat * effectiveBeatWidth,
                    ValueToY(sortedEvents[i - 1].Value, totalHeight));

                var stepPoint = new Point(currentPoint.X, prevPoint.Y);
                lineFigure.Segments.Add(new LineSegment(stepPoint, true));
                lineFigure.Segments.Add(new LineSegment(currentPoint, true));

                fillFigure.Segments.Add(new LineSegment(stepPoint, true));
                fillFigure.Segments.Add(new LineSegment(currentPoint, true));
            }
        }

        // Close fill path
        fillFigure.Segments.Add(new LineSegment(new Point(sortedEvents[^1].Beat * effectiveBeatWidth, totalHeight), true));
        fillFigure.IsClosed = true;

        lineGeometry.Figures.Add(lineFigure);
        fillGeometry.Figures.Add(fillFigure);

        // Draw fill
        var fillPath = new Shapes.Path
        {
            Data = fillGeometry,
            Fill = new SolidColorBrush(CCFillColor),
            Stroke = null
        };
        FillCanvas.Children.Add(fillPath);

        // Draw line
        var linePath = new Shapes.Path
        {
            Data = lineGeometry,
            Stroke = new SolidColorBrush(CCLineColor),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        LineCanvas.Children.Add(linePath);
    }

    private void RenderReferenceLines()
    {
        ReferenceCanvas.Children.Clear();

        var totalHeight = PointsCanvas.ActualHeight > 0 ? PointsCanvas.ActualHeight : ActualHeight - 4;

        // Draw a line at the middle value (64)
        var middleY = ValueToY(64, totalHeight);
        var middleLine = new Shapes.Line
        {
            X1 = 0,
            Y1 = middleY,
            X2 = GetTotalWidth(),
            Y2 = middleY,
            Stroke = new SolidColorBrush(Color.FromArgb(0x60, 0x4A, 0x9E, 0xFF)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([2, 4]),
            Opacity = 0.6
        };
        ReferenceCanvas.Children.Add(middleLine);
    }

    private void UpdatePointVisuals()
    {
        var totalHeight = PointsCanvas.ActualHeight > 0 ? PointsCanvas.ActualHeight : ActualHeight - 4;

        foreach (var kvp in _eventPoints)
        {
            var evt = kvp.Key;
            var point = kvp.Value;
            var isSelected = _selectedEventsInternal.Contains(evt);

            point.Fill = isSelected
                ? FindResource("CCPointSelectedGradient") as Brush
                : FindResource("CCPointGradient") as Brush;
            point.Stroke = isSelected ? new SolidColorBrush(Colors.White) : null;
            point.StrokeThickness = isSelected ? 1.5 : 0;
        }
    }

    private void ApplyScrollTransform()
    {
        var transform = new TranslateTransform(-ScrollX, 0);
        GridCanvas.RenderTransform = transform;
        FillCanvas.RenderTransform = transform;
        LineCanvas.RenderTransform = transform;
        PointsCanvas.RenderTransform = transform;
        ReferenceCanvas.RenderTransform = transform;
    }

    private void UpdateControllerDisplay()
    {
        ControllerNameText.Text = MidiCCEvent.GetControllerShortName(SelectedController);
        ControllerNumberText.Text = $"CC{SelectedController}";
        ControllerNameText.ToolTip = MidiCCEvent.GetControllerName(SelectedController);
    }

    private void UpdateModeButtons()
    {
        DrawModeButton.IsChecked = EditMode == CCLaneEditMode.Draw;
        EditModeButton.IsChecked = EditMode == CCLaneEditMode.Edit;
    }

    #endregion

    #region Mouse Event Handlers

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(PointsCanvas);
        var adjustedX = position.X + ScrollX;

        if (EditMode == CCLaneEditMode.Edit)
        {
            // Edit mode: Try to select and drag an existing point
            _draggedEvent = FindEventAtPosition(adjustedX, position.Y);

            if (_draggedEvent != null)
            {
                _isDragging = true;
                _dragStartPoint = position;
                _dragStartBeat = _draggedEvent.Beat;
                _dragStartValue = _draggedEvent.Value;
                PointsCanvas.CaptureMouse();

                // Select the point
                if (!_selectedEventsInternal.Contains(_draggedEvent))
                {
                    if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        ClearSelection();
                    }
                    SelectEvent(_draggedEvent);
                }
            }
            else if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                ClearSelection();
            }
        }
        else if (EditMode == CCLaneEditMode.Draw)
        {
            // Draw mode: Start drawing points
            _isDrawing = true;
            _lastDrawBeat = -1;
            PointsCanvas.CaptureMouse();

            // Add first point
            AddPointAtPosition(adjustedX, position.Y);
        }

        e.Handled = true;
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _draggedEvent = null;
            PointsCanvas.ReleaseMouseCapture();
        }

        if (_isDrawing)
        {
            _isDrawing = false;
            PointsCanvas.ReleaseMouseCapture();
        }

        HideHoverIndicator();
        e.Handled = true;
    }

    private void OnCanvasMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(PointsCanvas);
        var adjustedX = position.X + ScrollX;

        // Right-click to delete point
        var eventAtPosition = FindEventAtPosition(adjustedX, position.Y);
        if (eventAtPosition != null)
        {
            RemoveEvent(eventAtPosition);
        }

        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(PointsCanvas);
        var adjustedX = position.X + ScrollX;
        var totalHeight = PointsCanvas.ActualHeight;

        // Update hover indicator
        var value = YToValue(position.Y, totalHeight);
        var beat = XToBeat(adjustedX);
        ShowHoverIndicator(position, beat, value);

        if (_isDragging && _draggedEvent != null)
        {
            // Calculate new position
            var effectiveBeatWidth = BeatWidth * ZoomX;
            var beatDelta = (position.X - _dragStartPoint.X) / effectiveBeatWidth;
            var valueDelta = -(position.Y - _dragStartPoint.Y) / totalHeight * MaxValue;

            var newBeat = SnapToBeat(Math.Max(0, _dragStartBeat + beatDelta));
            var newValue = Math.Clamp((int)Math.Round(_dragStartValue + valueDelta), 0, MaxValue);

            if (Math.Abs(_draggedEvent.Beat - newBeat) > 0.001 || _draggedEvent.Value != newValue)
            {
                _draggedEvent.Beat = newBeat;
                _draggedEvent.Value = newValue;
                UpdateSinglePoint(_draggedEvent);
                RenderCCLines();
                CCEventChanged?.Invoke(this, new CCEventChangedEventArgs(_draggedEvent));
            }
        }
        else if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
        {
            // Drawing mode - add points as we drag
            AddPointAtPosition(adjustedX, position.Y);
        }
    }

    private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
    {
        HideHoverIndicator();
    }

    #endregion

    #region Helper Methods

    private double GetTotalWidth()
    {
        return TotalBeats * BeatWidth * ZoomX;
    }

    private double ValueToY(int value, double totalHeight)
    {
        return totalHeight - (value / (double)MaxValue) * (totalHeight - 4) - 2;
    }

    private int YToValue(double y, double totalHeight)
    {
        var ratio = (totalHeight - y - 2) / (totalHeight - 4);
        return Math.Clamp((int)Math.Round(ratio * MaxValue), 0, MaxValue);
    }

    private double XToBeat(double x)
    {
        var effectiveBeatWidth = BeatWidth * ZoomX;
        return x / effectiveBeatWidth;
    }

    private double SnapToBeat(double beat)
    {
        if (GridSnapValue <= 0) return beat;
        return Math.Round(beat / GridSnapValue) * GridSnapValue;
    }

    private MidiCCEvent? FindEventAtPosition(double x, double y)
    {
        if (CCEvents == null) return null;

        var effectiveBeatWidth = BeatWidth * ZoomX;
        var totalHeight = PointsCanvas.ActualHeight;

        foreach (var evt in CCEvents)
        {
            var eventX = evt.Beat * effectiveBeatWidth;
            var eventY = ValueToY(evt.Value, totalHeight);

            var distance = Math.Sqrt(Math.Pow(x - eventX, 2) + Math.Pow(y - eventY, 2));
            if (distance <= PointHitRadius)
            {
                return evt;
            }
        }

        return null;
    }

    private void AddPointAtPosition(double x, double y)
    {
        var totalHeight = PointsCanvas.ActualHeight;
        var beat = SnapToBeat(XToBeat(x));
        var value = YToValue(y, totalHeight);

        // Check if we should add a new point (minimum interval in draw mode)
        if (_isDrawing && Math.Abs(beat - _lastDrawBeat) < MinDrawInterval)
        {
            return;
        }

        // Check if there's already an event at this beat position
        var existingEvent = CCEvents?.FirstOrDefault(e => Math.Abs(e.Beat - beat) < 0.001);
        if (existingEvent != null)
        {
            // Update existing event's value
            existingEvent.Value = value;
            UpdateSinglePoint(existingEvent);
            RenderCCLines();
            CCEventChanged?.Invoke(this, new CCEventChangedEventArgs(existingEvent));
        }
        else
        {
            // Add new event
            var newEvent = new MidiCCEvent(beat, SelectedController, value);
            CCEvents?.Add(newEvent);
            CCEventAdded?.Invoke(this, new CCEventChangedEventArgs(newEvent));
        }

        _lastDrawBeat = beat;
    }

    private void RemoveEvent(MidiCCEvent evt)
    {
        CCEvents?.Remove(evt);
        SelectedEvents?.Remove(evt);
        _selectedEventsInternal.Remove(evt);
        CCEventRemoved?.Invoke(this, new CCEventChangedEventArgs(evt));
    }

    private void SelectEvent(MidiCCEvent evt)
    {
        evt.IsSelected = true;
        _selectedEventsInternal.Add(evt);
        SelectedEvents?.Add(evt);
        UpdatePointVisuals();
    }

    private void ClearSelection()
    {
        foreach (var evt in _selectedEventsInternal)
        {
            evt.IsSelected = false;
        }
        _selectedEventsInternal.Clear();
        SelectedEvents?.Clear();
        UpdatePointVisuals();
    }

    private void UpdateSinglePoint(MidiCCEvent evt)
    {
        if (!_eventPoints.TryGetValue(evt, out var point)) return;

        var totalHeight = PointsCanvas.ActualHeight > 0 ? PointsCanvas.ActualHeight : ActualHeight - 4;
        var effectiveBeatWidth = BeatWidth * ZoomX;

        var x = evt.Beat * effectiveBeatWidth;
        var y = ValueToY(evt.Value, totalHeight);

        Canvas.SetLeft(point, x - PointRadius);
        Canvas.SetTop(point, y - PointRadius);
        point.ToolTip = $"Beat: {evt.Beat:F2}, Value: {evt.Value}";
    }

    private void ShowHoverIndicator(Point position, double beat, int value)
    {
        HoverValueText.Text = $"Beat: {beat:F2}, Value: {value}";
        HoverIndicator.Visibility = Visibility.Visible;

        // Position the indicator near the cursor
        var left = position.X + 10;
        var top = position.Y - 30;

        // Keep within bounds
        if (left + HoverIndicator.ActualWidth > ActualWidth - 60)
        {
            left = position.X - HoverIndicator.ActualWidth - 10;
        }
        if (top < 0)
        {
            top = position.Y + 10;
        }

        HoverIndicator.Margin = new Thickness(left + 60, top, 0, 0); // Add 60 for header width
    }

    private void HideHoverIndicator()
    {
        HoverIndicator.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the CC lane display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    /// <summary>
    /// Gets the interpolated CC value at a specific beat.
    /// </summary>
    /// <param name="beat">The beat position.</param>
    /// <returns>The interpolated value (0-127), or null if no events exist.</returns>
    public int? GetValueAtBeat(double beat)
    {
        if (CCEvents == null || CCEvents.Count == 0) return null;

        var sortedEvents = CCEvents.OrderBy(e => e.Beat).ToList();

        if (beat <= sortedEvents[0].Beat)
        {
            return sortedEvents[0].Value;
        }

        if (beat >= sortedEvents[^1].Beat)
        {
            return sortedEvents[^1].Value;
        }

        for (int i = 0; i < sortedEvents.Count - 1; i++)
        {
            var current = sortedEvents[i];
            var next = sortedEvents[i + 1];

            if (beat >= current.Beat && beat < next.Beat)
            {
                if (!InterpolationEnabled)
                {
                    return current.Value;
                }

                double t = (beat - current.Beat) / (next.Beat - current.Beat);
                return (int)Math.Round(current.Value + t * (next.Value - current.Value));
            }
        }

        return sortedEvents[^1].Value;
    }

    #endregion
}

/// <summary>
/// Event arguments for CC event changes.
/// </summary>
public class CCEventChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the CC event that was changed.
    /// </summary>
    public MidiCCEvent Event { get; }

    /// <summary>
    /// Creates a new CCEventChangedEventArgs.
    /// </summary>
    /// <param name="evt">The changed event.</param>
    public CCEventChangedEventArgs(MidiCCEvent evt)
    {
        Event = evt;
    }
}
