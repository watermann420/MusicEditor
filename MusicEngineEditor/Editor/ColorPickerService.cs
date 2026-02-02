// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Color picker service for editing color values in code.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Provides inline color picker when hovering over color values in code.
/// Supports hex colors (#RRGGBB, #AARRGGBB) and Color.FromRgb/FromArgb calls.
/// </summary>
public class ColorPickerService : IDisposable
{
    private readonly TextEditor _editor;
    private readonly Popup _pickerPopup;
    private readonly Border _pickerBorder;
    private readonly StackPanel _pickerPanel;
    private readonly Rectangle _colorPreview;
    private readonly Slider _redSlider;
    private readonly Slider _greenSlider;
    private readonly Slider _blueSlider;
    private readonly Slider _alphaSlider;
    private readonly TextBlock _hexText;
    private bool _isDisposed;
    private DetectedColor? _currentColor;

    // Regex patterns for color detection
    private static readonly Regex HexColorRegex = new(@"#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})\b", RegexOptions.Compiled);
    private static readonly Regex FromRgbRegex = new(@"Color\.FromRgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.Compiled);
    private static readonly Regex FromArgbRegex = new(@"Color\.FromArgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.Compiled);
    private static readonly Regex HexColorConstructorRegex = new(@"0x([0-9A-Fa-f]{2})\s*,\s*0x([0-9A-Fa-f]{2})\s*,\s*0x([0-9A-Fa-f]{2})", RegexOptions.Compiled);

    public ColorPickerService(TextEditor editor)
    {
        _editor = editor;

        // Create color picker popup
        _pickerPopup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.Mouse,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade
        };

        // Create picker content
        _pickerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Width = 220
        };

        _pickerPanel = new StackPanel { Orientation = Orientation.Vertical };

        // Color preview
        _colorPreview = new Rectangle
        {
            Width = 196,
            Height = 40,
            RadiusX = 4,
            RadiusY = 4,
            Stroke = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)),
            StrokeThickness = 1,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _pickerPanel.Children.Add(_colorPreview);

        // Hex display
        _hexText = new TextBlock
        {
            FontFamily = new FontFamily("JetBrains Mono, Consolas"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _pickerPanel.Children.Add(_hexText);

        // Sliders
        _alphaSlider = CreateSlider("A", Color.FromRgb(0xFF, 0xFF, 0xFF));
        _redSlider = CreateSlider("R", Color.FromRgb(0xFF, 0x40, 0x40));
        _greenSlider = CreateSlider("G", Color.FromRgb(0x40, 0xFF, 0x40));
        _blueSlider = CreateSlider("B", Color.FromRgb(0x40, 0x80, 0xFF));

        _pickerBorder.Child = _pickerPanel;
        _pickerPopup.Child = _pickerBorder;

        // Attach events
        _editor.TextArea.MouseDown += OnMouseDown;
    }

    private Slider CreateSlider(string label, Color trackColor)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };

