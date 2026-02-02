// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Main container panel for all network/sync features.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicEngineEditor.Services;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.Controls.Network;

/// <summary>
/// Main container panel for all network/sync features including Ableton Link,
/// OSC Control, Network MIDI, and Machine Control (MMC/MTC).
/// </summary>
public partial class NetworkSyncPanel : UserControl, INotifyPropertyChanged
{
    #region Private Fields

    private readonly NetworkSyncService _networkSyncService;
    private readonly OSCControlSurfaceService _oscService;
    private readonly DispatcherTimer _statusUpdateTimer;
    private readonly ObservableCollection<NetworkInterfaceInfo> _networkInterfaces = [];
    private readonly ObservableCollection<NetworkMidiSessionInfo> _midiSessions = [];
    private bool _isInitialized;

    // Status indicator brushes
    private static readonly SolidColorBrush DisconnectedBrush = new(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly SolidColorBrush ConnectedBrush = new(Color.FromRgb(0x00, 0xCC, 0x66));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xFF, 0xB8, 0x00));
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(0xFF, 0x47, 0x57));
    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(0x00, 0xD9, 0xFF));

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty IsMasterEnabledProperty =
        DependencyProperty.Register(nameof(IsMasterEnabled), typeof(bool), typeof(NetworkSyncPanel),
            new PropertyMetadata(false, OnMasterEnabledChanged));

    /// <summary>
    /// Gets or sets whether all network features are enabled globally.
    /// </summary>
    public bool IsMasterEnabled
    {
        get => (bool)GetValue(IsMasterEnabledProperty);
        set => SetValue(IsMasterEnabledProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when the network sync state changes.
    /// </summary>
    public event EventHandler<NetworkSyncStateChangedEventArgs>? StateChanged;

    #endregion

    #region Constructor

    public NetworkSyncPanel()
    {
        InitializeComponent();
        DataContext = this;

        _networkSyncService = NetworkSyncService.Instance;
        _oscService = OSCControlSurfaceService.Instance;

        // Initialize status update timer
        _statusUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;

        MidiSessionsList.ItemsSource = _midiSessions;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;

        _isInitialized = true;

        // Subscribe to service events
        SubscribeToServiceEvents();

        // Load network interfaces
        LoadNetworkInterfaces();

        // Initialize UI state from service
        InitializeUiState();

        // Start status updates
        _statusUpdateTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _statusUpdateTimer.Stop();
        UnsubscribeFromServiceEvents();
    }

    #endregion

    #region Initialization

    private void LoadNetworkInterfaces()
    {
        _networkInterfaces.Clear();
        NetworkInterfaceComboBox.Items.Clear();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                              ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                .ToList();

            // Add "All Interfaces" option
            NetworkInterfaceComboBox.Items.Add(new ComboBoxItem { Content = "All Interfaces", Tag = "all" });

            foreach (var ni in interfaces)
            {
                var ipProps = ni.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                if (ipv4 != null)
                {
                    var info = new NetworkInterfaceInfo
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        IpAddress = ipv4.Address.ToString(),
                        Type = ni.NetworkInterfaceType
                    };
                    _networkInterfaces.Add(info);

                    var item = new ComboBoxItem
                    {
                        Content = $"{ni.Name} ({ipv4.Address})",
                        Tag = info
                    };
                    NetworkInterfaceComboBox.Items.Add(item);
                }
            }

            if (NetworkInterfaceComboBox.Items.Count > 0)
            {
                NetworkInterfaceComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load network interfaces: {ex.Message}");
            var item = new ComboBoxItem { Content = "Default", Tag = "default" };
            NetworkInterfaceComboBox.Items.Add(item);
            NetworkInterfaceComboBox.SelectedIndex = 0;
        }
    }

    private void InitializeUiState()
    {
        // Sync UI state with service state
        MasterEnableToggle.IsChecked = _networkSyncService.IsAnyServiceEnabled;
        IsMasterEnabled = _networkSyncService.IsAnyServiceEnabled;

        LinkEnableToggle.IsChecked = _networkSyncService.IsLinkEnabled;
        LinkEnableToggle.Content = _networkSyncService.IsLinkEnabled ? "ON" : "OFF";
        LinkStartStopSyncCheck.IsChecked = _networkSyncService.LinkStartStopSyncEnabled;

        OscEnableToggle.IsChecked = _oscService.IsRunning;
        OscEnableToggle.Content = _oscService.IsRunning ? "ON" : "OFF";
        OscListenPortInput.Text = _oscService.ListenPort.ToString();
        OscFeedbackPortInput.Text = _oscService.FeedbackPort.ToString();
        OscFeedbackHostInput.Text = _oscService.FeedbackHost;

        NetworkMidiEnableToggle.IsChecked = _networkSyncService.IsNetworkMidiEnabled;
        NetworkMidiEnableToggle.Content = _networkSyncService.IsNetworkMidiEnabled ? "ON" : "OFF";

        MmcEnableToggle.IsChecked = _networkSyncService.IsMmcEnabled;
        MmcEnableToggle.Content = _networkSyncService.IsMmcEnabled ? "ON" : "OFF";

        MtcEnableToggle.IsChecked = _networkSyncService.IsMtcEnabled;
        MtcEnableToggle.Content = _networkSyncService.IsMtcEnabled ? "ON" : "OFF";

        UpdateGlobalStatus();
        UpdateStatusBar("Ready");
    }

    private void SubscribeToServiceEvents()
    {
        _networkSyncService.LinkStateChanged += OnLinkStateChanged;
        _networkSyncService.LinkPeersChanged += OnLinkPeersChanged;
        _networkSyncService.TempoChanged += OnTempoChanged;
        _networkSyncService.BeatChanged += OnBeatChanged;
        _networkSyncService.TimecodeChanged += OnTimecodeChanged;
        _networkSyncService.NetworkMidiSessionDiscovered += OnMidiSessionDiscovered;
        _networkSyncService.ConnectionStateChanged += OnConnectionStateChanged;

        _oscService.ConnectionStateChanged += OnOscConnectionStateChanged;
    }

    private void UnsubscribeFromServiceEvents()
    {
        _networkSyncService.LinkStateChanged -= OnLinkStateChanged;
        _networkSyncService.LinkPeersChanged -= OnLinkPeersChanged;
        _networkSyncService.TempoChanged -= OnTempoChanged;
        _networkSyncService.BeatChanged -= OnBeatChanged;
        _networkSyncService.TimecodeChanged -= OnTimecodeChanged;
        _networkSyncService.NetworkMidiSessionDiscovered -= OnMidiSessionDiscovered;
        _networkSyncService.ConnectionStateChanged -= OnConnectionStateChanged;

        _oscService.ConnectionStateChanged -= OnOscConnectionStateChanged;
    }

    #endregion

    #region Status Updates

    private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Update Link status display
        if (_networkSyncService.IsLinkEnabled)
        {
            LinkTempoLabel.Text = _networkSyncService.LinkTempo.ToString("F2");
            LinkPeersLabel.Text = _networkSyncService.LinkPeerCount.ToString();
            LinkPeerCountLabel.Text = $"({_networkSyncService.LinkPeerCount} peers)";

            var beat = _networkSyncService.LinkBeat;
            var bar = (int)(beat / 4) + 1;
            var beatInBar = (int)(beat % 4) + 1;
            LinkBeatLabel.Text = $"{bar}.{beatInBar}";
        }

        // Update timecode display
        if (_networkSyncService.IsMtcEnabled)
        {
            TimecodeDisplay.Text = _networkSyncService.CurrentTimecode;
        }

        // Update MIDI session count
        MidiSessionCountLabel.Text = $"({_midiSessions.Count} sessions)";
    }

    private void UpdateGlobalStatus()
    {
        int activeCount = 0;
        if (_networkSyncService.IsLinkEnabled && _networkSyncService.LinkPeerCount > 0) activeCount++;
        if (_oscService.IsRunning) activeCount++;
        if (_networkSyncService.IsNetworkMidiEnabled) activeCount++;
        if (_networkSyncService.IsMmcEnabled || _networkSyncService.IsMtcEnabled) activeCount++;

        if (activeCount == 0)
        {
            GlobalStatusIndicator.Fill = DisconnectedBrush;
            GlobalStatusLabel.Text = "Offline";
            GlobalStatusText.Text = "All features disabled";
        }
        else
        {
            GlobalStatusIndicator.Fill = ConnectedBrush;
            GlobalStatusLabel.Text = "Active";
            GlobalStatusText.Text = $"{activeCount} feature(s) active";
        }
    }

    private void UpdateStatusBar(string message)
    {
        StatusBarText.Text = message;
    }

    #endregion

    #region Service Event Handlers

    private void OnLinkStateChanged(object? sender, bool isEnabled)
    {
        Dispatcher.Invoke(() =>
        {
            LinkEnableToggle.IsChecked = isEnabled;
            LinkEnableToggle.Content = isEnabled ? "ON" : "OFF";
            UpdateGlobalStatus();

            if (isEnabled)
            {
                UpdateStatusBar("Ableton Link enabled");
            }
            else
            {
                UpdateStatusBar("Ableton Link disabled");
            }
        });
    }

    private void OnLinkPeersChanged(object? sender, int peerCount)
    {
        Dispatcher.Invoke(() =>
        {
            LinkPeersLabel.Text = peerCount.ToString();
            LinkPeerCountLabel.Text = $"({peerCount} peers)";
            UpdateGlobalStatus();
        });
    }

    private void OnTempoChanged(object? sender, double tempo)
    {
        Dispatcher.Invoke(() =>
        {
            LinkTempoLabel.Text = tempo.ToString("F2");
        });
    }

    private void OnBeatChanged(object? sender, double beat)
    {
        Dispatcher.Invoke(() =>
        {
            var bar = (int)(beat / 4) + 1;
            var beatInBar = (int)(beat % 4) + 1;
            LinkBeatLabel.Text = $"{bar}.{beatInBar}";
        });
    }

    private void OnTimecodeChanged(object? sender, string timecode)
    {
        Dispatcher.Invoke(() =>
        {
            TimecodeDisplay.Text = timecode;
        });
    }

    private void OnMidiSessionDiscovered(object? sender, NetworkMidiSessionInfo session)
    {
        Dispatcher.Invoke(() =>
        {
            var existing = _midiSessions.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                var index = _midiSessions.IndexOf(existing);
                _midiSessions[index] = session;
            }
            else
            {
                _midiSessions.Add(session);
            }

            MidiSessionCountLabel.Text = $"({_midiSessions.Count} sessions)";
        });
    }

    private void OnConnectionStateChanged(object? sender, NetworkServiceType serviceType)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateGlobalStatus();
        });
    }

    private void OnOscConnectionStateChanged(object? sender, bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            OscEnableToggle.IsChecked = isConnected;
            OscEnableToggle.Content = isConnected ? "ON" : "OFF";
            UpdateGlobalStatus();

            if (isConnected)
            {
                UpdateStatusBar($"OSC server listening on port {_oscService.ListenPort}");
            }
            else
            {
                UpdateStatusBar("OSC server stopped");
            }
        });
    }

    #endregion

    #region UI Event Handlers - Header

    private static void OnMasterEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NetworkSyncPanel panel && panel._isInitialized)
        {
            panel.ApplyMasterEnabled((bool)e.NewValue);
        }
    }

    private void MasterEnableToggle_Click(object sender, RoutedEventArgs e)
    {
        IsMasterEnabled = MasterEnableToggle.IsChecked == true;
    }

    private void ApplyMasterEnabled(bool enabled)
    {
        if (enabled)
        {
            // Enable all previously enabled services or default set
            _networkSyncService.EnableAllServices();
            UpdateStatusBar("All network features enabled");
        }
        else
        {
            // Disable all services
            _networkSyncService.DisableAllServices();
            _oscService.Stop();
            UpdateStatusBar("All network features disabled");
        }

        MasterEnableToggle.Content = enabled ? "Disable All" : "Enable All";
        UpdateGlobalStatus();
        InitializeUiState();

        StateChanged?.Invoke(this, new NetworkSyncStateChangedEventArgs(
            NetworkServiceType.All, enabled));
    }

    private void NetworkInterfaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NetworkInterfaceComboBox.SelectedItem is ComboBoxItem item && item.Tag is NetworkInterfaceInfo info)
        {
            _networkSyncService.SelectedNetworkInterface = info.IpAddress;
            UpdateStatusBar($"Network interface changed to {info.Name}");
        }
        else if (NetworkInterfaceComboBox.SelectedIndex == 0)
        {
            _networkSyncService.SelectedNetworkInterface = null;
            UpdateStatusBar("Using all network interfaces");
        }
    }

    #endregion

    #region UI Event Handlers - Accordion

    private void SectionToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender == LinkSectionToggle)
        {
            LinkSectionContent.Visibility = LinkSectionToggle.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (sender == OscSectionToggle)
        {
            OscSectionContent.Visibility = OscSectionToggle.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (sender == MidiSectionToggle)
        {
            MidiSectionContent.Visibility = MidiSectionToggle.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

            // Refresh MIDI sessions when section is opened
            if (MidiSectionToggle.IsChecked == true)
            {
                _networkSyncService.RefreshNetworkMidiSessions();
            }
        }
        else if (sender == MmcSectionToggle)
        {
            MmcSectionContent.Visibility = MmcSectionToggle.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    #endregion

    #region UI Event Handlers - Ableton Link

    private void LinkEnableToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = LinkEnableToggle.IsChecked == true;
        LinkEnableToggle.Content = enabled ? "ON" : "OFF";

        if (enabled)
        {
            _networkSyncService.EnableLink();
        }
        else
        {
            _networkSyncService.DisableLink();
        }

        StateChanged?.Invoke(this, new NetworkSyncStateChangedEventArgs(
            NetworkServiceType.AbletonLink, enabled));
    }

    private void LinkStartStopSyncCheck_Click(object sender, RoutedEventArgs e)
    {
        _networkSyncService.LinkStartStopSyncEnabled = LinkStartStopSyncCheck.IsChecked == true;
    }

    #endregion

    #region UI Event Handlers - OSC

    private void OscEnableToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = OscEnableToggle.IsChecked == true;
        OscEnableToggle.Content = enabled ? "ON" : "OFF";

        if (enabled)
        {
            // Update ports from UI
            if (int.TryParse(OscListenPortInput.Text, out int listenPort))
            {
                _oscService.ListenPort = listenPort;
            }
            if (int.TryParse(OscFeedbackPortInput.Text, out int feedbackPort))
            {
                _oscService.FeedbackPort = feedbackPort;
            }
            _oscService.FeedbackHost = OscFeedbackHostInput.Text;

            try
            {
                _oscService.Start();
                UpdateStatusBar($"OSC server started on port {_oscService.ListenPort}");
            }
            catch (Exception ex)
            {
                OscEnableToggle.IsChecked = false;
                OscEnableToggle.Content = "OFF";
                UpdateStatusBar($"Failed to start OSC: {ex.Message}");
                MessageBox.Show($"Failed to start OSC server: {ex.Message}",
                    "OSC Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            _oscService.Stop();
        }

        UpdateGlobalStatus();
        StateChanged?.Invoke(this, new NetworkSyncStateChangedEventArgs(
            NetworkServiceType.OSC, enabled));
    }

    private void OscConfigureMappings_Click(object sender, RoutedEventArgs e)
    {
        // Open OSC mapping configuration dialog
        UpdateStatusBar("Opening OSC mapping configuration...");
        // TODO: Open OSC configuration dialog when implemented
    }

    #endregion

    #region UI Event Handlers - Network MIDI

    private void NetworkMidiEnableToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = NetworkMidiEnableToggle.IsChecked == true;
        NetworkMidiEnableToggle.Content = enabled ? "ON" : "OFF";

        if (enabled)
        {
            _networkSyncService.EnableNetworkMidi(MidiSessionNameInput.Text);
        }
        else
        {
            _networkSyncService.DisableNetworkMidi();
        }

        UpdateGlobalStatus();
        StateChanged?.Invoke(this, new NetworkSyncStateChangedEventArgs(
            NetworkServiceType.NetworkMIDI, enabled));
    }

    private void MidiConnect_Click(object sender, RoutedEventArgs e)
    {
        if (MidiSessionsList.SelectedItem is NetworkMidiSessionInfo session)
        {
            _networkSyncService.ConnectToMidiSession(session);
            UpdateStatusBar($"Connecting to {session.Name}...");
        }
        else
        {
            MessageBox.Show("Please select a session to connect to.",
                "No Session Selected", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void MidiOpenDialog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NetworkMidiDialog
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    #endregion

    #region UI Event Handlers - MMC/MTC

    private void MmcEnableToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = MmcEnableToggle.IsChecked == true;
        MmcEnableToggle.Content = enabled ? "ON" : "OFF";

        _networkSyncService.IsMmcEnabled = enabled;
        UpdateGlobalStatus();

        StateChanged?.Invoke(this, new NetworkSyncStateChangedEventArgs(
            NetworkServiceType.MMC, enabled));
    }

    private void MtcEnableToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = MtcEnableToggle.IsChecked == true;
        MtcEnableToggle.Content = enabled ? "ON" : "OFF";

        _networkSyncService.IsMtcEnabled = enabled;
        UpdateGlobalStatus();

        StateChanged?.Invoke(this, new NetworkSyncStateChangedEventArgs(
            NetworkServiceType.MTC, enabled));
    }

    private void MtcFrameRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MtcFrameRateComboBox.SelectedItem is ComboBoxItem item)
        {
            var frameRate = item.Content?.ToString() switch
            {
                "24 fps" => MtcFrameRate.Fps24,
                "25 fps" => MtcFrameRate.Fps25,
                "29.97 fps (Drop Frame)" => MtcFrameRate.Fps2997DropFrame,
                "30 fps" => MtcFrameRate.Fps30,
                _ => MtcFrameRate.Fps2997DropFrame
            };

            _networkSyncService.MtcFrameRate = frameRate;
        }
    }

    #endregion

    #region UI Event Handlers - Status Bar

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadNetworkInterfaces();
        _networkSyncService.RefreshNetworkMidiSessions();
        _midiSessions.Clear();
        UpdateStatusBar("Refreshing network services...");
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // Open network settings dialog
        UpdateStatusBar("Opening network settings...");
        // TODO: Implement settings dialog
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes all network services and UI state.
    /// </summary>
    public void RefreshAll()
    {
        LoadNetworkInterfaces();
        _networkSyncService.RefreshNetworkMidiSessions();
        InitializeUiState();
        UpdateStatusBar("Network services refreshed");
    }

    /// <summary>
    /// Gets the current tempo from Link or local.
    /// </summary>
    public double GetCurrentTempo()
    {
        return _networkSyncService.IsLinkEnabled
            ? _networkSyncService.LinkTempo
            : 120.0; // Default tempo
    }

    /// <summary>
    /// Sets the tempo (will be synced via Link if enabled).
    /// </summary>
    public void SetTempo(double tempo)
    {
        _networkSyncService.SetLinkTempo(tempo);
    }

    #endregion

    #region INotifyPropertyChanged

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Information about a network interface.
/// </summary>
public class NetworkInterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public NetworkInterfaceType Type { get; set; }
}

/// <summary>
/// Information about a discovered Network MIDI session.
/// </summary>
public partial class NetworkMidiSessionInfo : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _latencyMs;

    public SolidColorBrush StatusBrush =>
        IsConnected ? new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x66))
                    : new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
}

/// <summary>
/// Event arguments for network sync state changes.
/// </summary>
public class NetworkSyncStateChangedEventArgs : EventArgs
{
    public NetworkServiceType ServiceType { get; }
    public bool IsEnabled { get; }

    public NetworkSyncStateChangedEventArgs(NetworkServiceType serviceType, bool isEnabled)
    {
        ServiceType = serviceType;
        IsEnabled = isEnabled;
    }
}

/// <summary>
/// Types of network services.
/// </summary>
public enum NetworkServiceType
{
    All,
    AbletonLink,
    OSC,
    NetworkMIDI,
    MMC,
    MTC
}

/// <summary>
/// MTC frame rate options.
/// </summary>
public enum MtcFrameRate
{
    Fps24,
    Fps25,
    Fps2997DropFrame,
    Fps30
}

#endregion
