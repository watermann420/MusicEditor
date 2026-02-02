// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog for customizing Quick Actions toolbar.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MusicEngineEditor.Controls;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for customizing the Quick Actions toolbar.
/// </summary>
public partial class CustomizeQuickActionsDialog : Window
{
    private readonly ObservableCollection<QuickActionItem> _actions;
    private readonly List<QuickActionItem> _originalActions;

    public CustomizeQuickActionsDialog(ObservableCollection<QuickActionItem> currentActions)
    {
        InitializeComponent();

        // Create a working copy
        _originalActions = currentActions.ToList();
        _actions = new ObservableCollection<QuickActionItem>();

        foreach (var action in currentActions)
        {
            _actions.Add(new QuickActionItem
            {
                Id = action.Id,
                Name = action.Name,
                Icon = action.Icon,
                Category = action.Category,
                Order = action.Order,
                IsVisible = action.IsVisible,
                Tooltip = action.Tooltip
            });
        }

        ActionsListBox.ItemsSource = _actions;
        UpdateMoveButtonStates();
    }

    /// <summary>
    /// Gets the configured actions list.
    /// </summary>
    public List<QuickActionItem> GetConfiguration()
    {
        // Update order based on current position
        for (int i = 0; i < _actions.Count; i++)
        {
            _actions[i].Order = i;
        }
        return _actions.ToList();
    }

    private void ActionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMoveButtonStates();
    }

    private void UpdateMoveButtonStates()
    {
        var selectedIndex = ActionsListBox.SelectedIndex;
        MoveUpButton.IsEnabled = selectedIndex > 0;
        MoveDownButton.IsEnabled = selectedIndex >= 0 && selectedIndex < _actions.Count - 1;
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var selectedIndex = ActionsListBox.SelectedIndex;
        if (selectedIndex > 0)
        {
            var item = _actions[selectedIndex];
            _actions.RemoveAt(selectedIndex);
            _actions.Insert(selectedIndex - 1, item);
            ActionsListBox.SelectedIndex = selectedIndex - 1;
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var selectedIndex = ActionsListBox.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < _actions.Count - 1)
        {
            var item = _actions[selectedIndex];
            _actions.RemoveAt(selectedIndex);
            _actions.Insert(selectedIndex + 1, item);
            ActionsListBox.SelectedIndex = selectedIndex + 1;
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Reset all quick actions to their default configuration?",
            "Reset to Defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Restore original actions and reset visibility
            _actions.Clear();
            foreach (var action in _originalActions)
            {
                _actions.Add(new QuickActionItem
                {
                    Id = action.Id,
                    Name = action.Name,
                    Icon = action.Icon,
                    Category = action.Category,
                    Order = action.Order,
                    IsVisible = true, // Reset all to visible
                    Tooltip = action.Tooltip
                });
            }

            // Sort by original order
            var sorted = _actions.OrderBy(a => a.Order).ToList();
            _actions.Clear();
            foreach (var item in sorted)
            {
                _actions.Add(item);
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
