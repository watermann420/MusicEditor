using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Main ViewModel for the IDE
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IScriptExecutionService _executionService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MusicProject? _currentProject;

    [ObservableProperty]
    private EditorTabViewModel? _activeDocument;

    [ObservableProperty]
    private ProjectExplorerViewModel? _projectExplorer;

    [ObservableProperty]
    private OutputViewModel? _output;

    [ObservableProperty]
    private bool _isProjectExplorerVisible = true;

    [ObservableProperty]
    private bool _isOutputVisible = true;

    [ObservableProperty]
    private bool _isPropertiesVisible = true;

    [ObservableProperty]
    private int _currentBpm = 120;

    [ObservableProperty]
    private string _playbackStatus = "Stopped";

    [ObservableProperty]
    private string _caretPosition = "1:1";

    [ObservableProperty]
    private string _encoding = "UTF-8";

    [ObservableProperty]
    private ObservableCollection<string> _audioDevices = new();

    [ObservableProperty]
    private string? _selectedAudioDevice;

    public ObservableCollection<EditorTabViewModel> OpenDocuments { get; } = new();
    public ObservableCollection<string> RecentProjects { get; } = new();

    public MainViewModel(IProjectService projectService, IScriptExecutionService executionService, ISettingsService settingsService)
    {
        _projectService = projectService;
        _executionService = executionService;
        _settingsService = settingsService;

        ProjectExplorer = new ProjectExplorerViewModel();
        Output = new OutputViewModel();

        // Subscribe to events
        _projectService.ProjectLoaded += OnProjectLoaded;
        _executionService.OutputReceived += OnOutputReceived;
        _executionService.ExecutionStarted += OnExecutionStarted;
        _executionService.ExecutionStopped += OnExecutionStopped;

        // Load settings
        _ = _settingsService.LoadSettingsAsync();
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        var dialog = new NewProjectDialog
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            StatusMessage = "Creating new project...";
            IsBusy = true;

            try
            {
                var projectPath = Path.Combine(dialog.ProjectLocation, dialog.ProjectName);
                var project = await _projectService.CreateProjectAsync(dialog.ProjectName, projectPath);
                project.Namespace = dialog.Namespace;
                await _projectService.SaveProjectAsync(project);
                StatusMessage = $"Created project: {project.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Failed to create project";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open MusicEngine Project",
            Filter = "MusicEngine Project (*.meproj)|*.meproj|All Files (*.*)|*.*",
            DefaultExt = ".meproj"
        };

        if (dialog.ShowDialog() == true)
        {
            StatusMessage = "Opening project...";
            IsBusy = true;

            try
            {
                await _projectService.OpenProjectAsync(dialog.FileName);
                StatusMessage = $"Opened: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Failed to open project";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (ActiveDocument?.Script != null)
        {
            await _projectService.SaveScriptAsync(ActiveDocument.Script);
            ActiveDocument.IsDirty = false;
            StatusMessage = $"Saved: {ActiveDocument.Title}";
        }
    }

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        foreach (var doc in OpenDocuments)
        {
            if (doc.IsDirty && doc.Script != null)
            {
                await _projectService.SaveScriptAsync(doc.Script);
                doc.IsDirty = false;
            }
        }

        if (CurrentProject != null)
        {
            await _projectService.SaveProjectAsync(CurrentProject);
        }

        StatusMessage = "All files saved";
    }

    [RelayCommand]
    private void NewFile()
    {
        if (CurrentProject == null)
        {
            MessageBox.Show("Please open or create a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var fileName = InputDialog.Show("Enter file name:", "New File", "NewScript.me",
            Application.Current.MainWindow);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            // Ensure .me extension
            if (!fileName.EndsWith(".me", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".me";
            }

            try
            {
                var script = _projectService.CreateScript(CurrentProject, fileName);
                OpenScript(script);
                StatusMessage = $"Created: {fileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (CurrentProject == null)
        {
            Output?.AppendLine("No project loaded.");
            return;
        }

        await SaveAllAsync();
        PlaybackStatus = "Running";
        await _executionService.RunAsync(CurrentProject);
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _executionService.StopAsync();
        PlaybackStatus = "Stopped";
    }

    [RelayCommand]
    private void Find()
    {
        if (ActiveDocument == null)
        {
            MessageBox.Show("No document is currently open.", "Find",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var searchText = InputDialog.Show("Find:", "Find", "",
            Application.Current.MainWindow);

        if (!string.IsNullOrEmpty(searchText))
        {
            var content = ActiveDocument.Content;
            var index = content.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);

            if (index >= 0)
            {
                // Calculate line and column
                var lineNumber = content.Take(index).Count(c => c == '\n') + 1;
                StatusMessage = $"Found at line {lineNumber}";
            }
            else
            {
                MessageBox.Show($"'{searchText}' not found.", "Find",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    [RelayCommand]
    private void Replace()
    {
        if (ActiveDocument == null)
        {
            MessageBox.Show("No document is currently open.", "Replace",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var searchText = InputDialog.Show("Find:", "Replace", "",
            Application.Current.MainWindow);

        if (string.IsNullOrEmpty(searchText))
            return;

        var replaceText = InputDialog.Show("Replace with:", "Replace", "",
            Application.Current.MainWindow);

        if (replaceText == null)
            return;

        var content = ActiveDocument.Content;
        if (content.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            var result = MessageBox.Show($"Replace all occurrences of '{searchText}'?",
                "Replace", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ActiveDocument.Content = content.Replace(searchText, replaceText, StringComparison.OrdinalIgnoreCase);
                StatusMessage = "Replacements made";
            }
        }
        else
        {
            MessageBox.Show($"'{searchText}' not found.", "Replace",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void AddScript()
    {
        if (CurrentProject == null)
        {
            MessageBox.Show("Please open or create a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var scriptName = InputDialog.Show("Enter script name:", "Add Script", "NewScript",
            Application.Current.MainWindow);

        if (!string.IsNullOrWhiteSpace(scriptName))
        {
            // Ensure .me extension
            if (!scriptName.EndsWith(".me", StringComparison.OrdinalIgnoreCase))
            {
                scriptName += ".me";
            }

            try
            {
                var script = _projectService.CreateScript(CurrentProject, scriptName);
                OpenScript(script);
                StatusMessage = $"Added script: {scriptName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add script: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void AddExistingFile()
    {
        if (CurrentProject == null)
        {
            MessageBox.Show("Please open or create a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Add Existing File",
            Filter = "MusicEngine Script (*.me)|*.me|All Files (*.*)|*.*",
            DefaultExt = ".me",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var filePath in dialog.FileNames)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    var destPath = Path.Combine(CurrentProject.ProjectDirectory, fileName);

                    // Copy file to project directory if it's not already there
                    if (!filePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(filePath, destPath, overwrite: false);
                    }

                    // Create and add script
                    var script = new MusicScript
                    {
                        FilePath = destPath,
                        Content = File.ReadAllText(destPath),
                        Project = CurrentProject,
                        Namespace = CurrentProject.Namespace
                    };

                    CurrentProject.Scripts.Add(script);
                    OpenScript(script);
                    StatusMessage = $"Added: {fileName}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add file '{Path.GetFileName(filePath)}': {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    [RelayCommand]
    private async Task ImportAudioAsync()
    {
        if (CurrentProject == null)
        {
            MessageBox.Show("Please open or create a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import Audio",
            Filter = "Audio Files (*.wav;*.mp3;*.ogg;*.flac)|*.wav;*.mp3;*.ogg;*.flac|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var filePath in dialog.FileNames)
            {
                try
                {
                    var alias = InputDialog.Show($"Enter alias for '{Path.GetFileName(filePath)}':",
                        "Audio Alias", Path.GetFileNameWithoutExtension(filePath),
                        Application.Current.MainWindow);

                    if (string.IsNullOrWhiteSpace(alias))
                        continue;

                    await _projectService.ImportAudioAsync(CurrentProject, filePath, alias);
                    StatusMessage = $"Imported: {Path.GetFileName(filePath)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import '{Path.GetFileName(filePath)}': {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    [RelayCommand]
    private async Task AddReferenceAsync()
    {
        if (CurrentProject == null)
        {
            MessageBox.Show("Please open or create a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Add Reference",
            Filter = "MusicEngine Project (*.meproj)|*.meproj|DLL Assembly (*.dll)|*.dll|All Files (*.*)|*.*",
            DefaultExt = ".meproj"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var alias = InputDialog.Show("Enter alias for this reference:",
                    "Reference Alias", Path.GetFileNameWithoutExtension(dialog.FileName),
                    Application.Current.MainWindow);

                if (string.IsNullOrWhiteSpace(alias))
                    return;

                var reference = new ProjectReference
                {
                    Type = dialog.FileName.EndsWith(".meproj", StringComparison.OrdinalIgnoreCase) ? "project" : "assembly",
                    Path = dialog.FileName,
                    Alias = alias
                };

                await _projectService.AddReferenceAsync(CurrentProject, reference);
                StatusMessage = $"Added reference: {alias}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add reference: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void ProjectSettings()
    {
        if (CurrentProject == null)
        {
            MessageBox.Show("Please open or create a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Simple settings dialog using MessageBox for now
        // In a full implementation, you would use a dedicated ProjectSettingsDialog
        var settings = CurrentProject.Settings;
        var message = $"Project: {CurrentProject.Name}\n" +
                      $"Namespace: {CurrentProject.Namespace}\n" +
                      $"Sample Rate: {settings.SampleRate} Hz\n" +
                      $"Buffer Size: {settings.BufferSize}\n" +
                      $"Default BPM: {settings.DefaultBpm}\n" +
                      $"Output Device: {settings.OutputDevice}\n\n" +
                      "Use a dedicated settings dialog for editing.";

        MessageBox.Show(message, "Project Settings",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var dialog = new SettingsDialog(_settingsService)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            StatusMessage = "Settings saved";
            // Apply any immediate settings changes (e.g., theme changes)
            ApplySettings();
        }
    }

    /// <summary>
    /// Applies current settings to the application
    /// </summary>
    private void ApplySettings()
    {
        var settings = _settingsService.Settings;

        // Apply audio settings if needed
        // This would be where you'd update the audio engine with new settings

        // Apply editor settings
        // Theme changes would typically require a restart or dynamic resource update

        // Update status
        StatusMessage = $"Settings applied (Theme: {settings.Editor.Theme}, Sample Rate: {settings.Audio.SampleRate} Hz)";
    }

    [RelayCommand]
    private void Debug()
    {
        // TODO: Implement debugging
    }

    [RelayCommand]
    private void Documentation()
    {
        // Open documentation URL
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/watermann420/MusicEngine",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void About()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var message = $"MusicEngine Editor\n\n" +
                      $"Version: {version}\n\n" +
                      "A visual IDE for the MusicEngine DSL.\n" +
                      "Create music programmatically with a simple, expressive syntax.\n\n" +
                      "Copyright (c) 2024 MusicEngine Project\n" +
                      "https://github.com/watermann420/MusicEngine";

        MessageBox.Show(message, "About MusicEngine Editor",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void Exit()
    {
        // Check for unsaved changes
        var unsavedDocuments = OpenDocuments.Where(d => d.IsDirty).ToList();

        if (unsavedDocuments.Count > 0 || CurrentProject?.IsDirty == true)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before exiting?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
            {
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                // Save all changes
                _ = SaveAllAsync();
            }
        }

        Application.Current.Shutdown();
    }

    public void OpenScript(MusicScript script)
    {
        // Check if already open
        foreach (var doc in OpenDocuments)
        {
            if (doc.Script?.FilePath == script.FilePath)
            {
                ActiveDocument = doc;
                return;
            }
        }

        // Create new tab
        var tab = new EditorTabViewModel(script);
        OpenDocuments.Add(tab);
        ActiveDocument = tab;
    }

    public async Task CloseDocumentAsync(EditorTabViewModel document)
    {
        if (document.IsDirty)
        {
            var result = MessageBox.Show(
                $"Do you want to save changes to '{document.Title}'?",
                "Save Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                return;
            }

            if (result == MessageBoxResult.Yes && document.Script != null)
            {
                await _projectService.SaveScriptAsync(document.Script);
                document.IsDirty = false;
            }
        }

        OpenDocuments.Remove(document);

        if (ActiveDocument == document)
        {
            ActiveDocument = OpenDocuments.Count > 0 ? OpenDocuments[0] : null;
        }
    }

    // Keep synchronous version for backward compatibility
    public void CloseDocument(EditorTabViewModel document)
    {
        _ = CloseDocumentAsync(document);
    }

    private void OnProjectLoaded(object? sender, MusicProject project)
    {
        CurrentProject = project;
        ProjectExplorer?.LoadProject(project);
        StatusMessage = $"Loaded: {project.Name}";

        // Open entry point script
        foreach (var script in project.Scripts)
        {
            if (script.IsEntryPoint)
            {
                OpenScript(script);
                break;
            }
        }
    }

    private void OnOutputReceived(object? sender, string message)
    {
        Output?.AppendLine(message);
    }

    private void OnExecutionStarted(object? sender, EventArgs e)
    {
        PlaybackStatus = "Running";
    }

    private void OnExecutionStopped(object? sender, EventArgs e)
    {
        PlaybackStatus = "Stopped";
    }
}
