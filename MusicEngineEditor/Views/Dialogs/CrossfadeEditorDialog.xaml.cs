// MusicEngineEditor - Crossfade Editor Dialog
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core.Clips;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Information about a clip in a crossfade.
/// </summary>
public class CrossfadeClipInfo
{
    /// <summary>Clip identifier.</summary>
    public Guid ClipId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "Clip";

    /// <summary>Clip start position in beats.</summary>
    public double StartPosition { get; set; }

    /// <summary>Clip end position in beats.</summary>
    public double EndPosition { get; set; }

    /// <summary>Clip color for visualization.</summary>
    public Color Color { get; set; } = Colors.Gray;
}

/// <summary>
/// Result of crossfade editing.
/// </summary>
public class CrossfadeEditResult
{
    /// <summary>Crossfade curve type.</summary>
    public CrossfadeType CurveType { get; set; } = CrossfadeType.EqualPower;

    /// <summary>Crossfade length in milliseconds.</summary>
    public double LengthMs { get; set; } = 500;

    /// <summary>Custom fade out tension (-1 to 1).</summary>
    public double FadeOutTension { get; set; } = 0;

    /// <summary>Custom fade in tension (-1 to 1).</summary>
    public double FadeInTension { get; set; } = 0;
}

/// <summary>
/// Dialog for detailed crossfade curve editing between two overlapping clips.
/// </summary>
public partial class CrossfadeEditorDialog : Window
{
    private CrossfadeClipInfo? _fadeInClip;
    private CrossfadeClipInfo? _fadeOutClip;
    private CrossfadeType _curveType = CrossfadeType.EqualPower;
    private double _lengthMs = 500;
    private double _fadeOutTension = 0;
    private double _fadeInTension = 0;
    private bool _isUpdating = false;

    /// <summary>
    /// Gets or sets the fade-in (incoming) clip info.
    /// </summary>
    public CrossfadeClipInfo? FadeInClip
    {
        get => _fadeInClip;
        set
        {
            _fadeInClip = value;
            UpdateClipDisplay();
        }
    }

    /// <summary>
    /// Gets or sets the fade-out (outgoing) clip info.
    /// </summary>
    public CrossfadeClipInfo? FadeOutClip
    {
        get => _fadeOutClip;
        set
        {
            _fadeOutClip = value;
            UpdateClipDisplay();
        }
    }

    /// <summary>
    /// Gets or sets the crossfade length in milliseconds.
    /// </summary>
    public double CrossfadeLength
    {
        get => _lengthMs;
        set
        {
            _lengthMs = Math.Clamp(value, 10, 5000);
            if (!_isUpdating)
            {
                _isUpdating = true;
                LengthTextBox.Text = ((int)_lengthMs).ToString();
                LengthSlider.Value = _lengthMs;
                _isUpdating = false;
            }
            DrawCrossfade();
        }
    }

    /// <summary>
    /// Gets or sets the curve type.
    /// </summary>
    public CrossfadeType CurveType
    {
        get => _curveType;
        set
        {
            _curveType = value;
            UpdateCurveSelection();
            DrawCrossfade();
        }
    }

    /// <summary>
    /// Gets the edit result after dialog closes.
    /// </summary>
    public CrossfadeEditResult Result { get; private set; } = new();

    /// <summary>
    /// Event raised when preview is requested.
    /// </summary>
    public event EventHandler<CrossfadeEditResult>? PreviewRequested;

