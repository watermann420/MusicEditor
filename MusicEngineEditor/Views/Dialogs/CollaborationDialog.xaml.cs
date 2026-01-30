// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation with session persistence.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngine.Infrastructure.Collaboration;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Collaboration dialog for real-time multi-user editing sessions.
/// </summary>
public partial class CollaborationDialog : Window
{
    private bool _isConnected;
    private bool _isHost;
    private string _sessionCode = string.Empty;
    private string _sessionName = string.Empty;
    private string _username = string.Empty;

    // Real collaboration infrastructure
    private CollaborationSession? _collaborationSession;
    private CollaborationServer? _collaborationServer;
    private CollaborationClient? _collaborationClient;

    // Session persistence service
    private readonly CollaborationSessionService _sessionService;

    /// <summary>
    /// Collection of connected peers.
    /// </summary>
    public ObservableCollection<CollaborationPeer> Peers { get; } = new();

    /// <summary>
    /// Collection of chat messages.
    /// </summary>
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    /// <summary>
    /// Collection of recent sessions.
    /// </summary>
    public ObservableCollection<CollaborationSessionInfo> RecentSessions { get; } = new();

    /// <summary>
    /// Gets whether the current user is the session host.
    /// </summary>
    public bool IsHost => _isHost;

    /// <summary>
    /// Gets or sets whether transport sync is enabled.
    /// </summary>
    public bool TransportSyncEnabled
    {
        get => TransportSyncService.Instance.TransportSyncEnabled;
        set => TransportSyncService.Instance.TransportSyncEnabled = value;
    }

    /// <summary>
    /// Gets or sets whether this instance follows remote transport changes.
    /// </summary>
    public bool FollowRemoteTransport
    {
        get => TransportSyncService.Instance.FollowRemoteTransport;
        set => TransportSyncService.Instance.FollowRemoteTransport = value;
    }

    /// <summary>
    /// Gets or sets whether only the host can control transport.
    /// </summary>
    public bool HostOnlyTransportControl
    {
        get => TransportSyncService.Instance.HostOnlyTransportControl;
        set => TransportSyncService.Instance.HostOnlyTransportControl = value;
    }

    /// <summary>
    /// Creates a new collaboration dialog.
    /// </summary>
    public CollaborationDialog()
    {
        InitializeComponent();

        _sessionService = new CollaborationSessionService();

        PeerList.ItemsSource = Peers;
        ChatMessages.ItemsSource = Messages;
        RecentSessionsList.ItemsSource = RecentSessions;

        // Set default username based on environment
        var defaultUsername = Environment.UserName;
        CreateUsername.Text = defaultUsername;
        JoinUsername.Text = defaultUsername;

        // Subscribe to transport sync events
        TransportSyncService.Instance.RemoteTransportSyncReceived += OnRemoteTransportSyncReceived;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRecentSessionsAsync();
    }

    private async Task LoadRecentSessionsAsync()
    {
        try
        {
            await _sessionService.LoadSessionsAsync();

            RecentSessions.Clear();
            foreach (var session in _sessionService.Sessions)
            {
                RecentSessions.Add(session);
            }

            UpdateRecentSessionsUI();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load recent sessions: {ex.Message}");
        }
    }

    private void UpdateRecentSessionsUI()
    {
        var hasRecentSessions = RecentSessions.Count > 0;

        RecentSessionsEmpty.Visibility = hasRecentSessions ? Visibility.Collapsed : Visibility.Visible;
        RecentSessionsList.Visibility = hasRecentSessions ? Visibility.Visible : Visibility.Collapsed;
        QuickReconnectButton.Visibility = hasRecentSessions ? Visibility.Visible : Visibility.Collapsed;

        RecentSessionCount.Text = RecentSessions.Count == 1
            ? "1 session"
            : $"{RecentSessions.Count} sessions";
    }

