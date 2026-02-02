using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicEngineEditor.Controls.Mixer
{
    /// <summary>
    /// Master channel strip control with fader, stereo VU meter, LUFS display,
    /// true peak indicator, solo/mute buttons, and effects chain access.
    /// </summary>
    public partial class MasterChannelStrip : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register(nameof(Volume), typeof(double), typeof(MasterChannelStrip),
                new PropertyMetadata(0.0, OnVolumeChanged, CoerceVolume));

        public static readonly DependencyProperty IsSoloProperty =
            DependencyProperty.Register(nameof(IsSolo), typeof(bool), typeof(MasterChannelStrip),
                new PropertyMetadata(false, OnIsSoloChanged));

        public static readonly DependencyProperty IsMutedProperty =
            DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(MasterChannelStrip),
                new PropertyMetadata(false, OnIsMutedChanged));

        public static readonly DependencyProperty LeftLevelProperty =
            DependencyProperty.Register(nameof(LeftLevel), typeof(double), typeof(MasterChannelStrip),
                new PropertyMetadata(0.0, OnLeftLevelChanged));

        public static readonly DependencyProperty RightLevelProperty =
            DependencyProperty.Register(nameof(RightLevel), typeof(double), typeof(MasterChannelStrip),
                new PropertyMetadata(0.0, OnRightLevelChanged));

        public static readonly DependencyProperty IntegratedLUFSProperty =
            DependencyProperty.Register(nameof(IntegratedLUFS), typeof(double), typeof(MasterChannelStrip),
                new PropertyMetadata(-23.0, OnIntegratedLUFSChanged));

        public static readonly DependencyProperty ShortTermLUFSProperty =
            DependencyProperty.Register(nameof(ShortTermLUFS), typeof(double), typeof(MasterChannelStrip),
                new PropertyMetadata(-23.0, OnShortTermLUFSChanged));

        public static readonly DependencyProperty TruePeakProperty =
            DependencyProperty.Register(nameof(TruePeak), typeof(double), typeof(MasterChannelStrip),
                new PropertyMetadata(-10.0, OnTruePeakChanged));

        #endregion

        #region Properties

        /// <summary>
        /// Master volume in dB (-60 to +6).
        /// </summary>
        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        /// <summary>
        /// Whether the master is soloed.
        /// </summary>
        public bool IsSolo
        {
            get => (bool)GetValue(IsSoloProperty);
            set => SetValue(IsSoloProperty, value);
        }

        /// <summary>
        /// Whether the master is muted.
        /// </summary>
        public bool IsMuted
        {
            get => (bool)GetValue(IsMutedProperty);
            set => SetValue(IsMutedProperty, value);
        }

        /// <summary>
        /// Left channel level (0-1) for VU meter display.
        /// </summary>
        public double LeftLevel
        {
            get => (double)GetValue(LeftLevelProperty);
            set => SetValue(LeftLevelProperty, value);
        }

        /// <summary>
        /// Right channel level (0-1) for VU meter display.
        /// </summary>
        public double RightLevel
        {
            get => (double)GetValue(RightLevelProperty);
            set => SetValue(RightLevelProperty, value);
        }

        /// <summary>
        /// Integrated LUFS value (long-term average loudness).
        /// </summary>
        public double IntegratedLUFS
        {
            get => (double)GetValue(IntegratedLUFSProperty);
            set => SetValue(IntegratedLUFSProperty, value);
        }

        /// <summary>
        /// Short-term LUFS value (3-second window).
        /// </summary>
        public double ShortTermLUFS
        {
            get => (double)GetValue(ShortTermLUFSProperty);
            set => SetValue(ShortTermLUFSProperty, value);
        }

        /// <summary>
        /// True Peak value in dBTP.
        /// </summary>
        public double TruePeak
        {
            get => (double)GetValue(TruePeakProperty);
            set => SetValue(TruePeakProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<double>? VolumeChanged;
        public event EventHandler<bool>? SoloChanged;
        public event EventHandler<bool>? MuteChanged;
        public event EventHandler? EffectsChainRequested;

        #endregion

        #region Private Fields

        private static readonly SolidColorBrush NormalBrush = new(Color.FromRgb(0, 217, 255));      // Accent
        private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(255, 200, 0));     // Yellow
        private static readonly SolidColorBrush DangerBrush = new(Color.FromRgb(255, 64, 64));      // Red
        private static readonly SolidColorBrush NormalTextBrush = new(Color.FromRgb(224, 224, 224));
        private static readonly SolidColorBrush DangerBorderBrush = new(Color.FromRgb(255, 0, 0));
        private static readonly SolidColorBrush NormalBorderBrush = new(Color.FromRgb(42, 42, 42));

        #endregion

        #region Static Constructor

        static MasterChannelStrip()
        {
            NormalBrush.Freeze();
            WarningBrush.Freeze();
            DangerBrush.Freeze();
            NormalTextBrush.Freeze();
            DangerBorderBrush.Freeze();
            NormalBorderBrush.Freeze();
        }

        #endregion

        #region Constructor

        public MasterChannelStrip()
        {
            InitializeComponent();
            UpdateVolumeDisplay();
            UpdateLUFSDisplays();
            UpdateTruePeakDisplay();
        }

        #endregion

        #region Property Changed Callbacks

        private static object CoerceVolume(DependencyObject d, object baseValue)
        {
            var value = (double)baseValue;
            return Math.Max(-60.0, Math.Min(6.0, value));
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MasterChannelStrip control)
            {
                control.VolumeFader.Value = (double)e.NewValue;
                control.UpdateVolumeDisplay();
            }
        }

        private static void OnIsSoloChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MasterChannelStrip control)
            {
                control.SoloButton.IsChecked = (bool)e.NewValue;
            }
        }

        private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MasterChannelStrip control)
            {
                control.MuteButton.IsChecked = (bool)e.NewValue;
            }
        }

        private static void OnLeftLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MasterChannelStrip control)
            {
                control.StereoVUMeter.LeftLevel = (double)e.NewValue;
            }
        }

        private static void OnRightLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MasterChannelStrip control)
            {
                control.StereoVUMeter.RightLevel = (double)e.NewValue;
            }
        }

        private static void OnIntegratedLUFSChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MasterChannelStrip control)
            {
                control.UpdateLUFSDisplays();
            }
        }

        private static void OnShortTermLUFSChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MasterChannelStrip control)
            {
                control.UpdateLUFSDisplays();
            }
        }

        private static void OnTruePeakChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MasterChannelStrip control)
            {
                control.UpdateTruePeakDisplay();
            }
        }

        #endregion

        #region Event Handlers

        private void VolumeFader_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(Volume - e.NewValue) > 0.001)
            {
                Volume = e.NewValue;
                UpdateVolumeDisplay();
                VolumeChanged?.Invoke(this, e.NewValue);
            }
        }

        private void SoloButton_Changed(object sender, RoutedEventArgs e)
        {
            var isChecked = SoloButton.IsChecked ?? false;
            if (IsSolo != isChecked)
            {
                IsSolo = isChecked;
                SoloChanged?.Invoke(this, isChecked);
            }
        }

        private void MuteButton_Changed(object sender, RoutedEventArgs e)
        {
            var isChecked = MuteButton.IsChecked ?? false;
            if (IsMuted != isChecked)
            {
                IsMuted = isChecked;
                MuteChanged?.Invoke(this, isChecked);
            }
        }

        private void EffectsChainButton_Click(object sender, RoutedEventArgs e)
        {
            EffectsChainRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Private Methods

        private void UpdateVolumeDisplay()
        {
            if (Volume <= -60)
            {
                VolumeDisplay.Text = "-inf dB";
            }
            else
            {
                VolumeDisplay.Text = $"{Volume:F1} dB";
            }
        }

        private void UpdateLUFSDisplays()
        {
            // Update Integrated LUFS
            IntegratedLUFSText.Text = FormatLUFS(IntegratedLUFS);
            IntegratedLUFSText.Foreground = GetLUFSBrush(IntegratedLUFS);

            // Update Short-term LUFS
            ShortTermLUFSText.Text = FormatLUFS(ShortTermLUFS);
            ShortTermLUFSText.Foreground = GetLUFSBrush(ShortTermLUFS);
        }

        private void UpdateTruePeakDisplay()
        {
            TruePeakText.Text = FormatdB(TruePeak);

            // Color based on level - warning when exceeding 0dB
            if (TruePeak > 0)
            {
                TruePeakText.Foreground = DangerBrush;
                TruePeakBorder.BorderBrush = DangerBorderBrush;
                TruePeakBorder.Background = new SolidColorBrush(Color.FromArgb(40, 255, 64, 64));
            }
            else if (TruePeak > -1.0)
            {
                TruePeakText.Foreground = WarningBrush;
                TruePeakBorder.BorderBrush = NormalBorderBrush;
                TruePeakBorder.Background = new SolidColorBrush(Color.FromRgb(13, 13, 13));
            }
            else
            {
                TruePeakText.Foreground = NormalBrush;
                TruePeakBorder.BorderBrush = NormalBorderBrush;
                TruePeakBorder.Background = new SolidColorBrush(Color.FromRgb(13, 13, 13));
            }
        }

        private static string FormatLUFS(double value)
        {
            if (double.IsNegativeInfinity(value) || value < -70)
            {
                return "-inf";
            }
            return value.ToString("F1");
        }

        private static string FormatdB(double value)
        {
            if (double.IsNegativeInfinity(value) || value < -70)
            {
                return "-inf";
            }
            return value.ToString("F1");
        }

        private static SolidColorBrush GetLUFSBrush(double value)
        {
            if (value > -9)
            {
                return DangerBrush;
            }
            if (value > -14)
            {
                return WarningBrush;
            }
            return NormalTextBrush;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Resets the peak hold indicators on the VU meter.
        /// </summary>
        public void ResetPeaks()
        {
            StereoVUMeter.ResetPeaks();
        }

        /// <summary>
        /// Sets both left and right VU meter levels at once.
        /// </summary>
        public void SetLevels(double left, double right)
        {
            LeftLevel = left;
            RightLevel = right;
        }

        /// <summary>
        /// Updates all LUFS and true peak values at once.
        /// </summary>
        public void UpdateMetering(double integratedLUFS, double shortTermLUFS, double truePeak)
        {
            IntegratedLUFS = integratedLUFS;
            ShortTermLUFS = shortTermLUFS;
            TruePeak = truePeak;
        }

        /// <summary>
        /// Updates all metering values including VU levels.
        /// </summary>
        public void UpdateAllMetering(double leftLevel, double rightLevel,
            double integratedLUFS, double shortTermLUFS, double truePeak)
        {
            SetLevels(leftLevel, rightLevel);
            UpdateMetering(integratedLUFS, shortTermLUFS, truePeak);
        }

        /// <summary>
        /// Resets all metering to default values.
        /// </summary>
        public void ResetMetering()
        {
            LeftLevel = 0;
            RightLevel = 0;
            IntegratedLUFS = -23.0;
            ShortTermLUFS = -23.0;
            TruePeak = -10.0;
            ResetPeaks();
        }

        #endregion
    }
}
