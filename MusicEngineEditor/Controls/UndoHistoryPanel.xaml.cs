// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation for Undo History Panel with visual timeline.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Panel that displays the undo/redo history as a visual timeline with the ability to jump to any state.
/// Features:
/// - Timeline visualization with nodes for each action
/// - Action type icons (edit, add, delete, move, parameter change)
/// - Current position indicator with highlight
/// - Branch visualization for redo forks
/// - Compact and Clear history controls
/// </summary>
public partial class UndoHistoryPanel : UserControl, IDisposable
{
    private readonly UndoHistoryViewModel _viewModel;
    private bool _disposed;

    /// <summary>
    /// Event raised when the panel requests to be closed.
    /// </summary>
#pragma warning disable CS0067 // Event is never used - available for future external consumers
    public event EventHandler? CloseRequested;
#pragma warning restore CS0067

    /// <summary>
    /// Creates a new UndoHistoryPanel.
    /// </summary>
    public UndoHistoryPanel()
    {
        InitializeComponent();

        _viewModel = new UndoHistoryViewModel();
        DataContext = _viewModel;
    }

    /// <summary>
    /// Gets the ViewModel for external access.
    /// </summary>
    public UndoHistoryViewModel ViewModel => _viewModel;

    /// <summary>
    /// Handles double-click on a history item to jump to that state.
    /// </summary>
    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedItem != null)
        {
            _viewModel.JumpToSelectedState();
        }
    }

    /// <summary>
    /// Refreshes the history display.
    /// </summary>
    public void Refresh()
    {
        _viewModel.RefreshHistory();
    }

    /// <summary>
    /// Disposes the panel and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _viewModel.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Converter that converts a non-null/non-empty string to Visible.
/// </summary>
public class UndoHistoryStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that converts an int greater than 0 to true.
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal)
        {
            return intVal > 0;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that converts a boolean (IsCurrentPosition) to a border color.
/// Returns cyan (#00D9FF) for current position, transparent otherwise.
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCurrentPosition && isCurrentPosition)
        {
            return Color.FromRgb(0x00, 0xD9, 0xFF); // Cyan accent
        }
        return Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that converts an action type to an appropriate icon character.
/// Icons:
/// - Pencil for edit
/// - Plus for add
/// - Trash for delete
/// - Arrows for move
/// - Slider for parameter change
/// </summary>
public class ActionTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is UndoActionType actionType)
        {
            return actionType switch
            {
                UndoActionType.Edit => "\u270E",      // Pencil
                UndoActionType.Add => "+",            // Plus
                UndoActionType.Delete => "\u2212",    // Minus (trash concept)
                UndoActionType.Move => "\u2194",      // Left-right arrows
                UndoActionType.Parameter => "\u2261", // Three lines (slider)
                UndoActionType.Note => "\u266B",      // Music note
                UndoActionType.Mixer => "M",          // Mixer
                UndoActionType.Effect => "fx",        // Effects
                UndoActionType.Automation => "\u223F",// Sine wave
                UndoActionType.Arrangement => "\u25A6",// Grid
                _ => "\u2022"                          // Bullet
            };
        }
        return "\u2022"; // Default bullet
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that converts an action type to its associated color.
/// Colors:
/// - Edit: Cyan (#00D9FF)
/// - Add: Green (#00FF88)
/// - Delete: Red (#FF4757)
/// - Move: Purple (#9C7CE8)
/// - Parameter: Orange (#E8A73C)
/// </summary>
public class ActionTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isBackground = parameter as string == "background";

        if (value is UndoActionType actionType)
        {
            var color = actionType switch
            {
                UndoActionType.Edit => Color.FromRgb(0x00, 0xD9, 0xFF),      // Cyan
                UndoActionType.Add => Color.FromRgb(0x00, 0xFF, 0x88),       // Green
                UndoActionType.Delete => Color.FromRgb(0xFF, 0x47, 0x57),    // Red
                UndoActionType.Move => Color.FromRgb(0x9C, 0x7C, 0xE8),      // Purple
                UndoActionType.Parameter => Color.FromRgb(0xE8, 0xA7, 0x3C), // Orange
                UndoActionType.Note => Color.FromRgb(0x4A, 0x9E, 0xFF),      // Blue
                UndoActionType.Mixer => Color.FromRgb(0x00, 0xCC, 0x66),     // Green
                UndoActionType.Effect => Color.FromRgb(0xE8, 0x5C, 0xAF),    // Pink
                UndoActionType.Automation => Color.FromRgb(0xFF, 0xB8, 0x00),// Yellow
                UndoActionType.Arrangement => Color.FromRgb(0x5C, 0xBF, 0xE8),// Light blue
                _ => Color.FromRgb(0x4A, 0x4A, 0x4A)                          // Gray
            };

            if (isBackground)
            {
                // Return a semi-transparent version for backgrounds
                color.A = 0x40;
            }

            // Return Color for SolidColorBrush binding or Brush for direct binding
            if (targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
            {
                return new SolidColorBrush(color);
            }
            return color;
        }

        var defaultColor = Color.FromRgb(0x4A, 0x4A, 0x4A);
        if (isBackground)
        {
            defaultColor.A = 0x40;
        }

        if (targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
        {
            return new SolidColorBrush(defaultColor);
        }
        return defaultColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Note: InverseBooleanToVisibilityConverter is defined in EffectChainControl.xaml.cs

