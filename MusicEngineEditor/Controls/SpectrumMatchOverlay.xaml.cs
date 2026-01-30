// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Spectrum match overlay control for comparing mix and reference track spectrums.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicEngine.Core.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for overlaying and comparing mix spectrum with a reference track spectrum.
/// Features color-coded delta visualization, opacity control, and detailed band analysis.
/// </summary>
public partial class SpectrumMatchOverlay : UserControl
{
    #region Constants

    private const double MinFrequency = 20.0;
    private const double MaxFrequency = 20000.0;
    private const double MinDb = -80.0;
    private const double MaxDb = 0.0;
    private const double DeltaMinDb = -24.0;
    private const double DeltaMaxDb = 24.0;
    private const double SignificantDifferenceThreshold = 3.0; // dB

    // Frequency band boundaries (Hz)
    private static readonly double[] BandStartFrequencies = { 20, 60, 250, 500, 1000, 2000, 4000, 8000 };
    private static readonly double[] BandEndFrequencies = { 60, 250, 500, 1000, 2000, 4000, 8000, 20000 };

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty MixSpectrumProperty =
        DependencyProperty.Register(nameof(MixSpectrum), typeof(float[]), typeof(SpectrumMatchOverlay),
            new PropertyMetadata(null, OnSpectrumChanged));

    public static readonly DependencyProperty ReferenceSpectrumProperty =
        DependencyProperty.Register(nameof(ReferenceSpectrum), typeof(float[]), typeof(SpectrumMatchOverlay),
            new PropertyMetadata(null, OnSpectrumChanged));

