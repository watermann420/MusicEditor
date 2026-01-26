// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: MusicEngineEditor component.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing MusicEngine projects
/// </summary>
public interface IProjectService
{
    MusicProject? CurrentProject { get; }

    /// <summary>
    /// Gets the list of recently opened projects.
    /// </summary>
    IReadOnlyList<RecentProjectEntry> RecentProjects { get; }

    event EventHandler<MusicProject>? ProjectLoaded;
    event EventHandler? ProjectClosed;

    Task<MusicProject> CreateProjectAsync(string name, string path);
    Task<MusicProject> OpenProjectAsync(string projectFilePath);
    Task SaveProjectAsync(MusicProject project);
    Task CloseProjectAsync();

    MusicScript CreateScript(MusicProject project, string name, string? folder = null);
    Task SaveScriptAsync(MusicScript script);
    Task DeleteScriptAsync(MusicScript script);

    Task<AudioAsset> ImportAudioAsync(MusicProject project, string sourcePath, string alias, string category = "General");
    Task DeleteAudioAssetAsync(AudioAsset asset);

    Task AddReferenceAsync(MusicProject project, ProjectReference reference);
    Task RemoveReferenceAsync(MusicProject project, ProjectReference reference);

    /// <summary>
    /// Adds a project to the recent projects list.
    /// </summary>
    /// <param name="path">Full path to the project file.</param>
    void AddToRecentProjects(string path);

    /// <summary>
    /// Clears all recent projects.
    /// </summary>
    void ClearRecentProjects();
}
