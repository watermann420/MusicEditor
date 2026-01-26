// MusicEngineEditor - Render Queue Dialog
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MusicEngineEditor.Services;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for managing the render queue.
/// </summary>
public partial class RenderQueueDialog : Window
{
    private readonly RenderQueueService _renderService;
    private RenderQueueItem? _selectedItem;
    private bool _isUpdating;
    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    /// <summary>
    /// Creates a new render queue dialog.
    /// </summary>
    public RenderQueueDialog() : this(new RenderQueueService()) { }

    /// <summary>
    /// Creates a new render queue dialog with the specified service.
    /// </summary>
    /// <param name="renderService">The render queue service to use.</param>
    public RenderQueueDialog(RenderQueueService renderService)
    {
        InitializeComponent();
        _renderService = renderService;

        // Set up event handlers
        _renderService.ItemStarted += OnItemStarted;
        _renderService.ItemCompleted += OnItemCompleted;
        _renderService.ItemFailed += OnItemFailed;
        _renderService.ProgressChanged += OnProgressChanged;
        _renderService.QueueCompleted += OnQueueCompleted;
        _renderService.QueueCancelled += OnQueueCancelled;

        QueueList.ItemsSource = _renderService.Items;
        UpdateUI();
        UpdateItemSettings();
    }

    /// <summary>
    /// Gets the render queue service.
    /// </summary>
    public RenderQueueService RenderService => _renderService;

    /// <summary>
    /// Sets the output directory for new items.
    /// </summary>
    public void SetOutputDirectory(string path)
    {
        _outputDirectory = path;
    }

    private void UpdateUI()
    {
        Dispatcher.Invoke(() =>
        {
            CompletedCountRun.Text = _renderService.CompletedItems.ToString();
            TotalCountRun.Text = _renderService.TotalItems.ToString();
            OverallProgressBar.Value = _renderService.OverallProgress;
            ProgressText.Text = $"{_renderService.OverallProgress:F0}%";

            var estimated = _renderService.EstimatedTotalTime;
            EstimatedTimeText.Text = estimated.TotalSeconds > 0
                ? $"Estimated time: {estimated:mm\\:ss}"
                : "Estimated time: --:--";

            // Update button states
            bool isProcessing = _renderService.IsProcessing;
            bool isPaused = _renderService.IsPaused;
            bool hasItems = _renderService.Items.Count > 0;
            bool hasPending = _renderService.PendingItems > 0;

            StartButton.IsEnabled = !isProcessing && hasPending;
            StartButton.Content = isPaused ? "Resume" : "Start";
            PauseButton.IsEnabled = isProcessing && !isPaused;
            CancelButton.IsEnabled = isProcessing;

            RemoveButton.IsEnabled = _selectedItem != null && _selectedItem.Status != RenderStatus.Processing;
            MoveUpButton.IsEnabled = _selectedItem != null && _selectedItem.Status == RenderStatus.Pending;
            MoveDownButton.IsEnabled = _selectedItem != null && _selectedItem.Status == RenderStatus.Pending;
        });
    }

