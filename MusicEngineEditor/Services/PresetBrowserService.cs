// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service for browsing, indexing, and managing presets.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngineEditor.Models;
using NAudio.Wave;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for browsing, indexing, and managing presets across synths, effects, patterns, and grooves.
/// </summary>
public class PresetBrowserService : IDisposable
{
    private readonly PresetManager _presetManager;
    private readonly List<PresetInfo> _allPresets = new();
    private readonly List<PresetInfo> _favoritePresets = new();
    private readonly Dictionary<string, List<PresetInfo>> _presetIndex = new();
    private readonly object _previewLock = new();

    private WaveOutEvent? _waveOut;
    private WaveStream? _audioStream;
    private bool _disposed;
    private bool _isPreviewPlaying;

    /// <summary>
    /// Event raised when the preset collection changes.
    /// </summary>
    public event EventHandler? PresetsChanged;

    /// <summary>
    /// Event raised when preview playback state changes.
    /// </summary>
    public event EventHandler<bool>? PreviewStateChanged;

    /// <summary>
    /// Event raised when a preset is loaded.
    /// </summary>
    public event EventHandler<PresetInfo>? PresetLoaded;

    /// <summary>
    /// Gets whether a preview is currently playing.
    /// </summary>
    public bool IsPreviewPlaying => _isPreviewPlaying;

    /// <summary>
    /// Gets the currently previewing preset.
    /// </summary>
    public PresetInfo? CurrentPreviewPreset { get; private set; }

    /// <summary>
    /// Gets the preset manager instance.
    /// </summary>
    public PresetManager PresetManager => _presetManager;

    /// <summary>
    /// Creates a new PresetBrowserService instance.
    /// </summary>
    public PresetBrowserService()
    {
        _presetManager = new PresetManager();
        _presetManager.BanksChanged += OnBanksChanged;
        LoadFavorites();
        ScanDefaultDirectories();
    }

    /// <summary>
    /// Creates a new PresetBrowserService with a custom PresetManager.
    /// </summary>
    public PresetBrowserService(PresetManager presetManager)
    {
        _presetManager = presetManager;
        _presetManager.BanksChanged += OnBanksChanged;
        LoadFavorites();
    }

    #region Scanning and Indexing

    /// <summary>
    /// Scans the default preset directories.
    /// </summary>
    public void ScanDefaultDirectories()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var presetsPath = Path.Combine(appDataPath, "MusicEngine", "Presets");

        if (Directory.Exists(presetsPath))
        {
            _presetManager.ScanPresets(presetsPath);
        }