    private void OnRemoteTransportSyncReceived(object? sender, TransportSyncEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            string stateStr = e.State switch
            {
                TransportState.Playing => "started playback",
                TransportState.Stopped => "stopped playback",
                TransportState.Paused => "paused playback",
                TransportState.Recording => "started recording",
                _ => "changed transport"
            };

            var peerName = "A collaborator";
            if (e.SourcePeerId.HasValue && _collaborationSession != null)
            {
                var peer = _collaborationSession.GetPeer(e.SourcePeerId.Value);
                if (peer != null)
                {
                    peerName = peer.Name;
                }
            }

            AddSystemMessage($"{peerName} {stateStr} at beat {e.PositionBeats:F1}");
        });
    }

    private async void CreateSession_Click(object sender, RoutedEventArgs e)
    {
        var sessionName = CreateSessionName.Text.Trim();
        var username = CreateUsername.Text.Trim();

        if (string.IsNullOrEmpty(sessionName))
        {
            MessageBox.Show("Please enter a session name.", "Create Session",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(username))
        {
            MessageBox.Show("Please enter your name.", "Create Session",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isHost = true;
        _sessionName = sessionName;
        _sessionCode = GenerateSessionCode();
        _username = username;

        await ConnectToSessionAsync();

        // Add self as host
        AddPeer(new CollaborationPeer
        {
            Name = $"{username} (Host)",
            Role = "Host",
            RoleBrush = FindResource("HostBadgeBrush") as Brush ?? Brushes.Green,
            ColorBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF))
        });

        // Save session to history
        await SaveCurrentSessionAsync();

        StatusText.Text = $"Session created. Share code: {_sessionCode}";
    }

    private async void JoinSession_Click(object sender, RoutedEventArgs e)
    {
        var sessionCode = JoinSessionCode.Text.Trim().ToUpperInvariant();
        var username = JoinUsername.Text.Trim();

        if (string.IsNullOrEmpty(sessionCode))
        {
            MessageBox.Show("Please enter a session code.", "Join Session",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(username))
        {
            MessageBox.Show("Please enter your name.", "Join Session",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isHost = false;
        _sessionCode = sessionCode;
        _sessionName = "Remote Session";
        _username = username;

        await ConnectToSessionAsync();

        // Simulate receiving peer list
        AddPeer(new CollaborationPeer
        {
            Name = "Host User",
            Role = "Host",
            RoleBrush = FindResource("HostBadgeBrush") as Brush ?? Brushes.Green,
            ColorBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))
        });

        AddPeer(new CollaborationPeer
        {
            Name = username,
            Role = "Editor",
            RoleBrush = FindResource("EditorBadgeBrush") as Brush ?? Brushes.Blue,
            ColorBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF))
        });

        // Save session to history
        await SaveCurrentSessionAsync();

        StatusText.Text = $"Joined session: {_sessionName}";
    }

    private async Task SaveCurrentSessionAsync()
    {
        var sessionInfo = new CollaborationSessionInfo
        {
            SessionName = _sessionName,
            SessionCode = _sessionCode,
            Username = _username,
            WasHost = _isHost,
            HasPassword = _isHost
                ? CreateSessionPassword.Password.Length > 0
                : JoinSessionPassword.Password.Length > 0
        };

        await _sessionService.AddOrUpdateSessionAsync(sessionInfo);
        await LoadRecentSessionsAsync();
    }

    private async Task ConnectToSessionAsync()
    {
        // Show connecting state
        ConnectionIndicator.Fill = FindResource("ConnectingBrush") as Brush ?? Brushes.Orange;
        ConnectionStatus.Text = "Connecting...";
        StatusText.Text = "Connecting to session...";

        try
        {
            // Create collaboration session
            _collaborationSession = new CollaborationSession();

            if (_isHost)
            {
                // Create session as host
                var sessionId = _collaborationSession.CreateSession(_sessionName, _username);
                _sessionCode = sessionId.ToString("N")[..8].ToUpperInvariant();

                // Start server
                _collaborationServer = new CollaborationServer(_collaborationSession);
                await _collaborationServer.StartAsync(CollaborationProtocol.DefaultPort);

                // Attach cursor and transport sync services
                CollaboratorCursorService.Instance.AttachSession(_collaborationSession, null, _collaborationServer);
            }
            else
            {
                // Join session as client
                _collaborationClient = new CollaborationClient(_collaborationSession);

                // Parse session code to get host address (for demo purposes, use localhost)
                // In a real implementation, you would have a session discovery service
                await _collaborationClient.ConnectAsync("localhost", CollaborationProtocol.DefaultPort);

                // Attach cursor and transport sync services
                CollaboratorCursorService.Instance.AttachSession(_collaborationSession, _collaborationClient, null);
            }

            // Subscribe to session events
            _collaborationSession.PeerJoined += OnPeerJoined;
            _collaborationSession.PeerLeft += OnPeerLeft;
            _collaborationSession.ChatReceived += OnChatReceived;

            // Simulate connection delay for UI smoothness
            await Task.Delay(500);

            // Update to connected state
            _isConnected = true;
            ConnectionIndicator.Fill = FindResource("ConnectedBrush") as Brush ?? Brushes.Green;
            ConnectionStatus.Text = "Connected";

            // Switch to connected view
            NotConnectedView.Visibility = Visibility.Collapsed;
            ConnectedView.Visibility = Visibility.Visible;

            SessionNameDisplay.Text = _sessionName;
            SessionCodeDisplay.Text = _sessionCode;

            UpdateParticipantCount();

            // Add system message
            AddSystemMessage("You have joined the session.");
            if (TransportSyncEnabled)
            {
                AddSystemMessage("Transport sync is enabled. Playback will be synchronized with collaborators.");
            }
        }
        catch (Exception ex)
        {
            _isConnected = false;
            ConnectionIndicator.Fill = FindResource("DisconnectedBrush") as Brush ?? Brushes.Red;
            ConnectionStatus.Text = "Connection failed";
            StatusText.Text = $"Failed to connect: {ex.Message}";

            CleanupCollaboration();
        }
    }

    private void OnPeerJoined(object? sender, PeerEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            AddPeer(new CollaborationPeer
            {
                Name = e.Peer.Name,
                Role = e.Peer.Role.ToString(),
                RoleBrush = e.Peer.IsHost
                    ? FindResource("HostBadgeBrush") as Brush ?? Brushes.Green
                    : FindResource("EditorBadgeBrush") as Brush ?? Brushes.Blue,
                ColorBrush = new SolidColorBrush(Color.FromArgb(
                    (byte)((e.Peer.Color >> 24) & 0xFF),
                    (byte)((e.Peer.Color >> 16) & 0xFF),
                    (byte)((e.Peer.Color >> 8) & 0xFF),
                    (byte)(e.Peer.Color & 0xFF)))
            });

            AddSystemMessage($"{e.Peer.Name} joined the session.");
        });
    }

    private void OnPeerLeft(object? sender, PeerEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var peerToRemove = Peers.FirstOrDefault(p => p.Name == e.Peer.Name);
            if (peerToRemove != null)
            {
                RemovePeer(peerToRemove);
            }

            AddSystemMessage($"{e.Peer.Name} left the session. {e.Message ?? ""}");
        });
    }

    private void OnChatReceived(object? sender, ChatReceivedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Messages.Add(new ChatMessage
            {
                SenderName = e.Sender.Name,
                Message = e.Text,
                Timestamp = e.ReceivedAt,
                ColorBrush = new SolidColorBrush(Color.FromArgb(
                    (byte)((e.Sender.Color >> 24) & 0xFF),
                    (byte)((e.Sender.Color >> 16) & 0xFF),
                    (byte)((e.Sender.Color >> 8) & 0xFF),
                    (byte)(e.Sender.Color & 0xFF)))
            });

            if (ChatMessages.Items.Count > 0)
            {
                ChatMessages.ScrollIntoView(ChatMessages.Items[ChatMessages.Items.Count - 1]);
            }
        });
    }

    private void CleanupCollaboration()
    {
        if (_collaborationSession != null)
        {
            _collaborationSession.PeerJoined -= OnPeerJoined;
            _collaborationSession.PeerLeft -= OnPeerLeft;
            _collaborationSession.ChatReceived -= OnChatReceived;
        }

        CollaboratorCursorService.Instance.DetachSession();

        _collaborationServer?.Dispose();
        _collaborationServer = null;

        _collaborationClient?.Dispose();
        _collaborationClient = null;

        _collaborationSession?.Dispose();
        _collaborationSession = null;
    }

    private void LeaveSession_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to leave this session?",
            "Leave Session",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            DisconnectFromSession();
        }
    }

    private void DisconnectFromSession()
    {
        _isConnected = false;
        _isHost = false;
        _sessionCode = string.Empty;
        _sessionName = string.Empty;
        _username = string.Empty;

        // Clean up collaboration infrastructure
        CleanupCollaboration();

        Peers.Clear();
        Messages.Clear();

        ConnectionIndicator.Fill = FindResource("DisconnectedBrush") as Brush ?? Brushes.Red;
        ConnectionStatus.Text = "Disconnected";

        ConnectedView.Visibility = Visibility.Collapsed;
        NotConnectedView.Visibility = Visibility.Visible;

        StatusText.Text = "Disconnected from session";
    }

    private void CopySessionCode_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_sessionCode))
        {
            System.Windows.Clipboard.SetText(_sessionCode);
            StatusText.Text = "Session code copied to clipboard";
        }
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendMessage();
            e.Handled = true;
        }
    }

    private void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void SendMessage()
    {
        var message = ChatInput.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        // Add message to chat locally
        var localColor = _collaborationSession?.LocalPeer?.Color ?? 0xFF4B6EAF;
        Messages.Add(new ChatMessage
        {
            SenderName = string.IsNullOrEmpty(_username) ? "You" : _username,
            Message = message,
            Timestamp = DateTime.Now,
            ColorBrush = new SolidColorBrush(Color.FromArgb(
                (byte)((localColor >> 24) & 0xFF),
                (byte)((localColor >> 16) & 0xFF),
                (byte)((localColor >> 8) & 0xFF),
                (byte)(localColor & 0xFF)))
        });

        // Send through collaboration session
        if (_collaborationSession != null && _collaborationSession.State == SessionState.Active)
        {
            try
            {
                var chatMessage = _collaborationSession.SendChat(message);
                var broadcastMessage = _collaborationSession.BroadcastChange(chatMessage);

                if (_collaborationServer != null && _collaborationServer.IsRunning)
                {
                    _ = _collaborationServer.BroadcastAsync(broadcastMessage, _collaborationSession.LocalPeer?.Id);
                }
                else if (_collaborationClient != null && _collaborationClient.IsConnected)
                {
                    _collaborationClient.Send(broadcastMessage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to send chat message: {ex.Message}");
            }
        }

        ChatInput.Clear();
        ChatInput.Focus();

        // Scroll to bottom
        if (ChatMessages.Items.Count > 0)
        {
            ChatMessages.ScrollIntoView(ChatMessages.Items[ChatMessages.Items.Count - 1]);
        }
    }

    private void AddSystemMessage(string message)
    {
        Messages.Add(new ChatMessage
        {
            SenderName = "System",
            Message = message,
            Timestamp = DateTime.Now,
            ColorBrush = FindResource("SecondaryForegroundBrush") as Brush ?? Brushes.Gray,
            IsSystemMessage = true
        });
    }

    private void AddPeer(CollaborationPeer peer)
    {
        Peers.Add(peer);
        UpdateParticipantCount();
    }

    private void RemovePeer(CollaborationPeer peer)
    {
        Peers.Remove(peer);
        UpdateParticipantCount();
    }

    private void UpdateParticipantCount()
    {
        ParticipantCount.Text = $"{Peers.Count} connected";
    }

    private static string GenerateSessionCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        var code = new char[6];
        for (int i = 0; i < 6; i++)
        {
            code[i] = chars[random.Next(chars.Length)];
        }
        return new string(code);
    }

    private void RecentSessionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentSessionsList.SelectedItem is CollaborationSessionInfo session)
        {
            // Pre-fill the join form with session info
            JoinSessionCode.Text = session.SessionCode;
            JoinUsername.Text = string.IsNullOrEmpty(session.Username) ? Environment.UserName : session.Username;

            // Also update create form username
            CreateUsername.Text = string.IsNullOrEmpty(session.Username) ? Environment.UserName : session.Username;
        }
    }

    private async void RecentSessionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentSessionsList.SelectedItem is CollaborationSessionInfo session)
        {
            await QuickReconnectToSessionAsync(session);
        }
    }

    private async void QuickReconnect_Click(object sender, RoutedEventArgs e)
    {
        var recentSession = await _sessionService.GetMostRecentSessionAsync();
        if (recentSession != null)
        {
            await QuickReconnectToSessionAsync(recentSession);
        }
        else
        {
            MessageBox.Show("No recent sessions to reconnect to.", "Quick Reconnect",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task QuickReconnectToSessionAsync(CollaborationSessionInfo session)
    {
        // Check if session requires password
        if (session.HasPassword)
        {
            var result = MessageBox.Show(
                $"This session may require a password. Continue reconnecting to '{session.SessionName}'?",
                "Quick Reconnect",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _isHost = session.WasHost;
        _sessionCode = session.SessionCode;
        _sessionName = session.SessionName;
        _username = string.IsNullOrEmpty(session.Username) ? Environment.UserName : session.Username;

        await ConnectToSessionAsync();

        if (_isHost)
        {
            AddPeer(new CollaborationPeer
            {
                Name = $"{_username} (Host)",
                Role = "Host",
                RoleBrush = FindResource("HostBadgeBrush") as Brush ?? Brushes.Green,
                ColorBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF))
            });
        }
        else
        {
            AddPeer(new CollaborationPeer
            {
                Name = "Host User",
                Role = "Host",
                RoleBrush = FindResource("HostBadgeBrush") as Brush ?? Brushes.Green,
                ColorBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))
            });

            AddPeer(new CollaborationPeer
            {
                Name = _username,
                Role = "Editor",
                RoleBrush = FindResource("EditorBadgeBrush") as Brush ?? Brushes.Blue,
                ColorBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF))
            });
        }

        // Update last connected time
        await _sessionService.UpdateLastConnectedAsync(session.SessionCode);

        StatusText.Text = $"Reconnected to: {_sessionName}";
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all session history?",
            "Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await _sessionService.ClearHistoryAsync();
            RecentSessions.Clear();
            UpdateRecentSessionsUI();
            StatusText.Text = "Session history cleared";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isConnected)
        {
            var result = MessageBox.Show(
                "You are still connected to a session. Leave and close?",
                "Close Collaboration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            DisconnectFromSession();
        }

        // Unsubscribe from transport sync events
        TransportSyncService.Instance.RemoteTransportSyncReceived -= OnRemoteTransportSyncReceived;

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Shows the collaboration dialog.
    /// </summary>
    public static void ShowDialog(Window owner)
    {
        var dialog = new CollaborationDialog
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }
}

