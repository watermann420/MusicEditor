// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and editing punch in/out recording markers.
/// </summary>
public partial class PunchLocatorControl : UserControl
{
    private PunchRecording? _punchSettings;
    private bool _isDraggingPunchIn;
    private bool _isDraggingPunchOut;
    private Point _dragStartPoint;
    private double _dragStartBeat;

    /// <summary>
    /// Gets or sets the punch recording settings to display and edit.
    /// </summary>
    public PunchRecording? PunchSettings
    {
        get => _punchSettings;
        set
        {
            if (_punchSettings != null)
            {
                _punchSettings.SettingsChanged -= OnPunchSettingsChanged;
                _punchSettings.PunchInTriggered -= OnPunchInTriggered;
                _punchSettings.PunchOutTriggered -= OnPunchOutTriggered;
            }

            _punchSettings = value;

            if (_punchSettings != null)
            {
                _punchSettings.SettingsChanged += OnPunchSettingsChanged;
                _punchSettings.PunchInTriggered += OnPunchInTriggered;
                _punchSettings.PunchOutTriggered += OnPunchOutTriggered;
            }

            RefreshDisplay();
            UpdateToggleStates();
        }
    }

    /// <summary>
    /// Gets or sets the number of beats visible in the view.
    /// </summary>
    public double VisibleBeats { get; set; } = 32;

    /// <summary>
    /// Gets or sets the scroll offset in beats.
    /// </summary>
    public double ScrollOffset { get; set; }

    /// <summary>
    /// Gets or sets the current playback position in beats.
    /// </summary>
    public double PlaybackPosition
    {
        get => _playbackPosition;
        set
        {
            _playbackPosition = value;
            UpdatePlayhead();
        }
    }
    private double _playbackPosition;

    /// <summary>
    /// Gets or sets the number of beats per bar (for pre/post roll calculation).
    /// </summary>
    public double BeatsPerBar { get; set; } = 4.0;

    /// <summary>
    /// Event raised when the punch-in position changes.
    /// </summary>
    public event EventHandler<double>? PunchInChanged;

    /// <summary>
    /// Event raised when the punch-out position changes.
    /// </summary>
    public event EventHandler<double>? PunchOutChanged;

    /// <summary>
    /// Event raised when a jump to position is requested (double-click on marker).
    /// </summary>
    public event EventHandler<double>? JumpRequested;

    /// <summary>
    /// Event raised when punch-in is triggered during playback.
    /// </summary>
    public event EventHandler<PunchEventArgs>? PunchInTriggeredEvent;

    /// <summary>
    /// Event raised when punch-out is triggered during playback.
    /// </summary>
    public event EventHandler<PunchOutEventArgs>? PunchOutTriggeredEvent;

