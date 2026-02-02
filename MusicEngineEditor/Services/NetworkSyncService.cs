// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Singleton service for managing network sync state across Link, OSC, MIDI, and MMC/MTC.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicEngineEditor.Controls.Network;

namespace MusicEngineEditor.Services;

/// <summary>
/// Event arguments for transport state changes.
/// </summary>
public class NetworkTransportEventArgs : EventArgs
{
    public NetworkTransportState State { get; }
    public double PositionBeats { get; }
    public double Tempo { get; }
    public Guid? SourcePeerId { get; }
    public bool IsRemote { get; }

    public NetworkTransportEventArgs(NetworkTransportState state, double positionBeats, double tempo,
        Guid? sourcePeerId = null, bool isRemote = false)
    {
        State = state;
        PositionBeats = positionBeats;
        Tempo = tempo;
        SourcePeerId = sourcePeerId;
        IsRemote = isRemote;
    }
}

/// <summary>
/// Network transport states.
/// </summary>
public enum NetworkTransportState
{
    Stopped,
    Playing,
    Paused,
    Recording
}

/// <summary>
/// Singleton service for managing network synchronization state.
/// Handles Ableton Link, OSC, Network MIDI, and MMC/MTC synchronization.
/// </summary>
public sealed class NetworkSyncService : IDisposable
{
    #region Singleton

