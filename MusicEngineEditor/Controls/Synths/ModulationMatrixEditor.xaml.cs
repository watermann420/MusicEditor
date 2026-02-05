// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Visual Modulation Matrix Editor control for creating and editing modulation connections.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

#region Value Converters

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class ModEditorBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts modulation amount to color (green for positive, red for negative).
/// </summary>
public class ModEditorAmountToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush PositiveBrush = new(Color.FromRgb(0x00, 0xFF, 0x88));
    private static readonly SolidColorBrush NegativeBrush = new(Color.FromRgb(0xFF, 0x47, 0x57));
    private static readonly SolidColorBrush NeutralBrush = new(Color.FromRgb(0x80, 0x80, 0x80));

    static ModEditorAmountToColorConverter()
    {
        PositiveBrush.Freeze();
        NegativeBrush.Freeze();
        NeutralBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double amount)
        {
            if (Math.Abs(amount) < 0.5)
                return NeutralBrush;
            return amount > 0 ? PositiveBrush : NegativeBrush;
        }
        return NeutralBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts modulation amount to display text.
/// </summary>
public class ModEditorAmountToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double amount)
        {
            int percent = (int)Math.Round(amount);
            if (percent >= 0)
                return $"+{percent}%";
            return $"{percent}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts source active state to background color.
/// </summary>
public class ModEditorSourceActiveToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0x25, 0x25, 0x25));
    private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(0x1E, 0x1E, 0x1E));

    static ModEditorSourceActiveToColorConverter()
    {
        ActiveBrush.Freeze();
        InactiveBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            return ActiveBrush;
        }
        return InactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion

/// <summary>
/// Represents a visual connection line between a source and destination.
/// </summary>
internal class ConnectionVisual
{
    public ModulationConnectionViewModel Connection { get; set; } = null!;
    public Path Path { get; set; } = null!;
    public Ellipse SourceNode { get; set; } = null!;
    public Ellipse DestinationNode { get; set; } = null!;
    public Point SourcePoint { get; set; }
    public Point DestinationPoint { get; set; }
    public Storyboard? GlowAnimation { get; set; }
}

/// <summary>
/// Visual Modulation Matrix Editor control for creating and editing modulation connections.
/// Features drag-and-drop connection creation, visual connection lines, and animated feedback.
/// </summary>
public partial class ModulationMatrixEditor : UserControl
{
    #region Static Converters

    /// <summary>
    /// Converter for bipolar toggle text.
    /// </summary>
    public static readonly IValueConverter BipolarTextConverter = new BipolarToTextConverter();

    private class BipolarToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isBipolar && isBipolar ? "ON" : "OFF";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion

    #region Constants

    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color PositiveColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color NegativeColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color ConnectionColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color NodeColor = Color.FromRgb(0x00, 0xD9, 0xFF);

    private const double NodeRadius = 6;
    private const double ConnectionStrokeThickness = 2;
    private const double SelectedConnectionStrokeThickness = 3;

    #endregion

    #region Private Fields

    private ModulationMatrixEditorViewModel? _viewModel;
    private readonly Dictionary<ModulationConnectionViewModel, ConnectionVisual> _connectionVisuals = new();

    private bool _isDragging;
    private bool _isCreatingConnection;
    private ModulationSourceItemViewModel? _dragSource;
    private Path? _dragLine;
    private Point _dragStartPoint;
    private Point _dragCurrentPoint;

    private bool _isInitialized;

    #endregion

    #region Constructor

    public ModulationMatrixEditor()
    {
        InitializeComponent();

        _viewModel = new ModulationMatrixEditorViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        if (_viewModel != null)
        {
            _viewModel.ConnectionAdded += OnConnectionAdded;
            _viewModel.ConnectionRemoved += OnConnectionRemoved;
            _viewModel.ConnectionsCleared += OnConnectionsCleared;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // Initial draw of existing connections
        RedrawAllConnections();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;

        if (_viewModel != null)
        {
            _viewModel.ConnectionAdded -= OnConnectionAdded;
            _viewModel.ConnectionRemoved -= OnConnectionRemoved;
            _viewModel.ConnectionsCleared -= OnConnectionsCleared;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        // Stop all animations
        foreach (var visual in _connectionVisuals.Values)
        {
            visual.GlowAnimation?.Stop();
        }
        _connectionVisuals.Clear();
    }

    #endregion

    #region ViewModel Event Handlers

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_isInitialized) return;

        switch (e.PropertyName)
        {
            case nameof(ModulationMatrixEditorViewModel.SelectedConnection):
                UpdateConnectionSelection();
                break;
        }
    }

    private void OnConnectionAdded(object? sender, ModulationConnectionViewModel connection)
    {
        Dispatcher.Invoke(() => AddConnectionVisual(connection));
    }

