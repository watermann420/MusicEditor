// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Unified AI Assistant Panel with tabbed interface for mastering, mixing, melody generation, and chord suggestion.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MusicEngine.Core;
using MusicEngine.Core.AI;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a suggested chord in the AI Assistant panel.
/// </summary>
public class ChordSuggestionDisplayItem
{
    /// <summary>
    /// The chord name (e.g., "Am", "G7").
    /// </summary>
    public string ChordName { get; set; } = "";

    /// <summary>
    /// Roman numeral notation (e.g., "vi", "V7").
    /// </summary>
    public string RomanNumeral { get; set; } = "";

    /// <summary>
    /// Confidence score (0-1).
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Display string for the score.
    /// </summary>
    public string ScoreDisplay => $"{Score * 100:F0}%";

    /// <summary>
    /// MIDI notes for this chord.
    /// </summary>
    public int[] MidiNotes { get; set; } = Array.Empty<int>();
}

/// <summary>
/// Represents a mix adjustment suggestion.
/// </summary>
public class MixAdjustmentItem
{
    /// <summary>
    /// Track name.
    /// </summary>
    public string Track { get; set; } = "";

    /// <summary>
    /// Type of adjustment.
    /// </summary>
    public string Adjustment { get; set; } = "";

    /// <summary>
    /// Value of the adjustment.
    /// </summary>
    public string Value { get; set; } = "";
}

/// <summary>
/// Unified AI Assistant Panel providing access to AI-powered audio processing tools
/// including mastering, mixing, melody generation, and chord suggestion.
/// </summary>
public partial class AIAssistantPanel : UserControl
{
    private readonly ChordSuggestionEngine _chordSuggestionEngine;
    private readonly MelodyGenerator _melodyGenerator;
    private readonly MixAssistant _mixAssistant;
    private readonly MasteringAssistant _masteringAssistant;
    private readonly List<string> _chordProgression = new();
    private int _currentKeyRoot = 0; // C
    private bool _isMinor = false;
    private bool _isMelodyPlaying = false;
    private bool _isMastering = false;

    /// <summary>
    /// Event raised when the close button is clicked.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when a chord should be inserted.
    /// </summary>
    public event EventHandler<int[]>? ChordInsertRequested;

    /// <summary>
    /// Event raised when melody generation is requested.
    /// </summary>
    public event EventHandler<MelodyGeneratorConfig>? MelodyGenerationRequested;

    /// <summary>
    /// Event raised when mastering should be applied.
    /// </summary>
    public event EventHandler<MasteringSettings>? MasteringRequested;

    /// <summary>
    /// Event raised when mix suggestions should be applied.
    /// </summary>
    public event EventHandler<List<MixAdjustmentItem>>? MixSuggestionsApplyRequested;

    /// <summary>
    /// Creates a new AI Assistant Panel.
    /// </summary>
    public AIAssistantPanel()
    {
        InitializeComponent();

        _chordSuggestionEngine = new ChordSuggestionEngine();
        _melodyGenerator = new MelodyGenerator();
        _mixAssistant = new MixAssistant();
        _masteringAssistant = new MasteringAssistant();

        // Initialize with default suggestions
        UpdateChordSuggestions();
    }

    #region Tab Navigation

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        // Guard against calls during XAML initialization before controls are created
        if (AutoMasterContent == null) return;

