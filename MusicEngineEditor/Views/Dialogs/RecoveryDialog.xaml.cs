// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for recovering projects from auto-save files.
/// </summary>
public partial class RecoveryDialog : Window
{
    private readonly AutoSaveService _autoSaveService;
    private readonly RecoveryService _recoveryService;

    /// <summary>
    /// Collection of available auto-save files.
    /// </summary>
    public ObservableCollection<AutoSaveDisplayItem> AutoSaves { get; } = new();

    /// <summary>
    /// Gets the selected auto-save item.
    /// </summary>
    public AutoSaveDisplayItem? SelectedAutoSave => AutoSaveList.SelectedItem as AutoSaveDisplayItem;

    /// <summary>
    /// Gets the file path of the auto-save to recover.
    /// </summary>
    public string? RecoveryFilePath { get; private set; }

    /// <summary>
    /// Gets the selected recovery info for use with RecoveryService.
    /// </summary>
    public RecoveryInfo? SelectedRecoveryInfo { get; private set; }

    /// <summary>
    /// Creates a new recovery dialog.
    /// </summary>
    public RecoveryDialog()
    {
        InitializeComponent();
        _autoSaveService = AutoSaveService.Instance;
        _recoveryService = RecoveryService.Instance;
        AutoSaveList.ItemsSource = AutoSaves;

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Creates a new recovery dialog with a specific auto-save service.
    /// </summary>
    /// <param name="autoSaveService">The auto-save service to use.</param>
    public RecoveryDialog(AutoSaveService autoSaveService)
    {
        InitializeComponent();
        _autoSaveService = autoSaveService ?? throw new ArgumentNullException(nameof(autoSaveService));
        _recoveryService = RecoveryService.Instance;
        AutoSaveList.ItemsSource = AutoSaves;

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Creates a new recovery dialog with specific services.
    /// </summary>
    /// <param name="autoSaveService">The auto-save service to use.</param>
    /// <param name="recoveryService">The recovery service to use.</param>
    public RecoveryDialog(AutoSaveService autoSaveService, RecoveryService recoveryService)
    {
        InitializeComponent();
        _autoSaveService = autoSaveService ?? throw new ArgumentNullException(nameof(autoSaveService));
        _recoveryService = recoveryService ?? throw new ArgumentNullException(nameof(recoveryService));
        AutoSaveList.ItemsSource = AutoSaves;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshAutoSaveList();
    }

    private void RefreshAutoSaveList()
    {
        AutoSaves.Clear();

        // First, get recovery points from the RecoveryService (crash-related)
        var recoveryPoints = _recoveryService.GetRecoveryPoints();
        var processedGuids = new System.Collections.Generic.HashSet<Guid>();

        foreach (var recovery in recoveryPoints.OrderByDescending(r => r.CrashTime))
        {
            AutoSaves.Add(new AutoSaveDisplayItem
            {
                FilePath = recovery.AutoSavePath,
                ProjectName = recovery.ProjectName,
                ProjectGuid = recovery.ProjectGuid,
                SaveTime = recovery.CrashTime,
                OriginalPath = recovery.OriginalProjectPath,
                FileSize = recovery.FileSize,
                FileSizeDisplay = recovery.FileSizeDisplay,
                TimeAgo = recovery.TimeAgo,
                IsCrashRecovery = true
            });
            processedGuids.Add(recovery.ProjectGuid);
        }

        // Then, add any auto-save infos that weren't already added from recovery points
        var infos = _autoSaveService.GetAutoSaveInfos();

        foreach (var info in infos.OrderByDescending(i => i.SaveTime))
        {
            // Skip if already processed from recovery points
            if (processedGuids.Contains(info.ProjectGuid))
                continue;

            AutoSaves.Add(new AutoSaveDisplayItem
            {
                FilePath = info.FilePath,
                ProjectName = info.ProjectName,
                ProjectGuid = info.ProjectGuid,
                SaveTime = info.SaveTime,
                OriginalPath = info.OriginalPath,
                FileSize = info.FileSize,
                FileSizeDisplay = info.FileSizeDisplay,
                TimeAgo = GetTimeAgo(info.SaveTime),
                IsCrashRecovery = false
            });
        }

        UpdateEmptyState();
        UpdateStatus();
        UpdateHeaderDescription();
    }

    private void UpdateHeaderDescription()
    {
        // Check if any items are crash recoveries
        var hasCrashRecovery = AutoSaves.Any(a => a.IsCrashRecovery);

        if (hasCrashRecovery)
        {
            HeaderDescription.Text = "MusicEngine Editor did not close properly. Recovery data is available.";
        }
        else if (AutoSaves.Count > 0)
        {
            HeaderDescription.Text = "Auto-saved projects were found. Would you like to recover them?";
        }
        else
        {
            HeaderDescription.Text = "No recovery data available.";
        }
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = AutoSaves.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AutoSaveList.Visibility = AutoSaves.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DeleteAllButton.IsEnabled = AutoSaves.Count > 0;
    }

    private void UpdateStatus()
    {
        if (AutoSaves.Count == 0)
        {
            StatusText.Text = "No auto-save files available";
        }
        else if (AutoSaves.Count == 1)
        {
            StatusText.Text = "1 auto-save file found";
        }
        else
        {
            StatusText.Text = $"{AutoSaves.Count} auto-save files found";
        }
    }

    private static string GetTimeAgo(DateTime saveTime)
    {
        var diff = DateTime.Now - saveTime;

        if (diff.TotalMinutes < 1)
            return "Just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)} weeks ago";

        return saveTime.ToString("MMM d");
    }

    private void AutoSaveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RecoverButton.IsEnabled = AutoSaveList.SelectedItem != null;
    }

    private void RecoverButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAutoSave == null)
            return;

