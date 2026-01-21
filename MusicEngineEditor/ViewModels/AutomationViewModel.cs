using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Automation;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for managing automation tracks and lanes in the editor.
/// </summary>
public partial class AutomationViewModel : ViewModelBase
{
    private readonly AutomationPlayer _player;

    [ObservableProperty]
    private ObservableCollection<AutomationTrackViewModel> _tracks = [];

    [ObservableProperty]
    private AutomationTrackViewModel? _selectedTrack;

    [ObservableProperty]
    private AutomationLaneViewModel? _selectedLane;

    [ObservableProperty]
    private double _currentTime;

    [ObservableProperty]
    private double _timeScale = 50.0;

    [ObservableProperty]
    private double _timeOffset;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _snapToGrid = true;

    [ObservableProperty]
    private double _gridSubdivision = 0.25;

    [ObservableProperty]
    private AutomationCurveType _defaultCurveType = AutomationCurveType.Linear;

    /// <summary>
    /// Gets the automation player instance.
    /// </summary>
    public AutomationPlayer Player => _player;

    /// <summary>
    /// Creates a new AutomationViewModel.
    /// </summary>
    public AutomationViewModel()
    {
        _player = new AutomationPlayer();
        _player.StateChanged += OnPlayerStateChanged;
        _player.AutomationProcessed += OnAutomationProcessed;
        _player.TrackAdded += OnTrackAdded;
        _player.TrackRemoved += OnTrackRemoved;
    }

    /// <summary>
    /// Creates a new AutomationViewModel with a Sequencer.
    /// </summary>
    /// <param name="sequencer">The Sequencer to synchronize with.</param>
    public AutomationViewModel(MusicEngine.Core.Sequencer sequencer) : this()
    {
        _player.SetSequencer(sequencer);
    }

    #region Commands

    [RelayCommand]
    private void Play()
    {
        _player.Play();
    }

    [RelayCommand]
    private void Stop()
    {
        _player.Stop();
    }

    [RelayCommand]
    private void Pause()
    {
        _player.Pause();
    }

    [RelayCommand]
    private void StartRecording()
    {
        _player.StartRecording();
    }

    [RelayCommand]
    private void StopRecording()
    {
        _player.StopRecording();
    }

    [RelayCommand]
    private void AddTrack()
    {
        var track = new AutomationTrack
        {
            Name = $"Track {Tracks.Count + 1}"
        };
        _player.AddTrack(track);
    }

    [RelayCommand]
    private void RemoveTrack(AutomationTrackViewModel? trackVm)
    {
        if (trackVm?.Track == null) return;

        _player.RemoveTrack(trackVm.Track);
    }

    [RelayCommand]
    private void AddLane()
    {
        if (SelectedTrack?.Track == null) return;

        var lane = SelectedTrack.Track.AddLane($"Parameter {SelectedTrack.Lanes.Count + 1}");
        var laneVm = new AutomationLaneViewModel(lane);
        SelectedTrack.Lanes.Add(laneVm);
    }

    [RelayCommand]
    private void RemoveLane(AutomationLaneViewModel? laneVm)
    {
        if (laneVm?.Lane == null || SelectedTrack?.Track == null) return;

        SelectedTrack.Track.RemoveLane(laneVm.Lane);
        SelectedTrack.Lanes.Remove(laneVm);
    }

    [RelayCommand]
    private void ClearAllPoints()
    {
        if (SelectedLane?.Lane == null) return;

        SelectedLane.Lane.ClearPoints();
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        _player.ResetToDefaults();
    }

    [RelayCommand]
    private void ZoomIn()
    {
        TimeScale = Math.Min(500, TimeScale * 1.2);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        TimeScale = Math.Max(10, TimeScale / 1.2);
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        // TODO: Calculate optimal zoom based on all tracks
    }

    [RelayCommand]
    private void Seek(double time)
    {
        _player.Seek(time);
    }

    [RelayCommand]
    private void ToggleMute(AutomationLaneViewModel? laneVm)
    {
        if (laneVm?.Lane == null) return;
        laneVm.Lane.IsMuted = !laneVm.Lane.IsMuted;
        laneVm.IsMuted = laneVm.Lane.IsMuted;
    }

    [RelayCommand]
    private void ToggleSolo(AutomationLaneViewModel? laneVm)
    {
        if (laneVm?.Lane == null) return;
        laneVm.Lane.IsSoloed = !laneVm.Lane.IsSoloed;
        laneVm.IsSoloed = laneVm.Lane.IsSoloed;
    }

