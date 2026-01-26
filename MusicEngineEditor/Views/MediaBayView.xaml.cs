using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.Services;
using NAudio.Wave;
using Path = System.IO.Path;

namespace MusicEngineEditor.Views;

/// <summary>
/// Advanced sample browser with tag-based filtering, star ratings, and color labels.
/// </summary>
public partial class MediaBayView : Window
{
    private readonly ObservableCollection<MediaBayItem> _allItems = new();
    private readonly ObservableCollection<MediaBayItem> _filteredItems = new();
    private readonly ObservableCollection<MediaBayTag> _allTags = new();
    private readonly List<FolderNode> _folderNodes = new();
    private readonly HashSet<string> _selectedTags = new();
    private readonly HashSet<string> _selectedColors = new();
    private readonly HashSet<string> _selectedFormats = new();
    private readonly WaveformService _waveformService;

    private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    private string _searchText = string.Empty;
    private int _minRating = 0;
    private MediaBayItem? _selectedItem;
    private WaveOutEvent? _previewPlayer;
    private AudioFileReader? _previewReader;
    private CancellationTokenSource? _waveformCts;

    public event EventHandler<string>? FileImportRequested;

    public MediaBayView()
    {
        InitializeComponent();

        _waveformService = new WaveformService();
        FileListView.ItemsSource = _filteredItems;
        TagsPanel.ItemsSource = _allTags;

        InitializeFolderTree();
        InitializeDefaultTags();
        LoadDirectory(_currentPath);

        Closed += MediaBayView_Closed;
    }

    private void MediaBayView_Closed(object? sender, EventArgs e)
    {
        StopPreview();
        _waveformCts?.Cancel();
        _waveformService.Dispose();
    }

    private void InitializeFolderTree()
    {
        // Add common locations
        var locations = new[]
        {
            new FolderNode { Name = "Music", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) },
            new FolderNode { Name = "Desktop", Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) },
            new FolderNode { Name = "Documents", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
            new FolderNode { Name = "Downloads", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") }
        };

        foreach (var node in locations.Where(n => Directory.Exists(n.Path)))
        {
            _folderNodes.Add(node);
            var item = new TreeViewItem
            {
                Header = node.Name,
                Tag = node.Path
            };
            LoadSubFolders(item, node.Path);
            FolderTree.Items.Add(item);
        }
    }

