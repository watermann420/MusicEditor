// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for OSC Control Panel.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Network;

/// <summary>
/// Represents an OSC message for display in the monitor.
/// </summary>
public partial class OSCMessageDisplay : ObservableObject
{
    /// <summary>
    /// Timestamp when the message was received.
    /// </summary>
    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    /// <summary>
    /// OSC address pattern.
    /// </summary>
    [ObservableProperty]
    private string _address = string.Empty;

    /// <summary>
    /// Message arguments as formatted string.
    /// </summary>
    [ObservableProperty]
    private string _arguments = string.Empty;

    /// <summary>
    /// Message direction (In/Out).
    /// </summary>
    [ObservableProperty]
    private OSCDirection _direction = OSCDirection.In;

    /// <summary>
    /// Gets the direction display text.
    /// </summary>
    public string DirectionDisplay => Direction == OSCDirection.In ? "IN" : "OUT";
}

/// <summary>
/// OSC message direction.
/// </summary>
public enum OSCDirection
{
    In,
    Out
}

/// <summary>
/// Represents an OSC address mapping.
/// </summary>
public partial class OSCAddressMapping : ObservableObject
{
    /// <summary>
    /// Unique identifier for the mapping.
    /// </summary>
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    /// <summary>
    /// OSC address pattern (e.g., /track/1/volume).
    /// </summary>
    [ObservableProperty]
    private string _addressPattern = string.Empty;

    /// <summary>
    /// Mapped parameter name.
    /// </summary>
    [ObservableProperty]
    private string _parameterName = string.Empty;

    /// <summary>
    /// Minimum OSC value.
    /// </summary>
    [ObservableProperty]
    private float _oscMinValue;

    /// <summary>
    /// Maximum OSC value.
    /// </summary>
    [ObservableProperty]
    private float _oscMaxValue = 1.0f;

    /// <summary>
    /// Minimum parameter value.
    /// </summary>
    [ObservableProperty]
    private float _parameterMinValue;

    /// <summary>
    /// Maximum parameter value.
    /// </summary>
    [ObservableProperty]
    private float _parameterMaxValue = 1.0f;

    /// <summary>
    /// Whether this mapping is in learn mode.
    /// </summary>
    [ObservableProperty]
    private bool _isLearning;

    /// <summary>
    /// Whether this mapping is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// Last received value.
    /// </summary>
    [ObservableProperty]
    private float _lastValue;

    /// <summary>
    /// Gets the range display text.
    /// </summary>
    public string RangeDisplay => $"OSC [{OscMinValue:F2} - {OscMaxValue:F2}] -> Param [{ParameterMinValue:F2} - {ParameterMaxValue:F2}]";
}

/// <summary>
/// Represents a preset of OSC mappings.
/// </summary>
public partial class OSCPreset : ObservableObject
{
    /// <summary>
    /// Preset name.
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Preset file path.
    /// </summary>
    [ObservableProperty]
    private string _filePath = string.Empty;
}

/// <summary>
/// Connection status enumeration.
/// </summary>
public enum OSCConnectionStatus
{
    Disconnected,
    Listening,
    Connected,
    Error
}

/// <summary>
/// ViewModel for the OSC Control Panel.
/// Manages OSC server settings, message monitoring, and address mappings.
/// </summary>
public partial class OSCControlPanelViewModel : ViewModelBase
{
    #region Constants

    private const int DefaultIncomingPort = 8000;
    private const int DefaultOutgoingPort = 9000;
    private const int DefaultMaxMessages = 1000;
    private const string DefaultTargetIP = "127.0.0.1";
    private const string PresetsFolder = "OSCPresets";

    #endregion

    #region Private Fields

    private readonly DispatcherTimer _connectionCheckTimer;
    private readonly ObservableCollection<OSCMessageDisplay> _allMessages = [];
    private int _maxMessages = DefaultMaxMessages;
    private bool _isInitialized;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets whether the OSC server is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isServerEnabled;

