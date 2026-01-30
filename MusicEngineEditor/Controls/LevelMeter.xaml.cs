// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Audio level meter visualization.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Orientation for the level meter display.
/// </summary>
public enum MeterOrientation
{
    Vertical,
    Horizontal
}

/// <summary>
/// Visual style for peak hold indicators.
/// </summary>
public enum PeakIndicatorDisplayStyle
{
    /// <summary>
    /// Thin horizontal/vertical line at peak position.
    /// </summary>
    Line,

    /// <summary>
    /// Small rectangle block at peak position.
    /// </summary>
    Block,

    /// <summary>
    /// Triangle/arrow pointing to peak position.
    /// </summary>
    Arrow,

    /// <summary>
    /// Gradient fade from peak position.
    /// </summary>
    GradientFade
}

/// <summary>
/// A professional VU/Peak meter control for audio level visualization.
/// Features stereo support, peak hold indicators, clip detection, and smooth interpolation.
/// Styled to match professional DAWs like Ableton Live and FL Studio.
/// </summary>
public partial class LevelMeter : UserControl
{
    #region Constants

    private const double MinDb = -60.0;
    private const double MaxDb = 0.0;
    private const double ClipThresholdDb = 0.0;
    private const double DefaultPeakFallRate = 20.0; // dB per second
    private const double DefaultLevelFallRate = 40.0; // dB per second for smooth falloff
    private const double InterpolationSpeed = 12.0; // Speed of level interpolation
    private const int MeterSegmentCount = 30; // Number of LED-style segments
    private const double MeterBarWidth = 12.0;
    private const double MeterSpacing = 4.0;
    private const double ScaleWidth = 24.0;

    // Peak hold animation constants
    private const double PeakFallAcceleration = 15.0; // Acceleration when falling (dB/s^2)
    private const double MaxPeakFallVelocity = 80.0; // Maximum fall velocity (dB/s)
    private const double PeakHoldIndicatorHeight = 3.0; // Height of peak indicator line
    private const double MinPeakHoldTimeSeconds = 0.5;
    private const double MaxPeakHoldTimeSeconds = 5.0;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty LeftLevelProperty =
        DependencyProperty.Register(nameof(LeftLevel), typeof(float), typeof(LevelMeter),
            new PropertyMetadata(0f, OnLevelChanged));

    public static readonly DependencyProperty RightLevelProperty =
        DependencyProperty.Register(nameof(RightLevel), typeof(float), typeof(LevelMeter),
            new PropertyMetadata(0f, OnLevelChanged));

    public static readonly DependencyProperty PeakHoldTimeProperty =
        DependencyProperty.Register(nameof(PeakHoldTime), typeof(TimeSpan), typeof(LevelMeter),
            new PropertyMetadata(TimeSpan.FromSeconds(1.5)));

