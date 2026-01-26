// MusicEngineEditor - Audio Alignment Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Information about a track for alignment.
/// </summary>
public class AlignmentTrackInfo
{
    /// <summary>
    /// Track identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Track name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Audio samples (mono or interleaved stereo).
    /// </summary>
    public float[]? Samples { get; set; }

    /// <summary>
    /// Sample rate.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Number of channels.
    /// </summary>
    public int Channels { get; set; } = 1;

    /// <summary>
    /// Start position in the arrangement (seconds).
    /// </summary>
    public double StartPosition { get; set; }

    /// <summary>
    /// Whether this is the reference track.
    /// </summary>
    public bool IsReference { get; set; }
}

/// <summary>
/// Result of audio alignment analysis.
/// </summary>
public class AlignmentResult
{
    /// <summary>
    /// Track ID that was aligned.
    /// </summary>
    public Guid TrackId { get; set; }

    /// <summary>
    /// Offset in samples (positive = target is ahead, negative = target is behind).
    /// </summary>
    public int OffsetSamples { get; set; }

    /// <summary>
    /// Offset in milliseconds.
    /// </summary>
    public double OffsetMs { get; set; }

    /// <summary>
    /// Peak correlation value (0-1).
    /// </summary>
    public double Correlation { get; set; }

    /// <summary>
    /// Whether alignment was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if alignment failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Event args for alignment completed.
/// </summary>
public class AlignmentCompletedEventArgs : EventArgs
{
    public List<AlignmentResult> Results { get; set; } = [];
}

/// <summary>
/// Control for aligning audio takes to a reference track using cross-correlation.
/// </summary>
public partial class AudioAlignmentControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ReferenceTrackProperty =
        DependencyProperty.Register(nameof(ReferenceTrack), typeof(AlignmentTrackInfo), typeof(AudioAlignmentControl),
            new PropertyMetadata(null, OnReferenceTrackChanged));

    public static readonly DependencyProperty TargetTracksProperty =
        DependencyProperty.Register(nameof(TargetTracks), typeof(List<AlignmentTrackInfo>), typeof(AudioAlignmentControl),
            new PropertyMetadata(new List<AlignmentTrackInfo>(), OnTargetTracksChanged));

    /// <summary>
    /// Gets or sets the reference track.
    /// </summary>
    public AlignmentTrackInfo? ReferenceTrack
    {
        get => (AlignmentTrackInfo?)GetValue(ReferenceTrackProperty);
        set => SetValue(ReferenceTrackProperty, value);
    }

