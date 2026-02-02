using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Mixer
{
    /// <summary>
    /// A return track (aux bus) control with fader, meter, pan, mute, and effects chain.
    /// </summary>
    public partial class ReturnTrackControl : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty ReturnLabelProperty =
            DependencyProperty.Register(nameof(ReturnLabel), typeof(string), typeof(ReturnTrackControl),
                new PropertyMetadata("A", OnReturnLabelChanged));

        public static readonly DependencyProperty ReturnIndexProperty =
            DependencyProperty.Register(nameof(ReturnIndex), typeof(int), typeof(ReturnTrackControl),
                new PropertyMetadata(0));

        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register(nameof(Volume), typeof(double), typeof(ReturnTrackControl),
                new PropertyMetadata(0.0, OnVolumeChanged, CoerceVolume));

        public static readonly DependencyProperty PanProperty =
            DependencyProperty.Register(nameof(Pan), typeof(double), typeof(ReturnTrackControl),
                new PropertyMetadata(0.0, OnPanChanged, CoercePan));

        public static readonly DependencyProperty IsMutedProperty =
            DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(ReturnTrackControl),
                new PropertyMetadata(false, OnIsMutedChanged));

        public static readonly DependencyProperty LeftLevelProperty =
            DependencyProperty.Register(nameof(LeftLevel), typeof(double), typeof(ReturnTrackControl),
                new PropertyMetadata(0.0, OnLeftLevelChanged));

        public static readonly DependencyProperty RightLevelProperty =
            DependencyProperty.Register(nameof(RightLevel), typeof(double), typeof(ReturnTrackControl),
                new PropertyMetadata(0.0, OnRightLevelChanged));

        #endregion

        #region Properties

        /// <summary>
        /// The label for this return track (A, B, C, D, etc.).
        /// </summary>
        public string ReturnLabel
        {
            get => (string)GetValue(ReturnLabelProperty);
            set => SetValue(ReturnLabelProperty, value);
        }

        /// <summary>
        /// The index of this return track.
        /// </summary>
        public int ReturnIndex
        {
            get => (int)GetValue(ReturnIndexProperty);
            set => SetValue(ReturnIndexProperty, value);
        }

        /// <summary>
        /// Volume in dB (-60 to +6).
        /// </summary>
        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        /// <summary>
        /// Pan position (-100 to +100).
        /// </summary>
        public double Pan
        {
            get => (double)GetValue(PanProperty);
            set => SetValue(PanProperty, value);
        }

        /// <summary>
        /// Whether this return track is muted.
        /// </summary>
        public bool IsMuted
        {
            get => (bool)GetValue(IsMutedProperty);
            set => SetValue(IsMutedProperty, value);
        }

        /// <summary>
        /// Left channel level (0-1) for meter display.
        /// </summary>
        public double LeftLevel
        {
            get => (double)GetValue(LeftLevelProperty);
            set => SetValue(LeftLevelProperty, value);
        }

        /// <summary>
        /// Right channel level (0-1) for meter display.
        /// </summary>
        public double RightLevel
        {
            get => (double)GetValue(RightLevelProperty);
            set => SetValue(RightLevelProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<double>? VolumeChanged;
        public event EventHandler<double>? PanChanged;
        public event EventHandler<bool>? MuteChanged;
        public event EventHandler<int>? EffectsChainRequested;

        #endregion

        #region Constructor

        public ReturnTrackControl()
        {
            InitializeComponent();
            UpdateVolumeDisplay();
            UpdatePanDisplay();
        }

        #endregion

        #region Property Changed Callbacks

        private static object CoerceVolume(DependencyObject d, object baseValue)
        {
            var value = (double)baseValue;
            return Math.Max(-60.0, Math.Min(6.0, value));
        }

        private static object CoercePan(DependencyObject d, object baseValue)
        {
            var value = (double)baseValue;
            return Math.Max(-100.0, Math.Min(100.0, value));
        }

        private static void OnReturnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReturnTrackControl control)
            {
                control.ReturnLabelText.Text = (string)e.NewValue;
            }
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReturnTrackControl control)
            {
                control.VolumeFader.Value = (double)e.NewValue;
                control.UpdateVolumeDisplay();
            }
        }

        private static void OnPanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReturnTrackControl control)
            {
                control.PanSlider.Value = (double)e.NewValue;
                control.UpdatePanDisplay();
            }
        }

        private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReturnTrackControl control)
            {
                control.MuteButton.IsChecked = (bool)e.NewValue;
            }
        }

        private static void OnLeftLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReturnTrackControl control)
            {
                control.LevelMeter.LeftLevel = (double)e.NewValue;
            }
        }

        private static void OnRightLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReturnTrackControl control)
            {
                control.LevelMeter.RightLevel = (double)e.NewValue;
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

        private void PanSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(Pan - e.NewValue) > 0.001)
            {
                Pan = e.NewValue;
                UpdatePanDisplay();
                PanChanged?.Invoke(this, e.NewValue);
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

        private void FXButton_Click(object sender, RoutedEventArgs e)
        {
            EffectsChainRequested?.Invoke(this, ReturnIndex);
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

        private void UpdatePanDisplay()
        {
            var pan = (int)Math.Round(Pan);
            if (pan == 0)
            {
                PanValueText.Text = "C";
            }
            else if (pan < 0)
            {
                PanValueText.Text = $"L{Math.Abs(pan)}";
            }
            else
            {
                PanValueText.Text = $"R{pan}";
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Resets the peak hold indicators on the meter.
        /// </summary>
        public void ResetPeaks()
        {
            LevelMeter.ResetPeaks();
        }

        /// <summary>
        /// Sets both left and right levels at once.
        /// </summary>
        public void SetLevels(double left, double right)
        {
            LeftLevel = left;
            RightLevel = right;
        }

        /// <summary>
        /// Gets the return label for a given index (0=A, 1=B, etc.).
        /// </summary>
        public static string GetLabelForIndex(int index)
        {
            if (index < 26)
            {
                return ((char)('A' + index)).ToString();
            }
            // For indices beyond Z, use AA, AB, etc.
            return $"{(char)('A' + (index / 26) - 1)}{(char)('A' + (index % 26))}";
        }

        #endregion
    }
}
