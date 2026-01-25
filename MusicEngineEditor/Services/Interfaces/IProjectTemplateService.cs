// MusicEngineEditor - Project Template Service Interface
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing project templates.
/// </summary>
public interface IProjectTemplateService
{
    /// <summary>
    /// Gets all loaded templates.
    /// </summary>
    IReadOnlyList<ProjectTemplate> Templates { get; }

    /// <summary>
    /// Gets the built-in templates.
    /// </summary>
    IReadOnlyList<ProjectTemplate> BuiltInTemplates { get; }

    /// <summary>
    /// Gets user-created templates.
    /// </summary>
    IReadOnlyList<ProjectTemplate> UserTemplates { get; }

    /// <summary>
    /// Event raised when templates are reloaded.
    /// </summary>
    event EventHandler? TemplatesChanged;

    /// <summary>
    /// Initializes the service and loads all templates.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Loads all templates from the templates folder.
    /// </summary>
    Task LoadTemplatesAsync();

    /// <summary>
    /// Gets templates filtered by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Templates matching the category.</returns>
    IReadOnlyList<ProjectTemplate> GetTemplatesByCategory(TemplateCategory category);

    /// <summary>
    /// Searches templates by name or description.
    /// </summary>
    /// <param name="searchText">Text to search for.</param>
    /// <returns>Matching templates.</returns>
    IReadOnlyList<ProjectTemplate> SearchTemplates(string searchText);

    /// <summary>
    /// Gets a template by its ID.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <returns>The template, or null if not found.</returns>
    ProjectTemplate? GetTemplateById(Guid id);

    /// <summary>
    /// Creates a new MusicProject from a template.
    /// </summary>
    /// <param name="template">The template to use.</param>
    /// <param name="projectName">Name for the new project.</param>
    /// <param name="projectPath">Path where the project will be created.</param>
    /// <returns>The created project.</returns>
    MusicProject CreateProjectFromTemplate(ProjectTemplate template, string projectName, string projectPath);

    /// <summary>
    /// Saves a project as a new template.
    /// </summary>
    /// <param name="project">The project to save as template.</param>
    /// <param name="templateName">Name for the template.</param>
    /// <param name="category">Category for the template.</param>
    /// <param name="description">Description for the template.</param>
    /// <returns>The created template.</returns>
    Task<ProjectTemplate> SaveAsTemplateAsync(
        MusicProject project,
        string templateName,
        TemplateCategory category,
        string description = "");

    /// <summary>
    /// Saves a template to disk.
    /// </summary>
    /// <param name="template">The template to save.</param>
    Task SaveTemplateAsync(ProjectTemplate template);

    /// <summary>
    /// Deletes a user template.
    /// </summary>
    /// <param name="template">The template to delete.</param>
    /// <returns>True if deleted successfully.</returns>
    bool DeleteTemplate(ProjectTemplate template);
}