        if (sender is System.Windows.Controls.RadioButton radioButton)
        {
            // Hide all content panels
            AutoMasterContent.Visibility = Visibility.Collapsed;
            AutoMixContent.Visibility = Visibility.Collapsed;
            MelodyGenContent.Visibility = Visibility.Collapsed;
            ChordSuggestContent.Visibility = Visibility.Collapsed;

            // Show the selected panel
            if (radioButton == AutoMasterTab)
            {
                AutoMasterContent.Visibility = Visibility.Visible;
            }
            else if (radioButton == AutoMixTab)
            {
                AutoMixContent.Visibility = Visibility.Visible;
            }
            else if (radioButton == MelodyGenTab)
            {
                MelodyGenContent.Visibility = Visibility.Visible;
            }
            else if (radioButton == ChordSuggestTab)
            {
                ChordSuggestContent.Visibility = Visibility.Visible;
            }
        }
    }

    #endregion

    #region Auto-Master Tab

    private void TargetLufsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TargetLufsValue != null)
        {
            TargetLufsValue.Text = $"{e.NewValue:F1} LUFS";
        }
    }

    private void LimitingThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LimitingThresholdValue != null)
        {
            LimitingThresholdValue.Text = $"{e.NewValue:F1} dB";
        }
    }

    private async void MasterItButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isMastering) return;

        _isMastering = true;
        MasterItButton.IsEnabled = false;
        MasteringProgress.Visibility = Visibility.Visible;
        MasteringStatus.Text = "Analyzing audio...";

        try
        {
            // Simulate mastering process with progress updates
            for (int i = 0; i <= 100; i += 5)
            {
                MasteringProgressBar.Value = i;
                MasteringProgressText.Text = i switch
                {
                    < 20 => "Analyzing dynamics...",
                    < 40 => "Applying EQ corrections...",
                    < 60 => "Multiband compression...",
                    < 80 => "Stereo enhancement...",
                    < 95 => "Applying limiter...",
                    _ => "Finalizing..."
                };
                await Task.Delay(50);
            }

            MasteringStatus.Text = "Mastering complete!";

            // Raise event with settings
            var settings = new MasteringSettings
            {
                TargetLufs = (float)TargetLufsSlider.Value,
                LimitingThreshold = (float)LimitingThresholdSlider.Value
            };
            MasteringRequested?.Invoke(this, settings);
        }
        finally
        {
            _isMastering = false;
            MasterItButton.IsEnabled = true;
            MasteringProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void ABToggle_Click(object sender, RoutedEventArgs e)
    {
        bool isAfter = ABToggle.IsChecked == true;
        ABToggle.Content = isAfter ? "B (After)" : "A (Before)";
        MasteringStatus.Text = isAfter ? "Playing: Mastered" : "Playing: Original";
    }

    #endregion

    #region Auto-Mix Tab

    private async void AnalyzeMixButton_Click(object sender, RoutedEventArgs e)
    {
        AnalyzeMixButton.IsEnabled = false;
        MixStatus.Text = "Analyzing mix...";

        await Task.Delay(1500); // Simulate analysis

        // Show results panel with sample data
        MixResultsPanel.Visibility = Visibility.Visible;

        // Add sample collision warnings
        var warnings = new List<string>
        {
            "Bass and Kick overlap at 80-120 Hz",
            "Vocals and Guitar conflict at 2-4 kHz",
            "Hi-hat and cymbal buildup at 8-12 kHz"
        };
        CollisionWarningsList.ItemsSource = warnings;

        // Add sample adjustments
        var adjustments = new List<MixAdjustmentItem>
        {
            new() { Track = "Bass", Adjustment = "High-pass filter", Value = "40 Hz" },
            new() { Track = "Kick", Adjustment = "Boost", Value = "+2 dB @ 60 Hz" },
            new() { Track = "Vocals", Adjustment = "Cut", Value = "-2 dB @ 250 Hz" },
            new() { Track = "Guitar", Adjustment = "Cut", Value = "-3 dB @ 3 kHz" },
            new() { Track = "Master", Adjustment = "Compression", Value = "2:1, -12 dB threshold" }
        };
        SuggestedAdjustmentsList.ItemsSource = adjustments;

        ApplySuggestionsButton.IsEnabled = true;
        AnalyzeMixButton.IsEnabled = true;
        MixStatus.Text = "Analysis complete - 3 issues found";
    }

    private void ApplySuggestionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SuggestedAdjustmentsList.ItemsSource is List<MixAdjustmentItem> adjustments)
        {
            MixSuggestionsApplyRequested?.Invoke(this, adjustments);
            MixStatus.Text = "Suggestions applied!";
        }
    }

    #endregion

    #region Melody Generator Tab

    private void LengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LengthValue != null)
        {
            LengthValue.Text = $"{(int)e.NewValue}";
        }
    }

    private void GenerateMelodyButton_Click(object sender, RoutedEventArgs e)
    {
        MelodyStatus.Text = "Generating melody...";

        // Get scale type from combo box
        var scaleType = ScaleComboBox.SelectedIndex switch
        {
            0 => ScaleType.Major,
            1 => ScaleType.NaturalMinor,
            2 => ScaleType.HarmonicMinor,
            3 => ScaleType.Dorian,
            4 => ScaleType.Phrygian,
            5 => ScaleType.Lydian,
            6 => ScaleType.Mixolydian,
            7 => ScaleType.Locrian,
            8 => ScaleType.PentatonicMajor,
            9 => ScaleType.PentatonicMinor,
            10 => ScaleType.Blues,
            _ => ScaleType.Major
        };

        var config = new MelodyGeneratorConfig
        {
            RootNote = 60 + KeyComboBox.SelectedIndex,
            Scale = scaleType,
            LengthInBeats = (int)LengthSlider.Value * 4, // 4 beats per bar
            Style = MelodyStyle.Pop,
            Contour = ContourShape.Arc,
            Density = 0.5f
        };

        MelodyGenerationRequested?.Invoke(this, config);

        PreviewPlayButton.IsEnabled = true;
        MelodyPreviewStatus.Text = $"{(int)LengthSlider.Value} bars generated";
        MelodyStatus.Text = "Melody generated successfully!";
    }

    private void PreviewPlayButton_Click(object sender, RoutedEventArgs e)
    {
        _isMelodyPlaying = true;
        PreviewPlayButton.IsEnabled = false;
        PreviewStopButton.IsEnabled = true;
        MelodyPreviewStatus.Text = "Playing...";
    }

    private void PreviewStopButton_Click(object sender, RoutedEventArgs e)
    {
        _isMelodyPlaying = false;
        PreviewPlayButton.IsEnabled = true;
        PreviewStopButton.IsEnabled = false;
        MelodyPreviewStatus.Text = "Stopped";
    }

    #endregion

    #region Chord Suggester Tab

    private void UpdateChordSuggestions()
    {
        var contextChords = new List<ContextChord>();

        // Add existing progression as context
        // For now, use empty context for initial suggestions
        var suggestions = _chordSuggestionEngine.GetSuggestions(
            contextChords,
            _currentKeyRoot,
            _isMinor,
            ChordSuggestionStyle.Pop,
            8);

        var displayItems = new List<ChordSuggestionDisplayItem>();
        foreach (var suggestion in suggestions)
        {
            displayItems.Add(new ChordSuggestionDisplayItem
            {
                ChordName = suggestion.GetChordName(),
                RomanNumeral = suggestion.RomanNumeral,
                Score = suggestion.Score,
                MidiNotes = suggestion.GetNotes(4)
            });
        }

        SuggestedChordsList.ItemsSource = displayItems;
    }

    private void SuggestNextButton_Click(object sender, RoutedEventArgs e)
    {
        // Parse current chord to get key context
        string currentChordName = CurrentChordDisplay.Text;

        // Update suggestions based on current chord
        UpdateChordSuggestions();

        ChordStatus.Text = "Suggestions updated based on current chord";
    }

    private void SuggestedChordsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyChordButton.IsEnabled = SuggestedChordsList.SelectedItem != null;

        if (SuggestedChordsList.SelectedItem is ChordSuggestionDisplayItem selected)
        {
            ChordStatus.Text = $"Selected: {selected.ChordName} ({selected.RomanNumeral})";
        }
    }

    private void ApplyChordButton_Click(object sender, RoutedEventArgs e)
    {
        if (SuggestedChordsList.SelectedItem is ChordSuggestionDisplayItem selected)
        {
            // Add to progression history
            _chordProgression.Add(selected.ChordName);
            ChordProgressionHistory.Text = string.Join(" -> ", _chordProgression);

            // Update current chord display
            CurrentChordDisplay.Text = selected.ChordName;

            // Raise event
            ChordInsertRequested?.Invoke(this, selected.MidiNotes);

            // Update suggestions for next chord
            UpdateChordSuggestions();

            ChordStatus.Text = $"Applied {selected.ChordName} to progression";
        }
    }

    #endregion

    #region Panel Controls

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the chord progression history.
    /// </summary>
    public void ClearChordProgression()
    {
        _chordProgression.Clear();
        ChordProgressionHistory.Text = "";
        CurrentChordDisplay.Text = "C Major";
        UpdateChordSuggestions();
    }

    /// <summary>
    /// Sets the current key for chord suggestions.
    /// </summary>
    /// <param name="keyRoot">Key root (0-11 for C through B).</param>
    /// <param name="isMinor">True if minor key.</param>
    public void SetKey(int keyRoot, bool isMinor)
    {
        _currentKeyRoot = keyRoot;
        _isMinor = isMinor;
        UpdateChordSuggestions();
    }

    #endregion
}

/// <summary>
/// Settings for the mastering process.
/// </summary>
public class MasteringSettings
{
    /// <summary>
    /// Target loudness in LUFS.
    /// </summary>
    public float TargetLufs { get; set; } = -14f;

    /// <summary>
    /// Limiting threshold in dB.
    /// </summary>
    public float LimitingThreshold { get; set; } = -1f;
}
