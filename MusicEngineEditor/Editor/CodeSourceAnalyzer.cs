// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Source code analysis for live features.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MusicEngine.Core;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Represents a detected instrument/synth definition in code.
/// </summary>
public class InstrumentDefinition
{
    public string Name { get; set; } = "";
    public string VariableName { get; set; } = "";
    public int DefinitionStart { get; set; }
    public int DefinitionEnd { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public List<(int Start, int End)> AllReferences { get; set; } = new();
    public string InstrumentType { get; set; } = ""; // SimpleSynth, VstPlugin, etc.
}

/// <summary>
/// Represents a detected pattern definition in code.
/// </summary>
public class PatternDefinition
{
    public string VariableName { get; set; } = "";
    public string InstrumentName { get; set; } = "";
    public int DefinitionStart { get; set; }
    public int DefinitionEnd { get; set; }
    public int Line { get; set; }
    public List<NoteDefinition> Notes { get; set; } = new();
    public List<(int Start, int End)> AllReferences { get; set; } = new();
}

/// <summary>
/// Represents a detected note event in code.
/// </summary>
public class NoteDefinition
{
    public int Note { get; set; }
    public int Velocity { get; set; }
    public double Beat { get; set; }
    public double Duration { get; set; }
    public int SourceStart { get; set; }
    public int SourceEnd { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string RawText { get; set; } = "";
}

/// <summary>
/// Result of analyzing code for musical constructs.
/// </summary>
public class CodeAnalysisResult
{
    public List<InstrumentDefinition> Instruments { get; set; } = new();
    public List<PatternDefinition> Patterns { get; set; } = new();
    public Dictionary<string, List<(int Start, int End)>> InstrumentCodeRegions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Analyzes code to extract source location information for musical events.
/// </summary>
public static class CodeSourceAnalyzer
{
    // Regex patterns for detection
    private static readonly Regex SynthCreationRegex = new(
        @"(?<var>\w+)\s*=\s*(?:new\s+)?(?<type>SimpleSynth|CreateSynth)\s*\(\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex VstLoadRegex = new(
        @"(?<var>\w+)\s*=\s*(?:vst\.load|LoadVst)\s*\(\s*[""'](?<name>[^""']+)[""']\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex SamplerCreationRegex = new(
        @"(?<var>\w+)\s*=\s*(?:CreateSampler|CreateSamplerFromFile|CreateSamplerFromDirectory)\s*\([^)]*\)",
        RegexOptions.Compiled);

    private static readonly Regex PatternCreationRegex = new(
        @"(?<var>\w+)\s*=\s*(?:new\s+Pattern|CreatePattern|pattern|p|newPattern)\s*\(\s*(?<synth>\w+)?\s*(?:,[^)]*)?\)",
        RegexOptions.Compiled);

    private static readonly Regex NoteEventRegex = new(
        @"(?:new\s+NoteEvent\s*\{\s*|Events\.Add\s*\(\s*new\s+NoteEvent\s*\{\s*)(?<props>[^}]+)\}",
        RegexOptions.Compiled);

    private static readonly Regex NoteOnRegex = new(
        @"(?<synth>\w+)\.NoteOn\s*\(\s*(?<note>\d+)\s*,\s*(?<velocity>\d+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex PatternNoteRegex = new(
        @"(?<pattern>\w+)\.Events\.Add\s*\(\s*new\s+NoteEvent\s*\{(?<props>[^}]+)\}\s*\)",
        RegexOptions.Compiled);

