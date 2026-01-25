using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngine.Core.Warp;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Event arguments for warp marker operations.
/// </summary>
public class WarpMarkerEventArgs : EventArgs
{
    /// <summary>
    /// Gets the warp marker involved in this event.
    /// </summary>
    public WarpMarker Marker { get; }

    /// <summary>
    /// Gets the previous warped beat position (for move events).
    /// </summary>
    public double? PreviousBeatPosition { get; }

    /// <summary>
    /// Creates a new WarpMarkerEventArgs.
    /// </summary>
    /// <param name="marker">The marker involved.</param>
    /// <param name="previousBeatPosition">Optional previous beat position.</param>
    public WarpMarkerEventArgs(WarpMarker marker, double? previousBeatPosition = null)
    {
        Marker = marker;
        PreviousBeatPosition = previousBeatPosition;
    }
}

/// <summary>
/// Warp marker editor UI control that displays waveform with draggable warp markers.
/// Provides Ableton-style elastic audio editing with transient detection and beat grid snapping.
/// </summary>
public partial class WarpMarkerControl : UserControl
{
    #region Private Fields

    private readonly List<WarpMarker> _markers = [];
    private readonly Dictionary<WarpMarker, Canvas> _markerVisuals = new();

    private WarpMarker? _selectedMarker;
    private WarpMarker? _draggedMarker;
    private double _dragStartX;
    private double _dragStartWarpedBeat;
    private bool _isDragging;
    private bool _addMarkerMode;

    private double _gridSize = 0.25; // Default 1/16 note
    private int _sampleRate = 44100;
    private long _totalSamples;

    #endregion

    #region Dependency Properties

