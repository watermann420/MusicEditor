// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service for managing spatial audio state and settings.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents the output format for spatial audio rendering.
/// </summary>
public enum SpatialOutputFormat
{
    /// <summary>Standard stereo output.</summary>
    Stereo,
    /// <summary>Binaural (headphone) rendering.</summary>
    Binaural,
    /// <summary>Quadraphonic (4.0) surround.</summary>
    Quad,
    /// <summary>5.1 surround sound.</summary>
    Surround51,
    /// <summary>7.1 surround sound.</summary>
    Surround71,
    /// <summary>First-order ambisonics.</summary>
    AmbisonicsFOA,
    /// <summary>Second-order ambisonics.</summary>
    AmbisonicsSOA,
    /// <summary>Third-order ambisonics.</summary>
    AmbisonicsTOA,
    /// <summary>Dolby Atmos.</summary>
    DolbyAtmos,
    /// <summary>Sony 360 Reality Audio.</summary>
    Sony360RA
}

/// <summary>
/// Represents a 3D position in spatial audio space.
/// </summary>
public struct SpatialPosition
{
    /// <summary>X coordinate (left/right, -1 to 1).</summary>
    public float X { get; set; }

    /// <summary>Y coordinate (front/back, -1 to 1).</summary>
    public float Y { get; set; }

    /// <summary>Z coordinate (up/down, -1 to 1).</summary>
    public float Z { get; set; }

    /// <summary>
    /// Creates a new spatial position.
    /// </summary>
    public SpatialPosition(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Returns a position at the center/origin.
    /// </summary>
    public static SpatialPosition Center => new(0, 0, 0);

    /// <summary>
    /// Returns a position at the front.
    /// </summary>
    public static SpatialPosition Front => new(0, 1, 0);

    /// <summary>
    /// Calculates the distance from the origin.
    /// </summary>
    public float Distance => MathF.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>
    /// Calculates the azimuth angle in degrees (-180 to 180).
    /// </summary>
    public float Azimuth => MathF.Atan2(X, Y) * (180f / MathF.PI);

    /// <summary>
    /// Calculates the elevation angle in degrees (-90 to 90).
    /// </summary>
    public float Elevation => MathF.Asin(Math.Clamp(Z / Math.Max(Distance, 0.0001f), -1f, 1f)) * (180f / MathF.PI);
}

/// <summary>
/// Represents a spatial audio source with position and properties.
/// </summary>
public class SpatialSource : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Unnamed Source";
    private SpatialPosition _position = SpatialPosition.Center;
    private float _spread = 0f;
    private float _distance = 1f;
    private float _gain = 1f;
    private bool _isMuted;
    private bool _isSolo;
    private string? _linkedTrackId;

    /// <summary>
    /// Unique identifier for this source.
    /// </summary>
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Display name for this source.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 3D position of the source.
    /// </summary>
    public SpatialPosition Position
    {
        get => _position;
        set { _position = value; OnPropertyChanged(); PositionChanged?.Invoke(this, value); }
    }

    /// <summary>
    /// Source spread/width (0 = point source, 1 = fully diffuse).
    /// </summary>
    public float Spread
    {
        get => _spread;
        set { _spread = Math.Clamp(value, 0f, 1f); OnPropertyChanged(); }
    }

