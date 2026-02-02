// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Track color picker control with preset swatches and custom color option.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A compact color picker control for selecting track colors.
/// Features a 4x4 grid of preset color swatches, an auto option, and custom color picker.
/// </summary>
public partial class TrackColorPicker : UserControl
{
    #region Static Default Colors

    /// <summary>
    /// Default 16 color palette for track colors.
    /// </summary>
    public static readonly List<TrackColorSwatch> DefaultColors =
    [
        new TrackColorSwatch("#FF4444", "Red"),
        new TrackColorSwatch("#FF8C42", "Orange"),
        new TrackColorSwatch("#FFD93D", "Yellow"),
        new TrackColorSwatch("#A8E06C", "Lime"),
        new TrackColorSwatch("#4CAF50", "Green"),
        new TrackColorSwatch("#26A69A", "Teal"),
        new TrackColorSwatch("#00BCD4", "Cyan"),
        new TrackColorSwatch("#4A9EFF", "Blue"),
        new TrackColorSwatch("#5C6BC0", "Indigo"),
        new TrackColorSwatch("#9C27B0", "Purple"),
        new TrackColorSwatch("#E91E8C", "Magenta"),
        new TrackColorSwatch("#FF6B9D", "Pink"),
        new TrackColorSwatch("#8D6E63", "Brown"),
        new TrackColorSwatch("#808080", "Gray"),
        new TrackColorSwatch("#E0E0E0", "White"),
        new TrackColorSwatch("#2C2C2C", "Black")
    ];

