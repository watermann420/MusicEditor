// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Pattern Editor Piano Roll control.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls.PatternEditor;

/// <summary>
/// Piano Roll control for the Pattern Editor.
/// Provides a graphical interface for editing MIDI notes with a piano keyboard
/// on the left and a note grid on the right.
/// </summary>
public partial class PianoRollControl : UserControl
{
    #region Constants

    private const double DefaultNoteHeight = 18.0;
    private const double DefaultBeatWidth = 60.0;
    private const double NoteCornerRadius = 2.0;
    private const int BeatsPerBar = 4;
    private const double KeyboardWidth = 80.0;

    // Dark theme colors
    private static readonly Color BackgroundColor = (Color)ColorConverter.ConvertFromString("#0D0D0D")!;
    private static readonly Color AccentColor = (Color)ColorConverter.ConvertFromString("#00D9FF")!;
    private static readonly Color TextColor = (Color)ColorConverter.ConvertFromString("#E0E0E0")!;
    private static readonly Color BorderColor = (Color)ColorConverter.ConvertFromString("#2A2A2A")!;
    private static readonly Color PanelColor = (Color)ColorConverter.ConvertFromString("#181818")!;
    private static readonly Color WhiteKeyColor = (Color)ColorConverter.ConvertFromString("#E8E8E8")!;
    private static readonly Color BlackKeyColor = (Color)ColorConverter.ConvertFromString("#2D2D2D")!;
    private static readonly Color WhiteKeyLaneColor = (Color)ColorConverter.ConvertFromString("#1A1A1A")!;
    private static readonly Color BlackKeyLaneColor = (Color)ColorConverter.ConvertFromString("#151515")!;
    private static readonly Color GridLineColor = (Color)ColorConverter.ConvertFromString("#2A2A2A")!;
    private static readonly Color BarLineColor = (Color)ColorConverter.ConvertFromString("#3A3A3A")!;

    #endregion

    #region Private Fields

    private PianoRollViewModel _viewModel;
    private readonly Dictionary<NoteItem, Rectangle> _noteRectangles = new();
    private Rectangle? _ghostNoteRect;
    private int _ghostNotePitch = -1;
    private double _ghostNoteBeat = -1;

    #endregion

    #region Constructor

    public PianoRollControl()
    {
        InitializeComponent();

        _viewModel = new PianoRollViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        // Subscribe to notes collection changes
        _viewModel.Notes.CollectionChanged += (_, _) =>
        {
            RenderNotes();
            UpdateNoteCount();
        };
    }

    #endregion

    #region Lifecycle Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAll();
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Renders all visual elements of the piano roll.
    /// </summary>
    private void RenderAll()
    {
        if (!IsLoaded) return;

        UpdateCanvasSizes();
        RenderPianoKeyboard();
        RenderGridLines();
        RenderNotes();
        UpdateNoteCount();
    }

    /// <summary>
    /// Updates the size of all canvases based on zoom and note range.
    /// </summary>
    private void UpdateCanvasSizes()
    {
        var totalWidth = GetTotalWidth();
        var totalHeight = GetTotalHeight();

        GridLinesCanvas.Width = totalWidth;
        GridLinesCanvas.Height = totalHeight;
        NoteCanvas.Width = totalWidth;
        NoteCanvas.Height = totalHeight;
        GhostNoteCanvas.Width = totalWidth;
        GhostNoteCanvas.Height = totalHeight;
        PianoKeyboardCanvas.Height = totalHeight;
        PianoKeyboardCanvas.Width = KeyboardWidth;
    }

    /// <summary>
    /// Renders the piano keyboard on the left side.
    /// </summary>
    private void RenderPianoKeyboard()
    {
        PianoKeyboardCanvas.Children.Clear();

        double noteHeight = GetEffectiveNoteHeight();

        for (int midiNote = _viewModel.HighestNote; midiNote >= _viewModel.LowestNote; midiNote--)
        {
            double y = NoteToY(midiNote);
            bool isBlackKey = NoteItem.IsBlackKey(midiNote);
            string noteName = NoteItem.GetNoteName(midiNote);

            // Key background
            var keyRect = new Rectangle
            {
                Width = isBlackKey ? 50 : KeyboardWidth - 2,
                Height = noteHeight - 1,
                Fill = new SolidColorBrush(isBlackKey ? BlackKeyColor : WhiteKeyColor),
                RadiusX = 2,
                RadiusY = 2,
                Cursor = Cursors.Hand,
                Tag = midiNote
            };

            Canvas.SetLeft(keyRect, 1);
            Canvas.SetTop(keyRect, y);
            PianoKeyboardCanvas.Children.Add(keyRect);

            // Note label (show only C notes and some important notes)
            int pitchClass = midiNote % 12;
            if (pitchClass == 0 || noteHeight >= 16) // Show C notes, or all if zoomed in
            {
                var label = new TextBlock
                {
                    Text = noteName,
                    Foreground = new SolidColorBrush(isBlackKey ? TextColor : Color.FromRgb(0x40, 0x40, 0x40)),
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold
                };

                Canvas.SetLeft(label, isBlackKey ? 54 : 58);
                Canvas.SetTop(label, y + (noteHeight - 12) / 2);
                PianoKeyboardCanvas.Children.Add(label);
            }
        }
    }