        RecoveryFilePath = SelectedAutoSave.FilePath;

        // Create RecoveryInfo for the selected item
        SelectedRecoveryInfo = new RecoveryInfo
        {
            ProjectName = SelectedAutoSave.ProjectName,
            AutoSavePath = SelectedAutoSave.FilePath,
            CrashTime = SelectedAutoSave.SaveTime,
            OriginalProjectPath = SelectedAutoSave.OriginalPath,
            FileSize = SelectedAutoSave.FileSize,
            ProjectGuid = SelectedAutoSave.ProjectGuid
        };

        DialogResult = true;
        Close();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        RecoveryFilePath = null;
        DialogResult = false;
        Close();
    }

    private void DeleteAutoSave_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filePath)
        {
            var item = AutoSaves.FirstOrDefault(a => a.FilePath == filePath);
            if (item == null)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the auto-save for '{item.ProjectName}'?\n\nThis action cannot be undone.",
                "Delete Auto-Save",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _autoSaveService.DeleteAutoSave(filePath);
                AutoSaves.Remove(item);
                UpdateEmptyState();
                UpdateStatus();
            }
        }
    }

    private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete all {AutoSaves.Count} auto-save files?\n\nThis action cannot be undone.",
            "Delete All Auto-Saves",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            foreach (var item in AutoSaves.ToList())
            {
                _autoSaveService.DeleteAutoSave(item.FilePath);
            }

            AutoSaves.Clear();
            UpdateEmptyState();
            UpdateStatus();
        }
    }

    /// <summary>
    /// Shows the recovery dialog and returns the selected file path for recovery.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <returns>The file path to recover, or null if recovery was skipped.</returns>
    public static string? ShowRecoveryDialog(Window owner)
    {
        var autoSaveService = AutoSaveService.Instance;
        var recoveryService = RecoveryService.Instance;

        // Check for crash recovery or existing auto-saves
        if (!recoveryService.HasRecoverableSession() && !autoSaveService.HasAutoSaves())
            return null;

        var dialog = new RecoveryDialog(autoSaveService, recoveryService)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true)
        {
            return dialog.RecoveryFilePath;
        }

        return null;
    }

    /// <summary>
    /// Shows the recovery dialog if auto-saves are available.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="autoSaveService">The auto-save service to use.</param>
    /// <returns>The file path to recover, or null if recovery was skipped or no auto-saves exist.</returns>
    public static string? ShowRecoveryDialogIfNeeded(Window owner, AutoSaveService? autoSaveService = null)
    {
        var autoService = autoSaveService ?? AutoSaveService.Instance;
        var recoveryService = RecoveryService.Instance;

        // Check for crash recovery or existing auto-saves
        if (!recoveryService.HasRecoverableSession() && !autoService.HasAutoSaves())
            return null;

        var dialog = new RecoveryDialog(autoService, recoveryService)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true)
        {
            return dialog.RecoveryFilePath;
        }

        return null;
    }

    /// <summary>
    /// Shows the recovery dialog if there was a crash or auto-saves exist.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <returns>The recovery result containing the path and recovery info.</returns>
    public static RecoveryResult ShowRecoveryDialogWithInfo(Window owner)
    {
        var autoSaveService = AutoSaveService.Instance;
        var recoveryService = RecoveryService.Instance;

        // Check for crash recovery or existing auto-saves
        if (!recoveryService.HasRecoverableSession() && !autoSaveService.HasAutoSaves())
            return new RecoveryResult { WasRecoveryNeeded = false };

        var dialog = new RecoveryDialog(autoSaveService, recoveryService)
        {
            Owner = owner
        };

        var result = new RecoveryResult { WasRecoveryNeeded = true };

        if (dialog.ShowDialog() == true)
        {
            result.RecoveryFilePath = dialog.RecoveryFilePath;
            result.RecoveryInfo = dialog.SelectedRecoveryInfo;
            result.WasRecovered = true;
        }

        return result;
    }
}

