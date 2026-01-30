// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control for AI-based stem separation.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using MusicEngine.Core.Analysis;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Stem separation panel with quality presets, progress tracking, and stem playback/export controls.
/// Provides UI for separating audio into vocals, drums, bass, and other stems.
/// </summary>
public partial class StemSeparationPanel : UserControl
{
    private CancellationTokenSource? _separationCts;
    private StemSeparationResult? _separationResult;
    private SeparationQuality _quality = SeparationQuality.Medium;
    private string _filePath = string.Empty;

    #region Dependency Properties

    /// <summary>
    /// Identifies the Quality dependency property.
    /// </summary>
    public static readonly DependencyProperty QualityProperty =
        DependencyProperty.Register(
            nameof(Quality),
            typeof(SeparationQuality),
            typeof(StemSeparationPanel),
            new PropertyMetadata(SeparationQuality.Medium, OnQualityChanged));

    /// <summary>
    /// Identifies the FilePath dependency property.
    /// </summary>
    public static readonly DependencyProperty FilePathProperty =
        DependencyProperty.Register(
            nameof(FilePath),
            typeof(string),
            typeof(StemSeparationPanel),
            new PropertyMetadata(string.Empty, OnFilePathChanged));

    /// <summary>
    /// Identifies the IsSeparating dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSeparatingProperty =
        DependencyProperty.Register(
            nameof(IsSeparating),
            typeof(bool),
            typeof(StemSeparationPanel),
            new PropertyMetadata(false));

    /// <summary>
    /// Identifies the HasResult dependency property.
    /// </summary>
    public static readonly DependencyProperty HasResultProperty =
        DependencyProperty.Register(
            nameof(HasResult),
            typeof(bool),
            typeof(StemSeparationPanel),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets the separation quality preset.
    /// </summary>
    public SeparationQuality Quality
    {
        get => (SeparationQuality)GetValue(QualityProperty);
        set => SetValue(QualityProperty, value);
    }

    /// <summary>
    /// Gets or sets the source audio file path.
    /// </summary>
    public string FilePath
    {
        get => (string)GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    /// <summary>
    /// Gets whether separation is currently in progress.
    /// </summary>
    public bool IsSeparating
    {
        get => (bool)GetValue(IsSeparatingProperty);
        private set => SetValue(IsSeparatingProperty, value);
    }

    /// <summary>
    /// Gets whether separation results are available.
    /// </summary>
    public bool HasResult
    {
        get => (bool)GetValue(HasResultProperty);
        private set => SetValue(HasResultProperty, value);
    }

    private static void OnQualityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StemSeparationPanel panel)
        {
            panel._quality = (SeparationQuality)e.NewValue;
            panel.UpdateQualityComboBox();
        }
    }

