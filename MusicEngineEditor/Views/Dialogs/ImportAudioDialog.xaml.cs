using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

public enum AudioCategory
{
    Drums,
    Bass,
    Leads,
    FX,
    Other
}

public partial class ImportAudioDialog : Window
{
    public string FilePath
    {
        get => FilePathBox.Text;
        set => FilePathBox.Text = value;
    }

    public string SampleName
    {
        get => SampleNameBox.Text;
        set => SampleNameBox.Text = value;
    }

    public AudioCategory SelectedCategory
    {
        get
        {
            var content = (CategoryCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            return content switch
            {
                "Bass" => AudioCategory.Bass,
                "Leads" => AudioCategory.Leads,
                "FX" => AudioCategory.FX,
                "Other" => AudioCategory.Other,
                _ => AudioCategory.Drums
            };
        }
        set
        {
            var targetContent = value switch
            {
                AudioCategory.Bass => "Bass",
                AudioCategory.Leads => "Leads",
                AudioCategory.FX => "FX",
                AudioCategory.Other => "Other",
                _ => "Drums"
            };

            foreach (ComboBoxItem item in CategoryCombo.Items)
            {
                if (item.Content?.ToString() == targetContent)
                {
                    CategoryCombo.SelectedItem = item;
                    break;
                }
            }
        }
    }

    public ImportAudioDialog()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            FilePathBox.Focus();
        };
    }

    public static ImportAudioResult? Show(
        string defaultPath = "",
        string defaultName = "",
        AudioCategory defaultCategory = AudioCategory.Drums,
        Window? owner = null)
    {
        var dialog = new ImportAudioDialog
        {
            FilePath = defaultPath,
            SampleName = defaultName,
            SelectedCategory = defaultCategory,
            Owner = owner
        };

        if (dialog.ShowDialog() == true)
        {
            return new ImportAudioResult
            {
                FilePath = dialog.FilePath,
                SampleName = dialog.SampleName,
                Category = dialog.SelectedCategory
            };
        }

        return null;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Audio File",
            Filter = "Audio Files|*.wav;*.mp3;*.ogg;*.flac;*.aiff|WAV Files|*.wav|MP3 Files|*.mp3|OGG Files|*.ogg|FLAC Files|*.flac|All Files|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;

            // Auto-fill sample name from filename if empty
            if (string.IsNullOrWhiteSpace(SampleName))
            {
                SampleName = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            MessageBox.Show("Please select an audio file.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(FilePath))
        {
            MessageBox.Show("The specified file does not exist.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SampleName))
        {
            MessageBox.Show("Please enter a sample name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}

public class ImportAudioResult
{
    public string FilePath { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
    public AudioCategory Category { get; set; } = AudioCategory.Drums;
}
