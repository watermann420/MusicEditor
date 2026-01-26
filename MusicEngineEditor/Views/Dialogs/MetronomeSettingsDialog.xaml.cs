// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Metronome settings dialog.

using System;
using System.Windows;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Metronome settings dialog for configuring click track options.
/// </summary>
public partial class MetronomeSettingsDialog : Window
{
    private readonly MetronomeViewModel _viewModel;

    /// <summary>
    /// Creates a new metronome settings dialog.
    /// </summary>
    /// <param name="metronomeService">The metronome service to configure.</param>
    public MetronomeSettingsDialog(MetronomeService metronomeService)
    {
        InitializeComponent();

        _viewModel = new MetronomeViewModel(metronomeService);
        _viewModel.ApplyRequested += OnApplyRequested;
        _viewModel.CancelRequested += OnCancelRequested;
        DataContext = _viewModel;
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
