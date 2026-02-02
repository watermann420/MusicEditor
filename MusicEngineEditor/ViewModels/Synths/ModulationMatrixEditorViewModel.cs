// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Visual Modulation Matrix Editor control.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Synths;

#region Enums

/// <summary>
/// Types of modulation curve shapes.
/// </summary>
public enum ModulationCurveType
{
    Linear,
    Exponential,
    Logarithmic,
    SCurve
}

/// <summary>
/// Categories of modulation sources.
/// </summary>
public enum ModulationSourceCategory
{
    LFO,
    Envelope,
    MIDI,
    Other
}

/// <summary>
/// Categories of modulation destinations.
/// </summary>
public enum ModulationDestinationCategory
{
    Oscillator,
    Filter,
    Amp,
    Effects
}

#endregion

#region Source Item ViewModel

/// <summary>
/// Represents a modulation source item in the editor.
/// </summary>
public partial class ModulationSourceItemViewModel : ObservableObject
{
    /// <summary>
    /// Unique identifier for the source.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display name of the source.
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Category of the source.
    /// </summary>
    public ModulationSourceCategory Category { get; }

    /// <summary>
    /// Icon text for the source type.
    /// </summary>
    [ObservableProperty]
    private string _typeIcon = string.Empty;

    /// <summary>
    /// Whether the source is currently active (producing signal).
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Current value of the source (0-1 or -1 to 1).
    /// </summary>
    [ObservableProperty]
    private float _currentValue;

    /// <summary>
    /// Whether the source is bipolar.
    /// </summary>
    [ObservableProperty]
    private bool _isBipolar;

    public ModulationSourceItemViewModel(string id, string name, ModulationSourceCategory category, string typeIcon, bool isBipolar = false)
    {
        Id = id;
        _name = name;
        Category = category;
        _typeIcon = typeIcon;
        _isBipolar = isBipolar;
    }
}

#endregion

#region Destination Item ViewModel

/// <summary>
/// Represents a modulation destination item in the editor.
/// </summary>
public partial class ModulationDestinationItemViewModel : ObservableObject
{
    /// <summary>
    /// Unique identifier/path for the destination.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display name of the destination.
    /// </summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>
    /// Category of the destination.
    /// </summary>
    public ModulationDestinationCategory Category { get; }

    /// <summary>
    /// Whether this destination is currently being modulated.
    /// </summary>
    [ObservableProperty]
    private bool _isModulated;

    /// <summary>
    /// Total modulation amount from all sources.
    /// </summary>
    [ObservableProperty]
    private float _totalModulation;

    public ModulationDestinationItemViewModel(string id, string displayName, ModulationDestinationCategory category)
    {
        Id = id;
        _displayName = displayName;
        Category = category;
    }
}

#endregion

#region Connection ViewModel

/// <summary>
/// Represents a modulation connection between a source and destination.
/// </summary>
public partial class ModulationConnectionViewModel : ObservableObject
{
    /// <summary>
    /// The source of the modulation.
    /// </summary>
    public ModulationSourceItemViewModel Source { get; }

    /// <summary>
    /// The destination of the modulation.
    /// </summary>
    public ModulationDestinationItemViewModel Destination { get; }

    /// <summary>
    /// Display name of the source.
    /// </summary>
    public string SourceName => Source.Name;

    /// <summary>
    /// Display name of the destination.
    /// </summary>
    public string DestinationName => Destination.DisplayName;

    /// <summary>
    /// Modulation amount (-100 to +100 percent).
    /// </summary>
    [ObservableProperty]
    private double _amount;

    /// <summary>
    /// Type of curve for the modulation.
    /// </summary>
    [ObservableProperty]
    private ModulationCurveType _curveType = ModulationCurveType.Linear;

    /// <summary>
    /// Whether the modulation is bipolar.
    /// </summary>
    [ObservableProperty]
    private bool _isBipolar;

    /// <summary>
    /// Whether this connection is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    public ModulationConnectionViewModel(ModulationSourceItemViewModel source, ModulationDestinationItemViewModel destination)
    {
        Source = source;
        Destination = destination;
        _amount = 50; // Default to 50%
    }

