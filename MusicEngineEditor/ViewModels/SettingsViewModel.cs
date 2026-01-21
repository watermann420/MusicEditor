using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Settings dialog
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private AppSettings _originalSettings = null!;

    // Audio Settings
    [ObservableProperty]
    private string _selectedAudioDevice = "Default";

    [ObservableProperty]
    private int _selectedSampleRate = 44100;

    [ObservableProperty]
    private int _selectedBufferSize = 512;

    // MIDI Settings
    [ObservableProperty]
    private string _selectedMidiInputDevice = "None";

    [ObservableProperty]
    private string _selectedMidiOutputDevice = "None";

    [ObservableProperty]
    private bool _enableClockSync;

    [ObservableProperty]
    private bool _enableMidiThrough;

    // Editor Settings
    [ObservableProperty]
    private string _selectedTheme = "Dark";

    [ObservableProperty]
    private int _selectedFontSize = 14;

    [ObservableProperty]
    private int _autoSaveInterval = 5;

    [ObservableProperty]
    private bool _showLineNumbers = true;

    [ObservableProperty]
    private bool _highlightCurrentLine = true;

    [ObservableProperty]
    private bool _wordWrap;

    // Path Settings
    [ObservableProperty]
    private string _defaultProjectLocation = string.Empty;

    // Collections for ComboBoxes
    public ObservableCollection<string> AudioDevices { get; } = new();
    public ObservableCollection<int> SampleRates { get; } = new();
    public ObservableCollection<int> BufferSizes { get; } = new();
    public ObservableCollection<string> MidiInputDevices { get; } = new();
    public ObservableCollection<string> MidiOutputDevices { get; } = new();
    public ObservableCollection<string> Themes { get; } = new();
    public ObservableCollection<int> FontSizes { get; } = new();
    public ObservableCollection<string> VstPluginPaths { get; } = new();
    public ObservableCollection<string> SampleDirectories { get; } = new();

    [ObservableProperty]
    private string? _selectedVstPath;

    [ObservableProperty]
    private string? _selectedSampleDirectory;

    /// <summary>
    /// Event raised when settings should be applied and dialog closed
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Event raised when dialog should be cancelled
    /// </summary>
    public event EventHandler? CancelRequested;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _themeService = App.Services.GetRequiredService<IThemeService>();
        InitializeCollections();
    }

    /// <summary>
    /// Called when the selected theme changes - applies theme immediately for hot-swap
    /// </summary>
    partial void OnSelectedThemeChanged(string value)
    {
        try
        {
            // Apply theme immediately for hot-swap without restart
            _themeService.ApplyTheme(value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the static collections for combo boxes
    /// </summary>
    private void InitializeCollections()
    {
        // Sample rates
        foreach (var rate in AudioSettings.AvailableSampleRates)
        {
            SampleRates.Add(rate);
        }

        // Buffer sizes
        foreach (var size in AudioSettings.AvailableBufferSizes)
        {
            BufferSizes.Add(size);
        }

        // Themes
        foreach (var theme in EditorSettings.AvailableThemes)
        {
            Themes.Add(theme);
        }

        // Font sizes
        foreach (var size in EditorSettings.AvailableFontSizes)
        {
            FontSizes.Add(size);
        }
    }

    /// <summary>
    /// Loads settings and populates the ViewModel properties
    /// </summary>
    public async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading settings...";

        try
        {
            // Load settings
            var settings = await _settingsService.LoadSettingsAsync();
            _originalSettings = CloneSettings(settings);

            // Populate device lists
            AudioDevices.Clear();
            foreach (var device in _settingsService.GetAudioOutputDevices())
            {
                AudioDevices.Add(device);
            }

            MidiInputDevices.Clear();
            foreach (var device in _settingsService.GetMidiInputDevices())
            {
                MidiInputDevices.Add(device);
            }

            MidiOutputDevices.Clear();
            foreach (var device in _settingsService.GetMidiOutputDevices())
            {
                MidiOutputDevices.Add(device);
            }

            // Apply loaded settings to properties
            ApplySettingsToProperties(settings);

            StatusMessage = "Settings loaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load settings: {ex.Message}";
            MessageBox.Show($"Failed to load settings: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Applies settings from the model to ViewModel properties
    /// </summary>
    private void ApplySettingsToProperties(AppSettings settings)
    {
        // Audio
        SelectedAudioDevice = settings.Audio.OutputDevice;
        SelectedSampleRate = settings.Audio.SampleRate;
        SelectedBufferSize = settings.Audio.BufferSize;

        // MIDI
        SelectedMidiInputDevice = settings.Midi.InputDevice;
        SelectedMidiOutputDevice = settings.Midi.OutputDevice;
        EnableClockSync = settings.Midi.EnableClockSync;
        EnableMidiThrough = settings.Midi.EnableMidiThrough;

        // Editor
        SelectedTheme = settings.Editor.Theme;
        SelectedFontSize = settings.Editor.FontSize;
        AutoSaveInterval = settings.Editor.AutoSaveInterval;
        ShowLineNumbers = settings.Editor.ShowLineNumbers;
        HighlightCurrentLine = settings.Editor.HighlightCurrentLine;
        WordWrap = settings.Editor.WordWrap;

        // Paths
        DefaultProjectLocation = settings.Paths.DefaultProjectLocation;

        VstPluginPaths.Clear();
        foreach (var path in settings.Paths.VstPluginPaths)
        {
            VstPluginPaths.Add(path);
        }

        SampleDirectories.Clear();
        foreach (var path in settings.Paths.SampleDirectories)
        {
            SampleDirectories.Add(path);
        }
    }

    /// <summary>
    /// Creates settings from the current ViewModel properties
    /// </summary>
    private AppSettings CreateSettingsFromProperties()
    {
        return new AppSettings
        {
            Audio = new AudioSettings
            {
                OutputDevice = SelectedAudioDevice,
                SampleRate = SelectedSampleRate,
                BufferSize = SelectedBufferSize
            },
            Midi = new MidiSettings
            {
                InputDevice = SelectedMidiInputDevice,
                OutputDevice = SelectedMidiOutputDevice,
                EnableClockSync = EnableClockSync,
                EnableMidiThrough = EnableMidiThrough
            },
            Editor = new EditorSettings
            {
                Theme = SelectedTheme,
                FontSize = SelectedFontSize,
                AutoSaveInterval = AutoSaveInterval,
                ShowLineNumbers = ShowLineNumbers,
                HighlightCurrentLine = HighlightCurrentLine,
                WordWrap = WordWrap
            },
            Paths = new PathSettings
            {
                VstPluginPaths = VstPluginPaths.ToList(),
                SampleDirectories = SampleDirectories.ToList(),
                DefaultProjectLocation = DefaultProjectLocation
            }
        };
    }

    /// <summary>
    /// Creates a deep clone of settings
    /// </summary>
    private static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            Audio = new AudioSettings
            {
                OutputDevice = source.Audio.OutputDevice,
                SampleRate = source.Audio.SampleRate,
                BufferSize = source.Audio.BufferSize
            },
            Midi = new MidiSettings
            {
                InputDevice = source.Midi.InputDevice,
                OutputDevice = source.Midi.OutputDevice,
                EnableClockSync = source.Midi.EnableClockSync,
                EnableMidiThrough = source.Midi.EnableMidiThrough
            },
            Editor = new EditorSettings
            {
                Theme = source.Editor.Theme,
                FontSize = source.Editor.FontSize,
                AutoSaveInterval = source.Editor.AutoSaveInterval,
                ShowLineNumbers = source.Editor.ShowLineNumbers,
                HighlightCurrentLine = source.Editor.HighlightCurrentLine,
                WordWrap = source.Editor.WordWrap
            },
            Paths = new PathSettings
            {
                VstPluginPaths = new List<string>(source.Paths.VstPluginPaths),
                SampleDirectories = new List<string>(source.Paths.SampleDirectories),
                DefaultProjectLocation = source.Paths.DefaultProjectLocation
            }
        };
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        IsBusy = true;
        StatusMessage = "Saving settings...";

        try
        {
            var settings = CreateSettingsFromProperties();
            await _settingsService.SaveSettingsAsync(settings);
            StatusMessage = "Settings saved";
            ApplyRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Restore original settings
        ApplySettingsToProperties(_originalSettings);
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to their default values?",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var defaults = _settingsService.ResetToDefaults();
            ApplySettingsToProperties(defaults);
            StatusMessage = "Settings reset to defaults";
        }
    }

    [RelayCommand]
    private void BrowseDefaultProjectLocation()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select default project location",
            SelectedPath = DefaultProjectLocation
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            DefaultProjectLocation = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void AddVstPath()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select VST plugin directory"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            if (!VstPluginPaths.Contains(dialog.SelectedPath))
            {
                VstPluginPaths.Add(dialog.SelectedPath);
            }
        }
    }

    [RelayCommand]
    private void RemoveVstPath()
    {
        if (SelectedVstPath != null && VstPluginPaths.Contains(SelectedVstPath))
        {
            VstPluginPaths.Remove(SelectedVstPath);
            SelectedVstPath = VstPluginPaths.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void AddSampleDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select sample directory"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            if (!SampleDirectories.Contains(dialog.SelectedPath))
            {
                SampleDirectories.Add(dialog.SelectedPath);
            }
        }
    }

    [RelayCommand]
    private void RemoveSampleDirectory()
    {
        if (SelectedSampleDirectory != null && SampleDirectories.Contains(SelectedSampleDirectory))
        {
            SampleDirectories.Remove(SelectedSampleDirectory);
            SelectedSampleDirectory = SampleDirectories.FirstOrDefault();
        }
    }
}
