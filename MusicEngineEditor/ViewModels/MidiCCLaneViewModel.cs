// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for MIDI CC lanes.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Defines the editing mode for the CC lane.
/// </summary>
public enum CCLaneEditMode
{
    /// <summary>
    /// Draw mode: Click to add points, drag to draw curves.
    /// </summary>
    Draw,

    /// <summary>
    /// Edit mode: Select and move existing points.
    /// </summary>
    Edit,

    /// <summary>
    /// Line mode: Draw straight lines between points.
    /// </summary>
    Line
}

/// <summary>
/// ViewModel for a single MIDI CC automation lane.
/// </summary>
public partial class MidiCCLaneViewModel : ViewModelBase
{
    #region Constants

    private const int MaxValue = 127;
    private const int MinValue = 0;

    #endregion

    #region Private Fields

    private readonly ObservableCollection<MidiCCEvent> _allCCEvents;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the selected CC controller number (0-127).
    /// </summary>
    [ObservableProperty]
    private int _selectedController = 1;

    /// <summary>
    /// Gets or sets the current editing mode.
    /// </summary>
    [ObservableProperty]
    private CCLaneEditMode _editMode = CCLaneEditMode.Draw;

    /// <summary>
    /// Gets or sets whether the lane is expanded/visible.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>
    /// Gets or sets the height of the lane in pixels.
    /// </summary>
    [ObservableProperty]
    private double _laneHeight = 80;

    /// <summary>
    /// Gets or sets the display name for the lane.
    /// </summary>
    [ObservableProperty]
    private string _displayName = "CC1: Modulation";

    /// <summary>
    /// Gets or sets the unique identifier for this lane.
    /// </summary>
    [ObservableProperty]
    private Guid _laneId = Guid.NewGuid();

    /// <summary>
    /// Gets or sets whether interpolation between points is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _interpolationEnabled = true;

    /// <summary>
    /// Gets or sets the MIDI channel for this lane (0-15).
    /// </summary>
    [ObservableProperty]
    private int _channel;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of CC events for the selected controller.
    /// </summary>
    public ObservableCollection<MidiCCEvent> CCEvents { get; } = new();

    /// <summary>
    /// Gets the collection of currently selected CC events.
    /// </summary>
    public ObservableCollection<MidiCCEvent> SelectedEvents { get; } = new();

    /// <summary>
    /// Gets the available CC controllers for selection.
    /// </summary>
    public MidiCCController[] AvailableControllers => MidiCCController.CommonControllers;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new MidiCCLaneViewModel.
    /// </summary>
    /// <param name="allCCEvents">The shared collection of all CC events.</param>
    public MidiCCLaneViewModel(ObservableCollection<MidiCCEvent> allCCEvents)
    {
        _allCCEvents = allCCEvents ?? new ObservableCollection<MidiCCEvent>();

        // Subscribe to collection changes
        _allCCEvents.CollectionChanged += (_, _) => FilterEventsForController();

        // Initial filter
        FilterEventsForController();
        UpdateDisplayName();
    }

    /// <summary>
    /// Creates a new MidiCCLaneViewModel with default controller.
    /// </summary>
    public MidiCCLaneViewModel() : this(new ObservableCollection<MidiCCEvent>())
    {
    }

    #endregion

    #region Property Changed Handlers

