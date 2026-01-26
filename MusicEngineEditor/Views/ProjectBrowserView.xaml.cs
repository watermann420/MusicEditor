// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Project Browser window for browsing and opening MusicEngine projects.
/// </summary>
public partial class ProjectBrowserView : Window
{
    private readonly ProjectBrowserViewModel _viewModel;

    /// <summary>
    /// Gets the selected project, or null if cancelled.
    /// </summary>
    public ProjectInfo? SelectedProject { get; private set; }

    /// <summary>
    /// Creates a new project browser.
    /// </summary>
    public ProjectBrowserView()
    {
        InitializeComponent();

        _viewModel = new ProjectBrowserViewModel();
        _viewModel.ProjectOpened += OnProjectOpened;
        _viewModel.NewProjectRequested += OnNewProjectRequested;
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Creates a new project browser with a specific starting directory.
    /// </summary>
    /// <param name="startDirectory">The directory to browse.</param>
    public ProjectBrowserView(string startDirectory) : this()
    {
        if (!string.IsNullOrEmpty(startDirectory))
        {
            _viewModel.NavigateToCommand.Execute(startDirectory);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.LoadProjectsCommand.CanExecute(null))
        {
            _viewModel.LoadProjectsCommand.Execute(null);
        }
    }

    private void OnProjectOpened(object? sender, ProjectInfo project)
    {
        SelectedProject = project;
        DialogResult = true;
        Close();
    }

    private void OnNewProjectRequested(object? sender, EventArgs e)
    {
        SelectedProject = null;
        DialogResult = false;
        // The caller should handle creating a new project
        Close();
    }

    private void ProjectList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedProject != null)
        {
            _viewModel.OpenProject(_viewModel.SelectedProject);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ProjectOpened -= OnProjectOpened;
        _viewModel.NewProjectRequested -= OnNewProjectRequested;
        base.OnClosed(e);
    }
}

/// <summary>
/// Converts a boolean to a star character.
/// </summary>
public class BoolToStarConverter : IValueConverter
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
/// Converts a boolean to a star color.
/// </summary>
public class BoolToStarColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "#FFD700" : "#555555"; // Gold vs gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a count of 0 to Visible, otherwise Collapsed.
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to Collapsed, non-null to Visible.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to false, non-null to true.
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
