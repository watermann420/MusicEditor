// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code editor initialization and setup.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;

namespace MusicEngineEditor.Editor;

public static class EditorSetup
{
    private static FoldingManager? _foldingManager;
    private static CSharpFoldingStrategy? _foldingStrategy;
    private static DispatcherTimer? _foldingUpdateTimer;

    // Track completion providers per editor for cleanup
    private static readonly Dictionary<TextEditor, CompletionProvider> _completionProviders = new();

    // Track inline slider services per editor
    private static readonly Dictionary<TextEditor, InlineSliderService> _sliderServices = new();

    // Track parameter tooltip services per editor
    private static readonly Dictionary<TextEditor, ParameterTooltipService> _tooltipServices = new();

    // Track color picker services per editor
    private static readonly Dictionary<TextEditor, ColorPickerService> _colorPickerServices = new();

    public static void Configure(TextEditor editor)
    {
        // Editor behavior settings
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 4;
        editor.Options.HighlightCurrentLine = true;
        editor.Options.ShowEndOfLine = false;
        editor.Options.ShowSpaces = false;
        editor.Options.ShowTabs = false;
        editor.Options.AllowScrollBelowDocument = true;
        editor.Options.EnableRectangularSelection = true;
        editor.Options.EnableTextDragDrop = true;

        // Visual settings
        editor.ShowLineNumbers = true;
        editor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

        // Current line highlight - subtle but visible (#1E1E1E)
        editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(
            Color.FromRgb(0x1E, 0x1E, 0x1E));
        editor.TextArea.TextView.CurrentLineBorder = new Pen(
            new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)), 1);

        // Load custom syntax highlighting
        LoadSyntaxHighlighting(editor);

        // Setup code folding
        SetupFolding(editor);

        // Setup bracket highlighting
        SetupBracketHighlighting(editor);

        // Setup selection occurrence highlighting
        SetupSelectionHighlighting(editor);

        // Setup auto-indent
        SetupAutoIndent(editor);

        // Boolean toggle on double-click
        editor.TextArea.MouseDown += (s, e) =>
        {
            if (e.ClickCount == 2 && e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                ToggleBooleanAtMouse(editor, e);
            }
        };
    }

    /// <summary>
    /// Setup bracket highlighting for matching brackets
    /// </summary>
    private static void SetupBracketHighlighting(TextEditor editor)
    {
        var renderer = new BracketHighlightRenderer(editor.TextArea.TextView);
        editor.TextArea.TextView.BackgroundRenderers.Add(renderer);
        editor.TextArea.Caret.PositionChanged += (s, e) => renderer.UpdateBrackets(editor);
    }

    /// <summary>
    /// Setup selection occurrence highlighting
    /// </summary>
    private static void SetupSelectionHighlighting(TextEditor editor)
    {
        var renderer = new SelectionOccurrenceRenderer(editor);
        editor.TextArea.TextView.BackgroundRenderers.Add(renderer);
        editor.TextArea.SelectionChanged += (s, e) => renderer.UpdateSelection();
    }

    /// <summary>
    /// Setup auto-indent after { and dedent after }
    /// </summary>
    private static void SetupAutoIndent(TextEditor editor)
    {
        editor.TextArea.TextEntering += (s, e) =>
        {
            if (e.Text == "}")
            {
                // Check if we should dedent
                var line = editor.Document.GetLineByOffset(editor.CaretOffset);
                var lineText = editor.Document.GetText(line.Offset, editor.CaretOffset - line.Offset);
                if (string.IsNullOrWhiteSpace(lineText) && lineText.Length >= 4)
                {
                    // Remove one level of indentation
                    editor.Document.Remove(line.Offset, Math.Min(4, lineText.Length));
                }
            }
        };

        editor.TextArea.TextEntered += (s, e) =>
        {
            if (e.Text == "\n" || e.Text == "\r\n")
            {
                var line = editor.Document.GetLineByNumber(editor.TextArea.Caret.Line - 1);
                if (line.LineNumber > 0)
                {
                    var prevLineText = editor.Document.GetText(line.Offset, line.Length).TrimEnd();
                    if (prevLineText.EndsWith("{"))
                    {
                        // Get current indentation
                        var indent = GetIndentation(editor.Document.GetText(line.Offset, line.Length));
                        // Add one more level
                        editor.Document.Insert(editor.CaretOffset, indent + "    ");
                        editor.CaretOffset = editor.CaretOffset + indent.Length + 4;
                    }
                }
            }
        };
    }

    private static string GetIndentation(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ') count++;
            else if (c == '\t') count += 4;
            else break;
        }
        return new string(' ', count);
    }

    /// <summary>
    /// Setup code completion for the editor.
    /// Call this method to enable intelligent autocomplete for MusicEngine API.
    /// </summary>
    /// <param name="editor">The TextEditor to configure completion for</param>
    /// <returns>The CompletionProvider instance for further configuration if needed</returns>
    public static CompletionProvider SetupCompletion(TextEditor editor)
    {
        // Remove existing provider if any
        if (_completionProviders.TryGetValue(editor, out var existing))
        {
            existing.Detach();
            _completionProviders.Remove(editor);
        }

        // Create and attach new provider
        var provider = new CompletionProvider(editor);
        _completionProviders[editor] = provider;

        return provider;
    }

    /// <summary>
    /// Remove completion provider from an editor
    /// </summary>
    public static void RemoveCompletion(TextEditor editor)
    {
        if (_completionProviders.TryGetValue(editor, out var provider))
        {
            provider.Detach();
            _completionProviders.Remove(editor);
        }
    }

    /// <summary>
    /// Setup inline sliders for numeric literals in the editor.
    /// Allows users to hover over numbers and adjust them via slider controls.
    /// Similar to Strudel.cc's interactive number manipulation.
    /// </summary>
    /// <param name="editor">The TextEditor to configure sliders for</param>
    /// <returns>The InlineSliderService instance for further configuration</returns>
    public static InlineSliderService SetupInlineSliders(TextEditor editor)
    {
        // Remove existing service if any
        if (_sliderServices.TryGetValue(editor, out var existing))
        {
            existing.Dispose();
            _sliderServices.Remove(editor);
        }

        // Create and register new service
        var service = new InlineSliderService(editor);
        _sliderServices[editor] = service;

        // Optionally add visual highlighting for numbers
        // Uncomment the next line to add subtle highlighting to numeric literals
        // editor.TextArea.TextView.BackgroundRenderers.Add(new NumberHighlightRenderer(editor));

        return service;
    }

    /// <summary>
    /// Remove inline slider service from an editor
    /// </summary>
    public static void RemoveInlineSliders(TextEditor editor)
    {
        if (_sliderServices.TryGetValue(editor, out var service))
        {
            service.Dispose();
            _sliderServices.Remove(editor);
        }
    }

    /// <summary>
    /// Get the inline slider service for an editor if one exists
    /// </summary>
    public static InlineSliderService? GetInlineSliderService(TextEditor editor)
    {
        _sliderServices.TryGetValue(editor, out var service);
        return service;
    }

    /// <summary>
    /// Setup parameter tooltips for the editor.
    /// Shows function signatures and parameter descriptions on hover.
    /// </summary>
    public static ParameterTooltipService SetupParameterTooltips(TextEditor editor)
    {
        // Remove existing service if any
        if (_tooltipServices.TryGetValue(editor, out var existing))
        {
            existing.Dispose();
            _tooltipServices.Remove(editor);
        }

        // Create and register new service
        var service = new ParameterTooltipService(editor);
        _tooltipServices[editor] = service;

        return service;
    }

    /// <summary>
    /// Remove parameter tooltip service from an editor
    /// </summary>
    public static void RemoveParameterTooltips(TextEditor editor)
    {
        if (_tooltipServices.TryGetValue(editor, out var service))
        {
            service.Dispose();
            _tooltipServices.Remove(editor);
        }
    }

    /// <summary>
    /// Setup color picker for the editor.
    /// Ctrl+Click on color values to show a color picker popup.
    /// Supports hex colors (#RRGGBB, #AARRGGBB) and Color.FromRgb/FromArgb calls.
    /// </summary>
    public static ColorPickerService SetupColorPicker(TextEditor editor)
    {
        // Remove existing service if any
        if (_colorPickerServices.TryGetValue(editor, out var existing))
        {
            existing.Dispose();
            _colorPickerServices.Remove(editor);
        }

        // Create and register new service
        var service = new ColorPickerService(editor);
        _colorPickerServices[editor] = service;

        return service;
    }

    /// <summary>
    /// Remove color picker service from an editor
    /// </summary>
    public static void RemoveColorPicker(TextEditor editor)
    {
        if (_colorPickerServices.TryGetValue(editor, out var service))
        {
            service.Dispose();
            _colorPickerServices.Remove(editor);
        }
    }

    public static void SetupFolding(TextEditor editor)
    {
        // Initialize folding manager
        _foldingManager = FoldingManager.Install(editor.TextArea);
        _foldingStrategy = new CSharpFoldingStrategy();

        // Style the folding margin
        var foldingMargin = editor.TextArea.LeftMargins.OfType<FoldingMargin>().FirstOrDefault();
        if (foldingMargin != null)
        {
            foldingMargin.FoldingMarkerBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
            foldingMargin.FoldingMarkerBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
            foldingMargin.SelectedFoldingMarkerBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            foldingMargin.SelectedFoldingMarkerBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41));
        }

        // Update foldings when text changes (with debounce)
        _foldingUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _foldingUpdateTimer.Tick += (s, e) =>
        {
            _foldingUpdateTimer.Stop();
            UpdateFoldings(editor);
        };

        editor.TextChanged += (s, e) =>
        {
            _foldingUpdateTimer.Stop();
            _foldingUpdateTimer.Start();
        };

        // Initial folding update
        UpdateFoldings(editor);
    }

    private static void UpdateFoldings(TextEditor editor)
    {
        if (_foldingManager == null || _foldingStrategy == null) return;

        try
        {
            var foldings = _foldingStrategy.CreateFoldings(editor.Document);
            _foldingManager.UpdateFoldings(foldings, -1);
        }
        catch
        {
            // Ignore folding errors
        }
    }

    /// <summary>
    /// Fold all code blocks
    /// </summary>
    public static void FoldAll()
    {
        if (_foldingManager == null) return;
        foreach (var folding in _foldingManager.AllFoldings)
        {
            folding.IsFolded = true;
        }
    }

    /// <summary>
    /// Unfold all code blocks
    /// </summary>
    public static void UnfoldAll()
    {
        if (_foldingManager == null) return;
        foreach (var folding in _foldingManager.AllFoldings)
        {
            folding.IsFolded = false;
        }
    }

    public static void LoadSyntaxHighlighting(TextEditor editor)
    {
        // Always use programmatic Rider-like highlighting
        editor.SyntaxHighlighting = CreateRiderHighlighting();
    }

    private static IHighlightingDefinition CreateRiderHighlighting()
    {
        // Modern DAW-Inspired Theme Colors (Ableton/FL Studio/Bitwig style)
        // Using brighter, more vibrant colors for better visibility
        const string xshd = """
            <?xml version="1.0"?>
            <SyntaxDefinition name="CSharpScript" extensions=".csx;.cs"
                xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">

                <!-- DAW-Inspired Vibrant Colors - High Contrast -->
                <Color name="Comment" foreground="#7C8A7C" fontStyle="italic" />
                <Color name="String" foreground="#FFA657" />
                <Color name="Char" foreground="#FFA657" />
                <Color name="Number" foreground="#BD93F9" />
                <Color name="Preprocessor" foreground="#FFD700" />
                <Color name="Keyword" foreground="#56D4FF" fontWeight="bold" />
                <Color name="BuiltinType" foreground="#56D4FF" fontWeight="bold" />
                <Color name="ValueKeyword" foreground="#FF79C6" fontWeight="bold" />
                <Color name="Type" foreground="#8BE9FD" />
                <Color name="Method" foreground="#50FA7B" />
                <Color name="Punctuation" foreground="#F8F8F2" />

                <RuleSet ignoreCase="false">
                    <!-- Comments first (highest priority) -->
                    <Span color="Comment" begin="///"/>
                    <Span color="Comment" begin="//"/>
                    <Span color="Comment" multiline="true" begin="/\*" end="\*/"/>

                    <!-- Preprocessor -->
                    <Span color="Preprocessor" begin="#"/>

                    <!-- Strings - with proper escaping -->
                    <Span color="String" multiline="true">
                        <Begin>@"</Begin>
                        <End>"</End>
                    </Span>
                    <Span color="String">
                        <Begin>\$"</Begin>
                        <End>"</End>
                        <RuleSet>
                            <Span begin="\\" end="."/>
                        </RuleSet>
                    </Span>
                    <Span color="String">
                        <Begin>"</Begin>
                        <End>"</End>
                        <RuleSet>
                            <Span begin="\\" end="."/>
                        </RuleSet>
                    </Span>
                    <Span color="Char">
                        <Begin>'</Begin>
                        <End>'</End>
                        <RuleSet>
                            <Span begin="\\" end="."/>
                        </RuleSet>
                    </Span>

                    <!-- Value Keywords (true, false, null) - Pink/Magenta -->
                    <Keywords color="ValueKeyword">
                        <Word>true</Word>
                        <Word>false</Word>
                        <Word>null</Word>
                    </Keywords>

                    <!-- Built-in Types (same as keywords but grouped) -->
                    <Keywords color="BuiltinType">
                        <Word>int</Word>
                        <Word>uint</Word>
                        <Word>long</Word>
                        <Word>ulong</Word>
                        <Word>short</Word>
                        <Word>ushort</Word>
                        <Word>byte</Word>
                        <Word>sbyte</Word>
                        <Word>float</Word>
                        <Word>double</Word>
                        <Word>decimal</Word>
                        <Word>bool</Word>
                        <Word>char</Word>
                        <Word>string</Word>
                        <Word>object</Word>
                        <Word>void</Word>
                        <Word>dynamic</Word>
                        <Word>var</Word>
                        <Word>nint</Word>
                        <Word>nuint</Word>
                    </Keywords>

                    <!-- Control Flow Keywords -->
                    <Keywords color="Keyword">
                        <Word>if</Word>
                        <Word>else</Word>
                        <Word>switch</Word>
                        <Word>case</Word>
                        <Word>default</Word>
                        <Word>for</Word>
                        <Word>foreach</Word>
                        <Word>while</Word>
                        <Word>do</Word>
                        <Word>break</Word>
                        <Word>continue</Word>
                        <Word>return</Word>
                        <Word>throw</Word>
                        <Word>try</Word>
                        <Word>catch</Word>
                        <Word>finally</Word>
                        <Word>goto</Word>
                        <Word>yield</Word>
                        <Word>await</Word>
                        <Word>when</Word>
                        <Word>and</Word>
                        <Word>or</Word>
                        <Word>not</Word>
                        <Word>class</Word>
                        <Word>struct</Word>
                        <Word>record</Word>
                        <Word>interface</Word>
                        <Word>enum</Word>
                        <Word>delegate</Word>
                        <Word>event</Word>
                        <Word>namespace</Word>
                        <Word>public</Word>
                        <Word>private</Word>
                        <Word>protected</Word>
                        <Word>internal</Word>
                        <Word>static</Word>
                        <Word>readonly</Word>
                        <Word>const</Word>
                        <Word>volatile</Word>
                        <Word>async</Word>
                        <Word>virtual</Word>
                        <Word>override</Word>
                        <Word>abstract</Word>
                        <Word>sealed</Word>
                        <Word>extern</Word>
                        <Word>unsafe</Word>
                        <Word>partial</Word>
                        <Word>new</Word>
                        <Word>ref</Word>
                        <Word>out</Word>
                        <Word>in</Word>
                        <Word>params</Word>
                        <Word>this</Word>
                        <Word>base</Word>
                        <Word>using</Word>
                        <Word>lock</Word>
                        <Word>fixed</Word>
                        <Word>checked</Word>
                        <Word>unchecked</Word>
                        <Word>stackalloc</Word>
                        <Word>implicit</Word>
                        <Word>explicit</Word>
                        <Word>operator</Word>
                        <Word>init</Word>
                        <Word>required</Word>
                        <Word>file</Word>
                        <Word>scoped</Word>
                        <Word>global</Word>
                        <Word>nameof</Word>
                        <Word>typeof</Word>
                        <Word>sizeof</Word>
                        <Word>is</Word>
                        <Word>as</Word>
                        <Word>where</Word>
                        <Word>select</Word>
                        <Word>from</Word>
                        <Word>orderby</Word>
                        <Word>ascending</Word>
                        <Word>descending</Word>
                        <Word>group</Word>
                        <Word>by</Word>
                        <Word>into</Word>
                        <Word>join</Word>
                        <Word>on</Word>
                        <Word>equals</Word>
                        <Word>let</Word>
                        <Word>with</Word>
                        <Word>get</Word>
                        <Word>set</Word>
                        <Word>add</Word>
                        <Word>remove</Word>
                        <Word>value</Word>
                    </Keywords>

                    <!-- .NET Types (Light Cyan) -->
                    <Keywords color="Type">
                        <Word>String</Word>
                        <Word>Int32</Word>
                        <Word>Int64</Word>
                        <Word>Double</Word>
                        <Word>Single</Word>
                        <Word>Boolean</Word>
                        <Word>Object</Word>
                        <Word>List</Word>
                        <Word>Dictionary</Word>
                        <Word>HashSet</Word>
                        <Word>Array</Word>
                        <Word>Task</Word>
                        <Word>Action</Word>
                        <Word>Func</Word>
                        <Word>Exception</Word>
                        <Word>Console</Word>
                        <Word>Math</Word>
                        <Word>Random</Word>
                        <Word>DateTime</Word>
                        <Word>TimeSpan</Word>
                        <Word>Guid</Word>
                        <Word>IEnumerable</Word>
                        <Word>IList</Word>
                        <Word>IDictionary</Word>
                        <Word>IDisposable</Word>
                    </Keywords>

                    <!-- MusicEngine Types (Light Cyan) -->
                    <Keywords color="Type">
                        <Word>AudioEngine</Word>
                        <Word>Sequencer</Word>
                        <Word>Engine</Word>
                        <Word>SimpleSynth</Word>
                        <Word>PolySynth</Word>
                        <Word>FMSynth</Word>
                        <Word>WavetableSynth</Word>
                        <Word>GranularSynth</Word>
                        <Word>Pattern</Word>
                        <Word>NoteEvent</Word>
                        <Word>Track</Word>
                        <Word>VstPlugin</Word>
                        <Word>VstHost</Word>
                        <Word>EffectChain</Word>
                        <Word>ReverbEffect</Word>
                        <Word>DelayEffect</Word>
                        <Word>ChorusEffect</Word>
                        <Word>CompressorEffect</Word>
                        <Word>FilterEffect</Word>
                        <Word>Envelope</Word>
                        <Word>LFO</Word>
                        <Word>Oscillator</Word>
                        <Word>WaveType</Word>
                        <Word>MidiDevice</Word>
                        <Word>MidiMessage</Word>
                    </Keywords>

                    <!-- Methods (Green) -->
                    <Keywords color="Method">
                        <Word>CreateSynth</Word>
                        <Word>CreatePattern</Word>
                        <Word>NoteOn</Word>
                        <Word>NoteOff</Word>
                        <Word>Start</Word>
                        <Word>Stop</Word>
                        <Word>SetBpm</Word>
                        <Word>StartPattern</Word>
                        <Word>StopPattern</Word>
                        <Word>LoadVst</Word>
                        <Word>Print</Word>
                        <Word>PlayNote</Word>
                        <Word>AddEffect</Word>
                        <Word>SetVolume</Word>
                        <Word>SetPan</Word>
                        <Word>WriteLine</Word>
                        <Word>ReadLine</Word>
                        <Word>ToString</Word>
                        <Word>Parse</Word>
                        <Word>TryParse</Word>
                        <Word>GetType</Word>
                        <Word>Equals</Word>
                        <Word>GetHashCode</Word>
                    </Keywords>

                    <!-- Numbers (Purple) - Fixed regex without spaces -->
                    <Rule color="Number">\b0[xX][0-9a-fA-F_]+[uUlL]*\b</Rule>
                    <Rule color="Number">\b0[bB][01_]+[uUlL]*\b</Rule>
                    <Rule color="Number">\b[0-9]+\.?[0-9]*([eE][+-]?[0-9]+)?[fFdDmMlLuU]?\b</Rule>
                </RuleSet>
            </SyntaxDefinition>
            """;

        using var reader = new XmlTextReader(new StringReader(xshd));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static void ToggleBooleanAtMouse(TextEditor editor, System.Windows.Input.MouseButtonEventArgs e)
    {
        var textView = editor.TextArea.TextView;
        var pos = textView.GetPositionFloor(e.GetPosition(textView) + textView.ScrollOffset);
        if (pos == null) return;

        int offset = editor.Document.GetOffset(pos.Value.Location);
        if (offset < 0 || offset >= editor.Document.TextLength) return;

        var word = GetWordAt(editor.Document, offset);
        if (word == null) return;

        var text = editor.Document.GetText(word);
        string replacement = text switch
        {
            "true" => "false",
            "false" => "true",
            "True" => "False",
            "False" => "True",
            _ => text
        };

        if (replacement != text)
        {
            editor.Document.Replace(word, replacement);
            e.Handled = true;
        }
    }

    private static TextSegment? GetWordAt(TextDocument doc, int offset)
    {
        var text = doc.Text;
        if (offset < 0 || offset >= text.Length) return null;

        int start = offset;
        while (start > 0 && char.IsLetter(text[start - 1])) start--;
        int end = offset;
        while (end < text.Length && char.IsLetter(text[end])) end++;
        if (end <= start) return null;
        return new TextSegment { StartOffset = start, Length = end - start };
    }
}

