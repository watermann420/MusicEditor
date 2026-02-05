// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Main arrangement control combining timeline, track headers, and clip canvas.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Arrangement;

/// <summary>
/// Represents a track in the arrangement.
/// </summary>
public class ArrangementTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Track";
    public Color Color { get; set; } = Color.FromRgb(0x00, 0xD9, 0xFF);
    public bool IsMuted { get; set; }
    public bool IsSolo { get; set; }
    public bool IsCollapsed { get; set; }
    public int Index { get; set; }
    public double Height { get; set; } = 60;
}

/// <summary>
/// Main arrangement control combining timeline ruler, track headers, and clips canvas.
/// </summary>
public partial class ArrangementControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(ArrangementControl),
            new PropertyMetadata(20.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TotalBarsProperty =
        DependencyProperty.Register(nameof(TotalBars), typeof(int), typeof(ArrangementControl),
            new PropertyMetadata(64, OnLayoutPropertyChanged));

    public static readonly DependencyProperty BeatsPerBarProperty =
        DependencyProperty.Register(nameof(BeatsPerBar), typeof(int), typeof(ArrangementControl),
            new PropertyMetadata(4, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(ArrangementControl),
            new PropertyMetadata(0.0, OnScrollOffsetChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(ArrangementControl),
            new PropertyMetadata(0.0, OnPlayheadPositionChanged));

    public static readonly DependencyProperty TracksSourceProperty =
        DependencyProperty.Register(nameof(TracksSource), typeof(ObservableCollection<ArrangementTrack>), typeof(ArrangementControl),
            new PropertyMetadata(null, OnTracksSourceChanged));

    public static readonly DependencyProperty LoopStartProperty =
        DependencyProperty.Register(nameof(LoopStart), typeof(double), typeof(ArrangementControl),
            new PropertyMetadata(-1.0, OnLoopPropertyChanged));

    public static readonly DependencyProperty LoopEndProperty =
        DependencyProperty.Register(nameof(LoopEnd), typeof(double), typeof(ArrangementControl),
            new PropertyMetadata(-1.0, OnLoopPropertyChanged));

    public static readonly DependencyProperty IsLoopEnabledProperty =
        DependencyProperty.Register(nameof(IsLoopEnabled), typeof(bool), typeof(ArrangementControl),
            new PropertyMetadata(false, OnLoopPropertyChanged));

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
    /// Gets or sets the total number of bars.
    /// </summary>
    public int TotalBars
    {
        get => (int)GetValue(TotalBarsProperty);
        set => SetValue(TotalBarsProperty, value);
    }

    /// <summary>
    /// Gets or sets the beats per bar (time signature numerator).
    /// </summary>
    public int BeatsPerBar
    {
        get => (int)GetValue(BeatsPerBarProperty);
        set => SetValue(BeatsPerBarProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal scroll offset in beats.
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

    /// <summary>
    /// Gets or sets the tracks source collection.
    /// </summary>
    public ObservableCollection<ArrangementTrack>? TracksSource
    {
        get => (ObservableCollection<ArrangementTrack>?)GetValue(TracksSourceProperty);
        set => SetValue(TracksSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the loop start position in beats (-1 for no loop).
    /// </summary>
    public double LoopStart
    {
        get => (double)GetValue(LoopStartProperty);
        set => SetValue(LoopStartProperty, value);
    }

    /// <summary>
    /// Gets or sets the loop end position in beats (-1 for no loop).
    /// </summary>
    public double LoopEnd
    {
        get => (double)GetValue(LoopEndProperty);
        set => SetValue(LoopEndProperty, value);
    }

    /// <summary>
    /// Gets or sets whether looping is enabled.
    /// </summary>
    public bool IsLoopEnabled
    {
        get => (bool)GetValue(IsLoopEnabledProperty);
        set => SetValue(IsLoopEnabledProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the playhead position should change.
    /// </summary>
    public event EventHandler<double>? SeekRequested;

    /// <summary>
    /// Event raised when a track is selected.
    /// </summary>
    public event EventHandler<ArrangementTrack>? TrackSelected;

    /// <summary>
    /// Event raised when a clip is selected.
    /// </summary>
#pragma warning disable CS0067
    public event EventHandler<UIElement>? ClipSelected;
#pragma warning restore CS0067

    /// <summary>
    /// Event raised when a MIDI clip should be added.
    /// </summary>
    public event EventHandler<(double beat, int trackIndex)>? AddMidiClipRequested;

    /// <summary>
    /// Event raised when an audio clip should be added.
    /// </summary>
    public event EventHandler<(double beat, int trackIndex)>? AddAudioClipRequested;

    #endregion

    #region Fields

    private readonly Dictionary<Guid, TrackHeaderControl> _trackHeaders = new();
    private readonly Dictionary<Guid, UIElement> _clipElements = new();
    private readonly List<Line> _gridLines = new();

    private Point _contextMenuPosition;
    private bool _isDraggingPlayhead;

    private static readonly SolidColorBrush _majorGridBrush = CreateFrozenBrush(Color.FromRgb(0x30, 0x30, 0x30));
    private static readonly SolidColorBrush _minorGridBrush = CreateFrozenBrush(Color.FromRgb(0x20, 0x20, 0x20));
    private static readonly SolidColorBrush _accentBrush = CreateFrozenBrush(Color.FromRgb(0x00, 0xD9, 0xFF));

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    #endregion

    public ArrangementControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #region Property Changed Callbacks

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArrangementControl control)
        {
            control.RefreshLayout();
        }
    }

    private static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArrangementControl control)
        {
            control.UpdateScrollPosition();
        }
    }

    private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArrangementControl control)
        {
            control.UpdatePlayhead();
        }
    }

    private static void OnTracksSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArrangementControl control)
        {
            if (e.OldValue is ObservableCollection<ArrangementTrack> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnTracksCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<ArrangementTrack> newCollection)
            {
                newCollection.CollectionChanged += control.OnTracksCollectionChanged;
            }

            control.RefreshTracks();
        }
    }

    private static void OnLoopPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArrangementControl control)
        {
            control.UpdateLoopRegion();
        }
    }

    private void OnTracksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshTracks();
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshLayout();
        RefreshTracks();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshLayout();
    }

    private void TimelineRuler_PlayheadRequested(object? sender, double position)
    {
        PlayheadPosition = position;
        SeekRequested?.Invoke(this, position);
    }

    private void ClipsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Sync track header scroll with clips scroll
        TrackHeaderScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);

        // Update scroll offset in beats
        if (PixelsPerBeat > 0)
        {
            ScrollOffset = e.HorizontalOffset / PixelsPerBeat;
        }
    }

    private void ClipsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click to seek
        if (e.ClickCount == 2)
        {
            var beat = PositionToBeats(e.GetPosition(ClipsCanvas).X);
            PlayheadPosition = beat;
            SeekRequested?.Invoke(this, beat);
            return;
        }

        // Single click in empty area - deselect
        e.Handled = true;
    }

    private void ClipsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPlayhead = false;
    }

    private void ClipsCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingPlayhead && e.LeftButton == MouseButtonState.Pressed)
        {
            var beat = PositionToBeats(e.GetPosition(ClipsCanvas).X);
            beat = Math.Max(0, beat);
            PlayheadPosition = beat;
        }
    }

    private void ClipsCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuPosition = e.GetPosition(ClipsCanvas);
    }

    private void AddMidiClip_Click(object sender, RoutedEventArgs e)
    {
        var beat = PositionToBeats(_contextMenuPosition.X);
        var trackIndex = GetTrackIndexAtY(_contextMenuPosition.Y);
        beat = Math.Round(beat * 4) / 4; // Snap to quarter note
        AddMidiClipRequested?.Invoke(this, (beat, trackIndex));
    }

    private void AddAudioClip_Click(object sender, RoutedEventArgs e)
    {
        var beat = PositionToBeats(_contextMenuPosition.X);
        var trackIndex = GetTrackIndexAtY(_contextMenuPosition.Y);
        beat = Math.Round(beat * 4) / 4;
        AddAudioClipRequested?.Invoke(this, (beat, trackIndex));
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        // Paste logic would be handled by parent
    }

    private void SetLoopRegion_Click(object sender, RoutedEventArgs e)
    {
        var beat = PositionToBeats(_contextMenuPosition.X);
        beat = Math.Round(beat * 4) / 4;

        // Set a default 4-bar loop
        LoopStart = beat;
        LoopEnd = beat + BeatsPerBar * 4;
        IsLoopEnabled = true;
    }

    private void ClearLoopRegion_Click(object sender, RoutedEventArgs e)
    {
        IsLoopEnabled = false;
        LoopStart = -1;
        LoopEnd = -1;
    }

    #endregion

    #region Layout and Rendering

    /// <summary>
    /// Refreshes the entire layout.
    /// </summary>
    public void RefreshLayout()
    {
        UpdateCanvasSize();
        DrawGrid();
        UpdatePlayhead();
        UpdateLoopRegion();
        RefreshClipPositions();
    }

    private void UpdateCanvasSize()
    {
        var totalBeats = TotalBars * BeatsPerBar;
        var totalWidth = totalBeats * PixelsPerBeat;

        // Calculate total height from tracks
        var totalHeight = 0.0;
        if (TracksSource != null)
        {
            foreach (var track in TracksSource)
            {
                totalHeight += track.IsCollapsed ? 30 : track.Height;
            }
        }
        totalHeight = Math.Max(totalHeight, ClipsScrollViewer.ActualHeight - 20);

        ClipsCanvas.Width = Math.Max(totalWidth, ClipsScrollViewer.ActualWidth - 20);
        ClipsCanvas.Height = totalHeight;
    }

    private void DrawGrid()
    {
        // Remove old grid lines
        foreach (var line in _gridLines)
        {
            ClipsCanvas.Children.Remove(line);
        }
        _gridLines.Clear();

        var width = ClipsCanvas.Width;
        var height = ClipsCanvas.Height;

        if (width <= 0 || height <= 0 || PixelsPerBeat <= 0)
            return;

        var totalBeats = TotalBars * BeatsPerBar;

        // Draw vertical grid lines (bars and beats)
        for (var bar = 0; bar <= TotalBars; bar++)
        {
            var barBeat = bar * BeatsPerBar;
            var x = barBeat * PixelsPerBeat;

            // Bar line
            var barLine = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = height,
                Stroke = _majorGridBrush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            _gridLines.Add(barLine);
            ClipsCanvas.Children.Insert(0, barLine);

            // Beat lines within bar
            if (PixelsPerBeat >= 8 && bar < TotalBars)
            {
                for (var beat = 1; beat < BeatsPerBar; beat++)
                {
                    var beatX = (barBeat + beat) * PixelsPerBeat;
                    var beatLine = new Line
                    {
                        X1 = beatX,
                        X2 = beatX,
                        Y1 = 0,
                        Y2 = height,
                        Stroke = _minorGridBrush,
                        StrokeThickness = 1,
                        IsHitTestVisible = false
                    };
                    _gridLines.Add(beatLine);
                    ClipsCanvas.Children.Insert(0, beatLine);
                }
            }
        }

        // Draw horizontal lines between tracks
        var trackY = 0.0;
        if (TracksSource != null)
        {
            foreach (var track in TracksSource)
            {
                var trackHeight = track.IsCollapsed ? 30 : track.Height;
                trackY += trackHeight;

                var trackLine = new Line
                {
                    X1 = 0,
                    X2 = width,
                    Y1 = trackY,
                    Y2 = trackY,
                    Stroke = _majorGridBrush,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                _gridLines.Add(trackLine);
                ClipsCanvas.Children.Insert(0, trackLine);
            }
        }
    }

    private void UpdatePlayhead()
    {
        var x = (PlayheadPosition - ScrollOffset) * PixelsPerBeat;

        // Position playhead line
        Canvas.SetLeft(PlayheadLine, x - 1);
        PlayheadLine.Height = ClipsCanvas.Height;

        // Position playhead triangle
        Canvas.SetLeft(PlayheadTriangle, x - 5);
        Canvas.SetTop(PlayheadTriangle, 0);

        // Ensure playhead is on top
        System.Windows.Controls.Panel.SetZIndex(PlayheadLine, 1000);
        System.Windows.Controls.Panel.SetZIndex(PlayheadTriangle, 1001);
    }

    private void UpdateLoopRegion()
    {
        if (!IsLoopEnabled || LoopStart < 0 || LoopEnd <= LoopStart)
        {
            LoopRegionRect.Visibility = Visibility.Collapsed;
            return;
        }

        var startX = (LoopStart - ScrollOffset) * PixelsPerBeat;
        var endX = (LoopEnd - ScrollOffset) * PixelsPerBeat;
        var width = endX - startX;

        if (width <= 0)
        {
            LoopRegionRect.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(LoopRegionRect, startX);
        Canvas.SetTop(LoopRegionRect, 0);
        LoopRegionRect.Width = width;
        LoopRegionRect.Height = ClipsCanvas.Height;
        LoopRegionRect.Visibility = Visibility.Visible;

        System.Windows.Controls.Panel.SetZIndex(LoopRegionRect, -1);
    }

    private void UpdateScrollPosition()
    {
        var horizontalOffset = ScrollOffset * PixelsPerBeat;
        ClipsScrollViewer.ScrollToHorizontalOffset(horizontalOffset);
    }

    #endregion

    #region Track Management

    /// <summary>
    /// Refreshes the track headers display.
    /// </summary>
    public void RefreshTracks()
    {
        // Clear existing headers
        foreach (var header in _trackHeaders.Values)
        {
            TrackHeadersPanel.Children.Remove(header);
        }
        _trackHeaders.Clear();

        if (TracksSource == null)
            return;

        foreach (var track in TracksSource)
        {
            var header = new TrackHeaderControl
            {
                TrackName = track.Name,
                TrackColor = track.Color,
                IsMuted = track.IsMuted,
                IsSolo = track.IsSolo,
                IsCollapsed = track.IsCollapsed,
                TrackIndex = track.Index,
                Height = track.IsCollapsed ? 30 : track.Height
            };

            header.MuteChanged += (s, muted) =>
            {
                track.IsMuted = muted;
            };

            header.SoloChanged += (s, solo) =>
            {
                track.IsSolo = solo;
            };

            header.CollapsedChanged += (s, collapsed) =>
            {
                track.IsCollapsed = collapsed;
                header.Height = collapsed ? 30 : track.Height;
                RefreshLayout();
            };

            header.TrackSelected += (s, _) =>
            {
                TrackSelected?.Invoke(this, track);
            };

            _trackHeaders[track.Id] = header;
            TrackHeadersPanel.Children.Add(header);
        }

        RefreshLayout();
    }

    #endregion

    #region Clip Management

    /// <summary>
    /// Adds a clip element to the canvas.
    /// </summary>
    public void AddClip(UIElement clipElement, Guid clipId, double startBeat, int trackIndex)
    {
        if (_clipElements.ContainsKey(clipId))
            return;

        _clipElements[clipId] = clipElement;
        ClipsCanvas.Children.Add(clipElement);

        PositionClip(clipElement, startBeat, trackIndex);
    }

    /// <summary>
    /// Removes a clip element from the canvas.
    /// </summary>
    public void RemoveClip(Guid clipId)
    {
        if (_clipElements.TryGetValue(clipId, out var element))
        {
            ClipsCanvas.Children.Remove(element);
            _clipElements.Remove(clipId);
        }
    }

    /// <summary>
    /// Positions a clip on the canvas.
    /// </summary>
    public void PositionClip(UIElement clipElement, double startBeat, int trackIndex)
    {
        var x = startBeat * PixelsPerBeat;
        var y = GetTrackY(trackIndex);

        Canvas.SetLeft(clipElement, x);
        Canvas.SetTop(clipElement, y + 2);
    }

    /// <summary>
    /// Refreshes all clip positions.
    /// </summary>
    public void RefreshClipPositions()
    {
        // Clips would need to store their track index and start beat
        // This is a placeholder - actual implementation depends on clip data structure
    }

    private double GetTrackY(int trackIndex)
    {
        if (TracksSource == null)
            return trackIndex * 60;

        var y = 0.0;
        var index = 0;
        foreach (var track in TracksSource)
        {
            if (index >= trackIndex)
                break;
            y += track.IsCollapsed ? 30 : track.Height;
            index++;
        }
        return y;
    }

    private int GetTrackIndexAtY(double y)
    {
        if (TracksSource == null)
            return (int)(y / 60);

        var currentY = 0.0;
        var index = 0;
        foreach (var track in TracksSource)
        {
            var trackHeight = track.IsCollapsed ? 30 : track.Height;
            if (y >= currentY && y < currentY + trackHeight)
                return index;
            currentY += trackHeight;
            index++;
        }
        return Math.Max(0, index - 1);
    }

    #endregion

    #region Utility Methods

    private double PositionToBeats(double x)
    {
        if (PixelsPerBeat <= 0)
            return 0;
        return (x / PixelsPerBeat) + ScrollOffset;
    }

    private double BeatsToPosition(double beats)
    {
        return (beats - ScrollOffset) * PixelsPerBeat;
    }

    #endregion
}
