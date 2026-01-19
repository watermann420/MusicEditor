using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Views.Dialogs;

public partial class ProjectSettingsDialog : Window
{
    public string ProjectName
    {
        get => ProjectNameBox.Text;
        set => ProjectNameBox.Text = value;
    }

    public string DefaultNamespace
    {
        get => NamespaceBox.Text;
        set => NamespaceBox.Text = value;
    }

    public string OutputPath
    {
        get => OutputPathBox.Text;
        set => OutputPathBox.Text = value;
    }

    public string TargetFramework
    {
        get => (TargetFrameworkCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "net8.0";
        set
        {
            foreach (ComboBoxItem item in TargetFrameworkCombo.Items)
            {
                if (item.Content?.ToString() == value)
                {
                    TargetFrameworkCombo.SelectedItem = item;
                    break;
                }
            }
        }
    }

    public ProjectSettingsDialog()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            ProjectNameBox.Focus();
            ProjectNameBox.SelectAll();
        };
    }

    public static ProjectSettingsResult? Show(
        string projectName = "",
        string defaultNamespace = "",
        string outputPath = "",
        string targetFramework = "net8.0",
        Window? owner = null)
    {
        var dialog = new ProjectSettingsDialog
        {
            ProjectName = projectName,
            DefaultNamespace = defaultNamespace,
            OutputPath = outputPath,
            TargetFramework = targetFramework,
            Owner = owner
        };

        if (dialog.ShowDialog() == true)
        {
            return new ProjectSettingsResult
            {
                ProjectName = dialog.ProjectName,
                DefaultNamespace = dialog.DefaultNamespace,
                OutputPath = dialog.OutputPath,
                TargetFramework = dialog.TargetFramework
            };
        }

        return null;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select output path",
            SelectedPath = OutputPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputPath = dialog.SelectedPath;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show("Please enter a project name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}

public class ProjectSettingsResult
{
    public string ProjectName { get; set; } = string.Empty;
    public string DefaultNamespace { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = "net8.0";
}
