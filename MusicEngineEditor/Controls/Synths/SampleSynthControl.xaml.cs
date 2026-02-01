// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Sample-based Synthesizer control.

using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for SampleSynthControl.xaml.
/// A sample-based synthesizer with key/velocity mapping, loop settings, and filter.
/// </summary>
public partial class SampleSynthControl : UserControl
{
    /// <summary>
    /// Creates a new SampleSynthControl.
    /// </summary>
    public SampleSynthControl()
    {
        InitializeComponent();
    }
}
