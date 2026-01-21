// MusicEngineEditor - Recording Setup Dialog
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for configuring multi-track recording settings before starting recording.
/// </summary>
public partial class RecordingSetupDialog : Window, INotifyPropertyChanged, IDisposable
{
    private readonly RecordingService _recordingService;
    private bool _disposed;

    #region Properties

    /// <summary>
    /// Gets the collection of armed tracks.
    /// </summary>
    public ObservableCollection<ArmedTrackInfo> ArmedTracks { get; } = [];

    /// <summary>
    /// Gets the count of armed tracks.
    /// </summary>
    public int ArmedTrackCount => ArmedTracks.Count;

    /// <summary>
    /// Gets whether any tracks are armed.
    /// </summary>
    public bool HasArmedTracks => ArmedTracks.Count > 0;

    /// <summary>
    /// Gets whether recording can be started.
    /// </summary>
    public bool CanRecord => HasArmedTracks;

    private int _sampleRate = 44100;
    /// <summary>
    /// Gets or sets the sample rate.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            if (_sampleRate != value)
            {
                _sampleRate = value;
                OnPropertyChanged();
            }
        }
    }

    private int _bitDepth = 16;
    /// <summary>
    /// Gets or sets the bit depth.
    /// </summary>
    public int BitDepth
    {
        get => _bitDepth;
        set
        {
            if (_bitDepth != value)
            {
                _bitDepth = value;
                OnPropertyChanged();
            }
        }
    }

    private int _channels = 2;
    /// <summary>
    /// Gets or sets the number of channels.
    /// </summary>
    public int Channels
    {
        get => _channels;
        set
        {
            if (_channels != value)
            {
                _channels = value;
                OnPropertyChanged();
            }
        }
    }

    private int _countInBarsIndex = 1;
    /// <summary>
    /// Gets or sets the count-in bars index (0=none, 1=1bar, 2=2bars, 3=4bars).
    /// </summary>
    public int CountInBarsIndex
    {
        get => _countInBarsIndex;
        set
        {
            if (_countInBarsIndex != value)
            {
                _countInBarsIndex = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the count-in bars value.
    /// </summary>
    public int CountInBars => CountInBarsIndex switch
    {
        0 => 0,
        1 => 1,
        2 => 2,
        3 => 4,
        _ => 1
    };

    private bool _clickTrackEnabled = true;
    /// <summary>
    /// Gets or sets whether click track is enabled.
    /// </summary>
    public bool ClickTrackEnabled
    {
        get => _clickTrackEnabled;
        set
        {
            if (_clickTrackEnabled != value)
            {
                _clickTrackEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _playbackDuringRecording = true;
    /// <summary>
    /// Gets or sets whether to play existing tracks during recording.
    /// </summary>
    public bool PlaybackDuringRecording
    {
        get => _playbackDuringRecording;
        set
        {
            if (_playbackDuringRecording != value)
            {
                _playbackDuringRecording = value;
                OnPropertyChanged();
            }
        }
    }

    private string _recordingsPath = "";
    /// <summary>
    /// Gets or sets the recordings path.
    /// </summary>
    public string RecordingsPath
    {
        get => _recordingsPath;
        set
        {
            if (_recordingsPath != value)
            {
                _recordingsPath = value;
                OnPropertyChanged();
            }
        }
    }

    private string _statusMessage = "Ready to record";
    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the dialog result indicating whether recording should start.
    /// </summary>
    public bool ShouldStartRecording { get; private set; }

    #endregion

    /// <summary>
    /// Creates a new RecordingSetupDialog.
    /// </summary>
    public RecordingSetupDialog()
    {
        InitializeComponent();
        DataContext = this;

        _recordingService = RecordingService.Instance;
        RecordingsPath = _recordingService.ProjectRecordingsPath;

        // Load settings from service
        SampleRate = _recordingService.SampleRate;
        BitDepth = _recordingService.BitDepth;
        Channels = _recordingService.Channels;
        CountInBarsIndex = _recordingService.CountInBars switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            4 => 3,
            _ => 1
        };
        ClickTrackEnabled = _recordingService.ClickTrackEnabled;

        // Subscribe to events
        _recordingService.TrackArmed += OnTrackArmedChanged;
        _recordingService.TrackDisarmed += OnTrackArmedChanged;
        _recordingService.InputLevelsUpdated += OnInputLevelsUpdated;

        // Load armed tracks
        RefreshArmedTracks();
    }

    /// <summary>
    /// Refreshes the armed tracks list.
    /// </summary>
    private void RefreshArmedTracks()
    {
        ArmedTracks.Clear();

        foreach (var track in _recordingService.ArmedTracksList)
        {
            ArmedTracks.Add(track);
        }

        OnPropertyChanged(nameof(ArmedTrackCount));
        OnPropertyChanged(nameof(HasArmedTracks));
        OnPropertyChanged(nameof(CanRecord));

        StatusMessage = HasArmedTracks
            ? $"Ready to record {ArmedTrackCount} track(s)"
            : "No tracks armed for recording";
    }

    private void OnTrackArmedChanged(object? sender, TrackArmEventArgs e)
    {
        Dispatcher.Invoke(RefreshArmedTracks);
    }

    private void OnInputLevelsUpdated(object? sender, EventArgs e)
    {
        // Update levels in the UI thread
        Dispatcher.Invoke(() =>
        {
            foreach (var track in ArmedTracks)
            {
                var serviceTrack = _recordingService.GetArmedTrack(track.TrackId);
                if (serviceTrack != null)
                {
                    track.InputLevel = serviceTrack.InputLevel;
                    track.IsClipping = serviceTrack.IsClipping;
                }
            }
        });
    }

    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select recordings folder",
            SelectedPath = RecordingsPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            RecordingsPath = dialog.SelectedPath;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ShouldStartRecording = false;
        DialogResult = false;
        Close();
    }

    private void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (!CanRecord)
        {
            StatusMessage = "No tracks are armed for recording";
            return;
        }

        // Apply settings to service
        _recordingService.SampleRate = SampleRate;
        _recordingService.BitDepth = BitDepth;
        _recordingService.Channels = Channels;
        _recordingService.CountInBars = CountInBars;
        _recordingService.ClickTrackEnabled = ClickTrackEnabled;
        _recordingService.ProjectRecordingsPath = RecordingsPath;

        ShouldStartRecording = true;
        DialogResult = true;
        Close();
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _recordingService.TrackArmed -= OnTrackArmedChanged;
        _recordingService.TrackDisarmed -= OnTrackArmedChanged;
        _recordingService.InputLevelsUpdated -= OnInputLevelsUpdated;
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }
}