    partial void OnAmountChanged(double value)
    {
        // Notify that amount has changed (for visual updates)
        OnPropertyChanged(nameof(Amount));
    }
}

#endregion

#region Preset Data

/// <summary>
/// Data structure for saving/loading modulation presets.
/// </summary>
public class ModulationPresetData
{
    public string Name { get; set; } = string.Empty;
    public ModulationConnectionData[] Connections { get; set; } = Array.Empty<ModulationConnectionData>();
}

/// <summary>
/// Data structure for a single connection in a preset.
/// </summary>
public class ModulationConnectionData
{
    public string SourceId { get; set; } = string.Empty;
    public string DestinationId { get; set; } = string.Empty;
    public double Amount { get; set; }
    public ModulationCurveType CurveType { get; set; }
    public bool IsBipolar { get; set; }
}

#endregion

/// <summary>
/// ViewModel for the Visual Modulation Matrix Editor.
/// Manages sources, destinations, and connections for modulation routing.
/// </summary>
public partial class ModulationMatrixEditorViewModel : ViewModelBase
{
    #region Events

    /// <summary>
    /// Raised when a connection is added.
    /// </summary>
    public event EventHandler<ModulationConnectionViewModel>? ConnectionAdded;

    /// <summary>
    /// Raised when a connection is removed.
    /// </summary>
    public event EventHandler<ModulationConnectionViewModel>? ConnectionRemoved;

    /// <summary>
    /// Raised when all connections are cleared.
    /// </summary>
    public event EventHandler? ConnectionsCleared;

    #endregion

    #region Observable Properties

    /// <summary>
    /// All modulation connections.
    /// </summary>
    public ObservableCollection<ModulationConnectionViewModel> Connections { get; } = new();

    /// <summary>
    /// LFO modulation sources.
    /// </summary>
    public ObservableCollection<ModulationSourceItemViewModel> LFOSources { get; } = new();

    /// <summary>
    /// Envelope modulation sources.
    /// </summary>
    public ObservableCollection<ModulationSourceItemViewModel> EnvelopeSources { get; } = new();

    /// <summary>
    /// MIDI modulation sources.
    /// </summary>
    public ObservableCollection<ModulationSourceItemViewModel> MIDISources { get; } = new();

    /// <summary>
    /// Oscillator destinations.
    /// </summary>
    public ObservableCollection<ModulationDestinationItemViewModel> OscillatorDestinations { get; } = new();

    /// <summary>
    /// Filter destinations.
    /// </summary>
    public ObservableCollection<ModulationDestinationItemViewModel> FilterDestinations { get; } = new();

    /// <summary>
    /// Amp destinations.
    /// </summary>
    public ObservableCollection<ModulationDestinationItemViewModel> AmpDestinations { get; } = new();

    /// <summary>
    /// Effects destinations.
    /// </summary>
    public ObservableCollection<ModulationDestinationItemViewModel> EffectsDestinations { get; } = new();

    /// <summary>
    /// Available MIDI CC numbers.
    /// </summary>
    public ObservableCollection<int> MIDICCNumbers { get; } = new();

    /// <summary>
    /// Selected MIDI CC number.
    /// </summary>
    [ObservableProperty]
    private int _selectedMIDICC = 1;

    /// <summary>
    /// Currently selected connection.
    /// </summary>
    [ObservableProperty]
    private ModulationConnectionViewModel? _selectedConnection;

    /// <summary>
    /// Whether there is a selected connection.
    /// </summary>
    [ObservableProperty]
    private bool _hasSelectedConnection;

    /// <summary>
    /// Number of active connections.
    /// </summary>
    [ObservableProperty]
    private int _activeConnectionCount;

    /// <summary>
    /// Available curve types.
    /// </summary>
    public ModulationCurveType[] CurveTypes { get; } = (ModulationCurveType[])Enum.GetValues(typeof(ModulationCurveType));

    #endregion

    #region Constructor

    public ModulationMatrixEditorViewModel()
    {
        InitializeSources();
        InitializeDestinations();
        InitializeMIDICCNumbers();
    }

    #endregion

    #region Initialization

