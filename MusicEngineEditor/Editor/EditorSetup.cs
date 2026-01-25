using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

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

        // Current line highlight
        editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(
            Color.FromArgb(30, 255, 255, 255));
        editor.TextArea.TextView.CurrentLineBorder = new Pen(
            new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1);

        // Load custom syntax highlighting
        LoadSyntaxHighlighting(editor);

        // Setup code folding
        SetupFolding(editor);
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
            foldingMargin.SelectedFoldingMarkerBrush = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4));
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

    public static void LoadSyntaxHighlighting(TextEditor editor)
    {
        // Always use programmatic Rider-like highlighting
        editor.SyntaxHighlighting = CreateRiderHighlighting();
    }

    private static IHighlightingDefinition CreateRiderHighlighting()
    {
        // Rider/IntelliJ Darcula Theme Colors
        const string xshd = """
            <?xml version="1.0"?>
            <SyntaxDefinition name="CSharpScript" extensions=".csx;.cs"
                xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">

                <!-- Bright Rider-like Colors -->
                <Color name="Comment" foreground="#808080" fontStyle="italic" />
                <Color name="String" foreground="#E891B8" />
                <Color name="Char" foreground="#E891B8" />
                <Color name="Number" foreground="#79C0FF" />
                <Color name="Preprocessor" foreground="#D4D46B" />
                <Color name="Keyword" foreground="#FF9E4F" fontWeight="bold" />
                <Color name="Type" foreground="#6CDCF7" />
                <Color name="Method" foreground="#FFD980" />

                <RuleSet ignoreCase="false">
                    <!-- Comments (green, italic) -->
                    <Span color="Comment" begin="///" />
                    <Span color="Comment" begin="//" />
                    <Span color="Comment" multiline="true" begin="/\*" end="\*/" />

                    <!-- Preprocessor (yellow) -->
                    <Span color="Preprocessor" begin="#" />

                    <!-- Strings (green) -->
                    <Span color="String" multiline="true" begin="@&quot;" end="&quot;" />
                    <Span color="String" begin="\$&quot;" end="&quot;" />
                    <Span color="String" begin="&quot;" end="&quot;" />
                    <Span color="Char" begin="'" end="'" />

                    <!-- Keywords (orange, bold) -->
                    <Keywords color="Keyword">
                        <Word>if</Word><Word>else</Word><Word>switch</Word><Word>case</Word><Word>default</Word>
                        <Word>for</Word><Word>foreach</Word><Word>while</Word><Word>do</Word>
                        <Word>break</Word><Word>continue</Word><Word>return</Word><Word>throw</Word>
                        <Word>try</Word><Word>catch</Word><Word>finally</Word><Word>goto</Word>
                        <Word>yield</Word><Word>await</Word><Word>when</Word><Word>and</Word><Word>or</Word><Word>not</Word>
                        <Word>class</Word><Word>struct</Word><Word>record</Word><Word>interface</Word>
                        <Word>enum</Word><Word>delegate</Word><Word>event</Word><Word>namespace</Word>
                        <Word>public</Word><Word>private</Word><Word>protected</Word><Word>internal</Word>
                        <Word>static</Word><Word>readonly</Word><Word>const</Word><Word>volatile</Word>
                        <Word>async</Word><Word>virtual</Word><Word>override</Word><Word>abstract</Word>
                        <Word>sealed</Word><Word>extern</Word><Word>unsafe</Word><Word>partial</Word>
                        <Word>new</Word><Word>ref</Word><Word>out</Word><Word>in</Word><Word>params</Word>
                        <Word>this</Word><Word>base</Word><Word>using</Word><Word>lock</Word><Word>fixed</Word>
                        <Word>checked</Word><Word>unchecked</Word><Word>stackalloc</Word>
                        <Word>implicit</Word><Word>explicit</Word><Word>operator</Word>
                        <Word>init</Word><Word>required</Word><Word>file</Word><Word>scoped</Word><Word>global</Word>
                        <Word>var</Word><Word>nameof</Word><Word>typeof</Word><Word>sizeof</Word>
                        <Word>is</Word><Word>as</Word><Word>where</Word><Word>select</Word><Word>from</Word>
                        <Word>orderby</Word><Word>ascending</Word><Word>descending</Word>
                        <Word>group</Word><Word>by</Word><Word>into</Word><Word>join</Word><Word>on</Word>
                        <Word>equals</Word><Word>let</Word><Word>with</Word>
                        <Word>true</Word><Word>false</Word><Word>null</Word>
                        <Word>int</Word><Word>uint</Word><Word>long</Word><Word>ulong</Word>
                        <Word>short</Word><Word>ushort</Word><Word>byte</Word><Word>sbyte</Word>
                        <Word>float</Word><Word>double</Word><Word>decimal</Word><Word>bool</Word>
                        <Word>char</Word><Word>string</Word><Word>object</Word><Word>void</Word>
                        <Word>dynamic</Word><Word>nint</Word><Word>nuint</Word>
                    </Keywords>

                    <!-- Types (teal/cyan) -->
                    <Keywords color="Type">
                        <Word>String</Word><Word>Int32</Word><Word>Int64</Word><Word>Double</Word>
                        <Word>Single</Word><Word>Boolean</Word><Word>Object</Word><Word>List</Word>
                        <Word>Dictionary</Word><Word>HashSet</Word><Word>Array</Word><Word>Task</Word>
                        <Word>Action</Word><Word>Func</Word><Word>Exception</Word><Word>Console</Word>
                        <Word>Math</Word><Word>Random</Word><Word>DateTime</Word><Word>TimeSpan</Word>
                        <Word>IEnumerable</Word><Word>IList</Word><Word>IDictionary</Word><Word>IDisposable</Word>
                        <Word>AudioEngine</Word><Word>Sequencer</Word><Word>Engine</Word>
                        <Word>SimpleSynth</Word><Word>PolySynth</Word><Word>FMSynth</Word>
                        <Word>WavetableSynth</Word><Word>GranularSynth</Word><Word>Pattern</Word>
                        <Word>NoteEvent</Word><Word>Track</Word><Word>VstPlugin</Word><Word>VstHost</Word>
                        <Word>EffectChain</Word><Word>ReverbEffect</Word><Word>DelayEffect</Word>
                        <Word>ChorusEffect</Word><Word>CompressorEffect</Word><Word>FilterEffect</Word>
                        <Word>Envelope</Word><Word>LFO</Word><Word>Oscillator</Word><Word>WaveType</Word>
                    </Keywords>

                    <!-- Methods (yellow) -->
                    <Keywords color="Method">
                        <Word>CreateSynth</Word><Word>CreatePattern</Word><Word>NoteOn</Word><Word>NoteOff</Word>
                        <Word>Start</Word><Word>Stop</Word><Word>SetBpm</Word><Word>StartPattern</Word>
                        <Word>StopPattern</Word><Word>LoadVst</Word><Word>Print</Word><Word>PlayNote</Word>
                        <Word>AddEffect</Word><Word>SetVolume</Word><Word>SetPan</Word>
                        <Word>WriteLine</Word><Word>ReadLine</Word><Word>ToString</Word><Word>Parse</Word>
                        <Word>TryParse</Word><Word>GetType</Word><Word>Equals</Word><Word>GetHashCode</Word>
                    </Keywords>

                    <!-- Numbers (blue) -->
                    <Rule color="Number">
                        \b0[xX][0-9a-fA-F_]+[uUlL]*\b |
                        \b0[bB][01_]+[uUlL]*\b |
                        \b[0-9][0-9_]*\.?[0-9_]*([eE][+-]?[0-9_]+)?[fFdDmMlLuU]*\b
                    </Rule>
                </RuleSet>
            </SyntaxDefinition>
            """;

        using var reader = new XmlTextReader(new StringReader(xshd));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
