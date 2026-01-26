// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System.Windows.Controls;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

public partial class OutputView : UserControl
{
    public OutputView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            if (DataContext is OutputViewModel vm)
            {
                vm.OutputChanged += OnOutputChanged;
            }
        };
    }

    private void OnOutputChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is OutputViewModel vm && vm.AutoScroll)
        {
            OutputTextBox.ScrollToEnd();
        }
    }
}
