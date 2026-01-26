// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Win32;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngineEditor.Views;

/// <summary>
/// Audio Pool / Media Browser for managing all audio files in a project.
/// Provides listing, preview, search, tagging, and drag-drop functionality.
/// </summary>
public partial class AudioPoolView : UserControl
{
    private AudioPool _audioPool = new();
    private WaveOutEvent? _previewOutput;
    private AudioFileReader? _previewReader;
    private List<AudioPoolEntry> _filteredEntries = new();
    private HashSet<string> _selectedTags = new();
    private bool _showUnusedOnly;
    private bool _showExternalOnly;
    private Point _dragStartPoint;
    private bool _isDragging;

    /// <summary>
    /// Event raised when an audio file should be added to the arrangement.
    /// </summary>
    public event EventHandler<AudioPoolEntry>? AddToArrangementRequested;

    /// <summary>
    /// Gets or sets the AudioPool instance.
    /// </summary>
    public AudioPool AudioPool
    {
        get => _audioPool;
        set
        {
            if (_audioPool != value)
            {
                // Unsubscribe from old pool
                _audioPool.EntryAdded -= AudioPool_EntryChanged;
                _audioPool.EntryRemoved -= AudioPool_EntryChanged;
                _audioPool.EntryUpdated -= AudioPool_EntryChanged;

                _audioPool = value;

                // Subscribe to new pool
                _audioPool.EntryAdded += AudioPool_EntryChanged;
                _audioPool.EntryRemoved += AudioPool_EntryChanged;
                _audioPool.EntryUpdated += AudioPool_EntryChanged;

                RefreshView();
            }
        }
    }

    public AudioPoolView()
    {
        InitializeComponent();
        Loaded += AudioPoolView_Loaded;
        Unloaded += AudioPoolView_Unloaded;
    }

    private void AudioPoolView_Loaded(object sender, RoutedEventArgs e)
    {
        _audioPool.EntryAdded += AudioPool_EntryChanged;
        _audioPool.EntryRemoved += AudioPool_EntryChanged;
        _audioPool.EntryUpdated += AudioPool_EntryChanged;
        RefreshView();
    }

    private void AudioPoolView_Unloaded(object sender, RoutedEventArgs e)
    {
        StopPreview();
        _audioPool.EntryAdded -= AudioPool_EntryChanged;
        _audioPool.EntryRemoved -= AudioPool_EntryChanged;
        _audioPool.EntryUpdated -= AudioPool_EntryChanged;
    }

    private void AudioPool_EntryChanged(object? sender, AudioPoolEntryEventArgs e)
    {
        Dispatcher.Invoke(RefreshView);
    }

    /// <summary>
    /// Refreshes the view with current pool data.
    /// </summary>
    public void RefreshView()
    {
        var searchText = SearchTextBox?.Text ?? string.Empty;
        ApplyFilters(searchText);
        UpdateTags();
        UpdateStatusBar();
    }

    private void ApplyFilters(string searchText)
    {
        var entries = _audioPool.GetAllEntries().AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            entries = _audioPool.Search(searchText);
        }

        // Apply unused filter
        if (_showUnusedOnly)
        {
            entries = entries.Where(e => e.UsageCount == 0);
        }

        // Apply external filter
        if (_showExternalOnly)
        {
            entries = entries.Where(e => e.IsExternal);
        }

        // Apply tag filters
        if (_selectedTags.Count > 0)
        {
            entries = entries.Where(e => _selectedTags.All(t => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        _filteredEntries = entries.OrderBy(e => e.FileName).ToList();
        AudioPoolListView.ItemsSource = _filteredEntries;

        FileCountText.Text = $" ({_filteredEntries.Count} file{(_filteredEntries.Count == 1 ? "" : "s")})";
    }

    private void UpdateTags()
    {
        var tags = _audioPool.GetAllTags();
        TagsItemsControl.ItemsSource = tags;
        TagsItemsControl.Visibility = tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStatusBar()
    {
        var totalSize = _audioPool.TotalSizeFormatted;
        TotalSizeText.Text = $"Total: {totalSize}";

        var unusedCount = _audioPool.GetUnusedFiles().Count();
        var externalCount = _audioPool.GetExternalFiles().Count();

        var statusParts = new List<string>();
        if (unusedCount > 0)
            statusParts.Add($"{unusedCount} unused");
        if (externalCount > 0)
            statusParts.Add($"{externalCount} external");

        StatusText.Text = statusParts.Count > 0
            ? string.Join(", ", statusParts)
            : "Drop audio files here to add";
    }

    #region File Operations

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add Audio Files to Pool",
            Filter = "Audio Files|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.aif|" +
                     "WAV Files|*.wav|" +
                     "MP3 Files|*.mp3|" +
                     "FLAC Files|*.flac|" +
                     "OGG Files|*.ogg|" +
                     "AIFF Files|*.aiff;*.aif|" +
                     "All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                try
                {
                    _audioPool.AddFile(file);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add file: {file}\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    private void AudioPoolView_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var hasAudioFiles = files.Any(f =>
                f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".aif", StringComparison.OrdinalIgnoreCase));

            e.Effects = hasAudioFiles ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void AudioPoolView_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var audioExtensions = new[] { ".wav", ".mp3", ".flac", ".ogg", ".aiff", ".aif" };

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (audioExtensions.Contains(ext))
                {
                    try
                    {
                        _audioPool.AddFile(file);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to add file: {file}\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
    }

    #endregion

    #region Search and Filters

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters(SearchTextBox.Text);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
    }

    private void UnusedFilter_Click(object sender, RoutedEventArgs e)
    {
        _showUnusedOnly = !_showUnusedOnly;
        UnusedFilterButton.Tag = _showUnusedOnly ? "Active" : null;
        UnusedFilterButton.Background = _showUnusedOnly
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : System.Windows.Media.Brushes.Transparent;
        ApplyFilters(SearchTextBox.Text);
    }

    private void ExternalFilter_Click(object sender, RoutedEventArgs e)
    {
        _showExternalOnly = !_showExternalOnly;
        ExternalFilterButton.Tag = _showExternalOnly ? "Active" : null;
        ExternalFilterButton.Background = _showExternalOnly
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : System.Windows.Media.Brushes.Transparent;
        ApplyFilters(SearchTextBox.Text);
    }

    private void TagFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Content: string tag })
        {
            _selectedTags.Add(tag);
            ApplyFilters(SearchTextBox.Text);
        }
    }

