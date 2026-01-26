// MusicEngineEditor - MIDI Learn Panel Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Panel for visual MIDI controller mapping and learning.
/// </summary>
public partial class MIDILearnPanel : UserControl
{
    #region Properties

    /// <summary>
    /// Gets the collection of MIDI mappings.
    /// </summary>
    public ObservableCollection<MIDIMapping> Mappings { get; } = [];

    /// <summary>
    /// Gets or sets whether global learn mode is active.
    /// </summary>
    public bool IsLearning { get; private set; }

    /// <summary>
    /// Gets the currently selected mapping.
    /// </summary>
    public MIDIMapping? SelectedMapping { get; private set; }

    /// <summary>
    /// Gets the mapping currently in learn mode.
    /// </summary>
    public MIDIMapping? LearningMapping { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a parameter value should be updated.
    /// </summary>
    public event EventHandler<MIDIValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Occurs when a mapping is added, removed, or modified.
    /// </summary>
    public event EventHandler? MappingsChanged;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new MIDILearnPanel.
    /// </summary>
    public MIDILearnPanel()
    {
        InitializeComponent();

        MappingsList.ItemsSource = Mappings;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a parameter that can be MIDI-learned.
    /// </summary>
    /// <param name="parameterId">Unique identifier for the parameter.</param>
    /// <param name="parameterName">Display name for the parameter.</param>
    /// <param name="minValue">Minimum parameter value.</param>
    /// <param name="maxValue">Maximum parameter value.</param>
    /// <returns>The created mapping.</returns>
    public MIDIMapping AddParameter(string parameterId, string parameterName, double minValue = 0, double maxValue = 1)
    {
        var mapping = new MIDIMapping
        {
            ParameterId = parameterId,
            ParameterName = parameterName,
            ParameterMinValue = minValue,
            ParameterMaxValue = maxValue
        };

        Mappings.Add(mapping);
        MappingsChanged?.Invoke(this, EventArgs.Empty);

        return mapping;
    }

    /// <summary>
    /// Removes a parameter mapping.
    /// </summary>
    /// <param name="parameterId">The parameter ID to remove.</param>
    public void RemoveParameter(string parameterId)
    {
        var mapping = Mappings.FirstOrDefault(m => m.ParameterId == parameterId);
        if (mapping != null)
        {
            Mappings.Remove(mapping);
            MappingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Processes an incoming MIDI CC message.
    /// </summary>
    /// <param name="controller">CC controller number (0-127).</param>
    /// <param name="value">CC value (0-127).</param>
    /// <param name="channel">MIDI channel (0-15).</param>
    public void ProcessCC(int controller, int value, int channel = 0)
    {
        // If in learn mode, assign to learning mapping
        if (LearningMapping != null)
        {
            LearningMapping.CCNumber = controller;
            LearningMapping.Channel = channel;
            LearningMapping.IsMapped = true;
            LearningMapping.IsLearning = false;

            LearningMapping = null;
            UpdateLearnState();

            MappingsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Find mappings for this CC
        var matchingMappings = Mappings
            .Where(m => m.IsMapped && m.CCNumber == controller && m.Channel == channel)
            .ToList();

        foreach (var mapping in matchingMappings)
        {
            // Apply range and inversion
            var normalizedValue = (value - mapping.MidiMinValue) / (double)(mapping.MidiMaxValue - mapping.MidiMinValue);
            normalizedValue = Math.Clamp(normalizedValue, 0, 1);

            if (mapping.Invert)
            {
                normalizedValue = 1 - normalizedValue;
            }

            // Handle pickup mode
            if (mapping.PickupMode && !mapping.PickedUp)
            {
                var currentNormalized = (mapping.CurrentValue - mapping.ParameterMinValue) /
                    (mapping.ParameterMaxValue - mapping.ParameterMinValue);

                if (Math.Abs(normalizedValue - currentNormalized) > 0.05)
                {
                    // Not yet picked up
                    continue;
                }
                mapping.PickedUp = true;
            }

            // Calculate parameter value
            var parameterValue = mapping.ParameterMinValue +
                normalizedValue * (mapping.ParameterMaxValue - mapping.ParameterMinValue);

            mapping.CurrentValue = parameterValue;
            mapping.LastMidiValue = value;

            ValueChanged?.Invoke(this, new MIDIValueChangedEventArgs(mapping, parameterValue));
        }
    }

    /// <summary>
    /// Processes an incoming MIDI note message.
    /// </summary>
    /// <param name="noteNumber">MIDI note number (0-127).</param>
    /// <param name="velocity">Note velocity (0-127).</param>
    /// <param name="channel">MIDI channel (0-15).</param>
    public void ProcessNote(int noteNumber, int velocity, int channel = 0)
    {
        // If in learn mode, assign note to learning mapping
        if (LearningMapping != null)
        {
            LearningMapping.NoteNumber = noteNumber;
            LearningMapping.Channel = channel;
            LearningMapping.IsNoteMapping = true;
            LearningMapping.IsMapped = true;
            LearningMapping.IsLearning = false;

            LearningMapping = null;
            UpdateLearnState();

            MappingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Clears all mappings.
    /// </summary>
    public void ClearAllMappings()
    {
        foreach (var mapping in Mappings)
        {
            mapping.IsMapped = false;
            mapping.CCNumber = -1;
            mapping.NoteNumber = -1;
            mapping.IsLearning = false;
        }

        MappingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Starts learn mode for a specific mapping.
    /// </summary>
    /// <param name="mapping">The mapping to learn.</param>
    public void StartLearn(MIDIMapping mapping)
    {
        // Cancel any previous learning
        if (LearningMapping != null)
        {
            LearningMapping.IsLearning = false;
        }

        LearningMapping = mapping;
        mapping.IsLearning = true;

        UpdateLearnState();
    }

    /// <summary>
    /// Cancels learn mode.
    /// </summary>
    public void CancelLearn()
    {
        if (LearningMapping != null)
        {
            LearningMapping.IsLearning = false;
            LearningMapping = null;
        }

        UpdateLearnState();
    }

    #endregion

    #region Event Handlers

    private void GlobalLearn_Click(object sender, RoutedEventArgs e)
    {
        IsLearning = GlobalLearnToggle.IsChecked == true;

        if (IsLearning)
        {
            // Enable learn on next touch
            StatusText.Text = "Move a MIDI controller or click a parameter...";
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        }
        else
        {
            CancelLearn();
            StatusText.Text = "Ready";
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        }
    }

    private void MappingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedMapping = MappingsList.SelectedItem as MIDIMapping;
        UpdateEditorPanel();
    }

    private void LearnMapping_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MIDIMapping mapping)
        {
            StartLearn(mapping);
        }
    }

    private void ClearMapping_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MIDIMapping mapping)
        {
            mapping.IsMapped = false;
            mapping.CCNumber = -1;
            mapping.NoteNumber = -1;
            mapping.IsLearning = false;
            mapping.PickedUp = false;

            MappingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MinValue_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SelectedMapping != null && int.TryParse(MinValueTextBox.Text, out var value))
        {
            SelectedMapping.MidiMinValue = Math.Clamp(value, 0, 127);
        }
    }

    private void MaxValue_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SelectedMapping != null && int.TryParse(MaxValueTextBox.Text, out var value))
        {
            SelectedMapping.MidiMaxValue = Math.Clamp(value, 0, 127);
        }
    }

    private void Invert_Changed(object sender, RoutedEventArgs e)
    {
        if (SelectedMapping != null)
        {
            SelectedMapping.Invert = InvertCheck.IsChecked == true;
        }
    }

    private void Pickup_Changed(object sender, RoutedEventArgs e)
    {
        if (SelectedMapping != null)
        {
            SelectedMapping.PickupMode = PickupCheck.IsChecked == true;
            SelectedMapping.PickedUp = false;
        }
    }

    private void ResetMapping_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMapping != null)
        {
            SelectedMapping.MidiMinValue = 0;
            SelectedMapping.MidiMaxValue = 127;
            SelectedMapping.Invert = false;
            SelectedMapping.PickupMode = false;

            UpdateEditorPanel();
        }
    }

    private void ApplyMapping_Click(object sender, RoutedEventArgs e)
    {
        MappingsChanged?.Invoke(this, EventArgs.Empty);
        MappingEditorPanel.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Private Methods

    private void UpdateLearnState()
    {
        if (LearningMapping != null)
        {
            StatusText.Text = $"Waiting for MIDI input for '{LearningMapping.ParameterName}'...";
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            GlobalLearnToggle.IsChecked = true;
        }
        else
        {
            StatusText.Text = "Ready";
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
            GlobalLearnToggle.IsChecked = false;
        }

        MappingsList.Items.Refresh();
    }

    private void UpdateEditorPanel()
    {
        if (SelectedMapping == null)
        {
            MappingEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        MappingEditorPanel.Visibility = Visibility.Visible;

        // Update source text
        if (SelectedMapping.IsNoteMapping)
        {
            SourceText.Text = $"Note {SelectedMapping.NoteNumber}";
        }
        else if (SelectedMapping.IsMapped)
        {
            SourceText.Text = $"CC{SelectedMapping.CCNumber}";
        }
        else
        {
            SourceText.Text = "Not mapped";
        }

        // Update range controls
        MinValueTextBox.Text = SelectedMapping.MidiMinValue.ToString();
        MaxValueTextBox.Text = SelectedMapping.MidiMaxValue.ToString();
        InvertCheck.IsChecked = SelectedMapping.Invert;
        PickupCheck.IsChecked = SelectedMapping.PickupMode;
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Represents a MIDI to parameter mapping.
/// </summary>
public partial class MIDIMapping : ObservableObject
{
    /// <summary>
    /// Unique identifier for the parameter.
    /// </summary>
    [ObservableProperty]
    private string _parameterId = "";

    /// <summary>
    /// Display name for the parameter.
    /// </summary>
    [ObservableProperty]
    private string _parameterName = "";

    /// <summary>
    /// CC controller number (0-127), or -1 if not mapped.
    /// </summary>
    [ObservableProperty]
    private int _cCNumber = -1;

    /// <summary>
    /// Note number for note-based mappings.
    /// </summary>
    [ObservableProperty]
    private int _noteNumber = -1;

    /// <summary>
    /// MIDI channel (0-15).
    /// </summary>
    [ObservableProperty]
    private int _channel;

    /// <summary>
    /// Minimum MIDI value to consider.
    /// </summary>
    [ObservableProperty]
    private int _midiMinValue;

    /// <summary>
    /// Maximum MIDI value to consider.
    /// </summary>
    [ObservableProperty]
    private int _midiMaxValue = 127;

    /// <summary>
    /// Minimum parameter value.
    /// </summary>
    [ObservableProperty]
    private double _parameterMinValue;

    /// <summary>
    /// Maximum parameter value.
    /// </summary>
    [ObservableProperty]
    private double _parameterMaxValue = 1;

    /// <summary>
    /// Current parameter value.
    /// </summary>
    [ObservableProperty]
    private double _currentValue;

    /// <summary>
    /// Last received MIDI value.
    /// </summary>
    [ObservableProperty]
    private int _lastMidiValue;

    /// <summary>
    /// Whether the mapping is inverted.
    /// </summary>
    [ObservableProperty]
    private bool _invert;

    /// <summary>
    /// Whether pickup mode is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _pickupMode;

    /// <summary>
    /// Whether the controller has picked up the current value.
    /// </summary>
    [ObservableProperty]
    private bool _pickedUp;

    /// <summary>
    /// Whether this mapping is configured.
    /// </summary>
    [ObservableProperty]
    private bool _isMapped;

    /// <summary>
    /// Whether this is a note-based mapping.
    /// </summary>
    [ObservableProperty]
    private bool _isNoteMapping;

    /// <summary>
    /// Whether learn mode is active for this mapping.
    /// </summary>
    [ObservableProperty]
    private bool _isLearning;

    /// <summary>
    /// Gets the MIDI assignment display text.
    /// </summary>
    public string MidiAssignmentDisplay
    {
        get
        {
            if (IsLearning) return "Learning...";
            if (!IsMapped) return "Not mapped";
            if (IsNoteMapping) return $"Note {NoteNumber}";
            return $"CC{CCNumber}";
        }
    }

    /// <summary>
    /// Gets the range display text.
    /// </summary>
    public string RangeDisplay => $"{MidiMinValue}-{MidiMaxValue}";

    /// <summary>
    /// Gets whether a range is configured.
    /// </summary>
    public bool HasRange => MidiMinValue > 0 || MidiMaxValue < 127;
}

/// <summary>
/// Event arguments for MIDI value changes.
/// </summary>
public sealed class MIDIValueChangedEventArgs : EventArgs
{
    /// <summary>
    /// The mapping that triggered the change.
    /// </summary>
    public MIDIMapping Mapping { get; }

    /// <summary>
    /// The new parameter value.
    /// </summary>
    public double Value { get; }

    public MIDIValueChangedEventArgs(MIDIMapping mapping, double value)
    {
        Mapping = mapping;
        Value = value;
    }
}

#endregion