    public static readonly DependencyProperty ShowPeakHoldProperty =
        DependencyProperty.Register(nameof(ShowPeakHold), typeof(bool), typeof(LevelMeter),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowScaleProperty =
        DependencyProperty.Register(nameof(ShowScale), typeof(bool), typeof(LevelMeter),
            new PropertyMetadata(true, OnLayoutPropertyChanged));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(MeterOrientation), typeof(LevelMeter),
            new PropertyMetadata(MeterOrientation.Vertical, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ShowChannelLabelsProperty =
        DependencyProperty.Register(nameof(ShowChannelLabels), typeof(bool), typeof(LevelMeter),
            new PropertyMetadata(true, OnLayoutPropertyChanged));

    public static readonly DependencyProperty PeakFallRateProperty =
        DependencyProperty.Register(nameof(PeakFallRate), typeof(double), typeof(LevelMeter),
            new PropertyMetadata(DefaultPeakFallRate));

    public static readonly DependencyProperty SmoothingEnabledProperty =
        DependencyProperty.Register(nameof(SmoothingEnabled), typeof(bool), typeof(LevelMeter),
            new PropertyMetadata(true));

    public static readonly DependencyProperty PeakHoldFallAnimationProperty =
        DependencyProperty.Register(nameof(PeakHoldFallAnimation), typeof(bool), typeof(LevelMeter),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ResetPeakOnClickProperty =
        DependencyProperty.Register(nameof(ResetPeakOnClick), typeof(bool), typeof(LevelMeter),
            new PropertyMetadata(true));

    public static readonly DependencyProperty PeakIndicatorStyleProperty =
        DependencyProperty.Register(nameof(PeakIndicatorStyle), typeof(PeakIndicatorDisplayStyle), typeof(LevelMeter),
            new PropertyMetadata(PeakIndicatorDisplayStyle.Line, OnPeakIndicatorStyleChanged));

    /// <summary>
    /// Gets or sets the left channel level (0.0 to 1.0, can exceed 1.0 for clipping indication).
    /// </summary>
    public float LeftLevel
    {
        get => (float)GetValue(LeftLevelProperty);
        set => SetValue(LeftLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the right channel level (0.0 to 1.0, can exceed 1.0 for clipping indication).
    /// </summary>
    public float RightLevel
    {
        get => (float)GetValue(RightLevelProperty);
        set => SetValue(RightLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the duration that peak indicators hold their position before falling.
    /// </summary>
    public TimeSpan PeakHoldTime
    {
        get => (TimeSpan)GetValue(PeakHoldTimeProperty);
        set => SetValue(PeakHoldTimeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to display peak hold indicators.
    /// </summary>
    public bool ShowPeakHold
    {
        get => (bool)GetValue(ShowPeakHoldProperty);
        set => SetValue(ShowPeakHoldProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to display the dB scale markings.
    /// </summary>
    public bool ShowScale
    {
        get => (bool)GetValue(ShowScaleProperty);
        set => SetValue(ShowScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the meter orientation (Vertical or Horizontal).
    /// </summary>
    public MeterOrientation Orientation
    {
        get => (MeterOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to display channel labels (L/R).
    /// </summary>
    public bool ShowChannelLabels
    {
        get => (bool)GetValue(ShowChannelLabelsProperty);
        set => SetValue(ShowChannelLabelsProperty, value);
    }

    /// <summary>
    /// Gets or sets the rate at which peak indicators fall (dB per second).
    /// </summary>
    public double PeakFallRate
    {
        get => (double)GetValue(PeakFallRateProperty);
        set => SetValue(PeakFallRateProperty, value);
    }

    /// <summary>
    /// Gets or sets whether level smoothing/interpolation is enabled.
    /// </summary>
    public bool SmoothingEnabled
    {
        get => (bool)GetValue(SmoothingEnabledProperty);
        set => SetValue(SmoothingEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether smooth animated fallback is enabled for peak hold indicators.
    /// When enabled, peaks accelerate smoothly when falling instead of linear fall.
    /// </summary>
    public bool PeakHoldFallAnimation
    {
        get => (bool)GetValue(PeakHoldFallAnimationProperty);
        set => SetValue(PeakHoldFallAnimationProperty, value);
    }

    /// <summary>
    /// Gets or sets whether clicking on the meter resets the peak hold indicators.
    /// </summary>
    public bool ResetPeakOnClick
    {
        get => (bool)GetValue(ResetPeakOnClickProperty);
        set => SetValue(ResetPeakOnClickProperty, value);
    }

    /// <summary>
    /// Gets or sets the visual style of the peak hold indicator.
    /// </summary>
    public PeakIndicatorDisplayStyle PeakIndicatorStyle
    {
        get => (PeakIndicatorDisplayStyle)GetValue(PeakIndicatorStyleProperty);
        set => SetValue(PeakIndicatorStyleProperty, value);
    }

    #endregion

    #region Private Fields

    // Visual elements
    private Border? _leftMeterTrack;
    private Border? _rightMeterTrack;
    private Shapes.Rectangle? _leftLevelBar;
    private Shapes.Rectangle? _rightLevelBar;
    private Shapes.Rectangle? _leftPeakIndicator;
    private Shapes.Rectangle? _rightPeakIndicator;
    private Shapes.Ellipse? _leftClipIndicator;
    private Shapes.Ellipse? _rightClipIndicator;
    private Canvas? _scaleCanvas;
    private Grid? _metersContainer;

    // Level state
    private double _displayedLeftLevel;
    private double _displayedRightLevel;
    private double _leftPeakLevel;
    private double _rightPeakLevel;
    private DateTime _leftPeakTime;
    private DateTime _rightPeakTime;
    private bool _leftClipping;
    private bool _rightClipping;
    private DateTime _leftClipTime;
    private DateTime _rightClipTime;

    // Peak hold animation state
    private double _leftPeakFallVelocity;
    private double _rightPeakFallVelocity;
    private bool _leftPeakFalling;
    private bool _rightPeakFalling;
    private double _leftPeakOpacity = 1.0;
    private double _rightPeakOpacity = 1.0;

    // Animation
    private DispatcherTimer? _updateTimer;
    private DateTime _lastUpdateTime;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public LevelMeter()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildVisualTree();
        StartUpdateTimer();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopUpdateTimer();
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateMeterLayout();
        }
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LevelMeter meter && meter._isInitialized)
        {
            meter.UpdatePeakLevels();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LevelMeter meter && meter._isInitialized)
        {
            meter.BuildVisualTree();
        }
    }

    private static void OnPeakIndicatorStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LevelMeter meter && meter._isInitialized)
        {
            meter.UpdatePeakIndicatorStyle();
        }
    }

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResetPeakOnClick)
        {
            ResetPeakHold();
            e.Handled = true;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets both channel levels at once for efficiency.
    /// </summary>
    /// <param name="left">Left channel level (0.0 to 1.0+)</param>
    /// <param name="right">Right channel level (0.0 to 1.0+)</param>
    public void SetLevels(float left, float right)
    {
        LeftLevel = left;
        RightLevel = right;
    }

    /// <summary>
    /// Resets the meter, clearing all levels and peak indicators.
    /// </summary>
    public void Reset()
    {
        LeftLevel = 0f;
        RightLevel = 0f;
        _displayedLeftLevel = 0;
        _displayedRightLevel = 0;
        _leftPeakLevel = MinDb;
        _rightPeakLevel = MinDb;
        _leftClipping = false;
        _rightClipping = false;
        // Reset peak animation state
        _leftPeakFallVelocity = 0;
        _rightPeakFallVelocity = 0;
        _leftPeakFalling = false;
        _rightPeakFalling = false;
        _leftPeakOpacity = 1.0;
        _rightPeakOpacity = 1.0;
        UpdateVisuals();
    }

    /// <summary>
    /// Resets the clip indicators.
    /// </summary>
    public void ResetClipIndicators()
    {
        _leftClipping = false;
        _rightClipping = false;
        UpdateClipIndicators();
    }

    /// <summary>
    /// Resets only the peak hold indicators to minimum level.
    /// </summary>
    public void ResetPeakHold()
    {
        _leftPeakLevel = MinDb;
        _rightPeakLevel = MinDb;
        _leftPeakFallVelocity = 0;
        _rightPeakFallVelocity = 0;
        _leftPeakFalling = false;
        _rightPeakFalling = false;
        _leftPeakOpacity = 1.0;
        _rightPeakOpacity = 1.0;
        UpdatePeakIndicators();
    }

    /// <summary>
    /// Sets the peak hold time within valid range (0.5 to 5 seconds).
    /// </summary>
    /// <param name="seconds">Hold time in seconds.</param>
    public void SetPeakHoldTime(double seconds)
    {
        seconds = Math.Max(MinPeakHoldTimeSeconds, Math.Min(MaxPeakHoldTimeSeconds, seconds));
        PeakHoldTime = TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Gets the current peak level for the left channel in dB.
    /// </summary>
    public double LeftPeakDb => _leftPeakLevel;

    /// <summary>
    /// Gets the current peak level for the right channel in dB.
    /// </summary>
    public double RightPeakDb => _rightPeakLevel;

    #endregion

    #region Private Methods - Visual Tree Building

    private void BuildVisualTree()
    {
        MainGrid.Children.Clear();
        MainGrid.RowDefinitions.Clear();
        MainGrid.ColumnDefinitions.Clear();

        if (Orientation == MeterOrientation.Vertical)
        {
            BuildVerticalLayout();
        }
        else
        {
            BuildHorizontalLayout();
        }

        UpdateMeterLayout();
    }

    private void BuildVerticalLayout()
    {
        // Row definitions: Clip indicators, Meters, Channel labels
        MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Clip indicators
        MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Meters
        if (ShowChannelLabels)
        {
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) }); // Labels
        }

        // Column definitions: Scale (optional), Left meter, Right meter
        if (ShowScale)
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ScaleWidth) });
        }
        MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Create scale if enabled
        if (ShowScale)
        {
            _scaleCanvas = CreateScaleCanvas();
            Grid.SetRow(_scaleCanvas, 1);
            Grid.SetColumn(_scaleCanvas, 0);
            MainGrid.Children.Add(_scaleCanvas);
        }

        // Create meters container
        _metersContainer = new Grid();
        _metersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _metersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MeterSpacing) });
        _metersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetRow(_metersContainer, 1);
        Grid.SetColumn(_metersContainer, ShowScale ? 1 : 0);
        MainGrid.Children.Add(_metersContainer);

        // Create left meter
        var leftMeterGrid = CreateVerticalMeterBar(true);
        Grid.SetColumn(leftMeterGrid, 0);
        _metersContainer.Children.Add(leftMeterGrid);

        // Create right meter
        var rightMeterGrid = CreateVerticalMeterBar(false);
        Grid.SetColumn(rightMeterGrid, 2);
        _metersContainer.Children.Add(rightMeterGrid);

        // Create clip indicators
        var clipContainer = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _leftClipIndicator = CreateClipIndicator();
        _rightClipIndicator = CreateClipIndicator();

        clipContainer.Children.Add(_leftClipIndicator);
        clipContainer.Children.Add(new Border { Width = MeterSpacing });
        clipContainer.Children.Add(_rightClipIndicator);

        Grid.SetRow(clipContainer, 0);
        Grid.SetColumn(clipContainer, ShowScale ? 1 : 0);
        MainGrid.Children.Add(clipContainer);

        // Create channel labels if enabled
        if (ShowChannelLabels)
        {
            var labelContainer = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            labelContainer.Children.Add(CreateChannelLabel("L"));
            labelContainer.Children.Add(new Border { Width = MeterSpacing + 8 });
            labelContainer.Children.Add(CreateChannelLabel("R"));

            Grid.SetRow(labelContainer, 2);
            Grid.SetColumn(labelContainer, ShowScale ? 1 : 0);
            MainGrid.Children.Add(labelContainer);
        }
    }

    private void BuildHorizontalLayout()
    {
        // Row definitions: Left meter, spacing, Right meter
        MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MeterSpacing) });
        MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Column definitions: Labels (optional), Meters, Clip indicators, Scale (optional)
        if (ShowChannelLabels)
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        }
        MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Clip indicators
        if (ShowScale)
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ScaleWidth) });
        }

        int meterColumn = ShowChannelLabels ? 1 : 0;

        // Create left meter (horizontal)
        var leftMeterGrid = CreateHorizontalMeterBar(true);
        Grid.SetRow(leftMeterGrid, 0);
        Grid.SetColumn(leftMeterGrid, meterColumn);
        MainGrid.Children.Add(leftMeterGrid);

        // Create right meter (horizontal)
        var rightMeterGrid = CreateHorizontalMeterBar(false);
        Grid.SetRow(rightMeterGrid, 2);
        Grid.SetColumn(rightMeterGrid, meterColumn);
        MainGrid.Children.Add(rightMeterGrid);

        // Create clip indicators
        _leftClipIndicator = CreateClipIndicator();
        _rightClipIndicator = CreateClipIndicator();

        var leftClipContainer = new Border
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _leftClipIndicator
        };
        Grid.SetRow(leftClipContainer, 0);
        Grid.SetColumn(leftClipContainer, meterColumn + 1);
        MainGrid.Children.Add(leftClipContainer);

        var rightClipContainer = new Border
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _rightClipIndicator
        };
        Grid.SetRow(rightClipContainer, 2);
        Grid.SetColumn(rightClipContainer, meterColumn + 1);
        MainGrid.Children.Add(rightClipContainer);

        // Create channel labels if enabled
        if (ShowChannelLabels)
        {
            var leftLabel = CreateChannelLabel("L");
            leftLabel.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(leftLabel, 0);
            Grid.SetColumn(leftLabel, 0);
            MainGrid.Children.Add(leftLabel);

            var rightLabel = CreateChannelLabel("R");
            rightLabel.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(rightLabel, 2);
            Grid.SetColumn(rightLabel, 0);
            MainGrid.Children.Add(rightLabel);
        }

        // Create scale if enabled
        if (ShowScale)
        {
            _scaleCanvas = CreateHorizontalScaleCanvas();
            Grid.SetRow(_scaleCanvas, 0);
            Grid.SetRowSpan(_scaleCanvas, 3);
            Grid.SetColumn(_scaleCanvas, meterColumn + 2);
            MainGrid.Children.Add(_scaleCanvas);
        }
    }

    private Grid CreateVerticalMeterBar(bool isLeft)
    {
        var grid = new Grid();

        // Track background with border
        var track = new Border
        {
            Background = FindResource("MeterTrackBrush") as Brush,
            BorderBrush = FindResource("MeterBorderBrush") as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2)
        };
        grid.Children.Add(track);

        // Inner track for level display
        var innerTrack = new Border
        {
            Margin = new Thickness(2),
            ClipToBounds = true
        };

        // Level bar (will be scaled from bottom)
        var levelBar = new Shapes.Rectangle
        {
            Fill = FindResource("LevelGradientBrush") as Brush,
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 0
        };

        innerTrack.Child = levelBar;
        grid.Children.Add(innerTrack);

        // Peak hold indicator
        var peakIndicator = new Shapes.Rectangle
        {
            Fill = FindResource("PeakHoldBrush") as Brush,
            Height = 2,
            Margin = new Thickness(3, 0, 3, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = ShowPeakHold ? Visibility.Visible : Visibility.Collapsed,
            Effect = new DropShadowEffect
            {
                Color = Colors.White,
                BlurRadius = 4,
                ShadowDepth = 0,
                Opacity = 0.6
            },
            ToolTip = "Click to reset peak hold"
        };
        grid.Children.Add(peakIndicator);

        // Store references
        if (isLeft)
        {
            _leftMeterTrack = track;
            _leftLevelBar = levelBar;
            _leftPeakIndicator = peakIndicator;
        }
        else
        {
            _rightMeterTrack = track;
            _rightLevelBar = levelBar;
            _rightPeakIndicator = peakIndicator;
        }

        // Apply current peak indicator style
        ApplyPeakIndicatorStyle(peakIndicator, isLeft);

        return grid;
    }

    private Grid CreateHorizontalMeterBar(bool isLeft)
    {
        var grid = new Grid();

        // Track background with border
        var track = new Border
        {
            Background = FindResource("MeterTrackBrush") as Brush,
            BorderBrush = FindResource("MeterBorderBrush") as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2)
        };
        grid.Children.Add(track);

        // Inner track for level display
        var innerTrack = new Border
        {
            Margin = new Thickness(2),
            ClipToBounds = true
        };

        // Level bar (will be scaled from left)
        var levelBar = new Shapes.Rectangle
        {
            Fill = FindResource("LevelGradientHorizontalBrush") as Brush,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Width = 0
        };

        innerTrack.Child = levelBar;
        grid.Children.Add(innerTrack);

        // Peak hold indicator
        var peakIndicator = new Shapes.Rectangle
        {
            Fill = FindResource("PeakHoldBrush") as Brush,
            Width = 2,
            Margin = new Thickness(0, 3, 0, 3),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Visibility = ShowPeakHold ? Visibility.Visible : Visibility.Collapsed,
            Effect = new DropShadowEffect
            {
                Color = Colors.White,
                BlurRadius = 4,
                ShadowDepth = 0,
                Opacity = 0.6
            },
            ToolTip = "Click to reset peak hold"
        };
        grid.Children.Add(peakIndicator);

        // Store references
        if (isLeft)
        {
            _leftMeterTrack = track;
            _leftLevelBar = levelBar;
            _leftPeakIndicator = peakIndicator;
        }
        else
        {
            _rightMeterTrack = track;
            _rightLevelBar = levelBar;
            _rightPeakIndicator = peakIndicator;
        }

        // Apply current peak indicator style
        ApplyPeakIndicatorStyle(peakIndicator, isLeft);

        return grid;
    }

    private Shapes.Ellipse CreateClipIndicator()
    {
        return new Shapes.Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = FindResource("ClipIndicatorOffBrush") as Brush,
            Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0x20, 0x20)),
            StrokeThickness = 1
        };
    }

    private TextBlock CreateChannelLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = FindResource("ChannelLabelBrush") as Brush,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private Canvas CreateScaleCanvas()
    {
        var canvas = new Canvas
        {
            Background = Brushes.Transparent
        };

        // Scale markings will be drawn in UpdateMeterLayout
        return canvas;
    }

    private Canvas CreateHorizontalScaleCanvas()
    {
        var canvas = new Canvas
        {
            Background = Brushes.Transparent
        };

        return canvas;
    }

    #endregion

    #region Private Methods - Layout and Drawing

    private void UpdateMeterLayout()
    {
        if (_scaleCanvas != null)
        {
            DrawScaleMarkings();
        }

        UpdateVisuals();
    }

    private void DrawScaleMarkings()
    {
        if (_scaleCanvas == null) return;

        _scaleCanvas.Children.Clear();

        var brush = FindResource("ScaleTextBrush") as Brush ?? Brushes.Gray;
        var tickBrush = FindResource("MeterBorderBrush") as Brush ?? Brushes.DarkGray;

        // dB values to mark
        double[] dbMarks = { 0, -3, -6, -12, -18, -24, -36, -48, -60 };

        if (Orientation == MeterOrientation.Vertical)
        {
            double height = _scaleCanvas.ActualHeight > 0 ? _scaleCanvas.ActualHeight : 150;

            foreach (var db in dbMarks)
            {
                double normalizedLevel = (db - MinDb) / (MaxDb - MinDb);
                double y = height * (1 - normalizedLevel);

                // Tick mark
                var tick = new Shapes.Line
                {
                    X1 = ScaleWidth - 6,
                    Y1 = y,
                    X2 = ScaleWidth - 2,
                    Y2 = y,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                };
                _scaleCanvas.Children.Add(tick);

                // Label
                var label = new TextBlock
                {
                    Text = db == 0 ? "0" : db.ToString(),
                    Foreground = brush,
                    FontSize = 9,
                    TextAlignment = TextAlignment.Right
                };

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetRight(label, 8);
                Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
                _scaleCanvas.Children.Add(label);
            }
        }
        else // Horizontal
        {
            double width = _scaleCanvas.ActualWidth > 0 ? _scaleCanvas.ActualWidth : 150;

            foreach (var db in dbMarks)
            {
                double normalizedLevel = (db - MinDb) / (MaxDb - MinDb);
                double x = width * normalizedLevel;

                // Tick mark
                var tick = new Shapes.Line
                {
                    X1 = x,
                    Y1 = 2,
                    X2 = x,
                    Y2 = 8,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                };
                _scaleCanvas.Children.Add(tick);

                // Label (vertical text for horizontal meters)
                var label = new TextBlock
                {
                    Text = db == 0 ? "0" : db.ToString(),
                    Foreground = brush,
                    FontSize = 8,
                    RenderTransform = new RotateTransform(-90),
                    RenderTransformOrigin = new Point(0, 0)
                };

                Canvas.SetLeft(label, x - 3);
                Canvas.SetTop(label, 12);
                _scaleCanvas.Children.Add(label);
            }
        }
    }

    private void UpdateVisuals()
    {
        UpdateLevelBars();
        UpdatePeakIndicators();
        UpdateClipIndicators();
    }

    private void UpdateLevelBars()
    {
        if (Orientation == MeterOrientation.Vertical)
        {
            UpdateVerticalLevelBars();
        }
        else
        {
            UpdateHorizontalLevelBars();
        }
    }

    private void UpdateVerticalLevelBars()
    {
        if (_leftLevelBar != null && _leftMeterTrack != null)
        {
            double availableHeight = _leftMeterTrack.ActualHeight - 4; // Account for margin
            if (availableHeight > 0)
            {
                double normalizedLevel = Math.Max(0, Math.Min(1, _displayedLeftLevel));
                _leftLevelBar.Height = availableHeight * normalizedLevel;
            }
        }

        if (_rightLevelBar != null && _rightMeterTrack != null)
        {
            double availableHeight = _rightMeterTrack.ActualHeight - 4;
            if (availableHeight > 0)
            {
                double normalizedLevel = Math.Max(0, Math.Min(1, _displayedRightLevel));
                _rightLevelBar.Height = availableHeight * normalizedLevel;
            }
        }
    }

    private void UpdateHorizontalLevelBars()
    {
        if (_leftLevelBar != null && _leftMeterTrack != null)
        {
            double availableWidth = _leftMeterTrack.ActualWidth - 4;
            if (availableWidth > 0)
            {
                double normalizedLevel = Math.Max(0, Math.Min(1, _displayedLeftLevel));
                _leftLevelBar.Width = availableWidth * normalizedLevel;
            }
        }

        if (_rightLevelBar != null && _rightMeterTrack != null)
        {
            double availableWidth = _rightMeterTrack.ActualWidth - 4;
            if (availableWidth > 0)
            {
                double normalizedLevel = Math.Max(0, Math.Min(1, _displayedRightLevel));
                _rightLevelBar.Width = availableWidth * normalizedLevel;
            }
        }
    }

    private void UpdatePeakIndicators()
    {
        if (!ShowPeakHold) return;

        if (Orientation == MeterOrientation.Vertical)
        {
            UpdateVerticalPeakIndicators();
        }
        else
        {
            UpdateHorizontalPeakIndicators();
        }
    }

    private void UpdateVerticalPeakIndicators()
    {
        if (_leftPeakIndicator != null && _leftMeterTrack != null)
        {
            double availableHeight = _leftMeterTrack.ActualHeight - 4;
            if (availableHeight > 0)
            {
                double normalizedPeak = DbToNormalized(_leftPeakLevel);
                double bottomMargin = availableHeight * normalizedPeak;
                _leftPeakIndicator.Margin = new Thickness(3, 0, 3, bottomMargin + 2);
                _leftPeakIndicator.Opacity = _leftPeakOpacity;

                // Color the peak indicator based on level (red if near 0dB)
                UpdatePeakIndicatorColor(_leftPeakIndicator, _leftPeakLevel);
            }
        }

        if (_rightPeakIndicator != null && _rightMeterTrack != null)
        {
            double availableHeight = _rightMeterTrack.ActualHeight - 4;
            if (availableHeight > 0)
            {
                double normalizedPeak = DbToNormalized(_rightPeakLevel);
                double bottomMargin = availableHeight * normalizedPeak;
                _rightPeakIndicator.Margin = new Thickness(3, 0, 3, bottomMargin + 2);
                _rightPeakIndicator.Opacity = _rightPeakOpacity;

                // Color the peak indicator based on level (red if near 0dB)
                UpdatePeakIndicatorColor(_rightPeakIndicator, _rightPeakLevel);
            }
        }
    }

    private void UpdateHorizontalPeakIndicators()
    {
        if (_leftPeakIndicator != null && _leftMeterTrack != null)
        {
            double availableWidth = _leftMeterTrack.ActualWidth - 4;
            if (availableWidth > 0)
            {
                double normalizedPeak = DbToNormalized(_leftPeakLevel);
                double leftMargin = availableWidth * normalizedPeak;
                _leftPeakIndicator.Margin = new Thickness(leftMargin + 2, 3, 0, 3);
                _leftPeakIndicator.Opacity = _leftPeakOpacity;

                // Color the peak indicator based on level (red if near 0dB)
                UpdatePeakIndicatorColor(_leftPeakIndicator, _leftPeakLevel);
            }
        }

        if (_rightPeakIndicator != null && _rightMeterTrack != null)
        {
            double availableWidth = _rightMeterTrack.ActualWidth - 4;
            if (availableWidth > 0)
            {
                double normalizedPeak = DbToNormalized(_rightPeakLevel);
                double leftMargin = availableWidth * normalizedPeak;
                _rightPeakIndicator.Margin = new Thickness(leftMargin + 2, 3, 0, 3);
                _rightPeakIndicator.Opacity = _rightPeakOpacity;

                // Color the peak indicator based on level (red if near 0dB)
                UpdatePeakIndicatorColor(_rightPeakIndicator, _rightPeakLevel);
            }
        }
    }

    private void UpdatePeakIndicatorColor(Shapes.Rectangle? indicator, double peakLevelDb)
    {
        if (indicator == null || PeakIndicatorStyle != PeakIndicatorDisplayStyle.Line)
            return;

        // Color transitions: white -> yellow -> orange -> red based on level
        Color peakColor;
        if (peakLevelDb >= -3)
        {
            // Red zone (near 0dB)
            peakColor = Color.FromRgb(0xFF, 0x44, 0x44);
        }
        else if (peakLevelDb >= -6)
        {
            // Orange zone
            peakColor = Color.FromRgb(0xFF, 0x88, 0x00);
        }
        else if (peakLevelDb >= -12)
        {
            // Yellow zone
            peakColor = Color.FromRgb(0xFF, 0xFF, 0x00);
        }
        else
        {
            // Normal white
            peakColor = Colors.White;
        }

        indicator.Fill = new SolidColorBrush(peakColor);

        // Update glow effect color to match
        if (indicator.Effect is DropShadowEffect dropShadow)
        {
            dropShadow.Color = peakColor;
        }
    }

    private void UpdateClipIndicators()
    {
        if (_leftClipIndicator != null)
        {
            if (_leftClipping)
            {
                _leftClipIndicator.Fill = FindResource("ClipIndicatorOnBrush") as Brush;
                _leftClipIndicator.Effect = FindResource("ClipGlowEffect") as Effect;
            }
            else
            {
                _leftClipIndicator.Fill = FindResource("ClipIndicatorOffBrush") as Brush;
                _leftClipIndicator.Effect = null;
            }
        }

        if (_rightClipIndicator != null)
        {
            if (_rightClipping)
            {
                _rightClipIndicator.Fill = FindResource("ClipIndicatorOnBrush") as Brush;
                _rightClipIndicator.Effect = FindResource("ClipGlowEffect") as Effect;
            }
            else
            {
                _rightClipIndicator.Fill = FindResource("ClipIndicatorOffBrush") as Brush;
                _rightClipIndicator.Effect = null;
            }
        }
    }

    private void UpdatePeakIndicatorStyle()
    {
        // Update the visual appearance of peak indicators based on style
        ApplyPeakIndicatorStyle(_leftPeakIndicator, true);
        ApplyPeakIndicatorStyle(_rightPeakIndicator, false);
    }

    private void ApplyPeakIndicatorStyle(Shapes.Rectangle? indicator, bool isLeft)
    {
        if (indicator == null) return;

        bool isVertical = Orientation == MeterOrientation.Vertical;
        var style = PeakIndicatorStyle;

        // Get the appropriate brush based on style
        Brush fillBrush = GetPeakIndicatorBrush(style, isLeft);
        indicator.Fill = fillBrush;

        // Apply style-specific dimensions and effects
        switch (style)
        {
            case PeakIndicatorDisplayStyle.Line:
                if (isVertical)
                {
                    indicator.Height = 2;
                    indicator.RadiusX = 0;
                    indicator.RadiusY = 0;
                }
                else
                {
                    indicator.Width = 2;
                    indicator.RadiusX = 0;
                    indicator.RadiusY = 0;
                }
                indicator.Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = 4,
                    ShadowDepth = 0,
                    Opacity = 0.6
                };
                break;

            case PeakIndicatorDisplayStyle.Block:
                if (isVertical)
                {
                    indicator.Height = 4;
                    indicator.RadiusX = 1;
                    indicator.RadiusY = 1;
                }
                else
                {
                    indicator.Width = 4;
                    indicator.RadiusX = 1;
                    indicator.RadiusY = 1;
                }
                indicator.Effect = new DropShadowEffect
                {
                    Color = Colors.Yellow,
                    BlurRadius = 6,
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
                break;

            case PeakIndicatorDisplayStyle.Arrow:
                // Arrow style uses a pointed appearance
                if (isVertical)
                {
                    indicator.Height = 5;
                    indicator.RadiusX = 0;
                    indicator.RadiusY = 0;
                }
                else
                {
                    indicator.Width = 5;
                    indicator.RadiusX = 0;
                    indicator.RadiusY = 0;
                }
                indicator.Effect = new DropShadowEffect
                {
                    Color = Colors.Cyan,
                    BlurRadius = 5,
                    ShadowDepth = 0,
                    Opacity = 0.7
                };
                break;

            case PeakIndicatorDisplayStyle.GradientFade:
                if (isVertical)
                {
                    indicator.Height = 8;
                    indicator.RadiusX = 0;
                    indicator.RadiusY = 0;
                }
                else
                {
                    indicator.Width = 8;
                    indicator.RadiusX = 0;
                    indicator.RadiusY = 0;
                }
                indicator.Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
                break;
        }
    }

    private Brush GetPeakIndicatorBrush(PeakIndicatorDisplayStyle style, bool isLeft)
    {
        bool isVertical = Orientation == MeterOrientation.Vertical;

        switch (style)
        {
            case PeakIndicatorDisplayStyle.Line:
                return FindResource("PeakHoldBrush") as Brush ?? Brushes.White;

            case PeakIndicatorDisplayStyle.Block:
                return new SolidColorBrush(Color.FromRgb(0xFF, 0xEE, 0x00)); // Bright yellow

            case PeakIndicatorDisplayStyle.Arrow:
                return new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xFF)); // Cyan

            case PeakIndicatorDisplayStyle.GradientFade:
                if (isVertical)
                {
                    return new LinearGradientBrush(
                        Color.FromArgb(255, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255),
                        new Point(0, 0),
                        new Point(0, 1));
                }
                else
                {
                    return new LinearGradientBrush(
                        Color.FromArgb(255, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255),
                        new Point(1, 0),
                        new Point(0, 0));
                }

            default:
                return Brushes.White;
        }
    }

    #endregion

    #region Private Methods - Level Processing

    private void UpdatePeakLevels()
    {
        var now = DateTime.UtcNow;

        // Convert linear to dB
        double leftDb = LinearToDb(LeftLevel);
        double rightDb = LinearToDb(RightLevel);

        // Update left peak
        if (leftDb > _leftPeakLevel)
        {
            _leftPeakLevel = leftDb;
            _leftPeakTime = now;
            // Reset fall animation state when new peak is captured
            _leftPeakFalling = false;
            _leftPeakFallVelocity = 0;
            _leftPeakOpacity = 1.0;
        }

        // Update right peak
        if (rightDb > _rightPeakLevel)
        {
            _rightPeakLevel = rightDb;
            _rightPeakTime = now;
            // Reset fall animation state when new peak is captured
            _rightPeakFalling = false;
            _rightPeakFallVelocity = 0;
            _rightPeakOpacity = 1.0;
        }

        // Check for clipping
        if (LeftLevel >= 1.0f)
        {
            _leftClipping = true;
            _leftClipTime = now;
        }

        if (RightLevel >= 1.0f)
        {
            _rightClipping = true;
            _rightClipTime = now;
        }
    }

    private void ProcessLevels(double deltaTime)
    {
        // Target levels (convert from linear 0-1 to normalized display value)
        double targetLeft = Math.Max(0, Math.Min(1, LeftLevel));
        double targetRight = Math.Max(0, Math.Min(1, RightLevel));

        if (SmoothingEnabled)
        {
            // Smooth interpolation for rising levels
            double interpolationFactor = 1.0 - Math.Exp(-InterpolationSpeed * deltaTime);

            // Rising - fast attack
            if (targetLeft > _displayedLeftLevel)
            {
                _displayedLeftLevel = _displayedLeftLevel + (targetLeft - _displayedLeftLevel) * interpolationFactor * 3;
            }
            // Falling - slower decay
            else
            {
                double fallAmount = (DefaultLevelFallRate / 60.0) * deltaTime; // Normalized fall rate
                _displayedLeftLevel = Math.Max(targetLeft, _displayedLeftLevel - fallAmount);
            }

            if (targetRight > _displayedRightLevel)
            {
                _displayedRightLevel = _displayedRightLevel + (targetRight - _displayedRightLevel) * interpolationFactor * 3;
            }
            else
            {
                double fallAmount = (DefaultLevelFallRate / 60.0) * deltaTime;
                _displayedRightLevel = Math.Max(targetRight, _displayedRightLevel - fallAmount);
            }
        }
        else
        {
            _displayedLeftLevel = targetLeft;
            _displayedRightLevel = targetRight;
        }

        // Process peak hold and fall
        ProcessPeakHold(deltaTime);
    }

    private void ProcessPeakHold(double deltaTime)
    {
        var now = DateTime.UtcNow;
        var holdTime = PeakHoldTime;

        // Process left peak with animated fallback
        ProcessSinglePeakHold(
            ref _leftPeakLevel,
            ref _leftPeakFallVelocity,
            ref _leftPeakFalling,
            ref _leftPeakOpacity,
            _leftPeakTime,
            holdTime,
            now,
            deltaTime);

        // Process right peak with animated fallback
        ProcessSinglePeakHold(
            ref _rightPeakLevel,
            ref _rightPeakFallVelocity,
            ref _rightPeakFalling,
            ref _rightPeakOpacity,
            _rightPeakTime,
            holdTime,
            now,
            deltaTime);

        // Auto-reset clip indicators after 2 seconds
        if (_leftClipping && (now - _leftClipTime) > TimeSpan.FromSeconds(2))
        {
            _leftClipping = false;
        }
        if (_rightClipping && (now - _rightClipTime) > TimeSpan.FromSeconds(2))
        {
            _rightClipping = false;
        }
    }

    private void ProcessSinglePeakHold(
        ref double peakLevel,
        ref double fallVelocity,
        ref bool isFalling,
        ref double opacity,
        DateTime peakTime,
        TimeSpan holdTime,
        DateTime now,
        double deltaTime)
    {
        bool shouldFall = (now - peakTime) > holdTime;

        if (shouldFall)
        {
            if (!isFalling)
            {
                // Start falling - initialize velocity
                isFalling = true;
                fallVelocity = PeakFallRate * 0.5; // Start with half the base fall rate
            }

            if (PeakHoldFallAnimation)
            {
                // Animated fall with acceleration (smooth easing effect)
                // Accelerate the fall velocity over time
                fallVelocity += PeakFallAcceleration * deltaTime;
                fallVelocity = Math.Min(fallVelocity, MaxPeakFallVelocity);

                // Apply velocity to peak level
                peakLevel -= fallVelocity * deltaTime;

                // Fade opacity as peak falls for visual effect
                double normalizedLevel = DbToNormalized(peakLevel);
                opacity = Math.Max(0.3, normalizedLevel); // Fade but maintain minimum visibility
            }
            else
            {
                // Linear fall (original behavior)
                peakLevel -= PeakFallRate * deltaTime;
                opacity = 1.0;
            }

            peakLevel = Math.Max(peakLevel, MinDb);
        }
        else
        {
            // Holding - reset fall state
            isFalling = false;
            fallVelocity = 0;
            opacity = 1.0;
        }
    }

    #endregion

    #region Private Methods - Timer

    private void StartUpdateTimer()
    {
        _lastUpdateTime = DateTime.UtcNow;
        _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _updateTimer.Tick += OnUpdateTimerTick;
        _updateTimer.Start();
    }

    private void StopUpdateTimer()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= OnUpdateTimerTick;
            _updateTimer = null;
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double deltaTime = (now - _lastUpdateTime).TotalSeconds;
        _lastUpdateTime = now;

        // Clamp delta time to avoid large jumps
        deltaTime = Math.Min(deltaTime, 0.1);

        ProcessLevels(deltaTime);
        UpdateVisuals();
    }

    #endregion

    #region Private Methods - Utility

    /// <summary>
    /// Converts a linear amplitude value (0-1) to decibels.
    /// </summary>
    private static double LinearToDb(float linear)
    {
        if (linear <= 0)
            return MinDb;

        double db = 20.0 * Math.Log10(linear);
        return Math.Max(MinDb, Math.Min(MaxDb, db));
    }

    /// <summary>
    /// Converts a dB value to a normalized 0-1 range for display.
    /// </summary>
    private static double DbToNormalized(double db)
    {
        return (db - MinDb) / (MaxDb - MinDb);
    }

    #endregion
}
