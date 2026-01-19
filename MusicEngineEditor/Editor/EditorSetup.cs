using System;
using System.Collections.Generic;
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
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("MusicEngineEditor.Editor.CSharpScript.xshd");

            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                return;
            }
        }
        catch
        {
            // Fall through to default
        }

        // Fallback to built-in C# highlighting
        editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
    }
}
