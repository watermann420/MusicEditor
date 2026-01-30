// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI theme configuration model.

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for managing and customizing application themes
/// </summary>
public partial class ThemeSettingsDialog : Window
{
    private readonly ThemeSettingsViewModel _viewModel;

    /// <summary>
    /// Creates a new theme settings dialog
    /// </summary>
    /// <param name="themeService">The theme service to use</param>
    public ThemeSettingsDialog(IThemeService themeService)
    {
        InitializeComponent();

        _viewModel = new ThemeSettingsViewModel(themeService);
        _viewModel.OkRequested += OnOkRequested;
        _viewModel.CancelRequested += OnCancelRequested;
        _viewModel.ColorPickerRequested += OnColorPickerRequested;
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize();
    }

    private void OnOkRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnColorPickerRequested(object? sender, ColorPickerEventArgs e)
    {
        // Use Windows Forms ColorDialog for color picking
        using var colorDialog = new System.Windows.Forms.ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                e.CurrentColor.A,
                e.CurrentColor.R,
                e.CurrentColor.G,
                e.CurrentColor.B)
        };

        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            e.SelectedColor = System.Windows.Media.Color.FromArgb(
                colorDialog.Color.A,
                colorDialog.Color.R,
                colorDialog.Color.G,
                colorDialog.Color.B);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.OkRequested -= OnOkRequested;
        _viewModel.CancelRequested -= OnCancelRequested;
        _viewModel.ColorPickerRequested -= OnColorPickerRequested;
        base.OnClosed(e);
    }
}

/// <summary>
/// Converter that converts a boolean to one of two strings
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    /// <summary>
    /// The string format in "TrueValue|FalseValue" format
    /// </summary>
    public string TrueValue { get; set; } = "True";
    public string FalseValue { get; set; } = "False";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Check if parameter provides the format
            if (parameter is string format && format.Contains('|'))
            {
                var parts = format.Split('|');
                return boolValue ? parts[0] : parts[1];
            }

            return boolValue ? TrueValue : FalseValue;
        }
        return FalseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue)
        {
            if (parameter is string format && format.Contains('|'))
            {
                var parts = format.Split('|');
                return strValue == parts[0];
            }

            return strValue == TrueValue;
        }
        return false;
    }
}

/// <summary>
/// Event args for color picker requests
/// </summary>
public class ColorPickerEventArgs : EventArgs
{
    /// <summary>
    /// The current color value
    /// </summary>
    public System.Windows.Media.Color CurrentColor { get; set; }

    /// <summary>
    /// The selected color (set by the handler)
    /// </summary>
    public System.Windows.Media.Color SelectedColor { get; set; }

    /// <summary>
    /// Whether the color selection was handled
    /// </summary>
    public bool Handled { get; set; }
}
