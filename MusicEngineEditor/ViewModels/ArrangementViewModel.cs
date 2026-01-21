using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Arrangement view, managing song structure and sections.
/// </summary>
public partial class ArrangementViewModel : ViewModelBase
{
    private Arrangement? _arrangement;

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
            return _arrangement.FormatPosition(_playbackPosition);
        }
    }

    /// <summary>
    /// Gets the name of the section at the current position.
    /// </summary>
    public string CurrentSectionName
    {
        get
        {
            var section = _arrangement?.GetSectionAt(_playbackPosition);
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
        // Create a default arrangement
        Arrangement = new Arrangement { Name = "New Arrangement" };
    }

    public ArrangementViewModel(Arrangement arrangement)
    {
        Arrangement = arrangement;
    }

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

        var nextSection = _arrangement.GetNextSection(_playbackPosition);
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

        var currentSection = _arrangement.GetSectionAt(_playbackPosition);

        // If we're at the start of a section, go to the previous one
        // Otherwise, go to the start of the current section
        if (currentSection != null && Math.Abs(_playbackPosition - currentSection.StartPosition) < 0.5)
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
        ScrollOffset = Math.Max(0, _playbackPosition - VisibleBeats / 2);
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
        _playbackPosition = position;
        OnPropertyChanged(nameof(PlaybackPosition));
        OnPropertyChanged(nameof(CurrentPositionFormatted));
        OnPropertyChanged(nameof(CurrentSectionName));
    }
}
