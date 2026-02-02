// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for Ableton Link tempo synchronization.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Network;

/// <summary>
/// Connection status for Link session.
/// </summary>
public enum LinkConnectionStatus
{
    Disconnected,
    Searching,
    Connected
}

/// <summary>
/// Represents a connected peer in the Link session.
/// </summary>
public partial class LinkPeerInfo : ObservableObject
{
    /// <summary>
    /// Gets or sets the peer identifier.
    /// </summary>
    [ObservableProperty]
    private string _peerId = string.Empty;

    /// <summary>
    /// Gets or sets the peer application name.
    /// </summary>
    [ObservableProperty]
    private string _applicationName = string.Empty;

    /// <summary>
    /// Gets or sets when the peer was first discovered.
    /// </summary>
    [ObservableProperty]
    private DateTime _discoveredAt = DateTime.Now;

    /// <summary>
    /// Gets or sets whether this peer is the tempo leader.
    /// </summary>
    [ObservableProperty]
    private bool _isTempoLeader;

    /// <summary>
    /// Gets the display name for the peer.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(ApplicationName) ? PeerId : ApplicationName;

    /// <summary>
    /// Gets the formatted discovery time.
    /// </summary>
    public string DiscoveredTimeFormatted => DiscoveredAt.ToString("HH:mm:ss");
}

