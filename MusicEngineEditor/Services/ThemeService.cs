using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using MusicEngineEditor.Models;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace MusicEngineEditor.Services;

/// <summary>
/// Interface for managing application themes
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the currently applied theme
    /// </summary>
    Theme CurrentTheme { get; }

    /// <summary>
    /// Gets the currently applied theme name
    /// </summary>
    string CurrentThemeName { get; }

    /// <summary>
    /// Gets the list of available theme names
    /// </summary>
    IReadOnlyList<string> AvailableThemes { get; }

    /// <summary>
    /// Gets the collection of all available themes
    /// </summary>
    ObservableCollection<Theme> Themes { get; }

    /// <summary>
    /// Applies a theme by name
    /// </summary>
    /// <param name="themeName">The name of the theme to apply (e.g., "Dark", "Light")</param>
    void ApplyTheme(string themeName);

    /// <summary>
    /// Applies a theme object directly
    /// </summary>
    /// <param name="theme">The theme to apply</param>
    void ApplyTheme(Theme theme);

    /// <summary>
    /// Loads a custom theme from a file
    /// </summary>
    /// <param name="path">The path to the theme file</param>
    /// <returns>The loaded theme</returns>
    Theme LoadTheme(string path);

    /// <summary>
    /// Saves a theme to a file
    /// </summary>
    /// <param name="theme">The theme to save</param>
    /// <param name="path">The path to save to</param>
    void SaveTheme(Theme theme, string path);

    /// <summary>
    /// Creates a new custom theme based on an existing one
    /// </summary>
    /// <param name="basedOn">The theme to base the new theme on</param>
    /// <param name="newName">The name for the new theme</param>
    /// <returns>The new theme</returns>
    Theme CreateCustomTheme(Theme basedOn, string newName);

    /// <summary>
    /// Deletes a custom theme
    /// </summary>
    /// <param name="theme">The theme to delete</param>
    /// <returns>True if successful</returns>
    bool DeleteTheme(Theme theme);

    /// <summary>
    /// Gets the directory where custom themes are stored
    /// </summary>
    string CustomThemesDirectory { get; }

    /// <summary>
    /// Event raised when the theme changes
    /// </summary>
    event EventHandler<Theme>? ThemeChanged;
}

/// <summary>
/// Service for managing application themes with hot-swap support
/// </summary>
public class ThemeService : IThemeService
{
    private Theme _currentTheme;
    private readonly Dictionary<string, Theme> _themesMap = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public Theme CurrentTheme => _currentTheme;

    /// <inheritdoc/>
    public string CurrentThemeName => _currentTheme.Name;

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableThemes => _themesMap.Keys.ToList().AsReadOnly();

    /// <inheritdoc/>
    public ObservableCollection<Theme> Themes { get; } = [];

    /// <inheritdoc/>
    public string CustomThemesDirectory { get; }

    /// <inheritdoc/>
    public event EventHandler<Theme>? ThemeChanged;

    public ThemeService()
    {
        // Initialize custom themes directory
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicEngineEditor",
            "Themes");
        CustomThemesDirectory = appDataPath;

        // Ensure directory exists
        if (!Directory.Exists(CustomThemesDirectory))
        {
            Directory.CreateDirectory(CustomThemesDirectory);
        }

        // Initialize built-in themes
        var darkTheme = Theme.CreateDarkTheme();
        var lightTheme = Theme.CreateLightTheme();

        _themesMap["Dark"] = darkTheme;
        _themesMap["Light"] = lightTheme;
        Themes.Add(darkTheme);
        Themes.Add(lightTheme);

        // Load custom themes
        LoadCustomThemes();

