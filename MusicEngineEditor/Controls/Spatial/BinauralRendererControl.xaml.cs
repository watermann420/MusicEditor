// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Binaural renderer control for 3D spatial audio positioning.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core.Routing;

namespace MusicEngineEditor.Controls.Spatial;

/// <summary>
/// HRTF profile type for the binaural renderer.
/// </summary>
public enum HrtfProfile
{
    /// <summary>
    /// Generic HRTF suitable for most listeners.
    /// </summary>
    Generic,

    /// <summary>
    /// KEMAR dummy head measurements.
    /// </summary>
    KEMAR,

    /// <summary>
    /// Custom user-provided HRTF data.
    /// </summary>
    Custom
}

/// <summary>
/// Room size enumeration for spatial audio simulation.
/// </summary>
public enum BinauralRoomSize
{
    /// <summary>
    /// No room simulation (anechoic).
    /// </summary>
    None,

    /// <summary>
    /// Small room (e.g., bathroom, closet).
    /// </summary>
    Small,

    /// <summary>
    /// Medium room (e.g., living room, office).
    /// </summary>
    Medium,

    /// <summary>
    /// Large room (e.g., hall, auditorium).
    /// </summary>
    Large
}

/// <summary>
/// Control for configuring and visualizing binaural audio rendering.
/// Provides interactive 3D head visualization with azimuth, elevation, and distance controls.
/// </summary>
public partial class BinauralRendererControl : UserControl
{
    #region Constants

    private const double MinDistance = 0.1;
    private const double MaxDistance = 100.0;
    private const double DistanceExponent = 3.0; // For exponential scaling

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty AzimuthProperty =
        DependencyProperty.Register(nameof(Azimuth), typeof(double), typeof(BinauralRendererControl),
            new PropertyMetadata(0.0, OnPositionPropertyChanged));

    public static readonly DependencyProperty ElevationProperty =
        DependencyProperty.Register(nameof(Elevation), typeof(double), typeof(BinauralRendererControl),
            new PropertyMetadata(0.0, OnPositionPropertyChanged));

    public static readonly DependencyProperty DistanceProperty =
        DependencyProperty.Register(nameof(Distance), typeof(double), typeof(BinauralRendererControl),
            new PropertyMetadata(1.0, OnPositionPropertyChanged));

    public static readonly DependencyProperty IsEnabledProcessingProperty =
        DependencyProperty.Register(nameof(IsEnabledProcessing), typeof(bool), typeof(BinauralRendererControl),
            new PropertyMetadata(true, OnEnabledPropertyChanged));

    public static readonly DependencyProperty IsBypassedProperty =
        DependencyProperty.Register(nameof(IsBypassed), typeof(bool), typeof(BinauralRendererControl),
            new PropertyMetadata(false, OnBypassPropertyChanged));

    public static readonly DependencyProperty SelectedHrtfProfileProperty =
        DependencyProperty.Register(nameof(SelectedHrtfProfile), typeof(HrtfProfile), typeof(BinauralRendererControl),
            new PropertyMetadata(HrtfProfile.Generic));

    public static readonly DependencyProperty SelectedRoomSizeProperty =
        DependencyProperty.Register(nameof(SelectedRoomSize), typeof(BinauralRoomSize), typeof(BinauralRendererControl),
            new PropertyMetadata(BinauralRoomSize.None));

    public static readonly DependencyProperty RolloffFactorProperty =
        DependencyProperty.Register(nameof(RolloffFactor), typeof(double), typeof(BinauralRendererControl),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty AirAbsorptionProperty =
        DependencyProperty.Register(nameof(AirAbsorption), typeof(double), typeof(BinauralRendererControl),
            new PropertyMetadata(0.001));

    public static readonly DependencyProperty RendererProperty =
        DependencyProperty.Register(nameof(Renderer), typeof(BinauralRenderer), typeof(BinauralRendererControl),
            new PropertyMetadata(null, OnRendererChanged));

    /// <summary>
    /// Gets or sets the azimuth angle in degrees (-180 to +180).
    /// </summary>
    public double Azimuth
    {
        get => (double)GetValue(AzimuthProperty);
        set => SetValue(AzimuthProperty, Math.Clamp(value, -180, 180));
    }

    /// <summary>
    /// Gets or sets the elevation angle in degrees (-90 to +90).
    /// </summary>
    public double Elevation
    {
        get => (double)GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, Math.Clamp(value, -90, 90));
    }

