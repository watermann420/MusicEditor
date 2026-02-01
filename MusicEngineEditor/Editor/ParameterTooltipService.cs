// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Parameter tooltips on hover for function calls.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Provides parameter tooltips when hovering over function calls in the editor.
/// Shows function signature and parameter descriptions.
/// </summary>
public class ParameterTooltipService : IDisposable
{
    private readonly TextEditor _editor;
    private readonly Popup _tooltipPopup;
    private readonly Border _tooltipBorder;
    private readonly StackPanel _tooltipPanel;
    private readonly TextBlock _signatureText;
    private readonly TextBlock _descriptionText;
    private bool _isDisposed;

    // MusicEngine API function signatures
    private static readonly Dictionary<string, FunctionInfo> FunctionSignatures = new()
    {
        // Synth creation
        ["CreateSynth"] = new("SimpleSynth CreateSynth(string name, WaveType wave = WaveType.Saw)",
            "Creates a simple monophonic synthesizer with the specified waveform.",
            new[] { ("name", "Unique identifier for the synth"), ("wave", "Oscillator waveform type (Sine, Saw, Square, Triangle)") }),

        ["PolySynth"] = new("PolySynth PolySynth(string name, int voices = 8)",
            "Creates a polyphonic synthesizer with multiple voices.",
            new[] { ("name", "Unique identifier for the synth"), ("voices", "Maximum number of simultaneous notes (1-32)") }),

        ["FMSynth"] = new("FMSynth FMSynth(string name, float ratio = 2.0f, float index = 1.0f)",
            "Creates an FM synthesis synth with carrier/modulator.",
            new[] { ("name", "Unique identifier"), ("ratio", "Frequency ratio between carrier and modulator"), ("index", "Modulation depth") }),

        // Pattern functions
        ["CreatePattern"] = new("Pattern CreatePattern(string name, int bars = 4)",
            "Creates a pattern container for sequencing notes.",
            new[] { ("name", "Pattern identifier"), ("bars", "Length in bars (1-64)") }),

        ["NoteOn"] = new("void NoteOn(int note, float velocity = 0.8f, int channel = 0)",
            "Triggers a note with MIDI note number.",
            new[] { ("note", "MIDI note number (0-127, e.g., 60 = C4)"), ("velocity", "Note velocity (0.0-1.0)"), ("channel", "MIDI channel (0-15)") }),

        ["NoteOff"] = new("void NoteOff(int note, int channel = 0)",
            "Releases a note.",
            new[] { ("note", "MIDI note number to release"), ("channel", "MIDI channel") }),

        ["PlayNote"] = new("void PlayNote(string note, float duration = 0.25f, float velocity = 0.8f)",
            "Plays a note for the specified duration.",
            new[] { ("note", "Note name (e.g., \"C4\", \"F#3\", \"Bb5\")"), ("duration", "Duration in beats"), ("velocity", "Note velocity (0.0-1.0)") }),

        // Engine control
        ["SetBpm"] = new("void SetBpm(float bpm)",
            "Sets the tempo in beats per minute.",
            new[] { ("bpm", "Tempo (20-999 BPM)") }),

        ["Start"] = new("void Start()",
            "Starts the audio engine and pattern playback.", Array.Empty<(string, string)>()),

        ["Stop"] = new("void Stop()",
            "Stops the audio engine.", Array.Empty<(string, string)>()),

        ["StartPattern"] = new("void StartPattern(string name, bool loop = true)",
            "Starts playing a pattern.",
            new[] { ("name", "Pattern identifier"), ("loop", "Whether to loop the pattern") }),

        ["StopPattern"] = new("void StopPattern(string name)",
            "Stops a playing pattern.",
            new[] { ("name", "Pattern identifier") }),

        // Effects
        ["AddEffect"] = new("void AddEffect(string effectType, params object[] args)",
            "Adds an effect to the effect chain.",
            new[] { ("effectType", "Effect type (Reverb, Delay, Chorus, Compressor, Filter)"), ("args", "Effect-specific parameters") }),

        ["SetVolume"] = new("void SetVolume(float volume)",
            "Sets the master volume.",
            new[] { ("volume", "Volume level (0.0-1.0)") }),

        ["SetPan"] = new("void SetPan(float pan)",
            "Sets the stereo panning.",
            new[] { ("pan", "Pan position (-1.0 = left, 0.0 = center, 1.0 = right)") }),

        // VST
        ["LoadVst"] = new("VstPlugin LoadVst(string path)",
            "Loads a VST plugin from the specified path.",
            new[] { ("path", "Full path to the .dll VST plugin file") }),

        // MIDI
        ["MidiDevice"] = new("MidiDevice MidiDevice(string name)",
            "Opens a MIDI device by name.",
            new[] { ("name", "MIDI device name or partial match") }),

        // Utility
        ["Print"] = new("void Print(object message)",
            "Outputs a message to the console.",
            new[] { ("message", "Text or value to display") }),

        // Common .NET
        ["WriteLine"] = new("void Console.WriteLine(object value)",
            "Writes text followed by a newline to the output.",
            new[] { ("value", "Value to write") }),

        ["Sleep"] = new("void Thread.Sleep(int milliseconds)",
            "Pauses execution for the specified time.",
            new[] { ("milliseconds", "Time to sleep in milliseconds") }),

        ["Random"] = new("int Random(int min, int max)",
            "Returns a random integer between min and max.",
            new[] { ("min", "Minimum value (inclusive)"), ("max", "Maximum value (exclusive)") }),
    };

