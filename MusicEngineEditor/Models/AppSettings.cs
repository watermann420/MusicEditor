using System.Collections.Generic;

namespace MusicEngineEditor.Models;

/// <summary>
/// Root application settings container
/// </summary>
public class AppSettings
{
    public AudioSettings Audio { get; set; } = new();
    public MidiSettings Midi { get; set; } = new();
    public EditorSettings Editor { get; set; } = new();
    public PathSettings Paths { get; set; } = new();
}

/// <summary>
/// Audio configuration settings
/// </summary>
public class AudioSettings
{
    /// <summary>
    /// Selected audio output device name
    /// </summary>
    public string OutputDevice { get; set; } = "Default";

    /// <summary>
    /// Audio sample rate in Hz
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Audio buffer size in samples
    /// </summary>
    public int BufferSize { get; set; } = 512;

    /// <summary>
    /// Available sample rates for selection
    /// </summary>
    public static int[] AvailableSampleRates => new[] { 22050, 44100, 48000, 88200, 96000, 176400, 192000 };

    /// <summary>
    /// Available buffer sizes for selection
    /// </summary>
    public static int[] AvailableBufferSizes => new[] { 64, 128, 256, 512, 1024, 2048, 4096 };
}

/// <summary>
/// MIDI device configuration settings
/// </summary>
public class MidiSettings
{
    /// <summary>
    /// Selected MIDI input device name
    /// </summary>
    public string InputDevice { get; set; } = "None";

    /// <summary>
    /// Selected MIDI output device name
    /// </summary>
    public string OutputDevice { get; set; } = "None";

    /// <summary>
    /// Whether to enable MIDI clock sync
    /// </summary>
    public bool EnableClockSync { get; set; } = false;

    /// <summary>
    /// Whether to enable MIDI through (pass-through)
    /// </summary>
    public bool EnableMidiThrough { get; set; } = false;
}

/// <summary>
/// Editor appearance and behavior settings
/// </summary>
public class EditorSettings
{
    /// <summary>
    /// UI theme (Dark or Light)
    /// </summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// Editor font size in points
    /// </summary>
    public int FontSize { get; set; } = 14;

    /// <summary>
    /// Auto-save interval in minutes (0 = disabled)
    /// </summary>
    public int AutoSaveInterval { get; set; } = 5;

    /// <summary>
    /// Whether to show line numbers in editor
    /// </summary>
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>
    /// Whether to highlight the current line
    /// </summary>
    public bool HighlightCurrentLine { get; set; } = true;

    /// <summary>
    /// Whether to enable word wrap
    /// </summary>
    public bool WordWrap { get; set; } = false;

    /// <summary>
    /// Available themes for selection
    /// </summary>
    public static string[] AvailableThemes => new[] { "Dark", "Light" };

    /// <summary>
    /// Available font sizes for selection
    /// </summary>
    public static int[] AvailableFontSizes => new[] { 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24 };
}

/// <summary>
/// Path configuration settings
/// </summary>
public class PathSettings
{
    /// <summary>
    /// List of VST plugin search directories
    /// </summary>
    public List<string> VstPluginPaths { get; set; } = new()
    {
        @"C:\Program Files\Common Files\VST3",
        @"C:\Program Files\VSTPlugins",
        @"C:\Program Files (x86)\VSTPlugins"
    };

    /// <summary>
    /// List of sample/audio file directories
    /// </summary>
    public List<string> SampleDirectories { get; set; } = new();

    /// <summary>
    /// Default location for new projects
    /// </summary>
    public string DefaultProjectLocation { get; set; } = string.Empty;
}