    public static readonly DependencyProperty FrequenciesProperty =
        DependencyProperty.Register(nameof(Frequencies), typeof(float[]), typeof(SpectrumMatchOverlay),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShowOverlayProperty =
        DependencyProperty.Register(nameof(ShowOverlay), typeof(bool), typeof(SpectrumMatchOverlay),
            new PropertyMetadata(true, OnDisplayOptionChanged));

    public static readonly DependencyProperty ShowDeltaProperty =
        DependencyProperty.Register(nameof(ShowDelta), typeof(bool), typeof(SpectrumMatchOverlay),
            new PropertyMetadata(true, OnDisplayOptionChanged));

    public static readonly DependencyProperty AutoLevelMatchProperty =
        DependencyProperty.Register(nameof(AutoLevelMatch), typeof(bool), typeof(SpectrumMatchOverlay),
            new PropertyMetadata(true, OnDisplayOptionChanged));

    public static readonly DependencyProperty OverlayOpacityProperty =
        DependencyProperty.Register(nameof(OverlayOpacity), typeof(double), typeof(SpectrumMatchOverlay),
            new PropertyMetadata(0.7, OnOpacityChanged));

    /// <summary>
    /// Gets or sets the mix spectrum data (linear magnitude values 0.0 to 1.0).
    /// </summary>
    public float[]? MixSpectrum
    {
        get => (float[]?)GetValue(MixSpectrumProperty);
        set => SetValue(MixSpectrumProperty, value);
    }

    /// <summary>
    /// Gets or sets the reference spectrum data (linear magnitude values 0.0 to 1.0).
    /// </summary>
    public float[]? ReferenceSpectrum
    {
        get => (float[]?)GetValue(ReferenceSpectrumProperty);
        set => SetValue(ReferenceSpectrumProperty, value);
    }

    /// <summary>
    /// Gets or sets the center frequencies for each spectrum band.
    /// </summary>
    public float[]? Frequencies
    {
        get => (float[]?)GetValue(FrequenciesProperty);
        set => SetValue(FrequenciesProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the reference overlay.
    /// </summary>
    public bool ShowOverlay
    {
        get => (bool)GetValue(ShowOverlayProperty);
        set => SetValue(ShowOverlayProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the delta/difference visualization.
    /// </summary>
    public bool ShowDelta
    {
        get => (bool)GetValue(ShowDeltaProperty);
        set => SetValue(ShowDeltaProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to auto-match levels between mix and reference.
    /// </summary>
    public bool AutoLevelMatch
    {
        get => (bool)GetValue(AutoLevelMatchProperty);
        set => SetValue(AutoLevelMatchProperty, value);
    }

    /// <summary>
    /// Gets or sets the opacity of the overlay visualization (0.1 to 1.0).
    /// </summary>
    public double OverlayOpacity
    {
        get => (double)GetValue(OverlayOpacityProperty);
        set => SetValue(OverlayOpacityProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private double[]? _bandDifferences;
    private SpectrumMatchResult? _lastMatchResult;

    // Colors
    private readonly Color _mixColor = Color.FromRgb(0x00, 0xCE, 0xD1);
    private readonly Color _referenceColor = Color.FromRgb(0xFF, 0x98, 0x00);
    private readonly Color _exceedsColor = Color.FromRgb(0xE5, 0x39, 0x35);
    private readonly Color _belowColor = Color.FromRgb(0x1E, 0x88, 0xE5);
    private readonly Color _neutralColor = Color.FromRgb(0x55, 0x55, 0x55);
    private readonly Color _matchedColor = Color.FromRgb(0x00, 0xCC, 0x66);

    // Band indicator references
    private Border[]? _bandIndicators;
    private TextBlock[]? _bandDiffTexts;

    #endregion

    #region Constructor

    public SpectrumMatchOverlay()
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

        // Initialize band indicator arrays
        _bandIndicators = new[]
        {
            Band1Indicator, Band2Indicator, Band3Indicator, Band4Indicator,
            Band5Indicator, Band6Indicator, Band7Indicator, Band8Indicator
        };

        _bandDiffTexts = new[]
        {
            Band1DiffText, Band2DiffText, Band3DiffText, Band4DiffText,
            Band5DiffText, Band6DiffText, Band7DiffText, Band8DiffText
        };

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
        if (d is SpectrumMatchOverlay control && control._isInitialized)
        {
            control.UpdateDisplay();
        }
    }

    private static void OnDisplayOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumMatchOverlay control && control._isInitialized)
        {
            control.UpdateDisplay();
        }
    }

    private static void OnOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumMatchOverlay control && control._isInitialized)
        {
            control.UpdateDisplay();
        }
    }

    private void ShowOverlayToggle_Changed(object sender, RoutedEventArgs e)
    {
        ShowOverlay = ShowOverlayToggle.IsChecked ?? true;
    }

    private void ShowDeltaToggle_Changed(object sender, RoutedEventArgs e)
    {
        ShowDelta = ShowDeltaToggle.IsChecked ?? true;
    }

    private void LevelMatchToggle_Changed(object sender, RoutedEventArgs e)
    {
        AutoLevelMatch = LevelMatchToggle.IsChecked ?? true;
    }

    private void OverlayOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        OverlayOpacity = OverlayOpacitySlider.Value;
        OpacityValueText.Text = $"{(int)(OverlayOpacity * 100)}%";
    }

    #endregion

    #region Drawing Methods

    private void DrawAll()
    {
        DrawGrid();
        DrawDbScale();
        DrawFrequencyLabels();
        UpdateDisplay();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Horizontal grid lines (dB)
        double[] dbLines = { -12, -24, -36, -48, -60, -72 };
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

        // Vertical grid lines (frequency band boundaries)
        double[] freqLines = { 60, 250, 500, 1000, 2000, 4000, 8000, 12000 };
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

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double height = GridCanvas.ActualHeight > 0 ? GridCanvas.ActualHeight : 150;

        double[] dbMarks = { 0, -12, -24, -36, -48, -60 };
        foreach (var db in dbMarks)
        {
            double y = DbToY(db, height);

            var label = new TextBlock
            {
                Text = db.ToString(),
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

    private void UpdateDisplay()
    {
        MixSpectrumCanvas.Children.Clear();
        ReferenceOverlayCanvas.Children.Clear();
        DeltaFillCanvas.Children.Clear();
        DeltaLineCanvas.Children.Clear();
        ZeroLineCanvas.Children.Clear();

        double width = MixSpectrumCanvas.ActualWidth;
        double height = MixSpectrumCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        bool hasReference = ReferenceSpectrum != null && ReferenceSpectrum.Length > 0;
        NoReferenceText.Visibility = hasReference ? Visibility.Collapsed : Visibility.Visible;

        // Get adjusted reference spectrum if level matching is enabled
        float[]? adjustedReference = null;
        if (hasReference && ReferenceSpectrum != null)
        {
            adjustedReference = AutoLevelMatch && MixSpectrum != null
                ? ApplyLevelMatching(ReferenceSpectrum, MixSpectrum)
                : ReferenceSpectrum;
        }

        // Draw mix spectrum
        if (MixSpectrum != null && MixSpectrum.Length > 0)
        {
            DrawSpectrumLine(MixSpectrumCanvas, MixSpectrum, _mixColor, width, height, 2.0);
        }

        // Draw reference overlay
        if (ShowOverlay && hasReference && adjustedReference != null)
        {
            var refColorWithOpacity = Color.FromArgb(
                (byte)(OverlayOpacity * 255),
                _referenceColor.R, _referenceColor.G, _referenceColor.B);
            DrawSpectrumLine(ReferenceOverlayCanvas, adjustedReference, refColorWithOpacity, width, height, 1.5);
        }

        // Draw delta visualization
        if (ShowDelta && hasReference && MixSpectrum != null && adjustedReference != null)
        {
            DrawDeltaFill(MixSpectrum, adjustedReference, width, height);
        }

        // Update band indicators and statistics
        if (hasReference && MixSpectrum != null && adjustedReference != null)
        {
            UpdateBandIndicators(MixSpectrum, adjustedReference);
            UpdateStatistics(MixSpectrum, adjustedReference);
        }
        else
        {
            ResetBandIndicators();
            ResetStatistics();
        }
    }

    private void DrawSpectrumLine(Canvas canvas, float[] spectrum, Color color, double width, double height, double strokeThickness)
    {
        if (spectrum.Length < 2) return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            bool started = false;

            for (int i = 0; i < spectrum.Length; i++)
            {
                double freqRatio = (double)i / (spectrum.Length - 1);
                double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);
                double x = FrequencyToX(freq, width);

                double db = spectrum[i] > 0 ? 20 * Math.Log10(spectrum[i]) : MinDb;
                double y = DbToY(db, height);
                y = Math.Clamp(y, 0, height);

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
            StrokeThickness = strokeThickness,
            StrokeLineJoin = PenLineJoin.Round
        };

        canvas.Children.Add(path);
    }

    private void DrawDeltaFill(float[] mix, float[] reference, double width, double height)
    {
        if (mix.Length < 2 || reference.Length < 2) return;

        int length = Math.Min(mix.Length, reference.Length);

        // Create separate paths for exceeds (red) and below (blue) regions
        var exceedsPoints = new System.Collections.Generic.List<(double x, double mixY, double refY)>();
        var belowPoints = new System.Collections.Generic.List<(double x, double mixY, double refY)>();

        for (int i = 0; i < length; i++)
        {
            double freqRatio = (double)i / (length - 1);
            double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);
            double x = FrequencyToX(freq, width);

            double mixDb = mix[i] > 0 ? 20 * Math.Log10(mix[i]) : MinDb;
            double refDb = reference[i] > 0 ? 20 * Math.Log10(reference[i]) : MinDb;

            double mixY = Math.Clamp(DbToY(mixDb, height), 0, height);
            double refY = Math.Clamp(DbToY(refDb, height), 0, height);

            double diff = mixDb - refDb;

            // Only show significant differences
            if (Math.Abs(diff) > SignificantDifferenceThreshold)
            {
                if (diff > 0)
                {
                    // Mix exceeds reference (mix is louder)
                    exceedsPoints.Add((x, mixY, refY));
                }
                else
                {
                    // Mix is below reference
                    belowPoints.Add((x, mixY, refY));
                }
            }
        }

        // Draw exceeds regions (red)
        DrawDeltaRegions(exceedsPoints, _exceedsColor, width, height);

        // Draw below regions (blue)
        DrawDeltaRegions(belowPoints, _belowColor, width, height);
    }

    private void DrawDeltaRegions(System.Collections.Generic.List<(double x, double mixY, double refY)> points, Color color, double width, double height)
    {
        if (points.Count < 2) return;

        // Group consecutive points into regions
        var currentRegion = new System.Collections.Generic.List<(double x, double mixY, double refY)>();

        for (int i = 0; i < points.Count; i++)
        {
            if (currentRegion.Count == 0)
            {
                currentRegion.Add(points[i]);
            }
            else
            {
                double lastX = currentRegion[^1].x;
                if (points[i].x - lastX < width / 20) // Close enough to be same region
                {
                    currentRegion.Add(points[i]);
                }
                else
                {
                    // Draw current region and start new one
                    DrawSingleDeltaRegion(currentRegion, color);
                    currentRegion.Clear();
                    currentRegion.Add(points[i]);
                }
            }
        }

        // Draw final region
        if (currentRegion.Count >= 2)
        {
            DrawSingleDeltaRegion(currentRegion, color);
        }
    }

    private void DrawSingleDeltaRegion(System.Collections.Generic.List<(double x, double mixY, double refY)> region, Color color)
    {
        if (region.Count < 2) return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            // Start at first point on mix curve
            context.BeginFigure(new Point(region[0].x, region[0].mixY), true, true);

            // Draw along mix curve
            for (int i = 1; i < region.Count; i++)
            {
                context.LineTo(new Point(region[i].x, region[i].mixY), true, false);
            }

            // Draw back along reference curve (in reverse)
            for (int i = region.Count - 1; i >= 0; i--)
            {
                context.LineTo(new Point(region[i].x, region[i].refY), true, false);
            }
        }

        geometry.Freeze();

        byte opacity = (byte)(OverlayOpacity * 100);
        var fillColor = Color.FromArgb(opacity, color.R, color.G, color.B);

        var path = new Shapes.Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(fillColor),
            Stroke = null
        };

        DeltaFillCanvas.Children.Add(path);
    }

    private float[] ApplyLevelMatching(float[] reference, float[] mix)
    {
        // Calculate RMS of both
        double mixRms = 0;
        double refRms = 0;

        for (int i = 0; i < mix.Length; i++)
        {
            mixRms += mix[i] * mix[i];
        }
        mixRms = Math.Sqrt(mixRms / mix.Length);

        for (int i = 0; i < reference.Length; i++)
        {
            refRms += reference[i] * reference[i];
        }
        refRms = Math.Sqrt(refRms / reference.Length);

        // Calculate gain factor
        float gain = mixRms > 0 && refRms > 0 ? (float)(mixRms / refRms) : 1.0f;

        // Apply gain
        var adjusted = new float[reference.Length];
        for (int i = 0; i < reference.Length; i++)
        {
            adjusted[i] = reference[i] * gain;
        }

        return adjusted;
    }

    private void UpdateBandIndicators(float[] mix, float[] reference)
    {
        if (_bandIndicators == null || _bandDiffTexts == null) return;

        _bandDifferences = new double[8];
        int length = Math.Min(mix.Length, reference.Length);

        for (int band = 0; band < 8; band++)
        {
            double sumDiff = 0;
            int count = 0;

            for (int i = 0; i < length; i++)
            {
                double freqRatio = (double)i / (length - 1);
                double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);

                if (freq >= BandStartFrequencies[band] && freq < BandEndFrequencies[band])
                {
                    double mixDb = mix[i] > 0 ? 20 * Math.Log10(mix[i]) : MinDb;
                    double refDb = reference[i] > 0 ? 20 * Math.Log10(reference[i]) : MinDb;
                    sumDiff += mixDb - refDb;
                    count++;
                }
            }

            double avgDiff = count > 0 ? sumDiff / count : 0;
            _bandDifferences[band] = avgDiff;

            // Update indicator color
            Color indicatorColor;
            if (Math.Abs(avgDiff) < SignificantDifferenceThreshold)
            {
                indicatorColor = _matchedColor;
            }
            else if (avgDiff > 0)
            {
                indicatorColor = _exceedsColor;
            }
            else
            {
                indicatorColor = _belowColor;
            }

            _bandIndicators[band].Background = new SolidColorBrush(indicatorColor);

            // Update dB difference text
            string diffText = avgDiff > 0 ? $"+{avgDiff:F0}" : $"{avgDiff:F0}";
            _bandDiffTexts[band].Text = diffText;
        }
    }

    private void ResetBandIndicators()
    {
        if (_bandIndicators == null || _bandDiffTexts == null) return;

        var neutralBrush = new SolidColorBrush(_neutralColor);
        for (int i = 0; i < 8; i++)
        {
            _bandIndicators[i].Background = neutralBrush;
            _bandDiffTexts[i].Text = "0";
        }
    }

    private void UpdateStatistics(float[] mix, float[] reference)
    {
        int length = Math.Min(mix.Length, reference.Length);

        double sumSquaredDiff = 0;
        double maxDiff = 0;
        double maxDiffFreq = 0;

        for (int i = 0; i < length; i++)
        {
            double freqRatio = (double)i / (length - 1);
            double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);

            double mixDb = mix[i] > 0 ? 20 * Math.Log10(mix[i]) : MinDb;
            double refDb = reference[i] > 0 ? 20 * Math.Log10(reference[i]) : MinDb;
            double diff = mixDb - refDb;

            sumSquaredDiff += diff * diff;

            if (Math.Abs(diff) > Math.Abs(maxDiff))
            {
                maxDiff = diff;
                maxDiffFreq = freq;
            }
        }

        double rmsDiff = Math.Sqrt(sumSquaredDiff / length);
        double similarity = Math.Max(0, 100 * (1.0 - rmsDiff / 20.0));

        // Update UI
        SimilarityText.Text = $"{similarity:F0}%";
        SimilarityText.Foreground = new SolidColorBrush(similarity > 80 ? _matchedColor :
                                                        similarity > 50 ? Color.FromRgb(0xFF, 0xC1, 0x07) :
                                                        _exceedsColor);

        RmsDiffText.Text = $"{rmsDiff:F1} dB";

        string freqText = maxDiffFreq >= 1000 ? $"{maxDiffFreq / 1000:F1}k" : $"{maxDiffFreq:F0}";
        string diffSign = maxDiff > 0 ? "+" : "";
        MaxDiffText.Text = $"{diffSign}{maxDiff:F1} dB @ {freqText} Hz";
    }

