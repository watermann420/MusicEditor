using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Network MIDI dialog for RTP-MIDI session management.
/// </summary>
public partial class NetworkMidiDialog : Window
{
    private bool _isConnected;
    private bool _isHost;
    private string _currentSessionName = string.Empty;

    /// <summary>
    /// Collection of discovered MIDI sessions.
    /// </summary>
    public ObservableCollection<MidiSession> Sessions { get; } = new();

    /// <summary>
    /// Collection of connected peers.
    /// </summary>
    public ObservableCollection<MidiPeer> Peers { get; } = new();

    /// <summary>
    /// Gets whether the current user is the session host.
    /// </summary>
    public bool IsHost => _isHost;

    /// <summary>
    /// Creates a new network MIDI dialog.
    /// </summary>
    public NetworkMidiDialog()
    {
        InitializeComponent();

        SessionList.ItemsSource = Sessions;
        PeerList.ItemsSource = Peers;

        // Initialize channel checkboxes
        InitializeChannelCheckboxes();

        Loaded += OnLoaded;
    }

    private void InitializeChannelCheckboxes()
    {
        for (int i = 1; i <= 16; i++)
        {
            var checkBox = new System.Windows.Controls.CheckBox
            {
                Content = $"Channel {i}",
                IsChecked = true,
                IsEnabled = false,
                Margin = new Thickness(0, 2, 0, 2)
            };
            ChannelCheckboxes.Children.Add(checkBox);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ScanForSessionsAsync();
    }

    private async void RefreshSessions_Click(object sender, RoutedEventArgs e)
    {
        await ScanForSessionsAsync();
    }

    private async Task ScanForSessionsAsync()
    {
        ScanningPanel.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        StatusText.Text = "Scanning for MIDI sessions...";

        Sessions.Clear();

        // Simulate network scan
        await Task.Delay(1500);

        // Add sample sessions
        Sessions.Add(new MidiSession
        {
            Name = "Studio A",
            Host = "192.168.1.100",
            Latency = 12,
            LatencyBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73)),
            PeerCount = "3"
        });

