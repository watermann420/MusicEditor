// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for Network MIDI Panel.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Network;

/// <summary>
/// Represents a discovered RTP-MIDI session on the network.
/// </summary>
public partial class NetworkMidiSession : ObservableObject
{
    [ObservableProperty]
    private string _sessionName = string.Empty;

    [ObservableProperty]
    private string _hostAddress = string.Empty;

    [ObservableProperty]
    private int _port = 5004;

    [ObservableProperty]
    private int _latencyMs;

    [ObservableProperty]
    private int _peerCount;

    [ObservableProperty]
    private bool _isSecure;

    [ObservableProperty]
    private DateTime _discoveredAt = DateTime.Now;

    /// <summary>
    /// Gets the latency display color based on latency value.
    /// </summary>
    public Brush LatencyBrush => LatencyMs switch
    {
        < 20 => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),  // Green - excellent
        < 50 => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),  // Cyan - good
        < 100 => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)), // Yellow - acceptable
        _ => new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57))      // Red - poor
    };

    /// <summary>
    /// Gets formatted latency display.
    /// </summary>
    public string LatencyDisplay => $"{LatencyMs} ms";
}

/// <summary>
/// Represents a connected peer in a Network MIDI session.
/// </summary>
public partial class NetworkMidiPeer : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private int _latencyMs;

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private DateTime _connectedAt = DateTime.Now;

    [ObservableProperty]
    private long _messagesReceived;

    [ObservableProperty]
    private long _messagesSent;

    /// <summary>
    /// Gets the connection quality (0-100).
    /// </summary>
    public int ConnectionQuality => LatencyMs switch
    {
        < 10 => 100,
        < 20 => 90,
        < 50 => 75,
        < 100 => 50,
        < 200 => 25,
        _ => 10
    };

    /// <summary>
    /// Gets the status display color.
    /// </summary>
    public Brush StatusBrush => IsConnected
        ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))
        : new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));

    /// <summary>
    /// Gets the latency display color.
    /// </summary>
    public Brush LatencyBrush => LatencyMs switch
    {
        < 20 => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),
        < 50 => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
        < 100 => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57))
    };

    /// <summary>
    /// Gets formatted latency display.
    /// </summary>
    public string LatencyDisplay => $"{LatencyMs} ms";
}

/// <summary>
/// Represents a virtual MIDI port.
/// </summary>
public partial class VirtualMidiPort : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private bool _isInput;

    [ObservableProperty]
    private bool _isEnabled = true;

    public override string ToString() => Name;
}

/// <summary>
/// Connection quality levels.
/// </summary>
public enum ConnectionQuality
{
    Excellent,
    Good,
    Fair,
    Poor,
    Disconnected
}

/// <summary>
/// ViewModel for the Network MIDI Panel.
/// Manages RTP-MIDI/Network MIDI sessions with Bonjour/mDNS discovery.
/// </summary>
public partial class NetworkMIDIPanelViewModel : ViewModelBase
{
    #region Constants

    private const int DefaultRtpMidiPort = 5004;
    private const int DiscoveryTimeoutMs = 5000;
    private const int ActivityDecayMs = 500;

    #endregion

    #region Private Fields

    private readonly DispatcherTimer _activityTimer;
    private readonly DispatcherTimer _latencyUpdateTimer;
    private DateTime _lastMidiInActivity;
    private DateTime _lastMidiOutActivity;
    private bool _isInitialized;

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private bool _isNetworkMidiEnabled;

    [ObservableProperty]
    private string _sessionName = "My MIDI Session";

    [ObservableProperty]
    private bool _isBonjourAvailable;

    [ObservableProperty]
    private string _bonjourStatus = "Checking...";

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isHosting;

    [ObservableProperty]
    private string _currentSessionName = string.Empty;

    [ObservableProperty]
    private NetworkMidiSession? _selectedAvailableSession;

    [ObservableProperty]
    private NetworkMidiPeer? _selectedConnectedSession;

    [ObservableProperty]
    private VirtualMidiPort? _selectedInputPort;

    [ObservableProperty]
    private VirtualMidiPort? _selectedOutputPort;

    [ObservableProperty]
    private bool _midiInActive;

    [ObservableProperty]
    private bool _midiOutActive;

    [ObservableProperty]
    private long _totalMessagesIn;

    [ObservableProperty]
    private long _totalMessagesOut;

    [ObservableProperty]
    private int _averageLatencyMs;

    [ObservableProperty]
    private ConnectionQuality _connectionQualityLevel = ConnectionQuality.Disconnected;

    [ObservableProperty]
    private string _statusMessage = "Network MIDI disabled";

    // Channel filtering
    [ObservableProperty]
    private bool _filterAllChannels = true;

