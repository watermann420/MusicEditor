using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Sample Browser control for browsing and previewing audio samples from sound packs
/// </summary>
public partial class SampleBrowser : UserControl
{
    private SampleBrowserViewModel? _viewModel;

    /// <summary>
    /// Event raised when a sample is selected for use
    /// </summary>
    public event EventHandler<AudioSample>? SampleSelected;

    public SampleBrowser()
    {
        InitializeComponent();
        Loaded += SampleBrowser_Loaded;
    }

    private void SampleBrowser_Loaded(object sender, RoutedEventArgs e)
    {
        // Get the service from DI if available, otherwise create a new one
        ISoundPackService soundPackService;
        try
        {
            soundPackService = App.Services.GetService(typeof(ISoundPackService)) as ISoundPackService
                               ?? new SoundPackService();
        }
        catch
        {
            soundPackService = new SoundPackService();
        }

        _viewModel = new SampleBrowserViewModel(soundPackService);
        _viewModel.SampleSelected += ViewModel_SampleSelected;
        DataContext = _viewModel;
    }

    private void ViewModel_SampleSelected(object? sender, AudioSample sample)
    {
        SampleSelected?.Invoke(this, sample);
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // The binding handles the update via UpdateSourceTrigger=PropertyChanged
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        _viewModel?.ClearSearchCommand.Execute(null);
    }

    private void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && _viewModel != null)
        {
            var category = button.Content?.ToString() ?? "All";
            _viewModel.SelectCategoryCommand.Execute(category);
            UpdateCategoryButtons(category);
        }
    }

    private void UpdateCategoryButtons(string selectedCategory)
    {
        foreach (var child in CategoryPanel.Children)
        {
            if (child is Button btn)
            {
                btn.Tag = btn.Content?.ToString() == selectedCategory ? "Active" : null;
            }
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is AudioSample sample)
        {
            if (_viewModel != null)
            {
                _viewModel.SelectedSample = sample;
                _viewModel.PreviewCommand.Execute(null);
            }
        }
    }

    private void SampleListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.OnSampleDoubleClick();
    }
}

#region Value Converters

/// <summary>
/// Converts the selected category to an "Active" tag for styling
/// </summary>
public class CategoryTagConverter : IValueConverter
{
    public static readonly CategoryTagConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedCategory && parameter is string buttonCategory)
        {
            return selectedCategory == buttonCategory ? "Active" : null;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts preview playing state to a "Playing" tag for styling
/// </summary>
public class PreviewTagConverter : IValueConverter
{
    public static readonly PreviewTagConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying && isPlaying)
        {
            return "Playing";
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null values to Visibility.Collapsed
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts non-null values to true
/// </summary>
public class NotNullToBoolConverter : IValueConverter
{
    public static readonly NotNullToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
