using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Theme Settings dialog
/// </summary>
public partial class ThemeSettingsViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private Theme? _originalTheme;
    private Theme? _workingCopy;

    // Theme selection
    [ObservableProperty]
    private Theme? _selectedTheme;

    [ObservableProperty]
    private bool _isCustomThemeSelected;

    [ObservableProperty]
    private bool _canDeleteSelectedTheme;

    // Theme properties (bound to working copy)
    [ObservableProperty]
    private string _themeName = string.Empty;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private double _fontSize = 12;

    [ObservableProperty]
    private string _editorFontFamily = "JetBrains Mono";

    [ObservableProperty]
    private double _editorFontSize = 14;

    // Color collections
    public ObservableCollection<ColorItem> BackgroundColors { get; } = [];
    public ObservableCollection<ColorItem> TextColors { get; } = [];
    public ObservableCollection<ColorItem> AccentColors { get; } = [];

    // Available options
    public ObservableCollection<Theme> Themes { get; } = [];
    public ObservableCollection<string> AvailableFonts { get; } = [];
    public ObservableCollection<string> AvailableMonoFonts { get; } = [];
    public ObservableCollection<double> AvailableFontSizes { get; } = [];
    public ObservableCollection<double> AvailableEditorFontSizes { get; } = [];

    public string CustomThemesDirectory => _themeService.CustomThemesDirectory;

    // Computed property for light theme radio button
    public bool IsLightTheme
    {
        get => !IsDarkTheme;
        set => IsDarkTheme = !value;
    }

    // Events
    public event EventHandler? OkRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler<ColorPickerEventArgs>? ColorPickerRequested;

    public ThemeSettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        InitializeCollections();
    }

    private void InitializeCollections()
    {
        // UI Fonts
        var commonFonts = new[]
        {
            "Segoe UI", "Arial", "Verdana", "Tahoma", "Calibri",
            "Trebuchet MS", "Georgia", "Times New Roman"
        };
        foreach (var font in commonFonts)
        {
            AvailableFonts.Add(font);
        }

        // Monospace fonts for editor
        var monoFonts = new[]
        {
            "JetBrains Mono", "Cascadia Code", "Consolas", "Fira Code",
            "Source Code Pro", "Roboto Mono", "Monaco", "Courier New"
        };
        foreach (var font in monoFonts)
        {
            AvailableMonoFonts.Add(font);
        }

        // Font sizes
        var uiFontSizes = new double[] { 10, 11, 12, 13, 14, 15, 16 };
        foreach (var size in uiFontSizes)
        {
            AvailableFontSizes.Add(size);
        }

        var editorFontSizes = new double[] { 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24 };
        foreach (var size in editorFontSizes)
        {
            AvailableEditorFontSizes.Add(size);
        }
    }

    /// <summary>
    /// Initializes the ViewModel with data from the theme service
    /// </summary>
    public void Initialize()
    {
        // Copy themes from service
        Themes.Clear();
        foreach (var theme in _themeService.Themes)
        {
            Themes.Add(theme);
        }

        // Store original theme for cancel
        _originalTheme = _themeService.CurrentTheme;

        // Select current theme
        SelectedTheme = Themes.FirstOrDefault(t => t.Name == _originalTheme.Name) ?? Themes.FirstOrDefault();
    }

    partial void OnSelectedThemeChanged(Theme? value)
    {
        if (value == null)
        {
            IsCustomThemeSelected = false;
            CanDeleteSelectedTheme = false;
            ClearColorCollections();
            return;
        }

        IsCustomThemeSelected = !value.IsBuiltIn;
        CanDeleteSelectedTheme = !value.IsBuiltIn;

        // Create working copy
        _workingCopy = value.Clone();
        _workingCopy.Name = value.Name; // Keep original name for reference
        _workingCopy.IsBuiltIn = value.IsBuiltIn;

        // Update bound properties
        ThemeName = value.Name;
        IsDarkTheme = value.IsDark;
        FontFamily = value.FontFamily;
        FontSize = value.FontSize;
        EditorFontFamily = value.EditorFontFamily;
        EditorFontSize = value.EditorFontSize;

        // Populate color collections
        PopulateColorCollections(value.Colors);
    }

    partial void OnThemeNameChanged(string value)
    {
        if (_workingCopy != null && !_workingCopy.IsBuiltIn)
        {
            _workingCopy.Name = value;
        }
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (_workingCopy != null && !_workingCopy.IsBuiltIn)
        {
            _workingCopy.IsDark = value;
        }
        OnPropertyChanged(nameof(IsLightTheme));
    }

    partial void OnFontFamilyChanged(string value)
    {
        if (_workingCopy != null && !_workingCopy.IsBuiltIn)
        {
            _workingCopy.FontFamily = value;
        }
    }

    partial void OnFontSizeChanged(double value)
    {
        if (_workingCopy != null && !_workingCopy.IsBuiltIn)
        {
            _workingCopy.FontSize = value;
        }
    }

    partial void OnEditorFontFamilyChanged(string value)
    {
        if (_workingCopy != null && !_workingCopy.IsBuiltIn)
        {
            _workingCopy.EditorFontFamily = value;
        }
    }

    partial void OnEditorFontSizeChanged(double value)
    {
        if (_workingCopy != null && !_workingCopy.IsBuiltIn)
        {
            _workingCopy.EditorFontSize = value;
        }
    }

    private void ClearColorCollections()
    {
        BackgroundColors.Clear();
        TextColors.Clear();
        AccentColors.Clear();
    }

    private void PopulateColorCollections(ThemeColors colors)
    {
        ClearColorCollections();

        // Background colors
        BackgroundColors.Add(new ColorItem("Background", colors.Background, c => colors.Background = c));
        BackgroundColors.Add(new ColorItem("Editor Background", colors.EditorBackground, c => colors.EditorBackground = c));
        BackgroundColors.Add(new ColorItem("Panel Background", colors.PanelBackground, c => colors.PanelBackground = c));
        BackgroundColors.Add(new ColorItem("Toolbar", colors.ToolbarBackground, c => colors.ToolbarBackground = c));
        BackgroundColors.Add(new ColorItem("Menu", colors.MenuBackground, c => colors.MenuBackground = c));
        BackgroundColors.Add(new ColorItem("Status Bar", colors.StatusBarBackground, c => colors.StatusBarBackground = c));
        BackgroundColors.Add(new ColorItem("Input", colors.InputBackground, c => colors.InputBackground = c));
        BackgroundColors.Add(new ColorItem("Border", colors.Border, c => colors.Border = c));

        // Text colors
        TextColors.Add(new ColorItem("Foreground", colors.Foreground, c => colors.Foreground = c));
        TextColors.Add(new ColorItem("Secondary", colors.SecondaryForeground, c => colors.SecondaryForeground = c));
        TextColors.Add(new ColorItem("Disabled", colors.DisabledForeground, c => colors.DisabledForeground = c));
        TextColors.Add(new ColorItem("Bright", colors.BrightForeground, c => colors.BrightForeground = c));

        // Accent colors
        AccentColors.Add(new ColorItem("Accent", colors.Accent, c => colors.Accent = c));
        AccentColors.Add(new ColorItem("Accent Hover", colors.AccentHover, c => colors.AccentHover = c));
        AccentColors.Add(new ColorItem("Accent Pressed", colors.AccentPressed, c => colors.AccentPressed = c));
        AccentColors.Add(new ColorItem("Link", colors.Link, c => colors.Link = c));
        AccentColors.Add(new ColorItem("Success", colors.Success, c => colors.Success = c));
        AccentColors.Add(new ColorItem("Warning", colors.Warning, c => colors.Warning = c));
        AccentColors.Add(new ColorItem("Error", colors.Error, c => colors.Error = c));
        AccentColors.Add(new ColorItem("Run Button", colors.RunButton, c => colors.RunButton = c));
        AccentColors.Add(new ColorItem("Stop Button", colors.StopButton, c => colors.StopButton = c));
    }

    [RelayCommand]
    private void PickColor(ColorItem? colorItem)
    {
        if (colorItem == null || !IsCustomThemeSelected) return;

        var args = new ColorPickerEventArgs
        {
            CurrentColor = colorItem.ColorValue
        };

        ColorPickerRequested?.Invoke(this, args);

        if (args.Handled)
        {
            colorItem.ColorValue = args.SelectedColor;
            if (_workingCopy != null)
            {
                colorItem.UpdateSource(colorItem.HexValue);
            }
        }
    }

    [RelayCommand]
    private void CreateTheme()
    {
        var baseName = SelectedTheme?.Name ?? "Custom";
        var newName = GenerateUniqueName($"{baseName} Copy");

        try
        {
            var baseTheme = SelectedTheme ?? Theme.CreateDarkTheme();
            var newTheme = _themeService.CreateCustomTheme(baseTheme, newName);
            Themes.Add(newTheme);
            SelectedTheme = newTheme;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create theme: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DuplicateTheme()
    {
        if (SelectedTheme == null) return;

        var newName = GenerateUniqueName($"{SelectedTheme.Name} Copy");

        try
        {
            var newTheme = _themeService.CreateCustomTheme(SelectedTheme, newName);
            Themes.Add(newTheme);
            SelectedTheme = newTheme;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to duplicate theme: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DeleteTheme()
    {
        if (SelectedTheme == null || SelectedTheme.IsBuiltIn) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the theme '{SelectedTheme.Name}'?",
            "Delete Theme",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var themeToDelete = SelectedTheme;
            var index = Themes.IndexOf(themeToDelete);

            _themeService.DeleteTheme(themeToDelete);
            Themes.Remove(themeToDelete);

            // Select another theme
            SelectedTheme = Themes.ElementAtOrDefault(Math.Max(0, index - 1));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete theme: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ApplyPreview()
    {
        if (SelectedTheme == null) return;

        try
        {
            if (_workingCopy != null && !_workingCopy.IsBuiltIn)
            {
                _themeService.ApplyTheme(_workingCopy);
            }
            else
            {
                _themeService.ApplyTheme(SelectedTheme);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to apply preview: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ResetColors()
    {
        if (SelectedTheme == null || SelectedTheme.IsBuiltIn) return;

        var result = MessageBox.Show(
            "Reset all colors to the default values for this theme type?",
            "Reset Colors",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        // Reset to default colors based on dark/light mode
        var defaultColors = IsDarkTheme
            ? ThemeColors.CreateDarkColors()
            : ThemeColors.CreateLightColors();

        if (_workingCopy != null)
        {
            _workingCopy.Colors = defaultColors;
            PopulateColorCollections(defaultColors);
        }
    }

    [RelayCommand]
    private void ExportTheme()
    {
        if (SelectedTheme == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Theme",
            Filter = "Theme Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = $"{SelectedTheme.Name}.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _themeService.SaveTheme(SelectedTheme, dialog.FileName);
                MessageBox.Show($"Theme exported successfully to:\n{dialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export theme: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void ImportTheme()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Theme",
            Filter = "Theme Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var theme = _themeService.LoadTheme(dialog.FileName);

                // Check for name collision
                if (Themes.Any(t => t.Name == theme.Name))
                {
                    theme.Name = GenerateUniqueName(theme.Name);
                }

                // Save to custom themes directory
                var destPath = Path.Combine(CustomThemesDirectory, $"{theme.Name}.json");
                _themeService.SaveTheme(theme, destPath);

                Themes.Add(theme);
                SelectedTheme = theme;

                MessageBox.Show($"Theme '{theme.Name}' imported successfully!",
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import theme: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void OpenThemesFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = CustomThemesDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Apply()
    {
        SaveChanges();
    }

    [RelayCommand]
    private void Ok()
    {
        SaveChanges();
        OkRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        // Restore original theme
        if (_originalTheme != null)
        {
            try
            {
                _themeService.ApplyTheme(_originalTheme);
            }
            catch
            {
                // Ignore restore errors
            }
        }

        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveChanges()
    {
        if (SelectedTheme == null) return;

        try
        {
            // For custom themes, save the working copy
            if (_workingCopy != null && !_workingCopy.IsBuiltIn)
            {
                var path = Path.Combine(CustomThemesDirectory, $"{SanitizeFileName(_workingCopy.Name)}.json");
                _themeService.SaveTheme(_workingCopy, path);
                _themeService.ApplyTheme(_workingCopy);
            }
            else
            {
                _themeService.ApplyTheme(SelectedTheme);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save theme: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GenerateUniqueName(string baseName)
    {
        var name = baseName;
        var counter = 1;

        while (Themes.Any(t => t.Name == name))
        {
            name = $"{baseName} {counter++}";
        }

        return name;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }
}

/// <summary>
/// Represents a color item for editing in the UI
/// </summary>
public partial class ColorItem : ObservableObject
{
    private readonly Action<string> _updateSource;

    public string DisplayName { get; }

    [ObservableProperty]
    private string _hexValue;

    [ObservableProperty]
    private Color _colorValue;

    public ColorItem(string displayName, string hexValue, Action<string> updateSource)
    {
        DisplayName = displayName;
        _hexValue = hexValue;
        _updateSource = updateSource;
        _colorValue = ParseColor(hexValue);
    }

    partial void OnHexValueChanged(string value)
    {
        var color = ParseColor(value);
        if (color != ColorValue)
        {
            ColorValue = color;
        }
        _updateSource(value);
    }

    partial void OnColorValueChanged(Color value)
    {
        var hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        if (value.A < 255)
        {
            hex = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
        }

        if (hex != HexValue)
        {
            HexValue = hex;
        }
    }

    public void UpdateSource(string hexValue)
    {
        _updateSource(hexValue);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Magenta; // Fallback for invalid colors
        }
    }
}