    private void OnConnectionRemoved(object? sender, ModulationConnectionViewModel connection)
    {
        Dispatcher.Invoke(() => RemoveConnectionVisual(connection));
    }

    private void OnConnectionsCleared(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(ClearAllConnectionVisuals);
    }

    #endregion

    #region Source/Destination Event Handlers

    private void Source_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ModulationSourceItemViewModel source)
        {
            _isDragging = true;
            _isCreatingConnection = true;
            _dragSource = source;
            _dragStartPoint = GetSourceConnectionPoint(source);
            _dragCurrentPoint = e.GetPosition(ConnectionCanvas);

            // Create drag line
            _dragLine = new Path
            {
                Stroke = new SolidColorBrush(AccentColor),
                StrokeThickness = ConnectionStrokeThickness,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            ConnectionCanvas.Children.Add(_dragLine);
            UpdateDragLine();

            border.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Source_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _isCreatingConnection && _dragLine != null)
        {
            _dragCurrentPoint = e.GetPosition(ConnectionCanvas);
            UpdateDragLine();
        }
    }

    private void Source_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border)
        {
            border.ReleaseMouseCapture();
        }

        if (_dragLine != null)
        {
            ConnectionCanvas.Children.Remove(_dragLine);
            _dragLine = null;
        }

        _isDragging = false;
        _isCreatingConnection = false;
        _dragSource = null;
    }

    private void Destination_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCreatingConnection && _dragSource != null)
        {
            if (sender is Border border && border.Tag is ModulationDestinationItemViewModel destination)
            {
                // Create the connection
                _viewModel?.CreateConnectionCommand.Execute(
                    new Tuple<ModulationSourceItemViewModel, ModulationDestinationItemViewModel>(_dragSource, destination));

                // Clean up drag state
                if (_dragLine != null)
                {
                    ConnectionCanvas.Children.Remove(_dragLine);
                    _dragLine = null;
                }
                _isDragging = false;
                _isCreatingConnection = false;
                _dragSource = null;

                e.Handled = true;
            }
        }
        else if (sender is Border border && border.Tag is ModulationDestinationItemViewModel dest)
        {
            // Select the first connection to this destination, if any
            _viewModel?.SelectDestinationCommand.Execute(dest);
        }
    }

    #endregion

    #region Canvas Event Handlers

    private void ConnectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Check if clicking on a connection
        if (e.OriginalSource is Path path && path.Tag is ModulationConnectionViewModel connection)
        {
            _viewModel?.SelectConnectionCommand.Execute(connection);
            e.Handled = true;
        }
        else
        {
            // Deselect
            _viewModel?.DeselectConnectionCommand.Execute(null);
        }
    }

    private void ConnectionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _isCreatingConnection && _dragLine != null)
        {
            _dragCurrentPoint = e.GetPosition(ConnectionCanvas);
            UpdateDragLine();
        }
    }

    private void ConnectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragLine != null)
        {
            ConnectionCanvas.Children.Remove(_dragLine);
            _dragLine = null;
        }

        _isDragging = false;
        _isCreatingConnection = false;
        _dragSource = null;
    }

    private void ConnectionCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawAllConnections();
    }

    #endregion

    #region Slider Event Handler

    private void AmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel?.SelectedConnection == null) return;

        // Update the visual
        if (_connectionVisuals.TryGetValue(_viewModel.SelectedConnection, out var visual))
        {
            UpdateConnectionVisual(visual);
        }
    }

    #endregion

    #region Connection Visual Management

    private void AddConnectionVisual(ModulationConnectionViewModel connection)
    {
        if (_connectionVisuals.ContainsKey(connection)) return;

        var sourcePoint = GetSourceConnectionPoint(connection.Source);
        var destPoint = GetDestinationConnectionPoint(connection.Destination);

        // Create connection path
        var path = new Path
        {
            Stroke = new SolidColorBrush(GetConnectionColor(connection.Amount)),
            StrokeThickness = ConnectionStrokeThickness,
            Cursor = Cursors.Hand,
            Tag = connection,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = ConnectionColor,
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.5
            }
        };
        path.MouseLeftButtonDown += (s, e) =>
        {
            _viewModel?.SelectConnectionCommand.Execute(connection);
            e.Handled = true;
        };

        // Create source node
        var sourceNode = new Ellipse
        {
            Width = NodeRadius * 2,
            Height = NodeRadius * 2,
            Fill = new SolidColorBrush(NodeColor),
            IsHitTestVisible = false
        };

        // Create destination node
        var destNode = new Ellipse
        {
            Width = NodeRadius * 2,
            Height = NodeRadius * 2,
            Fill = new SolidColorBrush(GetConnectionColor(connection.Amount)),
            IsHitTestVisible = false
        };

        var visual = new ConnectionVisual
        {
            Connection = connection,
            Path = path,
            SourceNode = sourceNode,
            DestinationNode = destNode,
            SourcePoint = sourcePoint,
            DestinationPoint = destPoint
        };

        _connectionVisuals[connection] = visual;

        // Add to canvas
        ConnectionCanvas.Children.Add(path);
        ConnectionCanvas.Children.Add(sourceNode);
        ConnectionCanvas.Children.Add(destNode);

        // Update path geometry
        UpdateConnectionVisual(visual);

        // Subscribe to amount changes
        connection.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ModulationConnectionViewModel.Amount))
            {
                Dispatcher.Invoke(() => UpdateConnectionVisual(visual));
            }
        };

        // Start glow animation
        StartGlowAnimation(visual);
    }

    private void RemoveConnectionVisual(ModulationConnectionViewModel connection)
    {
        if (_connectionVisuals.TryGetValue(connection, out var visual))
        {
            visual.GlowAnimation?.Stop();
            ConnectionCanvas.Children.Remove(visual.Path);
            ConnectionCanvas.Children.Remove(visual.SourceNode);
            ConnectionCanvas.Children.Remove(visual.DestinationNode);
            _connectionVisuals.Remove(connection);
        }
    }

    private void ClearAllConnectionVisuals()
    {
        foreach (var visual in _connectionVisuals.Values)
        {
            visual.GlowAnimation?.Stop();
            ConnectionCanvas.Children.Remove(visual.Path);
            ConnectionCanvas.Children.Remove(visual.SourceNode);
            ConnectionCanvas.Children.Remove(visual.DestinationNode);
        }
        _connectionVisuals.Clear();
    }

    private void RedrawAllConnections()
    {
        if (_viewModel == null || !_isInitialized) return;

        foreach (var visual in _connectionVisuals.Values)
        {
            visual.SourcePoint = GetSourceConnectionPoint(visual.Connection.Source);
            visual.DestinationPoint = GetDestinationConnectionPoint(visual.Connection.Destination);
            UpdateConnectionVisual(visual);
        }
    }

    private void UpdateConnectionVisual(ConnectionVisual visual)
    {
        var startPoint = visual.SourcePoint;
        var endPoint = visual.DestinationPoint;

        // Create bezier curve path
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = startPoint };

        // Calculate control points for smooth curve
        double controlOffset = Math.Abs(endPoint.X - startPoint.X) * 0.5;
        var control1 = new Point(startPoint.X + controlOffset, startPoint.Y);
        var control2 = new Point(endPoint.X - controlOffset, endPoint.Y);

        figure.Segments.Add(new BezierSegment(control1, control2, endPoint, true));
        geometry.Figures.Add(figure);

        visual.Path.Data = geometry;

        // Update stroke color based on amount
        var color = GetConnectionColor(visual.Connection.Amount);
        visual.Path.Stroke = new SolidColorBrush(color);
        visual.DestinationNode.Fill = new SolidColorBrush(color);

        // Update stroke thickness for selection
        visual.Path.StrokeThickness = visual.Connection.IsSelected
            ? SelectedConnectionStrokeThickness
            : ConnectionStrokeThickness;

        // Update effect opacity for selection
        if (visual.Path.Effect is System.Windows.Media.Effects.DropShadowEffect effect)
        {
            effect.Opacity = visual.Connection.IsSelected ? 0.8 : 0.5;
            effect.BlurRadius = visual.Connection.IsSelected ? 12 : 8;
        }

        // Position nodes
        Canvas.SetLeft(visual.SourceNode, startPoint.X - NodeRadius);
        Canvas.SetTop(visual.SourceNode, startPoint.Y - NodeRadius);
        Canvas.SetLeft(visual.DestinationNode, endPoint.X - NodeRadius);
        Canvas.SetTop(visual.DestinationNode, endPoint.Y - NodeRadius);
    }

    private void UpdateConnectionSelection()
    {
        foreach (var kvp in _connectionVisuals)
        {
            kvp.Key.IsSelected = kvp.Key == _viewModel?.SelectedConnection;
            UpdateConnectionVisual(kvp.Value);
        }
    }

    private void UpdateDragLine()
    {
        if (_dragLine == null) return;

        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = _dragStartPoint };

        double controlOffset = Math.Abs(_dragCurrentPoint.X - _dragStartPoint.X) * 0.5;
        var control1 = new Point(_dragStartPoint.X + controlOffset, _dragStartPoint.Y);
        var control2 = new Point(_dragCurrentPoint.X - controlOffset, _dragCurrentPoint.Y);

        figure.Segments.Add(new BezierSegment(control1, control2, _dragCurrentPoint, true));
        geometry.Figures.Add(figure);

        _dragLine.Data = geometry;
    }

    #endregion

    #region Animation

    private void StartGlowAnimation(ConnectionVisual visual)
    {
        var storyboard = new Storyboard();

        // Animate the glow effect opacity
        var opacityAnimation = new DoubleAnimation
        {
            From = 0.3,
            To = 0.7,
            Duration = TimeSpan.FromSeconds(1),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(opacityAnimation, visual.Path);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Effect.Opacity"));

        storyboard.Children.Add(opacityAnimation);
        visual.GlowAnimation = storyboard;

        storyboard.Begin();
    }

    #endregion

    #region Helper Methods

    private Point GetSourceConnectionPoint(ModulationSourceItemViewModel source)
    {
        // Calculate point on the right side of the sources panel
        // This is a simplified calculation - in a real implementation you'd want to
        // get the actual position of the source item in the visual tree
        double canvasWidth = ConnectionCanvas.ActualWidth;
        double canvasHeight = ConnectionCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return new Point(0, 0);

        int sourceIndex = GetSourceIndex(source);
        int totalSources = GetTotalSourceCount();

        double x = 20;
        double y = (canvasHeight / (totalSources + 1)) * (sourceIndex + 1);

        return new Point(x, y);
    }

    private Point GetDestinationConnectionPoint(ModulationDestinationItemViewModel destination)
    {
        // Calculate point on the left side of the destinations panel
        double canvasWidth = ConnectionCanvas.ActualWidth;
        double canvasHeight = ConnectionCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return new Point(0, 0);

        int destIndex = GetDestinationIndex(destination);
        int totalDests = GetTotalDestinationCount();

        double x = canvasWidth - 20;
        double y = (canvasHeight / (totalDests + 1)) * (destIndex + 1);

        return new Point(x, y);
    }

    private int GetSourceIndex(ModulationSourceItemViewModel source)
    {
        if (_viewModel == null) return 0;

        int index = 0;
        foreach (var s in _viewModel.LFOSources)
        {
            if (s == source) return index;
            index++;
        }
        foreach (var s in _viewModel.EnvelopeSources)
        {
            if (s == source) return index;
            index++;
        }
        foreach (var s in _viewModel.MIDISources)
        {
            if (s == source) return index;
            index++;
        }
        return 0;
    }

    private int GetDestinationIndex(ModulationDestinationItemViewModel destination)
    {
        if (_viewModel == null) return 0;

        int index = 0;
        foreach (var d in _viewModel.OscillatorDestinations)
        {
            if (d == destination) return index;
            index++;
        }
        foreach (var d in _viewModel.FilterDestinations)
        {
            if (d == destination) return index;
            index++;
        }
        foreach (var d in _viewModel.AmpDestinations)
        {
            if (d == destination) return index;
            index++;
        }
        foreach (var d in _viewModel.EffectsDestinations)
        {
            if (d == destination) return index;
            index++;
        }
        return 0;
    }

    private int GetTotalSourceCount()
    {
        if (_viewModel == null) return 1;
        return _viewModel.LFOSources.Count +
               _viewModel.EnvelopeSources.Count +
               _viewModel.MIDISources.Count;
    }

    private int GetTotalDestinationCount()
    {
        if (_viewModel == null) return 1;
        return _viewModel.OscillatorDestinations.Count +
               _viewModel.FilterDestinations.Count +
               _viewModel.AmpDestinations.Count +
               _viewModel.EffectsDestinations.Count;
    }

    private static Color GetConnectionColor(double amount)
    {
        if (Math.Abs(amount) < 0.5)
            return ConnectionColor;
        return amount > 0 ? PositiveColor : NegativeColor;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the ViewModel for this editor.
    /// </summary>
    public void SetViewModel(ModulationMatrixEditorViewModel viewModel)
    {
        if (_viewModel != null)
        {
            _viewModel.ConnectionAdded -= OnConnectionAdded;
            _viewModel.ConnectionRemoved -= OnConnectionRemoved;
            _viewModel.ConnectionsCleared -= OnConnectionsCleared;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = viewModel;
        DataContext = _viewModel;

        if (_viewModel != null && _isInitialized)
        {
            _viewModel.ConnectionAdded += OnConnectionAdded;
            _viewModel.ConnectionRemoved += OnConnectionRemoved;
            _viewModel.ConnectionsCleared += OnConnectionsCleared;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            RedrawAllConnections();
        }
    }

    /// <summary>
    /// Refreshes all connection visuals.
    /// </summary>
    public void Refresh()
    {
        ClearAllConnectionVisuals();
        if (_viewModel != null)
        {
            foreach (var connection in _viewModel.Connections)
            {
                AddConnectionVisual(connection);
            }
        }
    }

    #endregion
}
