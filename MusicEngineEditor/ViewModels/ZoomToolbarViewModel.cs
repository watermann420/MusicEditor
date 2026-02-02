// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the ZoomToolbar control.

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Zoom preset options for the toolbar.
/// </summary>
public enum ZoomPreset
{
    /// <summary>Zoom to show entire project.</summary>
    FitAll,
    /// <summary>Zoom to show selected region.</summary>
    FitSelection,
    /// <summary>Zoom to show 1 bar.</summary>
    OneBar,
    /// <summary>Zoom to show 4 bars.</summary>
    FourBars,
    /// <summary>Zoom to show 16 bars.</summary>
    SixteenBars,
    /// <summary>Zoom to show entire song.</summary>
    FullSong,
    /// <summary>Custom zoom level.</summary>
    Custom
}

/// <summary>
/// ViewModel for the ZoomToolbar control providing zoom and navigation features.
/// </summary>
public partial class ZoomToolbarViewModel : ViewModelBase, IDisposable
{
    #region Constants

    private const double MinHorizontalZoom = 0.25;
    private const double MaxHorizontalZoom = 4.0;
    private const double MinVerticalZoom = 0.5;
    private const double MaxVerticalZoom = 2.0;
    private const double DefaultHorizontalZoom = 1.0;
    private const double DefaultVerticalZoom = 1.0;
    private const int DefaultBeatsPerBar = 4;

    #endregion

    #region Private Fields

    private readonly PlaybackService _playbackService;
    private EventBus.SubscriptionToken? _beatSubscription;
    private EventBus.SubscriptionToken? _playbackStartedSubscription;
    private EventBus.SubscriptionToken? _playbackStoppedSubscription;
    private bool _disposed;
    private bool _isFollowingPlayhead;
    private double _lastPlayheadPosition;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the horizontal zoom level (1.0 = 100%).
    /// </summary>
    [ObservableProperty]
    private double _horizontalZoom = DefaultHorizontalZoom;

    /// <summary>
    /// Gets or sets the vertical zoom level (1.0 = 100%).
    /// </summary>
    [ObservableProperty]
    private double _verticalZoom = DefaultVerticalZoom;

    /// <summary>
    /// Gets the horizontal zoom as a percentage (25% to 400%).
    /// </summary>
    public int HorizontalZoomPercent => (int)(HorizontalZoom * 100);

    /// <summary>
    /// Gets the vertical zoom as a percentage (50% to 200%).
    /// </summary>
    public int VerticalZoomPercent => (int)(VerticalZoom * 100);

    /// <summary>
    /// Gets or sets whether the view automatically follows the playhead during playback.
    /// </summary>
    [ObservableProperty]
    private bool _followPlayhead;

    /// <summary>
    /// Gets or sets the currently selected zoom preset.
    /// </summary>
    [ObservableProperty]
    private ZoomPreset _selectedPreset = ZoomPreset.Custom;

    /// <summary>
    /// Gets or sets the current scroll offset in beats.
    /// </summary>
    [ObservableProperty]
    private double _scrollOffset;

    /// <summary>
    /// Gets or sets the number of visible beats in the view.
    /// </summary>
    [ObservableProperty]
    private double _visibleBeats = 64;

    /// <summary>
    /// Gets or sets the total length of the project in beats.
    /// </summary>
    [ObservableProperty]
    private double _totalProjectLength = 256;

    /// <summary>
    /// Gets or sets the selection start position in beats (for Fit Selection).
    /// </summary>
    [ObservableProperty]
    private double _selectionStart;

    /// <summary>
    /// Gets or sets the selection end position in beats (for Fit Selection).
    /// </summary>
    [ObservableProperty]
    private double _selectionEnd;

    /// <summary>
    /// Gets or sets whether there is an active selection.
    /// </summary>
    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>
    /// Gets or sets the current playhead position in beats.
    /// </summary>
    [ObservableProperty]
    private double _playheadPosition;

    /// <summary>
    /// Gets or sets whether playback is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Gets or sets the beats per bar (time signature numerator).
    /// </summary>
    [ObservableProperty]
    private int _beatsPerBar = DefaultBeatsPerBar;

