// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Sampler Slicer control with waveform display and slice markers.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicEngine.Core.Synthesizers;
using MusicEngine.Core.Synthesizers.Slicer;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for SamplerSlicerControl.xaml.
/// A REX-style sample slicer with waveform display, automatic slice detection,
/// and multiple playback/trigger modes.
/// </summary>
public partial class SamplerSlicerControl : UserControl
{
    private bool _isDragging;
    private Point _dragStartPoint;

    /// <summary>
    /// Creates a new SamplerSlicerControl.
    /// </summary>
    public SamplerSlicerControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private SamplerSlicerViewModel? ViewModel => DataContext as SamplerSlicerViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SamplerSlicerViewModel oldVm)
        {
            oldVm.WaveformChanged -= OnWaveformChanged;
            oldVm.SlicesChanged -= OnSlicesChanged;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is SamplerSlicerViewModel newVm)
        {
            newVm.WaveformChanged += OnWaveformChanged;
            newVm.SlicesChanged += OnSlicesChanged;
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWaveformDisplay();
        UpdateSliceMarkers();
        UpdateCenterLine();
    }

    private void OnWaveformChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateWaveformDisplay();
            UpdateSliceMarkers();
        });
    }

    private void OnSlicesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateSliceMarkers);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SamplerSlicerViewModel.SelectedSlice))
        {
            Dispatcher.Invoke(UpdateSliceMarkers);
        }
    }

    #region Waveform Container Event Handlers

    private void WaveformContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null || !ViewModel.HasLoadedSample) return;

        _isDragging = true;
        _dragStartPoint = e.GetPosition(WaveformContainer);
        WaveformContainer.CaptureMouse();

        // Add slice at click position
        var position = e.GetPosition(WaveformContainer);
        var normalizedPosition = position.X / WaveformContainer.ActualWidth;
        ViewModel.AddSliceAtPosition(normalizedPosition);

        e.Handled = true;
    }

    private void WaveformContainer_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && ViewModel != null)
        {
            // Could be used for drag-to-adjust slice boundaries
            // For now, just track the position
        }
    }

    private void WaveformContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        WaveformContainer.ReleaseMouseCapture();
    }

    private void WaveformContainer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null || !ViewModel.HasLoadedSample) return;

        // Remove slice near click position
        var position = e.GetPosition(WaveformContainer);
        var normalizedPosition = position.X / WaveformContainer.ActualWidth;
        ViewModel.RemoveSliceNearPosition(normalizedPosition);

        e.Handled = true;
    }

    private void WaveformContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.WaveformWidth = e.NewSize.Width;
            ViewModel.WaveformHeight = e.NewSize.Height;
        }

        UpdateWaveformDisplay();
        UpdateSliceMarkers();
        UpdateCenterLine();
    }

    private void SliceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSliceMarkers();
    }

    #endregion

    #region Visualization Updates

    /// <summary>
    /// Updates the waveform display based on source data.
    /// </summary>
    private void UpdateWaveformDisplay()
    {
        WaveformCanvas.Children.Clear();

        var waveform = ViewModel?.SourceWaveform;
        if (waveform == null || waveform.Length == 0) return;

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var centerY = height / 2;
        var samplesPerPixel = Math.Max(1, waveform.Length / (int)width);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, centerY), false, false);

            // Draw top half (max values)
            for (int x = 0; x < (int)width; x++)
            {
                int startSample = x * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, waveform.Length);

                float maxVal = float.MinValue;
                for (int i = startSample; i < endSample; i++)
                {
                    maxVal = Math.Max(maxVal, waveform[i]);
                }

                double yMax = centerY - (maxVal * centerY * 0.9);
                ctx.LineTo(new Point(x, yMax), true, false);
            }

            // Draw bottom half (min values) in reverse
            for (int x = (int)width - 1; x >= 0; x--)
            {
                int startSample = x * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, waveform.Length);

                float minVal = float.MaxValue;
                for (int i = startSample; i < endSample; i++)
                {
                    minVal = Math.Min(minVal, waveform[i]);
                }

                double yMin = centerY - (minVal * centerY * 0.9);
                ctx.LineTo(new Point(x, yMin), true, false);
            }
        }

        geometry.Freeze();

        // Create waveform path with gradient fill
        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Fill = (Brush)FindResource("WaveformGradientBrush"),
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),
            StrokeThickness = 0.5,
            Opacity = 0.8
        };

        WaveformCanvas.Children.Add(path);
    }

    /// <summary>
    /// Updates the slice marker display.
    /// </summary>
    private void UpdateSliceMarkers()
    {
        SliceMarkersCanvas.Children.Clear();

        if (ViewModel == null || !ViewModel.HasLoadedSample) return;

        var width = WaveformContainer.ActualWidth;
        var height = WaveformContainer.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var slices = ViewModel.Slices;
        var selectedSlice = ViewModel.SelectedSlice;
        var totalSamples = ViewModel.TotalSamples;

        if (totalSamples <= 0) return;

        foreach (var sliceVm in slices)
        {
            // Calculate X position for slice start
            double xPos = (double)sliceVm.StartSample / totalSamples * width;

            // Determine if this slice is selected
            bool isSelected = selectedSlice != null && selectedSlice.Index == sliceVm.Index;

            // Create slice marker line
            var line = new Line
            {
                X1 = xPos,
                Y1 = 0,
                X2 = xPos,
                Y2 = height,
                Stroke = isSelected
                    ? (Brush)FindResource("SelectedSliceBrush")
                    : (Brush)FindResource("SliceBrush"),
                StrokeThickness = isSelected ? 2 : 1,
                StrokeDashArray = new DoubleCollection(new[] { 4.0, 2.0 })
            };

            SliceMarkersCanvas.Children.Add(line);

            // Add slice number label
            var label = new TextBlock
            {
                Text = sliceVm.Index.ToString(),
                Foreground = isSelected
                    ? (Brush)FindResource("SelectedSliceBrush")
                    : (Brush)FindResource("SliceBrush"),
                FontSize = 9,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal
            };

            Canvas.SetLeft(label, xPos + 2);
            Canvas.SetTop(label, 2);
            SliceMarkersCanvas.Children.Add(label);

            // Add shaded region for selected slice
            if (isSelected && sliceVm.EndSample > sliceVm.StartSample)
            {
                double endXPos = (double)sliceVm.EndSample / totalSamples * width;
                var rect = new Rectangle
                {
                    Width = Math.Max(1, endXPos - xPos),
                    Height = height,
                    Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xD7, 0x00)),
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(rect, xPos);
                Canvas.SetTop(rect, 0);
                SliceMarkersCanvas.Children.Add(rect);
            }
        }
    }

    /// <summary>
    /// Updates the center line position.
    /// </summary>
    private void UpdateCenterLine()
    {
        var height = WaveformContainer.ActualHeight;
        if (height > 0)
        {
            CenterLine.Y1 = height / 2;
            CenterLine.Y2 = height / 2;
        }
    }

    #endregion
}

