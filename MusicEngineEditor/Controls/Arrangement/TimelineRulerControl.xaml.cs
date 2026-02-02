// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Timeline ruler control for arrangement view showing bar numbers and beat subdivisions.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Arrangement;

/// <summary>
/// Control for displaying a timeline ruler with bar numbers and beat subdivisions.
/// </summary>
public partial class TimelineRulerControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(TimelineRulerControl),
            new PropertyMetadata(20.0, OnRulerPropertyChanged));

    public static readonly DependencyProperty TotalBarsProperty =
        DependencyProperty.Register(nameof(TotalBars), typeof(int), typeof(TimelineRulerControl),
            new PropertyMetadata(32, OnRulerPropertyChanged));

    public static readonly DependencyProperty BeatsPerBarProperty =
        DependencyProperty.Register(nameof(BeatsPerBar), typeof(int), typeof(TimelineRulerControl),
            new PropertyMetadata(4, OnRulerPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(TimelineRulerControl),
            new PropertyMetadata(0.0, OnRulerPropertyChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(TimelineRulerControl),
            new PropertyMetadata(0.0, OnPlayheadPositionChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the number of pixels per beat.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the total number of bars to display.
    /// </summary>
    public int TotalBars
    {
        get => (int)GetValue(TotalBarsProperty);
        set => SetValue(TotalBarsProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of beats per bar (time signature numerator).
    /// </summary>
    public int BeatsPerBar
    {
        get => (int)GetValue(BeatsPerBarProperty);
        set => SetValue(BeatsPerBarProperty, value);
    }

    /// <summary>
    /// Gets or sets the scroll offset in beats.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the current playhead position in beats.
    /// </summary>
    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the user clicks on the ruler to set playhead position.
    /// </summary>
    public event EventHandler<double>? PlayheadRequested;

    #endregion

    #region Fields

    private Line? _playheadLine;
    private bool _isDragging;
    private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0x00, 0xD9, 0xFF));
    private readonly SolidColorBrush _textBrush = new(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private readonly SolidColorBrush _secondaryTextBrush = new(Color.FromRgb(0x80, 0x80, 0x80));
    private readonly SolidColorBrush _majorTickBrush = new(Color.FromRgb(0x60, 0x60, 0x60));
    private readonly SolidColorBrush _minorTickBrush = new(Color.FromRgb(0x40, 0x40, 0x40));
    private readonly SolidColorBrush _beatTickBrush = new(Color.FromRgb(0x30, 0x30, 0x30));

    #endregion

    public TimelineRulerControl()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
    }

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawRuler();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawRuler();
    }

    private static void OnRulerPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineRulerControl control)
        {
            control.DrawRuler();
        }
    }

    private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineRulerControl control)
        {
            control.UpdatePlayhead();
        }
    }

    private void RulerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        RulerCanvas.CaptureMouse();
        UpdatePlayheadFromMouse(e.GetPosition(RulerCanvas).X);
        e.Handled = true;
    }

    private void RulerCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdatePlayheadFromMouse(e.GetPosition(RulerCanvas).X);
        }
    }

    private void RulerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        RulerCanvas.ReleaseMouseCapture();
    }

    #endregion

    #region Drawing

    /// <summary>
    /// Draws the timeline ruler with bar numbers and beat subdivisions.
    /// </summary>
    public void DrawRuler()
    {
        RulerCanvas.Children.Clear();

        var width = RulerCanvas.ActualWidth;
        var height = RulerCanvas.ActualHeight;

        if (width <= 0 || height <= 0 || PixelsPerBeat <= 0)
            return;

        var totalBeats = TotalBars * BeatsPerBar;
        var pixelsPerBar = PixelsPerBeat * BeatsPerBar;

        // Determine tick spacing based on zoom level
        var showSubdivisions = PixelsPerBeat >= 10;
        var showBeats = PixelsPerBeat >= 5;

        // Draw from scroll offset
        var startBar = (int)Math.Floor(ScrollOffset / BeatsPerBar);
        var endBar = (int)Math.Ceiling((ScrollOffset + width / PixelsPerBeat) / BeatsPerBar) + 1;
        endBar = Math.Min(endBar, TotalBars + 1);

        for (var bar = Math.Max(0, startBar); bar <= endBar; bar++)
        {
            var barBeat = bar * BeatsPerBar;
            var x = (barBeat - ScrollOffset) * PixelsPerBeat;

            if (x < -pixelsPerBar || x > width + pixelsPerBar)
                continue;

            // Draw bar line (major tick)
            var barLine = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = height,
                Stroke = _majorTickBrush,
                StrokeThickness = 1
            };
            RulerCanvas.Children.Add(barLine);

            // Draw bar number
            if (bar > 0 && x >= 0)
            {
                var barText = new TextBlock
                {
                    Text = bar.ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _textBrush
                };
                Canvas.SetLeft(barText, x + 4);
                Canvas.SetTop(barText, 2);
                RulerCanvas.Children.Add(barText);
            }

            // Draw beat ticks within this bar
            if (showBeats && bar < TotalBars)
            {
                for (var beat = 1; beat < BeatsPerBar; beat++)
                {
                    var beatX = x + beat * PixelsPerBeat;
                    if (beatX < 0 || beatX > width)
                        continue;

                    var beatLine = new Line
                    {
                        X1 = beatX,
                        X2 = beatX,
                        Y1 = height * 0.5,
                        Y2 = height,
                        Stroke = _minorTickBrush,
                        StrokeThickness = 1
                    };
                    RulerCanvas.Children.Add(beatLine);

                    // Draw beat number
                    if (PixelsPerBeat >= 15)
                    {
                        var beatText = new TextBlock
                        {
                            Text = $".{beat + 1}",
                            FontSize = 9,
                            Foreground = _secondaryTextBrush
                        };
                        Canvas.SetLeft(beatText, beatX + 2);
                        Canvas.SetTop(beatText, height * 0.35);
                        RulerCanvas.Children.Add(beatText);
                    }
                }
            }

            // Draw subdivision ticks (16th notes)
            if (showSubdivisions && bar < TotalBars)
            {
                for (var beat = 0; beat < BeatsPerBar; beat++)
                {
                    for (var sub = 1; sub < 4; sub++)
                    {
                        var subX = x + (beat + sub * 0.25) * PixelsPerBeat;
                        if (subX < 0 || subX > width)
                            continue;

                        var subLine = new Line
                        {
                            X1 = subX,
                            X2 = subX,
                            Y1 = height * 0.75,
                            Y2 = height,
                            Stroke = _beatTickBrush,
                            StrokeThickness = 1
                        };
                        RulerCanvas.Children.Add(subLine);
                    }
                }
            }
        }

        // Draw playhead
        DrawPlayhead();
    }

    private void DrawPlayhead()
    {
        if (_playheadLine != null)
        {
            RulerCanvas.Children.Remove(_playheadLine);
        }

        var x = (PlayheadPosition - ScrollOffset) * PixelsPerBeat;
        if (x < 0 || x > RulerCanvas.ActualWidth)
            return;

        _playheadLine = new Line
        {
            X1 = x,
            X2 = x,
            Y1 = 0,
            Y2 = RulerCanvas.ActualHeight,
            Stroke = _accentBrush,
            StrokeThickness = 2,
            IsHitTestVisible = false
        };
        RulerCanvas.Children.Add(_playheadLine);

        // Draw playhead triangle at top
        var triangle = new Polygon
        {
            Points = new PointCollection
            {
                new Point(x - 5, 0),
                new Point(x + 5, 0),
                new Point(x, 8)
            },
            Fill = _accentBrush,
            IsHitTestVisible = false
        };
        RulerCanvas.Children.Add(triangle);
    }

    private void UpdatePlayhead()
    {
        DrawPlayhead();
    }

    private void UpdatePlayheadFromMouse(double mouseX)
    {
        var beat = (mouseX / PixelsPerBeat) + ScrollOffset;
        beat = Math.Max(0, beat);

        // Snap to 16th note
        beat = Math.Round(beat * 4) / 4;

        PlayheadPosition = beat;
        PlayheadRequested?.Invoke(this, beat);
    }

    #endregion
}
