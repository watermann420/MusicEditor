// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

    /// <summary>
    /// Collection of connected peers.
    /// </summary>
    public ObservableCollection<CollaborationPeer> Peers { get; } = new();

    /// <summary>
    /// Collection of chat messages.
    /// </summary>
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    /// <summary>
    /// Gets whether the current user is the session host.
    /// </summary>
    public bool IsHost => _isHost;

    /// <summary>
    /// Creates a new collaboration dialog.
    /// </summary>
    public CollaborationDialog()
    {
        InitializeComponent();

        PeerList.ItemsSource = Peers;
        ChatMessages.ItemsSource = Messages;
    }

    private async void CreateSession_Click(object sender, RoutedEventArgs e)
    {
        var sessionName = CreateSessionName.Text.Trim();
        if (string.IsNullOrEmpty(sessionName))
        {
            MessageBox.Show("Please enter a session name.", "Create Session",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isHost = true;
        _sessionName = sessionName;
        _sessionCode = GenerateSessionCode();

        await ConnectToSessionAsync();

        // Add self as host
        AddPeer(new CollaborationPeer
        {
            Name = "You (Host)",
            Role = "Host",
            RoleBrush = FindResource("HostBadgeBrush") as Brush ?? Brushes.Green,
            ColorBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF))
        });

        StatusText.Text = $"Session created. Share code: {_sessionCode}";
    }

    private async void JoinSession_Click(object sender, RoutedEventArgs e)
    {
        var sessionCode = JoinSessionCode.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sessionCode))
        {
            MessageBox.Show("Please enter a session code.", "Join Session",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isHost = false;
        _sessionCode = sessionCode;
        _sessionName = "Remote Session";

        await ConnectToSessionAsync();

        // Simulate receiving peer list
        AddPeer(new CollaborationPeer
        {
            Name = "Host User",
            Role = "Host",
            RoleBrush = FindResource("HostBadgeBrush") as Brush ?? Brushes.Green,
            ColorBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73))
        });

        AddPeer(new CollaborationPeer
        {
            Name = "You",
            Role = "Editor",
            RoleBrush = FindResource("EditorBadgeBrush") as Brush ?? Brushes.Blue,
            ColorBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF))
        });

        StatusText.Text = $"Joined session: {_sessionName}";
    }

    private async Task ConnectToSessionAsync()
    {
        // Show connecting state
        ConnectionIndicator.Fill = FindResource("ConnectingBrush") as Brush ?? Brushes.Orange;
        ConnectionStatus.Text = "Connecting...";
        StatusText.Text = "Connecting to session...";

        // Simulate connection delay
        await Task.Delay(1000);

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

        // Add message to chat
        Messages.Add(new ChatMessage
        {
            SenderName = "You",
            Message = message,
            Timestamp = DateTime.Now,
            ColorBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF))
        });

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
