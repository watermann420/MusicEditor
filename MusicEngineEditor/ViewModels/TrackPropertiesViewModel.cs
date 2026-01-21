// MusicEngineEditor - Track Properties ViewModel
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Track Properties Panel.
/// Provides binding for track properties and commands for track operations.
/// </summary>
public partial class TrackPropertiesViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private TrackInfo? _selectedTrack;
    private readonly RecordingService _recordingService;
    private bool _disposed;

    #endregion

    #region Recording Properties

    /// <summary>
    /// Gets or sets the current input level (0.0 to 1.0) for armed tracks.
    /// </summary>
    [ObservableProperty]
    private float _inputLevel;

    /// <summary>
    /// Gets or sets the input level in dB for display.
    /// </summary>
    [ObservableProperty]
    private float _inputLevelDb = -60f;

    /// <summary>
    /// Gets or sets whether the input is clipping.
    /// </summary>
    [ObservableProperty]
    private bool _isClipping;

    /// <summary>
    /// Gets or sets whether recording is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isRecording;

    /// <summary>
    /// Gets whether input level should be shown (when track is armed).
    /// </summary>
    public bool ShowInputLevel => IsArmed && !IsRecording;

    /// <summary>
    /// Gets the input level display text.
    /// </summary>
    public string InputLevelDisplay => InputLevelDb <= -60f ? "-Inf dB" : $"{InputLevelDb:F1} dB";

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a track property has changed.
    /// </summary>
    public event EventHandler<TrackPropertyChangedEventArgs>? TrackPropertyChanged;

    /// <summary>
    /// Occurs when a track should be duplicated.
    /// </summary>
    public event EventHandler<TrackEventArgs>? TrackDuplicateRequested;

    /// <summary>
    /// Occurs when a track should be deleted.
    /// </summary>
    public event EventHandler<TrackEventArgs>? TrackDeleteRequested;

    /// <summary>
    /// Occurs when a track should be frozen/unfrozen.
    /// </summary>
    public event EventHandler<TrackEventArgs>? TrackFreezeRequested;

    /// <summary>
    /// Occurs when a track color was changed.
    /// </summary>
    public event EventHandler<TrackColorChangedEventArgs>? TrackColorChanged;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new TrackPropertiesViewModel.
    /// </summary>
    public TrackPropertiesViewModel()
    {
        _recordingService = RecordingService.Instance;

        // Subscribe to recording service events
        _recordingService.InputLevelsUpdated += OnInputLevelsUpdated;
        _recordingService.RecordingStateChanged += OnRecordingStateChanged;
        _recordingService.TrackArmed += OnTrackArmedChanged;
        _recordingService.TrackDisarmed += OnTrackArmedChanged;

        InitializeOptions();
    }

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the currently selected track.
    /// </summary>
    public TrackInfo? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (_selectedTrack != value)
            {
                // Unsubscribe from old track
                if (_selectedTrack != null)
                {
                    _selectedTrack.PropertyChanged -= OnSelectedTrackPropertyChanged;
                }

                _selectedTrack = value;

                // Subscribe to new track
                if (_selectedTrack != null)
                {
                    _selectedTrack.PropertyChanged += OnSelectedTrackPropertyChanged;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedTrack));
                OnPropertyChanged(nameof(TrackName));
                OnPropertyChanged(nameof(TrackColor));
                OnPropertyChanged(nameof(TrackType));
                OnPropertyChanged(nameof(IsArmed));
                OnPropertyChanged(nameof(IsMonitoring));
                OnPropertyChanged(nameof(IsFrozen));
                OnPropertyChanged(nameof(IsMuted));
                OnPropertyChanged(nameof(IsSoloed));
                OnPropertyChanged(nameof(TrackHeight));
                OnPropertyChanged(nameof(MidiChannel));
                OnPropertyChanged(nameof(ShowMidiOptions));
                OnPropertyChanged(nameof(ShowAudioOptions));
                OnPropertyChanged(nameof(CanFreeze));
                OnPropertyChanged(nameof(CanArm));
                OnPropertyChanged(nameof(InstrumentName));
                OnPropertyChanged(nameof(Volume));
                OnPropertyChanged(nameof(Pan));
                UpdateCommandStates();
            }
        }
    }

    /// <summary>
    /// Gets whether a track is selected.
    /// </summary>
    public bool HasSelectedTrack => SelectedTrack != null;

    /// <summary>
    /// Gets or sets the track name.
    /// </summary>
    public string TrackName
    {
        get => SelectedTrack?.Name ?? "";
        set
        {
            if (SelectedTrack != null && SelectedTrack.Name != value)
            {
                var oldValue = SelectedTrack.Name;
                SelectedTrack.Name = value;
                OnPropertyChanged();
                RaiseTrackPropertyChanged(nameof(TrackName), oldValue, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the track color.
    /// </summary>
    public string TrackColor
    {
        get => SelectedTrack?.Color ?? "#4A9EFF";
        set
        {
            if (SelectedTrack != null && SelectedTrack.Color != value)
            {
                var oldValue = SelectedTrack.Color;
                SelectedTrack.Color = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedColorOption));
                TrackColorChanged?.Invoke(this, new TrackColorChangedEventArgs(SelectedTrack, oldValue, value));
            }
        }
    }

    /// <summary>
    /// Gets the track type.
    /// </summary>
    public TrackType TrackType => SelectedTrack?.TrackType ?? Models.TrackType.Instrument;

    /// <summary>
    /// Gets or sets whether the track is armed.
    /// </summary>
    public bool IsArmed
    {
        get => SelectedTrack?.IsArmed ?? false;
        set
        {
            if (SelectedTrack != null && SelectedTrack.IsArmed != value)
            {
                SelectedTrack.IsArmed = value;
                OnPropertyChanged();
                RaiseTrackPropertyChanged(nameof(IsArmed), !value, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether input monitoring is enabled.
    /// </summary>
    public bool IsMonitoring
    {
        get => SelectedTrack?.IsMonitoring ?? false;
        set
        {
            if (SelectedTrack != null && SelectedTrack.IsMonitoring != value)
            {
                SelectedTrack.IsMonitoring = value;
                OnPropertyChanged();
                RaiseTrackPropertyChanged(nameof(IsMonitoring), !value, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the track is frozen.
    /// </summary>
    public bool IsFrozen
    {
        get => SelectedTrack?.IsFrozen ?? false;
        set
        {
            if (SelectedTrack != null && SelectedTrack.IsFrozen != value)
            {
                SelectedTrack.IsFrozen = value;
                OnPropertyChanged();
                RaiseTrackPropertyChanged(nameof(IsFrozen), !value, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the track is muted.
    /// </summary>
    public bool IsMuted
    {
        get => SelectedTrack?.IsMuted ?? false;
        set
        {
            if (SelectedTrack != null && SelectedTrack.IsMuted != value)
            {
                SelectedTrack.IsMuted = value;
                OnPropertyChanged();
                RaiseTrackPropertyChanged(nameof(IsMuted), !value, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the track is soloed.
    /// </summary>
    public bool IsSoloed
    {
        get => SelectedTrack?.IsSoloed ?? false;
        set
        {
            if (SelectedTrack != null && SelectedTrack.IsSoloed != value)
            {
                SelectedTrack.IsSoloed = value;
                OnPropertyChanged();
                RaiseTrackPropertyChanged(nameof(IsSoloed), !value, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the track height.
    /// </summary>
    public double TrackHeight
    {
        get => SelectedTrack?.Height ?? 80;
        set
        {
            if (SelectedTrack != null && Math.Abs(SelectedTrack.Height - value) > 0.1)
            {
                var oldValue = SelectedTrack.Height;
                SelectedTrack.Height = value;
                OnPropertyChanged();
                RaiseTrackPropertyChanged(nameof(TrackHeight), oldValue, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the MIDI channel (0-16, 0 = Omni).
    /// </summary>
    public int MidiChannel
    {
        get => SelectedTrack?.MidiChannel ?? 0;
        set
        {
            if (SelectedTrack != null && SelectedTrack.MidiChannel != value)
            {
                var oldValue = SelectedTrack.MidiChannel;
                SelectedTrack.MidiChannel = value;
                OnPropertyChanged();
                RaiseTrackPropertyChanged(nameof(MidiChannel), oldValue, value);
            }
        }
    }

    /// <summary>
    /// Gets the instrument name.
    /// </summary>
    public string InstrumentName => SelectedTrack?.InstrumentName ?? "None";

    /// <summary>
    /// Gets or sets the track volume.
    /// </summary>
    public float Volume
    {
        get => SelectedTrack?.Volume ?? 0.8f;
        set
        {
            if (SelectedTrack != null && Math.Abs(SelectedTrack.Volume - value) > 0.001f)
            {
                var oldValue = SelectedTrack.Volume;
                SelectedTrack.Volume = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumeDb));
                RaiseTrackPropertyChanged(nameof(Volume), oldValue, value);
            }
        }
    }

    /// <summary>
    /// Gets the volume in decibels.
    /// </summary>
    public string VolumeDb
    {
        get
        {
            if (Volume <= 0) return "-Inf dB";
            var db = 20.0 * Math.Log10(Volume);
            return $"{db:F1} dB";
        }
    }

    /// <summary>
    /// Gets or sets the track pan.
    /// </summary>
    public float Pan
    {
        get => SelectedTrack?.Pan ?? 0f;
        set
        {
            if (SelectedTrack != null && Math.Abs(SelectedTrack.Pan - value) > 0.001f)
            {
                var oldValue = SelectedTrack.Pan;
                SelectedTrack.Pan = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PanDisplay));
                RaiseTrackPropertyChanged(nameof(Pan), oldValue, value);
            }
        }
    }

    /// <summary>
    /// Gets the pan display text.
    /// </summary>
    public string PanDisplay
    {
        get
        {
            if (Math.Abs(Pan) < 0.01f) return "C";
            return Pan < 0 ? $"{(int)(Pan * -100)}L" : $"{(int)(Pan * 100)}R";
        }
    }

    /// <summary>
    /// Gets whether MIDI options should be shown.
    /// </summary>
    public bool ShowMidiOptions => SelectedTrack?.CanHaveMidiChannel ?? false;

    /// <summary>
    /// Gets whether audio input options should be shown.
    /// </summary>
    public bool ShowAudioOptions => SelectedTrack?.CanHaveAudioInput ?? false;

    /// <summary>
    /// Gets whether the track can be frozen.
    /// </summary>
    public bool CanFreeze => SelectedTrack?.CanFreeze ?? false;

    /// <summary>
    /// Gets whether the track can be armed.
    /// </summary>
    public bool CanArm => SelectedTrack?.CanArm ?? false;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the available input source options.
    /// </summary>
    public ObservableCollection<InputSourceOption> InputSources { get; } = [];

    /// <summary>
    /// Gets the available output bus options.
    /// </summary>
    public ObservableCollection<OutputBusOption> OutputBuses { get; } = [];

    /// <summary>
    /// Gets the available audio input options.
    /// </summary>
    public ObservableCollection<AudioInputOption> AudioInputs { get; } = [];

    /// <summary>
    /// Gets the available color preset options.
    /// </summary>
    public ObservableCollection<TrackColorOption> ColorOptions { get; } = [];

    /// <summary>
    /// Gets the available MIDI channel options.
    /// </summary>
    public ObservableCollection<MidiChannelOption> MidiChannelOptions { get; } = [];

    /// <summary>
    /// Gets the selected input source.
    /// </summary>
    [ObservableProperty]
    private InputSourceOption? _selectedInputSource;

    /// <summary>
    /// Gets the selected output bus.
    /// </summary>
    [ObservableProperty]
    private OutputBusOption? _selectedOutputBus;

    /// <summary>
    /// Gets the selected audio input.
    /// </summary>
    [ObservableProperty]
    private AudioInputOption? _selectedAudioInput;

    /// <summary>
    /// Gets the selected color option.
    /// </summary>
    public TrackColorOption? SelectedColorOption
    {
        get
        {
            foreach (var option in ColorOptions)
            {
                if (option.Color == TrackColor) return option;
            }
            return null;
        }
        set
        {
            if (value != null)
            {
                TrackColor = value.Color;
            }
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to rename the track.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void Rename()
    {
        // The actual rename is done through the TrackName property binding
        // This command can be used to trigger a rename dialog if needed
    }

    /// <summary>
    /// Command to set the track color.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void SetColor(string color)
    {
        if (!string.IsNullOrEmpty(color))
        {
            TrackColor = color;
        }
    }

    /// <summary>
    /// Command to toggle arm for recording.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanArm))]
    private void ToggleArm()
    {
        if (SelectedTrack == null || IsRecording) return;

        IsArmed = !IsArmed;

        // Update recording service
        if (IsArmed)
        {
            _recordingService.ArmTrack(
                SelectedTrack.Id,
                SelectedTrack.Name,
                SelectedTrack.Color,
                SelectedTrack.InputSource,
                SelectedTrack.InputSourceName);
        }
        else
        {
            _recordingService.DisarmTrack(SelectedTrack.Id);
        }

        OnPropertyChanged(nameof(ShowInputLevel));
    }

    /// <summary>
    /// Command to toggle input monitoring.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void ToggleMonitor()
    {
        IsMonitoring = !IsMonitoring;
    }

    /// <summary>
    /// Command to toggle mute.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    /// <summary>
    /// Command to toggle solo.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void ToggleSolo()
    {
        IsSoloed = !IsSoloed;
    }

    /// <summary>
    /// Command to freeze/unfreeze the track.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanFreeze))]
    private void ToggleFreeze()
    {
        if (SelectedTrack != null)
        {
            IsFrozen = !IsFrozen;
            TrackFreezeRequested?.Invoke(this, new TrackEventArgs(SelectedTrack));
        }
    }

    /// <summary>
    /// Command to duplicate the track.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void Duplicate()
    {
        if (SelectedTrack != null)
        {
            TrackDuplicateRequested?.Invoke(this, new TrackEventArgs(SelectedTrack));
        }
    }

    /// <summary>
    /// Command to delete the track.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void Delete()
    {
        if (SelectedTrack != null)
        {
            TrackDeleteRequested?.Invoke(this, new TrackEventArgs(SelectedTrack));
        }
    }

    /// <summary>
    /// Command to reset the track to defaults.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void ResetTrack()
    {
        SelectedTrack?.Reset();
        RefreshAllProperties();
    }

    /// <summary>
    /// Command to reset volume to unity.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void ResetVolume()
    {
        Volume = 0.8f;
    }

    /// <summary>
    /// Command to reset pan to center.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTrack))]
    private void ResetPan()
    {
        Pan = 0f;
    }

    #endregion

    #region Private Methods

    private void InitializeOptions()
    {
        // Initialize color options
        ColorOptions.Add(new TrackColorOption { Color = "#4A9EFF", Name = "Blue" });
        ColorOptions.Add(new TrackColorOption { Color = "#6AAB73", Name = "Green" });
        ColorOptions.Add(new TrackColorOption { Color = "#E89C4B", Name = "Orange" });
        ColorOptions.Add(new TrackColorOption { Color = "#FF5555", Name = "Red" });
        ColorOptions.Add(new TrackColorOption { Color = "#9C7CE8", Name = "Purple" });
        ColorOptions.Add(new TrackColorOption { Color = "#FF69B4", Name = "Pink" });
        ColorOptions.Add(new TrackColorOption { Color = "#FFDD55", Name = "Yellow" });
        ColorOptions.Add(new TrackColorOption { Color = "#55DDFF", Name = "Cyan" });
        ColorOptions.Add(new TrackColorOption { Color = "#BCBEC4", Name = "Gray" });
        ColorOptions.Add(new TrackColorOption { Color = "#FFFFFF", Name = "White" });

        // Initialize MIDI channel options
        MidiChannelOptions.Add(new MidiChannelOption { Channel = 0, Name = "Omni (All)" });
        for (int i = 1; i <= 16; i++)
        {
            MidiChannelOptions.Add(new MidiChannelOption { Channel = i, Name = $"Channel {i}" });
        }

        // Initialize default output buses
        OutputBuses.Add(new OutputBusOption { Id = null, Name = "Master" });

        // Initialize default input sources
        InputSources.Add(new InputSourceOption { Id = "midi-all", Name = "All MIDI", Type = "MIDI" });
        InputSources.Add(new InputSourceOption { Id = "none", Name = "None", Type = "None" });

        // Initialize default audio inputs
        AudioInputs.Add(new AudioInputOption { Id = "none", Name = "None", DeviceName = "-" });
        AudioInputs.Add(new AudioInputOption { Id = "input-1-2", Name = "Input 1/2", DeviceName = "Default", ChannelConfig = "Stereo" });
        AudioInputs.Add(new AudioInputOption { Id = "input-1", Name = "Input 1", DeviceName = "Default", ChannelConfig = "Mono" });
        AudioInputs.Add(new AudioInputOption { Id = "input-2", Name = "Input 2", DeviceName = "Default", ChannelConfig = "Mono" });
    }

    private void OnSelectedTrackPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Forward property changes to update bindings
        switch (e.PropertyName)
        {
            case nameof(TrackInfo.Name):
                OnPropertyChanged(nameof(TrackName));
                break;
            case nameof(TrackInfo.Color):
                OnPropertyChanged(nameof(TrackColor));
                OnPropertyChanged(nameof(SelectedColorOption));
                break;
            case nameof(TrackInfo.IsArmed):
                OnPropertyChanged(nameof(IsArmed));
                break;
            case nameof(TrackInfo.IsMonitoring):
                OnPropertyChanged(nameof(IsMonitoring));
                break;
            case nameof(TrackInfo.IsFrozen):
                OnPropertyChanged(nameof(IsFrozen));
                break;
            case nameof(TrackInfo.IsMuted):
                OnPropertyChanged(nameof(IsMuted));
                break;
            case nameof(TrackInfo.IsSoloed):
                OnPropertyChanged(nameof(IsSoloed));
                break;
            case nameof(TrackInfo.Height):
                OnPropertyChanged(nameof(TrackHeight));
                break;
            case nameof(TrackInfo.MidiChannel):
                OnPropertyChanged(nameof(MidiChannel));
                break;
            case nameof(TrackInfo.Volume):
                OnPropertyChanged(nameof(Volume));
                OnPropertyChanged(nameof(VolumeDb));
                break;
            case nameof(TrackInfo.Pan):
                OnPropertyChanged(nameof(Pan));
                OnPropertyChanged(nameof(PanDisplay));
                break;
        }
    }

    private void RefreshAllProperties()
    {
        OnPropertyChanged(nameof(TrackName));
        OnPropertyChanged(nameof(TrackColor));
        OnPropertyChanged(nameof(SelectedColorOption));
        OnPropertyChanged(nameof(IsArmed));
        OnPropertyChanged(nameof(IsMonitoring));
        OnPropertyChanged(nameof(IsFrozen));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(IsSoloed));
        OnPropertyChanged(nameof(TrackHeight));
        OnPropertyChanged(nameof(MidiChannel));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumeDb));
        OnPropertyChanged(nameof(Pan));
        OnPropertyChanged(nameof(PanDisplay));
        OnPropertyChanged(nameof(InstrumentName));
    }

    private void UpdateCommandStates()
    {
        RenameCommand.NotifyCanExecuteChanged();
        SetColorCommand.NotifyCanExecuteChanged();
        ToggleArmCommand.NotifyCanExecuteChanged();
        ToggleMonitorCommand.NotifyCanExecuteChanged();
        ToggleMuteCommand.NotifyCanExecuteChanged();
        ToggleSoloCommand.NotifyCanExecuteChanged();
        ToggleFreezeCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ResetTrackCommand.NotifyCanExecuteChanged();
        ResetVolumeCommand.NotifyCanExecuteChanged();
        ResetPanCommand.NotifyCanExecuteChanged();
    }

    private void RaiseTrackPropertyChanged(string propertyName, object? oldValue, object? newValue)
    {
        if (SelectedTrack != null)
        {
            TrackPropertyChanged?.Invoke(this, new TrackPropertyChangedEventArgs(SelectedTrack, propertyName, oldValue, newValue));
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the available output buses.
    /// </summary>
    /// <param name="buses">The list of available buses.</param>
    public void UpdateOutputBuses(System.Collections.Generic.IEnumerable<OutputBusOption> buses)
    {
        OutputBuses.Clear();
        OutputBuses.Add(new OutputBusOption { Id = null, Name = "Master" });
        foreach (var bus in buses)
        {
            OutputBuses.Add(bus);
        }
    }

    /// <summary>
    /// Updates the available input sources.
    /// </summary>
    /// <param name="sources">The list of available sources.</param>
    public void UpdateInputSources(System.Collections.Generic.IEnumerable<InputSourceOption> sources)
    {
        InputSources.Clear();
        foreach (var source in sources)
        {
            InputSources.Add(source);
        }
    }

    /// <summary>
    /// Updates the available audio inputs.
    /// </summary>
    /// <param name="inputs">The list of available inputs.</param>
    public void UpdateAudioInputs(System.Collections.Generic.IEnumerable<AudioInputOption> inputs)
    {
        AudioInputs.Clear();
        AudioInputs.Add(new AudioInputOption { Id = "none", Name = "None", DeviceName = "-" });
        foreach (var input in inputs)
        {
            AudioInputs.Add(input);
        }
    }

    /// <summary>
    /// Resets the input level peak indicators.
    /// </summary>
    public void ResetInputPeaks()
    {
        InputLevel = 0;
        InputLevelDb = -60f;
        IsClipping = false;
    }

    #endregion

    #region Recording Service Event Handlers

    private void OnInputLevelsUpdated(object? sender, EventArgs e)
    {
        if (SelectedTrack == null || !IsArmed) return;

        // Get input level from recording service
        var armedTrack = _recordingService.GetArmedTrack(SelectedTrack.Id);
        if (armedTrack != null)
        {
            InputLevel = armedTrack.InputLevel;
            InputLevelDb = armedTrack.InputLevelDb;
            IsClipping = armedTrack.IsClipping;
            OnPropertyChanged(nameof(InputLevelDisplay));
        }
    }

    private void OnRecordingStateChanged(object? sender, bool isRecording)
    {
        IsRecording = isRecording;
        OnPropertyChanged(nameof(ShowInputLevel));
    }

    private void OnTrackArmedChanged(object? sender, TrackArmEventArgs e)
    {
        if (SelectedTrack == null || e.TrackId != SelectedTrack.Id) return;

        // Sync armed state from service
        if (SelectedTrack.IsArmed != e.IsArmed)
        {
            SelectedTrack.IsArmed = e.IsArmed;
            OnPropertyChanged(nameof(IsArmed));
            OnPropertyChanged(nameof(ShowInputLevel));
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the TrackPropertiesViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe from recording service events
        _recordingService.InputLevelsUpdated -= OnInputLevelsUpdated;
        _recordingService.RecordingStateChanged -= OnRecordingStateChanged;
        _recordingService.TrackArmed -= OnTrackArmedChanged;
        _recordingService.TrackDisarmed -= OnTrackArmedChanged;

        // Unsubscribe from selected track
        if (_selectedTrack != null)
        {
            _selectedTrack.PropertyChanged -= OnSelectedTrackPropertyChanged;
        }
    }

    #endregion
}

#region Event Args Classes

/// <summary>
/// Event args for track property changes.
/// </summary>
public class TrackPropertyChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track that was changed.
    /// </summary>
    public TrackInfo Track { get; }

    /// <summary>
    /// Gets the property name that changed.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the old value.
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Creates new TrackPropertyChangedEventArgs.
    /// </summary>
    public TrackPropertyChangedEventArgs(TrackInfo track, string propertyName, object? oldValue, object? newValue)
    {
        Track = track;
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// Event args for track events.
/// </summary>
public class TrackEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track.
    /// </summary>
    public TrackInfo Track { get; }

    /// <summary>
    /// Creates new TrackEventArgs.
    /// </summary>
    public TrackEventArgs(TrackInfo track)
    {
        Track = track;
    }
}

/// <summary>
/// Event args for track color changes.
/// </summary>
public class TrackColorChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track.
    /// </summary>
    public TrackInfo Track { get; }

    /// <summary>
    /// Gets the old color.
    /// </summary>
    public string OldColor { get; }

    /// <summary>
    /// Gets the new color.
    /// </summary>
    public string NewColor { get; }

    /// <summary>
    /// Creates new TrackColorChangedEventArgs.
    /// </summary>
    public TrackColorChangedEventArgs(TrackInfo track, string oldColor, string newColor)
    {
        Track = track;
        OldColor = oldColor;
        NewColor = newColor;
    }
}

/// <summary>
/// Represents a MIDI channel option.
/// </summary>
public class MidiChannelOption
{
    /// <summary>
    /// Gets or sets the channel number (0 = Omni).
    /// </summary>
    public int Channel { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = "";
}

#endregion
