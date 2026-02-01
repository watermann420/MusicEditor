// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Polyphonic Synthesizer control.

using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for PolySynthControl.xaml.
/// A polyphonic synthesizer with voice management, ADSR, vibrato, and filter LFO.
/// </summary>
public partial class PolySynthControl : UserControl
{
    /// <summary>
    /// Creates a new PolySynthControl.
    /// </summary>
    public PolySynthControl()
    {
        InitializeComponent();
    }
}