    /// <summary>
    /// Gets or sets the view width in pixels (needed for zoom calculations).
    /// </summary>
    [ObservableProperty]
    private double _viewWidth = 800;

    #endregion

    #region Events

    /// <summary>
    /// Raised when the horizontal zoom changes.
    /// </summary>
    public event EventHandler<double>? HorizontalZoomChanged;

    /// <summary>
    /// Raised when the vertical zoom changes.
    /// </summary>
    public event EventHandler<double>? VerticalZoomChanged;

    /// <summary>
    /// Raised when the scroll offset should change.
    /// </summary>
    public event EventHandler<double>? ScrollOffsetChanged;

    /// <summary>
    /// Raised when the visible beats should change.
    /// </summary>
    public event EventHandler<double>? VisibleBeatsChanged;

    /// <summary>
    /// Raised when a zoom preset is applied.
    /// </summary>
    public event EventHandler<ZoomPreset>? PresetApplied;

    /// <summary>
    /// Raised when Go to Playhead is requested.
    /// </summary>
    public event EventHandler? GoToPlayheadRequested;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new ZoomToolbarViewModel instance.
    /// </summary>
    public ZoomToolbarViewModel()
    {
        _playbackService = PlaybackService.Instance;
        SubscribeToPlaybackEvents();
    }

    #endregion

    #region Playback Integration

    private void SubscribeToPlaybackEvents()
    {
        var eventBus = EventBus.Instance;

        _beatSubscription = eventBus.SubscribeBeatChanged(OnBeatChanged);
        _playbackStartedSubscription = eventBus.SubscribePlaybackStarted(OnPlaybackStarted);
        _playbackStoppedSubscription = eventBus.SubscribePlaybackStopped(OnPlaybackStopped);
    }

    private void OnBeatChanged(EventBus.BeatChangedEventArgs args)
    {
        PlayheadPosition = args.CurrentBeat;
        _lastPlayheadPosition = args.CurrentBeat;

        // Auto-scroll if following playhead
        if (FollowPlayhead && IsPlaying)
        {
            EnsurePlayheadVisible();
        }
    }

    private void OnPlaybackStarted(EventBus.PlaybackStartedEventArgs args)
    {
        IsPlaying = true;
        _isFollowingPlayhead = FollowPlayhead;
    }

    private void OnPlaybackStopped(EventBus.PlaybackStoppedEventArgs args)
    {
        IsPlaying = false;
        _isFollowingPlayhead = false;
    }

    /// <summary>
    /// Ensures the playhead is visible in the current view.
    /// </summary>
    private void EnsurePlayheadVisible()
    {
        if (!FollowPlayhead) return;

        // Check if playhead is outside visible range
        var visibleEnd = ScrollOffset + VisibleBeats;
        var margin = VisibleBeats * 0.1; // 10% margin

        if (PlayheadPosition < ScrollOffset || PlayheadPosition > visibleEnd - margin)
        {
            // Center the playhead in view
            var newOffset = Math.Max(0, PlayheadPosition - VisibleBeats * 0.3);
            ScrollOffset = newOffset;
            ScrollOffsetChanged?.Invoke(this, newOffset);
        }
    }

    #endregion

    #region Property Changed Handlers

    partial void OnHorizontalZoomChanged(double value)
    {
        // Clamp to valid range
        var clamped = Math.Clamp(value, MinHorizontalZoom, MaxHorizontalZoom);
        if (Math.Abs(clamped - value) > 0.001)
        {
            HorizontalZoom = clamped;
            return;
        }

        OnPropertyChanged(nameof(HorizontalZoomPercent));
        SelectedPreset = ZoomPreset.Custom;
        HorizontalZoomChanged?.Invoke(this, clamped);
    }

    partial void OnVerticalZoomChanged(double value)
    {
        // Clamp to valid range
        var clamped = Math.Clamp(value, MinVerticalZoom, MaxVerticalZoom);
        if (Math.Abs(clamped - value) > 0.001)
        {
            VerticalZoom = clamped;
            return;
        }

        OnPropertyChanged(nameof(VerticalZoomPercent));
        VerticalZoomChanged?.Invoke(this, clamped);
    }

