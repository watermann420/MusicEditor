// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog for managing workspace presets.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.Services;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Converter to map category names to brush colors.
/// </summary>
public class CategoryToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string category)
        {
            return category switch
            {
                "Recording" => new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)),
                "Mixing" => new SolidColorBrush(Color.FromRgb(0x44, 0xAA, 0xFF)),
                "Mastering" => new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                "Editing" => new SolidColorBrush(Color.FromRgb(0x44, 0xFF, 0x88)),
                "Performance" => new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0xFF)),
                _ => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter to show/hide shortcut display based on whether shortcut exists.
/// </summary>
public class ShortcutToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Dialog for managing workspace presets with preview and quick actions.
/// </summary>
public partial class WorkspacePresetDialog : Window
{
    private readonly WorkspacePresetService _presetService;
    private WorkspacePresetData? _selectedPreset;
    private WorkspacePresetData? _contextMenuPreset;
    private readonly Func<WorkspaceLayoutCapture>? _captureLayoutFunc;

    /// <summary>
    /// Gets the preset that was selected for loading, if any.
    /// </summary>
    public WorkspacePresetData? SelectedPreset => _selectedPreset;

    /// <summary>
    /// Gets whether a preset was applied.
    /// </summary>
    public bool PresetApplied { get; private set; }

    /// <summary>
    /// Creates a new workspace preset dialog.
    /// </summary>
    /// <param name="presetService">The preset service to use.</param>
    /// <param name="captureLayoutFunc">Function to capture current layout.</param>
    public WorkspacePresetDialog(WorkspacePresetService presetService,
        Func<WorkspaceLayoutCapture>? captureLayoutFunc = null)
    {
        InitializeComponent();
        _presetService = presetService;
        _captureLayoutFunc = captureLayoutFunc;

        RefreshLists();
        UpdateDetailsPanel();
    }

    private void RefreshLists()
    {
        BuiltInPresetsList.ItemsSource = null;
        BuiltInPresetsList.ItemsSource = _presetService.BuiltInPresets;

        UserPresetsList.ItemsSource = null;
        UserPresetsList.ItemsSource = _presetService.UserPresets;
    }

    private void UpdateDetailsPanel()
    {
        if (_selectedPreset == null)
        {
            DetailNameText.Text = "No preset selected";
            DetailDescriptionText.Text = "Select a preset to view details";
            DetailCategoryText.Text = "--";
            DetailShortcutText.Text = "--";
            DetailCreatedText.Text = "--";
            DetailModifiedText.Text = "--";
            NoPreviewText.Visibility = Visibility.Visible;
            PreviewImage.Visibility = Visibility.Collapsed;
            ApplyButton.IsEnabled = false;
            VisiblePanelsWrap.Children.Clear();
            return;
        }

        DetailNameText.Text = _selectedPreset.Name;
        DetailDescriptionText.Text = _selectedPreset.Description;
        DetailCategoryText.Text = _selectedPreset.Category;
        DetailShortcutText.Text = _selectedPreset.Shortcut ?? "None";
        DetailCreatedText.Text = _selectedPreset.Created.ToString("yyyy-MM-dd HH:mm");
        DetailModifiedText.Text = _selectedPreset.LastModified.ToString("yyyy-MM-dd HH:mm");
        ApplyButton.IsEnabled = true;

        // Update preview
        if (!string.IsNullOrEmpty(_selectedPreset.ThumbnailBase64))
        {
            try
            {
                var bytes = System.Convert.FromBase64String(_selectedPreset.ThumbnailBase64);
                using var stream = new System.IO.MemoryStream(bytes);
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
                NoPreviewText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                PreviewImage.Visibility = Visibility.Collapsed;
                NoPreviewText.Visibility = Visibility.Visible;
            }
        }
        else
        {
            PreviewImage.Visibility = Visibility.Collapsed;
            NoPreviewText.Visibility = Visibility.Visible;
        }

        // Update visible panels list
        UpdateVisiblePanelsList();
    }

