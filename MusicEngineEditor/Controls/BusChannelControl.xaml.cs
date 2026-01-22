//MusicEngineEditor - Bus Channel Control
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

// Event is declared for future use / public API
#pragma warning disable CS0067

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and managing a bus/group channel in the mixer.
/// Features volume fader, pan control, mute/solo buttons, level metering, and effect indicators.
/// </summary>
public partial class BusChannelControl : UserControl
{
    #region Constants

    private const float UnityGainVolume = 0.8f;
    private const float CenterPan = 0f;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty BusNameProperty =
        DependencyProperty.Register(nameof(BusName), typeof(string), typeof(BusChannelControl),
            new PropertyMetadata("Bus"));

    public static readonly DependencyProperty BusColorProperty =
        DependencyProperty.Register(nameof(BusColor), typeof(Brush), typeof(BusChannelControl),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00))));

    public static readonly DependencyProperty VolumeProperty =
        DependencyProperty.Register(nameof(Volume), typeof(float), typeof(BusChannelControl),
            new FrameworkPropertyMetadata(UnityGainVolume,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnVolumeChanged));

    public static readonly DependencyProperty VolumeDbProperty =
        DependencyProperty.Register(nameof(VolumeDb), typeof(float), typeof(BusChannelControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty PanProperty =
        DependencyProperty.Register(nameof(Pan), typeof(float), typeof(BusChannelControl),
            new FrameworkPropertyMetadata(CenterPan, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsMutedProperty =
        DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(BusChannelControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsSoloedProperty =
        DependencyProperty.Register(nameof(IsSoloed), typeof(bool), typeof(BusChannelControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty MeterLeftProperty =
        DependencyProperty.Register(nameof(MeterLeft), typeof(float), typeof(BusChannelControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty MeterRightProperty =
        DependencyProperty.Register(nameof(MeterRight), typeof(float), typeof(BusChannelControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty HasEffectsProperty =
        DependencyProperty.Register(nameof(HasEffects), typeof(bool), typeof(BusChannelControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty EffectCountProperty =
        DependencyProperty.Register(nameof(EffectCount), typeof(int), typeof(BusChannelControl),
            new PropertyMetadata(0));

    /// <summary>
    /// Gets or sets the bus name.
    /// </summary>
    public string BusName
    {
        get => (string)GetValue(BusNameProperty);
        set => SetValue(BusNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the bus color.
    /// </summary>
    public Brush BusColor
    {
        get => (Brush)GetValue(BusColorProperty);
        set => SetValue(BusColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the volume level (0.0 to 1.25).
    /// </summary>
    public float Volume
    {
        get => (float)GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    /// <summary>
    /// Gets the volume in decibels.
    /// </summary>
    public float VolumeDb
    {
        get => (float)GetValue(VolumeDbProperty);
        private set => SetValue(VolumeDbProperty, value);
    }

    /// <summary>
    /// Gets or sets the pan position (-1.0 to 1.0).
    /// </summary>
    public float Pan
    {
        get => (float)GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the bus is muted.
    /// </summary>
    public bool IsMuted
    {
        get => (bool)GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the bus is soloed.
    /// </summary>
    public bool IsSoloed
    {
        get => (bool)GetValue(IsSoloedProperty);
        set => SetValue(IsSoloedProperty, value);
    }

    /// <summary>
    /// Gets or sets the left meter level.
    /// </summary>
    public float MeterLeft
    {
        get => (float)GetValue(MeterLeftProperty);
        set => SetValue(MeterLeftProperty, value);
    }

    /// <summary>
    /// Gets or sets the right meter level.
    /// </summary>
    public float MeterRight
    {
        get => (float)GetValue(MeterRightProperty);
        set => SetValue(MeterRightProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the bus has effects.
    /// </summary>
    public bool HasEffects
    {
        get => (bool)GetValue(HasEffectsProperty);
        set => SetValue(HasEffectsProperty, value);
    }

    /// <summary>
    /// Gets or sets the effect count.
    /// </summary>
    public int EffectCount
    {
        get => (int)GetValue(EffectCountProperty);
        set => SetValue(EffectCountProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the effects button is clicked.
    /// </summary>
    public event EventHandler? EffectsClicked;

    #endregion

    #region Constructor

    public BusChannelControl()
    {
        InitializeComponent();
        UpdateVolumeDb(UnityGainVolume);
    }

    #endregion

    #region Property Changed Callbacks

    private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BusChannelControl control)
        {
            control.UpdateVolumeDb((float)e.NewValue);
        }
    }

    #endregion

    #region Event Handlers

    private void VolumeFader_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Volume = UnityGainVolume;
        e.Handled = true;
    }

    private void PanSlider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Pan = CenterPan;
        e.Handled = true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets both meter levels at once.
    /// </summary>
    /// <param name="left">Left channel level.</param>
    /// <param name="right">Right channel level.</param>
    public void SetMeterLevels(float left, float right)
    {
        MeterLeft = left;
        MeterRight = right;
    }

    /// <summary>
    /// Resets the bus to default values.
    /// </summary>
    public void Reset()
    {
        Volume = UnityGainVolume;
        Pan = CenterPan;
        IsMuted = false;
        IsSoloed = false;
        MeterLeft = 0f;
        MeterRight = 0f;
    }

    /// <summary>
    /// Resets the clip indicators on the meter.
    /// </summary>
    public void ResetClipIndicators()
    {
        BusMeter?.ResetClipIndicators();
    }

    #endregion

    #region Private Methods

    private void UpdateVolumeDb(float volume)
    {
        if (volume <= 0)
        {
            VolumeDb = -60f;
            return;
        }

        double db = 20.0 * Math.Log10(volume / UnityGainVolume);
        VolumeDb = (float)Math.Max(-60.0, db);
    }

    #endregion
}

#region Converters

/// <summary>
/// Converts a count to visibility (visible if count > 0).
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public static CountToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts BusChannelType to a color.
/// </summary>
public class BusTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BusChannelType busType)
        {
            return busType switch
            {
                BusChannelType.Group => new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00)),
                BusChannelType.Aux => new SolidColorBrush(Color.FromRgb(0x55, 0xAA, 0xFF)),
                BusChannelType.Master => new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55)),
                _ => new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
