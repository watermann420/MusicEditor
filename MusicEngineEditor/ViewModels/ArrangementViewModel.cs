using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;
using MusicEngineEditor.Commands;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Arrangement view, managing song structure and sections.
/// Integrates with PlaybackService for synchronized playhead and position control.
/// </summary>
public partial class ArrangementViewModel : ViewModelBase, IDisposable
{
    private Arrangement? _arrangement;
    private readonly PlaybackService _playbackService;
    private readonly RecordingService _recordingService;
    private EventBus.SubscriptionToken? _beatSubscription;
    private EventBus.SubscriptionToken? _playbackStartedSubscription;
    private EventBus.SubscriptionToken? _playbackStoppedSubscription;
    private bool _disposed;

    /// <summary>
    /// Gets or sets whether playback is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Gets or sets whether recording is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isRecording;

    /// <summary>
    /// Gets or sets whether count-in is active.
    /// </summary>
    [ObservableProperty]
    private bool _isCountingIn;

    /// <summary>
    /// Gets the collection of active recording clips.
    /// </summary>
    public ObservableCollection<RecordingClip> ActiveRecordingClips { get; } = [];

    /// <summary>
    /// Gets the collection of completed recording clips.
    /// </summary>
    public ObservableCollection<RecordingClip> CompletedRecordingClips { get; } = [];