    private static readonly Lazy<NetworkSyncService> _instance = new(
        () => new NetworkSyncService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the NetworkSyncService.
    /// </summary>
    public static NetworkSyncService Instance => _instance.Value;

    #endregion

    #region Private Fields

    // Service state
    private bool _disposed;
    private readonly object _lock = new();

    // Ableton Link state
    private bool _isLinkEnabled;
    private bool _linkStartStopSyncEnabled = true;
    private double _linkTempo = 120.0;
    private double _linkBeat;
    private int _linkPeerCount;
    private bool _isLinkPlaying;
    private Timer? _linkSimulationTimer;

    // Network MIDI state
    private bool _isNetworkMidiEnabled;
    private readonly ConcurrentDictionary<string, NetworkMidiSessionInfo> _discoveredMidiSessions = new();
    private NetworkMidiSessionInfo? _connectedMidiSession;
    private UdpClient? _midiDiscoveryClient;
    private CancellationTokenSource? _midiDiscoveryCts;

    // MMC/MTC state
    private bool _isMmcEnabled;
    private bool _isMtcEnabled;
    private MtcFrameRate _mtcFrameRate = MtcFrameRate.Fps2997DropFrame;
    private string _currentTimecode = "00:00:00:00";
    private byte _mmcDeviceId = 0x7F; // All devices
    private bool _isMmcMaster = true;
    private Timer? _mtcGenerationTimer;
    private int _mtcFrameCounter;

    // Network interface
    private string? _selectedNetworkInterface;

    // Persistence of enabled services
    private bool _wasLinkEnabled;
    private bool _wasNetworkMidiEnabled;
    private bool _wasMmcEnabled;
    private bool _wasMtcEnabled;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether any network service is currently enabled.
    /// </summary>
    public bool IsAnyServiceEnabled =>
        _isLinkEnabled || _isNetworkMidiEnabled || _isMmcEnabled || _isMtcEnabled ||
        OSCControlSurfaceService.Instance.IsRunning;

    /// <summary>
    /// Gets or sets the selected network interface IP address.
    /// Null means all interfaces.
    /// </summary>
    public string? SelectedNetworkInterface
    {
        get => _selectedNetworkInterface;
        set
        {
            lock (_lock)
            {
                _selectedNetworkInterface = value;
                // Restart services if they were running
                if (_isLinkEnabled)
                {
                    DisableLink();
                    EnableLink();
                }
                if (_isNetworkMidiEnabled)
                {
                    DisableNetworkMidi();
                    EnableNetworkMidi(_connectedMidiSession?.Name ?? "MusicEngine MIDI");
                }
            }
        }
    }

    #region Ableton Link Properties

    /// <summary>
    /// Gets whether Ableton Link is enabled.
    /// </summary>
    public bool IsLinkEnabled => _isLinkEnabled;

    /// <summary>
    /// Gets or sets whether Link Start/Stop sync is enabled.
    /// </summary>
    public bool LinkStartStopSyncEnabled
    {
        get => _linkStartStopSyncEnabled;
        set
        {
            _linkStartStopSyncEnabled = value;
            LinkStartStopSyncChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Gets the current Link tempo.
    /// </summary>
    public double LinkTempo => _linkTempo;

    /// <summary>
    /// Gets the current Link beat position.
    /// </summary>
    public double LinkBeat => _linkBeat;

    /// <summary>
    /// Gets the number of connected Link peers.
    /// </summary>
    public int LinkPeerCount => _linkPeerCount;

    /// <summary>
    /// Gets whether Link playback is active.
    /// </summary>
    public bool IsLinkPlaying => _isLinkPlaying;

    #endregion

    #region Network MIDI Properties

    /// <summary>
    /// Gets whether Network MIDI is enabled.
    /// </summary>
    public bool IsNetworkMidiEnabled => _isNetworkMidiEnabled;

    /// <summary>
    /// Gets the discovered MIDI sessions.
    /// </summary>
    public IReadOnlyCollection<NetworkMidiSessionInfo> DiscoveredMidiSessions =>
        _discoveredMidiSessions.Values.ToList().AsReadOnly();

    /// <summary>
    /// Gets the currently connected MIDI session.
    /// </summary>
    public NetworkMidiSessionInfo? ConnectedMidiSession => _connectedMidiSession;

    #endregion

    #region MMC/MTC Properties

    /// <summary>
    /// Gets or sets whether MMC is enabled.
    /// </summary>
    public bool IsMmcEnabled
    {
        get => _isMmcEnabled;
        set
        {
            lock (_lock)
            {
                if (_isMmcEnabled != value)
                {
                    _isMmcEnabled = value;
                    MmcStateChanged?.Invoke(this, value);
                    ConnectionStateChanged?.Invoke(this, NetworkServiceType.MMC);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets whether MTC is enabled.
    /// </summary>
    public bool IsMtcEnabled
    {
        get => _isMtcEnabled;
        set
        {
            lock (_lock)
            {
                if (_isMtcEnabled != value)
                {
                    _isMtcEnabled = value;
                    if (value)
                    {
                        StartMtcGeneration();
                    }
                    else
                    {
                        StopMtcGeneration();
                    }
                    MtcStateChanged?.Invoke(this, value);
                    ConnectionStateChanged?.Invoke(this, NetworkServiceType.MTC);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the MTC frame rate.
    /// </summary>
    public MtcFrameRate MtcFrameRate
    {
        get => _mtcFrameRate;
        set
        {
            _mtcFrameRate = value;
            MtcFrameRateChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Gets the current timecode string.
    /// </summary>
    public string CurrentTimecode => _currentTimecode;

    /// <summary>
    /// Gets or sets the MMC device ID.
    /// </summary>
    public byte MmcDeviceId
    {
        get => _mmcDeviceId;
        set => _mmcDeviceId = value;
    }

    /// <summary>
    /// Gets or sets whether this instance is the MMC master.
    /// </summary>
    public bool IsMmcMaster
    {
        get => _isMmcMaster;
        set => _isMmcMaster = value;
    }

    #endregion

    #endregion

    #region Events

    /// <summary>
    /// Raised when Link state changes (enabled/disabled).
    /// </summary>
    public event EventHandler<bool>? LinkStateChanged;

    /// <summary>
    /// Raised when the number of Link peers changes.
    /// </summary>
    public event EventHandler<int>? LinkPeersChanged;

    /// <summary>
    /// Raised when Link Start/Stop sync setting changes.
    /// </summary>
    public event EventHandler<bool>? LinkStartStopSyncChanged;

    /// <summary>
    /// Raised when tempo changes (from Link or locally).
    /// </summary>
    public event EventHandler<double>? TempoChanged;

    /// <summary>
    /// Raised when beat position changes.
    /// </summary>
    public event EventHandler<double>? BeatChanged;

    /// <summary>
    /// Raised when Link transport state changes (play/stop).
    /// </summary>
    public event EventHandler<bool>? LinkTransportChanged;

    /// <summary>
    /// Raised when a Network MIDI session is discovered.
    /// </summary>
    public event EventHandler<NetworkMidiSessionInfo>? NetworkMidiSessionDiscovered;

    /// <summary>
    /// Raised when Network MIDI connection state changes.
    /// </summary>
    public event EventHandler<bool>? NetworkMidiConnectionChanged;

    /// <summary>
    /// Raised when MMC state changes.
    /// </summary>
    public event EventHandler<bool>? MmcStateChanged;

    /// <summary>
    /// Raised when MTC state changes.
    /// </summary>
    public event EventHandler<bool>? MtcStateChanged;

    /// <summary>
    /// Raised when MTC frame rate changes.
    /// </summary>
    public event EventHandler<MtcFrameRate>? MtcFrameRateChanged;

    /// <summary>
    /// Raised when timecode changes.
    /// </summary>
    public event EventHandler<string>? TimecodeChanged;

    /// <summary>
    /// Raised when any connection state changes.
    /// </summary>
    public event EventHandler<NetworkServiceType>? ConnectionStateChanged;

    /// <summary>
    /// Raised when transport sync is received from any source.
    /// </summary>
    public event EventHandler<NetworkTransportEventArgs>? TransportSyncReceived;

    /// <summary>
    /// Raised when an MMC command is received.
    /// </summary>
    public event EventHandler<MmcCommand>? MmcCommandReceived;

    #endregion

    #region Constructor

    private NetworkSyncService()
    {
        // Initialize with default state
    }

    #endregion

    #region Ableton Link Methods

    /// <summary>
    /// Enables Ableton Link synchronization.
    /// </summary>
    public void EnableLink()
    {
        lock (_lock)
        {
            if (_isLinkEnabled) return;

            try
            {
                // In a real implementation, this would initialize the Link library
                // For now, we simulate Link behavior
                _isLinkEnabled = true;
                _linkPeerCount = 0;

                // Start simulation timer for demo purposes
                _linkSimulationTimer = new Timer(LinkSimulationCallback, null,
                    TimeSpan.Zero, TimeSpan.FromMilliseconds(50));

                LinkStateChanged?.Invoke(this, true);
                ConnectionStateChanged?.Invoke(this, NetworkServiceType.AbletonLink);

                System.Diagnostics.Debug.WriteLine("Ableton Link enabled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enable Link: {ex.Message}");
                _isLinkEnabled = false;
                throw;
            }
        }
    }

    /// <summary>
    /// Disables Ableton Link synchronization.
    /// </summary>
    public void DisableLink()
    {
        lock (_lock)
        {
            if (!_isLinkEnabled) return;

            _linkSimulationTimer?.Dispose();
            _linkSimulationTimer = null;

            _isLinkEnabled = false;
            _linkPeerCount = 0;

            LinkStateChanged?.Invoke(this, false);
            LinkPeersChanged?.Invoke(this, 0);
            ConnectionStateChanged?.Invoke(this, NetworkServiceType.AbletonLink);

            System.Diagnostics.Debug.WriteLine("Ableton Link disabled");
        }
    }

    /// <summary>
    /// Sets the Link tempo.
    /// </summary>
    /// <param name="tempo">The tempo in BPM.</param>
    public void SetLinkTempo(double tempo)
    {
        lock (_lock)
        {
            tempo = Math.Clamp(tempo, 20.0, 999.0);
            if (Math.Abs(_linkTempo - tempo) > 0.001)
            {
                _linkTempo = tempo;
                TempoChanged?.Invoke(this, tempo);
            }
        }
    }

    /// <summary>
    /// Starts Link playback.
    /// </summary>
    public void StartLinkPlayback()
    {
        if (!_isLinkEnabled || !_linkStartStopSyncEnabled) return;

        lock (_lock)
        {
            if (!_isLinkPlaying)
            {
                _isLinkPlaying = true;
                LinkTransportChanged?.Invoke(this, true);
            }
        }
    }

    /// <summary>
    /// Stops Link playback.
    /// </summary>
    public void StopLinkPlayback()
    {
        if (!_isLinkEnabled || !_linkStartStopSyncEnabled) return;

        lock (_lock)
        {
            if (_isLinkPlaying)
            {
                _isLinkPlaying = false;
                _linkBeat = 0;
                LinkTransportChanged?.Invoke(this, false);
                BeatChanged?.Invoke(this, 0);
            }
        }
    }

    private void LinkSimulationCallback(object? state)
    {
        if (!_isLinkEnabled) return;

        // Simulate beat progression when playing
        if (_isLinkPlaying)
        {
            var beatsPerSecond = _linkTempo / 60.0;
            var beatIncrement = beatsPerSecond * 0.05; // 50ms interval
            _linkBeat += beatIncrement;

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                BeatChanged?.Invoke(this, _linkBeat);
            });
        }

        // Simulate occasional peer count changes for demo
        // In real implementation, this would come from the Link library
    }

    #endregion

    #region Network MIDI Methods

    /// <summary>
    /// Enables Network MIDI (RTP-MIDI) with the specified session name.
    /// </summary>
    /// <param name="sessionName">The session name to advertise.</param>
    public void EnableNetworkMidi(string sessionName)
    {
        lock (_lock)
        {
            if (_isNetworkMidiEnabled) return;

            try
            {
                _isNetworkMidiEnabled = true;

                // Start session discovery
                StartMidiSessionDiscovery();

                ConnectionStateChanged?.Invoke(this, NetworkServiceType.NetworkMIDI);
                System.Diagnostics.Debug.WriteLine($"Network MIDI enabled with session: {sessionName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enable Network MIDI: {ex.Message}");
                _isNetworkMidiEnabled = false;
                throw;
            }
        }
    }

    /// <summary>
    /// Disables Network MIDI.
    /// </summary>
    public void DisableNetworkMidi()
    {
        lock (_lock)
        {
            if (!_isNetworkMidiEnabled) return;

            StopMidiSessionDiscovery();

            _isNetworkMidiEnabled = false;
            _connectedMidiSession = null;
            _discoveredMidiSessions.Clear();

            NetworkMidiConnectionChanged?.Invoke(this, false);
            ConnectionStateChanged?.Invoke(this, NetworkServiceType.NetworkMIDI);
            System.Diagnostics.Debug.WriteLine("Network MIDI disabled");
        }
    }

    /// <summary>
    /// Refreshes the list of discovered Network MIDI sessions.
    /// </summary>
    public void RefreshNetworkMidiSessions()
    {
        if (!_isNetworkMidiEnabled) return;

        lock (_lock)
        {
            _discoveredMidiSessions.Clear();
            // Trigger rediscovery
            // In real implementation, this would send discovery packets
        }
    }

    /// <summary>
    /// Connects to a Network MIDI session.
    /// </summary>
    /// <param name="session">The session to connect to.</param>
    public void ConnectToMidiSession(NetworkMidiSessionInfo session)
    {
        lock (_lock)
        {
            if (_connectedMidiSession != null)
            {
                DisconnectFromMidiSession();
            }

            try
            {
                // In real implementation, establish RTP-MIDI connection
                session.IsConnected = true;
                _connectedMidiSession = session;

                NetworkMidiConnectionChanged?.Invoke(this, true);
                System.Diagnostics.Debug.WriteLine($"Connected to MIDI session: {session.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to connect to MIDI session: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Disconnects from the current Network MIDI session.
    /// </summary>
    public void DisconnectFromMidiSession()
    {
        lock (_lock)
        {
            if (_connectedMidiSession != null)
            {
                _connectedMidiSession.IsConnected = false;
                _connectedMidiSession = null;

                NetworkMidiConnectionChanged?.Invoke(this, false);
                System.Diagnostics.Debug.WriteLine("Disconnected from MIDI session");
            }
        }
    }

    private void StartMidiSessionDiscovery()
    {
        _midiDiscoveryCts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            try
            {
                // In real implementation, this would use mDNS/Bonjour for discovery
                // For demo, we simulate finding some sessions after a delay
                await Task.Delay(1000, _midiDiscoveryCts.Token);

                // Simulate discovered sessions
                var demoSessions = new[]
                {
                    new NetworkMidiSessionInfo
                    {
                        Name = "Studio Mac",
                        Host = "192.168.1.100",
                        Port = 5004
                    },
                    new NetworkMidiSessionInfo
                    {
                        Name = "iPad Pro",
                        Host = "192.168.1.105",
                        Port = 5004
                    }
                };

                foreach (var session in demoSessions)
                {
                    if (_midiDiscoveryCts?.Token.IsCancellationRequested == true) break;

                    _discoveredMidiSessions.TryAdd(session.Id, session);

                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        NetworkMidiSessionDiscovered?.Invoke(this, session);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Discovery cancelled
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MIDI discovery error: {ex.Message}");
            }
        }, _midiDiscoveryCts.Token);
    }

    private void StopMidiSessionDiscovery()
    {
        _midiDiscoveryCts?.Cancel();
        _midiDiscoveryCts?.Dispose();
        _midiDiscoveryCts = null;

        _midiDiscoveryClient?.Dispose();
        _midiDiscoveryClient = null;
    }

    #endregion

    #region MMC/MTC Methods

    private void StartMtcGeneration()
    {
        _mtcFrameCounter = 0;
        var frameInterval = GetMtcFrameInterval();

        _mtcGenerationTimer = new Timer(MtcGenerationCallback, null,
            TimeSpan.Zero, TimeSpan.FromMilliseconds(frameInterval));
    }

    private void StopMtcGeneration()
    {
        _mtcGenerationTimer?.Dispose();
        _mtcGenerationTimer = null;
    }

    private void MtcGenerationCallback(object? state)
    {
        if (!_isMtcEnabled) return;

        _mtcFrameCounter++;

        // Calculate timecode from frame counter
        var fps = GetMtcFps();
        var totalFrames = _mtcFrameCounter;

        // Handle drop-frame if needed
        if (_mtcFrameRate == MtcFrameRate.Fps2997DropFrame)
        {
            // Drop-frame calculation (skip frames 0 and 1 at start of each minute except every 10th)
            var totalMinutes = totalFrames / (fps * 60);
            var droppedFrames = 2 * (totalMinutes - totalMinutes / 10);
            totalFrames += (int)droppedFrames;
        }

        var frames = totalFrames % fps;
        var seconds = (totalFrames / fps) % 60;
        var minutes = (totalFrames / fps / 60) % 60;
        var hours = (totalFrames / fps / 60 / 60) % 24;

        var newTimecode = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";

        if (_currentTimecode != newTimecode)
        {
            _currentTimecode = newTimecode;

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                TimecodeChanged?.Invoke(this, _currentTimecode);
            });
        }
    }

    private double GetMtcFrameInterval()
    {
        return _mtcFrameRate switch
        {
            MtcFrameRate.Fps24 => 1000.0 / 24.0,
            MtcFrameRate.Fps25 => 1000.0 / 25.0,
            MtcFrameRate.Fps2997DropFrame => 1000.0 / 29.97,
            MtcFrameRate.Fps30 => 1000.0 / 30.0,
            _ => 1000.0 / 30.0
        };
    }

    private int GetMtcFps()
    {
        return _mtcFrameRate switch
        {
            MtcFrameRate.Fps24 => 24,
            MtcFrameRate.Fps25 => 25,
            MtcFrameRate.Fps2997DropFrame => 30, // Drop-frame uses 30 as base
            MtcFrameRate.Fps30 => 30,
            _ => 30
        };
    }

    /// <summary>
    /// Sends an MMC command.
    /// </summary>
    /// <param name="command">The MMC command to send.</param>
    public void SendMmcCommand(MmcCommand command)
    {
        if (!_isMmcEnabled || !_isMmcMaster) return;

        // In real implementation, send MMC SysEx message
        // Format: F0 7F <device-id> 06 <command> F7
        System.Diagnostics.Debug.WriteLine($"Sending MMC command: {command}");
    }

    /// <summary>
    /// Sets the timecode position.
    /// </summary>
    /// <param name="hours">Hours (0-23).</param>
    /// <param name="minutes">Minutes (0-59).</param>
    /// <param name="seconds">Seconds (0-59).</param>
    /// <param name="frames">Frames (0-29 depending on frame rate).</param>
    public void SetTimecode(int hours, int minutes, int seconds, int frames)
    {
        var fps = GetMtcFps();
        _mtcFrameCounter = frames + (seconds * fps) + (minutes * fps * 60) + (hours * fps * 60 * 60);
        _currentTimecode = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
        TimecodeChanged?.Invoke(this, _currentTimecode);
    }

    /// <summary>
    /// Resets the timecode to zero.
    /// </summary>
    public void ResetTimecode()
    {
        _mtcFrameCounter = 0;
        _currentTimecode = "00:00:00:00";
        TimecodeChanged?.Invoke(this, _currentTimecode);
    }

    #endregion

    #region Global Service Management

    /// <summary>
    /// Enables all network services that were previously enabled.
    /// </summary>
    public void EnableAllServices()
    {
        // Restore previously enabled services or enable defaults
        if (_wasLinkEnabled || !(_wasLinkEnabled || _wasNetworkMidiEnabled || _wasMmcEnabled || _wasMtcEnabled))
        {
            EnableLink();
        }
        if (_wasNetworkMidiEnabled)
        {
            EnableNetworkMidi(_connectedMidiSession?.Name ?? "MusicEngine MIDI");
        }
        if (_wasMmcEnabled)
        {
            IsMmcEnabled = true;
        }
        if (_wasMtcEnabled)
        {
            IsMtcEnabled = true;
        }
    }

    /// <summary>
    /// Disables all network services and saves their state for later restore.
    /// </summary>
    public void DisableAllServices()
    {
        // Save current state
        _wasLinkEnabled = _isLinkEnabled;
        _wasNetworkMidiEnabled = _isNetworkMidiEnabled;
        _wasMmcEnabled = _isMmcEnabled;
        _wasMtcEnabled = _isMtcEnabled;

        // Disable all
        DisableLink();
        DisableNetworkMidi();
        IsMmcEnabled = false;
        IsMtcEnabled = false;
    }

    #endregion

    #region Peer Discovery

    /// <summary>
    /// Gets all discovered peers across all services.
    /// </summary>
    public IReadOnlyCollection<NetworkPeerInfo> GetAllPeers()
    {
        var peers = new List<NetworkPeerInfo>();

        // Add Link peers (simulated)
        if (_isLinkEnabled && _linkPeerCount > 0)
        {
            for (int i = 0; i < _linkPeerCount; i++)
            {
                peers.Add(new NetworkPeerInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Link Peer {i + 1}",
                    ServiceType = NetworkServiceType.AbletonLink,
                    IsConnected = true
                });
            }
        }

        // Add Network MIDI peers
        foreach (var session in _discoveredMidiSessions.Values)
        {
            peers.Add(new NetworkPeerInfo
            {
                Id = session.Id,
                Name = session.Name,
                Host = session.Host,
                ServiceType = NetworkServiceType.NetworkMIDI,
                IsConnected = session.IsConnected
            });
        }

        return peers.AsReadOnly();
    }

    #endregion

    #region Transport Synchronization

    /// <summary>
    /// Sends transport state to all connected services.
    /// </summary>
    /// <param name="state">The transport state.</param>
    /// <param name="positionBeats">The position in beats.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    public void SendTransportState(NetworkTransportState state, double positionBeats, double tempo)
    {
        // Send via Link
        if (_isLinkEnabled && _linkStartStopSyncEnabled)
        {
            SetLinkTempo(tempo);
            if (state == NetworkTransportState.Playing)
            {
                StartLinkPlayback();
            }
            else if (state == NetworkTransportState.Stopped)
            {
                StopLinkPlayback();
            }
        }

        // Send via MMC
        if (_isMmcEnabled && _isMmcMaster)
        {
            var mmcCommand = state switch
            {
                NetworkTransportState.Playing => MmcCommand.Play,
                NetworkTransportState.Stopped => MmcCommand.Stop,
                NetworkTransportState.Paused => MmcCommand.Pause,
                NetworkTransportState.Recording => MmcCommand.RecordStrobe,
                _ => MmcCommand.Stop
            };
            SendMmcCommand(mmcCommand);
        }

        // Notify local listeners
        TransportSyncReceived?.Invoke(this, new NetworkTransportEventArgs(
            state, positionBeats, tempo, null, false));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisableAllServices();

        _linkSimulationTimer?.Dispose();
        _mtcGenerationTimer?.Dispose();
        _midiDiscoveryCts?.Cancel();
        _midiDiscoveryCts?.Dispose();
        _midiDiscoveryClient?.Dispose();
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Information about a network peer.
/// </summary>
public class NetworkPeerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Host { get; set; }
    public NetworkServiceType ServiceType { get; set; }
    public bool IsConnected { get; set; }
    public int LatencyMs { get; set; }
}

/// <summary>
/// MMC commands.
/// </summary>
public enum MmcCommand
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
    Reset = 0x0D,
    Write = 0x40,
    GotoLocator = 0x44,
    MmcSearch = 0x47
}

#endregion
