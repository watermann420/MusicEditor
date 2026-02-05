// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control for surround sound panning visualization and control.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core.Routing;

namespace MusicEngineEditor.Controls.Spatial;

/// <summary>
/// Visual 2D panner control for surround sound positioning.
/// Supports Stereo, 5.1, 7.1, and Atmos formats with speaker position indicators
/// and draggable source positioning.
/// </summary>
public partial class SurroundPannerControl : UserControl
{
    #region Constants

    private const double SpeakerIndicatorSize = 24;
    private const double SourceIndicatorSize = 20;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SourceXProperty =
        DependencyProperty.Register(nameof(SourceX), typeof(float), typeof(SurroundPannerControl),
            new PropertyMetadata(0f, OnSourcePositionChanged));

    public static readonly DependencyProperty SourceYProperty =
        DependencyProperty.Register(nameof(SourceY), typeof(float), typeof(SurroundPannerControl),
            new PropertyMetadata(0f, OnSourcePositionChanged));

    public static readonly DependencyProperty FormatProperty =
        DependencyProperty.Register(nameof(Format), typeof(SurroundFormat), typeof(SurroundPannerControl),
            new PropertyMetadata(SurroundFormat.Surround_5_1, OnFormatChanged));

    public static readonly DependencyProperty LFELevelProperty =
        DependencyProperty.Register(nameof(LFELevel), typeof(float), typeof(SurroundPannerControl),
            new PropertyMetadata(0f, OnLFELevelChanged));

    public static readonly DependencyProperty SpreadProperty =
        DependencyProperty.Register(nameof(Spread), typeof(float), typeof(SurroundPannerControl),
            new PropertyMetadata(0f, OnSpreadChanged));

    public static readonly DependencyProperty CenterDivergenceProperty =
        DependencyProperty.Register(nameof(CenterDivergence), typeof(float), typeof(SurroundPannerControl),
            new PropertyMetadata(0.5f, OnCenterDivergenceChanged));

    public static readonly DependencyProperty SurroundPannerProperty =
        DependencyProperty.Register(nameof(SurroundPanner), typeof(SurroundPanner), typeof(SurroundPannerControl),
            new PropertyMetadata(null, OnSurroundPannerChanged));

    /// <summary>
    /// Source X position (-1 to 1, where -1 is left, 1 is right).
    /// </summary>
    public float SourceX
    {
        get => (float)GetValue(SourceXProperty);
        set => SetValue(SourceXProperty, Math.Clamp(value, -1f, 1f));
    }

    /// <summary>
    /// Source Y position (-1 to 1, where -1 is back, 1 is front).
    /// </summary>
    public float SourceY
    {
        get => (float)GetValue(SourceYProperty);
        set => SetValue(SourceYProperty, Math.Clamp(value, -1f, 1f));
    }