    /// <summary>
    /// Gets or sets the incoming port number.
    /// </summary>
    [ObservableProperty]
    private int _incomingPort = DefaultIncomingPort;

    /// <summary>
    /// Gets or sets the outgoing port number.
    /// </summary>
    [ObservableProperty]
    private int _outgoingPort = DefaultOutgoingPort;

    /// <summary>
    /// Gets or sets the target IP address.
    /// </summary>
    [ObservableProperty]
    private string _targetIPAddress = DefaultTargetIP;

    /// <summary>
    /// Gets or sets the message filter text.
    /// </summary>
    [ObservableProperty]
    private string _messageFilter = string.Empty;

    /// <summary>
    /// Gets or sets the connection status.
    /// </summary>
    [ObservableProperty]
    private OSCConnectionStatus _connectionStatus = OSCConnectionStatus.Disconnected;

    /// <summary>
    /// Gets or sets the status text.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Server stopped";

    /// <summary>
    /// Gets or sets the selected mapping.
    /// </summary>
    [ObservableProperty]
    private OSCAddressMapping? _selectedMapping;

    /// <summary>
    /// Gets or sets the selected preset.
    /// </summary>
    [ObservableProperty]
    private OSCPreset? _selectedPreset;

    /// <summary>
    /// Gets or sets the test message address.
    /// </summary>
    [ObservableProperty]
    private string _testMessageAddress = "/test";

    /// <summary>
    /// Gets or sets the test message value.
    /// </summary>
    [ObservableProperty]
    private string _testMessageValue = "1.0";

    /// <summary>
    /// Gets or sets whether auto-scroll is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _autoScrollEnabled = true;

    /// <summary>
    /// Gets or sets the messages received count.
    /// </summary>
    [ObservableProperty]
    private int _messagesReceivedCount;

    /// <summary>
    /// Gets or sets the messages sent count.
    /// </summary>
    [ObservableProperty]
    private int _messagesSentCount;

    #endregion

    #region Collections

    /// <summary>
    /// Collection of OSC messages for display.
    /// </summary>
    public ObservableCollection<OSCMessageDisplay> Messages { get; } = [];

    /// <summary>
    /// Collection of OSC address mappings.
    /// </summary>
    public ObservableCollection<OSCAddressMapping> Mappings { get; } = [];

    /// <summary>
    /// Collection of available presets.
    /// </summary>
    public ObservableCollection<OSCPreset> Presets { get; } = [];

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

    /// <summary>
    /// Occurs when scroll to bottom is requested.
    /// </summary>
    public event EventHandler? ScrollToBottomRequested;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new OSCControlPanelViewModel.
    /// </summary>
    public OSCControlPanelViewModel()
    {
        _connectionCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _connectionCheckTimer.Tick += OnConnectionCheckTick;

        LoadPresets();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the view model.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        _connectionCheckTimer.Start();
        _isInitialized = true;

        UpdateStatusText();
    }

    /// <summary>
    /// Shuts down the view model and cleans up resources.
    /// </summary>
    public void Shutdown()
    {
        _connectionCheckTimer.Stop();

        if (IsServerEnabled)
        {
            StopServer();
        }

        _isInitialized = false;
    }

    #endregion

    #region Commands

    /// <summary>
    /// Toggles the OSC server on/off.
    /// </summary>
    [RelayCommand]
    private void ToggleServer()
    {
        if (IsServerEnabled)
        {
            StopServer();
        }
        else
        {
            StartServer();
        }
    }

