// MusicEngineEditor - Editor Undo Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MusicEngine.Core.UndoRedo;

namespace MusicEngineEditor.Services;

/// <summary>
/// Wrapper service around MusicEngine.Core.UndoRedo.UndoManager for editor-wide undo/redo functionality.
/// Implements INotifyPropertyChanged for UI binding support.
/// </summary>
public sealed class EditorUndoService : INotifyPropertyChanged, IDisposable
{
    private static EditorUndoService? _instance;
    private static readonly object _lock = new();

    private readonly UndoManager _undoManager;
    private bool _disposed;

    /// <summary>
    /// Gets the singleton instance of the EditorUndoService.
    /// </summary>
    public static EditorUndoService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new EditorUndoService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets whether there are commands that can be undone.
    /// </summary>
    public bool CanUndo => _undoManager.CanUndo;

    /// <summary>
    /// Gets whether there are commands that can be redone.
    /// </summary>
    public bool CanRedo => _undoManager.CanRedo;

    /// <summary>
    /// Gets the number of commands in the undo stack.
    /// </summary>
    public int UndoCount => _undoManager.UndoCount;

    /// <summary>
    /// Gets the number of commands in the redo stack.
    /// </summary>
    public int RedoCount => _undoManager.RedoCount;

    /// <summary>
    /// Gets the description of the next undo operation, or null if none.
    /// </summary>
    public string? NextUndoDescription => _undoManager.NextUndoDescription;

    /// <summary>
    /// Gets the description of the next redo operation, or null if none.
    /// </summary>
    public string? NextRedoDescription => _undoManager.NextRedoDescription;

    /// <summary>
    /// Gets the formatted undo menu text (e.g., "Undo Add Note").
    /// </summary>
    public string UndoMenuText => CanUndo ? $"Undo {NextUndoDescription}" : "Undo";

    /// <summary>
    /// Gets the formatted redo menu text (e.g., "Redo Add Note").
    /// </summary>
    public string RedoMenuText => CanRedo ? $"Redo {NextRedoDescription}" : "Redo";

    /// <summary>
    /// Gets the list of undo command descriptions (most recent first).
    /// </summary>
    public IReadOnlyList<string> UndoDescriptions => _undoManager.GetUndoHistory();

    /// <summary>
    /// Gets the list of redo command descriptions (most recent first).
    /// </summary>
    public IReadOnlyList<string> RedoDescriptions => _undoManager.GetRedoHistory();

    /// <summary>
    /// Gets the underlying UndoManager for advanced operations.
    /// </summary>
    public UndoManager UndoManager => _undoManager;

    /// <summary>
    /// Gets the maximum number of commands kept in history.
    /// </summary>
    public int MaxHistorySize => _undoManager.MaxHistorySize;

    /// <summary>
    /// Event raised when the undo/redo state changes.
    /// </summary>
    public event EventHandler? UndoStackChanged;

    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Event raised before a command is executed.
    /// </summary>
    public event EventHandler<CommandEventArgs>? CommandExecuting;

    /// <summary>
    /// Event raised after a command is executed.
    /// </summary>
    public event EventHandler<CommandEventArgs>? CommandExecuted;

    /// <summary>
    /// Creates a new EditorUndoService with the specified history size.
    /// </summary>
    /// <param name="maxHistorySize">Maximum number of commands to keep in history.</param>
    private EditorUndoService(int maxHistorySize = 200)
    {
        _undoManager = new UndoManager(maxHistorySize);
        _undoManager.StateChanged += OnUndoManagerStateChanged;
        _undoManager.CommandExecuting += OnUndoManagerCommandExecuting;
        _undoManager.CommandExecuted += OnUndoManagerCommandExecuted;
    }

    /// <summary>
    /// Creates a new instance for dependency injection scenarios.
    /// Note: Prefer using the Instance singleton for most cases.
    /// </summary>
    /// <param name="maxHistorySize">Maximum number of commands to keep in history.</param>
    /// <returns>A new EditorUndoService instance.</returns>
    public static EditorUndoService CreateInstance(int maxHistorySize = 200)
    {
        return new EditorUndoService(maxHistorySize);
    }

    /// <summary>
    /// Executes a command and adds it to the undo stack.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    public void Execute(IUndoableCommand command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(command);
        _undoManager.Execute(command);
    }

    /// <summary>
    /// Undoes the most recent command.
    /// </summary>
    /// <returns>True if a command was undone, false if the undo stack was empty.</returns>
    public bool Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _undoManager.Undo();
    }

    /// <summary>
    /// Redoes the most recently undone command.
    /// </summary>
    /// <returns>True if a command was redone, false if the redo stack was empty.</returns>
    public bool Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _undoManager.Redo();
    }

    /// <summary>
    /// Clears all undo and redo history.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _undoManager.Clear();
    }

    /// <summary>
    /// Begins a batch operation that groups multiple commands into one undo step.
    /// </summary>
    /// <param name="description">Description for the batch.</param>
    /// <returns>A disposable batch that commits on dispose.</returns>
    public UndoBatch BeginBatch(string description)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _undoManager.BeginBatch(description);
    }

    /// <summary>
    /// Performs multiple undos.
    /// </summary>
    /// <param name="count">Number of commands to undo.</param>
    /// <returns>Number of commands actually undone.</returns>
    public int UndoMultiple(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int undone = 0;
        for (int i = 0; i < count && _undoManager.Undo(); i++)
        {
            undone++;
        }
        return undone;
    }

    /// <summary>
    /// Performs multiple redos.
    /// </summary>
    /// <param name="count">Number of commands to redo.</param>
    /// <returns>Number of commands actually redone.</returns>
    public int RedoMultiple(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int redone = 0;
        for (int i = 0; i < count && _undoManager.Redo(); i++)
        {
            redone++;
        }
        return redone;
    }

    private void OnUndoManagerStateChanged(object? sender, EventArgs e)
    {
        RaiseAllPropertyChanges();
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnUndoManagerCommandExecuting(object? sender, CommandEventArgs e)
    {
        CommandExecuting?.Invoke(this, e);
    }

    private void OnUndoManagerCommandExecuted(object? sender, CommandEventArgs e)
    {
        CommandExecuted?.Invoke(this, e);
    }

    private void RaiseAllPropertyChanges()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
        OnPropertyChanged(nameof(NextUndoDescription));
        OnPropertyChanged(nameof(NextRedoDescription));
        OnPropertyChanged(nameof(UndoMenuText));
        OnPropertyChanged(nameof(RedoMenuText));
        OnPropertyChanged(nameof(UndoDescriptions));
        OnPropertyChanged(nameof(RedoDescriptions));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Disposes the service and clears history.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _undoManager.StateChanged -= OnUndoManagerStateChanged;
        _undoManager.CommandExecuting -= OnUndoManagerCommandExecuting;
        _undoManager.CommandExecuted -= OnUndoManagerCommandExecuted;
        _undoManager.Clear();
        _disposed = true;
    }
}