    public ParameterTooltipService(TextEditor editor)
    {
        _editor = editor;

        // Create tooltip popup
        _tooltipPopup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.Mouse,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade
        };

        // Create tooltip content
        _tooltipBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            MaxWidth = 500
        };

        _tooltipPanel = new StackPanel { Orientation = Orientation.Vertical };

        _signatureText = new TextBlock
        {
            FontFamily = new FontFamily("JetBrains Mono, Consolas, Courier New"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0xD4, 0xFF)), // Cyan for signature
            TextWrapping = TextWrapping.Wrap
        };

        _descriptionText = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };

        _tooltipPanel.Children.Add(_signatureText);
        _tooltipPanel.Children.Add(_descriptionText);
        _tooltipBorder.Child = _tooltipPanel;
        _tooltipPopup.Child = _tooltipBorder;

        // Attach to editor events
        _editor.TextArea.TextView.MouseHover += OnMouseHover;
        _editor.TextArea.TextView.MouseHoverStopped += OnMouseHoverStopped;
        _editor.TextArea.TextView.MouseMove += OnMouseMove;
    }

    private void OnMouseHover(object sender, MouseEventArgs e)
    {
        var pos = _editor.TextArea.TextView.GetPositionFloor(
            e.GetPosition(_editor.TextArea.TextView) + _editor.TextArea.TextView.ScrollOffset);

        if (pos == null) return;

        int offset = _editor.Document.GetOffset(pos.Value.Location);
        if (offset < 0 || offset >= _editor.Document.TextLength) return;

        // Try to find a function name at or near the cursor
        var functionInfo = GetFunctionInfoAtOffset(offset);
        if (functionInfo != null)
        {
            ShowTooltip(functionInfo.Value.info, functionInfo.Value.paramIndex);
        }
    }

    private void OnMouseHoverStopped(object sender, MouseEventArgs e)
    {
        _tooltipPopup.IsOpen = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_tooltipPopup.IsOpen)
        {
            _tooltipPopup.IsOpen = false;
        }
    }

    private (FunctionInfo info, int paramIndex)? GetFunctionInfoAtOffset(int offset)
    {
        var text = _editor.Text;
        if (string.IsNullOrEmpty(text)) return null;

        // First, check if we're inside a function call (between parentheses)
        int parenDepth = 0;
        int functionStart = -1;
        int currentParam = 0;

        // Scan backwards to find the opening paren
        for (int i = offset - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c == ')')
                parenDepth++;
            else if (c == '(')
            {
                if (parenDepth == 0)
                {
                    functionStart = i;
                    break;
                }
                parenDepth--;
            }
            else if (c == ',' && parenDepth == 0)
            {
                currentParam++;
            }
            else if (c == ';' || c == '{' || c == '}')
            {
                break; // Stop at statement boundaries
            }
        }

        // If we found an opening paren, extract the function name
        if (functionStart > 0)
        {
            int nameEnd = functionStart;
            int nameStart = nameEnd - 1;

            // Skip whitespace
            while (nameStart >= 0 && char.IsWhiteSpace(text[nameStart]))
                nameStart--;

            nameEnd = nameStart + 1;

            // Extract identifier
            while (nameStart >= 0 && (char.IsLetterOrDigit(text[nameStart]) || text[nameStart] == '_'))
                nameStart--;

            nameStart++;

            if (nameStart < nameEnd)
            {
                string functionName = text.Substring(nameStart, nameEnd - nameStart);
                if (FunctionSignatures.TryGetValue(functionName, out var info))
                {
                    return (info, currentParam);
                }
            }
        }

        // Try to get word at cursor position (for hovering over function name itself)
        var word = GetWordAtOffset(offset);
        if (word != null && FunctionSignatures.TryGetValue(word, out var directInfo))
        {
            return (directInfo, -1);
        }

        return null;
    }

    private string? GetWordAtOffset(int offset)
    {
        var text = _editor.Text;
        if (offset < 0 || offset >= text.Length) return null;

        int start = offset;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
            start--;

        int end = offset;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
            end++;

        if (end <= start) return null;
        return text.Substring(start, end - start);
    }

    private void ShowTooltip(FunctionInfo info, int highlightParamIndex)
    {
        _signatureText.Inlines.Clear();

        // Build the signature with parameter highlighting
        var signature = info.Signature;
        _signatureText.Text = signature;

        // Build description with parameters
        var description = info.Description;
        if (info.Parameters.Length > 0)
        {
            description += "\n\nParameters:";
            for (int i = 0; i < info.Parameters.Length; i++)
            {
                var (name, desc) = info.Parameters[i];
                var prefix = i == highlightParamIndex ? "→ " : "  ";
                description += $"\n{prefix}{name}: {desc}";
            }
        }
        _descriptionText.Text = description;

        _tooltipPopup.IsOpen = true;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _editor.TextArea.TextView.MouseHover -= OnMouseHover;
        _editor.TextArea.TextView.MouseHoverStopped -= OnMouseHoverStopped;
        _editor.TextArea.TextView.MouseMove -= OnMouseMove;
        _tooltipPopup.IsOpen = false;
    }
}

/// <summary>
/// Contains information about a function's signature and parameters
/// </summary>
public readonly struct FunctionInfo
{
    public readonly string Signature;
    public readonly string Description;
    public readonly (string Name, string Description)[] Parameters;

    public FunctionInfo(string signature, string description, (string, string)[] parameters)
    {
        Signature = signature;
        Description = description;
        Parameters = parameters;
    }
}
