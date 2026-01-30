// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Panel for locking incoming MIDI to a selected scale.
/// Quantizes notes to the nearest note in the selected scale.
/// </summary>
public partial class ScaleLockPanel : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Dependency property for the selected scale.
    /// </summary>
    public static readonly DependencyProperty SelectedScaleProperty =
        DependencyProperty.Register(
            nameof(SelectedScale),
            typeof(ScaleDefinition),
            typeof(ScaleLockPanel),
            new PropertyMetadata(ScaleDefinition.Major, OnScaleChanged));

    /// <summary>
    /// Dependency property for the root note.
    /// </summary>
    public static readonly DependencyProperty RootNoteProperty =
        DependencyProperty.Register(
            nameof(RootNote),
            typeof(int),
            typeof(ScaleLockPanel),
            new PropertyMetadata(0, OnRootNoteChanged)); // 0 = C

    /// <summary>
    /// Dependency property for whether scale lock is enabled.
    /// </summary>
    public static readonly DependencyProperty IsLockEnabledProperty =
        DependencyProperty.Register(
            nameof(IsLockEnabled),
            typeof(bool),
            typeof(ScaleLockPanel),
            new PropertyMetadata(false, OnLockEnabledChanged));

    /// <summary>
    /// Gets or sets the selected scale.
    /// </summary>
    public ScaleDefinition SelectedScale
    {
        get => (ScaleDefinition)GetValue(SelectedScaleProperty);
        set => SetValue(SelectedScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the root note (0-11, where 0 = C).
    /// </summary>
    public int RootNote
    {
        get => (int)GetValue(RootNoteProperty);
        set => SetValue(RootNoteProperty, value);
    }

    /// <summary>
    /// Gets or sets whether scale lock is enabled.
    /// </summary>
    public bool IsLockEnabled
    {
        get => (bool)GetValue(IsLockEnabledProperty);
        set => SetValue(IsLockEnabledProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when scale lock settings change.
    /// </summary>
    public event EventHandler<ScaleLockChangedEventArgs>? ScaleLockChanged;

    #endregion

    #region Private Fields

    private bool _isInitializing = true;
    private bool[] _scaleMap = new bool[12];

    #endregion

    /// <summary>
    /// Creates a new ScaleLockPanel.
    /// </summary>
    public ScaleLockPanel()
    {
        InitializeComponent();
        UpdateScaleMap();
        _isInitializing = false;
    }

    #region Property Changed Handlers

    private static void OnScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScaleLockPanel panel)
        {
            panel.UpdateScaleMap();
            panel.RaiseScaleLockChanged();
        }
    }

    private static void OnRootNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScaleLockPanel panel)
        {
            panel.UpdateScaleMap();
            panel.RaiseScaleLockChanged();
        }
    }

    private static void OnLockEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScaleLockPanel panel)
        {
            panel.RaiseScaleLockChanged();
        }
    }

    #endregion

    #region Event Handlers

    private void RootNoteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        RootNote = RootNoteComboBox.SelectedIndex;
    }

    private void ScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var selectedItem = ScaleComboBox.SelectedItem as ComboBoxItem;
        var scaleName = selectedItem?.Content?.ToString() ?? "Major";

        SelectedScale = FindScaleByName(scaleName);
    }

    private void EnableToggle_Click(object sender, RoutedEventArgs e)
    {
        IsLockEnabled = EnableToggle.IsChecked == true;
    }

    #endregion

    #region Private Methods

    private void UpdateScaleMap()
    {
        _scaleMap = SelectedScale.CreateScaleMap(RootNote);
    }

    private void RaiseScaleLockChanged()
    {
        if (_isInitializing) return;

        ScaleLockChanged?.Invoke(this, new ScaleLockChangedEventArgs(
            SelectedScale,
            RootNote,
            IsLockEnabled));
    }

    private static ScaleDefinition FindScaleByName(string name)
    {
        foreach (var scale in ScaleDefinition.AllScales)
        {
            if (scale.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return scale;
            }
        }

        return ScaleDefinition.Major;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Quantizes a MIDI note to the selected scale.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <returns>The quantized MIDI note, or the original if not locked or already in scale.</returns>
    public int QuantizeNote(int midiNote)
    {
        if (!IsLockEnabled)
            return midiNote;

        // Check if already in scale
        int pitchClass = ((midiNote % 12) + 12) % 12;
        if (_scaleMap[pitchClass])
            return midiNote;

        // Find nearest note in scale
        int octave = midiNote / 12;

        // Check notes below and above
        for (int offset = 1; offset <= 6; offset++)
        {
            int below = ((pitchClass - offset) % 12 + 12) % 12;
            int above = (pitchClass + offset) % 12;

            if (_scaleMap[below])
            {
                return octave * 12 + below;
            }
            if (_scaleMap[above])
            {
                return octave * 12 + above;
            }
        }

        return midiNote;
    }

    /// <summary>
    /// Checks if a MIDI note is in the currently selected scale.
    /// </summary>
    /// <param name="midiNote">The MIDI note number.</param>
    /// <returns>True if in scale, false otherwise.</returns>
    public bool IsNoteInScale(int midiNote)
    {
        int pitchClass = ((midiNote % 12) + 12) % 12;
        return _scaleMap[pitchClass];
    }

    /// <summary>
    /// Gets the current scale map (12-element boolean array).
    /// </summary>
    /// <returns>Copy of the scale map.</returns>
    public bool[] GetScaleMap()
    {
        var copy = new bool[12];
        Array.Copy(_scaleMap, copy, 12);
        return copy;
    }

    /// <summary>
    /// Gets the scale name for display.
    /// </summary>
    /// <returns>Formatted scale name with root note.</returns>
    public string GetScaleDisplayName()
    {
        var rootName = ScaleDefinition.GetNoteName(RootNote);
        return $"{rootName} {SelectedScale.Name}";
    }

    /// <summary>
    /// Resets to default settings (C Major, disabled).
    /// </summary>
    public void Reset()
    {
        _isInitializing = true;

        RootNoteComboBox.SelectedIndex = 0;
        ScaleComboBox.SelectedIndex = 0;
        EnableToggle.IsChecked = false;

        RootNote = 0;
        SelectedScale = ScaleDefinition.Major;
        IsLockEnabled = false;

        _isInitializing = false;
        UpdateScaleMap();
        RaiseScaleLockChanged();
    }

    #endregion
}

/// <summary>
/// Event arguments for scale lock changes.
/// </summary>
public class ScaleLockChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the selected scale.
    /// </summary>
    public ScaleDefinition Scale { get; }

    /// <summary>
    /// Gets the root note (0-11).
    /// </summary>
    public int RootNote { get; }

    /// <summary>
    /// Gets whether scale lock is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets the root note name.
    /// </summary>
    public string RootNoteName => ScaleDefinition.GetNoteName(RootNote);

    /// <summary>
    /// Gets the full scale name (e.g., "C Major").
    /// </summary>
    public string FullScaleName => $"{RootNoteName} {Scale.Name}";

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public ScaleLockChangedEventArgs(ScaleDefinition scale, int rootNote, bool isEnabled)
    {
        Scale = scale;
        RootNote = rootNote;
        IsEnabled = isEnabled;
    }
}
