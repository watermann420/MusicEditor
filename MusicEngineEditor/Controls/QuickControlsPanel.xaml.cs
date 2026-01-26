// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a single quick control with assignable parameter mapping.
/// </summary>
public class QuickControl : INotifyPropertyChanged
{
    private string _name = "Unassigned";
    private string _parameterPath = string.Empty;
    private double _value = 0.5;
    private double _minValue = 0.0;
    private double _maxValue = 1.0;
    private double _defaultValue = 0.5;
    private int _midiCC = -1;
    private int _midiChannel = -1;
    private bool _isLearning;
    private Color _color = Colors.DodgerBlue;

    /// <summary>Gets or sets the control name.</summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    /// <summary>Gets or sets the parameter path (e.g., "Track1.Volume").</summary>
    public string ParameterPath
    {
        get => _parameterPath;
        set { _parameterPath = value; OnPropertyChanged(nameof(ParameterPath)); OnPropertyChanged(nameof(IsAssigned)); }
    }

    /// <summary>Gets or sets the current value (0.0 to 1.0 normalized).</summary>
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0.0, 1.0);
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(DisplayValue));
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the minimum value for display.</summary>
    public double MinValue
    {
        get => _minValue;
        set { _minValue = value; OnPropertyChanged(nameof(MinValue)); OnPropertyChanged(nameof(DisplayValue)); }
    }

    /// <summary>Gets or sets the maximum value for display.</summary>
    public double MaxValue
    {
        get => _maxValue;
        set { _maxValue = value; OnPropertyChanged(nameof(MaxValue)); OnPropertyChanged(nameof(DisplayValue)); }
    }

    /// <summary>Gets or sets the default value.</summary>
    public double DefaultValue
    {
        get => _defaultValue;
        set { _defaultValue = value; OnPropertyChanged(nameof(DefaultValue)); }
    }

    /// <summary>Gets or sets the assigned MIDI CC number (-1 if not assigned).</summary>
    public int MidiCC
    {
        get => _midiCC;
        set { _midiCC = value; OnPropertyChanged(nameof(MidiCC)); OnPropertyChanged(nameof(MidiInfo)); }
    }

    /// <summary>Gets or sets the MIDI channel (-1 for omni).</summary>
    public int MidiChannel
    {
        get => _midiChannel;
        set { _midiChannel = value; OnPropertyChanged(nameof(MidiChannel)); OnPropertyChanged(nameof(MidiInfo)); }
    }

    /// <summary>Gets or sets whether this control is in MIDI learn mode.</summary>
    public bool IsLearning
    {
        get => _isLearning;
        set { _isLearning = value; OnPropertyChanged(nameof(IsLearning)); }
    }

    /// <summary>Gets or sets the control color.</summary>
    public Color Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(nameof(Color)); }
    }

    /// <summary>Gets whether a parameter is assigned.</summary>
    public bool IsAssigned => !string.IsNullOrEmpty(ParameterPath);

    /// <summary>Gets the display value mapped to min/max range.</summary>
    public double DisplayValue => MinValue + (Value * (MaxValue - MinValue));

    /// <summary>Gets MIDI assignment info string.</summary>
    public string MidiInfo => MidiCC >= 0 ? $"CC{MidiCC}" : "No MIDI";

    /// <summary>Event raised when the value changes.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Event raised when a property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Resets the value to default.</summary>
    public void Reset() => Value = DefaultValue;

    /// <summary>Sets the current value as the default.</summary>
    public void SetAsDefault() => DefaultValue = Value;

    /// <summary>Clears the parameter assignment.</summary>
    public void ClearAssignment()
    {
        ParameterPath = string.Empty;
        Name = "Unassigned";
    }

    /// <summary>Clears MIDI assignment.</summary>
    public void ClearMidi()
    {
        MidiCC = -1;
        MidiChannel = -1;
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Event arguments for quick control selection events.
/// </summary>
public class QuickControlSelectedEventArgs : EventArgs
{
    /// <summary>Gets the selected control.</summary>
    public QuickControl Control { get; }

    /// <summary>Gets the control index.</summary>
    public int Index { get; }

    public QuickControlSelectedEventArgs(QuickControl control, int index)
    {
        Control = control;
        Index = index;
    }
}

/// <summary>
/// Panel displaying 8 assignable quick control knobs per track.
/// Supports parameter assignment, MIDI learn, and value persistence.
/// </summary>
public partial class QuickControlsPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ControlsProperty =
        DependencyProperty.Register(nameof(Controls), typeof(ObservableCollection<QuickControl>), typeof(QuickControlsPanel),
            new PropertyMetadata(null, OnControlsChanged));

    public static readonly DependencyProperty TrackIdProperty =
        DependencyProperty.Register(nameof(TrackId), typeof(int), typeof(QuickControlsPanel),
            new PropertyMetadata(-1, OnTrackIdChanged));

    public static readonly DependencyProperty IsMidiLearnActiveProperty =
        DependencyProperty.Register(nameof(IsMidiLearnActive), typeof(bool), typeof(QuickControlsPanel),
            new PropertyMetadata(false, OnIsMidiLearnActiveChanged));

    /// <summary>Gets or sets the collection of quick controls.</summary>
    public ObservableCollection<QuickControl> Controls
    {
        get => (ObservableCollection<QuickControl>)GetValue(ControlsProperty);
        set => SetValue(ControlsProperty, value);
    }

    /// <summary>Gets or sets the track ID for this panel.</summary>
    public int TrackId
    {
        get => (int)GetValue(TrackIdProperty);
        set => SetValue(TrackIdProperty, value);
    }

    /// <summary>Gets or sets whether MIDI learn mode is active.</summary>
    public bool IsMidiLearnActive
    {
        get => (bool)GetValue(IsMidiLearnActiveProperty);
        set => SetValue(IsMidiLearnActiveProperty, value);
    }

    #endregion

    #region Events

    /// <summary>Raised when a control value changes.</summary>
    public event EventHandler<QuickControlSelectedEventArgs>? ControlValueChanged;

    /// <summary>Raised when parameter assignment is requested.</summary>
    public event EventHandler<QuickControlSelectedEventArgs>? AssignParameterRequested;

    /// <summary>Raised when MIDI learn is requested for a control.</summary>
    public event EventHandler<QuickControlSelectedEventArgs>? MidiLearnRequested;

    /// <summary>Raised when all controls are reset.</summary>
    public event EventHandler? AllControlsReset;

    #endregion

    #region Private Fields

    private readonly List<QuickControlKnob> _knobControls = [];
    private int? _learningControlIndex;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public QuickControlsPanel()
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

        // Create default controls if none exist
        if (Controls == null)
        {
            Controls = new ObservableCollection<QuickControl>();
            for (int i = 0; i < 8; i++)
            {
                Controls.Add(new QuickControl { Name = $"QC {i + 1}" });
            }
        }

        BuildKnobControls();
        UpdateStatus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    #endregion

    #region Property Changed Handlers

    private static void OnControlsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuickControlsPanel panel && panel._isInitialized)
        {
            panel.BuildKnobControls();
            panel.UpdateStatus();
        }
    }

    private static void OnTrackIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuickControlsPanel panel && panel._isInitialized)
        {
            panel.UpdateStatus();
        }
    }

    private static void OnIsMidiLearnActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuickControlsPanel panel)
        {
            panel.MidiLearnToggle.IsChecked = (bool)e.NewValue;

            if (!(bool)e.NewValue)
            {
                // Clear learning state
                foreach (var control in panel.Controls ?? [])
                {
                    control.IsLearning = false;
                }
                panel._learningControlIndex = null;
            }
        }
    }

    #endregion

    #region Build UI

    private void BuildKnobControls()
    {
        ControlsContainer.Items.Clear();
        _knobControls.Clear();

        if (Controls == null) return;

        for (int i = 0; i < Controls.Count; i++)
        {
            var control = Controls[i];
            var knob = new QuickControlKnob
            {
                QuickControl = control,
                Margin = new Thickness(4)
            };

            int index = i;
            knob.ValueChanged += (s, e) => OnKnobValueChanged(index);
            knob.ContextMenu = (System.Windows.Controls.ContextMenu)Resources["KnobContextMenu"];
            knob.MouseRightButtonDown += (s, e) => OnKnobRightClick(index);

            _knobControls.Add(knob);
            ControlsContainer.Items.Add(knob);
        }
    }

    private void UpdateStatus()
    {
        if (Controls == null)
        {
            StatusText.Text = "";
            return;
        }

        int assignedCount = 0;
        int midiCount = 0;

        foreach (var control in Controls)
        {
            if (control.IsAssigned) assignedCount++;
            if (control.MidiCC >= 0) midiCount++;
        }

        if (assignedCount > 0 || midiCount > 0)
        {
            StatusText.Text = $"{assignedCount} assigned, {midiCount} MIDI";
        }
        else
        {
            StatusText.Text = "Right-click to assign";
        }
    }

    #endregion

    #region Event Handlers

    private int _currentContextIndex = -1;

    private void OnKnobRightClick(int index)
    {
        _currentContextIndex = index;
    }

    private void OnKnobValueChanged(int index)
    {
        if (Controls == null || index < 0 || index >= Controls.Count) return;
        ControlValueChanged?.Invoke(this, new QuickControlSelectedEventArgs(Controls[index], index));
    }

    private void AssignParameter_Click(object sender, RoutedEventArgs e)
    {
        if (_currentContextIndex >= 0 && _currentContextIndex < (Controls?.Count ?? 0))
        {
            AssignParameterRequested?.Invoke(this, new QuickControlSelectedEventArgs(Controls![_currentContextIndex], _currentContextIndex));
        }
    }

    private void ClearAssignment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentContextIndex >= 0 && _currentContextIndex < (Controls?.Count ?? 0))
        {
            Controls![_currentContextIndex].ClearAssignment();
            UpdateStatus();
        }
    }

    private void MidiLearn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentContextIndex >= 0 && _currentContextIndex < (Controls?.Count ?? 0))
        {
            StartMidiLearn(_currentContextIndex);
        }
    }

    private void ClearMidi_Click(object sender, RoutedEventArgs e)
    {
        if (_currentContextIndex >= 0 && _currentContextIndex < (Controls?.Count ?? 0))
        {
            Controls![_currentContextIndex].ClearMidi();
            UpdateStatus();
        }
    }

    private void ResetValue_Click(object sender, RoutedEventArgs e)
    {
        if (_currentContextIndex >= 0 && _currentContextIndex < (Controls?.Count ?? 0))
        {
            Controls![_currentContextIndex].Reset();
        }
    }

    private void SetDefault_Click(object sender, RoutedEventArgs e)
    {
        if (_currentContextIndex >= 0 && _currentContextIndex < (Controls?.Count ?? 0))
        {
            Controls![_currentContextIndex].SetAsDefault();
        }
    }

    private void TrackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Track selection changed - load controls for selected track
        // This would typically reload Controls from a service
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        if (Controls == null) return;

        foreach (var control in Controls)
        {
            control.Reset();
        }

        AllControlsReset?.Invoke(this, EventArgs.Empty);
    }

    private void MidiLearn_Checked(object sender, RoutedEventArgs e)
    {
        IsMidiLearnActive = true;
    }

    private void MidiLearn_Unchecked(object sender, RoutedEventArgs e)
    {
        IsMidiLearnActive = false;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts MIDI learn mode for a specific control.
    /// </summary>
    /// <param name="index">Control index.</param>
    public void StartMidiLearn(int index)
    {
        if (Controls == null || index < 0 || index >= Controls.Count) return;

        // Clear previous learning state
        if (_learningControlIndex.HasValue && _learningControlIndex.Value < Controls.Count)
        {
            Controls[_learningControlIndex.Value].IsLearning = false;
        }

        // Set new learning state
        _learningControlIndex = index;
        Controls[index].IsLearning = true;
        IsMidiLearnActive = true;

        MidiLearnRequested?.Invoke(this, new QuickControlSelectedEventArgs(Controls[index], index));
    }

    /// <summary>
    /// Processes a MIDI CC message.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="cc">CC number (0-127).</param>
    /// <param name="value">CC value (0-127).</param>
    /// <returns>True if message was handled.</returns>
    public bool ProcessMidiCC(int channel, int cc, int value)
    {
        if (Controls == null) return false;

        // If in learning mode, assign CC to learning control
        if (_learningControlIndex.HasValue)
        {
            var control = Controls[_learningControlIndex.Value];
            control.MidiCC = cc;
            control.MidiChannel = channel;
            control.IsLearning = false;
            _learningControlIndex = null;
            IsMidiLearnActive = false;
            UpdateStatus();
            return true;
        }

        // Otherwise, find matching control and update value
        bool handled = false;
        foreach (var control in Controls)
        {
            if (control.MidiCC == cc && (control.MidiChannel == -1 || control.MidiChannel == channel))
            {
                control.Value = value / 127.0;
                handled = true;
            }
        }

        return handled;
    }

    /// <summary>
    /// Assigns a parameter to a control.
    /// </summary>
    /// <param name="index">Control index.</param>
    /// <param name="name">Display name.</param>
    /// <param name="parameterPath">Parameter path.</param>
    /// <param name="minValue">Minimum value.</param>
    /// <param name="maxValue">Maximum value.</param>
    /// <param name="defaultValue">Default value (normalized).</param>
    public void AssignParameter(int index, string name, string parameterPath, double minValue = 0, double maxValue = 1, double defaultValue = 0.5)
    {
        if (Controls == null || index < 0 || index >= Controls.Count) return;

        var control = Controls[index];
        control.Name = name;
        control.ParameterPath = parameterPath;
        control.MinValue = minValue;
        control.MaxValue = maxValue;
        control.DefaultValue = defaultValue;
        control.Value = defaultValue;

        UpdateStatus();
    }

    /// <summary>
    /// Refreshes all knob displays.
    /// </summary>
    public void RefreshAll()
    {
        foreach (var knob in _knobControls)
        {
            knob.Refresh();
        }
        UpdateStatus();
    }

    #endregion
}

