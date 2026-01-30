// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Find and replace functionality.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using WpfColor = System.Windows.Media.Color;
using WpfSize = System.Windows.Size;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Find and Replace control for AvalonEdit, similar to Rider/VS Code
/// </summary>
public partial class FindReplaceControl : UserControl
{
    private TextEditor? _editor;
    private List<SearchResult> _searchResults = new();
    private int _currentResultIndex = -1;
    private SearchHighlightRenderer? _highlightRenderer;

    public FindReplaceControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Attach this control to a TextEditor
    /// </summary>
    public void AttachToEditor(TextEditor editor)
    {
        _editor = editor;

        // Add highlight renderer
        _highlightRenderer = new SearchHighlightRenderer();
        _editor.TextArea.TextView.BackgroundRenderers.Add(_highlightRenderer);

        // Listen for text changes to update highlights
        _editor.TextChanged += (s, e) => UpdateSearch();
    }

    /// <summary>
    /// Show the find bar
    /// </summary>
    public void ShowFind()
    {
        Visibility = Visibility.Visible;
        ExpandReplaceToggle.IsChecked = false;
        ReplaceRow.Visibility = Visibility.Collapsed;
        ExpandArrow.Text = "\u25B6"; // Right arrow

        // Pre-fill with selected text
        if (_editor != null && !string.IsNullOrEmpty(_editor.SelectedText) && !_editor.SelectedText.Contains('\n'))
        {
            SearchTextBox.Text = _editor.SelectedText;
        }

        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    /// <summary>
    /// Show the find and replace bar
    /// </summary>
    public void ShowReplace()
    {
        Visibility = Visibility.Visible;
        ExpandReplaceToggle.IsChecked = true;
        ReplaceRow.Visibility = Visibility.Visible;
        ExpandArrow.Text = "\u25BC"; // Down arrow

        // Pre-fill with selected text
        if (_editor != null && !string.IsNullOrEmpty(_editor.SelectedText) && !_editor.SelectedText.Contains('\n'))
        {
            SearchTextBox.Text = _editor.SelectedText;
        }

        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    /// <summary>
    /// Hide the find bar
    /// </summary>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        ClearHighlights();
        _editor?.Focus();
    }

    #region Event Handlers

    private void ExpandReplaceToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (ExpandReplaceToggle.IsChecked == true)
        {
            ReplaceRow.Visibility = Visibility.Visible;
            ExpandArrow.Text = "\u25BC";
        }
        else
        {
            ReplaceRow.Visibility = Visibility.Collapsed;
            ExpandArrow.Text = "\u25B6";
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearch();
    }

    private void SearchOptions_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSearch();
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                FindPrevious();
            else
                FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void ReplaceTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && Keyboard.Modifiers == ModifierKeys.Shift)
                ReplaceAll();
            else
                ReplaceNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e) => FindPrevious();
    private void NextButton_Click(object sender, RoutedEventArgs e) => FindNext();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();
    private void ReplaceButton_Click(object sender, RoutedEventArgs e) => ReplaceNext();
    private void ReplaceAllButton_Click(object sender, RoutedEventArgs e) => ReplaceAll();

    #endregion

    #region Search Logic

    private void UpdateSearch()
    {
        if (_editor == null || string.IsNullOrEmpty(SearchTextBox.Text))
        {
            _searchResults.Clear();
            _currentResultIndex = -1;
            ResultsCounter.Text = "0 results";
            ClearHighlights();
            return;
        }

        var searchText = SearchTextBox.Text;
        var documentText = _editor.Text;
        var caseSensitive = CaseSensitiveToggle.IsChecked == true;
        var wholeWord = WholeWordToggle.IsChecked == true;
        var useRegex = RegexToggle.IsChecked == true;

        _searchResults.Clear();

        try
        {
            if (useRegex)
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                var pattern = wholeWord ? $@"\b{searchText}\b" : searchText;
                var regex = new Regex(pattern, options);

                foreach (Match match in regex.Matches(documentText))
                {
                    _searchResults.Add(new SearchResult(match.Index, match.Length));
                }
            }
            else
            {
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int index = 0;

                while ((index = documentText.IndexOf(searchText, index, comparison)) >= 0)
                {
                    if (wholeWord)
                    {
                        // Check word boundaries
                        bool startOk = index == 0 || !char.IsLetterOrDigit(documentText[index - 1]);
                        bool endOk = index + searchText.Length >= documentText.Length ||
                                     !char.IsLetterOrDigit(documentText[index + searchText.Length]);

                        if (startOk && endOk)
                        {
                            _searchResults.Add(new SearchResult(index, searchText.Length));
                        }
                    }
                    else
                    {
                        _searchResults.Add(new SearchResult(index, searchText.Length));
                    }
                    index += Math.Max(1, searchText.Length);
                }
            }
        }
        catch (RegexParseException)
        {
            // Invalid regex - ignore
        }

        // Update counter
        ResultsCounter.Text = _searchResults.Count == 0 ? "No results" :
            _searchResults.Count == 1 ? "1 result" : $"{_searchResults.Count} results";

        // Update highlights
        UpdateHighlights();

        // Select first result near cursor
        if (_searchResults.Count > 0)
        {
            var caretOffset = _editor.CaretOffset;
            _currentResultIndex = _searchResults.FindIndex(r => r.Offset >= caretOffset);
            if (_currentResultIndex < 0) _currentResultIndex = 0;
            UpdateResultsCounter();
        }
        else
        {
            _currentResultIndex = -1;
        }
    }

    private void FindNext()
    {
        if (_searchResults.Count == 0) return;

        _currentResultIndex++;
        if (_currentResultIndex >= _searchResults.Count)
            _currentResultIndex = 0;

        SelectCurrentResult();
    }

    private void FindPrevious()
    {
        if (_searchResults.Count == 0) return;

        _currentResultIndex--;
        if (_currentResultIndex < 0)
            _currentResultIndex = _searchResults.Count - 1;

        SelectCurrentResult();
    }

    private void SelectCurrentResult()
    {
        if (_editor == null || _currentResultIndex < 0 || _currentResultIndex >= _searchResults.Count)
            return;

        var result = _searchResults[_currentResultIndex];
        _editor.Select(result.Offset, result.Length);
        _editor.ScrollTo(_editor.Document.GetLineByOffset(result.Offset).LineNumber, 0);

        UpdateHighlights();
        UpdateResultsCounter();
    }

    private void UpdateResultsCounter()
    {
        if (_searchResults.Count == 0)
        {
            ResultsCounter.Text = "No results";
        }
        else
        {
            ResultsCounter.Text = $"{_currentResultIndex + 1} of {_searchResults.Count}";
        }
    }

    private void ReplaceNext()
    {
        if (_editor == null || _searchResults.Count == 0 || _currentResultIndex < 0)
            return;

        var result = _searchResults[_currentResultIndex];
        var replaceText = ReplaceTextBox.Text ?? "";

        _editor.Document.Replace(result.Offset, result.Length, replaceText);

        // Re-search and find next
        UpdateSearch();
        if (_searchResults.Count > 0)
        {
            if (_currentResultIndex >= _searchResults.Count)
                _currentResultIndex = 0;
            SelectCurrentResult();
        }
    }

    private void ReplaceAll()
    {
        if (_editor == null || _searchResults.Count == 0)
            return;

        var replaceText = ReplaceTextBox.Text ?? "";
        int replacementCount = _searchResults.Count;

        // Replace from end to start to preserve offsets
        _editor.Document.BeginUpdate();
        try
        {
            for (int i = _searchResults.Count - 1; i >= 0; i--)
            {
                var result = _searchResults[i];
                _editor.Document.Replace(result.Offset, result.Length, replaceText);
            }
        }
        finally
        {
            _editor.Document.EndUpdate();
        }

        // Update search to show no results
        UpdateSearch();
        ResultsCounter.Text = $"Replaced {replacementCount} occurrences";
    }

    #endregion

    #region Highlighting

    private void UpdateHighlights()
    {
        if (_highlightRenderer == null) return;

        _highlightRenderer.SearchResults = _searchResults;
        _highlightRenderer.CurrentResultIndex = _currentResultIndex;
        _editor?.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    private void ClearHighlights()
    {
        if (_highlightRenderer == null) return;

        _highlightRenderer.SearchResults = new List<SearchResult>();
        _highlightRenderer.CurrentResultIndex = -1;
        _editor?.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    #endregion
}

#region Helper Classes

public class SearchResult
{
    public int Offset { get; }
    public int Length { get; }

    public SearchResult(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }
}

public class SearchHighlightRenderer : IBackgroundRenderer
{
    public List<SearchResult> SearchResults { get; set; } = new();
    public int CurrentResultIndex { get; set; } = -1;

    private static readonly SolidColorBrush HighlightBrush = new(WpfColor.FromArgb(80, 255, 200, 0));
    private static readonly SolidColorBrush CurrentHighlightBrush = new(WpfColor.FromArgb(150, 255, 200, 0));
    private static readonly System.Windows.Media.Pen HighlightPen = new(new SolidColorBrush(WpfColor.FromRgb(200, 160, 0)), 1);

    static SearchHighlightRenderer()
    {
        HighlightBrush.Freeze();
        CurrentHighlightBrush.Freeze();
        HighlightPen.Freeze();
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (SearchResults.Count == 0) return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;

        var viewStart = visualLines[0].FirstDocumentLine.Offset;
        var viewEnd = visualLines[^1].LastDocumentLine.EndOffset;

        for (int i = 0; i < SearchResults.Count; i++)
        {
            var result = SearchResults[i];

            // Skip if not visible
            if (result.Offset + result.Length < viewStart || result.Offset > viewEnd)
                continue;

            var brush = i == CurrentResultIndex ? CurrentHighlightBrush : HighlightBrush;

            var segment = new TextSegment { StartOffset = result.Offset, Length = result.Length };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawRectangle(brush, HighlightPen,
                    new Rect(rect.Location, new WpfSize(rect.Width, rect.Height)));
            }
        }
    }
}

#endregion
