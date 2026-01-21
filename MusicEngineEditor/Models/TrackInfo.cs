// MusicEngineEditor - Track Info Model
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Defines the types of tracks available in the arrangement.
/// </summary>
public enum TrackType
{
    /// <summary>
    /// A MIDI instrument track (synth, sampler, VST instrument).
    /// </summary>
    Instrument,

    /// <summary>
    /// An audio track for recording and playback of audio files.
    /// </summary>
    Audio,

    /// <summary>
    /// A bus/group track for mixing multiple tracks together.
    /// </summary>
    Bus,

    /// <summary>
    /// The master output track.
    /// </summary>
    Master
}

/// <summary>
/// Represents a track in the arrangement view with all its properties.
/// </summary>
public partial class TrackInfo : ObservableObject
{
    private static int _nextId = 1;

    /// <summary>
    /// Gets the unique track identifier.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets or sets the track name.
    /// </summary>
    [ObservableProperty]
    private string _name = "Track";

    /// <summary>
    /// Gets or sets the track type.
    /// </summary>
    [ObservableProperty]
    private TrackType _trackType = TrackType.Instrument;

    /// <summary>
    /// Gets or sets the track color (hex string).
    /// </summary>
    [ObservableProperty]
    private string _color = "#4A9EFF";

    /// <summary>
    /// Gets or sets the track icon identifier.
    /// </summary>
    [ObservableProperty]
    private string _icon = "synth";

    /// <summary>
    /// Gets or sets whether the track is armed for recording.
    /// </summary>
    [ObservableProperty]
    private bool _isArmed;

    /// <summary>
    /// Gets or sets whether input monitoring is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isMonitoring;

    /// <summary>
    /// Gets or sets whether the track is frozen (rendered to audio).
    /// </summary>
    [ObservableProperty]
    private bool _isFrozen;

    /// <summary>
    /// Gets or sets whether the track is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets or sets whether the track is soloed.
    /// </summary>
    [ObservableProperty]
    private bool _isSoloed;

    /// <summary>
    /// Gets or sets the input source identifier.
    /// </summary>
    [ObservableProperty]
    private string? _inputSource;

    /// <summary>
    /// Gets or sets the input source display name.
    /// </summary>
    [ObservableProperty]
    private string _inputSourceName = "None";

    /// <summary>
    /// Gets or sets the output bus identifier.
    /// </summary>
    [ObservableProperty]
    private string? _outputBus;

    /// <summary>
    /// Gets or sets the output bus display name.
    /// </summary>
    [ObservableProperty]
    private string _outputBusName = "Master";

    /// <summary>
    /// Gets or sets the MIDI channel (1-16, 0 = Omni/All).
    /// Only applicable for Instrument tracks.
    /// </summary>
    [ObservableProperty]
    private int _midiChannel;

    /// <summary>
    /// Gets or sets the audio input device/channel.
    /// Only applicable for Audio tracks.
    /// </summary>
    [ObservableProperty]
    private string? _audioInput;

    /// <summary>
    /// Gets or sets the audio input display name.
    /// </summary>
    [ObservableProperty]
    private string _audioInputName = "None";

    /// <summary>
    /// Gets or sets the track height in pixels.
    /// </summary>
    [ObservableProperty]
    private double _height = 80;

    /// <summary>
    /// Gets or sets whether the track is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Gets or sets whether the track is minimized (collapsed height).
    /// </summary>
    [ObservableProperty]
    private bool _isMinimized;

    /// <summary>
    /// Gets or sets whether the track is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets or sets the track volume (0.0 to 1.25+).
    /// </summary>
    [ObservableProperty]
    private float _volume = 0.8f;

    /// <summary>
    /// Gets or sets the track pan (-1.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _pan;

    /// <summary>
    /// Gets or sets the instrument/plugin name associated with this track.
    /// </summary>
    [ObservableProperty]
    private string? _instrumentName;

    /// <summary>
    /// Gets or sets the instrument/plugin path (for VST).
    /// </summary>
    [ObservableProperty]
    private string? _instrumentPath;

    /// <summary>
    /// Gets or sets whether the track has automation.
    /// </summary>
    [ObservableProperty]
    private bool _hasAutomation;

    /// <summary>
    /// Gets or sets whether automation lanes are expanded.
    /// </summary>
    [ObservableProperty]
    private bool _automationExpanded;

    /// <summary>
    /// Gets or sets the track order/index in the arrangement.
    /// </summary>
    [ObservableProperty]
    private int _order;