/// <summary>
/// Slice trigger mode for MIDI mapping.
/// </summary>
public enum SliceTriggerMode
{
    /// <summary>Map slices chromatically starting from a base note.</summary>
    Chromatic,
    /// <summary>MPC-style pad mapping (16 pads).</summary>
    Pad,
    /// <summary>Slices play in sequence on any note.</summary>
    Sequential
}

/// <summary>
/// ViewModel for a single slice in the UI.
/// </summary>
public partial class SliceViewModel : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private long _startSample;

    [ObservableProperty]
    private long _endSample;

    [ObservableProperty]
    private float _gain = 1.0f;

    [ObservableProperty]
    private float _pitch = 1.0f;

    [ObservableProperty]
    private bool _reverse;

    [ObservableProperty]
    private int _midiNote = -1;

    [ObservableProperty]
    private string _name = "";

    private int _sampleRate = 44100;

    /// <summary>
    /// Gets or sets the sample rate for time calculations.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set => _sampleRate = value > 0 ? value : 44100;
    }

    /// <summary>
    /// Gets the formatted start time string.
    /// </summary>
    public string StartTimeFormatted => FormatTime(StartSample);

    /// <summary>
    /// Gets the formatted end time string.
    /// </summary>
    public string EndTimeFormatted => FormatTime(EndSample);

    /// <summary>
    /// Gets the slice length in samples.
    /// </summary>
    public long LengthSamples => EndSample - StartSample;

    private string FormatTime(long samples)
    {
        var seconds = (double)samples / SampleRate;
        var minutes = (int)(seconds / 60);
        var remainingSeconds = seconds % 60;
        return $"{minutes}:{remainingSeconds:00.00}";
    }

    /// <summary>
    /// Creates a SliceViewModel from a Slice.
    /// </summary>
    public static SliceViewModel FromSlice(Slice slice, int sampleRate)
    {
        return new SliceViewModel
        {
            Index = slice.Index,
            StartSample = slice.StartSample,
            EndSample = slice.EndSample,
            Gain = slice.Gain,
            Pitch = slice.Pitch,
            Reverse = slice.Reverse,
            MidiNote = slice.MidiNote,
            Name = slice.Name,
            SampleRate = sampleRate
        };
    }
}

