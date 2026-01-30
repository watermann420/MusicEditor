// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Send/return routing control.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for managing sends from a mixer channel to aux buses.
/// Displays send levels and pre/post fader options.
/// </summary>
public partial class SendControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty SendsProperty =
        DependencyProperty.Register(nameof(Sends), typeof(ObservableCollection<Send>),
            typeof(SendControl), new PropertyMetadata(null));

    public static readonly DependencyProperty AvailableBusesProperty =
        DependencyProperty.Register(nameof(AvailableBuses), typeof(ObservableCollection<BusChannel>),
            typeof(SendControl), new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the collection of sends.
    /// </summary>
    public ObservableCollection<Send> Sends
    {
        get => (ObservableCollection<Send>)GetValue(SendsProperty);
        set => SetValue(SendsProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of available buses to send to.
    /// </summary>
    public ObservableCollection<BusChannel> AvailableBuses
    {
        get => (ObservableCollection<BusChannel>)GetValue(AvailableBusesProperty);
        set => SetValue(AvailableBusesProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the user requests to add a new send.
    /// </summary>
    public event EventHandler? AddSendRequested;

    /// <summary>
    /// Raised when a send is removed.
    /// </summary>
    public event EventHandler<SendEventArgs>? SendRemoved;

    /// <summary>
    /// Raised when a send level changes.
    /// </summary>
    public event EventHandler<SendLevelChangedEventArgs>? SendLevelChanged;

    #endregion

    #region Constructor

    public SendControl()
    {
        InitializeComponent();

        // Initialize with empty collection if not bound
        if (Sends == null)
        {
            Sends = [];
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a send to the specified bus.
    /// </summary>
    /// <param name="targetBus">The target bus.</param>
    /// <param name="level">The initial send level.</param>
    public void AddSend(BusChannel targetBus, float level = 0.5f)
    {
        ArgumentNullException.ThrowIfNull(targetBus);

        // Check if send to this bus already exists
        foreach (var existingSend in Sends)
        {
            if (existingSend.TargetBusId == targetBus.Id)
            {
                return; // Already exists
            }
        }

        var send = new Send(targetBus.Id, targetBus.Name, level)
        {
            Color = targetBus.Color
        };

        Sends.Add(send);
    }

    /// <summary>
    /// Removes the send to the specified bus.
    /// </summary>
    /// <param name="targetBusId">The target bus ID.</param>
    public void RemoveSend(string targetBusId)
    {
        Send? toRemove = null;
        foreach (var send in Sends)
        {
            if (send.TargetBusId == targetBusId)
            {
                toRemove = send;
                break;
            }
        }

        if (toRemove != null)
        {
            Sends.Remove(toRemove);
            SendRemoved?.Invoke(this, new SendEventArgs(toRemove));
        }
    }

    /// <summary>
    /// Sets the level of a send.
    /// </summary>
    /// <param name="targetBusId">The target bus ID.</param>
    /// <param name="level">The new level.</param>
    public void SetSendLevel(string targetBusId, float level)
    {
        foreach (var send in Sends)
        {
            if (send.TargetBusId == targetBusId)
            {
                float oldLevel = send.Level;
                send.Level = Math.Clamp(level, 0f, 1f);
                SendLevelChanged?.Invoke(this, new SendLevelChangedEventArgs(send, oldLevel, send.Level));
                break;
            }
        }
    }

    /// <summary>
    /// Clears all sends.
    /// </summary>
    public void ClearSends()
    {
        Sends.Clear();
    }

    #endregion

    #region Event Handlers

    private void AddSend_Click(object sender, RoutedEventArgs e)
    {
        AddSendRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}

#region Event Args

/// <summary>
/// Event arguments for send events.
/// </summary>
public class SendEventArgs : EventArgs
{
    /// <summary>
    /// Gets the send.
    /// </summary>
    public Send Send { get; }

    /// <summary>
    /// Creates new send event arguments.
    /// </summary>
    /// <param name="send">The send.</param>
    public SendEventArgs(Send send)
    {
        Send = send;
    }
}

/// <summary>
/// Event arguments for send level changes.
/// </summary>
public class SendLevelChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the send.
    /// </summary>
    public Send Send { get; }

    /// <summary>
    /// Gets the old level.
    /// </summary>
    public float OldLevel { get; }

    /// <summary>
    /// Gets the new level.
    /// </summary>
    public float NewLevel { get; }

    /// <summary>
    /// Creates new send level changed event arguments.
    /// </summary>
    /// <param name="send">The send.</param>
    /// <param name="oldLevel">The old level.</param>
    /// <param name="newLevel">The new level.</param>
    public SendLevelChangedEventArgs(Send send, float oldLevel, float newLevel)
    {
        Send = send;
        OldLevel = oldLevel;
        NewLevel = newLevel;
    }
}

#endregion

#region Converters

/// <summary>
/// Converts a value (0-1) to a rotation angle for a knob.
/// </summary>
public class KnobAngleConverter : IValueConverter
{
    public static KnobAngleConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            // Map 0-1 to -135 to +135 degrees
            return (doubleValue * 270) - 135;
        }
        return -135;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a percentage value (0-1) to a width.
/// </summary>
public class PercentageWidthConverter : IValueConverter
{
    public static PercentageWidthConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            // Return percentage as-is, actual width binding would use MultiBinding
            return doubleValue * 60; // Approximate width
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public static InverseBoolConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

#endregion
