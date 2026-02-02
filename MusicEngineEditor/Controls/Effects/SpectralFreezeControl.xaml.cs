// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Spectral Freeze effect editor control with spectrum capture, morph, and modulation.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Spectral freeze effect editor control with spectrum visualization, freeze capture,
/// multiple snapshot slots, morphing, and comprehensive parameter controls.
/// </summary>
public partial class SpectralFreezeControl : UserControl
{
    #region Constants

    private const double MinDb = -80.0;
    private const double MaxDb = 0.0;
    private const double MinFrequency = 20.0;
    private const double MaxFrequency = 20000.0;
    private const int DefaultNumBins = 128;
    private const double BarSpacing = 1.0;

    #endregion

    #region Private Fields

    private int _numBins = DefaultNumBins;
    private float[] _liveSpectrum = new float[DefaultNumBins];
    private float[] _frozenSpectrum = new float[DefaultNumBins];
    private float[] _blendedSpectrum = new float[DefaultNumBins];
    private float[][] _freezeSlots = new float[4][];
    private bool[] _slotHasData = new bool[4];
    private int _selectedSlot;

    private Shapes.Rectangle[]? _liveSpectrumBars;
    private Shapes.Rectangle[]? _frozenSpectrumBars;
    private Shapes.Rectangle[]? _blendedSpectrumBars;

    private bool _isInitialized;
    private bool _isFrozen;
    private bool _isBypassed;
    private bool _isMorphEnabled;

    private DispatcherTimer? _updateTimer;
    private Random _random = new();

    #endregion

    #region Events

    /// <summary>
    /// Raised when a parameter value changes.
    /// </summary>
    public event EventHandler<SpectralFreezeParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Raised when the freeze state changes.
    /// </summary>
    public event EventHandler<bool>? FreezeStateChanged;

    /// <summary>
    /// Raised when the bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Raised when a freeze capture is requested.
    /// </summary>
    public event EventHandler<int>? FreezeCaptureRequested;

    /// <summary>
    /// Raised when morph state changes.
    /// </summary>
    public event EventHandler<bool>? MorphStateChanged;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty LiveSpectrumProperty =
        DependencyProperty.Register(nameof(LiveSpectrum), typeof(float[]), typeof(SpectralFreezeControl),
            new PropertyMetadata(null, OnLiveSpectrumChanged));

