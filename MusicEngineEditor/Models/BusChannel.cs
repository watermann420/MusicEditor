//MusicEngineEditor - Bus Channel Model
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Defines the type of bus channel.
/// </summary>
public enum BusChannelType
{
    /// <summary>
    /// Standard group bus for summing multiple channels.
    /// </summary>
    Group,

    /// <summary>
    /// Auxiliary bus for send/return effects.
    /// </summary>
    Aux,

    /// <summary>
    /// Master output bus.
    /// </summary>
    Master
}

/// <summary>
/// Represents a bus/group channel in the mixer for routing and submixing.
/// </summary>
public partial class BusChannel : ObservableObject
{
    private readonly string _id;

    /// <summary>
    /// Gets the unique identifier for this bus.
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// Gets or sets the bus name.
    /// </summary>
    [ObservableProperty]
    private string _name = "Bus";

    /// <summary>
    /// Gets or sets the bus type.
    /// </summary>
    [ObservableProperty]
    private BusChannelType _busType = BusChannelType.Group;

    /// <summary>
    /// Gets or sets the bus color for visual identification.
    /// </summary>
    [ObservableProperty]
    private string _color = "#FF9500";

    /// <summary>
    /// Gets or sets the volume level (0.0 to 1.25).
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
    /// Gets or sets whether the bus is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets or sets whether the bus is soloed.
    /// </summary>
    [ObservableProperty]
    private bool _isSoloed;

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
    /// Gets or sets whether the bus is effectively muted (due to other buses being soloed).
    /// </summary>
    [ObservableProperty]
    private bool _isEffectivelyMuted;

    /// <summary>
    /// Gets the effect slots for this bus.
    /// </summary>
    public ObservableCollection<EffectSlot> EffectSlots { get; } = [];

    /// <summary>
    /// Gets the list of channel indices routed to this bus.
    /// </summary>
    public ObservableCollection<int> RoutedChannels { get; } = [];

    /// <summary>
    /// Gets or sets the output bus (for cascaded routing).
    /// Null means routed directly to master.
    /// </summary>
    [ObservableProperty]
    private BusChannel? _outputBus;

    /// <summary>
    /// Gets or sets whether this bus has any effects loaded.
    /// </summary>
    [ObservableProperty]
    private bool _hasEffects;

    /// <summary>
    /// Gets or sets the number of effects in the bus.
    /// </summary>
    [ObservableProperty]
    private int _effectCount;

    /// <summary>
    /// Creates a new bus channel with a unique identifier.
    /// </summary>
    public BusChannel()
    {
        _id = Guid.NewGuid().ToString("N")[..8];
        InitializeEffectSlots();
    }

    /// <summary>
    /// Creates a new bus channel with the specified name.
    /// </summary>
    /// <param name="name">The bus name.</param>
    /// <param name="busType">The type of bus.</param>
    public BusChannel(string name, BusChannelType busType = BusChannelType.Group) : this()
    {
        _name = name;
        _busType = busType;

        // Set default colors based on bus type
        _color = busType switch
        {
            BusChannelType.Master => "#FF5555",
            BusChannelType.Aux => "#55AAFF",
            BusChannelType.Group => "#FF9500",
            _ => "#FF9500"
        };
    }

    /// <summary>
    /// Initializes the default effect slots for this bus.
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
    /// Adds a channel to the routing list.
    /// </summary>
    /// <param name="channelIndex">The channel index to add.</param>
    public void AddRoutedChannel(int channelIndex)
    {
        if (!RoutedChannels.Contains(channelIndex))
        {
            RoutedChannels.Add(channelIndex);
        }
    }

    /// <summary>
    /// Removes a channel from the routing list.
    /// </summary>
    /// <param name="channelIndex">The channel index to remove.</param>
    public void RemoveRoutedChannel(int channelIndex)
    {
        RoutedChannels.Remove(channelIndex);
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
    /// <returns>True if the effect was added, false if no slots available.</returns>
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
    /// Resets the bus to default values.
    /// </summary>
    public void Reset()
    {
        Volume = 0.8f;
        Pan = 0f;
        IsMuted = false;
        IsSoloed = false;
        MeterLeft = 0f;
        MeterRight = 0f;

        foreach (var slot in EffectSlots)
        {
            slot.ClearEffect();
        }

        UpdateEffectCount();
    }

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
