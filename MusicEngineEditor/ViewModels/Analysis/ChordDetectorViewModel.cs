// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the ChordDetectorPanel providing chord detection and analysis.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Analysis;

/// <summary>
/// ViewModel for chord detection and analysis functionality.
/// </summary>
public partial class ChordDetectorViewModel : ViewModelBase
{
    #region Observable Properties

    /// <summary>
    /// The detected chord name (e.g., "Cmaj7", "F#m", "Gsus4").
    /// </summary>
    [ObservableProperty]
    private string _chordName = "--";

    /// <summary>
    /// The root note of the detected chord.
    /// </summary>
    [ObservableProperty]
    private string _rootNote = "--";

    /// <summary>
    /// The chord quality (major, minor, diminished, etc.).
    /// </summary>
    [ObservableProperty]
    private ChordQuality _quality = ChordQuality.Unknown;

    /// <summary>
    /// Display text for chord quality.
    /// </summary>
    [ObservableProperty]
    private string _qualityText = "Unknown";

    /// <summary>
    /// Bass note for slash chords (e.g., "G" in "C/G").
    /// </summary>
    [ObservableProperty]
    private string _bassNote = "--";

    /// <summary>
    /// Whether this is a slash chord (bass differs from root).
    /// </summary>
    [ObservableProperty]
    private bool _isSlashChord;

    /// <summary>
    /// Detection confidence (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private double _confidence;

    /// <summary>
    /// Confidence as percentage text.
    /// </summary>
    [ObservableProperty]
    private string _confidenceText = "0%";

    /// <summary>
    /// Currently detected MIDI notes.
    /// </summary>
    [ObservableProperty]
    private int[] _detectedNotes = Array.Empty<int>();

    /// <summary>
    /// Roman numeral analysis (I, IV, V, etc.).
    /// </summary>
    [ObservableProperty]
    private string _romanNumeral = "--";

    /// <summary>
    /// Whether the key is known for Roman numeral analysis.
    /// </summary>
    [ObservableProperty]
    private bool _isKeyKnown;

    /// <summary>
    /// The current key for Roman numeral analysis.
    /// </summary>
    [ObservableProperty]
    private string _currentKey = "C Major";

    /// <summary>
    /// Whether MIDI output is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isMidiOutputEnabled;

    /// <summary>
    /// Detection sensitivity (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private double _sensitivity = 0.5;

    /// <summary>
    /// Whether detection is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isDetectionActive = true;

    /// <summary>
    /// Alternative chord interpretations.
    /// </summary>
    public ObservableCollection<AlternativeChord> AlternativeChords { get; } = new();

    /// <summary>
    /// History of recently detected chords.
    /// </summary>
    public ObservableCollection<ChordHistoryItem> ChordHistory { get; } = new();

    /// <summary>
    /// Guitar chord diagram fret positions.
    /// </summary>
    [ObservableProperty]
    private int[] _guitarFrets = new int[6] { -1, -1, -1, -1, -1, -1 };

    /// <summary>
    /// Whether guitar diagram is available for current chord.
    /// </summary>
    [ObservableProperty]
    private bool _hasGuitarDiagram;

    /// <summary>
    /// Starting fret position for guitar diagram.
    /// </summary>
    [ObservableProperty]
    private int _guitarStartFret = 1;

    /// <summary>
    /// Available keys for selection.
    /// </summary>
    public ObservableCollection<string> AvailableKeys { get; } = new()
    {
        "C Major", "C Minor", "C# Major", "C# Minor",
        "D Major", "D Minor", "D# Major", "D# Minor",
        "E Major", "E Minor",
        "F Major", "F Minor", "F# Major", "F# Minor",
        "G Major", "G Minor", "G# Major", "G# Minor",
        "A Major", "A Minor", "A# Major", "A# Minor",
        "B Major", "B Minor"
    };

    #endregion

    #region Private Fields

