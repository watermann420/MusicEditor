// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Strudel.cc-style inline sliders in code.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Strudel.cc-style inline slider system.
/// Renders interactive sliders directly in the code text.
/// Drag left/right on numbers to change values in real-time.
/// </summary>
public class StrudelInlineSliderService : IDisposable
{
    private readonly TextEditor _editor;
    private readonly StrudelSliderRenderer _renderer;
    private readonly StrudelSliderElementGenerator _elementGenerator;
    private DetectedNumber? _activeNumber;
    private Point _dragStartPoint;
    private double _dragStartValue;
    private bool _isDragging;
    private bool _isDisposed;

    /// <summary>
    /// Event fired when a value changes during drag.
    /// </summary>
    public event EventHandler<StrudelValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Event fired when drag is completed.
    /// </summary>
    public event EventHandler<StrudelValueChangedEventArgs>? ValueChangeCompleted;

    public StrudelInlineSliderService(TextEditor editor)
    {
        _editor = editor;
        _renderer = new StrudelSliderRenderer(editor, this);
        _elementGenerator = new StrudelSliderElementGenerator(editor, this);

        // Add renderer for slider backgrounds
        _editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);

        // Subscribe to mouse events for drag behavior
        _editor.TextArea.TextView.MouseMove += TextView_MouseMove;
        _editor.TextArea.TextView.MouseLeftButtonDown += TextView_MouseLeftButtonDown;
        _editor.TextArea.TextView.MouseLeftButtonUp += TextView_MouseLeftButtonUp;
        _editor.TextArea.TextView.MouseLeave += TextView_MouseLeave;
        _editor.TextArea.PreviewKeyDown += TextArea_PreviewKeyDown;

