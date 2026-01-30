// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for metronome settings.

using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for metronome settings and controls.
/// </summary>
public partial class MetronomeViewModel : ViewModelBase
{
    private readonly MetronomeService _metronomeService;

    /// <summary>
    /// Gets or sets whether the metronome is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>
    /// Gets or sets the metronome volume (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _volume = 0.7f;

    /// <summary>
    /// Gets or sets whether accent on the first beat is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _accentEnabled = true;

    /// <summary>
    /// Gets or sets the accent volume multiplier.
    /// </summary>
    [ObservableProperty]
    private float _accentVolume = 1.5f;

    /// <summary>
    /// Gets or sets the selected sound type.
    /// </summary>
    [ObservableProperty]
    private MetronomeSoundType _soundType = MetronomeSoundType.Sine;

    /// <summary>
    /// Gets or sets the beats per bar.
    /// </summary>
    [ObservableProperty]
    private int _beatsPerBar = 4;

    /// <summary>
    /// Gets or sets the count-in bars setting.
    /// </summary>
    [ObservableProperty]
    private int _countInBars;

    /// <summary>
    /// Gets or sets the custom sound file path.
    /// </summary>
    [ObservableProperty]
    private string? _customSoundPath;

    /// <summary>
    /// Gets or sets the custom accent sound file path.
    /// </summary>
    [ObservableProperty]
    private string? _customAccentSoundPath;

    /// <summary>
    /// Gets or sets the BPM value.
    /// </summary>
    [ObservableProperty]
    private double _bpm = 120.0;

    /// <summary>
    /// Gets the available sound types.
    /// </summary>
    public ObservableCollection<MetronomeSoundType> SoundTypes { get; } = new()
    {
        MetronomeSoundType.Sine,
        MetronomeSoundType.Wood,
        MetronomeSoundType.Stick,
        MetronomeSoundType.Custom
    };

    /// <summary>
    /// Gets the available count-in bar options.
    /// </summary>
    public ObservableCollection<int> CountInOptions { get; } = new() { 0, 1, 2, 4 };

    /// <summary>
    /// Gets the available beats per bar options.
    /// </summary>
    public ObservableCollection<int> BeatsPerBarOptions { get; } = new() { 2, 3, 4, 5, 6, 7, 8, 9, 12, 16 };

    /// <summary>
    /// Event raised when apply is requested.
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Event raised when cancel is requested.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Creates a new MetronomeViewModel.
    /// </summary>
    /// <param name="metronomeService">The metronome service to wrap.</param>
    public MetronomeViewModel(MetronomeService metronomeService)
    {
        _metronomeService = metronomeService;
        LoadFromService();
    }

    /// <summary>
    /// Loads settings from the metronome service.
    /// </summary>
    public void LoadFromService()
    {
        IsEnabled = _metronomeService.IsEnabled;
        Volume = _metronomeService.Volume;
        AccentEnabled = _metronomeService.AccentEnabled;
        AccentVolume = _metronomeService.AccentVolume;
        SoundType = _metronomeService.SoundType;
        BeatsPerBar = _metronomeService.BeatsPerBar;
        CountInBars = _metronomeService.CountInBars;
        CustomSoundPath = _metronomeService.CustomSoundPath;
        CustomAccentSoundPath = _metronomeService.CustomAccentSoundPath;
        Bpm = _metronomeService.Bpm;
    }

    /// <summary>
    /// Applies settings to the metronome service.
    /// </summary>
    public void ApplyToService()
    {
        _metronomeService.IsEnabled = IsEnabled;
        _metronomeService.Volume = Volume;
        _metronomeService.AccentEnabled = AccentEnabled;
        _metronomeService.AccentVolume = AccentVolume;
        _metronomeService.SoundType = SoundType;
        _metronomeService.BeatsPerBar = BeatsPerBar;
        _metronomeService.CountInBars = CountInBars;
        _metronomeService.CustomSoundPath = CustomSoundPath;
        _metronomeService.CustomAccentSoundPath = CustomAccentSoundPath;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        // Immediately apply enable/disable to service for real-time feedback
        _metronomeService.IsEnabled = value;
    }

