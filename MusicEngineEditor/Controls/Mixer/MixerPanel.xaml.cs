using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Mixer
{
    /// <summary>
    /// Mixer panel with scrollable channel strips and a fixed master channel.
    /// </summary>
    public partial class MixerPanel : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty MasterVolumeProperty =
            DependencyProperty.Register(nameof(MasterVolume), typeof(double), typeof(MixerPanel),
                new PropertyMetadata(0.0, OnMasterVolumeChanged, CoerceVolume));

        public static readonly DependencyProperty MasterPanProperty =
            DependencyProperty.Register(nameof(MasterPan), typeof(double), typeof(MixerPanel),
                new PropertyMetadata(0.0, OnMasterPanChanged, CoercePan));

        public static readonly DependencyProperty IsMasterMutedProperty =
            DependencyProperty.Register(nameof(IsMasterMuted), typeof(bool), typeof(MixerPanel),
                new PropertyMetadata(false, OnIsMasterMutedChanged));

        #endregion

        #region Properties

        /// <summary>
        /// The collection of channel strips in the mixer.
        /// </summary>
        public ObservableCollection<ChannelStripControl> Channels { get; } = new();

        /// <summary>
        /// Master volume in dB (-60 to +6).
        /// </summary>
        public double MasterVolume
        {
            get => (double)GetValue(MasterVolumeProperty);
            set => SetValue(MasterVolumeProperty, value);
        }

        /// <summary>
        /// Master pan position (-100 to +100).
        /// </summary>
        public double MasterPan
        {
            get => (double)GetValue(MasterPanProperty);
            set => SetValue(MasterPanProperty, value);
        }

        /// <summary>
        /// Whether the master channel is muted.
        /// </summary>
        public bool IsMasterMuted
        {
            get => (bool)GetValue(IsMasterMutedProperty);
            set => SetValue(IsMasterMutedProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<double>? MasterVolumeChanged;
        public event EventHandler<double>? MasterPanChanged;
        public event EventHandler<bool>? MasterMuteChanged;
        public event EventHandler<(int ChannelIndex, double Volume)>? ChannelVolumeChanged;
        public event EventHandler<(int ChannelIndex, double Pan)>? ChannelPanChanged;
        public event EventHandler<(int ChannelIndex, bool IsSolo)>? ChannelSoloChanged;
        public event EventHandler<(int ChannelIndex, bool IsMuted)>? ChannelMuteChanged;
        public event EventHandler<(int ChannelIndex, string Name)>? ChannelNameChanged;

        #endregion

        #region Constructor

        public MixerPanel()
        {
            InitializeComponent();
            Channels.CollectionChanged += Channels_CollectionChanged;
            UpdateMasterVolumeDisplay();
            UpdateMasterPanDisplay();
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

        private static void OnMasterVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MixerPanel panel)
            {
                panel.MasterVolumeFader.Value = (double)e.NewValue;
                panel.UpdateMasterVolumeDisplay();
            }
        }

        private static void OnMasterPanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MixerPanel panel)
            {
                panel.MasterPanSlider.Value = (double)e.NewValue;
                panel.UpdateMasterPanDisplay();
            }
        }

        private static void OnIsMasterMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MixerPanel panel)
            {
                panel.MasterMuteButton.IsChecked = (bool)e.NewValue;
            }
        }

        #endregion

        #region Event Handlers

        private void Channels_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        foreach (ChannelStripControl channel in e.NewItems)
                        {
                            AddChannelToContainer(channel);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (ChannelStripControl channel in e.OldItems)
                        {
                            RemoveChannelFromContainer(channel);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    ChannelStripContainer.Children.Clear();
                    break;
            }

            UpdateChannelIndices();
        }

        private void MasterVolumeFader_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(MasterVolume - e.NewValue) > 0.001)
            {
                MasterVolume = e.NewValue;
                UpdateMasterVolumeDisplay();
                MasterVolumeChanged?.Invoke(this, e.NewValue);
            }
        }

        private void MasterPanSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(MasterPan - e.NewValue) > 0.001)
            {
                MasterPan = e.NewValue;
                UpdateMasterPanDisplay();
                MasterPanChanged?.Invoke(this, e.NewValue);
            }
        }

        private void MasterMuteButton_Changed(object sender, RoutedEventArgs e)
        {
            var isChecked = MasterMuteButton.IsChecked ?? false;
            if (IsMasterMuted != isChecked)
            {
                IsMasterMuted = isChecked;
                MasterMuteChanged?.Invoke(this, isChecked);
            }
        }

        private void Channel_VolumeChanged(object? sender, double volume)
        {
            if (sender is ChannelStripControl channel)
            {
                ChannelVolumeChanged?.Invoke(this, (channel.ChannelIndex, volume));
            }
        }

        private void Channel_PanChanged(object? sender, double pan)
        {
            if (sender is ChannelStripControl channel)
            {
                ChannelPanChanged?.Invoke(this, (channel.ChannelIndex, pan));
            }
        }

        private void Channel_SoloChanged(object? sender, bool isSolo)
        {
            if (sender is ChannelStripControl channel)
            {
                ChannelSoloChanged?.Invoke(this, (channel.ChannelIndex, isSolo));
            }
        }

        private void Channel_MuteChanged(object? sender, bool isMuted)
        {
            if (sender is ChannelStripControl channel)
            {
                ChannelMuteChanged?.Invoke(this, (channel.ChannelIndex, isMuted));
            }
        }

        private void Channel_NameChanged(object? sender, string name)
        {
            if (sender is ChannelStripControl channel)
            {
                ChannelNameChanged?.Invoke(this, (channel.ChannelIndex, name));
            }
        }

        #endregion

        #region Private Methods

        private void AddChannelToContainer(ChannelStripControl channel)
        {
            channel.VolumeChanged += Channel_VolumeChanged;
            channel.PanChanged += Channel_PanChanged;
            channel.SoloChanged += Channel_SoloChanged;
            channel.MuteChanged += Channel_MuteChanged;
            channel.TrackNameEdited += Channel_NameChanged;
            channel.Margin = new Thickness(2);
            ChannelStripContainer.Children.Add(channel);
        }

        private void RemoveChannelFromContainer(ChannelStripControl channel)
        {
            channel.VolumeChanged -= Channel_VolumeChanged;
            channel.PanChanged -= Channel_PanChanged;
            channel.SoloChanged -= Channel_SoloChanged;
            channel.MuteChanged -= Channel_MuteChanged;
            channel.TrackNameEdited -= Channel_NameChanged;
            ChannelStripContainer.Children.Remove(channel);
        }

        private void UpdateChannelIndices()
        {
            for (int i = 0; i < Channels.Count; i++)
            {
                Channels[i].ChannelIndex = i;
            }
        }

        private void UpdateMasterVolumeDisplay()
        {
            if (MasterVolume <= -60)
            {
                MasterVolumeDisplay.Text = "-inf dB";
            }
            else
            {
                MasterVolumeDisplay.Text = $"{MasterVolume:F1} dB";
            }
        }

        private void UpdateMasterPanDisplay()
        {
            var pan = (int)Math.Round(MasterPan);
            if (pan == 0)
            {
                MasterPanValueText.Text = "C";
            }
            else if (pan < 0)
            {
                MasterPanValueText.Text = $"L{Math.Abs(pan)}";
            }
            else
            {
                MasterPanValueText.Text = $"R{pan}";
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers a new channel in the mixer.
        /// </summary>
        /// <param name="trackName">The name of the track.</param>
        /// <returns>The created channel strip control.</returns>
        public ChannelStripControl RegisterChannel(string trackName)
        {
            var channel = new ChannelStripControl
            {
                TrackName = trackName,
                ChannelIndex = Channels.Count
            };
            Channels.Add(channel);
            return channel;
        }

        /// <summary>
        /// Registers a new channel with specific settings.
        /// </summary>
        public ChannelStripControl RegisterChannel(string trackName, double volume, double pan)
        {
            var channel = RegisterChannel(trackName);
            channel.Volume = volume;
            channel.Pan = pan;
            return channel;
        }

        /// <summary>
        /// Removes a channel from the mixer.
        /// </summary>
        public bool RemoveChannel(ChannelStripControl channel)
        {
            return Channels.Remove(channel);
        }

        /// <summary>
        /// Removes a channel by index.
        /// </summary>
        public bool RemoveChannelAt(int index)
        {
            if (index >= 0 && index < Channels.Count)
            {
                Channels.RemoveAt(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all channels from the mixer.
        /// </summary>
        public void ClearChannels()
        {
            Channels.Clear();
        }

        /// <summary>
        /// Gets a channel by index.
        /// </summary>
        public ChannelStripControl? GetChannel(int index)
        {
            if (index >= 0 && index < Channels.Count)
            {
                return Channels[index];
            }
            return null;
        }

        /// <summary>
        /// Updates the master LUFS meter values.
        /// </summary>
        public void UpdateMasterLUFS(double integrated, double shortTerm, double momentary, double truePeak)
        {
            MasterLUFSMeter.UpdateValues(integrated, shortTerm, momentary, truePeak);
        }

        /// <summary>
        /// Resets the master LUFS meter.
        /// </summary>
        public void ResetMasterLUFS()
        {
            MasterLUFSMeter.Reset();
        }

        /// <summary>
        /// Resets all channel peak meters.
        /// </summary>
        public void ResetAllPeaks()
        {
            foreach (var channel in Channels)
            {
                channel.ResetPeaks();
            }
        }

        #endregion
    }
}
