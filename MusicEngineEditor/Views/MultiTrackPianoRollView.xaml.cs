// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Views;

/// <summary>
/// Multi-Track Piano Roll View for overlaying and editing multiple MIDI tracks
/// in a single view with different colors per track.
/// </summary>
public partial class MultiTrackPianoRollView : UserControl
{
    #region Constants

    private const double DefaultBeatWidth = 40.0;
    private const double DefaultNoteHeight = 16.0;
    private const int BeatsPerBar = 4;
    private const int LowestNote = 0;
    private const int HighestNote = 127;
    private const double RulerHeight = 24.0;

    #endregion

    #region Private Fields

    private readonly ObservableCollection<PianoRollTrack> _tracks = [];
    private readonly Dictionary<Guid, Canvas> _trackCanvases = [];
    private readonly Dictionary<PianoRollNote, Shapes.Rectangle> _noteElements = [];

    private PianoRollTrack? _activeTrack;
    private double _zoomX = 1.0;
    private double _zoomY = 1.0;
    private double _scrollX;
    private double _scrollY;
    private double _gridSnapValue = 0.25;
    private double _totalBeats = 32;
    private double _playheadPosition;
    private double _inactiveOpacity = 0.5;

    // Selection state
    private bool _isSelecting;
    private bool _isDragging;
    private Point _selectionStart;
    private readonly HashSet<PianoRollNote> _selectedNotes = [];
    private Point _dragStart;
    private double _dragStartBeat;
    private int _dragStartNote;

    // Track colors
    private static readonly Color[] TrackColors =
    [
        Color.FromRgb(0x4C, 0xAF, 0x50), // Green
        Color.FromRgb(0x21, 0x96, 0xF3), // Blue
        Color.FromRgb(0xFF, 0x98, 0x00), // Orange
        Color.FromRgb(0xE9, 0x1E, 0x63), // Pink
        Color.FromRgb(0x9C, 0x27, 0xB0), // Purple
        Color.FromRgb(0x00, 0xBC, 0xD4), // Cyan
        Color.FromRgb(0xFF, 0xEB, 0x3B), // Yellow
        Color.FromRgb(0x79, 0x55, 0x48)  // Brown
    ];

    #endregion

    #region Properties

    /// <summary>
    /// Gets the collection of tracks.
    /// </summary>
    public ObservableCollection<PianoRollTrack> Tracks => _tracks;

    /// <summary>
    /// Gets or sets the active track for editing.
    /// </summary>
    public PianoRollTrack? ActiveTrack
    {
        get => _activeTrack;
        set
        {
            if (_activeTrack != value)
            {
                if (_activeTrack != null)
                    _activeTrack.IsActive = false;

                _activeTrack = value;

                if (_activeTrack != null)
                    _activeTrack.IsActive = true;

                UpdateTrackOpacities();
                ActiveTrackChanged?.Invoke(this, _activeTrack);
            }
        }
    }

    /// <summary>
    /// Gets or sets the horizontal zoom factor.
    /// </summary>
    public double ZoomX
    {
        get => _zoomX;
        set
        {
            _zoomX = Math.Clamp(value, 0.25, 4.0);
            RenderAll();
        }
    }

    /// <summary>
    /// Gets or sets the vertical zoom factor.
    /// </summary>
    public double ZoomY
    {
        get => _zoomY;
        set
        {
            _zoomY = Math.Clamp(value, 0.5, 2.0);
            RenderAll();
        }
    }

    /// <summary>
    /// Gets or sets the total beats in the pattern.
    /// </summary>
    public double TotalBeats
    {
        get => _totalBeats;
        set
        {
            _totalBeats = Math.Max(1, value);
            RenderAll();
        }
    }

