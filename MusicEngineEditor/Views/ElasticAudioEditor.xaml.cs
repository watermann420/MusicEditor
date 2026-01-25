using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngine.Core;
using MusicEngine.Core.Analysis;
using MusicEngine.Core.Warp;

using AnalysisWarpMarker = MusicEngine.Core.Analysis.WarpMarker;

namespace MusicEngineEditor.Views;

/// <summary>
/// Elastic Audio Editor view for editing warp markers and time-stretching audio clips.
/// Provides BPM detection, tempo matching, and marker manipulation.
/// </summary>
public partial class ElasticAudioEditor : UserControl
{
    #region Fields

    private AudioClip? _clip;
    private double _projectBpm = 120.0;
    private bool _isUpdatingUi;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the audio clip being edited.
    /// </summary>
    public new AudioClip? Clip
    {
        get => _clip;
        set => LoadClip(value);
    }

    /// <summary>
    /// Gets or sets the project tempo in BPM.
    /// </summary>
    public double ProjectBpm
    {
        get => _projectBpm;
        set
        {
            _projectBpm = Math.Clamp(value, 20, 300);
            if (!_isUpdatingUi)
            {
                ProjectBpmBox.Text = _projectBpm.ToString("F1");
            }
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the clip is modified.
    /// </summary>
    public event EventHandler? ClipModified;

    #endregion

    #region Constructor

    public ElasticAudioEditor()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    #endregion

    #region Initialization

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateStatus();
    }

    #endregion

    #region Clip Loading

    /// <summary>
    /// Loads an audio clip for editing.
    /// </summary>
    /// <param name="clip">The audio clip to edit.</param>
    public void LoadClip(AudioClip? clip)
    {
        _clip = clip;

        if (clip == null)
        {
            ClipNameText.Text = " - No clip loaded";
            WarpEnableToggle.IsChecked = false;
            WarpMarkerEditor.ClearMarkers();
            UpdateStatus();
            return;
        }

        _isUpdatingUi = true;

        try
        {
            // Update clip name
            ClipNameText.Text = $" - {clip.Name}";

            // Enable warping if not already
            if (clip.WarpProcessor == null)
            {
                clip.EnableWarping(true);
            }

            // Update BPM displays
            OriginalBpmBox.Text = clip.OriginalBpm.ToString("F1");
            if (clip.DetectedBpm.HasValue)
            {
                OriginalBpmBox.Text = clip.DetectedBpm.Value.ToString("F1");
            }

            // Set project BPM from clip's warp processor
            if (clip.WarpProcessor != null)
            {
                _projectBpm = clip.WarpProcessor.Bpm;
                ProjectBpmBox.Text = _projectBpm.ToString("F1");
            }

            // Update warp enable toggle
            WarpEnableToggle.IsChecked = clip.IsWarpEnabled;

            // Set algorithm selection
            if (clip.WarpProcessor != null)
            {
                AlgorithmCombo.SelectedIndex = (int)clip.WarpProcessor.DefaultAlgorithm;
            }

            // Connect warp marker control
            ConnectWarpMarkerEditor();
        }
        finally
        {
            _isUpdatingUi = false;
        }

        UpdateStatus();
    }

    /// <summary>
    /// Connects the warp marker editor control to the clip's warp processor.
    /// </summary>
    private void ConnectWarpMarkerEditor()
    {
        if (_clip?.WarpProcessor == null) return;

        var processor = _clip.WarpProcessor;

        // Calculate duration in seconds
        double duration = (double)processor.TotalOriginalSamples / _clip.SampleRate;
        WarpMarkerEditor.TotalDuration = duration;
        WarpMarkerEditor.Bpm = processor.Bpm;

        // Convert warp markers to editor markers
        var editorMarkers = processor.Markers.Select(m => new AnalysisWarpMarker
        {
            TimePosition = m.GetOriginalPositionSeconds(_clip.SampleRate),
            BeatPosition = m.GetOriginalPositionBeats(_clip.SampleRate, processor.Bpm),
            IsDownbeat = m.MarkerType == WarpMarkerType.Start || m.MarkerType == WarpMarkerType.Beat,
            IsManual = m.MarkerType == WarpMarkerType.User,
            Confidence = m.TransientStrength
        }).ToList();

        WarpMarkerEditor.SetMarkers(editorMarkers);
    }

    #endregion

    #region Event Handlers

    private void WarpEnableToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_clip == null) return;

