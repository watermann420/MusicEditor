// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service for synchronizing transport state across collaboration sessions.

using System;
using System.Threading;
using System.Windows;
using MusicEngine.Infrastructure.Collaboration;

namespace MusicEngineEditor.Services;

/// <summary>
/// Event arguments for transport sync events.
/// </summary>
public class TransportSyncEventArgs : EventArgs
{
    /// <summary>
    /// Gets the transport state.
    /// </summary>
    public TransportState State { get; }

    /// <summary>
    /// Gets the position in beats.
    /// </summary>
    public double PositionBeats { get; }

    /// <summary>
    /// Gets the tempo in BPM.
    /// </summary>
    public double Tempo { get; }

    /// <summary>
    /// Gets the peer ID that sent the sync.
    /// </summary>
    public Guid? SourcePeerId { get; }

    /// <summary>
    /// Gets whether the sync originated from a remote peer.
    /// </summary>
    public bool IsRemote { get; }

    public TransportSyncEventArgs(TransportState state, double positionBeats, double tempo,
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
/// Service for synchronizing transport (play/stop/pause/seek) state between collaborators.
/// </summary>
public class TransportSyncService : IDisposable
{
    private static TransportSyncService? _instance;
    private static readonly object _instanceLock = new();

    private CollaborationSession? _session;
    private CollaborationClient? _client;
    private CollaborationServer? _server;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isApplyingRemoteSync;
    private DateTime _lastSyncSent = DateTime.MinValue;
    private readonly TimeSpan _syncThrottleInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets the singleton instance of the service.
    /// </summary>
    public static TransportSyncService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new TransportSyncService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets or sets whether transport synchronization is enabled.
    /// When enabled, transport state changes (play, stop, pause, seek) are synchronized with collaborators.
    /// </summary>
    public bool TransportSyncEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this instance should follow remote transport changes.
    /// When false, local transport can be controlled independently.
    /// </summary>
    public bool FollowRemoteTransport { get; set; } = true;

    /// <summary>
    /// Gets or sets whether only the host can control transport.
    /// When true, non-host peers can only follow but not initiate transport changes.
    /// </summary>
    public bool HostOnlyTransportControl { get; set; } = false;

    /// <summary>
    /// Gets whether the current session allows transport control.
    /// </summary>
    public bool CanControlTransport
    {
        get
        {
            if (!TransportSyncEnabled) return true;
            if (_session == null) return true;
            if (!HostOnlyTransportControl) return true;
            return _session.IsHost;
        }
    }

    /// <summary>
    /// Gets whether the service is currently attached to a collaboration session.
    /// </summary>
    public bool IsAttached => _session != null && _session.State == SessionState.Active;

    /// <summary>
    /// Raised when a transport sync is received from a remote peer.
    /// </summary>
    public event EventHandler<TransportSyncEventArgs>? RemoteTransportSyncReceived;

    /// <summary>
    /// Raised when transport sync is sent to remote peers.
    /// </summary>
    public event EventHandler<TransportSyncEventArgs>? TransportSyncSent;

    private TransportSyncService()
    {
        // Subscribe to PlaybackService events
        SubscribeToPlaybackService();
    }

    /// <summary>
    /// Attaches the service to a collaboration session.
    /// </summary>
    /// <param name="session">The collaboration session.</param>
    /// <param name="client">Optional collaboration client (for sending messages as a client).</param>
    /// <param name="server">Optional collaboration server (for broadcasting messages as host).</param>
    public void AttachSession(CollaborationSession session, CollaborationClient? client = null,
        CollaborationServer? server = null)
    {
        lock (_lock)
        {
            if (_session != null)
            {
                DetachSession();
            }

            _session = session;
            _client = client;
            _server = server;

            // Subscribe to session events
            _session.TransportSyncReceived += Session_TransportSyncReceived;
            _session.StateChanged += Session_StateChanged;
        }
    }

    /// <summary>
    /// Detaches from the current collaboration session.
    /// </summary>
    public void DetachSession()
    {
        lock (_lock)
        {
            if (_session != null)
            {
                _session.TransportSyncReceived -= Session_TransportSyncReceived;
                _session.StateChanged -= Session_StateChanged;
                _session = null;
            }

            _client = null;
            _server = null;
        }
    }

    /// <summary>
    /// Sends a transport sync message to all collaborators.
    /// </summary>
    /// <param name="state">The transport state.</param>
    /// <param name="positionBeats">The current position in beats.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <param name="loopEnabled">Whether loop is enabled.</param>
    /// <param name="loopStart">Loop start position in beats.</param>
    /// <param name="loopEnd">Loop end position in beats.</param>
    public void SendTransportSync(TransportState state, double positionBeats, double tempo,
        bool loopEnabled = false, double loopStart = 0, double loopEnd = 0)
    {
        if (!TransportSyncEnabled || _session == null || _session.State != SessionState.Active)
            return;

        // Don't send if we're applying remote sync
        if (_isApplyingRemoteSync)
            return;

        // Check if we can control transport
        if (!CanControlTransport)
            return;

        // Throttle sync messages to avoid flooding
        var now = DateTime.UtcNow;
        if (state == TransportState.Playing && (now - _lastSyncSent) < _syncThrottleInterval)
            return;

        _lastSyncSent = now;

        try
        {
            var syncMessage = new TransportSyncMessage
            {
                PeerId = _session.LocalPeer?.Id ?? Guid.Empty,
                SessionId = _session.SessionId,
                State = state,
                PositionBeats = positionBeats,
                Tempo = tempo,
                LoopEnabled = loopEnabled,
                LoopStart = loopStart,
                LoopEnd = loopEnd,
                TimeSignatureNumerator = 4,
                TimeSignatureDenominator = 4
            };

            // Broadcast the message
            var broadcastMessage = _session.BroadcastChange(syncMessage);

            // Send through client or server
            if (_server != null && _server.IsRunning)
            {
                // As host, broadcast to all clients
                _ = _server.BroadcastAsync(broadcastMessage, _session.LocalPeer?.Id);
            }
            else if (_client != null && _client.IsConnected)
            {
                // As client, send to server
                _client.Send(broadcastMessage);
            }

            // Raise event
            var args = new TransportSyncEventArgs(state, positionBeats, tempo,
                _session.LocalPeer?.Id, false);
            TransportSyncSent?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send transport sync: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a play command to all collaborators.
    /// </summary>
    /// <param name="fromBeat">The beat position to start playing from.</param>
    public void SendPlay(double fromBeat)
    {
        var playbackService = PlaybackService.Instance;
        SendTransportSync(TransportState.Playing, fromBeat, playbackService.BPM,
            playbackService.LoopEnabled, playbackService.LoopStart, playbackService.LoopEnd);
    }

    /// <summary>
    /// Sends a stop command to all collaborators.
    /// </summary>
    public void SendStop()
    {
        var playbackService = PlaybackService.Instance;
        SendTransportSync(TransportState.Stopped, 0, playbackService.BPM,
            playbackService.LoopEnabled, playbackService.LoopStart, playbackService.LoopEnd);
    }

    /// <summary>
    /// Sends a pause command to all collaborators.
    /// </summary>
    /// <param name="atBeat">The beat position where playback is paused.</param>
    public void SendPause(double atBeat)
    {
        var playbackService = PlaybackService.Instance;
        SendTransportSync(TransportState.Paused, atBeat, playbackService.BPM,
            playbackService.LoopEnabled, playbackService.LoopStart, playbackService.LoopEnd);
    }

    /// <summary>
    /// Sends a seek command to all collaborators.
    /// </summary>
    /// <param name="toBeat">The beat position to seek to.</param>
    public void SendSeek(double toBeat)
    {
        var playbackService = PlaybackService.Instance;
        var state = playbackService.IsPlaying ? TransportState.Playing :
            (playbackService.IsPaused ? TransportState.Paused : TransportState.Stopped);

        SendTransportSync(state, toBeat, playbackService.BPM,
            playbackService.LoopEnabled, playbackService.LoopStart, playbackService.LoopEnd);
    }

    /// <summary>
    /// Sends a tempo change to all collaborators.
    /// </summary>
    /// <param name="newTempo">The new tempo in BPM.</param>
    public void SendTempoChange(double newTempo)
    {
        var playbackService = PlaybackService.Instance;
        var state = playbackService.IsPlaying ? TransportState.Playing :
            (playbackService.IsPaused ? TransportState.Paused : TransportState.Stopped);

        SendTransportSync(state, playbackService.CurrentBeat, newTempo,
            playbackService.LoopEnabled, playbackService.LoopStart, playbackService.LoopEnd);
    }

    private void Session_TransportSyncReceived(object? sender, ChangeEventArgs e)
    {
        if (!TransportSyncEnabled || !FollowRemoteTransport)
            return;

        // Don't process our own messages
        if (e.IsLocal)
            return;

        if (e.Message is TransportSyncMessage syncMessage)
        {
            ApplyRemoteTransportSync(syncMessage, e.SourcePeer?.Id);
        }
    }

    private void ApplyRemoteTransportSync(TransportSyncMessage syncMessage, Guid? sourcePeerId)
    {
        // Apply on UI thread
        Application.Current?.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                _isApplyingRemoteSync = true;
                try
                {
                    var playbackService = PlaybackService.Instance;

                    // Update tempo if changed
                    if (Math.Abs(playbackService.BPM - syncMessage.Tempo) > 0.001)
                    {
                        playbackService.BPM = syncMessage.Tempo;
                    }

                    // Update loop settings
                    playbackService.LoopEnabled = syncMessage.LoopEnabled;
                    if (syncMessage.LoopEnabled)
                    {
                        playbackService.SetLoopRegion(syncMessage.LoopStart, syncMessage.LoopEnd);
                    }

                    // Apply transport state
                    switch (syncMessage.State)
                    {
                        case TransportState.Playing:
                            // Seek to position and start playing
                            playbackService.SetPosition(syncMessage.PositionBeats);
                            if (!playbackService.IsPlaying)
                            {
                                playbackService.Play();
                            }
                            break;

                        case TransportState.Stopped:
                            if (playbackService.IsPlaying || playbackService.IsPaused)
                            {
                                playbackService.Stop();
                            }
                            break;

                        case TransportState.Paused:
                            playbackService.SetPosition(syncMessage.PositionBeats);
                            if (playbackService.IsPlaying)
                            {
                                playbackService.Pause();
                            }
                            break;

                        case TransportState.Recording:
                            // Recording sync can be handled by RecordingService
                            playbackService.SetPosition(syncMessage.PositionBeats);
                            break;
                    }

                    // Raise event
                    var args = new TransportSyncEventArgs(syncMessage.State, syncMessage.PositionBeats,
                        syncMessage.Tempo, sourcePeerId, true);
                    RemoteTransportSyncReceived?.Invoke(this, args);
                }
                finally
                {
                    _isApplyingRemoteSync = false;
                }
            }
        });
    }

    private void Session_StateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        if (e.NewState == SessionState.Inactive || e.NewState == SessionState.Closing)
        {
            // Session ended, could reset sync state
        }
    }

