using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MusicEngine.Core;
using MusicEngineEditor.ViewModels;
using NAudio.Wave;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Stem export dialog for exporting individual tracks/buses.
/// </summary>
public partial class StemExportDialog : Window
{
    private readonly StemExportViewModel _viewModel;

    /// <summary>
    /// Creates a new stem export dialog.
    /// </summary>
    public StemExportDialog() : this(new StemExporter()) { }

    /// <summary>
    /// Creates a new stem export dialog with the specified exporter.
    /// </summary>
    /// <param name="exporter">The stem exporter to use.</param>
    public StemExportDialog(StemExporter exporter)
    {
        InitializeComponent();

        _viewModel = new StemExportViewModel(exporter);
        _viewModel.ExportCompleted += OnExportCompleted;
        _viewModel.CancelRequested += OnCancelRequested;
        DataContext = _viewModel;
    }

    /// <summary>
    /// Gets the export result after the dialog closes.
    /// </summary>
    public StemExportResult? Result => _viewModel.LastResult;

    /// <summary>
    /// Gets the view model for external access.
    /// </summary>
    public StemExportViewModel ViewModel => _viewModel;

    /// <summary>
    /// Adds a stem to the export list.
    /// </summary>
    /// <param name="name">Display name of the stem.</param>
    /// <param name="source">Audio source for the stem.</param>
    /// <param name="enabled">Whether the stem is enabled by default.</param>
    public void AddStem(string name, ISampleProvider source, bool enabled = true)
    {
        _viewModel.AddStem(name, source, enabled);
    }

    /// <summary>
    /// Adds multiple stems from a dictionary.
    /// </summary>
    /// <param name="sources">Dictionary of stem name to audio source.</param>
    public void AddStems(IDictionary<string, ISampleProvider> sources)
    {
        foreach (var kvp in sources)
        {
            _viewModel.AddStem(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Loads stems from an audio engine.
    /// </summary>
    /// <param name="engine">The audio engine to load stems from.</param>
    public void LoadFromEngine(AudioEngine engine)
    {
        _viewModel.LoadFromEngine(engine);
    }

    /// <summary>
    /// Sets the project name for subfolder creation.
    /// </summary>
    /// <param name="name">Project name.</param>
    public void SetProjectName(string name)
    {
        _viewModel.SetProjectName(name);
    }

    /// <summary>
    /// Sets the output directory.
    /// </summary>
    /// <param name="path">Output directory path.</param>
    public void SetOutputDirectory(string path)
    {
        _viewModel.OutputDirectory = path;
    }

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
/// Converter that converts a non-empty string to Visible.
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
