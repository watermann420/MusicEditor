// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Stereo vector scope control for visualizing stereo image.
/// Shows XY plot (L-R vs L+R) with Lissajous display and color decay trails.
/// </summary>
public partial class VectorScopeControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty LeftSamplesProperty =
        DependencyProperty.Register(nameof(LeftSamples), typeof(float[]), typeof(VectorScopeControl),
            new PropertyMetadata(null, OnSamplesChanged));

    public static readonly DependencyProperty RightSamplesProperty =
        DependencyProperty.Register(nameof(RightSamples), typeof(float[]), typeof(VectorScopeControl),
            new PropertyMetadata(null, OnSamplesChanged));

    public static readonly DependencyProperty PointColorProperty =
        DependencyProperty.Register(nameof(PointColor), typeof(Color), typeof(VectorScopeControl),
            new PropertyMetadata(Color.FromRgb(0x00, 0xCE, 0xD1)));

    public static readonly DependencyProperty DecayFactorProperty =
        DependencyProperty.Register(nameof(DecayFactor), typeof(double), typeof(VectorScopeControl),
            new PropertyMetadata(0.95));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(VectorScopeControl),
            new PropertyMetadata(1.0, OnLayoutChanged));

    /// <summary>
    /// Gets or sets the left channel samples.
    /// </summary>
    public float[] LeftSamples
    {
        get => (float[])GetValue(LeftSamplesProperty);
        set => SetValue(LeftSamplesProperty, value);
    }

    /// <summary>
    /// Gets or sets the right channel samples.
    /// </summary>
    public float[] RightSamples
    {
        get => (float[])GetValue(RightSamplesProperty);
        set => SetValue(RightSamplesProperty, value);
    }

    /// <summary>
    /// Gets or sets the point color.
    /// </summary>
    public Color PointColor
    {
        get => (Color)GetValue(PointColorProperty);
        set => SetValue(PointColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the decay factor for trails (0-1).
    /// </summary>
    public double DecayFactor
    {
        get => (double)GetValue(DecayFactorProperty);
        set => SetValue(DecayFactorProperty, value);
    }

    /// <summary>
    /// Gets or sets the zoom level.
    /// </summary>
    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    #endregion

    #region Private Fields

    private WriteableBitmap? _bitmap;
    private int _bitmapWidth;
    private int _bitmapHeight;
    private byte[]? _pixelBuffer;
    private bool _isInitialized;
    private bool _showGrid = true;
    private bool _lissajousMode = true;
    private double _currentCorrelation;

    #endregion

    #region Constructor

    public VectorScopeControl()
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
        DrawLabels();
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
            DrawLabels();
        }
    }

    private static void OnSamplesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VectorScopeControl control && control._isInitialized)
        {
            control.UpdateDisplay();
        }
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VectorScopeControl control && control._isInitialized)
        {
            control.DrawGrid();
            control.UpdateDisplay();
        }
    }

    private void LissajousToggle_Changed(object sender, RoutedEventArgs e)
    {
        _lissajousMode = LissajousToggle.IsChecked ?? true;
        DrawGrid();
        ClearBitmap();
    }

    private void GridToggle_Changed(object sender, RoutedEventArgs e)
    {
        _showGrid = GridToggle.IsChecked ?? true;
        DrawGrid();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Zoom = ZoomSlider.Value;
    }

    private void DecaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DecayFactor = DecaySlider.Value;
    }

    #endregion

    #region Bitmap Management

    private void InitializeBitmap()
    {
        var parent = ScopeImage.Parent as FrameworkElement;
        if (parent == null) return;

        int size = (int)Math.Max(1, Math.Min(parent.ActualWidth, parent.ActualHeight));

        if (size == _bitmapWidth && size == _bitmapHeight && _bitmap != null)
        {
            return;
        }

        _bitmapWidth = size;
        _bitmapHeight = size;
        _bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        _pixelBuffer = new byte[size * size * 4];
        ScopeImage.Source = _bitmap;

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

        if (!_showGrid) return;

        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;

        if (width <= 0 || height <= 0) return;

        var gridBrush = FindResource("GridBrush") as Brush ?? Brushes.DarkGray;
        var axisBrush = FindResource("AxisBrush") as Brush ?? Brushes.Gray;

        double radius = Math.Min(width, height) / 2 - 4;

        // Outer circle
        var outerCircle = new Shapes.Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = gridBrush,
            StrokeThickness = 1,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(outerCircle, centerX - radius);
        Canvas.SetTop(outerCircle, centerY - radius);
        GridCanvas.Children.Add(outerCircle);

        // Inner circles at 75%, 50%, 25%
        foreach (double factor in new[] { 0.75, 0.5, 0.25 })
        {
            var innerCircle = new Shapes.Ellipse
            {
                Width = radius * 2 * factor,
                Height = radius * 2 * factor,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(innerCircle, centerX - radius * factor);
            Canvas.SetTop(innerCircle, centerY - radius * factor);
            GridCanvas.Children.Add(innerCircle);
        }

        if (_lissajousMode)
        {
            // L/R axes (diagonal)
            double axisLength = radius;

            // Left channel axis (45 degrees up-left)
            var lAxis = new Shapes.Line
            {
                X1 = centerX,
                Y1 = centerY,
                X2 = centerX - axisLength * 0.707,
                Y2 = centerY - axisLength * 0.707,
                Stroke = axisBrush,
                StrokeThickness = 1
            };
            GridCanvas.Children.Add(lAxis);

            // Right channel axis (45 degrees up-right)
            var rAxis = new Shapes.Line
            {
                X1 = centerX,
                Y1 = centerY,
                X2 = centerX + axisLength * 0.707,
                Y2 = centerY - axisLength * 0.707,
                Stroke = axisBrush,
                StrokeThickness = 1
            };
            GridCanvas.Children.Add(rAxis);

            // M axis (vertical - mono)
            var mAxis = new Shapes.Line
            {
                X1 = centerX,
                Y1 = centerY - axisLength,
                X2 = centerX,
                Y2 = centerY + axisLength,
                Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x66)),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            GridCanvas.Children.Add(mAxis);

            // S axis (horizontal - side)
            var sAxis = new Shapes.Line
            {
                X1 = centerX - axisLength,
                Y1 = centerY,
                X2 = centerX + axisLength,
                Y2 = centerY,
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            GridCanvas.Children.Add(sAxis);
        }
        else
        {
            // Standard XY mode - horizontal and vertical axes
            var hAxis = new Shapes.Line
            {
                X1 = centerX - radius,
                Y1 = centerY,
                X2 = centerX + radius,
                Y2 = centerY,
                Stroke = axisBrush,
                StrokeThickness = 1
            };
            GridCanvas.Children.Add(hAxis);

            var vAxis = new Shapes.Line
            {
                X1 = centerX,
                Y1 = centerY - radius,
                X2 = centerX,
                Y2 = centerY + radius,
                Stroke = axisBrush,
                StrokeThickness = 1
            };
            GridCanvas.Children.Add(vAxis);
        }

        // Center dot
        var centerDot = new Shapes.Ellipse
        {
            Width = 4,
            Height = 4,
            Fill = axisBrush
        };
        Canvas.SetLeft(centerDot, centerX - 2);
        Canvas.SetTop(centerDot, centerY - 2);
        GridCanvas.Children.Add(centerDot);
    }

    private void DrawLabels()
    {
        LabelsCanvas.Children.Clear();

        double width = LabelsCanvas.ActualWidth;
        double height = LabelsCanvas.ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 4;

        if (width <= 0 || height <= 0) return;

        var textBrush = FindResource("TextBrush") as Brush ?? Brushes.Gray;

        if (_lissajousMode)
        {
            // L label (top-left)
            var lLabel = new TextBlock
            {
                Text = "L",
                Foreground = textBrush,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(lLabel, centerX - radius * 0.707 - 12);
            Canvas.SetTop(lLabel, centerY - radius * 0.707 - 12);
            LabelsCanvas.Children.Add(lLabel);

            // R label (top-right)
            var rLabel = new TextBlock
            {
                Text = "R",
                Foreground = textBrush,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(rLabel, centerX + radius * 0.707 + 4);
            Canvas.SetTop(rLabel, centerY - radius * 0.707 - 12);
            LabelsCanvas.Children.Add(rLabel);

            // M label (top)
            var mLabel = new TextBlock
            {
                Text = "M",
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x66)),
                FontSize = 9
            };
            Canvas.SetLeft(mLabel, centerX + 4);
            Canvas.SetTop(mLabel, 4);
            LabelsCanvas.Children.Add(mLabel);

            // S label (right)
            var sLabel = new TextBlock
            {
                Text = "S",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                FontSize = 9
            };
            Canvas.SetLeft(sLabel, width - 14);
            Canvas.SetTop(sLabel, centerY - 6);
            LabelsCanvas.Children.Add(sLabel);
        }
        else
        {
            // Standard XY labels
            var leftLabel = new TextBlock { Text = "L", Foreground = textBrush, FontSize = 10 };
            Canvas.SetLeft(leftLabel, 4);
            Canvas.SetTop(leftLabel, centerY - 8);
            LabelsCanvas.Children.Add(leftLabel);

            var rightLabel = new TextBlock { Text = "R", Foreground = textBrush, FontSize = 10 };
            Canvas.SetLeft(rightLabel, width - 12);
            Canvas.SetTop(rightLabel, centerY - 8);
            LabelsCanvas.Children.Add(rightLabel);
        }
    }

    private void UpdateDisplay()
    {
        if (_bitmap == null || _pixelBuffer == null) return;
        if (LeftSamples == null || RightSamples == null) return;

        int count = Math.Min(LeftSamples.Length, RightSamples.Length);
        if (count == 0) return;

        // Apply decay to existing pixels
        ApplyDecay();

        int size = _bitmapWidth;
        double centerX = size / 2.0;
        double centerY = size / 2.0;
        double scale = (size / 2.0 - 4) * Zoom;

        Color color = PointColor;
        byte r = color.R;
        byte g = color.G;
        byte b = color.B;

        double sumLR = 0;
        double sumL2 = 0;
        double sumR2 = 0;

        for (int i = 0; i < count; i++)
        {
            float left = LeftSamples[i];
            float right = RightSamples[i];

            // Calculate correlation
            sumLR += left * right;
            sumL2 += left * left;
            sumR2 += right * right;

            double x, y;

            if (_lissajousMode)
            {
                // Lissajous: X = (L-R)/sqrt(2), Y = (L+R)/sqrt(2)
                // This rotates the display 45 degrees
                double mid = (left + right) / Math.Sqrt(2);
                double side = (left - right) / Math.Sqrt(2);
                x = centerX + side * scale;
                y = centerY - mid * scale;
            }
            else
            {
                // Standard XY: X = right, Y = left
                x = centerX + right * scale;
                y = centerY - left * scale;
            }

            int px = (int)Math.Clamp(x, 0, size - 1);
            int py = (int)Math.Clamp(y, 0, size - 1);

            float intensity = Math.Max(Math.Abs(left), Math.Abs(right));
            byte alpha = (byte)(128 + 127 * Math.Min(1.0, intensity * 2));

            SetPixelBlend(px, py, b, g, r, alpha);

            // Draw slightly larger point for visibility
            if (intensity > 0.1f)
            {
                SetPixelBlend(px - 1, py, b, g, r, (byte)(alpha / 2));
                SetPixelBlend(px + 1, py, b, g, r, (byte)(alpha / 2));
                SetPixelBlend(px, py - 1, b, g, r, (byte)(alpha / 2));
                SetPixelBlend(px, py + 1, b, g, r, (byte)(alpha / 2));
            }
        }

        // Calculate correlation coefficient
        double denominator = Math.Sqrt(sumL2 * sumR2);
        _currentCorrelation = denominator > 0 ? sumLR / denominator : 0;
        UpdateCorrelationDisplay();

        UpdateBitmap();
    }

    private void ApplyDecay()
    {
        if (_pixelBuffer == null) return;

        double decay = DecayFactor;
        int length = _pixelBuffer.Length;

        for (int i = 0; i < length; i += 4)
        {
            _pixelBuffer[i] = (byte)(_pixelBuffer[i] * decay);
            _pixelBuffer[i + 1] = (byte)(_pixelBuffer[i + 1] * decay);
            _pixelBuffer[i + 2] = (byte)(_pixelBuffer[i + 2] * decay);
            _pixelBuffer[i + 3] = (byte)(_pixelBuffer[i + 3] * decay);
        }
    }

    private void SetPixelBlend(int x, int y, byte b, byte g, byte r, byte a)
    {
        if (_pixelBuffer == null) return;
        if (x < 0 || x >= _bitmapWidth || y < 0 || y >= _bitmapHeight) return;

        int index = (y * _bitmapWidth + x) * 4;

        // Additive blend for glow effect
        _pixelBuffer[index] = (byte)Math.Min(255, _pixelBuffer[index] + b / 2);
        _pixelBuffer[index + 1] = (byte)Math.Min(255, _pixelBuffer[index + 1] + g / 2);
        _pixelBuffer[index + 2] = (byte)Math.Min(255, _pixelBuffer[index + 2] + r / 2);
        _pixelBuffer[index + 3] = (byte)Math.Min(255, _pixelBuffer[index + 3] + a / 2);
    }

    private void UpdateCorrelationDisplay()
    {
        string sign = _currentCorrelation >= 0 ? "+" : "";
        CorrelationText.Text = $"{sign}{_currentCorrelation:F2}";

        // Color based on correlation value
        if (_currentCorrelation > 0.5)
        {
            CorrelationText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x66)); // Green
        }
        else if (_currentCorrelation < -0.3)
        {
            CorrelationText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55)); // Red
        }
        else
        {
            CorrelationText.Foreground = FindResource("PointBrush") as Brush;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets stereo samples for display.
    /// </summary>
    public void SetSamples(float[] left, float[] right)
    {
        LeftSamples = left;
        RightSamples = right;
    }

    /// <summary>
    /// Clears the display.
    /// </summary>
    public void Clear()
    {
        ClearBitmap();
        _currentCorrelation = 0;
        UpdateCorrelationDisplay();
    }

    /// <summary>
    /// Gets the current correlation value.
    /// </summary>
    public double GetCorrelation() => _currentCorrelation;

    #endregion
}