    /// <summary>
    /// Gets or sets the arrangement being edited.
    /// </summary>
    public Arrangement? Arrangement
    {
        get => _arrangement;
        set
        {
            if (_arrangement != null)
            {
                _arrangement.ArrangementChanged -= OnArrangementChanged;
            }

            _arrangement = value;

            if (_arrangement != null)
            {
                _arrangement.ArrangementChanged += OnArrangementChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SectionCount));
            OnPropertyChanged(nameof(TotalLength));
            OnPropertyChanged(nameof(TotalLengthFormatted));
            OnPropertyChanged(nameof(Bpm));
            OnPropertyChanged(nameof(TimeSignature));
            RefreshSections();
        }
    }

    /// <summary>
    /// Gets or sets the current playback position in beats.
    /// </summary>
    [ObservableProperty]
    private double _playbackPosition;

    /// <summary>
    /// Gets or sets the number of beats visible in the view.
    /// </summary>
    [ObservableProperty]
    private double _visibleBeats = 64;

    /// <summary>
    /// Gets or sets the scroll offset in beats.
    /// </summary>
    [ObservableProperty]
    private double _scrollOffset;

    /// <summary>
    /// Gets or sets the currently selected section.
    /// </summary>
    [ObservableProperty]
    private ArrangementSection? _selectedSection;

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Ready";

    /// <summary>
    /// Gets the observable collection of sections for binding.
    /// </summary>
    public ObservableCollection<ArrangementSection> Sections { get; } = [];

    /// <summary>
    /// Gets the number of sections.
    /// </summary>
    public int SectionCount => _arrangement?.SectionCount ?? 0;

    /// <summary>
    /// Gets the total length in beats.
    /// </summary>
    public double TotalLength => _arrangement?.TotalLength ?? 0;

    /// <summary>
    /// Gets the total length formatted as bars:beats.
    /// </summary>
    public string TotalLengthFormatted
    {
        get
        {
            if (_arrangement == null) return "0:0";
            return _arrangement.FormatPosition(_arrangement.TotalLength);
        }
    }

    /// <summary>
    /// Gets or sets the BPM.
    /// </summary>
    public double Bpm
    {
        get => _arrangement?.Bpm ?? 120;
        set
        {
            if (_arrangement != null && Math.Abs(_arrangement.Bpm - value) > 0.001)
            {
                _arrangement.Bpm = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the time signature as a string.
    /// </summary>
    public string TimeSignature
    {
        get
        {
            if (_arrangement == null) return "4/4";
            return $"{_arrangement.TimeSignatureNumerator}/{_arrangement.TimeSignatureDenominator}";
        }
    }

    /// <summary>
    /// Gets the current position formatted as bar:beat.
    /// </summary>
    public string CurrentPositionFormatted
    {
        get
        {
            if (_arrangement == null) return "1:1";
            return _arrangement.FormatPosition(PlaybackPosition);
        }
    }

    /// <summary>
    /// Gets the name of the section at the current position.
    /// </summary>
    public string CurrentSectionName
    {
        get
        {
            var section = _arrangement?.GetSectionAt(PlaybackPosition);
            return section?.Name ?? "No Section";
        }
    }

    /// <summary>
    /// Event raised when a section is selected.
    /// </summary>
    public event EventHandler<ArrangementSection?>? SectionSelected;

    /// <summary>
    /// Event raised when seeking is requested.
    /// </summary>
    public event EventHandler<double>? SeekRequested;

    public ArrangementViewModel()
    {
        _playbackService = PlaybackService.Instance;
        _recordingService = RecordingService.Instance;

        // Create a default arrangement
        Arrangement = new Arrangement { Name = "New Arrangement" };

        // Subscribe to playback events
        SubscribeToPlaybackEvents();

        // Subscribe to recording events
        SubscribeToRecordingEvents();
    }

    public ArrangementViewModel(Arrangement arrangement)
    {
        _playbackService = PlaybackService.Instance;
        _recordingService = RecordingService.Instance;
        Arrangement = arrangement;

        // Subscribe to playback events
        SubscribeToPlaybackEvents();

        // Subscribe to recording events
        SubscribeToRecordingEvents();
    }

    #region Playback Integration

    /// <summary>
    /// Subscribes to playback service events for synchronized playhead.
    /// </summary>
    private void SubscribeToPlaybackEvents()
    {
        var eventBus = EventBus.Instance;

        _beatSubscription = eventBus.SubscribeBeatChanged(args =>
        {
            PlaybackPosition = args.CurrentBeat;
        });

        _playbackStartedSubscription = eventBus.SubscribePlaybackStarted(args =>
        {
            IsPlaying = true;
        });

        _playbackStoppedSubscription = eventBus.SubscribePlaybackStopped(args =>
        {
            IsPlaying = false;
        });
    }

    /// <summary>
    /// Starts playback from the current position.
    /// </summary>
    [RelayCommand]
    private void Play()
    {
        _playbackService.Play();
    }

    /// <summary>
    /// Pauses playback.
    /// </summary>
    [RelayCommand]
    private void Pause()
    {
        _playbackService.Pause();
    }

    /// <summary>
    /// Stops playback and resets position.
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        _playbackService.Stop();
        PlaybackPosition = 0;
    }

    /// <summary>
    /// Toggles play/pause.
    /// </summary>
    [RelayCommand]
    private void TogglePlayPause()
    {
        _playbackService.TogglePlayPause();
    }

    /// <summary>
    /// Handles double-click to jump to position.
    /// </summary>
    /// <param name="beat">The beat position to jump to.</param>
    [RelayCommand]
    private void JumpToPosition(double beat)
    {
        _playbackService.SetPosition(beat);
        PlaybackPosition = beat;
        SeekRequested?.Invoke(this, beat);
    }

    /// <summary>
    /// Sets the loop region for the arrangement.
    /// </summary>
    /// <param name="start">Loop start position in beats.</param>
    /// <param name="end">Loop end position in beats.</param>
    [RelayCommand]
    private void SetLoopRegion((double start, double end) region)
    {
        _playbackService.SetLoopRegion(region.start, region.end);
    }

    /// <summary>
    /// Enables or disables looping.
    /// </summary>
    [RelayCommand]
    private void ToggleLoop()
    {
        _playbackService.ToggleLoop();
    }

    /// <summary>
    /// Sets the loop region to the selected section.
    /// </summary>
    [RelayCommand]
    private void SetLoopToSelectedSection()
    {
        if (SelectedSection == null)
        {
            StatusMessage = "No section selected";
            return;
        }

        _playbackService.SetLoopRegion(SelectedSection.StartPosition, SelectedSection.EndPosition);
        _playbackService.LoopEnabled = true;
        StatusMessage = $"Loop set to: {SelectedSection.Name}";
    }

    #endregion

    #region Recording Integration

    /// <summary>
    /// Subscribes to recording service events.
    /// </summary>
    private void SubscribeToRecordingEvents()
    {
        _recordingService.RecordingStarted += OnRecordingStarted;
        _recordingService.RecordingStopped += OnRecordingStopped;
        _recordingService.RecordingStateChanged += OnRecordingStateChanged;
        _recordingService.CountInStarted += OnCountInStarted;
        _recordingService.CountInBeat += OnCountInBeat;
    }

    /// <summary>
    /// Starts recording on all armed tracks.
    /// </summary>
    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        if (IsRecording || !_recordingService.HasArmedTracks)
        {
            StatusMessage = IsRecording ? "Already recording" : "No tracks armed";
            return;
        }

        try
        {
            await _recordingService.StartRecordingAsync(useCountIn: true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start recording: {ex.Message}";
        }
    }

    /// <summary>
    /// Stops the current recording.
    /// </summary>
    [RelayCommand]
    private void StopRecording()
    {
        if (!IsRecording && !IsCountingIn)
        {
            return;
        }

        _recordingService.StopRecording(cancel: false);
    }

    /// <summary>
    /// Cancels the current recording, discarding all data.
    /// </summary>
    [RelayCommand]
    private void CancelRecording()
    {
        if (!IsRecording && !IsCountingIn)
        {
            return;
        }

        _recordingService.StopRecording(cancel: true);
    }

    /// <summary>
    /// Toggles recording state.
    /// </summary>
    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording || IsCountingIn)
        {
            StopRecording();
        }
        else
        {
            await StartRecordingAsync();
        }
    }

    private void OnRecordingStarted(object? sender, RecordingEventArgs e)
    {
        IsRecording = true;
        IsCountingIn = false;

        // Add active recording clips to the view
        ActiveRecordingClips.Clear();
        foreach (var clip in _recordingService.ActiveClips.Values)
        {
            ActiveRecordingClips.Add(clip);
        }

        StatusMessage = $"Recording {e.ArmedTracks.Count} track(s)";
    }

    private void OnRecordingStopped(object? sender, RecordingStoppedEventArgs e)
    {
        IsRecording = false;
        IsCountingIn = false;

        // Move completed clips to the completed collection
        ActiveRecordingClips.Clear();

        if (!e.WasCancelled)
        {
            foreach (var clip in e.RecordedClips)
            {
                CompletedRecordingClips.Add(clip);
            }

            StatusMessage = $"Recorded {e.RecordedClips.Count} clip(s)";
        }
        else
        {
            StatusMessage = "Recording cancelled";
        }
    }

    private void OnRecordingStateChanged(object? sender, bool isRecording)
    {
        IsRecording = isRecording;
        if (!isRecording)
        {
            IsCountingIn = false;
        }
    }

    private void OnCountInStarted(object? sender, int totalBars)
    {
        IsCountingIn = true;
        StatusMessage = $"Count-in: {totalBars} bar(s)";
    }

    private void OnCountInBeat(object? sender, int beatNumber)
    {
        StatusMessage = $"Count-in: Beat {beatNumber}";
    }

    /// <summary>
    /// Gets the recording clip for a specific track at a specific position.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <param name="beat">The beat position.</param>
    /// <returns>The recording clip or null if none found.</returns>
    public RecordingClip? GetRecordingClipAt(int trackId, double beat)
    {
        // First check active clips
        foreach (var clip in ActiveRecordingClips)
        {
            if (clip.TrackId == trackId && beat >= clip.StartBeat && beat < clip.EndBeat)
            {
                return clip;
            }
        }

        // Then check completed clips
        foreach (var clip in CompletedRecordingClips)
        {
            if (clip.TrackId == trackId && beat >= clip.StartBeat && beat < clip.EndBeat)
            {
                return clip;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all recording clips for a specific track.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <returns>Enumerable of recording clips for the track.</returns>
    public System.Collections.Generic.IEnumerable<RecordingClip> GetRecordingClipsForTrack(int trackId)
    {
        foreach (var clip in ActiveRecordingClips)
        {
            if (clip.TrackId == trackId)
            {
                yield return clip;
            }
        }

        foreach (var clip in CompletedRecordingClips)
        {
            if (clip.TrackId == trackId)
            {
                yield return clip;
            }
        }
    }

    /// <summary>
    /// Removes a recording clip from the arrangement.
    /// </summary>
    /// <param name="clip">The clip to remove.</param>
    public void RemoveRecordingClip(RecordingClip clip)
    {
        if (ActiveRecordingClips.Contains(clip))
        {
            ActiveRecordingClips.Remove(clip);
        }
        else if (CompletedRecordingClips.Contains(clip))
        {
            CompletedRecordingClips.Remove(clip);
        }
    }

    /// <summary>
    /// Clears all completed recording clips.
    /// </summary>
    [RelayCommand]
    private void ClearCompletedRecordings()
    {
        CompletedRecordingClips.Clear();
        StatusMessage = "Cleared all recorded clips";
    }

    #endregion

    private void OnArrangementChanged(object? sender, ArrangementChangedEventArgs e)
    {
        RefreshSections();
        OnPropertyChanged(nameof(SectionCount));
        OnPropertyChanged(nameof(TotalLength));
        OnPropertyChanged(nameof(TotalLengthFormatted));

        StatusMessage = e.ChangeType switch
        {
            ArrangementChangeType.SectionAdded => $"Added section: {e.Section?.Name}",
            ArrangementChangeType.SectionRemoved => $"Removed section: {e.Section?.Name}",
            ArrangementChangeType.SectionModified => $"Modified section: {e.Section?.Name}",
            ArrangementChangeType.SectionsReordered => "Sections reordered",
            ArrangementChangeType.Cleared => "Arrangement cleared",
            _ => "Arrangement changed"
        };
    }

    private void RefreshSections()
    {
        Sections.Clear();

        if (_arrangement != null)
        {
            foreach (var section in _arrangement.Sections)
            {
                Sections.Add(section);
            }
        }
    }

    partial void OnPlaybackPositionChanged(double value)
    {
        OnPropertyChanged(nameof(CurrentPositionFormatted));
        OnPropertyChanged(nameof(CurrentSectionName));
    }

    partial void OnSelectedSectionChanged(ArrangementSection? value)
    {
        SectionSelected?.Invoke(this, value);

        if (value != null)
        {
            StatusMessage = $"Selected: {value.Name} ({value.StartPosition:F1} - {value.EndPosition:F1})";
        }
    }

    #region Undo/Redo Integration

    /// <summary>
    /// Gets the editor undo service.
    /// </summary>
    private EditorUndoService UndoService => EditorUndoService.Instance;

    /// <summary>
    /// Adds a new section at the specified position with undo support.
    /// </summary>
    [RelayCommand]
    private void AddSectionWithUndo(SectionType type)
    {
        if (_arrangement == null) return;

        var startPosition = _arrangement.TotalLength;
        var length = 16.0; // 4 bars at 4/4

        var command = new SectionAddCommand(_arrangement, startPosition, startPosition + length, type);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Adds a custom section with undo support.
    /// </summary>
    [RelayCommand]
    private void AddCustomSectionWithUndo()
    {
        if (_arrangement == null) return;

        var startPosition = _arrangement.TotalLength;
        var length = 16.0;

        var command = new SectionAddCommand(_arrangement, startPosition, startPosition + length, "New Section");
        UndoService.Execute(command);
    }

    /// <summary>
    /// Deletes the selected section with undo support.
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedSectionWithUndo()
    {
        if (_arrangement == null || SelectedSection == null) return;

        if (SelectedSection.IsLocked)
        {
            StatusMessage = "Cannot delete locked section";
            return;
        }

        var command = new SectionDeleteCommand(_arrangement, SelectedSection);
        UndoService.Execute(command);
        SelectedSection = null;
    }

    /// <summary>
    /// Moves the selected section to a new position with undo support.
    /// </summary>
    [RelayCommand]
    private void MoveSectionWithUndo(double newPosition)
    {
        if (_arrangement == null || SelectedSection == null) return;

        if (SelectedSection.IsLocked)
        {
            StatusMessage = "Cannot move locked section";
            return;
        }

        var command = new SectionMoveCommand(_arrangement, SelectedSection, newPosition);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Resizes the selected section with undo support.
    /// </summary>
    [RelayCommand]
    private void ResizeSectionWithUndo((double newStart, double newEnd) bounds)
    {
        if (_arrangement == null || SelectedSection == null) return;

        if (SelectedSection.IsLocked)
        {
            StatusMessage = "Cannot resize locked section";
            return;
        }

        var command = new SectionMoveCommand(_arrangement, SelectedSection, bounds.newStart, bounds.newEnd);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Toggles mute for the selected section with undo support.
    /// </summary>
    [RelayCommand]
    private void ToggleMuteWithUndo()
    {
        if (SelectedSection == null) return;

        var oldValue = SelectedSection.IsMuted;
        var command = new SectionPropertyCommand(SelectedSection, nameof(ArrangementSection.IsMuted), oldValue, !oldValue);
        UndoService.Execute(command);

        StatusMessage = SelectedSection.IsMuted
            ? $"Muted: {SelectedSection.Name}"
            : $"Unmuted: {SelectedSection.Name}";
    }

    /// <summary>
    /// Toggles lock for the selected section with undo support.
    /// </summary>
    [RelayCommand]
    private void ToggleLockWithUndo()
    {
        if (SelectedSection == null) return;

        var oldValue = SelectedSection.IsLocked;
        var command = new SectionPropertyCommand(SelectedSection, nameof(ArrangementSection.IsLocked), oldValue, !oldValue);
        UndoService.Execute(command);

        StatusMessage = SelectedSection.IsLocked
            ? $"Locked: {SelectedSection.Name}"
            : $"Unlocked: {SelectedSection.Name}";
    }

    /// <summary>
    /// Sets the repeat count for the selected section with undo support.
    /// </summary>
    [RelayCommand]
    private void SetRepeatCountWithUndo(int count)
    {
        if (SelectedSection == null) return;

        if (SelectedSection.IsLocked)
        {
            StatusMessage = "Cannot modify locked section";
            return;
        }

        var oldValue = SelectedSection.RepeatCount;
        var newValue = Math.Max(1, count);
        var command = new SectionPropertyCommand(SelectedSection, nameof(ArrangementSection.RepeatCount), oldValue, newValue);
        UndoService.Execute(command);

        OnPropertyChanged(nameof(TotalLength));
        OnPropertyChanged(nameof(TotalLengthFormatted));
    }

    #endregion

    #region Legacy Section Commands (without undo)

    /// <summary>
    /// Adds a new section at the specified position.
    /// </summary>
    [RelayCommand]
    private void AddSection(SectionType type)
    {
        if (_arrangement == null) return;

        var startPosition = _arrangement.TotalLength;
        var length = 16.0; // 4 bars at 4/4

        _arrangement.AddSection(startPosition, startPosition + length, type);
    }

    /// <summary>
    /// Adds a custom section.
    /// </summary>
    [RelayCommand]
    private void AddCustomSection()
    {
        if (_arrangement == null) return;

        var startPosition = _arrangement.TotalLength;
        var length = 16.0;

        _arrangement.AddSection(startPosition, startPosition + length, "New Section");
    }

    /// <summary>
    /// Deletes the selected section.
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedSection()
    {
        if (_arrangement == null || SelectedSection == null) return;

        if (SelectedSection.IsLocked)
        {
            StatusMessage = "Cannot delete locked section";
            return;
        }

        _arrangement.RemoveSection(SelectedSection);
        SelectedSection = null;
    }

    /// <summary>
    /// Duplicates the selected section.
    /// </summary>
    [RelayCommand]
    private void DuplicateSelectedSection()
    {
        if (_arrangement == null || SelectedSection == null) return;

        var copy = _arrangement.DuplicateSection(SelectedSection);
        SelectedSection = copy;
    }

    /// <summary>
    /// Moves the selected section to a new position.
    /// </summary>
    [RelayCommand]
    private void MoveSection(double newPosition)
    {
        if (_arrangement == null || SelectedSection == null) return;

        if (SelectedSection.IsLocked)
        {
            StatusMessage = "Cannot move locked section";
            return;
        }

        _arrangement.MoveSection(SelectedSection, newPosition);
    }

    /// <summary>
    /// Sets the repeat count for the selected section.
    /// </summary>
    [RelayCommand]
    private void SetRepeatCount(int count)
    {
        if (SelectedSection == null) return;

        if (SelectedSection.IsLocked)
        {
            StatusMessage = "Cannot modify locked section";
            return;
        }

        SelectedSection.RepeatCount = Math.Max(1, count);
        SelectedSection.Touch();
        OnPropertyChanged(nameof(TotalLength));
        OnPropertyChanged(nameof(TotalLengthFormatted));
    }

    /// <summary>
    /// Toggles mute for the selected section.
    /// </summary>
    [RelayCommand]
    private void ToggleMute()
    {
        if (SelectedSection == null) return;

        SelectedSection.IsMuted = !SelectedSection.IsMuted;
        SelectedSection.Touch();
        StatusMessage = SelectedSection.IsMuted
            ? $"Muted: {SelectedSection.Name}"
            : $"Unmuted: {SelectedSection.Name}";
    }

    /// <summary>
    /// Toggles lock for the selected section.
    /// </summary>
    [RelayCommand]
    private void ToggleLock()
    {
        if (SelectedSection == null) return;

        SelectedSection.IsLocked = !SelectedSection.IsLocked;
        SelectedSection.Touch();
        StatusMessage = SelectedSection.IsLocked
            ? $"Locked: {SelectedSection.Name}"
            : $"Unlocked: {SelectedSection.Name}";
    }

    #endregion

    /// <summary>
    /// Clears all sections.
    /// </summary>
    [RelayCommand]
    private void ClearArrangement()
    {
        _arrangement?.Clear(includeLockedSections: false);
        SelectedSection = null;
    }

    /// <summary>
    /// Creates a standard song structure.
    /// </summary>
    [RelayCommand]
    private void CreateStandardStructure()
    {
        Arrangement = Arrangement.CreateStandardStructure();
    }

    /// <summary>
    /// Jumps to the specified section.
    /// </summary>
    [RelayCommand]
    private void JumpToSection(ArrangementSection section)
    {
        PlaybackPosition = section.StartPosition;
        SeekRequested?.Invoke(this, section.StartPosition);
    }

    /// <summary>
    /// Jumps to the next section.
    /// </summary>
    [RelayCommand]
    private void JumpToNextSection()
    {
        if (_arrangement == null) return;

        var nextSection = _arrangement.GetNextSection(PlaybackPosition);
        if (nextSection != null)
        {
            PlaybackPosition = nextSection.StartPosition;
            SeekRequested?.Invoke(this, nextSection.StartPosition);
            SelectedSection = nextSection;
        }
    }

    /// <summary>
    /// Jumps to the previous section.
    /// </summary>
    [RelayCommand]
    private void JumpToPreviousSection()
    {
        if (_arrangement == null) return;

        var currentSection = _arrangement.GetSectionAt(PlaybackPosition);

        // If we're at the start of a section, go to the previous one
        // Otherwise, go to the start of the current section
        if (currentSection != null && Math.Abs(PlaybackPosition - currentSection.StartPosition) < 0.5)
        {
            var prevSection = _arrangement.GetPreviousSection(currentSection.StartPosition);
            if (prevSection != null)
            {
                PlaybackPosition = prevSection.StartPosition;
                SeekRequested?.Invoke(this, prevSection.StartPosition);
                SelectedSection = prevSection;
            }
        }
        else if (currentSection != null)
        {
            PlaybackPosition = currentSection.StartPosition;
            SeekRequested?.Invoke(this, currentSection.StartPosition);
            SelectedSection = currentSection;
        }
    }

    /// <summary>
    /// Zooms in (fewer beats visible).
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        VisibleBeats = Math.Max(8, VisibleBeats / 2);
    }

    /// <summary>
    /// Zooms out (more beats visible).
    /// </summary>
    [RelayCommand]
    private void ZoomOut()
    {
        VisibleBeats = Math.Min(256, VisibleBeats * 2);
    }

    /// <summary>
    /// Resets zoom to default.
    /// </summary>
    [RelayCommand]
    private void ResetZoom()
    {
        VisibleBeats = 64;
    }

    /// <summary>
    /// Scrolls to show the current playback position.
    /// </summary>
    [RelayCommand]
    private void ScrollToPlayhead()
    {
        // Center the playhead in the view
        ScrollOffset = Math.Max(0, PlaybackPosition - VisibleBeats / 2);
    }

    /// <summary>
    /// Validates the arrangement and returns any issues.
    /// </summary>
    [RelayCommand]
    private void ValidateArrangement()
    {
        if (_arrangement == null)
        {
            StatusMessage = "No arrangement to validate";
            return;
        }

        var issues = _arrangement.Validate();

        if (issues.Count == 0)
        {
            StatusMessage = "Arrangement is valid";
        }
        else
        {
            StatusMessage = $"Found {issues.Count} issue(s)";
            // In a full implementation, show a dialog with the issues
        }
    }

    /// <summary>
    /// Updates the playback position from external source.
    /// </summary>
    public void UpdatePlaybackPosition(double position)
    {
        PlaybackPosition = position;
        OnPropertyChanged(nameof(CurrentPositionFormatted));
        OnPropertyChanged(nameof(CurrentSectionName));
    }

    #region IDisposable

    /// <summary>
    /// Disposes the ArrangementViewModel, cleaning up event subscriptions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose EventBus subscriptions
        _beatSubscription?.Dispose();
        _playbackStartedSubscription?.Dispose();
        _playbackStoppedSubscription?.Dispose();

        // Unsubscribe from arrangement events
        if (_arrangement != null)
        {
            _arrangement.ArrangementChanged -= OnArrangementChanged;
        }

        // Unsubscribe from recording events
        _recordingService.RecordingStarted -= OnRecordingStarted;
        _recordingService.RecordingStopped -= OnRecordingStopped;
        _recordingService.RecordingStateChanged -= OnRecordingStateChanged;
        _recordingService.CountInStarted -= OnCountInStarted;
        _recordingService.CountInBeat -= OnCountInBeat;
    }

    #endregion
}
