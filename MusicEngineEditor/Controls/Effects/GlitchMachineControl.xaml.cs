// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: GlitchMachine effect control with randomization, pattern sequencing,
// and multiple toggleable glitch effect modules.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MusicEngineEditor.ViewModels.Effects;

namespace MusicEngineEditor.Controls.Effects;

#region Converters

/// <summary>
/// Converts boolean values to visibility.
/// </summary>
public class GlitchBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts a percentage value to width.
/// </summary>
public class GlitchPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

/// <summary>
/// Converts step active state to brush.
/// </summary>
public class GlitchStepActiveConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive
                ? new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF))
                : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        }
        return new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion

#region Event Args

/// <summary>
/// Event arguments for glitch events.
/// </summary>
public class GlitchEventArgs : EventArgs
{
    /// <summary>Gets the effect type.</summary>
    public GlitchEffectType EffectType { get; }

    /// <summary>Gets the duration in milliseconds.</summary>
    public float DurationMs { get; }

    /// <summary>
    /// Creates new glitch event arguments.
    /// </summary>
    public GlitchEventArgs(GlitchEffectType effectType, float durationMs = 0)
    {
        EffectType = effectType;
        DurationMs = durationMs;
    }
}

/// <summary>
/// Event arguments for parameter changes.
/// </summary>
public class GlitchParameterChangedEventArgs : EventArgs
{
    /// <summary>Gets the parameter name.</summary>
    public string ParameterName { get; }

    /// <summary>Gets the new value.</summary>
    public float Value { get; }

