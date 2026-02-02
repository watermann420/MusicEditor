// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Spatial Audio Panel for 3D audio positioning and ambisonic/surround rendering.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core.Spatial;

namespace MusicEngineEditor.Controls.Spatial;

/// <summary>
/// Represents a spatial audio source in the panel's UI.
/// </summary>
public class SpatialSourceItem
{
    private static int _nextId = 1;
    private static readonly Brush[] SourceColors =
    {
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xD9, 0xFF)), // Cyan
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x88)), // Green
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xB8, 0x00)), // Orange
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x47, 0x57)), // Red
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA0, 0x5C, 0xFF)), // Purple
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0xB5)), // Pink
    };

    /// <summary>
    /// Unique identifier for this source.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Display name for the source.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// X position (right is positive).
    /// </summary>
    public float PositionX { get; set; }

    /// <summary>
    /// Y position (front is positive).
    /// </summary>
    public float PositionY { get; set; }

    /// <summary>
    /// Z position (up is positive).
    /// </summary>
    public float PositionZ { get; set; }

    /// <summary>
    /// Display color for this source.
    /// </summary>
    public Brush Color { get; }

    /// <summary>
    /// Formatted position display string.
    /// </summary>
    public string PositionDisplay => $"({PositionX:F1}, {PositionY:F1}, {PositionZ:F1})";

    /// <summary>
    /// Reference to the actual spatial source in the engine (if connected).
    /// </summary>
    public SpatialSource? EngineSource { get; set; }

    /// <summary>
    /// Creates a new spatial source item.
    /// </summary>
    public SpatialSourceItem(string name)
    {
        Id = _nextId++;
        Name = name;
        Color = SourceColors[(Id - 1) % SourceColors.Length];
        PositionX = 0;
        PositionY = 1; // Default to 1 unit in front
        PositionZ = 0;
    }
}

/// <summary>
/// Main container panel for spatial audio features including 3D room visualization,
/// spatial source management, listener position control, and output format configuration.
/// </summary>
public partial class SpatialAudioPanel : UserControl
{
    private readonly ObservableCollection<SpatialSourceItem> _sources = new();
    private SpatialSourceItem? _selectedSource;
    private SpatialSourceItem? _draggingSource;
    private bool _isDragging;
    private Point _dragStartPoint;
    private bool _isInitialized;

    // Engine reference
    private SpatialAudioEngine? _spatialEngine;

    // Visualization elements
    private Ellipse? _listenerMarker;
    private readonly Dictionary<int, Ellipse> _sourceMarkers = new();

    /// <summary>
    /// Event raised when the close button is clicked.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when spatial settings change.
    /// </summary>
    public event EventHandler<SpatialSettingsChangedEventArgs>? SettingsChanged;

    /// <summary>
    /// Event raised when a source position changes.
    /// </summary>
    public event EventHandler<SourcePositionChangedEventArgs>? SourcePositionChanged;

    /// <summary>
    /// Gets or sets the spatial audio engine instance.
    /// </summary>
    public SpatialAudioEngine? SpatialEngine
    {
        get => _spatialEngine;
        set
        {
            _spatialEngine = value;
            SyncWithEngine();
        }
    }

    /// <summary>
    /// Gets the current output format.
    /// </summary>
    public SpatialFormat OutputFormat { get; private set; } = SpatialFormat.AmbisonicsFirstOrder;

    /// <summary>
    /// Gets the current ambisonic order.
    /// </summary>
    public int AmbisonicOrder { get; private set; } = 1;

    /// <summary>
    /// Gets the current reverb send level (0-1).
    /// </summary>
    public float ReverbSendLevel { get; private set; } = 0.3f;

    /// <summary>
    /// Gets the current room size setting.
    /// </summary>
    public RoomSize RoomSize { get; private set; } = RoomSize.Medium;

    /// <summary>
    /// Creates a new Spatial Audio Panel.
    /// </summary>
    public SpatialAudioPanel()
    {
        InitializeComponent();

        SourcesListBox.ItemsSource = _sources;

        Loaded += SpatialAudioPanel_Loaded;
        Unloaded += SpatialAudioPanel_Unloaded;
        SizeChanged += SpatialAudioPanel_SizeChanged;
    }