    /// <summary>
    /// Gets or sets the distance from listener in meters (0.1 to 100).
    /// </summary>
    public double Distance
    {
        get => (double)GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, Math.Clamp(value, MinDistance, MaxDistance));
    }

    /// <summary>
    /// Gets or sets whether binaural processing is enabled.
    /// </summary>
    public bool IsEnabledProcessing
    {
        get => (bool)GetValue(IsEnabledProcessingProperty);
        set => SetValue(IsEnabledProcessingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether binaural processing is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => (bool)GetValue(IsBypassedProperty);
        set => SetValue(IsBypassedProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected HRTF profile.
    /// </summary>
    public HrtfProfile SelectedHrtfProfile
    {
        get => (HrtfProfile)GetValue(SelectedHrtfProfileProperty);
        set => SetValue(SelectedHrtfProfileProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected room size.
    /// </summary>
    public BinauralRoomSize SelectedRoomSize
    {
        get => (BinauralRoomSize)GetValue(SelectedRoomSizeProperty);
        set => SetValue(SelectedRoomSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the distance rolloff factor.
    /// </summary>
    public double RolloffFactor
    {
        get => (double)GetValue(RolloffFactorProperty);
        set => SetValue(RolloffFactorProperty, value);
    }

    /// <summary>
    /// Gets or sets the air absorption factor.
    /// </summary>
    public double AirAbsorption
    {
        get => (double)GetValue(AirAbsorptionProperty);
        set => SetValue(AirAbsorptionProperty, value);
    }

    /// <summary>
    /// Gets or sets the binaural renderer instance.
    /// </summary>
    public BinauralRenderer? Renderer
    {
        get => (BinauralRenderer?)GetValue(RendererProperty);
        set => SetValue(RendererProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private bool _isDraggingTopView;
    private bool _isDraggingSideView;
    private bool _suppressSliderEvents;

    // Visualization elements
    private Ellipse? _topViewHead;
    private Ellipse? _topViewSource;
    private Line? _topViewDirectionLine;
    private Ellipse? _sideViewHead;
    private Ellipse? _sideViewSource;
    private Line? _sideViewDirectionLine;

    // Colors
    private static readonly SolidColorBrush _headBrush = CreateFrozenBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
    private static readonly SolidColorBrush _headStrokeBrush = CreateFrozenBrush(Color.FromRgb(0x50, 0x50, 0x50));
    private static readonly SolidColorBrush _sourceBrush = CreateFrozenBrush(Color.FromRgb(0x00, 0xD9, 0xFF));
    private static readonly SolidColorBrush _sourceGlowBrush = CreateFrozenBrush(Color.FromArgb(0x40, 0x00, 0xD9, 0xFF));
    private static readonly SolidColorBrush _gridBrush = CreateFrozenBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
    private static readonly SolidColorBrush _gridLightBrush = CreateFrozenBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
    private static readonly SolidColorBrush _labelBrush = CreateFrozenBrush(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly SolidColorBrush _directionBrush = CreateFrozenBrush(Color.FromArgb(0x80, 0x00, 0xD9, 0xFF));
    private static readonly SolidColorBrush _earBrush = CreateFrozenBrush(Color.FromRgb(0x00, 0xD9, 0xFF));

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new BinauralRendererControl.
    /// </summary>
    public BinauralRendererControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers - Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeVisualization();
        UpdateVisualization();
        UpdateSliderValues();
        UpdateStatusDisplay();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            InitializeVisualization();
            UpdateVisualization();
        }
    }

    #endregion

    #region Event Handlers - Property Changes

    private static void OnPositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BinauralRendererControl control && control._isInitialized)
        {
            control.UpdateVisualization();
            control.UpdateSliderValues();
            control.UpdateStatusDisplay();
            control.SyncToRenderer();
        }
    }

    private static void OnEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BinauralRendererControl control)
        {
            control.UpdateStatusDisplay();
        }
    }

    private static void OnBypassPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BinauralRendererControl control)
        {
            control.UpdateStatusDisplay();
        }
    }

    private static void OnRendererChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BinauralRendererControl control && e.NewValue is BinauralRenderer renderer)
        {
            control.SyncFromRenderer(renderer);
        }
    }

    #endregion

    #region Event Handlers - UI Controls

    private void EnableToggle_Click(object sender, RoutedEventArgs e)
    {
        IsEnabledProcessing = EnableToggle.IsChecked == true;
        UpdateStatusDisplay();
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        IsBypassed = BypassToggle.IsChecked == true;
        UpdateStatusDisplay();
    }

    private void AzimuthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderEvents) return;
        Azimuth = e.NewValue;
        AzimuthValue.Text = $"{Azimuth:F1}\u00B0";
    }

    private void ElevationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderEvents) return;
        Elevation = e.NewValue;
        ElevationValue.Text = $"{Elevation:F1}\u00B0";
    }

    private void DistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderEvents) return;
        // Convert from linear slider (0-100) to exponential distance (0.1-100m)
        double normalized = e.NewValue / 100.0;
        Distance = MinDistance * Math.Pow(MaxDistance / MinDistance, normalized);
        UpdateDistanceDisplay();
    }

    private void RolloffSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        RolloffFactor = e.NewValue;
        RolloffValue.Text = $"{RolloffFactor:F1}";
        SyncToRenderer();
    }

    private void AirAbsorptionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Convert from slider (0-100) to absorption factor (0-0.1)
        AirAbsorption = e.NewValue / 1000.0;
        AirAbsorptionValue.Text = $"{AirAbsorption:F4}";
        SyncToRenderer();
    }

    private void HrtfProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedHrtfProfile = (HrtfProfile)HrtfProfileCombo.SelectedIndex;
        SyncToRenderer();
    }

    private void RoomSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedRoomSize = (BinauralRoomSize)RoomSizeCombo.SelectedIndex;
    }

    #endregion

    #region Event Handlers - Canvas Interaction

    private void TopViewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTopView = true;
        TopViewCanvas.CaptureMouse();
        UpdatePositionFromTopView(e.GetPosition(TopViewCanvas));
    }

    private void TopViewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTopView = false;
        TopViewCanvas.ReleaseMouseCapture();
    }

    private void TopViewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingTopView)
        {
            UpdatePositionFromTopView(e.GetPosition(TopViewCanvas));
        }
    }

    private void SideViewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSideView = true;
        SideViewCanvas.CaptureMouse();
        UpdatePositionFromSideView(e.GetPosition(SideViewCanvas));
    }

    private void SideViewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSideView = false;
        SideViewCanvas.ReleaseMouseCapture();
    }

    private void SideViewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingSideView)
        {
            UpdatePositionFromSideView(e.GetPosition(SideViewCanvas));
        }
    }

    #endregion

    #region Visualization

    private void InitializeVisualization()
    {
        InitializeTopView();
        InitializeSideView();
    }

    private void InitializeTopView()
    {
        TopViewCanvas.Children.Clear();

        double width = TopViewCanvas.ActualWidth;
        double height = TopViewCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 20;

        // Draw grid circles
        foreach (double factor in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            var circle = new Ellipse
            {
                Width = radius * 2 * factor,
                Height = radius * 2 * factor,
                Stroke = factor == 1.0 ? _gridBrush : _gridLightBrush,
                StrokeThickness = factor == 1.0 ? 1 : 0.5,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(circle, centerX - radius * factor);
            Canvas.SetTop(circle, centerY - radius * factor);
            TopViewCanvas.Children.Add(circle);
        }

        // Draw axis lines
        var horizontalLine = new Line
        {
            X1 = centerX - radius, Y1 = centerY,
            X2 = centerX + radius, Y2 = centerY,
            Stroke = _gridBrush,
            StrokeThickness = 0.5,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        TopViewCanvas.Children.Add(horizontalLine);

        var verticalLine = new Line
        {
            X1 = centerX, Y1 = centerY - radius,
            X2 = centerX, Y2 = centerY + radius,
            Stroke = _gridBrush,
            StrokeThickness = 0.5,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        TopViewCanvas.Children.Add(verticalLine);

        // Draw head (oval from top view)
        double headWidth = 30;
        double headHeight = 36;
        _topViewHead = new Ellipse
        {
            Width = headWidth,
            Height = headHeight,
            Fill = _headBrush,
            Stroke = _headStrokeBrush,
            StrokeThickness = 2
        };
        Canvas.SetLeft(_topViewHead, centerX - headWidth / 2);
        Canvas.SetTop(_topViewHead, centerY - headHeight / 2);
        TopViewCanvas.Children.Add(_topViewHead);

        // Draw ears
        double earSize = 8;
        double earOffset = headWidth / 2 + 2;
        var leftEar = new Ellipse
        {
            Width = earSize,
            Height = earSize * 1.5,
            Fill = _earBrush,
            Opacity = 0.8
        };
        Canvas.SetLeft(leftEar, centerX - earOffset - earSize / 2);
        Canvas.SetTop(leftEar, centerY - earSize * 0.75);
        TopViewCanvas.Children.Add(leftEar);

        var rightEar = new Ellipse
        {
            Width = earSize,
            Height = earSize * 1.5,
            Fill = _earBrush,
            Opacity = 0.8
        };
        Canvas.SetLeft(rightEar, centerX + earOffset - earSize / 2);
        Canvas.SetTop(rightEar, centerY - earSize * 0.75);
        TopViewCanvas.Children.Add(rightEar);

        // Draw nose indicator (front direction)
        var noseIndicator = new Polygon
        {
            Points = new PointCollection
            {
                new Point(centerX, centerY - headHeight / 2 - 8),
                new Point(centerX - 6, centerY - headHeight / 2 + 2),
                new Point(centerX + 6, centerY - headHeight / 2 + 2)
            },
            Fill = _headStrokeBrush
        };
        TopViewCanvas.Children.Add(noseIndicator);

        // Direction line to source
        _topViewDirectionLine = new Line
        {
            Stroke = _directionBrush,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        TopViewCanvas.Children.Add(_topViewDirectionLine);

        // Source glow
        var sourceGlow = new Ellipse
        {
            Width = 24,
            Height = 24,
            Fill = _sourceGlowBrush
        };
        TopViewCanvas.Children.Add(sourceGlow);

        // Source indicator
        _topViewSource = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = _sourceBrush
        };
        TopViewCanvas.Children.Add(_topViewSource);

        // Labels
        AddLabel(TopViewCanvas, "FRONT", centerX, 4, true);
        AddLabel(TopViewCanvas, "BACK", centerX, height - 16, true);
        AddLabel(TopViewCanvas, "L", 4, centerY - 6, false);
        AddLabel(TopViewCanvas, "R", width - 16, centerY - 6, false);
    }

    private void InitializeSideView()
    {
        SideViewCanvas.Children.Clear();

        double width = SideViewCanvas.ActualWidth;
        double height = SideViewCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 20;

        // Draw grid circles
        foreach (double factor in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            var circle = new Ellipse
            {
                Width = radius * 2 * factor,
                Height = radius * 2 * factor,
                Stroke = factor == 1.0 ? _gridBrush : _gridLightBrush,
                StrokeThickness = factor == 1.0 ? 1 : 0.5,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(circle, centerX - radius * factor);
            Canvas.SetTop(circle, centerY - radius * factor);
            SideViewCanvas.Children.Add(circle);
        }

        // Draw axis lines
        var horizontalLine = new Line
        {
            X1 = centerX - radius, Y1 = centerY,
            X2 = centerX + radius, Y2 = centerY,
            Stroke = _gridBrush,
            StrokeThickness = 0.5,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        SideViewCanvas.Children.Add(horizontalLine);

        var verticalLine = new Line
        {
            X1 = centerX, Y1 = centerY - radius,
            X2 = centerX, Y2 = centerY + radius,
            Stroke = _gridBrush,
            StrokeThickness = 0.5,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        SideViewCanvas.Children.Add(verticalLine);

        // Draw head (circle from side view)
        double headSize = 34;
        _sideViewHead = new Ellipse
        {
            Width = headSize,
            Height = headSize,
            Fill = _headBrush,
            Stroke = _headStrokeBrush,
            StrokeThickness = 2
        };
        Canvas.SetLeft(_sideViewHead, centerX - headSize / 2);
        Canvas.SetTop(_sideViewHead, centerY - headSize / 2);
        SideViewCanvas.Children.Add(_sideViewHead);

        // Draw ear indicator (side view shows one ear)
        double earSize = 10;
        var ear = new Ellipse
        {
            Width = earSize,
            Height = earSize * 1.2,
            Fill = _earBrush,
            Opacity = 0.8
        };
        Canvas.SetLeft(ear, centerX - headSize / 2 - earSize / 2 - 2);
        Canvas.SetTop(ear, centerY - earSize * 0.6);
        SideViewCanvas.Children.Add(ear);

        // Direction line to source
        _sideViewDirectionLine = new Line
        {
            Stroke = _directionBrush,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        SideViewCanvas.Children.Add(_sideViewDirectionLine);

        // Source glow
        var sourceGlow = new Ellipse
        {
            Width = 24,
            Height = 24,
            Fill = _sourceGlowBrush
        };
        SideViewCanvas.Children.Add(sourceGlow);

        // Source indicator
        _sideViewSource = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = _sourceBrush
        };
        SideViewCanvas.Children.Add(_sideViewSource);

        // Labels
        AddLabel(SideViewCanvas, "ABOVE", centerX, 4, true);
        AddLabel(SideViewCanvas, "BELOW", centerX, height - 16, true);
        AddLabel(SideViewCanvas, "BACK", 4, centerY - 6, false);
        AddLabel(SideViewCanvas, "FRONT", width - 45, centerY - 6, false);
    }

    private void AddLabel(Canvas canvas, string text, double x, double y, bool centerHorizontally)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = _labelBrush,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };

        if (centerHorizontally)
        {
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            x -= label.DesiredSize.Width / 2;
        }

        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        canvas.Children.Add(label);
    }

    private void UpdateVisualization()
    {
        UpdateTopViewSource();
        UpdateSideViewSource();
    }

    private void UpdateTopViewSource()
    {
        if (_topViewSource == null || _topViewDirectionLine == null) return;

        double width = TopViewCanvas.ActualWidth;
        double height = TopViewCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 20;

        // Convert azimuth and distance to screen coordinates
        // Azimuth: 0 = front (up), 90 = right, -90 = left, 180/-180 = back
        double azimuthRad = Azimuth * Math.PI / 180.0;
        double normalizedDistance = Math.Log(Distance / MinDistance) / Math.Log(MaxDistance / MinDistance);
        normalizedDistance = Math.Clamp(normalizedDistance, 0, 1);
        double displayRadius = normalizedDistance * radius;

        // X is sin(azimuth), Y is -cos(azimuth) because up is front
        double sourceX = centerX + Math.Sin(azimuthRad) * displayRadius;
        double sourceY = centerY - Math.Cos(azimuthRad) * displayRadius;

        // Update source position
        Canvas.SetLeft(_topViewSource, sourceX - 6);
        Canvas.SetTop(_topViewSource, sourceY - 6);

        // Update glow position (find glow element)
        foreach (UIElement element in TopViewCanvas.Children)
        {
            if (element is Ellipse ellipse && ellipse.Width == 24 && ellipse.Fill == _sourceGlowBrush)
            {
                Canvas.SetLeft(ellipse, sourceX - 12);
                Canvas.SetTop(ellipse, sourceY - 12);
                break;
            }
        }

        // Update direction line
        _topViewDirectionLine.X1 = centerX;
        _topViewDirectionLine.Y1 = centerY;
        _topViewDirectionLine.X2 = sourceX;
        _topViewDirectionLine.Y2 = sourceY;
    }

    private void UpdateSideViewSource()
    {
        if (_sideViewSource == null || _sideViewDirectionLine == null) return;

        double width = SideViewCanvas.ActualWidth;
        double height = SideViewCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 20;

        // Convert elevation and distance to screen coordinates
        // Elevation: 0 = ear level (horizontal), 90 = above, -90 = below
        // Also incorporate azimuth for front/back positioning
        double elevationRad = Elevation * Math.PI / 180.0;
        double azimuthRad = Azimuth * Math.PI / 180.0;
        double normalizedDistance = Math.Log(Distance / MinDistance) / Math.Log(MaxDistance / MinDistance);
        normalizedDistance = Math.Clamp(normalizedDistance, 0, 1);
        double displayRadius = normalizedDistance * radius;

        // X is based on azimuth (front/back projection)
        // Y is based on elevation
        double sourceX = centerX + Math.Cos(azimuthRad) * displayRadius;
        double sourceY = centerY - Math.Sin(elevationRad) * displayRadius;

        // Update source position
        Canvas.SetLeft(_sideViewSource, sourceX - 6);
        Canvas.SetTop(_sideViewSource, sourceY - 6);

        // Update glow position
        foreach (UIElement element in SideViewCanvas.Children)
        {
            if (element is Ellipse ellipse && ellipse.Width == 24 && ellipse.Fill == _sourceGlowBrush)
            {
                Canvas.SetLeft(ellipse, sourceX - 12);
                Canvas.SetTop(ellipse, sourceY - 12);
                break;
            }
        }

        // Update direction line
        _sideViewDirectionLine.X1 = centerX;
        _sideViewDirectionLine.Y1 = centerY;
        _sideViewDirectionLine.X2 = sourceX;
        _sideViewDirectionLine.Y2 = sourceY;
    }

    private void UpdatePositionFromTopView(Point mousePos)
    {
        double width = TopViewCanvas.ActualWidth;
        double height = TopViewCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 20;

        // Calculate relative position from center
        double dx = mousePos.X - centerX;
        double dy = centerY - mousePos.Y; // Invert Y so up is positive

        // Calculate azimuth (atan2 gives angle from positive X axis)
        // We want 0 = front (up), so adjust
        double azimuthRad = Math.Atan2(dx, dy);
        Azimuth = azimuthRad * 180.0 / Math.PI;

        // Calculate distance from center (normalized)
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double normalizedDist = Math.Clamp(dist / radius, 0, 1);

        // Convert from linear display to exponential distance
        Distance = MinDistance * Math.Pow(MaxDistance / MinDistance, normalizedDist);
    }

    private void UpdatePositionFromSideView(Point mousePos)
    {
        double width = SideViewCanvas.ActualWidth;
        double height = SideViewCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 20;

        // Calculate relative position from center
        double dx = mousePos.X - centerX;
        double dy = centerY - mousePos.Y; // Invert Y so up is positive

        // Calculate elevation from vertical position
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > 0.001)
        {
            double elevationRad = Math.Asin(Math.Clamp(dy / Math.Max(dist, radius), -1, 1));
            Elevation = elevationRad * 180.0 / Math.PI;
        }

        // Calculate distance from center (normalized)
        double normalizedDist = Math.Clamp(dist / radius, 0, 1);
        Distance = MinDistance * Math.Pow(MaxDistance / MinDistance, normalizedDist);

        // Update azimuth based on horizontal position (front/back)
        if (Math.Abs(dx) > 5)
        {
            // Adjust azimuth to be in front (dx > 0) or back (dx < 0)
            double currentAzimuth = Azimuth;
            if (dx > 0 && Math.Abs(currentAzimuth) > 90)
            {
                // Move to front
                Azimuth = currentAzimuth > 0 ? 180 - currentAzimuth : -180 - currentAzimuth;
            }
            else if (dx < 0 && Math.Abs(currentAzimuth) < 90)
            {
                // Move to back
                Azimuth = currentAzimuth > 0 ? 180 - currentAzimuth : -180 - currentAzimuth;
            }
        }
    }

    #endregion

    #region UI Updates

    private void UpdateSliderValues()
    {
        _suppressSliderEvents = true;

        AzimuthSlider.Value = Azimuth;
        AzimuthValue.Text = $"{Azimuth:F1}\u00B0";

        ElevationSlider.Value = Elevation;
        ElevationValue.Text = $"{Elevation:F1}\u00B0";

        // Convert exponential distance to linear slider value
        double normalizedDistance = Math.Log(Distance / MinDistance) / Math.Log(MaxDistance / MinDistance);
        DistanceSlider.Value = normalizedDistance * 100;
        UpdateDistanceDisplay();

        _suppressSliderEvents = false;
    }

    private void UpdateDistanceDisplay()
    {
        if (Distance < 1.0)
        {
            DistanceValue.Text = $"{Distance * 100:F0} cm";
        }
        else if (Distance < 10.0)
        {
            DistanceValue.Text = $"{Distance:F2} m";
        }
        else
        {
            DistanceValue.Text = $"{Distance:F1} m";
        }
    }

    private void UpdateStatusDisplay()
    {
        // Update position status
        PositionStatusText.Text = $"Az: {Azimuth:F1}\u00B0, El: {Elevation:F1}\u00B0, D: {Distance:F1}m";

        // Update status text
        if (!IsEnabledProcessing)
        {
            StatusText.Text = "Disabled";
            StatusText.Foreground = _labelBrush;
        }
        else if (IsBypassed)
        {
            StatusText.Text = "Bypassed";
            StatusText.Foreground = FindResource("BinauralWarningBrush") as Brush ?? Brushes.Orange;
        }
        else
        {
            StatusText.Text = "Active";
            StatusText.Foreground = FindResource("BinauralSuccessBrush") as Brush ?? Brushes.LightGreen;
        }
    }

    #endregion

    #region Renderer Synchronization

    private void SyncToRenderer()
    {
        if (Renderer == null) return;

        Renderer.Azimuth = (float)Azimuth;
        Renderer.Elevation = (float)Elevation;
        Renderer.Distance = (float)Distance;
        Renderer.RolloffFactor = (float)RolloffFactor;
        Renderer.AirAbsorption = (float)AirAbsorption;

        // Map HRTF profile to dataset
        // Note: The BinauralRenderer uses HrtfDataset enum
        // We would need to handle this mapping based on the actual implementation
    }

    private void SyncFromRenderer(BinauralRenderer renderer)
    {
        _suppressSliderEvents = true;

        Azimuth = renderer.Azimuth;
        Elevation = renderer.Elevation;
        Distance = renderer.Distance;
        RolloffFactor = renderer.RolloffFactor;
        AirAbsorption = renderer.AirAbsorption;

        if (_isInitialized)
        {
            UpdateSliderValues();
            UpdateVisualization();
            UpdateStatusDisplay();
        }

        _suppressSliderEvents = false;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the 3D position using Cartesian coordinates.
    /// </summary>
    /// <param name="x">X position (right is positive).</param>
    /// <param name="y">Y position (up is positive).</param>
    /// <param name="z">Z position (front is positive).</param>
    public void SetPosition(double x, double y, double z)
    {
        double distance = Math.Sqrt(x * x + y * y + z * z);
        if (distance < 0.001)
        {
            Distance = MinDistance;
            Azimuth = 0;
            Elevation = 0;
            return;
        }

        Distance = Math.Clamp(distance, MinDistance, MaxDistance);
        Azimuth = Math.Atan2(x, z) * 180.0 / Math.PI;
        Elevation = Math.Asin(y / distance) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Resets all parameters to default values.
    /// </summary>
    public void ResetToDefaults()
    {
        Azimuth = 0;
        Elevation = 0;
        Distance = 1.0;
        RolloffFactor = 1.0;
        AirAbsorption = 0.001;
        IsEnabledProcessing = true;
        IsBypassed = false;
        SelectedHrtfProfile = HrtfProfile.Generic;
        SelectedRoomSize = BinauralRoomSize.None;

        HrtfProfileCombo.SelectedIndex = 0;
        RoomSizeCombo.SelectedIndex = 0;
        EnableToggle.IsChecked = true;
        BypassToggle.IsChecked = false;
        RolloffSlider.Value = 1.0;
        AirAbsorptionSlider.Value = 1.0;

        UpdateSliderValues();
        UpdateVisualization();
        UpdateStatusDisplay();
    }

    #endregion
}
