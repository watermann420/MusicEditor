// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Panel for editing synthesizer parameters.

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a registered synth in the editor.
/// </summary>
public class RegisteredSynth
{
    /// <summary>
    /// The synth instance.
    /// </summary>
    public object Synth { get; set; } = null!;

    /// <summary>
    /// Display name of the synth.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Type identifier (Simple, Poly, FM, etc.).
    /// </summary>
    public string TypeName { get; set; } = "";

    /// <summary>
    /// Display name for the ComboBox.
    /// </summary>
    public string DisplayName => $"{Name} ({TypeName})";
}

/// <summary>
/// Panel that displays the appropriate synth editor control based on the selected synth type.
/// </summary>
public partial class SynthEditorPanel : UserControl
{
    private string? _currentSynthType;
    private object? _currentSynth;
    private bool _isUpdatingSelection;

    /// <summary>
    /// Collection of registered synths available for editing.
    /// </summary>
    public ObservableCollection<RegisteredSynth> RegisteredSynths { get; } = new();

    /// <summary>
    /// Event raised when the close button is clicked.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when synth parameters change.
    /// </summary>
    public event EventHandler<SynthParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Creates a new SynthEditorPanel.
    /// </summary>
    public SynthEditorPanel()
    {
        InitializeComponent();
        SynthSelector.ItemsSource = RegisteredSynths;
    }

    /// <summary>
    /// Gets or sets the current synth being edited.
    /// </summary>
    public object? CurrentSynth
    {
        get => _currentSynth;
        set
        {
            _currentSynth = value;
            UpdateSynthDisplay();
        }
    }

    /// <summary>
    /// Gets the current synth type.
    /// </summary>
    public string? CurrentSynthType => _currentSynthType;

    /// <summary>
    /// Registers a synth so it appears in the selection dropdown.
    /// </summary>
    /// <param name="synth">The synth object</param>
    /// <param name="name">Display name of the synth</param>
    /// <param name="typeName">Type identifier (Simple, Poly, FM, etc.)</param>
    public void RegisterSynth(object synth, string name, string typeName)
    {
        // Check if already registered
        foreach (var existing in RegisteredSynths)
        {
            if (ReferenceEquals(existing.Synth, synth))
            {
                // Update existing entry
                existing.Name = name;
                existing.TypeName = typeName;
                return;
            }
        }

        // Add new entry
        RegisteredSynths.Add(new RegisteredSynth
        {
            Synth = synth,
            Name = name,
            TypeName = typeName
        });
    }

    /// <summary>
    /// Clears all registered synths. Call this when scripts are reloaded.
    /// </summary>
    public void ClearRegisteredSynths()
    {
        RegisteredSynths.Clear();
        CloseSynth();
    }

