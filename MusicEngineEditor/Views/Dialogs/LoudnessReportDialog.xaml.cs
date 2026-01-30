// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Loudness target preset types.
/// </summary>
public enum LoudnessTarget
{
    /// <summary>Streaming platforms (Spotify, Apple Music, etc.) - -14 LUFS.</summary>
    Streaming,
    /// <summary>Broadcast (TV, Radio) - -24 LUFS.</summary>
    Broadcast,
    /// <summary>CD/Physical media - -9 LUFS.</summary>
    CD,
    /// <summary>User-defined custom target.</summary>
    Custom
}

/// <summary>
/// Detailed loudness statistics from analysis.
/// </summary>
public class LoudnessStats
{
    /// <summary>Integrated loudness (program loudness) in LUFS.</summary>
    public double IntegratedLoudness { get; set; } = double.NegativeInfinity;

    /// <summary>Maximum short-term loudness in LUFS.</summary>
    public double ShortTermMax { get; set; } = double.NegativeInfinity;

    /// <summary>Maximum momentary loudness in LUFS.</summary>
    public double MomentaryMax { get; set; } = double.NegativeInfinity;

    /// <summary>Loudness Range (LRA) in LU.</summary>
    public double LoudnessRange { get; set; } = 0;

    /// <summary>True Peak in dBTP.</summary>
    public double TruePeak { get; set; } = double.NegativeInfinity;

    /// <summary>Loudness values over time for graphing (time in seconds, value in LUFS).</summary>
    public List<(double Time, double Loudness)> LoudnessHistory { get; set; } = [];

    /// <summary>Short-term loudness values over time.</summary>
    public List<(double Time, double Loudness)> ShortTermHistory { get; set; } = [];

    /// <summary>Duration of analyzed audio in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>Analysis timestamp.</summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.Now;

    /// <summary>Source file or description.</summary>
    public string Source { get; set; } = "Current Project";
}

/// <summary>
/// Dialog for displaying detailed LUFS loudness statistics and compliance reports.
/// </summary>
public partial class LoudnessReportDialog : Window
{
    private LoudnessStats _stats = new();
    private LoudnessTarget _currentTarget = LoudnessTarget.Streaming;
    private double _customTargetLufs = -14.0;

    /// <summary>
    /// Gets or sets the loudness statistics to display.
    /// </summary>
    public LoudnessStats Stats
    {
        get => _stats;
        set
        {
            _stats = value;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Gets or sets the current target loudness.
    /// </summary>
    public LoudnessTarget CurrentTarget
    {
        get => _currentTarget;
        set
        {
            _currentTarget = value;
            UpdateComplianceStatus();
            DrawLoudnessGraph();
        }
    }

    /// <summary>
    /// Gets the target LUFS value based on current preset.
    /// </summary>
    public double TargetLufs => _currentTarget switch
    {
        LoudnessTarget.Streaming => -14.0,
        LoudnessTarget.Broadcast => -24.0,
        LoudnessTarget.CD => -9.0,
        LoudnessTarget.Custom => _customTargetLufs,
        _ => -14.0
    };

    /// <summary>
    /// Event raised when analyze is requested.
    /// </summary>
    public event EventHandler? AnalyzeRequested;

    public LoudnessReportDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDisplay();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawLoudnessGraph();
    }

    /// <summary>
    /// Updates all display values from stats.
    /// </summary>
    public void UpdateDisplay()
    {
        // Update stat values
        IntegratedValue.Text = FormatLufs(_stats.IntegratedLoudness);
        ShortTermValue.Text = FormatLufs(_stats.ShortTermMax);
        MomentaryValue.Text = FormatLufs(_stats.MomentaryMax);
        LRAValue.Text = _stats.LoudnessRange.ToString("F1");
        TruePeakValue.Text = FormatDbtp(_stats.TruePeak);

        // Color code true peak
        TruePeakValue.Foreground = _stats.TruePeak switch
        {
            > -0.1 => (SolidColorBrush)Resources["ErrorBrush"],
            > -1.0 => (SolidColorBrush)Resources["WarningBrush"],
            _ => (SolidColorBrush)Resources["TextPrimaryBrush"]
        };

        UpdateComplianceStatus();
        DrawLoudnessGraph();
    }

    private void UpdateComplianceStatus()
    {
        double deviation = _stats.IntegratedLoudness - TargetLufs;
        string targetName = _currentTarget switch
        {
            LoudnessTarget.Streaming => "Streaming",
            LoudnessTarget.Broadcast => "Broadcast",
            LoudnessTarget.CD => "CD",
            LoudnessTarget.Custom => "Custom",
            _ => "Unknown"
        };

        // Determine compliance (within 1 LU is compliant, within 2 LU is warning)
        bool isCompliant = Math.Abs(deviation) <= 1.0;
        bool isWarning = Math.Abs(deviation) <= 2.0 && !isCompliant;

        if (isCompliant)
        {
            ComplianceIndicator.Fill = (SolidColorBrush)Resources["SuccessBrush"];
            ComplianceText.Text = $"Compliant with {targetName} target ({TargetLufs:F0} LUFS)";
        }
        else if (isWarning)
        {
            ComplianceIndicator.Fill = (SolidColorBrush)Resources["WarningBrush"];
            ComplianceText.Text = $"Close to {targetName} target ({TargetLufs:F0} LUFS)";
        }
        else
        {
            ComplianceIndicator.Fill = (SolidColorBrush)Resources["ErrorBrush"];
            string direction = deviation > 0 ? "louder" : "quieter";
            ComplianceText.Text = $"Not compliant with {targetName} - too {direction}";
        }

        DeviationText.Text = $" | Deviation: {(deviation >= 0 ? "+" : "")}{deviation:F1} LU";

        // Update target legend
        TargetLegend.Text = $"Target ({TargetLufs:F0} LUFS)";
    }

    private void DrawLoudnessGraph()
    {
        LoudnessGraph.Children.Clear();

        double width = LoudnessGraph.ActualWidth;
        double height = LoudnessGraph.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Draw grid lines
        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        for (int lufs = 0; lufs >= -60; lufs -= 10)
        {
            double y = MapLufsToY(lufs, height);
            var gridLine = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            LoudnessGraph.Children.Add(gridLine);

            // LUFS label
            var label = new TextBlock
            {
                Text = $"{lufs}",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A))
            };
            Canvas.SetLeft(label, 4);
            Canvas.SetTop(label, y - 8);
            LoudnessGraph.Children.Add(label);
        }