/// <summary>
/// ViewModel for the Sampler Slicer control.
/// </summary>
public partial class SamplerSlicerViewModel : ViewModels.ViewModelBase, IDisposable
{
    private SamplerSlicer? _slicer;
    private bool _disposed;
    private float[]? _sourceWaveform;
    private int _sampleRate = 44100;

    #region Observable Properties

    [ObservableProperty]
    private string _synthName = "SamplerSlicer";

    [ObservableProperty]
    private string? _loadedFileName;

    [ObservableProperty]
    private bool _hasLoadedSample;

    [ObservableProperty]
    private int _sliceCount;

    [ObservableProperty]
    private bool _hasSlices;

    [ObservableProperty]
    private float _volume = 1.0f;

    [ObservableProperty]
    private float _velocitySensitivity = 0.5f;

    [ObservableProperty]
    private double _attackTime = 0.001;

    [ObservableProperty]
    private double _releaseTime = 0.01;

    [ObservableProperty]
    private int _crossfadeSamples = 64;

    [ObservableProperty]
    private double _bpm = 120;

    [ObservableProperty]
    private bool _quantizeToTempo;

    [ObservableProperty]
    private float _sensitivity = 1.0f;

    [ObservableProperty]
    private SliceMode _selectedSliceMode = SliceMode.Transient;

    [ObservableProperty]
    private SlicePlayMode _selectedPlayMode = SlicePlayMode.OneShot;

    [ObservableProperty]
    private SliceTriggerMode _selectedTriggerMode = SliceTriggerMode.Chromatic;

    [ObservableProperty]
    private int _startMidiNote = 36;

    [ObservableProperty]
    private bool _pitchLock;

    [ObservableProperty]
    private SliceViewModel? _selectedSlice;

    [ObservableProperty]
    private bool _hasSelectedSlice;

    [ObservableProperty]
    private float _selectedSliceGain = 1.0f;

    [ObservableProperty]
    private float _selectedSlicePitch = 1.0f;

    [ObservableProperty]
    private bool _selectedSliceReverse;

    [ObservableProperty]
    private double _totalDuration;

    [ObservableProperty]
    private double _waveformWidth = 600;

    [ObservableProperty]
    private double _waveformHeight = 200;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the available slice detection modes.
    /// </summary>
    public ObservableCollection<SliceMode> AvailableSliceModes { get; } = new(Enum.GetValues<SliceMode>());

    /// <summary>
    /// Gets the available playback modes.
    /// </summary>
    public ObservableCollection<SlicePlayMode> AvailablePlayModes { get; } = new(Enum.GetValues<SlicePlayMode>());

    /// <summary>
    /// Gets the available trigger modes.
    /// </summary>
    public ObservableCollection<SliceTriggerMode> AvailableTriggerModes { get; } = new(Enum.GetValues<SliceTriggerMode>());

    /// <summary>
    /// Gets the collection of slices.
    /// </summary>
    public ObservableCollection<SliceViewModel> Slices { get; } = new();

    /// <summary>
    /// Gets the waveform sample data for display.
    /// </summary>
    public float[]? SourceWaveform => _sourceWaveform;

    /// <summary>
    /// Gets the total number of samples in the loaded audio.
    /// </summary>
    public long TotalSamples => _sourceWaveform?.Length ?? 0;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the start MIDI note name.
    /// </summary>
    public string StartMidiNoteName => GetNoteName(StartMidiNote);

    #endregion

    #region Events

    /// <summary>
    /// Event raised when waveform data changes.
    /// </summary>
    public event EventHandler? WaveformChanged;

