// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: New project creation dialog.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

public partial class NewProjectDialog : Window, INotifyPropertyChanged
{
    private string _projectName = "MyMusicProject";
    private string _projectLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _namespace = "MyMusicProject";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (_projectName != value)
            {
                _projectName = value;
                OnPropertyChanged();
            }
        }
    }

    public string ProjectLocation
    {
        get => _projectLocation;
        set
        {
            if (_projectLocation != value)
            {
                _projectLocation = value;
                OnPropertyChanged();
            }
        }
    }

    public string Namespace
    {
        get => _namespace;
        set
        {
            if (_namespace != value)
            {
                _namespace = value;
                OnPropertyChanged();
            }
        }
    }

    public NewProjectDialog()
    {
        InitializeComponent();
        DataContext = this;

        // Ensure controls exist before accessing them
        Loaded += (s, e) =>
        {
            ProjectNameBox?.Focus();
            ProjectNameBox?.SelectAll();
        };
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select project location",
            SelectedPath = ProjectLocation ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ProjectLocation = dialog.SelectedPath;
        }
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show("Please enter a project name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectLocation) || !Directory.Exists(ProjectLocation))
        {
            MessageBox.Show("Please select a valid location.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Update namespace if still default
        if (string.IsNullOrWhiteSpace(Namespace) || Namespace == "MyMusicProject")
        {
            Namespace = SanitizeNamespace(ProjectName);
        }

        DialogResult = true;
        Close();
    }

    private static string SanitizeNamespace(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "MusicProject";

        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
        }
        return result.Length > 0 ? result.ToString() : "MusicProject";
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
