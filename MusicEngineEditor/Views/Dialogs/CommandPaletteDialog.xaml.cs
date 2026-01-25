using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Quick command palette dialog for keyboard-driven command execution.
/// </summary>
public partial class CommandPaletteDialog : Window
{
    private readonly List<PaletteCommand> _allCommands;
    private List<PaletteCommand> _filteredCommands;

    /// <summary>
    /// Gets the selected command, if any.
    /// </summary>
    public PaletteCommand? SelectedCommand { get; private set; }

    /// <summary>
    /// Creates a new command palette dialog.
    /// </summary>
    public CommandPaletteDialog()
    {
        InitializeComponent();

        _allCommands = CommandPaletteService.Instance.Commands.ToList();
        _filteredCommands = _allCommands;

        UpdateCommandList();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Focus the search box
        SearchBox.Focus();

        // Select first item if available
        if (CommandList.Items.Count > 0)
        {
            CommandList.SelectedIndex = 0;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text;

        // Update placeholder visibility
        PlaceholderText.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

        // Filter commands
        _filteredCommands = CommandPaletteService.Instance.Search(query).ToList();
        UpdateCommandList();

        // Update status text
        StatusText.Text = _filteredCommands.Count == 0
            ? "No matching commands"
            : $"{_filteredCommands.Count} command{(_filteredCommands.Count == 1 ? "" : "s")} found";
    }

    private void UpdateCommandList()
    {
        CommandList.ItemsSource = _filteredCommands;

        // Select first item
        if (_filteredCommands.Count > 0)
        {
            CommandList.SelectedIndex = 0;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                DialogResult = false;
                Close();
                e.Handled = true;
                break;

            case Key.Enter:
                ExecuteSelectedCommand();
                e.Handled = true;
                break;

            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;

            case Key.PageUp:
                MoveSelection(-10);
                e.Handled = true;
                break;

            case Key.PageDown:
                MoveSelection(10);
                e.Handled = true;
                break;

            case Key.Home:
                if (Keyboard.Modifiers == ModifierKeys.Control && CommandList.Items.Count > 0)
                {
                    CommandList.SelectedIndex = 0;
                    CommandList.ScrollIntoView(CommandList.SelectedItem);
                    e.Handled = true;
                }
                break;

            case Key.End:
                if (Keyboard.Modifiers == ModifierKeys.Control && CommandList.Items.Count > 0)
                {
                    CommandList.SelectedIndex = CommandList.Items.Count - 1;
                    CommandList.ScrollIntoView(CommandList.SelectedItem);
                    e.Handled = true;
                }
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (CommandList.Items.Count == 0) return;

        int newIndex = CommandList.SelectedIndex + delta;
        newIndex = Math.Clamp(newIndex, 0, CommandList.Items.Count - 1);

        CommandList.SelectedIndex = newIndex;
        CommandList.ScrollIntoView(CommandList.SelectedItem);
    }

    private void ExecuteSelectedCommand()
    {
        if (CommandList.SelectedItem is PaletteCommand command)
        {
            SelectedCommand = command;
            DialogResult = true;
            Close();

            // Execute the command
            CommandPaletteService.Instance.ExecuteCommand(command);
        }
    }

    private void CommandList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelectedCommand();
    }

    private void CommandList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ensure selection is visible
        if (CommandList.SelectedItem != null)
        {
            CommandList.ScrollIntoView(CommandList.SelectedItem);
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Close when focus is lost
        if (IsVisible)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Shows the command palette and returns the selected command, if any.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <returns>The selected command, or null if cancelled.</returns>
    public static PaletteCommand? ShowPalette(Window owner)
    {
        var dialog = new CommandPaletteDialog
        {
            Owner = owner
        };

        var result = dialog.ShowDialog();

        return result == true ? dialog.SelectedCommand : null;
    }
}
