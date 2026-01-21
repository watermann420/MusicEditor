// MusicEngineEditor - Mixer Commands
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using MusicEngine.Core.UndoRedo;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Commands;

/// <summary>
/// Command for changing mixer channel volume with merge support for continuous fader movement.
/// </summary>
public sealed class MixerVolumeCommand : IUndoableCommand
{
    private readonly MixerChannel _channel;
    private readonly float _oldVolume;
    private readonly float _newVolume;
    private readonly DateTime _timestamp;
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc/>
    public string Description => $"Change {_channel.Name} Volume";

    /// <summary>
    /// Creates a new MixerVolumeCommand.
    /// </summary>
    /// <param name="channel">The mixer channel to modify.</param>
    /// <param name="newVolume">The new volume value (0.0 to 1.0+).</param>
    public MixerVolumeCommand(MixerChannel channel, float newVolume)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _oldVolume = channel.Volume;
        _newVolume = newVolume;
        _timestamp = DateTime.UtcNow;
    }

    private MixerVolumeCommand(MixerChannel channel, float oldVolume, float newVolume, DateTime timestamp)
    {
        _channel = channel;
        _oldVolume = oldVolume;
        _newVolume = newVolume;
        _timestamp = timestamp;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _channel.Volume = _newVolume;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _channel.Volume = _oldVolume;
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        // Merge continuous fader movements within a time window
        return other is MixerVolumeCommand otherVol &&
               otherVol._channel == _channel &&
               DateTime.UtcNow - otherVol._timestamp < MergeWindow;
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is MixerVolumeCommand otherVol)
        {
            // Keep our original volume, use their new volume
            return new MixerVolumeCommand(_channel, _oldVolume, otherVol._newVolume, _timestamp);
        }
        return this;
    }
}

/// <summary>
/// Command for changing mixer channel pan with merge support for continuous movement.
/// </summary>
public sealed class MixerPanCommand : IUndoableCommand
{
    private readonly MixerChannel _channel;
    private readonly float _oldPan;
    private readonly float _newPan;
    private readonly DateTime _timestamp;
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc/>
    public string Description => $"Change {_channel.Name} Pan";

    /// <summary>
    /// Creates a new MixerPanCommand.
    /// </summary>
    /// <param name="channel">The mixer channel to modify.</param>
    /// <param name="newPan">The new pan value (-1.0 to 1.0).</param>
    public MixerPanCommand(MixerChannel channel, float newPan)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _oldPan = channel.Pan;
        _newPan = Math.Clamp(newPan, -1f, 1f);
        _timestamp = DateTime.UtcNow;
    }

    private MixerPanCommand(MixerChannel channel, float oldPan, float newPan, DateTime timestamp)
    {
        _channel = channel;
        _oldPan = oldPan;
        _newPan = newPan;
        _timestamp = timestamp;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _channel.Pan = _newPan;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _channel.Pan = _oldPan;
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        return other is MixerPanCommand otherPan &&
               otherPan._channel == _channel &&
               DateTime.UtcNow - otherPan._timestamp < MergeWindow;
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is MixerPanCommand otherPan)
        {
            return new MixerPanCommand(_channel, _oldPan, otherPan._newPan, _timestamp);
        }
        return this;
    }
}

/// <summary>
/// Command for toggling mixer channel mute state.
/// </summary>
public sealed class MixerMuteCommand : IUndoableCommand
{
    private readonly MixerChannel _channel;
    private readonly bool _oldMuted;
    private readonly bool _newMuted;

    /// <inheritdoc/>
    public string Description => _newMuted ? $"Mute {_channel.Name}" : $"Unmute {_channel.Name}";

    /// <summary>
    /// Creates a new MixerMuteCommand.
    /// </summary>
    /// <param name="channel">The mixer channel to modify.</param>
    /// <param name="muted">The new muted state.</param>
    public MixerMuteCommand(MixerChannel channel, bool muted)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _oldMuted = channel.IsMuted;
        _newMuted = muted;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _channel.IsMuted = _newMuted;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _channel.IsMuted = _oldMuted;
    }
}

/// <summary>
/// Command for toggling mixer channel solo state.
/// </summary>
public sealed class MixerSoloCommand : IUndoableCommand
{
    private readonly MixerChannel _channel;
    private readonly bool _oldSoloed;
    private readonly bool _newSoloed;

    /// <inheritdoc/>
    public string Description => _newSoloed ? $"Solo {_channel.Name}" : $"Unsolo {_channel.Name}";

    /// <summary>
    /// Creates a new MixerSoloCommand.
    /// </summary>
    /// <param name="channel">The mixer channel to modify.</param>
    /// <param name="soloed">The new soloed state.</param>
    public MixerSoloCommand(MixerChannel channel, bool soloed)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _oldSoloed = channel.IsSoloed;
        _newSoloed = soloed;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _channel.IsSoloed = _newSoloed;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _channel.IsSoloed = _oldSoloed;
    }
}

/// <summary>
/// Command for changing mixer channel name.
/// </summary>
public sealed class MixerChannelRenameCommand : IUndoableCommand
{
    private readonly MixerChannel _channel;
    private readonly string _oldName;
    private readonly string _newName;

    /// <inheritdoc/>
    public string Description => $"Rename Channel to {_newName}";

