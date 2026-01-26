// MusicEngineEditor - Zoom Preset Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A toolbar control providing zoom presets for the arrangement view.
/// </summary>
public partial class ZoomPresetControl : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Identifies the CurrentZoom dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentZoomProperty =
        DependencyProperty.Register(
            nameof(CurrentZoom),
            typeof(double),
            typeof(ZoomPresetControl),
            new FrameworkPropertyMetadata(
                100.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnCurrentZoomChanged));

    /// <summary>
    /// Identifies the SongLengthBars dependency property.
    /// </summary>
    public static readonly DependencyProperty SongLengthBarsProperty =
        DependencyProperty.Register(
            nameof(SongLengthBars),
            typeof(int),
            typeof(ZoomPresetControl),
            new PropertyMetadata(64));

    /// <summary>
    /// Identifies the BeatsPerBar dependency property.
    /// </summary>
    public static readonly DependencyProperty BeatsPerBarProperty =
        DependencyProperty.Register(
            nameof(BeatsPerBar),
            typeof(int),
            typeof(ZoomPresetControl),
            new PropertyMetadata(4));

    /// <summary>
    /// Gets or sets the current zoom level as a percentage (100 = 100%).
    /// </summary>
    public double CurrentZoom
    {
        get => (double)GetValue(CurrentZoomProperty);
        set => SetValue(CurrentZoomProperty, value);
    }

    /// <summary>
    /// Gets or sets the total length of the song in bars.
    /// </summary>
    public int SongLengthBars
    {
        get => (int)GetValue(SongLengthBarsProperty);
        set => SetValue(SongLengthBarsProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of beats per bar (time signature numerator).
    /// </summary>
    public int BeatsPerBar
    {
        get => (int)GetValue(BeatsPerBarProperty);
        set => SetValue(BeatsPerBarProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the zoom level changes.
    /// </summary>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    #endregion

    #region Fields

    private bool _isUpdatingComboBox;
    private Button? _currentActiveButton;

    // Zoom level constants
    private const double MinZoom = 10.0;
    private const double MaxZoom = 800.0;
    private const double ZoomStep = 25.0;

    #endregion

    #region Constructor

    public ZoomPresetControl()
    {
        InitializeComponent();
        UpdateComboBoxText();
    }

    #endregion

    #region Event Handlers

    private void Zoom1BarButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoomForBars(1);
        SetActiveButton(Zoom1BarButton);
    }

    private void Zoom4BarsButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoomForBars(4);
        SetActiveButton(Zoom4BarsButton);
    }

    private void Zoom8BarsButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoomForBars(8);
        SetActiveButton(Zoom8BarsButton);
    }

    private void Zoom16BarsButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoomForBars(16);
        SetActiveButton(Zoom16BarsButton);
    }

    private void ZoomFullSongButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoomForBars(SongLengthBars);
        SetActiveButton(ZoomFullSongButton);
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(Math.Min(CurrentZoom + ZoomStep, MaxZoom));
        ClearActiveButton();
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(Math.Max(CurrentZoom - ZoomStep, MinZoom));
        ClearActiveButton();
    }

    private void CustomZoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingComboBox || CustomZoomComboBox.SelectedItem == null)
            return;

        if (CustomZoomComboBox.SelectedItem is ComboBoxItem item)
        {
            var text = item.Content?.ToString()?.TrimEnd('%') ?? "100";
            if (double.TryParse(text, out var zoom))
            {
                SetZoom(Math.Clamp(zoom, MinZoom, MaxZoom));
                ClearActiveButton();
            }
        }
    }

    #endregion

    #region Private Methods

    private static void OnCurrentZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomPresetControl control)
        {
            control.UpdateComboBoxText();
            control.RaiseZoomChanged((double)e.OldValue, (double)e.NewValue);
        }
    }

    private void SetZoom(double zoom)
    {
        var clampedZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Math.Abs(CurrentZoom - clampedZoom) > 0.01)
        {
            CurrentZoom = clampedZoom;
        }
    }

    private void SetZoomForBars(int bars)
    {
        // Calculate zoom level to fit the specified number of bars
        // This is a simplified calculation - actual implementation would
        // depend on the view width and pixels per beat
        var baseZoom = 100.0;
        var targetBars = Math.Max(1, bars);
        var fullSongBars = Math.Max(1, SongLengthBars);

        // Zoom scales inversely with the number of bars to show
        // More bars = lower zoom, fewer bars = higher zoom
        var zoom = baseZoom * (fullSongBars / (double)targetBars);
        SetZoom(zoom);
    }

    private void UpdateComboBoxText()
    {
        _isUpdatingComboBox = true;
        try
        {
            CustomZoomComboBox.Text = $"{CurrentZoom:F0}%";
        }
        finally
        {
            _isUpdatingComboBox = false;
        }
    }

    private void SetActiveButton(Button button)
    {
        ClearActiveButton();
        _currentActiveButton = button;

        // Apply active style
        button.Style = (Style)FindResource("ZoomPresetButtonActiveStyle");
    }

    private void ClearActiveButton()
    {
        if (_currentActiveButton != null)
        {
            _currentActiveButton.Style = (Style)FindResource("ZoomPresetButtonStyle");
            _currentActiveButton = null;
        }
    }

    private void RaiseZoomChanged(double oldValue, double newValue)
    {
        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(oldValue, newValue));
    }

    #endregion
}

/// <summary>
/// Event arguments for zoom change events.
/// </summary>
public class ZoomChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous zoom level.
    /// </summary>
    public double OldZoom { get; }

    /// <summary>
    /// Gets the new zoom level.
    /// </summary>
    public double NewZoom { get; }

    /// <summary>
    /// Creates a new instance of ZoomChangedEventArgs.
    /// </summary>
    public ZoomChangedEventArgs(double oldZoom, double newZoom)
    {
        OldZoom = oldZoom;
        NewZoom = newZoom;
    }
}