    private const int MaxHistoryItems = 20;
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    // Chord templates for detection (intervals from root)
    private static readonly Dictionary<ChordQuality, int[]> ChordTemplates = new()
    {
        { ChordQuality.Major, new[] { 0, 4, 7 } },
        { ChordQuality.Minor, new[] { 0, 3, 7 } },
        { ChordQuality.Diminished, new[] { 0, 3, 6 } },
        { ChordQuality.Augmented, new[] { 0, 4, 8 } },
        { ChordQuality.Sus2, new[] { 0, 2, 7 } },
        { ChordQuality.Sus4, new[] { 0, 5, 7 } },
        { ChordQuality.Major7, new[] { 0, 4, 7, 11 } },
        { ChordQuality.Minor7, new[] { 0, 3, 7, 10 } },
        { ChordQuality.Dominant7, new[] { 0, 4, 7, 10 } },
        { ChordQuality.Diminished7, new[] { 0, 3, 6, 9 } },
        { ChordQuality.HalfDiminished7, new[] { 0, 3, 6, 10 } },
        { ChordQuality.MinorMajor7, new[] { 0, 3, 7, 11 } },
        { ChordQuality.Augmented7, new[] { 0, 4, 8, 10 } },
        { ChordQuality.Major6, new[] { 0, 4, 7, 9 } },
        { ChordQuality.Minor6, new[] { 0, 3, 7, 9 } },
        { ChordQuality.Add9, new[] { 0, 4, 7, 14 } },
        { ChordQuality.Major9, new[] { 0, 4, 7, 11, 14 } },
        { ChordQuality.Minor9, new[] { 0, 3, 7, 10, 14 } },
        { ChordQuality.Dominant9, new[] { 0, 4, 7, 10, 14 } },
    };

    // Guitar chord fingerings (string positions: E A D G B E, -1 = muted, 0 = open)
    private static readonly Dictionary<string, (int[] frets, int startFret)> GuitarChords = new()
    {
        { "C", (new[] { -1, 3, 2, 0, 1, 0 }, 1) },
        { "Cm", (new[] { -1, 3, 5, 5, 4, 3 }, 3) },
        { "D", (new[] { -1, -1, 0, 2, 3, 2 }, 1) },
        { "Dm", (new[] { -1, -1, 0, 2, 3, 1 }, 1) },
        { "E", (new[] { 0, 2, 2, 1, 0, 0 }, 1) },
        { "Em", (new[] { 0, 2, 2, 0, 0, 0 }, 1) },
        { "F", (new[] { 1, 3, 3, 2, 1, 1 }, 1) },
        { "Fm", (new[] { 1, 3, 3, 1, 1, 1 }, 1) },
        { "G", (new[] { 3, 2, 0, 0, 0, 3 }, 1) },
        { "Gm", (new[] { 3, 5, 5, 3, 3, 3 }, 3) },
        { "A", (new[] { -1, 0, 2, 2, 2, 0 }, 1) },
        { "Am", (new[] { -1, 0, 2, 2, 1, 0 }, 1) },
        { "B", (new[] { -1, 2, 4, 4, 4, 2 }, 2) },
        { "Bm", (new[] { -1, 2, 4, 4, 3, 2 }, 2) },
        { "Cmaj7", (new[] { -1, 3, 2, 0, 0, 0 }, 1) },
        { "Dm7", (new[] { -1, -1, 0, 2, 1, 1 }, 1) },
        { "Em7", (new[] { 0, 2, 0, 0, 0, 0 }, 1) },
        { "Fmaj7", (new[] { -1, -1, 3, 2, 1, 0 }, 1) },
        { "G7", (new[] { 3, 2, 0, 0, 0, 1 }, 1) },
        { "Am7", (new[] { -1, 0, 2, 0, 1, 0 }, 1) },
    };

    #endregion

    #region Constructor

