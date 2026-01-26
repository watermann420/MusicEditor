using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicEngine.Core;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for editing marker properties including name, type, color, and position.
/// </summary>
public partial class MarkerEditDialog : Window
{
    private readonly Marker _marker;
    private string _selectedColor;

    /// <summary>
    /// Predefined color palette for markers.
    /// </summary>
    private static readonly string[] PredefinedColors =
    [
        "#FF9500", // Orange (default cue)
        "#00AAFF", // Blue (default loop)
        "#9B59B6", // Purple (default section)
        "#E74C3C", // Red
        "#2ECC71", // Green
        "#F1C40F", // Yellow
        "#1ABC9C", // Teal
        "#E91E63", // Pink
        "#3498DB", // Light Blue
        "#95A5A6"  // Gray
    ];

    /// <summary>
    /// Gets the edited marker name.
    /// </summary>
    public string MarkerName => NameTextBox.Text;

    /// <summary>
    /// Gets the edited marker type.
    /// </summary>
    public MarkerType MarkerType
    {
        get
        {
            var typeItem = TypeComboBox.SelectedItem as ComboBoxItem;
            var typeString = typeItem?.Tag?.ToString() ?? "Cue";
            return typeString switch
            {
                "Loop" => MarkerType.Loop,
                "Section" => MarkerType.Section,
                _ => MarkerType.Cue
            };
        }
    }

    /// <summary>
    /// Gets the edited marker color.
    /// </summary>
    public string MarkerColor => _selectedColor;

    /// <summary>
    /// Gets the edited marker position.
    /// </summary>
    public double MarkerPosition
    {
        get
        {
            if (double.TryParse(PositionTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pos))
                return Math.Max(0, pos);
            return _marker.Position;
        }
    }

    /// <summary>
    /// Gets the edited marker end position (for loop markers).
    /// </summary>
    public double? MarkerEndPosition
    {
        get
        {
            if (MarkerType != MarkerType.Loop)
                return null;

            if (double.TryParse(EndPositionTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pos))
                return Math.Max(MarkerPosition + 0.25, pos);

            return MarkerPosition + 4;
        }
    }

    /// <summary>
    /// Creates a new MarkerEditDialog for the specified marker.
    /// </summary>
    /// <param name="marker">The marker to edit.</param>
    public MarkerEditDialog(Marker marker)
    {
        InitializeComponent();

        _marker = marker ?? throw new ArgumentNullException(nameof(marker));
        _selectedColor = marker.Color;

        InitializeColorPalette();
        LoadMarkerData();

        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    /// <summary>
    /// Shows the dialog to edit the specified marker.
    /// </summary>
    /// <param name="marker">The marker to edit.</param>
    /// <param name="owner">The owner window.</param>
    /// <returns>True if the dialog was confirmed; otherwise, false.</returns>
    public static bool? Show(Marker marker, Window? owner = null)
    {
        var dialog = new MarkerEditDialog(marker)
        {
            Owner = owner
        };

        return dialog.ShowDialog();
    }

    private void InitializeColorPalette()
    {
        ColorPalette.Children.Clear();

        foreach (var colorHex in PredefinedColors)
        {
            var color = ParseColor(colorHex);
            var button = new Button
            {
                Background = new SolidColorBrush(color),
                Style = (Style)FindResource("ColorSwatchStyle"),
                Tag = colorHex == _selectedColor ? "Selected" : null
            };

            button.Click += (sender, _) =>
            {
                SelectColor(colorHex);
                UpdateColorSelection(sender as Button);
            };

            ColorPalette.Children.Add(button);
        }
    }

    private void LoadMarkerData()
    {
        NameTextBox.Text = _marker.Name;

        // Set type
        var typeIndex = _marker.Type switch
        {
            MarkerType.Cue => 0,
            MarkerType.Loop => 1,
            MarkerType.Section => 2,
            _ => 0
        };
        TypeComboBox.SelectedIndex = typeIndex;

        // Set position
        PositionTextBox.Text = _marker.Position.ToString("F2", CultureInfo.InvariantCulture);

        // Set end position for loop markers
        if (_marker.EndPosition.HasValue)
        {
            EndPositionTextBox.Text = _marker.EndPosition.Value.ToString("F2", CultureInfo.InvariantCulture);
        }
        else
        {
            EndPositionTextBox.Text = (_marker.Position + 4).ToString("F2", CultureInfo.InvariantCulture);
        }

        // Update end position visibility
        UpdateEndPositionVisibility();

        // Set color
        SelectColor(_marker.Color);
        CustomColorTextBox.Text = _marker.Color;
    }

    private void SelectColor(string colorHex)
    {
        _selectedColor = colorHex;
        CustomColorTextBox.Text = colorHex;
        UpdateColorPreview();
    }

    private void UpdateColorSelection(Button? selectedButton)
    {
        // Clear all selections
        foreach (var child in ColorPalette.Children)
        {
            if (child is Button button)
            {
                button.Tag = null;
            }
        }

        // Mark selected
        if (selectedButton != null)
        {
            selectedButton.Tag = "Selected";
        }
    }

    private void UpdateColorPreview()
    {
        var color = ParseColor(_selectedColor);
        ColorPreview.Background = new SolidColorBrush(color);
    }

    private void UpdateEndPositionVisibility()
    {
        var typeItem = TypeComboBox.SelectedItem as ComboBoxItem;
        var isLoop = typeItem?.Tag?.ToString() == "Loop";
        EndPositionPanel.Visibility = isLoop ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)MediaColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Orange;
        }
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        UpdateEndPositionVisibility();