/// <summary>
/// Highlights matching brackets when cursor is next to one
/// </summary>
public class BracketHighlightRenderer : IBackgroundRenderer
{
    private static readonly Color BracketHighlightColor = Color.FromArgb(60, 0, 217, 255); // #00D9FF with alpha
    private readonly TextView _textView;
    private int _openBracketOffset = -1;
    private int _closeBracketOffset = -1;

    private static readonly Dictionary<char, char> BracketPairs = new()
    {
        { '(', ')' }, { ')', '(' },
        { '{', '}' }, { '}', '{' },
        { '[', ']' }, { ']', '[' }
    };

    public BracketHighlightRenderer(TextView textView)
    {
        _textView = textView;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void UpdateBrackets(TextEditor editor)
    {
        _openBracketOffset = -1;
        _closeBracketOffset = -1;

        var offset = editor.CaretOffset;
        var document = editor.Document;
        if (document == null || offset < 0 || offset > document.TextLength) return;

        // Check character at cursor and before cursor
        char? charAtCursor = offset < document.TextLength ? document.GetCharAt(offset) : null;
        char? charBefore = offset > 0 ? document.GetCharAt(offset - 1) : null;

        int bracketOffset = -1;
        char bracket = '\0';

        if (charAtCursor.HasValue && BracketPairs.ContainsKey(charAtCursor.Value))
        {
            bracketOffset = offset;
            bracket = charAtCursor.Value;
        }
        else if (charBefore.HasValue && BracketPairs.ContainsKey(charBefore.Value))
        {
            bracketOffset = offset - 1;
            bracket = charBefore.Value;
        }

        if (bracketOffset >= 0)
        {
            var matchingOffset = FindMatchingBracket(document, bracketOffset, bracket);
            if (matchingOffset >= 0)
            {
                _openBracketOffset = bracketOffset;
                _closeBracketOffset = matchingOffset;
            }
        }

        _textView.InvalidateLayer(Layer);
    }

    private int FindMatchingBracket(IDocument document, int offset, char bracket)
    {
        char matchingBracket = BracketPairs[bracket];
        bool isOpening = bracket == '(' || bracket == '{' || bracket == '[';
        int direction = isOpening ? 1 : -1;
        int depth = 1;
        int pos = offset + direction;

        while (pos >= 0 && pos < document.TextLength)
        {
            char c = document.GetCharAt(pos);
            if (c == bracket) depth++;
            else if (c == matchingBracket) depth--;

            if (depth == 0) return pos;
            pos += direction;
        }

        return -1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_openBracketOffset < 0 || _closeBracketOffset < 0) return;

        var builder = new BackgroundGeometryBuilder { CornerRadius = 2 };

        foreach (var offset in new[] { _openBracketOffset, _closeBracketOffset })
        {
            var segment = new TextSegment { StartOffset = offset, Length = 1 };
            builder.AddSegment(textView, segment);
        }

        var geometry = builder.CreateGeometry();
        if (geometry != null)
        {
            drawingContext.DrawGeometry(new SolidColorBrush(BracketHighlightColor), null, geometry);
        }
    }
}

