// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Loop Finder Panel control for automatic loop detection and editing.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngineEditor.ViewModels.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Loop Finder Panel for automatic loop detection with waveform visualization,
/// loop markers, A/B comparison, and export functionality.
/// </summary>
public partial class LoopFinderPanel : UserControl
{
    #region Constants

    private const double MinMarkerDragDistance = 5.0;
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color SuccessColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color WarningColor = Color.FromRgb(0xFF, 0xB8, 0x00);
    private static readonly Color ErrorColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color WaveformColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color LoopRegionColor = Color.FromArgb(0x33, 0x00, 0xD9, 0xFF);

    #endregion

    #region Private Fields

    private readonly LoopFinderViewModel _viewModel;
    private bool _isInitialized;
    private bool _isDraggingStartMarker;
    private bool _isDraggingEndMarker;
    private bool _isDraggingSelection;
    private Point _dragStartPoint;
    private double _dragStartPosition;

    // Visual elements
    private Shapes.Polyline? _waveformLine;
    private Shapes.Rectangle? _loopRegionRect;
    private Shapes.Line? _startMarkerLine;
    private Shapes.Line? _endMarkerLine;
    private Shapes.Polygon? _startMarkerHandle;
    private Shapes.Polygon? _endMarkerHandle;

    // Loop candidate highlighting
    private readonly List<Shapes.Rectangle> _candidateHighlights = new();

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(LoopFinderPanel),
            new PropertyMetadata(44100, OnSampleRateChanged));

    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(float), typeof(LoopFinderPanel),
            new PropertyMetadata(120f, OnBpmChanged));

    public static readonly DependencyProperty BeatsPerBarProperty =
        DependencyProperty.Register(nameof(BeatsPerBar), typeof(int), typeof(LoopFinderPanel),
            new PropertyMetadata(4, OnBeatsPerBarChanged));

    public static readonly DependencyProperty AudioDataProperty =
        DependencyProperty.Register(nameof(AudioData), typeof(float[]), typeof(LoopFinderPanel),
            new PropertyMetadata(null, OnAudioDataChanged));

    public static readonly DependencyProperty AudioFileNameProperty =
        DependencyProperty.Register(nameof(AudioFileName), typeof(string), typeof(LoopFinderPanel),
            new PropertyMetadata(string.Empty, OnAudioFileNameChanged));

    /// <summary>
    /// Gets or sets the sample rate for audio analysis.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the BPM for bar/beat calculations.
    /// </summary>
    public float Bpm
    {
        get => (float)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    /// <summary>
    /// Gets or sets the beats per bar.
    /// </summary>
    public int BeatsPerBar
    {
        get => (int)GetValue(BeatsPerBarProperty);
        set => SetValue(BeatsPerBarProperty, value);
    }

    /// <summary>
    /// Gets or sets the audio sample data (left channel).
    /// </summary>
    public float[]? AudioData
    {
        get => (float[]?)GetValue(AudioDataProperty);
        set => SetValue(AudioDataProperty, value);
    }

    /// <summary>
    /// Gets or sets the audio file name.
    /// </summary>
    public string AudioFileName
    {
        get => (string)GetValue(AudioFileNameProperty);
        set => SetValue(AudioFileNameProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a loop preview is requested.
    /// </summary>
    public event EventHandler<LoopPreviewEventArgs>? LoopPreviewRequested;

    /// <summary>
    /// Raised when a loop export is requested.
    /// </summary>
    public event EventHandler<LoopExportEventArgs>? LoopExportRequested;

    /// <summary>
    /// Raised when loop markers have changed.
    /// </summary>
    public event EventHandler<LoopMarkersChangedEventArgs>? LoopMarkersChanged;

    #endregion

    #region Constructor

    public LoopFinderPanel()
    {
        InitializeComponent();

        _viewModel = new LoopFinderViewModel();
        DataContext = _viewModel;

        // Subscribe to ViewModel events
        _viewModel.LoopPreviewRequested += OnViewModelLoopPreviewRequested;
        _viewModel.LoopExportRequested += OnViewModelLoopExportRequested;
        _viewModel.LoopMarkersChanged += OnViewModelLoopMarkersChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Bind list to ViewModel
        LoopCandidatesListBox.ItemsSource = _viewModel.LoopCandidates;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        InitializeVisualElements();
        UpdateDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    #endregion

    #region Initialization

    private void InitializeVisualElements()
    {
        // Initialize waveform polyline
        _waveformLine = new Shapes.Polyline
        {
            Stroke = new SolidColorBrush(WaveformColor),
            StrokeThickness = 1,
            StrokeLineJoin = PenLineJoin.Round
        };
        WaveformCanvas.Children.Add(_waveformLine);

        // Initialize loop region rectangle
        _loopRegionRect = new Shapes.Rectangle
        {
            Fill = new SolidColorBrush(LoopRegionColor),
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 1,
            Visibility = Visibility.Collapsed
        };
        LoopRegionCanvas.Children.Add(_loopRegionRect);

        // Initialize start marker
        _startMarkerLine = new Shapes.Line
        {
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 2,
            Visibility = Visibility.Collapsed,
            Cursor = Cursors.SizeWE
        };
        MarkersCanvas.Children.Add(_startMarkerLine);

        _startMarkerHandle = new Shapes.Polygon
        {
            Fill = new SolidColorBrush(AccentColor),
            Points = new PointCollection { new Point(0, 0), new Point(8, 0), new Point(8, 12), new Point(0, 12) },
            Visibility = Visibility.Collapsed,
            Cursor = Cursors.SizeWE
        };
        _startMarkerHandle.MouseLeftButtonDown += StartMarkerHandle_MouseLeftButtonDown;
        MarkersCanvas.Children.Add(_startMarkerHandle);

        // Initialize end marker
        _endMarkerLine = new Shapes.Line
        {
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 2,
            Visibility = Visibility.Collapsed,
            Cursor = Cursors.SizeWE
        };
        MarkersCanvas.Children.Add(_endMarkerLine);

        _endMarkerHandle = new Shapes.Polygon
        {
            Fill = new SolidColorBrush(AccentColor),
            Points = new PointCollection { new Point(0, 0), new Point(8, 0), new Point(8, 12), new Point(0, 12) },
            Visibility = Visibility.Collapsed,
            Cursor = Cursors.SizeWE
        };
        _endMarkerHandle.MouseLeftButtonDown += EndMarkerHandle_MouseLeftButtonDown;
        MarkersCanvas.Children.Add(_endMarkerHandle);
    }

    #endregion

    #region Display Updates

    private void UpdateDisplay()
    {
        if (!_isInitialized) return;

        DrawWaveform();
        DrawTimeScale();
        DrawLoopRegion();
        UpdateMarkers();
        UpdateLoopInfo();
        UpdateCrossfadePreview();
        UpdateEmptyState();
    }

    private void DrawWaveform()
    {
        if (_waveformLine == null) return;

        var waveformData = _viewModel.WaveformData;
        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;

        if (waveformData == null || waveformData.Length == 0 || width <= 0 || height <= 0)
        {
            _waveformLine.Points.Clear();
            return;
        }

        var points = new PointCollection();
        double centerY = height / 2;
        double samplesPerPixel = waveformData.Length / width;

        for (int x = 0; x < (int)width; x++)
        {
            int sampleIndex = (int)(x * samplesPerPixel);
            if (sampleIndex >= waveformData.Length) break;

            float magnitude = waveformData[sampleIndex];
            double y = centerY - magnitude * centerY * 0.9;
            points.Add(new Point(x, y));
        }

        // Add mirrored bottom half
        for (int x = (int)width - 1; x >= 0; x--)
        {
            int sampleIndex = (int)(x * samplesPerPixel);
            if (sampleIndex >= waveformData.Length) sampleIndex = waveformData.Length - 1;

            float magnitude = waveformData[sampleIndex];
            double y = centerY + magnitude * centerY * 0.9;
            points.Add(new Point(x, y));
        }

        _waveformLine.Points = points;
        _waveformLine.Fill = new SolidColorBrush(Color.FromArgb(40, WaveformColor.R, WaveformColor.G, WaveformColor.B));
    }

    private void DrawTimeScale()
    {
        TimeScaleCanvas.Children.Clear();

        double width = WaveformCanvas.ActualWidth;
        double totalLength = _viewModel.TotalLength;

        if (width <= 0 || totalLength <= 0) return;

        var textBrush = FindResource("LoopFinderDimTextBrush") as Brush ?? Brushes.Gray;
        var tickBrush = FindResource("LoopFinderBorderBrush") as Brush ?? Brushes.DarkGray;

        // Calculate appropriate tick interval
        double pixelsPerSecond = width / totalLength;
        double tickInterval = 1.0; // Start with 1 second

        if (pixelsPerSecond < 10) tickInterval = 10.0;
        else if (pixelsPerSecond < 20) tickInterval = 5.0;
        else if (pixelsPerSecond < 50) tickInterval = 2.0;
        else if (pixelsPerSecond > 200) tickInterval = 0.5;
        else if (pixelsPerSecond > 500) tickInterval = 0.1;

        for (double time = 0; time <= totalLength; time += tickInterval)
        {
            double x = time / totalLength * width;

            // Draw tick
            var tick = new Shapes.Line
            {
                X1 = x,
                Y1 = 15,
                X2 = x,
                Y2 = 20,
                Stroke = tickBrush,
                StrokeThickness = 1
            };
            TimeScaleCanvas.Children.Add(tick);

            // Draw label
            var label = new TextBlock
            {
                Text = FormatTime(time),
                Foreground = textBrush,
                FontSize = 9
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 2);
            TimeScaleCanvas.Children.Add(label);
        }
    }

    private void DrawLoopRegion()
    {
        if (_loopRegionRect == null) return;

        double width = LoopRegionCanvas.ActualWidth;
        double height = LoopRegionCanvas.ActualHeight;
        double totalLength = _viewModel.TotalLength;

        if (width <= 0 || totalLength <= 0)
        {
            _loopRegionRect.Visibility = Visibility.Collapsed;
            return;
        }

        double startX = _viewModel.LoopStartPosition / totalLength * width;
        double endX = _viewModel.LoopEndPosition / totalLength * width;

        if (endX <= startX)
        {
            _loopRegionRect.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(_loopRegionRect, startX);
        _loopRegionRect.Width = endX - startX;
        _loopRegionRect.Height = height;
        _loopRegionRect.Visibility = Visibility.Visible;
    }

    private void UpdateMarkers()
    {
        if (_startMarkerLine == null || _endMarkerLine == null ||
            _startMarkerHandle == null || _endMarkerHandle == null) return;

        double width = MarkersCanvas.ActualWidth;
        double height = MarkersCanvas.ActualHeight;
        double totalLength = _viewModel.TotalLength;

        if (width <= 0 || totalLength <= 0)
        {
            _startMarkerLine.Visibility = Visibility.Collapsed;
            _endMarkerLine.Visibility = Visibility.Collapsed;
            _startMarkerHandle.Visibility = Visibility.Collapsed;
            _endMarkerHandle.Visibility = Visibility.Collapsed;
            return;
        }

        double startX = _viewModel.LoopStartPosition / totalLength * width;
        double endX = _viewModel.LoopEndPosition / totalLength * width;

        // Update start marker
        _startMarkerLine.X1 = startX;
        _startMarkerLine.Y1 = 0;
        _startMarkerLine.X2 = startX;
        _startMarkerLine.Y2 = height;
        _startMarkerLine.Visibility = Visibility.Visible;

        Canvas.SetLeft(_startMarkerHandle, startX - 4);
        Canvas.SetTop(_startMarkerHandle, 0);
        _startMarkerHandle.Visibility = Visibility.Visible;

        // Update end marker
        _endMarkerLine.X1 = endX;
        _endMarkerLine.Y1 = 0;
        _endMarkerLine.X2 = endX;
        _endMarkerLine.Y2 = height;
        _endMarkerLine.Visibility = Visibility.Visible;

        Canvas.SetLeft(_endMarkerHandle, endX - 4);
        Canvas.SetTop(_endMarkerHandle, 0);
        _endMarkerHandle.Visibility = Visibility.Visible;
    }

    private void UpdateLoopInfo()
    {
        double startTime = _viewModel.LoopStartPosition;
        double endTime = _viewModel.LoopEndPosition;
        double duration = endTime - startTime;

        LoopStartText.Text = $"Start: {FormatTime(startTime)}";
        LoopEndText.Text = $"End: {FormatTime(endTime)}";

        if (duration > 0)
        {
            // Calculate bars/beats
            float secondsPerBeat = 60f / Bpm;
            float secondsPerBar = secondsPerBeat * BeatsPerBar;
            int bars = (int)(duration / secondsPerBar);
            int beats = (int)((duration % secondsPerBar) / secondsPerBeat);

            LoopDurationText.Text = beats > 0
                ? $"Duration: {bars} bars + {beats} beats ({duration:F2}s)"
                : $"Duration: {bars} bars ({duration:F2}s)";
        }
        else
        {
            LoopDurationText.Text = "Duration: --";
        }
    }

    private void UpdateCrossfadePreview()
    {
        CrossfadePreviewCanvas.Children.Clear();

        if (!CrossfadePreviewCheckBox.IsChecked == true) return;

        double width = CrossfadePreviewCanvas.ActualWidth;
        double height = CrossfadePreviewCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double crossfadeMs = _viewModel.CrossfadeLengthMs;
        double crossfadeWidth = Math.Min(width / 3, crossfadeMs / 100.0 * width / 3);

        // Draw fade out region
        var fadeOutRect = new Shapes.Rectangle
        {
            Width = crossfadeWidth,
            Height = height,
            Fill = new LinearGradientBrush(
                Color.FromArgb(100, AccentColor.R, AccentColor.G, AccentColor.B),
                Color.FromArgb(0, AccentColor.R, AccentColor.G, AccentColor.B),
                0)
        };
        Canvas.SetLeft(fadeOutRect, 0);
        CrossfadePreviewCanvas.Children.Add(fadeOutRect);

        // Draw fade in region
        var fadeInRect = new Shapes.Rectangle
        {
            Width = crossfadeWidth,
            Height = height,
            Fill = new LinearGradientBrush(
                Color.FromArgb(0, AccentColor.R, AccentColor.G, AccentColor.B),
                Color.FromArgb(100, AccentColor.R, AccentColor.G, AccentColor.B),
                0)
        };
        Canvas.SetLeft(fadeInRect, width - crossfadeWidth);
        CrossfadePreviewCanvas.Children.Add(fadeInRect);

        // Draw center line
        var centerLine = new Shapes.Line
        {
            X1 = width / 2,
            Y1 = 0,
            X2 = width / 2,
            Y2 = height,
            Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        CrossfadePreviewCanvas.Children.Add(centerLine);

        // Draw labels
        var fadeOutLabel = new TextBlock
        {
            Text = "Fade Out",
            Foreground = FindResource("LoopFinderDimTextBrush") as Brush,
            FontSize = 9
        };
        Canvas.SetLeft(fadeOutLabel, 4);
        Canvas.SetTop(fadeOutLabel, 4);
        CrossfadePreviewCanvas.Children.Add(fadeOutLabel);

        var fadeInLabel = new TextBlock
        {
            Text = "Fade In",
            Foreground = FindResource("LoopFinderDimTextBrush") as Brush,
            FontSize = 9
        };
        fadeInLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(fadeInLabel, width - fadeInLabel.DesiredSize.Width - 4);
        Canvas.SetTop(fadeInLabel, 4);
        CrossfadePreviewCanvas.Children.Add(fadeInLabel);
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = _viewModel.LoopCandidates.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        CandidateCountText.Text = _viewModel.LoopCandidates.Count == 1
            ? "1 candidate"
            : $"{_viewModel.LoopCandidates.Count} candidates";
    }

    private void HighlightCandidates()
    {
        // Clear existing highlights
        foreach (var highlight in _candidateHighlights)
        {
            LoopRegionCanvas.Children.Remove(highlight);
        }
        _candidateHighlights.Clear();

        double width = LoopRegionCanvas.ActualWidth;
        double height = LoopRegionCanvas.ActualHeight;
        double totalLength = _viewModel.TotalLength;

        if (width <= 0 || totalLength <= 0) return;

        foreach (var candidate in _viewModel.LoopCandidates)
        {
            double startX = candidate.StartTime / totalLength * width;
            double endX = candidate.EndTime / totalLength * width;

            var highlight = new Shapes.Rectangle
            {
                Width = Math.Max(2, endX - startX),
                Height = height,
                Fill = new SolidColorBrush(Color.FromArgb(20, SuccessColor.R, SuccessColor.G, SuccessColor.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(60, SuccessColor.R, SuccessColor.G, SuccessColor.B)),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(highlight, startX);
            LoopRegionCanvas.Children.Add(highlight);
            _candidateHighlights.Add(highlight);
        }
    }

    #endregion

    #region Event Handlers - UI

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateDisplay();
        }
    }

    private void WaveformCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.TotalLength <= 0) return;

        _dragStartPoint = e.GetPosition(WaveformCanvas);
        _isDraggingSelection = true;
        WaveformCanvas.CaptureMouse();

        double position = _dragStartPoint.X / WaveformCanvas.ActualWidth * _viewModel.TotalLength;
        _viewModel.UpdateLoopStart(position);
    }

    private void WaveformCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSelection && !_isDraggingStartMarker && !_isDraggingEndMarker) return;

        Point currentPoint = e.GetPosition(WaveformCanvas);
        double position = currentPoint.X / WaveformCanvas.ActualWidth * _viewModel.TotalLength;
        position = Math.Clamp(position, 0, _viewModel.TotalLength);

        if (_isDraggingSelection)
        {
            _viewModel.UpdateLoopEnd(position);
        }
        else if (_isDraggingStartMarker)
        {
            _viewModel.UpdateLoopStart(position);
        }
        else if (_isDraggingEndMarker)
        {
            _viewModel.UpdateLoopEnd(position);
        }
    }

    private void WaveformCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSelection = false;
        _isDraggingStartMarker = false;
        _isDraggingEndMarker = false;
        WaveformCanvas.ReleaseMouseCapture();
        MarkersCanvas.ReleaseMouseCapture();
    }

    private void StartMarkerHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingStartMarker = true;
        _dragStartPosition = _viewModel.LoopStartPosition;
        MarkersCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void EndMarkerHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingEndMarker = true;
        _dragStartPosition = _viewModel.LoopEndPosition;
        MarkersCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void DetectLoopsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.DetectLoopsCommand.CanExecute(null))
        {
            ProgressOverlay.Visibility = Visibility.Visible;
            _viewModel.DetectLoopsCommand.Execute(null);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetCommand.Execute(null);
        UpdateDisplay();
    }

    private void MinLoopLengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        _viewModel.MinimumLoopLengthBars = (int)e.NewValue;

        // Ensure max is at least equal to min
        if (MaxLoopLengthSlider.Value < e.NewValue)
        {
            MaxLoopLengthSlider.Value = e.NewValue;
        }

        UpdateLoopLengthDisplay();
    }

    private void MaxLoopLengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        _viewModel.MaximumLoopLengthBars = (int)e.NewValue;

        // Ensure min is at most equal to max
        if (MinLoopLengthSlider.Value > e.NewValue)
        {
            MinLoopLengthSlider.Value = e.NewValue;
        }

        UpdateLoopLengthDisplay();
    }

    private void UpdateLoopLengthDisplay()
    {
        LoopLengthValue.Text = $"{(int)MinLoopLengthSlider.Value}-{(int)MaxLoopLengthSlider.Value}";
    }

    private void SimilarityThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        _viewModel.SimilarityThreshold = (float)(e.NewValue / 100.0);
        SimilarityThresholdValue.Text = $"{e.NewValue:F0}%";
    }

    private void CrossfadeLengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        _viewModel.CrossfadeLengthMs = (float)e.NewValue;
        CrossfadeLengthValue.Text = $"{e.NewValue:F0}ms";
        UpdateCrossfadePreview();
    }

    private void SnapOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        _viewModel.SnapToBeat = SnapToBeatCheckBox.IsChecked == true;
        _viewModel.SnapToBar = SnapToBarCheckBox.IsChecked == true;
    }

    private void ZeroCrossingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        _viewModel.ZeroCrossingEnabled = ZeroCrossingCheckBox.IsChecked == true;
    }

    private void CrossfadePreviewCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        _viewModel.CrossfadePreviewEnabled = CrossfadePreviewCheckBox.IsChecked == true;
        UpdateCrossfadePreview();
    }

    private void LoopCandidatesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LoopCandidatesListBox.SelectedItem is LoopCandidateViewModel candidate)
        {
            _viewModel.SelectedLoopCandidate = candidate;
        }
    }

    private void PreviewLoopButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LoopCandidateViewModel candidate)
        {
            _viewModel.SelectedLoopCandidate = candidate;
            _viewModel.PreviewLoopCommand.Execute(null);
        }
    }

    private void PlayOriginalToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (PlayOriginalToggle.IsChecked == true)
        {
            PlayLoopedToggle.IsChecked = false;
            _viewModel.PlayOriginalCommand.Execute(null);
        }
    }

    private void PlayLoopedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (PlayLoopedToggle.IsChecked == true)
        {
            PlayOriginalToggle.IsChecked = false;
            _viewModel.PlayLoopedCommand.Execute(null);
        }
    }

    private void StopPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        PlayOriginalToggle.IsChecked = false;
        PlayLoopedToggle.IsChecked = false;
        _viewModel.StopPreviewCommand.Execute(null);
    }

    private void ExportLoopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedLoopCandidate != null)
        {
            _viewModel.ExportSelectedLoopCommand.Execute(null);
        }
        else if (_viewModel.LoopStartPosition < _viewModel.LoopEndPosition)
        {
            _viewModel.ExportCustomLoopCommand.Execute(null);
        }
    }

    #endregion

    #region Event Handlers - ViewModel

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LoopFinderViewModel.WaveformData):
                Dispatcher.BeginInvoke(DrawWaveform);
                break;

            case nameof(LoopFinderViewModel.LoopStartPosition):
            case nameof(LoopFinderViewModel.LoopEndPosition):
                Dispatcher.BeginInvoke(() =>
                {
                    DrawLoopRegion();
                    UpdateMarkers();
                    UpdateLoopInfo();
                });
                break;

            case nameof(LoopFinderViewModel.IsDetecting):
                Dispatcher.BeginInvoke(() =>
                {
                    ProgressOverlay.Visibility = _viewModel.IsDetecting
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                });
                break;

            case nameof(LoopFinderViewModel.DetectionProgress):
                Dispatcher.BeginInvoke(() =>
                {
                    ProgressBar.Width = _viewModel.DetectionProgress * 200;
                });
                break;

            case nameof(LoopFinderViewModel.DetectedBpm):
                Dispatcher.BeginInvoke(() =>
                {
                    BpmText.Text = $"{_viewModel.DetectedBpm:F1}";
                });
                break;

            case nameof(LoopFinderViewModel.StatusMessage):
                Dispatcher.BeginInvoke(() =>
                {
                    StatusText.Text = _viewModel.StatusMessage ?? "Ready";
                });
                break;
        }
    }

    private void OnViewModelLoopPreviewRequested(object? sender, LoopPreviewEventArgs e)
    {
        LoopPreviewRequested?.Invoke(this, e);
    }

    private void OnViewModelLoopExportRequested(object? sender, LoopExportEventArgs e)
    {
        LoopExportRequested?.Invoke(this, e);
    }

    private void OnViewModelLoopMarkersChanged(object? sender, LoopMarkersChangedEventArgs e)
    {
        LoopMarkersChanged?.Invoke(this, e);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Loads audio data for loop analysis.
    /// </summary>
    /// <param name="leftChannel">Left channel samples.</param>
    /// <param name="rightChannel">Right channel samples (can be null for mono).</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <param name="fileName">Name of the audio file.</param>
    public void LoadAudioData(float[] leftChannel, float[]? rightChannel, int sampleRate, string fileName)
    {
        _viewModel.LoadAudioData(leftChannel, rightChannel, sampleRate, fileName);
        AudioFileNameText.Text = fileName;

        if (_isInitialized)
        {
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Sets the tempo information for bar/beat calculations.
    /// </summary>
    /// <param name="bpm">Beats per minute.</param>
    /// <param name="beatsPerBar">Number of beats per bar.</param>
    public void SetTempo(float bpm, int beatsPerBar = 4)
    {
        _viewModel.SetTempo(bpm, beatsPerBar);
        BpmText.Text = $"{bpm:F1}";
    }

    /// <summary>
    /// Updates the playback position indicator.
    /// </summary>
    /// <param name="position">Current playback position in seconds.</param>
    public void UpdatePlaybackPosition(double position)
    {
        _viewModel.UpdatePlaybackPosition(position);

        double width = PlayheadCanvas.ActualWidth;
        double totalLength = _viewModel.TotalLength;

        if (width <= 0 || totalLength <= 0) return;

        double x = position / totalLength * width;
        PlayheadLine.X1 = x;
        PlayheadLine.X2 = x;
        PlayheadLine.Y2 = PlayheadCanvas.ActualHeight;
        PlayheadLine.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Clears the audio data and resets the control.
    /// </summary>
    public void Clear()
    {
        _viewModel.ResetCommand.Execute(null);
        AudioFileNameText.Text = string.Empty;
        PlayheadLine.Visibility = Visibility.Collapsed;

        if (_isInitialized)
        {
            UpdateDisplay();
        }
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnSampleRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // SampleRate is passed when loading audio data
    }

    private static void OnBpmChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoopFinderPanel panel)
        {
            panel._viewModel.SetTempo((float)e.NewValue, panel.BeatsPerBar);
            panel.BpmText.Text = $"{e.NewValue:F1}";
        }
    }

    private static void OnBeatsPerBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoopFinderPanel panel)
        {
            panel._viewModel.SetTempo(panel.Bpm, (int)e.NewValue);
        }
    }

    private static void OnAudioDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoopFinderPanel panel && e.NewValue is float[] audioData)
        {
            panel._viewModel.LoadAudioData(audioData, null, panel.SampleRate, panel.AudioFileName);

            if (panel._isInitialized)
            {
                panel.UpdateDisplay();
            }
        }
    }

    private static void OnAudioFileNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoopFinderPanel panel)
        {
            panel.AudioFileNameText.Text = e.NewValue?.ToString() ?? string.Empty;
        }
    }

    #endregion

    #region Helper Methods

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}"
            : $"{ts.Seconds}.{ts.Milliseconds / 10:D2}s";
    }

    #endregion
}