        // Set default theme
        _currentTheme = darkTheme;
    }

    /// <summary>
    /// Loads all custom themes from the themes directory
    /// </summary>
    private void LoadCustomThemes()
    {
        try
        {
            foreach (var file in Directory.GetFiles(CustomThemesDirectory, "*.json"))
            {
                try
                {
                    var theme = LoadTheme(file);
                    if (!_themesMap.ContainsKey(theme.Name))
                    {
                        _themesMap[theme.Name] = theme;
                        Themes.Add(theme);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load theme from {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate custom themes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void ApplyTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            throw new ArgumentException("Theme name cannot be null or empty", nameof(themeName));
        }

        if (!_themesMap.TryGetValue(themeName, out var theme))
        {
            throw new ArgumentException(
                $"Theme '{themeName}' is not available. Available themes: {string.Join(", ", AvailableThemes)}",
                nameof(themeName));
        }

        ApplyTheme(theme);
    }

    /// <inheritdoc/>
    public void ApplyTheme(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (_currentTheme.Name == theme.Name && ReferenceEquals(_currentTheme, theme))
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

            // Determine which base theme to use
            var baseThemeName = theme.IsDark ? "Dark" : "Light";

            // Add new base theme
            var newTheme = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Themes/{baseThemeName}Theme.xaml")
            };
            app.Resources.MergedDictionaries.Add(newTheme);

            // Apply custom colors if this is not a built-in theme
            if (!theme.IsBuiltIn)
            {
                ApplyCustomColors(app, theme);
            }

            _currentTheme = theme;

            // Notify listeners
            ThemeChanged?.Invoke(this, theme);

            System.Diagnostics.Debug.WriteLine($"Theme changed to: {theme.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme '{theme.Name}': {ex.Message}");
            throw new InvalidOperationException($"Failed to apply theme '{theme.Name}'", ex);
        }
    }

    /// <summary>
    /// Applies custom colors from a theme to the application resources
    /// </summary>
    private static void ApplyCustomColors(Application app, Theme theme)
    {
        var colors = theme.Colors;

        // Helper to set a color resource
        void SetColor(string key, string hexColor)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                app.Resources[key] = color;
            }
            catch
            {
                // Ignore invalid colors
            }
        }

        // Background Colors
        SetColor("BackgroundColor", colors.Background);
        SetColor("EditorBackgroundColor", colors.EditorBackground);
        SetColor("PanelBackgroundColor", colors.PanelBackground);
        SetColor("ToolbarBackgroundColor", colors.ToolbarBackground);
        SetColor("MenuBackgroundColor", colors.MenuBackground);
        SetColor("StatusBarBackgroundColor", colors.StatusBarBackground);
        SetColor("HeaderBackgroundColor", colors.HeaderBackground);
        SetColor("HoverBackgroundColor", colors.HoverBackground);
        SetColor("SelectedBackgroundColor", colors.SelectedBackground);
        SetColor("InputBackgroundColor", colors.InputBackground);

        // Border Colors
        SetColor("BorderColor", colors.Border);
        SetColor("SplitterColor", colors.Splitter);
        SetColor("SubtleBorderColor", colors.SubtleBorder);

        // Text Colors
        SetColor("ForegroundColor", colors.Foreground);
        SetColor("SecondaryForegroundColor", colors.SecondaryForeground);
        SetColor("DisabledForegroundColor", colors.DisabledForeground);
        SetColor("BrightForegroundColor", colors.BrightForeground);

        // Accent Colors
        SetColor("AccentColor", colors.Accent);
        SetColor("AccentHoverColor", colors.AccentHover);
        SetColor("AccentPressedColor", colors.AccentPressed);
        SetColor("LinkColor", colors.Link);

        // Status Colors
        SetColor("SuccessColor", colors.Success);
        SetColor("ErrorColor", colors.Error);
        SetColor("WarningColor", colors.Warning);
        SetColor("InfoColor", colors.Info);

        // Special Colors
        SetColor("RunButtonColor", colors.RunButton);
        SetColor("RunButtonHoverColor", colors.RunButtonHover);
        SetColor("StopButtonColor", colors.StopButton);
        SetColor("StopButtonHoverColor", colors.StopButtonHover);
    }

    /// <inheritdoc/>
    public Theme LoadTheme(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Theme file not found", path);
        }

        var json = File.ReadAllText(path);
        var theme = JsonSerializer.Deserialize<Theme>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize theme");

        theme.IsBuiltIn = false;
        return theme;
    }

    /// <inheritdoc/>
    public void SaveTheme(Theme theme, string path)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(theme, JsonOptions);
        File.WriteAllText(path, json);

        System.Diagnostics.Debug.WriteLine($"Theme saved to: {path}");
    }

    /// <inheritdoc/>
    public Theme CreateCustomTheme(Theme basedOn, string newName)
    {
        ArgumentNullException.ThrowIfNull(basedOn);

        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("New theme name cannot be null or empty", nameof(newName));
        }

        if (_themesMap.ContainsKey(newName))
        {
            throw new ArgumentException($"A theme with name '{newName}' already exists", nameof(newName));
        }

        var newTheme = basedOn.Clone();
        newTheme.Name = newName;
        newTheme.IsBuiltIn = false;

        // Save to disk
        var path = Path.Combine(CustomThemesDirectory, $"{SanitizeFileName(newName)}.json");
        SaveTheme(newTheme, path);

        // Add to collections
        _themesMap[newName] = newTheme;
        Themes.Add(newTheme);

        return newTheme;
    }

    /// <inheritdoc/>
    public bool DeleteTheme(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (theme.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot delete built-in themes");
        }

        if (!_themesMap.ContainsKey(theme.Name))
        {
            return false;
        }

        // If this is the current theme, switch to dark theme
        if (_currentTheme.Name == theme.Name)
        {
            ApplyTheme("Dark");
        }

        // Remove from collections
        _themesMap.Remove(theme.Name);
        Themes.Remove(theme);

        // Delete file
        var path = Path.Combine(CustomThemesDirectory, $"{SanitizeFileName(theme.Name)}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return true;
    }

    /// <summary>
    /// Sanitizes a string for use as a file name
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }
}
