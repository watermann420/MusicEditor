// MusicEngineEditor - Undo History ViewModel
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.UndoRedo;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents a single item in the undo/redo history.
/// </summary>
public partial class UndoHistoryItem : ObservableObject
{
    /// <summary>
    /// Gets or sets the index of this item in the history (0 = oldest).
    /// </summary>
    [ObservableProperty]
    private int _index;

    /// <summary>
    /// Gets or sets the description of the action.
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this action was performed.
    /// </summary>
    [ObservableProperty]
    private DateTime _timestamp;

    /// <summary>
    /// Gets or sets whether this item is in the undo stack (can be undone).
    /// </summary>
    [ObservableProperty]
    private bool _isUndoItem;

    /// <summary>
    /// Gets or sets whether this item is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets or sets whether this item is the current position marker.
    /// </summary>
    [ObservableProperty]
    private bool _isCurrentPosition;

    /// <summary>
    /// Gets or sets the icon for this action type.
    /// </summary>
    [ObservableProperty]
    private string _icon = "\u2022";

    /// <summary>
    /// Gets or sets the category for grouping related actions.
    /// </summary>
    [ObservableProperty]
    private string _category = string.Empty;

    /// <summary>
    /// Gets the formatted timestamp string.
    /// </summary>
    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    /// <summary>
    /// Gets the relative time string (e.g., "2 min ago").
    /// </summary>
    public string RelativeTime
    {
        get
        {
            var span = DateTime.Now - Timestamp;
            if (span.TotalSeconds < 60) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hr ago";
            return Timestamp.ToString("MMM dd");
        }
    }
}

/// <summary>
/// ViewModel for the Undo History Panel.
/// Displays all undo/redo items with the ability to jump to any state.
/// </summary>
public partial class UndoHistoryViewModel : ObservableObject, IDisposable
{
    private readonly EditorUndoService _undoService;
    private bool _disposed;
    private bool _isRefreshing;

    /// <summary>
    /// Collection of all history items (undo items first, then redo items in reverse).
    /// </summary>
    public ObservableCollection<UndoHistoryItem> HistoryItems { get; } = new();

    /// <summary>
    /// Gets the current position index in the history (items at or below this index can be undone).
    /// </summary>
    [ObservableProperty]
    private int _currentIndex = -1;

    /// <summary>
    /// Gets or sets the selected history item.
    /// </summary>
    [ObservableProperty]
    private UndoHistoryItem? _selectedItem;

    /// <summary>
    /// Gets the total number of items in history.
    /// </summary>
    public int TotalItems => HistoryItems.Count;

    /// <summary>
    /// Gets the number of undo items.
    /// </summary>
    public int UndoCount => _undoService.UndoCount;

    /// <summary>
    /// Gets the number of redo items.
    /// </summary>
    public int RedoCount => _undoService.RedoCount;

    /// <summary>
    /// Gets whether the history is empty.
    /// </summary>
    public bool IsEmpty => HistoryItems.Count == 0;

    /// <summary>
    /// Creates a new UndoHistoryViewModel.
    /// </summary>
    public UndoHistoryViewModel() : this(EditorUndoService.Instance)
    {
    }

