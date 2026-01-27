// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Visual editor for tempo automation curves.
/// </summary>
public partial class TempoCurveEditor : UserControl
{
    private readonly Dictionary<Guid, Ellipse> _pointElements = new();
    private TempoPoint? _selectedPoint;
    private TempoPoint? _draggingPoint;
    private Point _dragStartPosition;
    private bool _isDragging;
    private double _contextMenuBeat;
    private double _contextMenuTempo;

    /// <summary>
    /// Gets the collection of tempo points.
    /// </summary>
    public ObservableCollection<TempoPoint> TempoPoints { get; } = new();

    /// <summary>
    /// Gets or sets the minimum tempo value.
    /// </summary>
    public double MinTempo
    {
        get => (double)GetValue(MinTempoProperty);
        set => SetValue(MinTempoProperty, value);
    }

    public static readonly DependencyProperty MinTempoProperty =
        DependencyProperty.Register(nameof(MinTempo), typeof(double), typeof(TempoCurveEditor),
            new PropertyMetadata(TempoPoint.MinTempo, OnRangeChanged));

    /// <summary>
    /// Gets or sets the maximum tempo value.
    /// </summary>
    public double MaxTempo
    {
        get => (double)GetValue(MaxTempoProperty);
        set => SetValue(MaxTempoProperty, value);
    }

    public static readonly DependencyProperty MaxTempoProperty =
        DependencyProperty.Register(nameof(MaxTempo), typeof(double), typeof(TempoCurveEditor),
            new PropertyMetadata(TempoPoint.MaxTempo, OnRangeChanged));

