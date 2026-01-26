// MusicEngineEditor - Mixer Scene Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Threading;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents a snapshot of a single channel's mixer state.
/// </summary>
public class ChannelSnapshot
{
    /// <summary>
    /// Gets or sets the channel index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the channel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the volume level.
    /// </summary>
    public float Volume { get; set; }

    /// <summary>
    /// Gets or sets the pan position.
    /// </summary>
    public float Pan { get; set; }

    /// <summary>
    /// Gets or sets whether the channel is muted.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets whether the channel is soloed.
    /// </summary>
    public bool IsSoloed { get; set; }

    /// <summary>
    /// Gets or sets the output bus ID.
    /// </summary>
    public string? OutputBusId { get; set; }

    /// <summary>
    /// Gets or sets the send levels (bus ID -> level).
    /// </summary>
    public Dictionary<string, float> SendLevels { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the effect chain is bypassed.
    /// </summary>
    public bool IsEffectChainBypassed { get; set; }
}

/// <summary>
/// Represents a complete mixer scene (snapshot of all channels).
/// </summary>
public class MixerScene : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _modifiedAt = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the unique identifier for this scene.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the scene name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the scene description.
    /// </summary>
    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets when the scene was created.
    /// </summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set { _createdAt = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets when the scene was last modified.
    /// </summary>
    public DateTime ModifiedAt
    {
        get => _modifiedAt;
        set { _modifiedAt = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the channel snapshots.
    /// </summary>
    public List<ChannelSnapshot> Channels { get; set; } = new();

    /// <summary>
    /// Gets or sets the master channel snapshot.
    /// </summary>
    public ChannelSnapshot? MasterChannel { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Service for saving, recalling, and interpolating mixer scenes.
/// Provides A/B comparison functionality.
/// </summary>
public sealed class MixerSceneService : INotifyPropertyChanged, IDisposable
{
    #region Singleton

    private static MixerSceneService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance of the MixerSceneService.
    /// </summary>
    public static MixerSceneService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new MixerSceneService();
                }
            }
            return _instance;
        }
    }

    #endregion

    #region Private Fields

    private readonly ObservableCollection<MixerScene> _scenes = new();
    private MixerScene? _sceneA;
    private MixerScene? _sceneB;
    private bool _isAActive = true;
    private bool _isInterpolating;
    private double _interpolationProgress;
    private DispatcherTimer? _interpolationTimer;
    private MixerScene? _interpolationSource;
    private MixerScene? _interpolationTarget;
    private double _interpolationDuration;
    private DateTime _interpolationStartTime;
    private bool _disposed;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the collection of saved scenes.
    /// </summary>
    public ObservableCollection<MixerScene> Scenes => _scenes;

