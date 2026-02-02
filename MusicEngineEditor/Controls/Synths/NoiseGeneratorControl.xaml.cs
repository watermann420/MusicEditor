// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Noise Generator control.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for NoiseGeneratorControl.xaml.
/// A noise generator with various noise types and filter.
/// </summary>
public partial class NoiseGeneratorControl : UserControl
{
    /// <summary>
    /// Creates a new NoiseGeneratorControl.
    /// </summary>
    public NoiseGeneratorControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawSpectrumShape();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Clean up event handlers
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DrawSpectrumShape();
    }

    /// <summary>
    /// Draws the spectrum shape visualization based on selected noise type.
    /// </summary>
    private void DrawSpectrumShape()
    {
        if (SpectrumCanvas == null) return;

        SpectrumCanvas.Children.Clear();

        var width = SpectrumCanvas.ActualWidth;
        var height = SpectrumCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            width = 200;
            height = 80;
        }

        // Determine noise type from DataContext or default to White
        int noiseType = 0; // Default: White

        // Draw spectrum line
        var points = new PointCollection();
        for (int x = 0; x < (int)width; x++)
        {
            double freq = (double)x / width; // 0 to 1 representing frequency
            double amplitude = GetSpectrumAmplitude(noiseType, freq);
            double y = height - (amplitude * (height - 10));
            points.Add(new Point(x, y));
        }

        var polyline = new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromRgb(0xBD, 0x93, 0xF9)),
            StrokeThickness = 2
        };

        SpectrumCanvas.Children.Add(polyline);

        // Draw filled area
        var filledPoints = new PointCollection(points);
        filledPoints.Add(new Point(width, height));
        filledPoints.Add(new Point(0, height));

        var polygon = new Polygon
        {
            Points = filledPoints,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, 0xBD, 0x93, 0xF9))
        };

        SpectrumCanvas.Children.Insert(0, polygon);

        // Draw frequency axis labels
        var lowFreqLabel = new TextBlock
        {
            Text = "20Hz",
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
            FontSize = 9
        };
        Canvas.SetLeft(lowFreqLabel, 2);
        Canvas.SetBottom(lowFreqLabel, 2);
        SpectrumCanvas.Children.Add(lowFreqLabel);

        var highFreqLabel = new TextBlock
        {
            Text = "20kHz",
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
            FontSize = 9
        };
        Canvas.SetRight(highFreqLabel, 2);
        Canvas.SetBottom(highFreqLabel, 2);
        SpectrumCanvas.Children.Add(highFreqLabel);
    }

    /// <summary>
    /// Gets the spectrum amplitude for a given noise type and frequency.
    /// </summary>
    private static double GetSpectrumAmplitude(int noiseType, double freq)
    {
        // Avoid division by zero
        if (freq < 0.01) freq = 0.01;

        return noiseType switch
        {
            0 => 0.7, // White: flat
            1 => 0.7 * Math.Pow(freq, -0.5), // Pink: 1/f
            2 => 0.7 * Math.Pow(freq, -1), // Brown: 1/f²
            3 => 0.7 * Math.Pow(freq, 0.5), // Blue: f
            4 => 0.7 * Math.Pow(freq, 1), // Violet: f²
            _ => 0.7
        };
    }
}