    /// <summary>
    /// Distance factor for attenuation (1 = reference distance).
    /// </summary>
    public float Distance
    {
        get => _distance;
        set { _distance = Math.Max(0.01f, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// Gain/volume of this source (0 to 2, 1 = unity).
    /// </summary>
    public float Gain
    {
        get => _gain;
        set { _gain = Math.Clamp(value, 0f, 2f); OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether this source is muted.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set { _isMuted = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether this source is soloed.
    /// </summary>
    public bool IsSolo
    {
        get => _isSolo;
        set { _isSolo = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// ID of the track this source is linked to (if any).
    /// </summary>
    public string? LinkedTrackId
    {
        get => _linkedTrackId;
        set { _linkedTrackId = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Event raised when the position changes.
    /// </summary>
    public event EventHandler<SpatialPosition>? PositionChanged;

    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Global settings for spatial audio processing.
/// </summary>
public class SpatialGlobalSettings : INotifyPropertyChanged
{
    private float _roomSize = 0.3f;
    private float _reverbMix = 0.2f;
    private float _hrtfQuality = 1f;
    private bool _enableDoppler = true;
    private float _dopplerFactor = 1f;
    private bool _enableDistanceAttenuation = true;
    private float _referenceDistance = 1f;
    private float _maxDistance = 100f;
    private float _rolloffFactor = 1f;
    private bool _enableAirAbsorption = true;
    private float _speedOfSound = 343f;

    /// <summary>
    /// Room size for spatial reverb (0 to 1).
    /// </summary>
    public float RoomSize
    {
        get => _roomSize;
        set { _roomSize = Math.Clamp(value, 0f, 1f); OnPropertyChanged(); }
    }

    /// <summary>
    /// Reverb wet/dry mix (0 to 1).
    /// </summary>
    public float ReverbMix
    {
        get => _reverbMix;
        set { _reverbMix = Math.Clamp(value, 0f, 1f); OnPropertyChanged(); }
    }

    /// <summary>
    /// HRTF rendering quality (0 = fast, 1 = high quality).
    /// </summary>
    public float HrtfQuality
    {
        get => _hrtfQuality;
        set { _hrtfQuality = Math.Clamp(value, 0f, 1f); OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether Doppler effect is enabled.
    /// </summary>
    public bool EnableDoppler
    {
        get => _enableDoppler;
        set { _enableDoppler = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Doppler effect intensity factor.
    /// </summary>
    public float DopplerFactor
    {
        get => _dopplerFactor;
        set { _dopplerFactor = Math.Max(0f, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether distance-based attenuation is enabled.
    /// </summary>
    public bool EnableDistanceAttenuation
    {
        get => _enableDistanceAttenuation;
        set { _enableDistanceAttenuation = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Reference distance for attenuation calculations.
    /// </summary>
    public float ReferenceDistance
    {
        get => _referenceDistance;
        set { _referenceDistance = Math.Max(0.01f, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// Maximum distance for attenuation.
    /// </summary>
    public float MaxDistance
    {
        get => _maxDistance;
        set { _maxDistance = Math.Max(_referenceDistance, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// Rolloff factor for distance attenuation.
    /// </summary>
    public float RolloffFactor
    {
        get => _rolloffFactor;
        set { _rolloffFactor = Math.Max(0f, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether air absorption (high-frequency rolloff with distance) is enabled.
    /// </summary>
    public bool EnableAirAbsorption
    {
        get => _enableAirAbsorption;
        set { _enableAirAbsorption = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Speed of sound in meters per second (affects Doppler and delay).
    /// </summary>
    public float SpeedOfSound
    {
        get => _speedOfSound;
        set { _speedOfSound = Math.Max(1f, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Service for managing spatial audio state including output format, sources, listener position, and global settings.
/// </summary>
public class SpatialAudioService : INotifyPropertyChanged
{
    private SpatialOutputFormat _outputFormat = SpatialOutputFormat.Stereo;
    private SpatialPosition _listenerPosition = SpatialPosition.Center;
    private float _listenerRotation = 0f;
    private bool _isEnabled = true;

    /// <summary>
    /// Collection of spatial audio sources.
    /// </summary>
    public ObservableCollection<SpatialSource> Sources { get; } = new();

    /// <summary>
    /// Global settings for spatial audio processing.
    /// </summary>
    public SpatialGlobalSettings GlobalSettings { get; } = new();

    /// <summary>
    /// Gets or sets the current output format.
    /// </summary>
    public SpatialOutputFormat OutputFormat
    {
        get => _outputFormat;
        set
        {
            if (_outputFormat != value)
            {
                _outputFormat = value;
                OnPropertyChanged();
                OutputFormatChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the listener (camera/head) position.
    /// </summary>
    public SpatialPosition ListenerPosition
    {
        get => _listenerPosition;
        set
        {
            _listenerPosition = value;
            OnPropertyChanged();
            ListenerPositionChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Gets or sets the listener rotation in degrees (0 = facing front, positive = clockwise).
    /// </summary>
    public float ListenerRotation
    {
        get => _listenerRotation;
        set
        {
            // Normalize to -180 to 180
            _listenerRotation = ((value + 180f) % 360f) - 180f;
            OnPropertyChanged();
            ListenerRotationChanged?.Invoke(this, _listenerRotation);
        }
    }

    /// <summary>
    /// Gets or sets whether spatial audio processing is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
                EnabledChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Gets the number of output channels for the current format.
    /// </summary>
    public int OutputChannelCount => _outputFormat switch
    {
        SpatialOutputFormat.Stereo => 2,
        SpatialOutputFormat.Binaural => 2,
        SpatialOutputFormat.Quad => 4,
        SpatialOutputFormat.Surround51 => 6,
        SpatialOutputFormat.Surround71 => 8,
        SpatialOutputFormat.AmbisonicsFOA => 4,
        SpatialOutputFormat.AmbisonicsSOA => 9,
        SpatialOutputFormat.AmbisonicsTOA => 16,
        SpatialOutputFormat.DolbyAtmos => 10, // 7.1.2 base
        SpatialOutputFormat.Sony360RA => 2, // Binaural output
        _ => 2
    };

    /// <summary>
    /// Event raised when the output format changes.
    /// </summary>
    public event EventHandler<SpatialOutputFormat>? OutputFormatChanged;

    /// <summary>
    /// Event raised when the listener position changes.
    /// </summary>
    public event EventHandler<SpatialPosition>? ListenerPositionChanged;

    /// <summary>
    /// Event raised when the listener rotation changes.
    /// </summary>
    public event EventHandler<float>? ListenerRotationChanged;

    /// <summary>
    /// Event raised when spatial audio is enabled or disabled.
    /// </summary>
    public event EventHandler<bool>? EnabledChanged;

    /// <summary>
    /// Event raised when a source is added.
    /// </summary>
    public event EventHandler<SpatialSource>? SourceAdded;

    /// <summary>
    /// Event raised when a source is removed.
    /// </summary>
    public event EventHandler<SpatialSource>? SourceRemoved;

    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Creates a new SpatialAudioService.
    /// </summary>
    public SpatialAudioService()
    {
        Sources.CollectionChanged += Sources_CollectionChanged;
    }

    private void Sources_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (SpatialSource source in e.NewItems)
            {
                SourceAdded?.Invoke(this, source);
            }
        }

        if (e.OldItems != null)
        {
            foreach (SpatialSource source in e.OldItems)
            {
                SourceRemoved?.Invoke(this, source);
            }
        }
    }

    /// <summary>
    /// Adds a new spatial source with the given name.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <returns>The created source.</returns>
    public SpatialSource AddSource(string name)
    {
        var source = new SpatialSource { Name = name };
        Sources.Add(source);
        return source;
    }

    /// <summary>
    /// Adds a new spatial source linked to a track.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <param name="trackId">The ID of the linked track.</param>
    /// <returns>The created source.</returns>
    public SpatialSource AddSourceForTrack(string name, string trackId)
    {
        var source = new SpatialSource { Name = name, LinkedTrackId = trackId };
        Sources.Add(source);
        return source;
    }

    /// <summary>
    /// Removes a spatial source by ID.
    /// </summary>
    /// <param name="sourceId">The ID of the source to remove.</param>
    /// <returns>True if the source was removed.</returns>
    public bool RemoveSource(string sourceId)
    {
        var source = GetSourceById(sourceId);
        if (source != null)
        {
            Sources.Remove(source);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a spatial source by its ID.
    /// </summary>
    /// <param name="sourceId">The source ID.</param>
    /// <returns>The source, or null if not found.</returns>
    public SpatialSource? GetSourceById(string sourceId)
    {
        foreach (var source in Sources)
        {
            if (source.Id == sourceId)
                return source;
        }
        return null;
    }

    /// <summary>
    /// Gets a spatial source by its linked track ID.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <returns>The source, or null if not found.</returns>
    public SpatialSource? GetSourceByTrackId(string trackId)
    {
        foreach (var source in Sources)
        {
            if (source.LinkedTrackId == trackId)
                return source;
        }
        return null;
    }

    /// <summary>
    /// Resets the listener to the default position (center, facing forward).
    /// </summary>
    public void ResetListener()
    {
        ListenerPosition = SpatialPosition.Center;
        ListenerRotation = 0f;
    }

    /// <summary>
    /// Clears all spatial sources.
    /// </summary>
    public void ClearSources()
    {
        Sources.Clear();
    }

    /// <summary>
    /// Gets a friendly display name for the current output format.
    /// </summary>
    public string OutputFormatDisplayName => _outputFormat switch
    {
        SpatialOutputFormat.Stereo => "Stereo",
        SpatialOutputFormat.Binaural => "Binaural (Headphones)",
        SpatialOutputFormat.Quad => "Quadraphonic (4.0)",
        SpatialOutputFormat.Surround51 => "5.1 Surround",
        SpatialOutputFormat.Surround71 => "7.1 Surround",
        SpatialOutputFormat.AmbisonicsFOA => "Ambisonics (1st Order)",
        SpatialOutputFormat.AmbisonicsSOA => "Ambisonics (2nd Order)",
        SpatialOutputFormat.AmbisonicsTOA => "Ambisonics (3rd Order)",
        SpatialOutputFormat.DolbyAtmos => "Dolby Atmos",
        SpatialOutputFormat.Sony360RA => "Sony 360 Reality Audio",
        _ => "Unknown"
    };

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