    /// <summary>
    /// Creates new parameter changed event arguments.
    /// </summary>
    public GlitchParameterChangedEventArgs(string parameterName, float value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

#endregion

/// <summary>
/// GlitchMachine effect control providing multiple glitch effects with
/// randomization controls, pattern sequencing, and real-time waveform display.
/// </summary>
public partial class GlitchMachineControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(GlitchMachineViewModel), typeof(GlitchMachineControl),
            new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty TempoProperty =
        DependencyProperty.Register(nameof(Tempo), typeof(float), typeof(GlitchMachineControl),
            new PropertyMetadata(120f, OnTempoChanged));

    public static readonly DependencyProperty ChaosAmountProperty =
        DependencyProperty.Register(nameof(ChaosAmount), typeof(float), typeof(GlitchMachineControl),
            new PropertyMetadata(0.3f, OnChaosAmountChanged));

    public static readonly DependencyProperty TriggerRateProperty =
        DependencyProperty.Register(nameof(TriggerRate), typeof(float), typeof(GlitchMachineControl),
            new PropertyMetadata(4f, OnTriggerRateChanged));

    public static readonly DependencyProperty MixProperty =
        DependencyProperty.Register(nameof(Mix), typeof(float), typeof(GlitchMachineControl),
            new PropertyMetadata(0.5f, OnMixChanged));

    public static readonly DependencyProperty IsBypassedProperty =
        DependencyProperty.Register(nameof(IsBypassed), typeof(bool), typeof(GlitchMachineControl),
            new PropertyMetadata(false, OnIsBypassedChanged));

    public static readonly DependencyProperty SyncToTempoProperty =
        DependencyProperty.Register(nameof(SyncToTempo), typeof(bool), typeof(GlitchMachineControl),
            new PropertyMetadata(true, OnSyncToTempoChanged));

    /// <summary>
    /// Gets or sets the ViewModel.
    /// </summary>
    public GlitchMachineViewModel? ViewModel
    {
        get => (GlitchMachineViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Gets or sets the tempo in BPM.
    /// </summary>
    public float Tempo
    {
        get => (float)GetValue(TempoProperty);
        set => SetValue(TempoProperty, value);
    }

    /// <summary>
    /// Gets or sets the chaos amount (0.0 to 1.0).
    /// </summary>
    public float ChaosAmount
    {
        get => (float)GetValue(ChaosAmountProperty);
        set => SetValue(ChaosAmountProperty, value);
    }

    /// <summary>
    /// Gets or sets the trigger rate.
    /// </summary>
    public float TriggerRate
    {
        get => (float)GetValue(TriggerRateProperty);
        set => SetValue(TriggerRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the dry/wet mix (0.0 to 1.0).
    /// </summary>
    public float Mix
    {
        get => (float)GetValue(MixProperty);
        set => SetValue(MixProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => (bool)GetValue(IsBypassedProperty);
        set => SetValue(IsBypassedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether glitch timing syncs to tempo.
    /// </summary>
    public bool SyncToTempo
    {
        get => (bool)GetValue(SyncToTempoProperty);
        set => SetValue(SyncToTempoProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when a glitch is triggered.
    /// </summary>
    public event EventHandler<GlitchEventArgs>? GlitchTriggered;

    /// <summary>
    /// Fired when a glitch completes.
    /// </summary>
    public event EventHandler<GlitchEventArgs>? GlitchCompleted;

    /// <summary>
    /// Fired when a parameter changes.
    /// </summary>
    public event EventHandler<GlitchParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Fired when bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Fired when an effect module is toggled.
    /// </summary>
    public event EventHandler<GlitchEffectType>? EffectModuleToggled;

    #endregion

    #region Private Fields

    private GlitchMachineViewModel? _viewModel;
    private DispatcherTimer? _waveformTimer;
    private bool _isInitialized;
    private readonly ToggleButton[] _patternSteps = new ToggleButton[8];
    private int _currentStepIndex;

    #endregion

    #region Constructor

    public GlitchMachineControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        // Store pattern step references
        _patternSteps[0] = Step1;
        _patternSteps[1] = Step2;
        _patternSteps[2] = Step3;
        _patternSteps[3] = Step4;
        _patternSteps[4] = Step5;
        _patternSteps[5] = Step6;
        _patternSteps[6] = Step7;
        _patternSteps[7] = Step8;

        // Initialize ViewModel if not set
        if (_viewModel == null)
        {
            _viewModel = new GlitchMachineViewModel();
            SubscribeToViewModel();
        }

        // Start waveform update timer
        _waveformTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _waveformTimer.Tick += WaveformTimer_Tick;
        _waveformTimer.Start();

        // Update displays
        UpdateAllDisplays();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _waveformTimer?.Stop();
        _waveformTimer = null;

        UnsubscribeFromViewModel();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateWaveformDisplay();
        }
    }

    #endregion

    #region ViewModel Subscription

    private void SubscribeToViewModel()
    {
        if (_viewModel == null) return;

        _viewModel.GlitchTriggered += ViewModel_GlitchTriggered;
        _viewModel.GlitchCompleted += ViewModel_GlitchCompleted;
        _viewModel.WaveformUpdated += ViewModel_WaveformUpdated;
        _viewModel.PatternStepChanged += ViewModel_PatternStepChanged;
        _viewModel.ParameterChanged += ViewModel_ParameterChanged;
    }

    private void UnsubscribeFromViewModel()
    {
        if (_viewModel == null) return;

        _viewModel.GlitchTriggered -= ViewModel_GlitchTriggered;
        _viewModel.GlitchCompleted -= ViewModel_GlitchCompleted;
        _viewModel.WaveformUpdated -= ViewModel_WaveformUpdated;
        _viewModel.PatternStepChanged -= ViewModel_PatternStepChanged;
        _viewModel.ParameterChanged -= ViewModel_ParameterChanged;

        _viewModel.Dispose();
    }

    private void ViewModel_GlitchTriggered(object? sender, GlitchEffectType effectType)
    {
        UpdateGlitchIndicator(true, effectType);
        GlitchTriggered?.Invoke(this, new GlitchEventArgs(effectType));
    }

    private void ViewModel_GlitchCompleted(object? sender, GlitchEffectType effectType)
    {
        UpdateGlitchIndicator(false, effectType);
        GlitchCompleted?.Invoke(this, new GlitchEventArgs(effectType));
    }

    private void ViewModel_WaveformUpdated(object? sender, EventArgs e)
    {
        UpdateWaveformDisplay();
    }

    private void ViewModel_PatternStepChanged(object? sender, int stepIndex)
    {
        UpdatePatternStepIndicator(stepIndex);
    }

    private void ViewModel_ParameterChanged(object? sender, string parameterName)
    {
        ParameterChanged?.Invoke(this, new GlitchParameterChangedEventArgs(parameterName, 0));
    }

    #endregion

    #region Property Changed Handlers

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlitchMachineControl control)
        {
            if (e.OldValue is GlitchMachineViewModel oldVm)
            {
                control.UnsubscribeFromViewModel();
            }

            control._viewModel = e.NewValue as GlitchMachineViewModel;

            if (control._viewModel != null)
            {
                control.SubscribeToViewModel();
                control.SyncFromViewModel();
            }
        }
    }

    private static void OnTempoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlitchMachineControl control && control._isInitialized)
        {
            float tempo = (float)e.NewValue;
            control._viewModel?.SetTempo(tempo);
            control.UpdateTempoDisplay();
        }
    }

    private static void OnChaosAmountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlitchMachineControl control && control._isInitialized && control._viewModel != null)
        {
            control._viewModel.ChaosAmount = (float)e.NewValue;
            control.ChaosSlider.Value = (float)e.NewValue * 100;
        }
    }

    private static void OnTriggerRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlitchMachineControl control && control._isInitialized && control._viewModel != null)
        {
            control._viewModel.TriggerRate = (float)e.NewValue;
            control.RateSlider.Value = (float)e.NewValue;
        }
    }

