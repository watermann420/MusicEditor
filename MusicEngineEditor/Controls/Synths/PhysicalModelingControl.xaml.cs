// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Physical Modeling Synthesizer control.

using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for PhysicalModelingControl.xaml.
/// A physical modeling synthesizer with various instrument models.
/// </summary>
public partial class PhysicalModelingControl : UserControl
{
    /// <summary>
    /// Creates a new PhysicalModelingControl.
    /// </summary>
    public PhysicalModelingControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles model type selection changes to show/hide model-specific panels.
    /// </summary>
    private void ModelTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PluckedStringPanel == null) return; // Not yet initialized

        // Hide all panels
        PluckedStringPanel.Visibility = Visibility.Collapsed;
        BowedStringPanel.Visibility = Visibility.Collapsed;
        DrumMembranePanel.Visibility = Visibility.Collapsed;
        WindTubePanel.Visibility = Visibility.Collapsed;
        BellPanel.Visibility = Visibility.Collapsed;

        // Show selected panel
        switch (ModelTypeCombo.SelectedIndex)
        {
            case 0: // Plucked String
                PluckedStringPanel.Visibility = Visibility.Visible;
                break;
            case 1: // Bowed String
                BowedStringPanel.Visibility = Visibility.Visible;
                break;
            case 2: // Drum Membrane
                DrumMembranePanel.Visibility = Visibility.Visible;
                break;
            case 3: // Wind Tube
                WindTubePanel.Visibility = Visibility.Visible;
                break;
            case 4: // Bell
                BellPanel.Visibility = Visibility.Visible;
                break;
        }
    }
}
