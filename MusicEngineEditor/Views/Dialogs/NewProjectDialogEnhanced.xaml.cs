// MusicEngineEditor - Enhanced New Project Dialog
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Enhanced new project dialog with template gallery and preview.
/// </summary>
public partial class NewProjectDialogEnhanced : Window, INotifyPropertyChanged
{
    private readonly ProjectTemplateService _templateService;

    private string _projectName = "MyMusicProject";
    private string _projectLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _namespace = "MyMusicProject";
    private string _searchText = string.Empty;
    private ProjectTemplate? _selectedTemplate;
    private TemplateCategory? _selectedCategory;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (_projectName != value)
            {
                _projectName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullProjectPath));
                OnPropertyChanged(nameof(CanCreate));

                // Auto-update namespace
                if (string.IsNullOrWhiteSpace(Namespace) || Namespace == "MyMusicProject" ||
                    Namespace == SanitizeNamespace(_projectName.Replace(value.LastOrDefault().ToString(), "")))
                {
                    Namespace = SanitizeNamespace(value);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the project location.
    /// </summary>
    public string ProjectLocation
    {
        get => _projectLocation;
        set
        {
            if (_projectLocation != value)
            {
                _projectLocation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullProjectPath));
                OnPropertyChanged(nameof(CanCreate));
            }
        }
    }

    /// <summary>
    /// Gets or sets the namespace.
    /// </summary>
    public string Namespace
    {
        get => _namespace;
        set
        {
            if (_namespace != value)
            {
                _namespace = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the search text for filtering templates.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredTemplates));
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected template.
    /// </summary>
    public ProjectTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (_selectedTemplate != value)
            {
                _selectedTemplate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedTemplate));
            }
        }
    }

    /// <summary>
    /// Gets whether a template is selected.
    /// </summary>
    public bool HasSelectedTemplate => SelectedTemplate != null;

    /// <summary>
    /// Gets the full project path preview.
    /// </summary>
    public string FullProjectPath =>
        Path.Combine(ProjectLocation, ProjectName, $"{ProjectName}.meproj");

    /// <summary>
    /// Gets whether the create button should be enabled.
    /// </summary>
    public bool CanCreate =>
        !string.IsNullOrWhiteSpace(ProjectName) &&
        !string.IsNullOrWhiteSpace(ProjectLocation) &&
        Directory.Exists(ProjectLocation);

    /// <summary>
    /// Gets the filtered templates based on search and category.
    /// </summary>
    public ObservableCollection<ProjectTemplate> FilteredTemplates
    {
        get
        {
            var templates = _templateService.Templates.AsEnumerable();

            // Filter by category
            if (_selectedCategory.HasValue)
            {
                templates = templates.Where(t => t.Category == _selectedCategory.Value);
            }

            // Filter by search
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                templates = templates.Where(t =>
                    t.TemplateName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    t.Tags.Any(tag => tag.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
            }

            return new ObservableCollection<ProjectTemplate>(templates);
        }
    }

    /// <summary>
    /// Gets the recent templates.
    /// </summary>
    public ObservableCollection<ProjectTemplate> RecentTemplates { get; } = new();

    #endregion

    #region Category Selection Properties

    public bool IsAllCategorySelected
    {
        get => !_selectedCategory.HasValue;
        set { if (value) { _selectedCategory = null; RefreshTemplates(); } }
    }

    public bool IsEmptyCategorySelected
    {
        get => _selectedCategory == TemplateCategory.Empty;
        set { if (value) { _selectedCategory = TemplateCategory.Empty; RefreshTemplates(); } }
    }

    public bool IsElectronicCategorySelected
    {
        get => _selectedCategory == TemplateCategory.Electronic;
        set { if (value) { _selectedCategory = TemplateCategory.Electronic; RefreshTemplates(); } }
    }

    public bool IsBandCategorySelected
    {
        get => _selectedCategory == TemplateCategory.Band;
        set { if (value) { _selectedCategory = TemplateCategory.Band; RefreshTemplates(); } }
    }

    public bool IsOrchestralCategorySelected
    {
        get => _selectedCategory == TemplateCategory.Orchestral;
        set { if (value) { _selectedCategory = TemplateCategory.Orchestral; RefreshTemplates(); } }
    }

    public bool IsCustomCategorySelected
    {
        get => _selectedCategory == TemplateCategory.Custom;
        set { if (value) { _selectedCategory = TemplateCategory.Custom; RefreshTemplates(); } }
    }

    #endregion

    /// <summary>
    /// Creates a new NewProjectDialogEnhanced.
    /// </summary>
    public NewProjectDialogEnhanced()
    {
        InitializeComponent();

        _templateService = ProjectTemplateService.Instance;

        // Initialize the service if not already done
        _ = _templateService.InitializeAsync();

        DataContext = this;

        // Set default template selection
        Loaded += async (s, e) =>
        {
            await _templateService.InitializeAsync();
            OnPropertyChanged(nameof(FilteredTemplates));

            // Select the first template by default
            var firstTemplate = FilteredTemplates.FirstOrDefault();
            if (firstTemplate != null)
            {
                SelectedTemplate = firstTemplate;
            }

            ProjectNameBox?.Focus();
            ProjectNameBox?.SelectAll();
        };
    }

    /// <summary>
    /// Gets the created project after dialog closes.
    /// </summary>
    public MusicProject? CreatedProject { get; private set; }

    private void RefreshTemplates()
    {
        OnPropertyChanged(nameof(FilteredTemplates));
        OnPropertyChanged(nameof(IsAllCategorySelected));
        OnPropertyChanged(nameof(IsEmptyCategorySelected));
        OnPropertyChanged(nameof(IsElectronicCategorySelected));
        OnPropertyChanged(nameof(IsBandCategorySelected));
        OnPropertyChanged(nameof(IsOrchestralCategorySelected));
        OnPropertyChanged(nameof(IsCustomCategorySelected));
    }

    private void TemplateCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ProjectTemplate template)
        {
            SelectedTemplate = template;
        }
    }

    private void RecentTemplate_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ProjectTemplate template)
        {
            SelectedTemplate = template;
            OnPropertyChanged(nameof(FilteredTemplates));
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select project location",
            SelectedPath = ProjectLocation ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ProjectLocation = dialog.SelectedPath;
        }
    }

    private void CreateEmpty_Click(object sender, RoutedEventArgs e)
    {
        // Select the empty template
        var emptyTemplate = _templateService.Templates.FirstOrDefault(t => t.Category == TemplateCategory.Empty);
        if (emptyTemplate != null)
        {
            SelectedTemplate = emptyTemplate;
        }

        CreateProject();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        CreateProject();
    }

    private void CreateProject()
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show("Please enter a project name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectLocation) || !Directory.Exists(ProjectLocation))
        {
            MessageBox.Show("Please select a valid location.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if project already exists
        var projectDir = Path.Combine(ProjectLocation, ProjectName);
        if (Directory.Exists(projectDir))
        {
            var result = MessageBox.Show(
                $"A folder named '{ProjectName}' already exists. Do you want to overwrite it?",
                "Project Exists",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        // Update namespace if still default
        if (string.IsNullOrWhiteSpace(Namespace) || Namespace == "MyMusicProject")
        {
            Namespace = SanitizeNamespace(ProjectName);
        }

        // Create project from template
        var template = SelectedTemplate ?? _templateService.Templates.FirstOrDefault(t => t.Category == TemplateCategory.Empty);
        if (template != null)
        {
            CreatedProject = _templateService.CreateProjectFromTemplate(template, ProjectName, ProjectLocation);
            CreatedProject.Namespace = Namespace;
        }

        DialogResult = true;
        Close();
    }

    private static string SanitizeNamespace(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "MusicProject";

        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
        }
        return result.Length > 0 ? result.ToString() : "MusicProject";
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Converter that converts a string color (e.g., "#FF5722") to a WPF Color.
/// </summary>
public class StringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorString)
        {
            try
            {
                return (Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                return Colors.DodgerBlue;
            }
        }
        return Colors.DodgerBlue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
