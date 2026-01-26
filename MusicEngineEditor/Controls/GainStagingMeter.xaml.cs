// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A gain staging meter control that displays pre-fader and post-fader levels
/// with target level indicators for proper gain staging.
/// </summary>
public partial class GainStagingMeter : UserControl
{
    #region Constants

    private const double MinDb = -60.0;
    private const double MaxDb = 0.0;
    private const double DefaultTargetDb = -18.0;
    private const double ClipThresholdDb = 0.0;
    private const double DefaultFallRate = 30.0; // dB per second

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty PreFaderLevelProperty =
        DependencyProperty.Register(nameof(PreFaderLevel), typeof(float), typeof(GainStagingMeter),
            new PropertyMetadata(0f, OnLevelChanged));

    public static readonly DependencyProperty PostFaderLevelProperty =
        DependencyProperty.Register(nameof(PostFaderLevel), typeof(float), typeof(GainStagingMeter),
            new PropertyMetadata(0f, OnLevelChanged));

    public static readonly DependencyProperty TargetLevelProperty =
        DependencyProperty.Register(nameof(TargetLevel), typeof(float), typeof(GainStagingMeter),
            new PropertyMetadata((float)DefaultTargetDb, OnTargetLevelChanged));

    public static readonly DependencyProperty FallRateProperty =
        DependencyProperty.Register(nameof(FallRate), typeof(double), typeof(GainStagingMeter),
            new PropertyMetadata(DefaultFallRate));

