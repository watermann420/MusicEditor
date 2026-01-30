// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Trigger mode for oscilloscope display.
/// </summary>
public enum OscilloscopeTriggerMode
{
    FreeRun,
    RisingEdge,
    FallingEdge
}

/// <summary>
/// Real-time oscilloscope control for time-domain waveform visualization.
/// Features trigger modes, time/amplitude scaling, and freeze/hold functionality.
/// </summary>
public partial class OscilloscopeControl : UserControl
{
    #region Constants

    private const int GridDivisions = 8;
    private const double DefaultTimeScale = 10.0; // ms per division
    private const double DefaultAmplitudeScale = 1.0;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.Register(nameof(Samples), typeof(float[]), typeof(OscilloscopeControl),
            new PropertyMetadata(null, OnSamplesChanged));

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(OscilloscopeControl),
            new PropertyMetadata(44100));

    public static readonly DependencyProperty WaveformColorProperty =
        DependencyProperty.Register(nameof(WaveformColor), typeof(Color), typeof(OscilloscopeControl),
            new PropertyMetadata(Color.FromRgb(0x00, 0xCE, 0xD1)));

    /// <summary>
    /// Gets or sets the audio samples to display.
    /// </summary>
    public float[] Samples
    {
        get => (float[])GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    /// <summary>
    /// Gets or sets the sample rate of the audio.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the waveform display color.
    /// </summary>
    public Color WaveformColor
    {
        get => (Color)GetValue(WaveformColorProperty);
        set => SetValue(WaveformColorProperty, value);
    }

    #endregion

    #region Private Fields

    private WriteableBitmap? _bitmap;
    private int _bitmapWidth;
    private int _bitmapHeight;
    private byte[]? _pixelBuffer;
    private bool _isInitialized;
    private bool _isFrozen;
    private float[]? _frozenSamples;

    private OscilloscopeTriggerMode _triggerMode = OscilloscopeTriggerMode.FreeRun;
    private double _timeScale = DefaultTimeScale;
    private double _amplitudeScale = DefaultAmplitudeScale;
    private double _triggerLevel;

    #endregion

    #region Constructor

    public OscilloscopeControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeBitmap();
        DrawGrid();
        DrawCenterLine();
        DrawAmplitudeScale();
        UpdateScaleText();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _bitmap = null;
        _pixelBuffer = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            InitializeBitmap();
            DrawGrid();
            DrawCenterLine();
            DrawTriggerLine();
            DrawAmplitudeScale();
            UpdateWaveform();
        }
    }

    private static void OnSamplesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OscilloscopeControl control && control._isInitialized && !control._isFrozen)
        {
            control.UpdateWaveform();
        }
    }

    private void TriggerModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _triggerMode = TriggerModeCombo.SelectedIndex switch
        {
            0 => OscilloscopeTriggerMode.FreeRun,
            1 => OscilloscopeTriggerMode.RisingEdge,
            2 => OscilloscopeTriggerMode.FallingEdge,
            _ => OscilloscopeTriggerMode.FreeRun
        };

        DrawTriggerLine();
        if (_isInitialized && !_isFrozen) UpdateWaveform();
    }

    private void FreezeButton_Changed(object sender, RoutedEventArgs e)
    {
        _isFrozen = FreezeButton.IsChecked ?? false;
        if (_isFrozen && Samples != null)
        {
            _frozenSamples = (float[])Samples.Clone();
        }
        else
        {
            _frozenSamples = null;
            if (_isInitialized) UpdateWaveform();
        }
    }

    private void TimeScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _timeScale = TimeScaleSlider.Value;
        UpdateScaleText();
        if (_isInitialized) UpdateWaveform();
    }

    private void AmplitudeScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _amplitudeScale = AmplitudeScaleSlider.Value;
        UpdateScaleText();
        DrawAmplitudeScale();
        if (_isInitialized) UpdateWaveform();
    }

    private void TriggerLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _triggerLevel = TriggerLevelSlider.Value;
        DrawTriggerLine();
        if (_isInitialized && !_isFrozen) UpdateWaveform();
    }

    #endregion

    #region Bitmap Management

    private void InitializeBitmap()
    {
        var parent = WaveformImage.Parent as FrameworkElement;
        if (parent == null) return;

        int width = (int)Math.Max(1, parent.ActualWidth);
        int height = (int)Math.Max(1, parent.ActualHeight);

        if (width == _bitmapWidth && height == _bitmapHeight && _bitmap != null)
        {
            return;
        }

        _bitmapWidth = width;
        _bitmapHeight = height;
        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _pixelBuffer = new byte[width * height * 4];
        WaveformImage.Source = _bitmap;

        ClearBitmap();
    }

    private void ClearBitmap()
    {
        if (_pixelBuffer == null) return;
        Array.Clear(_pixelBuffer, 0, _pixelBuffer.Length);
        UpdateBitmap();
    }

    private void UpdateBitmap()
    {
        if (_bitmap == null || _pixelBuffer == null) return;

        _bitmap.Lock();
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(
                _pixelBuffer, 0, _bitmap.BackBuffer, _pixelBuffer.Length);
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmapWidth, _bitmapHeight));
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    #endregion

    #region Drawing

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var gridBrush = FindResource("GridBrush") as Brush ?? Brushes.DarkGray;

        // Vertical divisions (time)
        for (int i = 0; i <= GridDivisions; i++)
        {
            double x = i * width / GridDivisions;
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }

        // Horizontal divisions (amplitude)
        for (int i = 0; i <= GridDivisions; i++)
        {
            double y = i * height / GridDivisions;
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawCenterLine()
    {
        CenterLineCanvas.Children.Clear();

        double width = CenterLineCanvas.ActualWidth;
        double height = CenterLineCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var centerBrush = FindResource("CenterLineBrush") as Brush ?? Brushes.Gray;

        var line = new Shapes.Line
        {
            X1 = 0,
            Y1 = height / 2,
            X2 = width,
            Y2 = height / 2,
            Stroke = centerBrush,
            StrokeThickness = 1
        };
        CenterLineCanvas.Children.Add(line);
    }

    private void DrawTriggerLine()
    {
        TriggerLineCanvas.Children.Clear();

        if (_triggerMode == OscilloscopeTriggerMode.FreeRun) return;

        double width = TriggerLineCanvas.ActualWidth;
        double height = TriggerLineCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var triggerBrush = FindResource("TriggerLineBrush") as Brush ?? Brushes.Orange;

        double y = height / 2 - (_triggerLevel * height / 2 / _amplitudeScale);
        y = Math.Clamp(y, 0, height);

        var line = new Shapes.Line
        {
            X1 = 0,
            Y1 = y,
            X2 = width,
            Y2 = y,
            Stroke = triggerBrush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        TriggerLineCanvas.Children.Add(line);

        // Trigger indicator
        var indicator = new Shapes.Polygon
        {
            Points = new PointCollection { new Point(0, y - 5), new Point(8, y), new Point(0, y + 5) },
            Fill = triggerBrush
        };
        TriggerLineCanvas.Children.Add(indicator);
    }

    private void DrawAmplitudeScale()
    {
        AmplitudeScaleCanvas.Children.Clear();

        var textBrush = FindResource("TextBrush") as Brush ?? Brushes.Gray;
        double height = GridCanvas.ActualHeight > 0 ? GridCanvas.ActualHeight : 100;

        // Draw scale labels
        double[] amplitudes = { 1.0, 0.5, 0, -0.5, -1.0 };
        foreach (var amp in amplitudes)
        {
            double scaledAmp = amp * _amplitudeScale;
            double y = height / 2 - (amp * height / 2);

            var label = new TextBlock
            {
                Text = scaledAmp.ToString("F1"),
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            AmplitudeScaleCanvas.Children.Add(label);
        }
    }

    private void UpdateScaleText()
    {
        TimeScaleText.Text = $"Time: {_timeScale:F1} ms/div";
        AmplitudeScaleText.Text = $"Amp: {_amplitudeScale:F1}/div";
    }

    private void UpdateWaveform()
    {
        if (_bitmap == null || _pixelBuffer == null) return;

        float[]? samples = _isFrozen ? _frozenSamples : Samples;
        if (samples == null || samples.Length == 0)
        {
            ClearBitmap();
            return;
        }

        ClearBitmap();

        int width = _bitmapWidth;
        int height = _bitmapHeight;
        double centerY = height / 2.0;

        // Calculate samples per pixel based on time scale
        double totalTimeMs = _timeScale * GridDivisions;
        double totalTimeSec = totalTimeMs / 1000.0;
        int totalSamplesToDisplay = (int)(totalTimeSec * SampleRate);

        // Find trigger point
        int startIndex = FindTriggerPoint(samples, totalSamplesToDisplay);

        Color waveColor = WaveformColor;
        byte r = waveColor.R;
        byte g = waveColor.G;
        byte b = waveColor.B;

        // Draw waveform
        int prevY = -1;
        for (int x = 0; x < width; x++)
        {
            double sampleRatio = (double)x / width;
            int sampleIndex = startIndex + (int)(sampleRatio * totalSamplesToDisplay);

            if (sampleIndex >= 0 && sampleIndex < samples.Length)
            {
                float sample = samples[sampleIndex];
                sample = Math.Clamp(sample / (float)_amplitudeScale, -1f, 1f);

                int y = (int)(centerY - sample * centerY);
                y = Math.Clamp(y, 0, height - 1);

                // Draw vertical line between points for continuous waveform
                if (prevY >= 0)
                {
                    int minY = Math.Min(prevY, y);
                    int maxY = Math.Max(prevY, y);
                    for (int drawY = minY; drawY <= maxY; drawY++)
                    {
                        SetPixel(x, drawY, b, g, r, 255);
                    }
                }
                else
                {
                    SetPixel(x, y, b, g, r, 255);
                }

                prevY = y;
            }
        }

        UpdateBitmap();
    }

    private int FindTriggerPoint(float[] samples, int windowSize)
    {
        if (_triggerMode == OscilloscopeTriggerMode.FreeRun)
        {
            return Math.Max(0, samples.Length - windowSize);
        }

        float triggerLevel = (float)_triggerLevel;
        int searchStart = Math.Max(0, samples.Length - windowSize * 2);
        int searchEnd = Math.Max(0, samples.Length - windowSize);

        for (int i = searchStart; i < searchEnd - 1; i++)
        {
            if (_triggerMode == OscilloscopeTriggerMode.RisingEdge)
            {
                if (samples[i] < triggerLevel && samples[i + 1] >= triggerLevel)
                {
                    return i;
                }
            }
            else if (_triggerMode == OscilloscopeTriggerMode.FallingEdge)
            {
                if (samples[i] > triggerLevel && samples[i + 1] <= triggerLevel)
                {
                    return i;
                }
            }
        }

        // No trigger found, fall back to end of buffer
        return Math.Max(0, samples.Length - windowSize);
    }

    private void SetPixel(int x, int y, byte b, byte g, byte r, byte a)
    {
        if (_pixelBuffer == null) return;
        if (x < 0 || x >= _bitmapWidth || y < 0 || y >= _bitmapHeight) return;

        int index = (y * _bitmapWidth + x) * 4;
        _pixelBuffer[index] = b;
        _pixelBuffer[index + 1] = g;
        _pixelBuffer[index + 2] = r;
        _pixelBuffer[index + 3] = a;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Freezes the current display.
    /// </summary>
    public void Freeze()
    {
        FreezeButton.IsChecked = true;
    }

    /// <summary>
    /// Unfreezes and resumes live display.
    /// </summary>
    public void Unfreeze()
    {
        FreezeButton.IsChecked = false;
    }

    /// <summary>
    /// Clears the display.
    /// </summary>
    public void Clear()
    {
        ClearBitmap();
    }

    /// <summary>
    /// Sets the trigger mode programmatically.
    /// </summary>
    public void SetTriggerMode(OscilloscopeTriggerMode mode)
    {
        _triggerMode = mode;
        TriggerModeCombo.SelectedIndex = (int)mode;
    }

    #endregion
}
