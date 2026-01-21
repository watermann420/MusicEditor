using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MusicEngine.Core;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Export dialog for audio export with platform presets and loudness normalization.
/// </summary>
public partial class ExportDialog : Window
{
    private readonly ExportViewModel _viewModel;

    /// <summary>
    /// Creates a new export dialog.
    /// </summary>
    public ExportDialog() : this(new AudioRecorder()) { }

    /// <summary>
    /// Creates a new export dialog with the specified audio recorder.
    /// </summary>
    /// <param name="recorder">The audio recorder to use for export.</param>
    public ExportDialog(AudioRecorder recorder)
    {
        InitializeComponent();

        _viewModel = new ExportViewModel(recorder);
        _viewModel.ExportCompleted += OnExportCompleted;
        _viewModel.CancelRequested += OnCancelRequested;
        DataContext = _viewModel;
    }

    /// <summary>
    /// Creates a new export dialog with a pre-selected input file.
    /// </summary>
    /// <param name="inputFilePath">Path to the input audio file.</param>
    /// <param name="recorder">The audio recorder to use for export.</param>
    public ExportDialog(string inputFilePath, AudioRecorder recorder) : this(recorder)
    {
        _viewModel.SetInputFile(inputFilePath);
    }

    /// <summary>
    /// Gets the export result after the dialog closes.
    /// </summary>
    public ExportResult? Result => _viewModel.LastResult;

    /// <summary>
    /// Gets the view model for external access.
    /// </summary>
    public ExportViewModel ViewModel => _viewModel;

    private void OnExportCompleted(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ExportCompleted -= OnExportCompleted;
        _viewModel.CancelRequested -= OnCancelRequested;
        base.OnClosed(e);
    }
}

/// <summary>
/// Converter that inverts a boolean value and converts to Visibility.
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}