    /// <summary>
    /// Renders the grid lines on the note canvas.
    /// </summary>
    private void RenderGridLines()
    {
        GridLinesCanvas.Children.Clear();

        double totalWidth = GetTotalWidth();
        double totalHeight = GetTotalHeight();
        double beatWidth = GetEffectiveBeatWidth();
        double noteHeight = GetEffectiveNoteHeight();

        // Draw lane backgrounds (alternating for white/black keys)
        for (int midiNote = _viewModel.HighestNote; midiNote >= _viewModel.LowestNote; midiNote--)
        {
            double y = NoteToY(midiNote);
            bool isBlackKey = NoteItem.IsBlackKey(midiNote);

            var laneRect = new Rectangle
            {
                Width = totalWidth,
                Height = noteHeight,
                Fill = new SolidColorBrush(isBlackKey ? BlackKeyLaneColor : WhiteKeyLaneColor)
            };

            Canvas.SetLeft(laneRect, 0);
            Canvas.SetTop(laneRect, y);
            GridLinesCanvas.Children.Add(laneRect);
        }

        // Draw vertical grid lines (beats and bars)
        int totalBeatsInt = (int)Math.Ceiling(_viewModel.TotalBeats);
        for (int beat = 0; beat <= totalBeatsInt; beat++)
        {
            double x = beat * beatWidth;
            bool isBarLine = beat % BeatsPerBar == 0;

            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = totalHeight,
                Stroke = new SolidColorBrush(isBarLine ? BarLineColor : GridLineColor),
                StrokeThickness = isBarLine ? 2.0 : 1.0,
                Opacity = isBarLine ? 0.8 : 0.5
            };
            GridLinesCanvas.Children.Add(line);

            // Sub-beat lines based on grid resolution
            if (beat < totalBeatsInt && beatWidth > 30)
            {
                int subdivisions = (int)(1.0 / _viewModel.GridResolution);
                for (int sub = 1; sub < subdivisions && sub <= 4; sub++)
                {
                    double subX = x + (sub * beatWidth * _viewModel.GridResolution);
                    var subLine = new Line
                    {
                        X1 = subX,
                        Y1 = 0,
                        X2 = subX,
                        Y2 = totalHeight,
                        Stroke = new SolidColorBrush(GridLineColor),
                        StrokeThickness = 0.5,
                        Opacity = 0.3
                    };
                    GridLinesCanvas.Children.Add(subLine);
                }
            }
        }

