// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Track properties editor panel.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A dockable panel for viewing and editing track properties.
/// </summary>
public partial class TrackPropertiesPanel : UserControl
{
    #region Events

    /// <summary>
    /// Occurs when the close button is clicked.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Occurs when a track property has been changed.
    /// </summary>
    public event EventHandler<TrackPropertyChangedEventArgs>? TrackPropertyChanged;

    /// <summary>
    /// Occurs when a track duplication is requested.
    /// </summary>
    public event EventHandler<TrackEventArgs>? TrackDuplicateRequested;

    /// <summary>
    /// Occurs when a track deletion is requested.
    /// </summary>
    public event EventHandler<TrackEventArgs>? TrackDeleteRequested;

    /// <summary>
    /// Occurs when a track freeze/unfreeze is requested.
    /// </summary>
    public event EventHandler<TrackEventArgs>? TrackFreezeRequested;

    #endregion

    #region Private Fields

    private readonly TrackPropertiesViewModel _viewModel;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new TrackPropertiesPanel.
    /// </summary>
    public TrackPropertiesPanel()
    {
        InitializeComponent();

        _viewModel = new TrackPropertiesViewModel();
        DataContext = _viewModel;

        // Wire up ViewModel events to panel events
        _viewModel.TrackPropertyChanged += (s, e) => TrackPropertyChanged?.Invoke(this, e);
        _viewModel.TrackDuplicateRequested += (s, e) => TrackDuplicateRequested?.Invoke(this, e);
        _viewModel.TrackDeleteRequested += (s, e) => TrackDeleteRequested?.Invoke(this, e);
        _viewModel.TrackFreezeRequested += (s, e) => TrackFreezeRequested?.Invoke(this, e);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the view model.
    /// </summary>
    public TrackPropertiesViewModel ViewModel => _viewModel;

    /// <summary>
    /// Gets or sets the currently selected track.
    /// </summary>
    public TrackInfo? SelectedTrack
    {
        get => _viewModel.SelectedTrack;
        set => _viewModel.SelectedTrack = value;
    }

    #endregion

    #region Event Handlers

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TrackNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Move focus away from the text box to apply the change
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Revert to the original name
            if (sender is TextBox textBox)
            {
                textBox.Text = _viewModel.TrackName;
                Keyboard.ClearFocus();
            }
            e.Handled = true;
        }
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string color)
        {
            _viewModel.TrackColor = color;
        }
    }

    private void VolumeSlider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Reset volume to unity (0.8 = approximately 0dB)
        _viewModel.Volume = 0.8f;
    }

    private void PanSlider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Reset pan to center
        _viewModel.Pan = 0f;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Selects a track to display in the panel.
    /// </summary>
    /// <param name="track">The track to select.</param>
    public void SelectTrack(TrackInfo? track)
    {
        _viewModel.SelectedTrack = track;
    }

    /// <summary>
    /// Clears the selected track.
    /// </summary>
    public void ClearSelection()
    {
        _viewModel.SelectedTrack = null;
    }

    /// <summary>
    /// Updates the available output buses in the routing dropdown.
    /// </summary>
    /// <param name="buses">The list of available buses.</param>
    public void UpdateOutputBuses(System.Collections.Generic.IEnumerable<OutputBusOption> buses)
    {
        _viewModel.UpdateOutputBuses(buses);
    }

    /// <summary>
    /// Updates the available input sources in the routing dropdown.
    /// </summary>
    /// <param name="sources">The list of available sources.</param>
    public void UpdateInputSources(System.Collections.Generic.IEnumerable<InputSourceOption> sources)
    {
        _viewModel.UpdateInputSources(sources);
    }

    /// <summary>
    /// Updates the available audio inputs in the dropdown.
    /// </summary>
    /// <param name="inputs">The list of available inputs.</param>
    public void UpdateAudioInputs(System.Collections.Generic.IEnumerable<AudioInputOption> inputs)
    {
        _viewModel.UpdateAudioInputs(inputs);
    }

    /// <summary>
    /// Refreshes the display to reflect any external changes to the track.
    /// </summary>
    public void RefreshDisplay()
    {
        // Force property change notifications
        var track = _viewModel.SelectedTrack;
        _viewModel.SelectedTrack = null;
        _viewModel.SelectedTrack = track;
    }

    #endregion
}
