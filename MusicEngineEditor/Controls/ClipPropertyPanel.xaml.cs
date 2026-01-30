// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System.Windows.Controls;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Properties panel for editing AudioClip parameters including gain, fades, time stretch, and reverse.
/// </summary>
public partial class ClipPropertyPanel : UserControl
{
    public ClipPropertyPanel()
    {
        InitializeComponent();
    }
}
