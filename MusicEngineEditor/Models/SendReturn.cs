//MusicEngineEditor - Send/Return Models
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a send from a mixer channel to a bus.
/// </summary>
public partial class Send : ObservableObject
{
    /// <summary>
    /// Gets or sets the target bus ID.
    /// </summary>
    [ObservableProperty]
    private string _targetBusId = string.Empty;

    /// <summary>
    /// Gets or sets the target bus name (for display).
    /// </summary>
    [ObservableProperty]
    private string _targetBusName = string.Empty;

    /// <summary>
    /// Gets or sets the send level (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _level = 0.5f;

    /// <summary>
    /// Gets the send level in decibels.
    /// </summary>
    public float LevelDb
    {
        get => Level <= 0 ? -60f : (float)(20.0 * Math.Log10(Level));
        set => Level = value <= -60f ? 0f : (float)Math.Pow(10.0, value / 20.0);
    }

    /// <summary>
    /// Gets or sets whether this send is pre-fader.
    /// Pre-fader sends are not affected by the channel's volume fader.
    /// </summary>
    [ObservableProperty]
    private bool _isPreFader;

    /// <summary>
    /// Gets or sets whether this send is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// Gets or sets the send color (typically matches the target bus color).
    /// </summary>
    [ObservableProperty]
    private string _color = "#55AAFF";

    /// <summary>
    /// Creates a new send.
    /// </summary>
    public Send()
    {
    }

    /// <summary>
    /// Creates a new send to a specific bus.
    /// </summary>
    /// <param name="targetBusId">The target bus ID.</param>
    /// <param name="targetBusName">The target bus name.</param>
    /// <param name="level">The initial send level.</param>
    public Send(string targetBusId, string targetBusName, float level = 0.5f)
    {
        _targetBusId = targetBusId;
        _targetBusName = targetBusName;
        _level = level;
    }
}

/// <summary>
/// Represents a return channel that receives signal from a bus and routes it back to the mixer.
/// Typically used for send/return effect configurations.
/// </summary>
public partial class ReturnChannel : ObservableObject
{
    private readonly string _id;

    /// <summary>
    /// Gets the unique identifier for this return.
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// Gets or sets the return name.
    /// </summary>
    [ObservableProperty]
    private string _name = "Return";

    /// <summary>
    /// Gets or sets the source bus ID.
    /// </summary>
    [ObservableProperty]
    private string _sourceBusId = string.Empty;

    /// <summary>
    /// Gets or sets the source bus reference.
    /// </summary>
    [ObservableProperty]
    private BusChannel? _sourceBus;

    /// <summary>
    /// Gets or sets the return volume (0.0 to 1.25).
    /// </summary>
    [ObservableProperty]
    private float _volume = 0.8f;

    /// <summary>
    /// Gets the volume in decibels.
    /// </summary>
    public float VolumeDb
    {
        get => Volume <= 0 ? -60f : (float)(20.0 * Math.Log10(Volume));
        set => Volume = value <= -60f ? 0f : (float)Math.Pow(10.0, value / 20.0);
    }

    /// <summary>
    /// Gets or sets the pan position (-1.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _pan;

    /// <summary>
    /// Gets or sets whether the return is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets or sets the left meter level.
    /// </summary>
    [ObservableProperty]
    private float _meterLeft;

    /// <summary>
    /// Gets or sets the right meter level.
    /// </summary>
    [ObservableProperty]
    private float _meterRight;

    /// <summary>
    /// Gets or sets the return color.
    /// </summary>
    [ObservableProperty]
    private string _color = "#55AAFF";

    /// <summary>
    /// Creates a new return channel.
    /// </summary>
    public ReturnChannel()
    {
        _id = Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// Creates a new return channel linked to a bus.
    /// </summary>
    /// <param name="name">The return name.</param>
    /// <param name="sourceBus">The source bus.</param>
    public ReturnChannel(string name, BusChannel sourceBus) : this()
    {
        _name = name;
        _sourceBus = sourceBus;
        _sourceBusId = sourceBus.Id;
        _color = sourceBus.Color;
    }

    /// <summary>
    /// Updates the meter levels.
    /// </summary>
    /// <param name="left">Left channel level.</param>
    /// <param name="right">Right channel level.</param>
    public void UpdateMeters(float left, float right)
    {
        MeterLeft = left;
        MeterRight = right;
    }

    /// <summary>
    /// Resets the return to default values.
    /// </summary>
    public void Reset()
    {
        Volume = 0.8f;
        Pan = 0f;
        IsMuted = false;
        MeterLeft = 0f;
        MeterRight = 0f;
    }
}

/// <summary>
/// Represents a complete send/return routing configuration for effects.
/// </summary>
public partial class SendReturnConfiguration : ObservableObject
{
    /// <summary>
    /// Gets or sets the configuration name.
    /// </summary>
    [ObservableProperty]
    private string _name = "FX";

    /// <summary>
    /// Gets or sets the aux bus for this configuration.
    /// </summary>
    [ObservableProperty]
    private BusChannel? _auxBus;

    /// <summary>
    /// Gets or sets the return channel for this configuration.
    /// </summary>
    [ObservableProperty]
    private ReturnChannel? _returnChannel;

    /// <summary>
    /// Gets or sets whether this configuration is active.
    /// </summary>
    [ObservableProperty]
    private bool _isActive = true;

    /// <summary>
    /// Gets or sets the configuration color.
    /// </summary>
    [ObservableProperty]
    private string _color = "#55AAFF";

    /// <summary>
    /// Creates a new send/return configuration.
    /// </summary>
    public SendReturnConfiguration()
    {
    }

    /// <summary>
    /// Creates a new send/return configuration with a name.
    /// </summary>
    /// <param name="name">The configuration name.</param>
    public SendReturnConfiguration(string name)
    {
        _name = name;
    }

    /// <summary>
    /// Initializes the aux bus and return channel for this configuration.
    /// </summary>
    /// <param name="name">The name for the bus and return.</param>
    /// <param name="color">The color for visual identification.</param>
    public void Initialize(string name, string color)
    {
        Name = name;
        Color = color;

        AuxBus = new BusChannel($"{name} Bus", BusChannelType.Aux)
        {
            Color = color
        };

        ReturnChannel = new ReturnChannel($"{name} Return", AuxBus)
        {
            Color = color
        };
    }
}