    private void ResetStatistics()
    {
        SimilarityText.Text = "--%";
        SimilarityText.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        RmsDiffText.Text = "-- dB";
        MaxDiffText.Text = "-- dB @ -- Hz";
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
    /// Updates both mix and reference spectrum data.
    /// </summary>
    /// <param name="mixSpectrum">Mix spectrum magnitudes (linear 0-1).</param>
    /// <param name="referenceSpectrum">Reference spectrum magnitudes (linear 0-1).</param>
    /// <param name="frequencies">Optional frequency array for each bin.</param>
    public void UpdateSpectrums(float[] mixSpectrum, float[]? referenceSpectrum, float[]? frequencies = null)
    {
        MixSpectrum = mixSpectrum;
        ReferenceSpectrum = referenceSpectrum;
        if (frequencies != null)
        {
            Frequencies = frequencies;
        }
    }

    /// <summary>
    /// Sets the reference spectrum from a SpectrumProfile.
    /// </summary>
    /// <param name="profile">The spectrum profile to use as reference.</param>
    public void SetReferenceFromProfile(SpectrumProfile profile)
    {
        if (!profile.IsValid) return;

        // Convert from dB to linear
        var linearMagnitudes = new float[profile.MagnitudesDb.Length];
        for (int i = 0; i < profile.MagnitudesDb.Length; i++)
        {
            linearMagnitudes[i] = (float)Math.Pow(10, profile.MagnitudesDb[i] / 20.0);
        }

        ReferenceSpectrum = linearMagnitudes;
        Frequencies = profile.Frequencies;
    }

    /// <summary>
    /// Updates from a SpectrumMatchResult.
    /// </summary>
    /// <param name="result">The match result containing both spectrums and analysis.</param>
    public void UpdateFromMatchResult(SpectrumMatchResult result)
    {
        _lastMatchResult = result;

        if (result.TargetSpectrum?.IsValid == true)
        {
            // Convert target (mix) from dB to linear
            var mixLinear = new float[result.TargetSpectrum.MagnitudesDb.Length];
            for (int i = 0; i < result.TargetSpectrum.MagnitudesDb.Length; i++)
            {
                mixLinear[i] = (float)Math.Pow(10, result.TargetSpectrum.MagnitudesDb[i] / 20.0);
            }
            MixSpectrum = mixLinear;
            Frequencies = result.TargetSpectrum.Frequencies;
        }

        if (result.ReferenceSpectrum?.IsValid == true)
        {
            // Convert reference from dB to linear
            var refLinear = new float[result.ReferenceSpectrum.MagnitudesDb.Length];
            for (int i = 0; i < result.ReferenceSpectrum.MagnitudesDb.Length; i++)
            {
                refLinear[i] = (float)Math.Pow(10, result.ReferenceSpectrum.MagnitudesDb[i] / 20.0);
            }
            ReferenceSpectrum = refLinear;
        }
    }

    /// <summary>
    /// Gets the band differences (mix - reference in dB).
    /// </summary>
    /// <returns>Array of 8 band differences in dB.</returns>
    public double[]? GetBandDifferences()
    {
        return _bandDifferences;
    }

    /// <summary>
    /// Gets the last match result if available.
    /// </summary>
    public SpectrumMatchResult? GetLastMatchResult()
    {
        return _lastMatchResult;
    }

    /// <summary>
    /// Clears all spectrum data.
    /// </summary>
    public void Clear()
    {
        MixSpectrum = null;
        ReferenceSpectrum = null;
        _lastMatchResult = null;
        ResetBandIndicators();
        ResetStatistics();
    }

    #endregion
}