    /// <summary>
    /// Opens the synth editor for a specific synth instance.
    /// </summary>
    /// <param name="synth">The synth object to edit</param>
    /// <param name="synthName">Display name of the synth</param>
    /// <param name="synthType">Type identifier (Simple, Poly, FM, etc.)</param>
    public void OpenSynth(object? synth, string synthName, string synthType)
    {
        _currentSynth = synth;
        _currentSynthType = synthType;

        SynthNameText.Text = synthName;
        SynthTypeText.Text = GetSynthTypeDisplayName(synthType);

        ShowSynthEditor(synthType);

        // Set the DataContext of the active editor to the synth
        var activeEditor = GetActiveEditor(synthType);
        if (activeEditor != null)
        {
            activeEditor.DataContext = synth;
        }

        // Update ComboBox selection
        _isUpdatingSelection = true;
        try
        {
            foreach (var item in RegisteredSynths)
            {
                if (ReferenceEquals(item.Synth, synth))
                {
                    SynthSelector.SelectedItem = item;
                    break;
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    /// <summary>
    /// Opens the synth editor for a synth type without a specific instance (for preview/creation).
    /// </summary>
    /// <param name="synthType">Type identifier (Simple, Poly, FM, etc.)</param>
    public void OpenSynthByType(string synthType)
    {
        _currentSynth = null;
        _currentSynthType = synthType;

        SynthNameText.Text = GetSynthTypeDisplayName(synthType);
        SynthTypeText.Text = "Preview Mode";

        ShowSynthEditor(synthType);
    }

    /// <summary>
    /// Closes the synth editor and shows the empty state.
    /// </summary>
    public void CloseSynth()
    {
        _currentSynth = null;
        _currentSynthType = null;

        SynthNameText.Text = "No Synth Selected";
        SynthTypeText.Text = "Select a synth to edit";

        HideAllEditors();
        NoSynthPanel.Visibility = Visibility.Visible;

        _isUpdatingSelection = true;
        try
        {
            SynthSelector.SelectedItem = null;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void ShowSynthEditor(string synthType)
    {
        HideAllEditors();

        var editor = GetActiveEditor(synthType);
        if (editor != null)
        {
            editor.Visibility = Visibility.Visible;
        }
        else
        {
            // Unknown synth type, show no synth panel
            NoSynthPanel.Visibility = Visibility.Visible;
        }
    }

    private FrameworkElement? GetActiveEditor(string synthType)
    {
        return synthType?.ToLowerInvariant() switch
        {
            "simple" or "simplesynth" => SimpleSynthEditor,
            "poly" or "polysynth" => PolySynthEditor,
            "fm" or "fmsynth" => FMSynthEditor,
            "supersaw" or "supersawsynth" => SupersawSynthEditor,
            "advanced" or "advancedsynth" => AdvancedSynthEditor,
            "granular" or "granularsynth" => GranularSynthEditor,
            "sample" or "samplesynth" or "sampler" => SampleSynthEditor,
            "speech" or "speechsynth" or "vocal" => SpeechSynthEditor,
            "physical" or "physicalmodeling" or "pm" => PhysicalModelingSynthEditor,
            "noise" or "noisegenerator" => NoiseGeneratorEditor,
            "wavetable" or "wavetablesynth" => WavetableSynthEditor,
            "vector" or "vectorsynth" => VectorSynthEditor,
            _ => null
        };
    }

    private void HideAllEditors()
    {
        NoSynthPanel.Visibility = Visibility.Collapsed;
        SimpleSynthEditor.Visibility = Visibility.Collapsed;
        PolySynthEditor.Visibility = Visibility.Collapsed;
        FMSynthEditor.Visibility = Visibility.Collapsed;
        SupersawSynthEditor.Visibility = Visibility.Collapsed;
        AdvancedSynthEditor.Visibility = Visibility.Collapsed;
        GranularSynthEditor.Visibility = Visibility.Collapsed;
        SampleSynthEditor.Visibility = Visibility.Collapsed;
        SpeechSynthEditor.Visibility = Visibility.Collapsed;
        PhysicalModelingSynthEditor.Visibility = Visibility.Collapsed;
        NoiseGeneratorEditor.Visibility = Visibility.Collapsed;
        WavetableSynthEditor.Visibility = Visibility.Collapsed;
        VectorSynthEditor.Visibility = Visibility.Collapsed;
    }

    private static string GetSynthTypeDisplayName(string synthType)
    {
        return synthType?.ToLowerInvariant() switch
        {
            "simple" or "simplesynth" => "Simple Synthesizer",
            "poly" or "polysynth" => "Polyphonic Synthesizer",
            "fm" or "fmsynth" => "FM Synthesizer",
            "supersaw" or "supersawsynth" => "Supersaw Synthesizer",
            "advanced" or "advancedsynth" => "Advanced Synthesizer",
            "granular" or "granularsynth" => "Granular Synthesizer",
            "sample" or "samplesynth" or "sampler" => "Sample-Based Synthesizer",
            "speech" or "speechsynth" or "vocal" => "Speech Synthesizer",
            "physical" or "physicalmodeling" or "pm" => "Physical Modeling",
            "noise" or "noisegenerator" => "Noise Generator",
            "wavetable" or "wavetablesynth" => "Wavetable Synthesizer",
            "vector" or "vectorsynth" => "Vector Synthesizer",
            _ => synthType ?? "Unknown"
        };
    }

    private void UpdateSynthDisplay()
    {
        if (_currentSynth == null)
        {
            CloseSynth();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SynthSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        if (SynthSelector.SelectedItem is RegisteredSynth selected)
        {
            OpenSynth(selected.Synth, selected.Name, selected.TypeName);
        }
    }
}

/// <summary>
/// Event arguments for synth parameter changes.
/// </summary>
public class SynthParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Name of the parameter that changed.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// New value of the parameter.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public SynthParameterChangedEventArgs(string parameterName, object? newValue)
    {
        ParameterName = parameterName;
        NewValue = newValue;
    }
}
