// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Ableton Link tempo synchronization panel control.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MusicEngineEditor.ViewModels.Network;

namespace MusicEngineEditor.Controls.Network;

#region Converters

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class LinkBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Converts boolean to inverse boolean or visibility.
/// </summary>
public class LinkInverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Check if parameter contains text options for string conversion
            if (parameter is string paramStr && paramStr.Contains('|'))
            {
                var options = paramStr.Split('|');
                return boolValue ? options[0] : options[1];
            }

            // Return inverse visibility (always return Visibility type for XAML bindings)
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts LinkConnectionStatus to a color brush.
/// </summary>
public class LinkConnectionStatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush DisconnectedBrush = new(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly SolidColorBrush SearchingBrush = new(Color.FromRgb(0xFF, 0x95, 0x00));
    private static readonly SolidColorBrush ConnectedBrush = new(Color.FromRgb(0x00, 0xFF, 0x88));

    static LinkConnectionStatusToColorConverter()
    {
        DisconnectedBrush.Freeze();
        SearchingBrush.Freeze();
        ConnectedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LinkConnectionStatus status)
        {
            return status switch
            {
                LinkConnectionStatus.Connected => ConnectedBrush,
                LinkConnectionStatus.Searching => SearchingBrush,
                LinkConnectionStatus.Disconnected => DisconnectedBrush,
                _ => DisconnectedBrush
            };
        }
        return DisconnectedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts beat indicator state to color brush.
/// </summary>
public class LinkBeatIndicatorToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(0x3A, 0x3A, 0x3A));
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0x00, 0xD9, 0xFF));
    private static readonly SolidColorBrush DownbeatBrush = new(Color.FromRgb(0xFF, 0x6B, 0x6B));

    static LinkBeatIndicatorToColorConverter()
    {
        InactiveBrush.Freeze();
        ActiveBrush.Freeze();
        DownbeatBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BeatIndicator indicator)
        {
            if (!indicator.IsActive)
            {
                return InactiveBrush;
            }
            return indicator.IsDownbeat ? DownbeatBrush : ActiveBrush;
        }
        return InactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a fraction (0.0 to 1.0) to width for progress bars.