    private void TagFilter_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Content: string tag })
        {
            _selectedTags.Remove(tag);
            ApplyFilters(SearchTextBox.Text);
        }
    }

    #endregion

    #region Selection and Preview

    private void AudioPoolListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PreviewButton.IsEnabled = AudioPoolListView.SelectedItem != null;

        // Update remove tag submenu
        if (AudioPoolListView.SelectedItem is AudioPoolEntry entry && entry.Tags.Count > 0)
        {
            RemoveTagMenuItem.Items.Clear();
            foreach (var tag in entry.Tags)
            {
                var menuItem = new MenuItem { Header = tag };
                menuItem.Click += (s, args) =>
                {
                    _audioPool.RemoveTag(entry.Id, tag);
                };
                RemoveTagMenuItem.Items.Add(menuItem);
            }
            RemoveTagMenuItem.IsEnabled = true;
        }
        else
        {
            RemoveTagMenuItem.Items.Clear();
            RemoveTagMenuItem.IsEnabled = false;
        }
    }

    private void AudioPoolListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AudioPoolListView.SelectedItem is AudioPoolEntry entry)
        {
            TogglePreview(entry);
        }
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (AudioPoolListView.SelectedItem is AudioPoolEntry entry)
        {
            TogglePreview(entry);
        }
    }

    private void TogglePreview(AudioPoolEntry entry)
    {
        if (_previewReader != null && _previewReader.FileName == entry.FilePath)
        {
            StopPreview();
            PreviewButton.Content = "Preview";
        }
        else
        {
            StopPreview();
            StartPreview(entry);
            PreviewButton.Content = "Stop";
        }
    }

    private void StartPreview(AudioPoolEntry entry)
    {
        try
        {
            if (!entry.FileExists)
            {
                MessageBox.Show("File not found: " + entry.FilePath,
                    "Preview Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _previewReader = new AudioFileReader(entry.FilePath);
            _previewOutput = new WaveOutEvent();
            _previewOutput.Init(_previewReader);
            _previewOutput.PlaybackStopped += PreviewOutput_PlaybackStopped;
            _previewOutput.Play();

            StatusText.Text = $"Playing: {entry.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to preview: {ex.Message}",
                "Preview Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            StopPreview();
        }
    }

    private void StopPreview()
    {
        if (_previewOutput != null)
        {
            _previewOutput.PlaybackStopped -= PreviewOutput_PlaybackStopped;
            _previewOutput.Stop();
            _previewOutput.Dispose();
            _previewOutput = null;
        }

        if (_previewReader != null)
        {
            _previewReader.Dispose();
            _previewReader = null;
        }

        PreviewButton.Content = "Preview";
        UpdateStatusBar();
    }

    private void PreviewOutput_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            StopPreview();
        });
    }

    #endregion

    #region Drag and Drop to Arrangement

    private void AudioPoolListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void AudioPoolListView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging)
            return;

        var currentPos = e.GetPosition(null);
        var diff = _dragStartPoint - currentPos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            if (AudioPoolListView.SelectedItem is AudioPoolEntry entry)
            {
                _isDragging = true;

                var data = new DataObject();
                data.SetData("AudioPoolEntry", entry);
                data.SetFileDropList(new System.Collections.Specialized.StringCollection { entry.FilePath });

                DragDrop.DoDragDrop(AudioPoolListView, data, DragDropEffects.Copy);
                _isDragging = false;
            }
        }
    }

    #endregion

    #region Context Menu

    private void ContextMenu_Preview_Click(object sender, RoutedEventArgs e)
    {
        if (AudioPoolListView.SelectedItem is AudioPoolEntry entry)
        {
            StartPreview(entry);
        }
    }

    private void ContextMenu_StopPreview_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
    }

    private void ContextMenu_AddToArrangement_Click(object sender, RoutedEventArgs e)
    {
        if (AudioPoolListView.SelectedItem is AudioPoolEntry entry)
        {
            AddToArrangementRequested?.Invoke(this, entry);
        }
    }

    private async void ContextMenu_Analyze_Click(object sender, RoutedEventArgs e)
    {
        var selectedEntries = AudioPoolListView.SelectedItems.Cast<AudioPoolEntry>().ToList();
        if (selectedEntries.Count == 0)
            return;

        StatusText.Text = "Analyzing...";

        foreach (var entry in selectedEntries)
        {
            await _audioPool.AnalyzeEntryAsync(entry);
        }

        StatusText.Text = $"Analyzed {selectedEntries.Count} file(s)";
    }

    private async void ContextMenu_GenerateWaveform_Click(object sender, RoutedEventArgs e)
    {
        var selectedEntries = AudioPoolListView.SelectedItems.Cast<AudioPoolEntry>().ToList();
        if (selectedEntries.Count == 0)
            return;

        StatusText.Text = "Generating waveforms...";

        foreach (var entry in selectedEntries)
        {
            await _audioPool.GenerateWaveformAsync(entry);
        }

        StatusText.Text = $"Generated waveforms for {selectedEntries.Count} file(s)";
    }

    private void ContextMenu_AddTag_Click(object sender, RoutedEventArgs e)
    {
        if (AudioPoolListView.SelectedItem is not AudioPoolEntry entry)
            return;

        var dialog = new Dialogs.InputDialog
        {
            Title = "Add Tag",
            Prompt = "Enter tag name:",
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Value))
        {
            _audioPool.AddTag(entry.Id, dialog.Value.Trim());
        }
    }

    private void ContextMenu_RevealInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (AudioPoolListView.SelectedItem is AudioPoolEntry entry)
        {
            if (entry.FileExists)
            {
                Process.Start("explorer.exe", $"/select,\"{entry.FilePath}\"");
            }
            else
            {
                MessageBox.Show("File not found: " + entry.FilePath,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ContextMenu_CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (AudioPoolListView.SelectedItem is AudioPoolEntry entry)
        {
            Clipboard.SetText(entry.FilePath);
            StatusText.Text = "Path copied to clipboard";
        }
    }

    private void ContextMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        var selectedEntries = AudioPoolListView.SelectedItems.Cast<AudioPoolEntry>().ToList();
        if (selectedEntries.Count == 0)
            return;

        var inUse = selectedEntries.Where(e => e.UsageCount > 0).ToList();
        if (inUse.Count > 0)
        {
            var result = MessageBox.Show(
                $"{inUse.Count} file(s) are in use. Remove anyway?",
                "Remove Files",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        foreach (var entry in selectedEntries)
        {
            _audioPool.RemoveFile(entry.Id, force: true);
        }
    }

    #endregion

    #region Pool Operations

    private async void Consolidate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_audioPool.ProjectFolder))
        {
            MessageBox.Show("Project folder must be set before consolidation.",
                "Consolidate", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var externalCount = _audioPool.GetExternalFiles().Count();
        if (externalCount == 0)
        {
            MessageBox.Show("No external files to consolidate.",
                "Consolidate", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Copy {externalCount} external file(s) into the project folder?",
            "Consolidate Files",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        StatusText.Text = "Consolidating...";

        try
        {
            var progress = new Progress<double>(p =>
            {
                StatusText.Text = $"Consolidating... {p:P0}";
            });

            var count = await _audioPool.ConsolidateAsync(progress);
            MessageBox.Show($"Consolidated {count} file(s).",
                "Consolidate", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Consolidation failed: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        UpdateStatusBar();
    }

    private async void AnalyzeAll_Click(object sender, RoutedEventArgs e)
    {
        var count = _audioPool.Count;
        if (count == 0)
        {
            MessageBox.Show("No files to analyze.",
                "Analyze", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Analyze BPM and Key for {count} file(s)?\nThis may take a while.",
            "Analyze All",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        StatusText.Text = "Analyzing...";

        try
        {
            var progress = new Progress<double>(p =>
            {
                StatusText.Text = $"Analyzing... {p:P0}";
            });

            await _audioPool.AnalyzeAllAsync(progress);
            MessageBox.Show($"Analysis complete for {count} file(s).",
                "Analyze", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analysis failed: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        UpdateStatusBar();
    }

    private void RemoveUnused_Click(object sender, RoutedEventArgs e)
    {
        var unusedCount = _audioPool.GetUnusedFiles().Count();
        if (unusedCount == 0)
        {
            MessageBox.Show("No unused files to remove.",
                "Remove Unused", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Remove {unusedCount} unused file(s) from the pool?",
            "Remove Unused Files",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        var removed = _audioPool.RemoveUnusedFiles();
        MessageBox.Show($"Removed {removed} file(s).",
            "Remove Unused", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion
}