    partial void OnVolumeChanged(float value)
    {
        // Immediately apply volume change for real-time feedback
        _metronomeService.Volume = value;
    }

    partial void OnSoundTypeChanged(MetronomeSoundType value)
    {
        // Immediately apply sound type for real-time feedback
        _metronomeService.SoundType = value;
        OnPropertyChanged(nameof(IsCustomSoundSelected));
    }

    partial void OnAccentEnabledChanged(bool value)
    {
        // Immediately apply accent setting
        _metronomeService.AccentEnabled = value;
    }

    partial void OnAccentVolumeChanged(float value)
    {
        // Immediately apply accent volume
        _metronomeService.AccentVolume = value;
    }

    partial void OnBeatsPerBarChanged(int value)
    {
        // Immediately apply beats per bar
        _metronomeService.BeatsPerBar = value;
    }

    /// <summary>
    /// Gets whether custom sound is selected (for UI visibility).
    /// </summary>
    public bool IsCustomSoundSelected => SoundType == MetronomeSoundType.Custom;

    /// <summary>
    /// Toggles the metronome enabled state.
    /// </summary>
    [RelayCommand]
    private void Toggle()
    {
        IsEnabled = !IsEnabled;
    }

    /// <summary>
    /// Sets the volume level.
    /// </summary>
    /// <param name="volume">Volume level (0.0 to 1.0).</param>
    [RelayCommand]
    private void SetVolume(float volume)
    {
        Volume = Math.Clamp(volume, 0f, 1f);
    }

    /// <summary>
    /// Selects a sound type.
    /// </summary>
    /// <param name="soundType">The sound type to select.</param>
    [RelayCommand]
    private void SelectSound(MetronomeSoundType soundType)
    {
        SoundType = soundType;
    }

    /// <summary>
    /// Plays a preview click sound.
    /// </summary>
    [RelayCommand]
    private void Preview()
    {
        try
        {
            _metronomeService.PlayPreview();
            StatusMessage = "Preview played";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Browses for a custom click sound file.
    /// </summary>
    [RelayCommand]
    private void BrowseCustomSound()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Custom Click Sound",
            Filter = "Audio Files|*.wav;*.mp3;*.ogg;*.flac|WAV Files|*.wav|All Files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            CustomSoundPath = dialog.FileName;
            if (SoundType == MetronomeSoundType.Custom)
            {
                _metronomeService.LoadCustomSound(CustomSoundPath, CustomAccentSoundPath);
            }
        }
    }

    /// <summary>
    /// Browses for a custom accent click sound file.
    /// </summary>
    [RelayCommand]
    private void BrowseCustomAccentSound()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Custom Accent Click Sound",
            Filter = "Audio Files|*.wav;*.mp3;*.ogg;*.flac|WAV Files|*.wav|All Files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            CustomAccentSoundPath = dialog.FileName;
            if (SoundType == MetronomeSoundType.Custom && !string.IsNullOrEmpty(CustomSoundPath))
            {
                _metronomeService.LoadCustomSound(CustomSoundPath, CustomAccentSoundPath);
            }
        }
    }

    /// <summary>
    /// Clears the custom sound paths.
    /// </summary>
    [RelayCommand]
    private void ClearCustomSounds()
    {
        CustomSoundPath = null;
        CustomAccentSoundPath = null;
        if (SoundType == MetronomeSoundType.Custom)
        {
            SoundType = MetronomeSoundType.Sine;
        }
    }

    /// <summary>
    /// Applies settings and closes the dialog.
    /// </summary>
    [RelayCommand]
    private void Apply()
    {
        ApplyToService();
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Cancels changes and closes the dialog.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        // Reload original settings
        LoadFromService();
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resets settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset metronome settings to defaults?",
            "Reset Metronome Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            IsEnabled = false;
            Volume = 0.7f;
            AccentEnabled = true;
            AccentVolume = 1.5f;
            SoundType = MetronomeSoundType.Sine;
            BeatsPerBar = 4;
            CountInBars = 0;
            CustomSoundPath = null;
            CustomAccentSoundPath = null;

            StatusMessage = "Settings reset to defaults";
        }
    }
}
