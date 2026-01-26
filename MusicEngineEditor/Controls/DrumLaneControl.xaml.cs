// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Event args for drum step events.
/// </summary>
public class DrumStepEventArgs : EventArgs
{
    public int StepIndex { get; }
    public int Velocity { get; }
    public bool IsActive { get; }

    public DrumStepEventArgs(int stepIndex, bool isActive, int velocity)
    {
        StepIndex = stepIndex;
        IsActive = isActive;
        Velocity = velocity;
    }
}

/// <summary>
/// Control for a single drum lane (row) in the drum editor.
/// </summary>
public partial class DrumLaneControl : UserControl
{
    private readonly Dictionary<int, Rectangle> _stepElements = new();
    private bool _isDragging;
    private bool _dragSetActive;

    /// <summary>
    /// Gets or sets the drum lane data.
    /// </summary>
    public DrumLane? Lane
    {
        get => (DrumLane?)GetValue(LaneProperty);
        set => SetValue(LaneProperty, value);
    }

    public static readonly DependencyProperty LaneProperty =
        DependencyProperty.Register(nameof(Lane), typeof(DrumLane), typeof(DrumLaneControl),
            new PropertyMetadata(null, OnLaneChanged));

    /// <summary>
    /// Gets the collection of steps.
    /// </summary>
    public ObservableCollection<DrumStep> Steps { get; } = new();

    /// <summary>
    /// Gets or sets the number of steps.
    /// </summary>
    public int StepCount
    {
        get => (int)GetValue(StepCountProperty);
        set => SetValue(StepCountProperty, value);
    }

    public static readonly DependencyProperty StepCountProperty =
        DependencyProperty.Register(nameof(StepCount), typeof(int), typeof(DrumLaneControl),
            new PropertyMetadata(16, OnStepCountChanged));

    /// <summary>
    /// Gets or sets the current playback step (for highlighting).
    /// </summary>
    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(nameof(CurrentStep), typeof(int), typeof(DrumLaneControl),
            new PropertyMetadata(-1, OnCurrentStepChanged));

    /// <summary>
    /// Event raised when a step is toggled.
    /// </summary>
    public event EventHandler<DrumStepEventArgs>? StepToggled;

    /// <summary>
    /// Event raised when a step velocity is changed.
    /// </summary>
    public event EventHandler<DrumStepEventArgs>? VelocityChanged;

    /// <summary>
    /// Event raised when mute state changes.
    /// </summary>
    public event EventHandler<bool>? MuteChanged;

    /// <summary>
    /// Event raised when solo state changes.
    /// </summary>
    public event EventHandler<bool>? SoloChanged;

    public DrumLaneControl()
    {
        InitializeComponent();

        Steps.CollectionChanged += (_, _) => RefreshSteps();
        SizeChanged += (_, _) => RefreshSteps();
        Loaded += (_, _) =>
        {
            InitializeSteps();
            RefreshSteps();
        };
    }

