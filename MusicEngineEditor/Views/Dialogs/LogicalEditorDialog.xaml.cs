// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for complex MIDI filtering and transformation using logical conditions.
/// </summary>
public partial class LogicalEditorDialog : Window
{
    #region Properties

    /// <summary>
    /// Gets the conditions for filtering.
    /// </summary>
    public ObservableCollection<LogicalCondition> Conditions { get; } = [];

    /// <summary>
    /// Gets the actions to perform on matching notes.
    /// </summary>
    public ObservableCollection<LogicalAction> Actions { get; } = [];

    /// <summary>
    /// Gets or sets the source notes to filter.
    /// </summary>
    public List<PianoRollNote>? SourceNotes { get; set; }

    /// <summary>
    /// Gets the notes that matched the filter.
    /// </summary>
    public List<PianoRollNote> MatchedNotes { get; private set; } = [];

    /// <summary>
    /// Gets the available presets.
    /// </summary>
    public ObservableCollection<LogicalEditorPreset> Presets { get; } = [];

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the selection should be updated.
    /// </summary>
    public event EventHandler<List<PianoRollNote>>? SelectionRequested;

    /// <summary>
    /// Occurs when notes have been transformed.
    /// </summary>
    public event EventHandler<List<PianoRollNote>>? NotesTransformed;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new LogicalEditorDialog.
    /// </summary>
    public LogicalEditorDialog()
    {
        InitializeComponent();

        ConditionsListBox.ItemsSource = Conditions;
        ActionsListBox.ItemsSource = Actions;

        ConditionOperatorCombo.SelectionChanged += ConditionOperator_SelectionChanged;

        LoadBuiltInPresets();
        PresetComboBox.ItemsSource = Presets;
        PresetComboBox.DisplayMemberPath = "Name";
    }

    #endregion

    #region Preset Management

    private void LoadBuiltInPresets()
    {
        Presets.Add(new LogicalEditorPreset
        {
            Name = "Select High Notes",
            Conditions = [new LogicalCondition { Property = "Note Number", Operator = ">=", Value1 = "72" }],
            Actions = [new LogicalAction { Type = "Select", Target = "Note Number" }]
        });

        Presets.Add(new LogicalEditorPreset
        {
            Name = "Select Low Velocity",
            Conditions = [new LogicalCondition { Property = "Velocity", Operator = "<", Value1 = "64" }],
            Actions = [new LogicalAction { Type = "Select", Target = "Velocity" }]
        });

        Presets.Add(new LogicalEditorPreset
        {
            Name = "Boost Velocity +20",
            Conditions = [],
            Actions = [new LogicalAction { Type = "Add", Target = "Velocity", Value = "20" }]
        });

        Presets.Add(new LogicalEditorPreset
        {
            Name = "Delete Short Notes",
            Conditions = [new LogicalCondition { Property = "Length", Operator = "<", Value1 = "0.1" }],
            Actions = [new LogicalAction { Type = "Delete", Target = "Note Number" }]
        });

        Presets.Add(new LogicalEditorPreset
        {
            Name = "Humanize Velocity",
            Conditions = [],
            Actions = [new LogicalAction { Type = "Random", Target = "Velocity", Value = "10" }]
        });
    }

