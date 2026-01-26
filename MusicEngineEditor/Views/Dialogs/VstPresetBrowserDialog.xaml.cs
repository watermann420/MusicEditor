// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngine.Core;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views.Dialogs;

public partial class VstPresetBrowserDialog : Window
{
    private readonly VstPresetBrowserViewModel _viewModel;

    public VstPresetBrowserDialog(IVstPlugin plugin, string pluginName)
    {
        InitializeComponent();

        _viewModel = new VstPresetBrowserViewModel(plugin, pluginName);
        DataContext = _viewModel;

        // Update header with plugin name
        PluginNameHeader.Text = $"{pluginName} Presets";

        // Handle double-click to load preset
        AddHandler(System.Windows.Controls.ListBox.MouseDoubleClickEvent, new MouseButtonEventHandler(OnPresetDoubleClick));
    }

    private void OnPresetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Check if the double-click was on a list item
        if (e.OriginalSource is FrameworkElement element)
        {
            var listBoxItem = FindParent<ListBoxItem>(element);
            if (listBoxItem != null)
            {
                LoadAndClose();
            }
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);

        while (parent != null)
        {
            if (parent is T typedParent)
            {
                return typedParent;
            }
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        LoadAndClose();
    }

    private void LoadAndClose()
    {
        if (_viewModel.SelectedPreset == null)
        {
            MessageBox.Show("Please select a preset to load.",
                "No Preset Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _viewModel.LoadPresetCommand.Execute(null);

        if (_viewModel.PresetLoaded)
        {
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
