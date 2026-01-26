// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents an EQ suggestion based on spectral analysis.
/// </summary>
public class EqSuggestion
{
    public string Frequency { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public Brush SuggestionColor { get; set; } = Brushes.White;
}

/// <summary>
/// Spectral difference display for comparing EQ between two tracks.
/// Shows positive/negative differences with EQ adjustment suggestions.
/// </summary>
public partial class SpectralDifferenceControl : UserControl
{
    #region Constants

    private const double MinFrequency = 20.0;
    private const double MaxFrequency = 20000.0;
    private const double MinDb = -24.0;
    private const double MaxDb = 24.0;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty TrackASpectrumProperty =
        DependencyProperty.Register(nameof(TrackASpectrum), typeof(float[]), typeof(SpectralDifferenceControl),
            new PropertyMetadata(null, OnSpectrumChanged));

    public static readonly DependencyProperty TrackBSpectrumProperty =
        DependencyProperty.Register(nameof(TrackBSpectrum), typeof(float[]), typeof(SpectralDifferenceControl),
            new PropertyMetadata(null, OnSpectrumChanged));

    public static readonly DependencyProperty FrequenciesProperty =
        DependencyProperty.Register(nameof(Frequencies), typeof(float[]), typeof(SpectralDifferenceControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TrackNamesProperty =
        DependencyProperty.Register(nameof(TrackNames), typeof(string[]), typeof(SpectralDifferenceControl),
            new PropertyMetadata(null, OnTrackNamesChanged));

    /// <summary>
    /// Gets or sets Track A spectrum data (dB values).
    /// </summary>
    public float[]? TrackASpectrum
    {
        get => (float[]?)GetValue(TrackASpectrumProperty);
        set => SetValue(TrackASpectrumProperty, value);
    }

    /// <summary>
    /// Gets or sets Track B spectrum data (dB values).
    /// </summary>
    public float[]? TrackBSpectrum
    {
        get => (float[]?)GetValue(TrackBSpectrumProperty);
        set => SetValue(TrackBSpectrumProperty, value);
    }

    /// <summary>
    /// Gets or sets the frequency values for each spectrum bin.
    /// </summary>
    public float[] Frequencies
    {
        get => (float[])GetValue(FrequenciesProperty);
        set => SetValue(FrequenciesProperty, value);
    }

    /// <summary>
    /// Gets or sets the available track names.
    /// </summary>
    public string[] TrackNames
    {
        get => (string[])GetValue(TrackNamesProperty);
        set => SetValue(TrackNamesProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when track selection changes.
    /// </summary>
    public event EventHandler<(int TrackAIndex, int TrackBIndex)>? TrackSelectionChanged;

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private readonly List<EqSuggestion> _suggestions = new();

    private readonly Color _positiveColor = Color.FromRgb(0x4C, 0xAF, 0x50);
    private readonly Color _negativeColor = Color.FromRgb(0xF4, 0x43, 0x36);
    private readonly Color _trackAColor = Color.FromRgb(0x00, 0xCE, 0xD1);
    private readonly Color _trackBColor = Color.FromRgb(0xFF, 0x98, 0x00);

    #endregion

    #region Constructor

    public SpectralDifferenceControl()
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
        _isInitialized = true;
        DrawAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawAll();
        }
    }

    private static void OnSpectrumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralDifferenceControl control && control._isInitialized)
        {
            control.DrawDifferenceSpectrum();
            control.UpdateSuggestions();
        }
    }

