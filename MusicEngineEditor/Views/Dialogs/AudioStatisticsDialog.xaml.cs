// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using NAudio.Wave;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for analyzing and displaying audio file statistics.
/// </summary>
public partial class AudioStatisticsDialog : Window
{
    #region Fields

    private string? _audioFilePath;
    private AudioAnalysisResult? _analysisResult;
    private CancellationTokenSource? _analysisCts;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the path to the audio file to analyze.
    /// </summary>
    public string? AudioFilePath
    {
        get => _audioFilePath;
        set
        {
            _audioFilePath = value;
            UpdateFilePathDisplay();
        }
    }

    /// <summary>
    /// Gets the analysis result after analysis is complete.
    /// </summary>
    public AudioAnalysisResult? AnalysisResult => _analysisResult;

    #endregion

    #region Constructor

    public AudioStatisticsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates a new dialog with a pre-selected audio file.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file to analyze.</param>
    public AudioStatisticsDialog(string audioFilePath) : this()
    {
        AudioFilePath = audioFilePath;
    }

    #endregion

    #region Event Handlers

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Audio File",
            Filter = "Audio Files|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.aif|WAV Files|*.wav|MP3 Files|*.mp3|FLAC Files|*.flac|All Files|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            AudioFilePath = dialog.FileName;
        }
    }

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AudioFilePath) || !File.Exists(AudioFilePath))
        {
            MessageBox.Show("Please select a valid audio file.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await AnalyzeAudioAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _analysisCts?.Cancel();
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        base.OnClosed(e);
    }

    #endregion

    #region Analysis Methods

    private async Task AnalyzeAudioAsync()
    {
        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();
        var token = _analysisCts.Token;

        try
        {
            SetAnalyzing(true);
            ClearResults();

            _analysisResult = await Task.Run(() => PerformAnalysis(AudioFilePath!, token), token);

            if (!token.IsCancellationRequested)
            {
                DisplayResults(_analysisResult);
            }
        }
        catch (OperationCanceledException)
        {
            // Analysis was cancelled
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error analyzing audio: {ex.Message}", "Analysis Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetAnalyzing(false);
        }
    }

    private static AudioAnalysisResult PerformAnalysis(string filePath, CancellationToken token)
    {
        var result = new AudioAnalysisResult();

        using var reader = new AudioFileReader(filePath);

        // File information
        result.Duration = reader.TotalTime;
        result.SampleRate = reader.WaveFormat.SampleRate;
        result.BitDepth = reader.WaveFormat.BitsPerSample;
        result.Channels = reader.WaveFormat.Channels;

        // Analysis buffers
        var bufferSize = 4096;
        var buffer = new float[bufferSize];
        var samplesRead = 0L;
        var totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);

        double peak = 0;
        double sumSquares = 0;
        double dcSum = 0;
        double minSample = double.MaxValue;
        double maxSample = double.MinValue;

        // For True Peak (simple 4x oversampling approximation)
        var truePeak = 0.0;
        var prevSamples = new float[4];
        var prevIndex = 0;

        int read;
        while ((read = reader.Read(buffer, 0, bufferSize)) > 0)
        {
            token.ThrowIfCancellationRequested();

            for (int i = 0; i < read; i++)
            {
                var sample = buffer[i];
                var absSample = Math.Abs(sample);

                // Peak
                if (absSample > peak)
                    peak = absSample;

                // RMS
                sumSquares += sample * sample;

                // DC offset
                dcSum += sample;

                // Min/Max for dynamic range
                if (sample < minSample) minSample = sample;
                if (sample > maxSample) maxSample = sample;

                // Simple True Peak estimation using linear interpolation
                prevSamples[prevIndex] = sample;
                prevIndex = (prevIndex + 1) % 4;

                // Interpolate between samples to find intersample peaks
                if (samplesRead > 4)
                {
                    for (int j = 1; j <= 3; j++)
                    {
                        var t = j / 4.0;
                        var s0 = prevSamples[(prevIndex + 0) % 4];
                        var s1 = prevSamples[(prevIndex + 1) % 4];
                        var s2 = prevSamples[(prevIndex + 2) % 4];
                        var s3 = prevSamples[(prevIndex + 3) % 4];

                        // Cubic interpolation
                        var interpolated = CubicInterpolate(s0, s1, s2, s3, t);
                        var absInterpolated = Math.Abs(interpolated);
                        if (absInterpolated > truePeak)
                            truePeak = absInterpolated;
                    }
                }

                samplesRead++;
            }
        }

        // Calculate final statistics
        if (samplesRead > 0)
        {
            // Peak level in dBFS
            result.PeakLevel = peak > 0 ? 20.0 * Math.Log10(peak) : -144.0;

            // True Peak in dBTP
            if (truePeak < peak) truePeak = peak; // Ensure true peak is at least as high as sample peak
            result.TruePeak = truePeak > 0 ? 20.0 * Math.Log10(truePeak) : -144.0;

            // RMS level in dBFS
            var rms = Math.Sqrt(sumSquares / samplesRead);
            result.RmsLevel = rms > 0 ? 20.0 * Math.Log10(rms) : -144.0;

            // DC offset as percentage
            var dcOffset = dcSum / samplesRead;
            result.DcOffset = dcOffset * 100.0;

            // Crest factor (Peak / RMS) in dB
            result.CrestFactor = result.PeakLevel - result.RmsLevel;

            // Dynamic range (difference between loudest and softest meaningful signals)
            // Using a simplified approach: Peak to RMS difference with noise floor consideration
            result.DynamicRange = Math.Max(0, result.CrestFactor + 3.0); // Approximation
        }

        return result;
    }

    private static double CubicInterpolate(double y0, double y1, double y2, double y3, double t)
    {
        var a0 = y3 - y2 - y0 + y1;
        var a1 = y0 - y1 - a0;
        var a2 = y2 - y0;
        var a3 = y1;

        var t2 = t * t;
        var t3 = t2 * t;

        return a0 * t3 + a1 * t2 + a2 * t + a3;
    }

    #endregion

    #region UI Methods

    private void UpdateFilePathDisplay()
    {
        if (string.IsNullOrEmpty(_audioFilePath))
        {
            FilePathText.Text = "No file selected";
            AnalyzeButton.IsEnabled = false;
        }
        else
        {
            FilePathText.Text = _audioFilePath;
            AnalyzeButton.IsEnabled = File.Exists(_audioFilePath);
        }
    }

    private void SetAnalyzing(bool analyzing)
    {
        ProgressPanel.Visibility = analyzing ? Visibility.Visible : Visibility.Collapsed;
        AnalyzeButton.IsEnabled = !analyzing && !string.IsNullOrEmpty(AudioFilePath);
        BrowseButton.IsEnabled = !analyzing;
    }

    private void ClearResults()
    {
        DurationValue.Text = "--:--";
        SampleRateValue.Text = "--";
        BitDepthValue.Text = "--";
        ChannelsValue.Text = "--";

        PeakLevelValue.Text = "--";
        TruePeakValue.Text = "--";
        RmsLevelValue.Text = "--";
        DcOffsetValue.Text = "--";

        CrestFactorValue.Text = "--";
        DynamicRangeValue.Text = "--";
    }

    private void DisplayResults(AudioAnalysisResult result)
    {
        // File information
        DurationValue.Text = FormatDuration(result.Duration);
        SampleRateValue.Text = $"{result.SampleRate:N0}";
        BitDepthValue.Text = result.BitDepth.ToString();
        ChannelsValue.Text = result.Channels == 1 ? "Mono" : result.Channels == 2 ? "Stereo" : $"{result.Channels}ch";

        // Level statistics
        PeakLevelValue.Text = FormatDb(result.PeakLevel);
        TruePeakValue.Text = FormatDb(result.TruePeak);
        RmsLevelValue.Text = FormatDb(result.RmsLevel);
        DcOffsetValue.Text = $"{result.DcOffset:F4}";

        // Dynamics
        CrestFactorValue.Text = $"{result.CrestFactor:F1}";
        DynamicRangeValue.Text = $"{result.DynamicRange:F1}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        return $"{duration.Minutes}:{duration.Seconds:D2}";
    }

    private static string FormatDb(double db)
    {
        if (db <= -100)
            return "-inf";
        return $"{db:F2}";
    }

    #endregion
}

/// <summary>
/// Contains the results of audio file analysis.
/// </summary>
public class AudioAnalysisResult
{
    /// <summary>
    /// Gets or sets the duration of the audio file.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the sample rate in Hz.
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Gets or sets the bit depth.
    /// </summary>
    public int BitDepth { get; set; }

    /// <summary>
    /// Gets or sets the number of channels.
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// Gets or sets the peak level in dBFS.
    /// </summary>
    public double PeakLevel { get; set; }

    /// <summary>
    /// Gets or sets the true peak level in dBTP.
    /// </summary>
    public double TruePeak { get; set; }

    /// <summary>
    /// Gets or sets the RMS level in dBFS.
    /// </summary>
    public double RmsLevel { get; set; }

    /// <summary>
    /// Gets or sets the DC offset as a percentage.
    /// </summary>
    public double DcOffset { get; set; }

    /// <summary>
    /// Gets or sets the crest factor (Peak - RMS) in dB.
    /// </summary>
    public double CrestFactor { get; set; }

    /// <summary>
    /// Gets or sets the dynamic range in dB.
    /// </summary>
    public double DynamicRange { get; set; }
}
