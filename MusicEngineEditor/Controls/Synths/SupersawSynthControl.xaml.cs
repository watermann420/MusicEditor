// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Supersaw Synthesizer control.

using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for SupersawSynthControl.xaml.
/// A supersaw synthesizer with unison, detune, stereo spread, and filter envelope.
/// </summary>
public partial class SupersawSynthControl : UserControl
{
    /// <summary>
    /// Creates a new SupersawSynthControl.
    /// </summary>
    public SupersawSynthControl()
    {
        InitializeComponent();
    }
}
