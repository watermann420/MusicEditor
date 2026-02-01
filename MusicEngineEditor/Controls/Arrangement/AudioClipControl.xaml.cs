// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Audio clip control with waveform preview for arrangement view.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Arrangement;

/// <summary>
/// Control for displaying audio clips with waveform preview in the arrangement view.
/// </summary>
public partial class AudioClipControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ClipNameProperty =
        DependencyProperty.Register(nameof(ClipName), typeof(string), typeof(AudioClipControl),
            new PropertyMetadata("Audio Clip", OnClipNameChanged));

    public static readonly DependencyProperty ClipColorProperty =
        DependencyProperty.Register(nameof(ClipColor), typeof(Color), typeof(AudioClipControl),
            new PropertyMetadata(Color.FromRgb(0x00, 0xCC, 0x66), OnClipColorChanged));

    public static readonly DependencyProperty StartBeatProperty =
        DependencyProperty.Register(nameof(StartBeat), typeof(double), typeof(AudioClipControl),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(double), typeof(AudioClipControl),
            new PropertyMetadata(4.0, OnDurationChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(AudioClipControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty WaveformDataProperty =
        DependencyProperty.Register(nameof(WaveformData), typeof(float[]), typeof(AudioClipControl),
            new PropertyMetadata(null, OnWaveformDataChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the clip name.
    /// </summary>
    public string ClipName
    {
        get => (string)GetValue(ClipNameProperty);
        set => SetValue(ClipNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the clip color.
    /// </summary>
    public Color ClipColor
    {
        get => (Color)GetValue(ClipColorProperty);
        set => SetValue(ClipColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the start position in beats.
    /// </summary>
    public double StartBeat
    {
        get => (double)GetValue(StartBeatProperty);
        set => SetValue(StartBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the duration in beats.
    /// </summary>
    public double Duration
    {
        get => (double)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the clip is selected.
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Gets or sets the waveform data for preview display.
    /// Array of peak values normalized to -1.0 to 1.0.
    /// </summary>
    public float[]? WaveformData
    {
        get => (float[]?)GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the clip is selected.
    /// </summary>
    public event EventHandler? ClipSelected;

    /// <summary>
    /// Event raised when the clip is double-clicked for editing.
    /// </summary>
    public event EventHandler? EditRequested;

    /// <summary>
    /// Event raised when duplicate is requested.
    /// </summary>
    public event EventHandler? DuplicateRequested;

    /// <summary>
    /// Event raised when delete is requested.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Event raised when the clip is moved.
    /// </summary>
    public event EventHandler<double>? ClipMoved;

    /// <summary>
    /// Event raised when the clip is resized from the left.
    /// </summary>
    public event EventHandler<double>? ClipResizedLeft;

    /// <summary>
    /// Event raised when the clip is resized from the right.
    /// </summary>
    public event EventHandler<double>? ClipResizedRight;

    #endregion

    #region Fields

    private bool _isDragging;
    private bool _isResizingLeft;
    private bool _isResizingRight;
    private Point _dragStartPoint;
    private double _originalStartBeat;
    private double _originalDuration;

    #endregion

    public AudioClipControl()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseDoubleClick += OnMouseDoubleClick;
        SizeChanged += OnSizeChanged;
    }

    #region Property Changed Callbacks

    private static void OnClipNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control)
        {
            control.ClipNameLabel.Text = e.NewValue as string ?? "Audio Clip";
        }
    }

    private static void OnClipColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control && e.NewValue is Color color)
        {
            control.UpdateClipColor(color);
        }
    }

    private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control)
        {
            control.RenderWaveform();
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control)
        {
            control.SelectionBorder.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control)
        {
            control.RenderWaveform();
        }
    }

    #endregion

    #region Event Handlers

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderWaveform();
        UpdateCenterLine();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartBeat = StartBeat;
        _originalDuration = Duration;
        _isDragging = true;

        IsSelected = true;
        ClipSelected?.Invoke(this, EventArgs.Empty);

        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizingLeft && Math.Abs(StartBeat - _originalStartBeat) > 0.01)
        {
            ClipResizedLeft?.Invoke(this, StartBeat - _originalStartBeat);
        }
        else if (_isResizingRight && Math.Abs(Duration - _originalDuration) > 0.01)
        {
            ClipResizedRight?.Invoke(this, Duration - _originalDuration);
        }
        else if (_isDragging && Math.Abs(StartBeat - _originalStartBeat) > 0.01)
        {
            ClipMoved?.Invoke(this, StartBeat - _originalStartBeat);
        }

        _isDragging = false;
        _isResizingLeft = false;
        _isResizingRight = false;

        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // Movement is typically handled by the parent container
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        EditRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void LeftResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isResizingLeft = true;
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartBeat = StartBeat;
        _originalDuration = Duration;

        CaptureMouse();
        e.Handled = true;
    }

    private void RightResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isResizingRight = true;
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartBeat = StartBeat;
        _originalDuration = Duration;

        CaptureMouse();
        e.Handled = true;
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DuplicateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Rendering

    private void UpdateClipColor(Color color)
    {
        var fillBrush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
        var borderBrush = new SolidColorBrush(color);

        ClipBorder.Background = fillBrush;
        ClipBorder.BorderBrush = borderBrush;
        HeaderBar.Background = borderBrush;
    }

    private void UpdateCenterLine()
    {
        var height = WaveformCanvas.ActualHeight;
        var width = WaveformCanvas.ActualWidth;

        if (height <= 0 || width <= 0)
            return;

        CenterLine.X1 = 0;
        CenterLine.X2 = width;
        CenterLine.Y1 = height / 2;
        CenterLine.Y2 = height / 2;
    }

    /// <summary>
    /// Renders the waveform preview using a Polyline.
    /// </summary>
    public void RenderWaveform()
    {
        var data = WaveformData;

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            WaveformPolyline.Points = null;
            return;
        }

        if (data == null || data.Length == 0)
        {
            // Draw a simple line when no waveform data
            var emptyPoints = new PointCollection
            {
                new Point(0, height / 2),
                new Point(width, height / 2)
            };
            WaveformPolyline.Points = emptyPoints;
            return;
        }

        var centerY = height / 2;
        var halfHeight = height / 2 * 0.85;

        // Calculate samples per pixel
        var samplesPerPixel = Math.Max(1, data.Length / (int)width);
        var pixelCount = Math.Min((int)width, data.Length);

        var points = new PointCollection();

        // Draw upper half of waveform
        for (var x = 0; x < pixelCount; x++)
        {
            var sampleIndex = (int)(x * (double)data.Length / pixelCount);
            if (sampleIndex >= data.Length)
                sampleIndex = data.Length - 1;

            // Find peak in this pixel's samples
            var maxValue = 0f;
            var endIndex = Math.Min(sampleIndex + samplesPerPixel, data.Length);
            for (var i = sampleIndex; i < endIndex; i++)
            {
                var absVal = Math.Abs(data[i]);
                if (absVal > maxValue)
                    maxValue = absVal;
            }

            var y = centerY - maxValue * halfHeight;
            points.Add(new Point(x, y));
        }

        // Draw lower half of waveform (reverse direction)
        for (var x = pixelCount - 1; x >= 0; x--)
        {
            var sampleIndex = (int)(x * (double)data.Length / pixelCount);
            if (sampleIndex >= data.Length)
                sampleIndex = data.Length - 1;

            // Find peak in this pixel's samples
            var maxValue = 0f;
            var endIndex = Math.Min(sampleIndex + samplesPerPixel, data.Length);
            for (var i = sampleIndex; i < endIndex; i++)
            {
                var absVal = Math.Abs(data[i]);
                if (absVal > maxValue)
                    maxValue = absVal;
            }

            var y = centerY + maxValue * halfHeight;
            points.Add(new Point(x, y));
        }

        // Close the shape
        if (points.Count > 0)
        {
            points.Add(points[0]);
        }

        WaveformPolyline.Points = points;
        WaveformPolyline.Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
        WaveformPolyline.Stroke = new SolidColorBrush(Colors.White);
        WaveformPolyline.StrokeThickness = 0.5;
    }

    #endregion
}