    partial void OnScrollOffsetChanged(double value)
    {
        var clamped = Math.Max(0, value);
        if (Math.Abs(clamped - value) > 0.001)
        {
            ScrollOffset = clamped;
            return;
        }

        ScrollOffsetChanged?.Invoke(this, clamped);
    }

    partial void OnVisibleBeatsChanged(double value)
    {
        var clamped = Math.Max(4, value);
        if (Math.Abs(clamped - value) > 0.001)
        {
            VisibleBeats = clamped;
            return;
        }

        VisibleBeatsChanged?.Invoke(this, clamped);
    }

    #endregion

    #region Zoom Commands

    /// <summary>
    /// Increases horizontal zoom (zoom in).
    /// </summary>
    [RelayCommand]
    private void ZoomInHorizontal()
    {
        HorizontalZoom = Math.Min(MaxHorizontalZoom, HorizontalZoom * 1.25);
    }

    /// <summary>
    /// Decreases horizontal zoom (zoom out).
    /// </summary>
    [RelayCommand]
    private void ZoomOutHorizontal()
    {
        HorizontalZoom = Math.Max(MinHorizontalZoom, HorizontalZoom / 1.25);
    }

    /// <summary>
    /// Increases vertical zoom (zoom in).
    /// </summary>
    [RelayCommand]
    private void ZoomInVertical()
    {
        VerticalZoom = Math.Min(MaxVerticalZoom, VerticalZoom * 1.1);
    }

    /// <summary>
    /// Decreases vertical zoom (zoom out).
    /// </summary>
    [RelayCommand]
    private void ZoomOutVertical()
    {
        VerticalZoom = Math.Max(MinVerticalZoom, VerticalZoom / 1.1);
    }

    /// <summary>
    /// Resets both zoom levels to 100%.
    /// </summary>
    [RelayCommand]
    private void ResetZoom()
    {
        HorizontalZoom = DefaultHorizontalZoom;
        VerticalZoom = DefaultVerticalZoom;
        SelectedPreset = ZoomPreset.Custom;
    }

    #endregion

    #region Preset Commands

    /// <summary>
    /// Applies a zoom preset.
    /// </summary>
    /// <param name="preset">The preset to apply.</param>
    [RelayCommand]
    private void ApplyPreset(ZoomPreset preset)
    {
        SelectedPreset = preset;

        switch (preset)
        {
            case ZoomPreset.FitAll:
            case ZoomPreset.FullSong:
                ApplyFitAll();
                break;
            case ZoomPreset.FitSelection:
                ApplyFitSelection();
                break;
            case ZoomPreset.OneBar:
                ApplyBarZoom(1);
                break;
            case ZoomPreset.FourBars:
                ApplyBarZoom(4);
                break;
            case ZoomPreset.SixteenBars:
                ApplyBarZoom(16);
                break;
            case ZoomPreset.Custom:
                // Do nothing for custom
                break;
        }

        PresetApplied?.Invoke(this, preset);
    }

    /// <summary>
    /// Zooms to show the entire project.
    /// </summary>
    private void ApplyFitAll()
    {
        if (TotalProjectLength <= 0 || ViewWidth <= 0) return;

        // Calculate zoom to fit entire project
        var targetBeats = TotalProjectLength + BeatsPerBar; // Add some margin
        VisibleBeats = targetBeats;
        ScrollOffset = 0;

        // Calculate the effective horizontal zoom
        var defaultBeatsVisible = 64.0;
        HorizontalZoom = defaultBeatsVisible / targetBeats;
        HorizontalZoom = Math.Clamp(HorizontalZoom, MinHorizontalZoom, MaxHorizontalZoom);
    }

    /// <summary>
    /// Zooms to show the current selection.
    /// </summary>
    private void ApplyFitSelection()
    {
        if (!HasSelection || SelectionEnd <= SelectionStart) return;

        var selectionLength = SelectionEnd - SelectionStart;
        var margin = selectionLength * 0.1; // 10% margin on each side

        VisibleBeats = selectionLength + margin * 2;
        ScrollOffset = Math.Max(0, SelectionStart - margin);

        // Calculate the effective horizontal zoom
        var defaultBeatsVisible = 64.0;
        HorizontalZoom = defaultBeatsVisible / VisibleBeats;
        HorizontalZoom = Math.Clamp(HorizontalZoom, MinHorizontalZoom, MaxHorizontalZoom);
    }

