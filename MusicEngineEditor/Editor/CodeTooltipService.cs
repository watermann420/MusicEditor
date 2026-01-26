// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code tooltip/hover information service.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfToolTip = System.Windows.Controls.ToolTip;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Provides hover tooltips for code elements in the editor
/// </summary>
public class CodeTooltipService
{
    private readonly TextEditor _editor;
    private readonly WpfToolTip _tooltip;
    private readonly Dictionary<string, string> _documentation;
    private string? _lastWord;

    public CodeTooltipService(TextEditor editor)
    {
        _editor = editor;
        _documentation = BuildDocumentation();

        // Create styled tooltip
        _tooltip = new WpfToolTip
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2B, 0x2D, 0x30)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBC, 0xBE, 0xC4)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3C, 0x3F, 0x41)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            MaxWidth = 450,
            Placement = PlacementMode.Mouse
        };

        // Subscribe to mouse events
        _editor.TextArea.TextView.MouseHover += TextView_MouseHover;
        _editor.TextArea.TextView.MouseHoverStopped += TextView_MouseHoverStopped;
        _editor.TextArea.TextView.MouseMove += TextView_MouseMove;
    }

    private void TextView_MouseHover(object sender, WpfMouseEventArgs e)
    {
        var position = _editor.GetPositionFromPoint(e.GetPosition(_editor));
        if (position == null) return;

        var offset = _editor.Document.GetOffset(position.Value.Location);
        var word = GetWordAtOffset(offset);

        if (string.IsNullOrEmpty(word) || word == _lastWord && _tooltip.IsOpen)
            return;

        _lastWord = word;

        // Check for member access (e.g., synth.NoteOn)
        var fullExpression = GetFullExpressionAtOffset(offset);
        var description = GetDocumentation(fullExpression) ?? GetDocumentation(word);

        if (description != null)
        {
            ShowTooltip(description, fullExpression ?? word);
        }
    }

    private void TextView_MouseHoverStopped(object sender, WpfMouseEventArgs e)
    {
        HideTooltip();
    }

    private void TextView_MouseMove(object sender, WpfMouseEventArgs e)
    {
        // Only hide if we've moved significantly
        if (_tooltip.IsOpen)
        {
            var position = _editor.GetPositionFromPoint(e.GetPosition(_editor));
            if (position == null)
            {
                HideTooltip();
                return;
            }

            var offset = _editor.Document.GetOffset(position.Value.Location);
            var word = GetWordAtOffset(offset);
            var fullExpression = GetFullExpressionAtOffset(offset);

            if (word != _lastWord && fullExpression != _lastWord)
            {
                HideTooltip();
            }
        }
    }

    private string? GetWordAtOffset(int offset)
    {
        if (offset < 0 || offset >= _editor.Document.TextLength)
            return null;

        var document = _editor.Document;
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, line.Length);
        var offsetInLine = offset - line.Offset;

        // Find word boundaries
        int start = offsetInLine;
        int end = offsetInLine;

        while (start > 0 && IsWordChar(lineText[start - 1]))
            start--;

        while (end < lineText.Length && IsWordChar(lineText[end]))
            end++;

        if (start >= end) return null;

        return lineText.Substring(start, end - start);
    }

    private string? GetFullExpressionAtOffset(int offset)
    {
        if (offset < 0 || offset >= _editor.Document.TextLength)
            return null;

        var document = _editor.Document;
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, line.Length);
        var offsetInLine = offset - line.Offset;

        // Find expression boundaries (including dots for member access)
        int start = offsetInLine;
        int end = offsetInLine;

        while (start > 0 && (IsWordChar(lineText[start - 1]) || lineText[start - 1] == '.'))
            start--;

        while (end < lineText.Length && IsWordChar(lineText[end]))
            end++;

        if (start >= end) return null;

        var expression = lineText.Substring(start, end - start);

        // Handle cases like "synth.NoteOn" -> normalize to just the method name with context
        return expression;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private void ShowTooltip(string description, string identifier)
    {
        var stack = new StackPanel();

        // Identifier header
        var header = new TextBlock
        {
            Text = identifier,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new System.Windows.Media.FontFamily("JetBrains Mono, Consolas"),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4E, 0xC9, 0xB0)), // Teal for types/methods
            Margin = new Thickness(0, 0, 0, 6)
        };
        stack.Children.Add(header);

        // Description
        var descBlock = new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBC, 0xBE, 0xC4))
        };
        stack.Children.Add(descBlock);

        _tooltip.Content = stack;
        _tooltip.IsOpen = true;
    }

    private void HideTooltip()
    {
        _tooltip.IsOpen = false;
        _lastWord = null;
    }

    private string? GetDocumentation(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return null;

        // Try exact match first
        if (_documentation.TryGetValue(identifier, out var doc))
            return doc;

        // Try case-insensitive
        foreach (var kvp in _documentation)
        {
            if (kvp.Key.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        // Try partial match for member access (e.g., "synth.NoteOn" -> "NoteOn")
        if (identifier.Contains('.'))
        {
            var parts = identifier.Split('.');
            var memberName = parts[^1];

            // Get context from the object name
            var objectName = parts[0].ToLower();

            // Match based on object type
            if (objectName == "sequencer")
            {
                if (_documentation.TryGetValue($"Sequencer.{memberName}", out doc))
                    return doc;
            }
            else if (objectName == "engine")
            {
                if (_documentation.TryGetValue($"Engine.{memberName}", out doc))
                    return doc;
            }
            else if (objectName == "vst")
            {
                if (_documentation.TryGetValue($"vst.{memberName}", out doc))
                    return doc;
            }
            else if (objectName == "midi")
            {
                if (_documentation.TryGetValue($"midi.{memberName}", out doc))
                    return doc;
            }
            // Synth/sampler methods
            else if (_documentation.TryGetValue(memberName, out doc))
            {
                return doc;
            }
        }

        return null;
    }

    private static Dictionary<string, string> BuildDocumentation()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Global objects
            ["Sequencer"] = "Global sequencer for timing and patterns.\n\nProperties:\n  .Bpm - Get/set tempo in BPM\n  .CurrentBeat - Current playback position\n  .IsRunning - Check if sequencer is playing\n\nMethods:\n  .Start() - Begin playback\n  .Stop() - Stop playback\n  .Schedule(beat, action) - Schedule action at beat",

            ["Engine"] = "Audio engine controller.\n\nMethods:\n  .RouteMidiInput(device, target) - Route MIDI input\n  .GetMidiInputCount() - Get number of MIDI inputs\n  .GetMidiOutputCount() - Get number of MIDI outputs",

            ["vst"] = "VST plugin loader.\n\nMethods:\n  .load(name) - Load a VST plugin by name\n  .scan() - Scan for available plugins\n  .list() - List all available plugins",

            ["midi"] = "MIDI device controller.\n\nMethods:\n  .device(index) - Get MIDI device by index\n  .route(target) - Route MIDI to target\n  .cc(number) - Get CC controller",

            // Sequencer methods
            ["Sequencer.Bpm"] = "double Bpm { get; set; }\n\nGet or set the tempo in beats per minute.\n\nExample:\n  Sequencer.Bpm = 140;",
            ["Sequencer.Start"] = "void Start()\n\nStart sequencer playback.\n\nExample:\n  Sequencer.Start();",
            ["Sequencer.Stop"] = "void Stop()\n\nStop sequencer playback.\n\nExample:\n  Sequencer.Stop();",
            ["Sequencer.Schedule"] = "void Schedule(double beat, Action action)\n\nSchedule an action to run at a specific beat.\n\nParameters:\n  beat - The beat number to trigger at\n  action - Code to execute\n\nExample:\n  Sequencer.Schedule(0, () => synth.NoteOn(60, 100));",
            ["Sequencer.CurrentBeat"] = "double CurrentBeat { get; }\n\nGet the current playback position in beats.",
            ["Sequencer.IsRunning"] = "bool IsRunning { get; }\n\nReturns true if the sequencer is currently playing.",

            // Functions
            ["CreateSynth"] = "ISynth CreateSynth()\n\nCreate a new synthesizer instance.\n\nReturns: A synthesizer object\n\nExample:\n  var synth = CreateSynth();\n  synth.NoteOn(60, 100);",

            ["CreateSampler"] = "ISampler CreateSampler()\n\nCreate a new sampler instance.\n\nReturns: A sampler object\n\nExample:\n  var sampler = CreateSampler();\n  sampler.LoadSample(\"kick.wav\");",

            ["CreatePattern"] = "Pattern CreatePattern(ISynth target)\n\nCreate a new pattern for sequencing notes.\n\nParameters:\n  target - The synth/sampler to play notes on\n\nReturns: A pattern object\n\nExample:\n  var pattern = CreatePattern(synth);\n  pattern.Note(60, 0, 0.5, 100);",

            ["Print"] = "void Print(string message)\n\nOutput text to the console.\n\nParameters:\n  message - Text to display\n\nExample:\n  Print(\"Hello World!\");",

            ["LoadAudio"] = "AudioClip LoadAudio(string path)\n\nLoad an audio file from disk.\n\nParameters:\n  path - Path to the audio file\n\nReturns: AudioClip object",

            // Synth methods
            ["NoteOn"] = "void NoteOn(int note, int velocity)\n\nPlay a MIDI note.\n\nParameters:\n  note - MIDI note number (60 = Middle C)\n  velocity - Volume 0-127\n\nExample:\n  synth.NoteOn(60, 100);  // Play middle C",

            ["NoteOff"] = "void NoteOff(int note)\n\nStop a playing note.\n\nParameters:\n  note - MIDI note number to stop\n\nExample:\n  synth.NoteOff(60);",

            ["AllNotesOff"] = "void AllNotesOff()\n\nStop all currently playing notes.\n\nExample:\n  synth.AllNotesOff();",

            ["SetParameter"] = "void SetParameter(string name, float value)\n\nSet a synthesizer parameter.\n\nParameters:\n  name - Parameter name\n  value - Parameter value\n\nCommon parameters:\n  \"waveform\" - 0=Sine, 1=Square, 2=Saw, 3=Triangle, 4=Noise\n  \"cutoff\" - Filter cutoff 0.0-1.0\n  \"resonance\" - Filter resonance 0.0-1.0",

            // VST methods
            ["vst.load"] = "VstPlugin? vst.load(string name)\n\nLoad a VST plugin by name.\n\nParameters:\n  name - Plugin name (e.g., \"Vital\", \"Serum\")\n\nReturns: VstPlugin object or null if not found\n\nExample:\n  var vital = vst.load(\"Vital\");\n  vital?.ShowEditor();",

            ["ShowEditor"] = "void ShowEditor()\n\nOpen the VST plugin's editor window.\n\nExample:\n  vital.ShowEditor();",

            ["from"] = "void from(int deviceIndex)\n\nRoute MIDI input from a device to this plugin.\n\nParameters:\n  deviceIndex - MIDI device index\n\nExample:\n  vital.from(0);  // Route first MIDI device",

            // Pattern methods
            ["Note"] = "void Note(int pitch, double beat, double duration, int velocity)\n\nAdd a note to the pattern.\n\nParameters:\n  pitch - MIDI note number\n  beat - Beat position in the pattern\n  duration - Note length in beats\n  velocity - Volume 0-127\n\nExample:\n  pattern.Note(60, 0, 0.5, 100);",

            ["Play"] = "void Play()\n\nStart playing the pattern.\n\nExample:\n  pattern.Play();",

            ["Stop"] = "void Stop()\n\nStop the pattern playback.\n\nExample:\n  pattern.Stop();",

            ["Loop"] = "bool Loop { get; set; }\n\nEnable or disable pattern looping.\n\nExample:\n  pattern.Loop = true;",

            // Engine methods
            ["Engine.RouteMidiInput"] = "void RouteMidiInput(int device, ISoundSource target)\n\nRoute MIDI from input device to a synth/sampler.\n\nParameters:\n  device - MIDI device index\n  target - Target synth or sampler\n\nExample:\n  Engine.RouteMidiInput(0, synth);",

            ["Engine.GetMidiInputCount"] = "int GetMidiInputCount()\n\nGet the number of available MIDI input devices.\n\nReturns: Number of MIDI inputs",

            ["Engine.GetMidiOutputCount"] = "int GetMidiOutputCount()\n\nGet the number of available MIDI output devices.\n\nReturns: Number of MIDI outputs",

            // MIDI methods
            ["midi.device"] = "MidiDevice midi.device(int index)\n\nGet a MIDI device by index.\n\nParameters:\n  index - Device index\n\nReturns: MidiDevice object\n\nExample:\n  midi.device(0).route(synth);",

            ["route"] = "void route(ISoundSource target)\n\nRoute this MIDI device to a target synth/sampler.\n\nParameters:\n  target - The synth or VST to route to\n\nExample:\n  midi.device(0).route(synth);",

            ["cc"] = "MidiCC cc(int number)\n\nGet a MIDI CC controller.\n\nParameters:\n  number - CC number (0-127)\n\nReturns: MidiCC object for mapping\n\nExample:\n  midi.device(0).cc(1).to(synth, \"cutoff\");",

            ["to"] = "void to(ISoundSource target, string parameter)\n\nMap this CC to a synth parameter.\n\nParameters:\n  target - Target synth\n  parameter - Parameter name to control\n\nExample:\n  midi.device(0).cc(1).to(synth, \"cutoff\");",

            // Common keywords
            ["var"] = "var - Declare a variable with inferred type.\n\nExample:\n  var synth = CreateSynth();",
            ["await"] = "await - Wait for an async operation to complete.\n\nExample:\n  await Task.Delay(500);",
            ["Task"] = "Task - Represents an asynchronous operation.\n\nCommon methods:\n  Task.Delay(ms) - Wait for milliseconds\n\nExample:\n  await Task.Delay(500);",
            ["Delay"] = "Task Task.Delay(int milliseconds)\n\nCreate a task that completes after a delay.\n\nParameters:\n  milliseconds - Time to wait in ms\n\nExample:\n  await Task.Delay(500);  // Wait 500ms"
        };
    }
}
