// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Data model class.

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Defines the type of track icon for visual identification.
/// </summary>
public enum TrackIconType
{
    /// <summary>No icon.</summary>
    None,
    /// <summary>Generic audio track.</summary>
    Audio,
    /// <summary>MIDI track.</summary>
    Midi,
    /// <summary>Drums/Percussion track.</summary>
    Drums,
    /// <summary>Bass track.</summary>
    Bass,
    /// <summary>Synthesizer track.</summary>
    Synth,
    /// <summary>Vocals track.</summary>
    Vocals,
    /// <summary>Guitar track.</summary>
    Guitar,
    /// <summary>Piano/Keys track.</summary>
    Piano,
    /// <summary>Strings track.</summary>
    Strings,
    /// <summary>Brass track.</summary>
    Brass,
    /// <summary>Woodwinds track.</summary>
    Woodwinds,
    /// <summary>FX/Sound design track.</summary>
    FX,
    /// <summary>Bus/Group track.</summary>
    Bus,
    /// <summary>Master output.</summary>
    Master
}

/// <summary>
/// Represents a single channel in the mixer with volume, pan, mute/solo, metering,
/// effect slots, and send/return routing.
/// </summary>
public partial class MixerChannel : ObservableObject
{
    private readonly int _index;

    /// <summary>
    /// Gets the channel index (0-based).
    /// </summary>
    public int Index => _index;

    /// <summary>
    /// Gets or sets the channel name.
    /// </summary>
    [ObservableProperty]
    private string _name = "Channel";

    /// <summary>
    /// Gets or sets the channel color for visual identification.
    /// </summary>
    [ObservableProperty]
    private string _color = "#4A9EFF";

    /// <summary>
    /// Gets or sets the track icon type for visual identification.
    /// </summary>
    [ObservableProperty]
    private TrackIconType _icon = TrackIconType.None;

    /// <summary>
    /// Gets or sets a custom icon path (overrides Icon if set).
    /// </summary>
    [ObservableProperty]
    private string? _iconPath;

    /// <summary>
    /// Gets or sets whether the channel phase is inverted.
    /// When true, all samples are multiplied by -1.
    /// </summary>
    [ObservableProperty]
    private bool _isPhaseInverted;

    /// <summary>
    /// Gets or sets the volume level (0.0 to 1.0, can exceed for gain).
    /// </summary>
    [ObservableProperty]
    private float _volume = 0.8f;

    /// <summary>
    /// Gets or sets the volume in decibels (-60 to +12 dB).
    /// </summary>
    public float VolumeDb
    {
        get => Volume <= 0 ? -60f : (float)(20.0 * Math.Log10(Volume));
        set => Volume = value <= -60f ? 0f : (float)Math.Pow(10.0, value / 20.0);
    }

    /// <summary>
    /// Gets or sets the pan position (-1.0 = full left, 0.0 = center, 1.0 = full right).
    /// </summary>
    [ObservableProperty]
    private float _pan = 0f;

    /// <summary>
    /// Gets or sets whether the channel is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets or sets whether the channel is soloed.
    /// </summary>
    [ObservableProperty]
    private bool _isSoloed;

    /// <summary>
    /// Gets or sets whether the channel is armed for recording.
    /// </summary>
    [ObservableProperty]
    private bool _isArmed;

    /// <summary>
    /// Gets or sets the current left channel meter level (0.0 to 1.0+).
    /// </summary>
    [ObservableProperty]
    private float _meterLeft;

    /// <summary>
    /// Gets or sets the current right channel meter level (0.0 to 1.0+).
    /// </summary>
    [ObservableProperty]
    private float _meterRight;

    /// <summary>
    /// Gets or sets the instrument/synth name associated with this channel.
    /// </summary>
    [ObservableProperty]
    private string? _instrumentName;

    /// <summary>
    /// Gets or sets whether this channel has an effect chain.
    /// </summary>
    [ObservableProperty]
    private bool _hasEffects;

    /// <summary>
    /// Gets or sets the number of effects in the chain.
    /// </summary>
    [ObservableProperty]
    private int _effectCount;

    /// <summary>
    /// Gets whether the channel is effectively muted (muted or other channels are soloed).
    /// </summary>
    [ObservableProperty]
    private bool _isEffectivelyMuted;

    /// <summary>
    /// Gets or sets the output bus ID (null = direct to master).
    /// </summary>
    [ObservableProperty]
    private string? _outputBusId;

    /// <summary>
    /// Gets or sets the output bus name for display.
    /// </summary>
    [ObservableProperty]
    private string _outputBusName = "Master";

    /// <summary>
    /// Gets or sets whether the effect chain is bypassed.
    /// </summary>
    [ObservableProperty]
    private bool _isEffectChainBypassed;

    /// <summary>
    /// Gets the effect slots for this channel.
    /// </summary>
    public ObservableCollection<EffectSlot> EffectSlots { get; } = [];

    /// <summary>
    /// Gets the sends for this channel.
    /// </summary>
    public ObservableCollection<Send> Sends { get; } = [];

    /// <summary>
    /// Creates a new mixer channel.
    /// </summary>
    /// <param name="index">The channel index.</param>
    /// <param name="name">The channel name.</param>
    public MixerChannel(int index, string name)
    {
        _index = index;
        _name = name;
        InitializeEffectSlots();
    }