        var labelText = new TextBlock
        {
            Text = label,
            Width = 16,
            Foreground = new SolidColorBrush(trackColor),
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        DockPanel.SetDock(labelText, Dock.Left);
        panel.Children.Add(labelText);

        var valueText = new TextBlock
        {
            Width = 30,
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            FontSize = 10,
            TextAlignment = System.Windows.TextAlignment.Right,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        DockPanel.SetDock(valueText, Dock.Right);
        panel.Children.Add(valueText);

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 255,
            Width = 130,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };

        slider.ValueChanged += (s, e) =>
        {
            valueText.Text = ((int)e.NewValue).ToString();
            UpdateColorPreview();
            UpdateCodeValue();
        };

        panel.Children.Add(slider);
        _pickerPanel.Children.Add(panel);

        return slider;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Ctrl+Click to show color picker
        if (e.ChangedButton != MouseButton.Left || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        var pos = _editor.TextArea.TextView.GetPositionFloor(
            e.GetPosition(_editor.TextArea.TextView) + _editor.TextArea.TextView.ScrollOffset);
        if (pos == null) return;

        int offset = _editor.Document.GetOffset(pos.Value.Location);
        if (offset < 0 || offset >= _editor.Document.TextLength) return;

        // Try to detect a color at this position
        var color = DetectColorAtOffset(offset);
        if (color != null)
        {
            _currentColor = color;
            ShowColorPicker(color);
            e.Handled = true;
        }
    }

    private DetectedColor? DetectColorAtOffset(int offset)
    {
        var line = _editor.Document.GetLineByOffset(offset);
        var lineText = _editor.Document.GetText(line.Offset, line.Length);
        int lineOffset = offset - line.Offset;

        // Check hex colors (#RRGGBB or #AARRGGBB)
        foreach (Match match in HexColorRegex.Matches(lineText))
        {
            if (lineOffset >= match.Index && lineOffset <= match.Index + match.Length)
            {
                var hex = match.Groups[1].Value;
                byte a = 255, r, g, b;

                if (hex.Length == 8)
                {
                    a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                }
                else
                {
                    r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                }

                return new DetectedColor(
                    Color.FromArgb(a, r, g, b),
                    line.Offset + match.Index,
                    match.Length,
                    ColorFormat.Hex);
            }
        }

        // Check Color.FromRgb(r, g, b)
        foreach (Match match in FromRgbRegex.Matches(lineText))
        {
            if (lineOffset >= match.Index && lineOffset <= match.Index + match.Length)
            {
                byte r = byte.Parse(match.Groups[1].Value);
                byte g = byte.Parse(match.Groups[2].Value);
                byte b = byte.Parse(match.Groups[3].Value);

                return new DetectedColor(
                    Color.FromRgb(r, g, b),
                    line.Offset + match.Index,
                    match.Length,
                    ColorFormat.FromRgb);
            }
        }

        // Check Color.FromArgb(a, r, g, b)
        foreach (Match match in FromArgbRegex.Matches(lineText))
        {
            if (lineOffset >= match.Index && lineOffset <= match.Index + match.Length)
            {
                byte a = byte.Parse(match.Groups[1].Value);
                byte r = byte.Parse(match.Groups[2].Value);
                byte g = byte.Parse(match.Groups[3].Value);
                byte b = byte.Parse(match.Groups[4].Value);

                return new DetectedColor(
                    Color.FromArgb(a, r, g, b),
                    line.Offset + match.Index,
                    match.Length,
                    ColorFormat.FromArgb);
            }
        }

        // Check 0xRR, 0xGG, 0xBB format
        foreach (Match match in HexColorConstructorRegex.Matches(lineText))
        {
            if (lineOffset >= match.Index && lineOffset <= match.Index + match.Length)
            {
                byte r = byte.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
                byte g = byte.Parse(match.Groups[2].Value, NumberStyles.HexNumber);
                byte b = byte.Parse(match.Groups[3].Value, NumberStyles.HexNumber);

                return new DetectedColor(
                    Color.FromRgb(r, g, b),
                    line.Offset + match.Index,
                    match.Length,
                    ColorFormat.HexTuple);
            }
        }

        return null;
    }

    private void ShowColorPicker(DetectedColor color)
    {
        _alphaSlider.Value = color.Color.A;
        _redSlider.Value = color.Color.R;
        _greenSlider.Value = color.Color.G;
        _blueSlider.Value = color.Color.B;

        // Show/hide alpha slider based on format
        _alphaSlider.IsEnabled = color.Format == ColorFormat.FromArgb ||
                                  color.Format == ColorFormat.Hex && color.Length > 7;

        UpdateColorPreview();
        _pickerPopup.IsOpen = true;
    }

    private void UpdateColorPreview()
    {
        var color = Color.FromArgb(
            (byte)_alphaSlider.Value,
            (byte)_redSlider.Value,
            (byte)_greenSlider.Value,
            (byte)_blueSlider.Value);

        _colorPreview.Fill = new SolidColorBrush(color);
        _hexText.Text = color.A < 255
            ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void UpdateCodeValue()
    {
        if (_currentColor == null) return;

        var color = Color.FromArgb(
            (byte)_alphaSlider.Value,
            (byte)_redSlider.Value,
            (byte)_greenSlider.Value,
            (byte)_blueSlider.Value);

        string replacement = _currentColor.Format switch
        {
            ColorFormat.Hex when _currentColor.Length > 7 =>
                $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}",
            ColorFormat.Hex =>
                $"#{color.R:X2}{color.G:X2}{color.B:X2}",
            ColorFormat.FromRgb =>
                $"Color.FromRgb({color.R}, {color.G}, {color.B})",
            ColorFormat.FromArgb =>
                $"Color.FromArgb({color.A}, {color.R}, {color.G}, {color.B})",
            ColorFormat.HexTuple =>
                $"0x{color.R:X2}, 0x{color.G:X2}, 0x{color.B:X2}",
            _ => throw new InvalidOperationException()
        };

        // Update document
        _editor.Document.Replace(_currentColor.Offset, _currentColor.Length, replacement);

        // Update the current color tracking
        _currentColor = _currentColor with
        {
            Color = color,
            Length = replacement.Length
        };
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _editor.TextArea.MouseDown -= OnMouseDown;
        _pickerPopup.IsOpen = false;
    }
}

public enum ColorFormat
{
    Hex,        // #RRGGBB or #AARRGGBB
    FromRgb,    // Color.FromRgb(r, g, b)
    FromArgb,   // Color.FromArgb(a, r, g, b)
    HexTuple    // 0xRR, 0xGG, 0xBB
}

public record DetectedColor(Color Color, int Offset, int Length, ColorFormat Format);
