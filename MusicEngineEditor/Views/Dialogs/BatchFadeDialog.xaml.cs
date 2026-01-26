// MusicEngineEditor - Batch Fade Dialog
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Fade curve type.
/// </summary>
public enum FadeCurveType
{
    Linear,
    Logarithmic,
    Exponential,
    SCurve
}

/// <summary>
/// Settings for a single fade.
/// </summary>
public class FadeSettings
{
    /// <summary>
    /// Whether this fade is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Fade length in milliseconds.
    /// </summary>
    public double LengthMs { get; set; } = 100;

    /// <summary>
    /// Fade curve type.
    /// </summary>
    public FadeCurveType CurveType { get; set; } = FadeCurveType.Linear;
}

/// <summary>
/// Result from the batch fade dialog.
/// </summary>
public class BatchFadeResult
{
    /// <summary>
    /// Fade in settings.
    /// </summary>
    public FadeSettings FadeIn { get; set; } = new();

    /// <summary>
    /// Fade out settings.
    /// </summary>
    public FadeSettings FadeOut { get; set; } = new();

    /// <summary>
    /// Whether to create crossfades between adjacent clips.
    /// </summary>
    public bool CreateCrossfades { get; set; }

    /// <summary>
    /// Crossfade overlap in milliseconds.
    /// </summary>
    public double CrossfadeOverlapMs { get; set; } = 50;

    /// <summary>
    /// Whether to replace existing fades.
    /// </summary>
    public bool ReplaceExisting { get; set; } = true;
}

/// <summary>
/// Information about a clip for batch fade processing.
/// </summary>
public class BatchFadeClipInfo
{
    /// <summary>
    /// Clip identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Clip name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Start position in seconds.
    /// </summary>
    public double StartPosition { get; set; }

    /// <summary>
    /// End position in seconds.
    /// </summary>
    public double EndPosition { get; set; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public double Duration => EndPosition - StartPosition;

    /// <summary>
    /// Current fade in length in milliseconds (0 if none).
    /// </summary>
    public double CurrentFadeInMs { get; set; }

    /// <summary>
    /// Current fade out length in milliseconds (0 if none).
    /// </summary>
    public double CurrentFadeOutMs { get; set; }
}

/// <summary>
/// Dialog for applying fades to multiple clips at once.
/// </summary>
public partial class BatchFadeDialog : Window
{
    #region Private Fields

    private bool _isUpdating;
    private bool _linksEnabled;
    private List<BatchFadeClipInfo> _clips = [];

    #endregion

    #region Properties

    /// <summary>
    /// Gets the batch fade result.
    /// </summary>
    public BatchFadeResult Result { get; private set; } = new();

    /// <summary>
    /// Gets or sets the clips to process.
    /// </summary>
    public List<BatchFadeClipInfo> Clips
    {
        get => _clips;
        set
        {
            _clips = value ?? [];
            UpdateSelectionInfo();
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when preview is requested.
    /// </summary>
    public event EventHandler<BatchFadeResult>? PreviewRequested;

    #endregion

    #region Constructor

    public BatchFadeDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateSelectionInfo();
    }

    private void FadeEnabled_Changed(object sender, RoutedEventArgs e)
    {
        // Update UI state based on enabled checkboxes
    }

    private void FadeLength_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        _isUpdating = true;

        if (sender == FadeInLengthSlider)
        {
            FadeInLengthBox.Text = ((int)FadeInLengthSlider.Value).ToString();

            if (_linksEnabled)
            {
                FadeOutLengthSlider.Value = FadeInLengthSlider.Value;
                FadeOutLengthBox.Text = FadeInLengthBox.Text;
            }
        }
        else if (sender == FadeOutLengthSlider)
        {
            FadeOutLengthBox.Text = ((int)FadeOutLengthSlider.Value).ToString();

            if (_linksEnabled)
            {
                FadeInLengthSlider.Value = FadeOutLengthSlider.Value;
                FadeInLengthBox.Text = FadeOutLengthBox.Text;
            }
        }

        _isUpdating = false;
    }

    private void FadeInLengthBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (double.TryParse(FadeInLengthBox.Text, out double value))
        {
            _isUpdating = true;
            value = Math.Clamp(value, 1, 5000);
            FadeInLengthSlider.Value = value;

            if (_linksEnabled)
            {
                FadeOutLengthSlider.Value = value;
                FadeOutLengthBox.Text = ((int)value).ToString();
            }
            _isUpdating = false;
        }
    }

