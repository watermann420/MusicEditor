// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI theme configuration model.

using System.Text.Json.Serialization;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a theme configuration for the application
/// </summary>
public class Theme
{
    /// <summary>
    /// Gets or sets the unique name of the theme
    /// </summary>
    public string Name { get; set; } = "Custom";

    /// <summary>
    /// Gets or sets whether this is a dark theme
    /// </summary>
    public bool IsDark { get; set; } = true;

    /// <summary>
    /// Gets or sets the font family for the UI
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>
    /// Gets or sets the base font size for the UI
    /// </summary>
    public double FontSize { get; set; } = 12;

    /// <summary>
    /// Gets or sets the editor font family
    /// </summary>
    public string EditorFontFamily { get; set; } = "JetBrains Mono, Cascadia Code, Consolas";

    /// <summary>
    /// Gets or sets the editor font size
    /// </summary>
    public double EditorFontSize { get; set; } = 14;

    /// <summary>
    /// Gets or sets the theme colors dictionary
    /// </summary>
    public ThemeColors Colors { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this is a built-in theme (cannot be deleted or modified)
    /// </summary>
    [JsonIgnore]
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Creates a deep clone of this theme
    /// </summary>
    public Theme Clone()
    {
        return new Theme
        {
            Name = Name,
            IsDark = IsDark,
            FontFamily = FontFamily,
            FontSize = FontSize,
            EditorFontFamily = EditorFontFamily,
            EditorFontSize = EditorFontSize,
            IsBuiltIn = false, // Clones are never built-in
            Colors = Colors.Clone()
        };
    }

    /// <summary>
    /// Creates the default dark theme
    /// </summary>
    public static Theme CreateDarkTheme()
    {
        return new Theme
        {
            Name = "Dark",
            IsDark = true,
            IsBuiltIn = true,
            Colors = ThemeColors.CreateDarkColors()
        };
    }

    /// <summary>
    /// Creates the default light theme
    /// </summary>
    public static Theme CreateLightTheme()
    {
        return new Theme
        {
            Name = "Light",
            IsDark = false,
            IsBuiltIn = true,
            Colors = ThemeColors.CreateLightColors()
        };
    }
}

/// <summary>
/// Contains all color definitions for a theme
/// </summary>
public class ThemeColors
{
    // Background Colors
    public string Background { get; set; } = "#1E1F22";
    public string EditorBackground { get; set; } = "#1E1F22";
    public string PanelBackground { get; set; } = "#2B2D30";
    public string ToolbarBackground { get; set; } = "#2B2D30";
    public string MenuBackground { get; set; } = "#2B2D30";
    public string StatusBarBackground { get; set; } = "#2B2D30";
    public string HeaderBackground { get; set; } = "#1E1F22";
    public string HoverBackground { get; set; } = "#00D9FF33";
    public string SelectedBackground { get; set; } = "#00D9FF";
    public string InputBackground { get; set; } = "#1E1F22";

    // Border Colors
    public string Border { get; set; } = "#393B40";
    public string Splitter { get; set; } = "#2B2D30";
    public string SubtleBorder { get; set; } = "#43454A";

    // Text Colors
    public string Foreground { get; set; } = "#BCBEC4";
    public string SecondaryForeground { get; set; } = "#6F737A";
    public string DisabledForeground { get; set; } = "#5C5E63";
    public string BrightForeground { get; set; } = "#DFE1E5";

    // Accent Colors
    public string Accent { get; set; } = "#00D9FF";
    public string AccentHover { get; set; } = "#5A7FC4";
    public string AccentPressed { get; set; } = "#3D5A91";
    public string Link { get; set; } = "#589DF6";

    // Status Colors
    public string Success { get; set; } = "#00FF88";
    public string Error { get; set; } = "#F75464";
    public string Warning { get; set; } = "#FFB800";
    public string Info { get; set; } = "#00D9FF";

    // Special Colors
    public string RunButton { get; set; } = "#499C54";
    public string RunButtonHover { get; set; } = "#5AAF65";
    public string StopButton { get; set; } = "#C75450";
    public string StopButtonHover { get; set; } = "#D96560";

    /// <summary>
    /// Creates a deep clone of this colors configuration
    /// </summary>
    public ThemeColors Clone()
    {
        return new ThemeColors
        {
            Background = Background,
            EditorBackground = EditorBackground,
            PanelBackground = PanelBackground,
            ToolbarBackground = ToolbarBackground,
            MenuBackground = MenuBackground,
            StatusBarBackground = StatusBarBackground,
            HeaderBackground = HeaderBackground,
            HoverBackground = HoverBackground,
            SelectedBackground = SelectedBackground,
            InputBackground = InputBackground,
            Border = Border,
            Splitter = Splitter,
            SubtleBorder = SubtleBorder,
            Foreground = Foreground,
            SecondaryForeground = SecondaryForeground,
            DisabledForeground = DisabledForeground,
            BrightForeground = BrightForeground,
            Accent = Accent,
            AccentHover = AccentHover,
            AccentPressed = AccentPressed,
            Link = Link,
            Success = Success,
            Error = Error,
            Warning = Warning,
            Info = Info,
            RunButton = RunButton,
            RunButtonHover = RunButtonHover,
            StopButton = StopButton,
            StopButtonHover = StopButtonHover
        };
    }

    /// <summary>
    /// Creates the default dark color scheme
    /// </summary>
    public static ThemeColors CreateDarkColors()
    {
        return new ThemeColors
        {
            // Already defaults to dark colors
        };
    }

    /// <summary>
    /// Creates the default light color scheme
    /// </summary>
    public static ThemeColors CreateLightColors()
    {
        return new ThemeColors
        {
            // Background Colors
            Background = "#F5F5F5",
            EditorBackground = "#FFFFFF",
            PanelBackground = "#EBEBEB",
            ToolbarBackground = "#F0F0F0",
            MenuBackground = "#F5F5F5",
            StatusBarBackground = "#E8E8E8",
            HeaderBackground = "#E0E0E0",
            HoverBackground = "#CCE8FF",
            SelectedBackground = "#0078D4",
            InputBackground = "#FFFFFF",

            // Border Colors
            Border = "#CCCCCC",
            Splitter = "#D0D0D0",
            SubtleBorder = "#BBBBBB",

            // Text Colors
            Foreground = "#1E1E1E",
            SecondaryForeground = "#666666",
            DisabledForeground = "#999999",
            BrightForeground = "#000000",

            // Accent Colors
            Accent = "#0078D4",
            AccentHover = "#1084D9",
            AccentPressed = "#006CBD",
            Link = "#0066CC",

            // Status Colors
            Success = "#107C10",
            Error = "#D13438",
            Warning = "#CA5010",
            Info = "#0078D4",

            // Special Colors
            RunButton = "#107C10",
            RunButtonHover = "#1E8C1E",
            StopButton = "#D13438",
            StopButtonHover = "#E03C40"
        };
    }
}