/// <summary>
/// A single quick control knob with visual rendering.
/// </summary>
public class QuickControlKnob : System.Windows.Controls.Control
{
    private QuickControl? _quickControl;
    private bool _isDragging;
    private Point _dragStart;
    private double _dragStartValue;

    /// <summary>Gets or sets the quick control this knob represents.</summary>
    public QuickControl? QuickControl
    {
        get => _quickControl;
        set
        {
            if (_quickControl != null)
            {
                _quickControl.PropertyChanged -= OnControlPropertyChanged;
            }
            _quickControl = value;
            if (_quickControl != null)
            {
                _quickControl.PropertyChanged += OnControlPropertyChanged;
            }
            InvalidateVisual();
        }
    }

    /// <summary>Event raised when the value changes due to user interaction.</summary>
    public event EventHandler? ValueChanged;

    public QuickControlKnob()
    {
        Width = 70;
        Height = 90;
        Cursor = Cursors.Hand;
    }

    private void OnControlPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (QuickControl == null) return;

        _isDragging = true;
        _dragStart = e.GetPosition(this);
        _dragStartValue = QuickControl.Value;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDragging || QuickControl == null) return;

        var current = e.GetPosition(this);
        double delta = (_dragStart.Y - current.Y) / 100.0;
        QuickControl.Value = Math.Clamp(_dragStartValue + delta, 0.0, 1.0);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (QuickControl == null) return;

        double delta = e.Delta > 0 ? 0.01 : -0.01;
        QuickControl.Value = Math.Clamp(QuickControl.Value + delta, 0.0, 1.0);
        ValueChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        double knobSize = Math.Min(w, h - 30) * 0.7;
        double cx = w / 2;
        double cy = 10 + knobSize / 2;

        // Background
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        // Knob track (arc background)
        var trackBrush = new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A));
        var trackPen = new Pen(trackBrush, 4);
        double radius = knobSize / 2 - 4;
        double startAngle = 135;
        double endAngle = 405;

        DrawArc(dc, cx, cy, radius, startAngle, endAngle, trackPen);

        if (QuickControl != null)
        {
            // Knob value arc
            var valueBrush = new SolidColorBrush(QuickControl.Color);
            var valuePen = new Pen(valueBrush, 4);
            double valueAngle = startAngle + (QuickControl.Value * (endAngle - startAngle));
            DrawArc(dc, cx, cy, radius, startAngle, valueAngle, valuePen);

            // Knob circle
            var knobBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41));
            var knobPen = new Pen(new SolidColorBrush(Color.FromRgb(0x50, 0x52, 0x56)), 1);
            dc.DrawEllipse(knobBrush, knobPen, new Point(cx, cy), radius - 6, radius - 6);

            // Knob indicator line
            double indicatorAngle = (startAngle + QuickControl.Value * (endAngle - startAngle)) * Math.PI / 180;
            double x1 = cx + (radius - 12) * Math.Cos(indicatorAngle);
            double y1 = cy + (radius - 12) * Math.Sin(indicatorAngle);
            double x2 = cx + (radius - 20) * Math.Cos(indicatorAngle);
            double y2 = cy + (radius - 20) * Math.Sin(indicatorAngle);
            dc.DrawLine(new Pen(valueBrush, 2), new Point(x1, y1), new Point(x2, y2));

            // Name text
            var nameText = new FormattedText(
                QuickControl.Name,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(nameText, new Point(cx - nameText.Width / 2, cy + radius + 6));

            // Value text
            var valueText = new FormattedText(
                QuickControl.DisplayValue.ToString("F2"),
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9,
                new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(valueText, new Point(cx - valueText.Width / 2, cy + radius + 20));

            // Learning indicator
            if (QuickControl.IsLearning)
            {
                var learningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00));
                dc.DrawEllipse(learningBrush, null, new Point(cx + radius - 5, cy - radius + 5), 4, 4);
            }
        }
    }

    private void DrawArc(DrawingContext dc, double cx, double cy, double radius, double startAngle, double endAngle, Pen pen)
    {
        if (Math.Abs(endAngle - startAngle) < 0.1) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double start = startAngle * Math.PI / 180;
            double end = endAngle * Math.PI / 180;

            ctx.BeginFigure(
                new Point(cx + radius * Math.Cos(start), cy + radius * Math.Sin(start)),
                false, false);

            ctx.ArcTo(
                new Point(cx + radius * Math.Cos(end), cy + radius * Math.Sin(end)),
                new Size(radius, radius),
                0,
                endAngle - startAngle > 180,
                SweepDirection.Clockwise,
                true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    /// <summary>Refreshes the visual display.</summary>
    public void Refresh() => InvalidateVisual();
}