    public PunchLocatorControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RefreshDisplay();
    }

    private void OnPunchSettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshDisplay();
            UpdateToggleStates();
        });
    }

    private void OnPunchInTriggered(object? sender, PunchEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Flash the punch-in marker
            FlashMarker(PunchInMarker);
            PunchInTriggeredEvent?.Invoke(this, e);
        });
    }

    private void OnPunchOutTriggered(object? sender, PunchOutEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Flash the punch-out marker
            FlashMarker(PunchOutMarker);
            PunchOutTriggeredEvent?.Invoke(this, e);
        });
    }

    private void FlashMarker(UIElement marker)
    {
        // Simple visual feedback when punch is triggered
        marker.Opacity = 0.5;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += (s, e) =>
        {
            marker.Opacity = 1.0;
            timer.Stop();
        };
        timer.Start();
    }

    /// <summary>
    /// Refreshes the visual display of all punch markers and regions.
    /// </summary>
    public void RefreshDisplay()
    {
        if (PunchCanvas.ActualWidth <= 0 || VisibleBeats <= 0)
            return;

        var pixelsPerBeat = PunchCanvas.ActualWidth / VisibleBeats;

        // Update punch-in marker
        if (_punchSettings?.PunchInEnabled == true)
        {
            var punchInX = (_punchSettings.PunchInBeat - ScrollOffset) * pixelsPerBeat;
            Canvas.SetLeft(PunchInMarker, punchInX - 5); // Center the triangle
            PunchInMarker.Visibility = Visibility.Visible;
            PunchInPositionText.Text = FormatBeatPosition(_punchSettings.PunchInBeat);
        }
        else
        {
            PunchInMarker.Visibility = Visibility.Collapsed;
            PunchInPositionText.Text = "--.--.--";
        }

        // Update punch-out marker
        if (_punchSettings?.PunchOutEnabled == true)
        {
            var punchOutX = (_punchSettings.PunchOutBeat - ScrollOffset) * pixelsPerBeat;
            Canvas.SetLeft(PunchOutMarker, punchOutX - 5); // Center the triangle
            PunchOutMarker.Visibility = Visibility.Visible;
            PunchOutPositionText.Text = FormatBeatPosition(_punchSettings.PunchOutBeat);
        }
        else
        {
            PunchOutMarker.Visibility = Visibility.Collapsed;
            PunchOutPositionText.Text = "--.--.--";
        }

        // Update punch region rectangle
        if (_punchSettings?.IsPunchRangeComplete == true)
        {
            var startX = (_punchSettings.PunchInBeat - ScrollOffset) * pixelsPerBeat;
            var endX = (_punchSettings.PunchOutBeat - ScrollOffset) * pixelsPerBeat;
            var width = endX - startX;

            if (width > 0)
            {
                Canvas.SetLeft(PunchRegionRect, startX);
                PunchRegionRect.Width = width;
                PunchRegionRect.Height = PunchCanvas.ActualHeight;
                PunchRegionRect.Visibility = Visibility.Visible;
            }
            else
            {
                PunchRegionRect.Visibility = Visibility.Collapsed;
            }

            // Update pre-roll region
            if (_punchSettings.PreRollBars > 0)
            {
                var preRollStart = _punchSettings.GetPreRollStartBeat(BeatsPerBar);
                var preRollStartX = (preRollStart - ScrollOffset) * pixelsPerBeat;
                var preRollWidth = startX - preRollStartX;

                if (preRollWidth > 0)
                {
                    Canvas.SetLeft(PreRollRegionRect, preRollStartX);
                    PreRollRegionRect.Width = preRollWidth;
                    PreRollRegionRect.Height = PunchCanvas.ActualHeight;
                    PreRollRegionRect.Visibility = Visibility.Visible;
                }
                else
                {
                    PreRollRegionRect.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                PreRollRegionRect.Visibility = Visibility.Collapsed;
            }

            // Update post-roll region
            if (_punchSettings.PostRollBars > 0)
            {
                var postRollEnd = _punchSettings.GetPostRollEndBeat(BeatsPerBar);
                var postRollEndX = (postRollEnd - ScrollOffset) * pixelsPerBeat;
                var postRollWidth = postRollEndX - endX;

                if (postRollWidth > 0)
                {
                    Canvas.SetLeft(PostRollRegionRect, endX);
                    PostRollRegionRect.Width = postRollWidth;
                    PostRollRegionRect.Height = PunchCanvas.ActualHeight;
                    PostRollRegionRect.Visibility = Visibility.Visible;
                }
                else
                {
                    PostRollRegionRect.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                PostRollRegionRect.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            PunchRegionRect.Visibility = Visibility.Collapsed;
            PreRollRegionRect.Visibility = Visibility.Collapsed;
            PostRollRegionRect.Visibility = Visibility.Collapsed;
        }

        UpdatePlayhead();
    }

    private void UpdateToggleStates()
    {
        if (_punchSettings == null)
        {
            PunchInToggle.IsChecked = false;
            PunchOutToggle.IsChecked = false;
            AutoPunchToggle.IsChecked = false;
            PreRollTextBox.Text = "1";
            PostRollTextBox.Text = "1";
            return;
        }

        // Update without triggering events
        PunchInToggle.Checked -= PunchInToggle_Checked;
        PunchOutToggle.Checked -= PunchOutToggle_Checked;
        AutoPunchToggle.Checked -= AutoPunchToggle_Checked;

        PunchInToggle.IsChecked = _punchSettings.PunchInEnabled;
        PunchOutToggle.IsChecked = _punchSettings.PunchOutEnabled;
        AutoPunchToggle.IsChecked = _punchSettings.AutoPunchEnabled;
        PreRollTextBox.Text = _punchSettings.PreRollBars.ToString();
        PostRollTextBox.Text = _punchSettings.PostRollBars.ToString();

        PunchInToggle.Checked += PunchInToggle_Checked;
        PunchOutToggle.Checked += PunchOutToggle_Checked;
        AutoPunchToggle.Checked += AutoPunchToggle_Checked;
    }

    private void UpdatePlayhead()
    {
        if (PunchCanvas.ActualWidth <= 0 || VisibleBeats <= 0)
            return;

        var pixelsPerBeat = PunchCanvas.ActualWidth / VisibleBeats;
        var x = (_playbackPosition - ScrollOffset) * pixelsPerBeat;

        if (x >= 0 && x <= PunchCanvas.ActualWidth)
        {
            Playhead.Visibility = Visibility.Visible;
            Canvas.SetLeft(Playhead, x);
            Playhead.Height = PunchCanvas.ActualHeight;
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private double PositionToBeats(double x)
    {
        if (PunchCanvas.ActualWidth <= 0 || VisibleBeats <= 0)
            return 0;

        var pixelsPerBeat = PunchCanvas.ActualWidth / VisibleBeats;
        return (x / pixelsPerBeat) + ScrollOffset;
    }

    private static string FormatBeatPosition(double beats)
    {
        // Format as Bar.Beat.Tick (assuming 4 beats per bar, 4 ticks per beat)
        int bar = (int)(beats / 4.0) + 1;
        int beat = (int)(beats % 4.0) + 1;
        int tick = (int)((beats % 1.0) * 4.0);
        return $"{bar}.{beat}.{tick:D2}";
    }

    #region Event Handlers

    private void PunchInToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_punchSettings != null)
        {
            _punchSettings.PunchInEnabled = true;
            if (_punchSettings.PunchInBeat == 0 && _playbackPosition > 0)
            {
                // Set punch-in at current playback position if not set
                _punchSettings.PunchInBeat = _playbackPosition;
            }
            RefreshDisplay();
        }
    }

    private void PunchInToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_punchSettings != null)
        {
            _punchSettings.PunchInEnabled = false;
            RefreshDisplay();
        }
    }

    private void PunchOutToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_punchSettings != null)
        {
            _punchSettings.PunchOutEnabled = true;
            if (_punchSettings.PunchOutBeat == 0 || _punchSettings.PunchOutBeat <= _punchSettings.PunchInBeat)
            {
                // Set punch-out at current playback position + 4 beats if not set
                _punchSettings.PunchOutBeat = Math.Max(_punchSettings.PunchInBeat + 4, _playbackPosition + 4);
            }
            RefreshDisplay();
        }
    }

    private void PunchOutToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_punchSettings != null)
        {
            _punchSettings.PunchOutEnabled = false;
            RefreshDisplay();
        }
    }

    private void AutoPunchToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_punchSettings != null)
        {
            _punchSettings.AutoPunchEnabled = true;
        }
    }

    private void AutoPunchToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_punchSettings != null)
        {
            _punchSettings.AutoPunchEnabled = false;
        }
    }

    private void PreRollTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_punchSettings != null && int.TryParse(PreRollTextBox.Text, out int preRoll))
        {
            _punchSettings.PreRollBars = Math.Max(0, Math.Min(8, preRoll));
            RefreshDisplay();
        }
    }

    private void PostRollTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_punchSettings != null && int.TryParse(PostRollTextBox.Text, out int postRoll))
        {
            _punchSettings.PostRollBars = Math.Max(0, Math.Min(8, postRoll));
            RefreshDisplay();
        }
    }

    private void PunchCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && _punchSettings != null)
        {
            // Double-click to set punch region at click position
            var position = PositionToBeats(e.GetPosition(PunchCanvas).X);
            if (!_punchSettings.PunchInEnabled)
            {
                _punchSettings.PunchInBeat = position;
                _punchSettings.PunchInEnabled = true;
                PunchInToggle.IsChecked = true;
            }
            else if (!_punchSettings.PunchOutEnabled && position > _punchSettings.PunchInBeat)
            {
                _punchSettings.PunchOutBeat = position;
                _punchSettings.PunchOutEnabled = true;
                PunchOutToggle.IsChecked = true;
            }
            RefreshDisplay();
            e.Handled = true;
        }
    }

    private void PunchInMarker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_punchSettings == null) return;

        if (e.ClickCount == 2)
        {
            // Double-click to jump to punch-in position
            JumpRequested?.Invoke(this, _punchSettings.PunchInBeat);
        }
        else
        {
            // Start dragging
            _isDraggingPunchIn = true;
            _dragStartPoint = e.GetPosition(PunchCanvas);
            _dragStartBeat = _punchSettings.PunchInBeat;
            PunchInMarker.CaptureMouse();
        }
        e.Handled = true;
    }

    private void PunchOutMarker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_punchSettings == null) return;

        if (e.ClickCount == 2)
        {
            // Double-click to jump to punch-out position
            JumpRequested?.Invoke(this, _punchSettings.PunchOutBeat);
        }
        else
        {
            // Start dragging
            _isDraggingPunchOut = true;
            _dragStartPoint = e.GetPosition(PunchCanvas);
            _dragStartBeat = _punchSettings.PunchOutBeat;
            PunchOutMarker.CaptureMouse();
        }
        e.Handled = true;
    }

    private void PunchCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_punchSettings == null) return;

        var currentPoint = e.GetPosition(PunchCanvas);

        if (_isDraggingPunchIn && e.LeftButton == MouseButtonState.Pressed)
        {
            var newPosition = PositionToBeats(currentPoint.X);
            newPosition = Math.Max(0, newPosition);

            // Don't allow punch-in to go past punch-out
            if (_punchSettings.PunchOutEnabled)
            {
                newPosition = Math.Min(newPosition, _punchSettings.PunchOutBeat - 0.25);
            }

            _punchSettings.PunchInBeat = newPosition;
            RefreshDisplay();
            PunchInChanged?.Invoke(this, newPosition);
        }
        else if (_isDraggingPunchOut && e.LeftButton == MouseButtonState.Pressed)
        {
            var newPosition = PositionToBeats(currentPoint.X);
            newPosition = Math.Max(0, newPosition);

            // Don't allow punch-out to go before punch-in
            if (_punchSettings.PunchInEnabled)
            {
                newPosition = Math.Max(newPosition, _punchSettings.PunchInBeat + 0.25);
            }

            _punchSettings.PunchOutBeat = newPosition;
            RefreshDisplay();
            PunchOutChanged?.Invoke(this, newPosition);
        }
    }

    private void PunchCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingPunchIn)
        {
            PunchInMarker.ReleaseMouseCapture();
            _isDraggingPunchIn = false;
        }

        if (_isDraggingPunchOut)
        {
            PunchOutMarker.ReleaseMouseCapture();
            _isDraggingPunchOut = false;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the punch region programmatically.
    /// </summary>
    /// <param name="punchInBeat">Punch-in position in beats.</param>
    /// <param name="punchOutBeat">Punch-out position in beats.</param>
    public void SetPunchRegion(double punchInBeat, double punchOutBeat)
    {
        _punchSettings?.SetPunchRegion(punchInBeat, punchOutBeat, true);
        UpdateToggleStates();
        RefreshDisplay();
    }

    /// <summary>
    /// Clears the punch region.
    /// </summary>
    public void ClearPunchRegion()
    {
        _punchSettings?.ClearPunchRegion();
        UpdateToggleStates();
        RefreshDisplay();
    }

    /// <summary>
    /// Sets punch-in at the current playback position.
    /// </summary>
    public void SetPunchInAtPlayhead()
    {
        if (_punchSettings != null)
        {
            _punchSettings.PunchInBeat = _playbackPosition;
            _punchSettings.PunchInEnabled = true;
            UpdateToggleStates();
            RefreshDisplay();
        }
    }

    /// <summary>
    /// Sets punch-out at the current playback position.
    /// </summary>
    public void SetPunchOutAtPlayhead()
    {
        if (_punchSettings != null)
        {
            _punchSettings.PunchOutBeat = _playbackPosition;
            _punchSettings.PunchOutEnabled = true;
            UpdateToggleStates();
            RefreshDisplay();
        }
    }

    #endregion
}
