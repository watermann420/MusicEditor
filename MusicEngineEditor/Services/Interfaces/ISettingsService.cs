using System.Collections.Generic;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Loads settings from the settings file
    /// </summary>
    /// <returns>The loaded settings</returns>
    Task<AppSettings> LoadSettingsAsync();

    /// <summary>
    /// Saves settings to the settings file
    /// </summary>
    /// <param name="settings">The settings to save</param>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Resets settings to default values
    /// </summary>
    /// <returns>The default settings</returns>
    AppSettings ResetToDefaults();

    /// <summary>
    /// Gets available audio output devices
    /// </summary>
    /// <returns>List of audio device names</returns>
    IEnumerable<string> GetAudioOutputDevices();

    /// <summary>
    /// Gets available MIDI input devices
    /// </summary>
    /// <returns>List of MIDI input device names</returns>
    IEnumerable<string> GetMidiInputDevices();

    /// <summary>
    /// Gets available MIDI output devices
    /// </summary>
    /// <returns>List of MIDI output device names</returns>
    IEnumerable<string> GetMidiOutputDevices();
}
