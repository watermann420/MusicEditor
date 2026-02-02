// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Speech Synthesizer control.

using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for SpeechSynthControl.xaml.
/// A speech/vocal synthesizer with formant control, vowel selection, and singing mode.
/// </summary>
public partial class SpeechSynthControl : UserControl
{
    /// <summary>
    /// Creates a new SpeechSynthControl.
    /// </summary>
    public SpeechSynthControl()
    {
        InitializeComponent();
    }
}
