// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Auto-Tune pitch correction effect editor control.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Auto-Tune pitch correction editor control with real-time pitch display,
/// correction graph, and comprehensive parameter controls.
/// </summary>
public partial class AutoTuneControl : UserControl
{
    #region Constants

    private const double KnobMinAngle = -135.0;
    private const double KnobMaxAngle = 135.0;
    private const double KnobAngleRange = KnobMaxAngle - KnobMinAngle;
    private const double KnobRadius = 26.0;
    private const double KnobCenterX = 30.0;
    private const double KnobCenterY = 30.0;

    private const int GraphHistoryLength = 200;
    private const double MinPitchHz = 80.0;  // ~E2
    private const double MaxPitchHz = 1200.0; // ~D6

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private bool _isDraggingKnob;
    private Point _knobDragStartPoint;
    private double _knobDragStartValue;

    private readonly List<double> _inputPitchHistory = new();
    private readonly List<double> _outputPitchHistory = new();
    private Shapes.Polyline? _inputPitchLine;
    private Shapes.Polyline? _outputPitchLine;

    private DispatcherTimer? _updateTimer;
    private bool _isBypassed;

    private double _retuneSpeedMs = 50.0;
    private readonly bool[] _bypassedNotes = new bool[12];

    #endregion

    #region Events

    /// <summary>
    /// Raised when a parameter value changes.
    /// </summary>
    public event EventHandler<AutoTuneParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Raised when the bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Raised when the key/scale settings change.
    /// </summary>
    public event EventHandler<AutoTuneKeyScaleChangedEventArgs>? KeyScaleChanged;

    /// <summary>
    /// Raised when note bypass settings change.
    /// </summary>
    public event EventHandler<bool[]>? NoteBypassChanged;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty InputPitchProperty =
        DependencyProperty.Register(nameof(InputPitch), typeof(double), typeof(AutoTuneControl),
            new PropertyMetadata(0.0, OnInputPitchChanged));

    public static readonly DependencyProperty OutputPitchProperty =
        DependencyProperty.Register(nameof(OutputPitch), typeof(double), typeof(AutoTuneControl),
            new PropertyMetadata(0.0, OnOutputPitchChanged));

    public static readonly DependencyProperty CorrectionSpeedProperty =
        DependencyProperty.Register(nameof(CorrectionSpeed), typeof(double), typeof(AutoTuneControl),
            new PropertyMetadata(100.0, OnCorrectionSpeedChanged));

    public static readonly DependencyProperty HumanizeAmountProperty =
        DependencyProperty.Register(nameof(HumanizeAmount), typeof(double), typeof(AutoTuneControl),
            new PropertyMetadata(0.0, OnHumanizeAmountChanged));

    public static readonly DependencyProperty FormantPreservationProperty =
        DependencyProperty.Register(nameof(FormantPreservation), typeof(bool), typeof(AutoTuneControl),
            new PropertyMetadata(true, OnFormantPreservationChanged));

    public static readonly DependencyProperty RetuneSpeedProperty =
        DependencyProperty.Register(nameof(RetuneSpeed), typeof(double), typeof(AutoTuneControl),
            new PropertyMetadata(50.0, OnRetuneSpeedChanged));

    public static readonly DependencyProperty RootNoteProperty =
        DependencyProperty.Register(nameof(RootNote), typeof(int), typeof(AutoTuneControl),
            new PropertyMetadata(0, OnRootNoteChanged));

    public static readonly DependencyProperty ScaleTypeProperty =
        DependencyProperty.Register(nameof(ScaleType), typeof(int), typeof(AutoTuneControl),
            new PropertyMetadata(0, OnScaleTypeChanged));

    /// <summary>
    /// Gets or sets the detected input pitch in Hz.
    /// </summary>
    public double InputPitch
    {
        get => (double)GetValue(InputPitchProperty);
        set => SetValue(InputPitchProperty, value);
    }

