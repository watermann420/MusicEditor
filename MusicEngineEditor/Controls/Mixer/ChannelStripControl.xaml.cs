using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusicEngineEditor.Controls.Mixer
{
    /// <summary>
    /// A complete channel strip control with fader, meter, pan, solo/mute, and sends.
    /// </summary>
    public partial class ChannelStripControl : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty TrackNameProperty =
            DependencyProperty.Register(nameof(TrackName), typeof(string), typeof(ChannelStripControl),
                new PropertyMetadata("Track", OnTrackNameChanged));

        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register(nameof(Volume), typeof(double), typeof(ChannelStripControl),
                new PropertyMetadata(0.0, OnVolumeChanged, CoerceVolume));

        public static readonly DependencyProperty PanProperty =
            DependencyProperty.Register(nameof(Pan), typeof(double), typeof(ChannelStripControl),
                new PropertyMetadata(0.0, OnPanChanged, CoercePan));

        public static readonly DependencyProperty IsSoloProperty =
            DependencyProperty.Register(nameof(IsSolo), typeof(bool), typeof(ChannelStripControl),
                new PropertyMetadata(false, OnIsSoloChanged));

        public static readonly DependencyProperty IsMutedProperty =
            DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(ChannelStripControl),
                new PropertyMetadata(false, OnIsMutedChanged));

        public static readonly DependencyProperty LeftLevelProperty =
            DependencyProperty.Register(nameof(LeftLevel), typeof(double), typeof(ChannelStripControl),
                new PropertyMetadata(0.0, OnLeftLevelChanged));

        public static readonly DependencyProperty RightLevelProperty =
            DependencyProperty.Register(nameof(RightLevel), typeof(double), typeof(ChannelStripControl),
                new PropertyMetadata(0.0, OnRightLevelChanged));

        public static readonly DependencyProperty SendAProperty =
            DependencyProperty.Register(nameof(SendA), typeof(double), typeof(ChannelStripControl),
                new PropertyMetadata(0.0, OnSendAChanged));

        public static readonly DependencyProperty SendBProperty =
            DependencyProperty.Register(nameof(SendB), typeof(double), typeof(ChannelStripControl),
                new PropertyMetadata(0.0, OnSendBChanged));

        public static readonly DependencyProperty SendCProperty =
            DependencyProperty.Register(nameof(SendC), typeof(double), typeof(ChannelStripControl),
                new PropertyMetadata(0.0, OnSendCChanged));

        public static readonly DependencyProperty SendDProperty =
            DependencyProperty.Register(nameof(SendD), typeof(double), typeof(ChannelStripControl),
                new PropertyMetadata(0.0, OnSendDChanged));

        public static readonly DependencyProperty ChannelIndexProperty =
            DependencyProperty.Register(nameof(ChannelIndex), typeof(int), typeof(ChannelStripControl),
                new PropertyMetadata(-1));

        #endregion

        #region Properties

        /// <summary>
        /// The name of the track.
        /// </summary>
        public string TrackName
        {
            get => (string)GetValue(TrackNameProperty);
            set => SetValue(TrackNameProperty, value);
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
        /// Whether this channel is soloed.
        /// </summary>
        public bool IsSolo
        {
            get => (bool)GetValue(IsSoloProperty);
            set => SetValue(IsSoloProperty, value);
        }

        /// <summary>
        /// Whether this channel is muted.
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

        /// <summary>
        /// Send A level (0-100).
        /// </summary>
        public double SendA
        {
            get => (double)GetValue(SendAProperty);
            set => SetValue(SendAProperty, value);
        }

        /// <summary>
        /// Send B level (0-100).
        /// </summary>
        public double SendB
        {
            get => (double)GetValue(SendBProperty);
            set => SetValue(SendBProperty, value);
        }

        /// <summary>
        /// Send C level (0-100).
        /// </summary>
        public double SendC
        {
            get => (double)GetValue(SendCProperty);
            set => SetValue(SendCProperty, value);
        }

        /// <summary>
        /// Send D level (0-100).
        /// </summary>
        public double SendD
        {
            get => (double)GetValue(SendDProperty);
            set => SetValue(SendDProperty, value);
        }

        /// <summary>
        /// The index of this channel in the mixer.
        /// </summary>
        public int ChannelIndex
        {
            get => (int)GetValue(ChannelIndexProperty);
            set => SetValue(ChannelIndexProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<double>? VolumeChanged;
        public event EventHandler<double>? PanChanged;
        public event EventHandler<bool>? SoloChanged;
        public event EventHandler<bool>? MuteChanged;
        public event EventHandler<string>? TrackNameEdited;
        public event EventHandler<(int SendIndex, double Value)>? SendChanged;

        #endregion

        #region Constructor

        public ChannelStripControl()
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

        private static void OnTrackNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.TrackNameText.Text = (string)e.NewValue;
            }
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.VolumeFader.Value = (double)e.NewValue;
                control.UpdateVolumeDisplay();
            }
        }

        private static void OnPanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.PanSlider.Value = (double)e.NewValue;
                control.UpdatePanDisplay();
            }
        }

        private static void OnIsSoloChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.SoloButton.IsChecked = (bool)e.NewValue;
            }
        }

        private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.MuteButton.IsChecked = (bool)e.NewValue;
            }
        }

        private static void OnLeftLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.LevelMeter.LeftLevel = (double)e.NewValue;
            }
        }

        private static void OnRightLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.LevelMeter.RightLevel = (double)e.NewValue;
            }
        }

        private static void OnSendAChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.SendASlider.Value = (double)e.NewValue;
            }
        }

        private static void OnSendBChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.SendBSlider.Value = (double)e.NewValue;
            }
        }

        private static void OnSendCChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.SendCSlider.Value = (double)e.NewValue;
            }
        }

        private static void OnSendDChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChannelStripControl control)
            {
                control.SendDSlider.Value = (double)e.NewValue;
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

        private void TrackNameText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                StartEditing();
            }
        }

        private void TrackNameEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            FinishEditing();
        }

        private void TrackNameEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FinishEditing();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEditing();
                e.Handled = true;
            }
        }

        private void SendASlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(SendA - e.NewValue) > 0.001)
            {
                SendA = e.NewValue;
                SendChanged?.Invoke(this, (0, e.NewValue));
            }
        }

        private void SendBSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(SendB - e.NewValue) > 0.001)
            {
                SendB = e.NewValue;
                SendChanged?.Invoke(this, (1, e.NewValue));
            }
        }

        private void SendCSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(SendC - e.NewValue) > 0.001)
            {
                SendC = e.NewValue;
                SendChanged?.Invoke(this, (2, e.NewValue));
            }
        }

        private void SendDSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(SendD - e.NewValue) > 0.001)
            {
                SendD = e.NewValue;
                SendChanged?.Invoke(this, (3, e.NewValue));
            }
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

        private void StartEditing()
        {
            TrackNameEdit.Text = TrackName;
            TrackNameText.Visibility = Visibility.Collapsed;
            TrackNameEdit.Visibility = Visibility.Visible;
            TrackNameEdit.Focus();
            TrackNameEdit.SelectAll();
        }

        private void FinishEditing()
        {
            var newName = TrackNameEdit.Text?.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != TrackName)
            {
                TrackName = newName;
                TrackNameEdited?.Invoke(this, newName);
            }
            TrackNameText.Visibility = Visibility.Visible;
            TrackNameEdit.Visibility = Visibility.Collapsed;
        }

        private void CancelEditing()
        {
            TrackNameText.Visibility = Visibility.Visible;
            TrackNameEdit.Visibility = Visibility.Collapsed;
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

        #endregion
    }
}