    private static void OnLaneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DrumLaneControl control)
        {
            control.UpdateLaneDisplay();
        }
    }

    private static void OnStepCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DrumLaneControl control)
        {
            control.InitializeSteps();
            control.RefreshSteps();
        }
    }

    private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DrumLaneControl control)
        {
            control.HighlightCurrentStep((int)e.OldValue, (int)e.NewValue);
        }
    }

    private void UpdateLaneDisplay()
    {
        if (Lane == null) return;

        DrumNameText.Text = Lane.Name;
        ColorIndicator.Background = new SolidColorBrush(ParseColor(Lane.Color));
        MuteButton.IsChecked = Lane.IsMuted;
        SoloButton.IsChecked = Lane.IsSolo;
    }

    private void InitializeSteps()
    {
        Steps.Clear();
        for (int i = 0; i < StepCount; i++)
        {
            Steps.Add(new DrumStep(i));
        }
    }

    /// <summary>
    /// Refreshes the visual display of all steps.
    /// </summary>
    public void RefreshSteps()
    {
        // Clear existing step elements
        foreach (var element in _stepElements.Values)
        {
            StepsCanvas.Children.Remove(element);
        }
        _stepElements.Clear();

        if (StepsCanvas.ActualWidth <= 0 || StepsCanvas.ActualHeight <= 0 || StepCount == 0)
            return;

        double stepWidth = StepsCanvas.ActualWidth / StepCount;
        double stepHeight = StepsCanvas.ActualHeight - 4;
        double padding = 2;

        for (int i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            var rect = CreateStepElement(step, stepWidth - padding * 2, stepHeight);
            _stepElements[i] = rect;
            StepsCanvas.Children.Add(rect);

            Canvas.SetLeft(rect, i * stepWidth + padding);
            Canvas.SetTop(rect, 2);
        }
    }

    private Rectangle CreateStepElement(DrumStep step, double width, double height)
    {
        var baseColor = Lane != null ? ParseColor(Lane.Color) : Color.FromRgb(0x4B, 0x6E, 0xAF);

        // Determine background based on step position (beat grouping)
        var isDownbeat = step.StepIndex % 4 == 0;
        var backgroundColor = isDownbeat
            ? Color.FromRgb(0x2B, 0x2D, 0x30)
            : Color.FromRgb(0x25, 0x26, 0x28);

        Color fillColor;
        if (step.IsActive)
        {
            // Velocity affects opacity
            byte alpha = (byte)(Math.Max(80, step.NormalizedVelocity * 255));
            fillColor = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        }
        else
        {
            fillColor = backgroundColor;
        }

        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(fillColor),
            Stroke = step.IsPlaying
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(0x39, 0x3B, 0x40)),
            StrokeThickness = step.IsPlaying ? 2 : 1,
            RadiusX = 3,
            RadiusY = 3,
            Tag = step,
            Cursor = Cursors.Hand
        };

        return rect;
    }

    private void HighlightCurrentStep(int oldStep, int newStep)
    {
        // Remove highlight from old step
        if (oldStep >= 0 && oldStep < Steps.Count)
        {
            Steps[oldStep].IsPlaying = false;
            if (_stepElements.TryGetValue(oldStep, out var oldRect))
            {
                oldRect.Stroke = new SolidColorBrush(Color.FromRgb(0x39, 0x3B, 0x40));
                oldRect.StrokeThickness = 1;
            }
        }

        // Add highlight to new step
        if (newStep >= 0 && newStep < Steps.Count)
        {
            Steps[newStep].IsPlaying = true;
            if (_stepElements.TryGetValue(newStep, out var newRect))
            {
                newRect.Stroke = new SolidColorBrush(Colors.White);
                newRect.StrokeThickness = 2;
            }
        }
    }

    private int GetStepAtPosition(double x)
    {
        if (StepsCanvas.ActualWidth <= 0 || StepCount == 0)
            return -1;

        double stepWidth = StepsCanvas.ActualWidth / StepCount;
        int step = (int)(x / stepWidth);
        return Math.Clamp(step, 0, StepCount - 1);
    }

    private void ToggleStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= Steps.Count)
            return;

        var step = Steps[stepIndex];
        step.IsActive = !step.IsActive;

        // Update visual
        UpdateStepVisual(stepIndex);

        StepToggled?.Invoke(this, new DrumStepEventArgs(stepIndex, step.IsActive, step.Velocity));
    }

    private void SetStepActive(int stepIndex, bool active)
    {
        if (stepIndex < 0 || stepIndex >= Steps.Count)
            return;

        var step = Steps[stepIndex];
        if (step.IsActive != active)
        {
            step.IsActive = active;
            UpdateStepVisual(stepIndex);
            StepToggled?.Invoke(this, new DrumStepEventArgs(stepIndex, step.IsActive, step.Velocity));
        }
    }

    private void UpdateStepVisual(int stepIndex)
    {
        if (!_stepElements.TryGetValue(stepIndex, out var rect))
            return;

        var step = Steps[stepIndex];
        var baseColor = Lane != null ? ParseColor(Lane.Color) : Color.FromRgb(0x4B, 0x6E, 0xAF);
        var isDownbeat = stepIndex % 4 == 0;
        var backgroundColor = isDownbeat
            ? Color.FromRgb(0x2B, 0x2D, 0x30)
            : Color.FromRgb(0x25, 0x26, 0x28);

        if (step.IsActive)
        {
            byte alpha = (byte)(Math.Max(80, step.NormalizedVelocity * 255));
            rect.Fill = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        }
        else
        {
            rect.Fill = new SolidColorBrush(backgroundColor);
        }
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(0x4B, 0x6E, 0xAF);
        }
    }

    #region Event Handlers

    private void StepsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var stepIndex = GetStepAtPosition(e.GetPosition(StepsCanvas).X);
        if (stepIndex >= 0)
        {
            ToggleStep(stepIndex);
            _isDragging = true;
            _dragSetActive = Steps[stepIndex].IsActive;
            StepsCanvas.CaptureMouse();
        }
    }

    private void StepsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        StepsCanvas.ReleaseMouseCapture();
    }

    private void StepsCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var stepIndex = GetStepAtPosition(e.GetPosition(StepsCanvas).X);
            if (stepIndex >= 0)
            {
                SetStepActive(stepIndex, _dragSetActive);
            }
        }
    }

    private void StepsCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var stepIndex = GetStepAtPosition(e.GetPosition(StepsCanvas).X);
        if (stepIndex >= 0 && Steps[stepIndex].IsActive)
        {
            // Show velocity popup
            ShowVelocityPopup(stepIndex, e.GetPosition(this));
        }
    }

    private void ShowVelocityPopup(int stepIndex, Point position)
    {
        var step = Steps[stepIndex];

        // Create simple velocity popup
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            StaysOpen = false,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
        };

        var slider = new Slider
        {
            Minimum = 1,
            Maximum = 127,
            Value = step.Velocity,
            Width = 100,
            Orientation = Orientation.Horizontal
        };

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x39, 0x3B, 0x40)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8)
        };

        var stack = new StackPanel();
        var label = new TextBlock
        {
            Text = $"Velocity: {step.Velocity}",
            Foreground = Brushes.White,
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 4)
        };

        slider.ValueChanged += (_, args) =>
        {
            step.Velocity = (int)args.NewValue;
            label.Text = $"Velocity: {step.Velocity}";
            UpdateStepVisual(stepIndex);
            VelocityChanged?.Invoke(this, new DrumStepEventArgs(stepIndex, step.IsActive, step.Velocity));
        };

        stack.Children.Add(label);
        stack.Children.Add(slider);
        border.Child = stack;
        popup.Child = border;
        popup.IsOpen = true;
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Lane != null)
        {
            Lane.IsMuted = MuteButton.IsChecked == true;
            MuteChanged?.Invoke(this, Lane.IsMuted);
        }
    }

    private void SoloButton_Click(object sender, RoutedEventArgs e)
    {
        if (Lane != null)
        {
            Lane.IsSolo = SoloButton.IsChecked == true;
            SoloChanged?.Invoke(this, Lane.IsSolo);
        }
    }

    #endregion

    /// <summary>
    /// Gets the pattern as an array of active step indices.
    /// </summary>
    public int[] GetActiveSteps()
    {
        return Steps.Where(s => s.IsActive).Select(s => s.StepIndex).ToArray();
    }

    /// <summary>
    /// Sets steps as active from an array of indices.
    /// </summary>
    public void SetActiveSteps(int[] activeIndices)
    {
        foreach (var step in Steps)
        {
            step.IsActive = activeIndices.Contains(step.StepIndex);
        }
        RefreshSteps();
    }

    /// <summary>
    /// Clears all steps.
    /// </summary>
    public void ClearAllSteps()
    {
        foreach (var step in Steps)
        {
            step.IsActive = false;
        }
        RefreshSteps();
    }
}
