// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Visual editor for articulation maps (keyswitches, expression maps).
/// </summary>
public partial class ArticulationMapEditor : UserControl
{
    #region Properties

    /// <summary>
    /// Gets the current articulation map.
    /// </summary>
    public ArticulationMap CurrentMap { get; private set; } = new();

    /// <summary>
    /// Gets or sets the currently selected articulation.
    /// </summary>
    public Articulation? SelectedArticulation { get; private set; }

    /// <summary>
    /// Whether MIDI learn mode is active.
    /// </summary>
    public bool IsLearning { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when an articulation is triggered.
    /// </summary>
    public event EventHandler<ArticulationEventArgs>? ArticulationTriggered;

    /// <summary>
    /// Occurs when the map is modified.
    /// </summary>
    public event EventHandler? MapModified;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new ArticulationMapEditor.
    /// </summary>
    public ArticulationMapEditor()
    {
        InitializeComponent();

        // Initialize note combo
        InitializeNoteComboBox();

        // Initialize channel combo
        for (int i = 1; i <= 16; i++)
        {
            ChannelComboBox.Items.Add($"Channel {i}");
        }
        ChannelComboBox.SelectedIndex = 0;

        // Initialize type combo
        TypeComboBox.SelectedIndex = 0;

        // Bind the list
        ArticulationList.ItemsSource = CurrentMap.Articulations;
        UpdateStatusBar();
    }

    #endregion

    #region Initialization

    private void InitializeNoteComboBox()
    {
        string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

        for (int note = 0; note <= 127; note++)
        {
            var noteName = noteNames[note % 12];
            var octave = (note / 12) - 2;
            KeyswitchNoteCombo.Items.Add($"{noteName}{octave} ({note})");
        }

        KeyswitchNoteCombo.SelectedIndex = 24; // C0
    }

    #endregion

    #region Map Management

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentMap.Articulations.Count > 0)
        {
            var result = MessageBox.Show(
                "Create a new map? Unsaved changes will be lost.",
                "New Map",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        CurrentMap = new ArticulationMap();
        ArticulationList.ItemsSource = CurrentMap.Articulations;
        MapNameText.Text = " - New Map";
        SelectedArticulation = null;
        UpdateEditor();
        UpdateStatusBar();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Articulation Map (*.artmap)|*.artmap|Expression Map (*.expressionmap)|*.expressionmap|JSON (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Articulation Map"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var map = JsonSerializer.Deserialize<ArticulationMap>(json);

                if (map != null)
                {
                    CurrentMap = map;
                    ArticulationList.ItemsSource = CurrentMap.Articulations;
                    MapNameText.Text = $" - {Path.GetFileNameWithoutExtension(dialog.FileName)}";
                    UpdateStatusBar();
                    StatusText.Text = "Map imported successfully";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing map: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Articulation Map (*.artmap)|*.artmap|JSON (*.json)|*.json",
            Title = "Export Articulation Map",
            FileName = CurrentMap.Name
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(CurrentMap, options);
                File.WriteAllText(dialog.FileName, json);
                StatusText.Text = "Map exported successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting map: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Articulation List Management

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLowerInvariant();

        if (string.IsNullOrEmpty(searchText))
        {
            ArticulationList.ItemsSource = CurrentMap.Articulations;
        }
        else
        {
            var filtered = CurrentMap.Articulations
                .Where(a => a.Name.ToLowerInvariant().Contains(searchText))
                .ToList();
            ArticulationList.ItemsSource = filtered;
        }
    }

    private void ArticulationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedArticulation = ArticulationList.SelectedItem as Articulation;
        UpdateEditor();
    }

    private void AddArticulation_Click(object sender, RoutedEventArgs e)
    {
        var articulation = new Articulation
        {
            Name = $"Articulation {CurrentMap.Articulations.Count + 1}",
            KeyswitchNote = 24 + CurrentMap.Articulations.Count
        };

        CurrentMap.Articulations.Add(articulation);
        ArticulationList.SelectedItem = articulation;
        UpdateStatusBar();
        MapModified?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveArticulation_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedArticulation != null)
        {
            var index = CurrentMap.Articulations.IndexOf(SelectedArticulation);
            CurrentMap.Articulations.Remove(SelectedArticulation);

            if (CurrentMap.Articulations.Count > 0)
            {
                ArticulationList.SelectedIndex = Math.Min(index, CurrentMap.Articulations.Count - 1);
            }
            else
            {
                SelectedArticulation = null;
                UpdateEditor();
            }

            UpdateStatusBar();
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DuplicateArticulation_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedArticulation != null)
        {
            var duplicate = SelectedArticulation.Clone();
            duplicate.Name += " (Copy)";
            duplicate.KeyswitchNote = Math.Min(127, SelectedArticulation.KeyswitchNote + 1);

            CurrentMap.Articulations.Add(duplicate);
            ArticulationList.SelectedItem = duplicate;
            UpdateStatusBar();
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion

    #region Editor Updates

    private void UpdateEditor()
    {
        if (SelectedArticulation == null)
        {
            EditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        EditorPanel.Visibility = Visibility.Visible;

        // Update fields
        NameTextBox.Text = SelectedArticulation.Name;
        TypeComboBox.SelectedIndex = (int)SelectedArticulation.Type;
        KeyswitchNoteCombo.SelectedIndex = SelectedArticulation.KeyswitchNote;
        CCNumberTextBox.Text = SelectedArticulation.CCNumber.ToString();
        CCValueTextBox.Text = SelectedArticulation.CCValue.ToString();
        ChannelComboBox.SelectedIndex = SelectedArticulation.Channel;
        SymbolTextBox.Text = SelectedArticulation.Symbol;
        ShowInLaneCheck.IsChecked = SelectedArticulation.ShowInLane;
        OutputKeyswitchCheck.IsChecked = SelectedArticulation.OutputKeyswitch;
        SustainCheck.IsChecked = SelectedArticulation.Sustain;
        VelocityTextBox.Text = SelectedArticulation.Velocity.ToString();

        // Update color preview
        try
        {
            ColorPreview.Background = new SolidColorBrush(
                (Color)System.Windows.Media.ColorConverter.ConvertFromString(SelectedArticulation.Color));
        }
        catch
        {
            ColorPreview.Background = new SolidColorBrush(Colors.DodgerBlue);
        }

        // Update panel visibility based on type
        UpdateTypePanels();
    }

    private void UpdateTypePanels()
    {
        if (SelectedArticulation == null) return;

        var isCC = SelectedArticulation.Type == ArticulationTriggerType.CCSwitch;
        KeyswitchPanel.Visibility = isCC ? Visibility.Collapsed : Visibility.Visible;
        CCPanel.Visibility = isCC ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStatusBar()
    {
        ArticulationCountText.Text = $"{CurrentMap.Articulations.Count} articulation(s)";
    }

    #endregion

    #region Editor Event Handlers

    private void Name_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SelectedArticulation != null && NameTextBox.Text != SelectedArticulation.Name)
        {
            SelectedArticulation.Name = NameTextBox.Text;
            ArticulationList.Items.Refresh();
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedArticulation != null && TypeComboBox.SelectedIndex >= 0)
        {
            SelectedArticulation.Type = (ArticulationTriggerType)TypeComboBox.SelectedIndex;
            UpdateTypePanels();
            ArticulationList.Items.Refresh();
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void KeyswitchNote_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedArticulation != null && KeyswitchNoteCombo.SelectedIndex >= 0)
        {
            SelectedArticulation.KeyswitchNote = KeyswitchNoteCombo.SelectedIndex;
            ArticulationList.Items.Refresh();
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Learn_Click(object sender, RoutedEventArgs e)
    {
        IsLearning = !IsLearning;
        LearnButton.Content = IsLearning ? "Cancel" : "Learn";
        StatusText.Text = IsLearning ? "Press a key to assign keyswitch..." : "Ready";
    }

    /// <summary>
    /// Processes a MIDI note for learn mode.
    /// </summary>
    /// <param name="noteNumber">The MIDI note number.</param>
    public void ProcessLearnNote(int noteNumber)
    {
        if (IsLearning && SelectedArticulation != null)
        {
            SelectedArticulation.KeyswitchNote = noteNumber;
            KeyswitchNoteCombo.SelectedIndex = noteNumber;
            IsLearning = false;
            LearnButton.Content = "Learn";
            StatusText.Text = $"Assigned keyswitch: {noteNumber}";
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CCNumber_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SelectedArticulation != null && int.TryParse(CCNumberTextBox.Text, out var cc))
        {
            SelectedArticulation.CCNumber = Math.Clamp(cc, 0, 127);
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CCValue_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SelectedArticulation != null && int.TryParse(CCValueTextBox.Text, out var value))
        {
            SelectedArticulation.CCValue = Math.Clamp(value, 0, 127);
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Channel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedArticulation != null && ChannelComboBox.SelectedIndex >= 0)
        {
            SelectedArticulation.Channel = ChannelComboBox.SelectedIndex;
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Symbol_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SelectedArticulation != null)
        {
            SelectedArticulation.Symbol = SymbolTextBox.Text;
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShowInLane_Changed(object sender, RoutedEventArgs e)
    {
        if (SelectedArticulation != null)
        {
            SelectedArticulation.ShowInLane = ShowInLaneCheck.IsChecked == true;
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OutputKeyswitch_Changed(object sender, RoutedEventArgs e)
    {
        if (SelectedArticulation != null)
        {
            SelectedArticulation.OutputKeyswitch = OutputKeyswitchCheck.IsChecked == true;
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Sustain_Changed(object sender, RoutedEventArgs e)
    {
        if (SelectedArticulation != null)
        {
            SelectedArticulation.Sustain = SustainCheck.IsChecked == true;
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Velocity_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SelectedArticulation != null && int.TryParse(VelocityTextBox.Text, out var velocity))
        {
            SelectedArticulation.Velocity = Math.Clamp(velocity, 1, 127);
            MapModified?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ChangeColor_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedArticulation == null) return;

        // Simple color picker using predefined colors
        var colors = new[]
        {
            "#4B6EAF", "#8B5CF6", "#EC4899", "#F59E0B",
            "#10B981", "#EF4444", "#6366F1", "#14B8A6"
        };

        var currentIndex = Array.IndexOf(colors, SelectedArticulation.Color);
        var nextIndex = (currentIndex + 1) % colors.Length;

        SelectedArticulation.Color = colors[nextIndex];
        ColorPreview.Background = new SolidColorBrush(
            (Color)System.Windows.Media.ColorConverter.ConvertFromString(SelectedArticulation.Color));
        ArticulationList.Items.Refresh();
        MapModified?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Loads an articulation map.
    /// </summary>
    /// <param name="map">The map to load.</param>
    public void LoadMap(ArticulationMap map)
    {
        CurrentMap = map;
        ArticulationList.ItemsSource = CurrentMap.Articulations;
        MapNameText.Text = $" - {map.Name}";
        UpdateStatusBar();
    }

    /// <summary>
    /// Gets the articulation for a given keyswitch note.
    /// </summary>
    /// <param name="noteNumber">The MIDI note number.</param>
    /// <returns>The matching articulation or null.</returns>
    public Articulation? GetArticulationForNote(int noteNumber)
    {
        return CurrentMap.Articulations.FirstOrDefault(a =>
            a.Type == ArticulationTriggerType.Keyswitch &&
            a.KeyswitchNote == noteNumber);
    }

    /// <summary>
    /// Triggers an articulation.
    /// </summary>
    /// <param name="articulation">The articulation to trigger.</param>
    public void TriggerArticulation(Articulation articulation)
    {
        ArticulationTriggered?.Invoke(this, new ArticulationEventArgs(articulation));
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Represents an articulation map containing multiple articulations.
/// </summary>
public class ArticulationMap
{
    /// <summary>
    /// The name of the map.
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// The articulations in this map.
    /// </summary>
    public ObservableCollection<Articulation> Articulations { get; set; } = [];
}

/// <summary>
/// Represents a single articulation (keyswitch, expression, etc.).
/// </summary>
public partial class Articulation : ObservableObject
{
    [ObservableProperty]
    private string _name = "Articulation";

    [ObservableProperty]
    private ArticulationTriggerType _type = ArticulationTriggerType.Keyswitch;

    [ObservableProperty]
    private int _keyswitchNote = 24;

    [ObservableProperty]
    private int _cCNumber = 32;

    [ObservableProperty]
    private int _cCValue = 127;

    [ObservableProperty]
    private int _channel;

    [ObservableProperty]
    private string _color = "#4B6EAF";

    [ObservableProperty]
    private string _symbol = "";

    [ObservableProperty]
    private bool _showInLane = true;

    [ObservableProperty]
    private bool _outputKeyswitch = true;

    [ObservableProperty]
    private bool _sustain;

    [ObservableProperty]
    private int _velocity = 100;

    /// <summary>
    /// Gets the color brush for display.
    /// </summary>
    public Brush ColorBrush
    {
        get
        {
            try
            {
                return new SolidColorBrush(
                    (Color)System.Windows.Media.ColorConverter.ConvertFromString(Color));
            }
            catch
            {
                return Brushes.DodgerBlue;
            }
        }
    }

    /// <summary>
    /// Gets the type brush for the badge.
    /// </summary>
    public Brush TypeBrush => Type switch
    {
        ArticulationTriggerType.Keyswitch => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#8B5CF6")),
        ArticulationTriggerType.CCSwitch => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B")),
        ArticulationTriggerType.ProgramChange => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981")),
        ArticulationTriggerType.Expression => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#EC4899")),
        _ => Brushes.Gray
    };

    /// <summary>
    /// Gets the short type name.
    /// </summary>
    public string TypeShort => Type switch
    {
        ArticulationTriggerType.Keyswitch => "KS",
        ArticulationTriggerType.CCSwitch => "CC",
        ArticulationTriggerType.ProgramChange => "PC",
        ArticulationTriggerType.Expression => "EX",
        _ => "?"
    };

    /// <summary>
    /// Gets the keyswitch display text.
    /// </summary>
    public string KeyswitchDisplay
    {
        get
        {
            if (Type == ArticulationTriggerType.CCSwitch)
            {
                return $"CC{CCNumber} = {CCValue}";
            }

            string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
            var noteName = noteNames[KeyswitchNote % 12];
            var octave = (KeyswitchNote / 12) - 2;
            return $"{noteName}{octave}";
        }
    }

    /// <summary>
    /// Creates a clone of this articulation.
    /// </summary>
    public Articulation Clone()
    {
        return new Articulation
        {
            Name = Name,
            Type = Type,
            KeyswitchNote = KeyswitchNote,
            CCNumber = CCNumber,
            CCValue = CCValue,
            Channel = Channel,
            Color = Color,
            Symbol = Symbol,
            ShowInLane = ShowInLane,
            OutputKeyswitch = OutputKeyswitch,
            Sustain = Sustain,
            Velocity = Velocity
        };
    }
}

/// <summary>
/// Types of articulation triggers.
/// </summary>
public enum ArticulationTriggerType
{
    Keyswitch,
    CCSwitch,
    ProgramChange,
    Expression
}

/// <summary>
/// Event arguments for articulation events.
/// </summary>
public sealed class ArticulationEventArgs : EventArgs
{
    /// <summary>
    /// The articulation that was triggered.
    /// </summary>
    public Articulation Articulation { get; }

    public ArticulationEventArgs(Articulation articulation)
    {
        Articulation = articulation;
    }
}

#endregion
