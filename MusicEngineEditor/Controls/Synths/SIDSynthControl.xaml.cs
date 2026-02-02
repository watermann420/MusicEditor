// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the SID Synthesizer Editor control.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for SIDSynthControl.xaml.
/// Provides a visual editor for the MusicEngine SIDSynth (Commodore 64 SID chip emulation).
/// </summary>
public partial class SIDSynthControl : UserControl
{
    /// <summary>
    /// Converter for boolean to filter routing brush.
    /// </summary>
    public static BoolToFilterBrushConverter BoolToFilterBrushConverter { get; } = new();

    /// <summary>
    /// Creates a new SIDSynthControl.
    /// </summary>
    public SIDSynthControl()
    {
        InitializeComponent();
    }
}

/// <summary>
/// Converts a boolean to a filter routing indicator brush.
/// True = enabled (accent color), False = disabled (dim).
/// </summary>
public class BoolToFilterBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush EnabledBrush =
        new(Color.FromRgb(0x00, 0xD9, 0xFF)); // Accent cyan
    private static readonly SolidColorBrush DisabledBrush =
        new(Color.FromRgb(0x40, 0x40, 0x40)); // Dim gray

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? EnabledBrush : DisabledBrush;
        }
        return DisabledBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