    /// <summary>
    /// Event raised when slices change.
    /// </summary>
    public event EventHandler? SlicesChanged;

    #endregion

    /// <summary>
    /// Creates a new SamplerSlicerViewModel (design-time constructor).
    /// </summary>
    public SamplerSlicerViewModel()
    {
    }

    /// <summary>
    /// Creates a new SamplerSlicerViewModel with the specified slicer.
    /// </summary>
    public SamplerSlicerViewModel(SamplerSlicer slicer)
    {
        _slicer = slicer ?? throw new ArgumentNullException(nameof(slicer));
        LoadFromSlicer();
        _slicer.SlicesChanged += OnSlicerSlicesChanged;
    }

    /// <summary>
    /// Initializes with a new SamplerSlicer instance.
    /// </summary>
    public void Initialize(int sampleRate = 44100)
    {
        _sampleRate = sampleRate;
        _slicer = new SamplerSlicer(sampleRate);
        _slicer.SlicesChanged += OnSlicerSlicesChanged;
        LoadFromSlicer();
        StatusMessage = "Sampler slicer initialized";
    }

    /// <summary>
    /// Loads settings from the slicer instance.
    /// </summary>
    private void LoadFromSlicer()
    {
        if (_slicer == null) return;

        SynthName = _slicer.Name;
        Volume = _slicer.Volume;
        VelocitySensitivity = _slicer.VelocitySensitivity;
        AttackTime = _slicer.AttackTime;
        ReleaseTime = _slicer.ReleaseTime;
        CrossfadeSamples = _slicer.CrossfadeSamples;
        Bpm = _slicer.Bpm;
        QuantizeToTempo = _slicer.QuantizeToTempo;
        SelectedPlayMode = _slicer.PlayMode;
        Sensitivity = _slicer.Detector.Sensitivity;
        TotalDuration = _slicer.TotalDuration;

        UpdateSliceCollection();
    }

