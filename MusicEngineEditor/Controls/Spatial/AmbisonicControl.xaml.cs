// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Ambisonic encoder/decoder control for spatial audio in VR/360 applications.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MusicEngine.Core.Spatial;
using AmbisonicEncoder = MusicEngine.Core.Spatial.AmbisonicEncoder;

namespace MusicEngineEditor.Controls.Spatial;

/// <summary>
/// Interaction logic for AmbisonicControl.xaml.
/// Provides UI for ambisonic encoding/decoding with B-format visualization.
/// </summary>
public partial class AmbisonicControl : UserControl, INotifyPropertyChanged
{
    private bool _isDragging;
    private AmbisonicEncoder? _encoder;
    private AmbisonicDecoder? _decoder;
    private readonly DispatcherTimer _meterTimer;

    // Encoder parameters
    private float _azimuth;
    private float _elevation;
    private float _spread;

    // Decoder rotation
    private float _decoderYaw;
    private float _decoderPitch;
    private float _decoderRoll;

    // Head tracking
    private bool _headTrackingEnabled;

    // B-format channel levels (for visualization)
    private float _wLevel;
    private float _xLevel;
    private float _yLevel;
    private float _zLevel;

    /// <summary>
    /// Creates a new AmbisonicControl.
    /// </summary>
    public AmbisonicControl()
    {
        InitializeComponent();
        DataContext = this;

        // Initialize meter update timer
        _meterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _meterTimer.Tick += MeterTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the azimuth angle in degrees (-180 to 180).
    /// </summary>
    public float Azimuth
    {
        get => _azimuth;
        set
        {
            if (SetProperty(ref _azimuth, Math.Clamp(value, -180f, 180f)))
            {
                UpdateEncoder();
                UpdatePositionIndicator();
                UpdateStatusBar();
            }
        }
    }

    /// <summary>
    /// Gets or sets the elevation angle in degrees (-90 to 90).
    /// </summary>
    public float Elevation
    {
        get => _elevation;
        set
        {
            if (SetProperty(ref _elevation, Math.Clamp(value, -90f, 90f)))
            {
                UpdateEncoder();
                UpdatePositionIndicator();
                UpdateStatusBar();
            }
        }
    }

    /// <summary>
    /// Gets or sets the spread (0 = point source, 1 = omnidirectional).
    /// </summary>
    public float Spread
    {
        get => _spread;
        set
        {
            if (SetProperty(ref _spread, Math.Clamp(value, 0f, 1f)))
            {
                UpdateEncoder();
            }
        }
    }

    /// <summary>
    /// Gets or sets the decoder yaw rotation in degrees.
    /// </summary>
    public float DecoderYaw
    {
        get => _decoderYaw;
        set
        {
            if (SetProperty(ref _decoderYaw, NormalizeAngle(value)))
            {
                UpdateDecoderRotation();
            }
        }
    }

    /// <summary>
    /// Gets or sets the decoder pitch rotation in degrees.
    /// </summary>
    public float DecoderPitch
    {
        get => _decoderPitch;
        set
        {
            if (SetProperty(ref _decoderPitch, Math.Clamp(value, -90f, 90f)))
            {
                UpdateDecoderRotation();
            }
        }
    }

    /// <summary>
    /// Gets or sets the decoder roll rotation in degrees.
    /// </summary>
    public float DecoderRoll
    {
        get => _decoderRoll;
        set
        {
            if (SetProperty(ref _decoderRoll, NormalizeAngle(value)))
            {
                UpdateDecoderRotation();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether head tracking is enabled (for VR).
    /// </summary>
    public bool HeadTrackingEnabled
    {
        get => _headTrackingEnabled;
        set
        {
            if (SetProperty(ref _headTrackingEnabled, value))
            {
                OnHeadTrackingChanged();
            }
        }
    }

    /// <summary>
    /// Gets the connected ambisonic encoder, if any.
    /// </summary>
    public AmbisonicEncoder? Encoder => _encoder;

    /// <summary>
    /// Gets the connected ambisonic decoder, if any.
    /// </summary>
    public AmbisonicDecoder? Decoder => _decoder;

    #endregion

    #region Public Methods

    /// <summary>
    /// Connects an ambisonic encoder to this control.
    /// </summary>
    public void ConnectEncoder(AmbisonicEncoder encoder)
    {
        _encoder = encoder;
        UpdateOrderIndicator();
        UpdateEncoder();
    }

    /// <summary>
    /// Connects an ambisonic decoder to this control.
    /// </summary>
    public void ConnectDecoder(AmbisonicDecoder decoder)
    {
        _decoder = decoder;
        UpdateDecoderRotation();
    }

    /// <summary>
    /// Updates the B-format channel levels for visualization.
    /// </summary>
    public void UpdateChannelLevels(float w, float x, float y, float z)
    {
        _wLevel = Math.Abs(w);
        _xLevel = Math.Abs(x);
        _yLevel = Math.Abs(y);
        _zLevel = Math.Abs(z);
    }

    /// <summary>
    /// Sets the position from head tracking data.
    /// </summary>
    public void SetHeadTrackingRotation(float yaw, float pitch, float roll)
    {
        if (!_headTrackingEnabled) return;

        Dispatcher.Invoke(() =>
        {
            DecoderYaw = yaw;
            DecoderPitch = pitch;
            DecoderRoll = roll;
        });
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawSpatialGrid();
        UpdatePositionIndicator();
        UpdateStatusBar();
        _meterTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _meterTimer.Stop();
    }

    private void MeterTimer_Tick(object? sender, EventArgs e)
    {
        // Update meter rectangles with smooth decay
        UpdateMeterDisplay(WMeter, ref _wLevel);
        UpdateMeterDisplay(XMeter, ref _xLevel);
        UpdateMeterDisplay(YMeter, ref _yLevel);
        UpdateMeterDisplay(ZMeter, ref _zLevel);

        // Update value displays
        WValue.Text = _wLevel.ToString("F2");
        XValue.Text = _xLevel.ToString("F2");
        YValue.Text = _yLevel.ToString("F2");
        ZValue.Text = _zLevel.ToString("F2");

        // Calculate simulated levels based on current position
        CalculateSimulatedLevels();
    }

    private void UpdateMeterDisplay(Rectangle meter, ref float level)
    {
        // Apply decay
        level *= 0.95f;
        if (level < 0.001f) level = 0f;

        // Get parent height for scaling
        if (meter.Parent is Grid grid && grid.Parent is Border border)
        {
            double maxHeight = border.ActualHeight - 4; // Padding
            meter.Height = Math.Max(0, level * maxHeight);
        }
    }

    private void CalculateSimulatedLevels()
    {
        // Simulate B-format levels based on azimuth/elevation
        float azRad = _azimuth * MathF.PI / 180f;
        float elRad = _elevation * MathF.PI / 180f;

        float cosEl = MathF.Cos(elRad);
        float sinEl = MathF.Sin(elRad);
        float cosAz = MathF.Cos(azRad);
        float sinAz = MathF.Sin(azRad);

        // W is always positive (omnidirectional)
        float baseLevel = 0.5f;
        _wLevel = Math.Max(_wLevel, baseLevel);

        // X points forward (cosAz * cosEl)
        _xLevel = Math.Max(_xLevel, baseLevel * Math.Abs(cosAz * cosEl));

        // Y points left (sinAz * cosEl)
        _yLevel = Math.Max(_yLevel, baseLevel * Math.Abs(sinAz * cosEl));

        // Z points up (sinEl)
        _zLevel = Math.Max(_zLevel, baseLevel * Math.Abs(sinEl));
    }

    private void SpatialVisualization_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        SpatialVisualizationContainer.CaptureMouse();
        UpdatePositionFromMouse(e.GetPosition(SpatialVisualizationContainer));
        e.Handled = true;
    }

    private void SpatialVisualization_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            UpdatePositionFromMouse(e.GetPosition(SpatialVisualizationContainer));
        }
    }

    private void SpatialVisualization_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        SpatialVisualizationContainer.ReleaseMouseCapture();
    }

