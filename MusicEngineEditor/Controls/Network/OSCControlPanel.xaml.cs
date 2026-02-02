// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: OSC Control Panel for managing Open Sound Control connections and mappings.

using System;
using System.Windows;
using System.Windows.Controls;
using MusicEngineEditor.ViewModels.Network;

namespace MusicEngineEditor.Controls.Network;

/// <summary>
/// OSC Control Panel for managing Open Sound Control connections,
/// message monitoring, and parameter mappings.
/// </summary>
public partial class OSCControlPanel : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Identifies the IsServerEnabled dependency property.
    /// </summary>
    public static readonly DependencyProperty IsServerEnabledProperty =
        DependencyProperty.Register(
            nameof(IsServerEnabled),
            typeof(bool),
            typeof(OSCControlPanel),
            new PropertyMetadata(false, OnIsServerEnabledChanged));

    /// <summary>
    /// Identifies the IncomingPort dependency property.
    /// </summary>
    public static readonly DependencyProperty IncomingPortProperty =
        DependencyProperty.Register(
            nameof(IncomingPort),
            typeof(int),
            typeof(OSCControlPanel),
            new PropertyMetadata(8000, OnIncomingPortChanged));

    /// <summary>
    /// Identifies the OutgoingPort dependency property.
    /// </summary>
    public static readonly DependencyProperty OutgoingPortProperty =
        DependencyProperty.Register(
            nameof(OutgoingPort),
            typeof(int),
            typeof(OSCControlPanel),
            new PropertyMetadata(9000, OnOutgoingPortChanged));

    /// <summary>
    /// Identifies the TargetIPAddress dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetIPAddressProperty =
        DependencyProperty.Register(
            nameof(TargetIPAddress),
            typeof(string),
            typeof(OSCControlPanel),
            new PropertyMetadata("127.0.0.1", OnTargetIPAddressChanged));

    /// <summary>
    /// Gets or sets whether the OSC server is enabled.
    /// </summary>
    public bool IsServerEnabled
    {
        get => (bool)GetValue(IsServerEnabledProperty);
        set => SetValue(IsServerEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the incoming port number.
    /// </summary>
    public int IncomingPort
    {
        get => (int)GetValue(IncomingPortProperty);
        set => SetValue(IncomingPortProperty, value);
    }

    /// <summary>
    /// Gets or sets the outgoing port number.
    /// </summary>
    public int OutgoingPort
    {
        get => (int)GetValue(OutgoingPortProperty);
        set => SetValue(OutgoingPortProperty, value);
    }

    /// <summary>
    /// Gets or sets the target IP address.
    /// </summary>
    public string TargetIPAddress
    {
        get => (string)GetValue(TargetIPAddressProperty);
        set => SetValue(TargetIPAddressProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when an OSC message is received.
    /// </summary>
    public event EventHandler<OSCMessageDisplay>? MessageReceived;

    /// <summary>
    /// Occurs when a mapped parameter value changes.
    /// </summary>
    public event EventHandler<OSCParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Occurs when the server state changes.
    /// </summary>
    public event EventHandler<bool>? ServerStateChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the view model for this control.
    /// </summary>
    public OSCControlPanelViewModel ViewModel { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new OSCControlPanel.
    /// </summary>
    public OSCControlPanel()
    {
        InitializeComponent();

        ViewModel = new OSCControlPanelViewModel();
        DataContext = ViewModel;

        // Subscribe to ViewModel events
        ViewModel.MessageReceived += OnViewModelMessageReceived;
        ViewModel.ParameterChanged += OnViewModelParameterChanged;
        ViewModel.ServerStateChanged += OnViewModelServerStateChanged;
        ViewModel.ScrollToBottomRequested += OnScrollToBottomRequested;

        // Initialize
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Processes an incoming OSC message.
    /// </summary>
    /// <param name="address">The OSC address.</param>
    /// <param name="args">The message arguments.</param>
    public void ProcessIncomingMessage(string address, params object[] args)
    {
        ViewModel.ProcessIncomingMessage(address, args);
    }

    /// <summary>
    /// Sends an OSC message.
    /// </summary>
    /// <param name="address">The OSC address.</param>
    /// <param name="args">The message arguments.</param>
    public void SendMessage(string address, params object[] args)
    {
        ViewModel.SendMessage(address, args);
    }

    /// <summary>
    /// Adds a new address mapping.
    /// </summary>
    /// <param name="addressPattern">The OSC address pattern.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="oscMin">Minimum OSC value.</param>
    /// <param name="oscMax">Maximum OSC value.</param>
    /// <param name="paramMin">Minimum parameter value.</param>
    /// <param name="paramMax">Maximum parameter value.</param>
    /// <returns>The created mapping.</returns>
    public OSCAddressMapping AddMapping(
        string addressPattern,
        string parameterName,
        float oscMin = 0,
        float oscMax = 1,
        float paramMin = 0,
        float paramMax = 1)
    {
        var mapping = new OSCAddressMapping
        {
            AddressPattern = addressPattern,
            ParameterName = parameterName,
            OscMinValue = oscMin,
            OscMaxValue = oscMax,
            ParameterMinValue = paramMin,
            ParameterMaxValue = paramMax
        };

        ViewModel.Mappings.Add(mapping);
        return mapping;
    }

    /// <summary>
    /// Clears all mappings.
    /// </summary>
    public void ClearMappings()
    {
        ViewModel.Mappings.Clear();
    }

    /// <summary>
    /// Clears the message log.
    /// </summary>
    public void ClearLog()
    {
        ViewModel.ClearLogCommand.Execute(null);
    }

    /// <summary>
    /// Starts the OSC server.
    /// </summary>
    public void StartServer()
    {
        ViewModel.StartServerCommand.Execute(null);
    }

    /// <summary>
    /// Stops the OSC server.
    /// </summary>
    public void StopServer()
    {
        ViewModel.StopServerCommand.Execute(null);
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Initialize();

        // Sync dependency properties to view model
        ViewModel.IncomingPort = IncomingPort;
        ViewModel.OutgoingPort = OutgoingPort;
        ViewModel.TargetIPAddress = TargetIPAddress;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Shutdown();
    }

    private void ServerToggle_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleServerCommand.Execute(null);
        IsServerEnabled = ViewModel.IsServerEnabled;
    }

    private void LearnButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartLearnModeCommand.Execute(null);
    }

    private void OnViewModelMessageReceived(object? sender, OSCMessageDisplay e)
    {
        MessageReceived?.Invoke(this, e);
    }

    private void OnViewModelParameterChanged(object? sender, OSCParameterChangedEventArgs e)
    {
        ParameterChanged?.Invoke(this, e);
    }

    private void OnViewModelServerStateChanged(object? sender, bool e)
    {
        IsServerEnabled = e;
        ServerStateChanged?.Invoke(this, e);
    }

    private void OnScrollToBottomRequested(object? sender, EventArgs e)
    {
        if (MessageListBox.Items.Count > 0)
        {
            MessageListBox.ScrollIntoView(MessageListBox.Items[^1]);
        }
    }

    private static void OnIsServerEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OSCControlPanel panel && e.NewValue is bool enabled)
        {
            if (panel.ViewModel.IsServerEnabled != enabled)
            {
                if (enabled)
                {
                    panel.ViewModel.StartServerCommand.Execute(null);
                }
                else
                {
                    panel.ViewModel.StopServerCommand.Execute(null);
                }
            }
        }
    }

    private static void OnIncomingPortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OSCControlPanel panel && e.NewValue is int port)
        {
            panel.ViewModel.IncomingPort = port;
        }
    }

    private static void OnOutgoingPortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OSCControlPanel panel && e.NewValue is int port)
        {
            panel.ViewModel.OutgoingPort = port;
        }
    }

    private static void OnTargetIPAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OSCControlPanel panel && e.NewValue is string ip)
        {
            panel.ViewModel.TargetIPAddress = ip;
        }
    }

    #endregion
}