    /// <summary>
    /// Gets or sets the target tracks to align.
    /// </summary>
    public List<AlignmentTrackInfo> TargetTracks
    {
        get => (List<AlignmentTrackInfo>)GetValue(TargetTracksProperty);
        set => SetValue(TargetTracksProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when alignment analysis is complete.
    /// </summary>
    public event EventHandler<AlignmentCompletedEventArgs>? AlignmentCompleted;

    /// <summary>
    /// Raised when alignment should be applied.
    /// </summary>
    public event EventHandler<List<AlignmentResult>>? ApplyRequested;

    /// <summary>
    /// Raised when preview is requested.
    /// </summary>
    public event EventHandler<List<AlignmentResult>>? PreviewRequested;

    #endregion

    #region Private Fields

    private readonly List<AlignmentResult> _results = [];
    private CancellationTokenSource? _analysisCts;
    private int _selectedTargetIndex = 0;

    #endregion

    #region Constructor

    public AudioAlignmentControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDisplay();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDisplay();
    }

    private static void OnReferenceTrackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioAlignmentControl control)
        {
            control.UpdateTrackSelectors();
            control.UpdateDisplay();
        }
    }

    private static void OnTargetTracksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioAlignmentControl control)
        {
            control.UpdateTrackSelectors();
            control.UpdateDisplay();
        }
    }

    private void ReferenceTrack_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Handle reference track selection change
    }

    private void SetAsReference_Click(object sender, RoutedEventArgs e)
    {
        // Set selected target as reference
        if (TargetTracks.Count > 0 && _selectedTargetIndex < TargetTracks.Count)
        {
            var newRef = TargetTracks[_selectedTargetIndex];
            ReferenceTrack = newRef;
        }
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        await AnalyzeAlignmentAsync();
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count > 0)
        {
            PreviewRequested?.Invoke(this, _results.ToList());
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count > 0)
        {
            ApplyRequested?.Invoke(this, _results.ToList());
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _results.Clear();
        UpdateResultsDisplay();
        UpdateDisplay();
    }

    #endregion

    #region Analysis Methods

    /// <summary>
    /// Performs alignment analysis using cross-correlation.
    /// </summary>
    public async Task AnalyzeAlignmentAsync()
    {
        if (ReferenceTrack?.Samples == null || TargetTracks.Count == 0)
        {
            MessageBox.Show("Please select a reference track and target tracks.", "Missing Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();

        AnalyzeButton.IsEnabled = false;
        AnalyzeButton.Content = "Analyzing...";

        try
        {
            int maxOffsetMs = int.TryParse(MaxOffsetBox.Text, out int val) ? val : 500;
            int maxOffsetSamples = (int)(maxOffsetMs * ReferenceTrack.SampleRate / 1000.0);

            _results.Clear();

            foreach (var target in TargetTracks)
            {
                if (target.Samples == null) continue;

                var result = await Task.Run(() =>
                    CrossCorrelateAsync(ReferenceTrack.Samples, target.Samples,
                        ReferenceTrack.SampleRate, maxOffsetSamples, _analysisCts.Token),
                    _analysisCts.Token);

                result.TrackId = target.Id;
                result.OffsetMs = result.OffsetSamples * 1000.0 / ReferenceTrack.SampleRate;
                _results.Add(result);
            }

            UpdateResultsDisplay();
            UpdateDisplay();

            AlignmentCompleted?.Invoke(this, new AlignmentCompletedEventArgs { Results = _results.ToList() });
        }
        catch (OperationCanceledException)
        {
            // Analysis cancelled
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analysis failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            AnalyzeButton.IsEnabled = true;
            AnalyzeButton.Content = "Analyze";
        }
    }

    private static AlignmentResult CrossCorrelateAsync(float[] reference, float[] target, int sampleRate, int maxOffset, CancellationToken ct)
    {
        var result = new AlignmentResult();

        try
        {
            // Convert to mono if needed
            float[] refMono = ConvertToMono(reference);
            float[] targetMono = ConvertToMono(target);

            // Normalize
            Normalize(refMono);
            Normalize(targetMono);

            int minLength = Math.Min(refMono.Length, targetMono.Length);
            if (minLength < 100)
            {
                result.Success = false;
                result.ErrorMessage = "Audio too short for analysis";
                return result;
            }

            // Cross-correlation
            double bestCorrelation = -1;
            int bestOffset = 0;

            for (int offset = -maxOffset; offset <= maxOffset; offset++)
            {
                ct.ThrowIfCancellationRequested();

                double correlation = CalculateCorrelation(refMono, targetMono, offset);

                if (correlation > bestCorrelation)
                {
                    bestCorrelation = correlation;
                    bestOffset = offset;
                }
            }

            result.OffsetSamples = bestOffset;
            result.Correlation = bestCorrelation;
            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static float[] ConvertToMono(float[] samples)
    {
        // Assume stereo if even number of samples > 2
        if (samples.Length % 2 == 0 && samples.Length > 2)
        {
            var mono = new float[samples.Length / 2];
            for (int i = 0; i < mono.Length; i++)
            {
                mono[i] = (samples[i * 2] + samples[i * 2 + 1]) * 0.5f;
            }
            return mono;
        }
        return samples;
    }

    private static void Normalize(float[] samples)
    {
        float max = 0;
        foreach (var s in samples)
        {
            max = Math.Max(max, Math.Abs(s));
        }

        if (max > 0)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] /= max;
            }
        }
    }

    private static double CalculateCorrelation(float[] reference, float[] target, int offset)
    {
        double sum = 0;
        int count = 0;

        int refStart = Math.Max(0, offset);
        int targetStart = Math.Max(0, -offset);
        int length = Math.Min(reference.Length - refStart, target.Length - targetStart);

        for (int i = 0; i < length; i++)
        {
            sum += reference[refStart + i] * target[targetStart + i];
            count++;
        }

        return count > 0 ? sum / count : 0;
    }

    #endregion

    #region Display Methods

    private void UpdateTrackSelectors()
    {
        ReferenceTrackCombo.Items.Clear();
        ReferenceTrackCombo.Items.Add(new ComboBoxItem { Content = "(Select reference track)" });

        if (ReferenceTrack != null)
        {
            ReferenceTrackCombo.Items.Add(new ComboBoxItem
            {
                Content = ReferenceTrack.Name,
                Tag = ReferenceTrack.Id,
                IsSelected = true
            });
        }

        TargetTracksText.Text = $"{TargetTracks.Count} tracks selected";
    }

    private void UpdateResultsDisplay()
    {
        if (_results.Count == 0)
        {
            OffsetResultText.Text = "-- ms";
            CorrelationResultText.Text = "-- %";
            SamplesResultText.Text = "--";
            return;
        }

        // Show first result (or selected target)
        var result = _results.FirstOrDefault(r => r.Success);
        if (result != null)
        {
            string sign = result.OffsetMs >= 0 ? "+" : "";
            OffsetResultText.Text = $"{sign}{result.OffsetMs:F2} ms";
            CorrelationResultText.Text = $"{result.Correlation * 100:F1}%";
            SamplesResultText.Text = $"{(result.OffsetSamples >= 0 ? "+" : "")}{result.OffsetSamples}";

            // Color based on correlation quality
            var brush = result.Correlation switch
            {
                > 0.9 => (Brush)FindResource("ReferenceBrush"),
                > 0.7 => (Brush)FindResource("AccentBrush"),
                > 0.5 => (Brush)FindResource("TargetBrush"),
                _ => Brushes.Red
            };
            CorrelationResultText.Foreground = brush;
        }
    }

    private void UpdateDisplay()
    {
        DrawReferenceWaveform();
        DrawTargetWaveform();
        DrawCorrelation();
    }

    private void DrawReferenceWaveform()
    {
        ReferenceWaveformCanvas.Children.Clear();

        if (ReferenceTrack?.Samples == null) return;

        double width = ReferenceWaveformCanvas.ActualWidth;
        double height = ReferenceWaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        DrawWaveform(ReferenceWaveformCanvas, ReferenceTrack.Samples, (Color)FindResource("ReferenceColor"), 0);
    }

    private void DrawTargetWaveform()
    {
        TargetWaveformCanvas.Children.Clear();
        AlignedWaveformCanvas.Children.Clear();

        if (TargetTracks.Count == 0) return;

        var target = TargetTracks.FirstOrDefault();
        if (target?.Samples == null) return;

        double width = TargetWaveformCanvas.ActualWidth;
        double height = TargetWaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Draw original target
        DrawWaveform(TargetWaveformCanvas, target.Samples, (Color)FindResource("TargetColor"), 0);

        // Draw aligned version if we have results
        var result = _results.FirstOrDefault(r => r.TrackId == target.Id && r.Success);
        if (result != null)
        {
            DrawWaveform(AlignedWaveformCanvas, target.Samples, (Color)FindResource("AlignedColor"), result.OffsetSamples);
        }
    }

    private static void DrawWaveform(Canvas canvas, float[] samples, Color color, int offsetSamples)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        int samplesPerPixel = Math.Max(1, samples.Length / (int)width);
        var points = new PointCollection();

        for (int x = 0; x < (int)width; x++)
        {
            int sampleIndex = x * samplesPerPixel + offsetSamples;
            if (sampleIndex < 0 || sampleIndex >= samples.Length) continue;

            float maxVal = 0;
            for (int i = 0; i < samplesPerPixel && (sampleIndex + i) < samples.Length; i++)
            {
                if (sampleIndex + i >= 0)
                {
                    maxVal = Math.Max(maxVal, Math.Abs(samples[sampleIndex + i]));
                }
            }

            double y = height / 2 - (maxVal * height / 2 * 0.8);
            points.Add(new Point(x, y));
        }

        // Draw upper half
        var polyline = new Shapes.Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(color) { Opacity = 0.7 },
            StrokeThickness = 1.5
        };
        canvas.Children.Add(polyline);

        // Draw lower half (mirrored)
        var lowerPoints = new PointCollection();
        foreach (var pt in points)
        {
            lowerPoints.Add(new Point(pt.X, height - pt.Y));
        }

        var lowerPolyline = new Shapes.Polyline
        {
            Points = lowerPoints,
            Stroke = new SolidColorBrush(color) { Opacity = 0.7 },
            StrokeThickness = 1.5
        };
        canvas.Children.Add(lowerPolyline);
    }

    private void DrawCorrelation()
    {
        CorrelationCanvas.Children.Clear();

        // Draw a simple correlation indicator
        double width = CorrelationCanvas.ActualWidth;
        double height = CorrelationCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        if (_results.Count > 0)
        {
            var result = _results.FirstOrDefault(r => r.Success);
            if (result != null)
            {
                // Draw correlation bar
                double barWidth = width * result.Correlation;
                var rect = new Shapes.Rectangle
                {
                    Width = barWidth,
                    Height = height - 20,
                    Fill = new SolidColorBrush((Color)FindResource("AccentColor")) { Opacity = 0.5 },
                    RadiusX = 2,
                    RadiusY = 2
                };
                Canvas.SetLeft(rect, 0);
                Canvas.SetTop(rect, 15);
                CorrelationCanvas.Children.Add(rect);

                // Draw offset indicator
                double centerX = width / 2;
                double offsetX = centerX + (result.OffsetSamples / 1000.0) * (width / 2);

                var line = new Shapes.Line
                {
                    X1 = offsetX,
                    Y1 = 15,
                    X2 = offsetX,
                    Y2 = height,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                CorrelationCanvas.Children.Add(line);
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the tracks for alignment.
    /// </summary>
    /// <param name="reference">Reference track.</param>
    /// <param name="targets">Target tracks to align.</param>
    public void SetTracks(AlignmentTrackInfo reference, List<AlignmentTrackInfo> targets)
    {
        ReferenceTrack = reference;
        TargetTracks = targets;
    }

    /// <summary>
    /// Gets the alignment results.
    /// </summary>
    public List<AlignmentResult> GetResults() => _results.ToList();

    #endregion
}