    private void Preset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is LogicalEditorPreset preset)
        {
            Conditions.Clear();
            Actions.Clear();

            foreach (var condition in preset.Conditions)
            {
                Conditions.Add(condition.Clone());
            }

            foreach (var action in preset.Actions)
            {
                Actions.Add(action.Clone());
            }

            RefreshPreview();
        }
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var presetName = InputDialog.Show("Enter preset name:", "Save Preset");
        if (!string.IsNullOrWhiteSpace(presetName))
        {
            var preset = new LogicalEditorPreset
            {
                Name = presetName,
                Conditions = Conditions.Select(c => c.Clone()).ToList(),
                Actions = Actions.Select(a => a.Clone()).ToList()
            };

            // Remove existing preset with same name
            var existing = Presets.FirstOrDefault(p => p.Name == preset.Name);
            if (existing != null)
            {
                Presets.Remove(existing);
            }

            Presets.Add(preset);
            PresetComboBox.SelectedItem = preset;
        }
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is LogicalEditorPreset preset)
        {
            Presets.Remove(preset);
            PresetComboBox.SelectedIndex = -1;
        }
    }

    #endregion

    #region Condition Management

    private void ConditionOperator_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedOp = (ConditionOperatorCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var isRange = selectedOp == "Inside" || selectedOp == "Outside";

        RangeToLabel.Visibility = isRange ? Visibility.Visible : Visibility.Collapsed;
        ConditionValue2.Visibility = isRange ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddCondition_Click(object sender, RoutedEventArgs e)
    {
        var property = (ConditionPropertyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Note Number";
        var op = (ConditionOperatorCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "=";
        var value1 = ConditionValue1.Text;
        var value2 = ConditionValue2.Text;

        var condition = new LogicalCondition
        {
            Property = property,
            Operator = op,
            Value1 = value1,
            Value2 = (op == "Inside" || op == "Outside") ? value2 : null
        };

        Conditions.Add(condition);
        RefreshPreview();
    }

    private void RemoveCondition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LogicalCondition condition)
        {
            Conditions.Remove(condition);
            RefreshPreview();
        }
    }

    #endregion

    #region Action Management

    private void AddAction_Click(object sender, RoutedEventArgs e)
    {
        var type = (ActionTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Select";
        var target = (ActionTargetCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Velocity";
        var value = ActionValue.Text;

        var action = new LogicalAction
        {
            Type = type,
            Target = target,
            Value = value
        };

        Actions.Add(action);
    }

    private void RemoveAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LogicalAction action)
        {
            Actions.Remove(action);
        }
    }

    #endregion

    #region Filtering and Execution

    private void RefreshPreview_Click(object sender, RoutedEventArgs e)
    {
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (SourceNotes == null || SourceNotes.Count == 0)
        {
            MatchCountText.Text = "No source notes available";
            return;
        }

        MatchedNotes = FilterNotes(SourceNotes);

        var count = MatchedNotes.Count;
        var total = SourceNotes.Count;
        MatchCountText.Text = $"{count} of {total} notes match the filter conditions";
    }

    private List<PianoRollNote> FilterNotes(List<PianoRollNote> notes)
    {
        var invert = InvertSelectionCheck.IsChecked == true;

        var result = notes.Where(note =>
        {
            var matches = Conditions.Count == 0 || Conditions.All(c => EvaluateCondition(note, c));
            return invert ? !matches : matches;
        }).ToList();

        return result;
    }

    private static bool EvaluateCondition(PianoRollNote note, LogicalCondition condition)
    {
        var value = GetPropertyValue(note, condition.Property);

        if (!double.TryParse(condition.Value1, out var conditionValue1))
            return false;

        double.TryParse(condition.Value2, out var conditionValue2);

        return condition.Operator switch
        {
            "=" => Math.Abs(value - conditionValue1) < 0.001,
            "!=" => Math.Abs(value - conditionValue1) >= 0.001,
            "<" => value < conditionValue1,
            "<=" => value <= conditionValue1,
            ">" => value > conditionValue1,
            ">=" => value >= conditionValue1,
            "Inside" => value >= conditionValue1 && value <= conditionValue2,
            "Outside" => value < conditionValue1 || value > conditionValue2,
            _ => false
        };
    }

    private static double GetPropertyValue(PianoRollNote note, string property)
    {
        return property switch
        {
            "Note Number" => note.Note,
            "Velocity" => note.Velocity,
            "Channel" => note.Channel,
            "Position" => note.StartBeat,
            "Length" => note.Duration,
            _ => 0
        };
    }

    private List<PianoRollNote> ApplyActions(List<PianoRollNote> notes)
    {
        var random = new Random();
        var notesToDelete = new List<PianoRollNote>();

        foreach (var note in notes)
        {
            foreach (var action in Actions)
            {
                if (!double.TryParse(action.Value, out var actionValue))
                    actionValue = 0;

                switch (action.Type)
                {
                    case "Set Value":
                        SetPropertyValue(note, action.Target, actionValue);
                        break;

                    case "Add":
                        var currentValue = GetPropertyValue(note, action.Target);
                        SetPropertyValue(note, action.Target, currentValue + actionValue);
                        break;

                    case "Multiply":
                        var current = GetPropertyValue(note, action.Target);
                        SetPropertyValue(note, action.Target, current * actionValue);
                        break;

                    case "Random":
                        var range = (int)Math.Abs(actionValue);
                        var currentVal = GetPropertyValue(note, action.Target);
                        var offset = random.Next(-range, range + 1);
                        SetPropertyValue(note, action.Target, currentVal + offset);
                        break;

                    case "Quantize":
                        if (action.Target == "Position")
                        {
                            var grid = actionValue > 0 ? actionValue : 0.25;
                            note.StartBeat = Math.Round(note.StartBeat / grid) * grid;
                        }
                        break;

                    case "Delete":
                        notesToDelete.Add(note);
                        break;
                }
            }
        }

        return notesToDelete;
    }

    private static void SetPropertyValue(PianoRollNote note, string property, double value)
    {
        switch (property)
        {
            case "Note Number":
                note.Note = Math.Clamp((int)value, 0, 127);
                break;
            case "Velocity":
                note.Velocity = Math.Clamp((int)value, 1, 127);
                break;
            case "Channel":
                note.Channel = Math.Clamp((int)value, 0, 15);
                break;
            case "Position":
                note.StartBeat = Math.Max(0, value);
                break;
            case "Length":
                note.Duration = Math.Max(0.01, value);
                break;
        }
    }

    #endregion

    #region Dialog Buttons

    private void SelectOnly_Click(object sender, RoutedEventArgs e)
    {
        if (SourceNotes == null) return;

        MatchedNotes = FilterNotes(SourceNotes);
        SelectionRequested?.Invoke(this, MatchedNotes);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (SourceNotes == null) return;

        MatchedNotes = FilterNotes(SourceNotes);

        // Apply actions to matched notes and get list of notes to delete
        var notesToDelete = ApplyActions(MatchedNotes);

        // Remove deleted notes
        foreach (var note in notesToDelete)
        {
            SourceNotes.Remove(note);
            MatchedNotes.Remove(note);
        }

        if (SelectMatchingCheck.IsChecked == true)
        {
            SelectionRequested?.Invoke(this, MatchedNotes);
        }

        NotesTransformed?.Invoke(this, MatchedNotes);
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Represents a filter condition for the logical editor.
/// </summary>
public class LogicalCondition
{
    /// <summary>
    /// The property to filter on (Note Number, Velocity, Channel, Position, Length).
    /// </summary>
    public string Property { get; set; } = "Note Number";

    /// <summary>
    /// The comparison operator.
    /// </summary>
    public string Operator { get; set; } = "=";

    /// <summary>
    /// The first comparison value.
    /// </summary>
    public string Value1 { get; set; } = "60";

    /// <summary>
    /// The second comparison value (for range operators).
    /// </summary>
    public string? Value2 { get; set; }

    /// <summary>
    /// Gets the display text for this condition.
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (Operator == "Inside" || Operator == "Outside")
            {
                return $"{Property} {Operator} [{Value1} - {Value2}]";
            }
            return $"{Property} {Operator} {Value1}";
        }
    }

    /// <summary>
    /// Creates a clone of this condition.
    /// </summary>
    public LogicalCondition Clone()
    {
        return new LogicalCondition
        {
            Property = Property,
            Operator = Operator,
            Value1 = Value1,
            Value2 = Value2
        };
    }
}

