// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the ClipSlotControl - an individual clip slot in the session grid.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls.Session;

/// <summary>
/// Interaction logic for ClipSlotControl.xaml.
/// Represents an individual clip slot in the session grid with play/stop functionality,
/// progress display, and visual feedback for various states.
/// </summary>
public partial class ClipSlotControl : UserControl
{
    private bool _isPressed;
    private Storyboard? _recordingStoryboard;

    /// <summary>
    /// Event raised when the slot is clicked (to launch/trigger).
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? SlotClicked;

    /// <summary>
    /// Event raised when the slot is double-clicked (to edit).
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? SlotDoubleClicked;

    /// <summary>
    /// Event raised when launch is requested from context menu.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? LaunchRequested;

    /// <summary>
    /// Event raised when stop is requested from context menu.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? StopRequested;

    /// <summary>
    /// Event raised when edit is requested from context menu.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? EditRequested;

    /// <summary>
    /// Event raised when create clip is requested from context menu.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? CreateRequested;

    /// <summary>
    /// Event raised when duplicate is requested from context menu.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? DuplicateRequested;

    /// <summary>
    /// Event raised when delete is requested from context menu.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? DeleteRequested;

    /// <summary>
    /// Event raised when color change is requested from context menu.
    /// </summary>
    public event EventHandler<(ClipSlotViewModel Slot, Color Color)>? ColorChangeRequested;

    /// <summary>
    /// Creates a new ClipSlotControl.
    /// </summary>
    public ClipSlotControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Get the recording storyboard
        _recordingStoryboard = Resources["RecordingPulseStoryboard"] as Storyboard;

        // Update recording animation state
        UpdateRecordingAnimation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _recordingStoryboard?.Stop();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ClipSlotViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is ClipSlotViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateRecordingAnimation();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClipSlotViewModel.IsRecording))
        {
            Dispatcher.Invoke(UpdateRecordingAnimation);
        }
    }

    private void UpdateRecordingAnimation()
    {
        if (_recordingStoryboard == null) return;

        var vm = DataContext as ClipSlotViewModel;
        if (vm?.IsRecording == true)
        {
            _recordingStoryboard.Begin(this, true);
        }
        else
        {
            _recordingStoryboard.Stop(this);
            RecordingOverlay.Opacity = 0;
        }
    }

    #region Mouse Event Handlers

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPressed = true;
        SlotBorder.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));

        // Show play/stop icon on press
        if (DataContext is ClipSlotViewModel vm && vm.HasClip)
        {
            if (vm.IsPlaying)
            {
                StopIcon.Opacity = 0.8;
            }
            else
            {
                PlayIcon.Opacity = 0.8;
            }
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPressed)
        {
            _isPressed = false;
            SlotBorder.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            PlayIcon.Opacity = 0;
            StopIcon.Opacity = 0;

            if (DataContext is ClipSlotViewModel vm)
            {
                SlotClicked?.Invoke(this, vm);
            }
        }
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ClipSlotViewModel vm)
        {
            SlotDoubleClicked?.Invoke(this, vm);
            e.Handled = true;
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        HoverOverlay.Opacity = 1;

        // Show play/stop icon hint on hover
        if (DataContext is ClipSlotViewModel vm && vm.HasClip)
        {
            if (vm.IsPlaying)
            {
                StopIcon.Opacity = 0.4;
            }
            else
            {
                PlayIcon.Opacity = 0.4;
            }
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _isPressed = false;
        HoverOverlay.Opacity = 0;
        PlayIcon.Opacity = 0;
        StopIcon.Opacity = 0;
        SlotBorder.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
    }

    #endregion

    #region Context Menu Handlers

    private void LaunchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipSlotViewModel vm)
        {
            LaunchRequested?.Invoke(this, vm);
        }
    }

    private void StopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipSlotViewModel vm)
        {
            StopRequested?.Invoke(this, vm);
        }
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipSlotViewModel vm)
        {
            EditRequested?.Invoke(this, vm);
        }
    }

    private void CreateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipSlotViewModel vm)
        {
            CreateRequested?.Invoke(this, vm);
        }
    }

    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipSlotViewModel vm)
        {
            DuplicateRequested?.Invoke(this, vm);
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipSlotViewModel vm)
        {
            DeleteRequested?.Invoke(this, vm);
        }
    }

    private void SetColorRed_Click(object sender, RoutedEventArgs e) => SetColor(Color.FromRgb(0xFF, 0x55, 0x55));
    private void SetColorOrange_Click(object sender, RoutedEventArgs e) => SetColor(Color.FromRgb(0xFF, 0x95, 0x00));
    private void SetColorYellow_Click(object sender, RoutedEventArgs e) => SetColor(Color.FromRgb(0xFF, 0xFF, 0x55));
    private void SetColorGreen_Click(object sender, RoutedEventArgs e) => SetColor(Color.FromRgb(0x55, 0xFF, 0x55));
    private void SetColorCyan_Click(object sender, RoutedEventArgs e) => SetColor(Color.FromRgb(0x55, 0xFF, 0xFF));
    private void SetColorBlue_Click(object sender, RoutedEventArgs e) => SetColor(Color.FromRgb(0x55, 0x55, 0xFF));
    private void SetColorPurple_Click(object sender, RoutedEventArgs e) => SetColor(Color.FromRgb(0xAA, 0x55, 0xFF));
    private void SetColorMagenta_Click(object sender, RoutedEventArgs e) => SetColor(Color.FromRgb(0xFF, 0x55, 0xFF));

    private void SetColor(Color color)
    {
        if (DataContext is ClipSlotViewModel vm)
        {
            ColorChangeRequested?.Invoke(this, (vm, color));
        }
    }

    #endregion
}

