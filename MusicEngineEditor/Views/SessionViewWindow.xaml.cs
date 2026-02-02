// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Floating window for the Session View with clip launcher grid.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Floating window for the Session View / Clip Launcher.
/// Features custom title bar with drag support and the full session grid.
/// </summary>
public partial class SessionViewWindow : Window
{
    #region Private Fields

    private bool _keepRunning = true;

    #endregion

    #region Events

    /// <summary>
    /// Raised when a clip edit is requested.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? ClipEditRequested;

    /// <summary>
    /// Raised when the session starts.
    /// </summary>
    public event EventHandler? SessionStarted;

    /// <summary>
    /// Raised when the session stops.
    /// </summary>
    public event EventHandler? SessionStopped;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the SessionViewModel from the embedded SessionView.
    /// </summary>
    public SessionViewModel? ViewModel => SessionViewControl.GetViewModel();

    #endregion

    #region Constructor

    public SessionViewWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    #endregion

    #region Window Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set owner to MainWindow if available
        if (Owner == null && Application.Current.MainWindow != this)
        {
            Owner = Application.Current.MainWindow;
        }

        // Wire up clip edit event
        SessionViewControl.ClipEditRequested += OnClipEditRequested;

        // Update grid size display
        UpdateGridSizeDisplay();

        // Subscribe to ViewModel changes
        var vm = ViewModel;
        if (vm != null)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_keepRunning)
        {
            // Hide instead of close to allow re-showing
            e.Cancel = true;
            Hide();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Update maximize button icon based on state
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "\u2750" : "\u25A1";
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionViewModel.TrackCount) ||
            e.PropertyName == nameof(SessionViewModel.SceneCount))
        {
            Dispatcher.Invoke(UpdateGridSizeDisplay);
        }
        else if (e.PropertyName == nameof(SessionViewModel.IsRunning))
        {
            var vm = ViewModel;
            if (vm?.IsRunning == true)
            {
                SessionStarted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                SessionStopped?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnClipEditRequested(object? sender, ClipSlotViewModel slot)
    {
        ClipEditRequested?.Invoke(this, slot);
    }

    #endregion

    #region Title Bar Event Handlers

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to toggle maximize
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Shows the window.
    /// </summary>
    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    /// <summary>
    /// Forces the window to close permanently.
    /// </summary>
    public void ForceClose()
    {
        _keepRunning = false;
        SessionViewControl.Dispose();
        Close();
    }

    /// <summary>
    /// Starts the session.
    /// </summary>
    public void Start()
    {
        SessionViewControl.Start();
    }

    /// <summary>
    /// Stops the session.
    /// </summary>
    public void Stop()
    {
        SessionViewControl.Stop();
    }

    /// <summary>
    /// Stops all clips.
    /// </summary>
    public void StopAll()
    {
        SessionViewControl.StopAll();
    }

    /// <summary>
    /// Resets the session to beat 0.
    /// </summary>
    public void Reset()
    {
        SessionViewControl.Reset();
    }

    /// <summary>
    /// Launches a clip at the specified position.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <param name="sceneIndex">Scene index.</param>
    public void LaunchClip(int trackIndex, int sceneIndex)
    {
        SessionViewControl.LaunchClip(trackIndex, sceneIndex);
    }

    /// <summary>
    /// Launches a scene.
    /// </summary>
    /// <param name="sceneIndex">Scene index.</param>
    public void LaunchScene(int sceneIndex)
    {
        SessionViewControl.LaunchScene(sceneIndex);
    }

    /// <summary>
    /// Stops a track.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    public void StopTrack(int trackIndex)
    {
        SessionViewControl.StopTrack(trackIndex);
    }

    /// <summary>
    /// Sets the tempo.
    /// </summary>
    /// <param name="bpm">Tempo in BPM.</param>
    public void SetTempo(double bpm)
    {
        SessionViewControl.SetTempo(bpm);
    }

    /// <summary>
    /// Processes the clip launcher for the given time delta.
    /// </summary>
    /// <param name="deltaBeats">Time elapsed in beats.</param>
    public void Process(double deltaBeats)
    {
        SessionViewControl.Process(deltaBeats);
    }

    #endregion

    #region Private Methods

    private void UpdateGridSizeDisplay()
    {
        var vm = ViewModel;
        if (vm != null)
        {
            GridSizeText.Text = $"- {vm.TrackCount} x {vm.SceneCount} grid";
        }
    }

    #endregion
}
