// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Event arguments for macro selection events.
/// </summary>
public class MacroSelectedEventArgs : EventArgs
{
    /// <summary>Gets the selected macro control.</summary>
    public MacroControl MacroControl { get; }

    /// <summary>Gets the macro index.</summary>
    public int Index { get; }

    /// <summary>
    /// Creates new macro selected event arguments.
    /// </summary>
    /// <param name="macroControl">The selected macro.</param>
    /// <param name="index">The macro index.</param>
    public MacroSelectedEventArgs(MacroControl macroControl, int index)
    {
        MacroControl = macroControl;
        Index = index;
    }
}

/// <summary>
/// A control that displays a bank of 8 macro knobs.
/// Provides MIDI Learn, randomization, and mapping management.
/// </summary>
public partial class MacroBankControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty MacroBankProperty =
        DependencyProperty.Register(nameof(MacroBank), typeof(MacroBank), typeof(MacroBankControl),
            new PropertyMetadata(null, OnMacroBankChanged));

    public static readonly DependencyProperty IsMidiLearnActiveProperty =
        DependencyProperty.Register(nameof(IsMidiLearnActive), typeof(bool), typeof(MacroBankControl),
            new PropertyMetadata(false, OnIsMidiLearnActiveChanged));

    /// <summary>
    /// Gets or sets the macro bank.
    /// </summary>
    public MacroBank? MacroBank
    {
        get => (MacroBank?)GetValue(MacroBankProperty);
        set => SetValue(MacroBankProperty, value);
    }

    /// <summary>
    /// Gets or sets whether MIDI learn mode is active.
    /// </summary>
    public bool IsMidiLearnActive
    {
        get => (bool)GetValue(IsMidiLearnActiveProperty);
        set => SetValue(IsMidiLearnActiveProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when a macro is selected for editing.
    /// </summary>
    public event EventHandler<MacroSelectedEventArgs>? MacroSelected;

    /// <summary>
    /// Fired when the user requests to add a mapping to a macro.
    /// </summary>
    public event EventHandler<MacroSelectedEventArgs>? AddMappingRequested;

    /// <summary>
    /// Fired when the user requests to edit mappings for a macro.
    /// </summary>
    public event EventHandler<MacroSelectedEventArgs>? EditMappingsRequested;

    /// <summary>
    /// Fired when all macros are reset.
    /// </summary>
    public event EventHandler? AllMacrosReset;

    /// <summary>
    /// Fired when macros are randomized.
    /// </summary>
    public event EventHandler? MacrosRandomized;

    #endregion

    #region Private Fields

    private readonly List<MacroKnobControl> _knobControls = new();
    private int? _learningMacroIndex;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public MacroBankControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        // Create default macro bank if none is set
        if (MacroBank == null)
        {
            MacroBank = new MacroBank(8);
        }

        BuildKnobControls();
        UpdateMappingInfo();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    #endregion

    #region Property Changed Handlers

    private static void OnMacroBankChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MacroBankControl control && control._isInitialized)
        {
            control.BuildKnobControls();
            control.UpdateMappingInfo();
        }
    }

    private static void OnIsMidiLearnActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MacroBankControl control)
        {
            control.MidiLearnToggle.IsChecked = (bool)e.NewValue;

            if (!(bool)e.NewValue)
            {
                // Clear learning state from all knobs
                foreach (var knob in control._knobControls)
                {
                    knob.IsLearning = false;
                }
                control._learningMacroIndex = null;
            }
        }
    }

    #endregion

    #region Build UI

    private void BuildKnobControls()
    {
        MacroKnobsContainer.Items.Clear();
        _knobControls.Clear();

        if (MacroBank == null) return;

        var macros = MacroBank.GetMacros();

        for (int i = 0; i < macros.Count; i++)
        {
            var macro = macros[i];
            var knobControl = new MacroKnobControl
            {
                MacroControl = macro,
                Margin = new Thickness(4)
            };

            // Set color
            if (ColorConverter.ConvertFromString(macro.Color) is Color color)
            {
                knobControl.MacroColor = color;
            }

            // Wire up events
            int index = i;
            knobControl.AddMappingRequested += (s, e) => OnKnobAddMappingRequested(index);
            knobControl.EditMappingsRequested += (s, e) => OnKnobEditMappingsRequested(index);
            knobControl.MidiLearnRequested += (s, e) => OnKnobMidiLearnRequested(index);
            knobControl.ValueChanged += (s, e) => UpdateMappingInfo();

            _knobControls.Add(knobControl);
            MacroKnobsContainer.Items.Add(knobControl);
        }
    }

    private void UpdateMappingInfo()
    {
        if (MacroBank == null)
        {
            MappingInfoText.Text = "";
            return;
        }

        int totalMappings = 0;
        int activeMacros = 0;

        foreach (var macro in MacroBank.GetMacros())
        {
            int mappingCount = macro.MappingCount;
            totalMappings += mappingCount;
            if (mappingCount > 0)
            {
                activeMacros++;
            }
        }

        if (totalMappings > 0)
        {
            MappingInfoText.Text = $"{totalMappings} mapping(s) on {activeMacros} macro(s)";
        }
        else
        {
            MappingInfoText.Text = "No mappings";
        }
    }

    #endregion

    #region Event Handlers

    private void OnKnobAddMappingRequested(int index)
    {
        if (MacroBank == null || index < 0 || index >= MacroBank.Count) return;

        AddMappingRequested?.Invoke(this, new MacroSelectedEventArgs(MacroBank[index], index));
    }

    private void OnKnobEditMappingsRequested(int index)
    {
        if (MacroBank == null || index < 0 || index >= MacroBank.Count) return;

        EditMappingsRequested?.Invoke(this, new MacroSelectedEventArgs(MacroBank[index], index));
    }

    private void OnKnobMidiLearnRequested(int index)
    {
        if (MacroBank == null || index < 0 || index >= MacroBank.Count) return;

        // If already learning this macro, stop
        if (_learningMacroIndex == index)
        {
            _knobControls[index].IsLearning = false;
            _learningMacroIndex = null;
            IsMidiLearnActive = false;
            return;
        }

        // Clear previous learning state
        if (_learningMacroIndex.HasValue && _learningMacroIndex.Value < _knobControls.Count)
        {
            _knobControls[_learningMacroIndex.Value].IsLearning = false;
        }

        // Set new learning state
        _learningMacroIndex = index;
        _knobControls[index].IsLearning = true;
        IsMidiLearnActive = true;
    }

    private void Randomize_Click(object sender, RoutedEventArgs e)
    {
        MacroBank?.Randomize(1.0f);

        // Update all knob controls
        foreach (var knob in _knobControls)
        {
            knob.Refresh();
        }

        MacrosRandomized?.Invoke(this, EventArgs.Empty);
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        MacroBank?.ResetAll();

        // Update all knob controls
        foreach (var knob in _knobControls)
        {
            knob.Refresh();
        }

        AllMacrosReset?.Invoke(this, EventArgs.Empty);
    }

    private void MidiLearn_Checked(object sender, RoutedEventArgs e)
    {
        IsMidiLearnActive = true;
    }

    private void MidiLearn_Unchecked(object sender, RoutedEventArgs e)
    {
        IsMidiLearnActive = false;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Processes a MIDI CC message for all macros.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="cc">CC number (0-127).</param>
    /// <param name="value">CC value (0-127).</param>
    /// <returns>True if any macro processed the message.</returns>
    public bool ProcessMidiCC(int channel, int cc, int value)
    {
        if (MacroBank == null) return false;

        bool processed = MacroBank.ProcessMidiCC(channel, cc, value);

        // If in learning mode and a macro processed it, update the UI
        if (_learningMacroIndex.HasValue && _learningMacroIndex.Value < _knobControls.Count)
        {
            var knob = _knobControls[_learningMacroIndex.Value];
            if (!knob.MacroControl!.IsLearning)
            {
                // Learning completed
                knob.IsLearning = false;
                knob.Refresh();
                _learningMacroIndex = null;
                IsMidiLearnActive = false;
            }
        }

        // Update all knob values from their macro controls
        foreach (var knob in _knobControls)
        {
            if (knob.MacroControl != null)
            {
                knob.Value = knob.MacroControl.Value;
            }
        }

        return processed;
    }

    /// <summary>
    /// Gets the knob control at the specified index.
    /// </summary>
    /// <param name="index">The macro index.</param>
    /// <returns>The knob control, or null if not found.</returns>
    public MacroKnobControl? GetKnobControl(int index)
    {
        if (index >= 0 && index < _knobControls.Count)
        {
            return _knobControls[index];
        }
        return null;
    }

    /// <summary>
    /// Refreshes all knob controls.
    /// </summary>
    public void RefreshAll()
    {
        foreach (var knob in _knobControls)
        {
            knob.Refresh();
        }
        UpdateMappingInfo();
    }

    /// <summary>
    /// Selects a macro for editing.
    /// </summary>
    /// <param name="index">The macro index.</param>
    public void SelectMacro(int index)
    {
        if (MacroBank == null || index < 0 || index >= MacroBank.Count) return;

        MacroSelected?.Invoke(this, new MacroSelectedEventArgs(MacroBank[index], index));
    }

    #endregion
}
