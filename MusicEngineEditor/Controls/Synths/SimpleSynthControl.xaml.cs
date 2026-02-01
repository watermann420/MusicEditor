// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Simple Synthesizer control.

using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for SimpleSynthControl.xaml.
/// A basic monophonic synthesizer with waveform selection, filter, and ADSR envelope.
/// </summary>
public partial class SimpleSynthControl : UserControl
{
    /// <summary>
    /// Creates a new SimpleSynthControl.
    /// </summary>
    public SimpleSynthControl()
    {
        InitializeComponent();
    }
}
