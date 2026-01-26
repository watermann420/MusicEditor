// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Add new script dialog.

using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Views.Dialogs;

public enum ScriptTemplate
{
    Empty,
    SynthPattern,
    DrumPattern,
    MidiController
}

public partial class AddScriptDialog : Window
{
    public string ScriptName
    {
        get => ScriptNameBox.Text;
        set => ScriptNameBox.Text = value;
    }

    public ScriptTemplate SelectedTemplate
    {
        get
        {
            var content = (TemplateCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            return content switch
            {
                "Synth Pattern" => ScriptTemplate.SynthPattern,
                "Drum Pattern" => ScriptTemplate.DrumPattern,
                "MIDI Controller" => ScriptTemplate.MidiController,
                _ => ScriptTemplate.Empty
            };
        }
        set
        {
            var targetContent = value switch
            {
                ScriptTemplate.SynthPattern => "Synth Pattern",
                ScriptTemplate.DrumPattern => "Drum Pattern",
                ScriptTemplate.MidiController => "MIDI Controller",
                _ => "Empty"
            };

            foreach (ComboBoxItem item in TemplateCombo.Items)
            {
                if (item.Content?.ToString() == targetContent)
                {
                    TemplateCombo.SelectedItem = item;
                    break;
                }
            }
        }
    }

    public AddScriptDialog()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            ScriptNameBox.Focus();
            ScriptNameBox.SelectAll();
        };
    }

    public static AddScriptResult? Show(
        string defaultName = "NewScript",
        ScriptTemplate defaultTemplate = ScriptTemplate.Empty,
        Window? owner = null)
    {
        var dialog = new AddScriptDialog
        {
            ScriptName = defaultName,
            SelectedTemplate = defaultTemplate,
            Owner = owner
        };

        if (dialog.ShowDialog() == true)
        {
            return new AddScriptResult
            {
                ScriptName = dialog.ScriptName,
                Template = dialog.SelectedTemplate
            };
        }

        return null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ScriptName))
        {
            MessageBox.Show("Please enter a script name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validate script name (should be valid identifier)
        if (!IsValidIdentifier(ScriptName))
        {
            MessageBox.Show("Script name must be a valid identifier (letters, digits, and underscores only, cannot start with a digit).",
                "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        return true;
    }
}

public class AddScriptResult
{
    public string ScriptName { get; set; } = string.Empty;
    public ScriptTemplate Template { get; set; } = ScriptTemplate.Empty;
}
