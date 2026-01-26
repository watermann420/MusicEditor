// MusicEngineEditor - Range Selection Control
// Copyright (c) 2026 MusicEngine

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a time range selection across tracks.
/// </summary>
public class RangeSelection
{
    /// <summary>
    /// Gets or sets the start position in beats.
    /// </summary>
    public double StartBeat { get; set; }

    /// <summary>
    /// Gets or sets the end position in beats.
    /// </summary>
    public double EndBeat { get; set; }

    /// <summary>
    /// Gets or sets the first selected track index.
    /// </summary>
    public int StartTrackIndex { get; set; }

    /// <summary>
    /// Gets or sets the last selected track index.
    /// </summary>
    public int EndTrackIndex { get; set; }

    /// <summary>
    /// Gets the length in beats.
    /// </summary>
    public double Length => Math.Abs(EndBeat - StartBeat);

    /// <summary>
    /// Gets the number of tracks in the selection.
    /// </summary>
    public int TrackCount => Math.Abs(EndTrackIndex - StartTrackIndex) + 1;

    /// <summary>
    /// Gets whether the selection is valid (non-zero).
    /// </summary>
    public bool IsValid => Length > 0;

    /// <summary>
    /// Normalizes the selection (ensures start is less than end).
    /// </summary>
    public RangeSelection Normalize()
    {
        return new RangeSelection
        {
            StartBeat = Math.Min(StartBeat, EndBeat),
            EndBeat = Math.Max(StartBeat, EndBeat),
            StartTrackIndex = Math.Min(StartTrackIndex, EndTrackIndex),
            EndTrackIndex = Math.Max(StartTrackIndex, EndTrackIndex)
        };
    }

    /// <summary>
    /// Checks if a beat position is within the selection.
    /// </summary>
    public bool ContainsBeat(double beat)
    {
        var normalized = Normalize();
        return beat >= normalized.StartBeat && beat <= normalized.EndBeat;
    }

    /// <summary>
    /// Checks if a track index is within the selection.
    /// </summary>
    public bool ContainsTrack(int trackIndex)
    {
        var normalized = Normalize();
        return trackIndex >= normalized.StartTrackIndex && trackIndex <= normalized.EndTrackIndex;
    }
}

