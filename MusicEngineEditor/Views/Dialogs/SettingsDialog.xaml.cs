using System;
using System.Windows;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Settings dialog for application preferences
/// </summary>
public partial class SettingsDialog : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>
    /// Creates a new settings dialog
    /// </summary>
    /// <param name="settingsService">The settings service to use</param>
    public SettingsDialog(ISettingsService settingsService)
    {
        InitializeComponent();

        _viewModel = new SettingsViewModel(settingsService);
        _viewModel.ApplyRequested += OnApplyRequested;
        _viewModel.CancelRequested += OnCancelRequested;
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void OnApplyRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ApplyRequested -= OnApplyRequested;
        _viewModel.CancelRequested -= OnCancelRequested;
        base.OnClosed(e);
    }
}