    /// <summary>
    /// Starts the OSC server.
    /// </summary>
    [RelayCommand]
    private void StartServer()
    {
        try
        {
            // TODO: Implement actual OSC server start logic
            // This would integrate with an OSC library like Rug.Osc or SharpOSC

            IsServerEnabled = true;
            ConnectionStatus = OSCConnectionStatus.Listening;
            UpdateStatusText();
            ServerStateChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ConnectionStatus = OSCConnectionStatus.Error;
            StatusText = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Stops the OSC server.
    /// </summary>
    [RelayCommand]
    private void StopServer()
    {
        try
        {
            // TODO: Implement actual OSC server stop logic

            IsServerEnabled = false;
            ConnectionStatus = OSCConnectionStatus.Disconnected;
            UpdateStatusText();
            ServerStateChanged?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            StatusText = $"Error stopping server: {ex.Message}";
        }
    }

    /// <summary>
    /// Sends a test OSC message.
    /// </summary>
    [RelayCommand]
    private void SendTestMessage()
    {
        if (!IsServerEnabled)
        {
            StatusText = "Server not running";
            return;
        }

        try
        {
            // TODO: Implement actual OSC message sending
            // This would use the configured target IP and outgoing port

            var message = new OSCMessageDisplay
            {
                Timestamp = DateTime.Now,
                Address = TestMessageAddress,
                Arguments = TestMessageValue,
                Direction = OSCDirection.Out
            };

            AddMessage(message);
            MessagesSentCount++;

            StatusText = $"Sent: {TestMessageAddress} {TestMessageValue}";
        }
        catch (Exception ex)
        {
            StatusText = $"Send error: {ex.Message}";
        }
    }

    /// <summary>
    /// Clears the message log.
    /// </summary>
    [RelayCommand]
    private void ClearLog()
    {
        Messages.Clear();
        _allMessages.Clear();
        MessagesReceivedCount = 0;
        MessagesSentCount = 0;
    }

    /// <summary>
    /// Adds a new mapping.
    /// </summary>
    [RelayCommand]
    private void AddMapping()
    {
        var mapping = new OSCAddressMapping
        {
            AddressPattern = "/new/address",
            ParameterName = "New Parameter"
        };

        Mappings.Add(mapping);
        SelectedMapping = mapping;
    }

    /// <summary>
    /// Removes the selected mapping.
    /// </summary>
    [RelayCommand]
    private void RemoveMapping()
    {
        if (SelectedMapping != null)
        {
            Mappings.Remove(SelectedMapping);
            SelectedMapping = Mappings.FirstOrDefault();
        }
    }

    /// <summary>
    /// Starts learn mode for the selected mapping.
    /// </summary>
    [RelayCommand]
    private void StartLearnMode()
    {
        if (SelectedMapping == null) return;

        // Cancel any existing learn mode
        foreach (var mapping in Mappings)
        {
            mapping.IsLearning = false;
        }

        SelectedMapping.IsLearning = true;
        StatusText = $"Learning: Move a control to map to '{SelectedMapping.ParameterName}'";
    }

    /// <summary>
    /// Cancels learn mode.
    /// </summary>
    [RelayCommand]
    private void CancelLearnMode()
    {
        foreach (var mapping in Mappings)
        {
            mapping.IsLearning = false;
        }

        UpdateStatusText();
    }

    /// <summary>
    /// Saves the current mappings as a preset.
    /// </summary>
    [RelayCommand]
    private void SavePreset()
    {
        try
        {
            var presetName = $"OSC_Preset_{DateTime.Now:yyyyMMdd_HHmmss}";
            var presetsPath = GetPresetsPath();

            if (!Directory.Exists(presetsPath))
            {
                Directory.CreateDirectory(presetsPath);
            }

            var filePath = Path.Combine(presetsPath, $"{presetName}.json");

            var presetData = new OSCPresetData
            {
                Name = presetName,
                Mappings = Mappings.Select(m => new OSCMappingData
                {
                    AddressPattern = m.AddressPattern,
                    ParameterName = m.ParameterName,
                    OscMinValue = m.OscMinValue,
                    OscMaxValue = m.OscMaxValue,
                    ParameterMinValue = m.ParameterMinValue,
                    ParameterMaxValue = m.ParameterMaxValue,
                    IsEnabled = m.IsEnabled
                }).ToArray()
            };

            var json = JsonSerializer.Serialize(presetData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);

            LoadPresets();
            StatusText = $"Preset saved: {presetName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving preset: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads the selected preset.
    /// </summary>
    [RelayCommand]
    private void LoadPreset()
    {
        if (SelectedPreset == null) return;

        try
        {
            var json = File.ReadAllText(SelectedPreset.FilePath);
            var presetData = JsonSerializer.Deserialize<OSCPresetData>(json);

            if (presetData?.Mappings == null) return;

            Mappings.Clear();

            foreach (var mappingData in presetData.Mappings)
            {
                Mappings.Add(new OSCAddressMapping
                {
                    AddressPattern = mappingData.AddressPattern,
                    ParameterName = mappingData.ParameterName,
                    OscMinValue = mappingData.OscMinValue,
                    OscMaxValue = mappingData.OscMaxValue,
                    ParameterMinValue = mappingData.ParameterMinValue,
                    ParameterMaxValue = mappingData.ParameterMaxValue,
                    IsEnabled = mappingData.IsEnabled
                });
            }

            StatusText = $"Preset loaded: {SelectedPreset.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading preset: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes the selected preset.
    /// </summary>
    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset == null) return;

        try
        {
            if (File.Exists(SelectedPreset.FilePath))
            {
                File.Delete(SelectedPreset.FilePath);
            }

            LoadPresets();
            StatusText = "Preset deleted";
        }
        catch (Exception ex)
        {
            StatusText = $"Error deleting preset: {ex.Message}";
        }
    }

    /// <summary>
    /// Refreshes the preset list.
    /// </summary>
    [RelayCommand]
    private void RefreshPresets()
    {
        LoadPresets();
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
        var argsString = string.Join(", ", args.Select(a => a?.ToString() ?? "null"));

        var message = new OSCMessageDisplay
        {
            Timestamp = DateTime.Now,
            Address = address,
            Arguments = argsString,
            Direction = OSCDirection.In
        };

        AddMessage(message);
        MessagesReceivedCount++;

        // Check for learn mode
        var learningMapping = Mappings.FirstOrDefault(m => m.IsLearning);
        if (learningMapping != null)
        {
            learningMapping.AddressPattern = address;
            learningMapping.IsLearning = false;
            UpdateStatusText();
        }

        // Process mappings
        foreach (var mapping in Mappings.Where(m => m.IsEnabled && !m.IsLearning))
        {
            if (MatchesPattern(address, mapping.AddressPattern))
            {
                if (args.Length > 0 && TryParseFloat(args[0], out var value))
                {
                    // Apply range mapping
                    var normalizedValue = (value - mapping.OscMinValue) /
                        (mapping.OscMaxValue - mapping.OscMinValue);
                    normalizedValue = Math.Clamp(normalizedValue, 0, 1);

                    var parameterValue = mapping.ParameterMinValue +
                        normalizedValue * (mapping.ParameterMaxValue - mapping.ParameterMinValue);

                    mapping.LastValue = parameterValue;

                    ParameterChanged?.Invoke(this, new OSCParameterChangedEventArgs(mapping, parameterValue));
                }
            }
        }

        MessageReceived?.Invoke(this, message);
    }

    /// <summary>
    /// Sends an OSC message.
    /// </summary>
    /// <param name="address">The OSC address.</param>
    /// <param name="args">The message arguments.</param>
    public void SendMessage(string address, params object[] args)
    {
        if (!IsServerEnabled) return;

        var argsString = string.Join(", ", args.Select(a => a?.ToString() ?? "null"));

        var message = new OSCMessageDisplay
        {
            Timestamp = DateTime.Now,
            Address = address,
            Arguments = argsString,
            Direction = OSCDirection.Out
        };

        AddMessage(message);
        MessagesSentCount++;

        // TODO: Implement actual OSC sending via OSC library
    }

    #endregion

    #region Private Methods

    private void AddMessage(OSCMessageDisplay message)
    {
        _allMessages.Add(message);

        // Enforce max messages
        while (_allMessages.Count > _maxMessages)
        {
            _allMessages.RemoveAt(0);
        }

        // Apply filter
        if (PassesFilter(message))
        {
            Messages.Add(message);

            while (Messages.Count > _maxMessages)
            {
                Messages.RemoveAt(0);
            }

            if (AutoScrollEnabled)
            {
                ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private bool PassesFilter(OSCMessageDisplay message)
    {
        if (string.IsNullOrWhiteSpace(MessageFilter))
        {
            return true;
        }

        return message.Address.Contains(MessageFilter, StringComparison.OrdinalIgnoreCase) ||
               message.Arguments.Contains(MessageFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPattern(string address, string pattern)
    {
        // Simple pattern matching - supports exact match and wildcard (*)
        if (pattern == "*") return true;
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2];
            return address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return address.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseFloat(object? value, out float result)
    {
        result = 0;

        if (value == null) return false;

        if (value is float f) { result = f; return true; }
        if (value is double d) { result = (float)d; return true; }
        if (value is int i) { result = i; return true; }
        if (value is string s && float.TryParse(s, out result)) return true;

        return false;
    }

    private void LoadPresets()
    {
        Presets.Clear();

        var presetsPath = GetPresetsPath();

        if (!Directory.Exists(presetsPath)) return;

        foreach (var file in Directory.GetFiles(presetsPath, "*.json"))
        {
            Presets.Add(new OSCPreset
            {
                Name = Path.GetFileNameWithoutExtension(file),
                FilePath = file
            });
        }
    }

    private static string GetPresetsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MusicEngineEditor", PresetsFolder);
    }

    private void OnConnectionCheckTick(object? sender, EventArgs e)
    {
        // Update connection status based on actual server state
        if (IsServerEnabled && ConnectionStatus != OSCConnectionStatus.Error)
        {
            ConnectionStatus = OSCConnectionStatus.Listening;
        }
    }

    private void UpdateStatusText()
    {
        if (!IsServerEnabled)
        {
            StatusText = "Server stopped";
            return;
        }

        var learningMapping = Mappings.FirstOrDefault(m => m.IsLearning);
        if (learningMapping != null)
        {
            StatusText = $"Learning: {learningMapping.ParameterName}";
            return;
        }

        StatusText = $"Listening on port {IncomingPort} | Target: {TargetIPAddress}:{OutgoingPort}";
    }

    partial void OnIsServerEnabledChanged(bool value)
    {
        UpdateStatusText();
    }

    partial void OnMessageFilterChanged(string value)
    {
        // Refresh filtered messages
        Messages.Clear();

        foreach (var message in _allMessages)
        {
            if (PassesFilter(message))
            {
                Messages.Add(message);
            }
        }
    }

    partial void OnIncomingPortChanged(int value)
    {
        if (IsServerEnabled)
        {
            // Restart server with new port
            StopServer();
            StartServer();
        }
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Event arguments for OSC parameter changes.
/// </summary>
public sealed class OSCParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// The mapping that triggered the change.
    /// </summary>
    public OSCAddressMapping Mapping { get; }

    /// <summary>
    /// The new parameter value.
    /// </summary>
    public float Value { get; }

    public OSCParameterChangedEventArgs(OSCAddressMapping mapping, float value)
    {
        Mapping = mapping;
        Value = value;
    }
}

/// <summary>
/// Data structure for preset serialization.
/// </summary>
internal sealed class OSCPresetData
{
    public string Name { get; set; } = string.Empty;
    public OSCMappingData[] Mappings { get; set; } = [];
}

/// <summary>
/// Data structure for mapping serialization.
/// </summary>
internal sealed class OSCMappingData
{
    public string AddressPattern { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public float OscMinValue { get; set; }
    public float OscMaxValue { get; set; } = 1.0f;
    public float ParameterMinValue { get; set; }
    public float ParameterMaxValue { get; set; } = 1.0f;
    public bool IsEnabled { get; set; } = true;
}

#endregion