    /// <summary>
    /// Gets the track color as a WPF Brush.
    /// </summary>
    public Brush ColorBrush
    {
        get
        {
            try
            {
                return new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(Color));
            }
            catch
            {
                return new SolidColorBrush(Colors.DodgerBlue);
            }
        }
    }

    /// <summary>
    /// Gets the track icon symbol based on the track type.
    /// </summary>
    public string IconSymbol => TrackType switch
    {
        TrackType.Instrument => "\u266B",  // Musical note
        TrackType.Audio => "\u23F5",       // Play button (waveform)
        TrackType.Bus => "\u2630",         // Trigram (bus)
        TrackType.Master => "\u2302",      // House (master)
        _ => "\u266A"
    };

    /// <summary>
    /// Gets the MIDI channel display text.
    /// </summary>
    public string MidiChannelDisplay => MidiChannel == 0 ? "Omni" : MidiChannel.ToString();

    /// <summary>
    /// Gets whether this track can have MIDI channel assignment.
    /// </summary>
    public bool CanHaveMidiChannel => TrackType == TrackType.Instrument;

    /// <summary>
    /// Gets whether this track can have audio input.
    /// </summary>
    public bool CanHaveAudioInput => TrackType == TrackType.Audio;

    /// <summary>
    /// Gets whether this track can be frozen.
    /// </summary>
    public bool CanFreeze => TrackType == TrackType.Instrument || TrackType == TrackType.Audio;

    /// <summary>
    /// Gets whether this track can be armed for recording.
    /// </summary>
    public bool CanArm => TrackType == TrackType.Instrument || TrackType == TrackType.Audio;

    /// <summary>
    /// Creates a new track with default values.
    /// </summary>
    public TrackInfo()
    {
        Id = _nextId++;
    }

    /// <summary>
    /// Creates a new track with the specified name and type.
    /// </summary>
    /// <param name="name">The track name.</param>
    /// <param name="trackType">The track type.</param>
    public TrackInfo(string name, TrackType trackType) : this()
    {
        Name = name;
        TrackType = trackType;
        SetDefaultsForType(trackType);
    }

    /// <summary>
    /// Creates a new track with the specified name, type, and color.
    /// </summary>
    /// <param name="name">The track name.</param>
    /// <param name="trackType">The track type.</param>
    /// <param name="color">The track color (hex string).</param>
    public TrackInfo(string name, TrackType trackType, string color) : this(name, trackType)
    {
        Color = color;
    }

    /// <summary>
    /// Sets default values based on track type.
    /// </summary>
    private void SetDefaultsForType(TrackType type)
    {
        switch (type)
        {
            case TrackType.Instrument:
                Color = "#4A9EFF";
                Icon = "synth";
                InputSourceName = "MIDI In";
                break;

            case TrackType.Audio:
                Color = "#6AAB73";
                Icon = "audio";
                InputSourceName = "Audio In 1/2";
                break;

            case TrackType.Bus:
                Color = "#E89C4B";
                Icon = "bus";
                InputSourceName = "Mix";
                break;

            case TrackType.Master:
                Color = "#FF9500";
                Icon = "master";
                InputSourceName = "All";
                OutputBusName = "Output";
                break;
        }
    }

    /// <summary>
    /// Creates a duplicate of this track with a new ID.
    /// </summary>
    /// <returns>A new TrackInfo with copied properties.</returns>
    public TrackInfo Duplicate()
    {
        return new TrackInfo
        {
            Name = $"{Name} (Copy)",
            TrackType = TrackType,
            Color = Color,
            Icon = Icon,
            IsArmed = false,
            IsMonitoring = IsMonitoring,
            IsFrozen = false,
            IsMuted = IsMuted,
            IsSoloed = false,
            InputSource = InputSource,
            InputSourceName = InputSourceName,
            OutputBus = OutputBus,
            OutputBusName = OutputBusName,
            MidiChannel = MidiChannel,
            AudioInput = AudioInput,
            AudioInputName = AudioInputName,
            Height = Height,
            IsVisible = true,
            IsMinimized = IsMinimized,
            Volume = Volume,
            Pan = Pan,
            InstrumentName = InstrumentName,
            InstrumentPath = InstrumentPath,
            Order = Order + 1
        };
    }

    /// <summary>
    /// Resets the track to default values.
    /// </summary>
    public void Reset()
    {
        IsArmed = false;
        IsMonitoring = false;
        IsFrozen = false;
        IsMuted = false;
        IsSoloed = false;
        Volume = 0.8f;
        Pan = 0f;
        Height = 80;
        IsMinimized = false;
        HasAutomation = false;
        AutomationExpanded = false;
        SetDefaultsForType(TrackType);
    }

    /// <summary>
    /// Toggles the frozen state (freeze/unfreeze).
    /// </summary>
    public void ToggleFreeze()
    {
        if (CanFreeze)
        {
            IsFrozen = !IsFrozen;
        }
    }

    /// <summary>
    /// Toggles the minimized state.
    /// </summary>
    public void ToggleMinimize()
    {
        IsMinimized = !IsMinimized;
        Height = IsMinimized ? 24 : 80;
    }
}

/// <summary>
/// Represents an available input source option.
/// </summary>
public class InputSourceOption
{
    /// <summary>
    /// Gets or sets the source identifier.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the source type (MIDI, Audio, etc.).
    /// </summary>
    public string Type { get; set; } = "";
}

/// <summary>
/// Represents an available output bus option.
/// </summary>
public class OutputBusOption
{
    /// <summary>
    /// Gets or sets the bus identifier.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = "Master";

    /// <summary>
    /// Gets or sets whether this is the master output.
    /// </summary>
    public bool IsMaster => Id == null;
}

/// <summary>
/// Represents an available audio input option.
/// </summary>
public class AudioInputOption
{
    /// <summary>
    /// Gets or sets the input identifier.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the device name.
    /// </summary>
    public string DeviceName { get; set; } = "";

    /// <summary>
    /// Gets or sets the channel configuration (Mono/Stereo).
    /// </summary>
    public string ChannelConfig { get; set; } = "Stereo";
}

/// <summary>
/// Represents a preset track color option.
/// </summary>
public class TrackColorOption
{
    /// <summary>
    /// Gets or sets the color hex value.
    /// </summary>
    public string Color { get; set; } = "#4A9EFF";

    /// <summary>
    /// Gets or sets the color name.
    /// </summary>
    public string Name { get; set; } = "Blue";

    /// <summary>
    /// Gets the color as a WPF Brush.
    /// </summary>
    public Brush Brush
    {
        get
        {
            try
            {
                return new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(Color));
            }
            catch
            {
                return Brushes.DodgerBlue;
            }
        }
    }
}