    /// <summary>
    /// Gets or sets the pixels per beat for horizontal scaling.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(TempoCurveEditor),
            new PropertyMetadata(20.0, OnLayoutChanged));

    /// <summary>
    /// Gets or sets the scroll offset in beats.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(TempoCurveEditor),
            new PropertyMetadata(0.0, OnLayoutChanged));

    /// <summary>
    /// Gets or sets the playback position in beats.
    /// </summary>
    public double PlaybackPosition
    {
        get => (double)GetValue(PlaybackPositionProperty);
        set => SetValue(PlaybackPositionProperty, value);
    }

    public static readonly DependencyProperty PlaybackPositionProperty =
        DependencyProperty.Register(nameof(PlaybackPosition), typeof(double), typeof(TempoCurveEditor),
            new PropertyMetadata(0.0, OnPlaybackPositionChanged));

    /// <summary>
    /// Event raised when the tempo changes.
    /// </summary>
    public event EventHandler<double>? TempoChanged;

    public TempoCurveEditor()
    {
        InitializeComponent();

        TempoPoints.CollectionChanged += (_, _) => RefreshDisplay();

        SizeChanged += (_, _) => RefreshDisplay();
        Loaded += (_, _) =>
        {
            // Add initial point at 120 BPM if empty
            if (TempoPoints.Count == 0)
            {
                TempoPoints.Add(new TempoPoint(0, 120, TempoCurveType.Linear));
            }
            RefreshDisplay();
        };
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TempoCurveEditor editor)
            editor.RefreshDisplay();
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TempoCurveEditor editor)
            editor.RefreshDisplay();
    }

    private static void OnPlaybackPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TempoCurveEditor editor)
        {
            editor.UpdatePlayhead();
            editor.UpdateCurrentTempo();
        }
    }

    /// <summary>
    /// Refreshes the visual display.
    /// </summary>
    public void RefreshDisplay()
    {
        if (PointsCanvas.ActualWidth <= 0 || PointsCanvas.ActualHeight <= 0)
            return;

        DrawScaleLabels();
        DrawGridLines();
        DrawCurve();
        DrawPoints();
        UpdatePlayhead();
        UpdateCurrentTempo();
    }

    private void DrawScaleLabels()
    {
        ScaleCanvas.Children.Clear();

        if (ScaleCanvas.ActualHeight <= 0)
            return;

        var brush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        var tempoRange = MaxTempo - MinTempo;

        // Draw tempo labels at regular intervals
        for (double tempo = MinTempo; tempo <= MaxTempo; tempo += 20)
        {
            if (tempo % 40 != 0 && tempo != MinTempo && tempo != MaxTempo) continue;

            var y = TempoToY(tempo);
            var label = new TextBlock
            {
                Text = $"{tempo:F0}",
                FontSize = 9,
                Foreground = brush,
                TextAlignment = TextAlignment.Right
            };

            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - 6);
            ScaleCanvas.Children.Add(label);
        }
    }

    private void DrawGridLines()
    {
        GridCanvas.Children.Clear();

        if (GridCanvas.ActualWidth <= 0 || GridCanvas.ActualHeight <= 0)
            return;

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        var strongGridBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        var tempoBrush = new SolidColorBrush(Color.FromRgb(0x35, 0x38, 0x3D));

        // Horizontal tempo lines
        for (double tempo = MinTempo; tempo <= MaxTempo; tempo += 20)
        {
            var y = TempoToY(tempo);
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = GridCanvas.ActualWidth,
                Y2 = y,
                Stroke = tempo % 40 == 0 ? strongGridBrush : tempoBrush,
                StrokeThickness = tempo == 120 ? 1 : 0.5,
                StrokeDashArray = tempo == 120 ? null : new DoubleCollection { 4, 4 }
            };
            GridCanvas.Children.Add(line);
        }

        // Vertical beat lines
        double startBeat = Math.Floor(ScrollOffset / 4) * 4;
        for (double beat = startBeat; beat < ScrollOffset + (GridCanvas.ActualWidth / PixelsPerBeat) + 4; beat += 4)
        {
            double x = BeatToX(beat);
            if (x < 0 || x > GridCanvas.ActualWidth) continue;

            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = GridCanvas.ActualHeight,
                Stroke = beat % 16 == 0 ? strongGridBrush : gridBrush,
                StrokeThickness = beat % 16 == 0 ? 1 : 0.5
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawCurve()
    {
        CurveCanvas.Children.Clear();

        if (CurveCanvas.ActualWidth <= 0 || TempoPoints.Count == 0)
            return;

        var sortedPoints = TempoPoints.OrderBy(p => p.Position).ToList();
        var curveBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));
        var fillBrush = new SolidColorBrush(Color.FromArgb(40, 0x00, 0xFF, 0x88));

        var pathFigure = new PathFigure();
        var fillFigure = new PathFigure();

        // Start from left edge
        double startX = 0;
        double startY = TempoToY(sortedPoints[0].Tempo);
        pathFigure.StartPoint = new Point(startX, startY);
        fillFigure.StartPoint = new Point(startX, CurveCanvas.ActualHeight);
        fillFigure.Segments.Add(new LineSegment(new Point(startX, startY), true));

        // Draw curve through all points
        for (int i = 0; i < sortedPoints.Count; i++)
        {
            var point = sortedPoints[i];
            var x = BeatToX(point.Position);
            var y = TempoToY(point.Tempo);

            if (i == 0)
            {
                pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
                fillFigure.Segments.Add(new LineSegment(new Point(x, y), true));
            }
            else
            {
                var prevPoint = sortedPoints[i - 1];
                var prevX = BeatToX(prevPoint.Position);
                var prevY = TempoToY(prevPoint.Tempo);

                // Draw curve segment based on curve type
                switch (prevPoint.CurveType)
                {
                    case TempoCurveType.Step:
                        pathFigure.Segments.Add(new LineSegment(new Point(x, prevY), true));
                        pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
                        fillFigure.Segments.Add(new LineSegment(new Point(x, prevY), true));
                        fillFigure.Segments.Add(new LineSegment(new Point(x, y), true));
                        break;

                    case TempoCurveType.Linear:
                        pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
                        fillFigure.Segments.Add(new LineSegment(new Point(x, y), true));
                        break;

                    case TempoCurveType.SCurve:
                        var cp1 = new Point(prevX + (x - prevX) * 0.5, prevY);
                        var cp2 = new Point(prevX + (x - prevX) * 0.5, y);
                        pathFigure.Segments.Add(new BezierSegment(cp1, cp2, new Point(x, y), true));
                        fillFigure.Segments.Add(new BezierSegment(cp1, cp2, new Point(x, y), true));
                        break;

                    case TempoCurveType.ExponentialIn:
                        var qp1 = new Point(prevX + (x - prevX) * 0.8, prevY);
                        pathFigure.Segments.Add(new QuadraticBezierSegment(qp1, new Point(x, y), true));
                        fillFigure.Segments.Add(new QuadraticBezierSegment(qp1, new Point(x, y), true));
                        break;

                    case TempoCurveType.ExponentialOut:
                        var qp2 = new Point(prevX + (x - prevX) * 0.2, y);
                        pathFigure.Segments.Add(new QuadraticBezierSegment(qp2, new Point(x, y), true));
                        fillFigure.Segments.Add(new QuadraticBezierSegment(qp2, new Point(x, y), true));
                        break;
                }
            }
        }

        // Extend to right edge
        var lastPoint = sortedPoints.Last();
        var lastX = BeatToX(lastPoint.Position);
        var lastY = TempoToY(lastPoint.Tempo);
        pathFigure.Segments.Add(new LineSegment(new Point(CurveCanvas.ActualWidth, lastY), true));
        fillFigure.Segments.Add(new LineSegment(new Point(CurveCanvas.ActualWidth, lastY), true));
        fillFigure.Segments.Add(new LineSegment(new Point(CurveCanvas.ActualWidth, CurveCanvas.ActualHeight), true));
        fillFigure.IsClosed = true;

        // Create path geometries
        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        var fillGeometry = new PathGeometry();
        fillGeometry.Figures.Add(fillFigure);

        // Draw fill
        var fillPath = new Path
        {
            Data = fillGeometry,
            Fill = fillBrush
        };
        CurveCanvas.Children.Add(fillPath);

        // Draw curve line
        var curvePath = new Path
        {
            Data = pathGeometry,
            Stroke = curveBrush,
            StrokeThickness = 2
        };
        CurveCanvas.Children.Add(curvePath);
    }

    private void DrawPoints()
    {
        // Clear existing point elements
        foreach (var element in _pointElements.Values)
        {
            PointsCanvas.Children.Remove(element);
        }
        _pointElements.Clear();

        foreach (var point in TempoPoints)
        {
            var element = CreatePointElement(point);
            _pointElements[point.Id] = element;
            PointsCanvas.Children.Add(element);
            PositionPointElement(point, element);
        }
    }

    private Ellipse CreatePointElement(TempoPoint point)
    {
        var isSelected = point.IsSelected;
        var brush = isSelected
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x9B, 0x4B))
            : new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));

        var ellipse = new Ellipse
        {
            Width = isSelected ? 12 : 10,
            Height = isSelected ? 12 : 10,
            Fill = brush,
            Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
            StrokeThickness = 2,
            Tag = point,
            Cursor = point.IsLocked ? Cursors.Arrow : Cursors.SizeAll
        };

        ellipse.MouseLeftButtonDown += PointElement_MouseLeftButtonDown;
        ellipse.MouseEnter += PointElement_MouseEnter;
        ellipse.MouseLeave += PointElement_MouseLeave;

        return ellipse;
    }

    private void PositionPointElement(TempoPoint point, Ellipse element)
    {
        var x = BeatToX(point.Position);
        var y = TempoToY(point.Tempo);

        Canvas.SetLeft(element, x - element.Width / 2);
        Canvas.SetTop(element, y - element.Height / 2);
    }

    private void UpdatePlayhead()
    {
        if (PointsCanvas.ActualWidth <= 0)
            return;

        var x = BeatToX(PlaybackPosition);

        if (x >= 0 && x <= PointsCanvas.ActualWidth)
        {
            Playhead.Visibility = Visibility.Visible;
            Playhead.X1 = x;
            Playhead.X2 = x;
            Playhead.Y2 = PointsCanvas.ActualHeight;
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateCurrentTempo()
    {
        var tempo = GetTempoAtPosition(PlaybackPosition);
        CurrentTempoText.Text = $" {tempo:F1} BPM";
        TempoChanged?.Invoke(this, tempo);
    }

    /// <summary>
    /// Gets the interpolated tempo at a given beat position.
    /// </summary>
    public double GetTempoAtPosition(double beat)
    {
        if (TempoPoints.Count == 0) return 120;

        var sortedPoints = TempoPoints.OrderBy(p => p.Position).ToList();

        // Before first point
        if (beat <= sortedPoints[0].Position)
            return sortedPoints[0].Tempo;

        // After last point
        if (beat >= sortedPoints.Last().Position)
            return sortedPoints.Last().Tempo;

        // Find surrounding points and interpolate
        for (int i = 0; i < sortedPoints.Count - 1; i++)
        {
            if (beat >= sortedPoints[i].Position && beat < sortedPoints[i + 1].Position)
            {
                return sortedPoints[i].InterpolateTo(sortedPoints[i + 1], beat);
            }
        }

        return sortedPoints.Last().Tempo;
    }

    private double BeatToX(double beat) => (beat - ScrollOffset) * PixelsPerBeat;
    private double XToBeat(double x) => (x / PixelsPerBeat) + ScrollOffset;

    private double TempoToY(double tempo)
    {
        var range = MaxTempo - MinTempo;
        var normalized = (tempo - MinTempo) / range;
        return PointsCanvas.ActualHeight * (1 - normalized);
    }

    private double YToTempo(double y)
    {
        var normalized = 1 - (y / PointsCanvas.ActualHeight);
        return MinTempo + normalized * (MaxTempo - MinTempo);
    }

    private TempoCurveType GetSelectedCurveType()
    {
        if (CurveTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return Enum.TryParse<TempoCurveType>(tag, out var type) ? type : TempoCurveType.Linear;
        }
        return TempoCurveType.Linear;
    }

    private void SelectPoint(TempoPoint? point)
    {
        // Deselect previous
        if (_selectedPoint != null)
        {
            _selectedPoint.IsSelected = false;
            if (_pointElements.TryGetValue(_selectedPoint.Id, out var oldElement))
            {
                oldElement.Width = 10;
                oldElement.Height = 10;
                oldElement.Fill = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                PositionPointElement(_selectedPoint, oldElement);
            }
        }

        _selectedPoint = point;

        // Select new
        if (_selectedPoint != null)
        {
            _selectedPoint.IsSelected = true;
            if (_pointElements.TryGetValue(_selectedPoint.Id, out var newElement))
            {
                newElement.Width = 12;
                newElement.Height = 12;
                newElement.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x9B, 0x4B));
                PositionPointElement(_selectedPoint, newElement);
            }
        }

        DeletePointMenuItem.IsEnabled = _selectedPoint != null && !_selectedPoint?.IsLocked == true;
    }

    #region Event Handlers

    private void PointElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.Tag is TempoPoint point)
        {
            SelectPoint(point);

            if (!point.IsLocked)
            {
                _draggingPoint = point;
                _dragStartPosition = e.GetPosition(PointsCanvas);
                _isDragging = false;
                ellipse.CaptureMouse();
            }

            e.Handled = true;
        }
    }

    private void PointElement_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.Tag is TempoPoint point)
        {
            var x = BeatToX(point.Position);
            var y = TempoToY(point.Tempo);

            PopupPositionText.Text = $"Beat: {point.Position:F2}";
            PopupTempoText.Text = $"Tempo: {point.Tempo:F1} BPM";

            Canvas.SetLeft(ValuePopup, x + 10);
            Canvas.SetTop(ValuePopup, y - 30);
            ValuePopup.Visibility = Visibility.Visible;
        }
    }

    private void PointElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            ValuePopup.Visibility = Visibility.Collapsed;
        }
    }

    private void PointsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to add point
            var beat = XToBeat(e.GetPosition(PointsCanvas).X);
            var tempo = YToTempo(e.GetPosition(PointsCanvas).Y);
            tempo = Math.Clamp(tempo, MinTempo, MaxTempo);

            var point = new TempoPoint(Math.Max(0, beat), tempo, GetSelectedCurveType());
            TempoPoints.Add(point);
            SelectPoint(point);
        }
        else
        {
            SelectPoint(null);
        }
    }

    private void PointsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPoint != null)
        {
            if (_pointElements.TryGetValue(_draggingPoint.Id, out var element))
            {
                element.ReleaseMouseCapture();
            }

            _draggingPoint = null;
            _isDragging = false;
            ValuePopup.Visibility = Visibility.Collapsed;
            RefreshDisplay();
        }
    }

    private void PointsCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuBeat = XToBeat(e.GetPosition(PointsCanvas).X);
        _contextMenuTempo = YToTempo(e.GetPosition(PointsCanvas).Y);
    }

    private void PointsCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingPoint != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(PointsCanvas);

            if (!_isDragging && (Math.Abs(currentPoint.X - _dragStartPosition.X) > 3 ||
                                 Math.Abs(currentPoint.Y - _dragStartPosition.Y) > 3))
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                var newBeat = Math.Max(0, XToBeat(currentPoint.X));
                var newTempo = Math.Clamp(YToTempo(currentPoint.Y), MinTempo, MaxTempo);

                // Snap to common tempo values
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    var tempPoint = new TempoPoint(0, newTempo);
                    newTempo = tempPoint.GetSnappedTempo(5);
                }

                _draggingPoint.Position = newBeat;
                _draggingPoint.Tempo = newTempo;

                if (_pointElements.TryGetValue(_draggingPoint.Id, out var element))
                {
                    PositionPointElement(_draggingPoint, element);
                }

                // Update popup
                PopupPositionText.Text = $"Beat: {newBeat:F2}";
                PopupTempoText.Text = $"Tempo: {newTempo:F1} BPM";
                Canvas.SetLeft(ValuePopup, currentPoint.X + 10);
                Canvas.SetTop(ValuePopup, currentPoint.Y - 30);
                ValuePopup.Visibility = Visibility.Visible;

                DrawCurve();
                UpdateCurrentTempo();
            }
        }
    }

    private void PointsCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom with Ctrl+Wheel
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var factor = e.Delta > 0 ? 1.1 : 0.9;
            PixelsPerBeat = Math.Clamp(PixelsPerBeat * factor, 5, 100);
            e.Handled = true;
        }
    }

    private void CurveTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Apply to selected point if any
        if (_selectedPoint != null)
        {
            _selectedPoint.CurveType = GetSelectedCurveType();
            RefreshDisplay();
        }
    }

    private void AddPoint_Click(object sender, RoutedEventArgs e)
    {
        // Add point at playback position with current interpolated tempo
        var tempo = GetTempoAtPosition(PlaybackPosition);
        var point = new TempoPoint(PlaybackPosition, tempo, GetSelectedCurveType());
        TempoPoints.Add(point);
        SelectPoint(point);
    }

    private void AddPointHere_Click(object sender, RoutedEventArgs e)
    {
        var tempo = Math.Clamp(_contextMenuTempo, MinTempo, MaxTempo);
        var point = new TempoPoint(Math.Max(0, _contextMenuBeat), tempo, GetSelectedCurveType());
        TempoPoints.Add(point);
        SelectPoint(point);
    }

    private void DeletePoint_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPoint != null && !_selectedPoint.IsLocked)
        {
            TempoPoints.Remove(_selectedPoint);
            SelectPoint(null);
        }
    }

    private void SetPointCurveType_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPoint != null && sender is MenuItem menuItem && menuItem.Tag is string tag)
        {
            if (Enum.TryParse<TempoCurveType>(tag, out var curveType))
            {
                _selectedPoint.CurveType = curveType;
                RefreshDisplay();
            }
        }
    }

    private void SnapToCommon_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPoint != null)
        {
            _selectedPoint.Tempo = _selectedPoint.GetSnappedTempo();
            RefreshDisplay();
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Reset tempo to constant 120 BPM?",
            "Reset Tempo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            TempoPoints.Clear();
            TempoPoints.Add(new TempoPoint(0, 120, TempoCurveType.Linear));
            SelectPoint(null);
        }
    }

    #endregion
}
