// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.Controls;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Keyboard shortcuts configuration dialog
/// </summary>
public partial class ShortcutsDialog : Window
{
    private readonly ShortcutsViewModel _viewModel;

    /// <summary>
    /// Creates a new shortcuts dialog
    /// </summary>
    /// <param name="shortcutService">The shortcut service to use</param>
    public ShortcutsDialog(IShortcutService shortcutService)
    {
        InitializeComponent();

        _viewModel = new ShortcutsViewModel(shortcutService);
        _viewModel.ApplyRequested += OnApplyRequested;
        _viewModel.CancelRequested += OnCancelRequested;
        DataContext = _viewModel;

        // Handle selection in the shortcuts list
        AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnShortcutItemClicked), true);
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

    private void OnShortcutItemClicked(object sender, MouseButtonEventArgs e)
    {
        // Find if we clicked on a shortcut item
        if (e.OriginalSource is FrameworkElement element)
        {
            var item = FindParentDataContext<ShortcutItemViewModel>(element);
            if (item != null)
            {
                // Deselect previous
                if (_viewModel.SelectedShortcut != null)
                {
                    _viewModel.SelectedShortcut.IsSelected = false;
                }

                // Select new
                item.IsSelected = true;
                _viewModel.SelectedShortcut = item;
            }
        }
    }

    private static T? FindParentDataContext<T>(DependencyObject element) where T : class
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is T target)
            {
                return target;
            }
            if (current is FrameworkContentElement fce && fce.DataContext is T contentTarget)
            {
                return contentTarget;
            }
            current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void ShortcutRecorder_ShortcutRecorded(object? sender, ShortcutRecordedEventArgs e)
    {
        if (_viewModel.SelectedShortcut == null)
            return;

        // Try to update the shortcut
        _viewModel.UpdateShortcut(e.Key, e.Modifiers);
    }

    private void ShortcutRecorder_ShortcutCleared(object? sender, EventArgs e)
    {
        _viewModel.ClearShortcutCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ApplyRequested -= OnApplyRequested;
        _viewModel.CancelRequested -= OnCancelRequested;
        base.OnClosed(e);
    }
}

/// <summary>
/// Converts null values to Visibility with optional inversion
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value == null;

        // Check for "Invert" parameter
        if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            isNull = !isNull;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
