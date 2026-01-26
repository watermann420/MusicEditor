// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Cloud storage dialog for managing project synchronization with cloud providers.
/// </summary>
public partial class CloudStorageDialog : Window
{
    private CancellationTokenSource? _operationCts;
    private bool _isOperationInProgress;

    /// <summary>
    /// Collection of cloud projects.
    /// </summary>
    public ObservableCollection<CloudProject> Projects { get; } = new();

    /// <summary>
    /// Gets the selected project.
    /// </summary>
    public CloudProject? SelectedProject => ProjectList.SelectedItem as CloudProject;

    /// <summary>
    /// Creates a new cloud storage dialog.
    /// </summary>
    public CloudStorageDialog()
    {
        InitializeComponent();
        ProjectList.ItemsSource = Projects;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshProjectListAsync();
    }

    private async Task RefreshProjectListAsync()
    {
        StatusText.Text = "Loading projects...";

        // Simulate loading projects from cloud
        await Task.Delay(500);

        Projects.Clear();

        // Add sample projects for demonstration
        Projects.Add(new CloudProject
        {
            Name = "Demo Song",
            LastModified = DateTime.Now.AddDays(-1),
            Size = "12.5 MB",
            Provider = "Dropbox",
            ProviderBrush = FindResource("DropboxBadgeBrush") as Brush ?? Brushes.Blue,
            SyncStatus = "Synced",
            StatusBrush = FindResource("SyncedBrush") as Brush ?? Brushes.Green
        });

        Projects.Add(new CloudProject
        {
            Name = "Work in Progress",
            LastModified = DateTime.Now.AddHours(-2),
            Size = "8.3 MB",
            Provider = "Google Drive",
            ProviderBrush = FindResource("GoogleDriveBadgeBrush") as Brush ?? Brushes.Blue,
            SyncStatus = "Pending",
            StatusBrush = FindResource("PendingBrush") as Brush ?? Brushes.Orange
        });

        Projects.Add(new CloudProject
        {
            Name = "Remix Project",
            LastModified = DateTime.Now.AddDays(-5),
            Size = "25.1 MB",
            Provider = "OneDrive",
            ProviderBrush = FindResource("OneDriveBadgeBrush") as Brush ?? Brushes.Blue,
            SyncStatus = "Conflict",
            StatusBrush = FindResource("ConflictBrush") as Brush ?? Brushes.Red,
            HasConflict = true
        });

        UpdateEmptyState();
        StatusText.Text = $"{Projects.Count} projects found";
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = Projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Filter projects based on search text
        var searchText = SearchBox.Text.ToLowerInvariant();
        // In a real implementation, filter the Projects collection
    }

