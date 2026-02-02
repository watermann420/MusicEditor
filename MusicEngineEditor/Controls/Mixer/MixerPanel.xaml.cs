using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Mixer
{
    /// <summary>
    /// Mixer panel with scrollable channel strips, return tracks, and a fixed master channel.
    /// </summary>
    public partial class MixerPanel : UserControl
    {
        #region Constants

        private const int DefaultReturnCount = 4;

        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty MasterVolumeProperty =
            DependencyProperty.Register(nameof(MasterVolume), typeof(double), typeof(MixerPanel),
                new PropertyMetadata(0.0, OnMasterVolumeChanged, CoerceVolume));

        public static readonly DependencyProperty IsMasterMutedProperty =
            DependencyProperty.Register(nameof(IsMasterMuted), typeof(bool), typeof(MixerPanel),
                new PropertyMetadata(false, OnIsMasterMutedChanged));

        public static readonly DependencyProperty IsMasterSoloProperty =
            DependencyProperty.Register(nameof(IsMasterSolo), typeof(bool), typeof(MixerPanel),
                new PropertyMetadata(false, OnIsMasterSoloChanged));

        #endregion

        #region Properties

        /// <summary>
        /// The collection of channel strips in the mixer.
        /// </summary>
        public ObservableCollection<ChannelStripControl> Channels { get; } = new();

        /// <summary>
        /// The collection of return tracks in the mixer.
        /// </summary>
        public ObservableCollection<ReturnTrackControl> Returns { get; } = new();

        /// <summary>
        /// Master volume in dB (-60 to +6).
        /// </summary>
        public double MasterVolume
        {
            get => (double)GetValue(MasterVolumeProperty);
            set => SetValue(MasterVolumeProperty, value);
        }

        /// <summary>
        /// Whether the master channel is muted.
        /// </summary>
        public bool IsMasterMuted
        {
            get => (bool)GetValue(IsMasterMutedProperty);
            set => SetValue(IsMasterMutedProperty, value);
        }

        /// <summary>
        /// Whether the master channel is soloed.
        /// </summary>
        public bool IsMasterSolo
        {
            get => (bool)GetValue(IsMasterSoloProperty);
            set => SetValue(IsMasterSoloProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<double>? MasterVolumeChanged;
        public event EventHandler<bool>? MasterMuteChanged;
        public event EventHandler<bool>? MasterSoloChanged;
        public event EventHandler? MasterEffectsChainRequested;
        public event EventHandler<(int ChannelIndex, double Volume)>? ChannelVolumeChanged;
        public event EventHandler<(int ChannelIndex, double Pan)>? ChannelPanChanged;
        public event EventHandler<(int ChannelIndex, bool IsSolo)>? ChannelSoloChanged;
        public event EventHandler<(int ChannelIndex, bool IsMuted)>? ChannelMuteChanged;
        public event EventHandler<(int ChannelIndex, string Name)>? ChannelNameChanged;
        public event EventHandler<(int ChannelIndex, int SendIndex, double Value)>? ChannelSendChanged;

        // Return track events
        public event EventHandler<(int ReturnIndex, double Volume)>? ReturnVolumeChanged;
        public event EventHandler<(int ReturnIndex, double Pan)>? ReturnPanChanged;
        public event EventHandler<(int ReturnIndex, bool IsMuted)>? ReturnMuteChanged;
        public event EventHandler<int>? ReturnEffectsChainRequested;
        public event EventHandler<int>? ReturnAdded;
        public event EventHandler<int>? ReturnRemoved;

        #endregion

        #region Constructor

        public MixerPanel()
        {
            InitializeComponent();
            Channels.CollectionChanged += Channels_CollectionChanged;
            Returns.CollectionChanged += Returns_CollectionChanged;

            // Initialize default return tracks
            InitializeDefaultReturns();
        }

        #endregion

        #region Initialization

        private void InitializeDefaultReturns()
        {
            for (int i = 0; i < DefaultReturnCount; i++)
            {
                AddReturnTrack();
            }
        }

        #endregion

        #region Property Changed Callbacks

        private static object CoerceVolume(DependencyObject d, object baseValue)
        {
            var value = (double)baseValue;
            return Math.Max(-60.0, Math.Min(6.0, value));
        }

        private static void OnMasterVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MixerPanel panel)
            {
                panel.MasterChannel.Volume = (double)e.NewValue;
            }
        }

        private static void OnIsMasterMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MixerPanel panel)
            {
                panel.MasterChannel.IsMuted = (bool)e.NewValue;
            }
        }

        private static void OnIsMasterSoloChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MixerPanel panel)
            {
                panel.MasterChannel.IsSolo = (bool)e.NewValue;
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

        private void Returns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        foreach (ReturnTrackControl returnTrack in e.NewItems)
                        {
                            AddReturnToContainer(returnTrack);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (ReturnTrackControl returnTrack in e.OldItems)
                        {
                            RemoveReturnFromContainer(returnTrack);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    ReturnTrackContainer.Children.Clear();
                    break;
            }

            UpdateReturnIndices();
        }

        private void MasterChannel_VolumeChanged(object? sender, double volume)
        {
            if (Math.Abs(MasterVolume - volume) > 0.001)
            {
                MasterVolume = volume;
                MasterVolumeChanged?.Invoke(this, volume);
            }
        }

        private void MasterChannel_SoloChanged(object? sender, bool isSolo)
        {
            if (IsMasterSolo != isSolo)
            {
                IsMasterSolo = isSolo;
                MasterSoloChanged?.Invoke(this, isSolo);
            }
        }

        private void MasterChannel_MuteChanged(object? sender, bool isMuted)
        {
            if (IsMasterMuted != isMuted)
            {
                IsMasterMuted = isMuted;
                MasterMuteChanged?.Invoke(this, isMuted);
            }
        }

        private void MasterChannel_EffectsChainRequested(object? sender, EventArgs e)
        {
            MasterEffectsChainRequested?.Invoke(this, EventArgs.Empty);
        }

        private void AddReturnButton_Click(object sender, RoutedEventArgs e)
        {
            var returnTrack = AddReturnTrack();
            ReturnAdded?.Invoke(this, returnTrack.ReturnIndex);
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

        private void Channel_SendChanged(object? sender, (int SendIndex, double Value) sendData)
        {
            if (sender is ChannelStripControl channel)
            {
                ChannelSendChanged?.Invoke(this, (channel.ChannelIndex, sendData.SendIndex, sendData.Value));
            }
        }

        private void Return_VolumeChanged(object? sender, double volume)
        {
            if (sender is ReturnTrackControl returnTrack)
            {
                ReturnVolumeChanged?.Invoke(this, (returnTrack.ReturnIndex, volume));
            }
        }

        private void Return_PanChanged(object? sender, double pan)
        {
            if (sender is ReturnTrackControl returnTrack)
            {
                ReturnPanChanged?.Invoke(this, (returnTrack.ReturnIndex, pan));
            }
        }

        private void Return_MuteChanged(object? sender, bool isMuted)
        {
            if (sender is ReturnTrackControl returnTrack)
            {
                ReturnMuteChanged?.Invoke(this, (returnTrack.ReturnIndex, isMuted));
            }
        }

        private void Return_EffectsChainRequested(object? sender, int returnIndex)
        {
            ReturnEffectsChainRequested?.Invoke(this, returnIndex);
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
            channel.SendChanged += Channel_SendChanged;
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
            channel.SendChanged -= Channel_SendChanged;
            ChannelStripContainer.Children.Remove(channel);
        }

        private void AddReturnToContainer(ReturnTrackControl returnTrack)
        {
            returnTrack.VolumeChanged += Return_VolumeChanged;
            returnTrack.PanChanged += Return_PanChanged;
            returnTrack.MuteChanged += Return_MuteChanged;
            returnTrack.EffectsChainRequested += Return_EffectsChainRequested;
            returnTrack.Margin = new Thickness(2);
            ReturnTrackContainer.Children.Add(returnTrack);
        }

        private void RemoveReturnFromContainer(ReturnTrackControl returnTrack)
        {
            returnTrack.VolumeChanged -= Return_VolumeChanged;
            returnTrack.PanChanged -= Return_PanChanged;
            returnTrack.MuteChanged -= Return_MuteChanged;
            returnTrack.EffectsChainRequested -= Return_EffectsChainRequested;
            ReturnTrackContainer.Children.Remove(returnTrack);
        }

        private void UpdateChannelIndices()
        {
            for (int i = 0; i < Channels.Count; i++)
            {
                Channels[i].ChannelIndex = i;
            }
        }

        private void UpdateReturnIndices()
        {
            for (int i = 0; i < Returns.Count; i++)
            {
                Returns[i].ReturnIndex = i;
                Returns[i].ReturnLabel = ReturnTrackControl.GetLabelForIndex(i);
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
        /// Adds a new return track to the mixer.
        /// </summary>
        /// <returns>The created return track control.</returns>
        public ReturnTrackControl AddReturnTrack()
        {
            var returnTrack = new ReturnTrackControl
            {
                ReturnIndex = Returns.Count,
                ReturnLabel = ReturnTrackControl.GetLabelForIndex(Returns.Count)
            };
            Returns.Add(returnTrack);
            return returnTrack;
        }

        /// <summary>
        /// Adds a new return track with specific settings.
        /// </summary>
        public ReturnTrackControl AddReturnTrack(double volume, double pan)
        {
            var returnTrack = AddReturnTrack();
            returnTrack.Volume = volume;
            returnTrack.Pan = pan;
            return returnTrack;
        }

        /// <summary>
        /// Removes a return track from the mixer.
        /// </summary>
        public bool RemoveReturn(ReturnTrackControl returnTrack)
        {
            var result = Returns.Remove(returnTrack);
            if (result)
            {
                ReturnRemoved?.Invoke(this, returnTrack.ReturnIndex);
            }
            return result;
        }

        /// <summary>
        /// Removes a return track by index.
        /// </summary>
        public bool RemoveReturnAt(int index)
        {
            if (index >= 0 && index < Returns.Count)
            {
                var returnIndex = Returns[index].ReturnIndex;
                Returns.RemoveAt(index);
                ReturnRemoved?.Invoke(this, returnIndex);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a return track by index.
        /// </summary>
        public ReturnTrackControl? GetReturn(int index)
        {
            if (index >= 0 && index < Returns.Count)
            {
                return Returns[index];
            }
            return null;
        }

        /// <summary>
        /// Clears all return tracks and re-creates the default 4.
        /// </summary>
        public void ResetReturns()
        {
            Returns.Clear();
            InitializeDefaultReturns();
        }

        /// <summary>
        /// Updates the master metering values (LUFS and VU levels).
        /// </summary>
        public void UpdateMasterMetering(double leftLevel, double rightLevel,
            double integratedLUFS, double shortTermLUFS, double truePeak)
        {
            MasterChannel.UpdateAllMetering(leftLevel, rightLevel, integratedLUFS, shortTermLUFS, truePeak);
        }

        /// <summary>
        /// Updates the master VU meter levels.
        /// </summary>
        public void UpdateMasterLevels(double left, double right)
        {
            MasterChannel.SetLevels(left, right);
        }

        /// <summary>
        /// Updates the master LUFS values.
        /// </summary>
        public void UpdateMasterLUFS(double integrated, double shortTerm, double truePeak)
        {
            MasterChannel.UpdateMetering(integrated, shortTerm, truePeak);
        }

        /// <summary>
        /// Resets the master metering.
        /// </summary>
        public void ResetMasterMetering()
        {
            MasterChannel.ResetMetering();
        }

        /// <summary>
        /// Resets the master peak indicators.
        /// </summary>
        public void ResetMasterPeaks()
        {
            MasterChannel.ResetPeaks();
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
            foreach (var returnTrack in Returns)
            {
                returnTrack.ResetPeaks();
            }
            MasterChannel.ResetPeaks();
        }

        /// <summary>
        /// Gets the number of return tracks.
        /// </summary>
        public int ReturnCount => Returns.Count;

        /// <summary>
        /// Gets the master channel strip control for direct access.
        /// </summary>
        public MasterChannelStrip MasterChannelStrip => MasterChannel;

        #endregion
    }
}
