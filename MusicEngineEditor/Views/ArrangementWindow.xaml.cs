// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Floating arrangement window with timeline and track controls.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngineEditor.Controls.Arrangement;

namespace MusicEngineEditor.Views;

/// <summary>
/// Floating window for editing the arrangement timeline.
/// Features custom title bar with drag support, grid controls, and overview minimap.
/// </summary>
public partial class ArrangementWindow : Window
{
    #region Private Fields

    private double _zoomLevel = 1.0;
    private double _gridResolution = 4.0; // Default: 1 bar (4 beats)
    private bool _snapEnabled = true;
    private bool _keepRunning = true;

    #endregion

    #region Events

    /// <summary>
    /// Raised when the snap toggle state changes.
    /// </summary>
#pragma warning disable CS0067
    public event EventHandler<bool>? SnapToggled;
#pragma warning restore CS0067

    /// <summary>
    /// Raised when the grid resolution changes.
    /// </summary>
    public event EventHandler<double>? GridResolutionChanged;

    /// <summary>
    /// Raised when the zoom level changes.
    /// </summary>
    public event EventHandler<double>? ZoomLevelChanged;

    /// <summary>
    /// Raised when add track is requested.
    /// </summary>
    public event EventHandler? AddTrackRequested;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current zoom level.
    /// </summary>
    public double ZoomLevel => _zoomLevel;

    /// <summary>
    /// Gets the current grid resolution in beats.
    /// </summary>
    public double GridResolution => _gridResolution;

    /// <summary>
    /// Gets whether snap is enabled.
    /// </summary>
    public bool IsSnapEnabled => _snapEnabled;

    #endregion

    #region Constructor

    public ArrangementWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Window Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set owner to MainWindow if available
        if (Owner == null && Application.Current.MainWindow != this)
        {
            Owner = Application.Current.MainWindow;
        }

        UpdateMinimapViewport();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_keepRunning)
        {
            // Hide instead of close to allow re-showing
            e.Cancel = true;
            Hide();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMinimapViewport();
    }

    #endregion

    #region Title Bar Event Handlers

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to toggle maximize
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    #endregion

    #region Toolbar Event Handlers

    private void GridCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (GridCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
        {
            if (double.TryParse(tagStr, out double resolution))
            {
                _gridResolution = resolution;
                GridResolutionChanged?.Invoke(this, _gridResolution);
            }
        }
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || ZoomText == null) return;

        _zoomLevel = ZoomSlider.Value;
        ZoomText.Text = $"{(int)(_zoomLevel * 100)}%";
        ZoomLevelChanged?.Invoke(this, _zoomLevel);
        UpdateMinimapViewport();
    }

    private void AddTrackButton_Click(object sender, RoutedEventArgs e)
    {
        AddTrackRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the snap state.
    /// </summary>
    /// <param name="enabled">Whether snap should be enabled.</param>
    public void SetSnapEnabled(bool enabled)
    {
        _snapEnabled = enabled;
        SnapToggle.IsChecked = enabled;
    }

    /// <summary>
    /// Sets the grid resolution.
    /// </summary>
    /// <param name="beatsPerGrid">The grid resolution in beats.</param>
    public void SetGridResolution(double beatsPerGrid)
    {
        _gridResolution = beatsPerGrid;

        // Find and select matching combo item
        for (int i = 0; i < GridCombo.Items.Count; i++)
        {
            if (GridCombo.Items[i] is ComboBoxItem item && item.Tag is string tagStr)
            {
                if (double.TryParse(tagStr, out double resolution) && Math.Abs(resolution - beatsPerGrid) < 0.001)
                {
                    GridCombo.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Sets the zoom level.
    /// </summary>
    /// <param name="zoom">The zoom level (1.0 = 100%).</param>
    public void SetZoomLevel(double zoom)
    {
        zoom = Math.Clamp(zoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
        _zoomLevel = zoom;
        ZoomSlider.Value = zoom;
    }

    /// <summary>
    /// Updates the track count display.
    /// </summary>
    /// <param name="trackCount">The number of tracks.</param>
    public void SetTrackCount(int trackCount)
    {
        TrackCountText.Text = trackCount == 1 ? "- 1 track" : $"- {trackCount} tracks";
    }

    /// <summary>
    /// Updates the status text.
    /// </summary>
    /// <param name="status">The status message to display.</param>
    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    /// <summary>
    /// Shows the window.
    /// </summary>
    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    /// <summary>
    /// Forces the window to close permanently.
    /// </summary>
    public void ForceClose()
    {
        _keepRunning = false;
        Close();
    }

    /// <summary>
    /// Updates the minimap viewport indicator position and size.
    /// </summary>
    /// <param name="scrollPosition">The current scroll position (0.0 to 1.0).</param>
    /// <param name="viewportSize">The visible viewport size as fraction of total (0.0 to 1.0).</param>
    public void UpdateMinimapViewport(double scrollPosition = 0, double viewportSize = 0.2)
    {
        if (MinimapCanvas == null || ViewportIndicator == null || MinimapContent == null)
            return;

        double totalWidth = MinimapContent.ActualWidth - 2; // Account for border
        if (totalWidth <= 0) return;

        // Calculate viewport indicator size based on zoom
        double indicatorWidth = Math.Max(20, totalWidth * viewportSize / _zoomLevel);
        indicatorWidth = Math.Min(indicatorWidth, totalWidth);

        // Calculate position
        double maxLeft = totalWidth - indicatorWidth;
        double left = scrollPosition * maxLeft;

        ViewportIndicator.Width = indicatorWidth;
        Canvas.SetLeft(ViewportIndicator, left);
    }

    #endregion

    #region Private Methods

    // Additional private helper methods can be added here as needed

    #endregion
}
