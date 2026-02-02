// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Spectral Freeze effect editor control.

using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// Represents a frozen spectrum snapshot slot.
/// </summary>
public partial class FreezeSlot : ObservableObject
{
    [ObservableProperty]
    private int _slotIndex;

    [ObservableProperty]
    private string _name = "Empty";

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private float[]? _spectrumData;

    [ObservableProperty]
    private DateTime? _captureTime;

    /// <summary>
    /// Gets a display label for the slot.
    /// </summary>
    public string DisplayLabel => HasData ? $"Slot {SlotIndex + 1}: {Name}" : $"Slot {SlotIndex + 1}: (Empty)";

    /// <summary>
    /// Notifies that the display label has changed.
    /// </summary>
    public void NotifyDisplayLabelChanged()
    {
        OnPropertyChanged(nameof(DisplayLabel));
    }
}

/// <summary>
/// ViewModel for the Spectral Freeze effect editor control.
/// </summary>
public partial class SpectralFreezeViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private System.Timers.Timer? _updateTimer;
    private readonly Random _random = new();
    private const int DefaultNumBins = 512;

    #region Observable Properties

    // FFT Settings
    [ObservableProperty]
    private int _selectedFftSize = 2048;

    [ObservableProperty]
    private int _sampleRate = 44100;

    // Freeze Controls
    [ObservableProperty]
    private bool _isFrozen;

    [ObservableProperty]
    private float _freezeBlend = 0.5f;

    [ObservableProperty]
    private float _spectralShift;

    [ObservableProperty]
    private float _spectralTilt;

    [ObservableProperty]
    private float _blurAmount;

    [ObservableProperty]
    private float _feedbackAmount;

    [ObservableProperty]
    private float _freezeDecay;

    [ObservableProperty]
    private bool _randomizeBins;

    // Morph Controls
    [ObservableProperty]
    private float _morphPosition;

    [ObservableProperty]
    private int _morphSourceSlot;

    [ObservableProperty]
    private int _morphTargetSlot = 1;

    [ObservableProperty]
    private bool _isMorphEnabled;

    // Selected Slot
    [ObservableProperty]
    private int _selectedSlotIndex;

    // Display States
    [ObservableProperty]
    private bool _isBypassed;

    [ObservableProperty]
    private bool _showLiveInput = true;

    [ObservableProperty]
    private bool _showFrozenSpectrum = true;

    [ObservableProperty]
    private bool _showBlendResult = true;

    // Spectrum Data
    [ObservableProperty]
    private float[]? _liveSpectrum;

    [ObservableProperty]
    private float[]? _frozenSpectrum;

    [ObservableProperty]
    private float[]? _blendedSpectrum;

    // Display dimensions
    [ObservableProperty]
    private double _displayWidth = 500;

    [ObservableProperty]
    private double _displayHeight = 200;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the available FFT sizes.
    /// </summary>
    public ObservableCollection<int> AvailableFftSizes { get; } = new()
    {
        512, 1024, 2048, 4096, 8192
    };

    /// <summary>
    /// Gets the freeze slots collection.
    /// </summary>
    public ObservableCollection<FreezeSlot> FreezeSlots { get; } = new();

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    /// <summary>
    /// Event raised when freeze state changes.
    /// </summary>
    public event EventHandler<bool>? FreezeStateChanged;

    /// <summary>
    /// Event raised when a freeze capture is requested.
    /// </summary>
    public event EventHandler<int>? FreezeCaptureRequested;

    /// <summary>
    /// Event raised when spectrum data needs to be updated.
    /// </summary>
    public event EventHandler? SpectrumUpdateRequested;

    /// <summary>
    /// Event raised when bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    #endregion

    public SpectralFreezeViewModel()
    {
        // Initialize freeze slots
        for (int i = 0; i < 4; i++)
        {
            FreezeSlots.Add(new FreezeSlot
            {
                SlotIndex = i,
                Name = $"Snapshot {i + 1}",
                IsSelected = i == 0
            });
        }

        // Initialize spectrum arrays
        int numBins = SelectedFftSize / 2 + 1;
        LiveSpectrum = new float[numBins];
        FrozenSpectrum = new float[numBins];
        BlendedSpectrum = new float[numBins];

        // Start visualization timer
        StartUpdateTimer();
    }

    /// <summary>
    /// Starts the spectrum visualization update timer.
    /// </summary>
    private void StartUpdateTimer()
    {
        _updateTimer = new System.Timers.Timer(33); // ~30 FPS
        _updateTimer.Elapsed += (s, e) => UpdateSpectrum();
        _updateTimer.AutoReset = true;
        _updateTimer.Start();
    }

    /// <summary>
    /// Updates the spectrum visualization.
    /// </summary>
    private void UpdateSpectrum()
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                SpectrumUpdateRequested?.Invoke(this, EventArgs.Empty);

                // Calculate blended spectrum
                CalculateBlendedSpectrum();
            });
        }
        catch
        {
            // Ignore dispatcher exceptions during shutdown
        }
    }

    /// <summary>
    /// Calculates the blended spectrum from live and frozen data.
    /// </summary>
    private void CalculateBlendedSpectrum()
    {
        if (LiveSpectrum == null || BlendedSpectrum == null) return;

        float[] frozenData;

        if (IsMorphEnabled && FreezeSlots.Count >= 2)
        {
            // Morph between two freeze slots
            frozenData = MorphBetweenSlots(MorphSourceSlot, MorphTargetSlot, MorphPosition);
        }
        else if (SelectedSlotIndex >= 0 && SelectedSlotIndex < FreezeSlots.Count)
        {
            frozenData = FreezeSlots[SelectedSlotIndex].SpectrumData ?? FrozenSpectrum ?? new float[BlendedSpectrum.Length];
        }
        else
        {
            frozenData = FrozenSpectrum ?? new float[BlendedSpectrum.Length];
        }

        int length = Math.Min(LiveSpectrum.Length, BlendedSpectrum.Length);
        length = Math.Min(length, frozenData.Length);

        for (int i = 0; i < length; i++)
        {
            // Apply spectral shift
            int sourceIndex = i;
            if (Math.Abs(SpectralShift) > 0.01f)
            {
                float shiftFactor = MathF.Pow(2f, SpectralShift / 12f);
                sourceIndex = (int)(i / shiftFactor);
                sourceIndex = Math.Clamp(sourceIndex, 0, length - 1);
            }

            // Get frozen value with blur/smear
            float frozenValue = GetBlurredValue(frozenData, sourceIndex, BlurAmount);

            // Apply spectral tilt
            if (Math.Abs(SpectralTilt) > 0.01f)
            {
                float freqRatio = (float)i / length;
                float tiltFactor = 1f + SpectralTilt * (freqRatio - 0.5f) * 2f;
                frozenValue *= Math.Max(0f, tiltFactor);
            }

            // Randomize bins if enabled
            if (RandomizeBins && IsFrozen)
            {
                if (_random.NextDouble() < 0.1)
                {
                    frozenValue *= (float)(_random.NextDouble() * 0.5 + 0.75);
                }
            }

            // Apply freeze decay
            if (FreezeDecay > 0.01f && IsFrozen)
            {
                frozenValue *= 1f - FreezeDecay * 0.01f;
            }

            // Blend between live and frozen
            float blend = IsFrozen ? FreezeBlend : 0f;
            BlendedSpectrum[i] = LiveSpectrum[i] * (1f - blend) + frozenValue * blend;

            // Apply feedback
            if (FeedbackAmount > 0.01f)
            {
                BlendedSpectrum[i] += BlendedSpectrum[i] * FeedbackAmount * 0.1f;
            }

            // Clamp to valid range
            BlendedSpectrum[i] = Math.Clamp(BlendedSpectrum[i], 0f, 1f);
        }

        OnPropertyChanged(nameof(BlendedSpectrum));
    }

    /// <summary>
    /// Gets a blurred/smeared value from the spectrum.
    /// </summary>
    private float GetBlurredValue(float[] spectrum, int index, float blurAmount)
    {
        if (blurAmount < 0.01f || spectrum.Length == 0)
        {
            return index < spectrum.Length ? spectrum[index] : 0f;
        }

        int blurRadius = (int)(blurAmount * 10);
        if (blurRadius == 0) return spectrum[index];

        float sum = 0f;
        float weightSum = 0f;

        for (int offset = -blurRadius; offset <= blurRadius; offset++)
        {
            int sampleIndex = index + offset;
            if (sampleIndex >= 0 && sampleIndex < spectrum.Length)
            {
                float weight = 1f - (float)Math.Abs(offset) / (blurRadius + 1);
                sum += spectrum[sampleIndex] * weight;
                weightSum += weight;
            }
        }

        return weightSum > 0 ? sum / weightSum : 0f;
    }

    /// <summary>
    /// Morphs between two freeze slots.
    /// </summary>
    private float[] MorphBetweenSlots(int sourceSlot, int targetSlot, float position)
    {
        var sourceData = sourceSlot >= 0 && sourceSlot < FreezeSlots.Count
            ? FreezeSlots[sourceSlot].SpectrumData
            : null;

        var targetData = targetSlot >= 0 && targetSlot < FreezeSlots.Count
            ? FreezeSlots[targetSlot].SpectrumData
            : null;

        int length = SelectedFftSize / 2 + 1;
        var result = new float[length];

        if (sourceData == null && targetData == null)
        {
            return result;
        }

        for (int i = 0; i < length; i++)
        {
            float sourceVal = sourceData != null && i < sourceData.Length ? sourceData[i] : 0f;
            float targetVal = targetData != null && i < targetData.Length ? targetData[i] : 0f;
            result[i] = sourceVal * (1f - position) + targetVal * position;
        }

        return result;
    }

    #region Property Changed Handlers

    partial void OnSelectedFftSizeChanged(int value)
    {
        // Resize spectrum arrays
        int numBins = value / 2 + 1;
        LiveSpectrum = new float[numBins];
        FrozenSpectrum = new float[numBins];
        BlendedSpectrum = new float[numBins];

        ParameterChanged?.Invoke(this, nameof(SelectedFftSize));
    }

    partial void OnIsFrozenChanged(bool value)
    {
        FreezeStateChanged?.Invoke(this, value);
        ParameterChanged?.Invoke(this, nameof(IsFrozen));

        if (value)
        {
            StatusMessage = "Spectrum frozen";
        }
        else
        {
            StatusMessage = "Live spectrum";
        }
    }

    partial void OnFreezeBlendChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(FreezeBlend));
    }

    partial void OnSpectralShiftChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(SpectralShift));
    }

    partial void OnSpectralTiltChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(SpectralTilt));
    }

    partial void OnBlurAmountChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(BlurAmount));
    }

    partial void OnFeedbackAmountChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(FeedbackAmount));
    }

    partial void OnFreezeDecayChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(FreezeDecay));
    }

    partial void OnRandomizeBinsChanged(bool value)
    {
        ParameterChanged?.Invoke(this, nameof(RandomizeBins));
    }

    partial void OnMorphPositionChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(MorphPosition));
    }

    partial void OnIsMorphEnabledChanged(bool value)
    {
        ParameterChanged?.Invoke(this, nameof(IsMorphEnabled));
        StatusMessage = value ? "Morph mode enabled" : "Morph mode disabled";
    }

    partial void OnSelectedSlotIndexChanged(int value)
    {
        // Update slot selection states
        for (int i = 0; i < FreezeSlots.Count; i++)
        {
            FreezeSlots[i].IsSelected = i == value;
        }

        // Load frozen spectrum from selected slot
        if (value >= 0 && value < FreezeSlots.Count && FreezeSlots[value].SpectrumData != null)
        {
            FrozenSpectrum = FreezeSlots[value].SpectrumData;
        }

        ParameterChanged?.Invoke(this, nameof(SelectedSlotIndex));
    }

    partial void OnIsBypassedChanged(bool value)
    {
        BypassChanged?.Invoke(this, value);
        StatusMessage = value ? "Effect bypassed" : "Effect active";
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void CaptureFreeze()
    {
        CaptureToSlot(SelectedSlotIndex);
    }

    [RelayCommand]
    private void CaptureToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= FreezeSlots.Count) return;

        // Copy current live spectrum to slot
        var slot = FreezeSlots[slotIndex];
        if (LiveSpectrum != null)
        {
            slot.SpectrumData = (float[])LiveSpectrum.Clone();
            slot.HasData = true;
            slot.CaptureTime = DateTime.Now;
            slot.Name = $"Capture {slot.CaptureTime:HH:mm:ss}";
            slot.NotifyDisplayLabelChanged();

            // Also update frozen spectrum for display
            FrozenSpectrum = slot.SpectrumData;
            IsFrozen = true;

            StatusMessage = $"Captured to slot {slotIndex + 1}";
        }

        FreezeCaptureRequested?.Invoke(this, slotIndex);
    }

    [RelayCommand]
    private void ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= FreezeSlots.Count) return;

        var slot = FreezeSlots[slotIndex];
        slot.SpectrumData = null;
        slot.HasData = false;
        slot.CaptureTime = null;
        slot.Name = $"Snapshot {slotIndex + 1}";
        slot.NotifyDisplayLabelChanged();

        StatusMessage = $"Cleared slot {slotIndex + 1}";
    }

    [RelayCommand]
    private void SelectSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < FreezeSlots.Count)
        {
            SelectedSlotIndex = slotIndex;
        }
    }

    [RelayCommand]
    private void ToggleFreeze()
    {
        IsFrozen = !IsFrozen;
    }

    [RelayCommand]
    private void ToggleBypass()
    {
        IsBypassed = !IsBypassed;
    }

    [RelayCommand]
    private void ToggleMorph()
    {
        IsMorphEnabled = !IsMorphEnabled;
    }

    [RelayCommand]
    private void ResetParameters()
    {
        FreezeBlend = 0.5f;
        SpectralShift = 0f;
        SpectralTilt = 0f;
        BlurAmount = 0f;
        FeedbackAmount = 0f;
        FreezeDecay = 0f;
        RandomizeBins = false;
        MorphPosition = 0f;
        IsMorphEnabled = false;

        StatusMessage = "Parameters reset to defaults";
    }

    [RelayCommand]
    private void ApplyPreset(string presetName)
    {
        switch (presetName)
        {
            case "Clean Freeze":
                FreezeBlend = 1f;
                SpectralShift = 0f;
                SpectralTilt = 0f;
                BlurAmount = 0f;
                FeedbackAmount = 0f;
                FreezeDecay = 0f;
                RandomizeBins = false;
                break;

            case "Shimmer":
                FreezeBlend = 0.7f;
                SpectralShift = 12f;
                SpectralTilt = 0.3f;
                BlurAmount = 0.2f;
                FeedbackAmount = 0.3f;
                FreezeDecay = 0.1f;
                RandomizeBins = false;
                break;

            case "Dark Drone":
                FreezeBlend = 0.9f;
                SpectralShift = -12f;
                SpectralTilt = -0.5f;
                BlurAmount = 0.5f;
                FeedbackAmount = 0.2f;
                FreezeDecay = 0.05f;
                RandomizeBins = false;
                break;

            case "Glitch":
                FreezeBlend = 0.8f;
                SpectralShift = 0f;
                SpectralTilt = 0f;
                BlurAmount = 0f;
                FeedbackAmount = 0f;
                FreezeDecay = 0f;
                RandomizeBins = true;
                break;

            case "Ambient Pad":
                FreezeBlend = 0.6f;
                SpectralShift = 0f;
                SpectralTilt = 0.2f;
                BlurAmount = 0.8f;
                FeedbackAmount = 0.4f;
                FreezeDecay = 0.02f;
                RandomizeBins = false;
                break;
        }

        StatusMessage = $"Applied preset: {presetName}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the live spectrum data.
    /// </summary>
    public void UpdateLiveSpectrum(float[] data)
    {
        if (data == null) return;

        if (LiveSpectrum == null || LiveSpectrum.Length != data.Length)
        {
            LiveSpectrum = new float[data.Length];
        }

        Array.Copy(data, LiveSpectrum, data.Length);
        OnPropertyChanged(nameof(LiveSpectrum));
    }

    /// <summary>
    /// Updates the frozen spectrum data.
    /// </summary>
    public void UpdateFrozenSpectrum(float[] data)
    {
        if (data == null) return;

        if (FrozenSpectrum == null || FrozenSpectrum.Length != data.Length)
        {
            FrozenSpectrum = new float[data.Length];
        }

        Array.Copy(data, FrozenSpectrum, data.Length);
        OnPropertyChanged(nameof(FrozenSpectrum));
    }

    /// <summary>
    /// Gets the processed output spectrum.
    /// </summary>
    public float[]? GetOutputSpectrum()
    {
        return IsBypassed ? LiveSpectrum : BlendedSpectrum;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _updateTimer = null;
    }
}