    /// <summary>
    /// Creates a new mixer channel with default name.
    /// </summary>
    /// <param name="index">The channel index.</param>
    public MixerChannel(int index) : this(index, $"Ch {index + 1}")
    {
    }

    /// <summary>
    /// Initializes the default effect slots for this channel.
    /// </summary>
    private void InitializeEffectSlots()
    {
        // Start with 4 empty effect slots
        for (int i = 0; i < 4; i++)
        {
            EffectSlots.Add(new EffectSlot(i));
        }
    }

    /// <summary>
    /// Resets the channel to default values.
    /// </summary>
    public void Reset()
    {
        Volume = 0.8f;
        Pan = 0f;
        IsMuted = false;
        IsSoloed = false;
        IsArmed = false;
        IsPhaseInverted = false;
        MeterLeft = 0f;
        MeterRight = 0f;
        OutputBusId = null;
        OutputBusName = "Master";
        IsEffectChainBypassed = false;
        Icon = TrackIconType.None;
        IconPath = null;

        // Clear effects
        foreach (var slot in EffectSlots)
        {
            slot.ClearEffect();
        }

        // Clear sends
        Sends.Clear();

        UpdateEffectCount();
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
    /// Adds an effect to the first available slot.
    /// </summary>
    /// <param name="effectType">The effect type identifier.</param>
    /// <param name="displayName">The display name.</param>
    /// <returns>True if the effect was added.</returns>
    public bool AddEffect(string effectType, string displayName)
    {
        foreach (var slot in EffectSlots)
        {
            if (slot.IsEmpty)
            {
                slot.LoadEffect(effectType, displayName);
                UpdateEffectCount();
                return true;
            }
        }

        // Add a new slot if all are full
        var newSlot = new EffectSlot(EffectSlots.Count, effectType, displayName);
        EffectSlots.Add(newSlot);
        UpdateEffectCount();
        return true;
    }

    /// <summary>
    /// Removes an effect at the specified slot index.
    /// </summary>
    /// <param name="slotIndex">The slot index.</param>
    public void RemoveEffect(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < EffectSlots.Count)
        {
            EffectSlots[slotIndex].ClearEffect();
            UpdateEffectCount();
        }
    }

    /// <summary>
    /// Adds a send to a bus.
    /// </summary>
    /// <param name="targetBusId">The target bus ID.</param>
    /// <param name="targetBusName">The target bus name.</param>
    /// <param name="level">The send level.</param>
    public void AddSend(string targetBusId, string targetBusName, float level = 0.5f)
    {
        // Check if send already exists
        foreach (var send in Sends)
        {
            if (send.TargetBusId == targetBusId)
            {
                return;
            }
        }

        Sends.Add(new Send(targetBusId, targetBusName, level));
    }

    /// <summary>
    /// Removes a send to the specified bus.
    /// </summary>
    /// <param name="targetBusId">The target bus ID.</param>
    public void RemoveSend(string targetBusId)
    {
        Send? toRemove = null;
        foreach (var send in Sends)
        {
            if (send.TargetBusId == targetBusId)
            {
                toRemove = send;
                break;
            }
        }

        if (toRemove != null)
        {
            Sends.Remove(toRemove);
        }
    }

    /// <summary>
    /// Sets the output bus for this channel.
    /// </summary>
    /// <param name="busId">The bus ID (null for master).</param>
    /// <param name="busName">The bus name.</param>
    public void SetOutputBus(string? busId, string busName)
    {
        OutputBusId = busId;
        OutputBusName = busName;
    }

    /// <summary>
    /// Updates the effect count and HasEffects property.
    /// </summary>
    private void UpdateEffectCount()
    {
        int count = 0;
        foreach (var slot in EffectSlots)
        {
            if (!slot.IsEmpty)
                count++;
        }

        EffectCount = count;
        HasEffects = count > 0;
    }
}

/// <summary>
/// Represents the master channel with additional properties.
/// </summary>
public partial class MasterChannel : MixerChannel
{
    /// <summary>
    /// Gets or sets whether the limiter is enabled on the master.
    /// </summary>
    [ObservableProperty]
    private bool _limiterEnabled = true;

    /// <summary>
    /// Gets or sets the limiter ceiling in dB.
    /// </summary>
    [ObservableProperty]
    private float _limiterCeiling = -0.3f;

    /// <summary>
    /// Gets or sets the master stereo width (0.0 = mono, 1.0 = stereo, 2.0 = wide).
    /// </summary>
    [ObservableProperty]
    private float _stereoWidth = 1.0f;

    /// <summary>
    /// Creates the master channel.
    /// </summary>
    public MasterChannel() : base(-1, "Master")
    {
        Color = "#FF9500";
        Volume = 1.0f;
        OutputBusName = "Output";
    }

    /// <summary>
    /// Resets the master channel to default values.
    /// </summary>
    public new void Reset()
    {
        base.Reset();
        Volume = 1.0f;
        LimiterEnabled = true;
        LimiterCeiling = -0.3f;
        StereoWidth = 1.0f;
        OutputBusName = "Output";
    }
}