    private void InitializeSources()
    {
        // LFO Sources
        LFOSources.Add(new ModulationSourceItemViewModel("LFO1", "LFO 1", ModulationSourceCategory.LFO, "~", true));
        LFOSources.Add(new ModulationSourceItemViewModel("LFO2", "LFO 2", ModulationSourceCategory.LFO, "~", true));
        LFOSources.Add(new ModulationSourceItemViewModel("LFO3", "LFO 3", ModulationSourceCategory.LFO, "~", true));

        // Envelope Sources
        EnvelopeSources.Add(new ModulationSourceItemViewModel("ENV1", "Envelope 1", ModulationSourceCategory.Envelope, "/\\", false));
        EnvelopeSources.Add(new ModulationSourceItemViewModel("ENV2", "Envelope 2", ModulationSourceCategory.Envelope, "/\\", false));

        // MIDI Sources
        MIDISources.Add(new ModulationSourceItemViewModel("VEL", "Velocity", ModulationSourceCategory.MIDI, "V", false));
        MIDISources.Add(new ModulationSourceItemViewModel("AT", "Aftertouch", ModulationSourceCategory.MIDI, "AT", false));
        MIDISources.Add(new ModulationSourceItemViewModel("MW", "Mod Wheel", ModulationSourceCategory.MIDI, "MW", false));
        MIDISources.Add(new ModulationSourceItemViewModel("PB", "Pitch Bend", ModulationSourceCategory.MIDI, "PB", true));
    }

    private void InitializeDestinations()
    {
        // Oscillator Destinations
        OscillatorDestinations.Add(new ModulationDestinationItemViewModel("OSC1.Pitch", "Osc 1 Pitch", ModulationDestinationCategory.Oscillator));
        OscillatorDestinations.Add(new ModulationDestinationItemViewModel("OSC2.Pitch", "Osc 2 Pitch", ModulationDestinationCategory.Oscillator));
        OscillatorDestinations.Add(new ModulationDestinationItemViewModel("OSC1.PW", "Osc 1 Pulse Width", ModulationDestinationCategory.Oscillator));
        OscillatorDestinations.Add(new ModulationDestinationItemViewModel("OSC2.PW", "Osc 2 Pulse Width", ModulationDestinationCategory.Oscillator));
        OscillatorDestinations.Add(new ModulationDestinationItemViewModel("OSC.Mix", "Osc Mix", ModulationDestinationCategory.Oscillator));

        // Filter Destinations
        FilterDestinations.Add(new ModulationDestinationItemViewModel("FILTER.Cutoff", "Filter Cutoff", ModulationDestinationCategory.Filter));
        FilterDestinations.Add(new ModulationDestinationItemViewModel("FILTER.Resonance", "Filter Resonance", ModulationDestinationCategory.Filter));
        FilterDestinations.Add(new ModulationDestinationItemViewModel("FILTER.Drive", "Filter Drive", ModulationDestinationCategory.Filter));

        // Amp Destinations
        AmpDestinations.Add(new ModulationDestinationItemViewModel("AMP.Level", "Amplitude", ModulationDestinationCategory.Amp));
        AmpDestinations.Add(new ModulationDestinationItemViewModel("AMP.Pan", "Pan", ModulationDestinationCategory.Amp));

        // Effects Destinations
        EffectsDestinations.Add(new ModulationDestinationItemViewModel("FX.Delay.Time", "Delay Time", ModulationDestinationCategory.Effects));
        EffectsDestinations.Add(new ModulationDestinationItemViewModel("FX.Delay.Feedback", "Delay Feedback", ModulationDestinationCategory.Effects));
        EffectsDestinations.Add(new ModulationDestinationItemViewModel("FX.Reverb.Size", "Reverb Size", ModulationDestinationCategory.Effects));
        EffectsDestinations.Add(new ModulationDestinationItemViewModel("FX.Chorus.Rate", "Chorus Rate", ModulationDestinationCategory.Effects));
        EffectsDestinations.Add(new ModulationDestinationItemViewModel("FX.Chorus.Depth", "Chorus Depth", ModulationDestinationCategory.Effects));
    }

    private void InitializeMIDICCNumbers()
    {
        // Add common MIDI CC numbers
        for (int i = 1; i <= 127; i++)
        {
            MIDICCNumbers.Add(i);
        }
    }