    public CrossfadeEditorDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateClipDisplay();
        UpdateCurveSelection();
        DrawCrossfade();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawCrossfade();
    }

    private void UpdateClipDisplay()
    {
        if (_fadeOutClip != null)
        {
            FadeOutClipName.Text = $"{_fadeOutClip.Name} (Fade Out)";
        }

        if (_fadeInClip != null)
        {
            FadeInClipName.Text = $"{_fadeInClip.Name} (Fade In)";
        }
    }

    private void UpdateCurveSelection()
    {
        _isUpdating = true;

        LinearCurve.IsChecked = _curveType == CrossfadeType.Linear;
        EqualPowerCurve.IsChecked = _curveType == CrossfadeType.EqualPower;
        SCurveCurve.IsChecked = _curveType == CrossfadeType.SCurve;
        LogarithmicCurve.IsChecked = _curveType == CrossfadeType.Logarithmic;
        CustomCurve.IsChecked = false;

        CustomControlsPanel.Visibility = CustomCurve.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        _isUpdating = false;
    }

    private void DrawCrossfade()
    {
        CrossfadeCanvas.Children.Clear();

        double width = CrossfadeCanvas.ActualWidth;
        double height = CrossfadeCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double margin = 20;
        double graphWidth = width - 2 * margin;
        double graphHeight = height - 2 * margin;

        // Draw grid
        DrawGrid(margin, graphWidth, graphHeight);

        // Draw waveform representations
        DrawWaveformRepresentations(margin, graphWidth, graphHeight);

        // Draw fade curves
        DrawFadeCurves(margin, graphWidth, graphHeight);

        // Draw combined level curve
        DrawCombinedCurve(margin, graphWidth, graphHeight);
    }

    private void DrawGrid(double margin, double graphWidth, double graphHeight)
    {
        var gridBrush = new SolidColorBrush(Color.FromRgb(0x39, 0x3B, 0x40));

        // Vertical center line
        var centerLine = new Line
        {
            X1 = margin + graphWidth / 2,
            Y1 = margin,
            X2 = margin + graphWidth / 2,
            Y2 = margin + graphHeight,
            Stroke = gridBrush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        CrossfadeCanvas.Children.Add(centerLine);

        // Horizontal lines at 0%, 50%, 100%
        for (double level = 0; level <= 1; level += 0.5)
        {
            double y = margin + graphHeight - (level * graphHeight);
            var line = new Line
            {
                X1 = margin,
                Y1 = y,
                X2 = margin + graphWidth,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            CrossfadeCanvas.Children.Add(line);

            // Level label
            var label = new TextBlock
            {
                Text = $"{(int)(level * 100)}%",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A))
            };
            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, y - 8);
            CrossfadeCanvas.Children.Add(label);
        }
    }

    private void DrawWaveformRepresentations(double margin, double graphWidth, double graphHeight)
    {
        // Draw simplified waveform representations for context
        var fadeOutBrush = new SolidColorBrush(Color.FromArgb(60, 255, 107, 107));
        var fadeInBrush = new SolidColorBrush(Color.FromArgb(60, 76, 175, 80));

        // Fade out clip (left portion visible, fading)
        var fadeOutRect = new Rectangle
        {
            Width = graphWidth / 2 + graphWidth * 0.2,
            Height = graphHeight * 0.5,
            Fill = fadeOutBrush,
            RadiusX = 4,
            RadiusY = 4
        };
        Canvas.SetLeft(fadeOutRect, margin - graphWidth * 0.1);
        Canvas.SetTop(fadeOutRect, margin + graphHeight * 0.25);
        CrossfadeCanvas.Children.Add(fadeOutRect);

        // Fade in clip (right portion visible, fading)
        var fadeInRect = new Rectangle
        {
            Width = graphWidth / 2 + graphWidth * 0.2,
            Height = graphHeight * 0.5,
            Fill = fadeInBrush,
            RadiusX = 4,
            RadiusY = 4
        };
        Canvas.SetLeft(fadeInRect, margin + graphWidth / 2 - graphWidth * 0.1);
        Canvas.SetTop(fadeInRect, margin + graphHeight * 0.25);
        CrossfadeCanvas.Children.Add(fadeInRect);
    }

    private void DrawFadeCurves(double margin, double graphWidth, double graphHeight)
    {
        // Fade out curve (red)
        var fadeOutPoints = new PointCollection();
        var fadeInPoints = new PointCollection();

        const int steps = 100;
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            double x = margin + t * graphWidth;

            double fadeOutLevel = CalculateFadeOutLevel(t);
            double fadeInLevel = CalculateFadeInLevel(t);

            double yOut = margin + graphHeight - (fadeOutLevel * graphHeight);
            double yIn = margin + graphHeight - (fadeInLevel * graphHeight);

            fadeOutPoints.Add(new Point(x, yOut));
            fadeInPoints.Add(new Point(x, yIn));
        }

        // Draw fade out curve
        var fadeOutLine = new Polyline
        {
            Points = fadeOutPoints,
            Stroke = (SolidColorBrush)Resources["FadeOutBrush"],
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        CrossfadeCanvas.Children.Add(fadeOutLine);

        // Draw fade in curve
        var fadeInLine = new Polyline
        {
            Points = fadeInPoints,
            Stroke = (SolidColorBrush)Resources["FadeInBrush"],
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        CrossfadeCanvas.Children.Add(fadeInLine);
    }

    private void DrawCombinedCurve(double margin, double graphWidth, double graphHeight)
    {
        // Combined level curve (blue, showing total energy)
        var combinedPoints = new PointCollection();

        const int steps = 100;
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            double x = margin + t * graphWidth;

            double fadeOutLevel = CalculateFadeOutLevel(t);
            double fadeInLevel = CalculateFadeInLevel(t);

            // For equal power crossfade, this should be roughly constant
            // Combined level = sqrt(fadeOut^2 + fadeIn^2)
            double combined = Math.Sqrt(fadeOutLevel * fadeOutLevel + fadeInLevel * fadeInLevel);
            combined = Math.Min(combined, 1.0); // Clamp

            double y = margin + graphHeight - (combined * graphHeight);
            combinedPoints.Add(new Point(x, y));
        }

        var combinedLine = new Polyline
        {
            Points = combinedPoints,
            Stroke = (SolidColorBrush)Resources["AccentBrush"],
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            StrokeLineJoin = PenLineJoin.Round
        };
        CrossfadeCanvas.Children.Add(combinedLine);
    }

    private double CalculateFadeOutLevel(double t)
    {
        if (CustomCurve.IsChecked == true)
        {
            return CalculateCustomCurve(1.0 - t, _fadeOutTension);
        }

        return CrossfadeProcessor.CalculateOutgoingGain(t, _curveType);
    }

    private double CalculateFadeInLevel(double t)
    {
        if (CustomCurve.IsChecked == true)
        {
            return CalculateCustomCurve(t, _fadeInTension);
        }

        return CrossfadeProcessor.CalculateIncomingGain(t, _curveType);
    }

    private static double CalculateCustomCurve(double t, double tension)
    {
        // Custom bezier-like curve with tension control
        // tension < 0: more curved (slow start, fast end)
        // tension > 0: more linear (even rate)
        // tension = 0: equal power approximation

        if (Math.Abs(tension) < 0.01)
        {
            // Equal power
            return Math.Sin(t * Math.PI / 2.0);
        }
        else if (tension > 0)
        {
            // More linear (blend toward linear)
            double linear = t;
            double equalPower = Math.Sin(t * Math.PI / 2.0);
            return linear * tension + equalPower * (1 - tension);
        }
        else
        {
            // More curved (exponential-like)
            double absT = Math.Abs(tension);
            double exp = Math.Pow(t, 1.0 + absT);
            double equalPower = Math.Sin(t * Math.PI / 2.0);
            return exp * absT + equalPower * (1 - absT);
        }
    }

    private void CurveType_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        if (LinearCurve.IsChecked == true)
            _curveType = CrossfadeType.Linear;
        else if (EqualPowerCurve.IsChecked == true)
            _curveType = CrossfadeType.EqualPower;
        else if (SCurveCurve.IsChecked == true)
            _curveType = CrossfadeType.SCurve;
        else if (LogarithmicCurve.IsChecked == true)
            _curveType = CrossfadeType.Logarithmic;

        CustomControlsPanel.Visibility = CustomCurve.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        DrawCrossfade();
    }

    private void LengthTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (double.TryParse(LengthTextBox.Text, out double value))
        {
            _isUpdating = true;
            _lengthMs = Math.Clamp(value, 10, 5000);
            LengthSlider.Value = _lengthMs;
            _isUpdating = false;
        }
    }

    private void LengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        _isUpdating = true;
        _lengthMs = e.NewValue;
        LengthTextBox.Text = ((int)_lengthMs).ToString();
        _isUpdating = false;
    }

    private void CustomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _fadeOutTension = FadeOutTensionSlider.Value;
        _fadeInTension = FadeInTensionSlider.Value;
        DrawCrossfade();
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        Result = new CrossfadeEditResult
        {
            CurveType = _curveType,
            LengthMs = _lengthMs,
            FadeOutTension = _fadeOutTension,
            FadeInTension = _fadeInTension
        };

        PreviewRequested?.Invoke(this, Result);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _curveType = CrossfadeType.EqualPower;
        _lengthMs = 500;
        _fadeOutTension = 0;
        _fadeInTension = 0;

        EqualPowerCurve.IsChecked = true;
        LengthSlider.Value = 500;
        LengthTextBox.Text = "500";
        FadeOutTensionSlider.Value = 0;
        FadeInTensionSlider.Value = 0;
        CustomControlsPanel.Visibility = Visibility.Collapsed;

        DrawCrossfade();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Result = new CrossfadeEditResult
        {
            CurveType = _curveType,
            LengthMs = _lengthMs,
            FadeOutTension = _fadeOutTension,
            FadeInTension = _fadeInTension
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