    private void SpatialAudioPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        SizeChanged -= SpatialAudioPanel_SizeChanged;
    }

    private void SpatialAudioPanel_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        DrawRoomVisualization();
        UpdateListenerMarker();
    }

    private void SpatialAudioPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawRoomVisualization();
        UpdateAllMarkers();
    }

    #region Room Visualization

    private void DrawRoomVisualization()
    {
        RoomVisualizationCanvas.Children.Clear();
        _sourceMarkers.Clear();

        double width = RoomVisualizationCanvas.ActualWidth;
        double height = RoomVisualizationCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        var axisBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

        // Draw grid lines
        int gridSize = 8;
        double cellWidth = width / gridSize;
        double cellHeight = height / gridSize;

        for (int i = 1; i < gridSize; i++)
        {
            // Vertical lines
            var vLine = new Line
            {
                X1 = i * cellWidth,
                Y1 = 0,
                X2 = i * cellWidth,
                Y2 = height,
                Stroke = i == gridSize / 2 ? axisBrush : gridBrush,
                StrokeThickness = i == gridSize / 2 ? 1.5 : 0.5
            };
            RoomVisualizationCanvas.Children.Add(vLine);

            // Horizontal lines
            var hLine = new Line
            {
                X1 = 0,
                Y1 = i * cellHeight,
                X2 = width,
                Y2 = i * cellHeight,
                Stroke = i == gridSize / 2 ? axisBrush : gridBrush,
                StrokeThickness = i == gridSize / 2 ? 1.5 : 0.5
            };
            RoomVisualizationCanvas.Children.Add(hLine);
        }

        // Draw listener position
        UpdateListenerMarker();

        // Draw all source markers
        foreach (var source in _sources)
        {
            DrawSourceMarker(source);
        }
    }

    private void UpdateListenerMarker()
    {
        double width = RoomVisualizationCanvas.ActualWidth;
        double height = RoomVisualizationCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Remove old marker
        if (_listenerMarker != null)
        {
            RoomVisualizationCanvas.Children.Remove(_listenerMarker);
        }

        // Calculate listener position in canvas coordinates
        float listenerX = (float)ListenerXSlider.Value;
        float listenerY = (float)ListenerYSlider.Value;

        double canvasX = (listenerX + 10) / 20 * width;
        double canvasY = (10 - listenerY) / 20 * height; // Invert Y so front is up

        // Create listener marker (triangle pointing forward)
        var listenerPath = new Path
        {
            Data = Geometry.Parse("M 0,-10 L 7,7 L -7,7 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
            StrokeThickness = 2,
            RenderTransform = new RotateTransform(ListenerYawSlider.Value)
        };

        Canvas.SetLeft(listenerPath, canvasX);
        Canvas.SetTop(listenerPath, canvasY);

        // Use ellipse as a simple marker for now
        _listenerMarker = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
            StrokeThickness = 2,
            ToolTip = "Listener"
        };

        Canvas.SetLeft(_listenerMarker, canvasX - 8);
        Canvas.SetTop(_listenerMarker, canvasY - 8);

        RoomVisualizationCanvas.Children.Add(_listenerMarker);
    }

    private void DrawSourceMarker(SpatialSourceItem source)
    {
        double width = RoomVisualizationCanvas.ActualWidth;
        double height = RoomVisualizationCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Remove old marker if exists
        if (_sourceMarkers.TryGetValue(source.Id, out var oldMarker))
        {
            RoomVisualizationCanvas.Children.Remove(oldMarker);
        }

        // Calculate source position in canvas coordinates
        double canvasX = (source.PositionX + 10) / 20 * width;
        double canvasY = (10 - source.PositionY) / 20 * height;

        // Size based on Z position (larger = higher)
        double size = 12 + source.PositionZ * 2;
        size = Math.Clamp(size, 6, 20);

        var marker = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = source.Color,
            Stroke = _selectedSource?.Id == source.Id
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x0D)),
            StrokeThickness = _selectedSource?.Id == source.Id ? 2 : 1,
            ToolTip = $"{source.Name}\n{source.PositionDisplay}",
            Cursor = Cursors.Hand,
            Tag = source
        };

        marker.MouseLeftButtonDown += SourceMarker_MouseLeftButtonDown;

        Canvas.SetLeft(marker, canvasX - size / 2);
        Canvas.SetTop(marker, canvasY - size / 2);

        _sourceMarkers[source.Id] = marker;
        RoomVisualizationCanvas.Children.Add(marker);
    }

    private void UpdateAllMarkers()
    {
        UpdateListenerMarker();
        foreach (var source in _sources)
        {
            DrawSourceMarker(source);
        }
    }

    private void SourceMarker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse marker && marker.Tag is SpatialSourceItem source)
        {
            _selectedSource = source;
            _draggingSource = source;
            _isDragging = true;
            _dragStartPoint = e.GetPosition(RoomVisualizationCanvas);
            marker.CaptureMouse();

            // Update list selection
            SourcesListBox.SelectedItem = source;

            UpdateAllMarkers();
            e.Handled = true;
        }
    }

    #endregion

    #region Canvas Mouse Handling

    private void RoomCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedSource != null && !_isDragging)
        {
            // Move selected source to click position
            Point pos = e.GetPosition(RoomVisualizationCanvas);
            UpdateSourcePositionFromCanvas(_selectedSource, pos);
        }
    }

    private void RoomCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _draggingSource != null)
        {
            Point pos = e.GetPosition(RoomVisualizationCanvas);
            UpdateSourcePositionFromCanvas(_draggingSource, pos);
        }
    }

    private void RoomCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && _draggingSource != null)
        {
            if (_sourceMarkers.TryGetValue(_draggingSource.Id, out var marker))
            {
                marker.ReleaseMouseCapture();
            }
        }

        _isDragging = false;
        _draggingSource = null;
    }

    private void UpdateSourcePositionFromCanvas(SpatialSourceItem source, Point canvasPos)
    {
        double width = RoomVisualizationCanvas.ActualWidth;
        double height = RoomVisualizationCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Convert canvas coordinates to world coordinates
        source.PositionX = (float)(canvasPos.X / width * 20 - 10);
        source.PositionY = (float)(10 - canvasPos.Y / height * 20);

        // Update engine source if connected
        source.EngineSource?.SetPosition(source.PositionX, source.PositionY, source.PositionZ);

        // Raise event
        SourcePositionChanged?.Invoke(this, new SourcePositionChangedEventArgs(source));

        // Update UI
        DrawSourceMarker(source);
        RefreshSourcesList();

        // Update elevation display
        SelectedElevationText.Text = source.PositionZ.ToString("F1");
    }

    private void RefreshSourcesList()
    {
        // Force refresh of the list binding
        var selected = SourcesListBox.SelectedItem;
        SourcesListBox.ItemsSource = null;
        SourcesListBox.ItemsSource = _sources;
        SourcesListBox.SelectedItem = selected;
    }

    #endregion

    #region Source Management

    private void AddSource_Click(object sender, RoutedEventArgs e)
    {
        var source = new SpatialSourceItem($"Source {_sources.Count + 1}");
        _sources.Add(source);

        // Create engine source if available
        if (_spatialEngine != null)
        {
            // Engine source creation would require an audio provider
            // For now, leave EngineSource as null until connected
        }

        DrawSourceMarker(source);
        StatusText.Text = $"Added {source.Name} - drag to position";
    }

    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SpatialSourceItem source)
        {
            // Remove from engine
            if (source.EngineSource != null)
            {
                _spatialEngine?.RemoveSource(source.EngineSource);
            }

            // Remove marker
            if (_sourceMarkers.TryGetValue(source.Id, out var marker))
            {
                RoomVisualizationCanvas.Children.Remove(marker);
                _sourceMarkers.Remove(source.Id);
            }

            _sources.Remove(source);

            if (_selectedSource?.Id == source.Id)
            {
                _selectedSource = null;
            }

            StatusText.Text = $"Removed {source.Name}";
        }
    }

    private void SourcesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedSource = SourcesListBox.SelectedItem as SpatialSourceItem;
        UpdateAllMarkers();

        if (_selectedSource != null)
        {
            SelectedElevationText.Text = _selectedSource.PositionZ.ToString("F1");
            StatusText.Text = $"Selected: {_selectedSource.Name} - drag on canvas or use mouse wheel for elevation";
        }
    }

    #endregion

    #region Listener Controls

    private void ListenerPosition_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return; // Guard during initialization

        ListenerXValue.Text = ListenerXSlider.Value.ToString("F1");
        ListenerYValue.Text = ListenerYSlider.Value.ToString("F1");
        ListenerZValue.Text = ListenerZSlider.Value.ToString("F1");

        // Update engine listener
        _spatialEngine?.Listener.SetPosition(
            (float)ListenerXSlider.Value,
            (float)ListenerYSlider.Value,
            (float)ListenerZSlider.Value);

        UpdateListenerMarker();
        RaiseSettingsChanged();
    }

    private void ListenerOrientation_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return; // Guard during initialization

        ListenerYawValue.Text = $"{ListenerYawSlider.Value:F0}";

        // Update engine listener orientation
        if (_spatialEngine != null)
        {
            _spatialEngine.Listener.Yaw = (float)ListenerYawSlider.Value;
        }

        UpdateListenerMarker();
        RaiseSettingsChanged();
    }

    #endregion

    #region Format and Order Controls

    private void AmbisonicOrder_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return; // Guard during initialization

        if (Order1Radio.IsChecked == true)
        {
            AmbisonicOrder = 1;
            AmbisonicChannelInfo.Text = "4 channels (W, X, Y, Z)";
            OutputFormat = SpatialFormat.AmbisonicsFirstOrder;
        }
        else if (Order2Radio.IsChecked == true)
        {
            AmbisonicOrder = 2;
            AmbisonicChannelInfo.Text = "9 channels (2nd order HOA)";
            OutputFormat = SpatialFormat.AmbisonicsSecondOrder;
        }
        else if (Order3Radio.IsChecked == true)
        {
            AmbisonicOrder = 3;
            AmbisonicChannelInfo.Text = "16 channels (3rd order HOA)";
            OutputFormat = SpatialFormat.AmbisonicsThirdOrder;
        }

        // Update engine output format if ambisonics is selected
        if (_spatialEngine != null && OutputFormatComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag?.ToString() == "Ambisonics")
        {
            _spatialEngine.OutputFormat = OutputFormat;
        }

        RaiseSettingsChanged();
    }

    private void OutputFormat_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return; // Guard during initialization
        if (OutputFormatComboBox.SelectedItem is ComboBoxItem item)
        {
            string? formatTag = item.Tag?.ToString();

            OutputFormat = formatTag switch
            {
                "Binaural" => SpatialFormat.Binaural,
                "Surround51" => SpatialFormat.Surround51,
                "Surround71" => SpatialFormat.Surround71,
                "Ambisonics" => AmbisonicOrder switch
                {
                    1 => SpatialFormat.AmbisonicsFirstOrder,
                    2 => SpatialFormat.AmbisonicsSecondOrder,
                    3 => SpatialFormat.AmbisonicsThirdOrder,
                    _ => SpatialFormat.AmbisonicsFirstOrder
                },
                "Stereo" => SpatialFormat.Stereo,
                _ => SpatialFormat.Stereo
            };

            if (_spatialEngine != null)
            {
                _spatialEngine.OutputFormat = OutputFormat;
            }

            RaiseSettingsChanged();
        }
    }

    #endregion

    #region Reverb Controls

    private void ReverbSend_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return; // Guard during initialization

        ReverbSendLevel = (float)(ReverbSendSlider.Value / 100.0);
        ReverbSendValue.Text = $"{ReverbSendSlider.Value:F0}%";

        RaiseSettingsChanged();
    }

    private void RoomSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return; // Guard during initialization
        if (RoomSizeComboBox.SelectedItem is ComboBoxItem item)
        {
            string? sizeTag = item.Tag?.ToString();

            RoomSize = sizeTag switch
            {
                "Small" => RoomSize.Small,
                "Medium" => RoomSize.Medium,
                "Large" => RoomSize.Large,
                "Hall" => RoomSize.Hall,
                "Outdoor" => RoomSize.Outdoor,
                _ => RoomSize.Medium
            };

            if (_spatialEngine != null)
            {
                _spatialEngine.Listener.RoomSize = RoomSize;
            }

            // Update reverb time display
            UpdateReverbTimeDisplay();
            RaiseSettingsChanged();
        }
    }

    private void UpdateReverbTimeDisplay()
    {
        if (ReverbTimeDisplay == null) return; // Guard during initialization

        if (_spatialEngine != null)
        {
            float rt60 = _spatialEngine.Listener.EstimatedReverbTime;
            ReverbTimeDisplay.Text = $"{rt60:F1}s";
        }
        else
        {
            // Estimate based on room size
            float rt60 = RoomSize switch
            {
                RoomSize.Small => 0.3f,
                RoomSize.Medium => 0.8f,
                RoomSize.Large => 1.2f,
                RoomSize.Hall => 2.5f,
                RoomSize.Outdoor => 0.1f,
                _ => 0.8f
            };
            ReverbTimeDisplay.Text = $"{rt60:F1}s";
        }
    }

    #endregion

    #region Panel Controls

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseSettingsChanged()
    {
        SettingsChanged?.Invoke(this, new SpatialSettingsChangedEventArgs
        {
            OutputFormat = OutputFormat,
            AmbisonicOrder = AmbisonicOrder,
            ReverbSendLevel = ReverbSendLevel,
            RoomSize = RoomSize,
            ListenerX = (float)ListenerXSlider.Value,
            ListenerY = (float)ListenerYSlider.Value,
            ListenerZ = (float)ListenerZSlider.Value,
            ListenerYaw = (float)ListenerYawSlider.Value
        });
    }

    #endregion

    #region Engine Sync

    private void SyncWithEngine()
    {
        if (_spatialEngine == null) return;

        // Sync listener position
        ListenerXSlider.Value = _spatialEngine.Listener.PositionX;
        ListenerYSlider.Value = _spatialEngine.Listener.PositionY;
        ListenerZSlider.Value = _spatialEngine.Listener.PositionZ;
        ListenerYawSlider.Value = _spatialEngine.Listener.Yaw;

        // Sync output format
        OutputFormat = _spatialEngine.OutputFormat;

        // Update room size
        RoomSize = _spatialEngine.Listener.RoomSize;

        // Sync sources
        _sources.Clear();
        foreach (var engineSource in _spatialEngine.Sources)
        {
            var sourceItem = new SpatialSourceItem($"Source {_sources.Count + 1}")
            {
                PositionX = engineSource.PositionX,
                PositionY = engineSource.PositionY,
                PositionZ = engineSource.PositionZ,
                EngineSource = engineSource
            };
            _sources.Add(sourceItem);
        }

        UpdateReverbTimeDisplay();
        DrawRoomVisualization();
    }

    /// <summary>
    /// Adds a spatial source to the panel.
    /// </summary>
    /// <param name="name">Display name for the source.</param>
    /// <param name="engineSource">Optional engine source reference.</param>
    /// <returns>The created source item.</returns>
    public SpatialSourceItem AddSpatialSource(string name, SpatialSource? engineSource = null)
    {
        var sourceItem = new SpatialSourceItem(name)
        {
            EngineSource = engineSource
        };

        if (engineSource != null)
        {
            sourceItem.PositionX = engineSource.PositionX;
            sourceItem.PositionY = engineSource.PositionY;
            sourceItem.PositionZ = engineSource.PositionZ;
        }

        _sources.Add(sourceItem);
        DrawSourceMarker(sourceItem);

        return sourceItem;
    }

    /// <summary>
    /// Gets all spatial source items.
    /// </summary>
    public IReadOnlyCollection<SpatialSourceItem> Sources => _sources;

    #endregion
}

