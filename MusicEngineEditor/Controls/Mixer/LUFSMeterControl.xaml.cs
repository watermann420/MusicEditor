using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicEngineEditor.Controls.Mixer
{
    /// <summary>
    /// LUFS meter control displaying Integrated, Short-term, Momentary LUFS and True Peak values.
    /// </summary>
    public partial class LUFSMeterControl : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty IntegratedLUFSProperty =
            DependencyProperty.Register(nameof(IntegratedLUFS), typeof(double), typeof(LUFSMeterControl),
                new PropertyMetadata(-23.0, OnIntegratedChanged));

        public static readonly DependencyProperty ShortTermLUFSProperty =
            DependencyProperty.Register(nameof(ShortTermLUFS), typeof(double), typeof(LUFSMeterControl),
                new PropertyMetadata(-23.0, OnShortTermChanged));

        public static readonly DependencyProperty MomentaryLUFSProperty =
            DependencyProperty.Register(nameof(MomentaryLUFS), typeof(double), typeof(LUFSMeterControl),
                new PropertyMetadata(-23.0, OnMomentaryChanged));

        public static readonly DependencyProperty TruePeakProperty =
            DependencyProperty.Register(nameof(TruePeak), typeof(double), typeof(LUFSMeterControl),
                new PropertyMetadata(-10.0, OnTruePeakChanged));

        public static readonly DependencyProperty TruePeakWarningThresholdProperty =
            DependencyProperty.Register(nameof(TruePeakWarningThreshold), typeof(double), typeof(LUFSMeterControl),
                new PropertyMetadata(-1.0, OnTruePeakChanged));

        #endregion

        #region Properties

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
        /// Momentary LUFS value (400ms window).
        /// </summary>
        public double MomentaryLUFS
        {
            get => (double)GetValue(MomentaryLUFSProperty);
            set => SetValue(MomentaryLUFSProperty, value);
        }

        /// <summary>
        /// True Peak value in dBTP.
        /// </summary>
        public double TruePeak
        {
            get => (double)GetValue(TruePeakProperty);
            set => SetValue(TruePeakProperty, value);
        }

        /// <summary>
        /// Threshold for True Peak warning indicator.
        /// </summary>
        public double TruePeakWarningThreshold
        {
            get => (double)GetValue(TruePeakWarningThresholdProperty);
            set => SetValue(TruePeakWarningThresholdProperty, value);
        }

        #endregion

        #region Private Fields

        private static readonly SolidColorBrush NormalBrush = new(Color.FromRgb(224, 224, 224));
        private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(255, 200, 0));
        private static readonly SolidColorBrush DangerBrush = new(Color.FromRgb(255, 80, 80));
        private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(0, 217, 255));

        #endregion

        #region Static Constructor

        static LUFSMeterControl()
        {
            NormalBrush.Freeze();
            WarningBrush.Freeze();
            DangerBrush.Freeze();
            AccentBrush.Freeze();
        }

        #endregion

        #region Constructor

        public LUFSMeterControl()
        {
            InitializeComponent();
            UpdateAllDisplays();
        }

        #endregion

        #region Event Handlers

        private static void OnIntegratedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LUFSMeterControl meter)
            {
                meter.UpdateIntegratedDisplay();
            }
        }

        private static void OnShortTermChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LUFSMeterControl meter)
            {
                meter.UpdateShortTermDisplay();
            }
        }

        private static void OnMomentaryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LUFSMeterControl meter)
            {
                meter.UpdateMomentaryDisplay();
            }
        }

        private static void OnTruePeakChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LUFSMeterControl meter)
            {
                meter.UpdateTruePeakDisplay();
            }
        }

        #endregion

        #region Private Methods

        private void UpdateAllDisplays()
        {
            UpdateIntegratedDisplay();
            UpdateShortTermDisplay();
            UpdateMomentaryDisplay();
            UpdateTruePeakDisplay();
        }

        private void UpdateIntegratedDisplay()
        {
            IntegratedText.Text = FormatLUFS(IntegratedLUFS);
            IntegratedText.Foreground = GetLUFSBrush(IntegratedLUFS);
        }

        private void UpdateShortTermDisplay()
        {
            ShortTermText.Text = FormatLUFS(ShortTermLUFS);
            ShortTermText.Foreground = GetLUFSBrush(ShortTermLUFS);
        }

        private void UpdateMomentaryDisplay()
        {
            MomentaryText.Text = FormatLUFS(MomentaryLUFS);
            MomentaryText.Foreground = GetLUFSBrush(MomentaryLUFS);
        }

        private void UpdateTruePeakDisplay()
        {
            TruePeakText.Text = FormatdB(TruePeak);

            // Color based on level
            if (TruePeak > 0)
            {
                TruePeakText.Foreground = DangerBrush;
                TruePeakWarning.Visibility = Visibility.Visible;
            }
            else if (TruePeak > TruePeakWarningThreshold)
            {
                TruePeakText.Foreground = WarningBrush;
                TruePeakWarning.Visibility = Visibility.Collapsed;
            }
            else
            {
                TruePeakText.Foreground = AccentBrush;
                TruePeakWarning.Visibility = Visibility.Collapsed;
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
            return NormalBrush;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Resets all LUFS values to their defaults.
        /// </summary>
        public void Reset()
        {
            IntegratedLUFS = -23.0;
            ShortTermLUFS = -23.0;
            MomentaryLUFS = -23.0;
            TruePeak = -10.0;
        }

        /// <summary>
        /// Updates all values at once.
        /// </summary>
        public void UpdateValues(double integrated, double shortTerm, double momentary, double truePeak)
        {
            IntegratedLUFS = integrated;
            ShortTermLUFS = shortTerm;
            MomentaryLUFS = momentary;
            TruePeak = truePeak;
        }

        #endregion
    }
}
