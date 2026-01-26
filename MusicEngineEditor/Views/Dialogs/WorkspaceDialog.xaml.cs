// MusicEngineEditor - Workspace Dialog
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MusicEngineEditor.Services;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for managing workspace layouts.
/// </summary>
public partial class WorkspaceDialog : Window
{
    private readonly WorkspaceService _workspaceService;
    private Workspace? _selectedWorkspace;
    private bool _isUpdating;

    /// <summary>
    /// Creates a new workspace dialog.
    /// </summary>
    /// <param name="workspaceService">The workspace service to use.</param>
    public WorkspaceDialog(WorkspaceService workspaceService)
    {
        InitializeComponent();
        _workspaceService = workspaceService;
        RefreshList();
    }

    /// <summary>
    /// Gets the workspace that was loaded, if any.
    /// </summary>
    public Workspace? LoadedWorkspace { get; private set; }

    private void RefreshList()
    {
        WorkspaceList.ItemsSource = null;
        WorkspaceList.ItemsSource = _workspaceService.Workspaces;

        if (_selectedWorkspace != null)
        {
            WorkspaceList.SelectedItem = _workspaceService.Workspaces
                .FirstOrDefault(w => w.Id == _selectedWorkspace.Id);
        }
    }

    private void UpdateDetails()
    {
        _isUpdating = true;

        if (_selectedWorkspace == null)
        {
            NameTextBox.Text = string.Empty;
            DescriptionTextBox.Text = string.Empty;
            ActiveViewCombo.SelectedIndex = 0;
            ShortcutTextBox.Text = string.Empty;
            CreatedText.Text = "--";
            ModifiedText.Text = "--";
            WindowCountText.Text = "--";

            NameTextBox.IsEnabled = false;
            DescriptionTextBox.IsEnabled = false;
            ActiveViewCombo.IsEnabled = false;
            ShortcutTextBox.IsEnabled = false;
            SaveCurrentButton.IsEnabled = false;
            LoadButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
            DuplicateButton.IsEnabled = false;
        }
        else
        {
            NameTextBox.Text = _selectedWorkspace.Name;
            DescriptionTextBox.Text = _selectedWorkspace.Description;
            ShortcutTextBox.Text = _selectedWorkspace.Shortcut ?? string.Empty;
            CreatedText.Text = _selectedWorkspace.Created.ToString("yyyy-MM-dd HH:mm");
            ModifiedText.Text = _selectedWorkspace.LastModified.ToString("yyyy-MM-dd HH:mm");
            WindowCountText.Text = _selectedWorkspace.Windows.Count.ToString();

            // Set active view
            ActiveViewCombo.SelectedIndex = _selectedWorkspace.ActiveView switch
            {
                "Arrangement" => 0,
                "Mixer" => 1,
                "PianoRoll" => 2,
                "AudioEditor" => 3,
                _ => 0
            };

            // Enable/disable based on built-in status
            bool isEditable = !_selectedWorkspace.IsBuiltIn;
            NameTextBox.IsEnabled = isEditable;
            DescriptionTextBox.IsEnabled = isEditable;
            ActiveViewCombo.IsEnabled = isEditable;
            ShortcutTextBox.IsEnabled = isEditable;
            SaveCurrentButton.IsEnabled = isEditable;
            DeleteButton.IsEnabled = isEditable;
            LoadButton.IsEnabled = true;
            ExportButton.IsEnabled = true;
            DuplicateButton.IsEnabled = true;
        }

        _isUpdating = false;
    }

    private void WorkspaceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedWorkspace = WorkspaceList.SelectedItem as Workspace;
        UpdateDetails();
    }

    private void WorkspaceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_selectedWorkspace != null)
        {
            LoadWorkspace();
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        var workspace = _workspaceService.CreateWorkspace("New Workspace");
        RefreshList();
        WorkspaceList.SelectedItem = workspace;
    }

    private void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace != null)
        {
            var duplicate = _workspaceService.DuplicateWorkspace(_selectedWorkspace);
            RefreshList();
            WorkspaceList.SelectedItem = duplicate;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null || _selectedWorkspace.IsBuiltIn) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the workspace \"{_selectedWorkspace.Name}\"?",
            "Delete Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _workspaceService.DeleteWorkspace(_selectedWorkspace);
            _selectedWorkspace = null;
            RefreshList();
            UpdateDetails();
        }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Workspace",
            Filter = "Workspace Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var workspace = await _workspaceService.ImportWorkspaceAsync(dialog.FileName);
                RefreshList();
                WorkspaceList.SelectedItem = workspace;
                MessageBox.Show($"Workspace \"{workspace.Name}\" imported successfully.",
                    "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import workspace: {ex.Message}",
                    "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Workspace",
            Filter = "Workspace Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = $"{_selectedWorkspace.Name}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _workspaceService.ExportWorkspaceAsync(_selectedWorkspace, dialog.FileName);
                MessageBox.Show($"Workspace exported to \"{dialog.FileName}\".",
                    "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export workspace: {ex.Message}",
                    "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _selectedWorkspace == null || _selectedWorkspace.IsBuiltIn) return;
        _workspaceService.RenameWorkspace(_selectedWorkspace, NameTextBox.Text);
        RefreshList();
    }

    private void DescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _selectedWorkspace == null || _selectedWorkspace.IsBuiltIn) return;
        _selectedWorkspace.Description = DescriptionTextBox.Text;
        _selectedWorkspace.LastModified = DateTime.Now;
    }

    private void ActiveViewCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _selectedWorkspace == null || _selectedWorkspace.IsBuiltIn) return;

        _selectedWorkspace.ActiveView = ActiveViewCombo.SelectedIndex switch
        {
            0 => "Arrangement",
            1 => "Mixer",
            2 => "PianoRoll",
            3 => "AudioEditor",
            _ => "Arrangement"
        };
        _selectedWorkspace.LastModified = DateTime.Now;
    }

    private void ShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_selectedWorkspace == null || _selectedWorkspace.IsBuiltIn) return;

        e.Handled = true;

        var modifiers = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers.Add("Shift");

        // Only accept if there's a modifier and a key
        if (modifiers.Count > 0 && e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl &&
            e.Key != Key.LeftAlt && e.Key != Key.RightAlt &&
            e.Key != Key.LeftShift && e.Key != Key.RightShift &&
            e.Key != Key.System)
        {
            var keyStr = e.Key.ToString();
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                keyStr = (e.Key - Key.D0).ToString();
            }

            _selectedWorkspace.Shortcut = string.Join("+", modifiers) + "+" + keyStr;
            ShortcutTextBox.Text = _selectedWorkspace.Shortcut;
            _selectedWorkspace.LastModified = DateTime.Now;
        }
        else if (e.Key == Key.Escape || e.Key == Key.Delete || e.Key == Key.Back)
        {
            _selectedWorkspace.Shortcut = null;
            ShortcutTextBox.Text = string.Empty;
            _selectedWorkspace.LastModified = DateTime.Now;
        }
    }

    private async void SaveCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null || _selectedWorkspace.IsBuiltIn) return;

        try
        {
            // In a real implementation, this would capture the current window states
            // from the main window and save them to the workspace
            await _workspaceService.SaveCurrentWorkspaceAsync();
            UpdateDetails();
            MessageBox.Show("Current layout saved to workspace.",
                "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save workspace: {ex.Message}",
                "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        LoadWorkspace();
    }

    private void LoadWorkspace()
    {
        if (_selectedWorkspace == null) return;

        LoadedWorkspace = _selectedWorkspace;
        _workspaceService.LoadWorkspace(_selectedWorkspace);
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
