// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: About dialog window.

using System.Windows;

namespace MusicEngineEditor.Views.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    public static void Show(Window? owner = null)
    {
        var dialog = new AboutDialog
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
