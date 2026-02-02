// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Electric Piano Synthesizer Editor control.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for EPianoSynthControl.xaml.
/// Provides a visual editor for the MusicEngine.Core.Synthesizers.EPianoSynth.
/// </summary>
public partial class EPianoSynthControl : UserControl
{
    private const int WaveformSampleCount = 256;

    /// <summary>
    /// Creates a new EPianoSynthControl.
    /// </summary>
    public EPianoSynthControl()
    {
        InitializeComponent();
    }

    private EPianoSynthViewModel? ViewModel => DataContext as EPianoSynthViewModel;

    private void EPianoSynthControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            RenderWaveform();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Update waveform when relevant parameters change
        if (e.PropertyName is nameof(EPianoSynthViewModel.TineBarMix) or
            nameof(EPianoSynthViewModel.BellAmount) or
            nameof(EPianoSynthViewModel.BarkAmount) or
            nameof(EPianoSynthViewModel.Drive) or
            nameof(EPianoSynthViewModel.SelectedModel))
        {
            RenderWaveform();
        }
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderWaveform();
    }

    /// <summary>
    /// Renders a representative electric piano waveform visualization.
    /// </summary>
    private void RenderWaveform()
    {
        if (WaveformCanvas.ActualWidth <= 0 || WaveformCanvas.ActualHeight <= 0)
            return;

        WaveformCanvas.Children.Clear();

        double canvasWidth = WaveformCanvas.ActualWidth;
        double canvasHeight = WaveformCanvas.ActualHeight;
        double centerY = canvasHeight / 2;
        double amplitude = canvasHeight * 0.4;

        // Draw center line
        var centerLine = new Line
        {
            X1 = 0,
            Y1 = centerY,
            X2 = canvasWidth,
            Y2 = centerY,
            Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            StrokeThickness = 1
        };
        WaveformCanvas.Children.Add(centerLine);

        // Generate electric piano waveform samples
        var samples = GenerateEPianoWaveform();

        // Create the waveform path
        var pathFigure = new PathFigure();
        bool started = false;

        for (int i = 0; i < samples.Length; i++)
        {
            double x = (double)i / (samples.Length - 1) * canvasWidth;
            double y = centerY - samples[i] * amplitude;

            if (!started)
            {
                pathFigure.StartPoint = new Point(x, y);
                started = true;
            }
            else
            {
                pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
            }
        }

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        // Create gradient brush (cyan to orange for Rhodes character)
        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0xD9, 0xFF), 0.0));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xB3, 0x47), 0.5));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0xD9, 0xFF), 1.0));

        var path = new Path
        {
            Data = pathGeometry,
            Stroke = gradientBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        WaveformCanvas.Children.Add(path);

        // Add subtle glow effect for "electric" feel
        var glowPath = new Path
        {
            Data = pathGeometry,
            Stroke = new SolidColorBrush(Color.FromArgb(40, 0x00, 0xD9, 0xFF)),
            StrokeThickness = 6,
            StrokeLineJoin = PenLineJoin.Round
        };
        WaveformCanvas.Children.Insert(0, glowPath);

        // Draw model indicator text
        var modelText = new TextBlock
        {
            Text = ViewModel?.SelectedModelName ?? "Rhodes Mark I",
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
            FontSize = 10
        };
        Canvas.SetLeft(modelText, 4);
        Canvas.SetTop(modelText, 4);
        WaveformCanvas.Children.Add(modelText);
    }

    /// <summary>
    /// Generates a representative electric piano waveform based on current settings.
    /// </summary>
    private float[] GenerateEPianoWaveform()
    {
        var samples = new float[WaveformSampleCount];

        float tineBarMix = ViewModel?.TineBarMix ?? 0.5f;
        float bellAmount = ViewModel?.BellAmount ?? 0.3f;
        float barkAmount = ViewModel?.BarkAmount ?? 0.3f;
        float drive = ViewModel?.Drive ?? 0f;

        for (int i = 0; i < WaveformSampleCount; i++)
        {
            float phase = (float)i / WaveformSampleCount * 2f * MathF.PI;

            // Tine component (primary tone) - complex waveform with asymmetry
            float tine = MathF.Sin(phase);
            tine += MathF.Sin(phase * 2f) * 0.3f;  // 2nd harmonic
            tine += MathF.Sin(phase * 3f) * 0.15f; // 3rd harmonic

            // Tone bar component (warmer, more fundamental)
            float toneBar = MathF.Sin(phase);
            toneBar += MathF.Sin(phase * 0.5f) * 0.2f; // Sub-harmonic warmth

            // Mix tine and tone bar
            float output = tine * tineBarMix + toneBar * (1f - tineBarMix);

            // Add bell overtones (inharmonic partials)
            if (bellAmount > 0)
            {
                float bell = MathF.Sin(phase * 4.5f) * 0.15f;  // Slightly detuned 4th harmonic
                bell += MathF.Sin(phase * 6.7f) * 0.1f;        // Slightly detuned 7th
                bell += MathF.Sin(phase * 9.2f) * 0.05f;       // High shimmer
                output += bell * bellAmount;
            }

            // Add bark (asymmetric clipping for growl)
            if (barkAmount > 0)
            {
                float bark = MathF.Sign(output) * MathF.Pow(MathF.Abs(output), 0.7f);
                output = output * (1f - barkAmount) + bark * barkAmount;
            }

            // Apply drive/saturation
            if (drive > 0)
            {
                float driveAmount = 1f + drive * 4f;
                output = MathF.Tanh(output * driveAmount) / MathF.Tanh(driveAmount);
            }

            // Normalize
            samples[i] = output * 0.7f;
        }

        return samples;
    }
}

/// <summary>
/// Converts a boolean to a color brush for status indication.
/// </summary>
public class EPianoBoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return new SolidColorBrush(boolValue
                ? Color.FromRgb(0x00, 0xFF, 0x88)  // Green for playing
                : Color.FromRgb(0x33, 0x33, 0x33)); // Gray for idle
        }
        return new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to a status text string.
/// </summary>
public class EPianoBoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "Playing" : "Idle";
        }
        return "Idle";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