        // Update default color based on type
        var typeItem = TypeComboBox.SelectedItem as ComboBoxItem;
        var typeString = typeItem?.Tag?.ToString();

        var defaultColor = typeString switch
        {
            "Loop" => "#00AAFF",
            "Section" => "#9B59B6",
            _ => "#FF9500"
        };

        // Only change color if the current color was a default color
        if (_selectedColor == "#FF9500" || _selectedColor == "#00AAFF" || _selectedColor == "#9B59B6")
        {
            SelectColor(defaultColor);

            // Update button selection
            foreach (var child in ColorPalette.Children)
            {
                if (child is Button button)
                {
                    var buttonColor = (button.Background as SolidColorBrush)?.Color;
                    var targetColor = ParseColor(defaultColor);
                    button.Tag = buttonColor == targetColor ? "Selected" : null;
                }
            }
        }
    }

    private void CustomColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var text = CustomColorTextBox.Text;
        if (!string.IsNullOrEmpty(text))
        {
            // Ensure it starts with #
            if (!text.StartsWith('#'))
            {
                text = "#" + text;
            }

            // Validate hex format
            if (IsValidHexColor(text))
            {
                _selectedColor = text;
                UpdateColorPreview();

                // Clear palette selection if custom color
                var isPreset = Array.Exists(PredefinedColors, c => c.Equals(text, StringComparison.OrdinalIgnoreCase));
                if (!isPreset)
                {
                    foreach (var child in ColorPalette.Children)
                    {
                        if (child is Button button)
                        {
                            button.Tag = null;
                        }
                    }
                }
            }
        }
    }

    private static bool IsValidHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || !hex.StartsWith('#'))
            return false;

        var value = hex[1..];
        return (value.Length == 6 || value.Length == 8) &&
               value.All(c => Uri.IsHexDigit(c));
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate name
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Please enter a marker name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        // Validate position
        if (!double.TryParse(PositionTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var position) || position < 0)
        {
            MessageBox.Show("Please enter a valid position (0 or greater).", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            PositionTextBox.Focus();
            return;
        }

        // Validate end position for loops
        if (MarkerType == MarkerType.Loop)
        {
            if (!double.TryParse(EndPositionTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var endPos))
            {
                MessageBox.Show("Please enter a valid end position.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EndPositionTextBox.Focus();
                return;
            }

            if (endPos <= position)
            {
                MessageBox.Show("End position must be greater than start position.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EndPositionTextBox.Focus();
                return;
            }
        }

        // Validate color
        if (!IsValidHexColor(_selectedColor))
        {
            MessageBox.Show("Please enter a valid color in hex format (e.g., #FF9500).", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            CustomColorTextBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }
}