    /// <summary>
    /// Auto colors based on track type.
    /// </summary>
    public static readonly Dictionary<Models.TrackType, string> AutoColors = new()
    {
        { Models.TrackType.Instrument, "#4A9EFF" },  // Blue for MIDI/Instrument
        { Models.TrackType.Audio, "#00CC66" },       // Green for Audio
        { Models.TrackType.Bus, "#E89C4B" },         // Orange for Bus
        { Models.TrackType.Master, "#FF9500" }       // Amber for Master
    };

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(string), typeof(TrackColorPicker),
            new FrameworkPropertyMetadata("#4A9EFF",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public static readonly DependencyProperty TrackTypeProperty =
        DependencyProperty.Register(nameof(TrackType), typeof(Models.TrackType?), typeof(TrackColorPicker),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ColorsProperty =
        DependencyProperty.Register(nameof(Colors), typeof(List<TrackColorSwatch>), typeof(TrackColorPicker),
            new PropertyMetadata(null, OnColorsChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the currently selected color in hex format.
    /// </summary>
    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the track type for auto color assignment.
    /// </summary>
    public Models.TrackType? TrackType
    {
        get => (Models.TrackType?)GetValue(TrackTypeProperty);
        set => SetValue(TrackTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets the list of color swatches to display.
    /// If null, uses DefaultColors.
    /// </summary>
    public List<TrackColorSwatch>? Colors
    {
        get => (List<TrackColorSwatch>?)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a color is selected.
    /// </summary>
    public event EventHandler<TrackColorSelectedEventArgs>? ColorSelected;

    /// <summary>
    /// Event raised when auto color is requested.
    /// </summary>
    public event EventHandler? AutoColorRequested;

    /// <summary>
    /// Event raised when custom color picker is requested.
    /// </summary>
    public event EventHandler? CustomColorRequested;

    #endregion

    #region Fields

    private readonly List<Button> _swatchButtons = [];

    #endregion

    #region Constructor

    public TrackColorPicker()
    {
        InitializeComponent();
        BuildSwatchGrid();
    }

    #endregion

    #region Property Changed Callbacks

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackColorPicker picker)
        {
            picker.UpdateSwatchSelection();
        }
    }

    private static void OnColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackColorPicker picker)
        {
            picker.BuildSwatchGrid();
        }
    }

    #endregion

    #region Private Methods

    private void BuildSwatchGrid()
    {
        ColorSwatchGrid.Children.Clear();
        _swatchButtons.Clear();

        var colors = Colors ?? DefaultColors;

        foreach (var swatch in colors)
        {
            var button = CreateSwatchButton(swatch);
            ColorSwatchGrid.Children.Add(button);
            _swatchButtons.Add(button);
        }

        UpdateSwatchSelection();
    }

    private Button CreateSwatchButton(TrackColorSwatch swatch)
    {
        var brush = CreateBrushFromHex(swatch.HexColor);

        var button = new Button
        {
            Background = brush,
            Tag = swatch,
            ToolTip = swatch.Name,
            Style = (Style)FindResource("ColorSwatchStyle")
        };

        button.Click += SwatchButton_Click;

        return button;
    }

    private void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TrackColorSwatch swatch)
        {
            SelectedColor = swatch.HexColor;
            ColorSelected?.Invoke(this, new TrackColorSelectedEventArgs(swatch.HexColor, swatch.Name, false));
        }
    }

    private void AutoButton_Click(object sender, RoutedEventArgs e)
    {
        if (TrackType.HasValue && AutoColors.TryGetValue(TrackType.Value, out var autoColor))
        {
            SelectedColor = autoColor;
            ColorSelected?.Invoke(this, new TrackColorSelectedEventArgs(autoColor, "Auto", true));
        }

        AutoColorRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CustomButton_Click(object sender, RoutedEventArgs e)
    {
        // Show Windows color picker dialog
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            AnyColor = true,
            FullOpen = true,
            Color = ConvertHexToDrawingColor(SelectedColor)
        };

        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var hexColor = $"#{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}";
            SelectedColor = hexColor;
            ColorSelected?.Invoke(this, new TrackColorSelectedEventArgs(hexColor, "Custom", false));
        }

        CustomColorRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSwatchSelection()
    {
        foreach (var button in _swatchButtons)
        {
            if (button.Tag is TrackColorSwatch swatch)
            {
                // Visual feedback for selected swatch could be enhanced here
                // Currently handled by the SelectedColor binding
            }
        }
    }

    private static SolidColorBrush CreateBrushFromHex(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
    }

    private static System.Drawing.Color ConvertHexToDrawingColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return System.Drawing.Color.FromArgb(color.R, color.G, color.B);
        }
        catch
        {
            return System.Drawing.Color.Gray;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the auto color for a given track type.
    /// </summary>
    public static string GetAutoColor(Models.TrackType trackType)
    {
        return AutoColors.TryGetValue(trackType, out var color) ? color : "#4A9EFF";
    }

    /// <summary>
    /// Resets the color swatches to default colors.
    /// </summary>
    public void ResetToDefaultColors()
    {
        Colors = null;
        BuildSwatchGrid();
    }

    #endregion
}

/// <summary>
/// Represents a color swatch with hex value and display name.
/// </summary>
public class TrackColorSwatch
{
    /// <summary>
    /// Gets or sets the color in hex format (e.g., "#FF5500").
    /// </summary>
    public string HexColor { get; set; }

    /// <summary>
    /// Gets or sets the display name of the color.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Creates a new TrackColorSwatch.
    /// </summary>
    public TrackColorSwatch(string hexColor, string name)
    {
        HexColor = hexColor;
        Name = name;
    }

    /// <summary>
    /// Gets the color as a WPF Brush.
    /// </summary>
    public Brush Brush
    {
        get
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(HexColor));
            }
            catch
            {
                return Brushes.Gray;
            }
        }
    }
}

/// <summary>
/// Event arguments for track color selection.
/// </summary>
public class TrackColorSelectedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the selected color in hex format.
    /// </summary>
    public string HexColor { get; }

    /// <summary>
    /// Gets the color name or description.
    /// </summary>
    public string ColorName { get; }

    /// <summary>
    /// Gets whether this was an auto color assignment.
    /// </summary>
    public bool IsAuto { get; }

    public TrackColorSelectedEventArgs(string hexColor, string colorName, bool isAuto)
    {
        HexColor = hexColor;
        ColorName = colorName;
        IsAuto = isAuto;
    }
}