    private void UpdateItemSettings()
    {
        _isUpdating = true;

        if (_selectedItem == null)
        {
            FormatCombo.SelectedIndex = 0;
            BitDepthCombo.SelectedIndex = 1;
            SampleRateCombo.SelectedIndex = 0;
            Mp3BitrateCombo.SelectedIndex = 3;
            NormalizeCheckbox.IsChecked = false;
            DitherCheckbox.IsChecked = true;
            SetSettingsEnabled(false);
        }
        else
        {
            FormatCombo.SelectedIndex = (int)_selectedItem.Format;
            BitDepthCombo.SelectedIndex = _selectedItem.BitDepth switch
            {
                16 => 0,
                24 => 1,
                32 => 2,
                _ => 1
            };
            SampleRateCombo.SelectedIndex = _selectedItem.SampleRate switch
            {
                44100 => 0,
                48000 => 1,
                88200 => 2,
                96000 => 3,
                _ => 0
            };
            Mp3BitrateCombo.SelectedIndex = _selectedItem.Mp3Bitrate switch
            {
                128 => 0,
                192 => 1,
                256 => 2,
                320 => 3,
                _ => 3
            };
            NormalizeCheckbox.IsChecked = _selectedItem.Normalize;
            DitherCheckbox.IsChecked = _selectedItem.ApplyDither;

            bool canEdit = _selectedItem.Status == RenderStatus.Pending;
            SetSettingsEnabled(canEdit);

            Mp3BitratePanel.Visibility = _selectedItem.Format == RenderFormat.Mp3
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        _isUpdating = false;
    }

    private void SetSettingsEnabled(bool enabled)
    {
        FormatCombo.IsEnabled = enabled;
        BitDepthCombo.IsEnabled = enabled;
        SampleRateCombo.IsEnabled = enabled;
        Mp3BitrateCombo.IsEnabled = enabled;
        NormalizeCheckbox.IsEnabled = enabled;
        DitherCheckbox.IsEnabled = enabled;
        ApplyToAllButton.IsEnabled = enabled;
    }

    #region Event Handlers

    private void OnItemStarted(object? sender, RenderQueueItem item)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentItemText.Text = $"Rendering: {item.Name}";
            UpdateUI();
        });
    }

    private void OnItemCompleted(object? sender, RenderQueueItem item)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateUI();
        });
    }

    private void OnItemFailed(object? sender, RenderQueueItem item)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateUI();
            MessageBox.Show($"Failed to render \"{item.Name}\": {item.ErrorMessage}",
                "Render Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private void OnProgressChanged(object? sender, double progress)
    {
        Dispatcher.Invoke(() =>
        {
            OverallProgressBar.Value = progress;
            ProgressText.Text = $"{progress:F0}%";

            var estimated = _renderService.EstimatedTotalTime;
            EstimatedTimeText.Text = estimated.TotalSeconds > 0
                ? $"Estimated time: {estimated:mm\\:ss}"
                : "Estimated time: --:--";
        });
    }

    private void OnQueueCompleted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentItemText.Text = "Render complete!";
            UpdateUI();
            MessageBox.Show("All items have been rendered successfully.",
                "Render Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private void OnQueueCancelled(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentItemText.Text = "Render cancelled";
            UpdateUI();
        });
    }

    #endregion

    #region UI Event Handlers

    private void QueueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedItem = QueueList.SelectedItem as RenderQueueItem;
        UpdateItemSettings();
        UpdateUI();
    }

    private void AddMixdownButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Add Mixdown to Queue",
            Filter = "WAV Files (*.wav)|*.wav|MP3 Files (*.mp3)|*.mp3|FLAC Files (*.flac)|*.flac|All Files (*.*)|*.*",
            DefaultExt = ".wav",
            InitialDirectory = _outputDirectory,
            FileName = "Mixdown"
        };

        if (dialog.ShowDialog() == true)
        {
            var item = new RenderQueueItem
            {
                Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                OutputPath = dialog.FileName,
                Type = RenderItemType.Mixdown,
                Format = Path.GetExtension(dialog.FileName).ToLower() switch
                {
                    ".mp3" => RenderFormat.Mp3,
                    ".flac" => RenderFormat.Flac,
                    ".ogg" => RenderFormat.Ogg,
                    ".aiff" or ".aif" => RenderFormat.Aiff,
                    _ => RenderFormat.Wav
                },
                EndTime = 180 // Default 3 minutes - would be set from project
            };

            _renderService.AddItem(item);
            UpdateUI();
        }
    }

    private void AddStemsButton_Click(object sender, RoutedEventArgs e)
    {
        // In a real implementation, this would show a dialog to select tracks
        // For now, add a placeholder stem item
        var dialog = new SaveFileDialog
        {
            Title = "Add Stem to Queue",
            Filter = "WAV Files (*.wav)|*.wav|MP3 Files (*.mp3)|*.mp3|FLAC Files (*.flac)|*.flac|All Files (*.*)|*.*",
            DefaultExt = ".wav",
            InitialDirectory = _outputDirectory,
            FileName = "Stem"
        };

        if (dialog.ShowDialog() == true)
        {
            var item = new RenderQueueItem
            {
                Name = $"Stem: {Path.GetFileNameWithoutExtension(dialog.FileName)}",
                OutputPath = dialog.FileName,
                Type = RenderItemType.Stem,
                Format = Path.GetExtension(dialog.FileName).ToLower() switch
                {
                    ".mp3" => RenderFormat.Mp3,
                    ".flac" => RenderFormat.Flac,
                    ".ogg" => RenderFormat.Ogg,
                    ".aiff" or ".aif" => RenderFormat.Aiff,
                    _ => RenderFormat.Wav
                },
                EndTime = 180
            };

            _renderService.AddItem(item);
            UpdateUI();
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            _renderService.RemoveItem(_selectedItem);
            _selectedItem = null;
            UpdateUI();
            UpdateItemSettings();
        }
    }

    private void ClearCompletedButton_Click(object sender, RoutedEventArgs e)
    {
        _renderService.ClearCompleted();
        UpdateUI();
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            _renderService.MoveUp(_selectedItem);
        }
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            _renderService.MoveDown(_selectedItem);
        }
    }

    private void FormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _selectedItem == null) return;

        _selectedItem.Format = (RenderFormat)FormatCombo.SelectedIndex;
        Mp3BitratePanel.Visibility = _selectedItem.Format == RenderFormat.Mp3
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Update file extension
        var extension = _selectedItem.Format switch
        {
            RenderFormat.Mp3 => ".mp3",
            RenderFormat.Flac => ".flac",
            RenderFormat.Ogg => ".ogg",
            RenderFormat.Aiff => ".aiff",
            _ => ".wav"
        };
        _selectedItem.OutputPath = Path.ChangeExtension(_selectedItem.OutputPath, extension);
    }

    private void BitDepthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _selectedItem == null) return;

        _selectedItem.BitDepth = BitDepthCombo.SelectedIndex switch
        {
            0 => 16,
            1 => 24,
            2 => 32,
            _ => 24
        };
    }

    private void SampleRateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _selectedItem == null) return;

        _selectedItem.SampleRate = SampleRateCombo.SelectedIndex switch
        {
            0 => 44100,
            1 => 48000,
            2 => 88200,
            3 => 96000,
            _ => 44100
        };
    }

    private void Mp3BitrateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _selectedItem == null) return;

        _selectedItem.Mp3Bitrate = Mp3BitrateCombo.SelectedIndex switch
        {
            0 => 128,
            1 => 192,
            2 => 256,
            3 => 320,
            _ => 320
        };
    }

    private void NormalizeCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || _selectedItem == null) return;
        _selectedItem.Normalize = NormalizeCheckbox.IsChecked == true;
    }

    private void DitherCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || _selectedItem == null) return;
        _selectedItem.ApplyDither = DitherCheckbox.IsChecked == true;
    }

    private void ApplyToAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;

        foreach (var item in _renderService.Items.Where(i => i.Status == RenderStatus.Pending))
        {
            item.Format = _selectedItem.Format;
            item.BitDepth = _selectedItem.BitDepth;
            item.SampleRate = _selectedItem.SampleRate;
            item.Mp3Bitrate = _selectedItem.Mp3Bitrate;
            item.Normalize = _selectedItem.Normalize;
            item.ApplyDither = _selectedItem.ApplyDither;

            // Update file extension
            var extension = item.Format switch
            {
                RenderFormat.Mp3 => ".mp3",
                RenderFormat.Flac => ".flac",
                RenderFormat.Ogg => ".ogg",
                RenderFormat.Aiff => ".aiff",
                _ => ".wav"
            };
            item.OutputPath = Path.ChangeExtension(item.OutputPath, extension);
        }

        MessageBox.Show("Settings applied to all pending items.",
            "Settings Applied", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_renderService.IsPaused)
        {
            _renderService.Resume();
        }
        else
        {
            await _renderService.StartAsync();
        }
        UpdateUI();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _renderService.Pause();
        StartButton.Content = "Resume";
        UpdateUI();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to cancel the render queue?",
            "Cancel Render",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _renderService.Cancel();
            UpdateUI();
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(_outputDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _outputDirectory,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Output folder does not exist.",
                    "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_renderService.IsProcessing)
        {
            var result = MessageBox.Show(
                "Rendering is in progress. Are you sure you want to close?",
                "Close Dialog",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;
        }

        Close();
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _renderService.ItemStarted -= OnItemStarted;
        _renderService.ItemCompleted -= OnItemCompleted;
        _renderService.ItemFailed -= OnItemFailed;
        _renderService.ProgressChanged -= OnProgressChanged;
        _renderService.QueueCompleted -= OnQueueCompleted;
        _renderService.QueueCancelled -= OnQueueCancelled;

        base.OnClosed(e);
    }
}