    public ChordDetectorViewModel()
    {
        // Initialize with no detection
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ToggleDetection()
    {
        IsDetectionActive = !IsDetectionActive;
    }

    [RelayCommand]
    private void ToggleMidiOutput()
    {
        IsMidiOutputEnabled = !IsMidiOutputEnabled;
    }

    [RelayCommand]
    private void ClearHistory()
    {
        ChordHistory.Clear();
    }

    [RelayCommand]
    private void SetKey(string key)
    {
        CurrentKey = key;
        IsKeyKnown = true;
        UpdateRomanNumeral();
    }

    [RelayCommand]
    private void ResetDetection()
    {
        ChordName = "--";
        RootNote = "--";
        Quality = ChordQuality.Unknown;
        QualityText = "Unknown";
        BassNote = "--";
        IsSlashChord = false;
        Confidence = 0;
        ConfidenceText = "0%";
        DetectedNotes = Array.Empty<int>();
        RomanNumeral = "--";
        AlternativeChords.Clear();
        HasGuitarDiagram = false;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Analyzes a set of MIDI notes and detects the chord.
    /// </summary>
    /// <param name="midiNotes">Array of MIDI note numbers (0-127).</param>
    public void AnalyzeNotes(int[] midiNotes)
    {
        if (!IsDetectionActive || midiNotes == null || midiNotes.Length < 2)
        {
            if (midiNotes == null || midiNotes.Length == 0)
            {
                ResetDetection();
            }
            return;
        }

        DetectedNotes = midiNotes;

        // Normalize notes to pitch classes (0-11)
        var pitchClasses = midiNotes.Select(n => n % 12).Distinct().OrderBy(n => n).ToArray();

        if (pitchClasses.Length < 2)
        {
            return;
        }

        // Find the bass note (lowest MIDI note)
        int bassNoteIndex = midiNotes.Min() % 12;
        BassNote = NoteNames[bassNoteIndex];

        // Detect chord by trying each pitch class as root
        var bestMatch = DetectChord(pitchClasses, bassNoteIndex);

        if (bestMatch != null)
        {
            RootNote = NoteNames[bestMatch.Root];
            Quality = bestMatch.Quality;
            QualityText = GetQualityDisplayText(bestMatch.Quality);
            ChordName = FormatChordName(bestMatch.Root, bestMatch.Quality, bassNoteIndex);
            Confidence = bestMatch.Confidence;
            ConfidenceText = $"{bestMatch.Confidence * 100:F0}%";
            IsSlashChord = bestMatch.Root != bassNoteIndex;

            // Update guitar diagram
            UpdateGuitarDiagram(ChordName);

            // Update Roman numeral
            UpdateRomanNumeral();

            // Find alternative interpretations
            FindAlternatives(pitchClasses, bassNoteIndex, bestMatch);

            // Add to history
            AddToHistory(ChordName, bestMatch.Quality, bestMatch.Confidence);
        }
    }

    /// <summary>
    /// Updates detection with new audio frequency data.
    /// </summary>
    /// <param name="fundamentals">Detected fundamental frequencies in Hz.</param>
    /// <param name="amplitudes">Corresponding amplitudes (0-1).</param>
    public void AnalyzeFrequencies(float[] fundamentals, float[] amplitudes)
    {
        if (!IsDetectionActive || fundamentals == null || fundamentals.Length == 0)
        {
            return;
        }

        // Filter by sensitivity threshold
        var threshold = 1.0 - Sensitivity;
        var significantFreqs = new List<int>();

        for (int i = 0; i < fundamentals.Length && i < amplitudes.Length; i++)
        {
            if (amplitudes[i] >= threshold && fundamentals[i] > 20 && fundamentals[i] < 4200)
            {
                // Convert frequency to MIDI note
                int midiNote = (int)Math.Round(69 + 12 * Math.Log2(fundamentals[i] / 440.0));
                if (midiNote >= 0 && midiNote <= 127)
                {
                    significantFreqs.Add(midiNote);
                }
            }
        }

        if (significantFreqs.Count >= 2)
        {
            AnalyzeNotes(significantFreqs.ToArray());
        }
    }

    #endregion

    #region Private Methods

    private ChordMatch? DetectChord(int[] pitchClasses, int bassNote)
    {
        ChordMatch? bestMatch = null;
        double bestScore = 0;

        // Try each pitch class as potential root
        foreach (int potentialRoot in pitchClasses)
        {
            // Transpose pitch classes relative to potential root
            var intervals = pitchClasses.Select(p => (p - potentialRoot + 12) % 12).OrderBy(i => i).ToArray();

            // Match against chord templates
            foreach (var template in ChordTemplates)
            {
                double score = CalculateMatchScore(intervals, template.Value);

                // Bonus for bass being the root
                if (potentialRoot == bassNote)
                {
                    score *= 1.1;
                }

                // Slight penalty for slash chords
                if (potentialRoot != bassNote)
                {
                    score *= 0.95;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = new ChordMatch
                    {
                        Root = potentialRoot,
                        Quality = template.Key,
                        Confidence = Math.Min(score, 1.0)
                    };
                }
            }
        }

        return bestMatch;
    }

    private double CalculateMatchScore(int[] intervals, int[] template)
    {
        // Count matching intervals
        int matches = 0;
        foreach (int interval in template)
        {
            if (intervals.Contains(interval))
            {
                matches++;
            }
        }

        // Calculate score based on matches and extra notes
        double matchRatio = (double)matches / template.Length;
        int extraNotes = intervals.Length - matches;
        double extraPenalty = extraNotes * 0.1;

        return Math.Max(0, matchRatio - extraPenalty);
    }

    private void FindAlternatives(int[] pitchClasses, int bassNote, ChordMatch primaryMatch)
    {
        AlternativeChords.Clear();
        var alternatives = new List<(string name, ChordQuality quality, double confidence)>();

        foreach (int potentialRoot in pitchClasses)
        {
            var intervals = pitchClasses.Select(p => (p - potentialRoot + 12) % 12).OrderBy(i => i).ToArray();

            foreach (var template in ChordTemplates)
            {
                if (potentialRoot == primaryMatch.Root && template.Key == primaryMatch.Quality)
                {
                    continue; // Skip primary match
                }

                double score = CalculateMatchScore(intervals, template.Value);
                if (score >= 0.5) // Only show reasonable alternatives
                {
                    string name = FormatChordName(potentialRoot, template.Key, bassNote);
                    alternatives.Add((name, template.Key, score));
                }
            }
        }

        // Add top 5 alternatives
        foreach (var alt in alternatives.OrderByDescending(a => a.confidence).Take(5))
        {
            AlternativeChords.Add(new AlternativeChord
            {
                Name = alt.name,
                Quality = alt.quality,
                Confidence = alt.confidence
            });
        }
    }

    private string FormatChordName(int root, ChordQuality quality, int bass)
    {
        string rootName = NoteNames[root];
        string qualitySuffix = GetQualitySuffix(quality);
        string name = rootName + qualitySuffix;

        if (root != bass)
        {
            name += "/" + NoteNames[bass];
        }

        return name;
    }

    private static string GetQualitySuffix(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Major => "",
            ChordQuality.Minor => "m",
            ChordQuality.Diminished => "dim",
            ChordQuality.Augmented => "aug",
            ChordQuality.Sus2 => "sus2",
            ChordQuality.Sus4 => "sus4",
            ChordQuality.Major7 => "maj7",
            ChordQuality.Minor7 => "m7",
            ChordQuality.Dominant7 => "7",
            ChordQuality.Diminished7 => "dim7",
            ChordQuality.HalfDiminished7 => "m7b5",
            ChordQuality.MinorMajor7 => "mMaj7",
            ChordQuality.Augmented7 => "aug7",
            ChordQuality.Major6 => "6",
            ChordQuality.Minor6 => "m6",
            ChordQuality.Add9 => "add9",
            ChordQuality.Major9 => "maj9",
            ChordQuality.Minor9 => "m9",
            ChordQuality.Dominant9 => "9",
            _ => ""
        };
    }

    private static string GetQualityDisplayText(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Major => "Major",
            ChordQuality.Minor => "Minor",
            ChordQuality.Diminished => "Diminished",
            ChordQuality.Augmented => "Augmented",
            ChordQuality.Sus2 => "Suspended 2nd",
            ChordQuality.Sus4 => "Suspended 4th",
            ChordQuality.Major7 => "Major 7th",
            ChordQuality.Minor7 => "Minor 7th",
            ChordQuality.Dominant7 => "Dominant 7th",
            ChordQuality.Diminished7 => "Diminished 7th",
            ChordQuality.HalfDiminished7 => "Half-Diminished",
            ChordQuality.MinorMajor7 => "Minor Major 7th",
            ChordQuality.Augmented7 => "Augmented 7th",
            ChordQuality.Major6 => "Major 6th",
            ChordQuality.Minor6 => "Minor 6th",
            ChordQuality.Add9 => "Add 9",
            ChordQuality.Major9 => "Major 9th",
            ChordQuality.Minor9 => "Minor 9th",
            ChordQuality.Dominant9 => "Dominant 9th",
            _ => "Unknown"
        };
    }