    [RelayCommand]
    private void ToggleArm(AutomationLaneViewModel? laneVm)
    {
        if (laneVm?.Lane == null) return;
        laneVm.Lane.IsArmed = !laneVm.Lane.IsArmed;
        laneVm.IsArmed = laneVm.Lane.IsArmed;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Registers an automatable target.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    public void RegisterTarget(IAutomatable target)
    {
        _player.RegisterTarget(target);
    }

    /// <summary>
    /// Creates a track for the specified target.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    /// <returns>The created track ViewModel.</returns>
    public AutomationTrackViewModel CreateTrack(IAutomatable target)
    {
        var track = _player.AddTrack(target);
        return Tracks.First(t => t.Track == track);
    }

    /// <summary>
    /// Gets or creates a track for the specified target.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    /// <returns>The track ViewModel.</returns>
    public AutomationTrackViewModel GetOrCreateTrack(IAutomatable target)
    {
        var existing = Tracks.FirstOrDefault(t => t.TargetId == target.AutomationId);
        if (existing != null) return existing;

        return CreateTrack(target);
    }

    /// <summary>
    /// Refreshes the tracks list from the player.
    /// </summary>
    public void RefreshTracks()
    {
        Tracks.Clear();
        foreach (var track in _player.Tracks)
        {
            Tracks.Add(new AutomationTrackViewModel(track));
        }
    }

    /// <summary>
    /// Sets the Sequencer for synchronization.
    /// </summary>
    /// <param name="sequencer">The Sequencer.</param>
    public void SetSequencer(MusicEngine.Core.Sequencer? sequencer)
    {
        _player.SetSequencer(sequencer);
    }

    #endregion

    #region Event Handlers

    private void OnPlayerStateChanged(object? sender, AutomationPlaybackStateEventArgs e)
    {
        IsPlaying = e.State == AutomationPlaybackState.Playing;
        IsRecording = e.State == AutomationPlaybackState.Recording;
        CurrentTime = e.CurrentTime;
    }

    private void OnAutomationProcessed(object? sender, AutomationProcessedEventArgs e)
    {
        CurrentTime = e.Time;
    }

    private void OnTrackAdded(object? sender, AutomationTrack track)
    {
        var trackVm = new AutomationTrackViewModel(track);
        Tracks.Add(trackVm);
    }

    private void OnTrackRemoved(object? sender, AutomationTrack track)
    {
        var trackVm = Tracks.FirstOrDefault(t => t.Track == track);
        if (trackVm != null)
        {
            Tracks.Remove(trackVm);
        }
    }

    #endregion
}

/// <summary>
/// ViewModel for an automation track.
/// </summary>
public partial class AutomationTrackViewModel : ViewModelBase
{
    [ObservableProperty]
    private AutomationTrack? _track;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _targetId = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isSoloed;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private string _color = "#4B6EAF";

    [ObservableProperty]
    private ObservableCollection<AutomationLaneViewModel> _lanes = [];

    [ObservableProperty]
    private AutomationLaneViewModel? _selectedLane;

    public AutomationTrackViewModel()
    {
    }

    public AutomationTrackViewModel(AutomationTrack track)
    {
        Track = track;
        SyncFromTrack();

        track.TrackChanged += (_, _) => SyncFromTrack();
        track.LaneAdded += OnLaneAdded;
        track.LaneRemoved += OnLaneRemoved;
    }

    private void SyncFromTrack()
    {
        if (Track == null) return;

        Name = Track.Name;
        TargetId = Track.TargetId;
        IsEnabled = Track.Enabled;
        IsMuted = Track.IsMuted;
        IsSoloed = Track.IsSoloed;
        IsExpanded = Track.IsExpanded;
        Color = Track.Color;
    }

    private void OnLaneAdded(object? sender, AutomationLane lane)
    {
        var laneVm = new AutomationLaneViewModel(lane);
        Lanes.Add(laneVm);
    }

    private void OnLaneRemoved(object? sender, AutomationLane lane)
    {
        var laneVm = Lanes.FirstOrDefault(l => l.Lane == lane);
        if (laneVm != null)
        {
            Lanes.Remove(laneVm);
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (Track != null) Track.Enabled = value;
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (Track != null) Track.IsMuted = value;
    }

    partial void OnIsSoloedChanged(bool value)
    {
        if (Track != null) Track.IsSoloed = value;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (Track != null) Track.IsExpanded = value;
    }

    partial void OnColorChanged(string value)
    {
        if (Track != null) Track.Color = value;
    }
}

/// <summary>
/// ViewModel for an automation lane.
/// </summary>
public partial class AutomationLaneViewModel : ViewModelBase
{
    [ObservableProperty]
    private AutomationLane? _lane;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _parameterName = string.Empty;

    [ObservableProperty]
    private string _targetId = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isSoloed;

    [ObservableProperty]
    private bool _isArmed;

    [ObservableProperty]
    private float _minValue;

    [ObservableProperty]
    private float _maxValue = 1f;

    [ObservableProperty]
    private float _defaultValue;

    [ObservableProperty]
    private float _currentValue;

    [ObservableProperty]
    private string _color = "#6AAB73";

    [ObservableProperty]
    private int _pointCount;

    public AutomationLaneViewModel()
    {
    }

    public AutomationLaneViewModel(AutomationLane lane)
    {
        Lane = lane;
        SyncFromLane();

        lane.LaneChanged += (_, _) => SyncFromLane();
        lane.ValueApplied += OnValueApplied;
        lane.Curve.CurveChanged += OnCurveChanged;
    }

    private void SyncFromLane()
    {
        if (Lane == null) return;

        Name = Lane.Name;
        ParameterName = Lane.ParameterName;
        TargetId = Lane.TargetId;
        IsEnabled = Lane.Enabled;
        IsMuted = Lane.IsMuted;
        IsSoloed = Lane.IsSoloed;
        IsArmed = Lane.IsArmed;
        MinValue = Lane.MinValue;
        MaxValue = Lane.MaxValue;
        DefaultValue = Lane.DefaultValue;
        Color = Lane.Color;
        PointCount = Lane.Curve.Count;
        CurrentValue = Lane.LastAppliedValue;
    }

    private void OnValueApplied(object? sender, AutomationValueAppliedEventArgs e)
    {
        CurrentValue = e.Value;
    }

    private void OnCurveChanged(object? sender, EventArgs e)
    {
        if (Lane != null)
        {
            PointCount = Lane.Curve.Count;
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (Lane != null) Lane.Enabled = value;
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (Lane != null) Lane.IsMuted = value;
    }

    partial void OnIsSoloedChanged(bool value)
    {
        if (Lane != null) Lane.IsSoloed = value;
    }

    partial void OnIsArmedChanged(bool value)
    {
        if (Lane != null) Lane.IsArmed = value;
    }

    partial void OnColorChanged(string value)
    {
        if (Lane != null) Lane.Color = value;
    }
}