    partial void OnSelectedControllerChanged(int value)
    {
        FilterEventsForController();
        UpdateDisplayName();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Filters events from the shared collection to show only events for the selected controller.
    /// </summary>
    private void FilterEventsForController()
    {
        CCEvents.Clear();

        var filteredEvents = _allCCEvents
            .Where(e => e.Controller == SelectedController)
            .OrderBy(e => e.Beat);

        foreach (var evt in filteredEvents)
        {
            CCEvents.Add(evt);
        }
    }

    /// <summary>
    /// Updates the display name based on the selected controller.
    /// </summary>
    private void UpdateDisplayName()
    {
        DisplayName = $"CC{SelectedController}: {MidiCCEvent.GetControllerName(SelectedController)}";
    }

    #endregion

    #region Commands

    /// <summary>
    /// Adds a new CC event at the specified position.
    /// </summary>
    /// <param name="beat">The beat position.</param>
    /// <param name="value">The CC value (0-127).</param>
    private void AddEvent(double beat, int value)
    {
        value = Math.Clamp(value, MinValue, MaxValue);

        var newEvent = new MidiCCEvent(beat, SelectedController, value)
        {
            Channel = Channel
        };

        _allCCEvents.Add(newEvent);

        // The collection changed event will trigger FilterEventsForController
        StatusMessage = $"Added CC{SelectedController} = {value} at beat {beat:F2}";
    }

    /// <summary>
    /// Adds a CC event with beat and value as a tuple parameter.
    /// </summary>
    [RelayCommand]
    private void AddEventAtPosition((double beat, int value) position)
    {
        AddEvent(position.beat, position.value);
    }

    /// <summary>
    /// Removes the specified CC event.
    /// </summary>
    /// <param name="evt">The event to remove.</param>
    [RelayCommand]
    private void RemoveEvent(MidiCCEvent? evt)
    {
        if (evt == null) return;

        _allCCEvents.Remove(evt);
        SelectedEvents.Remove(evt);

        StatusMessage = $"Removed CC event at beat {evt.Beat:F2}";
    }

    /// <summary>
    /// Moves an event to a new position and/or value.
    /// </summary>
    /// <param name="evt">The event to move.</param>
    /// <param name="newBeat">The new beat position.</param>
    /// <param name="newValue">The new value.</param>
    private void MoveEvent(MidiCCEvent? evt, double newBeat, int newValue)
    {
        if (evt == null) return;

        evt.Beat = Math.Max(0, newBeat);
        evt.Value = Math.Clamp(newValue, MinValue, MaxValue);

        // Re-sort if needed
        FilterEventsForController();
    }

    /// <summary>
    /// Sets the value of an event.
    /// </summary>
    /// <param name="evt">The event to modify.</param>
    /// <param name="newValue">The new value (0-127).</param>
    private void SetEventValue(MidiCCEvent? evt, int newValue)
    {
        if (evt == null) return;

        evt.Value = Math.Clamp(newValue, MinValue, MaxValue);
    }

    /// <summary>
    /// Selects all events in the lane.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        SelectedEvents.Clear();
        foreach (var evt in CCEvents)
        {
            evt.IsSelected = true;
            SelectedEvents.Add(evt);
        }
    }