    /// <summary>
    /// Current surround format.
    /// </summary>
    public SurroundFormat Format
    {
        get => (SurroundFormat)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>
    /// LFE channel level (0 to 1).
    /// </summary>
    public float LFELevel
    {
        get => (float)GetValue(LFELevelProperty);
        set => SetValue(LFELevelProperty, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Spread/divergence amount (0 to 1).
    /// </summary>
    public float Spread
    {
        get => (float)GetValue(SpreadProperty);
        set => SetValue(SpreadProperty, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Center channel divergence (0 = phantom center, 1 = discrete center).
    /// </summary>
    public float CenterDivergence
    {
        get => (float)GetValue(CenterDivergenceProperty);
        set => SetValue(CenterDivergenceProperty, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Connected SurroundPanner instance from MusicEngine.
    /// </summary>
    public SurroundPanner? SurroundPanner
    {
        get => (SurroundPanner?)GetValue(SurroundPannerProperty);
        set => SetValue(SurroundPannerProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when source position changes.
    /// </summary>
    public event EventHandler<SurroundPositionChangedEventArgs>? SourcePositionChanged;

    /// <summary>
    /// Raised when any panner parameter changes.
    /// </summary>
    public event EventHandler<PannerParameterChangedEventArgs>? ParameterChanged;

    #endregion

    #region Private Fields

    private bool _isDragging;
    private bool _isInitialized;
    private readonly Dictionary<string, (double X, double Y, double Gain)> _speakerPositions = new();
    private readonly List<(Ellipse Indicator, TextBlock Label)> _speakerIndicators = new();
    private readonly List<(TextBlock NameBlock, TextBlock ValueBlock)> _gainDisplays = new();

    // Static brushes for performance
    private static readonly SolidColorBrush SpeakerBrush = new(Color.FromRgb(0x4A, 0x4D, 0x52));
    private static readonly SolidColorBrush SpeakerActiveBrush = new(Color.FromRgb(0x00, 0xD9, 0xFF));
    private static readonly SolidColorBrush SourceBrush = new(Color.FromRgb(0x00, 0xFF, 0x88));
    private static readonly SolidColorBrush LFEBrush = new(Color.FromRgb(0xFF, 0x6B, 0x6B));
    private static readonly SolidColorBrush CenterBrush = new(Color.FromRgb(0xFF, 0xA5, 0x00));
    private static readonly SolidColorBrush TextBrush = new(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private static readonly SolidColorBrush TextSecondaryBrush = new(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly SolidColorBrush GridBrush = new(Color.FromArgb(0x40, 0x80, 0x80, 0x80));
    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(0x00, 0xD9, 0xFF));

    static SurroundPannerControl()
    {
        SpeakerBrush.Freeze();
        SpeakerActiveBrush.Freeze();
        SourceBrush.Freeze();
        LFEBrush.Freeze();
        CenterBrush.Freeze();
        TextBrush.Freeze();
        TextSecondaryBrush.Freeze();
        GridBrush.Freeze();
        AccentBrush.Freeze();
    }

    #endregion

    #region Constructor

    public SurroundPannerControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        InitializeFormatCombo();
        UpdateSpeakerLayout();
        DrawGridLines();
        UpdateSourcePosition();
        UpdateGainsDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateSpeakerLayout();
            DrawGridLines();
            UpdateSourcePosition();
            UpdateSpreadIndicator();
        }
    }

    private void InitializeFormatCombo()
    {
        FormatCombo.SelectedIndex = Format switch
        {
            SurroundFormat.Stereo => 0,
            SurroundFormat.Surround_5_1 => 1,
            SurroundFormat.Surround_7_1 => 2,
            SurroundFormat.Atmos_7_1_4 => 3,
            _ => 1
        };
    }

    #endregion

    #region Property Changed Handlers

    private static void OnSourcePositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SurroundPannerControl control && control._isInitialized)
        {
            control.UpdateSourcePosition();
            control.UpdateGainsDisplay();
            control.SyncToSurroundPanner();
            control.SourcePositionChanged?.Invoke(control,
                new SurroundPositionChangedEventArgs(control.SourceX, control.SourceY));
        }
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SurroundPannerControl control && control._isInitialized)
        {
            control.UpdateSpeakerLayout();
            control.UpdateGainsDisplay();
            control.ParameterChanged?.Invoke(control,
                new PannerParameterChangedEventArgs("Format", e.NewValue));
        }
    }

    private static void OnLFELevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SurroundPannerControl control && control._isInitialized)
        {
            control.LFESlider.Value = control.LFELevel * 100;
            control.LFEValueText.Text = $"{control.LFELevel * 100:F0}%";
            control.SyncToSurroundPanner();
            control.UpdateGainsDisplay();
            control.ParameterChanged?.Invoke(control,
                new PannerParameterChangedEventArgs("LFELevel", e.NewValue));
        }
    }

    private static void OnSpreadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SurroundPannerControl control && control._isInitialized)
        {
            control.SpreadSlider.Value = control.Spread * 100;
            control.SpreadValueText.Text = $"{control.Spread * 100:F0}%";
            control.UpdateSpreadIndicator();
            control.SyncToSurroundPanner();
            control.UpdateGainsDisplay();
            control.ParameterChanged?.Invoke(control,
                new PannerParameterChangedEventArgs("Spread", e.NewValue));
        }
    }

    private static void OnCenterDivergenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SurroundPannerControl control && control._isInitialized)
        {
            control.CenterSlider.Value = control.CenterDivergence * 100;
            control.CenterValueText.Text = $"{control.CenterDivergence * 100:F0}%";
            control.SyncToSurroundPanner();
            control.UpdateGainsDisplay();
            control.ParameterChanged?.Invoke(control,
                new PannerParameterChangedEventArgs("CenterDivergence", e.NewValue));
        }
    }

    private static void OnSurroundPannerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SurroundPannerControl control)
        {
            control.SyncFromSurroundPanner();
        }
    }

    #endregion

    #region Panner Synchronization

    private void SyncToSurroundPanner()
    {
        if (SurroundPanner == null) return;

        // Convert X/Y to azimuth/elevation
        // X: -1 (left) to 1 (right) maps to azimuth -90 to +90
        // Y: -1 (back) to 1 (front) maps to azimuth 0 (front) to +/-180 (back)
        float azimuth = CalculateAzimuth(SourceX, SourceY);
        float elevation = 0f; // 2D panner, no elevation

        SurroundPanner.Azimuth = azimuth;
        SurroundPanner.Elevation = elevation;
        SurroundPanner.LFELevel = LFELevel;
        SurroundPanner.Spread = Spread;
        SurroundPanner.CenterDivergence = CenterDivergence;
    }

    private void SyncFromSurroundPanner()
    {
        if (SurroundPanner == null) return;

        // Convert azimuth back to X/Y
        var (x, y) = AzimuthToXY(SurroundPanner.Azimuth);
        SourceX = x;
        SourceY = y;
        LFELevel = SurroundPanner.LFELevel;
        Spread = SurroundPanner.Spread;
        CenterDivergence = SurroundPanner.CenterDivergence;
    }

    private static float CalculateAzimuth(float x, float y)
    {
        // Calculate azimuth from X/Y position
        // Front center = 0, Left = -90, Right = +90, Back = +/-180
        float angle = MathF.Atan2(x, y) * 180f / MathF.PI;
        return angle;
    }

    private static (float X, float Y) AzimuthToXY(float azimuth)
    {
        float rad = azimuth * MathF.PI / 180f;
        float x = MathF.Sin(rad);
        float y = MathF.Cos(rad);
        return (x, y);
    }

    #endregion

    #region Speaker Layout

    private void UpdateSpeakerLayout()
    {
        if (!_isInitialized || SpeakerCanvas == null) return;

        SpeakerCanvas.Children.Clear();
        _speakerIndicators.Clear();
        _speakerPositions.Clear();

        double width = PanningCanvas.ActualWidth;
        double height = PanningCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - SpeakerIndicatorSize;

        // Define speaker positions based on format
        var speakers = GetSpeakerPositions(Format);

        foreach (var speaker in speakers)
        {
            // Convert angle to canvas position
            double angleRad = (speaker.Angle - 90) * Math.PI / 180.0; // -90 to make 0 = front
            double sx = centerX + radius * speaker.Distance * Math.Cos(angleRad);
            double sy = centerY - radius * speaker.Distance * Math.Sin(angleRad); // Invert Y

            // Store position
            _speakerPositions[speaker.Name] = (sx, sy, 0);

            // Create speaker indicator
            var indicator = new Ellipse
            {
                Width = SpeakerIndicatorSize,
                Height = SpeakerIndicatorSize,
                Fill = speaker.IsLFE ? LFEBrush :
                       speaker.Name == "C" ? CenterBrush : SpeakerBrush,
                Stroke = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                StrokeThickness = 1,
                Opacity = 0.8
            };

            Canvas.SetLeft(indicator, sx - SpeakerIndicatorSize / 2);
            Canvas.SetTop(indicator, sy - SpeakerIndicatorSize / 2);
            SpeakerCanvas.Children.Add(indicator);

            // Create label
            var label = new TextBlock
            {
                Text = speaker.Name,
                Foreground = TextBrush,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            // Position label below indicator
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, sx - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, sy + SpeakerIndicatorSize / 2 + 2);
            SpeakerCanvas.Children.Add(label);

            _speakerIndicators.Add((indicator, label));
        }

        // Update gains display for new format
        CreateGainsDisplay();
    }

    private List<SpeakerInfo> GetSpeakerPositions(SurroundFormat format)
    {
        return format switch
        {
            SurroundFormat.Stereo => new List<SpeakerInfo>
            {
                new("L", -30, 1.0, false),
                new("R", 30, 1.0, false)
            },
            SurroundFormat.Surround_5_1 => new List<SpeakerInfo>
            {
                new("L", -30, 1.0, false),
                new("C", 0, 1.0, false),
                new("R", 30, 1.0, false),
                new("Ls", -110, 1.0, false),
                new("Rs", 110, 1.0, false),
                new("LFE", -60, 0.3, true)
            },
            SurroundFormat.Surround_7_1 => new List<SpeakerInfo>
            {
                new("L", -30, 1.0, false),
                new("C", 0, 1.0, false),
                new("R", 30, 1.0, false),
                new("Lss", -90, 1.0, false),
                new("Rss", 90, 1.0, false),
                new("Lb", -150, 1.0, false),
                new("Rb", 150, 1.0, false),
                new("LFE", -60, 0.3, true)
            },
            SurroundFormat.Atmos_7_1_4 => new List<SpeakerInfo>
            {
                new("L", -30, 1.0, false),
                new("C", 0, 1.0, false),
                new("R", 30, 1.0, false),
                new("Lss", -90, 1.0, false),
                new("Rss", 90, 1.0, false),
                new("Lb", -150, 1.0, false),
                new("Rb", 150, 1.0, false),
                new("LFE", -60, 0.3, true),
                // Height channels shown at reduced distance
                new("TFL", -45, 0.6, false),
                new("TFR", 45, 0.6, false),
                new("TRL", -135, 0.6, false),
                new("TRR", 135, 0.6, false)
            },
            _ => new List<SpeakerInfo>
            {
                new("L", -30, 1.0, false),
                new("R", 30, 1.0, false)
            }
        };
    }

    private record SpeakerInfo(string Name, double Angle, double Distance, bool IsLFE);

    #endregion

    #region Grid Drawing

    private void DrawGridLines()
    {
        if (GridLinesCanvas == null || PanningCanvas == null) return;

        double width = PanningCanvas.ActualWidth;
        double height = PanningCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        GridLinesCanvas.Children.Clear();

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 10;

        // Draw concentric circles
        for (int i = 1; i <= 3; i++)
        {
            double r = radius * i / 3;
            var circle = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = GridBrush,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(circle, centerX - r);
            Canvas.SetTop(circle, centerY - r);
            GridLinesCanvas.Children.Add(circle);
        }

        // Draw crosshairs
        var hLine = new Line
        {
            X1 = 0, Y1 = centerY,
            X2 = width, Y2 = centerY,
            Stroke = GridBrush, StrokeThickness = 1
        };
        GridLinesCanvas.Children.Add(hLine);

        var vLine = new Line
        {
            X1 = centerX, Y1 = 0,
            X2 = centerX, Y2 = height,
            Stroke = GridBrush, StrokeThickness = 1
        };
        GridLinesCanvas.Children.Add(vLine);

        // Draw diagonal lines
        var diag1 = new Line
        {
            X1 = centerX - radius * 0.707, Y1 = centerY - radius * 0.707,
            X2 = centerX + radius * 0.707, Y2 = centerY + radius * 0.707,
            Stroke = GridBrush, StrokeThickness = 0.5, StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        GridLinesCanvas.Children.Add(diag1);

        var diag2 = new Line
        {
            X1 = centerX + radius * 0.707, Y1 = centerY - radius * 0.707,
            X2 = centerX - radius * 0.707, Y2 = centerY + radius * 0.707,
            Stroke = GridBrush, StrokeThickness = 0.5, StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        GridLinesCanvas.Children.Add(diag2);

        // Front indicator
        var frontLabel = new TextBlock
        {
            Text = "FRONT",
            Foreground = TextSecondaryBrush,
            FontSize = 9
        };
        frontLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(frontLabel, centerX - frontLabel.DesiredSize.Width / 2);
        Canvas.SetTop(frontLabel, 4);
        GridLinesCanvas.Children.Add(frontLabel);

        // Rear indicator
        var rearLabel = new TextBlock
        {
            Text = "REAR",
            Foreground = TextSecondaryBrush,
            FontSize = 9
        };
        rearLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(rearLabel, centerX - rearLabel.DesiredSize.Width / 2);
        Canvas.SetTop(rearLabel, height - rearLabel.DesiredSize.Height - 4);
        GridLinesCanvas.Children.Add(rearLabel);
    }

    #endregion

    #region Source Position

    private void UpdateSourcePosition()
    {
        if (!_isInitialized || PanningCanvas == null || SourceIndicator == null) return;

        double width = PanningCanvas.ActualWidth;
        double height = PanningCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - SourceIndicatorSize;

        // Convert normalized coordinates to canvas position
        // X: -1 to 1 (left to right)
        // Y: -1 to 1 (back to front, but we flip for screen coordinates)
        double screenX = centerX + SourceX * radius;
        double screenY = centerY - SourceY * radius; // Flip Y for screen

        Canvas.SetLeft(SourceIndicator, screenX - SourceIndicatorSize / 2);
        Canvas.SetTop(SourceIndicator, screenY - SourceIndicatorSize / 2);

        // Update coordinate display
        XCoordText.Text = $"{SourceX:F2}";
        YCoordText.Text = $"{SourceY:F2}";

        // Update speaker brightness based on proximity
        UpdateSpeakerBrightness();
    }

    private void UpdateSpreadIndicator()
    {
        if (!_isInitialized || PanningCanvas == null || SpreadIndicator == null) return;

        double width = PanningCanvas.ActualWidth;
        double height = PanningCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - SourceIndicatorSize;

        // Calculate spread circle size
        double spreadRadius = radius * Spread;

        // Position spread indicator centered on source position
        double screenX = centerX + SourceX * radius;
        double screenY = centerY - SourceY * radius;

        SpreadIndicator.Width = spreadRadius * 2;
        SpreadIndicator.Height = spreadRadius * 2;
        Canvas.SetLeft(SpreadIndicator, screenX - spreadRadius);
        Canvas.SetTop(SpreadIndicator, screenY - spreadRadius);

        SpreadIndicator.Visibility = Spread > 0.01 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSpeakerBrightness()
    {
        if (SurroundPanner == null || _speakerIndicators.Count == 0) return;

        // Calculate gains from current position
        float azimuth = CalculateAzimuth(SourceX, SourceY);
        var gains = SurroundPanner.CalculateGains(azimuth, 0);

        var channelNames = Format.GetChannelNames();
        int speakerIndex = 0;

        foreach (var (indicator, label) in _speakerIndicators)
        {
            if (speakerIndex < gains.Length && speakerIndex < channelNames.Length)
            {
                float gain = gains[speakerIndex];

                // Interpolate color based on gain
                byte brightness = (byte)(128 + 127 * gain);
                var isLFE = channelNames[speakerIndex] == "LFE";
                var isCenter = channelNames[speakerIndex] == "C";

                if (isLFE)
                {
                    indicator.Fill = new SolidColorBrush(Color.FromRgb(
                        (byte)(0x80 + 0x7F * LFELevel),
                        (byte)(0x40 + 0x2B * LFELevel),
                        (byte)(0x40 + 0x2B * LFELevel)));
                }
                else if (isCenter)
                {
                    indicator.Fill = new SolidColorBrush(Color.FromRgb(
                        brightness,
                        (byte)(0xA5 * gain + 0x4D * (1 - gain)),
                        (byte)(0x4D * (1 - gain))));
                }
                else
                {
                    indicator.Fill = new SolidColorBrush(Color.FromRgb(
                        (byte)(0x00 + 0x00 * (1 - gain)),
                        (byte)(0xD9 * gain + 0x4D * (1 - gain)),
                        (byte)(0xFF * gain + 0x52 * (1 - gain))));
                }

                indicator.Opacity = 0.5 + gain * 0.5;
            }
            speakerIndex++;
        }
    }

    #endregion

    #region Gains Display

    private void CreateGainsDisplay()
    {
        if (GainsPanel == null) return;

        GainsPanel.Children.Clear();
        _gainDisplays.Clear();

        var channelNames = Format.GetChannelNames();

        foreach (var name in channelNames)
        {
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 12, 4)
            };

            var nameBlock = new TextBlock
            {
                Text = $"{name}:",
                Foreground = name == "LFE" ? LFEBrush :
                            name == "C" ? CenterBrush : TextSecondaryBrush,
                FontSize = 10,
                Width = 28
            };

            var valueBlock = new TextBlock
            {
                Text = "0%",
                Foreground = AccentBrush,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Width = 35
            };

            container.Children.Add(nameBlock);
            container.Children.Add(valueBlock);
            GainsPanel.Children.Add(container);

            _gainDisplays.Add((nameBlock, valueBlock));
        }
    }

    private void UpdateGainsDisplay()
    {
        if (_gainDisplays.Count == 0) return;

        float[]? gains = null;

        if (SurroundPanner != null)
        {
            float azimuth = CalculateAzimuth(SourceX, SourceY);
            gains = SurroundPanner.CalculateGains(azimuth, 0);
        }
        else
        {
            // Calculate simple gains without a SurroundPanner
            gains = CalculateSimpleGains();
        }

        if (gains == null) return;

        for (int i = 0; i < _gainDisplays.Count && i < gains.Length; i++)
        {
            var (_, valueBlock) = _gainDisplays[i];
            float gain = Math.Clamp(gains[i], 0f, 1f);
            valueBlock.Text = $"{gain * 100:F0}%";
        }
    }

    private float[] CalculateSimpleGains()
    {
        var channelNames = Format.GetChannelNames();
        var gains = new float[channelNames.Length];

        // Simple distance-based panning
        float azimuth = CalculateAzimuth(SourceX, SourceY);
        float distance = MathF.Sqrt(SourceX * SourceX + SourceY * SourceY);
        distance = Math.Clamp(distance, 0f, 1f);

        for (int i = 0; i < channelNames.Length; i++)
        {
            string name = channelNames[i];

            if (name == "LFE")
            {
                gains[i] = LFELevel;
                continue;
            }

            // Get speaker angle based on standard positions
            float speakerAngle = GetSpeakerAngle(name);
            float angleDiff = MathF.Abs(NormalizeAngle(azimuth - speakerAngle));

            // Simple cosine-based gain falloff
            float gain = MathF.Max(0, MathF.Cos(angleDiff * MathF.PI / 180f));

            // Apply spread
            if (Spread > 0)
            {
                float equalGain = 1f / MathF.Sqrt(channelNames.Length - (Format.HasLFE() ? 1 : 0));
                gain = gain * (1 - Spread) + equalGain * Spread;
            }

            gains[i] = gain;
        }

        // Normalize
        float sumSquared = 0f;
        for (int i = 0; i < gains.Length; i++)
        {
            if (channelNames[i] != "LFE")
            {
                sumSquared += gains[i] * gains[i];
            }
        }
        if (sumSquared > 0.001f)
        {
            float normalizer = 1f / MathF.Sqrt(sumSquared);
            for (int i = 0; i < gains.Length; i++)
            {
                if (channelNames[i] != "LFE")
                {
                    gains[i] *= normalizer;
                }
            }
        }

        return gains;
    }

    private static float GetSpeakerAngle(string name)
    {
        return name switch
        {
            "L" => -30f,
            "R" => 30f,
            "C" => 0f,
            "Ls" or "Lss" => -110f,
            "Rs" or "Rss" => 110f,
            "Lb" or "Lsr" => -150f,
            "Rb" or "Rsr" => 150f,
            "TFL" => -45f,
            "TFR" => 45f,
            "TRL" => -135f,
            "TRR" => 135f,
            _ => 0f
        };
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    #endregion

    #region Mouse Handlers

    private void PanningCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        PanningCanvas.CaptureMouse();
        UpdatePositionFromMouse(e.GetPosition(PanningCanvas));
    }

    private void PanningCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            UpdatePositionFromMouse(e.GetPosition(PanningCanvas));
        }
    }

    private void PanningCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        PanningCanvas.ReleaseMouseCapture();
    }

    private void PanningCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        // Don't stop dragging on leave - mouse capture handles this
    }

    private void UpdatePositionFromMouse(Point position)
    {
        double width = PanningCanvas.ActualWidth;
        double height = PanningCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - SourceIndicatorSize;

        // Convert mouse position to normalized coordinates
        double x = (position.X - centerX) / radius;
        double y = -(position.Y - centerY) / radius; // Flip Y

        // Clamp to unit circle
        double distance = Math.Sqrt(x * x + y * y);
        if (distance > 1.0)
        {
            x /= distance;
            y /= distance;
        }

        SourceX = (float)x;
        SourceY = (float)y;
    }

    #endregion

    #region Slider Handlers

    private void FormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        Format = FormatCombo.SelectedIndex switch
        {
            0 => SurroundFormat.Stereo,
            1 => SurroundFormat.Surround_5_1,
            2 => SurroundFormat.Surround_7_1,
            3 => SurroundFormat.Atmos_7_1_4,
            _ => SurroundFormat.Surround_5_1
        };
    }

    private void LFESlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        LFELevel = (float)(e.NewValue / 100.0);
        LFEValueText.Text = $"{e.NewValue:F0}%";
    }

    private void SpreadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        Spread = (float)(e.NewValue / 100.0);
        SpreadValueText.Text = $"{e.NewValue:F0}%";
        UpdateSpreadIndicator();
    }

    private void CenterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        CenterDivergence = (float)(e.NewValue / 100.0);
        CenterValueText.Text = $"{e.NewValue:F0}%";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the source position programmatically.
    /// </summary>
    /// <param name="x">X position (-1 to 1)</param>
    /// <param name="y">Y position (-1 to 1)</param>
    public void SetSourcePosition(float x, float y)
    {
        SourceX = x;
        SourceY = y;
    }

    /// <summary>
    /// Resets the panner to default center position.
    /// </summary>
    public void Reset()
    {
        SourceX = 0f;
        SourceY = 0f;
        LFELevel = 0f;
        Spread = 0f;
        CenterDivergence = 0.5f;
    }

    /// <summary>
    /// Forces a refresh of the visual display.
    /// </summary>
    public void Refresh()
    {
        UpdateSpeakerLayout();
        DrawGridLines();
        UpdateSourcePosition();
        UpdateSpreadIndicator();
        UpdateGainsDisplay();
    }

    #endregion
}

#region Event Args

/// <summary>
/// Event args for source position changes.
/// </summary>
public class SurroundPositionChangedEventArgs : EventArgs
{
    public float X { get; }
    public float Y { get; }

    public SurroundPositionChangedEventArgs(float x, float y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Event args for panner parameter changes.
/// </summary>
public class PannerParameterChangedEventArgs : EventArgs
{
    public string ParameterName { get; }
    public object? Value { get; }

    public PannerParameterChangedEventArgs(string parameterName, object? value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

#endregion
