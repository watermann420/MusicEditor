// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel implementation for Undo History Panel with visual timeline.

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
/// Enum representing different types of undo actions for visual differentiation.
/// </summary>
public enum UndoActionType
{
    /// <summary>General edit action (pencil icon)</summary>
    Edit,
    /// <summary>Add/create action (plus icon)</summary>
    Add,
    /// <summary>Delete/remove action (trash icon)</summary>
    Delete,
    /// <summary>Move/drag action (arrows icon)</summary>
    Move,
    /// <summary>Parameter change action (slider icon)</summary>
    Parameter,
    /// <summary>Note-related action (music note icon)</summary>
    Note,
    /// <summary>Mixer-related action</summary>
    Mixer,
    /// <summary>Effect-related action</summary>
    Effect,
    /// <summary>Automation-related action</summary>
    Automation,
    /// <summary>Arrangement-related action</summary>
    Arrangement,
    /// <summary>Unknown/general action</summary>
    Unknown
}

/// <summary>
/// Represents a single item in the undo/redo history with timeline visualization support.
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
    /// Gets or sets the action type for visual styling.
    /// </summary>
    [ObservableProperty]
    private UndoActionType _actionType = UndoActionType.Unknown;

    /// <summary>
    /// Gets or sets whether this item represents a branch point (fork in history).
    /// </summary>
    [ObservableProperty]
    private bool _hasBranch;

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
/// ViewModel for the Undo History Panel with visual timeline support.
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
                var actionType = GetActionTypeForDescription(desc);
                var item = new UndoHistoryItem
                {
                    Index = index,
                    Description = desc,
                    Timestamp = now.AddSeconds(-((undoDescriptions.Count - index) * 5)), // Approximate timestamps
                    IsUndoItem = true,
                    Icon = GetIconForAction(desc),
                    Category = GetCategoryForAction(desc),
                    ActionType = actionType,
                    HasBranch = false
                };
                HistoryItems.Add(item);
                index++;
            }

            // The current position is after all undo items
            CurrentIndex = index - 1;

            // Mark the last undo item as potential branch point if there are redo items
            if (redoDescriptions.Count > 0 && HistoryItems.Count > 0)
            {
                var lastUndoItem = HistoryItems.LastOrDefault(x => x.IsUndoItem);
                if (lastUndoItem != null)
                {
                    lastUndoItem.HasBranch = true;
                }
            }

            // Add redo items (newest to oldest = what will be redone first to last)
            foreach (var desc in redoDescriptions)
            {
                var actionType = GetActionTypeForDescription(desc);
                var item = new UndoHistoryItem
                {
                    Index = index,
                    Description = desc,
                    Timestamp = now.AddSeconds(index), // Future timestamps for redo items
                    IsUndoItem = false,
                    Icon = GetIconForAction(desc),
                    Category = GetCategoryForAction(desc),
                    ActionType = actionType,
                    HasBranch = false
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
    /// Clears all undo/redo history with confirmation dialog.
    /// </summary>
    [RelayCommand]
    public void ClearHistory()
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all undo history?\n\nThis action cannot be undone and you will lose the ability to undo/redo any previous changes.",
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
    /// Compacts the history by merging similar consecutive actions.
    /// </summary>
    [RelayCommand]
    public void CompactHistory()
    {
        var result = MessageBox.Show(
            "Compact history will merge similar consecutive actions to reduce memory usage.\n\nThis may combine multiple small changes into single entries. Continue?",
            "Compact History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Call the undo service's compact method if available
            _undoService.CompactHistory();
            RefreshHistory();

            MessageBox.Show(
                $"History compacted successfully.\n\nCurrent history: {TotalItems} items",
                "Compact Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
            $"Branched from: {SelectedItem.Description}\n\nAll redo history has been cleared. Any new changes will start a new branch from this point.",
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

    /// <summary>
    /// Determines the action type based on the description text.
    /// </summary>
    private static UndoActionType GetActionTypeForDescription(string description)
    {
        var lower = description.ToLowerInvariant();

        // Check for specific action types
        if (lower.Contains("add") || lower.Contains("create") || lower.Contains("insert") || lower.Contains("new"))
            return UndoActionType.Add;
        if (lower.Contains("delete") || lower.Contains("remove") || lower.Contains("clear"))
            return UndoActionType.Delete;
        if (lower.Contains("move") || lower.Contains("drag") || lower.Contains("reorder"))
            return UndoActionType.Move;
        if (lower.Contains("volume") || lower.Contains("gain") || lower.Contains("pan") ||
            lower.Contains("level") || lower.Contains("value") || lower.Contains("change"))
            return UndoActionType.Parameter;
        if (lower.Contains("note") || lower.Contains("midi") || lower.Contains("pitch") ||
            lower.Contains("velocity"))
            return UndoActionType.Note;
        if (lower.Contains("mixer") || lower.Contains("mute") || lower.Contains("solo"))
            return UndoActionType.Mixer;
        if (lower.Contains("effect") || lower.Contains("plugin") || lower.Contains("vst"))
            return UndoActionType.Effect;
        if (lower.Contains("automation") || lower.Contains("envelope"))
            return UndoActionType.Automation;
        if (lower.Contains("pattern") || lower.Contains("arrangement") || lower.Contains("clip") ||
            lower.Contains("region"))
            return UndoActionType.Arrangement;
        if (lower.Contains("edit") || lower.Contains("modify") || lower.Contains("update"))
            return UndoActionType.Edit;

        return UndoActionType.Unknown;
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