    /// <summary>
    /// Gets or sets scene A for A/B comparison.
    /// </summary>
    public MixerScene? SceneA
    {
        get => _sceneA;
        set { _sceneA = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets scene B for A/B comparison.
    /// </summary>
    public MixerScene? SceneB
    {
        get => _sceneB;
        set { _sceneB = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets whether scene A is currently active (vs scene B).
    /// </summary>
    public bool IsAActive
    {
        get => _isAActive;
        set { _isAActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveScene)); }
    }

    /// <summary>
    /// Gets the currently active scene (A or B).
    /// </summary>
    public MixerScene? ActiveScene => IsAActive ? SceneA : SceneB;

    /// <summary>
    /// Gets whether an interpolation is currently in progress.
    /// </summary>
    public bool IsInterpolating
    {
        get => _isInterpolating;
        private set { _isInterpolating = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets the current interpolation progress (0-1).
    /// </summary>
    public double InterpolationProgress
    {
        get => _interpolationProgress;
        private set { _interpolationProgress = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets the number of saved scenes.
    /// </summary>
    public int SceneCount => _scenes.Count;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a scene is recalled.
    /// </summary>
    public event EventHandler<MixerScene>? SceneRecalled;

    /// <summary>
    /// Event raised when interpolation requests a channel value update.
    /// </summary>
    public event EventHandler<(int channelIndex, string property, float value)>? InterpolationUpdate;

    /// <summary>
    /// Event raised when interpolation completes.
    /// </summary>
    public event EventHandler? InterpolationCompleted;

    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region Constructor

    private MixerSceneService()
    {
    }

    #endregion

    #region Public Methods - Scene Management

    /// <summary>
    /// Creates a new scene from the current mixer state.
    /// </summary>
    /// <param name="channels">The current mixer channels.</param>
    /// <param name="masterChannel">The master channel.</param>
    /// <param name="name">The scene name.</param>
    /// <param name="description">Optional description.</param>
    /// <returns>The created scene.</returns>
    public MixerScene SaveScene(
        IEnumerable<MixerChannel> channels,
        MasterChannel? masterChannel,
        string name,
        string description = "")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var scene = new MixerScene
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        // Capture channel states
        foreach (var channel in channels)
        {
            var snapshot = CreateChannelSnapshot(channel);
            scene.Channels.Add(snapshot);
        }

        // Capture master state
        if (masterChannel != null)
        {
            scene.MasterChannel = CreateChannelSnapshot(masterChannel);
        }

        _scenes.Add(scene);
        OnPropertyChanged(nameof(SceneCount));

        return scene;
    }

    /// <summary>
    /// Updates an existing scene with the current mixer state.
    /// </summary>
    public void UpdateScene(
        MixerScene scene,
        IEnumerable<MixerChannel> channels,
        MasterChannel? masterChannel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);

        scene.Channels.Clear();

        foreach (var channel in channels)
        {
            scene.Channels.Add(CreateChannelSnapshot(channel));
        }

        if (masterChannel != null)
        {
            scene.MasterChannel = CreateChannelSnapshot(masterChannel);
        }

        scene.ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Recalls a scene, applying it to the mixer channels.
    /// </summary>
    /// <param name="scene">The scene to recall.</param>
    /// <param name="channels">The mixer channels to update.</param>
    /// <param name="masterChannel">The master channel to update.</param>
    public void RecallScene(
        MixerScene scene,
        IList<MixerChannel> channels,
        MasterChannel? masterChannel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);

        foreach (var snapshot in scene.Channels)
        {
            if (snapshot.Index >= 0 && snapshot.Index < channels.Count)
            {
                ApplySnapshot(channels[snapshot.Index], snapshot);
            }
        }

        if (masterChannel != null && scene.MasterChannel != null)
        {
            ApplySnapshot(masterChannel, scene.MasterChannel);
        }

        SceneRecalled?.Invoke(this, scene);
    }

    /// <summary>
    /// Recalls a scene with interpolation over time.
    /// </summary>
    /// <param name="scene">The target scene.</param>
    /// <param name="currentChannels">Current mixer channels.</param>
    /// <param name="masterChannel">The master channel.</param>
    /// <param name="durationMs">Interpolation duration in milliseconds.</param>
    public void RecallWithInterpolation(
        MixerScene scene,
        IList<MixerChannel> currentChannels,
        MasterChannel? masterChannel,
        double durationMs = 500)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);

        if (IsInterpolating)
        {
            CancelInterpolation();
        }

        // Create snapshot of current state
        _interpolationSource = new MixerScene { Name = "_current" };
        foreach (var channel in currentChannels)
        {
            _interpolationSource.Channels.Add(CreateChannelSnapshot(channel));
        }
        if (masterChannel != null)
        {
            _interpolationSource.MasterChannel = CreateChannelSnapshot(masterChannel);
        }

        _interpolationTarget = scene;
        _interpolationDuration = durationMs;
        _interpolationStartTime = DateTime.UtcNow;
        InterpolationProgress = 0;
        IsInterpolating = true;

        _interpolationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _interpolationTimer.Tick += OnInterpolationTick;
        _interpolationTimer.Start();
    }

    /// <summary>
    /// Cancels an ongoing interpolation.
    /// </summary>
    public void CancelInterpolation()
    {
        if (_interpolationTimer != null)
        {
            _interpolationTimer.Stop();
            _interpolationTimer.Tick -= OnInterpolationTick;
            _interpolationTimer = null;
        }

        IsInterpolating = false;
        InterpolationProgress = 0;
        _interpolationSource = null;
        _interpolationTarget = null;
    }

    /// <summary>
    /// Deletes a scene.
    /// </summary>
    public bool DeleteScene(MixerScene scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        bool removed = _scenes.Remove(scene);
        if (removed)
        {
            OnPropertyChanged(nameof(SceneCount));
        }
        return removed;
    }

    /// <summary>
    /// Deletes a scene by ID.
    /// </summary>
    public bool DeleteScene(string sceneId)
    {
        var scene = _scenes.FirstOrDefault(s => s.Id == sceneId);
        return scene != null && DeleteScene(scene);
    }

    /// <summary>
    /// Gets a scene by ID.
    /// </summary>
    public MixerScene? GetScene(string sceneId)
    {
        return _scenes.FirstOrDefault(s => s.Id == sceneId);
    }

    /// <summary>
    /// Gets a scene by name.
    /// </summary>
    public MixerScene? GetSceneByName(string name)
    {
        return _scenes.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears all scenes.
    /// </summary>
    public void ClearScenes()
    {
        _scenes.Clear();
        SceneA = null;
        SceneB = null;
        OnPropertyChanged(nameof(SceneCount));
    }

    #endregion

    #region Public Methods - A/B Comparison

    /// <summary>
    /// Stores the current state as scene A for comparison.
    /// </summary>
    public void StoreAsA(IEnumerable<MixerChannel> channels, MasterChannel? master)
    {
        SceneA = new MixerScene { Name = "A" };
        foreach (var channel in channels)
        {
            SceneA.Channels.Add(CreateChannelSnapshot(channel));
        }
        if (master != null)
        {
            SceneA.MasterChannel = CreateChannelSnapshot(master);
        }
    }

    /// <summary>
    /// Stores the current state as scene B for comparison.
    /// </summary>
    public void StoreAsB(IEnumerable<MixerChannel> channels, MasterChannel? master)
    {
        SceneB = new MixerScene { Name = "B" };
        foreach (var channel in channels)
        {
            SceneB.Channels.Add(CreateChannelSnapshot(channel));
        }
        if (master != null)
        {
            SceneB.MasterChannel = CreateChannelSnapshot(master);
        }
    }

    /// <summary>
    /// Toggles between scene A and B.
    /// </summary>
    public void ToggleAB(IList<MixerChannel> channels, MasterChannel? master)
    {
        var targetScene = IsAActive ? SceneB : SceneA;
        if (targetScene != null)
        {
            RecallScene(targetScene, channels, master);
            IsAActive = !IsAActive;
        }
    }

    /// <summary>
    /// Copies scene A to scene B.
    /// </summary>
    public void CopyAToB()
    {
        if (SceneA != null)
        {
            SceneB = CloneScene(SceneA);
            SceneB.Name = "B";
        }
    }

    /// <summary>
    /// Copies scene B to scene A.
    /// </summary>
    public void CopyBToA()
    {
        if (SceneB != null)
        {
            SceneA = CloneScene(SceneB);
            SceneA.Name = "A";
        }
    }

    #endregion

    #region Public Methods - Serialization

    /// <summary>
    /// Exports all scenes to JSON.
    /// </summary>
    public string ExportToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        return JsonSerializer.Serialize(_scenes.ToList(), options);
    }

    /// <summary>
    /// Imports scenes from JSON.
    /// </summary>
    public void ImportFromJson(string json, bool append = false)
    {
        var scenes = JsonSerializer.Deserialize<List<MixerScene>>(json);
        if (scenes == null) return;

        if (!append)
        {
            _scenes.Clear();
        }

        foreach (var scene in scenes)
        {
            // Generate new IDs if appending to avoid duplicates
            if (append)
            {
                scene.Id = Guid.NewGuid().ToString();
            }
            _scenes.Add(scene);
        }

        OnPropertyChanged(nameof(SceneCount));
    }

    #endregion

    #region Private Methods

    private static ChannelSnapshot CreateChannelSnapshot(MixerChannel channel)
    {
        var snapshot = new ChannelSnapshot
        {
            Index = channel.Index,
            Name = channel.Name,
            Volume = channel.Volume,
            Pan = channel.Pan,
            IsMuted = channel.IsMuted,
            IsSoloed = channel.IsSoloed,
            OutputBusId = channel.OutputBusId,
            IsEffectChainBypassed = channel.IsEffectChainBypassed
        };

        foreach (var send in channel.Sends)
        {
            snapshot.SendLevels[send.TargetBusId] = send.Level;
        }

        return snapshot;
    }

    private static void ApplySnapshot(MixerChannel channel, ChannelSnapshot snapshot)
    {
        channel.Volume = snapshot.Volume;
        channel.Pan = snapshot.Pan;
        channel.IsMuted = snapshot.IsMuted;
        channel.IsSoloed = snapshot.IsSoloed;
        channel.IsEffectChainBypassed = snapshot.IsEffectChainBypassed;

        if (snapshot.OutputBusId != channel.OutputBusId)
        {
            channel.OutputBusId = snapshot.OutputBusId;
        }

        // Update send levels
        foreach (var send in channel.Sends)
        {
            if (snapshot.SendLevels.TryGetValue(send.TargetBusId, out float level))
            {
                send.Level = level;
            }
        }
    }

    private static MixerScene CloneScene(MixerScene source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<MixerScene>(json) ?? new MixerScene();
    }

    private void OnInterpolationTick(object? sender, EventArgs e)
    {
        if (_interpolationSource == null || _interpolationTarget == null)
        {
            CancelInterpolation();
            return;
        }

        var elapsed = (DateTime.UtcNow - _interpolationStartTime).TotalMilliseconds;
        var progress = Math.Min(1.0, elapsed / _interpolationDuration);

        // Apply easing (ease-out cubic)
        double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3);
        InterpolationProgress = easedProgress;

        // Interpolate each channel
        for (int i = 0; i < _interpolationSource.Channels.Count && i < _interpolationTarget.Channels.Count; i++)
        {
            var source = _interpolationSource.Channels[i];
            var target = _interpolationTarget.Channels[i];

            float volume = Lerp(source.Volume, target.Volume, (float)easedProgress);
            float pan = Lerp(source.Pan, target.Pan, (float)easedProgress);

            InterpolationUpdate?.Invoke(this, (source.Index, "Volume", volume));
            InterpolationUpdate?.Invoke(this, (source.Index, "Pan", pan));
        }

        if (progress >= 1.0)
        {
            CancelInterpolation();
            InterpolationCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        CancelInterpolation();
        _scenes.Clear();
        _disposed = true;
    }

    #endregion
}