        Sessions.Add(new MidiSession
        {
            Name = "Remote Session",
            Host = "192.168.1.105",
            Latency = 45,
            LatencyBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xB3, 0x39)),
            PeerCount = "1"
        });

        ScanningPanel.Visibility = Visibility.Collapsed;
        UpdateEmptyState();
        StatusText.Text = $"Found {Sessions.Count} sessions";
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        JoinButton.IsEnabled = SessionList.SelectedItem != null;
    }

    private async void CreateSession_Click(object sender, RoutedEventArgs e)
    {
        var sessionName = SessionNameInput.Text.Trim();
        if (string.IsNullOrEmpty(sessionName))
        {
            MessageBox.Show("Please enter a session name.", "Create Session",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isHost = true;
        _currentSessionName = sessionName;

        await ConnectToSessionAsync(sessionName, true);
    }

    private async void JoinSession_Click(object sender, RoutedEventArgs e)
    {
        if (SessionList.SelectedItem is not MidiSession session) return;

        _isHost = false;
        _currentSessionName = session.Name;

        await ConnectToSessionAsync(session.Name, false);
    }

    private async Task ConnectToSessionAsync(string sessionName, bool isHost)
    {
        // Show connecting state
        ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xE8, 0xB3, 0x39));
        ConnectionStatus.Text = "Connecting...";
        ConnectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x2D));
        StatusText.Text = $"Connecting to {sessionName}...";

        // Simulate connection
        await Task.Delay(1000);

        _isConnected = true;

        // Update UI
        ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73));
        ConnectionStatus.Text = "Connected";
        ConnectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x3D, 0x2D));

        SessionNameDisplay.Text = sessionName;
        SessionHostDisplay.Text = isHost ? "Hosted by: You" : "Joined as: Guest";

        // Add peers
        Peers.Clear();
        if (!isHost)
        {
            Peers.Add(new MidiPeer
            {
                Name = "Host",
                IpAddress = "192.168.1.100",
                Latency = 12,
                LatencyDisplay = "12 ms",
                LatencyBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73)),
                StatusBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73))
            });
        }

        Peers.Add(new MidiPeer
        {
            Name = isHost ? "You (Host)" : "You",
            IpAddress = "192.168.1.50",
            Latency = 0,
            LatencyDisplay = "Local",
            LatencyBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A)),
            StatusBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73))
        });

        // Switch to connected view
        NotConnectedView.Visibility = Visibility.Collapsed;
        ConnectedView.Visibility = Visibility.Visible;

        StatusText.Text = $"Connected to {sessionName}";
    }

    private void LeaveSession_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            _isHost
                ? "This will end the session for all participants. Continue?"
                : "Leave this MIDI session?",
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
        _currentSessionName = string.Empty;

        Peers.Clear();

        ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xF7, 0x54, 0x64));
        ConnectionStatus.Text = "Not Connected";
        ConnectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x2D, 0x2D));

        ConnectedView.Visibility = Visibility.Collapsed;
        NotConnectedView.Visibility = Visibility.Visible;

        StatusText.Text = "Disconnected";
    }

    private void AllChannelsCheck_Click(object sender, RoutedEventArgs e)
    {
        bool allChecked = AllChannelsCheck.IsChecked == true;
        foreach (System.Windows.Controls.CheckBox cb in ChannelCheckboxes.Children)
        {
            cb.IsEnabled = !allChecked;
            if (allChecked)
            {
                cb.IsChecked = true;
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isConnected)
        {
            var result = MessageBox.Show(
                "You are still connected to a MIDI session. Leave and close?",
                "Close Network MIDI",
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
    /// Shows the network MIDI dialog.
    /// </summary>
    public static void ShowDialog(Window owner)
    {
        var dialog = new NetworkMidiDialog
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }
}

/// <summary>
/// Represents a discovered MIDI session.
/// </summary>
public class MidiSession : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _host = string.Empty;
    private int _latency;
    private Brush? _latencyBrush;
    private string _peerCount = "0";

    /// <summary>
    /// Gets or sets the session name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    /// <summary>
    /// Gets or sets the host address.
    /// </summary>
    public string Host
    {
        get => _host;
        set { _host = value; OnPropertyChanged(nameof(Host)); }
    }

    /// <summary>
    /// Gets or sets the latency in milliseconds.
    /// </summary>
    public int Latency
    {
        get => _latency;
        set { _latency = value; OnPropertyChanged(nameof(Latency)); }
    }

    /// <summary>
    /// Gets or sets the latency color brush.
    /// </summary>
    public Brush? LatencyBrush
    {
        get => _latencyBrush;
        set { _latencyBrush = value; OnPropertyChanged(nameof(LatencyBrush)); }
    }

    /// <summary>
    /// Gets or sets the peer count display.
    /// </summary>
    public string PeerCount
    {
        get => _peerCount;
        set { _peerCount = value; OnPropertyChanged(nameof(PeerCount)); }
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
/// Represents a peer in a MIDI session.
/// </summary>
public class MidiPeer : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _ipAddress = string.Empty;
    private int _latency;
    private string _latencyDisplay = string.Empty;
    private Brush? _latencyBrush;
    private Brush? _statusBrush;

    /// <summary>
    /// Gets or sets the peer name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    /// <summary>
    /// Gets or sets the IP address.
    /// </summary>
    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(nameof(IpAddress)); }
    }

    /// <summary>
    /// Gets or sets the latency in milliseconds.
    /// </summary>
    public int Latency
    {
        get => _latency;
        set { _latency = value; OnPropertyChanged(nameof(Latency)); }
    }

    /// <summary>
    /// Gets or sets the latency display string.
    /// </summary>
    public string LatencyDisplay
    {
        get => _latencyDisplay;
        set { _latencyDisplay = value; OnPropertyChanged(nameof(LatencyDisplay)); }
    }

    /// <summary>
    /// Gets or sets the latency color brush.
    /// </summary>
    public Brush? LatencyBrush
    {
        get => _latencyBrush;
        set { _latencyBrush = value; OnPropertyChanged(nameof(LatencyBrush)); }
    }

    /// <summary>
    /// Gets or sets the status color brush.
    /// </summary>
    public Brush? StatusBrush
    {
        get => _statusBrush;
        set { _statusBrush = value; OnPropertyChanged(nameof(StatusBrush)); }
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