    /// <summary>
    /// Gets or sets the playhead position in beats.
    /// </summary>
    public double PlayheadPosition
    {
        get => _playheadPosition;
        set
        {
            _playheadPosition = value;
            UpdatePlayhead();
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the active track changes.
    /// </summary>
    public event EventHandler<PianoRollTrack?>? ActiveTrackChanged;

    /// <summary>
    /// Occurs when notes are changed.
    /// </summary>
    public event EventHandler<PianoRollTrack>? NotesChanged;

    /// <summary>
    /// Occurs when a note is selected.
    /// </summary>
    public event EventHandler<PianoRollNote>? NoteSelected;

    #endregion

    #region Constructor

    public MultiTrackPianoRollView()
    {
        InitializeComponent();

        _tracks.CollectionChanged += OnTracksChanged;
        TrackListBox.ItemsSource = _tracks;

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Add some demo tracks
        AddDemoTracks();
        RenderAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAll();
    }

    private void OnTracksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    foreach (PianoRollTrack track in e.NewItems)
                    {
                        CreateTrackCanvas(track);
                        track.Notes.CollectionChanged += (s, args) => OnTrackNotesChanged(track);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (PianoRollTrack track in e.OldItems)
                    {
                        RemoveTrackCanvas(track);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                ClearAllTrackCanvases();
                break;
        }

        RenderAll();
    }

    private void OnTrackNotesChanged(PianoRollTrack track)
    {
        RenderTrackNotes(track);
        NotesChanged?.Invoke(this, track);
    }

    #endregion

    #region Track Management

    private void CreateTrackCanvas(PianoRollTrack track)
    {
        var canvas = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };

        _trackCanvases[track.Id] = canvas;
        TrackNotesContainer.Children.Add(canvas);
    }

    private void RemoveTrackCanvas(PianoRollTrack track)
    {
        if (_trackCanvases.TryGetValue(track.Id, out var canvas))
        {
            TrackNotesContainer.Children.Remove(canvas);
            _trackCanvases.Remove(track.Id);
        }
    }

    private void ClearAllTrackCanvases()
    {
        TrackNotesContainer.Children.Clear();
        _trackCanvases.Clear();
        _noteElements.Clear();
    }

    private void UpdateTrackOpacities()
    {
        foreach (var track in _tracks)
        {
            if (_trackCanvases.TryGetValue(track.Id, out var canvas))
            {
                canvas.Opacity = track.IsActive ? 1.0 : _inactiveOpacity;
            }
        }
    }

    /// <summary>
    /// Adds a new track.
    /// </summary>
    public PianoRollTrack AddTrack(string name = "Track")
    {
        var colorIndex = _tracks.Count % TrackColors.Length;
        var track = new PianoRollTrack
        {
            Name = name,
            Color = TrackColors[colorIndex],
            IsVisible = true
        };

        _tracks.Add(track);

        if (_activeTrack == null)
        {
            ActiveTrack = track;
        }

        return track;
    }

    /// <summary>
    /// Removes a track.
    /// </summary>
    public void RemoveTrack(PianoRollTrack track)
    {
        _tracks.Remove(track);

        if (_activeTrack == track)
        {
            ActiveTrack = _tracks.FirstOrDefault();
        }
    }

    private void AddDemoTracks()
    {
        // Track 1: Lead
        var lead = AddTrack("Lead");
        lead.Notes.Add(new PianoRollNote { Note = 72, StartBeat = 0, Duration = 1, Velocity = 100 });
        lead.Notes.Add(new PianoRollNote { Note = 74, StartBeat = 1, Duration = 1, Velocity = 100 });
        lead.Notes.Add(new PianoRollNote { Note = 76, StartBeat = 2, Duration = 2, Velocity = 100 });
        lead.Notes.Add(new PianoRollNote { Note = 77, StartBeat = 4, Duration = 1, Velocity = 100 });
        lead.Notes.Add(new PianoRollNote { Note = 79, StartBeat = 5, Duration = 3, Velocity = 100 });

        // Track 2: Bass
        var bass = AddTrack("Bass");
        bass.Notes.Add(new PianoRollNote { Note = 48, StartBeat = 0, Duration = 2, Velocity = 100 });
        bass.Notes.Add(new PianoRollNote { Note = 48, StartBeat = 4, Duration = 2, Velocity = 100 });
        bass.Notes.Add(new PianoRollNote { Note = 53, StartBeat = 8, Duration = 2, Velocity = 100 });
        bass.Notes.Add(new PianoRollNote { Note = 55, StartBeat = 12, Duration = 4, Velocity = 100 });

        // Track 3: Chords
        var chords = AddTrack("Chords");
        chords.Notes.Add(new PianoRollNote { Note = 60, StartBeat = 0, Duration = 4, Velocity = 80 });
        chords.Notes.Add(new PianoRollNote { Note = 64, StartBeat = 0, Duration = 4, Velocity = 80 });
        chords.Notes.Add(new PianoRollNote { Note = 67, StartBeat = 0, Duration = 4, Velocity = 80 });
        chords.Notes.Add(new PianoRollNote { Note = 65, StartBeat = 4, Duration = 4, Velocity = 80 });
        chords.Notes.Add(new PianoRollNote { Note = 69, StartBeat = 4, Duration = 4, Velocity = 80 });
        chords.Notes.Add(new PianoRollNote { Note = 72, StartBeat = 4, Duration = 4, Velocity = 80 });

        ActiveTrack = lead;
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        if (!IsLoaded) return;

        RenderGrid();
        RenderRuler();

        foreach (var track in _tracks)
        {
            RenderTrackNotes(track);
        }

        UpdatePlayhead();
        UpdateScrollBars();
    }

    private void RenderGrid()
    {
        GridCanvas.Children.Clear();

        var effectiveBeatWidth = DefaultBeatWidth * _zoomX;
        var effectiveNoteHeight = DefaultNoteHeight * _zoomY;
        var totalWidth = _totalBeats * effectiveBeatWidth;
        var totalHeight = (HighestNote - LowestNote + 1) * effectiveNoteHeight;

        GridCanvas.Width = totalWidth;
        GridCanvas.Height = totalHeight;

        // Draw horizontal lines (note lanes)
        for (int note = LowestNote; note <= HighestNote; note++)
        {
            var y = (HighestNote - note) * effectiveNoteHeight;
            var isBlackKey = PianoRollNote.IsBlackKey(note);

            // Background for white/black keys
            var lane = new Shapes.Rectangle
            {
                Width = totalWidth,
                Height = effectiveNoteHeight,
                Fill = new SolidColorBrush(isBlackKey
                    ? Color.FromRgb(0x1A, 0x1A, 0x1A)
                    : Color.FromRgb(0x1E, 0x1F, 0x22))
            };
            Canvas.SetLeft(lane, 0);
            Canvas.SetTop(lane, y);
            GridCanvas.Children.Add(lane);

            // C note highlight
            if (note % 12 == 0)
            {
                var cLine = new Shapes.Line
                {
                    X1 = 0,
                    Y1 = y + effectiveNoteHeight,
                    X2 = totalWidth,
                    Y2 = y + effectiveNoteHeight,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
                    StrokeThickness = 1
                };
                GridCanvas.Children.Add(cLine);
            }
        }

        // Draw vertical lines (beats)
        for (int beat = 0; beat <= _totalBeats; beat++)
        {
            var x = beat * effectiveBeatWidth;
            var isBarLine = beat % BeatsPerBar == 0;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = totalHeight,
                Stroke = new SolidColorBrush(isBarLine
                    ? Color.FromRgb(0x4A, 0x4A, 0x4A)
                    : Color.FromRgb(0x2A, 0x2A, 0x2A)),
                StrokeThickness = isBarLine ? 1 : 0.5
            };
            GridCanvas.Children.Add(line);
        }

        ApplyScrollTransform(GridCanvas);
    }