    private void UpdateVisiblePanelsList()
    {
        VisiblePanelsWrap.Children.Clear();

        if (_selectedPreset == null) return;

        var panels = _selectedPreset.Panels;
        var panelNames = new (string Name, bool Visible)[]
        {
            ("Project Explorer", panels.ProjectExplorerVisible),
            ("Output", panels.OutputVisible),
            ("Mixer", panels.MixerVisible),
            ("Arrangement", panels.ArrangementVisible),
            ("Piano Roll", panels.PianoRollVisible),
            ("Transport", panels.TransportVisible),
            ("Input Monitor", panels.InputMonitorVisible),
            ("Spectrum", panels.SpectrumAnalyzerVisible),
            ("Loudness", panels.LoudnessMeterVisible),
            ("Goniometer", panels.GoniometerVisible),
            ("VST Browser", panels.VstBrowserVisible),
            ("Session View", panels.SessionViewVisible),
            ("DJ Effects", panels.DJEffectsVisible)
        };

        foreach (var (name, visible) in panelNames)
        {
            if (visible)
            {
                var tag = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 4, 4)
                };

                var text = new TextBlock
                {
                    Text = name,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0))
                };

                tag.Child = text;
                VisiblePanelsWrap.Children.Add(tag);
            }
        }
    }

    private void PresetCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is WorkspacePresetData preset)
        {
            _selectedPreset = preset;
            UpdateDetailsPanel();
        }
    }

    private void PresetCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Double-click to apply
        if (e.ClickCount == 2 && _selectedPreset != null)
        {
            ApplyPreset();
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyPreset();
    }

    private void ApplyPreset()
    {
        if (_selectedPreset == null) return;

        _presetService.LoadPreset(_selectedPreset);
        PresetApplied = true;
        DialogResult = true;
        Close();
    }

    private void NewPresetButton_Click(object sender, RoutedEventArgs e)
    {
        var presetName = InputDialog.Show("Enter a name for the new preset:", "New Preset", "My Preset", this);

        if (!string.IsNullOrWhiteSpace(presetName))
        {
            var preset = _presetService.CreatePreset(presetName);

            // Optionally capture current layout
            if (_captureLayoutFunc != null)
            {
                var capture = _captureLayoutFunc();
                _ = _presetService.SaveCurrentLayoutAsync(preset, capture);
            }

            RefreshLists();
            _selectedPreset = preset;
            UpdateDetailsPanel();
        }
    }

    private async void SaveCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_captureLayoutFunc == null)
        {
            MessageBox.Show("Cannot capture current layout.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var presetName = InputDialog.Show("Enter a name for this preset:", "Save Current Layout", "My Layout", this);

        if (!string.IsNullOrWhiteSpace(presetName))
        {
            var preset = _presetService.CreatePreset(presetName);
            var capture = _captureLayoutFunc();

            await _presetService.SaveCurrentLayoutAsync(preset, capture);

            RefreshLists();
            _selectedPreset = preset;
            UpdateDetailsPanel();

            MessageBox.Show($"Layout saved as \"{preset.Name}\".", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Workspace Preset",
            Filter = "Workspace Preset (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var preset = await _presetService.ImportPresetAsync(dialog.FileName);
                RefreshLists();
                _selectedPreset = preset;
                UpdateDetailsPanel();

                MessageBox.Show($"Preset \"{preset.Name}\" imported successfully.",
                    "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import preset: {ex.Message}",
                    "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null)
        {
            MessageBox.Show("Please select a preset to export.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Workspace Preset",
            Filter = "Workspace Preset (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = $"{_selectedPreset.Name}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _presetService.ExportPresetAsync(_selectedPreset, dialog.FileName);
                MessageBox.Show($"Preset exported to \"{dialog.FileName}\".",
                    "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export preset: {ex.Message}",
                    "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void PresetMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is WorkspacePresetData preset)
        {
            _contextMenuPreset = preset;
            PresetContextMenu.IsOpen = true;
        }
    }

    private async void RenamePreset_Click(object sender, RoutedEventArgs e)
    {
        PresetContextMenu.IsOpen = false;

        if (_contextMenuPreset == null || _contextMenuPreset.IsBuiltIn) return;

        var newName = InputDialog.Show("Enter new name:", "Rename Preset", _contextMenuPreset.Name, this);

        if (!string.IsNullOrWhiteSpace(newName))
        {
            await _presetService.RenamePresetAsync(_contextMenuPreset, newName);
            RefreshLists();
            UpdateDetailsPanel();
        }
    }

    private void DuplicatePreset_Click(object sender, RoutedEventArgs e)
    {
        PresetContextMenu.IsOpen = false;

        if (_contextMenuPreset == null) return;

        var duplicate = _presetService.DuplicatePreset(_contextMenuPreset);
        RefreshLists();
        _selectedPreset = duplicate;
        UpdateDetailsPanel();
    }

    private async void UpdatePreset_Click(object sender, RoutedEventArgs e)
    {
        PresetContextMenu.IsOpen = false;

        if (_contextMenuPreset == null || _contextMenuPreset.IsBuiltIn) return;

        if (_captureLayoutFunc == null)
        {
            MessageBox.Show("Cannot capture current layout.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Update \"{_contextMenuPreset.Name}\" with the current window layout?",
            "Update Preset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var capture = _captureLayoutFunc();
            await _presetService.SaveCurrentLayoutAsync(_contextMenuPreset, capture);
            UpdateDetailsPanel();

            MessageBox.Show("Preset updated with current layout.", "Updated",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        PresetContextMenu.IsOpen = false;

        if (_contextMenuPreset == null || _contextMenuPreset.IsBuiltIn) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete \"{_contextMenuPreset.Name}\"?",
            "Delete Preset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await _presetService.DeletePresetAsync(_contextMenuPreset);

            if (_selectedPreset == _contextMenuPreset)
            {
                _selectedPreset = null;
            }

            RefreshLists();
            UpdateDetailsPanel();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
