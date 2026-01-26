// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Data model class.

using System.Windows.Input;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a command in the command palette.
/// </summary>
public class PaletteCommand
{
    /// <summary>
    /// Gets or sets the display name of the command.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category (File, Edit, View, Transport, etc.).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the keyboard shortcut display text.
    /// </summary>
    public string? Shortcut { get; set; }

    /// <summary>
    /// Gets or sets the command to execute.
    /// </summary>
    public ICommand? Command { get; set; }

    /// <summary>
    /// Gets or sets the command parameter.
    /// </summary>
    public object? CommandParameter { get; set; }

    /// <summary>
    /// Gets or sets a description of what the command does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets search keywords for better matching.
    /// </summary>
    public string[]? Keywords { get; set; }

    /// <summary>
    /// Gets the formatted display text including category.
    /// </summary>
    public string DisplayText => string.IsNullOrEmpty(Category) ? Name : $"{Category}: {Name}";

    /// <summary>
    /// Gets whether the command can be executed.
    /// </summary>
    public bool CanExecute => Command?.CanExecute(CommandParameter) ?? false;

    /// <summary>
    /// Executes the command.
    /// </summary>
    public void Execute()
    {
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }

    /// <summary>
    /// Checks if this command matches the search query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>True if the command matches the query.</returns>
    public bool MatchesQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var lowerQuery = query.ToLowerInvariant();

        // Check name
        if (Name.ToLowerInvariant().Contains(lowerQuery))
            return true;

        // Check category
        if (Category.ToLowerInvariant().Contains(lowerQuery))
            return true;

        // Check description
        if (Description?.ToLowerInvariant().Contains(lowerQuery) == true)
            return true;

        // Check keywords
        if (Keywords != null)
        {
            foreach (var keyword in Keywords)
            {
                if (keyword.ToLowerInvariant().Contains(lowerQuery))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculates a relevance score for ranking search results.
    /// Higher score = more relevant.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>The relevance score.</returns>
    public int GetRelevanceScore(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 0;

        var lowerQuery = query.ToLowerInvariant();
        int score = 0;

        // Exact name match (highest priority)
        if (Name.Equals(query, System.StringComparison.OrdinalIgnoreCase))
            score += 100;
        // Name starts with query
        else if (Name.StartsWith(query, System.StringComparison.OrdinalIgnoreCase))
            score += 50;
        // Name contains query
        else if (Name.ToLowerInvariant().Contains(lowerQuery))
            score += 25;

        // Category match
        if (Category.ToLowerInvariant().Contains(lowerQuery))
            score += 10;

        // Keyword match
        if (Keywords != null)
        {
            foreach (var keyword in Keywords)
            {
                if (keyword.Equals(query, System.StringComparison.OrdinalIgnoreCase))
                    score += 30;
                else if (keyword.ToLowerInvariant().Contains(lowerQuery))
                    score += 5;
            }
        }

        return score;
    }
}