    private void SubscribeToPlaybackService()
    {
        var playbackService = PlaybackService.Instance;

        playbackService.PlaybackStarted += PlaybackService_PlaybackStarted;
        playbackService.PlaybackStopped += PlaybackService_PlaybackStopped;
        playbackService.PlaybackPaused += PlaybackService_PlaybackPaused;
        playbackService.PlaybackResumed += PlaybackService_PlaybackResumed;
        playbackService.PositionChanged += PlaybackService_PositionChanged;
        playbackService.BpmChanged += PlaybackService_BpmChanged;
    }

    private void UnsubscribeFromPlaybackService()
    {
        var playbackService = PlaybackService.Instance;

        playbackService.PlaybackStarted -= PlaybackService_PlaybackStarted;
        playbackService.PlaybackStopped -= PlaybackService_PlaybackStopped;
        playbackService.PlaybackPaused -= PlaybackService_PlaybackPaused;
        playbackService.PlaybackResumed -= PlaybackService_PlaybackResumed;
        playbackService.PositionChanged -= PlaybackService_PositionChanged;
        playbackService.BpmChanged -= PlaybackService_BpmChanged;
    }

    private void PlaybackService_PlaybackStarted(object? sender, PlaybackStartedEventArgs e)
    {
        if (!_isApplyingRemoteSync)
        {
            SendPlay(e.StartBeat);
        }
    }