    private void RenderRuler()
    {
        RulerCanvas.Children.Clear();

        var effectiveBeatWidth = DefaultBeatWidth * _zoomX;
        var totalWidth = _totalBeats * effectiveBeatWidth;

        RulerCanvas.Width = totalWidth;
        RulerCanvas.Height = RulerHeight;

        // Draw beat markers
        for (int beat = 0; beat <= _totalBeats; beat++)
        {
            var x = beat * effectiveBeatWidth;
            var isBarLine = beat % BeatsPerBar == 0;
            var barNumber = beat / BeatsPerBar + 1;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = isBarLine ? 0 : RulerHeight * 0.6,
                X2 = x,
                Y2 = RulerHeight,
                Stroke = new SolidColorBrush(isBarLine
                    ? Color.FromRgb(0x6F, 0x73, 0x7A)
                    : Color.FromRgb(0x43, 0x45, 0x4A)),
                StrokeThickness = isBarLine ? 1.5 : 1
            };
            RulerCanvas.Children.Add(line);

            if (isBarLine)
            {
                var text = new TextBlock
                {
                    Text = barNumber.ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(text, x + 4);
                Canvas.SetTop(text, 2);
                RulerCanvas.Children.Add(text);
            }
        }

        ApplyHorizontalScrollTransform(RulerCanvas);
    }