    /// <summary>
    /// Deselects all events.
    /// </summary>
    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var evt in SelectedEvents)
        {
            evt.IsSelected = false;
        }
        SelectedEvents.Clear();
    }

    /// <summary>
    /// Deletes all selected events.
    /// </summary>
    [RelayCommand]
    private void DeleteSelected()
    {
        var eventsToRemove = SelectedEvents.ToList();
        foreach (var evt in eventsToRemove)
        {
            _allCCEvents.Remove(evt);
        }
        SelectedEvents.Clear();

        StatusMessage = $"Deleted {eventsToRemove.Count} CC event(s)";
    }

    /// <summary>
    /// Sets the editing mode.
    /// </summary>
    /// <param name="mode">The editing mode.</param>
    [RelayCommand]
    private void SetEditMode(CCLaneEditMode mode)
    {
        EditMode = mode;
    }

    /// <summary>
    /// Toggles the expanded state of the lane.
    /// </summary>
    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    /// <summary>
    /// Changes the selected controller.
    /// </summary>
    /// <param name="controller">The new controller number.</param>
    [RelayCommand]
    private void ChangeController(int controller)
    {
        SelectedController = Math.Clamp(controller, 0, 127);
    }

    /// <summary>
    /// Creates a linear ramp between two points.
    /// </summary>
    /// <param name="startBeat">Start position in beats.</param>
    /// <param name="endBeat">End position in beats.</param>
    /// <param name="startValue">Start value.</param>
    /// <param name="endValue">End value.</param>
    /// <param name="steps">Number of intermediate steps.</param>
    private void CreateRamp(double startBeat, double endBeat, int startValue, int endValue, int steps = 8)
    {
        if (endBeat <= startBeat || steps < 1) return;

        double beatStep = (endBeat - startBeat) / steps;
        double valueStep = (endValue - startValue) / (double)steps;

        for (int i = 0; i <= steps; i++)
        {
            double beat = startBeat + (i * beatStep);
            int value = (int)Math.Round(startValue + (i * valueStep));
            value = Math.Clamp(value, MinValue, MaxValue);

            var newEvent = new MidiCCEvent(beat, SelectedController, value)
            {
                Channel = Channel
            };
            _allCCEvents.Add(newEvent);
        }

        StatusMessage = $"Created ramp from {startValue} to {endValue}";
    }

    /// <summary>
    /// Clears all events for the current controller.
    /// </summary>
    [RelayCommand]
    private void ClearAll()
    {
        var eventsToRemove = _allCCEvents
            .Where(e => e.Controller == SelectedController)
            .ToList();

        foreach (var evt in eventsToRemove)
        {
            _allCCEvents.Remove(evt);
        }

        SelectedEvents.Clear();
        StatusMessage = $"Cleared all CC{SelectedController} events";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the interpolated value at a specific beat position.
    /// </summary>
    /// <param name="beat">The beat position.</param>
    /// <returns>The interpolated value (0-127), or null if no events exist.</returns>
    public int? GetValueAtBeat(double beat)
    {
        if (CCEvents.Count == 0) return null;

        var sortedEvents = CCEvents.OrderBy(e => e.Beat).ToList();

        // If before first event, return first event's value
        if (beat <= sortedEvents[0].Beat)
        {
            return sortedEvents[0].Value;
        }

        // If after last event, return last event's value
        if (beat >= sortedEvents[^1].Beat)
        {
            return sortedEvents[^1].Value;
        }

        // Find surrounding events and interpolate
        for (int i = 0; i < sortedEvents.Count - 1; i++)
        {
            var current = sortedEvents[i];
            var next = sortedEvents[i + 1];

            if (beat >= current.Beat && beat < next.Beat)
            {
                if (!InterpolationEnabled)
                {
                    return current.Value;
                }

                // Linear interpolation
                double t = (beat - current.Beat) / (next.Beat - current.Beat);
                return (int)Math.Round(current.Value + t * (next.Value - current.Value));
            }
        }

        return sortedEvents[^1].Value;
    }

    /// <summary>
    /// Finds the event closest to the specified beat position.
    /// </summary>
    /// <param name="beat">The beat position.</param>
    /// <param name="tolerance">Maximum distance in beats to consider.</param>
    /// <returns>The closest event, or null if none within tolerance.</returns>
    public MidiCCEvent? FindEventAtBeat(double beat, double tolerance = 0.1)
    {
        return CCEvents
            .Where(e => Math.Abs(e.Beat - beat) <= tolerance)
            .OrderBy(e => Math.Abs(e.Beat - beat))
            .FirstOrDefault();
    }

    /// <summary>
    /// Adds a CC event directly (used by the control).
    /// </summary>
    /// <param name="beat">Beat position.</param>
    /// <param name="value">CC value.</param>
    /// <returns>The created event.</returns>
    public MidiCCEvent AddCCEvent(double beat, int value)
    {
        value = Math.Clamp(value, MinValue, MaxValue);

        var newEvent = new MidiCCEvent(beat, SelectedController, value)
        {
            Channel = Channel
        };

        _allCCEvents.Add(newEvent);

        return newEvent;
    }

    /// <summary>
    /// Removes a CC event directly (used by the control).
    /// </summary>
    /// <param name="evt">The event to remove.</param>
    public void RemoveCCEvent(MidiCCEvent evt)
    {
        _allCCEvents.Remove(evt);
        SelectedEvents.Remove(evt);
    }

    /// <summary>
    /// Updates an event's position and value.
    /// </summary>
    /// <param name="evt">The event to update.</param>
    /// <param name="newBeat">New beat position.</param>
    /// <param name="newValue">New value.</param>
    public void UpdateEvent(MidiCCEvent evt, double newBeat, int newValue)
    {
        evt.Beat = Math.Max(0, newBeat);
        evt.Value = Math.Clamp(newValue, MinValue, MaxValue);
    }

    #endregion
}
