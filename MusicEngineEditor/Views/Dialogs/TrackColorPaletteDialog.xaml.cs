// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog for editing the track color palette presets.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicEngineEditor.Controls;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for editing the 16 preset track colors.
/// </summary>
public partial class TrackColorPaletteDialog : Window
{
    #region Fields

    private readonly List<string> _originalColors;
    private readonly List<string> _currentColors;
    private readonly List<Button> _swatchButtons = [];
    private int _selectedIndex = -1;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the edited color palette.
    /// </summary>
    public List<TrackColorSwatch> EditedPalette { get; private set; }

    /// <summary>
    /// Gets whether the palette was saved.
    /// </summary>
    public bool WasSaved { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the palette is saved.
    /// </summary>
    public event EventHandler<List<TrackColorSwatch>>? PaletteSaved;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new TrackColorPaletteDialog with default colors.
    /// </summary>
    public TrackColorPaletteDialog() : this(TrackColorPicker.DefaultColors)
    {
    }

    /// <summary>
    /// Creates a new TrackColorPaletteDialog with the specified colors.
    /// </summary>
    public TrackColorPaletteDialog(List<TrackColorSwatch> initialColors)
    {
        InitializeComponent();

        // Store original colors for reset/cancel
        _originalColors = initialColors.Select(c => c.HexColor).ToList();
        _currentColors = new List<string>(_originalColors);

        EditedPalette = initialColors.Select(c => new TrackColorSwatch(c.HexColor, c.Name)).ToList();

        BuildColorGrid();
    }

    #endregion

    #region Private Methods

    private void BuildColorGrid()
    {
        ColorGrid.Children.Clear();
        _swatchButtons.Clear();

        for (int i = 0; i < _currentColors.Count && i < 16; i++)
        {
            var button = CreateSwatchButton(i, _currentColors[i]);
            ColorGrid.Children.Add(button);
            _swatchButtons.Add(button);
        }

        // Fill remaining slots if less than 16 colors
        while (_swatchButtons.Count < 16)
        {
            _currentColors.Add("#808080");
            var button = CreateSwatchButton(_swatchButtons.Count, "#808080");
            ColorGrid.Children.Add(button);
            _swatchButtons.Add(button);
        }
    }

    private Button CreateSwatchButton(int index, string hexColor)
    {
        var brush = CreateBrushFromHex(hexColor);

        var button = new Button
        {
            Background = brush,
            Tag = index,
            ToolTip = $"Color {index + 1}: {hexColor}",
            Style = (Style)FindResource("EditableSwatchStyle")
        };

        button.Click += SwatchButton_Click;

        return button;
    }

    private void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index)
        {
            _selectedIndex = index;
            var currentColor = _currentColors[index];

            // Show Windows color picker dialog
            var colorDialog = new System.Windows.Forms.ColorDialog
            {
                AnyColor = true,
                FullOpen = true,
                Color = ConvertHexToDrawingColor(currentColor)
            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newHex = $"#{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}";
                UpdateColor(index, newHex);
            }
        }
    }

    private void UpdateColor(int index, string hexColor)
    {
        if (index < 0 || index >= _currentColors.Count) return;

        _currentColors[index] = hexColor;

        // Update button appearance
        if (index < _swatchButtons.Count)
        {
            _swatchButtons[index].Background = CreateBrushFromHex(hexColor);
            _swatchButtons[index].ToolTip = $"Color {index + 1}: {hexColor}";
        }

        // Update selected preview
        UpdateSelectedPreview(index, hexColor);
    }

    private void UpdateSelectedPreview(int index, string hexColor)
    {
        SelectedColorPreview.Background = CreateBrushFromHex(hexColor);
        SelectedColorText.Text = hexColor;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Reset all colors to the default palette?",
            "Reset Colors",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _currentColors.Clear();
            _currentColors.AddRange(TrackColorPicker.DefaultColors.Select(c => c.HexColor));

            // Rebuild the grid with default colors
            BuildColorGrid();

            // Reset preview
            if (_currentColors.Count > 0)
            {
                UpdateSelectedPreview(0, _currentColors[0]);
            }
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Build the edited palette
        EditedPalette = [];
        var defaultNames = TrackColorPicker.DefaultColors.Select(c => c.Name).ToList();

        for (int i = 0; i < _currentColors.Count; i++)
        {
            var name = i < defaultNames.Count ? defaultNames[i] : $"Color {i + 1}";
            EditedPalette.Add(new TrackColorSwatch(_currentColors[i], name));
        }

        WasSaved = true;
        PaletteSaved?.Invoke(this, EditedPalette);

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        WasSaved = false;
        DialogResult = false;
        Close();
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
            return new SolidColorBrush(Colors.Gray);
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
    /// Gets the list of hex colors in the current palette.
    /// </summary>
    public List<string> GetColors()
    {
        return new List<string>(_currentColors);
    }

    /// <summary>
    /// Sets the colors in the palette.
    /// </summary>
    public void SetColors(List<string> colors)
    {
        _currentColors.Clear();
        _currentColors.AddRange(colors);
        BuildColorGrid();
    }

    #endregion
}