    // Simple pattern.Note(note, beat, duration, velocity) syntax - primary syntax (note can be expression or literal)
    private static readonly Regex SimplePatternNoteRegex = new(
        @"(?<pattern>\w+)\.Note\s*\(\s*(?<note>[^,]+)\s*,\s*(?<beat>[\d.]+)\s*,\s*(?<duration>[\d.]+)\s*,\s*(?<velocity>\d+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex VariableUsageRegex = new(
        @"\b(?<var>{0})\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Analyzes source code and extracts instrument and pattern definitions.
    /// </summary>
    public static CodeAnalysisResult Analyze(string code)
    {
        var result = new CodeAnalysisResult();

        try
        {
            // Find all synth/instrument definitions
            FindSynthDefinitions(code, result);
            FindVstDefinitions(code, result);
            FindSamplerDefinitions(code, result);

            // Find all pattern definitions
            FindPatternDefinitions(code, result);

            // Find note events within patterns (including simple .Note() syntax)
            FindNoteEvents(code, result);
            FindSimpleNoteEvents(code, result);

            // Find all references to each instrument
            FindInstrumentReferences(code, result);

            // Build code regions map
            BuildCodeRegionsMap(result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Analysis error: {ex.Message}");
        }

        return result;
    }

    private static void FindSynthDefinitions(string code, CodeAnalysisResult result)
    {
        foreach (Match match in SynthCreationRegex.Matches(code))
        {
            var instrument = new InstrumentDefinition
            {
                VariableName = match.Groups["var"].Value,
                Name = match.Groups["var"].Value, // Use variable name as default
                InstrumentType = match.Groups["type"].Value == "CreateSynth" ? "SimpleSynth" : match.Groups["type"].Value,
                DefinitionStart = match.Index,
                DefinitionEnd = match.Index + match.Length,
                Line = GetLineNumber(code, match.Index),
                Column = GetColumnNumber(code, match.Index)
            };

            // Check for .SetName() or Name = "" nearby
            var nameMatch = Regex.Match(code.Substring(match.Index),
                $@"{instrument.VariableName}\s*\.\s*(?:Name\s*=|SetName\s*\()\s*[""']([^""']+)[""']");
            if (nameMatch.Success)
            {
                instrument.Name = nameMatch.Groups[1].Value;
            }

            result.Instruments.Add(instrument);
        }
    }

    private static void FindVstDefinitions(string code, CodeAnalysisResult result)
    {
        foreach (Match match in VstLoadRegex.Matches(code))
        {
            var instrument = new InstrumentDefinition
            {
                VariableName = match.Groups["var"].Value,
                Name = match.Groups["name"].Value,
                InstrumentType = "VstPlugin",
                DefinitionStart = match.Index,
                DefinitionEnd = match.Index + match.Length,
                Line = GetLineNumber(code, match.Index),
                Column = GetColumnNumber(code, match.Index)
            };

            result.Instruments.Add(instrument);
        }
    }

    private static void FindSamplerDefinitions(string code, CodeAnalysisResult result)
    {
        foreach (Match match in SamplerCreationRegex.Matches(code))
        {
            var instrument = new InstrumentDefinition
            {
                VariableName = match.Groups["var"].Value,
                Name = match.Groups["var"].Value,
                InstrumentType = "SampleInstrument",
                DefinitionStart = match.Index,
                DefinitionEnd = match.Index + match.Length,
                Line = GetLineNumber(code, match.Index),
                Column = GetColumnNumber(code, match.Index)
            };

            result.Instruments.Add(instrument);
        }
    }

    private static void FindPatternDefinitions(string code, CodeAnalysisResult result)
    {
        foreach (Match match in PatternCreationRegex.Matches(code))
        {
            var synthName = match.Groups["synth"].Value;

            var pattern = new PatternDefinition
            {
                VariableName = match.Groups["var"].Value,
                InstrumentName = synthName,
                DefinitionStart = match.Index,
                DefinitionEnd = match.Index + match.Length,
                Line = GetLineNumber(code, match.Index)
            };

            // Link to instrument
            var instrument = result.Instruments.FirstOrDefault(i => i.VariableName == synthName);
            if (instrument != null)
            {
                pattern.InstrumentName = instrument.Name;
            }

            result.Patterns.Add(pattern);
        }
    }

    private static void FindNoteEvents(string code, CodeAnalysisResult result)
    {
        // Find note events added to patterns
        foreach (Match match in PatternNoteRegex.Matches(code))
        {
            var patternVar = match.Groups["pattern"].Value;
            var props = match.Groups["props"].Value;

            var pattern = result.Patterns.FirstOrDefault(p => p.VariableName == patternVar);
            if (pattern == null) continue;

            var note = ParseNoteEvent(props, match.Index, match.Index + match.Length, code);
            if (note != null)
            {
                pattern.Notes.Add(note);
            }
        }

        // Also find inline note definitions
        var noteEventRegex = new Regex(@"new\s+NoteEvent\s*\{\s*([^}]+)\}", RegexOptions.Compiled);
        foreach (Match match in noteEventRegex.Matches(code))
        {
            var note = ParseNoteEvent(match.Groups[1].Value, match.Index, match.Index + match.Length, code);
            if (note != null)
            {
                // Try to associate with a pattern based on context
                foreach (var pattern in result.Patterns)
                {
                    if (match.Index > pattern.DefinitionStart &&
                        match.Index < pattern.DefinitionStart + 2000) // Reasonable proximity
                    {
                        if (!pattern.Notes.Any(n => n.SourceStart == note.SourceStart))
                        {
                            pattern.Notes.Add(note);
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Finds the simple pattern.Note(note, beat, duration, velocity) syntax
    /// This is the primary syntax used in the workshop examples.
    /// </summary>
    private static void FindSimpleNoteEvents(string code, CodeAnalysisResult result)
    {
        foreach (Match match in SimplePatternNoteRegex.Matches(code))
        {
            var patternVar = match.Groups["pattern"].Value;

            var pattern = result.Patterns.FirstOrDefault(p => p.VariableName == patternVar);
            if (pattern == null) continue;

            var note = new NoteDefinition
            {
                Note = TryParseNoteExpression(match.Groups["note"].Value),
                Beat = double.Parse(match.Groups["beat"].Value, System.Globalization.CultureInfo.InvariantCulture),
                Duration = double.Parse(match.Groups["duration"].Value, System.Globalization.CultureInfo.InvariantCulture),
                Velocity = int.Parse(match.Groups["velocity"].Value),
                SourceStart = match.Index,
                SourceEnd = match.Index + match.Length,
                Line = GetLineNumber(code, match.Index),
                Column = GetColumnNumber(code, match.Index),
                RawText = match.Value
            };

            // Avoid duplicates
            if (!pattern.Notes.Any(n => n.SourceStart == note.SourceStart))
            {
                pattern.Notes.Add(note);
            }
        }
    }

    private static NoteDefinition? ParseNoteEvent(string props, int start, int end, string code)
    {
        var note = new NoteDefinition
        {
            SourceStart = start,
            SourceEnd = end,
            RawText = props.Trim(),
            Line = GetLineNumber(code, start),
            Column = GetColumnNumber(code, start)
        };

        // Parse properties
        var beatMatch = Regex.Match(props, @"Beat\s*=\s*([\d.]+)");
        var noteMatch = Regex.Match(props, @"Note\s*=\s*(\d+)");
        var velocityMatch = Regex.Match(props, @"Velocity\s*=\s*(\d+)");
        var durationMatch = Regex.Match(props, @"Duration\s*=\s*([\d.]+)");

        if (beatMatch.Success) note.Beat = double.Parse(beatMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        if (noteMatch.Success) note.Note = int.Parse(noteMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        if (velocityMatch.Success) note.Velocity = int.Parse(velocityMatch.Groups[1].Value);
        if (durationMatch.Success) note.Duration = double.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        return note;
    }

    private static void FindInstrumentReferences(string code, CodeAnalysisResult result)
    {
        foreach (var instrument in result.Instruments)
        {
            var pattern = new Regex($@"\b{Regex.Escape(instrument.VariableName)}\b");
            foreach (Match match in pattern.Matches(code))
            {
                instrument.AllReferences.Add((match.Index, match.Index + match.Length));
            }
        }

        foreach (var pattern in result.Patterns)
        {
            var regex = new Regex($@"\b{Regex.Escape(pattern.VariableName)}\b");
            foreach (Match match in regex.Matches(code))
            {
                pattern.AllReferences.Add((match.Index, match.Index + match.Length));
            }
        }
    }

    private static void BuildCodeRegionsMap(CodeAnalysisResult result)
    {
        foreach (var instrument in result.Instruments)
        {
            if (!result.InstrumentCodeRegions.ContainsKey(instrument.Name))
            {
                result.InstrumentCodeRegions[instrument.Name] = new List<(int, int)>();
            }

            // Add definition
            result.InstrumentCodeRegions[instrument.Name].Add(
                (instrument.DefinitionStart, instrument.DefinitionEnd));

            // Add all references
            result.InstrumentCodeRegions[instrument.Name].AddRange(instrument.AllReferences);
        }

        // Add pattern regions to their associated instruments
        foreach (var pattern in result.Patterns)
        {
            if (!result.InstrumentCodeRegions.ContainsKey(pattern.InstrumentName))
            {
                result.InstrumentCodeRegions[pattern.InstrumentName] = new List<(int, int)>();
            }

            // Add pattern definition
            result.InstrumentCodeRegions[pattern.InstrumentName].Add(
                (pattern.DefinitionStart, pattern.DefinitionEnd));

            // Add note definitions
            foreach (var note in pattern.Notes)
            {
                result.InstrumentCodeRegions[pattern.InstrumentName].Add(
                    (note.SourceStart, note.SourceEnd));
            }
        }
    }

    /// <summary>
    /// Creates CodeSourceInfo objects for all detected note events and attaches them.
    /// Call this after script execution to map NoteEvents to their source locations.
    /// </summary>
    public static void AttachSourceInfoToPatterns(string code, IEnumerable<Pattern> patterns)
    {
        var analysis = Analyze(code);
        var remainingDefinitions = new List<PatternDefinition>(analysis.Patterns);
        var indexedDefinitions = analysis.Patterns.ToList();

        foreach (var pattern in patterns)
        {
            // Try to find matching pattern definition
            PatternDefinition? patternDef = null;
            if (!string.IsNullOrWhiteSpace(pattern.Name))
            {
                patternDef = remainingDefinitions.FirstOrDefault(p =>
                    string.Equals(p.VariableName, pattern.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (patternDef == null)
            {
                var patternIndex = GetPatternIndex(pattern);
                if (patternIndex >= 0 && patternIndex < indexedDefinitions.Count)
                {
                    var candidate = indexedDefinitions[patternIndex];
                    if (remainingDefinitions.Contains(candidate))
                    {
                        patternDef = candidate;
                    }
                }
            }

            if (patternDef == null)
            {
                patternDef = remainingDefinitions.FirstOrDefault(p =>
                    string.Equals(p.InstrumentName, pattern.InstrumentName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.InstrumentName, pattern.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (patternDef != null)
            {
                remainingDefinitions.Remove(patternDef);

                if (string.IsNullOrWhiteSpace(pattern.Name))
                {
                    pattern.Name = patternDef.VariableName;
                }

                if (string.IsNullOrWhiteSpace(pattern.InstrumentName))
                {
                    pattern.InstrumentName = patternDef.InstrumentName;
                }

                // Attach source info to the pattern itself
                pattern.SourceInfo = new CodeSourceInfo
                {
                    StartIndex = patternDef.DefinitionStart,
                    EndIndex = patternDef.DefinitionEnd,
                    StartLine = patternDef.Line,
                    InstrumentName = pattern.InstrumentName
                };

                // Match note events by their Beat position
                for (int i = 0; i < pattern.Events.Count; i++)
                {
                    var noteEvent = pattern.Events[i];
                    var matchingNote = patternDef.Notes.FirstOrDefault(n =>
                        Math.Abs(n.Beat - noteEvent.Beat) < 0.01 &&
                        (n.Note <= 0 || n.Note == noteEvent.Note));

                    if (matchingNote == null && i < patternDef.Notes.Count)
                    {
                        matchingNote = patternDef.Notes[i];
                    }

                    if (matchingNote != null)
                    {
                        noteEvent.SourceInfo = new CodeSourceInfo
                        {
                            StartIndex = matchingNote.SourceStart,
                            EndIndex = matchingNote.SourceEnd,
                            StartLine = matchingNote.Line,
                            StartColumn = matchingNote.Column,
                            SourceText = matchingNote.RawText,
                            InstrumentName = pattern.InstrumentName
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extracts source info for a specific note event based on code analysis.
    /// </summary>
    public static CodeSourceInfo? GetSourceInfoForNote(string code, int note, double beat, string? instrumentName = null, string? patternVariable = null)
    {
        var analysis = Analyze(code);

        foreach (var pattern in analysis.Patterns)
        {
            if (!string.IsNullOrWhiteSpace(patternVariable) &&
                !string.Equals(pattern.VariableName, patternVariable, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (instrumentName != null && pattern.InstrumentName != instrumentName)
                continue;

            var matchingNote = pattern.Notes.FirstOrDefault(n =>
                Math.Abs(n.Beat - beat) < 0.01 && (n.Note <= 0 || n.Note == note));

            if (matchingNote != null)
            {
                return new CodeSourceInfo
                {
                    StartIndex = matchingNote.SourceStart,
                    EndIndex = matchingNote.SourceEnd,
                    StartLine = matchingNote.Line,
                    StartColumn = matchingNote.Column,
                    SourceText = matchingNote.RawText,
                    InstrumentName = pattern.InstrumentName
                };
            }
        }

        return null;
    }

    public static CodeSourceInfo? GetSourceInfoForNoteIndex(string code, int patternIndex, int noteIndex, string? patternVariable = null)
    {
        var analysis = Analyze(code);
        PatternDefinition? patternDef = null;

        if (!string.IsNullOrWhiteSpace(patternVariable))
        {
            patternDef = analysis.Patterns.FirstOrDefault(p =>
                string.Equals(p.VariableName, patternVariable, StringComparison.OrdinalIgnoreCase));
        }

        if (patternDef == null && patternIndex >= 0 && patternIndex < analysis.Patterns.Count)
        {
            patternDef = analysis.Patterns[patternIndex];
        }

        if (patternDef == null || noteIndex < 0 || noteIndex >= patternDef.Notes.Count)
        {
            return null;
        }

        var matchingNote = patternDef.Notes[noteIndex];
        return new CodeSourceInfo
        {
            StartIndex = matchingNote.SourceStart,
            EndIndex = matchingNote.SourceEnd,
            StartLine = matchingNote.Line,
            StartColumn = matchingNote.Column,
            SourceText = matchingNote.RawText,
            InstrumentName = patternDef.InstrumentName
        };
    }

    private static int GetLineNumber(string text, int index)
    {
        if (index >= text.Length) return 1;
        return text.Substring(0, index).Count(c => c == '\n') + 1;
    }

    private static int GetColumnNumber(string text, int index)
    {
        if (index >= text.Length) return 1;
        int lastNewline = text.LastIndexOf('\n', index);
        return index - lastNewline;
    }

    private static int GetPatternIndex(Pattern pattern)
    {
        var prop = pattern.GetType().GetProperty("PatternIndex",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (prop?.GetValue(pattern) is int index)
        {
            return index;
        }

        return -1;
    }

    private static int TryParseNoteExpression(string expression)
    {
        var trimmed = expression.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var note))
        {
            return note;
        }

        if (trimmed.Length >= 2 &&
            (trimmed[0] == '"' && trimmed[^1] == '"' || trimmed[0] == '\'' && trimmed[^1] == '\''))
        {
            var name = trimmed.Substring(1, trimmed.Length - 2);
            var parsed = TryParseNoteName(name);
            if (parsed.HasValue)
            {
                return parsed.Value;
            }
        }

        var funcMatch = Regex.Match(trimmed, @"(?<note>[A-Ga-g])\s*(?<accidental>[#b]?)[\s_]*\(\s*(?<octave>-?\d+)\s*\)");
        if (funcMatch.Success)
        {
            var name = funcMatch.Groups["note"].Value.ToUpperInvariant() + funcMatch.Groups["accidental"].Value;
            var octaveText = funcMatch.Groups["octave"].Value;
            if (int.TryParse(octaveText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var octave))
            {
                var parsed = TryParseNoteName($"{name}{octave}");
                if (parsed.HasValue)
                {
                    return parsed.Value;
                }
            }
        }

        return -1;
    }

    private static int? TryParseNoteName(string noteText)
    {
        if (string.IsNullOrWhiteSpace(noteText))
        {
            return null;
        }

        var match = Regex.Match(noteText.Trim(), @"^(?<note>[A-Ga-g])(?<accidental>[#b]?)(?<octave>-?\d+)$");
        if (!match.Success)
        {
            return null;
        }

        var noteLetter = match.Groups["note"].Value.ToUpperInvariant();
        var accidental = match.Groups["accidental"].Value;
        if (!int.TryParse(match.Groups["octave"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var octave))
        {
            return null;
        }

        var baseIndex = noteLetter switch
        {
            "C" => 0,
            "D" => 2,
            "E" => 4,
            "F" => 5,
            "G" => 7,
            "A" => 9,
            "B" => 11,
            _ => 0
        };

        if (accidental == "#")
        {
            baseIndex += 1;
        }
        else if (accidental == "b")
        {
            baseIndex -= 1;
        }

        baseIndex = (baseIndex + 12) % 12;
        var midi = (octave + 1) * 12 + baseIndex;
        return Math.Clamp(midi, 0, 127);
    }
}
