// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for MIDI Machine Control (MMC) and MIDI Time Code (MTC) panel.

using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Network;

/// <summary>
/// Sync status enumeration for MTC synchronization.
/// </summary>
public enum SyncStatus
{
    Unlocked,
    Searching,
    Locked
}

/// <summary>
/// Frame rate enumeration for MTC.
/// </summary>
public enum MtcFrameRate
{
    Fps24 = 24,
    Fps25 = 25,
    Fps2997 = 30, // 29.97 drop-frame
    Fps30 = 31    // 30 non-drop
}

/// <summary>
/// MIDI port information for display.
/// </summary>
public partial class MidiPortInfo : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _portIndex;

    [ObservableProperty]
    private bool _isInput;

    public override string ToString() => Name;
}

/// <summary>
/// ViewModel for the Machine Control Panel providing MMC and MTC functionality.
/// </summary>
public partial class MachineControlPanelViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private readonly DispatcherTimer _timecodeTimer;
    private readonly DispatcherTimer _syncCheckTimer;
    private DateTime _lastMtcReceived;
    private bool _disposed;
    private double _internalTimecode;
#pragma warning disable CS0414 // Field is assigned but never read - reserved for future MTC chase logic
    private bool _isChasing;
#pragma warning restore CS0414

    #endregion

    #region Observable Properties - Transport State

    /// <summary>
    /// Gets or sets whether playback is active.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Gets or sets whether recording is active.
    /// </summary>
    [ObservableProperty]
    private bool _isRecording;

    /// <summary>
    /// Gets or sets whether the transport is stopped.
    /// </summary>
    [ObservableProperty]
    private bool _isStopped = true;

    #endregion

    #region Observable Properties - Timecode

    /// <summary>
    /// Gets or sets the current hours value.
    /// </summary>
    [ObservableProperty]
    private int _hours;

    /// <summary>
    /// Gets or sets the current minutes value.
    /// </summary>
    [ObservableProperty]
    private int _minutes;

    /// <summary>
    /// Gets or sets the current seconds value.
    /// </summary>
    [ObservableProperty]
    private int _seconds;

    /// <summary>
    /// Gets or sets the current frames value.
    /// </summary>
    [ObservableProperty]
    private int _frames;

    /// <summary>
    /// Gets or sets the offset hours value.
    /// </summary>
    [ObservableProperty]
    private int _offsetHours;

    /// <summary>
    /// Gets or sets the offset minutes value.
    /// </summary>
    [ObservableProperty]
    private int _offsetMinutes;

    /// <summary>
    /// Gets or sets the offset seconds value.
    /// </summary>
    [ObservableProperty]
    private int _offsetSeconds;

    /// <summary>
    /// Gets or sets the offset frames value.
    /// </summary>
    [ObservableProperty]
    private int _offsetFrames;

    /// <summary>
    /// Gets or sets the locate timecode string (HH:MM:SS:FF).
    /// </summary>
    [ObservableProperty]
    private string _locateTimecode = "00:00:00:00";

    #endregion

    #region Observable Properties - MTC Settings

    /// <summary>
    /// Gets or sets the selected frame rate.
    /// </summary>
    [ObservableProperty]
    private MtcFrameRate _selectedFrameRate = MtcFrameRate.Fps25;

    /// <summary>
    /// Gets or sets whether MTC generation is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _mtcGeneratorEnabled;

    /// <summary>
    /// Gets or sets whether MTC receiving/chasing is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _mtcReceiverEnabled;

    /// <summary>
    /// Gets or sets the current sync status.
    /// </summary>
    [ObservableProperty]
    private SyncStatus _syncStatus = SyncStatus.Unlocked;

    /// <summary>
    /// Gets or sets whether chase lock is achieved.
    /// </summary>
    [ObservableProperty]
    private bool _isChaseLocked;

    #endregion

    #region Observable Properties - MMC Settings

    /// <summary>
    /// Gets or sets the MMC device ID (0-127, 127 = all devices).
    /// </summary>
    [ObservableProperty]
    private int _deviceId = 127;

    /// <summary>
    /// Gets or sets whether MMC sending is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _mmcSendEnabled;

    /// <summary>
    /// Gets or sets whether MMC receiving is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _mmcReceiveEnabled;

    #endregion

    #region Observable Properties - MIDI Ports

    /// <summary>
    /// Gets the available MIDI input ports.
    /// </summary>
    public ObservableCollection<MidiPortInfo> MidiInputPorts { get; } = new();

    /// <summary>
    /// Gets the available MIDI output ports.
    /// </summary>
    public ObservableCollection<MidiPortInfo> MidiOutputPorts { get; } = new();

    /// <summary>
    /// Gets or sets the selected MIDI input port.
    /// </summary>
    [ObservableProperty]
    private MidiPortInfo? _selectedMidiInputPort;

    /// <summary>
    /// Gets or sets the selected MIDI output port.
    /// </summary>
    [ObservableProperty]
    private MidiPortInfo? _selectedMidiOutputPort;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the formatted timecode display string.
    /// </summary>
    public string TimecodeDisplay => $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}:{Frames:D2}";

    /// <summary>
    /// Gets the formatted offset display string.
    /// </summary>
    public string OffsetDisplay => $"{OffsetHours:D2}:{OffsetMinutes:D2}:{OffsetSeconds:D2}:{OffsetFrames:D2}";

    /// <summary>
    /// Gets the sync status display text.
    /// </summary>
    public string SyncStatusText => SyncStatus switch
    {
        SyncStatus.Locked => "LOCKED",
        SyncStatus.Searching => "SEARCHING...",
        SyncStatus.Unlocked => "UNLOCKED",
        _ => "UNKNOWN"
    };

    /// <summary>
    /// Gets the frame rate as a numeric value for display.
    /// </summary>
    public double FrameRateValue => SelectedFrameRate switch
    {
        MtcFrameRate.Fps24 => 24.0,
        MtcFrameRate.Fps25 => 25.0,
        MtcFrameRate.Fps2997 => 29.97,
        MtcFrameRate.Fps30 => 30.0,
        _ => 25.0
    };

    /// <summary>
    /// Gets the maximum frames value based on frame rate.
    /// </summary>
    public int MaxFrames => SelectedFrameRate switch
    {
        MtcFrameRate.Fps24 => 23,
        MtcFrameRate.Fps25 => 24,
        MtcFrameRate.Fps2997 => 29,
        MtcFrameRate.Fps30 => 29,
        _ => 24
    };

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new MachineControlPanelViewModel instance.
    /// </summary>
    public MachineControlPanelViewModel()
    {
        // Initialize MIDI ports
        RefreshMidiPorts();

        // Setup timecode update timer (running at frame rate)
        _timecodeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 25.0) // Default 25fps
        };
        _timecodeTimer.Tick += OnTimecodeTimerTick;

        // Setup sync check timer
        _syncCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _syncCheckTimer.Tick += OnSyncCheckTimerTick;
        _syncCheckTimer.Start();
    }

    #endregion

    #region Commands - Transport

    /// <summary>
    /// Sends MMC Play command and starts local playback.
    /// </summary>
    [RelayCommand]
    private void Play()
    {
        if (MmcSendEnabled)
        {
            SendMmcCommand(MmcCommandType.Play);
        }

        IsPlaying = true;
        IsRecording = false;
        IsStopped = false;

        if (MtcGeneratorEnabled)
        {
            _timecodeTimer.Start();
        }

        StatusMessage = "Playing";
    }

    /// <summary>
    /// Sends MMC Stop command and stops local playback.
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        if (MmcSendEnabled)
        {
            SendMmcCommand(MmcCommandType.Stop);
        }

        IsPlaying = false;
        IsRecording = false;
        IsStopped = true;
        _timecodeTimer.Stop();

        StatusMessage = "Stopped";
    }

    /// <summary>
    /// Sends MMC Record Strobe command and starts recording.
    /// </summary>
    [RelayCommand]
    private void Record()
    {
        if (MmcSendEnabled)
        {
            SendMmcCommand(MmcCommandType.RecordStrobe);
        }

        IsPlaying = true;
        IsRecording = true;
        IsStopped = false;

        if (MtcGeneratorEnabled)
        {
            _timecodeTimer.Start();
        }

        StatusMessage = "Recording";
    }

    /// <summary>
    /// Sends MMC Rewind command.
    /// </summary>
    [RelayCommand]
    private void Rewind()
    {
        if (MmcSendEnabled)
        {
            SendMmcCommand(MmcCommandType.Rewind);
        }

        // Jump to start (00:00:00:00)
        SetTimecode(0, 0, 0, 0);
        StatusMessage = "Rewind";
    }

    /// <summary>
    /// Sends MMC Fast Forward command.
    /// </summary>
    [RelayCommand]
    private void FastForward()
    {
        if (MmcSendEnabled)
        {
            SendMmcCommand(MmcCommandType.FastForward);
        }

        // Jump forward by 10 seconds
        var totalFrames = TimecodeToFrames(Hours, Minutes, Seconds, Frames);
        totalFrames += (int)(10 * FrameRateValue);
        FramesToTimecode(totalFrames, out var h, out var m, out var s, out var f);
        SetTimecode(h, m, s, f);
        StatusMessage = "Fast Forward";
    }

    /// <summary>
    /// Sends MMC Locate command to go to a specific timecode.
    /// </summary>
    [RelayCommand]
    private void Locate()
    {
        if (ParseTimecode(LocateTimecode, out var h, out var m, out var s, out var f))
        {
            if (MmcSendEnabled)
            {
                SendMmcLocate(h, m, s, f);
            }

            SetTimecode(h, m, s, f);
            StatusMessage = $"Located to {LocateTimecode}";
        }
        else
        {
            StatusMessage = "Invalid timecode format (use HH:MM:SS:FF)";
        }
    }

    #endregion

    #region Commands - MTC

    /// <summary>
    /// Toggles MTC generator on/off.
    /// </summary>
    [RelayCommand]
    private void ToggleMtcGenerator()
    {
        MtcGeneratorEnabled = !MtcGeneratorEnabled;

        if (MtcGeneratorEnabled && IsPlaying)
        {
            _timecodeTimer.Start();
        }
        else if (!MtcGeneratorEnabled)
        {
            _timecodeTimer.Stop();
        }

        StatusMessage = MtcGeneratorEnabled ? "MTC Generator enabled" : "MTC Generator disabled";
    }

    /// <summary>
    /// Toggles MTC receiver/chase on/off.
    /// </summary>
    [RelayCommand]
    private void ToggleMtcReceiver()
    {
        MtcReceiverEnabled = !MtcReceiverEnabled;

        if (MtcReceiverEnabled)
        {
            SyncStatus = SyncStatus.Searching;
            _isChasing = true;
        }
        else
        {
            SyncStatus = SyncStatus.Unlocked;
            IsChaseLocked = false;
            _isChasing = false;
        }

        StatusMessage = MtcReceiverEnabled ? "MTC Receiver enabled - Searching..." : "MTC Receiver disabled";
    }

    #endregion

    #region Commands - Settings

    /// <summary>
    /// Refreshes the list of available MIDI ports.
    /// </summary>
    [RelayCommand]
    private void RefreshMidiPorts()
    {
        MidiInputPorts.Clear();
        MidiOutputPorts.Clear();

        // Add virtual/placeholder ports for demonstration
        // In a real implementation, these would come from the MIDI service
        MidiInputPorts.Add(new MidiPortInfo { Name = "None", PortIndex = -1, IsInput = true });
        MidiInputPorts.Add(new MidiPortInfo { Name = "MIDI Input 1", PortIndex = 0, IsInput = true });
        MidiInputPorts.Add(new MidiPortInfo { Name = "MIDI Input 2", PortIndex = 1, IsInput = true });
        MidiInputPorts.Add(new MidiPortInfo { Name = "Virtual MIDI In", PortIndex = 2, IsInput = true });

        MidiOutputPorts.Add(new MidiPortInfo { Name = "None", PortIndex = -1, IsInput = false });
        MidiOutputPorts.Add(new MidiPortInfo { Name = "MIDI Output 1", PortIndex = 0, IsInput = false });
        MidiOutputPorts.Add(new MidiPortInfo { Name = "MIDI Output 2", PortIndex = 1, IsInput = false });
        MidiOutputPorts.Add(new MidiPortInfo { Name = "Virtual MIDI Out", PortIndex = 2, IsInput = false });

        SelectedMidiInputPort = MidiInputPorts[0];
        SelectedMidiOutputPort = MidiOutputPorts[0];

        StatusMessage = "MIDI ports refreshed";
    }

    /// <summary>
    /// Resets the timecode offset to zero.
    /// </summary>
    [RelayCommand]
    private void ResetOffset()
    {
        OffsetHours = 0;
        OffsetMinutes = 0;
        OffsetSeconds = 0;
        OffsetFrames = 0;
        OnPropertyChanged(nameof(OffsetDisplay));
        StatusMessage = "Offset reset to 00:00:00:00";
    }

    /// <summary>
    /// Sets the current timecode as the offset.
    /// </summary>
    [RelayCommand]
    private void SetCurrentAsOffset()
    {
        OffsetHours = Hours;
        OffsetMinutes = Minutes;
        OffsetSeconds = Seconds;
        OffsetFrames = Frames;
        OnPropertyChanged(nameof(OffsetDisplay));
        StatusMessage = $"Offset set to {OffsetDisplay}";
    }

    #endregion

    #region Timer Event Handlers

    private void OnTimecodeTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || !MtcGeneratorEnabled) return;

        // Increment timecode by one frame
        _internalTimecode += 1.0 / FrameRateValue;

        var totalFrames = (int)(_internalTimecode * FrameRateValue);
        FramesToTimecode(totalFrames, out var h, out var m, out var s, out var f);

        Hours = h;
        Minutes = m;
        Seconds = s;
        Frames = f;

        OnPropertyChanged(nameof(TimecodeDisplay));

        // Send MTC quarter frame messages
        SendMtcQuarterFrame();
    }

    private void OnSyncCheckTimerTick(object? sender, EventArgs e)
    {
        if (!MtcReceiverEnabled) return;

        var timeSinceLastMtc = DateTime.Now - _lastMtcReceived;

        if (timeSinceLastMtc.TotalMilliseconds < 200)
        {
            if (SyncStatus != SyncStatus.Locked)
            {
                SyncStatus = SyncStatus.Locked;
                IsChaseLocked = true;
                OnPropertyChanged(nameof(SyncStatusText));
            }
        }
        else if (timeSinceLastMtc.TotalMilliseconds < 1000)
        {
            if (SyncStatus != SyncStatus.Searching)
            {
                SyncStatus = SyncStatus.Searching;
                IsChaseLocked = false;
                OnPropertyChanged(nameof(SyncStatusText));
            }
        }
        else
        {
            if (SyncStatus != SyncStatus.Unlocked)
            {
                SyncStatus = SyncStatus.Unlocked;
                IsChaseLocked = false;
                OnPropertyChanged(nameof(SyncStatusText));
            }
        }
    }

    #endregion

    #region Helper Methods

    private void SetTimecode(int h, int m, int s, int f)
    {
        Hours = h;
        Minutes = m;
        Seconds = s;
        Frames = f;
        _internalTimecode = TimecodeToFrames(h, m, s, f) / FrameRateValue;
        OnPropertyChanged(nameof(TimecodeDisplay));
    }

    private int TimecodeToFrames(int h, int m, int s, int f)
    {
        var fps = (int)FrameRateValue;
        return (h * 3600 + m * 60 + s) * fps + f;
    }

    private void FramesToTimecode(int totalFrames, out int h, out int m, out int s, out int f)
    {
        var fps = (int)FrameRateValue;
        f = totalFrames % fps;
        var totalSeconds = totalFrames / fps;
        s = totalSeconds % 60;
        var totalMinutes = totalSeconds / 60;
        m = totalMinutes % 60;
        h = totalMinutes / 60;

        // Clamp to valid ranges
        if (h > 23) h = 23;
    }

    private bool ParseTimecode(string timecode, out int h, out int m, out int s, out int f)
    {
        h = m = s = f = 0;

        if (string.IsNullOrWhiteSpace(timecode))
            return false;

        var parts = timecode.Split(':');
        if (parts.Length != 4)
            return false;

        if (!int.TryParse(parts[0], out h) || h < 0 || h > 23)
            return false;
        if (!int.TryParse(parts[1], out m) || m < 0 || m > 59)
            return false;
        if (!int.TryParse(parts[2], out s) || s < 0 || s > 59)
            return false;
        if (!int.TryParse(parts[3], out f) || f < 0 || f > MaxFrames)
            return false;

        return true;
    }

    /// <summary>
    /// Called when MTC is received from external source.
    /// </summary>
    public void OnMtcReceived(int h, int m, int s, int f)
    {
        if (!MtcReceiverEnabled) return;

        _lastMtcReceived = DateTime.Now;

        // Apply offset
        var totalFrames = TimecodeToFrames(h, m, s, f);
        var offsetFrames = TimecodeToFrames(OffsetHours, OffsetMinutes, OffsetSeconds, OffsetFrames);
        totalFrames -= offsetFrames;

        if (totalFrames < 0) totalFrames = 0;

        FramesToTimecode(totalFrames, out var newH, out var newM, out var newS, out var newF);

        Hours = newH;
        Minutes = newM;
        Seconds = newS;
        Frames = newF;
        _internalTimecode = totalFrames / FrameRateValue;

        OnPropertyChanged(nameof(TimecodeDisplay));
    }

    /// <summary>
    /// Called when MMC command is received from external source.
    /// </summary>
    public void OnMmcReceived(MmcCommandType command)
    {
        if (!MmcReceiveEnabled) return;

        switch (command)
        {
            case MmcCommandType.Stop:
                Stop();
                break;
            case MmcCommandType.Play:
                Play();
                break;
            case MmcCommandType.RecordStrobe:
                Record();
                break;
            case MmcCommandType.Rewind:
                Rewind();
                break;
            case MmcCommandType.FastForward:
                FastForward();
                break;
        }
    }

    #endregion

    #region MMC/MTC Sending (Placeholders)

    private void SendMmcCommand(MmcCommandType command)
    {
        if (SelectedMidiOutputPort == null || SelectedMidiOutputPort.PortIndex < 0)
            return;

        // In a real implementation, this would send the MMC SysEx message
        // F0 7F <device_id> 06 <command> F7
        System.Diagnostics.Debug.WriteLine($"MMC Send: {command} to Device {DeviceId}");
    }

    private void SendMmcLocate(int h, int m, int s, int f)
    {
        if (SelectedMidiOutputPort == null || SelectedMidiOutputPort.PortIndex < 0)
            return;

        // In a real implementation, this would send MMC Locate command
        // F0 7F <device_id> 06 44 06 01 <hr> <mn> <sc> <fr> <sf> F7
        System.Diagnostics.Debug.WriteLine($"MMC Locate: {h:D2}:{m:D2}:{s:D2}:{f:D2} to Device {DeviceId}");
    }

    private void SendMtcQuarterFrame()
    {
        if (SelectedMidiOutputPort == null || SelectedMidiOutputPort.PortIndex < 0)
            return;

        // In a real implementation, this would send MTC Quarter Frame messages
        // F1 <piece_type_and_data>
        System.Diagnostics.Debug.WriteLine($"MTC QF: {TimecodeDisplay}");
    }

    #endregion

    #region Property Change Handlers

    partial void OnSelectedFrameRateChanged(MtcFrameRate value)
    {
        // Update timer interval based on frame rate
        _timecodeTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameRateValue);
        OnPropertyChanged(nameof(FrameRateValue));
        OnPropertyChanged(nameof(MaxFrames));
    }

    partial void OnHoursChanged(int value)
    {
        OnPropertyChanged(nameof(TimecodeDisplay));
    }

    partial void OnMinutesChanged(int value)
    {
        OnPropertyChanged(nameof(TimecodeDisplay));
    }

    partial void OnSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(TimecodeDisplay));
    }

    partial void OnFramesChanged(int value)
    {
        OnPropertyChanged(nameof(TimecodeDisplay));
    }

    partial void OnSyncStatusChanged(SyncStatus value)
    {
        OnPropertyChanged(nameof(SyncStatusText));
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the MachineControlPanelViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _timecodeTimer.Stop();
        _syncCheckTimer.Stop();
    }

    #endregion
}

/// <summary>
/// MMC command types.
/// </summary>
public enum MmcCommandType
{
    Stop = 0x01,
    Play = 0x02,
    DeferredPlay = 0x03,
    FastForward = 0x04,
    Rewind = 0x05,
    RecordStrobe = 0x06,
    RecordExit = 0x07,
    RecordPause = 0x08,
    Pause = 0x09,
    Eject = 0x0A,
    Chase = 0x0B,
    MmcReset = 0x0D,
    Write = 0x40,
    Locate = 0x44
}