    /// <summary>
    /// Creates a new MixerChannelRenameCommand.
    /// </summary>
    /// <param name="channel">The mixer channel to rename.</param>
    /// <param name="newName">The new name.</param>
    public MixerChannelRenameCommand(MixerChannel channel, string newName)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _oldName = channel.Name;
        _newName = newName ?? throw new ArgumentNullException(nameof(newName));
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _channel.Name = _newName;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _channel.Name = _oldName;
    }
}

/// <summary>
/// Command for changing mixer channel color.
/// </summary>
public sealed class MixerChannelColorCommand : IUndoableCommand
{
    private readonly MixerChannel _channel;
    private readonly string _oldColor;
    private readonly string _newColor;

    /// <inheritdoc/>
    public string Description => $"Change {_channel.Name} Color";

    /// <summary>
    /// Creates a new MixerChannelColorCommand.
    /// </summary>
    /// <param name="channel">The mixer channel to modify.</param>
    /// <param name="newColor">The new color hex string.</param>
    public MixerChannelColorCommand(MixerChannel channel, string newColor)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _oldColor = channel.Color;
        _newColor = newColor ?? throw new ArgumentNullException(nameof(newColor));
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _channel.Color = _newColor;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _channel.Color = _oldColor;
    }
}

/// <summary>
/// Command for changing master channel properties.
/// </summary>
public sealed class MasterChannelPropertyCommand : IUndoableCommand
{
    private readonly MasterChannel _master;
    private readonly string _propertyName;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    /// <inheritdoc/>
    public string Description => $"Change Master {_propertyName}";

    /// <summary>
    /// Creates a new MasterChannelPropertyCommand.
    /// </summary>
    /// <param name="master">The master channel to modify.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="oldValue">The old value.</param>
    /// <param name="newValue">The new value.</param>
    public MasterChannelPropertyCommand(MasterChannel master, string propertyName, object? oldValue, object? newValue)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _oldValue = oldValue;
        _newValue = newValue;
    }

    /// <inheritdoc/>
    public void Execute() => SetProperty(_newValue);

    /// <inheritdoc/>
    public void Undo() => SetProperty(_oldValue);

    private void SetProperty(object? value)
    {
        switch (_propertyName)
        {
            case nameof(MasterChannel.LimiterEnabled):
                _master.LimiterEnabled = (bool)(value ?? true);
                break;
            case nameof(MasterChannel.LimiterCeiling):
                _master.LimiterCeiling = (float)(value ?? -0.3f);
                break;
            case nameof(MasterChannel.StereoWidth):
                _master.StereoWidth = (float)(value ?? 1.0f);
                break;
            case nameof(MasterChannel.Volume):
                _master.Volume = (float)(value ?? 1.0f);
                break;
        }
    }
}

/// <summary>
/// Command for changing send level with merge support.
/// </summary>
public sealed class SendLevelCommand : IUndoableCommand
{
    private readonly Send _send;
    private readonly float _oldLevel;
    private readonly float _newLevel;
    private readonly DateTime _timestamp;
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc/>
    public string Description => $"Change Send Level to {_send.TargetBusName}";

    /// <summary>
    /// Creates a new SendLevelCommand.
    /// </summary>
    /// <param name="send">The send to modify.</param>
    /// <param name="newLevel">The new send level.</param>
    public SendLevelCommand(Send send, float newLevel)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _oldLevel = send.Level;
        _newLevel = Math.Clamp(newLevel, 0f, 1f);
        _timestamp = DateTime.UtcNow;
    }

    private SendLevelCommand(Send send, float oldLevel, float newLevel, DateTime timestamp)
    {
        _send = send;
        _oldLevel = oldLevel;
        _newLevel = newLevel;
        _timestamp = timestamp;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _send.Level = _newLevel;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _send.Level = _oldLevel;
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        return other is SendLevelCommand otherSend &&
               otherSend._send == _send &&
               DateTime.UtcNow - otherSend._timestamp < MergeWindow;
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is SendLevelCommand otherSend)
        {
            return new SendLevelCommand(_send, _oldLevel, otherSend._newLevel, _timestamp);
        }
        return this;
    }
}

/// <summary>
/// Command for changing bus channel volume with merge support.
/// </summary>
public sealed class BusVolumeCommand : IUndoableCommand
{
    private readonly BusChannel _bus;
    private readonly float _oldVolume;
    private readonly float _newVolume;
    private readonly DateTime _timestamp;
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc/>
    public string Description => $"Change {_bus.Name} Bus Volume";

    /// <summary>
    /// Creates a new BusVolumeCommand.
    /// </summary>
    /// <param name="bus">The bus channel to modify.</param>
    /// <param name="newVolume">The new volume value.</param>
    public BusVolumeCommand(BusChannel bus, float newVolume)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _oldVolume = bus.Volume;
        _newVolume = newVolume;
        _timestamp = DateTime.UtcNow;
    }

    private BusVolumeCommand(BusChannel bus, float oldVolume, float newVolume, DateTime timestamp)
    {
        _bus = bus;
        _oldVolume = oldVolume;
        _newVolume = newVolume;
        _timestamp = timestamp;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _bus.Volume = _newVolume;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _bus.Volume = _oldVolume;
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        return other is BusVolumeCommand otherBus &&
               otherBus._bus == _bus &&
               DateTime.UtcNow - otherBus._timestamp < MergeWindow;
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is BusVolumeCommand otherBus)
        {
            return new BusVolumeCommand(_bus, _oldVolume, otherBus._newVolume, _timestamp);
        }
        return this;
    }
}
