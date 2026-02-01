// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Advanced Synthesizer control.

using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for AdvancedSynthControl.xaml.
/// A multi-oscillator synthesizer with per-osc controls, filter types, LFO, and ADSR.
/// </summary>
public partial class AdvancedSynthControl : UserControl
{
    /// <summary>
    /// Creates a new AdvancedSynthControl.
    /// </summary>
    public AdvancedSynthControl()
    {
        InitializeComponent();
    }
}