    private static void OnTrackNamesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralDifferenceControl control)
        {
            control.UpdateTrackComboBoxes();
        }
    }

    private void TrackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int trackAIndex = TrackACombo.SelectedIndex;
        int trackBIndex = TrackBCombo.SelectedIndex;

        if (trackAIndex >= 0 && trackBIndex >= 0)
        {
            TrackSelectionChanged?.Invoke(this, (trackAIndex, trackBIndex));
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        DrawDifferenceSpectrum();
        UpdateSuggestions();
    }

    #endregion

    #region Drawing

    private void DrawAll()
    {
        DrawGrid();
        DrawZeroLine();
        DrawDbScale();
        DrawFrequencyLabels();
        DrawDifferenceSpectrum();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Horizontal grid lines (dB)
        double[] dbLines = { -24, -18, -12, -6, 0, 6, 12, 18, 24 };
        foreach (var db in dbLines)
        {
            double y = DbToY(db, height);
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }

        // Vertical grid lines (frequency, log scale)
        double[] freqLines = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (var freq in freqLines)
        {
            double x = FrequencyToX(freq, width);
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawZeroLine()
    {
        ZeroLineCanvas.Children.Clear();

        double width = ZeroLineCanvas.ActualWidth;
        double height = ZeroLineCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double y = DbToY(0, height);
        var line = new Shapes.Line
        {
            X1 = 0,
            Y1 = y,
            X2 = width,
            Y2 = y,
            Stroke = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A)),
            StrokeThickness = 1.5
        };
        ZeroLineCanvas.Children.Add(line);
    }

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double height = GridCanvas.ActualHeight > 0 ? GridCanvas.ActualHeight : 150;

        double[] dbMarks = { 24, 12, 6, 0, -6, -12, -24 };
        foreach (var db in dbMarks)
        {
            double y = DbToY(db, height);

            var label = new TextBlock
            {
                Text = db >= 0 ? $"+{db}" : db.ToString(),
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            DbScaleCanvas.Children.Add(label);
        }
    }

    private void DrawFrequencyLabels()
    {
        FrequencyLabelsCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double width = GridCanvas.ActualWidth;

        if (width <= 0) return;

        double[] frequencies = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (var freq in frequencies)
        {
            double x = FrequencyToX(freq, width);
            string text = freq >= 1000 ? $"{freq / 1000}k" : freq.ToString();

            var label = new TextBlock
            {
                Text = text,
                Foreground = textBrush,
                FontSize = 9
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 2);
            FrequencyLabelsCanvas.Children.Add(label);
        }
    }

    private void DrawDifferenceSpectrum()
    {
        SpectrumCanvas.Children.Clear();
        TrackACanvas.Children.Clear();
        TrackBCanvas.Children.Clear();

        if (TrackASpectrum == null || TrackBSpectrum == null) return;

        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        int count = Math.Min(TrackASpectrum.Length, TrackBSpectrum.Length);
        if (count < 2) return;

        double zeroY = DbToY(0, height);

        // Draw filled difference area
        var positiveGeometry = new StreamGeometry();
        var negativeGeometry = new StreamGeometry();

        using (var posContext = positiveGeometry.Open())
        using (var negContext = negativeGeometry.Open())
        {
            bool posStarted = false;
            bool negStarted = false;
            double lastPosX = 0, lastNegX = 0;

            for (int i = 0; i < count; i++)
            {
                double freqRatio = (double)i / (count - 1);
                double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);
                double x = FrequencyToX(freq, width);

                float diff = TrackASpectrum[i] - TrackBSpectrum[i];
                diff = Math.Clamp(diff, (float)MinDb, (float)MaxDb);
                double y = DbToY(diff, height);

                if (diff >= 0)
                {
                    if (!posStarted)
                    {
                        posContext.BeginFigure(new Point(x, zeroY), true, true);
                        posStarted = true;
                    }
                    posContext.LineTo(new Point(x, y), true, false);
                    lastPosX = x;
                }

                if (diff <= 0)
                {
                    if (!negStarted)
                    {
                        negContext.BeginFigure(new Point(x, zeroY), true, true);
                        negStarted = true;
                    }
                    negContext.LineTo(new Point(x, y), true, false);
                    lastNegX = x;
                }
            }

            if (posStarted)
            {
                posContext.LineTo(new Point(lastPosX, zeroY), true, false);
            }
            if (negStarted)
            {
                negContext.LineTo(new Point(lastNegX, zeroY), true, false);
            }
        }

        positiveGeometry.Freeze();
        negativeGeometry.Freeze();

        // Positive area (Track A louder)
        var posPath = new Shapes.Path
        {
            Data = positiveGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(128, _positiveColor.R, _positiveColor.G, _positiveColor.B)),
            Stroke = new SolidColorBrush(_positiveColor),
            StrokeThickness = 1
        };
        SpectrumCanvas.Children.Add(posPath);

        // Negative area (Track B louder)
        var negPath = new Shapes.Path
        {
            Data = negativeGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(128, _negativeColor.R, _negativeColor.G, _negativeColor.B)),
            Stroke = new SolidColorBrush(_negativeColor),
            StrokeThickness = 1
        };
        SpectrumCanvas.Children.Add(negPath);

        // Draw track curves as outlines
        DrawTrackCurve(TrackACanvas, TrackASpectrum, _trackAColor, width, height);
        DrawTrackCurve(TrackBCanvas, TrackBSpectrum, _trackBColor, width, height);
    }

    private void DrawTrackCurve(Canvas canvas, float[] spectrum, Color color, double width, double height)
    {
        if (spectrum == null || spectrum.Length < 2) return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            bool started = false;

            for (int i = 0; i < spectrum.Length; i++)
            {
                double freqRatio = (double)i / (spectrum.Length - 1);
                double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);
                double x = FrequencyToX(freq, width);

                // Offset for visualization (show absolute levels around center)
                float db = spectrum[i];
                db = Math.Clamp(db, (float)MinDb, (float)MaxDb);
                double y = DbToY(db, height);

                if (!started)
                {
                    context.BeginFigure(new Point(x, y), false, false);
                    started = true;
                }
                else
                {
                    context.LineTo(new Point(x, y), true, true);
                }
            }
        }

        geometry.Freeze();

        var path = new Shapes.Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        canvas.Children.Add(path);
    }

    #endregion

    #region EQ Suggestions

    private void UpdateSuggestions()
    {
        _suggestions.Clear();

        if (TrackASpectrum == null || TrackBSpectrum == null)
        {
            SuggestionsPanel.ItemsSource = null;
            return;
        }

        int count = Math.Min(TrackASpectrum.Length, TrackBSpectrum.Length);
        if (count < 2)
        {
            SuggestionsPanel.ItemsSource = null;
            return;
        }

        // Find frequency bands with significant differences
        var bands = new (string Name, int StartBin, int EndBin, double CenterFreq)[]
        {
            ("Sub", 0, count / 20, 40),
            ("Bass", count / 20, count / 10, 100),
            ("Low-Mid", count / 10, count / 5, 300),
            ("Mid", count / 5, count / 3, 1000),
            ("Hi-Mid", count / 3, count / 2, 3000),
            ("High", count / 2, count * 3 / 4, 8000),
            ("Air", count * 3 / 4, count - 1, 15000)
        };

        foreach (var band in bands)
        {
            double avgDiff = 0;
            int binCount = 0;

            for (int i = band.StartBin; i <= Math.Min(band.EndBin, count - 1); i++)
            {
                avgDiff += TrackASpectrum[i] - TrackBSpectrum[i];
                binCount++;
            }

            if (binCount > 0)
            {
                avgDiff /= binCount;

                // Only suggest if difference is significant (> 3dB)
                if (Math.Abs(avgDiff) > 3)
                {
                    string freqText = band.CenterFreq >= 1000
                        ? $"{band.CenterFreq / 1000}kHz"
                        : $"{band.CenterFreq}Hz";

                    string suggestionText;
                    Brush suggestionColor;

                    if (avgDiff > 0)
                    {
                        suggestionText = $"Cut {Math.Abs(avgDiff):F0}dB on A";
                        suggestionColor = new SolidColorBrush(_negativeColor);
                    }
                    else
                    {
                        suggestionText = $"Boost {Math.Abs(avgDiff):F0}dB on A";
                        suggestionColor = new SolidColorBrush(_positiveColor);
                    }

                    _suggestions.Add(new EqSuggestion
                    {
                        Frequency = freqText,
                        Suggestion = suggestionText,
                        SuggestionColor = suggestionColor
                    });
                }
            }
        }

        SuggestionsPanel.ItemsSource = _suggestions.Count > 0 ? _suggestions : null;
    }

    #endregion

    #region Track Selection

    private void UpdateTrackComboBoxes()
    {
        TrackACombo.Items.Clear();
        TrackBCombo.Items.Clear();

        if (TrackNames == null) return;

        foreach (var name in TrackNames)
        {
            TrackACombo.Items.Add(new ComboBoxItem { Content = name });
            TrackBCombo.Items.Add(new ComboBoxItem { Content = name });
        }

        if (TrackNames.Length > 0)
        {
            TrackACombo.SelectedIndex = 0;
        }
        if (TrackNames.Length > 1)
        {
            TrackBCombo.SelectedIndex = 1;
        }
    }

    #endregion

    #region Coordinate Conversions

    private static double FrequencyToX(double frequency, double width)
    {
        double logMin = Math.Log10(MinFrequency);
        double logMax = Math.Log10(MaxFrequency);
        double logFreq = Math.Log10(Math.Clamp(frequency, MinFrequency, MaxFrequency));
        return ((logFreq - logMin) / (logMax - logMin)) * width;
    }

    private static double DbToY(double db, double height)
    {
        double normalized = (db - MinDb) / (MaxDb - MinDb);
        return height * (1 - normalized);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets spectrum data for both tracks.
    /// </summary>
    public void SetSpectra(float[] trackA, float[] trackB, float[]? frequencies = null)
    {
        TrackASpectrum = trackA;
        TrackBSpectrum = trackB;
        if (frequencies != null) Frequencies = frequencies;
    }

    /// <summary>
    /// Clears the display.
    /// </summary>
    public void Clear()
    {
        TrackASpectrum = null;
        TrackBSpectrum = null;
        _suggestions.Clear();
        SuggestionsPanel.ItemsSource = null;
        SpectrumCanvas.Children.Clear();
        TrackACanvas.Children.Clear();
        TrackBCanvas.Children.Clear();
    }

    /// <summary>
    /// Gets the current EQ suggestions.
    /// </summary>
    public IReadOnlyList<EqSuggestion> GetSuggestions() => _suggestions.AsReadOnly();

    #endregion
}