    private void FadeOutLengthBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (double.TryParse(FadeOutLengthBox.Text, out double value))
        {
            _isUpdating = true;
            value = Math.Clamp(value, 1, 5000);
            FadeOutLengthSlider.Value = value;

            if (_linksEnabled)
            {
                FadeInLengthSlider.Value = value;
                FadeInLengthBox.Text = ((int)value).ToString();
            }
            _isUpdating = false;
        }
    }

    private void FadeCurve_Changed(object sender, RoutedEventArgs e)
    {
        // Curve changed - update preview if auto-preview is enabled
    }

    private void LinkFades_Changed(object sender, RoutedEventArgs e)
    {
        _linksEnabled = LinkFadesCheck.IsChecked == true;

        if (_linksEnabled)
        {
            // Sync fade out to fade in
            FadeOutLengthSlider.Value = FadeInLengthSlider.Value;
            FadeOutLengthBox.Text = FadeInLengthBox.Text;
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        BuildResult();
        PreviewRequested?.Invoke(this, Result);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        BuildResult();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion

    #region Helper Methods

    private void UpdateSelectionInfo()
    {
        int clipCount = _clips.Count;
        string clipText = clipCount == 1 ? "clip" : "clips";
        SelectionInfoText.Text = $"Apply fades to {clipCount} selected {clipText}";
    }

    private void BuildResult()
    {
        Result = new BatchFadeResult
        {
            FadeIn = new FadeSettings
            {
                Enabled = FadeInEnabledCheck.IsChecked == true,
                LengthMs = FadeInLengthSlider.Value,
                CurveType = GetFadeInCurveType()
            },
            FadeOut = new FadeSettings
            {
                Enabled = FadeOutEnabledCheck.IsChecked == true,
                LengthMs = FadeOutLengthSlider.Value,
                CurveType = GetFadeOutCurveType()
            },
            CreateCrossfades = CreateCrossfadesCheck.IsChecked == true,
            CrossfadeOverlapMs = double.TryParse(CrossfadeOverlapBox.Text, out double overlap) ? overlap : 50,
            ReplaceExisting = ReplaceExistingCheck.IsChecked == true
        };
    }

    private FadeCurveType GetFadeInCurveType()
    {
        if (FadeInLog.IsChecked == true) return FadeCurveType.Logarithmic;
        if (FadeInExp.IsChecked == true) return FadeCurveType.Exponential;
        if (FadeInSCurve.IsChecked == true) return FadeCurveType.SCurve;
        return FadeCurveType.Linear;
    }

    private FadeCurveType GetFadeOutCurveType()
    {
        if (FadeOutLog.IsChecked == true) return FadeCurveType.Logarithmic;
        if (FadeOutExp.IsChecked == true) return FadeCurveType.Exponential;
        if (FadeOutSCurve.IsChecked == true) return FadeCurveType.SCurve;
        return FadeCurveType.Linear;
    }

    /// <summary>
    /// Calculates a fade curve value at a given position.
    /// </summary>
    /// <param name="t">Position (0-1).</param>
    /// <param name="curveType">Curve type.</param>
    /// <param name="isFadeIn">Whether this is a fade in (vs fade out).</param>
    /// <returns>Gain value (0-1).</returns>
    public static double CalculateFadeValue(double t, FadeCurveType curveType, bool isFadeIn)
    {
        t = Math.Clamp(t, 0, 1);

        double value = curveType switch
        {
            FadeCurveType.Linear => t,
            FadeCurveType.Logarithmic => isFadeIn
                ? 1 - Math.Pow(1 - t, 2)
                : Math.Pow(t, 2),
            FadeCurveType.Exponential => isFadeIn
                ? Math.Pow(t, 2)
                : 1 - Math.Pow(1 - t, 2),
            FadeCurveType.SCurve => t * t * (3 - 2 * t),
            _ => t
        };

        return isFadeIn ? value : 1 - value;
    }

    /// <summary>
    /// Applies fades to audio samples.
    /// </summary>
    /// <param name="samples">Audio samples to process.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="fadeIn">Fade in settings.</param>
    /// <param name="fadeOut">Fade out settings.</param>
    public static void ApplyFades(float[] samples, int sampleRate, int channels, FadeSettings fadeIn, FadeSettings fadeOut)
    {
        int totalSamples = samples.Length / channels;

        // Apply fade in
        if (fadeIn.Enabled && fadeIn.LengthMs > 0)
        {
            int fadeSamples = (int)(fadeIn.LengthMs * sampleRate / 1000.0);
            fadeSamples = Math.Min(fadeSamples, totalSamples / 2);

            for (int i = 0; i < fadeSamples; i++)
            {
                double t = (double)i / fadeSamples;
                double gain = CalculateFadeValue(t, fadeIn.CurveType, true);

                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i * channels + ch;
                    if (index < samples.Length)
                    {
                        samples[index] *= (float)gain;
                    }
                }
            }
        }

        // Apply fade out
        if (fadeOut.Enabled && fadeOut.LengthMs > 0)
        {
            int fadeSamples = (int)(fadeOut.LengthMs * sampleRate / 1000.0);
            fadeSamples = Math.Min(fadeSamples, totalSamples / 2);

            int startSample = totalSamples - fadeSamples;

            for (int i = 0; i < fadeSamples; i++)
            {
                double t = (double)i / fadeSamples;
                double gain = CalculateFadeValue(t, fadeOut.CurveType, false);

                for (int ch = 0; ch < channels; ch++)
                {
                    int index = (startSample + i) * channels + ch;
                    if (index < samples.Length)
                    {
                        samples[index] *= (float)gain;
                    }
                }
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the default fade lengths based on clip durations.
    /// </summary>
    /// <param name="defaultFadeMs">Default fade length in milliseconds.</param>
    public void SetDefaultFadeLength(double defaultFadeMs)
    {
        _isUpdating = true;
        FadeInLengthSlider.Value = defaultFadeMs;
        FadeInLengthBox.Text = ((int)defaultFadeMs).ToString();
        FadeOutLengthSlider.Value = defaultFadeMs;
        FadeOutLengthBox.Text = ((int)defaultFadeMs).ToString();
        _isUpdating = false;
    }

    #endregion
}