    [ObservableProperty]
    private bool _channel1Enabled = true;

    [ObservableProperty]
    private bool _channel2Enabled = true;

    [ObservableProperty]
    private bool _channel3Enabled = true;

    [ObservableProperty]
    private bool _channel4Enabled = true;

    [ObservableProperty]
    private bool _channel5Enabled = true;

    [ObservableProperty]
    private bool _channel6Enabled = true;

    [ObservableProperty]
    private bool _channel7Enabled = true;

    [ObservableProperty]
    private bool _channel8Enabled = true;

    [ObservableProperty]
    private bool _channel9Enabled = true;

    [ObservableProperty]
    private bool _channel10Enabled = true;

    [ObservableProperty]
    private bool _channel11Enabled = true;

    [ObservableProperty]
    private bool _channel12Enabled = true;

    [ObservableProperty]
    private bool _channel13Enabled = true;

    [ObservableProperty]
    private bool _channel14Enabled = true;

    [ObservableProperty]
    private bool _channel15Enabled = true;

    [ObservableProperty]
    private bool _channel16Enabled = true;

    // MIDI routing options
    [ObservableProperty]
    private bool _sendClock = true;

    [ObservableProperty]
    private bool _receiveClock = true;

    [ObservableProperty]
    private bool _sendProgramChanges = true;

    [ObservableProperty]
    private bool _receiveProgramChanges = true;

    [ObservableProperty]
    private bool _sendSysEx;

    [ObservableProperty]
    private bool _receiveSysEx;

    #endregion

    #region Collections

    /// <summary>
    /// Collection of discovered sessions on the network.
    /// </summary>
    public ObservableCollection<NetworkMidiSession> AvailableSessions { get; } = [];

    /// <summary>
    /// Collection of currently connected peers.
    /// </summary>
    public ObservableCollection<NetworkMidiPeer> ConnectedPeers { get; } = [];

    /// <summary>
    /// Collection of available virtual MIDI input ports.
    /// </summary>
    public ObservableCollection<VirtualMidiPort> InputPorts { get; } = [];

    /// <summary>
    /// Collection of available virtual MIDI output ports.
    /// </summary>
    public ObservableCollection<VirtualMidiPort> OutputPorts { get; } = [];

    /// <summary>
    /// Gets the channel enable states as an array.
    /// </summary>
    public bool[] ChannelEnableStates => [
        Channel1Enabled, Channel2Enabled, Channel3Enabled, Channel4Enabled,
        Channel5Enabled, Channel6Enabled, Channel7Enabled, Channel8Enabled,
        Channel9Enabled, Channel10Enabled, Channel11Enabled, Channel12Enabled,
        Channel13Enabled, Channel14Enabled, Channel15Enabled, Channel16Enabled
    ];

    #endregion

    #region Events

    /// <summary>
    /// Raised when a session is created.
    /// </summary>
    public event EventHandler<string>? SessionCreated;

    /// <summary>
    /// Raised when connected to a session.
    /// </summary>
    public event EventHandler<NetworkMidiSession>? SessionJoined;

    /// <summary>
    /// Raised when disconnected from a session.
    /// </summary>
    public event EventHandler? SessionLeft;

    /// <summary>
    /// Raised when MIDI data is received.
    /// </summary>
#pragma warning disable CS0067
    public event EventHandler<byte[]>? MidiDataReceived;
#pragma warning restore CS0067