/// <summary>
/// Represents a peer in a collaboration session.
/// </summary>
public class CollaborationPeer : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _role = string.Empty;
    private Brush? _roleBrush;
    private Brush? _colorBrush;

    /// <summary>
    /// Gets or sets the peer name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    /// <summary>
    /// Gets or sets the peer role (Host, Editor, Viewer).
    /// </summary>
    public string Role
    {
        get => _role;
        set { _role = value; OnPropertyChanged(nameof(Role)); }
    }

    /// <summary>
    /// Gets or sets the role badge brush.
    /// </summary>
    public Brush? RoleBrush
    {
        get => _roleBrush;
        set { _roleBrush = value; OnPropertyChanged(nameof(RoleBrush)); }
    }

    /// <summary>
    /// Gets or sets the peer color brush.
    /// </summary>
    public Brush? ColorBrush
    {
        get => _colorBrush;
        set { _colorBrush = value; OnPropertyChanged(nameof(ColorBrush)); }
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a chat message in a collaboration session.
/// </summary>
public class ChatMessage : INotifyPropertyChanged
{
    private string _senderName = string.Empty;
    private string _message = string.Empty;
    private DateTime _timestamp;
    private Brush? _colorBrush;
    private bool _isSystemMessage;

    /// <summary>
    /// Gets or sets the sender name.
    /// </summary>
    public string SenderName
    {
        get => _senderName;
        set { _senderName = value; OnPropertyChanged(nameof(SenderName)); }
    }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Message
    {
        get => _message;
        set { _message = value; OnPropertyChanged(nameof(Message)); }
    }

    /// <summary>
    /// Gets or sets the message timestamp.
    /// </summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); }
    }

    /// <summary>
    /// Gets or sets the sender color brush.
    /// </summary>
    public Brush? ColorBrush
    {
        get => _colorBrush;
        set { _colorBrush = value; OnPropertyChanged(nameof(ColorBrush)); }
    }

    /// <summary>
    /// Gets or sets whether this is a system message.
    /// </summary>
    public bool IsSystemMessage
    {
        get => _isSystemMessage;
        set { _isSystemMessage = value; OnPropertyChanged(nameof(IsSystemMessage)); }
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