    #endregion

    #region Property Changed Handlers

    partial void OnSelectedConnectionChanged(ModulationConnectionViewModel? value)
    {
        HasSelectedConnection = value != null;

        // Update selection state on all connections
        foreach (var conn in Connections)
        {
            conn.IsSelected = conn == value;
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Creates a new connection between a source and destination.
    /// </summary>
    [RelayCommand]
    private void CreateConnection(Tuple<ModulationSourceItemViewModel, ModulationDestinationItemViewModel> sourceAndDest)
    {
        if (sourceAndDest == null) return;

        var source = sourceAndDest.Item1;
        var destination = sourceAndDest.Item2;

        // Check if connection already exists
        if (Connections.Any(c => c.Source == source && c.Destination == destination))
        {
            StatusMessage = $"Connection from {source.Name} to {destination.DisplayName} already exists";
            return;
        }

        var connection = new ModulationConnectionViewModel(source, destination);
        Connections.Add(connection);
        destination.IsModulated = true;
        source.IsActive = true;

        ActiveConnectionCount = Connections.Count;
        ConnectionAdded?.Invoke(this, connection);

        // Select the new connection
        SelectedConnection = connection;

        StatusMessage = $"Created connection: {source.Name} -> {destination.DisplayName}";
    }

    /// <summary>
    /// Selects a connection for editing.
    /// </summary>
    [RelayCommand]
    private void SelectConnection(ModulationConnectionViewModel? connection)
    {
        SelectedConnection = connection;
    }

    /// <summary>
    /// Deselects the current connection.
    /// </summary>
    [RelayCommand]
    private void DeselectConnection()
    {
        SelectedConnection = null;
    }

    /// <summary>
    /// Selects a connection going to a specific destination.
    /// </summary>
    [RelayCommand]
    private void SelectDestination(ModulationDestinationItemViewModel destination)
    {
        var connection = Connections.FirstOrDefault(c => c.Destination == destination);
        if (connection != null)
        {
            SelectedConnection = connection;
        }
    }

    /// <summary>
    /// Deletes the currently selected connection.
    /// </summary>
    [RelayCommand]
    private void DeleteConnection()
    {
        if (SelectedConnection == null) return;

        var connection = SelectedConnection;
        Connections.Remove(connection);

        // Update destination modulation state
        connection.Destination.IsModulated = Connections.Any(c => c.Destination == connection.Destination);

        // Update source active state
        connection.Source.IsActive = Connections.Any(c => c.Source == connection.Source);

        ActiveConnectionCount = Connections.Count;
        ConnectionRemoved?.Invoke(this, connection);

        SelectedConnection = null;
        StatusMessage = $"Deleted connection: {connection.SourceName} -> {connection.DestinationName}";
    }

    /// <summary>
    /// Increments the amount of the selected connection.
    /// </summary>
    [RelayCommand]
    private void IncrementAmount()
    {
        if (SelectedConnection == null) return;
        SelectedConnection.Amount = Math.Min(SelectedConnection.Amount + 5, 100);
    }

    /// <summary>
    /// Decrements the amount of the selected connection.
    /// </summary>
    [RelayCommand]
    private void DecrementAmount()
    {
        if (SelectedConnection == null) return;
        SelectedConnection.Amount = Math.Max(SelectedConnection.Amount - 5, -100);
    }

    /// <summary>
    /// Clears all connections.
    /// </summary>
    [RelayCommand]
    private void ClearAllConnections()
    {
        Connections.Clear();

        // Reset all destination modulation states
        foreach (var dest in OscillatorDestinations) dest.IsModulated = false;
        foreach (var dest in FilterDestinations) dest.IsModulated = false;
        foreach (var dest in AmpDestinations) dest.IsModulated = false;
        foreach (var dest in EffectsDestinations) dest.IsModulated = false;

        // Reset all source active states
        foreach (var src in LFOSources) src.IsActive = false;
        foreach (var src in EnvelopeSources) src.IsActive = false;
        foreach (var src in MIDISources) src.IsActive = false;

        ActiveConnectionCount = 0;
        SelectedConnection = null;
        ConnectionsCleared?.Invoke(this, EventArgs.Empty);

        StatusMessage = "Cleared all modulation connections";
    }

    /// <summary>
    /// Saves the current modulation setup as a preset.
    /// </summary>
    [RelayCommand]
    private void SavePreset()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Modulation Preset",
            Filter = "Modulation Preset (*.modpreset)|*.modpreset",
            DefaultExt = ".modpreset"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var preset = new ModulationPresetData
                {
                    Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                    Connections = Connections.Select(c => new ModulationConnectionData
                    {
                        SourceId = c.Source.Id,
                        DestinationId = c.Destination.Id,
                        Amount = c.Amount,
                        CurveType = c.CurveType,
                        IsBipolar = c.IsBipolar
                    }).ToArray()
                };

                var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);

                StatusMessage = $"Saved preset: {preset.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save preset: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Loads a modulation preset.
    /// </summary>
    [RelayCommand]
    private void LoadPreset()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load Modulation Preset",
            Filter = "Modulation Preset (*.modpreset)|*.modpreset|All Files (*.*)|*.*",
            DefaultExt = ".modpreset"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var preset = JsonSerializer.Deserialize<ModulationPresetData>(json);

                if (preset == null)
                {
                    StatusMessage = "Failed to load preset: Invalid format";
                    return;
                }

                // Clear existing connections
                ClearAllConnections();

                // Load connections from preset
                foreach (var connData in preset.Connections)
                {
                    var source = FindSourceById(connData.SourceId);
                    var destination = FindDestinationById(connData.DestinationId);

                    if (source != null && destination != null)
                    {
                        var connection = new ModulationConnectionViewModel(source, destination)
                        {
                            Amount = connData.Amount,
                            CurveType = connData.CurveType,
                            IsBipolar = connData.IsBipolar
                        };

                        Connections.Add(connection);
                        destination.IsModulated = true;
                        source.IsActive = true;
                        ConnectionAdded?.Invoke(this, connection);
                    }
                }

                ActiveConnectionCount = Connections.Count;
                StatusMessage = $"Loaded preset: {preset.Name} ({Connections.Count} connections)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load preset: {ex.Message}";
            }
        }
    }

    #endregion

    #region Helper Methods

    private ModulationSourceItemViewModel? FindSourceById(string id)
    {
        return LFOSources.FirstOrDefault(s => s.Id == id) ??
               EnvelopeSources.FirstOrDefault(s => s.Id == id) ??
               MIDISources.FirstOrDefault(s => s.Id == id);
    }

    private ModulationDestinationItemViewModel? FindDestinationById(string id)
    {
        return OscillatorDestinations.FirstOrDefault(d => d.Id == id) ??
               FilterDestinations.FirstOrDefault(d => d.Id == id) ??
               AmpDestinations.FirstOrDefault(d => d.Id == id) ??
               EffectsDestinations.FirstOrDefault(d => d.Id == id);
    }

    /// <summary>
    /// Updates source values for real-time visualization.
    /// </summary>
    public void UpdateSourceValues()
    {
        // This would be called periodically to update source activity indicators
        // For now, we just pulse the active sources
        foreach (var source in LFOSources.Concat(EnvelopeSources).Concat(MIDISources))
        {
            if (source.IsActive)
            {
                // Simulate LFO oscillation
                source.CurrentValue = (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 2 * Math.PI);
            }
        }
    }

    /// <summary>
    /// Gets a connection by source and destination.
    /// </summary>
    public ModulationConnectionViewModel? GetConnection(ModulationSourceItemViewModel source, ModulationDestinationItemViewModel destination)
    {
        return Connections.FirstOrDefault(c => c.Source == source && c.Destination == destination);
    }

    /// <summary>
    /// Adds a custom MIDI CC source.
    /// </summary>
    public void AddMIDICCSource(int ccNumber)
    {
        var id = $"CC{ccNumber}";
        if (MIDISources.Any(s => s.Id == id)) return;

        MIDISources.Add(new ModulationSourceItemViewModel(id, $"CC {ccNumber}", ModulationSourceCategory.MIDI, "CC", false));
    }

    #endregion
}
