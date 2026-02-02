// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Key Detector panel for musical key analysis and visualization.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.ViewModels.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Key Detector panel providing musical key analysis, circle of fifths visualization,
/// chromagram display, mode detection, and key change detection over time.
/// </summary>
public partial class KeyDetectorPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty DetectedKeyIndexProperty =
        DependencyProperty.Register(nameof(DetectedKeyIndex), typeof(int), typeof(KeyDetectorPanel),
            new PropertyMetadata(-1, OnDetectedKeyChanged));

    public static readonly DependencyProperty IsMajorProperty =
        DependencyProperty.Register(nameof(IsMajor), typeof(bool), typeof(KeyDetectorPanel),
            new PropertyMetadata(true, OnDetectedKeyChanged));

    public static readonly DependencyProperty ConfidenceProperty =
        DependencyProperty.Register(nameof(Confidence), typeof(double), typeof(KeyDetectorPanel),
            new PropertyMetadata(0.0, OnConfidenceChanged));

    public static readonly DependencyProperty IsLiveModeProperty =
        DependencyProperty.Register(nameof(IsLiveMode), typeof(bool), typeof(KeyDetectorPanel),
            new PropertyMetadata(true));

    public static readonly DependencyProperty UseFlatsProperty =
        DependencyProperty.Register(nameof(UseFlats), typeof(bool), typeof(KeyDetectorPanel),
            new PropertyMetadata(false, OnUseFlatsChanged));

    /// <summary>
    /// Gets or sets the detected key index (0-11, where 0=C).
    /// </summary>
    public int DetectedKeyIndex
    {
        get => (int)GetValue(DetectedKeyIndexProperty);
        set => SetValue(DetectedKeyIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the detected key is major (true) or minor (false).
    /// </summary>
    public bool IsMajor
    {
        get => (bool)GetValue(IsMajorProperty);
        set => SetValue(IsMajorProperty, value);
    }

    /// <summary>
    /// Gets or sets the detection confidence (0-1).
    /// </summary>
    public double Confidence
    {
        get => (double)GetValue(ConfidenceProperty);
        set => SetValue(ConfidenceProperty, value);
    }

    /// <summary>
    /// Gets or sets whether live analysis mode is enabled.
    /// </summary>
    public bool IsLiveMode
    {
        get => (bool)GetValue(IsLiveModeProperty);
        set => SetValue(IsLiveModeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to use flat notation instead of sharps.
    /// </summary>
    public bool UseFlats
    {
        get => (bool)GetValue(UseFlatsProperty);
        set => SetValue(UseFlatsProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private KeyDetectorViewModel? _viewModel;

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly string[] NoteNamesFlat = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };

    // Circle of fifths order (clockwise from top): C, G, D, A, E, B, F#/Gb, Db, Ab, Eb, Bb, F
    private static readonly int[] CircleOfFifthsOrder = { 0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5 };

    // Colors
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color SuccessColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color WarningColor = Color.FromRgb(0xFF, 0xB8, 0x00);
    private static readonly Color ErrorColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color DimColor = Color.FromRgb(0x80, 0x80, 0x80);
    private static readonly Color PanelColor = Color.FromRgb(0x18, 0x18, 0x18);

    // Visualization elements
    private readonly List<Shapes.Ellipse> _circleOfFifthsDots = new();
    private readonly List<TextBlock> _circleOfFifthsLabels = new();
    private readonly List<Shapes.Rectangle> _chromagramBars = new();
    private readonly List<TextBlock> _noteLabels = new();

    // Chromagram data
    private double[] _chromagram = new double[12];

    // Alternative keys
    private readonly ObservableCollection<AlternativeKeyViewModel> _alternativeKeys = new();

    // Key changes
    private readonly ObservableCollection<KeyChangeViewModel> _keyChanges = new();
    private double _audioDuration;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the detected key should be exported to project settings.
    /// </summary>
    public event EventHandler<KeyExportEventArgs>? ExportKeyRequested;

    /// <summary>
    /// Event raised when analysis from selection is requested.
    /// </summary>
    public event EventHandler? AnalyzeFromSelectionRequested;

    #endregion

    #region Constructor

    public KeyDetectorPanel()
    {
        InitializeComponent();

        AlternativeKeysItemsControl.ItemsSource = _alternativeKeys;
        KeyChangesItemsControl.ItemsSource = _keyChanges;

        InitializeNoteLabels();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Initialization

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        InitializeCircleOfFifths();
        InitializeChromagram();
        UpdateDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private void InitializeNoteLabels()
    {
        NoteLabelsControl.Items.Clear();
        _noteLabels.Clear();

        for (int i = 0; i < 12; i++)
        {
            var label = new TextBlock
            {
                Text = NoteNames[i],
                FontSize = 9,
                Foreground = new SolidColorBrush(DimColor),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            _noteLabels.Add(label);
            NoteLabelsControl.Items.Add(label);
        }
    }

    private void InitializeCircleOfFifths()
    {
        CircleOfFifthsCanvas.Children.Clear();
        _circleOfFifthsDots.Clear();
        _circleOfFifthsLabels.Clear();

        double centerX = CircleOfFifthsCanvas.ActualWidth / 2;
        double centerY = CircleOfFifthsCanvas.ActualHeight / 2;
        double radius = Math.Min(centerX, centerY) - 25;

        if (radius <= 0) radius = 75;

        // Draw background circle
        var backgroundCircle = new Shapes.Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(backgroundCircle, centerX - radius);
        Canvas.SetTop(backgroundCircle, centerY - radius);
        CircleOfFifthsCanvas.Children.Add(backgroundCircle);

        // Draw inner circle for minor keys
        double innerRadius = radius * 0.65;
        var innerCircle = new Shapes.Ellipse
        {
            Width = innerRadius * 2,
            Height = innerRadius * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(innerCircle, centerX - innerRadius);
        Canvas.SetTop(innerCircle, centerY - innerRadius);
        CircleOfFifthsCanvas.Children.Add(innerCircle);

        // Draw key positions
        for (int i = 0; i < 12; i++)
        {
            double angle = (i * 30 - 90) * Math.PI / 180; // Start from top (12 o'clock)

            // Major key (outer ring)
            double majorX = centerX + radius * Math.Cos(angle);
            double majorY = centerY + radius * Math.Sin(angle);

            var majorDot = new Shapes.Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(majorDot, majorX - 10);
            Canvas.SetTop(majorDot, majorY - 10);
            CircleOfFifthsCanvas.Children.Add(majorDot);
            _circleOfFifthsDots.Add(majorDot);

            // Major key label
            int noteIndex = CircleOfFifthsOrder[i];
            var majorLabel = new TextBlock
            {
                Text = NoteNames[noteIndex],
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(DimColor),
                TextAlignment = TextAlignment.Center
            };
            majorLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(majorLabel, majorX - majorLabel.DesiredSize.Width / 2);
            Canvas.SetTop(majorLabel, majorY - majorLabel.DesiredSize.Height / 2);
            CircleOfFifthsCanvas.Children.Add(majorLabel);
            _circleOfFifthsLabels.Add(majorLabel);

            // Minor key (inner ring) - relative minor is 3 semitones below
            double minorX = centerX + innerRadius * Math.Cos(angle);
            double minorY = centerY + innerRadius * Math.Sin(angle);

            var minorDot = new Shapes.Ellipse
            {
                Width = 16,
                Height = 16,
                Fill = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(minorDot, minorX - 8);
            Canvas.SetTop(minorDot, minorY - 8);
            CircleOfFifthsCanvas.Children.Add(minorDot);
            _circleOfFifthsDots.Add(minorDot);

            // Minor key label
            int minorNoteIndex = (noteIndex + 9) % 12; // Relative minor
            var minorLabel = new TextBlock
            {
                Text = NoteNames[minorNoteIndex].ToLower().Replace("#", "#"),
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                TextAlignment = TextAlignment.Center
            };
            minorLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(minorLabel, minorX - minorLabel.DesiredSize.Width / 2);
            Canvas.SetTop(minorLabel, minorY - minorLabel.DesiredSize.Height / 2);
            CircleOfFifthsCanvas.Children.Add(minorLabel);
            _circleOfFifthsLabels.Add(minorLabel);
        }
    }

    private void InitializeChromagram()
    {
        ChromagramCanvas.Children.Clear();
        _chromagramBars.Clear();

        double width = ChromagramCanvas.ActualWidth;
        double height = ChromagramCanvas.ActualHeight;

        if (width <= 0) width = 400;
        if (height <= 0) height = 80;

        double barWidth = (width - 24) / 12 - 4;

        for (int i = 0; i < 12; i++)
        {
            double x = 12 + i * (barWidth + 4);

            // Background bar
            var bgBar = new Shapes.Rectangle
            {
                Width = barWidth,
                Height = height - 10,
                Fill = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(bgBar, x);
            Canvas.SetTop(bgBar, 5);
            ChromagramCanvas.Children.Add(bgBar);

            // Value bar
            var valueBar = new Shapes.Rectangle
            {
                Width = barWidth,
                Height = 0,
                Fill = new SolidColorBrush(AccentColor),
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(valueBar, x);
            Canvas.SetBottom(valueBar, 5);
            ChromagramCanvas.Children.Add(valueBar);
            _chromagramBars.Add(valueBar);
        }
    }

    #endregion

    #region Event Handlers

    private static void OnDetectedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyDetectorPanel panel && panel._isInitialized)
        {
            panel.UpdateDisplay();
        }
    }

    private static void OnConfidenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyDetectorPanel panel && panel._isInitialized)
        {
            panel.UpdateConfidenceDisplay();
        }
    }

    private static void OnUseFlatsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyDetectorPanel panel && panel._isInitialized)
        {
            panel.UpdateNoteLabels();
            panel.UpdateDisplay();
        }
    }

    private void LiveModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsLiveMode = LiveModeToggle.IsChecked ?? true;
        _viewModel?.ToggleLiveModeCommand.Execute(null);
    }

    private void FlatsToggle_Changed(object sender, RoutedEventArgs e)
    {
        UseFlats = FlatsToggle.IsChecked ?? false;
        _viewModel?.ToggleFlatsCommand.Execute(null);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Reset();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (DetectedKeyIndex < 0) return;

        var args = new KeyExportEventArgs
        {
            RootNote = DetectedKeyIndex,
            IsMajor = IsMajor,
            KeyName = GetKeyName(DetectedKeyIndex, IsMajor),
            Confidence = Confidence,
            Mode = _viewModel?.DetectedMode ?? (IsMajor ? "Ionian (Major)" : "Aeolian (Minor)")
        };

        ExportKeyRequested?.Invoke(this, args);
        _viewModel?.ExportToProjectCommand.Execute(null);

        StatusText.Text = $"Exported {args.KeyName} to project";
    }

    private void AlternativeKey_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is AlternativeKeyViewModel altKey)
        {
            DetectedKeyIndex = altKey.RootNoteIndex;
            IsMajor = altKey.IsMajor;
            Confidence = altKey.Confidence;
            _viewModel?.SelectAlternativeKeyCommand.Execute(altKey);
        }
    }

    private void CircleOfFifthsCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            InitializeCircleOfFifths();
            UpdateCircleOfFifths();
        }
    }

    private void ChromagramCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            InitializeChromagram();
            UpdateChromagram();
        }
    }

    private void KeyChangesCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawKeyChangesTimeline();
        }
    }

    #endregion

    #region Update Methods

    private void UpdateDisplay()
    {
        UpdateKeyDisplay();
        UpdateConfidenceDisplay();
        UpdateRelatedKeys();
        UpdateCircleOfFifths();
        UpdateChromagram();
        UpdateScaleDegreeHighlighting();
        UpdateAlternativeKeysVisibility();
        UpdateKeyChangesVisibility();

        ExportButton.IsEnabled = DetectedKeyIndex >= 0;
    }

    private void UpdateKeyDisplay()
    {
        if (DetectedKeyIndex < 0)
        {
            DetectedKeyDisplay.Text = "---";
            DetectedModeDisplay.Text = "";
            return;
        }

        DetectedKeyDisplay.Text = GetKeyName(DetectedKeyIndex, IsMajor);
        DetectedModeDisplay.Text = _viewModel?.DetectedMode ?? (IsMajor ? "Ionian (Major)" : "Aeolian (Minor)");
    }

    private void UpdateConfidenceDisplay()
    {
        double percentage = Confidence * 100;
        ConfidenceText.Text = $"{percentage:F0}%";

        // Update confidence bar
        double maxWidth = ((Grid)ConfidenceFill.Parent).ActualWidth;
        if (maxWidth > 0)
        {
            ConfidenceFill.Width = maxWidth * Confidence;
        }

        // Update color and level based on confidence
        Color color;
        string level;

        if (Confidence >= 0.7)
        {
            color = SuccessColor;
            level = "High";
        }
        else if (Confidence >= 0.4)
        {
            color = WarningColor;
            level = "Medium";
        }
        else if (Confidence > 0)
        {
            color = ErrorColor;
            level = "Low";
        }
        else
        {
            color = DimColor;
            level = "Unknown";
        }

        ConfidenceText.Foreground = new SolidColorBrush(color);
        ConfidenceFill.Background = new SolidColorBrush(color);
        ConfidenceLevelText.Text = $" ({level})";
    }

    private void UpdateRelatedKeys()
    {
        if (DetectedKeyIndex < 0)
        {
            RelativeKeyDisplay.Text = "---";
            ParallelKeyDisplay.Text = "---";
            return;
        }

        // Relative major/minor
        int relativeIndex = IsMajor
            ? (DetectedKeyIndex + 9) % 12  // Relative minor
            : (DetectedKeyIndex + 3) % 12; // Relative major

        RelativeKeyDisplay.Text = GetKeyName(relativeIndex, !IsMajor);

        // Parallel major/minor
        ParallelKeyDisplay.Text = GetKeyName(DetectedKeyIndex, !IsMajor);
    }

    private void UpdateCircleOfFifths()
    {
        if (_circleOfFifthsDots.Count < 24) return;

        // Reset all dots
        for (int i = 0; i < 12; i++)
        {
            // Major dot
            _circleOfFifthsDots[i * 2].Fill = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            _circleOfFifthsDots[i * 2].Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

            // Minor dot
            _circleOfFifthsDots[i * 2 + 1].Fill = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
            _circleOfFifthsDots[i * 2 + 1].Stroke = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));

            // Labels
            if (_circleOfFifthsLabels.Count > i * 2)
            {
                _circleOfFifthsLabels[i * 2].Foreground = new SolidColorBrush(DimColor);
                if (_circleOfFifthsLabels.Count > i * 2 + 1)
                {
                    _circleOfFifthsLabels[i * 2 + 1].Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));
                }
            }
        }

        if (DetectedKeyIndex < 0) return;

        // Find position in circle of fifths
        int circlePosition = Array.IndexOf(CircleOfFifthsOrder, DetectedKeyIndex);
        if (circlePosition < 0) return;

        // Highlight detected key
        int dotIndex = IsMajor ? circlePosition * 2 : circlePosition * 2 + 1;

        if (dotIndex < _circleOfFifthsDots.Count)
        {
            _circleOfFifthsDots[dotIndex].Fill = new SolidColorBrush(AccentColor);
            _circleOfFifthsDots[dotIndex].Stroke = new SolidColorBrush(AccentColor);
        }

        if (dotIndex < _circleOfFifthsLabels.Count)
        {
            _circleOfFifthsLabels[dotIndex].Foreground = Brushes.White;
            _circleOfFifthsLabels[dotIndex].FontWeight = FontWeights.Bold;
        }

        // Highlight relative key
        int relativePosition = IsMajor ? circlePosition * 2 + 1 : circlePosition * 2;
        if (relativePosition < _circleOfFifthsDots.Count)
        {
            _circleOfFifthsDots[relativePosition].Fill = new SolidColorBrush(Color.FromArgb(128, AccentColor.R, AccentColor.G, AccentColor.B));
            _circleOfFifthsDots[relativePosition].Stroke = new SolidColorBrush(AccentColor);
        }
    }

    private void UpdateChromagram()
    {
        if (_chromagramBars.Count != 12) return;

        double maxHeight = ChromagramCanvas.ActualHeight - 10;
        if (maxHeight <= 0) maxHeight = 70;

        for (int i = 0; i < 12; i++)
        {
            double value = _viewModel?.NormalizedChromagram[i] ?? _chromagram[i];
            _chromagramBars[i].Height = maxHeight * Math.Max(0, Math.Min(1, value));

            // Position from bottom
            Canvas.SetTop(_chromagramBars[i], maxHeight + 5 - _chromagramBars[i].Height);
        }
    }

    private void UpdateScaleDegreeHighlighting()
    {
        if (DetectedKeyIndex < 0 || _noteLabels.Count != 12)
        {
            // Reset all labels
            for (int i = 0; i < _noteLabels.Count; i++)
            {
                _noteLabels[i].Foreground = new SolidColorBrush(DimColor);
                _noteLabels[i].FontWeight = FontWeights.Normal;
            }

            for (int i = 0; i < _chromagramBars.Count; i++)
            {
                _chromagramBars[i].Fill = new SolidColorBrush(AccentColor);
            }
            return;
        }

        // Scale intervals
        int[] majorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
        int[] minorIntervals = { 0, 2, 3, 5, 7, 8, 10 };
        var intervals = IsMajor ? majorIntervals : minorIntervals;

        var inScale = new HashSet<int>();
        foreach (int interval in intervals)
        {
            inScale.Add((DetectedKeyIndex + interval) % 12);
        }

        // Update labels and bars
        for (int i = 0; i < 12; i++)
        {
            bool isInScale = inScale.Contains(i);
            bool isRoot = i == DetectedKeyIndex;

            // Label styling
            _noteLabels[i].Foreground = new SolidColorBrush(
                isRoot ? AccentColor : (isInScale ? SuccessColor : DimColor));
            _noteLabels[i].FontWeight = isRoot ? FontWeights.Bold : FontWeights.Normal;

            // Bar styling
            _chromagramBars[i].Fill = new SolidColorBrush(
                isRoot ? AccentColor : (isInScale ? SuccessColor : Color.FromRgb(0x40, 0x40, 0x40)));
        }
    }

    private void UpdateNoteLabels()
    {
        string[] notes = UseFlats ? NoteNamesFlat : NoteNames;

        for (int i = 0; i < _noteLabels.Count && i < 12; i++)
        {
            _noteLabels[i].Text = notes[i];
        }

        // Also update circle of fifths labels
        for (int i = 0; i < 12 && i * 2 < _circleOfFifthsLabels.Count; i++)
        {
            int noteIndex = CircleOfFifthsOrder[i];
            _circleOfFifthsLabels[i * 2].Text = notes[noteIndex];

            if (i * 2 + 1 < _circleOfFifthsLabels.Count)
            {
                int minorNoteIndex = (noteIndex + 9) % 12;
                _circleOfFifthsLabels[i * 2 + 1].Text = notes[minorNoteIndex].ToLower();
            }
        }
    }

    private void UpdateAlternativeKeysVisibility()
    {
        NoAlternativesText.Visibility = _alternativeKeys.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateKeyChangesVisibility()
    {
        NoKeyChangesText.Visibility = _keyChanges.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        DrawKeyChangesTimeline();
    }

    private void DrawKeyChangesTimeline()
    {
        KeyChangesCanvas.Children.Clear();

        if (_keyChanges.Count == 0 || _audioDuration <= 0) return;

        double width = KeyChangesCanvas.ActualWidth;
        double height = KeyChangesCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Draw timeline background
        var timeline = new Shapes.Rectangle
        {
            Width = width,
            Height = 4,
            Fill = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            RadiusX = 2,
            RadiusY = 2
        };
        Canvas.SetTop(timeline, height / 2 - 2);
        KeyChangesCanvas.Children.Add(timeline);

        // Draw key change markers
        foreach (var change in _keyChanges)
        {
            double x = (change.TimePosition / _audioDuration) * width;

            // Vertical marker
            var marker = new Shapes.Rectangle
            {
                Width = 3,
                Height = height - 10,
                Fill = new SolidColorBrush(WarningColor),
                RadiusX = 1,
                RadiusY = 1
            };
            Canvas.SetLeft(marker, x - 1.5);
            Canvas.SetTop(marker, 5);
            KeyChangesCanvas.Children.Add(marker);

            // Tooltip
            marker.ToolTip = $"{change.TimeText}: {change.FromKey} -> {change.ToKey}";
        }
    }

    #endregion

    #region Helper Methods

    private string GetKeyName(int keyIndex, bool isMajor)
    {
        string[] notes = UseFlats ? NoteNamesFlat : NoteNames;
        string quality = isMajor ? "Major" : "Minor";
        return $"{notes[keyIndex]} {quality}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets all analysis data.
    /// </summary>
    public void Reset()
    {
        DetectedKeyIndex = -1;
        IsMajor = true;
        Confidence = 0;
        _chromagram = new double[12];
        _alternativeKeys.Clear();
        _keyChanges.Clear();
        _audioDuration = 0;

        _viewModel?.ResetCommand.Execute(null);

        StatusText.Text = "Ready";
        UpdateDisplay();
    }

    /// <summary>
    /// Updates the chromagram visualization with new data.
    /// </summary>
    public void UpdateChromagramData(double[] chromagramData)
    {
        if (chromagramData.Length != 12) return;

        Array.Copy(chromagramData, _chromagram, 12);

        if (_isInitialized && IsLiveMode)
        {
            UpdateChromagram();
        }

        _viewModel?.UpdateChromagram(chromagramData);
    }

    /// <summary>
    /// Analyzes audio samples for key detection.
    /// </summary>
    public void AnalyzeAudio(float[] samples, int sampleRate = 44100)
    {
        StatusText.Text = "Analyzing...";

        _viewModel?.AnalyzeAudio(samples, sampleRate);
    }

    /// <summary>
    /// Sets the key detection result directly.
    /// </summary>
    public void SetKeyResult(int keyIndex, bool isMajor, double confidence)
    {
        DetectedKeyIndex = keyIndex;
        IsMajor = isMajor;
        Confidence = confidence;

        ExportButton.IsEnabled = keyIndex >= 0;
        StatusText.Text = keyIndex >= 0 ? $"Detected: {GetKeyName(keyIndex, isMajor)}" : "Ready";
    }

    /// <summary>
    /// Sets alternative key suggestions.
    /// </summary>
    public void SetAlternativeKeys(IEnumerable<AlternativeKeyViewModel> alternatives)
    {
        _alternativeKeys.Clear();
        foreach (var alt in alternatives)
        {
            _alternativeKeys.Add(alt);
        }
        UpdateAlternativeKeysVisibility();
    }

    /// <summary>
    /// Sets detected key changes.
    /// </summary>
    public void SetKeyChanges(IEnumerable<KeyChangeViewModel> changes, double audioDuration)
    {
        _keyChanges.Clear();
        _audioDuration = audioDuration;

        foreach (var change in changes)
        {
            _keyChanges.Add(change);
        }
        UpdateKeyChangesVisibility();
    }

    /// <summary>
    /// Binds to a KeyDetectorViewModel.
    /// </summary>
    public void BindViewModel(KeyDetectorViewModel viewModel)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        DataContext = viewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Sync initial values
            DetectedKeyIndex = _viewModel.DetectedKeyIndex;
            IsMajor = _viewModel.IsMajor;
            Confidence = _viewModel.Confidence;
            IsLiveMode = _viewModel.IsLiveMode;
            UseFlats = _viewModel.UseFlats;

            LiveModeToggle.IsChecked = _viewModel.IsLiveMode;
            FlatsToggle.IsChecked = _viewModel.UseFlats;

            // Bind collections
            AlternativeKeysItemsControl.ItemsSource = _viewModel.AlternativeKeys;
            KeyChangesItemsControl.ItemsSource = _viewModel.KeyChanges;

            _audioDuration = _viewModel.AudioDuration;

            UpdateDisplay();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(KeyDetectorViewModel.DetectedKeyIndex):
                    DetectedKeyIndex = _viewModel.DetectedKeyIndex;
                    break;
                case nameof(KeyDetectorViewModel.IsMajor):
                    IsMajor = _viewModel.IsMajor;
                    break;
                case nameof(KeyDetectorViewModel.Confidence):
                    Confidence = _viewModel.Confidence;
                    break;
                case nameof(KeyDetectorViewModel.DetectedKey):
                    UpdateKeyDisplay();
                    break;
                case nameof(KeyDetectorViewModel.DetectedMode):
                    DetectedModeDisplay.Text = _viewModel.DetectedMode;
                    break;
                case nameof(KeyDetectorViewModel.RelativeMajorMinor):
                    RelativeKeyDisplay.Text = _viewModel.RelativeMajorMinor;
                    break;
                case nameof(KeyDetectorViewModel.ParallelMajorMinor):
                    ParallelKeyDisplay.Text = _viewModel.ParallelMajorMinor;
                    break;
                case nameof(KeyDetectorViewModel.NormalizedChromagram):
                    UpdateChromagram();
                    UpdateScaleDegreeHighlighting();
                    break;
                case nameof(KeyDetectorViewModel.IsLiveMode):
                    IsLiveMode = _viewModel.IsLiveMode;
                    LiveModeToggle.IsChecked = _viewModel.IsLiveMode;
                    break;
                case nameof(KeyDetectorViewModel.UseFlats):
                    UseFlats = _viewModel.UseFlats;
                    FlatsToggle.IsChecked = _viewModel.UseFlats;
                    break;
                case nameof(KeyDetectorViewModel.CanExportKey):
                    ExportButton.IsEnabled = _viewModel.CanExportKey;
                    break;
                case nameof(KeyDetectorViewModel.StatusMessage):
                    if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
                    {
                        StatusText.Text = _viewModel.StatusMessage;
                    }
                    break;
                case nameof(KeyDetectorViewModel.IsAnalyzing):
                    StatusText.Text = _viewModel.IsAnalyzing ? "Analyzing..." : "Ready";
                    break;
                case nameof(KeyDetectorViewModel.HasKeyChanges):
                    UpdateKeyChangesVisibility();
                    break;
                case nameof(KeyDetectorViewModel.AudioDuration):
                    _audioDuration = _viewModel.AudioDuration;
                    DrawKeyChangesTimeline();
                    break;
            }
        });
    }

    /// <summary>
    /// Gets the associated ViewModel.
    /// </summary>
    public KeyDetectorViewModel? ViewModel => _viewModel;

    #endregion
}

#region Converters

/// <summary>
/// Converts a confidence value to a brush for visualization.
/// </summary>
public class KeyConfidenceToBrushConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double confidence)
        {
            if (confidence >= 0.7)
                return new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)); // Success
            if (confidence >= 0.4)
                return new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)); // Warning
            if (confidence > 0)
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)); // Error
        }
        return new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)); // Dim
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts key index and major/minor to display string.
/// </summary>
public class KeyIndexToStringConverter : System.Windows.Data.IMultiValueConverter
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is int keyIndex && values[1] is bool isMajor)
        {
            if (keyIndex < 0 || keyIndex >= 12)
                return "---";

            string quality = isMajor ? "Major" : "Minor";
            return $"{NoteNames[keyIndex]} {quality}";
        }
        return "---";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
