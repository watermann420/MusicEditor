// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents a marker on the timeline.
/// </summary>
public class TimelineMarker : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Marker";
    private double _position;
    private string _color = "#FFE66D";
    private MarkerType _type = MarkerType.Standard;

    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the marker name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the position in beats.
    /// </summary>
    public double Position
    {
        get => _position;
        set { _position = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the marker color.
    /// </summary>
    public string Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the marker type.
    /// </summary>
    public MarkerType Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Types of markers.
/// </summary>
public enum MarkerType
{
    Standard,
    LoopStart,
    LoopEnd,
    CuePoint,
    Section
}

/// <summary>
/// Represents a region created from markers.
/// </summary>
public class MarkerRegion : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Region";
    private double _startPosition;
    private double _endPosition;
    private string _color = "#304B6EAF";
    private string? _startMarkerId;
    private string? _endMarkerId;

    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the region name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the start position in beats.
    /// </summary>
    public double StartPosition
    {
        get => _startPosition;
        set { _startPosition = value; OnPropertyChanged(); OnPropertyChanged(nameof(Length)); }
    }

    /// <summary>
    /// Gets or sets the end position in beats.
    /// </summary>
    public double EndPosition
    {
        get => _endPosition;
        set { _endPosition = value; OnPropertyChanged(); OnPropertyChanged(nameof(Length)); }
    }

    /// <summary>
    /// Gets the region length in beats.
    /// </summary>
    public double Length => EndPosition - StartPosition;

    /// <summary>
    /// Gets or sets the region color.
    /// </summary>
    public string Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the ID of the marker at the start.
    /// </summary>
    public string? StartMarkerId
    {
        get => _startMarkerId;
        set { _startMarkerId = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the ID of the marker at the end.
    /// </summary>
    public string? EndMarkerId
    {
        get => _endMarkerId;
        set { _endMarkerId = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Export settings for a region.
/// </summary>
public class RegionExportSettings
{
    public string OutputDirectory { get; set; } = string.Empty;
    public string FileFormat { get; set; } = "wav";
    public string NamingPattern { get; set; } = "{region}_{index}";
    public bool IncludeEffects { get; set; } = true;
    public bool NormalizeAudio { get; set; }
    public double FadeDuration { get; set; }
    public int SampleRate { get; set; } = 44100;
    public int BitDepth { get; set; } = 24;
}

/// <summary>
/// Service for creating and managing regions from markers with export capabilities.
/// </summary>
public class MarkerRangeService : IMarkerRangeService
{
    #region Fields

    private readonly Dictionary<string, TimelineMarker> _markers = new();
    private readonly Dictionary<string, MarkerRegion> _regions = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] RegionColors = new[]
    {
        "#304B6EAF", "#30FF6B6B", "#304ECDC4", "#30FFE66D",
        "#30A78BFA", "#30FF8C42", "#306BCB77", "#30FF6B9D"
    };

    #endregion

    #region Events

    public event EventHandler<MarkerEventArgs>? MarkerAdded;
    public event EventHandler<MarkerEventArgs>? MarkerRemoved;
    public event EventHandler<MarkerEventArgs>? MarkerMoved;
    public event EventHandler<RegionEventArgs>? RegionCreated;
    public event EventHandler<RegionEventArgs>? RegionDeleted;
    public event EventHandler<RegionsCreatedEventArgs>? RegionsAutoCreated;
    public event EventHandler<RegionExportEventArgs>? RegionExported;
    public event EventHandler<BatchExportEventArgs>? BatchExportCompleted;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets all markers.
    /// </summary>
    public ObservableCollection<TimelineMarker> Markers { get; } = new();

    /// <summary>
    /// Gets all regions.
    /// </summary>
    public ObservableCollection<MarkerRegion> Regions { get; } = new();

    #endregion

    #region Marker Management

    /// <summary>
    /// Adds a marker at the specified position.
    /// </summary>
    public TimelineMarker AddMarker(string name, double position, MarkerType type = MarkerType.Standard, string? color = null)
    {
        var marker = new TimelineMarker
        {
            Name = name,
            Position = position,
            Type = type,
            Color = color ?? "#FFE66D"
        };

        _markers[marker.Id] = marker;
        RefreshMarkersCollection();
        MarkerAdded?.Invoke(this, new MarkerEventArgs(marker));

        return marker;
    }

    /// <summary>
    /// Removes a marker.
    /// </summary>
    public bool RemoveMarker(string markerId)
    {
        if (!_markers.TryGetValue(markerId, out var marker))
            return false;

        _markers.Remove(markerId);
        RefreshMarkersCollection();
        MarkerRemoved?.Invoke(this, new MarkerEventArgs(marker));

        // Update any regions that reference this marker
        foreach (var region in _regions.Values)
        {
            if (region.StartMarkerId == markerId)
                region.StartMarkerId = null;
            if (region.EndMarkerId == markerId)
                region.EndMarkerId = null;
        }

        return true;
    }

    /// <summary>
    /// Moves a marker to a new position.
    /// </summary>
    public bool MoveMarker(string markerId, double newPosition)
    {
        if (!_markers.TryGetValue(markerId, out var marker))
            return false;

        marker.Position = newPosition;
        RefreshMarkersCollection();
        MarkerMoved?.Invoke(this, new MarkerEventArgs(marker));

        // Update linked regions
        UpdateLinkedRegions(markerId, newPosition);

        return true;
    }

    /// <summary>
    /// Renames a marker.
    /// </summary>
    public bool RenameMarker(string markerId, string newName)
    {
        if (!_markers.TryGetValue(markerId, out var marker))
            return false;

        marker.Name = newName;
        return true;
    }

    /// <summary>
    /// Gets a marker by ID.
    /// </summary>
    public TimelineMarker? GetMarker(string markerId)
    {
        return _markers.TryGetValue(markerId, out var marker) ? marker : null;
    }

    /// <summary>
    /// Gets markers in position order.
    /// </summary>
    public IReadOnlyList<TimelineMarker> GetMarkersInOrder()
    {
        return _markers.Values.OrderBy(m => m.Position).ToList().AsReadOnly();
    }

    #endregion

    #region Region Management

    /// <summary>
    /// Creates a region between two markers.
    /// </summary>
    public MarkerRegion? CreateRegionFromMarkers(string startMarkerId, string endMarkerId, string? name = null)
    {
        if (!_markers.TryGetValue(startMarkerId, out var startMarker) ||
            !_markers.TryGetValue(endMarkerId, out var endMarker))
            return null;

        var colorIndex = _regions.Count % RegionColors.Length;

        var region = new MarkerRegion
        {
            Name = name ?? $"{startMarker.Name} - {endMarker.Name}",
            StartPosition = Math.Min(startMarker.Position, endMarker.Position),
            EndPosition = Math.Max(startMarker.Position, endMarker.Position),
            StartMarkerId = startMarkerId,
            EndMarkerId = endMarkerId,
            Color = RegionColors[colorIndex]
        };

        _regions[region.Id] = region;
        RefreshRegionsCollection();
        RegionCreated?.Invoke(this, new RegionEventArgs(region));

        return region;
    }

    /// <summary>
    /// Creates a region at the specified position.
    /// </summary>
    public MarkerRegion CreateRegion(string name, double startPosition, double endPosition, string? color = null)
    {
        var colorIndex = _regions.Count % RegionColors.Length;

        var region = new MarkerRegion
        {
            Name = name,
            StartPosition = Math.Min(startPosition, endPosition),
            EndPosition = Math.Max(startPosition, endPosition),
            Color = color ?? RegionColors[colorIndex]
        };

        _regions[region.Id] = region;
        RefreshRegionsCollection();
        RegionCreated?.Invoke(this, new RegionEventArgs(region));

        return region;
    }

    /// <summary>
    /// Deletes a region.
    /// </summary>
    public bool DeleteRegion(string regionId)
    {
        if (!_regions.TryGetValue(regionId, out var region))
            return false;

        _regions.Remove(regionId);
        RefreshRegionsCollection();
        RegionDeleted?.Invoke(this, new RegionEventArgs(region));

        return true;
    }

    /// <summary>
    /// Gets a region by ID.
    /// </summary>
    public MarkerRegion? GetRegion(string regionId)
    {
        return _regions.TryGetValue(regionId, out var region) ? region : null;
    }

    #endregion

    #region Auto-Create Regions

    /// <summary>
    /// Auto-creates regions between consecutive markers.
    /// </summary>
    public IReadOnlyList<MarkerRegion> AutoCreateRegionsBetweenMarkers(bool useMarkerNames = true)
    {
        var sortedMarkers = GetMarkersInOrder();
        if (sortedMarkers.Count < 2)
            return Array.Empty<MarkerRegion>();

        var createdRegions = new List<MarkerRegion>();

        for (int i = 0; i < sortedMarkers.Count - 1; i++)
        {
            var startMarker = sortedMarkers[i];
            var endMarker = sortedMarkers[i + 1];

            var name = useMarkerNames
                ? startMarker.Name
                : $"Region {i + 1}";

            var region = CreateRegionFromMarkers(startMarker.Id, endMarker.Id, name);
            if (region != null)
            {
                createdRegions.Add(region);
            }
        }

        RegionsAutoCreated?.Invoke(this, new RegionsCreatedEventArgs(createdRegions));

        return createdRegions.AsReadOnly();
    }

    /// <summary>
    /// Auto-creates regions between section markers only.
    /// </summary>
    public IReadOnlyList<MarkerRegion> AutoCreateRegionsFromSectionMarkers()
    {
        var sectionMarkers = _markers.Values
            .Where(m => m.Type == MarkerType.Section)
            .OrderBy(m => m.Position)
            .ToList();

        if (sectionMarkers.Count < 2)
            return Array.Empty<MarkerRegion>();

        var createdRegions = new List<MarkerRegion>();

        for (int i = 0; i < sectionMarkers.Count - 1; i++)
        {
            var region = CreateRegionFromMarkers(sectionMarkers[i].Id, sectionMarkers[i + 1].Id, sectionMarkers[i].Name);
            if (region != null)
            {
                createdRegions.Add(region);
            }
        }

        RegionsAutoCreated?.Invoke(this, new RegionsCreatedEventArgs(createdRegions));

        return createdRegions.AsReadOnly();
    }

    /// <summary>
    /// Clears all auto-created regions.
    /// </summary>
    public void ClearAllRegions()
    {
        _regions.Clear();
        Regions.Clear();
    }

    #endregion

    #region Region Export

    /// <summary>
    /// Exports a single region to a file.
    /// </summary>
    public async Task<string?> ExportRegionAsync(string regionId, RegionExportSettings settings, Func<double, double, Task<byte[]>>? audioRenderer = null)
    {
        if (!_regions.TryGetValue(regionId, out var region))
            return null;

        var fileName = GenerateFileName(region, 0, settings.NamingPattern, settings.FileFormat);
        var filePath = Path.Combine(settings.OutputDirectory, fileName);

        // Ensure directory exists
        Directory.CreateDirectory(settings.OutputDirectory);

        // The actual audio rendering would be done by the provided callback
        if (audioRenderer != null)
        {
            var audioData = await audioRenderer(region.StartPosition, region.EndPosition);
            await File.WriteAllBytesAsync(filePath, audioData);
        }
        else
        {
            // Create placeholder file
            await File.WriteAllTextAsync(filePath + ".info", $"Region: {region.Name}\nStart: {region.StartPosition}\nEnd: {region.EndPosition}");
        }

        RegionExported?.Invoke(this, new RegionExportEventArgs(region, filePath));

        return filePath;
    }

    /// <summary>
    /// Exports all regions as separate files.
    /// </summary>
    public async Task<IReadOnlyList<string>> ExportAllRegionsAsync(RegionExportSettings settings, Func<double, double, Task<byte[]>>? audioRenderer = null, IProgress<double>? progress = null)
    {
        var exportedPaths = new List<string>();
        var sortedRegions = _regions.Values.OrderBy(r => r.StartPosition).ToList();
        var total = sortedRegions.Count;

        Directory.CreateDirectory(settings.OutputDirectory);

        for (int i = 0; i < sortedRegions.Count; i++)
        {
            var region = sortedRegions[i];
            var fileName = GenerateFileName(region, i + 1, settings.NamingPattern, settings.FileFormat);
            var filePath = Path.Combine(settings.OutputDirectory, fileName);

            if (audioRenderer != null)
            {
                var audioData = await audioRenderer(region.StartPosition, region.EndPosition);
                await File.WriteAllBytesAsync(filePath, audioData);
            }
            else
            {
                await File.WriteAllTextAsync(filePath + ".info", $"Region: {region.Name}\nStart: {region.StartPosition}\nEnd: {region.EndPosition}");
            }

            exportedPaths.Add(filePath);
            progress?.Report((double)(i + 1) / total);
        }

        BatchExportCompleted?.Invoke(this, new BatchExportEventArgs(exportedPaths, settings));

        return exportedPaths.AsReadOnly();
    }

    /// <summary>
    /// Exports selected regions as separate files.
    /// </summary>
    public async Task<IReadOnlyList<string>> ExportRegionsAsync(IEnumerable<string> regionIds, RegionExportSettings settings, Func<double, double, Task<byte[]>>? audioRenderer = null, IProgress<double>? progress = null)
    {
        var exportedPaths = new List<string>();
        var regions = regionIds.Select(id => _regions.GetValueOrDefault(id)).Where(r => r != null).ToList();
        var total = regions.Count;

        Directory.CreateDirectory(settings.OutputDirectory);

        for (int i = 0; i < regions.Count; i++)
        {
            var region = regions[i]!;
            var fileName = GenerateFileName(region, i + 1, settings.NamingPattern, settings.FileFormat);
            var filePath = Path.Combine(settings.OutputDirectory, fileName);

            if (audioRenderer != null)
            {
                var audioData = await audioRenderer(region.StartPosition, region.EndPosition);
                await File.WriteAllBytesAsync(filePath, audioData);
            }
            else
            {
                await File.WriteAllTextAsync(filePath + ".info", $"Region: {region.Name}\nStart: {region.StartPosition}\nEnd: {region.EndPosition}");
            }

            exportedPaths.Add(filePath);
            progress?.Report((double)(i + 1) / total);
        }

        BatchExportCompleted?.Invoke(this, new BatchExportEventArgs(exportedPaths, settings));

        return exportedPaths.AsReadOnly();
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Saves markers and regions to a file.
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var data = new MarkerRangePersistenceData
        {
            Markers = _markers.Values.ToList(),
            Regions = _regions.Values.ToList()
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads markers and regions from a file.
    /// </summary>
    public async Task LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<MarkerRangePersistenceData>(json, JsonOptions);

        if (data != null)
        {
            _markers.Clear();
            _regions.Clear();

            foreach (var marker in data.Markers)
            {
                _markers[marker.Id] = marker;
            }

            foreach (var region in data.Regions)
            {
                _regions[region.Id] = region;
            }

            RefreshMarkersCollection();
            RefreshRegionsCollection();
        }
    }

    /// <summary>
    /// Clears all data.
    /// </summary>
    public void Clear()
    {
        _markers.Clear();
        _regions.Clear();
        Markers.Clear();
        Regions.Clear();
    }

    #endregion

    #region Private Methods

    private void RefreshMarkersCollection()
    {
        Markers.Clear();
        foreach (var marker in _markers.Values.OrderBy(m => m.Position))
        {
            Markers.Add(marker);
        }
    }

    private void RefreshRegionsCollection()
    {
        Regions.Clear();
        foreach (var region in _regions.Values.OrderBy(r => r.StartPosition))
        {
            Regions.Add(region);
        }
    }

    private void UpdateLinkedRegions(string markerId, double newPosition)
    {
        foreach (var region in _regions.Values)
        {
            if (region.StartMarkerId == markerId)
            {
                region.StartPosition = newPosition;
            }
            if (region.EndMarkerId == markerId)
            {
                region.EndPosition = newPosition;
            }
        }
    }

    private static string GenerateFileName(MarkerRegion region, int index, string pattern, string format)
    {
        var safeName = string.Join("_", region.Name.Split(Path.GetInvalidFileNameChars()));
        var fileName = pattern
            .Replace("{region}", safeName)
            .Replace("{name}", safeName)
            .Replace("{index}", index.ToString("D3"))
            .Replace("{start}", region.StartPosition.ToString("F2"))
            .Replace("{end}", region.EndPosition.ToString("F2"));

        return $"{fileName}.{format}";
    }

    #endregion
}

#region Interfaces

/// <summary>
/// Interface for marker range service.
/// </summary>
public interface IMarkerRangeService
{
    event EventHandler<MarkerEventArgs>? MarkerAdded;
    event EventHandler<MarkerEventArgs>? MarkerRemoved;
    event EventHandler<MarkerEventArgs>? MarkerMoved;
    event EventHandler<RegionEventArgs>? RegionCreated;
    event EventHandler<RegionEventArgs>? RegionDeleted;
    event EventHandler<RegionsCreatedEventArgs>? RegionsAutoCreated;
    event EventHandler<RegionExportEventArgs>? RegionExported;
    event EventHandler<BatchExportEventArgs>? BatchExportCompleted;

    ObservableCollection<TimelineMarker> Markers { get; }
    ObservableCollection<MarkerRegion> Regions { get; }

    TimelineMarker AddMarker(string name, double position, MarkerType type = MarkerType.Standard, string? color = null);
    bool RemoveMarker(string markerId);
    bool MoveMarker(string markerId, double newPosition);
    bool RenameMarker(string markerId, string newName);
    TimelineMarker? GetMarker(string markerId);
    IReadOnlyList<TimelineMarker> GetMarkersInOrder();

    MarkerRegion? CreateRegionFromMarkers(string startMarkerId, string endMarkerId, string? name = null);
    MarkerRegion CreateRegion(string name, double startPosition, double endPosition, string? color = null);
    bool DeleteRegion(string regionId);
    MarkerRegion? GetRegion(string regionId);

    IReadOnlyList<MarkerRegion> AutoCreateRegionsBetweenMarkers(bool useMarkerNames = true);
    IReadOnlyList<MarkerRegion> AutoCreateRegionsFromSectionMarkers();
    void ClearAllRegions();

    Task<string?> ExportRegionAsync(string regionId, RegionExportSettings settings, Func<double, double, Task<byte[]>>? audioRenderer = null);
    Task<IReadOnlyList<string>> ExportAllRegionsAsync(RegionExportSettings settings, Func<double, double, Task<byte[]>>? audioRenderer = null, IProgress<double>? progress = null);
    Task<IReadOnlyList<string>> ExportRegionsAsync(IEnumerable<string> regionIds, RegionExportSettings settings, Func<double, double, Task<byte[]>>? audioRenderer = null, IProgress<double>? progress = null);

    Task SaveToFileAsync(string filePath);
    Task LoadFromFileAsync(string filePath);
    void Clear();
}

#endregion

#region Event Args

public class MarkerEventArgs : EventArgs
{
    public TimelineMarker Marker { get; }

    public MarkerEventArgs(TimelineMarker marker)
    {
        Marker = marker;
    }
}

public class RegionEventArgs : EventArgs
{
    public MarkerRegion Region { get; }

    public RegionEventArgs(MarkerRegion region)
    {
        Region = region;
    }
}

public class RegionsCreatedEventArgs : EventArgs
{
    public IReadOnlyList<MarkerRegion> Regions { get; }

    public RegionsCreatedEventArgs(IReadOnlyList<MarkerRegion> regions)
    {
        Regions = regions;
    }
}

public class RegionExportEventArgs : EventArgs
{
    public MarkerRegion Region { get; }
    public string FilePath { get; }

    public RegionExportEventArgs(MarkerRegion region, string filePath)
    {
        Region = region;
        FilePath = filePath;
    }
}

public class BatchExportEventArgs : EventArgs
{
    public IReadOnlyList<string> ExportedPaths { get; }
    public RegionExportSettings Settings { get; }

    public BatchExportEventArgs(IReadOnlyList<string> exportedPaths, RegionExportSettings settings)
    {
        ExportedPaths = exportedPaths;
        Settings = settings;
    }
}

#endregion

#region Persistence Data

internal class MarkerRangePersistenceData
{
    public List<TimelineMarker> Markers { get; set; } = new();
    public List<MarkerRegion> Regions { get; set; } = new();
}

#endregion