/// </summary>
public class FractionToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double fraction)
        {
            // Default max width, can be overridden by parameter
            double maxWidth = 100.0;
            if (parameter is double maxParam)
            {
                maxWidth = maxParam;
            }
            else if (parameter is string strParam && double.TryParse(strParam, out var parsed))
            {
                maxWidth = parsed;
            }

            return Math.Max(0, Math.Min(maxWidth, fraction * maxWidth));
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion

/// <summary>
/// Ableton Link tempo synchronization panel.
/// Provides Link session management, tempo sync, and peer discovery UI.
/// </summary>
public partial class LinkSyncPanel : UserControl, IDisposable
{
    #region Dependency Properties

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(LinkSyncViewModel), typeof(LinkSyncPanel),
            new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty IsLinkEnabledProperty =
        DependencyProperty.Register(nameof(IsLinkEnabled), typeof(bool), typeof(LinkSyncPanel),
            new PropertyMetadata(false, OnIsLinkEnabledChanged));

    public static readonly DependencyProperty SessionTempoProperty =
        DependencyProperty.Register(nameof(SessionTempo), typeof(double), typeof(LinkSyncPanel),
            new PropertyMetadata(120.0, OnSessionTempoChanged));

    public static readonly DependencyProperty QuantumProperty =
        DependencyProperty.Register(nameof(Quantum), typeof(int), typeof(LinkSyncPanel),
            new PropertyMetadata(4, OnQuantumChanged));

    /// <summary>
    /// Gets or sets the ViewModel.
    /// </summary>
    public LinkSyncViewModel? ViewModel
    {
        get => (LinkSyncViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Gets or sets whether Link is enabled.
    /// </summary>
    public bool IsLinkEnabled
    {
        get => (bool)GetValue(IsLinkEnabledProperty);
        set => SetValue(IsLinkEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the session tempo.
    /// </summary>
    public double SessionTempo
    {
        get => (double)GetValue(SessionTempoProperty);
        set => SetValue(SessionTempoProperty, value);
    }

    /// <summary>
    /// Gets or sets the quantum value.
    /// </summary>
    public int Quantum
    {
        get => (int)GetValue(QuantumProperty);
        set => SetValue(QuantumProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when Link is enabled or disabled.
    /// </summary>
    public event EventHandler<bool>? LinkStateChanged;

    /// <summary>
    /// Fired when tempo changes.
    /// </summary>
    public event EventHandler<double>? TempoChanged;

    /// <summary>
    /// Fired when a beat occurs.
    /// </summary>
    public event EventHandler<int>? BeatOccurred;

    /// <summary>
    /// Fired when sync state changes.
    /// </summary>
    public event EventHandler<bool>? SyncStateChanged;

    /// <summary>
    /// Fired when peer count changes.
    /// </summary>
    public event EventHandler<int>? PeerCountChanged;

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private bool _disposed;

    #endregion

    #region Constructor

    public LinkSyncPanel()
    {
        InitializeComponent();

        // Create default ViewModel
        ViewModel = new LinkSyncViewModel();
        DataContext = ViewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        if (ViewModel != null)
        {
            SubscribeToViewModelEvents();
            SyncDependencyPropertiesToViewModel();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;

        if (ViewModel != null)
        {
            UnsubscribeFromViewModelEvents();
        }
    }

    #endregion

    #region Property Changed Handlers

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LinkSyncPanel panel)
        {
            if (e.OldValue is LinkSyncViewModel oldVm)
            {
                panel.UnsubscribeFromViewModelEvents(oldVm);
            }

            if (e.NewValue is LinkSyncViewModel newVm)
            {
                panel.DataContext = newVm;

                if (panel._isInitialized)
                {
                    panel.SubscribeToViewModelEvents(newVm);
                    panel.SyncDependencyPropertiesToViewModel();
                }
            }
        }
    }

    private static void OnIsLinkEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LinkSyncPanel panel && panel.ViewModel != null)
        {
            panel.ViewModel.IsLinkEnabled = (bool)e.NewValue;
        }
    }

    private static void OnSessionTempoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LinkSyncPanel panel && panel.ViewModel != null)
        {
            panel.ViewModel.SessionTempo = (double)e.NewValue;
        }
    }

    private static void OnQuantumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LinkSyncPanel panel && panel.ViewModel != null)
        {
            panel.ViewModel.Quantum = (int)e.NewValue;
        }
    }

    #endregion

    #region ViewModel Event Subscriptions

    private void SubscribeToViewModelEvents(LinkSyncViewModel? vm = null)
    {
        vm ??= ViewModel;
        if (vm == null) return;

        vm.LinkStateChanged += OnViewModelLinkStateChanged;
        vm.TempoChanged += OnViewModelTempoChanged;
        vm.BeatOccurred += OnViewModelBeatOccurred;
        vm.SyncStateChanged += OnViewModelSyncStateChanged;
        vm.PeerCountChanged += OnViewModelPeerCountChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void UnsubscribeFromViewModelEvents(LinkSyncViewModel? vm = null)
    {
        vm ??= ViewModel;
        if (vm == null) return;

        vm.LinkStateChanged -= OnViewModelLinkStateChanged;
        vm.TempoChanged -= OnViewModelTempoChanged;
        vm.BeatOccurred -= OnViewModelBeatOccurred;
        vm.SyncStateChanged -= OnViewModelSyncStateChanged;
        vm.PeerCountChanged -= OnViewModelPeerCountChanged;
        vm.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void SyncDependencyPropertiesToViewModel()
    {
        if (ViewModel == null) return;

        // Sync from dependency properties to ViewModel
        if (IsLinkEnabled != ViewModel.IsLinkEnabled)
        {
            ViewModel.IsLinkEnabled = IsLinkEnabled;
        }

        if (Math.Abs(SessionTempo - ViewModel.SessionTempo) > 0.01)
        {
            ViewModel.SessionTempo = SessionTempo;
        }

        if (Quantum != ViewModel.Quantum)
        {
            ViewModel.Quantum = Quantum;
        }
    }

    #endregion

    #region ViewModel Event Handlers

    private void OnViewModelLinkStateChanged(object? sender, bool isEnabled)
    {
        IsLinkEnabled = isEnabled;
        LinkStateChanged?.Invoke(this, isEnabled);
    }

    private void OnViewModelTempoChanged(object? sender, double tempo)
    {
        SessionTempo = tempo;
        TempoChanged?.Invoke(this, tempo);
    }

    private void OnViewModelBeatOccurred(object? sender, int beat)
    {
        BeatOccurred?.Invoke(this, beat);
    }

    private void OnViewModelSyncStateChanged(object? sender, bool isRunning)
    {
        SyncStateChanged?.Invoke(this, isRunning);
    }

    private void OnViewModelPeerCountChanged(object? sender, int count)
    {
        PeerCountChanged?.Invoke(this, count);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Sync ViewModel properties back to dependency properties
        if (ViewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(LinkSyncViewModel.IsLinkEnabled):
                if (IsLinkEnabled != ViewModel.IsLinkEnabled)
                {
                    IsLinkEnabled = ViewModel.IsLinkEnabled;
                }
                break;

            case nameof(LinkSyncViewModel.SessionTempo):
                if (Math.Abs(SessionTempo - ViewModel.SessionTempo) > 0.01)
                {
                    SessionTempo = ViewModel.SessionTempo;
                }
                break;

            case nameof(LinkSyncViewModel.Quantum):
                if (Quantum != ViewModel.Quantum)
                {
                    Quantum = ViewModel.Quantum;
                }
                break;

            case nameof(LinkSyncViewModel.BeatFraction):
                UpdateBeatProgressBar();
                break;

            case nameof(LinkSyncViewModel.IsSyncRunning):
                UpdateSyncButtonText();
                break;
        }
    }

    #endregion

    #region UI Event Handlers

    private void DecreaseSessionTempo_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.DecreaseTempoCommand.Execute(null);
    }

    private void IncreaseSessionTempo_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.IncreaseTempoCommand.Execute(null);
    }

    private void DecreaseLatency_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.DecreaseLatencyCommand.Execute(null);
    }

    private void IncreaseLatency_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.IncreaseLatencyCommand.Execute(null);
    }

    private void ResetLatency_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ResetLatencyCommand.Execute(null);
    }

    private void RefreshPeers_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.RefreshPeersCommand.Execute(null);
    }

    #endregion

    #region Display Methods

    private void UpdateBeatProgressBar()
    {
        if (ViewModel == null) return;

        var fraction = ViewModel.BeatFraction;
        var containerWidth = BeatProgressContainer?.ActualWidth ?? 100;
        BeatProgressBar.Width = fraction * containerWidth;
    }

    private void UpdateSyncButtonText()
    {
        if (ViewModel == null) return;

        SyncButtonText.Text = ViewModel.IsSyncRunning ? "STOP" : "START";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Enables Link synchronization.
    /// </summary>
    public void EnableLink()
    {
        ViewModel?.EnableLink();
    }

    /// <summary>
    /// Disables Link synchronization.
    /// </summary>
    public void DisableLink()
    {
        ViewModel?.DisableLink();
    }

    /// <summary>
    /// Starts sync playback.
    /// </summary>
    public void StartSync()
    {
        ViewModel?.StartSync();
    }

    /// <summary>
    /// Stops sync playback.
    /// </summary>
    public void StopSync()
    {
        ViewModel?.StopSync();
    }

    /// <summary>
    /// Sets the session tempo.
    /// </summary>
    /// <param name="tempo">The tempo in BPM.</param>
    public void SetSessionTempo(double tempo)
    {
        if (ViewModel != null)
        {
            ViewModel.SessionTempo = tempo;
        }
    }

    /// <summary>
    /// Sets the quantum value.
    /// </summary>
    /// <param name="quantum">Beats per bar (1-16).</param>
    public void SetQuantum(int quantum)
    {
        ViewModel?.SetQuantum(quantum);
    }

    /// <summary>
    /// Updates the panel from an external Link session.
    /// </summary>
    /// <param name="tempo">Current tempo.</param>
    /// <param name="phase">Current phase.</param>
    /// <param name="isPlaying">Whether the session is playing.</param>
    public void UpdateFromLinkSession(double tempo, double phase, bool isPlaying)
    {
        ViewModel?.UpdateFromLinkSession(tempo, phase, isPlaying);
    }

    /// <summary>
    /// Adds a peer to the session.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="applicationName">The application name.</param>
    /// <param name="isLeader">Whether this peer is the tempo leader.</param>
    public void AddPeer(string peerId, string applicationName, bool isLeader = false)
    {
        ViewModel?.AddPeer(new LinkPeerInfo
        {
            PeerId = peerId,
            ApplicationName = applicationName,
            DiscoveredAt = DateTime.Now,
            IsTempoLeader = isLeader
        });
    }

    /// <summary>
    /// Removes a peer from the session.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    public void RemovePeer(string peerId)
    {
        ViewModel?.RemovePeer(peerId);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the control and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        UnsubscribeFromViewModelEvents();
        ViewModel?.Dispose();
        ViewModel = null;

        GC.SuppressFinalize(this);
    }

    #endregion
}