    private void OnSlicerSlicesChanged(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateSliceCollection();
            SlicesChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Updates the slice collection from the slicer.
    /// </summary>
    private void UpdateSliceCollection()
    {
        if (_slicer == null) return;

        Slices.Clear();
        var slices = _slicer.Slices;

        foreach (var slice in slices)
        {
            Slices.Add(SliceViewModel.FromSlice(slice, _sampleRate));
        }

        SliceCount = Slices.Count;
        HasSlices = Slices.Count > 0;
    }

    #region Property Changed Handlers

    partial void OnVolumeChanged(float value)
    {
        _slicer?.SetParameter("volume", value);
    }

    partial void OnVelocitySensitivityChanged(float value)
    {
        _slicer?.SetParameter("velocity_sensitivity", value);
    }

    partial void OnAttackTimeChanged(double value)
    {
        _slicer?.SetParameter("attack", (float)value);
    }

    partial void OnReleaseTimeChanged(double value)
    {
        _slicer?.SetParameter("release", (float)value);
    }

    partial void OnCrossfadeSamplesChanged(int value)
    {
        _slicer?.SetParameter("crossfade", value);
    }

    partial void OnBpmChanged(double value)
    {
        _slicer?.SetParameter("bpm", (float)value);
    }

    partial void OnQuantizeToTempoChanged(bool value)
    {
        if (_slicer != null)
        {
            _slicer.QuantizeToTempo = value;
        }
    }

    partial void OnSensitivityChanged(float value)
    {
        _slicer?.SetParameter("sensitivity", value);
    }

    partial void OnSelectedPlayModeChanged(SlicePlayMode value)
    {
        if (_slicer != null)
        {
            _slicer.PlayMode = value;
        }
    }

    partial void OnStartMidiNoteChanged(int value)
    {
        OnPropertyChanged(nameof(StartMidiNoteName));
    }

    partial void OnSelectedSliceChanged(SliceViewModel? value)
    {
        HasSelectedSlice = value != null;
        if (value != null)
        {
            SelectedSliceGain = value.Gain;
            SelectedSlicePitch = value.Pitch;
            SelectedSliceReverse = value.Reverse;
        }
    }

    partial void OnSelectedSliceGainChanged(float value)
    {
        if (SelectedSlice != null && _slicer != null)
        {
            SelectedSlice.Gain = value;
            var slice = _slicer.GetSlice(SelectedSlice.Index);
            if (slice != null)
            {
                slice.Gain = value;
            }
        }
    }

    partial void OnSelectedSlicePitchChanged(float value)
    {
        if (SelectedSlice != null && _slicer != null)
        {
            SelectedSlice.Pitch = value;
            var slice = _slicer.GetSlice(SelectedSlice.Index);
            if (slice != null)
            {
                slice.Pitch = value;
            }
        }
    }

    partial void OnSelectedSliceReverseChanged(bool value)
    {
        if (SelectedSlice != null && _slicer != null)
        {
            SelectedSlice.Reverse = value;
            var slice = _slicer.GetSlice(SelectedSlice.Index);
            if (slice != null)
            {
                slice.Reverse = value;
            }
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void LoadSample()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Sample for Slicing",
            Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.flac;*.ogg|WAV Files|*.wav|MP3 Files|*.mp3|All Files|*.*",
            DefaultExt = ".wav"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadSampleFromFile(dialog.FileName);
        }
    }

    /// <summary>
    /// Loads a sample from the specified file path.
    /// </summary>
    public void LoadSampleFromFile(string filePath)
    {
        try
        {
            IsBusy = true;
            StatusMessage = $"Loading {System.IO.Path.GetFileName(filePath)}...";

            if (_slicer == null)
            {
                Initialize();
            }

            _slicer!.LoadFromFile(filePath);
            LoadedFileName = System.IO.Path.GetFileName(filePath);
            HasLoadedSample = true;

            // Generate waveform data for display
            GenerateWaveformData(filePath);

            TotalDuration = _slicer.TotalDuration;
            StatusMessage = $"Loaded: {LoadedFileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading sample: {ex.Message}";
            HasLoadedSample = false;
            LoadedFileName = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Generates waveform data for display from the loaded file.
    /// </summary>
    private void GenerateWaveformData(string filePath)
    {
        try
        {
            using var reader = new NAudio.Wave.AudioFileReader(filePath);
            _sampleRate = reader.WaveFormat.SampleRate;
            var samples = new List<float>();
            var buffer = new float[4096];
            int read;

            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    samples.Add(buffer[i]);
                }
            }

            // Convert to mono if stereo
            if (reader.WaveFormat.Channels == 2)
            {
                var monoSamples = new List<float>();
                for (int i = 0; i < samples.Count - 1; i += 2)
                {
                    monoSamples.Add((samples[i] + samples[i + 1]) * 0.5f);
                }
                _sourceWaveform = monoSamples.ToArray();
            }
            else
            {
                _sourceWaveform = samples.ToArray();
            }

            WaveformChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _sourceWaveform = null;
        }
    }

    [RelayCommand]
    private void AutoSlice()
    {
        if (_slicer == null || !HasLoadedSample) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Detecting slices...";

            _slicer.Detector.Sensitivity = Sensitivity;
            _slicer.AutoSlice(SelectedSliceMode, Bpm);

            StatusMessage = $"Detected {_slicer.SliceCount} slices";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error detecting slices: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearSlices()
    {
        _slicer?.ClearSlices();
        StatusMessage = "Slices cleared";
    }

    [RelayCommand]
    private void AddSliceAtCursor()
    {
        // Add slice at the center of the waveform as default
        AddSliceAtPosition(0.5);
    }

    /// <summary>
    /// Adds a slice at the specified normalized position (0-1).
    /// </summary>
    public void AddSliceAtPosition(double normalizedPosition)
    {
        if (_slicer == null || _sourceWaveform == null) return;

        long samplePosition = (long)(normalizedPosition * _sourceWaveform.Length);
        samplePosition = Math.Clamp(samplePosition, 0, _sourceWaveform.Length - 1);

        // Find the end position (next slice or end of audio)
        long endPosition = _sourceWaveform.Length;
        var existingSlices = _slicer.Slices.OrderBy(s => s.StartSample).ToList();

        foreach (var slice in existingSlices)
        {
            if (slice.StartSample > samplePosition)
            {
                endPosition = slice.StartSample;
                break;
            }
        }

        _slicer.AddSlice(samplePosition, endPosition);
        StatusMessage = $"Added slice at {FormatTime(samplePosition)}";
    }

    /// <summary>
    /// Removes the slice nearest to the specified normalized position.
    /// </summary>
    public void RemoveSliceNearPosition(double normalizedPosition)
    {
        if (_slicer == null || _sourceWaveform == null) return;

        long samplePosition = (long)(normalizedPosition * _sourceWaveform.Length);
        var slices = _slicer.Slices;

        // Find nearest slice
        int nearestIndex = -1;
        long nearestDistance = long.MaxValue;

        for (int i = 0; i < slices.Count; i++)
        {
            long distance = Math.Abs(slices[i].StartSample - samplePosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        // Only remove if within reasonable distance (5% of total length)
        long threshold = (long)(_sourceWaveform.Length * 0.05);
        if (nearestIndex >= 0 && nearestDistance < threshold)
        {
            _slicer.RemoveSlice(nearestIndex);
            StatusMessage = $"Removed slice {nearestIndex}";
        }
    }

    [RelayCommand]
    private void RemoveSelectedSlice()
    {
        if (_slicer == null || SelectedSlice == null) return;

        _slicer.RemoveSlice(SelectedSlice.Index);
        SelectedSlice = null;
        StatusMessage = "Removed selected slice";
    }

    [RelayCommand]
    private void AssignMidiNotes()
    {
        _slicer?.AssignMidiNotes(StartMidiNote);
        UpdateSliceCollection();
        StatusMessage = $"Assigned MIDI notes starting from {StartMidiNoteName}";
    }

    [RelayCommand]
    private void PreviewSelectedSlice()
    {
        if (_slicer == null || SelectedSlice == null) return;

        _slicer.TriggerSlice(SelectedSlice.Index, 100);
        StatusMessage = $"Previewing slice {SelectedSlice.Index}";
    }

    [RelayCommand]
    private void ExportSlices()
    {
        if (_slicer == null || !HasSlices || _sourceWaveform == null) return;

        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to export slices",
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Exporting slices...";

                string baseName = System.IO.Path.GetFileNameWithoutExtension(LoadedFileName ?? "slice");
                var slices = _slicer.Slices;

                for (int i = 0; i < slices.Count; i++)
                {
                    var slice = slices[i];
                    string fileName = System.IO.Path.Combine(dialog.SelectedPath, $"{baseName}_{i:D3}.wav");

                    // Extract slice audio
                    int startIdx = (int)Math.Max(0, slice.StartSample);
                    int endIdx = (int)Math.Min(_sourceWaveform.Length, slice.EndSample);
                    int length = endIdx - startIdx;

                    if (length > 0)
                    {
                        var sliceAudio = new float[length];
                        Array.Copy(_sourceWaveform, startIdx, sliceAudio, 0, length);

                        // Write WAV file
                        WriteWavFile(fileName, sliceAudio, _sampleRate);
                    }
                }

                StatusMessage = $"Exported {slices.Count} slices to {dialog.SelectedPath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting slices: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Writes audio data to a WAV file.
    /// </summary>
    private void WriteWavFile(string path, float[] samples, int sampleRate)
    {
        using var writer = new NAudio.Wave.WaveFileWriter(path,
            NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
        writer.WriteSamples(samples, 0, samples.Length);
    }

    #endregion

    #region Helper Methods

    private string FormatTime(long samples)
    {
        var seconds = (double)samples / _sampleRate;
        var minutes = (int)(seconds / 60);
        var remainingSeconds = seconds % 60;
        return $"{minutes}:{remainingSeconds:00.00}";
    }

    private static string GetNoteName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        return $"{noteNames[noteIndex]}{octave}";
    }

    #endregion

    /// <summary>
    /// Gets the underlying SamplerSlicer instance.
    /// </summary>
    public SamplerSlicer? GetSlicer() => _slicer;

    /// <summary>
    /// Sets the slicer instance.
    /// </summary>
    public void SetSlicer(SamplerSlicer slicer)
    {
        if (_slicer != null)
        {
            _slicer.SlicesChanged -= OnSlicerSlicesChanged;
        }

        _slicer = slicer ?? throw new ArgumentNullException(nameof(slicer));
        _slicer.SlicesChanged += OnSlicerSlicesChanged;
        LoadFromSlicer();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_slicer != null)
        {
            _slicer.SlicesChanged -= OnSlicerSlicesChanged;
            _slicer.AllNotesOff();
        }
    }
}

/// <summary>
/// Converts boolean to inverse visibility for Sampler Slicer control.
/// </summary>
public class SlicerInverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