    private void PlaybackService_PlaybackStopped(object? sender, PlaybackStoppedEventArgs e)
    {
        if (!_isApplyingRemoteSync)
        {
            SendStop();
        }
    }

    private void PlaybackService_PlaybackPaused(object? sender, EventArgs e)
    {
        if (!_isApplyingRemoteSync)
        {
            var playbackService = PlaybackService.Instance;
            SendPause(playbackService.CurrentBeat);
        }
    }

    private void PlaybackService_PlaybackResumed(object? sender, EventArgs e)
    {
        if (!_isApplyingRemoteSync)
        {
            var playbackService = PlaybackService.Instance;
            SendPlay(playbackService.CurrentBeat);
        }
    }

    private void PlaybackService_PositionChanged(object? sender, PositionChangedEventArgs e)
    {
        // Only sync seek operations, not continuous position updates
        // Continuous updates are handled by the transport state sync
        if (!_isApplyingRemoteSync && PlaybackService.Instance.IsScrubbing)
        {
            SendSeek(e.Beat);
        }
    }

    private void PlaybackService_BpmChanged(object? sender, BpmChangedEventArgs e)
    {
        if (!_isApplyingRemoteSync)
        {
            SendTempoChange(e.NewBpm);
        }
    }

    /// <summary>
    /// Disposes the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        UnsubscribeFromPlaybackService();
        DetachSession();

        _disposed = true;
    }
}