/// <summary>
/// Represents a track highlight area for display.
/// </summary>
public class TrackHighlightInfo
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>
/// Control for multi-track time range selection with snap-to-grid support.
/// </summary>
public partial class RangeSelectionControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty SelectionProperty =
        DependencyProperty.Register(nameof(Selection), typeof(RangeSelection),
            typeof(RangeSelectionControl), new PropertyMetadata(null, OnSelectionChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double),
            typeof(RangeSelectionControl), new PropertyMetadata(40.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TrackHeightProperty =
        DependencyProperty.Register(nameof(TrackHeight), typeof(double),
            typeof(RangeSelectionControl), new PropertyMetadata(60.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TrackCountProperty =
        DependencyProperty.Register(nameof(TrackCount), typeof(int),
            typeof(RangeSelectionControl), new PropertyMetadata(1));

    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(nameof(SnapToGrid), typeof(bool),
            typeof(RangeSelectionControl), new PropertyMetadata(true));

    public static readonly DependencyProperty GridDivisionProperty =
        DependencyProperty.Register(nameof(GridDivision), typeof(double),
            typeof(RangeSelectionControl), new PropertyMetadata(0.25));

    public static readonly DependencyProperty ScrollOffsetXProperty =
        DependencyProperty.Register(nameof(ScrollOffsetX), typeof(double),
            typeof(RangeSelectionControl), new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetYProperty =
        DependencyProperty.Register(nameof(ScrollOffsetY), typeof(double),
            typeof(RangeSelectionControl), new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty IsSelectingProperty =
        DependencyProperty.Register(nameof(IsSelecting), typeof(bool),
            typeof(RangeSelectionControl), new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets the current range selection.
    /// </summary>
    public RangeSelection? Selection
    {
        get => (RangeSelection?)GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the pixels per beat for horizontal scaling.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of each track.
    /// </summary>
    public double TrackHeight
    {
        get => (double)GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the total number of tracks.
    /// </summary>
    public int TrackCount
    {
        get => (int)GetValue(TrackCountProperty);
        set => SetValue(TrackCountProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to snap to grid.
    /// </summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>
    /// Gets or sets the grid division in beats.
    /// </summary>
    public double GridDivision
    {
        get => (double)GetValue(GridDivisionProperty);
        set => SetValue(GridDivisionProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal scroll offset.
    /// </summary>
    public double ScrollOffsetX
    {
        get => (double)GetValue(ScrollOffsetXProperty);
        set => SetValue(ScrollOffsetXProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical scroll offset.
    /// </summary>
    public double ScrollOffsetY
    {
        get => (double)GetValue(ScrollOffsetYProperty);
        set => SetValue(ScrollOffsetYProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a selection is in progress.
    /// </summary>
    public bool IsSelecting
    {
        get => (bool)GetValue(IsSelectingProperty);
        set => SetValue(IsSelectingProperty, value);
    }

    #endregion

    #region Commands

    public ICommand ClearSelectionCommand { get; }
    public ICommand SelectAllCommand { get; }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the selection changes.
    /// </summary>
    public event EventHandler<RangeSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Raised when selection starts.
    /// </summary>
    public event EventHandler<RangeSelectionEventArgs>? SelectionStarted;

    /// <summary>
    /// Raised when selection ends.
    /// </summary>
    public event EventHandler<RangeSelectionEventArgs>? SelectionEnded;

    #endregion

    #region Fields

    private Point _selectionStart;
    private bool _isDraggingLeftHandle;
    private bool _isDraggingRightHandle;
    private double _handleDragOffset;

    #endregion

    public RangeSelectionControl()
    {
        InitializeComponent();

        ClearSelectionCommand = new RelayCommand(ClearSelection);
        SelectAllCommand = new RelayCommand(SelectAll);

        SizeChanged += (_, _) => UpdateSelectionVisuals();
    }

    #region Property Changed Callbacks

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeSelectionControl control)
        {
            control.UpdateSelectionVisuals();
            control.SelectionChanged?.Invoke(control, new RangeSelectionChangedEventArgs(e.NewValue as RangeSelection));
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeSelectionControl control)
        {
            control.UpdateSelectionVisuals();
        }
    }

    #endregion

    #region Mouse Handlers

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(SelectionCanvas);
        _selectionStart = position;

        var startBeat = PositionToBeat(position.X);
        var startTrack = PositionToTrack(position.Y);

        if (SnapToGrid)
        {
            startBeat = SnapBeat(startBeat);
        }

        Selection = new RangeSelection
        {
            StartBeat = startBeat,
            EndBeat = startBeat,
            StartTrackIndex = startTrack,
            EndTrackIndex = startTrack
        };

        IsSelecting = true;
        CaptureMouse();

        SelectionStarted?.Invoke(this, new RangeSelectionEventArgs(Selection));

        e.Handled = true;
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsSelecting || _isDraggingLeftHandle || _isDraggingRightHandle)
        {
            IsSelecting = false;
            _isDraggingLeftHandle = false;
            _isDraggingRightHandle = false;
            ReleaseMouseCapture();

            if (Selection != null)
            {
                Selection = Selection.Normalize();

                // Clear if selection is too small
                if (Selection.Length < GridDivision / 2)
                {
                    ClearSelection();
                }
                else
                {
                    SelectionEnded?.Invoke(this, new RangeSelectionEventArgs(Selection));
                }
            }
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsSelecting && !_isDraggingLeftHandle && !_isDraggingRightHandle) return;

        var position = e.GetPosition(SelectionCanvas);

        if (IsSelecting)
        {
            UpdateSelectionFromDrag(position);
        }
        else if (_isDraggingLeftHandle || _isDraggingRightHandle)
        {
            UpdateHandleDrag(position);
        }
    }

    private void Canvas_MouseLeave(object sender, MouseEventArgs e)
    {
        // Continue selection if mouse is captured
    }

    private void LeftHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Selection == null) return;

        _isDraggingLeftHandle = true;
        _handleDragOffset = e.GetPosition(LeftHandle).X;
        CaptureMouse();
        e.Handled = true;
    }

    private void RightHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Selection == null) return;

        _isDraggingRightHandle = true;
        _handleDragOffset = e.GetPosition(RightHandle).X;
        CaptureMouse();
        e.Handled = true;
    }

    #endregion

    #region Selection Updates

    private void UpdateSelectionFromDrag(Point position)
    {
        if (Selection == null) return;

        var endBeat = PositionToBeat(position.X);
        var endTrack = PositionToTrack(position.Y);

        if (SnapToGrid)
        {
            endBeat = SnapBeat(endBeat);
        }

        Selection = new RangeSelection
        {
            StartBeat = Selection.StartBeat,
            EndBeat = endBeat,
            StartTrackIndex = Selection.StartTrackIndex,
            EndTrackIndex = Math.Clamp(endTrack, 0, TrackCount - 1)
        };

        UpdateSelectionVisuals();
        UpdateSelectionInfo();
    }

    private void UpdateHandleDrag(Point position)
    {
        if (Selection == null) return;

        var beat = PositionToBeat(position.X);
        if (SnapToGrid)
        {
            beat = SnapBeat(beat);
        }

        var normalized = Selection.Normalize();

        if (_isDraggingLeftHandle)
        {
            Selection = new RangeSelection
            {
                StartBeat = Math.Min(beat, normalized.EndBeat - GridDivision),
                EndBeat = normalized.EndBeat,
                StartTrackIndex = normalized.StartTrackIndex,
                EndTrackIndex = normalized.EndTrackIndex
            };
        }
        else if (_isDraggingRightHandle)
        {
            Selection = new RangeSelection
            {
                StartBeat = normalized.StartBeat,
                EndBeat = Math.Max(beat, normalized.StartBeat + GridDivision),
                StartTrackIndex = normalized.StartTrackIndex,
                EndTrackIndex = normalized.EndTrackIndex
            };
        }

        UpdateSelectionVisuals();
        UpdateSelectionInfo();
    }

    private void UpdateSelectionVisuals()
    {
        if (Selection == null || !Selection.IsValid)
        {
            HideSelectionVisuals();
            return;
        }

        var normalized = Selection.Normalize();

        var startX = BeatToPosition(normalized.StartBeat);
        var endX = BeatToPosition(normalized.EndBeat);
        var startY = TrackToPosition(normalized.StartTrackIndex);
        var endY = TrackToPosition(normalized.EndTrackIndex + 1);

        var width = endX - startX;
        var height = endY - startY;

        // Update fill
        Canvas.SetLeft(SelectionFill, startX);
        Canvas.SetTop(SelectionFill, startY);
        SelectionFill.Width = Math.Max(0, width);
        SelectionFill.Height = Math.Max(0, height);
        SelectionFill.Visibility = Visibility.Visible;

        // Update left edge
        Canvas.SetLeft(LeftEdge, startX);
        Canvas.SetTop(LeftEdge, startY);
        LeftEdge.Height = height;
        LeftEdge.Visibility = Visibility.Visible;

        // Update right edge
        Canvas.SetLeft(RightEdge, endX - 2);
        Canvas.SetTop(RightEdge, startY);
        RightEdge.Height = height;
        RightEdge.Visibility = Visibility.Visible;

        // Update handles
        Canvas.SetLeft(LeftHandle, startX - 3);
        Canvas.SetTop(LeftHandle, startY);
        LeftHandle.Height = height;
        LeftHandle.Visibility = Visibility.Visible;

        Canvas.SetLeft(RightHandle, endX - 3);
        Canvas.SetTop(RightHandle, startY);
        RightHandle.Height = height;
        RightHandle.Visibility = Visibility.Visible;

        // Update info popup
        UpdateSelectionInfo();
    }

    private void HideSelectionVisuals()
    {
        SelectionFill.Visibility = Visibility.Collapsed;
        LeftEdge.Visibility = Visibility.Collapsed;
        RightEdge.Visibility = Visibility.Collapsed;
        LeftHandle.Visibility = Visibility.Collapsed;
        RightHandle.Visibility = Visibility.Collapsed;
        SelectionInfo.Visibility = Visibility.Collapsed;
    }

    private void UpdateSelectionInfo()
    {
        if (Selection == null || !Selection.IsValid)
        {
            SelectionInfo.Visibility = Visibility.Collapsed;
            return;
        }

        var normalized = Selection.Normalize();

        StartPositionText.Text = FormatBeat(normalized.StartBeat);
        EndPositionText.Text = FormatBeat(normalized.EndBeat);
        LengthText.Text = FormatBeat(normalized.Length);
        TrackCountText.Text = normalized.TrackCount.ToString();

        // Position info popup
        var infoX = BeatToPosition(normalized.EndBeat) + 10;
        var infoY = TrackToPosition(normalized.StartTrackIndex);

        // Keep in bounds
        if (infoX + 100 > ActualWidth)
        {
            infoX = BeatToPosition(normalized.StartBeat) - 110;
        }

        Canvas.SetLeft(SelectionInfo, Math.Max(0, infoX));
        Canvas.SetTop(SelectionInfo, Math.Max(0, infoY));
        SelectionInfo.Visibility = Visibility.Visible;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        Selection = null;
        HideSelectionVisuals();
    }

    /// <summary>
    /// Selects all visible area.
    /// </summary>
    public void SelectAll()
    {
        var visibleBeats = ActualWidth / PixelsPerBeat;

        Selection = new RangeSelection
        {
            StartBeat = ScrollOffsetX / PixelsPerBeat,
            EndBeat = (ScrollOffsetX + ActualWidth) / PixelsPerBeat,
            StartTrackIndex = 0,
            EndTrackIndex = Math.Max(0, TrackCount - 1)
        };
    }

    /// <summary>
    /// Sets a selection programmatically.
    /// </summary>
    public void SetSelection(double startBeat, double endBeat, int startTrack, int endTrack)
    {
        Selection = new RangeSelection
        {
            StartBeat = startBeat,
            EndBeat = endBeat,
            StartTrackIndex = startTrack,
            EndTrackIndex = endTrack
        }.Normalize();
    }

    /// <summary>
    /// Extends the selection to include a beat position.
    /// </summary>
    public void ExtendSelectionTo(double beat, int trackIndex)
    {
        if (Selection == null)
        {
            SetSelection(beat, beat, trackIndex, trackIndex);
            return;
        }

        var normalized = Selection.Normalize();
        Selection = new RangeSelection
        {
            StartBeat = Math.Min(normalized.StartBeat, beat),
            EndBeat = Math.Max(normalized.EndBeat, beat),
            StartTrackIndex = Math.Min(normalized.StartTrackIndex, trackIndex),
            EndTrackIndex = Math.Max(normalized.EndTrackIndex, trackIndex)
        };
    }

    #endregion

    #region Helper Methods

    private double PositionToBeat(double x)
    {
        return (x + ScrollOffsetX) / PixelsPerBeat;
    }

    private double BeatToPosition(double beat)
    {
        return (beat * PixelsPerBeat) - ScrollOffsetX;
    }

    private int PositionToTrack(double y)
    {
        return (int)((y + ScrollOffsetY) / TrackHeight);
    }

    private double TrackToPosition(int track)
    {
        return (track * TrackHeight) - ScrollOffsetY;
    }

    private double SnapBeat(double beat)
    {
        return Math.Round(beat / GridDivision) * GridDivision;
    }

    private static string FormatBeat(double beat)
    {
        var bars = (int)(beat / 4) + 1;
        var beatsInBar = (beat % 4) + 1;
        return $"{bars}:{beatsInBar:F2}";
    }

    #endregion
}

#region Event Args

public class RangeSelectionChangedEventArgs : EventArgs
{
    public RangeSelection? Selection { get; }

    public RangeSelectionChangedEventArgs(RangeSelection? selection)
    {
        Selection = selection;
    }
}

public class RangeSelectionEventArgs : EventArgs
{
    public RangeSelection Selection { get; }

    public RangeSelectionEventArgs(RangeSelection selection)
    {
        Selection = selection;
    }
}

#endregion