    /// <summary>
    /// Gets or sets the corrected output pitch in Hz.
    /// </summary>
    public double OutputPitch
    {
        get => (double)GetValue(OutputPitchProperty);
        set => SetValue(OutputPitchProperty, value);
    }

    /// <summary>
    /// Gets or sets the correction speed (0-100%).
    /// </summary>
    public double CorrectionSpeed
    {
        get => (double)GetValue(CorrectionSpeedProperty);
        set => SetValue(CorrectionSpeedProperty, value);
    }

    /// <summary>
    /// Gets or sets the humanize amount (0-100%).
    /// </summary>
    public double HumanizeAmount
    {
        get => (double)GetValue(HumanizeAmountProperty);
        set => SetValue(HumanizeAmountProperty, value);
    }

    /// <summary>
    /// Gets or sets whether formant preservation is enabled.
    /// </summary>
    public bool FormantPreservation
    {
        get => (bool)GetValue(FormantPreservationProperty);
        set => SetValue(FormantPreservationProperty, value);
    }

    /// <summary>
    /// Gets or sets the retune speed in milliseconds.
    /// </summary>
    public double RetuneSpeed
    {
        get => (double)GetValue(RetuneSpeedProperty);
        set => SetValue(RetuneSpeedProperty, value);
    }

    /// <summary>
    /// Gets or sets the root note (0=C, 1=C#, etc.).
    /// </summary>
    public int RootNote
    {
        get => (int)GetValue(RootNoteProperty);
        set => SetValue(RootNoteProperty, value);
    }

