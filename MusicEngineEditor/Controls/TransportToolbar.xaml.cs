// MusicEngineEditor - Transport Toolbar Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Transport toolbar control providing playback controls, BPM adjustment,
/// time display, and position scrubbing.
/// </summary>
public partial class TransportToolbar : UserControl
{
    #region Private Fields

    private TransportViewModel? _viewModel;
    private bool _isDraggingSlider;
    private bool _isScrubbing;
    private bool _showTimeDisplay;
    private Storyboard? _metronomePulseStoryboard;
    private double _scrubStartBeat;

    #endregion

    #region Constructor

    public TransportToolbar()
    {
        InitializeComponent();
        InitializeMetronomeAnimation();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Binds the toolbar to a TransportViewModel.
    /// </summary>
    /// <param name="viewModel">The TransportViewModel to bind to.</param>
    public void BindToViewModel(TransportViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Subscribe to property changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Initialize UI from ViewModel
        UpdatePlayPauseButton();
        UpdateRecordButton();
        UpdateLoopButton();
        UpdateMetronomeButton();
        UpdateBpmDisplay();
        UpdateTimeDisplay();
        UpdatePositionSlider();
    }

    /// <summary>
    /// Unbinds the toolbar from the current ViewModel.
    /// </summary>
    public void Unbind()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel = null;
        }
        DataContext = null;
    }

    #endregion

    #region Event Handlers - Transport Buttons

    private void RewindButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.JumpToStartCommand.Execute(null);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.StopCommand.Execute(null);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.TogglePlayPauseCommand.Execute(null);
    }

    private void RecordButton_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var isChecked = RecordButton.IsChecked == true;
        if (_viewModel.IsRecording != isChecked)
        {
            _viewModel.ToggleRecordCommand.Execute(null);
        }
    }

    #endregion

    #region Event Handlers - BPM Control

    private void BpmDecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.DecreaseBpmCommand.Execute(null);
    }

    private void BpmIncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.IncreaseBpmCommand.Execute(null);
    }

    private void BpmTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyBpmFromTextBox();
            e.Handled = true;
            Keyboard.ClearFocus();
        }
        else if (e.Key == Key.Escape)
        {
            UpdateBpmDisplay();
            e.Handled = true;
            Keyboard.ClearFocus();
        }
    }

    private void BpmTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyBpmFromTextBox();
    }

    private void ApplyBpmFromTextBox()
    {
        if (_viewModel == null) return;

        if (double.TryParse(BpmTextBox.Text, out var bpm))
        {
            bpm = Math.Clamp(bpm, 20.0, 999.0);
            _viewModel.SetBpmCommand.Execute(bpm);
        }
        else
        {
            UpdateBpmDisplay();
        }
    }

    #endregion

    #region Event Handlers - Position Slider

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel == null || !_isDraggingSlider) return;

        // Calculate beat from slider position
        var beat = (PositionSlider.Value / 100.0) * _viewModel.LoopEnd;

        // Update scrub position for audio feedback
        if (_isScrubbing)
        {
            Services.ScrubService.Instance.UpdateScrub(beat);
        }

        // Update time display while dragging
        UpdateTimeDisplayForBeat(beat);
    }

    private void PositionSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = true;

        if (_viewModel == null) return;

        // Calculate initial beat position
        var beat = (PositionSlider.Value / 100.0) * _viewModel.LoopEnd;
        _scrubStartBeat = beat;

        // Start scrubbing for audio feedback
        _isScrubbing = true;
        Services.ScrubService.Instance.StartScrub(beat);

        // Update cursor to indicate scrubbing
        PositionSlider.Cursor = Cursors.IBeam;
    }

    private void PositionSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null || !_isDraggingSlider) return;

        _isDraggingSlider = false;

        // Calculate final beat position
        var beat = (PositionSlider.Value / 100.0) * _viewModel.LoopEnd;

        // End scrubbing
        if (_isScrubbing)
        {
            _isScrubbing = false;

            // Check if Shift is held to continue playback
            var continuePlayback = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            Services.ScrubService.Instance.EndScrub(continuePlayback);
        }

        // Reset cursor
        PositionSlider.Cursor = Cursors.Arrow;

        // If not continuing playback, set the final position
        try
        {
            var playbackService = Services.PlaybackService.Instance;
            playbackService.SetPosition(beat);
        }
        catch
        {
            // Ignore errors
        }
    }

    #endregion

    #region Event Handlers - Time Display

    private void TimeDisplay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _showTimeDisplay = !_showTimeDisplay;

        if (_showTimeDisplay)
        {
            BarBeatDisplay.Visibility = Visibility.Collapsed;
            TimeDisplay.Visibility = Visibility.Visible;
        }
        else
        {
            BarBeatDisplay.Visibility = Visibility.Visible;
            TimeDisplay.Visibility = Visibility.Collapsed;
        }

        UpdateTimeDisplay();
    }

    #endregion

    #region Event Handlers - Toggle Buttons

    private void LoopToggleButton_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var isChecked = LoopToggleButton.IsChecked == true;
        if (_viewModel.LoopEnabled != isChecked)
        {
            _viewModel.ToggleLoopCommand.Execute(null);
        }
    }

    private void MetronomeToggleButton_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var isChecked = MetronomeToggleButton.IsChecked == true;
        if (_viewModel.MetronomeEnabled != isChecked)
        {
            _viewModel.ToggleMetronomeCommand.Execute(null);
        }

        // Start or stop the metronome animation
        if (isChecked)
        {
            _metronomePulseStoryboard?.Begin(this, true);
        }
        else
        {
            _metronomePulseStoryboard?.Stop(this);
            MetronomePendulumTransform.Angle = 0;
        }
    }

    #endregion

    #region ViewModel Property Changed

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TransportViewModel.IsPlaying):
            case nameof(TransportViewModel.IsPaused):
                UpdatePlayPauseButton();
                break;

            case nameof(TransportViewModel.IsRecording):
                UpdateRecordButton();
                break;

            case nameof(TransportViewModel.LoopEnabled):
                UpdateLoopButton();
                break;

            case nameof(TransportViewModel.MetronomeEnabled):
                UpdateMetronomeButton();
                break;

            case nameof(TransportViewModel.Bpm):
                UpdateBpmDisplay();
                break;

            case nameof(TransportViewModel.CurrentBeat):
            case nameof(TransportViewModel.CurrentTime):
            case nameof(TransportViewModel.CurrentBar):
            case nameof(TransportViewModel.BeatInBar):
                UpdateTimeDisplay();
                UpdatePositionSlider();
                break;
        }
    }

    #endregion

    #region UI Update Methods

    private void UpdatePlayPauseButton()
    {
        if (_viewModel == null) return;

        var isPlaying = _viewModel.IsPlaying;
        PlayIcon.Visibility = isPlaying ? Visibility.Collapsed : Visibility.Visible;
        PauseIcon.Visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateRecordButton()
    {
        if (_viewModel == null) return;

        RecordButton.IsChecked = _viewModel.IsRecording;
    }

    private void UpdateLoopButton()
    {
        if (_viewModel == null) return;

        LoopToggleButton.IsChecked = _viewModel.LoopEnabled;
    }

    private void UpdateMetronomeButton()
    {
        if (_viewModel == null) return;

        MetronomeToggleButton.IsChecked = _viewModel.MetronomeEnabled;

        if (_viewModel.MetronomeEnabled)
        {
            _metronomePulseStoryboard?.Begin(this, true);
        }
        else
        {
            _metronomePulseStoryboard?.Stop(this);
            MetronomePendulumTransform.Angle = 0;
        }
    }

    private void UpdateBpmDisplay()
    {
        if (_viewModel == null) return;

        BpmTextBox.Text = _viewModel.Bpm.ToString("F1");
    }

    private void UpdateTimeDisplay()
    {
        if (_viewModel == null) return;

        if (_showTimeDisplay)
        {
            TimeDisplay.Text = _viewModel.CurrentTimeFormatted;
        }
        else
        {
            BarDisplay.Text = _viewModel.CurrentBar.ToString();
            BeatDisplay.Text = _viewModel.BeatInBar.ToString();
        }
    }

    private void UpdateTimeDisplayForBeat(double beat)
    {
        if (_viewModel == null) return;

        var beatsPerBar = _viewModel.TimeSignatureNumerator;
        var bar = (int)(beat / beatsPerBar) + 1;
        var beatInBar = (int)(beat % beatsPerBar) + 1;

        if (_showTimeDisplay)
        {
            var time = (beat / _viewModel.Bpm) * 60.0;
            var minutes = (int)(time / 60);
            var seconds = (int)(time % 60);
            var milliseconds = (int)((time % 1) * 1000);
            TimeDisplay.Text = $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
        }
        else
        {
            BarDisplay.Text = bar.ToString();
            BeatDisplay.Text = beatInBar.ToString();
        }
    }

    private void UpdatePositionSlider()
    {
        if (_viewModel == null || _isDraggingSlider) return;

        var maxBeat = _viewModel.LoopEnd;
        if (maxBeat > 0)
        {
            var percentage = (_viewModel.CurrentBeat / maxBeat) * 100.0;
            PositionSlider.Value = Math.Clamp(percentage, 0, 100);
        }
    }

    #endregion

    #region Animation

    private void InitializeMetronomeAnimation()
    {
        _metronomePulseStoryboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true
        };

        var animation = new DoubleAnimation
        {
            From = -15,
            To = 15,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTargetName(animation, nameof(MetronomePendulumTransform));
        Storyboard.SetTargetProperty(animation, new PropertyPath(System.Windows.Media.RotateTransform.AngleProperty));

        _metronomePulseStoryboard.Children.Add(animation);
    }

    #endregion
}