    private void LoadSubFolders(TreeViewItem parentItem, string path)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(path).Take(50))
            {
                var item = new TreeViewItem
                {
                    Header = Path.GetFileName(dir),
                    Tag = dir
                };
                // Add dummy item for expansion
                item.Items.Add(new TreeViewItem { Header = "Loading..." });
                item.Expanded += TreeViewItem_Expanded;
                parentItem.Items.Add(item);
            }
        }
        catch
        {
            // Ignore access denied
        }
    }

    private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem item && item.Items.Count == 1 && item.Items[0] is TreeViewItem dummy && dummy.Header.ToString() == "Loading...")
        {
            item.Items.Clear();
            if (item.Tag is string path)
            {
                LoadSubFolders(item, path);
            }
        }
    }

    private void InitializeDefaultTags()
    {
        var defaultTags = new[]
        {
            "Drums", "Bass", "Synth", "Vocals", "Guitar", "Piano", "Strings",
            "FX", "Ambient", "One-Shot", "Loop", "Percussion", "Lead", "Pad"
        };

        foreach (var tag in defaultTags)
        {
            _allTags.Add(new MediaBayTag { Name = tag });
        }
    }

    private void LoadDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        _currentPath = path;
        _allItems.Clear();

        try
        {
            var audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".wav", ".mp3", ".flac", ".ogg", ".aiff", ".aif", ".m4a"
            };

            foreach (var file in Directory.GetFiles(path))
            {
                var ext = Path.GetExtension(file);
                if (!audioExtensions.Contains(ext)) continue;

                var item = new MediaBayItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    Format = ext.TrimStart('.').ToUpperInvariant()
                };

                // Load metadata async
                Task.Run(() => LoadItemMetadata(item));

                _allItems.Add(item);
            }

            ApplyFilters();
            FileCountText.Text = $" - {_filteredItems.Count} files";
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Access denied to this folder.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadItemMetadata(MediaBayItem item)
    {
        try
        {
            var fileInfo = new FileInfo(item.FullPath);
            item.Size = fileInfo.Length;

            using var reader = new AudioFileReader(item.FullPath);
            item.Duration = reader.TotalTime;
            item.SampleRate = reader.WaveFormat.SampleRate;
            item.Channels = reader.WaveFormat.Channels;
            item.BitDepth = reader.WaveFormat.BitsPerSample;

            // Try to detect BPM from filename
            var name = item.Name.ToLowerInvariant();
            var bpmMatch = System.Text.RegularExpressions.Regex.Match(name, @"(\d{2,3})\s*bpm");
            if (bpmMatch.Success && double.TryParse(bpmMatch.Groups[1].Value, out double bpm))
            {
                item.Bpm = bpm;
            }

            // Try to detect key from filename
            var keyMatch = System.Text.RegularExpressions.Regex.Match(name, @"([A-Ga-g][#b]?)\s*(maj|min|m)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (keyMatch.Success)
            {
                var key = keyMatch.Groups[1].Value.ToUpperInvariant();
                var mode = keyMatch.Groups[2].Value.ToLowerInvariant();
                if (mode == "m" || mode == "min")
                {
                    key += "m";
                }
                item.Key = key;
            }

            // Auto-detect tags from filename
            foreach (var tag in _allTags)
            {
                if (name.Contains(tag.Name.ToLowerInvariant()))
                {
                    if (!item.Tags.Contains(tag.Name))
                    {
                        item.Tags.Add(tag.Name);
                    }
                }
            }
        }
        catch
        {
            // Ignore metadata errors
        }
    }

    private void ApplyFilters()
    {
        _filteredItems.Clear();

        var filtered = _allItems.AsEnumerable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var search = _searchText.ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.Name.ToLowerInvariant().Contains(search) ||
                i.Tags.Any(t => t.ToLowerInvariant().Contains(search)));
        }

        // Rating filter
        if (_minRating > 0)
        {
            filtered = filtered.Where(i => i.Rating >= _minRating);
        }

        // Color filter
        if (_selectedColors.Count > 0)
        {
            filtered = filtered.Where(i => _selectedColors.Contains(i.ColorLabel));
        }

        // Tag filter
        if (_selectedTags.Count > 0)
        {
            filtered = filtered.Where(i => i.Tags.Any(t => _selectedTags.Contains(t)));
        }

        // Format filter
        if (_selectedFormats.Count > 0)
        {
            filtered = filtered.Where(i => _selectedFormats.Contains(i.Format.ToLowerInvariant()));
        }

        foreach (var item in filtered.OrderBy(i => i.Name))
        {
            _filteredItems.Add(item);
        }

        FileCountText.Text = $" - {_filteredItems.Count} files";
    }

    #region Event Handlers

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is string path)
        {
            LoadDirectory(path);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text ?? string.Empty;
        ApplyFilters();
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedItem = FileListView.SelectedItem as MediaBayItem;

        if (_selectedItem != null)
        {
            SelectedFileInfo.Text = $"{_selectedItem.Name} | {_selectedItem.DurationDisplay} | {_selectedItem.Format} | {_selectedItem.SizeDisplay}";
            UpdateStarButtons(_selectedItem.Rating);
            PreviewFileName.Text = _selectedItem.Name;
            PreviewFileName.Visibility = Visibility.Collapsed;
            PreviewDuration.Text = _selectedItem.DurationDisplay;
            _ = LoadWaveformAsync(_selectedItem.FullPath);
        }
        else
        {
            SelectedFileInfo.Text = "No file selected";
            UpdateStarButtons(0);
            PreviewFileName.Text = "Select a file to preview";
            PreviewFileName.Visibility = Visibility.Visible;
            WaveformCanvas.Children.Clear();
        }
    }

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_selectedItem != null)
        {
            FileImportRequested?.Invoke(this, _selectedItem.FullPath);
            DialogResult = true;
            Close();
        }
    }

    private void RatingFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender == Star5Filter) _minRating = 5;
        else if (sender == Star4Filter) _minRating = 4;
        else if (sender == Star3Filter) _minRating = 3;
        else _minRating = 0;

        ApplyFilters();
    }

    private void ColorFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.Tag is string color)
        {
            if (btn.IsChecked == true)
                _selectedColors.Add(color);
            else
                _selectedColors.Remove(color);

            ApplyFilters();
        }
    }

    private void TagFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.DataContext is MediaBayTag tag)
        {
            if (tag.IsSelected)
                _selectedTags.Add(tag.Name);
            else
                _selectedTags.Remove(tag.Name);

            ApplyFilters();
        }
    }

    private void FormatFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.Tag is string format)
        {
            if (btn.IsChecked == true)
                _selectedFormats.Add(format);
            else
                _selectedFormats.Remove(format);

            ApplyFilters();
        }
    }

    private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        _searchText = string.Empty;
        SearchBox.Text = string.Empty;
        _minRating = 0;
        _selectedColors.Clear();
        _selectedTags.Clear();
        _selectedFormats.Clear();

        // Reset UI
        StarAnyFilter.IsChecked = true;
        foreach (var tag in _allTags) tag.IsSelected = false;

        ApplyFilters();
    }

    private void StarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int rating) && _selectedItem != null)
        {
            _selectedItem.Rating = rating;
            UpdateStarButtons(rating);
        }
    }

    private void UpdateStarButtons(int rating)
    {
        var stars = new[] { Star1, Star2, Star3, Star4, Star5 };
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].Foreground = i < rating
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));
        }
    }

    private void ListViewButton_Click(object sender, RoutedEventArgs e)
    {
        ListViewButton.Background = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF));
        ListViewButton.Foreground = Brushes.White;
        GridViewButton.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37));
        GridViewButton.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
    }

    private void GridViewButton_Click(object sender, RoutedEventArgs e)
    {
        GridViewButton.Background = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF));
        GridViewButton.Foreground = Brushes.White;
        ListViewButton.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37));
        ListViewButton.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
    }

    private void AddFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentPath))
        {
            var item = new TreeViewItem
            {
                Header = Path.GetFileName(_currentPath),
                Tag = _currentPath
            };
            FolderTree.Items.Add(item);
        }
    }

    private void AddToTimelineButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            FileImportRequested?.Invoke(this, _selectedItem.FullPath);
            DialogResult = true;
            Close();
        }
    }

    private void PlayPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            StartPreview(_selectedItem.FullPath);
        }
    }

    private void StopPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
    }

    #endregion

    #region Preview Methods

    private void StartPreview(string path)
    {
        StopPreview();

        try
        {
            _previewReader = new AudioFileReader(path);
            _previewPlayer = new WaveOutEvent();
            _previewPlayer.Init(_previewReader);
            _previewPlayer.Play();
        }
        catch
        {
            // Ignore preview errors
        }
    }

    private void StopPreview()
    {
        _previewPlayer?.Stop();
        _previewPlayer?.Dispose();
        _previewReader?.Dispose();
        _previewPlayer = null;
        _previewReader = null;
    }

    private async Task LoadWaveformAsync(string path)
    {
        _waveformCts?.Cancel();
        _waveformCts = new CancellationTokenSource();

        try
        {
            var data = await _waveformService.LoadFromFileAsync(path, _waveformCts.Token);
            Dispatcher.Invoke(() => DrawWaveform(data.Samples));
        }
        catch
        {
            // Ignore waveform errors
        }
    }

    private void DrawWaveform(float[] samples)
    {
        WaveformCanvas.Children.Clear();

        if (samples == null || samples.Length == 0) return;

        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double centerY = height / 2;
        int samplesPerPixel = Math.Max(1, samples.Length / (int)width);

        var points = new PointCollection();
        var pointsNeg = new PointCollection();

        for (int x = 0; x < (int)width; x++)
        {
            int start = x * samplesPerPixel;
            int end = Math.Min(start + samplesPerPixel, samples.Length);

            float max = 0, min = 0;
            for (int i = start; i < end; i++)
            {
                if (samples[i] > max) max = samples[i];
                if (samples[i] < min) min = samples[i];
            }

            points.Add(new Point(x, centerY - max * centerY * 0.9));
            pointsNeg.Add(new Point(x, centerY - min * centerY * 0.9));
        }

        var allPoints = new PointCollection();
        foreach (var p in points) allPoints.Add(p);
        for (int i = pointsNeg.Count - 1; i >= 0; i--) allPoints.Add(pointsNeg[i]);

        var polygon = new Polygon
        {
            Points = allPoints,
            Fill = new SolidColorBrush(Color.FromArgb(180, 75, 110, 175)),
            Stroke = new SolidColorBrush(Color.FromArgb(255, 75, 110, 175)),
            StrokeThickness = 0.5
        };

        WaveformCanvas.Children.Add(polygon);
    }

    #endregion
}