    /// <summary>
    /// Identifies the PixelsPerBeat dependency property.
    /// </summary>
    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(WarpMarkerControl),
            new PropertyMetadata(40.0, OnLayoutPropertyChanged));

    /// <summary>
    /// Identifies the Bpm dependency property.
    /// </summary>
    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(double), typeof(WarpMarkerControl),
            new PropertyMetadata(120.0, OnLayoutPropertyChanged));

    /// <summary>
    /// Identifies the TotalBeats dependency property.
    /// </summary>
    public static readonly DependencyProperty TotalBeatsProperty =
        DependencyProperty.Register(nameof(TotalBeats), typeof(double), typeof(WarpMarkerControl),
            new PropertyMetadata(16.0, OnLayoutPropertyChanged));

    /// <summary>
    /// Identifies the WaveformData dependency property.
    /// </summary>
    public static readonly DependencyProperty WaveformDataProperty =
        DependencyProperty.Register(nameof(WaveformData), typeof(float[]), typeof(WarpMarkerControl),
            new PropertyMetadata(null, OnWaveformDataChanged));

    /// <summary>
    /// Identifies the WarpEnabled dependency property.
    /// </summary>
    public static readonly DependencyProperty WarpEnabledProperty =
        DependencyProperty.Register(nameof(WarpEnabled), typeof(bool), typeof(WarpMarkerControl),
            new PropertyMetadata(true, OnWarpEnabledChanged));

    /// <summary>
    /// Identifies the SampleRate dependency property.
    /// </summary>
    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(WarpMarkerControl),
            new PropertyMetadata(44100, OnSampleRateChanged));

    /// <summary>
    /// Gets or sets the pixels per beat for horizontal scaling.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the tempo in BPM.
    /// </summary>
    public double Bpm
    {
        get => (double)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    /// <summary>
    /// Gets or sets the total number of beats to display.
    /// </summary>
    public double TotalBeats
    {
        get => (double)GetValue(TotalBeatsProperty);
        set => SetValue(TotalBeatsProperty, value);
    }

    /// <summary>
    /// Gets or sets the waveform sample data for display.
    /// </summary>
    public float[]? WaveformData
    {
        get => (float[]?)GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    /// <summary>
    /// Gets or sets whether warping is enabled.
    /// </summary>
    public bool WarpEnabled
    {
        get => (bool)GetValue(WarpEnabledProperty);
        set => SetValue(WarpEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the audio sample rate.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a marker is added.
    /// </summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerAdded;

    /// <summary>
    /// Raised when a marker position is changed.
    /// </summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerMoved;

    /// <summary>
    /// Raised when a marker is deleted.
    /// </summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerDeleted;

    /// <summary>
    /// Raised when warp settings change (enabled/disabled, grid size, etc.).
    /// </summary>
    public event EventHandler? WarpSettingsChanged;

    /// <summary>
    /// Raised when auto-warp is requested.
    /// </summary>
    public event EventHandler? AutoWarpRequested;

    /// <summary>
    /// Raised when quantize is requested.
    /// </summary>
    public event EventHandler? QuantizeRequested;

    /// <summary>
    /// Raised when reset is requested.
    /// </summary>
    public event EventHandler? ResetRequested;

    #endregion

    #region Constructor

    public WarpMarkerControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWarpToggle();
        RenderAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAll();
    }

    #endregion

    #region Dependency Property Changed Handlers

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WarpMarkerControl control)
        {
            control.RenderAll();
        }
    }

    private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WarpMarkerControl control)
        {
            control._totalSamples = control.WaveformData?.Length ?? 0;
            control.RenderWaveform();
        }
    }

    private static void OnWarpEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WarpMarkerControl control)
        {
            control.UpdateWarpToggle();
            control.RenderMarkers();
        }
    }

    private static void OnSampleRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WarpMarkerControl control)
        {
            control._sampleRate = control.SampleRate;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the current warp markers.
    /// </summary>
    public IReadOnlyList<WarpMarker> GetMarkers() => _markers.AsReadOnly();

    /// <summary>
    /// Sets the warp markers from an external source.
    /// </summary>
    /// <param name="markers">The markers to set.</param>
    public void SetMarkers(IEnumerable<WarpMarker> markers)
    {
        _markers.Clear();
        _markers.AddRange(markers.OrderBy(m => m.OriginalPositionSamples));
        RenderMarkers();
        UpdateMarkerInfo();
    }

    /// <summary>
    /// Adds a marker at the specified beat position.
    /// </summary>
    /// <param name="beat">The beat position.</param>
    /// <param name="markerType">The type of marker.</param>
    /// <returns>The created marker.</returns>
    public WarpMarker AddMarkerAtBeat(double beat, WarpMarkerType markerType = WarpMarkerType.User)
    {
        long originalSamples = BeatToSamples(beat);
        long warpedSamples = originalSamples; // Initially no warping

        var marker = new WarpMarker(originalSamples, warpedSamples, markerType);
        _markers.Add(marker);
        _markers.Sort((a, b) => a.OriginalPositionSamples.CompareTo(b.OriginalPositionSamples));

        RenderMarkers();
        UpdateMarkerInfo();

        MarkerAdded?.Invoke(this, new WarpMarkerEventArgs(marker));

        return marker;
    }

    /// <summary>
    /// Moves a marker to a new warped beat position.
    /// </summary>
    /// <param name="markerId">The marker ID.</param>
    /// <param name="newWarpedBeat">The new warped beat position.</param>
    public void MoveMarker(Guid markerId, double newWarpedBeat)
    {
        var marker = _markers.FirstOrDefault(m => m.Id == markerId);
        if (marker == null || marker.IsLocked) return;

        // Constrain to neighbors
        newWarpedBeat = ConstrainToNeighbors(marker, newWarpedBeat);

        double previousBeat = SamplesToBeat(marker.WarpedPositionSamples);
        marker.WarpedPositionSamples = BeatToSamples(newWarpedBeat);
        marker.Touch();

        RenderMarkers();

        MarkerMoved?.Invoke(this, new WarpMarkerEventArgs(marker, previousBeat));
    }

    /// <summary>
    /// Removes a marker by ID.
    /// </summary>
    /// <param name="markerId">The marker ID to remove.</param>
    public void RemoveMarker(Guid markerId)
    {
        var marker = _markers.FirstOrDefault(m => m.Id == markerId);
        if (marker == null) return;

        // Cannot remove anchor markers
        if (marker.MarkerType == WarpMarkerType.Start || marker.MarkerType == WarpMarkerType.End)
            return;

        _markers.Remove(marker);
        RenderMarkers();
        UpdateMarkerInfo();

        MarkerDeleted?.Invoke(this, new WarpMarkerEventArgs(marker));
    }

    /// <summary>
    /// Removes the currently selected marker.
    /// </summary>
    public void RemoveSelectedMarker()
    {
        if (_selectedMarker != null)
        {
            RemoveMarker(_selectedMarker.Id);
            _selectedMarker = null;
        }
    }

    /// <summary>
    /// Clears all markers except anchor points.
    /// </summary>
    public void ClearMarkers()
    {
        var anchors = _markers.Where(m =>
            m.MarkerType == WarpMarkerType.Start ||
            m.MarkerType == WarpMarkerType.End).ToList();

        _markers.Clear();
        _markers.AddRange(anchors);

        _selectedMarker = null;
        RenderMarkers();
        UpdateMarkerInfo();
    }

    /// <summary>
    /// Selects a marker by ID.
    /// </summary>
    /// <param name="markerId">The marker ID to select.</param>
    public void SelectMarker(Guid markerId)
    {
        _selectedMarker = _markers.FirstOrDefault(m => m.Id == markerId);
        foreach (var marker in _markers)
        {
            marker.IsSelected = marker.Id == markerId;
        }
        RenderMarkers();
    }

    /// <summary>
    /// Refreshes the display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        if (!IsLoaded) return;

        RenderGrid();
        RenderWaveform();
        RenderMarkers();
        RenderBeatRuler();
    }

    private void RenderGrid()
    {
        GridCanvas.Children.Clear();

        double canvasWidth = WarpCanvas.ActualWidth;
        double canvasHeight = WarpCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        var beatLineBrush = FindResource("GridBeatBrush") as Brush ?? Brushes.Gray;
        var barLineBrush = FindResource("GridBarBrush") as Brush ?? Brushes.LightGray;

        // Draw grid lines at beat intervals
        for (double beat = 0; beat <= TotalBeats; beat += _gridSize)
        {
            double x = beat * PixelsPerBeat;
            bool isBarLine = Math.Abs(beat % 4) < 0.001;
            bool isBeat = Math.Abs(beat % 1) < 0.001;

            if (x > canvasWidth) break;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = canvasHeight,
                Stroke = isBarLine ? barLineBrush : beatLineBrush,
                StrokeThickness = isBarLine ? 1.5 : (isBeat ? 1.0 : 0.5),
                Opacity = isBarLine ? 0.8 : (isBeat ? 0.5 : 0.3)
            };

            GridCanvas.Children.Add(line);
        }
    }

    private void RenderWaveform()
    {
        if (WaveformData == null || WaveformData.Length == 0) return;

        double canvasWidth = WarpCanvas.ActualWidth;
        double canvasHeight = WarpCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double centerY = canvasHeight / 2;
            double samplesPerPixel = WaveformData.Length / canvasWidth;

            bool started = false;
            for (int x = 0; x < (int)canvasWidth; x++)
            {
                int sampleIndex = (int)(x * samplesPerPixel);
                if (sampleIndex >= WaveformData.Length) break;

                // Get warped sample position
                long originalPos = GetOriginalPositionForWarpedX(x);
                int warpedSampleIndex = (int)Math.Clamp(originalPos, 0, WaveformData.Length - 1);

                float sample = WaveformData[warpedSampleIndex];
                double y = centerY - (sample * centerY * 0.9);

                if (!started)
                {
                    ctx.BeginFigure(new Point(x, y), false, false);
                    started = true;
                }
                else
                {
                    ctx.LineTo(new Point(x, y), true, false);
                }
            }
        }

        geometry.Freeze();
        WaveformPath.Data = geometry;
    }

    private void RenderMarkers()
    {
        ClearMarkerVisuals();

        if (!WarpEnabled) return;

        double canvasHeight = WarpCanvas.ActualHeight;
        if (canvasHeight <= 0) return;

        foreach (var marker in _markers)
        {
            double warpedBeat = SamplesToBeat(marker.WarpedPositionSamples);
            double x = warpedBeat * PixelsPerBeat;

            var markerVisual = CreateMarkerVisual(marker, canvasHeight);
            Canvas.SetLeft(markerVisual, x - 6); // Center the 12px wide marker
            Canvas.SetTop(markerVisual, 0);

            WarpCanvas.Children.Add(markerVisual);
            _markerVisuals[marker] = markerVisual;
        }

        UpdateMarkerInfo();
    }

    private void ClearMarkerVisuals()
    {
        foreach (var visual in _markerVisuals.Values)
        {
            WarpCanvas.Children.Remove(visual);
        }
        _markerVisuals.Clear();
    }

    private Canvas CreateMarkerVisual(WarpMarker marker, double height)
    {
        var group = new Canvas
        {
            Width = 12,
            Height = height,
            Tag = marker,
            Cursor = marker.IsLocked ? Cursors.Arrow : Cursors.SizeWE
        };

        // Get marker color
        var brush = GetMarkerBrush(marker);

        // Vertical line
        var line = new Shapes.Line
        {
            X1 = 6,
            Y1 = 12,
            X2 = 6,
            Y2 = height,
            Stroke = brush,
            StrokeThickness = marker.MarkerType == WarpMarkerType.Start ||
                             marker.MarkerType == WarpMarkerType.End ? 2 : 1,
            StrokeDashArray = marker.IsLocked ? new DoubleCollection { 4, 2 } : null
        };
        group.Children.Add(line);

        // Triangle handle at top
        var handle = new Shapes.Polygon
        {
            Points = new PointCollection
            {
                new Point(0, 0),
                new Point(12, 0),
                new Point(6, 10)
            },
            Fill = brush,
            Cursor = marker.IsLocked ? Cursors.Arrow : Cursors.SizeWE
        };
        group.Children.Add(handle);

        // Selection highlight
        if (marker.IsSelected)
        {
            var selectedBrush = FindResource("MarkerSelectedBrush") as Brush ?? Brushes.White;
            var highlight = new Shapes.Rectangle
            {
                Width = 14,
                Height = 12,
                Stroke = selectedBrush,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(highlight, -1);
            Canvas.SetTop(highlight, -1);
            group.Children.Add(highlight);
        }

        // Attach mouse handlers for the handle area
        handle.MouseLeftButtonDown += MarkerHandle_MouseLeftButtonDown;
        handle.MouseRightButtonDown += MarkerHandle_MouseRightButtonDown;

        return group;
    }

    private Brush GetMarkerBrush(WarpMarker marker)
    {
        if (marker.IsLocked)
            return FindResource("MarkerLockedBrush") as Brush ?? Brushes.Gray;

        return marker.MarkerType switch
        {
            WarpMarkerType.User => FindResource("MarkerUserBrush") as Brush ?? Brushes.Orange,
            WarpMarkerType.Transient => FindResource("MarkerTransientBrush") as Brush ?? Brushes.Blue,
            WarpMarkerType.Beat => FindResource("MarkerBeatBrush") as Brush ?? Brushes.Green,
            WarpMarkerType.Start or WarpMarkerType.End => FindResource("MarkerAnchorBrush") as Brush ?? Brushes.Red,
            _ => Brushes.Orange
        };
    }

    private void RenderBeatRuler()
    {
        BeatRuler.Children.Clear();

        double canvasWidth = BeatRuler.ActualWidth;
        if (canvasWidth <= 0) return;

        var textBrush = FindResource("RulerTextBrush") as Brush ?? Brushes.Gray;

        // Draw beat numbers
        for (int beat = 0; beat <= (int)TotalBeats; beat++)
        {
            double x = beat * PixelsPerBeat;
            if (x > canvasWidth) break;

            // Bar number (every 4 beats)
            if (beat % 4 == 0)
            {
                int bar = beat / 4 + 1;
                var label = new TextBlock
                {
                    Text = bar.ToString(),
                    FontSize = 10,
                    Foreground = textBrush
                };
                Canvas.SetLeft(label, x + 2);
                Canvas.SetTop(label, 4);
                BeatRuler.Children.Add(label);

                // Tick mark
                var tick = new Shapes.Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = 6,
                    Stroke = textBrush,
                    StrokeThickness = 1
                };
                BeatRuler.Children.Add(tick);
            }
            else
            {
                // Small tick for beats
                var tick = new Shapes.Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = 3,
                    Stroke = textBrush,
                    StrokeThickness = 0.5,
                    Opacity = 0.5
                };
                BeatRuler.Children.Add(tick);
            }
        }
    }

    #endregion

    #region Mouse Event Handlers

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(WarpCanvas);

        // Check if clicking on a marker
        var hitMarker = HitTestMarker(pos);
        if (hitMarker != null)
        {
            // Select and potentially start dragging
            SelectMarker(hitMarker.Id);

            if (!hitMarker.IsLocked)
            {
                _draggedMarker = hitMarker;
                _dragStartX = pos.X;
                _dragStartWarpedBeat = SamplesToBeat(hitMarker.WarpedPositionSamples);
                _isDragging = true;
                WarpCanvas.CaptureMouse();
            }
        }
        else if (_addMarkerMode && WarpEnabled)
        {
            // Add new marker at click position
            double beat = pos.X / PixelsPerBeat;
            AddMarkerAtBeat(beat, WarpMarkerType.User);
            _addMarkerMode = false;
            WarpCanvas.Cursor = Cursors.Arrow;
        }
        else
        {
            // Deselect
            _selectedMarker = null;
            foreach (var marker in _markers)
            {
                marker.IsSelected = false;
            }
            RenderMarkers();
        }

        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(WarpCanvas);

        if (_isDragging && _draggedMarker != null)
        {
            double deltaX = pos.X - _dragStartX;
            double deltaBeat = deltaX / PixelsPerBeat;
            double newBeat = _dragStartWarpedBeat + deltaBeat;

            // Snap to grid if close enough
            double snappedBeat = SnapToGrid(newBeat);
            if (Math.Abs(snappedBeat - newBeat) < 0.1)
            {
                newBeat = snappedBeat;
            }

            MoveMarker(_draggedMarker.Id, newBeat);
        }
        else if (_addMarkerMode)
        {
            WarpCanvas.Cursor = Cursors.Cross;
        }
        else
        {
            // Update cursor based on marker hit test
            var hitMarker = HitTestMarker(pos);
            WarpCanvas.Cursor = hitMarker != null && !hitMarker.IsLocked ? Cursors.SizeWE : Cursors.Arrow;
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _draggedMarker = null;
            WarpCanvas.ReleaseMouseCapture();
        }
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(WarpCanvas);
        var hitMarker = HitTestMarker(pos);

        if (hitMarker != null)
        {
            SelectMarker(hitMarker.Id);
            ShowMarkerContextMenu(hitMarker, e);
        }

        e.Handled = true;
    }

    private void MarkerHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Shapes.Polygon handle && handle.Parent is Canvas markerCanvas)
        {
            if (markerCanvas.Tag is WarpMarker marker)
            {
                SelectMarker(marker.Id);

                if (!marker.IsLocked)
                {
                    var pos = e.GetPosition(WarpCanvas);
                    _draggedMarker = marker;
                    _dragStartX = pos.X;
                    _dragStartWarpedBeat = SamplesToBeat(marker.WarpedPositionSamples);
                    _isDragging = true;
                    WarpCanvas.CaptureMouse();
                }

                e.Handled = true;
            }
        }
    }

    private void MarkerHandle_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Shapes.Polygon handle && handle.Parent is Canvas markerCanvas)
        {
            if (markerCanvas.Tag is WarpMarker marker)
            {
                SelectMarker(marker.Id);
                ShowMarkerContextMenu(marker, e);
                e.Handled = true;
            }
        }
    }

    #endregion

    #region Context Menu

    private void ShowMarkerContextMenu(WarpMarker marker, MouseButtonEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // Delete
        var deleteItem = new MenuItem
        {
            Header = "Delete Marker",
            IsEnabled = marker.MarkerType != WarpMarkerType.Start &&
                       marker.MarkerType != WarpMarkerType.End
        };
        deleteItem.Click += (s, args) => RemoveMarker(marker.Id);
        menu.Items.Add(deleteItem);

        menu.Items.Add(new Separator());

        // Lock/Unlock
        var lockItem = new MenuItem
        {
            Header = marker.IsLocked ? "Unlock" : "Lock"
        };
        lockItem.Click += (s, args) =>
        {
            marker.IsLocked = !marker.IsLocked;
            RenderMarkers();
        };
        menu.Items.Add(lockItem);

        // Snap to Grid
        var snapItem = new MenuItem
        {
            Header = "Snap to Grid",
            IsEnabled = !marker.IsLocked
        };
        snapItem.Click += (s, args) =>
        {
            double currentBeat = SamplesToBeat(marker.WarpedPositionSamples);
            double snappedBeat = SnapToGrid(currentBeat);
            MoveMarker(marker.Id, snappedBeat);
        };
        menu.Items.Add(snapItem);

        menu.Items.Add(new Separator());

        // Properties (placeholder)
        var propsItem = new MenuItem { Header = "Properties..." };
        propsItem.Click += (s, args) =>
        {
            MessageBox.Show(
                $"Marker ID: {marker.Id}\n" +
                $"Type: {marker.MarkerType}\n" +
                $"Original: {marker.OriginalPositionSamples} samples\n" +
                $"Warped: {marker.WarpedPositionSamples} samples\n" +
                $"Locked: {marker.IsLocked}",
                "Marker Properties",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };
        menu.Items.Add(propsItem);

        menu.IsOpen = true;
    }

    #endregion

    #region Toolbar Event Handlers

    private void WarpEnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        WarpEnabled = WarpEnabledToggle.IsChecked == true;
        WarpSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddMarkerBtn_Click(object sender, RoutedEventArgs e)
    {
        _addMarkerMode = true;
        WarpCanvas.Cursor = Cursors.Cross;
    }

    private void AutoWarpBtn_Click(object sender, RoutedEventArgs e)
    {
        AutoWarpRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuantizeBtn_Click(object sender, RoutedEventArgs e)
    {
        QuantizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void GridSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSizeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagValue)
        {
            if (double.TryParse(tagValue, out double gridSize))
            {
                _gridSize = gridSize;
                RenderGrid();
                WarpSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    #endregion

    #region Helper Methods

    private WarpMarker? HitTestMarker(Point pos)
    {
        double tolerance = 8; // Pixels

        foreach (var marker in _markers)
        {
            double warpedBeat = SamplesToBeat(marker.WarpedPositionSamples);
            double markerX = warpedBeat * PixelsPerBeat;

            if (Math.Abs(pos.X - markerX) <= tolerance)
            {
                return marker;
            }
        }

        return null;
    }

    private double ConstrainToNeighbors(WarpMarker marker, double newBeat)
    {
        int index = _markers.IndexOf(marker);
        if (index < 0) return newBeat;

        // Get neighboring markers
        WarpMarker? prev = index > 0 ? _markers[index - 1] : null;
        WarpMarker? next = index < _markers.Count - 1 ? _markers[index + 1] : null;

        double minBeat = prev != null ? SamplesToBeat(prev.WarpedPositionSamples) + 0.01 : 0;
        double maxBeat = next != null ? SamplesToBeat(next.WarpedPositionSamples) - 0.01 : TotalBeats;

        return Math.Clamp(newBeat, minBeat, maxBeat);
    }

    private double SnapToGrid(double beat)
    {
        return Math.Round(beat / _gridSize) * _gridSize;
    }

    private double SamplesToBeat(long samples)
    {
        double seconds = (double)samples / _sampleRate;
        return seconds * Bpm / 60.0;
    }

    private long BeatToSamples(double beat)
    {
        double seconds = beat * 60.0 / Bpm;
        return (long)(seconds * _sampleRate);
    }

    private long GetOriginalPositionForWarpedX(double x)
    {
        double warpedBeat = x / PixelsPerBeat;
        long warpedSamples = BeatToSamples(warpedBeat);

        // Find the region this position falls into
        for (int i = 0; i < _markers.Count - 1; i++)
        {
            var start = _markers[i];
            var end = _markers[i + 1];

            if (warpedSamples >= start.WarpedPositionSamples &&
                warpedSamples < end.WarpedPositionSamples)
            {
                // Interpolate within this region
                long warpedDelta = end.WarpedPositionSamples - start.WarpedPositionSamples;
                long originalDelta = end.OriginalPositionSamples - start.OriginalPositionSamples;

                if (warpedDelta == 0) return start.OriginalPositionSamples;

                double progress = (double)(warpedSamples - start.WarpedPositionSamples) / warpedDelta;
                return start.OriginalPositionSamples + (long)(progress * originalDelta);
            }
        }

        // If no markers or outside range, return linear mapping
        return warpedSamples;
    }

    private void UpdateWarpToggle()
    {
        WarpEnabledToggle.IsChecked = WarpEnabled;
    }

    private void UpdateMarkerInfo()
    {
        MarkerInfoText.Text = $"{_markers.Count} marker{(_markers.Count != 1 ? "s" : "")}";
    }

    #endregion
}