        if (WarpEnableToggle.IsChecked == true)
        {
            _clip.EnableWarping(false);
            StatusText.Text = "Warping enabled";
        }
        else
        {
            _clip.DisableWarping();
            StatusText.Text = "Warping disabled";
        }

        ClipModified?.Invoke(this, EventArgs.Empty);
    }

    private void DetectBpm_Click(object sender, RoutedEventArgs e)
    {
        if (_clip == null) return;

        StatusText.Text = "Detecting tempo...";

        try
        {
            double? bpm = _clip.DetectTempo();

            if (bpm.HasValue)
            {
                OriginalBpmBox.Text = bpm.Value.ToString("F1");
                _clip.OriginalBpm = bpm.Value;

                if (_clip.WarpProcessor != null)
                {
                    _clip.WarpProcessor.Bpm = bpm.Value;
                    WarpMarkerEditor.Bpm = bpm.Value;
                }

                StatusText.Text = $"Detected BPM: {bpm.Value:F1}";
                ClipModified?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusText.Text = "Could not detect tempo - try a longer sample";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error detecting tempo: {ex.Message}";
        }

        UpdateStatus();
    }

    private void MatchBpm_Click(object sender, RoutedEventArgs e)
    {
        if (_clip == null) return;

        if (!double.TryParse(ProjectBpmBox.Text, out double projectBpm))
        {
            StatusText.Text = "Invalid project BPM value";
            return;
        }

        _projectBpm = Math.Clamp(projectBpm, 20, 300);
        ProjectBpmBox.Text = _projectBpm.ToString("F1");

        try
        {
            _clip.QuantizeToTempo(_projectBpm, 0.25);
            ConnectWarpMarkerEditor();
            StatusText.Text = $"Matched to {_projectBpm:F1} BPM";
            ClipModified?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error matching tempo: {ex.Message}";
        }

        UpdateStatus();
    }

    private void OriginalBpmBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_clip == null) return;

        if (double.TryParse(OriginalBpmBox.Text, out double bpm))
        {
            bpm = Math.Clamp(bpm, 20, 300);
            _clip.OriginalBpm = bpm;
            OriginalBpmBox.Text = bpm.ToString("F1");
            ClipModified?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            OriginalBpmBox.Text = _clip.OriginalBpm.ToString("F1");
        }
    }

    private void ProjectBpmBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(ProjectBpmBox.Text, out double bpm))
        {
            _projectBpm = Math.Clamp(bpm, 20, 300);
            ProjectBpmBox.Text = _projectBpm.ToString("F1");

            if (_clip?.WarpProcessor != null)
            {
                _clip.WarpProcessor.Bpm = _projectBpm;
                WarpMarkerEditor.Bpm = _projectBpm;
                UpdateStatus();
            }
        }
        else
        {
            ProjectBpmBox.Text = _projectBpm.ToString("F1");
        }
    }

    private void BpmBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Move focus to trigger LostFocus event
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void AlgorithmCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_clip?.WarpProcessor == null || _isUpdatingUi) return;

        _clip.WarpProcessor.DefaultAlgorithm = (WarpAlgorithm)AlgorithmCombo.SelectedIndex;
        StatusText.Text = $"Algorithm changed to {(WarpAlgorithm)AlgorithmCombo.SelectedIndex}";
        ClipModified?.Invoke(this, EventArgs.Empty);
    }

    private void WarpMarkerEditor_MarkerAdded(object? sender, MusicEngine.Core.Analysis.WarpMarker e)
    {
        if (_clip?.WarpProcessor == null) return;

        // Convert editor marker to warp processor marker
        long positionSamples = (long)(e.TimePosition * _clip.SampleRate);
        _clip.WarpProcessor.AddMarker(positionSamples, positionSamples, WarpMarkerType.User);

        StatusText.Text = $"Added marker at {e.TimePosition:F3}s";
        ClipModified?.Invoke(this, EventArgs.Empty);
        UpdateStatus();
    }

    private void WarpMarkerEditor_MarkerDeleted(object? sender, MusicEngine.Core.Analysis.WarpMarker e)
    {
        if (_clip?.WarpProcessor == null) return;

        // Find and remove corresponding warp processor marker
        long positionSamples = (long)(e.TimePosition * _clip.SampleRate);
        var marker = _clip.WarpProcessor.GetMarkerNear(positionSamples, (long)(_clip.SampleRate * 0.01));

        if (marker != null)
        {
            _clip.WarpProcessor.RemoveMarker(marker);
            StatusText.Text = "Marker deleted";
            ClipModified?.Invoke(this, EventArgs.Empty);
        }

        UpdateStatus();
    }

    private void WarpMarkerEditor_MarkerMoved(object? sender, MusicEngine.Core.Analysis.WarpMarker e)
    {
        if (_clip?.WarpProcessor == null) return;

        // This is handled by the WarpMarkerEditor internally
        // We just need to update the status
        StatusText.Text = $"Marker moved to {e.TimePosition:F3}s";
        ClipModified?.Invoke(this, EventArgs.Empty);
        UpdateStatus();
    }

    private void WarpMarkerEditor_AutoDetectRequested(object? sender, EventArgs e)
    {
        if (_clip == null) return;

        StatusText.Text = "Detecting transients...";

        try
        {
            if (_clip.AudioData != null)
            {
                // Convert to mono for transient detection if stereo
                float[] monoData;
                if (_clip.Channels == 2)
                {
                    monoData = new float[_clip.AudioData.Length / 2];
                    for (int i = 0; i < monoData.Length; i++)
                    {
                        monoData[i] = (_clip.AudioData[i * 2] + _clip.AudioData[i * 2 + 1]) * 0.5f;
                    }
                }
                else
                {
                    monoData = _clip.AudioData;
                }

                // Clear existing transient markers
                _clip.WarpProcessor?.ClearTransientMarkers();

                // Detect transients
                var markers = _clip.WarpProcessor?.DetectTransients(monoData);

                // Refresh the warp marker editor
                ConnectWarpMarkerEditor();

                int count = markers?.Count ?? 0;
                StatusText.Text = $"Detected {count} transients";
                ClipModified?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusText.Text = "No audio data available for transient detection";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error detecting transients: {ex.Message}";
        }

        UpdateStatus();
    }

    #endregion

    #region Status Updates

    /// <summary>
    /// Updates the status bar with current warp information.
    /// </summary>
    private void UpdateStatus()
    {
        if (_clip?.WarpProcessor == null)
        {
            MarkerCountRun.Text = "0";
            StretchRangeRun.Text = "1.00x";
            DurationChangeRun.Text = "100%";
            return;
        }

        var processor = _clip.WarpProcessor;
        var markers = processor.Markers;

        // Update marker count
        MarkerCountRun.Text = markers.Count.ToString();

        // Calculate stretch range
        double minStretch = 1.0;
        double maxStretch = 1.0;

        foreach (var region in processor.Regions)
        {
            double stretch = region.StretchRatio;
            minStretch = Math.Min(minStretch, stretch);
            maxStretch = Math.Max(maxStretch, stretch);
        }

        if (Math.Abs(minStretch - maxStretch) < 0.001)
        {
            StretchRangeRun.Text = $"{minStretch:F2}x";
        }
        else
        {
            StretchRangeRun.Text = $"{minStretch:F2}x - {maxStretch:F2}x";
        }

        // Calculate duration change
        double originalDuration = (double)processor.TotalOriginalSamples;
        double warpedDuration = (double)processor.TotalWarpedSamples;

        if (originalDuration > 0)
        {
            double durationPercent = (warpedDuration / originalDuration) * 100;
            DurationChangeRun.Text = $"{durationPercent:F0}%";
        }
        else
        {
            DurationChangeRun.Text = "100%";
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the editor display.
    /// </summary>
    public void Refresh()
    {
        if (_clip != null)
        {
            ConnectWarpMarkerEditor();
        }
        UpdateStatus();
    }

    /// <summary>
    /// Resets the warp markers to their original positions.
    /// </summary>
    public void ResetWarp()
    {
        if (_clip?.WarpProcessor != null)
        {
            _clip.ResetWarp();
            ConnectWarpMarkerEditor();
            StatusText.Text = "Warp markers reset";
            ClipModified?.Invoke(this, EventArgs.Empty);
            UpdateStatus();
        }
    }

    /// <summary>
    /// Clears all user-placed warp markers.
    /// </summary>
    public void ClearUserMarkers()
    {
        if (_clip?.WarpProcessor != null)
        {
            _clip.ClearUserWarpMarkers();
            ConnectWarpMarkerEditor();
            StatusText.Text = "User markers cleared";
            ClipModified?.Invoke(this, EventArgs.Empty);
            UpdateStatus();
        }
    }

    #endregion
}