#region Models

public class MediaBayItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private string _format = string.Empty;
    private TimeSpan _duration;
    private long _size;
    private int _sampleRate;
    private int _channels;
    private int _bitDepth;
    private double? _bpm;
    private string? _key;
    private int _rating;
    private string _colorLabel = "None";
    private List<string> _tags = new();

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string FullPath { get => _fullPath; set { _fullPath = value; OnPropertyChanged(); } }
    public string Format { get => _format; set { _format = value; OnPropertyChanged(); } }
    public TimeSpan Duration { get => _duration; set { _duration = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationDisplay)); } }
    public long Size { get => _size; set { _size = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
    public int SampleRate { get => _sampleRate; set { _sampleRate = value; OnPropertyChanged(); } }
    public int Channels { get => _channels; set { _channels = value; OnPropertyChanged(); } }
    public int BitDepth { get => _bitDepth; set { _bitDepth = value; OnPropertyChanged(); } }
    public double? Bpm { get => _bpm; set { _bpm = value; OnPropertyChanged(); OnPropertyChanged(nameof(BpmDisplay)); } }
    public string? Key { get => _key; set { _key = value; OnPropertyChanged(); OnPropertyChanged(nameof(KeyDisplay)); } }
    public int Rating { get => _rating; set { _rating = value; OnPropertyChanged(); OnPropertyChanged(nameof(StarDisplay)); } }
    public string ColorLabel { get => _colorLabel; set { _colorLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(ColorLabelBrush)); } }
    public List<string> Tags { get => _tags; set { _tags = value; OnPropertyChanged(); OnPropertyChanged(nameof(TagsDisplay)); } }

    public string DurationDisplay => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours}:{Duration.Minutes:D2}:{Duration.Seconds:D2}"
        : $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}";

    public string SizeDisplay
    {
        get
        {
            if (Size < 1024) return $"{Size} B";
            if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
            return $"{Size / (1024.0 * 1024.0):F1} MB";
        }
    }

    public string BpmDisplay => Bpm.HasValue ? $"{Bpm:F0}" : "";
    public string KeyDisplay => Key ?? "";
    public string StarDisplay => new string('*', Rating);
    public string TagsDisplay => string.Join(", ", Tags);

    public SolidColorBrush ColorLabelBrush => ColorLabel switch
    {
        "Red" => new SolidColorBrush(Color.FromRgb(0xE8, 0x5C, 0x5C)),
        "Orange" => new SolidColorBrush(Color.FromRgb(0xE8, 0xA7, 0x3C)),
        "Yellow" => new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0x5C)),
        "Green" => new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73)),
        "Blue" => new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF)),
        "Purple" => new SolidColorBrush(Color.FromRgb(0x9C, 0x7C, 0xE8)),
        _ => new SolidColorBrush(Colors.Transparent)
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class MediaBayTag : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isSelected;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsExpanded { get; set; }
}

#endregion
