// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: MIDI Machine Control (MMC) and MIDI Time Code (MTC) panel control.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MusicEngineEditor.ViewModels.Network;

namespace MusicEngineEditor.Controls.Network;

/// <summary>
/// Machine Control Panel providing MMC (MIDI Machine Control) and MTC (MIDI Time Code) functionality.
/// </summary>
public partial class MachineControlPanel : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Identifies the IsCompactMode dependency property.
    /// </summary>
    public static readonly DependencyProperty IsCompactModeProperty =
        DependencyProperty.Register(
            nameof(IsCompactMode),
            typeof(bool),
            typeof(MachineControlPanel),
            new PropertyMetadata(false, OnIsCompactModeChanged));

    /// <summary>
    /// Gets or sets whether the panel is in compact mode.
    /// </summary>
    public bool IsCompactMode
    {
        get => (bool)GetValue(IsCompactModeProperty);
        set => SetValue(IsCompactModeProperty, value);
    }

    /// <summary>
    /// Identifies the ShowTransportControls dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowTransportControlsProperty =
        DependencyProperty.Register(
            nameof(ShowTransportControls),
            typeof(bool),
            typeof(MachineControlPanel),
            new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets whether transport controls are visible.
    /// </summary>
    public bool ShowTransportControls
    {
        get => (bool)GetValue(ShowTransportControlsProperty);
        set => SetValue(ShowTransportControlsProperty, value);
    }

    /// <summary>
    /// Identifies the ShowMtcSection dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowMtcSectionProperty =
        DependencyProperty.Register(
            nameof(ShowMtcSection),
            typeof(bool),
            typeof(MachineControlPanel),
            new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets whether the MTC section is visible.
    /// </summary>
    public bool ShowMtcSection
    {
        get => (bool)GetValue(ShowMtcSectionProperty);
        set => SetValue(ShowMtcSectionProperty, value);
    }

    /// <summary>
    /// Identifies the ShowMmcSection dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowMmcSectionProperty =
        DependencyProperty.Register(
            nameof(ShowMmcSection),
            typeof(bool),
            typeof(MachineControlPanel),
            new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets whether the MMC section is visible.
    /// </summary>
    public bool ShowMmcSection
    {
        get => (bool)GetValue(ShowMmcSectionProperty);
        set => SetValue(ShowMmcSectionProperty, value);
    }

    #endregion

    #region Private Fields

    private MachineControlPanelViewModel? _viewModel;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the MachineControlPanel class.
    /// </summary>
    public MachineControlPanel()
    {
        InitializeComponent();

        // Add converters to resources
        Resources.Add("BoolToVisibilityConverter", new BoolToVisibilityConverter());
        Resources.Add("BoolToChaseColorConverter", new BoolToChaseColorConverter());
        Resources.Add("BoolToChaseLabelConverter", new BoolToChaseLabelConverter());

        // Create and bind ViewModel
        _viewModel = new MachineControlPanelViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Binds the panel to a specific ViewModel.
    /// </summary>
    /// <param name="viewModel">The ViewModel to bind to.</param>
    public void BindToViewModel(MachineControlPanelViewModel viewModel)
    {
        if (_viewModel != null)
        {
            _viewModel.Dispose();
        }

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
    }

    /// <summary>
    /// Gets the current ViewModel.
    /// </summary>
    /// <returns>The current MachineControlPanelViewModel.</returns>
    public MachineControlPanelViewModel? GetViewModel() => _viewModel;

    /// <summary>
    /// Processes an incoming MTC message.
    /// </summary>
    /// <param name="hours">Hours component.</param>
    /// <param name="minutes">Minutes component.</param>
    /// <param name="seconds">Seconds component.</param>
    /// <param name="frames">Frames component.</param>
    public void ProcessMtcMessage(int hours, int minutes, int seconds, int frames)
    {
        _viewModel?.OnMtcReceived(hours, minutes, seconds, frames);
    }

    /// <summary>
    /// Processes an incoming MMC command.
    /// </summary>
    /// <param name="command">The MMC command type.</param>
    public void ProcessMmcCommand(MmcCommandType command)
    {
        _viewModel?.OnMmcReceived(command);
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize any runtime state
        _viewModel?.RefreshMidiPortsCommand.Execute(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup
        _viewModel?.Dispose();
    }

    private static void OnIsCompactModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MachineControlPanel panel)
        {
            panel.UpdateCompactMode((bool)e.NewValue);
        }
    }

    #endregion

    #region Private Methods

    private void UpdateCompactMode(bool isCompact)
    {
        // In compact mode, we could hide certain sections or reduce padding
        // This is a placeholder for layout adjustments
        if (isCompact)
        {
            // Compact layout adjustments
        }
        else
        {
            // Full layout
        }
    }

    #endregion
}

#region Value Converters

/// <summary>
/// Converts a boolean value to Visibility.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts a boolean chase lock state to a brush color.
/// </summary>
public class BoolToChaseColorConverter : IValueConverter
{
    private static readonly SolidColorBrush LockedBrush = new(Color.FromRgb(0x00, 0xCC, 0x66));
    private static readonly SolidColorBrush UnlockedBrush = new(Color.FromRgb(0xFF, 0xA5, 0x00));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isLocked)
        {
            return isLocked ? LockedBrush : UnlockedBrush;
        }
        return UnlockedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean chase lock state to a label string.
/// </summary>
public class BoolToChaseLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isLocked)
        {
            return isLocked ? "CHASE LOCKED" : "CHASING...";
        }
        return "CHASING...";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