/// <summary>
/// Highlights all occurrences of selected text
/// </summary>
public class SelectionOccurrenceRenderer : IBackgroundRenderer
{
    private static readonly Color OccurrenceHighlightColor = Color.FromArgb(40, 0, 217, 255); // Subtle cyan
    private readonly TextEditor _editor;
    private readonly List<TextSegment> _occurrences = new();

    public SelectionOccurrenceRenderer(TextEditor editor)
    {
        _editor = editor;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void UpdateSelection()
    {
        _occurrences.Clear();

        var selection = _editor.TextArea.Selection;
        if (selection.IsEmpty)
        {
            _editor.TextArea.TextView.InvalidateLayer(Layer);
            return;
        }

        var selectedText = _editor.SelectedText;
        if (string.IsNullOrWhiteSpace(selectedText) || selectedText.Length < 2)
        {
            _editor.TextArea.TextView.InvalidateLayer(Layer);
            return;
        }

        // Find all occurrences
        var text = _editor.Text;
        int index = 0;
        while ((index = text.IndexOf(selectedText, index, StringComparison.Ordinal)) >= 0)
        {
            _occurrences.Add(new TextSegment { StartOffset = index, Length = selectedText.Length });
            index += selectedText.Length;
        }

        _editor.TextArea.TextView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_occurrences.Count <= 1) return; // Don't highlight if only the selection itself

        var builder = new BackgroundGeometryBuilder { CornerRadius = 2 };

        foreach (var segment in _occurrences)
        {
            builder.AddSegment(textView, segment);
        }

        var geometry = builder.CreateGeometry();
        if (geometry != null)
        {
            drawingContext.DrawGeometry(new SolidColorBrush(OccurrenceHighlightColor), null, geometry);
        }
    }
}
