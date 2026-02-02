// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: VS Code-style minimap for code overview.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace MusicEngineEditor.Editor;

/// <summary>
/// A VS Code-style minimap that shows a scaled-down overview of the code.
/// Click to navigate, drag to scroll through the document.
/// </summary>
public class MinimapControl : Canvas
{
    private readonly TextEditor _editor;
    private readonly double _lineHeight = 2.0;  // Height per line in minimap
    private readonly double _charWidth = 1.0;   // Width per character
    private readonly int _maxCharsToRender = 80; // Max characters per line to render
    private bool _isDragging;
    private DrawingVisual? _codeVisual;
    private DrawingVisual? _viewportVisual;

    // Colors
    private static readonly Brush BackgroundBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly Brush ViewportBrush = new SolidColorBrush(Color.FromArgb(60, 0, 217, 255)); // Cyan semi-transparent
    private static readonly Brush ViewportBorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 217, 255));
    private static readonly Brush KeywordBrush = new SolidColorBrush(Color.FromRgb(0x56, 0xD4, 0xFF));
    private static readonly Brush StringBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA6, 0x57));
    private static readonly Brush CommentBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0x8A, 0x7C));
    private static readonly Brush NumberBrush = new SolidColorBrush(Color.FromRgb(0xBD, 0x93, 0xF9));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
    private static readonly Brush CurrentLineBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 0)); // Yellow highlight

    public MinimapControl(TextEditor editor)
    {
        _editor = editor;

        // Setup control
        Width = 80;
        MinWidth = 60;
        MaxWidth = 100;
        Background = BackgroundBrush;
        ClipToBounds = true;
        SnapsToDevicePixels = true;

        // Create visual layers
        _codeVisual = new DrawingVisual();
        _viewportVisual = new DrawingVisual();
        AddVisualChild(_codeVisual);
        AddVisualChild(_viewportVisual);

        // Subscribe to editor events
        _editor.TextChanged += (s, e) => InvalidateVisual();
        _editor.TextArea.TextView.ScrollOffsetChanged += (s, e) => UpdateViewport();
        _editor.TextArea.Caret.PositionChanged += (s, e) => UpdateViewport();

        // Mouse events
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;

        // Initial render
        Loaded += (s, e) => InvalidateVisual();
    }

    protected override int VisualChildrenCount => 2;

    protected override Visual GetVisualChild(int index)
    {
        return index == 0 ? _codeVisual! : _viewportVisual!;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        RenderCode();
        UpdateViewport();
    }

    private void RenderCode()
    {
        if (_codeVisual == null) return;

        using var dc = _codeVisual.RenderOpen();
        dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var document = _editor.Document;
        if (document == null || document.LineCount == 0) return;

        var text = document.Text;
        double y = 0;

        for (int lineNum = 1; lineNum <= document.LineCount && y < ActualHeight + 100; lineNum++)
        {
            var line = document.GetLineByNumber(lineNum);
            var lineText = document.GetText(line.Offset, Math.Min(line.Length, _maxCharsToRender));

            RenderLine(dc, lineText, y, lineNum == _editor.TextArea.Caret.Line);
            y += _lineHeight;
        }
    }

    private void RenderLine(DrawingContext dc, string lineText, double y, bool isCurrentLine)
    {
        if (string.IsNullOrEmpty(lineText)) return;

        // Highlight current line
        if (isCurrentLine)
        {
            dc.DrawRectangle(CurrentLineBrush, null, new Rect(0, y, ActualWidth, _lineHeight));
        }

        double x = 4; // Left margin
        int i = 0;

        while (i < lineText.Length && x < ActualWidth - 4)
        {
            char c = lineText[i];

            if (char.IsWhiteSpace(c))
            {
                x += _charWidth;
                i++;
                continue;
            }

            // Determine color based on simple syntax detection
            Brush brush = TextBrush;
            int tokenLength = 1;

            // Comment detection
            if (c == '/' && i + 1 < lineText.Length && lineText[i + 1] == '/')
            {
                brush = CommentBrush;
                tokenLength = lineText.Length - i;
            }
            // String detection
            else if (c == '"' || c == '\'')
            {
                brush = StringBrush;
                tokenLength = FindStringEnd(lineText, i);
            }
            // Number detection
            else if (char.IsDigit(c) || (c == '.' && i + 1 < lineText.Length && char.IsDigit(lineText[i + 1])))
            {
                brush = NumberBrush;
                tokenLength = FindNumberEnd(lineText, i);
            }
            // Keyword detection (simple heuristic)
            else if (char.IsLetter(c))
            {
                tokenLength = FindWordEnd(lineText, i);
                var word = lineText.Substring(i, tokenLength);
                if (IsKeyword(word))
                {
                    brush = KeywordBrush;
                }
            }

            // Draw the token as a small rectangle
            double tokenWidth = tokenLength * _charWidth;
            dc.DrawRectangle(brush, null, new Rect(x, y + 0.5, Math.Min(tokenWidth, ActualWidth - x - 4), _lineHeight - 1));

            x += tokenWidth;
            i += tokenLength;
        }
    }

    private static int FindStringEnd(string text, int start)
    {
        char quote = text[start];
        for (int i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++; // Skip escaped character
                continue;
            }
            if (text[i] == quote)
                return i - start + 1;
        }
        return text.Length - start;
    }

    private static int FindNumberEnd(string text, int start)
    {
        int i = start;
        bool hasDecimal = false;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsDigit(c))
            {
                i++;
            }
            else if (c == '.' && !hasDecimal)
            {
                hasDecimal = true;
                i++;
            }
            else if (c == 'f' || c == 'd' || c == 'm' || c == 'F' || c == 'D' || c == 'M')
            {
                i++;
                break;
            }
            else
            {
                break;
            }
        }
        return Math.Max(1, i - start);
    }

    private static int FindWordEnd(string text, int start)
    {
        int i = start;
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            i++;
        return Math.Max(1, i - start);
    }

    private static readonly HashSet<string> Keywords = new()
    {
        "if", "else", "for", "foreach", "while", "do", "switch", "case", "default",
        "break", "continue", "return", "throw", "try", "catch", "finally",
        "class", "struct", "interface", "enum", "namespace", "using",
        "public", "private", "protected", "internal", "static", "readonly", "const",
        "virtual", "override", "abstract", "sealed", "new", "async", "await",
        "var", "int", "float", "double", "string", "bool", "void", "true", "false", "null"
    };

    private static bool IsKeyword(string word) => Keywords.Contains(word);

    private void UpdateViewport()
    {
        if (_viewportVisual == null) return;

        using var dc = _viewportVisual.RenderOpen();

        var document = _editor.Document;
        if (document == null || document.LineCount == 0) return;

        // Calculate viewport position
        var firstVisibleLine = _editor.TextArea.TextView.GetDocumentLineByVisualTop(
            _editor.TextArea.TextView.ScrollOffset.Y);
        var lastVisibleLine = _editor.TextArea.TextView.GetDocumentLineByVisualTop(
            _editor.TextArea.TextView.ScrollOffset.Y + _editor.TextArea.TextView.ActualHeight);

        int startLine = firstVisibleLine?.LineNumber ?? 1;
        int endLine = lastVisibleLine?.LineNumber ?? document.LineCount;

        double y = (startLine - 1) * _lineHeight;
        double height = (endLine - startLine + 1) * _lineHeight;

        // Draw viewport rectangle
        dc.DrawRectangle(ViewportBrush, new Pen(ViewportBorderBrush, 1),
            new Rect(0, y, ActualWidth, Math.Max(height, 10)));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        CaptureMouse();
        NavigateToPosition(e.GetPosition(this).Y);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            NavigateToPosition(e.GetPosition(this).Y);
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Forward scroll to editor
        _editor.ScrollToVerticalOffset(_editor.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private void NavigateToPosition(double y)
    {
        var document = _editor.Document;
        if (document == null) return;

        int targetLine = Math.Max(1, Math.Min((int)(y / _lineHeight) + 1, document.LineCount));

        // Calculate offset to center the target line in the viewport
        double targetOffset = (targetLine - 1) * _editor.TextArea.TextView.DefaultLineHeight;
        double viewportHeight = _editor.TextArea.TextView.ActualHeight;
        double centeredOffset = targetOffset - viewportHeight / 2;

        _editor.ScrollToVerticalOffset(Math.Max(0, centeredOffset));
    }

    /// <summary>
    /// Force a refresh of the minimap
    /// </summary>
    public void Refresh()
    {
        InvalidateVisual();
    }
}

/// <summary>
/// Helper class to integrate minimap with the editor
/// </summary>
public static class MinimapHelper
{
    private static readonly Dictionary<TextEditor, MinimapControl> _minimaps = new();

    /// <summary>
    /// Create and attach a minimap to the editor's parent container.
    /// The minimap should be placed to the right of the editor in the XAML.
    /// </summary>
    public static MinimapControl CreateMinimap(TextEditor editor)
    {
        // Remove existing minimap if any
        if (_minimaps.TryGetValue(editor, out var existing))
        {
            _minimaps.Remove(editor);
        }

        var minimap = new MinimapControl(editor);
        _minimaps[editor] = minimap;
        return minimap;
    }

    /// <summary>
    /// Get the minimap for an editor if one exists
    /// </summary>
    public static MinimapControl? GetMinimap(TextEditor editor)
    {
        _minimaps.TryGetValue(editor, out var minimap);
        return minimap;
    }

    /// <summary>
    /// Remove the minimap for an editor
    /// </summary>
    public static void RemoveMinimap(TextEditor editor)
    {
        _minimaps.Remove(editor);
    }
}
