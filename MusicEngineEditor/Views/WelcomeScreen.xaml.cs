// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Welcome screen shown on startup for quick project access.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views;

/// <summary>
/// Welcome screen shown on startup with recent projects and quick actions.
/// </summary>
public partial class WelcomeScreen : Window
{
    private readonly IRecentProjectsService _recentProjectsService;

    /// <summary>
    /// Gets the action result from the welcome screen.
    /// </summary>
    public WelcomeScreenResult Result { get; private set; } = new();

    /// <summary>
    /// Creates a new welcome screen.
    /// </summary>
    /// <param name="recentProjectsService">The recent projects service.</param>
    public WelcomeScreen(IRecentProjectsService recentProjectsService)
    {
        InitializeComponent();

        _recentProjectsService = recentProjectsService;

        Loaded += WelcomeScreen_Loaded;
    }

    private void WelcomeScreen_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshRecentProjects();

        // Set checkbox state
        DontShowCheckBox.IsChecked = !_recentProjectsService.ShowWelcomeOnStartup;
    }

    private void RefreshRecentProjects()
    {
        var projects = _recentProjectsService.RecentProjects;
        RecentProjectsList.ItemsSource = projects;

        // Show/hide empty state
        EmptyState.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentProjectsList.Visibility = projects.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result.Action = WelcomeScreenAction.Close;
        DialogResult = false;
        Close();
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        Result.Action = WelcomeScreenAction.NewProject;
        DialogResult = true;
        Close();
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        Result.Action = WelcomeScreenAction.OpenProject;
        DialogResult = true;
        Close();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        Result.Action = WelcomeScreenAction.Skip;
        DialogResult = true;
        Close();
    }

    private void RecentProjectsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentProjectsList.SelectedItem is RecentProject entry)
        {
            Result.Action = WelcomeScreenAction.OpenRecentProject;
            Result.SelectedProjectPath = entry.FilePath;
            DialogResult = true;
            Close();
        }
    }

    private async void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filePath)
        {
            await _recentProjectsService.TogglePinnedAsync(filePath);
            RefreshRecentProjects();
        }
    }

    private async void DontShowCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var showOnStartup = !(DontShowCheckBox.IsChecked ?? false);
        await _recentProjectsService.SetShowWelcomeOnStartupAsync(showOnStartup);
    }
}

/// <summary>
/// Result from the welcome screen dialog.
/// </summary>
public class WelcomeScreenResult
{
    /// <summary>
    /// The action selected by the user.
    /// </summary>
    public WelcomeScreenAction Action { get; set; } = WelcomeScreenAction.Close;

    /// <summary>
    /// The path to the selected recent project (if applicable).
    /// </summary>
    public string? SelectedProjectPath { get; set; }
}

/// <summary>
/// Actions that can be taken from the welcome screen.
/// </summary>
public enum WelcomeScreenAction
{
    /// <summary>Close the welcome screen without action.</summary>
    Close,
    /// <summary>Skip the welcome screen and continue to main window.</summary>
    Skip,
    /// <summary>Create a new project.</summary>
    NewProject,
    /// <summary>Open an existing project via file browser.</summary>
    OpenProject,
    /// <summary>Open a recently opened project.</summary>
    OpenRecentProject
}

/// <summary>
/// Converts a pinned boolean to a star character.
/// </summary>
public class PinnedToStarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "\u2605" : "\u2606"; // Filled vs empty star
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a pinned boolean to a color.
/// </summary>
public class PinnedToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush PinnedBrush = new(Color.FromRgb(0xFF, 0xD7, 0x00)); // Gold
    private static readonly SolidColorBrush UnpinnedBrush = new(Color.FromRgb(0x55, 0x55, 0x55)); // Gray

    static PinnedToColorConverter()
    {
        PinnedBrush.Freeze();
        UnpinnedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? PinnedBrush : UnpinnedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