    private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Filter by provider
        // In a real implementation, filter the Projects collection based on selected provider
    }

    private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = ProjectList.SelectedItem != null;
        DownloadButton.IsEnabled = hasSelection;
        SyncButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;

        // Show conflict panel if project has conflict
        if (SelectedProject?.HasConflict == true)
        {
            ConflictPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ConflictPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "MusicEngine Project (*.meproj)|*.meproj|All files (*.*)|*.*",
            Title = "Select Project to Upload"
        };

        if (dialog.ShowDialog() == true)
        {
            await PerformOperationAsync("Uploading project...", async (progress, ct) =>
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(50, ct);
                    progress(i);
                }
            });

            await RefreshProjectListAsync();
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProject == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "MusicEngine Project (*.meproj)|*.meproj|All files (*.*)|*.*",
            FileName = SelectedProject.Name,
            Title = "Save Project"
        };

        if (dialog.ShowDialog() == true)
        {
            await PerformOperationAsync($"Downloading {SelectedProject.Name}...", async (progress, ct) =>
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(50, ct);
                    progress(i);
                }
            });

            MessageBox.Show($"Project downloaded to:\n{dialog.FileName}", "Download Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProject == null) return;

        await PerformOperationAsync($"Syncing {SelectedProject.Name}...", async (progress, ct) =>
        {
            for (int i = 0; i <= 100; i += 10)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(100, ct);
                progress(i);
            }
        });

        SelectedProject.SyncStatus = "Synced";
        SelectedProject.StatusBrush = FindResource("SyncedBrush") as Brush ?? Brushes.Green;
        SelectedProject.HasConflict = false;
        ConflictPanel.Visibility = Visibility.Collapsed;

        // Refresh the list to show updated status
        ProjectList.Items.Refresh();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProject == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedProject.Name}' from the cloud?\n\nThis action cannot be undone.",
            "Delete Project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            Projects.Remove(SelectedProject);
            UpdateEmptyState();
            StatusText.Text = "Project deleted";
        }
    }

    private void ConnectProvider_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Cloud provider connection would be configured here.\n\nSupported providers:\n- Dropbox\n- Google Drive\n- OneDrive",
            "Connect Cloud Provider",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void KeepLocal_Click(object sender, RoutedEventArgs e)
    {
        await ResolveConflictAsync("local");
    }

    private async void KeepCloud_Click(object sender, RoutedEventArgs e)
    {
        await ResolveConflictAsync("cloud");
    }

    private async void KeepBoth_Click(object sender, RoutedEventArgs e)
    {
        await ResolveConflictAsync("both");
    }

    private async Task ResolveConflictAsync(string resolution)
    {
        if (SelectedProject == null) return;

        await PerformOperationAsync($"Resolving conflict ({resolution})...", async (progress, ct) =>
        {
            for (int i = 0; i <= 100; i += 20)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(100, ct);
                progress(i);
            }
        });

        SelectedProject.SyncStatus = "Synced";
        SelectedProject.StatusBrush = FindResource("SyncedBrush") as Brush ?? Brushes.Green;
        SelectedProject.HasConflict = false;
        ConflictPanel.Visibility = Visibility.Collapsed;

        ProjectList.Items.Refresh();
        StatusText.Text = "Conflict resolved";
    }

    private async Task PerformOperationAsync(string operationText, Func<Action<int>, CancellationToken, Task> operation)
    {
        if (_isOperationInProgress) return;

        _isOperationInProgress = true;
        _operationCts = new CancellationTokenSource();

        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = operationText;
        ProgressBar.Value = 0;
        ProgressPercentage.Text = "0%";

        try
        {
            await operation(progress =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Value = progress;
                    ProgressPercentage.Text = $"{progress}%";
                });
            }, _operationCts.Token);

            StatusText.Text = "Operation completed";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operation cancelled";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isOperationInProgress = false;
            ProgressPanel.Visibility = Visibility.Collapsed;
            _operationCts?.Dispose();
            _operationCts = null;
        }
    }

    private void CancelOperation_Click(object sender, RoutedEventArgs e)
    {
        _operationCts?.Cancel();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Shows the cloud storage dialog.
    /// </summary>
    public static void ShowDialog(Window owner)
    {
        var dialog = new CloudStorageDialog
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }
}

/// <summary>
/// Represents a project stored in the cloud.
/// </summary>
public class CloudProject : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private DateTime _lastModified;
    private string _size = string.Empty;
    private string _provider = string.Empty;
    private Brush? _providerBrush;
    private string _syncStatus = string.Empty;
    private Brush? _statusBrush;
    private bool _hasConflict;

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    /// <summary>
    /// Gets or sets the last modified date.
    /// </summary>
    public DateTime LastModified
    {
        get => _lastModified;
        set { _lastModified = value; OnPropertyChanged(nameof(LastModified)); }
    }

    /// <summary>
    /// Gets or sets the project size.
    /// </summary>
    public string Size
    {
        get => _size;
        set { _size = value; OnPropertyChanged(nameof(Size)); }
    }

    /// <summary>
    /// Gets or sets the cloud provider name.
    /// </summary>
    public string Provider
    {
        get => _provider;
        set { _provider = value; OnPropertyChanged(nameof(Provider)); }
    }

    /// <summary>
    /// Gets or sets the provider badge brush.
    /// </summary>
    public Brush? ProviderBrush
    {
        get => _providerBrush;
        set { _providerBrush = value; OnPropertyChanged(nameof(ProviderBrush)); }
    }

    /// <summary>
    /// Gets or sets the sync status.
    /// </summary>
    public string SyncStatus
    {
        get => _syncStatus;
        set { _syncStatus = value; OnPropertyChanged(nameof(SyncStatus)); }
    }

    /// <summary>
    /// Gets or sets the status brush.
    /// </summary>
    public Brush? StatusBrush
    {
        get => _statusBrush;
        set { _statusBrush = value; OnPropertyChanged(nameof(StatusBrush)); }
    }

    /// <summary>
    /// Gets or sets whether the project has a sync conflict.
    /// </summary>
    public bool HasConflict
    {
        get => _hasConflict;
        set { _hasConflict = value; OnPropertyChanged(nameof(HasConflict)); }
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
