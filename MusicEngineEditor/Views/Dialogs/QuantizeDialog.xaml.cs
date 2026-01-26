// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for configuring quantize settings.
/// </summary>
public partial class QuantizeDialog : Window
{
    #region Properties

    /// <summary>
    /// Gets the selected grid value in beats.
    /// </summary>
    public double GridValue { get; private set; } = 0.25;

    /// <summary>
    /// Gets the selected quantize mode.
    /// </summary>
    public QuantizeMode Mode { get; private set; } = QuantizeMode.StartAndEnd;

    /// <summary>
    /// Gets the quantize strength (0-100).
    /// </summary>
    public double Strength { get; private set; } = 100;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new QuantizeDialog.
    /// </summary>
    public QuantizeDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates a new QuantizeDialog with preset values.
    /// </summary>
    /// <param name="currentGridValue">The current grid value to pre-select.</param>
    public QuantizeDialog(double currentGridValue) : this()
    {
        // Pre-select the matching radio button
        SelectGridValueRadioButton(currentGridValue);
    }

    #endregion

    #region Event Handlers

    private void QuantizeButton_Click(object sender, RoutedEventArgs e)
    {
        // Get selected grid value
        GridValue = GetSelectedGridValue();

        // Get selected mode
        Mode = GetSelectedMode();

        // Get strength
        Strength = StrengthSlider.Value;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion

    #region Helper Methods

    private double GetSelectedGridValue()
    {
        if (Radio1_4.IsChecked == true) return 1.0;
        if (Radio1_8.IsChecked == true) return 0.5;
        if (Radio1_16.IsChecked == true) return 0.25;
        if (Radio1_32.IsChecked == true) return 0.125;
        if (Radio1_8T.IsChecked == true) return 1.0 / 3.0;
        if (Radio1_16T.IsChecked == true) return 1.0 / 6.0;
        return 0.25; // Default to 1/16
    }

    private QuantizeMode GetSelectedMode()
    {
        if (ModeStartAndEnd.IsChecked == true) return QuantizeMode.StartAndEnd;
        if (ModeStartOnly.IsChecked == true) return QuantizeMode.StartOnly;
        if (ModeEndOnly.IsChecked == true) return QuantizeMode.EndOnly;
        return QuantizeMode.StartAndEnd;
    }

    private void SelectGridValueRadioButton(double value)
    {
        const double tolerance = 0.001;

        if (System.Math.Abs(value - 1.0) < tolerance)
            Radio1_4.IsChecked = true;
        else if (System.Math.Abs(value - 0.5) < tolerance)
            Radio1_8.IsChecked = true;
        else if (System.Math.Abs(value - 0.25) < tolerance)
            Radio1_16.IsChecked = true;
        else if (System.Math.Abs(value - 0.125) < tolerance)
            Radio1_32.IsChecked = true;
        else if (System.Math.Abs(value - 1.0 / 3.0) < tolerance)
            Radio1_8T.IsChecked = true;
        else if (System.Math.Abs(value - 1.0 / 6.0) < tolerance)
            Radio1_16T.IsChecked = true;
        else
            Radio1_16.IsChecked = true; // Default
    }

    #endregion
}

/// <summary>
/// Defines the quantize mode options.
/// </summary>
public enum QuantizeMode
{
    /// <summary>
    /// Quantize both start and end positions.
    /// </summary>
    StartAndEnd,

    /// <summary>
    /// Quantize start position only, preserving note duration.
    /// </summary>
    StartOnly,

    /// <summary>
    /// Quantize end position only, adjusting note duration.
    /// </summary>
    EndOnly
}