    private void RenderTrackNotes(PianoRollTrack track)
    {
        if (!_trackCanvases.TryGetValue(track.Id, out var canvas))
            return;

        canvas.Children.Clear();

        if (!track.IsVisible)
            return;

        var effectiveBeatWidth = DefaultBeatWidth * _zoomX;
        var effectiveNoteHeight = DefaultNoteHeight * _zoomY;
        var totalWidth = _totalBeats * effectiveBeatWidth;
        var totalHeight = (HighestNote - LowestNote + 1) * effectiveNoteHeight;

        canvas.Width = totalWidth;
        canvas.Height = totalHeight;

        foreach (var note in track.Notes)
        {
            var x = note.StartBeat * effectiveBeatWidth;
            var y = (HighestNote - note.Note) * effectiveNoteHeight;
            var width = note.Duration * effectiveBeatWidth;

            var rect = new Shapes.Rectangle
            {
                Width = Math.Max(width - 1, 2),
                Height = effectiveNoteHeight - 2,
                Fill = new SolidColorBrush(track.Color),
                Stroke = note.IsSelected ? Brushes.White : null,
                StrokeThickness = note.IsSelected ? 2 : 0,
                RadiusX = 2,
                RadiusY = 2,
                Tag = note
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y + 1);
            canvas.Children.Add(rect);

            _noteElements[note] = rect;
        }

        canvas.Opacity = track.IsActive ? 1.0 : _inactiveOpacity;
        ApplyScrollTransform(canvas);
    }

    private void UpdatePlayhead()
    {
        var effectiveBeatWidth = DefaultBeatWidth * _zoomX;
        var effectiveNoteHeight = DefaultNoteHeight * _zoomY;
        var totalHeight = (HighestNote - LowestNote + 1) * effectiveNoteHeight;

        var x = _playheadPosition * effectiveBeatWidth - _scrollX;

        if (x >= 0 && x <= NotesCanvas.ActualWidth)
        {
            Playhead.Visibility = Visibility.Visible;
            Playhead.Height = totalHeight;
            Canvas.SetLeft(Playhead, x);
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateScrollBars()
    {
        var effectiveBeatWidth = DefaultBeatWidth * _zoomX;
        var effectiveNoteHeight = DefaultNoteHeight * _zoomY;
        var totalWidth = _totalBeats * effectiveBeatWidth;
        var totalHeight = (HighestNote - LowestNote + 1) * effectiveNoteHeight;

        HorizontalScrollBar.Maximum = Math.Max(0, _totalBeats - (NotesCanvas.ActualWidth / effectiveBeatWidth));
        VerticalScrollBar.Maximum = Math.Max(0, 127 - (NotesCanvas.ActualHeight / effectiveNoteHeight));
    }

    private void ApplyScrollTransform(Canvas canvas)
    {
        var effectiveNoteHeight = DefaultNoteHeight * _zoomY;
        var offsetY = _scrollY * effectiveNoteHeight;
        canvas.RenderTransform = new TranslateTransform(-_scrollX, -offsetY);
    }

    private void ApplyHorizontalScrollTransform(Canvas canvas)
    {
        canvas.RenderTransform = new TranslateTransform(-_scrollX, 0);
    }

    #endregion

    #region Mouse Event Handlers

    private void NotesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(NotesCanvas);
        position.X += _scrollX;
        position.Y += _scrollY * DefaultNoteHeight * _zoomY;

        var hitNote = GetNoteAtPosition(position);

        if (hitNote != null)
        {
            // Select or start drag
            if (!_selectedNotes.Contains(hitNote))
            {
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    ClearSelection();
                }
                SelectNote(hitNote);
            }

            _isDragging = true;
            _dragStart = position;
            _dragStartBeat = hitNote.StartBeat;
            _dragStartNote = hitNote.Note;
            NotesCanvas.CaptureMouse();
        }
        else
        {
            // Start selection rectangle
            ClearSelection();
            _isSelecting = true;
            _selectionStart = position;
            NotesCanvas.CaptureMouse();
        }
    }

