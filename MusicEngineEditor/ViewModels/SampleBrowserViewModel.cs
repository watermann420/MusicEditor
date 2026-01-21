using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Sample Browser control
/// </summary>
public partial class SampleBrowserViewModel : ViewModelBase
{
    private readonly ISoundPackService _soundPackService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private AudioSample? _selectedSample;

    [ObservableProperty]
    private bool _isPreviewPlaying;

    [ObservableProperty]
    private ObservableCollection<AudioSample> _filteredSamples = new();

    [ObservableProperty]
    private ObservableCollection<string> _categories = new()
    {
        "All",
        "Drums",
        "Bass",
        "Synths",
        "FX"
    };

    /// <summary>
    /// Event raised when a sample should be used (double-click or drag)
    /// </summary>
    public event EventHandler<AudioSample>? SampleSelected;

    public SampleBrowserViewModel(ISoundPackService soundPackService)
    {
        _soundPackService = soundPackService;
        LoadSamples();
    }

    /// <summary>
    /// Parameterless constructor for design-time support
    /// </summary>
    public SampleBrowserViewModel() : this(new SoundPackService())
    {
    }

    partial void OnSearchTextChanged(string value)
    {
        LoadSamples();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        LoadSamples();
    }

    private void LoadSamples()
    {
        var samples = _soundPackService.SearchSamples(
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            SelectedCategory == "All" ? null : SelectedCategory);

        FilteredSamples.Clear();
        foreach (var sample in samples)
        {
            FilteredSamples.Add(sample);
        }

        StatusMessage = $"{FilteredSamples.Count} samples found";
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (SelectedSample == null) return;

        if (IsPreviewPlaying && _soundPackService.CurrentPreviewSample?.Id == SelectedSample.Id)
        {
            // Stop if same sample is playing
            StopPreview();
        }
        else
        {
            // Start new preview
            try
            {
                await _soundPackService.PreviewSampleAsync(SelectedSample);
                IsPreviewPlaying = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Preview failed: {ex.Message}";
                IsPreviewPlaying = false;
            }
        }
    }

    [RelayCommand]
    private void StopPreview()
    {
        _soundPackService.StopPreview();
        IsPreviewPlaying = false;
    }

    [RelayCommand]
    private void UseSample()
    {
        if (SelectedSample != null)
        {
            SampleSelected?.Invoke(this, SelectedSample);
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category;
    }

    /// <summary>
    /// Called when a sample is double-clicked
    /// </summary>
    public void OnSampleDoubleClick()
    {
        if (SelectedSample != null)
        {
            SampleSelected?.Invoke(this, SelectedSample);
        }
    }

    /// <summary>
    /// Refresh the sample list
    /// </summary>
    public void Refresh()
    {
        LoadSamples();
    }
}