/// <summary>
/// Event arguments for spatial settings changes.
/// </summary>
public class SpatialSettingsChangedEventArgs : EventArgs
{
    /// <summary>
    /// The new output format.
    /// </summary>
    public SpatialFormat OutputFormat { get; init; }

    /// <summary>
    /// The new ambisonic order.
    /// </summary>
    public int AmbisonicOrder { get; init; }

    /// <summary>
    /// The new reverb send level (0-1).
    /// </summary>
    public float ReverbSendLevel { get; init; }

    /// <summary>
    /// The new room size.
    /// </summary>
    public RoomSize RoomSize { get; init; }

    /// <summary>
    /// Listener X position.
    /// </summary>
    public float ListenerX { get; init; }

    /// <summary>
    /// Listener Y position.
    /// </summary>
    public float ListenerY { get; init; }

    /// <summary>
    /// Listener Z position.
    /// </summary>
    public float ListenerZ { get; init; }

    /// <summary>
    /// Listener yaw rotation.
    /// </summary>
    public float ListenerYaw { get; init; }
}

/// <summary>
/// Event arguments for source position changes.
/// </summary>
public class SourcePositionChangedEventArgs : EventArgs
{
    /// <summary>
    /// The source that was moved.
    /// </summary>
    public SpatialSourceItem Source { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public SourcePositionChangedEventArgs(SpatialSourceItem source)
    {
        Source = source;
    }
}