        // Change cursor when over draggable numbers
        _editor.TextArea.TextView.QueryCursor += TextView_QueryCursor;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _editor.TextArea.TextView.BackgroundRenderers.Remove(_renderer);
        _editor.TextArea.TextView.MouseMove -= TextView_MouseMove;
        _editor.TextArea.TextView.MouseLeftButtonDown -= TextView_MouseLeftButtonDown;
        _editor.TextArea.TextView.MouseLeftButtonUp -= TextView_MouseLeftButtonUp;
        _editor.TextArea.TextView.MouseLeave -= TextView_MouseLeave;
        _editor.TextArea.PreviewKeyDown -= TextArea_PreviewKeyDown;
        _editor.TextArea.TextView.QueryCursor -= TextView_QueryCursor;
    }

    /// <summary>
    /// Gets the currently hovered number (if any).
    /// </summary>
    public DetectedNumber? HoveredNumber { get; private set; }

    /// <summary>
    /// Gets whether a drag operation is in progress.
    /// </summary>
    public bool IsDragging => _isDragging;

    private void TextView_QueryCursor(object sender, QueryCursorEventArgs e)
    {
        if (HoveredNumber != null || _isDragging)
        {
            e.Cursor = Cursors.SizeWE; // East-West resize cursor
            e.Handled = true;
        }
    }

    private void TextView_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(_editor.TextArea.TextView);

        if (_isDragging && _activeNumber != null)
        {
            // Calculate value change based on horizontal drag distance
            var deltaX = pos.X - _dragStartPoint.X;
            var config = _activeNumber.SliderConfig ?? SliderConfig.FromContext(_activeNumber.Context, _activeNumber.Value, _activeNumber.IsFloat);

            // Sensitivity: more drag = bigger change
            // Use step size and range to determine sensitivity
            var range = config.MaxValue - config.MinValue;
            var sensitivity = range / 300.0; // 300 pixels for full range

            // Shift key for fine control
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                sensitivity *= 0.1;
            }

            // Ctrl key for coarse control
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                sensitivity *= 10;
            }

            var newValue = _dragStartValue + (deltaX * sensitivity);

            // Snap to step
            if (config.Step > 0)
            {
                newValue = Math.Round(newValue / config.Step) * config.Step;
            }

            // Clamp to range
            newValue = Math.Clamp(newValue, config.MinValue, config.MaxValue);

            // Update document
            UpdateDocumentValue(_activeNumber, newValue);

            // Fire event
            ValueChanged?.Invoke(this, new StrudelValueChangedEventArgs
            {
                Number = _activeNumber,
                OldValue = _dragStartValue,
                NewValue = newValue
            });

            e.Handled = true;
            return;
        }

        // Check if hovering over a number
        var textPosition = _editor.GetPositionFromPoint(pos);
        if (textPosition != null)
        {
            var offset = _editor.Document.GetOffset(textPosition.Value.Location);
            HoveredNumber = NumberDetector.GetNumberAtOffset(_editor.Document, offset);

            // Redraw to update hover effect
            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }
        else
        {
            HoveredNumber = null;
        }
    }

    private void TextView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (HoveredNumber != null)
        {
            _isDragging = true;
            _activeNumber = HoveredNumber;
            _dragStartPoint = e.GetPosition(_editor.TextArea.TextView);
            _dragStartValue = _activeNumber.Value;

            // Capture mouse to track drag outside editor
            _editor.TextArea.TextView.CaptureMouse();

            e.Handled = true;
        }
    }

    private void TextView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && _activeNumber != null)
        {
            _editor.TextArea.TextView.ReleaseMouseCapture();

            // Fire completion event
            var currentValue = GetCurrentValue(_activeNumber);
            ValueChangeCompleted?.Invoke(this, new StrudelValueChangedEventArgs
            {
                Number = _activeNumber,
                OldValue = _dragStartValue,
                NewValue = currentValue
            });

            _isDragging = false;
            _activeNumber = null;
            e.Handled = true;
        }
    }

    private void TextView_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            HoveredNumber = null;
            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }
    }

    private void TextArea_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isDragging && e.Key == Key.Escape)
        {
            // Cancel drag and restore original value
            if (_activeNumber != null)
            {
                UpdateDocumentValue(_activeNumber, _dragStartValue);
            }

            _editor.TextArea.TextView.ReleaseMouseCapture();
            _isDragging = false;
            _activeNumber = null;
            e.Handled = true;
        }
    }

    private double GetCurrentValue(DetectedNumber number)
    {
        var text = _editor.Document.GetText(number.StartOffset, number.Length);
        var numText = text.TrimEnd('f', 'F', 'd', 'D');
        if (double.TryParse(numText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        return number.Value;
    }

    private void UpdateDocumentValue(DetectedNumber number, double newValue)
    {
        var formattedValue = NumberDetector.FormatNumber(newValue, number);

        // Replace the text in the document
        _editor.Document.Replace(number.StartOffset, number.Length, formattedValue);

        // Update the number's offsets if the length changed
        var lengthDiff = formattedValue.Length - number.Length;
        if (lengthDiff != 0)
        {
            // Create updated number info
            var updatedNumber = new DetectedNumber
            {
                StartOffset = number.StartOffset,
                EndOffset = number.StartOffset + formattedValue.Length,
                OriginalText = formattedValue,
                Value = newValue,
                IsFloat = number.IsFloat,
                HasFloatSuffix = number.HasFloatSuffix,
                HasDoubleSuffix = number.HasDoubleSuffix,
                Line = number.Line,
                Column = number.Column,
                SliderConfig = number.SliderConfig,
                Context = number.Context
            };

            // Update active number reference
            if (_activeNumber == number)
            {
                _activeNumber = updatedNumber;
            }
        }
    }
}

/// <summary>
/// Event arguments for Strudel slider value changes.
/// </summary>
public class StrudelValueChangedEventArgs : EventArgs
{
    public DetectedNumber Number { get; init; } = null!;
    public double OldValue { get; init; }
    public double NewValue { get; init; }
}

/// <summary>
/// Renders visual slider indicators in the code editor background.
/// Shows slider range, current position, and hover effects.
/// </summary>
public class StrudelSliderRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private readonly StrudelInlineSliderService _service;

    // Colors matching dark theme
    private static readonly SolidColorBrush SliderTrackBrush = new(Color.FromArgb(40, 78, 201, 176)); // Cyan track
    private static readonly SolidColorBrush SliderFillBrush = new(Color.FromArgb(100, 78, 201, 176)); // Cyan fill
    private static readonly SolidColorBrush HoverBrush = new(Color.FromArgb(60, 78, 201, 176)); // Hover highlight
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromArgb(80, 232, 145, 184)); // Pink when dragging
    private static readonly Pen SliderBorderPen = new(new SolidColorBrush(Color.FromArgb(120, 78, 201, 176)), 1);

    public StrudelSliderRenderer(TextEditor editor, StrudelInlineSliderService service)
    {
        _editor = editor;
        _service = service;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid) return;

        var numbers = NumberDetector.DetectNumbers(_editor.Document);

        foreach (var number in numbers)
        {
            var segment = new TextSegment
            {
                StartOffset = number.StartOffset,
                Length = number.Length
            };

            var isHovered = _service.HoveredNumber != null &&
                           _service.HoveredNumber.StartOffset == number.StartOffset;
            var isActive = _service.IsDragging && isHovered;

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                // Draw slider track background
                var trackRect = new Rect(rect.Left - 2, rect.Top, rect.Width + 4, rect.Height);

                if (isActive)
                {
                    // Active (dragging) state - pink glow
                    drawingContext.DrawRoundedRectangle(ActiveBrush, null, trackRect, 3, 3);
                }
                else if (isHovered)
                {
                    // Hover state - cyan highlight
                    drawingContext.DrawRoundedRectangle(HoverBrush, SliderBorderPen, trackRect, 3, 3);
                }
                else
                {
                    // Normal state - subtle track
                    drawingContext.DrawRoundedRectangle(SliderTrackBrush, null, trackRect, 3, 3);
                }

                // Draw fill indicator based on value position in range
                var config = number.SliderConfig ?? SliderConfig.FromContext(number.Context, number.Value, number.IsFloat);
                var range = config.MaxValue - config.MinValue;
                if (range > 0)
                {
                    var fillPercent = (number.Value - config.MinValue) / range;
                    fillPercent = Math.Clamp(fillPercent, 0, 1);

                    var fillWidth = trackRect.Width * fillPercent;
                    var fillRect = new Rect(trackRect.Left, trackRect.Top, fillWidth, trackRect.Height);

                    if (fillWidth > 0)
                    {
                        drawingContext.DrawRoundedRectangle(SliderFillBrush, null, fillRect, 3, 3);
                    }
                }
            }
        }
    }
}

