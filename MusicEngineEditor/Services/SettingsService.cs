using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing application settings with JSON persistence
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor");

    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private AppSettings _settings = new();

    /// <summary>
    /// Gets the current application settings
    /// </summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// Loads settings from the settings file
    /// </summary>
    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = await File.ReadAllTextAsync(SettingsFilePath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                // Initialize with defaults and set default project location
                _settings = CreateDefaultSettings();
                await SaveSettingsAsync(_settings);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            _settings = CreateDefaultSettings();
        }

        return _settings;
    }

    /// <summary>
    /// Saves settings to the settings file
    /// </summary>
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(SettingsFolder);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json);
            _settings = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Resets settings to default values
    /// </summary>
    public AppSettings ResetToDefaults()
    {
        _settings = CreateDefaultSettings();
        return _settings;
    }

    /// <summary>
    /// Gets available audio output devices
    /// </summary>
    public IEnumerable<string> GetAudioOutputDevices()
    {
        var devices = new List<string> { "Default" };

        try
        {
            // In a real implementation, you would enumerate audio devices using NAudio or similar
            // For now, return placeholder devices
            devices.Add("Primary Sound Driver");
            devices.Add("Speakers (Realtek High Definition Audio)");
            devices.Add("ASIO Driver");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate audio devices: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Gets available MIDI input devices
    /// </summary>
    public IEnumerable<string> GetMidiInputDevices()
    {
        var devices = new List<string> { "None" };

        try
        {
            // In a real implementation, you would enumerate MIDI devices
            // For now, return placeholder devices
            devices.Add("MIDI Keyboard");
            devices.Add("USB MIDI Controller");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate MIDI input devices: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Gets available MIDI output devices
    /// </summary>
    public IEnumerable<string> GetMidiOutputDevices()
    {
        var devices = new List<string> { "None" };

        try
        {
            // In a real implementation, you would enumerate MIDI devices
            // For now, return placeholder devices
            devices.Add("Microsoft GS Wavetable Synth");
            devices.Add("External MIDI Device");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate MIDI output devices: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Creates default settings with sensible values
    /// </summary>
    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            Audio = new AudioSettings
            {
                OutputDevice = "Default",
                SampleRate = 44100,
                BufferSize = 512
            },
            Midi = new MidiSettings
            {
                InputDevice = "None",
                OutputDevice = "None",
                EnableClockSync = false,
                EnableMidiThrough = false
            },
            Editor = new EditorSettings
            {
                Theme = "Dark",
                FontSize = 14,
                AutoSaveInterval = 5,
                ShowLineNumbers = true,
                HighlightCurrentLine = true,
                WordWrap = false
            },
            Paths = new PathSettings
            {
                VstPluginPaths = new List<string>
                {
                    @"C:\Program Files\Common Files\VST3",
                    @"C:\Program Files\VSTPlugins",
                    @"C:\Program Files (x86)\VSTPlugins"
                },
                SampleDirectories = new List<string>(),
                DefaultProjectLocation = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "MusicEngineProjects")
            }
        };
    }
}