    private void SpatialVisualization_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawSpatialGrid();
        UpdatePositionIndicator();
    }

    private void UpdatePositionFromMouse(Point position)
    {
        var width = SpatialVisualizationContainer.ActualWidth;
        var height = SpatialVisualizationContainer.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Map mouse position to azimuth (-180 to 180) and elevation (-90 to 90)
        // X axis: left = -90, right = +90 (we use left-right for azimuth)
        // Y axis: top = front (0), bottom = back (180 or -180)

        double normalizedX = (position.X / width - 0.5) * 2; // -1 to 1
        double normalizedY = (position.Y / height - 0.5) * 2; // -1 to 1

        // Convert to azimuth: -90 (left) to +90 (right), with wraparound for back
        // Top of the circle is front (0), bottom is back (+/-180)
        // This creates a top-down view of the sound field

        float newAzimuth = (float)(normalizedX * 180);

        // For elevation, use the distance from center
        // Center = horizon (0), edges = above/below based on another control
        // For simplicity, keep elevation on separate slider

        Azimuth = newAzimuth;

        StatusText.Text = $"Dragging: Az={Azimuth:F1}";
    }

    private void Azimuth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Handled by binding
    }

    private void Elevation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Handled by binding
    }

    private void Spread_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Handled by binding
    }

    private void Rotation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Handled by binding
    }

    private void SpeakerLayout_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_decoder == null || SpeakerLayoutCombo.SelectedIndex < 0) return;

        var outputType = SpeakerLayoutCombo.SelectedIndex switch
        {
            0 => AmbisonicDecoderOutput.Stereo,
            1 => AmbisonicDecoderOutput.Surround51,
            2 => AmbisonicDecoderOutput.Surround71,
            3 => AmbisonicDecoderOutput.Binaural,
            4 => AmbisonicDecoderOutput.Custom,
            _ => AmbisonicDecoderOutput.Stereo
        };

        _decoder.OutputType = outputType;
        StatusText.Text = $"Speaker layout: {SpeakerLayoutCombo.SelectedItem}";
    }

    private void OutputFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_encoder == null || OutputFormatCombo.SelectedIndex < 0) return;

        var normalization = OutputFormatCombo.SelectedIndex switch
        {
            0 => AmbisonicNormalization.SN3D,
            1 => AmbisonicNormalization.FuMa,
            2 => AmbisonicNormalization.N3D,
            _ => AmbisonicNormalization.SN3D
        };

        // Note: Normalization is set at encoder construction time
        // To change normalization, a new encoder would need to be created
        StatusText.Text = $"Output format: {OutputFormatCombo.SelectedItem}";
    }

    private void DecodingMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_decoder == null || DecodingMethodCombo.SelectedIndex < 0) return;

        var method = DecodingMethodCombo.SelectedIndex switch
        {
            0 => AmbisonicDecodingMethod.Basic,
            1 => AmbisonicDecodingMethod.MaxRE,
            2 => AmbisonicDecodingMethod.InPhase,
            _ => AmbisonicDecodingMethod.Basic
        };

        _decoder.DecodingMethod = method;
        StatusText.Text = $"Decoding method: {DecodingMethodCombo.SelectedItem}";
    }

    private void AmbisonicOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateOrderIndicator();
        StatusText.Text = $"Order changed: {AmbisonicOrderCombo.SelectedItem}";
    }

    private void HeadTrackingToggle_Changed(object sender, RoutedEventArgs e)
    {
        // Handled by binding
    }

    private void ResetRotation_Click(object sender, RoutedEventArgs e)
    {
        DecoderYaw = 0;
        DecoderPitch = 0;
        DecoderRoll = 0;
        StatusText.Text = "Rotation reset";
    }

    private void QuickPosition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string position)
        {
            switch (position)
            {
                case "Front":
                    Azimuth = 0;
                    Elevation = 0;
                    break;
                case "Back":
                    Azimuth = 180;
                    Elevation = 0;
                    break;
                case "Left":
                    Azimuth = -90;
                    Elevation = 0;
                    break;
                case "Right":
                    Azimuth = 90;
                    Elevation = 0;
                    break;
                case "Above":
                    Azimuth = 0;
                    Elevation = 90;
                    break;
                case "Below":
                    Azimuth = 0;
                    Elevation = -90;
                    break;
                case "Center":
                    Azimuth = 0;
                    Elevation = 0;
                    Spread = 1; // Omnidirectional
                    break;
            }

            StatusText.Text = $"Position: {position}";
        }
    }

    #endregion

    #region Private Methods

    private void UpdateEncoder()
    {
        if (_encoder == null) return;

        // The encoder position parameters are used during the Encode() call
        // Store the current values for use when encoding audio
        // The actual encoding happens when audio samples are processed
    }

    private void UpdateDecoderRotation()
    {
        // Decoder rotation is applied during the decode process
        // Store the rotation values for use when decoding
        // The actual rotation transform is applied in the AmbisonicDecoder
        if (_decoder != null)
        {
            // Rotation will be applied during audio processing
            StatusText.Text = $"Rotation: Y={_decoderYaw:F0}° P={_decoderPitch:F0}° R={_decoderRoll:F0}°";
        }
    }

    private void OnHeadTrackingChanged()
    {
        if (_headTrackingEnabled)
        {
            StatusText.Text = "Head tracking enabled - awaiting VR data";
            // Disable manual rotation controls when head tracking is on
            YawSlider.IsEnabled = false;
            PitchSlider.IsEnabled = false;
            RollSlider.IsEnabled = false;
        }
        else
        {
            StatusText.Text = "Head tracking disabled";
            YawSlider.IsEnabled = true;
            PitchSlider.IsEnabled = true;
            RollSlider.IsEnabled = true;
        }
    }

    private void UpdateOrderIndicator()
    {
        int order = AmbisonicOrderCombo.SelectedIndex + 1;
        int channels = (order + 1) * (order + 1);
        string suffix = order switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
        OrderIndicator.Text = $"{order}{suffix} Order ({channels}ch)";
    }

    private void UpdatePositionIndicator()
    {
        var width = SpatialVisualizationContainer.ActualWidth;
        var height = SpatialVisualizationContainer.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Convert azimuth to X position
        double normalizedX = _azimuth / 180.0; // -1 to 1
        double x = (normalizedX + 1) / 2 * width;

        // Convert elevation to Y offset (visual only - center is horizon)
        double normalizedY = -_elevation / 90.0; // Invert so positive elevation goes up
        double y = (normalizedY + 1) / 2 * height;

        // Update position indicator
        var transform = PositionIndicator.RenderTransform as TranslateTransform;
        if (transform == null)
        {
            transform = new TranslateTransform();
            PositionIndicator.RenderTransform = transform;
        }

        transform.X = x - width / 2;
        transform.Y = y - height / 2;

        // Update spread visualization (indicator size)
        double baseSize = 16;
        double spreadSize = baseSize + (_spread * 40);
        PositionIndicator.Width = spreadSize;
        PositionIndicator.Height = spreadSize;
    }

    private void UpdateStatusBar()
    {
        PositionStatus.Text = $"Az: {_azimuth:F1}, El: {_elevation:F1}";
    }

    private void DrawSpatialGrid()
    {
        SpatialCanvas.Children.Clear();

        var width = SpatialCanvas.ActualWidth;
        var height = SpatialCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var centerX = width / 2;
        var centerY = height / 2;
        var radius = Math.Min(centerX, centerY) - 20;

        // Draw concentric circles for elevation reference
        var circleBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        double[] circleRadii = { 0.33, 0.66, 1.0 };

        foreach (var r in circleRadii)
        {
            var circle = new Ellipse
            {
                Width = radius * 2 * r,
                Height = radius * 2 * r,
                Stroke = circleBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            Canvas.SetLeft(circle, centerX - radius * r);
            Canvas.SetTop(circle, centerY - radius * r);
            SpatialCanvas.Children.Add(circle);
        }

        // Draw azimuth lines (every 45 degrees)
        for (int angle = 0; angle < 360; angle += 45)
        {
            double rad = angle * Math.PI / 180;
            var line = new Line
            {
                X1 = centerX,
                Y1 = centerY,
                X2 = centerX + radius * Math.Sin(rad),
                Y2 = centerY - radius * Math.Cos(rad),
                Stroke = circleBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            SpatialCanvas.Children.Add(line);
        }

        // Update crosshair data
        CenterCrosshair.Data = Geometry.Parse($"M {centerX},{centerY - radius} L {centerX},{centerY + radius} M {centerX - radius},{centerY} L {centerX + radius},{centerY}");
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
