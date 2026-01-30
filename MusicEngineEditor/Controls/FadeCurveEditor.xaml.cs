// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Identifies which handle is being dragged.
/// </summary>
public enum DragHandle
{
    None,
    Start,
    End,
    ControlPoint
}

/// <summary>
/// Control for visualizing and editing fade curves with interactive handles.
/// </summary>
public partial class FadeCurveEditor : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty FadeTypeProperty =
        DependencyProperty.Register(nameof(FadeType), typeof(FadeType), typeof(FadeCurveEditor),
            new PropertyMetadata(FadeType.Linear, OnFadeTypeChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(double), typeof(FadeCurveEditor),
            new PropertyMetadata(0.0, OnDurationChanged));

    public static readonly DependencyProperty IsFadeInProperty =
        DependencyProperty.Register(nameof(IsFadeIn), typeof(bool), typeof(FadeCurveEditor),
            new PropertyMetadata(true, OnFadeDirectionChanged));

    public static readonly DependencyProperty ShowHandlesProperty =
        DependencyProperty.Register(nameof(ShowHandles), typeof(bool), typeof(FadeCurveEditor),
            new PropertyMetadata(false, OnShowHandlesChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(FadeCurveEditor),
            new PropertyMetadata("Fade Curve", OnTitleChanged));

    public static readonly DependencyProperty CurveTensionProperty =
        DependencyProperty.Register(nameof(CurveTension), typeof(double), typeof(FadeCurveEditor),
            new PropertyMetadata(0.5, OnCurveTensionChanged));

    public static readonly DependencyProperty ControlPointXProperty =
        DependencyProperty.Register(nameof(ControlPointX), typeof(double), typeof(FadeCurveEditor),
            new PropertyMetadata(0.5, OnControlPointChanged));

    public static readonly DependencyProperty ControlPointYProperty =
        DependencyProperty.Register(nameof(ControlPointY), typeof(double), typeof(FadeCurveEditor),
            new PropertyMetadata(0.5, OnControlPointChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the fade type.
    /// </summary>
    public FadeType FadeType
    {
        get => (FadeType)GetValue(FadeTypeProperty);
        set => SetValue(FadeTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets the fade duration in beats.
    /// </summary>
    public double Duration
    {
        get => (double)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this is a fade-in (true) or fade-out (false).
    /// </summary>
    public bool IsFadeIn
    {
        get => (bool)GetValue(IsFadeInProperty);
        set => SetValue(IsFadeInProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show interactive handles.
    /// </summary>
    public bool ShowHandles
    {
        get => (bool)GetValue(ShowHandlesProperty);
        set => SetValue(ShowHandlesProperty, value);
    }

    /// <summary>
    /// Gets or sets the title text.
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the curve tension (0.0 = linear, 1.0 = maximum curve).
    /// </summary>
    public double CurveTension
    {
        get => (double)GetValue(CurveTensionProperty);
        set => SetValue(CurveTensionProperty, value);
    }

    /// <summary>
    /// Gets or sets the X position of the control point (0.0 to 1.0).
    /// </summary>
    public double ControlPointX
    {
        get => (double)GetValue(ControlPointXProperty);
        set => SetValue(ControlPointXProperty, value);
    }

    /// <summary>
    /// Gets or sets the Y position of the control point (0.0 to 1.0).
    /// </summary>
    public double ControlPointY
    {
        get => (double)GetValue(ControlPointYProperty);
        set => SetValue(ControlPointYProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the fade type changes.
    /// </summary>
    public event EventHandler<FadeType>? FadeTypeChanged;

    /// <summary>
    /// Event raised when the duration changes.
    /// </summary>
    public event EventHandler<double>? DurationChanged;

    /// <summary>
    /// Event raised when the curve tension changes.
    /// </summary>
    public event EventHandler<double>? CurveTensionChanged;

    /// <summary>
    /// Event raised when the control point position changes.
    /// </summary>
    public event EventHandler<Point>? ControlPointChanged;

    /// <summary>
    /// Event raised when dragging starts.
    /// </summary>
    public event EventHandler<DragHandle>? DragStarted;

    /// <summary>
    /// Event raised when dragging ends.
    /// </summary>
    public event EventHandler<DragHandle>? DragEnded;

    #endregion

    #region Fields

    private bool _isDragging;
    private DragHandle _currentDragHandle = DragHandle.None;
    private Point _dragStartPoint;
    private double _dragStartTension;
    private double _dragStartControlPointX;
    private double _dragStartControlPointY;
    private bool _isUpdatingUI;
    private readonly Line[] _gridLines = new Line[5];
    private Ellipse? _controlPointHandle;
    private Line? _controlPointLine1;
    private Line? _controlPointLine2;

    // Visual feedback brushes (cached)
    private SolidColorBrush? _handleBrush;
    private SolidColorBrush? _handleHoverBrush;
    private SolidColorBrush? _handleDragBrush;

    #endregion

    public FadeCurveEditor()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeBrushes();
        InitializeGridLines();
        InitializeControlPointHandle();
        UpdateFadeTypeComboBox();
        UpdateDurationTextBox();
        InvalidateCurve();
    }

    private void InitializeBrushes()
    {
        _handleBrush = (SolidColorBrush)Resources["HandleBrush"];
        _handleHoverBrush = (SolidColorBrush)Resources["HandleHoverBrush"];
        _handleDragBrush = new SolidColorBrush(Color.FromRgb(102, 187, 106)); // Green for active drag
        _handleDragBrush.Freeze();
    }

    private void InitializeControlPointHandle()
    {
        // Create the control point handle
        _controlPointHandle = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = _handleBrush,
            Stroke = new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Amber color for control point
            StrokeThickness = 2,
            Cursor = Cursors.SizeAll,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = true
        };

        // Create lines connecting control point to start/end
        var controlLineBrush = new SolidColorBrush(Color.FromArgb(128, 255, 193, 7));
        controlLineBrush.Freeze();

        _controlPointLine1 = new Line
        {
            Stroke = controlLineBrush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([4, 2]),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        _controlPointLine2 = new Line
        {
            Stroke = controlLineBrush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([4, 2]),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        // Add to canvas (before the main handles)
        CurveCanvas.Children.Add(_controlPointLine1);
        CurveCanvas.Children.Add(_controlPointLine2);
        CurveCanvas.Children.Add(_controlPointHandle);
    }

    #region Property Changed Callbacks

    private static void OnFadeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.UpdateFadeTypeComboBox();
            editor.InvalidateCurve();
            editor.FadeTypeChanged?.Invoke(editor, editor.FadeType);
        }
    }

    private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.UpdateDurationTextBox();
            editor.InvalidateCurve();
            editor.DurationChanged?.Invoke(editor, editor.Duration);
        }
    }

    private static void OnFadeDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.InvalidateCurve();
        }
    }

    private static void OnShowHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            var visibility = editor.ShowHandles ? Visibility.Visible : Visibility.Collapsed;
            editor.StartHandle.Visibility = visibility;
            editor.EndHandle.Visibility = visibility;

            if (editor._controlPointHandle != null)
            {
                editor._controlPointHandle.Visibility = visibility;
            }
            if (editor._controlPointLine1 != null)
            {
                editor._controlPointLine1.Visibility = visibility;
            }
            if (editor._controlPointLine2 != null)
            {
                editor._controlPointLine2.Visibility = visibility;
            }
        }
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.TitleText.Text = editor.Title;
        }
    }

    private static void OnCurveTensionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.InvalidateCurve();
            editor.CurveTensionChanged?.Invoke(editor, editor.CurveTension);
        }
    }

    private static void OnControlPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.InvalidateCurve();
            editor.ControlPointChanged?.Invoke(editor, new Point(editor.ControlPointX, editor.ControlPointY));
        }
    }

    #endregion

    #region Rendering

    private void InitializeGridLines()
    {
        var gridBrush = (SolidColorBrush)Resources["GridLineBrush"];

        for (var i = 0; i < _gridLines.Length; i++)
        {
            _gridLines[i] = new Line
            {
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection([2, 2]),
                IsHitTestVisible = false
            };
            CurveCanvas.Children.Insert(0, _gridLines[i]);
        }
    }

    private void InvalidateCurve()
    {
        if (!IsLoaded) return;

        var width = CurveCanvas.ActualWidth;
        var height = CurveCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        UpdateGridLines(width, height);
        RenderCurve(width, height);
        UpdateHandlePositions(width, height);
    }

    private void UpdateGridLines(double width, double height)
    {
        // Horizontal grid lines (25%, 50%, 75%)
        for (var i = 0; i < 3; i++)
        {
            var y = height * (i + 1) / 4;
            _gridLines[i].X1 = 0;
            _gridLines[i].X2 = width;
            _gridLines[i].Y1 = y;
            _gridLines[i].Y2 = y;
        }

        // Vertical grid lines (33%, 66%)
        for (var i = 0; i < 2; i++)
        {
            var x = width * (i + 1) / 3;
            _gridLines[i + 3].X1 = x;
            _gridLines[i + 3].X2 = x;
            _gridLines[i + 3].Y1 = 0;
            _gridLines[i + 3].Y2 = height;
        }
    }

    private void RenderCurve(double width, double height)
    {
        const int segments = 50;
        var points = new Point[segments + 1];

        // Calculate start and end points based on fade direction
        Point startPoint, endPoint;
        if (IsFadeIn)
        {
            startPoint = new Point(0, 0); // Bottom-left (0 amplitude)
            endPoint = new Point(1, 1);   // Top-right (full amplitude)
        }
        else
        {
            startPoint = new Point(0, 1); // Top-left (full amplitude)
            endPoint = new Point(1, 0);   // Bottom-right (0 amplitude)
        }

        // Control point position (normalized 0-1)
        var ctrlX = ControlPointX;
        var ctrlY = ControlPointY;

        for (var i = 0; i <= segments; i++)
        {
            var t = (double)i / segments;
            double curveValue;

            // Use Bezier curve with control point for custom curve shape
            curveValue = CalculateCurveValueWithControlPoint(t, FadeType, ctrlX, ctrlY, IsFadeIn, CurveTension);

            var x = t * width;
            var y = (1.0 - curveValue) * height; // Invert Y since canvas origin is top-left

            points[i] = new Point(x, y);
        }

        // Create fill geometry
        var fillGeometry = new StreamGeometry();
        using (var context = fillGeometry.Open())
        {
            context.BeginFigure(new Point(0, height), true, true);

            foreach (var point in points)
            {
                context.LineTo(point, true, false);
            }

            context.LineTo(new Point(width, height), true, false);
        }
        fillGeometry.Freeze();
        CurveFillPath.Data = fillGeometry;

        // Create stroke geometry
        var strokeGeometry = new StreamGeometry();
        using (var context = strokeGeometry.Open())
        {
            context.BeginFigure(points[0], false, false);

            for (var i = 1; i < points.Length; i++)
            {
                context.LineTo(points[i], true, false);
            }
        }
        strokeGeometry.Freeze();
        CurveStrokePath.Data = strokeGeometry;
    }

    private static double CalculateCurveValueWithControlPoint(
        double t, FadeType fadeType, double ctrlX, double ctrlY, bool isFadeIn, double tension)
    {
        t = Math.Clamp(t, 0, 1);

        // Get base curve value from fade type
        var baseValue = fadeType switch
        {
            FadeType.Linear => t,
            FadeType.Exponential => t * t,
            FadeType.Logarithmic => Math.Sqrt(t),
            FadeType.SCurve => t * t * (3 - 2 * t),
            FadeType.EqualPower => Math.Sin(t * Math.PI / 2),
            _ => t
        };

        // Apply control point influence using quadratic Bezier
        // The control point allows user to shape the curve interactively
        var bezierValue = CalculateQuadraticBezier(t, ctrlX, ctrlY, isFadeIn);

        // Blend between base curve and Bezier curve based on tension
        // Tension of 0.5 (default) means equal blend
        // Higher tension means more influence from control point
        var blendFactor = (tension - 0.5) * 2.0; // Maps 0-1 to -1 to 1
        var result = baseValue + blendFactor * (bezierValue - baseValue);

        return Math.Clamp(result, 0, 1);
    }

    private static double CalculateQuadraticBezier(double t, double ctrlX, double ctrlY, bool isFadeIn)
    {
        // Quadratic Bezier: B(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
        // For fade-in: P0 = (0,0), P1 = (ctrlX, ctrlY), P2 = (1,1)
        // For fade-out: P0 = (0,1), P1 = (ctrlX, ctrlY), P2 = (1,0)

        double p0Y, p2Y;
        if (isFadeIn)
        {
            p0Y = 0;
            p2Y = 1;
        }
        else
        {
            p0Y = 1;
            p2Y = 0;
        }

        // We need to find the Y value for a given X (t)
        // This is more complex because Bezier is parametric
        // For simplicity, we use t directly as the Bezier parameter
        // which gives a good approximation for most cases

        var oneMinusT = 1 - t;
        var y = oneMinusT * oneMinusT * p0Y
              + 2 * oneMinusT * t * ctrlY
              + t * t * p2Y;

        return y;
    }

    private void UpdateHandlePositions(double width, double height)
    {
        if (!ShowHandles) return;

        // Start handle at (0, bottom for fade-in or top for fade-out)
        var startY = IsFadeIn ? height - 5 : 5;
        Canvas.SetLeft(StartHandle, -5);
        Canvas.SetTop(StartHandle, startY - 5);

        // End handle at (width, top for fade-in or bottom for fade-out)
        var endY = IsFadeIn ? 5 : height - 5;
        Canvas.SetLeft(EndHandle, width - 5);
        Canvas.SetTop(EndHandle, endY - 5);

        // Update control point handle position
        if (_controlPointHandle != null)
        {
            var cpX = ControlPointX * width;
            var cpY = (1.0 - ControlPointY) * height; // Invert Y for canvas coordinates

            Canvas.SetLeft(_controlPointHandle, cpX - 4);
            Canvas.SetTop(_controlPointHandle, cpY - 4);
        }

        // Update control point lines
        UpdateControlPointLines(width, height);
    }

    private void UpdateControlPointLines(double width, double height)
    {
        if (_controlPointLine1 == null || _controlPointLine2 == null || !ShowHandles) return;

        var cpX = ControlPointX * width;
        var cpY = (1.0 - ControlPointY) * height;

        // Line from start handle to control point
        var startY = IsFadeIn ? height : 0;
        _controlPointLine1.X1 = 0;
        _controlPointLine1.Y1 = startY;
        _controlPointLine1.X2 = cpX;
        _controlPointLine1.Y2 = cpY;

        // Line from control point to end handle
        var endY = IsFadeIn ? 0 : height;
        _controlPointLine2.X1 = cpX;
        _controlPointLine2.Y1 = cpY;
        _controlPointLine2.X2 = width;
        _controlPointLine2.Y2 = endY;
    }

    #endregion

    #region UI Updates

    private void UpdateFadeTypeComboBox()
    {
        if (_isUpdatingUI) return;
        _isUpdatingUI = true;

        var index = FadeType switch
        {
            FadeType.Linear => 0,
            FadeType.Exponential => 1,
            FadeType.SCurve => 2,
            FadeType.Logarithmic => 3,
            FadeType.EqualPower => 4,
            _ => 0
        };

        FadeTypeComboBox.SelectedIndex = index;
        _isUpdatingUI = false;
    }

    private void UpdateDurationTextBox()
    {
        if (_isUpdatingUI) return;
        _isUpdatingUI = true;

        DurationTextBox.Text = Duration.ToString("F2");
        _isUpdatingUI = false;
    }

    #endregion

    #region Event Handlers

    private void CurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateCurve();
    }

    private void FadeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUI) return;

        if (FadeTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagString)
        {
            if (Enum.TryParse<FadeType>(tagString, out var fadeType))
            {
                FadeType = fadeType;
            }
        }
    }

    private void DurationTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI) return;

        if (double.TryParse(DurationTextBox.Text, out var duration) && duration >= 0)
        {
            Duration = duration;
        }
    }

    private void CurveCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShowHandles) return;

        var position = e.GetPosition(CurveCanvas);
        _dragStartPoint = position;

        // Store initial values for smooth dragging
        _dragStartTension = CurveTension;
        _dragStartControlPointX = ControlPointX;
        _dragStartControlPointY = ControlPointY;

        // Check if clicking on handles (check control point first as it's smaller)
        var controlPointPos = GetControlPointCanvasPosition();
        var startHandlePos = new Point(Canvas.GetLeft(StartHandle) + 5, Canvas.GetTop(StartHandle) + 5);
        var endHandlePos = new Point(Canvas.GetLeft(EndHandle) + 5, Canvas.GetTop(EndHandle) + 5);

        if (_controlPointHandle != null && IsNearPoint(position, controlPointPos, 12))
        {
            StartDrag(DragHandle.ControlPoint);
        }
        else if (IsNearPoint(position, startHandlePos, 10))
        {
            StartDrag(DragHandle.Start);
        }
        else if (IsNearPoint(position, endHandlePos, 10))
        {
            StartDrag(DragHandle.End);
        }
    }

    private void StartDrag(DragHandle handle)
    {
        _isDragging = true;
        _currentDragHandle = handle;
        CurveCanvas.CaptureMouse();

        // Visual feedback - highlight the active handle
        UpdateHandleVisualState(handle, isDragging: true);

        // Show preview line for start/end handles
        if (handle == DragHandle.Start || handle == DragHandle.End)
        {
            PreviewLine.Visibility = Visibility.Visible;
        }

        // Show drag info overlay
        ShowDragInfo(handle);

        // Raise event
        DragStarted?.Invoke(this, handle);
    }

    private void ShowDragInfo(DragHandle handle)
    {
        DragInfoOverlay.Visibility = Visibility.Visible;
        UpdateDragInfoText(handle);

        // Position the overlay near the top-left of the canvas
        Canvas.SetLeft(DragInfoOverlay, 8);
        Canvas.SetTop(DragInfoOverlay, 8);
    }

    private void UpdateDragInfoText(DragHandle handle)
    {
        var text = handle switch
        {
            DragHandle.Start => $"Tension: {CurveTension:F2}",
            DragHandle.End => $"Tension: {CurveTension:F2}",
            DragHandle.ControlPoint => $"Control: ({ControlPointX:F2}, {ControlPointY:F2})",
            _ => ""
        };
        DragInfoText.Text = text;
    }

    private void HideDragInfo()
    {
        DragInfoOverlay.Visibility = Visibility.Collapsed;
    }

    private void EndDrag()
    {
        var previousHandle = _currentDragHandle;

        _isDragging = false;
        _currentDragHandle = DragHandle.None;
        PreviewLine.Visibility = Visibility.Collapsed;
        CurveCanvas.ReleaseMouseCapture();

        // Reset visual state
        UpdateHandleVisualState(previousHandle, isDragging: false);

        // Hide drag info overlay
        HideDragInfo();

        // Raise event
        DragEnded?.Invoke(this, previousHandle);
    }

    private void UpdateHandleVisualState(DragHandle handle, bool isDragging)
    {
        var brush = isDragging ? _handleDragBrush : _handleBrush;

        switch (handle)
        {
            case DragHandle.Start:
                StartHandle.Fill = brush;
                break;
            case DragHandle.End:
                EndHandle.Fill = brush;
                break;
            case DragHandle.ControlPoint:
                if (_controlPointHandle != null)
                {
                    _controlPointHandle.Fill = brush;
                }
                break;
        }
    }

    private Point GetControlPointCanvasPosition()
    {
        var width = CurveCanvas.ActualWidth;
        var height = CurveCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return new Point(0, 0);

        var cpX = ControlPointX * width;
        var cpY = (1.0 - ControlPointY) * height;
        return new Point(cpX, cpY);
    }

    private void CurveCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(CurveCanvas);
        var width = CurveCanvas.ActualWidth;
        var height = CurveCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        if (_isDragging)
        {
            HandleDragMove(position, width, height);
        }
        else if (ShowHandles)
        {
            HandleHoverFeedback(position);
        }
    }

    private void HandleDragMove(Point position, double width, double height)
    {
        switch (_currentDragHandle)
        {
            case DragHandle.Start:
                HandleStartDrag(position, width, height);
                break;

            case DragHandle.End:
                HandleEndDrag(position, width, height);
                break;

            case DragHandle.ControlPoint:
                HandleControlPointDrag(position, width, height);
                break;
        }

        // Update preview line for visual feedback
        if (_currentDragHandle == DragHandle.Start || _currentDragHandle == DragHandle.End)
        {
            PreviewLine.X1 = _dragStartPoint.X;
            PreviewLine.Y1 = _dragStartPoint.Y;
            PreviewLine.X2 = position.X;
            PreviewLine.Y2 = position.Y;
        }

        // Update drag info text in real-time
        UpdateDragInfoText(_currentDragHandle);
    }

    private void HandleStartDrag(Point position, double width, double height)
    {
        // Dragging the start handle adjusts the curve tension based on vertical movement
        // Moving up increases tension (more curved), moving down decreases (more linear)
        var deltaY = _dragStartPoint.Y - position.Y;
        var tensionChange = deltaY / height; // Normalize to 0-1 range

        var newTension = Math.Clamp(_dragStartTension + tensionChange, 0.0, 1.0);
        CurveTension = newTension;

        // Also adjust the control point X position based on horizontal drag
        // This allows shaping the early part of the curve
        var normalizedX = Math.Clamp(position.X / width, 0.05, 0.95);
        if (normalizedX < 0.5)
        {
            // Only affect control point X when dragging towards the start
            ControlPointX = Math.Min(_dragStartControlPointX, 0.3 + normalizedX * 0.4);
        }
    }

    private void HandleEndDrag(Point position, double width, double height)
    {
        // Dragging the end handle also adjusts curve tension
        // But affects the latter part of the curve
        var deltaY = position.Y - _dragStartPoint.Y;
        var tensionChange = deltaY / height;

        var newTension = Math.Clamp(_dragStartTension + tensionChange, 0.0, 1.0);
        CurveTension = newTension;

        // Adjust control point X based on horizontal drag towards end
        var normalizedX = Math.Clamp(position.X / width, 0.05, 0.95);
        if (normalizedX > 0.5)
        {
            // Affect control point X when dragging towards the end
            ControlPointX = Math.Max(_dragStartControlPointX, 0.3 + (normalizedX - 0.5) * 0.8);
        }
    }

    private void HandleControlPointDrag(Point position, double width, double height)
    {
        // Direct manipulation of the control point
        // Normalize position to 0-1 range with some padding
        var normalizedX = Math.Clamp(position.X / width, 0.05, 0.95);
        var normalizedY = Math.Clamp(1.0 - (position.Y / height), 0.05, 0.95);

        ControlPointX = normalizedX;
        ControlPointY = normalizedY;

        // Auto-adjust tension based on how far the control point deviates from the diagonal
        // If control point is on the diagonal (x == y for fade-in), tension should be 0.5
        double expectedY = IsFadeIn ? normalizedX : (1.0 - normalizedX);
        var deviation = Math.Abs(normalizedY - expectedY);
        CurveTension = 0.5 + deviation * 0.5;
    }

    private void HandleHoverFeedback(Point position)
    {
        // Get handle positions
        var startHandlePos = new Point(Canvas.GetLeft(StartHandle) + 5, Canvas.GetTop(StartHandle) + 5);
        var endHandlePos = new Point(Canvas.GetLeft(EndHandle) + 5, Canvas.GetTop(EndHandle) + 5);
        var controlPointPos = GetControlPointCanvasPosition();

        // Check hover states
        var hoverStart = IsNearPoint(position, startHandlePos, 10);
        var hoverEnd = IsNearPoint(position, endHandlePos, 10);
        var hoverControl = _controlPointHandle != null && IsNearPoint(position, controlPointPos, 12);

        // Update visual feedback
        StartHandle.Fill = hoverStart ? _handleHoverBrush : _handleBrush;
        EndHandle.Fill = hoverEnd ? _handleHoverBrush : _handleBrush;

        if (_controlPointHandle != null)
        {
            _controlPointHandle.Fill = hoverControl ? _handleHoverBrush : _handleBrush;
        }

        // Update cursor
        if (hoverStart || hoverEnd || hoverControl)
        {
            CurveCanvas.Cursor = Cursors.SizeAll;
        }
        else
        {
            CurveCanvas.Cursor = Cursors.Arrow;
        }
    }

    private void CurveCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            EndDrag();
        }
    }

    private void CurveCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging && ShowHandles)
        {
            // Reset all handles to default state when mouse leaves
            StartHandle.Fill = _handleBrush;
            EndHandle.Fill = _handleBrush;
            if (_controlPointHandle != null)
            {
                _controlPointHandle.Fill = _handleBrush;
            }
            CurveCanvas.Cursor = Cursors.Arrow;
        }
    }

    private static bool IsNearPoint(Point a, Point b, double threshold)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy < threshold * threshold;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the fade parameters.
    /// </summary>
    /// <param name="fadeType">The fade type.</param>
    /// <param name="duration">The duration in beats.</param>
    /// <param name="isFadeIn">Whether this is a fade-in.</param>
    public void SetFade(FadeType fadeType, double duration, bool isFadeIn)
    {
        _isUpdatingUI = true;

        FadeType = fadeType;
        Duration = duration;
        IsFadeIn = isFadeIn;

        // Reset control point to default position based on fade type
        ResetControlPointForFadeType(fadeType, isFadeIn);

        UpdateFadeTypeComboBox();
        UpdateDurationTextBox();

        _isUpdatingUI = false;
        InvalidateCurve();
    }

    /// <summary>
    /// Sets the fade parameters including control point position.
    /// </summary>
    /// <param name="fadeType">The fade type.</param>
    /// <param name="duration">The duration in beats.</param>
    /// <param name="isFadeIn">Whether this is a fade-in.</param>
    /// <param name="controlPointX">X position of control point (0-1).</param>
    /// <param name="controlPointY">Y position of control point (0-1).</param>
    /// <param name="tension">Curve tension (0-1).</param>
    public void SetFade(FadeType fadeType, double duration, bool isFadeIn,
                        double controlPointX, double controlPointY, double tension)
    {
        _isUpdatingUI = true;

        FadeType = fadeType;
        Duration = duration;
        IsFadeIn = isFadeIn;
        ControlPointX = Math.Clamp(controlPointX, 0, 1);
        ControlPointY = Math.Clamp(controlPointY, 0, 1);
        CurveTension = Math.Clamp(tension, 0, 1);

        UpdateFadeTypeComboBox();
        UpdateDurationTextBox();

        _isUpdatingUI = false;
        InvalidateCurve();
    }

    /// <summary>
    /// Resets the control point to a default position based on the fade type.
    /// </summary>
    /// <param name="fadeType">The fade type.</param>
    /// <param name="isFadeIn">Whether this is a fade-in.</param>
    public void ResetControlPointForFadeType(FadeType fadeType, bool isFadeIn)
    {
        // Set control point based on fade type characteristics
        switch (fadeType)
        {
            case FadeType.Linear:
                ControlPointX = 0.5;
                ControlPointY = isFadeIn ? 0.5 : 0.5;
                CurveTension = 0.5;
                break;

            case FadeType.Exponential:
                // Exponential: fast start, slow end
                ControlPointX = 0.7;
                ControlPointY = isFadeIn ? 0.3 : 0.7;
                CurveTension = 0.7;
                break;

            case FadeType.Logarithmic:
                // Logarithmic: slow start, fast end
                ControlPointX = 0.3;
                ControlPointY = isFadeIn ? 0.7 : 0.3;
                CurveTension = 0.7;
                break;

            case FadeType.SCurve:
                // S-Curve: centered control point
                ControlPointX = 0.5;
                ControlPointY = 0.5;
                CurveTension = 0.6;
                break;

            case FadeType.EqualPower:
                // Equal power: similar to S-Curve but different curve
                ControlPointX = 0.5;
                ControlPointY = isFadeIn ? 0.6 : 0.4;
                CurveTension = 0.55;
                break;

            default:
                ControlPointX = 0.5;
                ControlPointY = 0.5;
                CurveTension = 0.5;
                break;
        }
    }

    /// <summary>
    /// Gets the current control point position.
    /// </summary>
    /// <returns>The control point as a normalized Point (0-1 range).</returns>
    public Point GetControlPoint()
    {
        return new Point(ControlPointX, ControlPointY);
    }

    /// <summary>
    /// Sets the control point position.
    /// </summary>
    /// <param name="x">X position (0-1).</param>
    /// <param name="y">Y position (0-1).</param>
    public void SetControlPoint(double x, double y)
    {
        ControlPointX = Math.Clamp(x, 0, 1);
        ControlPointY = Math.Clamp(y, 0, 1);
    }

    /// <summary>
    /// Gets the curve value at a specific position (0-1).
    /// </summary>
    /// <param name="t">Position along the curve (0-1).</param>
    /// <returns>The curve value at that position (0-1).</returns>
    public double GetCurveValueAt(double t)
    {
        return CalculateCurveValueWithControlPoint(t, FadeType, ControlPointX, ControlPointY, IsFadeIn, CurveTension);
    }

    /// <summary>
    /// Gets all curve data for external use (e.g., audio processing).
    /// </summary>
    /// <returns>A tuple containing fade parameters.</returns>
    public (FadeType Type, double Duration, bool IsFadeIn, double ControlPointX, double ControlPointY, double Tension) GetFadeData()
    {
        return (FadeType, Duration, IsFadeIn, ControlPointX, ControlPointY, CurveTension);
    }

    /// <summary>
    /// Checks if the control is currently being dragged.
    /// </summary>
    /// <returns>True if a drag operation is in progress.</returns>
    public bool IsDragging => _isDragging;

    /// <summary>
    /// Gets the currently active drag handle.
    /// </summary>
    public DragHandle CurrentDragHandle => _currentDragHandle;

    #endregion
}