    public static readonly DependencyProperty FrozenSpectrumProperty =
        DependencyProperty.Register(nameof(FrozenSpectrum), typeof(float[]), typeof(SpectralFreezeControl),
            new PropertyMetadata(null, OnFrozenSpectrumChanged));

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(SpectralFreezeControl),
            new PropertyMetadata(44100));

    public static readonly DependencyProperty FftSizeProperty =
        DependencyProperty.Register(nameof(FftSize), typeof(int), typeof(SpectralFreezeControl),
            new PropertyMetadata(2048, OnFftSizeChanged));

    /// <summary>
    /// Gets or sets the live spectrum magnitude data for display.
    /// </summary>
    public float[]? LiveSpectrum
    {
        get => (float[]?)GetValue(LiveSpectrumProperty);
        set => SetValue(LiveSpectrumProperty, value);
    }

    /// <summary>
    /// Gets or sets the frozen spectrum magnitude data for display.
    /// </summary>
    public float[]? FrozenSpectrum
    {
        get => (float[]?)GetValue(FrozenSpectrumProperty);
        set => SetValue(FrozenSpectrumProperty, value);
    }

    /// <summary>
    /// Gets or sets the sample rate used for frequency calculations.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the FFT size.
    /// </summary>
    public int FftSize
    {
        get => (int)GetValue(FftSizeProperty);
        set => SetValue(FftSizeProperty, value);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the freeze blend amount (0-1).
    /// </summary>
    public double FreezeBlend
    {
        get => FreezeBlendSlider.Value / 100.0;
        set => FreezeBlendSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets the spectral shift in semitones.
    /// </summary>
    public double SpectralShift
    {
        get => SpectralShiftSlider.Value;
        set => SpectralShiftSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the spectral tilt (-1 to 1).
    /// </summary>
    public double SpectralTilt
    {
        get => SpectralTiltSlider.Value / 100.0;
        set => SpectralTiltSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets the blur/smear amount (0-1).
    /// </summary>
    public double BlurAmount
    {
        get => BlurAmountSlider.Value / 100.0;
        set => BlurAmountSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets the feedback amount (0-1).
    /// </summary>
    public double FeedbackAmount
    {
        get => FeedbackAmountSlider.Value / 100.0;
        set => FeedbackAmountSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets the freeze decay amount (0-1).
    /// </summary>
    public double FreezeDecay
    {
        get => FreezeDecaySlider.Value / 100.0;
        set => FreezeDecaySlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets whether bins are randomized.
    /// </summary>
    public bool RandomizeBins
    {
        get => RandomizeBinsCheckBox.IsChecked == true;
        set => RandomizeBinsCheckBox.IsChecked = value;
    }

    /// <summary>
    /// Gets or sets whether the spectrum is frozen.
    /// </summary>
    public bool IsFrozen
    {
        get => _isFrozen;
        set
        {
            _isFrozen = value;
            FreezeToggle.IsChecked = value;
        }
    }

    /// <summary>
    /// Gets or sets whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            _isBypassed = value;
            BypassToggle.IsChecked = value;
        }
    }

    /// <summary>
    /// Gets or sets whether morph mode is enabled.
    /// </summary>
    public bool IsMorphEnabled
    {
        get => _isMorphEnabled;
        set
        {
            _isMorphEnabled = value;
            MorphToggle.IsChecked = value;
        }
    }

    /// <summary>
    /// Gets or sets the morph position (0-1).
    /// </summary>
    public double MorphPosition
    {
        get => MorphPositionSlider.Value / 100.0;
        set => MorphPositionSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets the selected slot index.
    /// </summary>
    public int SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            _selectedSlot = Math.Clamp(value, 0, 3);
            UpdateSlotButtonStates();
        }
    }

    #endregion

    #region Constructor

    public SpectralFreezeControl()
    {
        InitializeComponent();

        // Initialize freeze slots
        for (int i = 0; i < 4; i++)
        {
            _freezeSlots[i] = new float[_numBins];
            _slotHasData[i] = false;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildVisualTree();
        _isInitialized = true;
        UpdateSlotButtonStates();

        // Start update timer for animations
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _updateTimer?.Stop();
        _updateTimer = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateLayoutPositions();
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_isInitialized)
        {
            CalculateBlendedSpectrum();
            UpdateSpectrumDisplay();
        }
    }

    #endregion

    #region Visual Tree Building

    private void BuildVisualTree()
    {
        SpectrumCanvas.Children.Clear();
        DbScaleCanvas.Children.Clear();
        FrequencyLabelCanvas.Children.Clear();

        _liveSpectrumBars = new Shapes.Rectangle[_numBins];
        _frozenSpectrumBars = new Shapes.Rectangle[_numBins];
        _blendedSpectrumBars = new Shapes.Rectangle[_numBins];

        var liveBrush = FindResource("SpectralFreezeLiveBrush") as Brush ?? Brushes.Cyan;
        var frozenBrush = FindResource("SpectralFreezeFrozenBrush") as Brush ?? Brushes.Purple;
        var blendBrush = FindResource("SpectralFreezeBlendBrush") as Brush ?? Brushes.Green;

        // Create spectrum bars (layered: blended on bottom, then frozen, then live on top)
        for (int i = 0; i < _numBins; i++)
        {
            // Blended spectrum bars (green)
            var blendBar = new Shapes.Rectangle
            {
                Fill = blendBrush,
                Opacity = 0.6,
                RadiusX = 1,
                RadiusY = 1
            };
            _blendedSpectrumBars[i] = blendBar;
            SpectrumCanvas.Children.Add(blendBar);

            // Frozen spectrum bars (purple) - shown as outline/overlay
            var frozenBar = new Shapes.Rectangle
            {
                Fill = frozenBrush,
                Opacity = 0.5,
                RadiusX = 1,
                RadiusY = 1
            };
            _frozenSpectrumBars[i] = frozenBar;
            SpectrumCanvas.Children.Add(frozenBar);

            // Live spectrum bars (cyan)
            var liveBar = new Shapes.Rectangle
            {
                Fill = liveBrush,
                Opacity = 0.8,
                RadiusX = 1,
                RadiusY = 1
            };
            _liveSpectrumBars[i] = liveBar;
            SpectrumCanvas.Children.Add(liveBar);
        }

        // Draw scales
        DrawDbScale();
        DrawFrequencyLabels();
        UpdateLayoutPositions();
    }

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        var textBrush = FindResource("SpectralFreezeSecondaryTextBrush") as Brush ?? Brushes.Gray;
        var gridBrush = FindResource("SpectralFreezeBorderBrush") as Brush ?? Brushes.DarkGray;

        double height = SpectrumCanvas.ActualHeight > 0 ? SpectrumCanvas.ActualHeight : 180;
        double[] dbMarks = { 0, -12, -24, -36, -48, -60, -72 };

        foreach (var db in dbMarks)
        {
            double normalizedLevel = (db - MinDb) / (MaxDb - MinDb);
            double y = height * (1 - normalizedLevel);

            // Tick mark
            var tick = new Shapes.Line
            {
                X1 = 34,
                Y1 = y,
                X2 = 38,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            DbScaleCanvas.Children.Add(tick);

            // Horizontal grid line on spectrum canvas
            var gridLine = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = SpectrumCanvas.ActualWidth,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                Opacity = 0.3
            };
            SpectrumCanvas.Children.Insert(0, gridLine);

            // Label
            var label = new TextBlock
            {
                Text = db == 0 ? "0" : $"{db}",
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 6);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            DbScaleCanvas.Children.Add(label);
        }
    }

    private void DrawFrequencyLabels()
    {
        FrequencyLabelCanvas.Children.Clear();

        var textBrush = FindResource("SpectralFreezeSecondaryTextBrush") as Brush ?? Brushes.Gray;
        double width = SpectrumCanvas.ActualWidth;
        if (width <= 0) return;

        double[] frequencies = { 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };

        foreach (var freq in frequencies)
        {
            double x = FrequencyToX(freq, width);
            if (x < 0 || x > width) continue;

            string text = freq >= 1000 ? $"{freq / 1000}k" : $"{freq}";

            var label = new TextBlock
            {
                Text = text,
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Center
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 2);
            FrequencyLabelCanvas.Children.Add(label);
        }
    }

    private void UpdateLayoutPositions()
    {
        if (_liveSpectrumBars == null || _frozenSpectrumBars == null || _blendedSpectrumBars == null) return;

        double spectrumWidth = SpectrumCanvas.ActualWidth;
        double spectrumHeight = SpectrumCanvas.ActualHeight;

        if (spectrumWidth <= 0 || spectrumHeight <= 0) return;

        double barWidth = Math.Max(1, (spectrumWidth - (_numBins - 1) * BarSpacing) / _numBins);

        for (int i = 0; i < _numBins; i++)
        {
            double x = i * (barWidth + BarSpacing);

            // Live bars
            _liveSpectrumBars[i].Width = Math.Max(1, barWidth * 0.3);
            Canvas.SetLeft(_liveSpectrumBars[i], x);
            Canvas.SetBottom(_liveSpectrumBars[i], 0);

            // Frozen bars (offset slightly)
            _frozenSpectrumBars[i].Width = Math.Max(1, barWidth * 0.3);
            Canvas.SetLeft(_frozenSpectrumBars[i], x + barWidth * 0.35);
            Canvas.SetBottom(_frozenSpectrumBars[i], 0);

            // Blended bars (offset)
            _blendedSpectrumBars[i].Width = Math.Max(1, barWidth * 0.3);
            Canvas.SetLeft(_blendedSpectrumBars[i], x + barWidth * 0.7);
            Canvas.SetBottom(_blendedSpectrumBars[i], 0);
        }

        // Redraw scales
        DrawDbScale();
        DrawFrequencyLabels();
    }

    #endregion

    #region Spectrum Calculations

    private void CalculateBlendedSpectrum()
    {
        if (_liveSpectrum == null || _blendedSpectrum == null) return;

        float[] frozenData = _isMorphEnabled ? GetMorphedSpectrum() : GetSelectedFrozenSpectrum();

        float blendAmount = _isFrozen ? (float)(FreezeBlendSlider.Value / 100.0) : 0f;
        float shift = (float)SpectralShiftSlider.Value;
        float tilt = (float)(SpectralTiltSlider.Value / 100.0);
        float blur = (float)(BlurAmountSlider.Value / 100.0);
        float feedback = (float)(FeedbackAmountSlider.Value / 100.0);
        float decay = (float)(FreezeDecaySlider.Value / 100.0);
        bool randomize = RandomizeBinsCheckBox.IsChecked == true;

        int length = Math.Min(_liveSpectrum.Length, _blendedSpectrum.Length);
        length = Math.Min(length, frozenData.Length);

        for (int i = 0; i < length; i++)
        {
            // Apply spectral shift
            int sourceIndex = i;
            if (Math.Abs(shift) > 0.01f)
            {
                float shiftFactor = MathF.Pow(2f, shift / 12f);
                sourceIndex = (int)(i / shiftFactor);
                sourceIndex = Math.Clamp(sourceIndex, 0, length - 1);
            }

            // Get frozen value with blur
            float frozenValue = GetBlurredValue(frozenData, sourceIndex, blur);

            // Apply spectral tilt
            if (Math.Abs(tilt) > 0.01f)
            {
                float freqRatio = (float)i / length;
                float tiltFactor = 1f + tilt * (freqRatio - 0.5f) * 2f;
                frozenValue *= Math.Max(0f, tiltFactor);
            }

            // Randomize bins if enabled
            if (randomize && _isFrozen)
            {
                if (_random.NextDouble() < 0.1)
                {
                    frozenValue *= (float)(_random.NextDouble() * 0.5 + 0.75);
                }
            }

            // Apply freeze decay
            if (decay > 0.01f && _isFrozen)
            {
                frozenValue *= 1f - decay * 0.01f;
            }

            // Blend between live and frozen
            _blendedSpectrum[i] = _liveSpectrum[i] * (1f - blendAmount) + frozenValue * blendAmount;

            // Apply feedback
            if (feedback > 0.01f)
            {
                _blendedSpectrum[i] += _blendedSpectrum[i] * feedback * 0.1f;
            }

            // Clamp to valid range
            _blendedSpectrum[i] = Math.Clamp(_blendedSpectrum[i], 0f, 1f);
        }
    }

    private float[] GetSelectedFrozenSpectrum()
    {
        if (_selectedSlot >= 0 && _selectedSlot < 4 && _slotHasData[_selectedSlot])
        {
            return _freezeSlots[_selectedSlot];
        }
        return _frozenSpectrum;
    }

    private float[] GetMorphedSpectrum()
    {
        int sourceSlot = MorphSourceComboBox.SelectedIndex;
        int targetSlot = MorphTargetComboBox.SelectedIndex;
        float position = (float)(MorphPositionSlider.Value / 100.0);

        float[] sourceData = (sourceSlot >= 0 && sourceSlot < 4 && _slotHasData[sourceSlot])
            ? _freezeSlots[sourceSlot]
            : _frozenSpectrum;

        float[] targetData = (targetSlot >= 0 && targetSlot < 4 && _slotHasData[targetSlot])
            ? _freezeSlots[targetSlot]
            : _frozenSpectrum;

        float[] result = new float[_numBins];

        for (int i = 0; i < _numBins; i++)
        {
            float sourceVal = i < sourceData.Length ? sourceData[i] : 0f;
            float targetVal = i < targetData.Length ? targetData[i] : 0f;
            result[i] = sourceVal * (1f - position) + targetVal * position;
        }

        return result;
    }

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

    #endregion

    #region Display Updates

    private void UpdateSpectrumDisplay()
    {
        if (_liveSpectrumBars == null || _frozenSpectrumBars == null || _blendedSpectrumBars == null) return;

        double height = SpectrumCanvas.ActualHeight;
        if (height <= 0) return;

        var liveData = LiveSpectrum ?? _liveSpectrum;
        var frozenData = GetSelectedFrozenSpectrum();

        int count = Math.Min(_numBins, liveData.Length);
        count = Math.Min(count, _blendedSpectrum.Length);

        for (int i = 0; i < count; i++)
        {
            // Live spectrum bar
            double liveMag = Math.Clamp(liveData[i], 0, 1);
            _liveSpectrumBars[i].Height = Math.Max(0, height * liveMag);

            // Frozen spectrum bar
            double frozenMag = Math.Clamp(frozenData[i], 0, 1);
            _frozenSpectrumBars[i].Height = Math.Max(0, height * frozenMag);
            _frozenSpectrumBars[i].Opacity = _isFrozen ? 0.7 : 0.2;

            // Blended spectrum bar
            double blendMag = Math.Clamp(_blendedSpectrum[i], 0, 1);
            _blendedSpectrumBars[i].Height = Math.Max(0, height * blendMag);
            _blendedSpectrumBars[i].Opacity = _isFrozen ? 0.9 : 0.3;
        }
    }

    private void UpdateSlotButtonStates()
    {
        var selectedBrush = FindResource("SpectralFreezeFrozenBrush") as Brush ?? Brushes.Purple;
        var normalBrush = FindResource("SpectralFreezePanelBrush") as Brush ?? Brushes.DarkGray;
        var hasDataBrush = FindResource("SpectralFreezeAccentBrush") as Brush ?? Brushes.Cyan;

        Button[] slotButtons = { Slot1Button, Slot2Button, Slot3Button, Slot4Button };

        for (int i = 0; i < 4; i++)
        {
            var button = slotButtons[i];
            if (button == null) continue;

            if (i == _selectedSlot)
            {
                button.Background = selectedBrush;
                button.Foreground = Brushes.White;
            }
            else if (_slotHasData[i])
            {
                button.Background = normalBrush;
                button.BorderBrush = hasDataBrush;
                button.Foreground = FindResource("SpectralFreezeTextBrush") as Brush ?? Brushes.White;
            }
            else
            {
                button.Background = normalBrush;
                button.BorderBrush = FindResource("SpectralFreezeBorderBrush") as Brush ?? Brushes.DarkGray;
                button.Foreground = FindResource("SpectralFreezeSecondaryTextBrush") as Brush ?? Brushes.Gray;
            }
        }
    }

    #endregion

    #region Event Handlers

    private void FftSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FftSizeComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int fftSize))
        {
            FftSize = fftSize;
            _numBins = fftSize / 8; // Display bins are reduced from FFT size

            // Resize arrays
            _liveSpectrum = new float[_numBins];
            _frozenSpectrum = new float[_numBins];
            _blendedSpectrum = new float[_numBins];

            for (int i = 0; i < 4; i++)
            {
                _freezeSlots[i] = new float[_numBins];
            }

            if (_isInitialized)
            {
                BuildVisualTree();
            }

            RaiseParameterChanged("FftSize", fftSize);
        }
    }

    private void FreezeToggle_Click(object sender, RoutedEventArgs e)
    {
        _isFrozen = FreezeToggle.IsChecked == true;

        if (_isFrozen)
        {
            // Capture current spectrum
            CaptureToCurrentSlot();
            StatusText.Text = "Spectrum frozen";
        }
        else
        {
            StatusText.Text = "Live spectrum";
        }

        FreezeStateChanged?.Invoke(this, _isFrozen);
        RaiseParameterChanged("IsFrozen", _isFrozen ? 1f : 0f);
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = BypassToggle.IsChecked == true;
        StatusText.Text = _isBypassed ? "Effect bypassed" : "Effect active";
        BypassChanged?.Invoke(this, _isBypassed);
    }

    private void SlotButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagStr && int.TryParse(tagStr, out int slotIndex))
        {
            _selectedSlot = slotIndex;
            UpdateSlotButtonStates();

            // Load frozen spectrum from selected slot if it has data
            if (_slotHasData[slotIndex])
            {
                Array.Copy(_freezeSlots[slotIndex], _frozenSpectrum, Math.Min(_freezeSlots[slotIndex].Length, _frozenSpectrum.Length));
            }

            StatusText.Text = $"Selected slot {slotIndex + 1}";
        }
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureToCurrentSlot();
    }

    private void ClearSlotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSlot >= 0 && _selectedSlot < 4)
        {
            Array.Clear(_freezeSlots[_selectedSlot], 0, _freezeSlots[_selectedSlot].Length);
            _slotHasData[_selectedSlot] = false;
            UpdateSlotButtonStates();
            StatusText.Text = $"Cleared slot {_selectedSlot + 1}";
        }
    }

    private void CaptureToCurrentSlot()
    {
        if (_selectedSlot >= 0 && _selectedSlot < 4)
        {
            // Copy live spectrum to both frozen and slot
            var liveData = LiveSpectrum ?? _liveSpectrum;
            int length = Math.Min(liveData.Length, _freezeSlots[_selectedSlot].Length);

            Array.Copy(liveData, _freezeSlots[_selectedSlot], length);
            Array.Copy(liveData, _frozenSpectrum, Math.Min(liveData.Length, _frozenSpectrum.Length));

            _slotHasData[_selectedSlot] = true;
            UpdateSlotButtonStates();

            FreezeCaptureRequested?.Invoke(this, _selectedSlot);
            StatusText.Text = $"Captured to slot {_selectedSlot + 1}";
        }
    }

    private void MorphToggle_Click(object sender, RoutedEventArgs e)
    {
        _isMorphEnabled = MorphToggle.IsChecked == true;
        StatusText.Text = _isMorphEnabled ? "Morph mode enabled" : "Morph mode disabled";
        MorphStateChanged?.Invoke(this, _isMorphEnabled);
        RaiseParameterChanged("IsMorphEnabled", _isMorphEnabled ? 1f : 0f);
    }

    private void MorphSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RaiseParameterChanged("MorphSource", MorphSourceComboBox.SelectedIndex);
    }

    private void MorphTargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RaiseParameterChanged("MorphTarget", MorphTargetComboBox.SelectedIndex);
    }

    private void FreezeBlendSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FreezeBlendValue == null) return;
        FreezeBlendValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("FreezeBlend", (float)(e.NewValue / 100.0));
    }

    private void SpectralShiftSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpectralShiftValue == null) return;
        SpectralShiftValue.Text = $"{e.NewValue:F0} st";
        RaiseParameterChanged("SpectralShift", (float)e.NewValue);
    }

    private void SpectralTiltSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpectralTiltValue == null) return;
        SpectralTiltValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("SpectralTilt", (float)(e.NewValue / 100.0));
    }

    private void BlurAmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BlurAmountValue == null) return;
        BlurAmountValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("BlurAmount", (float)(e.NewValue / 100.0));
    }

    private void FeedbackAmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FeedbackAmountValue == null) return;
        FeedbackAmountValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("FeedbackAmount", (float)(e.NewValue / 100.0));
    }

    private void FreezeDecaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FreezeDecayValue == null) return;
        FreezeDecayValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("FreezeDecay", (float)(e.NewValue / 100.0));
    }

    private void RandomizeBinsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RaiseParameterChanged("RandomizeBins", RandomizeBinsCheckBox.IsChecked == true ? 1f : 0f);
    }

    private void MorphPositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MorphPositionValue == null) return;
        MorphPositionValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("MorphPosition", (float)(e.NewValue / 100.0));
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedIndex <= 0) return;

        var item = PresetComboBox.SelectedItem as ComboBoxItem;
        if (item == null) return;

        string preset = item.Content?.ToString() ?? "";
        ApplyPreset(preset);

        // Reset to placeholder
        PresetComboBox.SelectedIndex = 0;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Reset();
    }

    #endregion

    #region Presets

    private void ApplyPreset(string preset)
    {
        switch (preset)
        {
            case "Clean Freeze":
                FreezeBlendSlider.Value = 100;
                SpectralShiftSlider.Value = 0;
                SpectralTiltSlider.Value = 0;
                BlurAmountSlider.Value = 0;
                FeedbackAmountSlider.Value = 0;
                FreezeDecaySlider.Value = 0;
                RandomizeBinsCheckBox.IsChecked = false;
                break;

            case "Shimmer":
                FreezeBlendSlider.Value = 70;
                SpectralShiftSlider.Value = 12;
                SpectralTiltSlider.Value = 30;
                BlurAmountSlider.Value = 20;
                FeedbackAmountSlider.Value = 30;
                FreezeDecaySlider.Value = 10;
                RandomizeBinsCheckBox.IsChecked = false;
                break;

            case "Dark Drone":
                FreezeBlendSlider.Value = 90;
                SpectralShiftSlider.Value = -12;
                SpectralTiltSlider.Value = -50;
                BlurAmountSlider.Value = 50;
                FeedbackAmountSlider.Value = 20;
                FreezeDecaySlider.Value = 5;
                RandomizeBinsCheckBox.IsChecked = false;
                break;

            case "Glitch":
                FreezeBlendSlider.Value = 80;
                SpectralShiftSlider.Value = 0;
                SpectralTiltSlider.Value = 0;
                BlurAmountSlider.Value = 0;
                FeedbackAmountSlider.Value = 0;
                FreezeDecaySlider.Value = 0;
                RandomizeBinsCheckBox.IsChecked = true;
                break;

            case "Ambient Pad":
                FreezeBlendSlider.Value = 60;
                SpectralShiftSlider.Value = 0;
                SpectralTiltSlider.Value = 20;
                BlurAmountSlider.Value = 80;
                FeedbackAmountSlider.Value = 40;
                FreezeDecaySlider.Value = 2;
                RandomizeBinsCheckBox.IsChecked = false;
                break;
        }

        StatusText.Text = $"Applied preset: {preset}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the live spectrum display data.
    /// </summary>
    public void UpdateLiveSpectrum(float[] magnitudes)
    {
        if (magnitudes == null) return;

        if (_liveSpectrum.Length != magnitudes.Length)
        {
            _numBins = magnitudes.Length;
            _liveSpectrum = new float[_numBins];
            _frozenSpectrum = new float[_numBins];
            _blendedSpectrum = new float[_numBins];

            if (_isInitialized)
            {
                BuildVisualTree();
            }
        }

        Array.Copy(magnitudes, _liveSpectrum, _numBins);
    }

    /// <summary>
    /// Sets the frozen spectrum data directly.
    /// </summary>
    public void SetFrozenSpectrum(float[] spectrum, int? slotIndex = null)
    {
        if (spectrum == null) return;

        int length = Math.Min(spectrum.Length, _frozenSpectrum.Length);
        Array.Copy(spectrum, _frozenSpectrum, length);

        if (slotIndex.HasValue && slotIndex.Value >= 0 && slotIndex.Value < 4)
        {
            Array.Copy(spectrum, _freezeSlots[slotIndex.Value], Math.Min(spectrum.Length, _freezeSlots[slotIndex.Value].Length));
            _slotHasData[slotIndex.Value] = true;
            UpdateSlotButtonStates();
        }
    }

    /// <summary>
    /// Gets the current blended/processed spectrum output.
    /// </summary>
    public float[] GetOutputSpectrum()
    {
        return _isBypassed ? _liveSpectrum : _blendedSpectrum;
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    public void Reset()
    {
        FreezeBlendSlider.Value = 50;
        SpectralShiftSlider.Value = 0;
        SpectralTiltSlider.Value = 0;
        BlurAmountSlider.Value = 0;
        FeedbackAmountSlider.Value = 0;
        FreezeDecaySlider.Value = 0;
        RandomizeBinsCheckBox.IsChecked = false;
        MorphPositionSlider.Value = 0;

        FreezeToggle.IsChecked = false;
        _isFrozen = false;

        MorphToggle.IsChecked = false;
        _isMorphEnabled = false;

        BypassToggle.IsChecked = false;
        _isBypassed = false;

        _selectedSlot = 0;
        UpdateSlotButtonStates();

        StatusText.Text = "Reset to defaults";
    }

    #endregion

    #region Helper Methods

    private void RaiseParameterChanged(string name, float value)
    {
        ParameterChanged?.Invoke(this, new SpectralFreezeParameterChangedEventArgs(name, value));
    }

    private double FrequencyToX(double frequency, double width)
    {
        double t = Math.Log(frequency / MinFrequency) / Math.Log(MaxFrequency / MinFrequency);
        return t * width;
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnLiveSpectrumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralFreezeControl control && e.NewValue is float[] magnitudes)
        {
            control.UpdateLiveSpectrum(magnitudes);
        }
    }

    private static void OnFrozenSpectrumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralFreezeControl control && e.NewValue is float[] spectrum)
        {
            control.SetFrozenSpectrum(spectrum);
        }
    }

    private static void OnFftSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralFreezeControl control && e.NewValue is int fftSize)
        {
            control._numBins = fftSize / 8;
            if (control._isInitialized)
            {
                control.BuildVisualTree();
            }
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for spectral freeze parameter changes.
/// </summary>
public class SpectralFreezeParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public SpectralFreezeParameterChangedEventArgs(string parameterName, float value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}