    private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StemSeparationPanel panel)
        {
            panel._filePath = (string)e.NewValue ?? string.Empty;
            panel.FilePathTextBox.Text = panel._filePath;
            panel.UpdateSeparateButtonState();
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when stem separation completes successfully.
    /// </summary>
    public event EventHandler<StemSeparationCompletedEventArgs>? SeparationCompleted;

    /// <summary>
    /// Event raised when export is requested for a stem.
    /// </summary>
    public event EventHandler<StemExportRequestedEventArgs>? ExportRequested;

    /// <summary>
    /// Event raised when "Use Selected Clip" is clicked.
    /// </summary>
    public event EventHandler? UseSelectedClipRequested;

    /// <summary>
    /// Event raised when stem volume/mute/solo changes.
    /// </summary>
    public event EventHandler<StemMixChangedEventArgs>? StemMixChanged;

    #endregion

    /// <summary>
    /// Creates a new stem separation panel.
    /// </summary>
    public StemSeparationPanel()
    {
        InitializeComponent();
        UpdateSeparateButtonState();
    }

    #region UI Event Handlers

    private void QualityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QualityComboBox.SelectedItem is ComboBoxItem item && item.Tag is string qualityTag)
        {
            if (Enum.TryParse<SeparationQuality>(qualityTag, out var quality))
            {
                _quality = quality;
                Quality = quality;

                string description = quality switch
                {
                    SeparationQuality.Fast => "Fast processing, lower quality",
                    SeparationQuality.Medium => "Balanced speed and quality",
                    SeparationQuality.High => "Slower processing, higher quality",
                    SeparationQuality.Ultra => "Highest quality, slowest processing",
                    _ => ""
                };
                StatusText.Text = description;
            }
        }
    }

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
            FilePath = dialog.FileName;
            _filePath = dialog.FileName;
            FilePathTextBox.Text = dialog.FileName;
            StatusText.Text = $"Selected: {Path.GetFileName(dialog.FileName)}";
            UpdateSeparateButtonState();
        }
    }

    private void UseSelectedClipButton_Click(object sender, RoutedEventArgs e)
    {
        UseSelectedClipRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void SeparateButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
        {
            StatusText.Text = "Please select a valid audio file";
            return;
        }

        await SeparateAsync(_filePath);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelSeparation();
    }

    private void SoloButton_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button)
        {
            var stemType = GetStemTypeFromButton(button);
            if (stemType.HasValue)
            {
                StemMixChanged?.Invoke(this, new StemMixChangedEventArgs(
                    stemType.Value,
                    StemMixChangeType.Solo,
                    button.IsChecked == true ? 1.0 : 0.0));
            }
        }
    }

    private void MuteButton_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button)
        {
            var stemType = GetStemTypeFromButton(button);
            if (stemType.HasValue)
            {
                StemMixChanged?.Invoke(this, new StemMixChangedEventArgs(
                    stemType.Value,
                    StemMixChangeType.Mute,
                    button.IsChecked == true ? 1.0 : 0.0));
            }
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider)
        {
            // Update volume text display
            UpdateVolumeText(slider);

            // Raise event
            var stemType = GetStemTypeFromSlider(slider);
            if (stemType.HasValue)
            {
                StemMixChanged?.Invoke(this, new StemMixChangedEventArgs(
                    stemType.Value,
                    StemMixChangeType.Volume,
                    e.NewValue / 100.0));
            }
        }
    }

    private void ExportStemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string stemTag)
        {
            if (Enum.TryParse<StemType>(stemTag, out var stemType))
            {
                ExportStem(stemType);
            }
        }
    }

    private void ExportAllButton_Click(object sender, RoutedEventArgs e)
    {
        ExportAllStems();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the file path from an external source (e.g., selected clip).
    /// </summary>
    public void SetFilePath(string path)
    {
        FilePath = path;
        _filePath = path;
        FilePathTextBox.Text = path;
        StatusText.Text = $"Selected: {Path.GetFileName(path)}";
        UpdateSeparateButtonState();
    }

    /// <summary>
    /// Starts stem separation asynchronously.
    /// </summary>
    public async Task SeparateAsync(string audioPath)
    {
        if (IsSeparating)
        {
            StatusText.Text = "Separation already in progress";
            return;
        }

        // Clean up previous result
        _separationResult?.Dispose();
        _separationResult = null;
        HasResult = false;

        // Cancel any existing operation
        _separationCts?.Cancel();
        _separationCts = new CancellationTokenSource();

        try
        {
            IsSeparating = true;
            ShowProgressUI(true);
            UpdateSeparateButtonState();

            var separator = new StemSeparation(_quality);
            var progress = new Progress<StemSeparationProgress>(UpdateProgress);

            _separationResult = await separator.SeparateAsync(audioPath, progress, _separationCts.Token);

            HasResult = true;
            ShowStemControls(true);
            StatusText.Text = "Separation complete. Use controls to preview and export stems.";

            SeparationCompleted?.Invoke(this, new StemSeparationCompletedEventArgs(_separationResult));
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Separation cancelled";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            HasResult = false;
        }
        finally
        {
            IsSeparating = false;
            ShowProgressUI(false);
            UpdateSeparateButtonState();
        }
    }

    /// <summary>
    /// Cancels the current separation operation.
    /// </summary>
    public void CancelSeparation()
    {
        _separationCts?.Cancel();
        StatusText.Text = "Cancelling...";
    }

    /// <summary>
    /// Exports a specific stem to a file.
    /// </summary>
    public void ExportStem(StemType stemType)
    {
        if (_separationResult == null)
        {
            StatusText.Text = "No separation result available";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = $"Export {stemType} Stem",
            Filter = "WAV Files|*.wav",
            FileName = $"{Path.GetFileNameWithoutExtension(_filePath)}_{stemType.ToString().ToLower()}.wav"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _separationResult.ExportStem(stemType, dialog.FileName);
                StatusText.Text = $"Exported {stemType} to {Path.GetFileName(dialog.FileName)}";

                ExportRequested?.Invoke(this, new StemExportRequestedEventArgs(stemType, dialog.FileName));
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Export error: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Exports all stems to a folder.
    /// </summary>
    public void ExportAllStems()
    {
        if (_separationResult == null)
        {
            StatusText.Text = "No separation result available";
            return;
        }

        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to export all stems",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                string baseName = Path.GetFileNameWithoutExtension(_filePath);
                _separationResult.ExportAllStems(dialog.SelectedPath, baseName);
                StatusText.Text = $"All stems exported to {dialog.SelectedPath}";

                ExportRequested?.Invoke(this, new StemExportRequestedEventArgs(null, dialog.SelectedPath));
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Export error: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Gets the current separation result.
    /// </summary>
    public StemSeparationResult? GetResult() => _separationResult;

    /// <summary>
    /// Gets the volume level for a stem (0-100).
    /// </summary>
    public double GetStemVolume(StemType stemType)
    {
        return stemType switch
        {
            StemType.Vocals => VocalsVolumeSlider.Value,
            StemType.Drums => DrumsVolumeSlider.Value,
            StemType.Bass => BassVolumeSlider.Value,
            StemType.Other => OtherVolumeSlider.Value,
            _ => 100
        };
    }

    /// <summary>
    /// Gets whether a stem is soloed.
    /// </summary>
    public bool IsStemSoloed(StemType stemType)
    {
        return stemType switch
        {
            StemType.Vocals => VocalsSoloButton.IsChecked == true,
            StemType.Drums => DrumsSoloButton.IsChecked == true,
            StemType.Bass => BassSoloButton.IsChecked == true,
            StemType.Other => OtherSoloButton.IsChecked == true,
            _ => false
        };
    }

    /// <summary>
    /// Gets whether a stem is muted.
    /// </summary>
    public bool IsStemMuted(StemType stemType)
    {
        return stemType switch
        {
            StemType.Vocals => VocalsMuteButton.IsChecked == true,
            StemType.Drums => DrumsMuteButton.IsChecked == true,
            StemType.Bass => BassMuteButton.IsChecked == true,
            StemType.Other => OtherMuteButton.IsChecked == true,
            _ => false
        };
    }

    /// <summary>
    /// Resets the panel to initial state.
    /// </summary>
    public void Reset()
    {
        CancelSeparation();

        _separationResult?.Dispose();
        _separationResult = null;

        FilePath = string.Empty;
        _filePath = string.Empty;
        FilePathTextBox.Text = string.Empty;

        HasResult = false;
        IsSeparating = false;

        ShowProgressUI(false);
        ShowStemControls(false);

        // Reset all volume sliders
        VocalsVolumeSlider.Value = 100;
        DrumsVolumeSlider.Value = 100;
        BassVolumeSlider.Value = 100;
        OtherVolumeSlider.Value = 100;

        // Reset all solo/mute buttons
        VocalsSoloButton.IsChecked = false;
        VocalsMuteButton.IsChecked = false;
        DrumsSoloButton.IsChecked = false;
        DrumsMuteButton.IsChecked = false;
        BassSoloButton.IsChecked = false;
        BassMuteButton.IsChecked = false;
        OtherSoloButton.IsChecked = false;
        OtherMuteButton.IsChecked = false;

        StatusText.Text = "Select an audio file or clip to separate";
        UpdateSeparateButtonState();
    }

    #endregion

    #region Private Helpers

    private void UpdateProgress(StemSeparationProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            SeparationProgressBar.Value = progress.OverallProgress * 100;
            ProgressPercentText.Text = $"{progress.OverallProgress * 100:0}%";
            ProgressPhaseText.Text = progress.CurrentStem.HasValue
                ? $"{progress.CurrentPhase} ({progress.CurrentStem})"
                : progress.CurrentPhase;
        });
    }

    private void ShowProgressUI(bool show)
    {
        ProgressPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SeparateButton.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowStemControls(bool show)
    {
        StemControlsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSeparateButtonState()
    {
        SeparateButton.IsEnabled = !string.IsNullOrEmpty(_filePath) && File.Exists(_filePath) && !IsSeparating;
    }

    private void UpdateQualityComboBox()
    {
        for (int i = 0; i < QualityComboBox.Items.Count; i++)
        {
            if (QualityComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is string tag &&
                Enum.TryParse<SeparationQuality>(tag, out var quality) &&
                quality == _quality)
            {
                QualityComboBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void UpdateVolumeText(Slider slider)
    {
        TextBlock? volumeText = slider.Name switch
        {
            nameof(VocalsVolumeSlider) => VocalsVolumeText,
            nameof(DrumsVolumeSlider) => DrumsVolumeText,
            nameof(BassVolumeSlider) => BassVolumeText,
            nameof(OtherVolumeSlider) => OtherVolumeText,
            _ => null
        };

        if (volumeText != null)
        {
            volumeText.Text = $"{slider.Value:0}%";
        }
    }

    private StemType? GetStemTypeFromButton(ToggleButton button)
    {
        return button.Name switch
        {
            nameof(VocalsSoloButton) or nameof(VocalsMuteButton) => StemType.Vocals,
            nameof(DrumsSoloButton) or nameof(DrumsMuteButton) => StemType.Drums,
            nameof(BassSoloButton) or nameof(BassMuteButton) => StemType.Bass,
            nameof(OtherSoloButton) or nameof(OtherMuteButton) => StemType.Other,
            _ => null
        };
    }

    private StemType? GetStemTypeFromSlider(Slider slider)
    {
        return slider.Name switch
        {
            nameof(VocalsVolumeSlider) => StemType.Vocals,
            nameof(DrumsVolumeSlider) => StemType.Drums,
            nameof(BassVolumeSlider) => StemType.Bass,
            nameof(OtherVolumeSlider) => StemType.Other,
            _ => null
        };
    }

    #endregion
}

#region Event Args

/// <summary>
/// Event arguments for stem separation completed.
/// </summary>
public class StemSeparationCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the separation result.
    /// </summary>
    public StemSeparationResult Result { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public StemSeparationCompletedEventArgs(StemSeparationResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Event arguments for stem export requested.
/// </summary>
public class StemExportRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the stem type being exported, or null for all stems.
    /// </summary>
    public StemType? StemType { get; }

    /// <summary>
    /// Gets the export path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public StemExportRequestedEventArgs(StemType? stemType, string path)
    {
        StemType = stemType;
        Path = path;
    }
}

/// <summary>
/// Types of stem mix changes.
/// </summary>
public enum StemMixChangeType
{
    /// <summary>Volume change</summary>
    Volume,
    /// <summary>Mute state change</summary>
    Mute,
    /// <summary>Solo state change</summary>
    Solo
}

/// <summary>
/// Event arguments for stem mix changes.
/// </summary>
public class StemMixChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the stem type that changed.
    /// </summary>
    public StemType StemType { get; }

    /// <summary>
    /// Gets the type of change.
    /// </summary>
    public StemMixChangeType ChangeType { get; }

    /// <summary>
    /// Gets the new value (0-1 for volume, 0 or 1 for mute/solo).
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public StemMixChangedEventArgs(StemType stemType, StemMixChangeType changeType, double value)
    {
        StemType = stemType;
        ChangeType = changeType;
        Value = value;
    }
}

#endregion