/// <summary>
/// Generates inline slider UI elements in the code editor.
/// Shows min/max range and current value tooltip.
/// </summary>
public class StrudelSliderElementGenerator : VisualLineElementGenerator
{
    private readonly TextEditor _editor;
    private readonly StrudelInlineSliderService _service;

    public StrudelSliderElementGenerator(TextEditor editor, StrudelInlineSliderService service)
    {
        _editor = editor;
        _service = service;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        // Not generating inline elements for now - using background rendering instead
        return -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        return null;
    }
}

/// <summary>
/// Inline toggle element for boolean values in code.
/// Click to toggle between true/false.
/// </summary>
public class InlineBooleanToggle
{
    private readonly TextEditor _editor;

    public InlineBooleanToggle(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// Finds boolean values in code and makes them toggleable.
    /// </summary>
    public void EnableBooleanToggles()
    {
        _editor.TextArea.TextView.MouseLeftButtonDown += (sender, e) =>
        {
            var pos = e.GetPosition(_editor.TextArea.TextView);
            var textPos = _editor.GetPositionFromPoint(pos);

            if (textPos == null) return;

            var offset = _editor.Document.GetOffset(textPos.Value.Location);
            var line = _editor.Document.GetLineByOffset(offset);
            var lineText = _editor.Document.GetText(line);

            // Check for "true" or "false"
            var beforeOffset = offset - line.Offset;

            // Find true/false at current position
            var trueIndex = lineText.IndexOf("true", StringComparison.Ordinal);
            var falseIndex = lineText.IndexOf("false", StringComparison.Ordinal);

            if (trueIndex >= 0 && beforeOffset >= trueIndex && beforeOffset <= trueIndex + 4 &&
                Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                // Toggle true -> false
                _editor.Document.Replace(line.Offset + trueIndex, 4, "false");
                e.Handled = true;
            }
            else if (falseIndex >= 0 && beforeOffset >= falseIndex && beforeOffset <= falseIndex + 5 &&
                     Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                // Toggle false -> true
                _editor.Document.Replace(line.Offset + falseIndex, 5, "true");
                e.Handled = true;
            }
        };
    }
}

/// <summary>
/// Configuration for Strudel-style sliders.
/// </summary>
public class StrudelSliderConfig
{
    /// <summary>
    /// Whether to show the slider fill indicator.
    /// </summary>
    public bool ShowFill { get; set; } = true;

    /// <summary>
    /// Whether to show the hover effect.
    /// </summary>
    public bool ShowHoverEffect { get; set; } = true;

    /// <summary>
    /// Drag sensitivity multiplier.
    /// </summary>
    public double Sensitivity { get; set; } = 1.0;

    /// <summary>
    /// Whether to snap to step values.
    /// </summary>
    public bool SnapToStep { get; set; } = true;

    /// <summary>
    /// Whether Shift key enables fine control.
    /// </summary>
    public bool ShiftForFineControl { get; set; } = true;

    /// <summary>
    /// Whether Ctrl key enables coarse control.
    /// </summary>
    public bool CtrlForCoarseControl { get; set; } = true;
}
