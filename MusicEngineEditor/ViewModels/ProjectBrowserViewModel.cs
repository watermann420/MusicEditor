using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Project Browser.
/// </summary>
public partial class ProjectBrowserViewModel : ViewModelBase
{
    private static readonly string[] ProjectExtensions = { ".mep", ".json" };
    private string _currentDirectory;

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _projects = new();

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _filteredProjects = new();

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _recentProjects = new();

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _favoriteProjects = new();

    [ObservableProperty]
    private ProjectInfo? _selectedProject;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ProjectSortOption _sortOption = ProjectSortOption.DateModified;

    [ObservableProperty]
    private ProjectViewMode _viewMode = ProjectViewMode.List;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _recentDirectories = new();

    /// <summary>
    /// Event raised when a project should be opened.
    /// </summary>
    public event EventHandler<ProjectInfo>? ProjectOpened;

    /// <summary>
    /// Event raised when a new project should be created.
    /// </summary>
    public event EventHandler? NewProjectRequested;

    /// <summary>
    /// Gets the default projects directory.
    /// </summary>
    public static string DefaultProjectsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MusicEngine", "Projects");

    public ProjectBrowserViewModel()
    {
        _currentDirectory = DefaultProjectsDirectory;
        CurrentPath = _currentDirectory;

        // Ensure default directory exists
        if (!Directory.Exists(_currentDirectory))
        {
            Directory.CreateDirectory(_currentDirectory);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSortOptionChanged(ProjectSortOption value)
    {
        ApplyFilter();
    }

    partial void OnShowFavoritesOnlyChanged(bool value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Loads projects from the current directory.
    /// </summary>
    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        IsLoading = true;
        IsBusy = true;
        StatusMessage = "Loading projects...";

        try
        {
            Projects.Clear();

            await Task.Run(() =>
            {
                if (!Directory.Exists(_currentDirectory))
                    return;

                var files = Directory.GetFiles(_currentDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => ProjectExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                foreach (var file in files)
                {
                    var info = ProjectInfo.FromFile(file);
                    Application.Current.Dispatcher.Invoke(() => Projects.Add(info));
                }
            });

            ApplyFilter();
            StatusMessage = $"Found {Projects.Count} projects";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading projects: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Navigates to a directory.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            StatusMessage = "Directory does not exist";
            return;
        }

        _currentDirectory = path;
        CurrentPath = path;

        // Add to recent directories
        if (!RecentDirectories.Contains(path))
        {
            RecentDirectories.Insert(0, path);
            if (RecentDirectories.Count > 10)
            {
                RecentDirectories.RemoveAt(RecentDirectories.Count - 1);
            }
        }

        await LoadProjectsAsync();
    }

    /// <summary>
    /// Opens a folder browser dialog.
    /// </summary>
    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Projects Folder",
            SelectedPath = _currentDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            await NavigateToAsync(dialog.SelectedPath);
        }
    }

    /// <summary>
    /// Opens the selected project.
    /// </summary>
    [RelayCommand]
    private void OpenProject()
    {
        if (SelectedProject != null)
        {
            ProjectOpened?.Invoke(this, SelectedProject);
        }
    }

    /// <summary>
    /// Opens a project by double-click.
    /// </summary>
    public void OpenProject(ProjectInfo project)
    {
        ProjectOpened?.Invoke(this, project);
    }

    /// <summary>
    /// Creates a new project.
    /// </summary>
    [RelayCommand]
    private void NewProject()
    {
        NewProjectRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggles favorite status for the selected project.
    /// </summary>
    [RelayCommand]
    private void ToggleFavorite()
    {
        if (SelectedProject != null)
        {
            SelectedProject.IsFavorite = !SelectedProject.IsFavorite;
            UpdateFavorites();
            ApplyFilter();
        }
    }

    /// <summary>
    /// Deletes the selected project.
    /// </summary>
    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        if (SelectedProject == null)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedProject.Name}'?\n\nThis action cannot be undone.",
            "Delete Project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                if (File.Exists(SelectedProject.FilePath))
                {
                    File.Delete(SelectedProject.FilePath);
                }

                Projects.Remove(SelectedProject);
                FilteredProjects.Remove(SelectedProject);
                SelectedProject = null;
                StatusMessage = "Project deleted";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Opens the project folder in Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenInExplorer()
    {
        if (SelectedProject != null && Directory.Exists(SelectedProject.Directory))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{SelectedProject.FilePath}\"",
                UseShellExecute = true
            });
        }
        else if (Directory.Exists(_currentDirectory))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _currentDirectory,
                UseShellExecute = true
            });
        }
    }

    /// <summary>
    /// Duplicates the selected project.
    /// </summary>
    [RelayCommand]
    private async Task DuplicateProjectAsync()
    {
        if (SelectedProject == null || !File.Exists(SelectedProject.FilePath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(SelectedProject.FilePath) ?? _currentDirectory;
            var name = Path.GetFileNameWithoutExtension(SelectedProject.FilePath);
            var ext = Path.GetExtension(SelectedProject.FilePath);

            // Find a unique name
            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
                counter++;
            } while (File.Exists(newPath));

            File.Copy(SelectedProject.FilePath, newPath);

            // Reload projects
            await LoadProjectsAsync();
            StatusMessage = "Project duplicated";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to duplicate project: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Refreshes the project list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadProjectsAsync();
    }

    /// <summary>
    /// Applies filtering and sorting to the projects list.
    /// </summary>
    private void ApplyFilter()
    {
        var filtered = Projects.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Author.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply favorites filter
        if (ShowFavoritesOnly)
        {
            filtered = filtered.Where(p => p.IsFavorite);
        }

        // Apply sorting
        filtered = SortOption switch
        {
            ProjectSortOption.Name => filtered.OrderBy(p => p.Name),
            ProjectSortOption.NameDescending => filtered.OrderByDescending(p => p.Name),
            ProjectSortOption.DateModified => filtered.OrderByDescending(p => p.ModifiedDate),
            ProjectSortOption.DateModifiedAscending => filtered.OrderBy(p => p.ModifiedDate),
            ProjectSortOption.DateCreated => filtered.OrderByDescending(p => p.CreatedDate),
            ProjectSortOption.FileSize => filtered.OrderByDescending(p => p.FileSize),
            ProjectSortOption.Bpm => filtered.OrderBy(p => p.Bpm),
            _ => filtered
        };

        FilteredProjects.Clear();
        foreach (var project in filtered)
        {
            FilteredProjects.Add(project);
        }
    }

    /// <summary>
    /// Updates the favorites collection.
    /// </summary>
    private void UpdateFavorites()
    {
        FavoriteProjects.Clear();
        foreach (var project in Projects.Where(p => p.IsFavorite))
        {
            FavoriteProjects.Add(project);
        }
    }

    /// <summary>
    /// Adds a project to the recent list.
    /// </summary>
    public void AddToRecent(ProjectInfo project)
    {
        // Remove if already in list
        var existing = RecentProjects.FirstOrDefault(p => p.FilePath == project.FilePath);
        if (existing != null)
        {
            RecentProjects.Remove(existing);
        }

        // Add to front
        RecentProjects.Insert(0, project);

        // Limit to 10 recent projects
        while (RecentProjects.Count > 10)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }
    }
}