        // Draw target line
        double targetY = MapLufsToY(TargetLufs, height);
        var targetLine = new Line
        {
            X1 = 0,
            Y1 = targetY,
            X2 = width,
            Y2 = targetY,
            Stroke = (SolidColorBrush)Resources["AccentBrush"],
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 6, 3 }
        };
        LoudnessGraph.Children.Add(targetLine);

        // Draw loudness history
        if (_stats.LoudnessHistory.Count > 1)
        {
            var shortTermBrush = new SolidColorBrush(Color.FromArgb(128, 85, 170, 255));
            var integratedBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));

            // Draw short-term history (filled area)
            if (_stats.ShortTermHistory.Count > 1)
            {
                var shortTermGeometry = CreateAreaGeometry(_stats.ShortTermHistory, width, height);
                var shortTermPath = new System.Windows.Shapes.Path
                {
                    Data = shortTermGeometry,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 85, 170, 255)),
                    Stroke = shortTermBrush,
                    StrokeThickness = 1
                };
                LoudnessGraph.Children.Add(shortTermPath);
            }

            // Draw momentary loudness line
            var momentaryPoints = new PointCollection();
            double duration = _stats.Duration > 0 ? _stats.Duration : _stats.LoudnessHistory.Max(p => p.Time);

            foreach (var point in _stats.LoudnessHistory)
            {
                double x = (point.Time / duration) * width;
                double y = MapLufsToY(point.Loudness, height);
                momentaryPoints.Add(new Point(x, y));
            }

            if (momentaryPoints.Count > 1)
            {
                var polyline = new Polyline
                {
                    Points = momentaryPoints,
                    Stroke = integratedBrush,
                    StrokeThickness = 1.5,
                    StrokeLineJoin = PenLineJoin.Round
                };
                LoudnessGraph.Children.Add(polyline);
            }

            // Draw integrated loudness line
            double integratedY = MapLufsToY(_stats.IntegratedLoudness, height);
            var integratedLine = new Line
            {
                X1 = 0,
                Y1 = integratedY,
                X2 = width,
                Y2 = integratedY,
                Stroke = new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                StrokeThickness = 2
            };
            LoudnessGraph.Children.Add(integratedLine);
        }
    }

    private Geometry CreateAreaGeometry(List<(double Time, double Loudness)> points, double width, double height)
    {
        if (points.Count < 2) return Geometry.Empty;

        double duration = _stats.Duration > 0 ? _stats.Duration : points.Max(p => p.Time);
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            double firstX = (points[0].Time / duration) * width;
            double firstY = MapLufsToY(points[0].Loudness, height);

            ctx.BeginFigure(new Point(firstX, height), true, true);
            ctx.LineTo(new Point(firstX, firstY), false, false);

            foreach (var point in points)
            {
                double x = (point.Time / duration) * width;
                double y = MapLufsToY(point.Loudness, height);
                ctx.LineTo(new Point(x, y), true, false);
            }

            double lastX = (points[^1].Time / duration) * width;
            ctx.LineTo(new Point(lastX, height), false, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static double MapLufsToY(double lufs, double height)
    {
        // Map LUFS range (-60 to 0) to canvas height
        const double minLufs = -60.0;
        const double maxLufs = 0.0;
        double clamped = Math.Clamp(lufs, minLufs, maxLufs);
        return height - ((clamped - minLufs) / (maxLufs - minLufs)) * height;
    }

    private static string FormatLufs(double lufs)
    {
        if (double.IsNegativeInfinity(lufs) || double.IsNaN(lufs))
            return "---";
        return lufs.ToString("F1");
    }

    private static string FormatDbtp(double dbtp)
    {
        if (double.IsNegativeInfinity(dbtp) || double.IsNaN(dbtp))
            return "---";
        return dbtp.ToString("F1");
    }

    private void PresetChanged(object sender, RoutedEventArgs e)
    {
        if (StreamingPreset.IsChecked == true)
        {
            CurrentTarget = LoudnessTarget.Streaming;
            CustomTargetTextBox.IsEnabled = false;
        }
        else if (BroadcastPreset.IsChecked == true)
        {
            CurrentTarget = LoudnessTarget.Broadcast;
            CustomTargetTextBox.IsEnabled = false;
        }
        else if (CDPreset.IsChecked == true)
        {
            CurrentTarget = LoudnessTarget.CD;
            CustomTargetTextBox.IsEnabled = false;
        }
        else if (CustomPreset.IsChecked == true)
        {
            CurrentTarget = LoudnessTarget.Custom;
            CustomTargetTextBox.IsEnabled = true;
        }
    }

    private void CustomTarget_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(CustomTargetTextBox.Text, out double value))
        {
            _customTargetLufs = Math.Clamp(value, -60, 0);
            if (_currentTarget == LoudnessTarget.Custom)
            {
                UpdateComplianceStatus();
                DrawLoudnessGraph();
            }
        }
    }

    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Report (*.txt)|*.txt|CSV Report (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"LoudnessReport_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string extension = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
                string content = extension == ".csv" ? GenerateCsvReport() : GenerateTextReport();
                File.WriteAllText(dialog.FileName, content);

                MessageBox.Show($"Report exported successfully to:\n{dialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting report:\n{ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private string GenerateTextReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================");
        sb.AppendLine("    LOUDNESS ANALYSIS REPORT");
        sb.AppendLine("================================");
        sb.AppendLine();
        sb.AppendLine($"Source: {_stats.Source}");
        sb.AppendLine($"Analyzed: {_stats.AnalyzedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration: {TimeSpan.FromSeconds(_stats.Duration):mm\\:ss\\.fff}");
        sb.AppendLine();
        sb.AppendLine("--- Loudness Measurements ---");
        sb.AppendLine($"Integrated Loudness:  {FormatLufs(_stats.IntegratedLoudness)} LUFS");
        sb.AppendLine($"Short-Term Max:       {FormatLufs(_stats.ShortTermMax)} LUFS");
        sb.AppendLine($"Momentary Max:        {FormatLufs(_stats.MomentaryMax)} LUFS");
        sb.AppendLine($"Loudness Range (LRA): {_stats.LoudnessRange:F1} LU");
        sb.AppendLine($"True Peak:            {FormatDbtp(_stats.TruePeak)} dBTP");
        sb.AppendLine();
        sb.AppendLine("--- Target Compliance ---");
        sb.AppendLine($"Target: {CurrentTarget} ({TargetLufs:F0} LUFS)");
        double deviation = _stats.IntegratedLoudness - TargetLufs;
        sb.AppendLine($"Deviation: {(deviation >= 0 ? "+" : "")}{deviation:F1} LU");
        sb.AppendLine($"Status: {(Math.Abs(deviation) <= 1.0 ? "COMPLIANT" : Math.Abs(deviation) <= 2.0 ? "WARNING" : "NOT COMPLIANT")}");
        sb.AppendLine();
        sb.AppendLine("================================");
        sb.AppendLine("Generated by MusicEngineEditor");

        return sb.ToString();
    }

    private string GenerateCsvReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Metric,Value,Unit");
        sb.AppendLine($"Source,\"{_stats.Source}\",");
        sb.AppendLine($"Analyzed,{_stats.AnalyzedAt:yyyy-MM-dd HH:mm:ss},");
        sb.AppendLine($"Duration,{_stats.Duration:F3},seconds");
        sb.AppendLine($"Integrated Loudness,{_stats.IntegratedLoudness:F2},LUFS");
        sb.AppendLine($"Short-Term Max,{_stats.ShortTermMax:F2},LUFS");
        sb.AppendLine($"Momentary Max,{_stats.MomentaryMax:F2},LUFS");
        sb.AppendLine($"Loudness Range (LRA),{_stats.LoudnessRange:F2},LU");
        sb.AppendLine($"True Peak,{_stats.TruePeak:F2},dBTP");
        sb.AppendLine($"Target,{TargetLufs:F0},LUFS");
        sb.AppendLine($"Deviation,{_stats.IntegratedLoudness - TargetLufs:F2},LU");

        return sb.ToString();
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        AnalyzeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