    /// <summary>
    /// Gets or sets the scale type index.
    /// </summary>
    public int ScaleType
    {
        get => (int)GetValue(ScaleTypeProperty);
        set => SetValue(ScaleTypeProperty, value);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            _isBypassed = value;
            BypassToggle.IsChecked = value;
        }
    }

    /// <summary>
    /// Gets the bypassed notes array.
    /// </summary>
    public bool[] BypassedNotes => (bool[])_bypassedNotes.Clone();

    #endregion

    #region Constructor

    public AutoTuneControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        BuildGraphVisuals();
        UpdateRetuneSpeedKnob();
        DrawPitchScale();
        DrawTimeScale();

        // Start update timer for animations
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _updateTimer?.Stop();
        _updateTimer = null;
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_isInitialized)
        {
            UpdatePitchGraph();
        }
    }

    #endregion

    #region Visual Building

    private void BuildGraphVisuals()
    {
        PitchGraphCanvas.Children.Clear();

        // Grid lines
        DrawGraphGrid();

        // Input pitch line
        _inputPitchLine = new Shapes.Polyline
        {
            Stroke = FindResource("AutoTuneInputPitchBrush") as Brush ?? Brushes.Orange,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        PitchGraphCanvas.Children.Add(_inputPitchLine);

        // Output pitch line
        _outputPitchLine = new Shapes.Polyline
        {
            Stroke = FindResource("AutoTuneOutputPitchBrush") as Brush ?? Brushes.Cyan,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        PitchGraphCanvas.Children.Add(_outputPitchLine);
    }

    private void DrawGraphGrid()
    {
        var borderBrush = FindResource("AutoTuneBorderBrush") as Brush ?? Brushes.Gray;
        double width = PitchGraphCanvas.ActualWidth;
        double height = PitchGraphCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Horizontal grid lines (pitch levels)
        double[] semitones = { 0, 12, 24, 36, 48 }; // C2, C3, C4, C5, C6
        foreach (var st in semitones)
        {
            double freq = 65.41 * Math.Pow(2, st / 12.0); // C2 = 65.41 Hz
            double y = FrequencyToY(freq, height);

            if (y >= 0 && y <= height)
            {
                var line = new Shapes.Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = borderBrush,
                    StrokeThickness = 0.5,
                    Opacity = 0.3
                };
                PitchGraphCanvas.Children.Add(line);
            }
        }

        // Vertical grid lines (time divisions)
        for (int i = 1; i < 4; i++)
        {
            double x = width * i / 4.0;
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = borderBrush,
                StrokeThickness = 0.5,
                Opacity = 0.3
            };
            PitchGraphCanvas.Children.Add(line);
        }
    }

    private void DrawPitchScale()
    {
        PitchScaleCanvas.Children.Clear();

        var textBrush = FindResource("AutoTuneSecondaryTextBrush") as Brush ?? Brushes.Gray;
        double height = PitchGraphCanvas.ActualHeight;
        if (height <= 0) return;

        string[] notes = { "C2", "C3", "C4", "C5", "C6" };
        double[] freqs = { 65.41, 130.81, 261.63, 523.25, 1046.50 };

        for (int i = 0; i < notes.Length; i++)
        {
            double y = FrequencyToY(freqs[i], height);
            if (y < 0 || y > height) continue;

            var label = new TextBlock
            {
                Text = notes[i],
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            PitchScaleCanvas.Children.Add(label);
        }
    }

    private void DrawTimeScale()
    {
        TimeScaleCanvas.Children.Clear();

        var textBrush = FindResource("AutoTuneSecondaryTextBrush") as Brush ?? Brushes.Gray;
        double width = PitchGraphCanvas.ActualWidth;
        if (width <= 0) return;

        // Time markers (assuming 10 seconds of history)
        for (int i = 0; i <= 4; i++)
        {
            double x = width * i / 4.0;
            double seconds = (4 - i) * 2.5; // 10 seconds total, newest on right

            var label = new TextBlock
            {
                Text = seconds > 0 ? $"-{seconds:F1}s" : "Now",
                Foreground = textBrush,
                FontSize = 9
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 2);
            TimeScaleCanvas.Children.Add(label);
        }
    }

    private void PitchGraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            BuildGraphVisuals();
            DrawPitchScale();
            DrawTimeScale();
            UpdatePitchGraph();
        }
    }

    #endregion

    #region Retune Speed Knob

    private void UpdateRetuneSpeedKnob()
    {
        if (!_isInitialized) return;

        double normalizedValue = Math.Clamp((_retuneSpeedMs - 0) / (200 - 0), 0, 1);
        double angle = KnobMinAngle + normalizedValue * KnobAngleRange;

        // Update track arc (full range)
        RetuneSpeedTrackArc.Data = CreateArcGeometry(KnobMinAngle, KnobMaxAngle);

        // Update value arc
        RetuneSpeedValueArc.Data = CreateArcGeometry(KnobMinAngle, angle);

        // Update indicator line
        double rad = (angle - 90) * Math.PI / 180.0;
        double indicatorLength = 14;
        double endX = KnobCenterX + indicatorLength * Math.Cos(rad);
        double endY = KnobCenterY + indicatorLength * Math.Sin(rad);

        RetuneSpeedIndicator.X1 = KnobCenterX;
        RetuneSpeedIndicator.Y1 = KnobCenterY;
        RetuneSpeedIndicator.X2 = endX;
        RetuneSpeedIndicator.Y2 = endY;

        // Update value display
        RetuneSpeedValue.Text = $"{_retuneSpeedMs:F0} ms";
    }

    private Geometry CreateArcGeometry(double startAngle, double endAngle)
    {
        if (Math.Abs(endAngle - startAngle) < 0.1)
        {
            return Geometry.Empty;
        }

        double startRad = (startAngle - 90) * Math.PI / 180.0;
        double endRad = (endAngle - 90) * Math.PI / 180.0;

        double startX = KnobCenterX + KnobRadius * Math.Cos(startRad);
        double startY = KnobCenterY + KnobRadius * Math.Sin(startRad);
        double endX = KnobCenterX + KnobRadius * Math.Cos(endRad);
        double endY = KnobCenterY + KnobRadius * Math.Sin(endRad);

        bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;

        var pathFigure = new PathFigure
        {
            StartPoint = new Point(startX, startY),
            IsClosed = false
        };

        var arcSegment = new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(KnobRadius, KnobRadius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise
        };

        pathFigure.Segments.Add(arcSegment);

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        return pathGeometry;
    }

    private void RetuneSpeedKnob_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingKnob = true;
        _knobDragStartPoint = e.GetPosition(this);
        _knobDragStartValue = _retuneSpeedMs;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void RetuneSpeedKnob_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingKnob = false;
        ((UIElement)sender).ReleaseMouseCapture();
        e.Handled = true;
    }

    private void RetuneSpeedKnob_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingKnob) return;

        Point currentPoint = e.GetPosition(this);
        double deltaY = _knobDragStartPoint.Y - currentPoint.Y;

        double sensitivity = 1.0;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            sensitivity = 0.2;
        }
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            sensitivity = 3.0;
        }

        _retuneSpeedMs = Math.Clamp(_knobDragStartValue + deltaY * sensitivity, 0, 200);
        RetuneSpeed = _retuneSpeedMs;
        UpdateRetuneSpeedKnob();

        RaiseParameterChanged("RetuneSpeed", (float)_retuneSpeedMs);

        e.Handled = true;
    }

    private void RetuneSpeedKnob_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double delta = 5.0;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            delta = 1.0;
        }
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            delta = 20.0;
        }

        if (e.Delta > 0)
        {
            _retuneSpeedMs = Math.Min(200, _retuneSpeedMs + delta);
        }
        else
        {
            _retuneSpeedMs = Math.Max(0, _retuneSpeedMs - delta);
        }

        RetuneSpeed = _retuneSpeedMs;
        UpdateRetuneSpeedKnob();
        RaiseParameterChanged("RetuneSpeed", (float)_retuneSpeedMs);

        e.Handled = true;
    }

    #endregion

    #region Pitch Graph Updates

    private void UpdatePitchGraph()
    {
        if (_inputPitchLine == null || _outputPitchLine == null) return;

        double width = PitchGraphCanvas.ActualWidth;
        double height = PitchGraphCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        // Update input pitch line
        var inputPoints = new PointCollection();
        for (int i = 0; i < _inputPitchHistory.Count; i++)
        {
            double x = (double)i / GraphHistoryLength * width;
            double y = FrequencyToY(_inputPitchHistory[i], height);
            inputPoints.Add(new Point(x, Math.Clamp(y, 0, height)));
        }
        _inputPitchLine.Points = inputPoints;

        // Update output pitch line
        var outputPoints = new PointCollection();
        for (int i = 0; i < _outputPitchHistory.Count; i++)
        {
            double x = (double)i / GraphHistoryLength * width;
            double y = FrequencyToY(_outputPitchHistory[i], height);
            outputPoints.Add(new Point(x, Math.Clamp(y, 0, height)));
        }
        _outputPitchLine.Points = outputPoints;
    }

    private double FrequencyToY(double frequency, double height)
    {
        if (frequency <= 0) return height;

        // Logarithmic mapping
        double logMin = Math.Log10(MinPitchHz);
        double logMax = Math.Log10(MaxPitchHz);
        double logFreq = Math.Log10(Math.Clamp(frequency, MinPitchHz, MaxPitchHz));

        double normalized = (logFreq - logMin) / (logMax - logMin);
        return height * (1 - normalized);
    }

    #endregion

    #region Pitch Display Updates

    private void UpdateInputPitchDisplay(double pitchHz)
    {
        if (pitchHz <= 0)
        {
            InputPitchNote.Text = "--";
            InputPitchCents.Text = "0 cents";
            return;
        }

        var (note, octave, cents) = FrequencyToNoteInfo(pitchHz);
        InputPitchNote.Text = $"{note}{octave}";
        InputPitchCents.Text = cents >= 0 ? $"+{cents} cents" : $"{cents} cents";
    }

    private void UpdateOutputPitchDisplay(double pitchHz)
    {
        if (pitchHz <= 0)
        {
            OutputPitchNote.Text = "--";
            OutputPitchCents.Text = "0 cents";
            return;
        }

        var (note, octave, cents) = FrequencyToNoteInfo(pitchHz);
        OutputPitchNote.Text = $"{note}{octave}";
        OutputPitchCents.Text = cents >= 0 ? $"+{cents} cents" : $"{cents} cents";
    }

    private static (string note, int octave, int cents) FrequencyToNoteInfo(double frequency)
    {
        // A4 = 440 Hz, MIDI note 69
        double midiNote = 69 + 12 * Math.Log2(frequency / 440.0);
        int nearestNote = (int)Math.Round(midiNote);
        int cents = (int)Math.Round((midiNote - nearestNote) * 100);

        int noteIndex = nearestNote % 12;
        if (noteIndex < 0) noteIndex += 12;
        int octave = (nearestNote / 12) - 1;

        return (NoteNames[noteIndex], octave, cents);
    }

    #endregion

    #region Parameter Event Handlers

    private void CorrectionSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CorrectionSpeedValue == null) return;
        CorrectionSpeedValue.Text = $"{e.NewValue:F0}%";
        CorrectionSpeed = e.NewValue;
        RaiseParameterChanged("CorrectionSpeed", (float)(e.NewValue / 100.0));
    }

    private void HumanizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HumanizeValue == null) return;
        HumanizeValue.Text = $"{e.NewValue:F0}%";
        HumanizeAmount = e.NewValue;
        RaiseParameterChanged("Humanize", (float)(e.NewValue / 100.0));
    }

    private void RootNoteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        RootNote = RootNoteComboBox.SelectedIndex;
        RaiseKeyScaleChanged();
    }

    private void ScaleTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ScaleType = ScaleTypeComboBox.SelectedIndex;
        RaiseKeyScaleChanged();
    }

    private void FormantPreservationToggle_Click(object sender, RoutedEventArgs e)
    {
        FormantPreservation = FormantPreservationToggle.IsChecked == true;
        RaiseParameterChanged("FormantPreservation", FormantPreservation ? 1f : 0f);
        StatusText.Text = FormantPreservation ? "Formant preservation enabled" : "Formant preservation disabled";
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = BypassToggle.IsChecked == true;
        StatusText.Text = _isBypassed ? "Effect bypassed" : "Effect active";
        BypassChanged?.Invoke(this, _isBypassed);
    }

    private void NoteBypass_Changed(object sender, RoutedEventArgs e)
    {
        UpdateBypassedNotesFromUI();
        NoteBypassChanged?.Invoke(this, BypassedNotes);

        int bypassCount = 0;
        foreach (var bypassed in _bypassedNotes)
        {
            if (bypassed) bypassCount++;
        }
        StatusText.Text = bypassCount > 0 ? $"{bypassCount} note(s) bypassed" : "All notes active";
    }

    private void ClearBypassButton_Click(object sender, RoutedEventArgs e)
    {
        BypassC.IsChecked = false;
        BypassCSharp.IsChecked = false;
        BypassD.IsChecked = false;
        BypassDSharp.IsChecked = false;
        BypassE.IsChecked = false;
        BypassF.IsChecked = false;
        BypassFSharp.IsChecked = false;
        BypassG.IsChecked = false;
        BypassGSharp.IsChecked = false;
        BypassA.IsChecked = false;
        BypassASharp.IsChecked = false;
        BypassB.IsChecked = false;

        for (int i = 0; i < 12; i++)
        {
            _bypassedNotes[i] = false;
        }

        NoteBypassChanged?.Invoke(this, BypassedNotes);
        StatusText.Text = "Note bypass cleared";
    }

    private void UpdateBypassedNotesFromUI()
    {
        _bypassedNotes[0] = BypassC.IsChecked == true;
        _bypassedNotes[1] = BypassCSharp.IsChecked == true;
        _bypassedNotes[2] = BypassD.IsChecked == true;
        _bypassedNotes[3] = BypassDSharp.IsChecked == true;
        _bypassedNotes[4] = BypassE.IsChecked == true;
        _bypassedNotes[5] = BypassF.IsChecked == true;
        _bypassedNotes[6] = BypassFSharp.IsChecked == true;
        _bypassedNotes[7] = BypassG.IsChecked == true;
        _bypassedNotes[8] = BypassGSharp.IsChecked == true;
        _bypassedNotes[9] = BypassA.IsChecked == true;
        _bypassedNotes[10] = BypassASharp.IsChecked == true;
        _bypassedNotes[11] = BypassB.IsChecked == true;
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedIndex <= 0) return;

        var item = PresetComboBox.SelectedItem as ComboBoxItem;
        if (item == null) return;

        string preset = item.Content?.ToString() ?? "";
        ApplyPreset(preset);

        PresetComboBox.SelectedIndex = 0;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Reset();
    }

    #endregion

    #region Presets

    private void ApplyPreset(string preset)
    {
        switch (preset)
        {
            case "Subtle Correction":
                CorrectionSpeedSlider.Value = 30;
                HumanizeSlider.Value = 40;
                _retuneSpeedMs = 100;
                FormantPreservationToggle.IsChecked = true;
                break;

            case "Natural Voice":
                CorrectionSpeedSlider.Value = 50;
                HumanizeSlider.Value = 25;
                _retuneSpeedMs = 80;
                FormantPreservationToggle.IsChecked = true;
                break;

            case "Standard":
                CorrectionSpeedSlider.Value = 70;
                HumanizeSlider.Value = 15;
                _retuneSpeedMs = 50;
                FormantPreservationToggle.IsChecked = true;
                break;

            case "Hard Correction":
                CorrectionSpeedSlider.Value = 90;
                HumanizeSlider.Value = 5;
                _retuneSpeedMs = 20;
                FormantPreservationToggle.IsChecked = true;
                break;

            case "T-Pain Effect":
                CorrectionSpeedSlider.Value = 100;
                HumanizeSlider.Value = 0;
                _retuneSpeedMs = 0;
                FormantPreservationToggle.IsChecked = false;
                break;

            case "Cher Effect":
                CorrectionSpeedSlider.Value = 100;
                HumanizeSlider.Value = 0;
                _retuneSpeedMs = 0;
                FormantPreservationToggle.IsChecked = true;
                break;
        }

        RetuneSpeed = _retuneSpeedMs;
        UpdateRetuneSpeedKnob();
        FormantPreservation = FormantPreservationToggle.IsChecked == true;

        StatusText.Text = $"Preset applied: {preset}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the pitch displays with new detected and corrected pitch values.
    /// </summary>
    public void UpdatePitch(double inputPitchHz, double outputPitchHz)
    {
        InputPitch = inputPitchHz;
        OutputPitch = outputPitchHz;

        // Add to history
        _inputPitchHistory.Add(inputPitchHz);
        _outputPitchHistory.Add(outputPitchHz);

        // Trim history
        while (_inputPitchHistory.Count > GraphHistoryLength)
        {
            _inputPitchHistory.RemoveAt(0);
        }
        while (_outputPitchHistory.Count > GraphHistoryLength)
        {
            _outputPitchHistory.RemoveAt(0);
        }

        // Update displays
        UpdateInputPitchDisplay(inputPitchHz);
        UpdateOutputPitchDisplay(outputPitchHz);
    }

    /// <summary>
    /// Sets the note bypass state for a specific note.
    /// </summary>
    public void SetNoteBypassed(int noteIndex, bool bypassed)
    {
        if (noteIndex < 0 || noteIndex >= 12) return;

        _bypassedNotes[noteIndex] = bypassed;

        // Update UI
        switch (noteIndex)
        {
            case 0: BypassC.IsChecked = bypassed; break;
            case 1: BypassCSharp.IsChecked = bypassed; break;
            case 2: BypassD.IsChecked = bypassed; break;
            case 3: BypassDSharp.IsChecked = bypassed; break;
            case 4: BypassE.IsChecked = bypassed; break;
            case 5: BypassF.IsChecked = bypassed; break;
            case 6: BypassFSharp.IsChecked = bypassed; break;
            case 7: BypassG.IsChecked = bypassed; break;
            case 8: BypassGSharp.IsChecked = bypassed; break;
            case 9: BypassA.IsChecked = bypassed; break;
            case 10: BypassASharp.IsChecked = bypassed; break;
            case 11: BypassB.IsChecked = bypassed; break;
        }
    }

    /// <summary>
    /// Resets all parameters to default values.
    /// </summary>
    public void Reset()
    {
        CorrectionSpeedSlider.Value = 100;
        HumanizeSlider.Value = 0;
        _retuneSpeedMs = 50;
        RetuneSpeed = _retuneSpeedMs;
        UpdateRetuneSpeedKnob();

        FormantPreservationToggle.IsChecked = true;
        FormantPreservation = true;

        RootNoteComboBox.SelectedIndex = 0;
        ScaleTypeComboBox.SelectedIndex = 0;

        ClearBypassButton_Click(this, new RoutedEventArgs());

        BypassToggle.IsChecked = false;
        _isBypassed = false;

        _inputPitchHistory.Clear();
        _outputPitchHistory.Clear();

        StatusText.Text = "Reset to defaults";
    }

    /// <summary>
    /// Clears the pitch history graph.
    /// </summary>
    public void ClearHistory()
    {
        _inputPitchHistory.Clear();
        _outputPitchHistory.Clear();
        UpdatePitchGraph();
    }

    #endregion

    #region Helper Methods

    private void RaiseParameterChanged(string name, float value)
    {
        ParameterChanged?.Invoke(this, new AutoTuneParameterChangedEventArgs(name, value));
    }

    private void RaiseKeyScaleChanged()
    {
        KeyScaleChanged?.Invoke(this, new AutoTuneKeyScaleChangedEventArgs(RootNote, ScaleType));
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnInputPitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoTuneControl control)
        {
            control.UpdateInputPitchDisplay((double)e.NewValue);
        }
    }

    private static void OnOutputPitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoTuneControl control)
        {
            control.UpdateOutputPitchDisplay((double)e.NewValue);
        }
    }

    private static void OnCorrectionSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoTuneControl control && control._isInitialized)
        {
            control.CorrectionSpeedSlider.Value = (double)e.NewValue;
        }
    }

    private static void OnHumanizeAmountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoTuneControl control && control._isInitialized)
        {
            control.HumanizeSlider.Value = (double)e.NewValue;
        }
    }

    private static void OnFormantPreservationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoTuneControl control && control._isInitialized)
        {
            control.FormantPreservationToggle.IsChecked = (bool)e.NewValue;
        }
    }

    private static void OnRetuneSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoTuneControl control)
        {
            control._retuneSpeedMs = (double)e.NewValue;
            if (control._isInitialized)
            {
                control.UpdateRetuneSpeedKnob();
            }
        }
    }

    private static void OnRootNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoTuneControl control && control._isInitialized)
        {
            control.RootNoteComboBox.SelectedIndex = (int)e.NewValue;
        }
    }

    private static void OnScaleTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoTuneControl control && control._isInitialized)
        {
            control.ScaleTypeComboBox.SelectedIndex = (int)e.NewValue;
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for auto-tune parameter changes.
/// </summary>
public class AutoTuneParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public AutoTuneParameterChangedEventArgs(string parameterName, float value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

/// <summary>
/// Event arguments for key/scale changes.
/// </summary>
public class AutoTuneKeyScaleChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the root note (0=C, 1=C#, etc.).
    /// </summary>
    public int RootNote { get; }

    /// <summary>
    /// Gets the scale type index.
    /// </summary>
    public int ScaleType { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public AutoTuneKeyScaleChangedEventArgs(int rootNote, int scaleType)
    {
        RootNote = rootNote;
        ScaleType = scaleType;
    }
}
