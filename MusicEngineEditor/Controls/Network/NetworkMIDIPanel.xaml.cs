// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Network MIDI Panel control for RTP-MIDI support.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MusicEngineEditor.ViewModels.Network;

namespace MusicEngineEditor.Controls.Network;

/// <summary>
/// Network MIDI Panel for RTP-MIDI / Network MIDI support.
/// Provides session management, discovery, and MIDI routing controls.
/// </summary>
public partial class NetworkMIDIPanel : UserControl
{
    #region Private Fields

    private NetworkMIDIPanelViewModel? _viewModel;

    #endregion

    #region Dependency Properties

    /// <summary>
    /// Dependency property for network MIDI enabled state.
    /// </summary>
    public static readonly DependencyProperty IsNetworkMidiEnabledProperty =
        DependencyProperty.Register(
            nameof(IsNetworkMidiEnabled),
            typeof(bool),
            typeof(NetworkMIDIPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsNetworkMidiEnabledChanged));

    /// <summary>
    /// Dependency property for session name.
    /// </summary>
    public static readonly DependencyProperty SessionNameProperty =
        DependencyProperty.Register(
            nameof(SessionName),
            typeof(string),
            typeof(NetworkMIDIPanel),
            new FrameworkPropertyMetadata("My MIDI Session", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Dependency property for connection state.
    /// </summary>
    public static readonly DependencyProperty IsConnectedProperty =
        DependencyProperty.Register(
            nameof(IsConnected),
            typeof(bool),
            typeof(NetworkMIDIPanel),
            new PropertyMetadata(false));

    /// <summary>
    /// Dependency property for hosting state.
    /// </summary>
    public static readonly DependencyProperty IsHostingProperty =
        DependencyProperty.Register(
            nameof(IsHosting),
            typeof(bool),
            typeof(NetworkMIDIPanel),
            new PropertyMetadata(false));

    /// <summary>
    /// Dependency property for available sessions.
    /// </summary>
    public static readonly DependencyProperty AvailableSessionsProperty =
        DependencyProperty.Register(
            nameof(AvailableSessions),
            typeof(ObservableCollection<NetworkMidiSession>),
            typeof(NetworkMIDIPanel),
            new PropertyMetadata(null));

    /// <summary>
    /// Dependency property for connected peers.
    /// </summary>
    public static readonly DependencyProperty ConnectedPeersProperty =
        DependencyProperty.Register(
            nameof(ConnectedPeers),
            typeof(ObservableCollection<NetworkMidiPeer>),
            typeof(NetworkMIDIPanel),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets whether network MIDI is enabled.
    /// </summary>
    public bool IsNetworkMidiEnabled
    {
        get => (bool)GetValue(IsNetworkMidiEnabledProperty);
        set => SetValue(IsNetworkMidiEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the session name.
    /// </summary>
    public string SessionName
    {
        get => (string)GetValue(SessionNameProperty);
        set => SetValue(SessionNameProperty, value);
    }

    /// <summary>
    /// Gets whether connected to a session.
    /// </summary>
    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        private set => SetValue(IsConnectedProperty, value);
    }

    /// <summary>
    /// Gets whether hosting a session.
    /// </summary>
    public bool IsHosting
    {
        get => (bool)GetValue(IsHostingProperty);
        private set => SetValue(IsHostingProperty, value);
    }

    /// <summary>
    /// Gets the collection of available sessions.
    /// </summary>
    public ObservableCollection<NetworkMidiSession>? AvailableSessions
    {
        get => (ObservableCollection<NetworkMidiSession>?)GetValue(AvailableSessionsProperty);
        private set => SetValue(AvailableSessionsProperty, value);
    }

    /// <summary>
    /// Gets the collection of connected peers.
    /// </summary>
    public ObservableCollection<NetworkMidiPeer>? ConnectedPeers
    {
        get => (ObservableCollection<NetworkMidiPeer>?)GetValue(ConnectedPeersProperty);
        private set => SetValue(ConnectedPeersProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when network MIDI is enabled or disabled.
    /// </summary>
    public event EventHandler<bool>? NetworkMidiEnabledChanged;

    /// <summary>
    /// Raised when a session is created.
    /// </summary>
    public event EventHandler<string>? SessionCreated;

    /// <summary>
    /// Raised when joined a session.
    /// </summary>
    public event EventHandler<NetworkMidiSession>? SessionJoined;

    /// <summary>
    /// Raised when left a session.
    /// </summary>
    public event EventHandler? SessionLeft;

    /// <summary>
    /// Raised when MIDI data is received.
    /// </summary>
    public event EventHandler<byte[]>? MidiDataReceived;

    /// <summary>
    /// Raised when MIDI data is sent.
    /// </summary>
    public event EventHandler<byte[]>? MidiDataSent;

    /// <summary>
    /// Raised when refresh is requested.
    /// </summary>
    public event EventHandler? RefreshRequested;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new NetworkMIDIPanel.
    /// </summary>
    public NetworkMIDIPanel()
    {
        InitializeComponent();

        _viewModel = new NetworkMIDIPanelViewModel();
        DataContext = _viewModel;

        // Bind dependency properties to view model
        AvailableSessions = _viewModel.AvailableSessions;
        ConnectedPeers = _viewModel.ConnectedPeers;

        // Subscribe to view model events
        _viewModel.SessionCreated += OnViewModelSessionCreated;
        _viewModel.SessionJoined += OnViewModelSessionJoined;
        _viewModel.SessionLeft += OnViewModelSessionLeft;
        _viewModel.MidiDataReceived += OnViewModelMidiDataReceived;
        _viewModel.MidiDataSent += OnViewModelMidiDataSent;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // Update quality bars
        UpdateConnectionQualityDisplay(ConnectionQuality.Disconnected);
    }

    #endregion

    #region Lifecycle

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.InitializeAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel?.Shutdown();
    }

    #endregion

    #region Event Handlers

    private static void OnIsNetworkMidiEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NetworkMIDIPanel panel)
        {
            panel._viewModel?.ToggleNetworkMidiCommand.Execute(null);
            panel.NetworkMidiEnabledChanged?.Invoke(panel, (bool)e.NewValue);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NetworkMIDIPanelViewModel.IsConnected):
                IsConnected = _viewModel?.IsConnected ?? false;
                break;
            case nameof(NetworkMIDIPanelViewModel.IsHosting):
                IsHosting = _viewModel?.IsHosting ?? false;
                break;
            case nameof(NetworkMIDIPanelViewModel.IsNetworkMidiEnabled):
                IsNetworkMidiEnabled = _viewModel?.IsNetworkMidiEnabled ?? false;
                break;
            case nameof(NetworkMIDIPanelViewModel.ConnectionQualityLevel):
                if (_viewModel != null)
                {
                    UpdateConnectionQualityDisplay(_viewModel.ConnectionQualityLevel);
                }
                break;
        }
    }

    private void OnViewModelSessionCreated(object? sender, string sessionName)
    {
        SessionCreated?.Invoke(this, sessionName);
    }

    private void OnViewModelSessionJoined(object? sender, NetworkMidiSession session)
    {
        SessionJoined?.Invoke(this, session);
    }

    private void OnViewModelSessionLeft(object? sender, EventArgs e)
    {
        SessionLeft?.Invoke(this, EventArgs.Empty);
    }

    private void OnViewModelMidiDataReceived(object? sender, byte[] data)
    {
        MidiDataReceived?.Invoke(this, data);
    }

    private void OnViewModelMidiDataSent(object? sender, byte[] data)
    {
        MidiDataSent?.Invoke(this, data);
    }

    private void EnableToggle_Click(object sender, RoutedEventArgs e)
    {
        NetworkMidiEnabledChanged?.Invoke(this, EnableToggle.IsChecked == true);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AllChannelsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        // Handled by binding
    }

    #endregion

    #region Private Methods

    private void UpdateConnectionQualityDisplay(ConnectionQuality quality)
    {
        var activeColor = quality switch
        {
            ConnectionQuality.Excellent => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),
            ConnectionQuality.Good => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
            ConnectionQuality.Fair => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)),
            ConnectionQuality.Poor => new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)),
            _ => new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x44))
        };

        var inactiveColor = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x44));

        int activeBars = quality switch
        {
            ConnectionQuality.Excellent => 4,
            ConnectionQuality.Good => 3,
            ConnectionQuality.Fair => 2,
            ConnectionQuality.Poor => 1,
            _ => 0
        };

        QualityBar1.Background = activeBars >= 1 ? activeColor : inactiveColor;
        QualityBar2.Background = activeBars >= 2 ? activeColor : inactiveColor;
        QualityBar3.Background = activeBars >= 3 ? activeColor : inactiveColor;
        QualityBar4.Background = activeBars >= 4 ? activeColor : inactiveColor;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the view model.
    /// </summary>
    public NetworkMIDIPanelViewModel? ViewModel => _viewModel;

    /// <summary>
    /// Creates a new session with the specified name.
    /// </summary>
    /// <param name="name">Session name.</param>
    public void CreateSession(string name)
    {
        if (_viewModel == null) return;

        _viewModel.SessionName = name;
        _viewModel.CreateSessionCommand.Execute(null);
    }

    /// <summary>
    /// Joins the specified session.
    /// </summary>
    /// <param name="session">Session to join.</param>
    public void JoinSession(NetworkMidiSession session)
    {
        if (_viewModel == null) return;

        _viewModel.SelectedAvailableSession = session;
        _viewModel.JoinSessionCommand.Execute(null);
    }

    /// <summary>
    /// Leaves the current session.
    /// </summary>
    public void LeaveSession()
    {
        _viewModel?.LeaveSessionCommand.Execute(null);
    }

    /// <summary>
    /// Refreshes the list of available sessions.
    /// </summary>
    public void RefreshSessions()
    {
        _viewModel?.RefreshSessionsCommand.Execute(null);
    }

    /// <summary>
    /// Records incoming MIDI activity.
    /// </summary>
    public void RecordMidiIn()
    {
        _viewModel?.RecordMidiIn();
    }

    /// <summary>
    /// Records outgoing MIDI activity.
    /// </summary>
    public void RecordMidiOut()
    {
        _viewModel?.RecordMidiOut();
    }

    /// <summary>
    /// Checks if a specific MIDI channel is enabled for filtering.
    /// </summary>
    /// <param name="channel">Channel number (1-16).</param>
    /// <returns>True if the channel is enabled.</returns>
    public bool IsChannelEnabled(int channel)
    {
        return _viewModel?.IsChannelEnabled(channel) ?? true;
    }

    #endregion
}

/// <summary>
/// Converts a boolean value to its inverse.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}