    private static void OnMixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlitchMachineControl control && control._isInitialized && control._viewModel != null)
        {
            control._viewModel.Mix = (float)e.NewValue;
            control.MixSlider.Value = (float)e.NewValue * 100;
        }
    }

    private static void OnIsBypassedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlitchMachineControl control)
        {
            bool isBypassed = (bool)e.NewValue;
            control.BypassToggle.IsChecked = isBypassed;
            if (control._viewModel != null)
            {
                control._viewModel.IsBypassed = isBypassed;
            }
            control.BypassChanged?.Invoke(control, isBypassed);
        }
    }

    private static void OnSyncToTempoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlitchMachineControl control && control._viewModel != null)
        {
            control._viewModel.SyncToTempo = (bool)e.NewValue;
            control.SyncTempoToggle.IsChecked = (bool)e.NewValue;
        }
    }

    #endregion

    #region UI Event Handlers

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        IsBypassed = BypassToggle.IsChecked == true;
    }

    private void EffectModule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggle && toggle.Tag is string tagStr)
        {
            if (Enum.TryParse<GlitchEffectType>(tagStr, out var effectType))
            {
                bool isEnabled = toggle.IsChecked == true;
                _viewModel?.SetEffectEnabled(effectType, isEnabled);
                EffectModuleToggled?.Invoke(this, effectType);
            }
        }
    }

    private void ChaosSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        float value = (float)(e.NewValue / 100.0);
        ChaosAmount = value;
        ChaosValueText.Text = $"{e.NewValue:F0}%";

        if (_viewModel != null)
        {
            _viewModel.ChaosAmount = value;
        }

        ParameterChanged?.Invoke(this, new GlitchParameterChangedEventArgs("ChaosAmount", value));
    }

    private void RateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        float value = (float)e.NewValue;
        TriggerRate = value;
        RateValueText.Text = $"{value:F0}x";

        if (_viewModel != null)
        {
            _viewModel.TriggerRate = value;
        }

        ParameterChanged?.Invoke(this, new GlitchParameterChangedEventArgs("TriggerRate", value));
    }

    private void DurationMinSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        float value = (float)e.NewValue;
        DurationMinValueText.Text = $"{value:F0}ms";

        if (_viewModel != null)
        {
            _viewModel.DurationMin = value;
        }

        ParameterChanged?.Invoke(this, new GlitchParameterChangedEventArgs("DurationMin", value));
    }

    private void DurationMaxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        float value = (float)e.NewValue;
        DurationMaxValueText.Text = $"{value:F0}ms";

        if (_viewModel != null)
        {
            _viewModel.DurationMax = value;
        }

        ParameterChanged?.Invoke(this, new GlitchParameterChangedEventArgs("DurationMax", value));
    }

    private void SyncTempoToggle_Click(object sender, RoutedEventArgs e)
    {
        SyncToTempo = SyncTempoToggle.IsChecked == true;
    }

    private void TriggerGlitch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.TriggerManualGlitchCommand.Execute(null);
    }

    private void PatternStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggle && toggle.Tag is string tagStr && int.TryParse(tagStr, out int stepIndex))
        {
            bool isActive = toggle.IsChecked == true;

            // For now, just toggle the step. In a full implementation,
            // you would also allow selecting which effect to use
            _viewModel?.SetPatternStep(stepIndex, isActive, isActive ? GlitchEffectType.BufferRepeat : null);
        }
    }

    private void RandomizePattern_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.RandomizePatternCommand.Execute(null);
        SyncPatternStepsFromViewModel();
    }

    private void ClearPattern_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ClearPatternCommand.Execute(null);
        SyncPatternStepsFromViewModel();
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        if (PresetComboBox.SelectedItem is ComboBoxItem item && item.Content is string presetName)
        {
            if (Enum.TryParse<GlitchPreset>(presetName, out var preset))
            {
                _viewModel?.ApplyPresetCommand.Execute(preset);
                SyncFromViewModel();
            }
        }
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        float value = (float)(e.NewValue / 100.0);
        Mix = value;
        MixValueText.Text = $"{e.NewValue:F0}%";

        if (_viewModel != null)
        {
            _viewModel.Mix = value;
        }

        ParameterChanged?.Invoke(this, new GlitchParameterChangedEventArgs("Mix", value));
    }

    #endregion

    #region Display Updates

    private void WaveformTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isInitialized) return;

        UpdateWaveformDisplay();
        UpdateGlitchCountDisplay();
    }

    private void UpdateAllDisplays()
    {
        UpdateTempoDisplay();
        UpdateGlitchIndicator(false, GlitchEffectType.BufferRepeat);
        UpdateWaveformDisplay();
        SyncFromViewModel();
    }

    private void UpdateTempoDisplay()
    {
        TempoValueText.Text = $"{Tempo:F0} BPM";
    }

    private void UpdateGlitchIndicator(bool isActive, GlitchEffectType effectType)
    {
        if (isActive)
        {
            ActiveGlitchText.Text = GetEffectDisplayName(effectType);
            GlitchStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)); // Red for active
            GlitchOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            ActiveGlitchText.Text = "READY";
            GlitchStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)); // Green for ready
            GlitchOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateGlitchCountDisplay()
    {
        if (_viewModel != null)
        {
            GlitchCountText.Text = _viewModel.GlitchesTriggered.ToString();
        }
    }

    private void UpdatePatternStepIndicator(int stepIndex)
    {
        // Clear previous current indicator
        for (int i = 0; i < _patternSteps.Length; i++)
        {
            // The visual state is handled by the style's DataTrigger
        }

        _currentStepIndex = stepIndex;
    }

    private void UpdateWaveformDisplay()
    {
        if (_viewModel == null || WaveformCanvas.ActualWidth <= 0 || WaveformCanvas.ActualHeight <= 0)
            return;

        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;
        double centerY = height / 2;

        // Draw input waveform
        var inputData = _viewModel.InputWaveform;
        if (inputData != null && inputData.Length > 0)
        {
            var inputGeometry = CreateWaveformGeometry(inputData, width, height, centerY);
            InputWaveformPath.Data = inputGeometry;
        }

        // Draw output waveform
        var outputData = _viewModel.OutputWaveform;
        if (outputData != null && outputData.Length > 0)
        {
            var outputGeometry = CreateWaveformGeometry(outputData, width, height, centerY);
            OutputWaveformPath.Data = outputGeometry;
        }

        // Update glitch overlay size
        GlitchOverlay.Width = width;
        GlitchOverlay.Height = height;
    }

    private PathGeometry CreateWaveformGeometry(float[] samples, double width, double height, double centerY)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure();

        int sampleCount = samples.Length;
        double xStep = width / sampleCount;

        figure.StartPoint = new Point(0, centerY - samples[0] * centerY * 0.9);

        for (int i = 1; i < sampleCount; i++)
        {
            double x = i * xStep;
            double y = centerY - samples[i] * centerY * 0.9;
            y = Math.Clamp(y, 2, height - 2);

            figure.Segments.Add(new LineSegment(new Point(x, y), true));
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    private void SyncFromViewModel()
    {
        if (_viewModel == null) return;

        ChaosSlider.Value = _viewModel.ChaosAmount * 100;
        RateSlider.Value = _viewModel.TriggerRate;
        DurationMinSlider.Value = _viewModel.DurationMin;
        DurationMaxSlider.Value = _viewModel.DurationMax;
        MixSlider.Value = _viewModel.Mix * 100;
        SyncTempoToggle.IsChecked = _viewModel.SyncToTempo;
        BypassToggle.IsChecked = _viewModel.IsBypassed;

        ChaosValueText.Text = $"{_viewModel.ChaosAmount * 100:F0}%";
        RateValueText.Text = $"{_viewModel.TriggerRate:F0}x";
        DurationMinValueText.Text = $"{_viewModel.DurationMin:F0}ms";
        DurationMaxValueText.Text = $"{_viewModel.DurationMax:F0}ms";
        MixValueText.Text = $"{_viewModel.Mix * 100:F0}%";

        SyncEffectModulesFromViewModel();
        SyncPatternStepsFromViewModel();
    }

    private void SyncEffectModulesFromViewModel()
    {
        if (_viewModel == null) return;

        foreach (var module in _viewModel.EffectModules)
        {
            var toggle = GetToggleForEffectType(module.EffectType);
            if (toggle != null)
            {
                toggle.IsChecked = module.IsEnabled;
            }
        }
    }

    private void SyncPatternStepsFromViewModel()
    {
        if (_viewModel == null) return;

        for (int i = 0; i < _patternSteps.Length && i < _viewModel.PatternSteps.Count; i++)
        {
            _patternSteps[i].IsChecked = _viewModel.PatternSteps[i].IsActive;
        }
    }

    private ToggleButton? GetToggleForEffectType(GlitchEffectType effectType)
    {
        return effectType switch
        {
            GlitchEffectType.BufferRepeat => BufferRepeatToggle,
            GlitchEffectType.TapeStop => TapeStopToggle,
            GlitchEffectType.BitReduction => BitCrushToggle,
            GlitchEffectType.SampleRateReduction => SampleRateToggle,
            GlitchEffectType.Reverse => ReverseToggle,
            GlitchEffectType.Stretch => StretchToggle,
            GlitchEffectType.Gate => GateToggle,
            GlitchEffectType.FilterSweep => FilterSweepToggle,
            _ => null
        };
    }

    private static string GetEffectDisplayName(GlitchEffectType effectType)
    {
        return effectType switch
        {
            GlitchEffectType.BufferRepeat => "BUFFER",
            GlitchEffectType.TapeStop => "TAPE",
            GlitchEffectType.BitReduction => "BITCRUSH",
            GlitchEffectType.SampleRateReduction => "SAMPLERATE",
            GlitchEffectType.Reverse => "REVERSE",
            GlitchEffectType.Stretch => "STRETCH",
            GlitchEffectType.Gate => "GATE",
            GlitchEffectType.FilterSweep => "FILTER",
            _ => "UNKNOWN"
        };
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the tempo from external source.
    /// </summary>
    public void SetTempo(float tempo)
    {
        Tempo = tempo;
    }

    /// <summary>
    /// Triggers a manual glitch.
    /// </summary>
    public void TriggerGlitch()
    {
        _viewModel?.TriggerManualGlitchCommand.Execute(null);
    }

    /// <summary>
    /// Triggers a specific glitch effect.
    /// </summary>
    public void TriggerGlitch(GlitchEffectType effectType)
    {
        _viewModel?.TriggerSpecificGlitchCommand.Execute(effectType);
    }

    /// <summary>
    /// Stops all active glitches.
    /// </summary>
    public void StopAllGlitches()
    {
        _viewModel?.StopAllGlitchesCommand.Execute(null);
    }

    /// <summary>
    /// Enables or disables a specific effect module.
    /// </summary>
    public void SetEffectEnabled(GlitchEffectType effectType, bool enabled)
    {
        _viewModel?.SetEffectEnabled(effectType, enabled);

        var toggle = GetToggleForEffectType(effectType);
        if (toggle != null)
        {
            toggle.IsChecked = enabled;
        }
    }

    /// <summary>
    /// Applies a preset.
    /// </summary>
    public void ApplyPreset(GlitchPreset preset)
    {
        _viewModel?.ApplyPresetCommand.Execute(preset);
        SyncFromViewModel();

        // Update combo box
        for (int i = 0; i < PresetComboBox.Items.Count; i++)
        {
            if (PresetComboBox.Items[i] is ComboBoxItem item &&
                item.Content?.ToString() == preset.ToString())
            {
                PresetComboBox.SelectedIndex = i;
                break;
            }
        }
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    public void Reset()
    {
        _viewModel?.ResetAllCommand.Execute(null);
        SyncFromViewModel();
    }

    /// <summary>
    /// Updates input waveform data for visualization.
    /// </summary>
    public void UpdateInputWaveform(float[] samples)
    {
        _viewModel?.UpdateInputWaveform(samples);
    }

    /// <summary>
    /// Gets the current ViewModel.
    /// </summary>
    public GlitchMachineViewModel? GetViewModel() => _viewModel;

    #endregion
}
