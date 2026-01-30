// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

public partial class ProjectExplorerView : UserControl
{
    public ProjectExplorerView()
    {
        InitializeComponent();
    }

    private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && item.DataContext is FileTreeNode node)
        {
            if (DataContext is ProjectExplorerViewModel vm)
            {
                vm.NodeDoubleClickCommand.Execute(node);
            }
            e.Handled = true;
        }
    }
}
