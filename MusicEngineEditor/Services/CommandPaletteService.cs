using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing and searching palette commands.
/// </summary>
public class CommandPaletteService
{
    private static CommandPaletteService? _instance;
    private readonly List<PaletteCommand> _commands = [];
    private readonly Dictionary<string, PaletteCommand> _commandsById = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the singleton instance of the command palette service.
    /// </summary>
    public static CommandPaletteService Instance => _instance ??= new CommandPaletteService();

    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    public IReadOnlyList<PaletteCommand> Commands => _commands.AsReadOnly();

    /// <summary>
    /// Event raised when a command is executed.
    /// </summary>
    public event EventHandler<PaletteCommand>? CommandExecuted;

    private CommandPaletteService()
    {
    }

    /// <summary>
    /// Registers a command with the palette.
    /// </summary>
    /// <param name="command">The command to register.</param>
    public void RegisterCommand(PaletteCommand command)
    {
        if (command == null) return;

        var key = $"{command.Category}:{command.Name}";
        if (_commandsById.ContainsKey(key))
        {
            // Update existing
            var existing = _commandsById[key];
            _commands.Remove(existing);
        }

        _commands.Add(command);
        _commandsById[key] = command;
    }

    /// <summary>
    /// Registers a command with the palette.
    /// </summary>
    public void RegisterCommand(string name, string category, ICommand command, string? shortcut = null, string? description = null, string[]? keywords = null)
    {
        RegisterCommand(new PaletteCommand
        {
            Name = name,
            Category = category,
            Command = command,
            Shortcut = shortcut,
            Description = description,
            Keywords = keywords
        });
    }

    /// <summary>
    /// Registers a command with an action.
    /// </summary>
    public void RegisterCommand(string name, string category, Action action, string? shortcut = null, string? description = null, string[]? keywords = null)
    {
        RegisterCommand(new PaletteCommand
        {
            Name = name,
            Category = category,
            Command = new RelayCommand(action),
            Shortcut = shortcut,
            Description = description,
            Keywords = keywords
        });
    }

    /// <summary>
    /// Unregisters a command.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="category">The command category.</param>
    public void UnregisterCommand(string name, string category)
    {
        var key = $"{category}:{name}";
        if (_commandsById.TryGetValue(key, out var command))
        {
            _commands.Remove(command);
            _commandsById.Remove(key);
        }
    }

    /// <summary>
    /// Clears all registered commands.
    /// </summary>
    public void ClearCommands()
    {
        _commands.Clear();
        _commandsById.Clear();
    }

    /// <summary>
    /// Searches commands by query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>Matching commands sorted by relevance.</returns>
    public IEnumerable<PaletteCommand> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            // Return all commands sorted by category then name
            return _commands
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Name);
        }

        // Filter and sort by relevance
        return _commands
            .Where(c => c.MatchesQuery(query))
            .OrderByDescending(c => c.GetRelevanceScore(query))
            .ThenBy(c => c.Name);
    }

    /// <summary>
    /// Gets commands in a specific category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>Commands in the category.</returns>
    public IEnumerable<PaletteCommand> GetByCategory(string category)
    {
        return _commands
            .Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name);
    }

    /// <summary>
    /// Executes a command by name and category.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="category">The command category.</param>
    /// <returns>True if the command was executed.</returns>
    public bool ExecuteCommand(string name, string category)
    {
        var key = $"{category}:{name}";
        if (_commandsById.TryGetValue(key, out var command) && command.CanExecute)
        {
            command.Execute();
            CommandExecuted?.Invoke(this, command);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Executes a command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    public void ExecuteCommand(PaletteCommand command)
    {
        if (command?.CanExecute == true)
        {
            command.Execute();
            CommandExecuted?.Invoke(this, command);
        }
    }

    /// <summary>
    /// Simple relay command implementation.
    /// </summary>
    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();
    }
}
