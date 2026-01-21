using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MusicEngineEditor.Services;

/// <summary>
/// Interface for managing application themes
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the currently applied theme name
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Gets the list of available theme names
    /// </summary>
    IReadOnlyList<string> AvailableThemes { get; }

    /// <summary>
    /// Applies a theme by name
    /// </summary>
    /// <param name="themeName">The name of the theme to apply (e.g., "Dark", "Light")</param>
    void ApplyTheme(string themeName);

    /// <summary>
    /// Event raised when the theme changes
    /// </summary>
    event EventHandler<string>? ThemeChanged;
}

/// <summary>
/// Service for managing application themes with hot-swap support
/// </summary>
public class ThemeService : IThemeService
{
    private string _currentTheme = "Dark";
    private readonly List<string> _availableThemes = new() { "Dark", "Light" };

    /// <inheritdoc/>
    public string CurrentTheme => _currentTheme;

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableThemes => _availableThemes.AsReadOnly();

    /// <inheritdoc/>
    public event EventHandler<string>? ThemeChanged;

    /// <inheritdoc/>
    public void ApplyTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            throw new ArgumentException("Theme name cannot be null or empty", nameof(themeName));
        }

        if (!_availableThemes.Contains(themeName))
        {
            throw new ArgumentException($"Theme '{themeName}' is not available. Available themes: {string.Join(", ", _availableThemes)}", nameof(themeName));
        }

        if (_currentTheme == themeName)
        {
            return; // Theme already applied
        }

        var app = Application.Current;
        if (app == null)
        {
            throw new InvalidOperationException("Application.Current is null. Cannot apply theme.");
        }

        try
        {
            // Remove old theme dictionary
            var oldTheme = app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.ToString().Contains("Theme") == true);

            if (oldTheme != null)
            {
                app.Resources.MergedDictionaries.Remove(oldTheme);
            }

            // Add new theme
            var newTheme = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Themes/{themeName}Theme.xaml")
            };
            app.Resources.MergedDictionaries.Add(newTheme);

            _currentTheme = themeName;

            // Notify listeners
            ThemeChanged?.Invoke(this, themeName);

            System.Diagnostics.Debug.WriteLine($"Theme changed to: {themeName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme '{themeName}': {ex.Message}");
            throw new InvalidOperationException($"Failed to apply theme '{themeName}'", ex);
        }
    }
}