    /// <summary>
    /// Zooms to show a specific number of bars.
    /// </summary>
    /// <param name="bars">Number of bars to show.</param>
    private void ApplyBarZoom(int bars)
    {
        var targetBeats = bars * BeatsPerBar;
        VisibleBeats = targetBeats;

        // Center on current playhead position
        ScrollOffset = Math.Max(0, PlayheadPosition - targetBeats / 2.0);

        // Calculate the effective horizontal zoom
        var defaultBeatsVisible = 64.0;
        HorizontalZoom = defaultBeatsVisible / targetBeats;
        HorizontalZoom = Math.Clamp(HorizontalZoom, MinHorizontalZoom, MaxHorizontalZoom);
    }

    #endregion

    #region Navigation Commands

    /// <summary>
    /// Scrolls to show the current playhead position.
    /// </summary>
    [RelayCommand]
    private void GoToPlayhead()
    {
        // Center the playhead in view
        var newOffset = Math.Max(0, PlayheadPosition - VisibleBeats / 2);
        ScrollOffset = newOffset;
        ScrollOffsetChanged?.Invoke(this, newOffset);
        GoToPlayheadRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggles the follow playhead mode.
    /// </summary>
    [RelayCommand]
    private void ToggleFollowPlayhead()
    {
        FollowPlayhead = !FollowPlayhead;
    }

    /// <summary>
    /// Scrolls left by one page.
    /// </summary>
    [RelayCommand]
    private void ScrollLeft()
    {
        ScrollOffset = Math.Max(0, ScrollOffset - VisibleBeats * 0.8);
    }

    /// <summary>
    /// Scrolls right by one page.
    /// </summary>
    [RelayCommand]
    private void ScrollRight()
    {
        ScrollOffset = Math.Min(TotalProjectLength - VisibleBeats * 0.2, ScrollOffset + VisibleBeats * 0.8);
    }

    /// <summary>
    /// Scrolls to the beginning.
    /// </summary>
    [RelayCommand]
    private void ScrollToStart()
    {
        ScrollOffset = 0;
    }

    /// <summary>
    /// Scrolls to the end.
    /// </summary>
    [RelayCommand]
    private void ScrollToEnd()
    {
        ScrollOffset = Math.Max(0, TotalProjectLength - VisibleBeats * 0.8);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the selection range for Fit Selection.
    /// </summary>
    /// <param name="start">Selection start in beats.</param>
    /// <param name="end">Selection end in beats.</param>
    public void SetSelection(double start, double end)
    {
        SelectionStart = Math.Min(start, end);
        SelectionEnd = Math.Max(start, end);
        HasSelection = SelectionEnd > SelectionStart;
    }

    /// <summary>
    /// Clears the selection.
    /// </summary>
    public void ClearSelection()
    {
        SelectionStart = 0;
        SelectionEnd = 0;
        HasSelection = false;
    }

    /// <summary>
    /// Updates the total project length.
    /// </summary>
    /// <param name="length">The total length in beats.</param>
    public void SetProjectLength(double length)
    {
        TotalProjectLength = Math.Max(BeatsPerBar, length);
    }

    /// <summary>
    /// Updates the view width for zoom calculations.
    /// </summary>
    /// <param name="width">The view width in pixels.</param>
    public void SetViewWidth(double width)
    {
        ViewWidth = Math.Max(100, width);
    }

    /// <summary>
    /// Sets the beats per bar (time signature).
    /// </summary>
    /// <param name="beatsPerBar">Beats per bar (e.g., 4 for 4/4 time).</param>
    public void SetBeatsPerBar(int beatsPerBar)
    {
        BeatsPerBar = Math.Max(1, beatsPerBar);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the ViewModel and cleans up subscriptions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _beatSubscription?.Dispose();
        _playbackStartedSubscription?.Dispose();
        _playbackStoppedSubscription?.Dispose();
    }

    #endregion
}