        // Also scan the executable directory for factory presets
        var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(exePath))
        {
            var factoryPresetsPath = Path.Combine(exePath, "Presets");
            if (Directory.Exists(factoryPresetsPath))
            {
                _presetManager.ScanPresets(factoryPresetsPath);
            }
        }

        RebuildIndex();
    }

    /// <summary>
    /// Scans a specific directory for presets.
    /// </summary>
    public async Task ScanDirectoryAsync(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return;

        await Task.Run(() => _presetManager.ScanPresets(directoryPath));
        RebuildIndex();
    }

    private void OnBanksChanged(object? sender, EventArgs e)
    {
        RebuildIndex();
    }

    private void RebuildIndex()
    {
        _allPresets.Clear();
        _presetIndex.Clear();

        foreach (var bank in _presetManager.Banks)
        {
            foreach (var preset in bank.Presets)
            {
                var presetInfo = PresetInfo.FromPreset(preset, bank.Name, bank.Id);
                presetInfo.IsFavorite = _favoritePresets.Any(f => f.Id == presetInfo.Id);
                _allPresets.Add(presetInfo);

                // Index by name words
                IndexPreset(presetInfo);
            }
        }

        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void IndexPreset(PresetInfo preset)
    {
        // Index by name words
        var nameWords = preset.Name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in nameWords)
        {
            AddToIndex(word, preset);
        }

        // Index by category
        if (!string.IsNullOrWhiteSpace(preset.Category))
        {
            AddToIndex(preset.Category.ToLowerInvariant(), preset);
        }

        // Index by tags
        foreach (var tag in preset.Tags)
        {
            AddToIndex(tag.ToLowerInvariant(), preset);
        }

        // Index by author
        if (!string.IsNullOrWhiteSpace(preset.Author))
        {
            AddToIndex(preset.Author.ToLowerInvariant(), preset);
        }

        // Index by target type
        AddToIndex(preset.TargetType.ToString().ToLowerInvariant(), preset);
    }

    private void AddToIndex(string key, PresetInfo preset)
    {
        if (!_presetIndex.TryGetValue(key, out var list))
        {
            list = new List<PresetInfo>();
            _presetIndex[key] = list;
        }
        if (!list.Contains(preset))
        {
            list.Add(preset);
        }
    }

    #endregion

    #region Search

    /// <summary>
    /// Searches presets using fuzzy matching.
    /// </summary>
    public IReadOnlyList<PresetInfo> Search(string? query, PresetFilterOptions? options = null)
    {
        options ??= new PresetFilterOptions();
        var results = _allPresets.AsEnumerable();

        // Apply search query
        if (!string.IsNullOrWhiteSpace(query))
        {
            results = FuzzySearch(query, results);
        }

        // Apply filters
        results = ApplyFilters(results, options);

        // Apply sorting
        results = ApplySorting(results, options);

        return results.ToList().AsReadOnly();
    }

    private IEnumerable<PresetInfo> FuzzySearch(string query, IEnumerable<PresetInfo> presets)
    {
        var queryLower = query.ToLowerInvariant();
        var terms = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return presets.Select(preset =>
        {
            var score = CalculateFuzzyScore(preset, terms);
            return (preset, score);
        })
        .Where(x => x.score > 0)
        .OrderByDescending(x => x.score)
        .Select(x => x.preset);
    }

    private int CalculateFuzzyScore(PresetInfo preset, string[] terms)
    {
        var score = 0;
        var nameLower = preset.Name.ToLowerInvariant();
        var categoryLower = preset.Category.ToLowerInvariant();
        var authorLower = preset.Author.ToLowerInvariant();

        foreach (var term in terms)
        {
            var termMatched = false;

            // Exact name match (highest score)
            if (nameLower == term)
            {
                score += 100;
                termMatched = true;
            }
            // Name starts with term
            else if (nameLower.StartsWith(term))
            {
                score += 50;
                termMatched = true;
            }
            // Name contains term
            else if (nameLower.Contains(term))
            {
                score += 30;
                termMatched = true;
            }

            // Category match
            if (categoryLower.Contains(term))
            {
                score += 20;
                termMatched = true;
            }

            // Tag match
            if (preset.Tags.Any(t => t.ToLowerInvariant().Contains(term)))
            {
                score += 25;
                termMatched = true;
            }

            // Author match
            if (authorLower.Contains(term))
            {
                score += 15;
                termMatched = true;
            }

            // If term didn't match anything, reduce overall score
            if (!termMatched)
            {
                score -= 50;
            }
        }

        return score;
    }

    private IEnumerable<PresetInfo> ApplyFilters(IEnumerable<PresetInfo> presets, PresetFilterOptions options)
    {
        // Filter by target type
        if (options.TargetType.HasValue)
        {
            presets = presets.Where(p => p.TargetType == options.TargetType.Value);
        }

        // Filter by category
        if (!string.IsNullOrWhiteSpace(options.Category) && options.Category != "All")
        {
            presets = presets.Where(p => p.Category.Equals(options.Category, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by tags
        if (options.Tags?.Count > 0)
        {
            presets = presets.Where(p => options.Tags.All(t =>
                p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        // Filter by favorites only
        if (options.FavoritesOnly)
        {
            presets = presets.Where(p => p.IsFavorite);
        }

        // Filter by bank
        if (!string.IsNullOrWhiteSpace(options.BankId))
        {
            presets = presets.Where(p => p.BankId == options.BankId);
        }

        return presets;
    }

    private IEnumerable<PresetInfo> ApplySorting(IEnumerable<PresetInfo> presets, PresetFilterOptions options)
    {
        return options.SortBy switch
        {
            PresetSortBy.Name => options.SortAscending
                ? presets.OrderBy(p => p.Name)
                : presets.OrderByDescending(p => p.Name),
            PresetSortBy.Category => options.SortAscending
                ? presets.OrderBy(p => p.Category)
                : presets.OrderByDescending(p => p.Category),
            PresetSortBy.Author => options.SortAscending
                ? presets.OrderBy(p => p.Author)
                : presets.OrderByDescending(p => p.Author),
            PresetSortBy.Date => options.SortAscending
                ? presets.OrderBy(p => p.ModifiedDate)
                : presets.OrderByDescending(p => p.ModifiedDate),
            PresetSortBy.Rating => options.SortAscending
                ? presets.OrderBy(p => p.Rating)
                : presets.OrderByDescending(p => p.Rating),
            _ => presets.OrderBy(p => p.Name)
        };
    }

    #endregion

    #region Favorites

    /// <summary>
    /// Gets the favorite presets.
    /// </summary>
    public IReadOnlyList<PresetInfo> GetFavorites()
    {
        return _allPresets.Where(p => p.IsFavorite).ToList().AsReadOnly();
    }

    /// <summary>
    /// Toggles the favorite status of a preset.
    /// </summary>
    public void ToggleFavorite(PresetInfo preset)
    {
        preset.IsFavorite = !preset.IsFavorite;
        preset.UpdateFavoriteStatus();

        // Update in-memory list
        if (preset.IsFavorite)
        {
            if (!_favoritePresets.Any(f => f.Id == preset.Id))
            {
                _favoritePresets.Add(preset);
            }
        }
        else
        {
            _favoritePresets.RemoveAll(f => f.Id == preset.Id);
        }

        SaveFavorites();

        // Save to preset file
        if (preset.SourcePreset != null)
        {
            var bank = _presetManager.GetBankById(preset.BankId);
            if (bank != null)
            {
                _presetManager.SavePreset(preset.SourcePreset, bank);
            }
        }
    }

    private void LoadFavorites()
    {
        try
        {
            var favoritesPath = GetFavoritesFilePath();
            if (File.Exists(favoritesPath))
            {
                var json = File.ReadAllText(favoritesPath);
                var favoriteIds = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

                // We'll sync favorites when presets are loaded
                _favoritePresets.Clear();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load favorites: {ex.Message}");
        }
    }

    private void SaveFavorites()
    {
        try
        {
            var favoritesPath = GetFavoritesFilePath();
            var directory = Path.GetDirectoryName(favoritesPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var favoriteIds = _favoritePresets.Select(f => f.Id).ToList();
            var json = JsonSerializer.Serialize(favoriteIds, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(favoritesPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save favorites: {ex.Message}");
        }
    }

    private string GetFavoritesFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "MusicEngine", "PresetBrowser", "favorites.json");
    }

    #endregion

    #region Preview Playback

    /// <summary>
    /// Plays a preview of the selected preset using a test note.
    /// </summary>
    public async Task PreviewPresetAsync(PresetInfo preset)
    {
        if (preset == null)
            return;

        StopPreview();

        CurrentPreviewPreset = preset;
        _isPreviewPlaying = true;
        PreviewStateChanged?.Invoke(this, true);

        try
        {
            // For synth presets, we would trigger a test note through the engine
            // For effect presets, we would apply to a test signal
            // For now, we'll simulate preview with a short delay

            // TODO: Integrate with MusicEngine to actually preview the preset
            // This would involve:
            // 1. Loading the preset into a temporary synth/effect instance
            // 2. Playing a test note (e.g., C4 for 1 second)
            // 3. Cleaning up after preview

            await Task.Delay(2000); // Simulated preview duration

            if (_isPreviewPlaying && CurrentPreviewPreset?.Id == preset.Id)
            {
                StopPreview();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preview failed: {ex.Message}");
            StopPreview();
        }
    }

    /// <summary>
    /// Stops the current preview playback.
    /// </summary>
    public void StopPreview()
    {
        lock (_previewLock)
        {
            try
            {
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }

                if (_audioStream != null)
                {
                    _audioStream.Dispose();
                    _audioStream = null;
                }

                _isPreviewPlaying = false;
                CurrentPreviewPreset = null;
                PreviewStateChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping preview: {ex.Message}");
            }
        }
    }

    #endregion

    #region Preset Loading

    /// <summary>
    /// Loads a preset into the appropriate target.
    /// </summary>
    public void LoadPreset(PresetInfo preset)
    {
        if (preset?.SourcePreset == null)
            return;

        // Notify listeners that a preset should be loaded
        PresetLoaded?.Invoke(this, preset);
    }

    #endregion

    #region Categories and Tags

    /// <summary>
    /// Gets all available categories.
    /// </summary>
    public IReadOnlyList<string> GetCategories()
    {
        var categories = _allPresets
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        categories.Insert(0, "All");
        return categories.AsReadOnly();
    }

    /// <summary>
    /// Gets tag statistics (tag name and usage count).
    /// </summary>
    public IReadOnlyDictionary<string, int> GetTagStatistics()
    {
        return _allPresets
            .SelectMany(p => p.Tags)
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Gets the hierarchical tree structure of presets.
    /// </summary>
    public PresetCategory GetPresetTree()
    {
        return PresetCategory.CreateFromPresets(_allPresets);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes of resources used by the service.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            StopPreview();
            _presetManager.BanksChanged -= OnBanksChanged;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Options for filtering presets.
/// </summary>
public class PresetFilterOptions
{
    /// <summary>
    /// Gets or sets the target type filter.
    /// </summary>
    public PresetTargetType? TargetType { get; set; }

    /// <summary>
    /// Gets or sets the category filter.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the tags to filter by.
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets whether to show only favorites.
    /// </summary>
    public bool FavoritesOnly { get; set; }

    /// <summary>
    /// Gets or sets the bank ID to filter by.
    /// </summary>
    public string? BankId { get; set; }

    /// <summary>
    /// Gets or sets the sort field.
    /// </summary>
    public PresetSortBy SortBy { get; set; } = PresetSortBy.Name;

    /// <summary>
    /// Gets or sets whether to sort in ascending order.
    /// </summary>
    public bool SortAscending { get; set; } = true;
}

/// <summary>
/// Preset sort options.
/// </summary>
public enum PresetSortBy
{
    /// <summary>Sort by name.</summary>
    Name,
    /// <summary>Sort by category.</summary>
    Category,
    /// <summary>Sort by author.</summary>
    Author,
    /// <summary>Sort by modification date.</summary>
    Date,
    /// <summary>Sort by rating.</summary>
    Rating
}
