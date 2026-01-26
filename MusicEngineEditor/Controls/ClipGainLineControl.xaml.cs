// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a gain automation point on the clip.
/// </summary>
public class GainPoint : INotifyPropertyChanged
{
    private double _position;
    private double _gainDb;
    private GainCurveType _curveType = GainCurveType.Linear;

    /// <summary>
    /// Gets or sets the position as a normalized value (0.0 to 1.0).
    /// </summary>
    public double Position
    {
        get => _position;
        set { _position = Math.Clamp(value, 0.0, 1.0); OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the gain in decibels (-24 to +12 dB).
    /// </summary>
    public double GainDb
    {
        get => _gainDb;
        set { _gainDb = Math.Clamp(value, -24.0, 12.0); OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the curve type for interpolation to the next point.
    /// </summary>
    public GainCurveType CurveType
    {
        get => _curveType;
        set { _curveType = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Creates a new gain point.
    /// </summary>
    public GainPoint(double position, double gainDb, GainCurveType curveType = GainCurveType.Linear)
    {
        _position = Math.Clamp(position, 0.0, 1.0);
        _gainDb = Math.Clamp(gainDb, -24.0, 12.0);
        _curveType = curveType;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Curve type for gain interpolation.
/// </summary>
public enum GainCurveType
{
    Linear,
    Bezier
}

/// <summary>
/// Control for displaying and editing a gain envelope line over a clip waveform.
/// Supports adding, moving, and deleting gain points with optional bezier curves.
/// </summary>
public partial class ClipGainLineControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty GainPointsProperty =
        DependencyProperty.Register(nameof(GainPoints), typeof(ObservableCollection<GainPoint>),
            typeof(ClipGainLineControl), new PropertyMetadata(null, OnGainPointsChanged));

    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(nameof(SnapToGrid), typeof(bool),
            typeof(ClipGainLineControl), new PropertyMetadata(true));

    public static readonly DependencyProperty GridDivisionsProperty =
        DependencyProperty.Register(nameof(GridDivisions), typeof(int),
            typeof(ClipGainLineControl), new PropertyMetadata(16));

    public static readonly DependencyProperty UseBezierCurvesProperty =
        DependencyProperty.Register(nameof(UseBezierCurves), typeof(bool),
            typeof(ClipGainLineControl), new PropertyMetadata(false, OnUseBezierCurvesChanged));

    public static readonly DependencyProperty ClipLengthBeatsProperty =
        DependencyProperty.Register(nameof(ClipLengthBeats), typeof(double),
            typeof(ClipGainLineControl), new PropertyMetadata(4.0));

    /// <summary>
    /// Gets or sets the collection of gain points.
    /// </summary>
    public ObservableCollection<GainPoint> GainPoints
    {
        get => (ObservableCollection<GainPoint>)GetValue(GainPointsProperty);
        set => SetValue(GainPointsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to snap points to the grid.
    /// </summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of grid divisions.
    /// </summary>
    public int GridDivisions
    {
        get => (int)GetValue(GridDivisionsProperty);
        set => SetValue(GridDivisionsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to use bezier curves for interpolation.
    /// </summary>
    public bool UseBezierCurves
    {
        get => (bool)GetValue(UseBezierCurvesProperty);
        set => SetValue(UseBezierCurvesProperty, value);
    }

    /// <summary>
    /// Gets or sets the clip length in beats.
    /// </summary>
    public double ClipLengthBeats
    {
        get => (double)GetValue(ClipLengthBeatsProperty);
        set => SetValue(ClipLengthBeatsProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when gain points are modified.
    /// </summary>
    public event EventHandler<GainPointsChangedEventArgs>? GainPointsChanged;

    #endregion

    #region Fields

    private GainPoint? _selectedPoint;
    private GainPoint? _draggedPoint;
    private Ellipse? _draggedEllipse;
    private Point _dragStartPoint;
    private bool _isDragging;
    private const double MinGainDb = -24.0;
    private const double MaxGainDb = 12.0;

    #endregion

    public ClipGainLineControl()
    {
        InitializeComponent();

        GainPoints = new ObservableCollection<GainPoint>();
        GainPoints.CollectionChanged += (_, _) => RedrawAll();

        SizeChanged += (_, _) => RedrawAll();
        Loaded += (_, _) => RedrawAll();
    }

    #region Property Changed Callbacks

    private static void OnGainPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClipGainLineControl control)
        {
            if (e.OldValue is ObservableCollection<GainPoint> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnGainPointsCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<GainPoint> newCollection)
            {
                newCollection.CollectionChanged += control.OnGainPointsCollectionChanged;
            }

            control.RedrawAll();
        }
    }

    private static void OnUseBezierCurvesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClipGainLineControl control)
        {
            control.RedrawAll();
        }
    }

    private void OnGainPointsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RedrawAll();
        GainPointsChanged?.Invoke(this, new GainPointsChangedEventArgs(GainPoints.ToList()));
    }

    #endregion

    #region Drawing

    private void RedrawAll()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        DrawGrid();
        DrawGainLine();
        DrawPoints();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var width = ActualWidth;
        var height = ActualHeight;

        // Horizontal grid lines (dB levels)
        var dbLevels = new[] { 12.0, 6.0, 0.0, -6.0, -12.0, -18.0, -24.0 };
        foreach (var db in dbLevels)
        {
            var y = DbToY(db, height);
            var line = new Line
            {
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = db == 0 ? new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF)) : new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30)),
                StrokeThickness = db == 0 ? 1 : 0.5,
                Opacity = db == 0 ? 0.8 : 0.5
            };
            GridCanvas.Children.Add(line);
        }

        // Vertical grid lines
        for (int i = 0; i <= GridDivisions; i++)
        {
            var x = (width / GridDivisions) * i;
            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = height,
                Stroke = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30)),
                StrokeThickness = (i % 4 == 0) ? 1 : 0.5,
                Opacity = 0.5
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawGainLine()
    {
        LineCanvas.Children.Clear();
        FillCanvas.Children.Clear();

        if (GainPoints == null || GainPoints.Count == 0)
        {
            // Draw default 0 dB line
            var defaultY = DbToY(0, ActualHeight);
            var defaultLine = new Line
            {
                X1 = 0,
                X2 = ActualWidth,
                Y1 = defaultY,
                Y2 = defaultY,
                Stroke = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection(new[] { 4.0, 2.0 })
            };
            LineCanvas.Children.Add(defaultLine);
            return;
        }

        var sortedPoints = GainPoints.OrderBy(p => p.Position).ToList();
        var geometry = new PathGeometry();
        var figure = new PathFigure();

        // Start from left edge at first point's gain level
        var startY = DbToY(sortedPoints[0].GainDb, ActualHeight);
        figure.StartPoint = new Point(0, startY);

        // Add line to first point
        figure.Segments.Add(new LineSegment(new Point(sortedPoints[0].Position * ActualWidth, startY), true));

        // Connect points
        for (int i = 0; i < sortedPoints.Count; i++)
        {
            var current = sortedPoints[i];
            var x = current.Position * ActualWidth;
            var y = DbToY(current.GainDb, ActualHeight);

            if (i < sortedPoints.Count - 1)
            {
                var next = sortedPoints[i + 1];
                var nextX = next.Position * ActualWidth;
                var nextY = DbToY(next.GainDb, ActualHeight);

                if (UseBezierCurves || current.CurveType == GainCurveType.Bezier)
                {
                    // Bezier curve
                    var controlPoint1 = new Point(x + (nextX - x) * 0.5, y);
                    var controlPoint2 = new Point(x + (nextX - x) * 0.5, nextY);
                    figure.Segments.Add(new BezierSegment(controlPoint1, controlPoint2, new Point(nextX, nextY), true));
                }
                else
                {
                    // Linear
                    figure.Segments.Add(new LineSegment(new Point(nextX, nextY), true));
                }
            }
            else
            {
                // Last point - extend to right edge
                figure.Segments.Add(new LineSegment(new Point(ActualWidth, y), true));
            }
        }

        geometry.Figures.Add(figure);

        var path = new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF)),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        LineCanvas.Children.Add(path);

        // Draw fill area
        var fillGeometry = geometry.Clone();
        var fillFigure = fillGeometry.Figures[0];
        fillFigure.Segments.Add(new LineSegment(new Point(ActualWidth, ActualHeight), true));
        fillFigure.Segments.Add(new LineSegment(new Point(0, ActualHeight), true));
        fillFigure.IsClosed = true;

        var fillPath = new Path
        {
            Data = fillGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x4B, 0x6E, 0xAF)),
            Stroke = null
        };
        FillCanvas.Children.Add(fillPath);
    }

    private void DrawPoints()
    {
        PointsCanvas.Children.Clear();

        if (GainPoints == null) return;

        foreach (var point in GainPoints)
        {
            var x = point.Position * ActualWidth;
            var y = DbToY(point.GainDb, ActualHeight);

            var ellipse = new Ellipse
            {
                Width = point == _selectedPoint ? 12 : 10,
                Height = point == _selectedPoint ? 12 : 10,
                Fill = point == _selectedPoint
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
                    : new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF)),
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                Cursor = Cursors.Hand,
                Tag = point
            };

            Canvas.SetLeft(ellipse, x - ellipse.Width / 2);
            Canvas.SetTop(ellipse, y - ellipse.Height / 2);

            ellipse.MouseLeftButtonDown += Point_MouseLeftButtonDown;
            ellipse.MouseEnter += Point_MouseEnter;
            ellipse.MouseLeave += Point_MouseLeave;

            PointsCanvas.Children.Add(ellipse);
        }
    }

    #endregion

    #region Mouse Handlers

    private void PointsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_draggedPoint != null) return;

        var position = e.GetPosition(PointsCanvas);
        var normalizedPos = position.X / ActualWidth;
        var gainDb = YToDb(position.Y, ActualHeight);

        // Snap to grid if enabled
        if (SnapToGrid && SnapToGridToggle.IsChecked == true)
        {
            normalizedPos = SnapPosition(normalizedPos);
        }

        // Create new point
        var newPoint = new GainPoint(normalizedPos, gainDb, UseBezierCurves ? GainCurveType.Bezier : GainCurveType.Linear);
        GainPoints.Add(newPoint);
        _selectedPoint = newPoint;

        GainPointsChanged?.Invoke(this, new GainPointsChangedEventArgs(GainPoints.ToList()));
        RedrawAll();
    }

    private void PointsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _draggedPoint = null;
            _draggedEllipse = null;
            ReleaseMouseCapture();
            GainPointsChanged?.Invoke(this, new GainPointsChangedEventArgs(GainPoints.ToList()));
        }
    }

    private void PointsCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _draggedPoint == null) return;

        var position = e.GetPosition(PointsCanvas);
        var normalizedPos = Math.Clamp(position.X / ActualWidth, 0.0, 1.0);
        var gainDb = YToDb(position.Y, ActualHeight);

        // Snap to grid if enabled
        if (SnapToGrid && SnapToGridToggle.IsChecked == true)
        {
            normalizedPos = SnapPosition(normalizedPos);
        }

        _draggedPoint.Position = normalizedPos;
        _draggedPoint.GainDb = gainDb;

        // Update tooltip
        ShowValueTooltip(position, _draggedPoint.GainDb);

        RedrawAll();
    }

    private void PointsCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Find point under cursor
        if (e.OriginalSource is Ellipse ellipse && ellipse.Tag is GainPoint point)
        {
            _selectedPoint = point;
            var menu = (System.Windows.Controls.ContextMenu)Resources["PointContextMenu"];
            menu.DataContext = point;
            menu.IsOpen = true;
        }
    }

    private void Point_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.Tag is GainPoint point)
        {
            _draggedPoint = point;
            _draggedEllipse = ellipse;
            _selectedPoint = point;
            _isDragging = true;
            _dragStartPoint = e.GetPosition(PointsCanvas);
            CaptureMouse();
            e.Handled = true;

            ShowValueTooltip(_dragStartPoint, point.GainDb);
            RedrawAll();
        }
    }

    private void Point_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.Tag is GainPoint point)
        {
            var position = e.GetPosition(PointsCanvas);
            ShowValueTooltip(position, point.GainDb);
        }
    }

    private void Point_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            ValueTooltip.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region Context Menu Handlers

    private void DeletePoint_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPoint != null && GainPoints.Contains(_selectedPoint))
        {
            GainPoints.Remove(_selectedPoint);
            _selectedPoint = null;
            GainPointsChanged?.Invoke(this, new GainPointsChangedEventArgs(GainPoints.ToList()));
            RedrawAll();
        }
    }

    private void SetLinearCurve_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPoint != null)
        {
            _selectedPoint.CurveType = GainCurveType.Linear;
            RedrawAll();
        }
    }

    private void SetBezierCurve_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPoint != null)
        {
            _selectedPoint.CurveType = GainCurveType.Bezier;
            RedrawAll();
        }
    }

    private void ResetToZero_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPoint != null)
        {
            _selectedPoint.GainDb = 0.0;
            GainPointsChanged?.Invoke(this, new GainPointsChangedEventArgs(GainPoints.ToList()));
            RedrawAll();
        }
    }

    private void BezierModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        UseBezierCurves = true;
    }

    private void BezierModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        UseBezierCurves = false;
    }

    #endregion

    #region Helper Methods

    private double DbToY(double db, double height)
    {
        // Map dB to Y coordinate (top = +12dB, bottom = -24dB)
        var normalized = (MaxGainDb - db) / (MaxGainDb - MinGainDb);
        return normalized * height;
    }

    private double YToDb(double y, double height)
    {
        // Map Y coordinate to dB
        var normalized = y / height;
        return MaxGainDb - (normalized * (MaxGainDb - MinGainDb));
    }

    private double SnapPosition(double position)
    {
        var gridStep = 1.0 / GridDivisions;
        return Math.Round(position / gridStep) * gridStep;
    }

    private void ShowValueTooltip(Point position, double gainDb)
    {
        ValueText.Text = $"{gainDb:F1} dB";
        Canvas.SetLeft(ValueTooltip, position.X + 15);
        Canvas.SetTop(ValueTooltip, position.Y - 10);
        ValueTooltip.Visibility = Visibility.Visible;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears all gain points.
    /// </summary>
    public void ClearPoints()
    {
        GainPoints.Clear();
        _selectedPoint = null;
        GainPointsChanged?.Invoke(this, new GainPointsChangedEventArgs(new List<GainPoint>()));
        RedrawAll();
    }

    /// <summary>
    /// Gets the gain at a specific normalized position using interpolation.
    /// </summary>
    public double GetGainAtPosition(double position)
    {
        if (GainPoints == null || GainPoints.Count == 0)
            return 0.0; // Default 0 dB

        var sorted = GainPoints.OrderBy(p => p.Position).ToList();

        // Before first point
        if (position <= sorted[0].Position)
            return sorted[0].GainDb;

        // After last point
        if (position >= sorted[^1].Position)
            return sorted[^1].GainDb;

        // Find surrounding points
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (position >= sorted[i].Position && position <= sorted[i + 1].Position)
            {
                var t = (position - sorted[i].Position) / (sorted[i + 1].Position - sorted[i].Position);

                if (UseBezierCurves || sorted[i].CurveType == GainCurveType.Bezier)
                {
                    // S-curve interpolation
                    t = t * t * (3 - 2 * t);
                }

                return sorted[i].GainDb + t * (sorted[i + 1].GainDb - sorted[i].GainDb);
            }
        }

        return 0.0;
    }

    #endregion
}

/// <summary>
/// Event arguments for gain points changes.
/// </summary>
public class GainPointsChangedEventArgs : EventArgs
{
    public IReadOnlyList<GainPoint> GainPoints { get; }

    public GainPointsChangedEventArgs(IReadOnlyList<GainPoint> gainPoints)
    {
        GainPoints = gainPoints;
    }
}