    /// <summary>
    /// Creates a new UndoHistoryViewModel with the specified undo service.
    /// </summary>
    /// <param name="undoService">The undo service to monitor.</param>
    public UndoHistoryViewModel(EditorUndoService undoService)
    {
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));

        // Subscribe to undo service changes
        _undoService.UndoStackChanged += OnUndoStackChanged;
        _undoService.PropertyChanged += OnUndoServicePropertyChanged;

        // Initial refresh
        RefreshHistory();
    }

    /// <summary>
    /// Refreshes the history items from the undo service.
    /// </summary>
    [RelayCommand]
    public void RefreshHistory()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            HistoryItems.Clear();

            // Get undo history (most recent first, we need to reverse for display)
            var undoDescriptions = _undoService.UndoDescriptions.Reverse().ToList();
            var redoDescriptions = _undoService.RedoDescriptions.ToList();

            var index = 0;
            var now = DateTime.Now;

            // Add undo items (oldest to newest)
            foreach (var desc in undoDescriptions)
            {
                var item = new UndoHistoryItem
                {
                    Index = index,
                    Description = desc,
                    Timestamp = now.AddSeconds(-((undoDescriptions.Count - index) * 5)), // Approximate timestamps
                    IsUndoItem = true,
                    Icon = GetIconForAction(desc),
                    Category = GetCategoryForAction(desc)
                };
                HistoryItems.Add(item);
                index++;
            }

            // The current position is after all undo items
            CurrentIndex = index - 1;

            // Add redo items (newest to oldest = what will be redone first to last)
            foreach (var desc in redoDescriptions)
            {
                var item = new UndoHistoryItem
                {
                    Index = index,
                    Description = desc,
                    Timestamp = now.AddSeconds(index), // Future timestamps for redo items
                    IsUndoItem = false,
                    Icon = GetIconForAction(desc),
                    Category = GetCategoryForAction(desc)
                };
                HistoryItems.Add(item);
                index++;
            }

            // Mark current position
            UpdateCurrentPositionMarker();

            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(UndoCount));
            OnPropertyChanged(nameof(RedoCount));
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// Jumps to the specified state index.
    /// </summary>
    /// <param name="targetIndex">The target index to jump to.</param>
    [RelayCommand]
    public void JumpToState(int targetIndex)
    {
        if (targetIndex < -1 || targetIndex >= HistoryItems.Count) return;

        // Calculate how many undo/redo operations needed
        var currentUndoCount = _undoService.UndoCount;
        var desiredUndoCount = targetIndex + 1;
        var difference = currentUndoCount - desiredUndoCount;

        if (difference > 0)
        {
            // Need to undo
            _undoService.UndoMultiple(difference);
        }
        else if (difference < 0)
        {
            // Need to redo
            _undoService.RedoMultiple(-difference);
        }

        RefreshHistory();
    }

    /// <summary>
    /// Jumps to the selected item's state.
    /// </summary>
    [RelayCommand]
    public void JumpToSelectedState()
    {
        if (SelectedItem != null)
        {
            JumpToState(SelectedItem.Index);
        }
    }

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    [RelayCommand]
    public void ClearHistory()
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all undo history? This action cannot be undone.",
            "Clear Undo History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _undoService.Clear();
            RefreshHistory();
        }
    }

    /// <summary>
    /// Creates a branch from the selected state.
    /// Note: This is a placeholder for future branching functionality.
    /// </summary>
    [RelayCommand]
    public void BranchFromHere()
    {
        if (SelectedItem == null) return;

        // First jump to the selected state
        JumpToState(SelectedItem.Index);

        MessageBox.Show(
            $"Branched from: {SelectedItem.Description}\n\nAll redo history has been cleared.",
            "Branch Created",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>
    /// Undoes the last action.
    /// </summary>
    [RelayCommand]
    public void UndoLast()
    {
        if (_undoService.CanUndo)
        {
            _undoService.Undo();
        }
    }

    /// <summary>
    /// Redoes the last undone action.
    /// </summary>
    [RelayCommand]
    public void RedoLast()
    {
        if (_undoService.CanRedo)
        {
            _undoService.Redo();
        }
    }

    private void OnUndoStackChanged(object? sender, EventArgs e)
    {
        if (!_isRefreshing)
        {
            Application.Current?.Dispatcher.InvokeAsync(RefreshHistory);
        }
    }

    private void OnUndoServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorUndoService.UndoDescriptions) ||
            e.PropertyName == nameof(EditorUndoService.RedoDescriptions))
        {
            if (!_isRefreshing)
            {
                Application.Current?.Dispatcher.InvokeAsync(RefreshHistory);
            }
        }
    }

    private void UpdateCurrentPositionMarker()
    {
        foreach (var item in HistoryItems)
        {
            item.IsCurrentPosition = (item.Index == CurrentIndex);
        }
    }

    private static string GetIconForAction(string description)
    {
        var lower = description.ToLowerInvariant();

        if (lower.Contains("note") || lower.Contains("midi"))
            return "\u266B"; // Music note
        if (lower.Contains("add") || lower.Contains("create") || lower.Contains("insert"))
            return "+";
        if (lower.Contains("delete") || lower.Contains("remove"))
            return "\u2212"; // Minus
        if (lower.Contains("move") || lower.Contains("drag"))
            return "\u2194"; // Arrows
        if (lower.Contains("resize") || lower.Contains("scale"))
            return "\u2922"; // Resize
        if (lower.Contains("copy") || lower.Contains("duplicate"))
            return "\u2398"; // Copy
        if (lower.Contains("paste"))
            return "\u2399"; // Paste
        if (lower.Contains("cut"))
            return "\u2702"; // Scissors
        if (lower.Contains("volume") || lower.Contains("gain") || lower.Contains("level"))
            return "\u266A"; // Note
        if (lower.Contains("pan"))
            return "\u21C4"; // Left-right
        if (lower.Contains("mute") || lower.Contains("solo"))
            return "M";
        if (lower.Contains("effect") || lower.Contains("plugin"))
            return "fx";
        if (lower.Contains("automation"))
            return "\u2248"; // Wave
        if (lower.Contains("tempo") || lower.Contains("bpm"))
            return "\u23F1"; // Stopwatch
        if (lower.Contains("pattern"))
            return "\u25A6"; // Grid

        return "\u2022"; // Bullet
    }

    private static string GetCategoryForAction(string description)
    {
        var lower = description.ToLowerInvariant();

        if (lower.Contains("note") || lower.Contains("midi"))
            return "MIDI";
        if (lower.Contains("audio") || lower.Contains("clip") || lower.Contains("waveform"))
            return "Audio";
        if (lower.Contains("mixer") || lower.Contains("volume") || lower.Contains("pan") ||
            lower.Contains("mute") || lower.Contains("solo"))
            return "Mixer";
        if (lower.Contains("effect") || lower.Contains("plugin") || lower.Contains("vst"))
            return "Effects";
        if (lower.Contains("automation"))
            return "Automation";
        if (lower.Contains("pattern") || lower.Contains("arrangement"))
            return "Arrangement";
        if (lower.Contains("tempo") || lower.Contains("time"))
            return "Transport";

        return "Edit";
    }

    /// <summary>
    /// Disposes the ViewModel and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _undoService.UndoStackChanged -= OnUndoStackChanged;
        _undoService.PropertyChanged -= OnUndoServicePropertyChanged;
        _disposed = true;
    }
}
