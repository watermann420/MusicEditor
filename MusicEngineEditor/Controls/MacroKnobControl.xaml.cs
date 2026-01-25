//MusicEngineEditor - Macro Knob Control
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A rotary knob control for macro parameters.
/// Supports mouse drag, mouse wheel, and displays value arc.
/// </summary>
public partial class MacroKnobControl : UserControl
{
    #region Constants

    private const double MinAngle = -135.0;
    private const double MaxAngle = 135.0;
    private const double AngleRange = MaxAngle - MinAngle;
    private const double ArcRadius = 22.0;
    private const double ArcCenterX = 25.0;
    private const double ArcCenterY = 25.0;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty MacroNameProperty =
        DependencyProperty.Register(nameof(MacroName), typeof(string), typeof(MacroKnobControl),
            new PropertyMetadata("Macro", OnMacroNameChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(float), typeof(MacroKnobControl),
            new PropertyMetadata(0.5f, OnValueChanged));

    public static readonly DependencyProperty MacroColorProperty =
        DependencyProperty.Register(nameof(MacroColor), typeof(Color), typeof(MacroKnobControl),
            new PropertyMetadata(Color.FromRgb(0x55, 0xAA, 0xFF), OnColorChanged));

    public static readonly DependencyProperty MacroControlProperty =
        DependencyProperty.Register(nameof(MacroControl), typeof(MacroControl), typeof(MacroKnobControl),
            new PropertyMetadata(null, OnMacroControlChanged));

    public static readonly DependencyProperty IsLearningProperty =
        DependencyProperty.Register(nameof(IsLearning), typeof(bool), typeof(MacroKnobControl),
            new PropertyMetadata(false, OnIsLearningChanged));

    public static readonly DependencyProperty ShowValueProperty =
        DependencyProperty.Register(nameof(ShowValue), typeof(bool), typeof(MacroKnobControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowMidiCCProperty =
        DependencyProperty.Register(nameof(ShowMidiCC), typeof(bool), typeof(MacroKnobControl),
            new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets the macro name.
    /// </summary>
    public string MacroName
    {
        get => (string)GetValue(MacroNameProperty);
        set => SetValue(MacroNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the macro value (0-1).
    /// </summary>
    public float Value
    {
        get => (float)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the macro color.
    /// </summary>
    public Color MacroColor
    {
        get => (Color)GetValue(MacroColorProperty);
        set => SetValue(MacroColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the bound MacroControl.
    /// </summary>
    public MacroControl? MacroControl
    {
        get => (MacroControl?)GetValue(MacroControlProperty);
        set => SetValue(MacroControlProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the macro is in MIDI learn mode.
    /// </summary>
    public bool IsLearning
    {
        get => (bool)GetValue(IsLearningProperty);
        set => SetValue(IsLearningProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the value display.
    /// </summary>
    public bool ShowValue
    {
        get => (bool)GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the MIDI CC assignment.
    /// </summary>
    public bool ShowMidiCC
    {
        get => (bool)GetValue(ShowMidiCCProperty);
        set => SetValue(ShowMidiCCProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when the value changes.
    /// </summary>
    public event EventHandler<MacroValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Fired when the user requests to add a mapping.
    /// </summary>
    public event EventHandler? AddMappingRequested;

    /// <summary>
    /// Fired when the user requests to edit mappings.
    /// </summary>
    public event EventHandler? EditMappingsRequested;

    /// <summary>
    /// Fired when MIDI learn is requested.
    /// </summary>
    public event EventHandler? MidiLearnRequested;

    #endregion

    #region Private Fields

    private bool _isDragging;
    private Point _dragStartPoint;
    private float _dragStartValue;
    private bool _isInitialized;
    private DispatcherTimer? _learningAnimationTimer;
    private bool _learningAnimationState;

    #endregion

    #region Constructor

    public MacroKnobControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        UpdateVisuals();
        UpdateMappingBadge();
        UpdateMidiDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopLearningAnimation();
        _isInitialized = false;
    }

    #endregion

    #region Property Changed Handlers

    private static void OnMacroNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Name is bound directly in XAML
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MacroKnobControl control && control._isInitialized)
        {
            control.UpdateVisuals();

            // Sync with MacroControl if bound
            if (control.MacroControl != null && Math.Abs(control.MacroControl.Value - control.Value) > 0.001f)
            {
                control.MacroControl.Value = control.Value;
            }

            control.ValueChanged?.Invoke(control,
                new MacroValueChangedEventArgs(control.MacroControl!, (float)(e.OldValue ?? 0f), (float)(e.NewValue ?? 0f)));
        }
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MacroKnobControl control && control._isInitialized)
        {
            control.UpdateVisuals();
        }
    }

    private static void OnMacroControlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MacroKnobControl control)
        {
            // Unsubscribe from old
            if (e.OldValue is MacroControl oldMacro)
            {
                oldMacro.ValueChanged -= control.OnMacroValueChanged;
                oldMacro.MappingAdded -= control.OnMappingChanged;
                oldMacro.MappingRemoved -= control.OnMappingChanged;
            }

            // Subscribe to new
            if (e.NewValue is MacroControl newMacro)
            {
                newMacro.ValueChanged += control.OnMacroValueChanged;
                newMacro.MappingAdded += control.OnMappingChanged;
                newMacro.MappingRemoved += control.OnMappingChanged;

                // Sync properties
                control.MacroName = newMacro.Name;
                control.Value = newMacro.Value;
                control.IsLearning = newMacro.IsLearning;

                if (ColorConverter.ConvertFromString(newMacro.Color) is Color color)
                {
                    control.MacroColor = color;
                }

                control.UpdateMappingBadge();
                control.UpdateMidiDisplay();
            }
        }
    }

    private static void OnIsLearningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MacroKnobControl control)
        {
            if ((bool)e.NewValue)
            {
                control.StartLearningAnimation();
            }
            else
            {
                control.StopLearningAnimation();
            }

            if (control.MacroControl != null)
            {
                control.MacroControl.IsLearning = (bool)e.NewValue;
            }
        }
    }

    private void OnMacroValueChanged(object? sender, MacroValueChangedEventArgs e)
    {
        // Sync value from MacroControl
        Dispatcher.Invoke(() =>
        {
            if (Math.Abs(Value - e.NewValue) > 0.001f)
            {
                Value = e.NewValue;
            }
        });
    }

    private void OnMappingChanged(object? sender, MacroMappingEventArgs e)
    {
        Dispatcher.Invoke(UpdateMappingBadge);
    }

    #endregion

    #region Visual Updates

    private void UpdateVisuals()
    {
        if (!_isInitialized) return;

        // Update value arc
        UpdateValueArc();

        // Update knob indicator rotation
        UpdateKnobIndicator();

        // Update value text
        UpdateValueText();
    }

    private void UpdateValueArc()
    {
        var brush = new SolidColorBrush(MacroColor);

        // Track arc (full range from MinAngle to MaxAngle)
        TrackArc.Data = CreateArcGeometry(MinAngle, MaxAngle);

        // Value arc (from MinAngle to current value angle)
        double valueAngle = MinAngle + (Value * AngleRange);
        ValueArc.Data = CreateArcGeometry(MinAngle, valueAngle);
        ValueArc.Stroke = brush;

        // Update indicator color
        KnobIndicator.Stroke = brush;
    }

    private Geometry CreateArcGeometry(double startAngle, double endAngle)
    {
        if (Math.Abs(endAngle - startAngle) < 0.1)
        {
            return Geometry.Empty;
        }

        // Convert to radians and adjust for coordinate system
        double startRad = (startAngle - 90) * Math.PI / 180.0;
        double endRad = (endAngle - 90) * Math.PI / 180.0;

        // Calculate start and end points
        double startX = ArcCenterX + ArcRadius * Math.Cos(startRad);
        double startY = ArcCenterY + ArcRadius * Math.Sin(startRad);
        double endX = ArcCenterX + ArcRadius * Math.Cos(endRad);
        double endY = ArcCenterY + ArcRadius * Math.Sin(endRad);

        // Determine if we need the large arc
        bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;

        var pathFigure = new PathFigure
        {
            StartPoint = new Point(startX, startY),
            IsClosed = false
        };

        var arcSegment = new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(ArcRadius, ArcRadius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise
        };

        pathFigure.Segments.Add(arcSegment);

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        return pathGeometry;
    }

    private void UpdateKnobIndicator()
    {
        double angle = MinAngle + (Value * AngleRange);
        double rad = (angle - 90) * Math.PI / 180.0;

        double indicatorLength = 12;
        double endX = ArcCenterX + indicatorLength * Math.Cos(rad);
        double endY = ArcCenterY + indicatorLength * Math.Sin(rad);

        KnobIndicator.X1 = ArcCenterX;
        KnobIndicator.Y1 = ArcCenterY;
        KnobIndicator.X2 = endX;
        KnobIndicator.Y2 = endY;
    }

    private void UpdateValueText()
    {
        if (ShowValue)
        {
            ValueText.Text = $"{Value * 100:F0}%";
            ValueText.Visibility = Visibility.Visible;
        }
        else
        {
            ValueText.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateMappingBadge()
    {
        if (MacroControl == null)
        {
            MappingBadge.Visibility = Visibility.Collapsed;
            return;
        }

        int count = MacroControl.MappingCount;
        if (count > 0)
        {
            MappingCountText.Text = count.ToString();
            MappingBadge.Visibility = Visibility.Visible;
        }
        else
        {
            MappingBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateMidiDisplay()
    {
        if (!ShowMidiCC || MacroControl?.MidiCC == null)
        {
            MidiCCText.Visibility = Visibility.Collapsed;
            return;
        }

        MidiCCText.Text = $"CC{MacroControl.MidiCC}";
        MidiCCText.Visibility = Visibility.Visible;
    }

    #endregion

    #region Learning Animation

    private void StartLearningAnimation()
    {
        LearningIndicator.Visibility = Visibility.Visible;

        _learningAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _learningAnimationTimer.Tick += (s, e) =>
        {
            _learningAnimationState = !_learningAnimationState;
            LearningIndicator.Opacity = _learningAnimationState ? 1.0 : 0.3;
        };
        _learningAnimationTimer.Start();
    }

    private void StopLearningAnimation()
    {
        _learningAnimationTimer?.Stop();
        _learningAnimationTimer = null;
        LearningIndicator.Visibility = Visibility.Collapsed;
        UpdateMidiDisplay();
    }

    #endregion

    #region Mouse Handling

    private void Knob_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        _dragStartValue = Value;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void Knob_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Knob_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        Point currentPoint = e.GetPosition(this);

        // Calculate vertical delta (up = increase, down = decrease)
        double deltaY = _dragStartPoint.Y - currentPoint.Y;

        // Sensitivity based on modifier keys
        double sensitivity = 0.005;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            sensitivity = 0.001; // Fine control
        }
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            sensitivity = 0.02; // Coarse control
        }

        float newValue = _dragStartValue + (float)(deltaY * sensitivity);
        Value = Math.Clamp(newValue, 0f, 1f);

        e.Handled = true;
    }

    private void Knob_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Sensitivity based on modifier keys
        float delta = 0.05f;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            delta = 0.01f; // Fine control
        }
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            delta = 0.1f; // Coarse control
        }

        if (e.Delta > 0)
        {
            Value = Math.Min(1f, Value + delta);
        }
        else
        {
            Value = Math.Max(0f, Value - delta);
        }

        e.Handled = true;
    }

    #endregion

    #region Context Menu Handlers

    private void AddMapping_Click(object sender, RoutedEventArgs e)
    {
        AddMappingRequested?.Invoke(this, EventArgs.Empty);
    }

    private void EditMappings_Click(object sender, RoutedEventArgs e)
    {
        EditMappingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MidiLearn_Click(object sender, RoutedEventArgs e)
    {
        IsLearning = !IsLearning;
        MidiLearnRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearMidi_Click(object sender, RoutedEventArgs e)
    {
        if (MacroControl != null)
        {
            MacroControl.MidiCC = null;
            MacroControl.MidiChannel = null;
        }
        IsLearning = false;
        UpdateMidiDisplay();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        MacroControl?.Reset();
        if (MacroControl != null)
        {
            Value = MacroControl.Value;
        }
        else
        {
            Value = 0.5f;
        }
    }

    private void SetDefault_Click(object sender, RoutedEventArgs e)
    {
        if (MacroControl != null)
        {
            MacroControl.DefaultValue = Value;
        }
    }

    private void ClearMappings_Click(object sender, RoutedEventArgs e)
    {
        MacroControl?.ClearMappings();
        UpdateMappingBadge();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the visual state of the control.
    /// </summary>
    public void Refresh()
    {
        UpdateVisuals();
        UpdateMappingBadge();
        UpdateMidiDisplay();
    }

    #endregion
}