/// <summary>
/// Result from showing the recovery dialog.
/// </summary>
public class RecoveryResult
{
    /// <summary>
    /// Gets or sets whether recovery data existed and dialog was shown.
    /// </summary>
    public bool WasRecoveryNeeded { get; set; }

    /// <summary>
    /// Gets or sets whether the user chose to recover.
    /// </summary>
    public bool WasRecovered { get; set; }

    /// <summary>
    /// Gets or sets the path to the recovery file.
    /// </summary>
    public string? RecoveryFilePath { get; set; }

    /// <summary>
    /// Gets or sets the detailed recovery information.
    /// </summary>
    public RecoveryInfo? RecoveryInfo { get; set; }
}

/// <summary>
/// Display item for auto-save files in the list.
/// </summary>
public class AutoSaveDisplayItem : INotifyPropertyChanged
{
    private string _filePath = string.Empty;
    private string _projectName = string.Empty;
    private Guid _projectGuid;
    private DateTime _saveTime;
    private string _originalPath = string.Empty;
    private long _fileSize;
    private string _fileSizeDisplay = string.Empty;
    private string _timeAgo = string.Empty;

    /// <summary>
    /// Gets or sets the auto-save file path.
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
    }

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string ProjectName
    {
        get => _projectName;
        set { _projectName = value; OnPropertyChanged(nameof(ProjectName)); }
    }

    /// <summary>
    /// Gets or sets the project GUID.
    /// </summary>
    public Guid ProjectGuid
    {
        get => _projectGuid;
        set { _projectGuid = value; OnPropertyChanged(nameof(ProjectGuid)); }
    }

    /// <summary>
    /// Gets or sets the save time.
    /// </summary>
    public DateTime SaveTime
    {
        get => _saveTime;
        set { _saveTime = value; OnPropertyChanged(nameof(SaveTime)); }
    }

    /// <summary>
    /// Gets or sets the original project path.
    /// </summary>
    public string OriginalPath
    {
        get => _originalPath;
        set { _originalPath = value; OnPropertyChanged(nameof(OriginalPath)); }
    }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize
    {
        get => _fileSize;
        set { _fileSize = value; OnPropertyChanged(nameof(FileSize)); }
    }

    /// <summary>
    /// Gets or sets the human-readable file size.
    /// </summary>
    public string FileSizeDisplay
    {
        get => _fileSizeDisplay;
        set { _fileSizeDisplay = value; OnPropertyChanged(nameof(FileSizeDisplay)); }
    }

    /// <summary>
    /// Gets or sets the time ago string.
    /// </summary>
    public string TimeAgo
    {
        get => _timeAgo;
        set { _timeAgo = value; OnPropertyChanged(nameof(TimeAgo)); }
    }

    private bool _isCrashRecovery;

    /// <summary>
    /// Gets or sets whether this is from a crash recovery.
    /// </summary>
    public bool IsCrashRecovery
    {
        get => _isCrashRecovery;
        set { _isCrashRecovery = value; OnPropertyChanged(nameof(IsCrashRecovery)); }
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