/// <summary>
/// Represents an action for the logical editor.
/// </summary>
public class LogicalAction
{
    /// <summary>
    /// The action type (Select, Delete, Set Value, Add, Multiply, Random, Quantize).
    /// </summary>
    public string Type { get; set; } = "Select";

    /// <summary>
    /// The target property for the action.
    /// </summary>
    public string Target { get; set; } = "Velocity";

    /// <summary>
    /// The value for the action.
    /// </summary>
    public string Value { get; set; } = "0";

    /// <summary>
    /// Gets the display text for this action.
    /// </summary>
    public string DisplayText
    {
        get
        {
            return Type switch
            {
                "Select" => $"Select matching notes",
                "Delete" => $"Delete matching notes",
                "Set Value" => $"Set {Target} to {Value}",
                "Add" => $"Add {Value} to {Target}",
                "Multiply" => $"Multiply {Target} by {Value}",
                "Random" => $"Randomize {Target} +/- {Value}",
                "Quantize" => $"Quantize {Target} to {Value}",
                _ => $"{Type} {Target}"
            };
        }
    }

    /// <summary>
    /// Creates a clone of this action.
    /// </summary>
    public LogicalAction Clone()
    {
        return new LogicalAction
        {
            Type = Type,
            Target = Target,
            Value = Value
        };
    }
}

/// <summary>
/// Represents a saved preset for the logical editor.
/// </summary>
public class LogicalEditorPreset
{
    /// <summary>
    /// The name of the preset.
    /// </summary>
    public string Name { get; set; } = "Untitled";

    /// <summary>
    /// The filter conditions.
    /// </summary>
    public List<LogicalCondition> Conditions { get; set; } = [];

    /// <summary>
    /// The actions to perform.
    /// </summary>
    public List<LogicalAction> Actions { get; set; } = [];
}

#endregion
