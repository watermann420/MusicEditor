// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the OPN/YM2612 Synthesizer Editor control.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for OPNSynthControl.xaml.
/// Provides a visual editor for the YM2612/OPN FM synthesizer with 4 operators,
/// 8 algorithms, LFO, SSG-EG, and classic Genesis/Mega Drive presets.
/// </summary>
public partial class OPNSynthControl : UserControl
{
    /// <summary>
    /// Converter for Color to Brush.
    /// </summary>
    public static ColorToBrushConverter ColorToBrushConverter { get; } = new();

    /// <summary>
    /// Converter for carrier/modulator text.
    /// </summary>
    public static OPNCarrierTextConverter CarrierTextConverter { get; } = new();

    /// <summary>
    /// Converter for carrier/modulator background color.
    /// </summary>
    public static OPNCarrierBackgroundConverter CarrierBackgroundConverter { get; } = new();

    /// <summary>
    /// Converter for algorithm radio button binding.
    /// </summary>
    public static OPNAlgorithmConverter AlgorithmConverter { get; } = new();

    /// <summary>
    /// Creates a new OPNSynthControl.
    /// </summary>
    public OPNSynthControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private OPNSynthViewModel? ViewModel => DataContext as OPNSynthViewModel;

    private void Operator_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is OPNOperatorViewModel operatorVm)
        {
            ViewModel?.SelectOperatorCommand.Execute(operatorVm);
        }
    }

    /// <summary>
    /// Called when the control is loaded to draw the initial algorithm diagram.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawAlgorithmDiagram();

        if (DataContext is OPNSynthViewModel vm)
        {
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OPNSynthViewModel vm)
        {
            vm.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OPNSynthViewModel.Algorithm) ||
            e.PropertyName == nameof(OPNSynthViewModel.Operators))
        {
            DrawAlgorithmDiagram();
        }
    }

    /// <summary>
    /// Draws the YM2612 algorithm diagram showing operator connections.
    /// </summary>
    private void DrawAlgorithmDiagram()
    {
        if (AlgorithmCanvas == null || ViewModel == null) return;

        AlgorithmCanvas.Children.Clear();

        double canvasWidth = AlgorithmCanvas.ActualWidth > 0 ? AlgorithmCanvas.ActualWidth : 240;
        double canvasHeight = AlgorithmCanvas.ActualHeight > 0 ? AlgorithmCanvas.ActualHeight : 120;

        // Operator colors matching YM2612 style (4 operators)
        var opColors = new Color[]
        {
            Color.FromRgb(0xFF, 0x6B, 0x6B),  // Op1 - Red
            Color.FromRgb(0xFF, 0xD9, 0x3D),  // Op2 - Yellow
            Color.FromRgb(0x6B, 0xFF, 0x6B),  // Op3 - Green
            Color.FromRgb(0x00, 0xD9, 0xFF),  // Op4 - Cyan
        };

        // Output position
        var outputPos = new Point(canvasWidth * 0.5, canvasHeight * 0.9);

        // Get algorithm-specific layout and connections
        var (opPositions, connections) = GetAlgorithmLayout(ViewModel.Algorithm, canvasWidth, canvasHeight);

        // Draw connections first (behind operators)
        foreach (var conn in connections)
        {
            if (conn.FromOp < 0 || conn.FromOp >= 4) continue;

            var fromPos = opPositions[conn.FromOp];
            Point toPos;

            if (conn.ToOp == -1)
            {
                // Connection to output
                toPos = outputPos;
            }
            else if (conn.ToOp >= 0 && conn.ToOp < 4)
            {
                toPos = opPositions[conn.ToOp];
            }
            else
            {
                continue;
            }

            // Draw connection line
            var line = new Line
            {
                X1 = fromPos.X,
                Y1 = fromPos.Y,
                X2 = toPos.X,
                Y2 = toPos.Y,
                Stroke = conn.IsFeedback
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x00))
                    : new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = conn.IsFeedback ? 2 : 1.5,
                StrokeDashArray = conn.IsFeedback ? new DoubleCollection { 4, 2 } : null
            };
            AlgorithmCanvas.Children.Add(line);

            // Draw arrowhead for non-output connections
            if (conn.ToOp != -1 && !conn.IsFeedback)
            {
                DrawArrowhead(fromPos, toPos, line.Stroke);
            }
        }

        // Draw operators
        for (int i = 0; i < 4; i++)
        {
            var pos = opPositions[i];
            var op = ViewModel.Operators.Count > i ? ViewModel.Operators[i] : null;
            bool isCarrier = op?.IsCarrier ?? false;
            bool isActive = op?.TotalLevel < 127;

            // Operator circle
            double radius = 16;
            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = isActive
                    ? new SolidColorBrush(Color.FromArgb(0x40, opColors[i].R, opColors[i].G, opColors[i].B))
                    : new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
                Stroke = new SolidColorBrush(opColors[i]),
                StrokeThickness = isCarrier ? 3 : 2
            };
            Canvas.SetLeft(ellipse, pos.X - radius);
            Canvas.SetTop(ellipse, pos.Y - radius);
            AlgorithmCanvas.Children.Add(ellipse);

            // Carrier indicator (outer dashed ring)
            if (isCarrier)
            {
                var carrierRing = new Ellipse
                {
                    Width = radius * 2 + 8,
                    Height = radius * 2 + 8,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 3, 2 }
                };
                Canvas.SetLeft(carrierRing, pos.X - radius - 4);
                Canvas.SetTop(carrierRing, pos.Y - radius - 4);
                AlgorithmCanvas.Children.Add(carrierRing);
            }

            // Operator number text
            var text = new TextBlock
            {
                Text = (i + 1).ToString(),
                Foreground = new SolidColorBrush(opColors[i]),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextAlignment = TextAlignment.Center
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, pos.X - text.DesiredSize.Width / 2);
            Canvas.SetTop(text, pos.Y - text.DesiredSize.Height / 2);
            AlgorithmCanvas.Children.Add(text);

            // Level indicator bar below operator
            if (op != null)
            {
                double barWidth = 26;
                double barHeight = 3;
                double barY = pos.Y + radius + 3;

                // Background bar
                var bgBar = new Rectangle
                {
                    Width = barWidth,
                    Height = barHeight,
                    Fill = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)),
                    RadiusX = 1.5,
                    RadiusY = 1.5
                };
                Canvas.SetLeft(bgBar, pos.X - barWidth / 2);
                Canvas.SetTop(bgBar, barY);
                AlgorithmCanvas.Children.Add(bgBar);

                // Level bar (inverse of TL - higher TL = lower level)
                double levelNormalized = 1.0 - (op.TotalLevel / 127.0);
                var levelBar = new Rectangle
                {
                    Width = barWidth * levelNormalized,
                    Height = barHeight,
                    Fill = new SolidColorBrush(opColors[i]),
                    RadiusX = 1.5,
                    RadiusY = 1.5
                };
                Canvas.SetLeft(levelBar, pos.X - barWidth / 2);
                Canvas.SetTop(levelBar, barY);
                AlgorithmCanvas.Children.Add(levelBar);
            }
        }

        // Draw output symbol
        var outputEllipse = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xAA, 0x55)),
            StrokeThickness = 2
        };
        Canvas.SetLeft(outputEllipse, outputPos.X - 7);
        Canvas.SetTop(outputEllipse, outputPos.Y - 7);
        AlgorithmCanvas.Children.Add(outputEllipse);

        // Output label
        var outputText = new TextBlock
        {
            Text = "OUT",
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            FontSize = 8
        };
        outputText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(outputText, outputPos.X + 10);
        Canvas.SetTop(outputText, outputPos.Y - outputText.DesiredSize.Height / 2);
        AlgorithmCanvas.Children.Add(outputText);

        // Draw feedback indicator if enabled
        if (ViewModel.Feedback > 0)
        {
            DrawFeedbackLoop(opPositions[0], opColors[0]);
        }
    }

    private void DrawFeedbackLoop(Point opPos, Color opColor)
    {
        double radius = 16;
        double loopRadius = 10;

        // Draw a curved feedback arrow around op1
        var path = new Path
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x00)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 }
        };

        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(opPos.X - radius - 2, opPos.Y - 5),
            IsClosed = false
        };

        // Create arc around the operator
        var arc = new ArcSegment
        {
            Point = new Point(opPos.X - radius - 2, opPos.Y + 5),
            Size = new Size(loopRadius, loopRadius),
            SweepDirection = SweepDirection.Counterclockwise,
            IsLargeArc = true
        };

        figure.Segments.Add(arc);
        geometry.Figures.Add(figure);
        path.Data = geometry;

        AlgorithmCanvas.Children.Add(path);

        // Small "FB" label
        var fbText = new TextBlock
        {
            Text = "FB",
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x00)),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };
        Canvas.SetLeft(fbText, opPos.X - radius - 18);
        Canvas.SetTop(fbText, opPos.Y - 5);
        AlgorithmCanvas.Children.Add(fbText);
    }

    /// <summary>
    /// Gets the operator positions and connections for a YM2612 algorithm.
    /// </summary>
    private (Point[] positions, List<AlgConnection> connections) GetAlgorithmLayout(int algorithm, double width, double height)
    {
        var positions = new Point[4];
        var connections = new List<AlgConnection>();

        // Standard vertical spacing
        double topY = height * 0.15;
        double midY = height * 0.45;
        double bottomY = height * 0.75;

        switch (algorithm)
        {
            case 0: // Serial: 4->3->2->1
                positions[3] = new Point(width * 0.5, topY);
                positions[2] = new Point(width * 0.5, topY + (bottomY - topY) / 3);
                positions[1] = new Point(width * 0.5, topY + (bottomY - topY) * 2 / 3);
                positions[0] = new Point(width * 0.5, bottomY);
                connections.Add(new AlgConnection(3, 2));
                connections.Add(new AlgConnection(2, 1));
                connections.Add(new AlgConnection(1, 0));
                connections.Add(new AlgConnection(0, -1)); // Op1 -> Output
                break;

            case 1: // 4->3->2, 1 parallel output
                positions[3] = new Point(width * 0.35, topY);
                positions[2] = new Point(width * 0.35, midY);
                positions[1] = new Point(width * 0.35, bottomY);
                positions[0] = new Point(width * 0.65, bottomY);
                connections.Add(new AlgConnection(3, 2));
                connections.Add(new AlgConnection(2, 1));
                connections.Add(new AlgConnection(1, -1));
                connections.Add(new AlgConnection(0, -1));
                break;

            case 2: // 4->3, 2->1
                positions[3] = new Point(width * 0.25, topY);
                positions[2] = new Point(width * 0.25, bottomY);
                positions[1] = new Point(width * 0.75, topY);
                positions[0] = new Point(width * 0.75, bottomY);
                connections.Add(new AlgConnection(3, 2));
                connections.Add(new AlgConnection(2, -1));
                connections.Add(new AlgConnection(1, 0));
                connections.Add(new AlgConnection(0, -1));
                break;

            case 3: // 4->3->(2+1)
                positions[3] = new Point(width * 0.5, topY);
                positions[2] = new Point(width * 0.5, midY);
                positions[1] = new Point(width * 0.3, bottomY);
                positions[0] = new Point(width * 0.7, bottomY);
                connections.Add(new AlgConnection(3, 2));
                connections.Add(new AlgConnection(2, 1));
                connections.Add(new AlgConnection(2, 0));
                connections.Add(new AlgConnection(1, -1));
                connections.Add(new AlgConnection(0, -1));
                break;

            case 4: // (4->3) + (2->1)
                positions[3] = new Point(width * 0.25, topY);
                positions[2] = new Point(width * 0.25, bottomY);
                positions[1] = new Point(width * 0.75, topY);
                positions[0] = new Point(width * 0.75, bottomY);
                connections.Add(new AlgConnection(3, 2));
                connections.Add(new AlgConnection(2, -1));
                connections.Add(new AlgConnection(1, 0));
                connections.Add(new AlgConnection(0, -1));
                break;

            case 5: // 4->(3+2+1)
                positions[3] = new Point(width * 0.5, topY);
                positions[2] = new Point(width * 0.2, bottomY);
                positions[1] = new Point(width * 0.5, bottomY);
                positions[0] = new Point(width * 0.8, bottomY);
                connections.Add(new AlgConnection(3, 2));
                connections.Add(new AlgConnection(3, 1));
                connections.Add(new AlgConnection(3, 0));
                connections.Add(new AlgConnection(2, -1));
                connections.Add(new AlgConnection(1, -1));
                connections.Add(new AlgConnection(0, -1));
                break;

            case 6: // (4->3) + 2 + 1
                positions[3] = new Point(width * 0.2, topY);
                positions[2] = new Point(width * 0.2, bottomY);
                positions[1] = new Point(width * 0.5, bottomY);
                positions[0] = new Point(width * 0.8, bottomY);
                connections.Add(new AlgConnection(3, 2));
                connections.Add(new AlgConnection(2, -1));
                connections.Add(new AlgConnection(1, -1));
                connections.Add(new AlgConnection(0, -1));
                break;

            case 7: // All parallel: 4+3+2+1
            default:
                positions[3] = new Point(width * 0.15, bottomY);
                positions[2] = new Point(width * 0.38, bottomY);
                positions[1] = new Point(width * 0.62, bottomY);
                positions[0] = new Point(width * 0.85, bottomY);
                connections.Add(new AlgConnection(3, -1));
                connections.Add(new AlgConnection(2, -1));
                connections.Add(new AlgConnection(1, -1));
                connections.Add(new AlgConnection(0, -1));
                break;
        }

        return (positions, connections);
    }

    private void DrawArrowhead(Point from, Point to, Brush stroke)
    {
        double headLength = 6;
        double headAngle = Math.PI / 6; // 30 degrees

        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);

        // Shorten the arrow to not overlap the operator circle
        double shortenBy = 18;
        double distance = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
        if (distance < shortenBy * 2) return;

        var arrowTip = new Point(
            to.X - shortenBy * Math.Cos(angle),
            to.Y - shortenBy * Math.Sin(angle)
        );

        var point1 = new Point(
            arrowTip.X - headLength * Math.Cos(angle - headAngle),
            arrowTip.Y - headLength * Math.Sin(angle - headAngle)
        );
        var point2 = new Point(
            arrowTip.X - headLength * Math.Cos(angle + headAngle),
            arrowTip.Y - headLength * Math.Sin(angle + headAngle)
        );

        var arrowHead = new Polygon
        {
            Points = new PointCollection { arrowTip, point1, point2 },
            Fill = stroke
        };
        AlgorithmCanvas.Children.Add(arrowHead);
    }

    private void AlgorithmCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawAlgorithmDiagram();
    }

    /// <summary>
    /// Internal connection representation for algorithm diagrams.
    /// </summary>
    private class AlgConnection
    {
        public int FromOp { get; }
        public int ToOp { get; }
        public bool IsFeedback { get; }

        public AlgConnection(int from, int to, bool feedback = false)
        {
            FromOp = from;
            ToOp = to;
            IsFeedback = feedback;
        }
    }
}

/// <summary>
/// Converts IsCarrier boolean to Carrier/Modulator text for OPN.
/// </summary>
public class OPNCarrierTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCarrier)
        {
            return isCarrier ? "[C]" : "[M]";
        }
        return "[M]";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts IsCarrier boolean to background brush for OPN operators.
/// </summary>
public class OPNCarrierBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush CarrierBrush =
        new(Color.FromRgb(0x1A, 0x3A, 0x1A)); // Dark green for carriers
    private static readonly SolidColorBrush ModulatorBrush =
        new(Color.FromRgb(0x1A, 0x1A, 0x2A)); // Dark blue for modulators

    static OPNCarrierBackgroundConverter()
    {
        CarrierBrush.Freeze();
        ModulatorBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCarrier)
        {
            return isCarrier ? CarrierBrush : ModulatorBrush;
        }
        return ModulatorBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for algorithm radio button IsChecked binding.
/// </summary>
public class OPNAlgorithmConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int algorithm && parameter is string paramStr && int.TryParse(paramStr, out int param))
        {
            return algorithm == param;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr && int.TryParse(paramStr, out int param))
        {
            return param;
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}