    private void UpdateRomanNumeral()
    {
        if (!IsKeyKnown || string.IsNullOrEmpty(CurrentKey) || RootNote == "--")
        {
            RomanNumeral = "--";
            return;
        }

        // Parse key
        var keyParts = CurrentKey.Split(' ');
        if (keyParts.Length < 2) return;

        string keyRoot = keyParts[0];
        bool isMinorKey = keyParts[1].ToLower() == "minor";

        // Find root note index
        int keyRootIndex = Array.IndexOf(NoteNames, keyRoot);
        int chordRootIndex = Array.IndexOf(NoteNames, RootNote);

        if (keyRootIndex < 0 || chordRootIndex < 0) return;

        // Calculate scale degree
        int degree = (chordRootIndex - keyRootIndex + 12) % 12;

        // Map semitones to scale degrees for major/minor
        var majorDegrees = new Dictionary<int, string>
        {
            { 0, "I" }, { 2, "II" }, { 4, "III" }, { 5, "IV" },
            { 7, "V" }, { 9, "VI" }, { 11, "VII" }
        };

        var minorDegrees = new Dictionary<int, string>
        {
            { 0, "i" }, { 2, "ii" }, { 3, "III" }, { 5, "iv" },
            { 7, "v" }, { 8, "VI" }, { 10, "VII" }
        };

        var degrees = isMinorKey ? minorDegrees : majorDegrees;

        if (degrees.TryGetValue(degree, out string? numeral))
        {
            // Adjust for chord quality
            bool isMinorChord = Quality == ChordQuality.Minor ||
                               Quality == ChordQuality.Minor7 ||
                               Quality == ChordQuality.Minor9;
            bool isDiminished = Quality == ChordQuality.Diminished ||
                               Quality == ChordQuality.Diminished7 ||
                               Quality == ChordQuality.HalfDiminished7;
            bool isAugmented = Quality == ChordQuality.Augmented ||
                              Quality == ChordQuality.Augmented7;

            if (isMinorChord && !isMinorKey)
            {
                numeral = numeral.ToLower();
            }
            else if (!isMinorChord && isMinorKey && numeral == numeral.ToLower())
            {
                numeral = numeral.ToUpper();
            }

            if (isDiminished)
            {
                numeral = numeral.ToLower() + "\u00B0"; // degree symbol
            }
            else if (isAugmented)
            {
                numeral += "+";
            }

            // Add 7th indicator if applicable
            if (Quality == ChordQuality.Major7 || Quality == ChordQuality.Minor7 ||
                Quality == ChordQuality.Dominant7 || Quality == ChordQuality.Diminished7 ||
                Quality == ChordQuality.HalfDiminished7)
            {
                numeral += "7";
            }

            RomanNumeral = numeral;
        }
        else
        {
            // Non-diatonic chord
            RomanNumeral = "#" + degree;
        }
    }