        // Draw horizontal lines between note lanes
        int noteCount = _viewModel.HighestNote - _viewModel.LowestNote + 1;
        for (int i = 0; i <= noteCount; i++)
        {
            double y = i * noteHeight;
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = totalWidth,
                Y2 = y,
                Stroke = new SolidColorBrush(GridLineColor),
                StrokeThickness = 0.5,
                Opacity = 0.3
            };
            GridLinesCanvas.Children.Add(line);
        }
    }

    /// <summary>
    /// Renders all notes on the canvas.
    /// </summary>
    private void RenderNotes()
    {
        NoteCanvas.Children.Clear();
        _noteRectangles.Clear();

        foreach (var note in _viewModel.Notes)
        {
            var rect = CreateNoteRectangle(note);
            _noteRectangles[note] = rect;
            NoteCanvas.Children.Add(rect);
        }
    }

    /// <summary>
    /// Creates a visual rectangle for a note.
    /// </summary>
    private Rectangle CreateNoteRectangle(NoteItem note)
    {
        double beatWidth = GetEffectiveBeatWidth();
        double noteHeight = GetEffectiveNoteHeight();

        double x = note.Start * beatWidth;
        double y = NoteToY(note.Pitch);
        double width = Math.Max(note.Duration * beatWidth - 2, 4);
        double height = noteHeight - 2;

        // Get velocity-based color (blue=soft, red=loud)
        var noteColor = GetVelocityColor(note.Velocity);
        double velocityOpacity = 0.5 + (note.Velocity / 127.0) * 0.5;

        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = NoteCornerRadius,
            RadiusY = NoteCornerRadius,
            Fill = new SolidColorBrush(noteColor) { Opacity = velocityOpacity },
            Stroke = note.IsSelected ? new SolidColorBrush(Colors.White) : null,
            StrokeThickness = note.IsSelected ? 2.0 : 0,
            Cursor = Cursors.Hand,
            Tag = note
        };

        // Apply effect
        rect.Effect = note.IsSelected
            ? FindResource("SelectedNoteGlow") as System.Windows.Media.Effects.Effect
            : FindResource("NoteDropShadow") as System.Windows.Media.Effects.Effect;

        Canvas.SetLeft(rect, x + 1);
        Canvas.SetTop(rect, y + 1);

        // Attach event handlers
        rect.MouseLeftButtonDown += OnNoteMouseLeftButtonDown;
        rect.MouseRightButtonDown += OnNoteMouseRightButtonDown;

        return rect;
    }

    /// <summary>
    /// Gets the note color based on velocity (blue=soft, red=loud).
    /// </summary>
    private static Color GetVelocityColor(int velocity)
    {
        double t = velocity / 127.0;

        if (t < 0.33)
        {
            // Blue to Cyan (low velocity)
            double factor = t / 0.33;
            return Color.FromRgb(
                (byte)(0x00),
                (byte)(0x66 + (0x99 * factor)),
                (byte)(0xFF));
        }
        else if (t < 0.66)
        {
            // Cyan to Yellow (mid velocity)
            double factor = (t - 0.33) / 0.33;
            return Color.FromRgb(
                (byte)(0xFF * factor),
                (byte)(0xFF),
                (byte)(0xFF * (1 - factor)));
        }
        else
        {
            // Yellow to Red (high velocity)
            double factor = (t - 0.66) / 0.34;
            return Color.FromRgb(
                (byte)(0xFF),
                (byte)(0xFF * (1 - factor)),
                (byte)(0x00));
        }
    }

    /// <summary>
    /// Updates the note count display.
    /// </summary>
    private void UpdateNoteCount()
    {
        NoteCountText.Text = _viewModel.Notes.Count.ToString();
    }

    #endregion

    #region Ghost Note

    /// <summary>
    /// Shows a ghost note preview at the specified position.
    /// </summary>
    private void ShowGhostNote(int pitch, double beat)
    {
        if (pitch < _viewModel.LowestNote || pitch > _viewModel.HighestNote)
        {
            HideGhostNote();
            return;
        }

        double beatWidth = GetEffectiveBeatWidth();
        double noteHeight = GetEffectiveNoteHeight();

        double snappedBeat = _viewModel.IsSnapEnabled ? _viewModel.SnapToGrid(beat) : beat;
        double x = snappedBeat * beatWidth;
        double y = NoteToY(pitch);
        double width = _viewModel.GridResolution * beatWidth;

        if (_ghostNoteRect == null)
        {
            _ghostNoteRect = new Rectangle
            {
                RadiusX = NoteCornerRadius,
                RadiusY = NoteCornerRadius,
                Fill = new SolidColorBrush(AccentColor) { Opacity = 0.3 },
                Stroke = new SolidColorBrush(AccentColor),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            GhostNoteCanvas.Children.Add(_ghostNoteRect);
        }

        _ghostNoteRect.Width = Math.Max(width - 2, 4);
        _ghostNoteRect.Height = noteHeight - 2;
        _ghostNoteRect.Visibility = Visibility.Visible;

        Canvas.SetLeft(_ghostNoteRect, x + 1);
        Canvas.SetTop(_ghostNoteRect, y + 1);

        _ghostNotePitch = pitch;
        _ghostNoteBeat = snappedBeat;
    }

    /// <summary>
    /// Hides the ghost note preview.
    /// </summary>
    private void HideGhostNote()
    {
        if (_ghostNoteRect != null)
        {
            _ghostNoteRect.Visibility = Visibility.Collapsed;
        }
        _ghostNotePitch = -1;
        _ghostNoteBeat = -1;
    }

    #endregion

    #region Coordinate Conversion

    private double GetTotalWidth()
    {
        return _viewModel.TotalBeats * GetEffectiveBeatWidth();
    }

    private double GetTotalHeight()
    {
        return (_viewModel.HighestNote - _viewModel.LowestNote + 1) * GetEffectiveNoteHeight();
    }

    private double GetEffectiveBeatWidth()
    {
        return DefaultBeatWidth * _viewModel.HorizontalZoom;
    }

    private double GetEffectiveNoteHeight()
    {
        return DefaultNoteHeight;
    }

    private double XToBeat(double x)
    {
        return x / GetEffectiveBeatWidth();
    }

    private int YToNote(double y)
    {
        double noteHeight = GetEffectiveNoteHeight();
        int noteIndex = (int)(y / noteHeight);
        return _viewModel.HighestNote - noteIndex;
    }

    private double NoteToY(int midiNote)
    {
        double noteHeight = GetEffectiveNoteHeight();
        return (_viewModel.HighestNote - midiNote) * noteHeight;
    }

    #endregion

    #region Mouse Event Handlers - Canvas

    private void NoteCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(NoteCanvas);
        double beat = XToBeat(position.X);
        int pitch = YToNote(position.Y);

        if (pitch >= _viewModel.LowestNote && pitch <= _viewModel.HighestNote)
        {
            // Check if clicking on an existing note
            var existingNote = _viewModel.GetNoteAt(pitch, beat);
            if (existingNote == null)
            {
                // Add a new note
                double snappedBeat = _viewModel.IsSnapEnabled ? _viewModel.SnapToGrid(beat) : beat;
                _viewModel.AddNote(pitch, snappedBeat, _viewModel.GridResolution, _viewModel.DefaultVelocity);
                RenderNotes();
            }
        }

        e.Handled = true;
    }

    private void NoteCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(NoteCanvas);
        double beat = XToBeat(position.X);
        int pitch = YToNote(position.Y);

        // Delete note at this position
        var noteToDelete = _viewModel.GetNoteAt(pitch, beat);
        if (noteToDelete != null)
        {
            _viewModel.DeleteNote(noteToDelete);
            RenderNotes();
        }

        e.Handled = true;
    }

    private void NoteCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(NoteCanvas);
        double beat = XToBeat(position.X);
        int pitch = YToNote(position.Y);

        ShowGhostNote(pitch, beat);
    }

    private void NoteCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        HideGhostNote();
    }

    #endregion

    #region Mouse Event Handlers - Notes

    private void OnNoteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle rect && rect.Tag is NoteItem note)
        {
            bool shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (shiftHeld)
            {
                // Toggle selection
                if (note.IsSelected)
                {
                    _viewModel.DeselectNote(note);
                }
                else
                {
                    _viewModel.SelectNote(note, addToSelection: true);
                }
            }
            else
            {
                // Select only this note
                _viewModel.SelectNote(note, addToSelection: false);
            }

            RenderNotes();
            e.Handled = true;
        }
    }

    private void OnNoteMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle rect && rect.Tag is NoteItem note)
        {
            // Right-click to delete
            _viewModel.DeleteNote(note);
            RenderNotes();
            e.Handled = true;
        }
    }

    #endregion

    #region Mouse Event Handlers - Piano Keyboard

    private void PianoKeyboardCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(PianoKeyboardCanvas);
        int pitch = YToNote(position.Y);

        if (pitch >= _viewModel.LowestNote && pitch <= _viewModel.HighestNote)
        {
            // Could trigger note preview here
            _viewModel.StatusMessage = $"Key: {NoteItem.GetNoteName(pitch)}";
        }
    }

    #endregion

    #region Toolbar Event Handlers

    private void GridResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _viewModel == null) return;

        if (GridResolutionComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagValue)
        {
            if (double.TryParse(tagValue, out double resolution))
            {
                _viewModel.GridResolution = resolution;
                RenderGridLines();
            }
        }
    }

    private void QuantizeButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.QuantizeSelectedCommand.Execute(null);
        RenderNotes();
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ZoomInCommand.Execute(null);
        UpdateZoomDisplay();
        RenderAll();
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ZoomOutCommand.Execute(null);
        UpdateZoomDisplay();
        RenderAll();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _viewModel == null) return;

        UpdateZoomDisplay();
        RenderAll();
    }

    private void UpdateZoomDisplay()
    {
        if (_viewModel == null || ZoomPercentText == null) return;

        int percent = (int)(_viewModel.HorizontalZoom * 100);
        ZoomPercentText.Text = $"{percent}%";
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectAllCommand.Execute(null);
        RenderNotes();
    }

    private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedCommand.Execute(null);
        RenderNotes();
    }

    #endregion

    #region Scroll Synchronization

    private void NoteGridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Synchronize keyboard scroll with note grid vertical scroll
        KeyboardScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the ViewModel for external access.
    /// </summary>
    public PianoRollViewModel ViewModel => _viewModel;

    /// <summary>
    /// Refreshes the entire display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    /// <summary>
    /// Sets the ViewModel externally.
    /// </summary>
    /// <param name="viewModel">The new ViewModel.</param>
    public void SetViewModel(PianoRollViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.Notes.CollectionChanged += (_, _) =>
        {
            RenderNotes();
            UpdateNoteCount();
        };

        RenderAll();
    }

    #endregion
}