    /// <summary>
    /// Raised when MIDI data is sent.
    /// </summary>
#pragma warning disable CS0067
    public event EventHandler<byte[]>? MidiDataSent;
#pragma warning restore CS0067

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new NetworkMIDIPanelViewModel.
    /// </summary>
    public NetworkMIDIPanelViewModel()
    {
        _activityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _activityTimer.Tick += OnActivityTimerTick;

        _latencyUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _latencyUpdateTimer.Tick += OnLatencyUpdateTick;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the Network MIDI panel.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        IsBusy = true;
        StatusMessage = "Initializing...";

        try
        {
            // Check Bonjour/mDNS availability
            await CheckBonjourAvailabilityAsync();

            // Load virtual ports
            LoadVirtualPorts();

            _activityTimer.Start();
            _latencyUpdateTimer.Start();
            _isInitialized = true;

            StatusMessage = IsNetworkMidiEnabled ? "Ready" : "Network MIDI disabled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Initialization failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Shuts down the Network MIDI panel.
    /// </summary>
    public void Shutdown()
    {
        _activityTimer.Stop();
        _latencyUpdateTimer.Stop();

        if (IsConnected || IsHosting)
        {
            LeaveSessionInternal();
        }

        _isInitialized = false;
    }

    private async Task CheckBonjourAvailabilityAsync()
    {
        BonjourStatus = "Checking Bonjour/mDNS...";

        await Task.Delay(500); // Simulate check

        // In a real implementation, check for Bonjour service
        IsBonjourAvailable = true;
        BonjourStatus = IsBonjourAvailable ? "Available" : "Not Available";
    }

    private void LoadVirtualPorts()
    {
        InputPorts.Clear();
        OutputPorts.Clear();

        // Add virtual input ports
        InputPorts.Add(new VirtualMidiPort { Name = "Network MIDI In 1", Index = 0, IsInput = true });
        InputPorts.Add(new VirtualMidiPort { Name = "Network MIDI In 2", Index = 1, IsInput = true });

        // Add virtual output ports
        OutputPorts.Add(new VirtualMidiPort { Name = "Network MIDI Out 1", Index = 0, IsInput = false });
        OutputPorts.Add(new VirtualMidiPort { Name = "Network MIDI Out 2", Index = 1, IsInput = false });

        if (InputPorts.Count > 0) SelectedInputPort = InputPorts[0];
        if (OutputPorts.Count > 0) SelectedOutputPort = OutputPorts[0];
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ToggleNetworkMidi()
    {
        IsNetworkMidiEnabled = !IsNetworkMidiEnabled;
    }

    [RelayCommand]
    private async Task RefreshSessionsAsync()
    {
        if (!IsNetworkMidiEnabled || IsDiscovering) return;

        await DiscoverSessionsAsync();
    }

    [RelayCommand]
    private async Task CreateSessionAsync()
    {
        if (!IsNetworkMidiEnabled) return;
        if (string.IsNullOrWhiteSpace(SessionName)) return;
        if (IsConnected || IsHosting) return;

        IsBusy = true;
        StatusMessage = "Creating session...";

        try
        {
            await Task.Delay(500); // Simulate session creation

            IsHosting = true;
            IsConnected = true;
            CurrentSessionName = SessionName;

            // Add self as first peer
            ConnectedPeers.Add(new NetworkMidiPeer
            {
                Name = "You (Host)",
                IpAddress = "Local",
                Port = DefaultRtpMidiPort,
                LatencyMs = 0,
                IsConnected = true
            });

            UpdateConnectionQuality();
            StatusMessage = $"Hosting: {CurrentSessionName}";
            SessionCreated?.Invoke(this, CurrentSessionName);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to create session: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task JoinSessionAsync()
    {
        if (!IsNetworkMidiEnabled) return;
        if (SelectedAvailableSession == null) return;
        if (IsConnected || IsHosting) return;

        IsBusy = true;
        StatusMessage = $"Joining {SelectedAvailableSession.SessionName}...";

        try
        {
            await Task.Delay(800); // Simulate connection

            IsConnected = true;
            CurrentSessionName = SelectedAvailableSession.SessionName;

            // Add host peer
            ConnectedPeers.Add(new NetworkMidiPeer
            {
                Name = "Host",
                IpAddress = SelectedAvailableSession.HostAddress,
                Port = SelectedAvailableSession.Port,
                LatencyMs = SelectedAvailableSession.LatencyMs,
                IsConnected = true
            });

            // Add self
            ConnectedPeers.Add(new NetworkMidiPeer
            {
                Name = "You",
                IpAddress = "Local",
                Port = DefaultRtpMidiPort,
                LatencyMs = 0,
                IsConnected = true
            });

            UpdateConnectionQuality();
            StatusMessage = $"Connected: {CurrentSessionName}";
            SessionJoined?.Invoke(this, SelectedAvailableSession);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to join: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void LeaveSession()
    {
        if (!IsConnected && !IsHosting) return;

        LeaveSessionInternal();
        StatusMessage = "Disconnected";
        SessionLeft?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SetAllChannels(bool enabled)
    {
        FilterAllChannels = enabled;
        if (enabled)
        {
            Channel1Enabled = Channel2Enabled = Channel3Enabled = Channel4Enabled =
            Channel5Enabled = Channel6Enabled = Channel7Enabled = Channel8Enabled =
            Channel9Enabled = Channel10Enabled = Channel11Enabled = Channel12Enabled =
            Channel13Enabled = Channel14Enabled = Channel15Enabled = Channel16Enabled = true;
        }
    }

    [RelayCommand]
    private void ResetStatistics()
    {
        TotalMessagesIn = 0;
        TotalMessagesOut = 0;

        foreach (var peer in ConnectedPeers)
        {
            peer.MessagesReceived = 0;
            peer.MessagesSent = 0;
        }
    }

    #endregion

    #region Property Changed Handlers

    partial void OnIsNetworkMidiEnabledChanged(bool value)
    {
        if (value)
        {
            StatusMessage = "Network MIDI enabled";
            _ = DiscoverSessionsAsync();
        }
        else
        {
            if (IsConnected || IsHosting)
            {
                LeaveSessionInternal();
            }
            AvailableSessions.Clear();
            StatusMessage = "Network MIDI disabled";
        }
    }

    partial void OnFilterAllChannelsChanged(bool value)
    {
        if (value)
        {
            SetAllChannels(true);
        }
    }

    #endregion

    #region Private Methods

    private async Task DiscoverSessionsAsync()
    {
        IsDiscovering = true;
        StatusMessage = "Discovering sessions...";

        AvailableSessions.Clear();

        try
        {
            await Task.Delay(1500); // Simulate network discovery

            // Add sample discovered sessions
            AvailableSessions.Add(new NetworkMidiSession
            {
                SessionName = "Studio A",
                HostAddress = "192.168.1.100",
                Port = DefaultRtpMidiPort,
                LatencyMs = 12,
                PeerCount = 3
            });

            AvailableSessions.Add(new NetworkMidiSession
            {
                SessionName = "Remote Session",
                HostAddress = "192.168.1.105",
                Port = DefaultRtpMidiPort,
                LatencyMs = 45,
                PeerCount = 1
            });

            AvailableSessions.Add(new NetworkMidiSession
            {
                SessionName = "Jam Room",
                HostAddress = "192.168.1.120",
                Port = DefaultRtpMidiPort,
                LatencyMs = 8,
                PeerCount = 2
            });

            StatusMessage = $"Found {AvailableSessions.Count} session(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Discovery failed: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    private void LeaveSessionInternal()
    {
        IsConnected = false;
        IsHosting = false;
        CurrentSessionName = string.Empty;
        ConnectedPeers.Clear();
        ConnectionQualityLevel = ConnectionQuality.Disconnected;
    }

    private void UpdateConnectionQuality()
    {
        if (!IsConnected && !IsHosting)
        {
            ConnectionQualityLevel = ConnectionQuality.Disconnected;
            return;
        }

        if (ConnectedPeers.Count == 0)
        {
            ConnectionQualityLevel = ConnectionQuality.Disconnected;
            return;
        }

        // Calculate average latency excluding self
        var remotePeers = ConnectedPeers.Where(p => p.IpAddress != "Local").ToList();
        if (remotePeers.Count == 0)
        {
            ConnectionQualityLevel = ConnectionQuality.Excellent;
            AverageLatencyMs = 0;
            return;
        }

        AverageLatencyMs = (int)remotePeers.Average(p => p.LatencyMs);

        ConnectionQualityLevel = AverageLatencyMs switch
        {
            < 20 => ConnectionQuality.Excellent,
            < 50 => ConnectionQuality.Good,
            < 100 => ConnectionQuality.Fair,
            _ => ConnectionQuality.Poor
        };
    }

    private void OnActivityTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        // Decay MIDI activity indicators
        if ((now - _lastMidiInActivity).TotalMilliseconds > ActivityDecayMs)
        {
            MidiInActive = false;
        }

        if ((now - _lastMidiOutActivity).TotalMilliseconds > ActivityDecayMs)
        {
            MidiOutActive = false;
        }
    }

    private void OnLatencyUpdateTick(object? sender, EventArgs e)
    {
        if (!IsConnected && !IsHosting) return;

        // Simulate latency updates for connected peers
        foreach (var peer in ConnectedPeers.Where(p => p.IpAddress != "Local"))
        {
            // Add slight random variation
            var variation = Random.Shared.Next(-5, 6);
            peer.LatencyMs = Math.Max(1, peer.LatencyMs + variation);
        }

        UpdateConnectionQuality();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Records incoming MIDI activity.
    /// </summary>
    public void RecordMidiIn()
    {
        MidiInActive = true;
        _lastMidiInActivity = DateTime.UtcNow;
        TotalMessagesIn++;
    }

    /// <summary>
    /// Records outgoing MIDI activity.
    /// </summary>
    public void RecordMidiOut()
    {
        MidiOutActive = true;
        _lastMidiOutActivity = DateTime.UtcNow;
        TotalMessagesOut++;
    }

    /// <summary>
    /// Checks if a specific MIDI channel is enabled for filtering.
    /// </summary>
    /// <param name="channel">Channel number (1-16).</param>
    /// <returns>True if the channel is enabled.</returns>
    public bool IsChannelEnabled(int channel)
    {
        if (FilterAllChannels) return true;
        if (channel < 1 || channel > 16) return false;
        return ChannelEnableStates[channel - 1];
    }

    #endregion
}