    private void UpdateGuitarDiagram(string chordName)
    {
        // Try to find exact match first
        string searchKey = chordName.Split('/')[0]; // Remove bass note for slash chords

        if (GuitarChords.TryGetValue(searchKey, out var chord))
        {
            GuitarFrets = chord.frets;
            GuitarStartFret = chord.startFret;
            HasGuitarDiagram = true;
            return;
        }

        // Try simplified version (just root + major/minor)
        string simplified = RootNote;
        if (Quality == ChordQuality.Minor || Quality == ChordQuality.Minor7 || Quality == ChordQuality.Minor9)
        {
            simplified += "m";
        }

        if (GuitarChords.TryGetValue(simplified, out chord))
        {
            GuitarFrets = chord.frets;
            GuitarStartFret = chord.startFret;
            HasGuitarDiagram = true;
            return;
        }

        HasGuitarDiagram = false;
        GuitarFrets = new int[6] { -1, -1, -1, -1, -1, -1 };
    }

    private void AddToHistory(string chordName, ChordQuality quality, double confidence)
    {
        // Don't add duplicates consecutively
        if (ChordHistory.Count > 0 && ChordHistory[0].Name == chordName)
        {
            return;
        }

        ChordHistory.Insert(0, new ChordHistoryItem
        {
            Name = chordName,
            Quality = quality,
            Confidence = confidence,
            Timestamp = DateTime.Now
        });

        // Trim history
        while (ChordHistory.Count > MaxHistoryItems)
        {
            ChordHistory.RemoveAt(ChordHistory.Count - 1);
        }
    }

    #endregion

    #region Property Change Handlers

    partial void OnSensitivityChanged(double value)
    {
        // Sensitivity affects detection threshold
    }

    partial void OnCurrentKeyChanged(string value)
    {
        IsKeyKnown = !string.IsNullOrEmpty(value);
        UpdateRomanNumeral();
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Chord quality enumeration.
/// </summary>
public enum ChordQuality
{
    Unknown,
    Major,
    Minor,
    Diminished,
    Augmented,
    Sus2,
    Sus4,
    Major7,
    Minor7,
    Dominant7,
    Diminished7,
    HalfDiminished7,
    MinorMajor7,
    Augmented7,
    Major6,
    Minor6,
    Add9,
    Major9,
    Minor9,
    Dominant9
}

/// <summary>
/// Internal chord match result.
/// </summary>
internal class ChordMatch
{
    public int Root { get; set; }
    public ChordQuality Quality { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Alternative chord interpretation.
/// </summary>
public class AlternativeChord
{
    public string Name { get; set; } = string.Empty;
    public ChordQuality Quality { get; set; }
    public double Confidence { get; set; }
    public string ConfidenceText => $"{Confidence * 100:F0}%";
}

/// <summary>
/// Chord history item.
/// </summary>
public class ChordHistoryItem
{
    public string Name { get; set; } = string.Empty;
    public ChordQuality Quality { get; set; }
    public double Confidence { get; set; }
    public DateTime Timestamp { get; set; }
    public string TimeText => Timestamp.ToString("HH:mm:ss");
}

#endregion