/// <summary>
/// ViewModel for Ableton Link tempo synchronization panel.
/// Provides Link session management, tempo sync, and peer discovery.
/// </summary>
public partial class LinkSyncViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _metronomeTimer;
    private bool _disposed;
    private DateTime _lastBeatTime;
    private double _beatAccumulator;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets whether Link is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isLinkEnabled;

    /// <summary>
    /// Gets or sets the connection status.
    /// </summary>
    [ObservableProperty]
    private LinkConnectionStatus _connectionStatus = LinkConnectionStatus.Disconnected;

    /// <summary>
    /// Gets or sets the number of connected peers.
    /// </summary>
    [ObservableProperty]
    private int _peerCount;

    /// <summary>
    /// Gets or sets the session tempo (BPM from Link session).
    /// </summary>
    [ObservableProperty]
    private double _sessionTempo = 120.0;

    /// <summary>
    /// Gets or sets the local tempo.
    /// </summary>
    [ObservableProperty]
    private double _localTempo = 120.0;

    /// <summary>
    /// Gets or sets whether tempo is locked to follow session.
    /// </summary>
    [ObservableProperty]
    private bool _isTempoLocked = true;

    /// <summary>
    /// Gets or sets whether sync is running (start/stop sync).
    /// </summary>
    [ObservableProperty]
    private bool _isSyncRunning;

    /// <summary>
    /// Gets or sets the current phase (beat position in bar, 0.0 to Quantum).
    /// </summary>
    [ObservableProperty]
    private double _currentPhase;

    /// <summary>
    /// Gets or sets the quantum (beats per bar for phase alignment).
    /// </summary>
    [ObservableProperty]
    private int _quantum = 4;

    /// <summary>
    /// Gets or sets the current beat (0 to Quantum-1).
    /// </summary>
    [ObservableProperty]
    private int _currentBeat;

    /// <summary>
    /// Gets or sets the latency compensation in milliseconds.
    /// </summary>
    [ObservableProperty]
    private double _latencyCompensation;

    /// <summary>
    /// Gets or sets whether start/stop sync is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isStartStopSyncEnabled = true;

    /// <summary>
    /// Gets or sets the current beat fraction for visual metronome (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private double _beatFraction;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of connected peers.
    /// </summary>
    public ObservableCollection<LinkPeerInfo> Peers { get; } = new();

    /// <summary>
    /// Gets the available quantum options.
    /// </summary>
    public ObservableCollection<int> QuantumOptions { get; } = new() { 1, 2, 3, 4, 5, 6, 7, 8, 12, 16 };

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the session tempo formatted as a string.
    /// </summary>
    public string SessionTempoFormatted => $"{SessionTempo:F1}";

    /// <summary>
    /// Gets the local tempo formatted as a string.
    /// </summary>
    public string LocalTempoFormatted => $"{LocalTempo:F1}";

    /// <summary>
    /// Gets the connection status text.
    /// </summary>
    public string ConnectionStatusText => ConnectionStatus switch
    {
        LinkConnectionStatus.Connected => $"Connected ({PeerCount} peer{(PeerCount != 1 ? "s" : "")})",
        LinkConnectionStatus.Searching => "Searching...",
        LinkConnectionStatus.Disconnected => "Disconnected",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets whether the panel is in connected state.
    /// </summary>
    public bool IsConnected => ConnectionStatus == LinkConnectionStatus.Connected;

    /// <summary>
    /// Gets whether the panel is searching for peers.
    /// </summary>
    public bool IsSearching => ConnectionStatus == LinkConnectionStatus.Searching;

    /// <summary>
    /// Gets the phase display text.
    /// </summary>
    public string PhaseDisplayText => $"{CurrentBeat + 1}.{(CurrentPhase % 1.0):F2}";

    /// <summary>
    /// Gets the latency compensation formatted.
    /// </summary>
    public string LatencyFormatted => $"{LatencyCompensation:F1} ms";

    /// <summary>
    /// Gets whether tempo controls should be enabled.
    /// </summary>
    public bool CanEditTempo => !IsTempoLocked || !IsLinkEnabled;

    /// <summary>
    /// Gets the beat indicators for the visual metronome.
    /// </summary>
    public ObservableCollection<BeatIndicator> BeatIndicators { get; } = new();

    #endregion

    #region Events

    /// <summary>
    /// Fired when Link is enabled or disabled.
    /// </summary>
    public event EventHandler<bool>? LinkStateChanged;

    /// <summary>
    /// Fired when tempo changes.
    /// </summary>
    public event EventHandler<double>? TempoChanged;

    /// <summary>
    /// Fired when a beat occurs (for external synchronization).
    /// </summary>
    public event EventHandler<int>? BeatOccurred;

    /// <summary>
    /// Fired when sync state changes.
    /// </summary>
    public event EventHandler<bool>? SyncStateChanged;

    /// <summary>
    /// Fired when peer count changes.
    /// </summary>
    public event EventHandler<int>? PeerCountChanged;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new LinkSyncViewModel.
    /// </summary>
    public LinkSyncViewModel()
    {
        // Initialize update timer (60fps for smooth visuals)
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        // Initialize metronome timer for beat simulation
        _metronomeTimer = new DispatcherTimer();
        UpdateMetronomeInterval();
        _metronomeTimer.Tick += OnMetronomeTimerTick;

        // Initialize beat indicators
        UpdateBeatIndicators();

        _lastBeatTime = DateTime.UtcNow;
    }

    #endregion

    #region Commands

    /// <summary>
    /// Toggles Link enabled state.
    /// </summary>
    [RelayCommand]
    private void ToggleLink()
    {
        IsLinkEnabled = !IsLinkEnabled;
    }

    /// <summary>
    /// Toggles tempo lock state.
    /// </summary>
    [RelayCommand]
    private void ToggleTempoLock()
    {
        IsTempoLocked = !IsTempoLocked;
    }

    /// <summary>
    /// Toggles sync running state.
    /// </summary>
    [RelayCommand]
    private void ToggleSync()
    {
        IsSyncRunning = !IsSyncRunning;
    }

    /// <summary>
    /// Toggles start/stop sync enabled.
    /// </summary>
    [RelayCommand]
    private void ToggleStartStopSync()
    {
        IsStartStopSyncEnabled = !IsStartStopSyncEnabled;
    }

    /// <summary>
    /// Increases the session tempo.
    /// </summary>
    [RelayCommand]
    private void IncreaseTempo()
    {
        if (CanEditTempo || !IsTempoLocked)
        {
            var newTempo = Math.Min(999.0, SessionTempo + 1.0);
            SetSessionTempo(newTempo);
        }
    }

    /// <summary>
    /// Decreases the session tempo.
    /// </summary>
    [RelayCommand]
    private void DecreaseTempo()
    {
        if (CanEditTempo || !IsTempoLocked)
        {
            var newTempo = Math.Max(20.0, SessionTempo - 1.0);
            SetSessionTempo(newTempo);
        }
    }

    /// <summary>
    /// Sets the session tempo to a specific value.
    /// </summary>
    /// <param name="tempo">The tempo in BPM.</param>
    [RelayCommand]
    private void SetTempo(double tempo)
    {
        SetSessionTempo(Math.Clamp(tempo, 20.0, 999.0));
    }

    /// <summary>
    /// Resets phase to the start of the bar.
    /// </summary>
    [RelayCommand]
    private void ResetPhase()
    {
        CurrentPhase = 0;
        CurrentBeat = 0;
        _beatAccumulator = 0;
        UpdateBeatIndicatorStates();
        StatusMessage = "Phase reset to start of bar";
    }

    /// <summary>
    /// Increases latency compensation.
    /// </summary>
    [RelayCommand]
    private void IncreaseLatency()
    {
        LatencyCompensation = Math.Min(500.0, LatencyCompensation + 1.0);
    }

    /// <summary>
    /// Decreases latency compensation.
    /// </summary>
    [RelayCommand]
    private void DecreaseLatency()
    {
        LatencyCompensation = Math.Max(-500.0, LatencyCompensation - 1.0);
    }

    /// <summary>
    /// Resets latency compensation to zero.
    /// </summary>
    [RelayCommand]
    private void ResetLatency()
    {
        LatencyCompensation = 0;
        StatusMessage = "Latency compensation reset";
    }

    /// <summary>
    /// Refreshes the peer list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshPeersAsync()
    {
        IsBusy = true;
        StatusMessage = "Refreshing peer list...";

        try
        {
            // Simulate peer discovery delay
            await Task.Delay(500);

            // In a real implementation, this would query the Link session
            // For now, we just refresh the display
            OnPropertyChanged(nameof(ConnectionStatusText));
            StatusMessage = $"Found {PeerCount} peer(s)";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Property Change Handlers

    partial void OnIsLinkEnabledChanged(bool value)
    {
        if (value)
        {
            ConnectionStatus = LinkConnectionStatus.Searching;
            _updateTimer.Start();
            _metronomeTimer.Start();

            // Simulate finding peers after a delay
            SimulateConnectionAsync();
        }
        else
        {
            ConnectionStatus = LinkConnectionStatus.Disconnected;
            _updateTimer.Stop();
            _metronomeTimer.Stop();
            PeerCount = 0;
            Peers.Clear();
            IsSyncRunning = false;
        }

        LinkStateChanged?.Invoke(this, value);
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(CanEditTempo));
    }

    partial void OnConnectionStatusChanged(LinkConnectionStatus value)
    {
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsSearching));
    }

    partial void OnPeerCountChanged(int value)
    {
        OnPropertyChanged(nameof(ConnectionStatusText));
        PeerCountChanged?.Invoke(this, value);
    }

    partial void OnSessionTempoChanged(double value)
    {
        OnPropertyChanged(nameof(SessionTempoFormatted));
        UpdateMetronomeInterval();

        if (IsTempoLocked)
        {
            LocalTempo = value;
        }

        TempoChanged?.Invoke(this, value);
    }

    partial void OnLocalTempoChanged(double value)
    {
        OnPropertyChanged(nameof(LocalTempoFormatted));

        if (!IsTempoLocked && IsLinkEnabled)
        {
            // When not locked, local tempo can differ
            UpdateMetronomeInterval();
        }
    }

    partial void OnIsTempoLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditTempo));

        if (value)
        {
            LocalTempo = SessionTempo;
        }
    }

    partial void OnIsSyncRunningChanged(bool value)
    {
        SyncStateChanged?.Invoke(this, value);
        StatusMessage = value ? "Sync started" : "Sync stopped";
    }

    partial void OnQuantumChanged(int value)
    {
        UpdateBeatIndicators();
        OnPropertyChanged(nameof(PhaseDisplayText));
    }

    partial void OnCurrentPhaseChanged(double value)
    {
        OnPropertyChanged(nameof(PhaseDisplayText));
    }

    partial void OnCurrentBeatChanged(int value)
    {
        OnPropertyChanged(nameof(PhaseDisplayText));
        UpdateBeatIndicatorStates();
        BeatOccurred?.Invoke(this, value);
    }

    partial void OnLatencyCompensationChanged(double value)
    {
        OnPropertyChanged(nameof(LatencyFormatted));
    }

    #endregion

    #region Private Methods

    private async void SimulateConnectionAsync()
    {
        // Simulate connection delay
        await Task.Delay(1500);

        if (!IsLinkEnabled) return;

        ConnectionStatus = LinkConnectionStatus.Connected;

        // Simulate discovering some peers
        await Task.Delay(500);

        if (!IsLinkEnabled) return;

        // Add some simulated peers
        Peers.Clear();
        var random = new Random();
        var peerApps = new[] { "Ableton Live", "Logic Pro", "Reason", "Bitwig Studio", "Max/MSP" };

        var numPeers = random.Next(1, 4);
        for (int i = 0; i < numPeers; i++)
        {
            Peers.Add(new LinkPeerInfo
            {
                PeerId = Guid.NewGuid().ToString()[..8],
                ApplicationName = peerApps[random.Next(peerApps.Length)],
                DiscoveredAt = DateTime.Now.AddSeconds(-random.Next(0, 60)),
                IsTempoLeader = i == 0
            });
        }

        PeerCount = Peers.Count;
        StatusMessage = $"Connected to Link session with {PeerCount} peer(s)";
    }

    private void UpdateMetronomeInterval()
    {
        // Calculate interval based on tempo
        // One beat = 60000ms / BPM
        var tempo = IsTempoLocked ? SessionTempo : LocalTempo;
        var beatInterval = 60000.0 / tempo;

        // Update at higher rate for smooth phase display
        _metronomeTimer.Interval = TimeSpan.FromMilliseconds(beatInterval / 24.0);
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        // Update beat fraction for smooth visual metronome
        var now = DateTime.UtcNow;
        var tempo = IsTempoLocked ? SessionTempo : LocalTempo;
        var beatDuration = 60.0 / tempo; // seconds per beat

        var elapsed = (now - _lastBeatTime).TotalSeconds;
        BeatFraction = (elapsed / beatDuration) % 1.0;
    }

    private void OnMetronomeTimerTick(object? sender, EventArgs e)
    {
        if (!IsSyncRunning) return;

        var tempo = IsTempoLocked ? SessionTempo : LocalTempo;
        var beatDuration = 60.0 / tempo; // seconds per beat

        _beatAccumulator += _metronomeTimer.Interval.TotalSeconds;

        // Update phase
        CurrentPhase = (_beatAccumulator / beatDuration) % Quantum;

        // Check for beat change
        var newBeat = (int)CurrentPhase;
        if (newBeat != CurrentBeat)
        {
            CurrentBeat = newBeat;
            _lastBeatTime = DateTime.UtcNow;
        }
    }

    private void UpdateBeatIndicators()
    {
        BeatIndicators.Clear();
        for (int i = 0; i < Quantum; i++)
        {
            BeatIndicators.Add(new BeatIndicator
            {
                Index = i,
                IsDownbeat = i == 0,
                IsActive = i == CurrentBeat
            });
        }
    }

    private void UpdateBeatIndicatorStates()
    {
        for (int i = 0; i < BeatIndicators.Count; i++)
        {
            BeatIndicators[i].IsActive = i == CurrentBeat;
        }
    }

    private void SetSessionTempo(double tempo)
    {
        SessionTempo = tempo;

        if (IsTempoLocked)
        {
            LocalTempo = tempo;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Enables Link synchronization.
    /// </summary>
    public void EnableLink()
    {
        IsLinkEnabled = true;
    }

    /// <summary>
    /// Disables Link synchronization.
    /// </summary>
    public void DisableLink()
    {
        IsLinkEnabled = false;
    }

    /// <summary>
    /// Starts sync playback.
    /// </summary>
    public void StartSync()
    {
        IsSyncRunning = true;
    }

    /// <summary>
    /// Stops sync playback.
    /// </summary>
    public void StopSync()
    {
        IsSyncRunning = false;
    }

    /// <summary>
    /// Sets the quantum value.
    /// </summary>
    /// <param name="quantum">Beats per bar (1-16).</param>
    public void SetQuantum(int quantum)
    {
        Quantum = Math.Clamp(quantum, 1, 16);
    }

    /// <summary>
    /// Updates the session state from an external Link source.
    /// </summary>
    /// <param name="tempo">Current tempo.</param>
    /// <param name="phase">Current phase.</param>
    /// <param name="isPlaying">Whether the session is playing.</param>
    public void UpdateFromLinkSession(double tempo, double phase, bool isPlaying)
    {
        SessionTempo = tempo;

        if (IsTempoLocked)
        {
            LocalTempo = tempo;
        }

        CurrentPhase = phase % Quantum;
        CurrentBeat = (int)CurrentPhase;

        if (IsStartStopSyncEnabled)
        {
            IsSyncRunning = isPlaying;
        }
    }

    /// <summary>
    /// Adds a peer to the session.
    /// </summary>
    /// <param name="peer">The peer information.</param>
    public void AddPeer(LinkPeerInfo peer)
    {
        Peers.Add(peer);
        PeerCount = Peers.Count;
    }

    /// <summary>
    /// Removes a peer from the session.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    public void RemovePeer(string peerId)
    {
        var peer = Peers.FirstOrDefault(p => p.PeerId == peerId);
        if (peer != null)
        {
            Peers.Remove(peer);
            PeerCount = Peers.Count;
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the ViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _updateTimer.Stop();
        _metronomeTimer.Stop();
        Peers.Clear();

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Represents a beat indicator in the visual metronome.
/// </summary>
public partial class BeatIndicator : ObservableObject
{
    /// <summary>
    /// Gets or sets the beat index.
    /// </summary>
    [ObservableProperty]
    private int _index;

    /// <summary>
    /// Gets or sets whether this is the downbeat (first beat).
    /// </summary>
    [ObservableProperty]
    private bool _isDownbeat;

    /// <summary>
    /// Gets or sets whether this beat is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Gets the display number (1-based).
    /// </summary>
    public int DisplayNumber => Index + 1;
}