    private void NotesCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting)
        {
            _isSelecting = false;
            SelectionRect.Visibility = Visibility.Collapsed;
            NotesCanvas.ReleaseMouseCapture();
        }
        else if (_isDragging)
        {
            _isDragging = false;
            NotesCanvas.ReleaseMouseCapture();

            if (_activeTrack != null)
            {
                NotesChanged?.Invoke(this, _activeTrack);
            }
        }
    }

    private void NotesCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(NotesCanvas);
        position.X += _scrollX;
        position.Y += _scrollY * DefaultNoteHeight * _zoomY;

        if (_isSelecting)
        {
            UpdateSelectionRectangle(position);
        }
        else if (_isDragging && _selectedNotes.Count > 0 && _activeTrack != null)
        {
            var effectiveBeatWidth = DefaultBeatWidth * _zoomX;
            var effectiveNoteHeight = DefaultNoteHeight * _zoomY;

            var deltaBeat = (position.X - _dragStart.X) / effectiveBeatWidth;
            var deltaNote = -(int)((position.Y - _dragStart.Y) / effectiveNoteHeight);

            foreach (var note in _selectedNotes.Where(n => _activeTrack.Notes.Contains(n)))
            {
                var originalElement = _noteElements.GetValueOrDefault(note);
                if (originalElement != null)
                {
                    note.StartBeat = Math.Max(0, SnapToGrid(_dragStartBeat + deltaBeat));
                    note.Note = Math.Clamp(_dragStartNote + deltaNote, 0, 127);
                }
            }

            RenderTrackNotes(_activeTrack);
        }
    }

    private void NotesCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(NotesCanvas);
        position.X += _scrollX;
        position.Y += _scrollY * DefaultNoteHeight * _zoomY;

        var hitNote = GetNoteAtPosition(position);

        if (hitNote != null && !_selectedNotes.Contains(hitNote))
        {
            ClearSelection();
            SelectNote(hitNote);
        }
    }

    private void UpdateSelectionRectangle(Point currentPoint)
    {
        var x = Math.Min(_selectionStart.X, currentPoint.X) - _scrollX;
        var y = Math.Min(_selectionStart.Y, currentPoint.Y) - (_scrollY * DefaultNoteHeight * _zoomY);
        var width = Math.Abs(currentPoint.X - _selectionStart.X);
        var height = Math.Abs(currentPoint.Y - _selectionStart.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = width;
        SelectionRect.Height = height;
        SelectionRect.Visibility = Visibility.Visible;

        // Select notes within rectangle
        SelectNotesInRectangle(_selectionStart, currentPoint);
    }

    #endregion

    #region Selection

    private PianoRollNote? GetNoteAtPosition(Point position)
    {
        var effectiveBeatWidth = DefaultBeatWidth * _zoomX;
        var effectiveNoteHeight = DefaultNoteHeight * _zoomY;
        var beat = position.X / effectiveBeatWidth;
        var note = HighestNote - (int)(position.Y / effectiveNoteHeight);

        foreach (var track in _tracks.Where(t => t.IsVisible))
        {
            foreach (var trackNote in track.Notes)
            {
                if (trackNote.Note == note &&
                    beat >= trackNote.StartBeat &&
                    beat <= trackNote.StartBeat + trackNote.Duration)
                {
                    return trackNote;
                }
            }
        }

        return null;
    }

    private void SelectNote(PianoRollNote note)
    {
        note.IsSelected = true;
        _selectedNotes.Add(note);

        if (_noteElements.TryGetValue(note, out var element))
        {
            element.Stroke = Brushes.White;
            element.StrokeThickness = 2;
        }

        NoteSelected?.Invoke(this, note);
    }

    private void ClearSelection()
    {
        foreach (var note in _selectedNotes)
        {
            note.IsSelected = false;
            if (_noteElements.TryGetValue(note, out var element))
            {
                element.Stroke = null;
                element.StrokeThickness = 0;
            }
        }
        _selectedNotes.Clear();
    }

    private void SelectNotesInRectangle(Point start, Point end)
    {
        var effectiveBeatWidth = DefaultBeatWidth * _zoomX;
        var effectiveNoteHeight = DefaultNoteHeight * _zoomY;

        var minBeat = Math.Min(start.X, end.X) / effectiveBeatWidth;
        var maxBeat = Math.Max(start.X, end.X) / effectiveBeatWidth;
        var maxNote = HighestNote - (int)(Math.Min(start.Y, end.Y) / effectiveNoteHeight);
        var minNote = HighestNote - (int)(Math.Max(start.Y, end.Y) / effectiveNoteHeight);

        ClearSelection();

        foreach (var track in _tracks.Where(t => t.IsVisible))
        {
            foreach (var note in track.Notes)
            {
                if (note.Note >= minNote && note.Note <= maxNote &&
                    note.StartBeat + note.Duration >= minBeat &&
                    note.StartBeat <= maxBeat)
                {
                    SelectNote(note);
                }
            }
        }
    }

    #endregion

    #region Toolbar Event Handlers

    private void ZoomInX_Click(object sender, RoutedEventArgs e)
    {
        ZoomX = Math.Min(_zoomX * 1.25, 4.0);
    }

    private void ZoomOutX_Click(object sender, RoutedEventArgs e)
    {
        ZoomX = Math.Max(_zoomX / 1.25, 0.25);
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var track in _tracks)
        {
            track.IsVisible = true;
        }
        RenderAll();
    }

    private void HideAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var track in _tracks)
        {
            if (track != _activeTrack)
            {
                track.IsVisible = false;
            }
        }
        RenderAll();
    }

    private void InactiveOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _inactiveOpacity = e.NewValue;
        UpdateTrackOpacities();
    }

    private void HorizontalScroll_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _scrollX = e.NewValue * DefaultBeatWidth * _zoomX;
        RenderAll();
    }

    private void VerticalScroll_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _scrollY = e.NewValue;
        RenderAll();
    }

    private void TrackList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TrackListBox.SelectedItem is PianoRollTrack track)
        {
            ActiveTrack = track;
        }
    }

    #endregion

    #region Context Menu Handlers

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var track in _tracks)
        {
            var toRemove = track.Notes.Where(n => n.IsSelected).ToList();
            foreach (var note in toRemove)
            {
                track.Notes.Remove(note);
                _noteElements.Remove(note);
            }
        }
        _selectedNotes.Clear();
    }

    private void DuplicateSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTrack == null) return;

        var newNotes = new List<PianoRollNote>();
        foreach (var note in _selectedNotes.Where(n => _activeTrack.Notes.Contains(n)))
        {
            var clone = note.Clone();
            clone.StartBeat += note.Duration;
            newNotes.Add(clone);
        }

        ClearSelection();
        foreach (var note in newNotes)
        {
            _activeTrack.Notes.Add(note);
            SelectNote(note);
        }
    }

    private void Quantize_Click(object sender, RoutedEventArgs e)
    {
        foreach (var note in _selectedNotes)
        {
            note.StartBeat = SnapToGrid(note.StartBeat);
        }

        foreach (var track in _tracks)
        {
            RenderTrackNotes(track);
        }
    }

    private void TransposeUp_Click(object sender, RoutedEventArgs e)
    {
        foreach (var note in _selectedNotes)
        {
            note.Note = Math.Min(note.Note + 1, 127);
        }

        foreach (var track in _tracks)
        {
            RenderTrackNotes(track);
        }
    }

    private void TransposeDown_Click(object sender, RoutedEventArgs e)
    {
        foreach (var note in _selectedNotes)
        {
            note.Note = Math.Max(note.Note - 1, 0);
        }

        foreach (var track in _tracks)
        {
            RenderTrackNotes(track);
        }
    }

    private void SelectAllInTrack_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTrack == null) return;

        ClearSelection();
        foreach (var note in _activeTrack.Notes)
        {
            SelectNote(note);
        }
    }

    #endregion

    #region Helper Methods

    private double SnapToGrid(double beat)
    {
        return Math.Round(beat / _gridSnapValue) * _gridSnapValue;
    }

    #endregion
}

/// <summary>
/// Represents a track in the multi-track piano roll.
/// </summary>
public partial class PianoRollTrack : ObservableObject
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Track name.
    /// </summary>
    [ObservableProperty]
    private string _name = "Track";

    /// <summary>
    /// Track color.
    /// </summary>
    [ObservableProperty]
    private Color _color = Colors.Gray;

    /// <summary>
    /// Whether the track is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Whether this is the active track (editable).
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Track mute state.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Track solo state.
    /// </summary>
    [ObservableProperty]
    private bool _isSolo;

    /// <summary>
    /// Notes in this track.
    /// </summary>
    public ObservableCollection<PianoRollNote> Notes { get; } = [];

    /// <summary>
    /// Gets a brush from the color for binding.
    /// </summary>
    public SolidColorBrush ColorBrush => new(Color);
}