    public static readonly DependencyProperty SmoothingEnabledProperty =
        DependencyProperty.Register(nameof(SmoothingEnabled), typeof(bool), typeof(GainStagingMeter),
            new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets the pre-fader level (0.0 to 1.0+, linear scale).
    /// </summary>
    public float PreFaderLevel
    {
        get => (float)GetValue(PreFaderLevelProperty);
        set => SetValue(PreFaderLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the post-fader level (0.0 to 1.0+, linear scale).
    /// </summary>
    public float PostFaderLevel
    {
        get => (float)GetValue(PostFaderLevelProperty);
        set => SetValue(PostFaderLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the target level in dB (default -18 dB).
    /// </summary>
    public float TargetLevel
    {
        get => (float)GetValue(TargetLevelProperty);
        set => SetValue(TargetLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the fall rate in dB per second.
    /// </summary>
    public double FallRate
    {
        get => (double)GetValue(FallRateProperty);
        set => SetValue(FallRateProperty, value);
    }

    /// <summary>
    /// Gets or sets whether level smoothing is enabled.
    /// </summary>
    public bool SmoothingEnabled
    {
        get => (bool)GetValue(SmoothingEnabledProperty);
        set => SetValue(SmoothingEnabledProperty, value);
    }

    #endregion

    #region Private Fields

    private double _displayedPreLevel;
    private double _displayedPostLevel;
    private DispatcherTimer? _updateTimer;
    private DateTime _lastUpdateTime;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public GainStagingMeter()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartUpdateTimer();
        UpdateTargetLinePositions();
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
            UpdateTargetLinePositions();
            UpdateVisuals();
        }
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Level processing is handled by the timer
    }

    private static void OnTargetLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GainStagingMeter meter && meter._isInitialized)
        {
            meter.UpdateTargetLinePositions();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets both pre and post fader levels at once.
    /// </summary>
    /// <param name="preFaderLevel">Pre-fader level (linear 0-1+).</param>
    /// <param name="postFaderLevel">Post-fader level (linear 0-1+).</param>
    public void SetLevels(float preFaderLevel, float postFaderLevel)
    {
        PreFaderLevel = preFaderLevel;
        PostFaderLevel = postFaderLevel;
    }

    /// <summary>
    /// Resets the meter to zero.
    /// </summary>
    public void Reset()
    {
        PreFaderLevel = 0f;
        PostFaderLevel = 0f;
        _displayedPreLevel = MinDb;
        _displayedPostLevel = MinDb;
        UpdateVisuals();
    }

    #endregion

    #region Private Methods - Timer

    private void StartUpdateTimer()
    {
        _lastUpdateTime = DateTime.UtcNow;
        _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
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

        // Clamp delta time
        deltaTime = Math.Min(deltaTime, 0.1);

        ProcessLevels(deltaTime);
        UpdateVisuals();
    }

    #endregion

    #region Private Methods - Level Processing

    private void ProcessLevels(double deltaTime)
    {
        // Convert linear to dB
        double targetPreDb = LinearToDb(PreFaderLevel);
        double targetPostDb = LinearToDb(PostFaderLevel);

        if (SmoothingEnabled)
        {
            // Fast attack, slow release
            double fallAmount = FallRate * deltaTime;

            // Pre-fader
            if (targetPreDb > _displayedPreLevel)
            {
                _displayedPreLevel = targetPreDb; // Instant attack
            }
            else
            {
                _displayedPreLevel = Math.Max(MinDb, _displayedPreLevel - fallAmount);
            }

            // Post-fader
            if (targetPostDb > _displayedPostLevel)
            {
                _displayedPostLevel = targetPostDb;
            }
            else
            {
                _displayedPostLevel = Math.Max(MinDb, _displayedPostLevel - fallAmount);
            }
        }
        else
        {
            _displayedPreLevel = targetPreDb;
            _displayedPostLevel = targetPostDb;
        }
    }

    #endregion

    #region Private Methods - Visual Updates

    private void UpdateVisuals()
    {
        UpdateMeterBar(PreFaderBar, _displayedPreLevel, PreFaderDbText);
        UpdateMeterBar(PostFaderBar, _displayedPostLevel, PostFaderDbText);
    }

    private void UpdateMeterBar(Border? bar, double levelDb, TextBlock? dbText)
    {
        if (bar?.Parent is not Grid parent)
            return;

        double availableWidth = parent.ActualWidth - 2; // Account for margins
        if (availableWidth <= 0)
            return;

        // Calculate bar width
        double normalizedLevel = DbToNormalized(levelDb);
        bar.Width = Math.Max(0, availableWidth * normalizedLevel);

        // Update color based on level
        UpdateBarColor(bar, levelDb);

        // Update dB text
        if (dbText != null)
        {
            if (levelDb <= MinDb)
            {
                dbText.Text = "-inf dB";
            }
            else
            {
                dbText.Text = $"{levelDb:F1} dB";
            }

            // Color code the text
            if (levelDb >= ClipThresholdDb)
            {
                dbText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));
            }
            else if (levelDb >= -6)
            {
                dbText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x00));
            }
            else if (levelDb >= -12)
            {
                dbText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00));
            }
            else
            {
                dbText.Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4));
            }
        }
    }

    private void UpdateBarColor(Border bar, double levelDb)
    {
        // The gradient handles color transitions, but we can adjust opacity for clipping
        if (levelDb >= ClipThresholdDb)
        {
            bar.Opacity = 1.0;
        }
        else
        {
            bar.Opacity = 0.9;
        }
    }

    private void UpdateTargetLinePositions()
    {
        UpdateTargetLine(PreTargetLine, PreFaderBar?.Parent as Grid);
        UpdateTargetLine(PostTargetLine, PostFaderBar?.Parent as Grid);
    }

    private void UpdateTargetLine(System.Windows.Shapes.Rectangle? line, Grid? parent)
    {
        if (line == null || parent == null)
            return;

        double availableWidth = parent.ActualWidth - 2;
        if (availableWidth <= 0)
            return;

        double normalizedTarget = DbToNormalized(TargetLevel);
        double leftMargin = availableWidth * normalizedTarget;

        line.Margin = new Thickness(leftMargin, 1, 0, 1);
        line.ToolTip = $"Target: {TargetLevel:F1} dB";
    }

    #endregion

    #region Private Methods - Utility

    private static double LinearToDb(float linear)
    {
        if (linear <= 0)
            return MinDb;

        double db = 20.0 * Math.Log10(linear);
        return Math.Max(MinDb, Math.Min(MaxDb, db));
    }

    private static double DbToNormalized(double db)
    {
        return Math.Max(0, Math.Min(1, (db - MinDb) / (MaxDb - MinDb)));
    }

    #endregion
}