#region Converters

/// <summary>
/// Converter for slider fill width.
/// </summary>
public class LoopFinderSliderWidthConverter : IValueConverter
{
    public static readonly LoopFinderSliderWidthConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // This is a placeholder - actual width would need track width
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for similarity score to color.
/// </summary>
public class LoopFinderSimilarityToColorConverter : IValueConverter
{
    public static readonly LoopFinderSimilarityToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not float score) return Brushes.Gray;

        return score switch
        {
            >= 0.9f => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),  // Success
            >= 0.8f => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),  // Accent
            >= 0.7f => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)),  // Warning
            _ => new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57))         // Error
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for duration to formatted string.
/// </summary>
public class LoopFinderDurationToStringConverter : IValueConverter
{
    public static readonly LoopFinderDurationToStringConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double duration) return "--";

        var ts = TimeSpan.FromSeconds(duration);
        return ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}"
            : $"{ts.Seconds}.{ts.Milliseconds / 10:D2}s";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for boolean to visibility with inverse support.
/// </summary>
public class LoopFinderBoolToVisibilityConverter : IValueConverter
{
    public static readonly LoopFinderBoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        bool inverse = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);

        if (inverse) boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool inverse = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
            bool result = visibility == Visibility.Visible;
            return inverse ? !result : result;
        }
        return false;
    }
}

#endregion
