// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
using MusicEngineEditor.Controls;
using MusicEngineEditor.Models;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Code-behind for the ScoreEditorView user control.
/// Manages the music notation editor interface with staff rendering,
/// note input, and playback visualization.
/// </summary>
public partial class ScoreEditorView : UserControl
{
    #region Constants

    private const double DefaultMeasureWidth = 200.0;
    private const double StaffMarginLeft = 80.0;
    private const double StaffMarginTop = 60.0;
    private const double StaffMarginBottom = 40.0;
    private const double StaffSpacing = 120.0; // Space between grand staff staves
    private const double PlayheadWidth = 2.0;

    #endregion

    #region Private Fields

    private readonly ScoreEditorViewModel _viewModel;
    private readonly ScoreRenderer _renderer;

    private bool _isDragging;
    private bool _isSelecting;
    private Point _dragStartPoint;
    private Point _lastMousePosition;
    private ScoreNote? _draggedNote;
    private Shapes.Rectangle? _selectionRectangle;
    private List<ScoreNote> _preSelectionState = [];

    // Cached visual elements
    private readonly List<UIElement> _noteVisuals = [];
    private Shapes.Line? _playheadLine;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new ScoreEditorView and initializes the ScoreEditorViewModel.
    /// </summary>
    public ScoreEditorView()
    {
        InitializeComponent();

        _viewModel = new ScoreEditorViewModel();
        _renderer = new ScoreRenderer();

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the ScoreEditorViewModel associated with this view.
    /// </summary>
    public ScoreEditorViewModel ViewModel => _viewModel;

    /// <summary>
    /// Gets the ScoreRenderer used for drawing notation.
    /// </summary>
    public ScoreRenderer Renderer => _renderer;

    /// <summary>
    /// Gets the effective measure width based on zoom.
    /// </summary>
    private double EffectiveMeasureWidth => DefaultMeasureWidth * _viewModel.Zoom;

    /// <summary>
    /// Gets the effective staff line spacing based on zoom.
    /// </summary>
    private double EffectiveStaffLineSpacing => _renderer.StaffLineSpacing * _viewModel.Zoom;

    #endregion

    #region Lifecycle Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set up keyboard focus
        Focusable = true;
        Focus();

        // Wire up events
        PreviewKeyDown += OnPreviewKeyDown;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Notes.CollectionChanged += OnNotesCollectionChanged;

        // Initial render
        UpdateCanvasSize();
        RenderScore();

        // Load demo notes for testing
        _viewModel.LoadDemoNotes();
        RenderScore();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PreviewKeyDown -= OnPreviewKeyDown;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Notes.CollectionChanged -= OnNotesCollectionChanged;
        _viewModel.Dispose();
    }

    #endregion

    #region Property Changed Handlers

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScoreEditorViewModel.Zoom):
            case nameof(ScoreEditorViewModel.MeasureCount):
            case nameof(ScoreEditorViewModel.UseGrandStaff):
                UpdateCanvasSize();
                RenderScore();
                break;

            case nameof(ScoreEditorViewModel.Clef):
            case nameof(ScoreEditorViewModel.KeySignature):
            case nameof(ScoreEditorViewModel.TimeSignatureNumerator):
            case nameof(ScoreEditorViewModel.TimeSignatureDenominator):
            case nameof(ScoreEditorViewModel.ShowMeasureNumbers):
            case nameof(ScoreEditorViewModel.ShowNoteNames):
                RenderScore();
                break;

            case nameof(ScoreEditorViewModel.PlayheadPosition):
                UpdatePlayhead();
                break;

            case nameof(ScoreEditorViewModel.CurrentTool):
                UpdateToolSelection();
                break;
        }
    }

    private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RenderScore();
    }

    #endregion

    #region Canvas Rendering

    /// <summary>
    /// Updates the canvas size based on measure count and zoom.
    /// </summary>
    private void UpdateCanvasSize()
    {
        double width = StaffMarginLeft + (_viewModel.MeasureCount * EffectiveMeasureWidth) + 50;
        double height = StaffMarginTop + StaffMarginBottom + (4 * EffectiveStaffLineSpacing);

        if (_viewModel.UseGrandStaff)
        {
            height += StaffSpacing * _viewModel.Zoom;
        }

        ScoreCanvas.Width = width;
        ScoreCanvas.Height = Math.Max(height, 300);
    }

    /// <summary>
    /// Renders the entire score including staff, notes, and other elements.
    /// </summary>
    private void RenderScore()
    {
        ScoreCanvas.Children.Clear();
        _noteVisuals.Clear();

        // Draw background
        var background = new Shapes.Rectangle
        {
            Width = ScoreCanvas.Width,
            Height = ScoreCanvas.Height,
            Fill = new SolidColorBrush(Color.FromRgb(0x25, 0x26, 0x29))
        };
        ScoreCanvas.Children.Add(background);

        // Draw staff(s)
        DrawStaff(_viewModel.Clef, StaffMarginTop);

        if (_viewModel.UseGrandStaff)
        {
            DrawStaff(_viewModel.SecondStaffClef, StaffMarginTop + StaffSpacing * _viewModel.Zoom);
            DrawGrandStaffBrace();
        }

        // Draw bar lines
        DrawBarLines();

        // Draw key signature
        DrawKeySignature();

        // Draw time signature
        DrawTimeSignature();

        // Draw notes
        DrawNotes();

        // Draw measure numbers
        if (_viewModel.ShowMeasureNumbers)
        {
            DrawMeasureNumbers();
        }

        // Draw playhead
        CreatePlayhead();
        UpdatePlayhead();
    }

    /// <summary>
    /// Draws a five-line staff at the specified Y position.
    /// </summary>
    private void DrawStaff(ClefType clef, double topY)
    {
        double staffWidth = _viewModel.MeasureCount * EffectiveMeasureWidth;
        var staffBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        var staffPen = new Pen(staffBrush, 1);

        // Draw the five staff lines
        for (int i = 0; i < 5; i++)
        {
            double y = topY + i * EffectiveStaffLineSpacing;
            var line = new Shapes.Line
            {
                X1 = StaffMarginLeft,
                Y1 = y,
                X2 = StaffMarginLeft + staffWidth,
                Y2 = y,
                Stroke = staffBrush,
                StrokeThickness = 1
            };
            ScoreCanvas.Children.Add(line);
        }

        // Draw the clef
        DrawClef(clef, StaffMarginLeft + 10, topY);
    }

    /// <summary>
    /// Draws a clef symbol at the specified position.
    /// </summary>
    private void DrawClef(ClefType clef, double x, double topY)
    {
        var textBlock = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 36 * _viewModel.Zoom,
            Foreground = new SolidColorBrush(Colors.White)
        };

        double yOffset = 0;

        switch (clef)
        {
            case ClefType.Treble:
                textBlock.Text = "\U0001D11E"; // G clef
                yOffset = -EffectiveStaffLineSpacing * 1.5;
                break;
            case ClefType.Bass:
                textBlock.Text = "\U0001D122"; // F clef
                yOffset = -EffectiveStaffLineSpacing * 0.5;
                break;
            case ClefType.Alto:
            case ClefType.Tenor:
                textBlock.Text = "\U0001D121"; // C clef
                yOffset = 0;
                break;
        }

        // Fallback to text-based representation if symbol doesn't render
        textBlock.Text = clef switch
        {
            ClefType.Treble => "G",
            ClefType.Bass => "F",
            ClefType.Alto => "C",
            ClefType.Tenor => "C",
            _ => "G"
        };
        textBlock.FontFamily = new FontFamily("Segoe UI");
        textBlock.FontWeight = FontWeights.Bold;
        textBlock.FontSize = 24 * _viewModel.Zoom;

        Canvas.SetLeft(textBlock, x);
        Canvas.SetTop(textBlock, topY + yOffset + EffectiveStaffLineSpacing);
        ScoreCanvas.Children.Add(textBlock);
    }

    /// <summary>
    /// Draws bar lines for all measures.
    /// </summary>
    private void DrawBarLines()
    {
        var barBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        double staffHeight = 4 * EffectiveStaffLineSpacing;
        double topY = StaffMarginTop;

        if (_viewModel.UseGrandStaff)
        {
            staffHeight = StaffSpacing * _viewModel.Zoom + 4 * EffectiveStaffLineSpacing;
        }

        for (int measure = 0; measure <= _viewModel.MeasureCount; measure++)
        {
            double x = StaffMarginLeft + measure * EffectiveMeasureWidth;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = topY,
                X2 = x,
                Y2 = topY + staffHeight,
                Stroke = barBrush,
                StrokeThickness = measure == 0 || measure == _viewModel.MeasureCount ? 2 : 1
            };
            ScoreCanvas.Children.Add(line);
        }
    }

    /// <summary>
    /// Draws the key signature after the clef.
    /// </summary>
    private void DrawKeySignature()
    {
        // Key signature rendering is handled by the ScoreRenderer
        // For now, display a text representation
        var keyText = new TextBlock
        {
            Text = _viewModel.KeySignature,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12 * _viewModel.Zoom,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0))
        };

        Canvas.SetLeft(keyText, StaffMarginLeft + 40);
        Canvas.SetTop(keyText, StaffMarginTop - 20);
        ScoreCanvas.Children.Add(keyText);
    }

    /// <summary>
    /// Draws the time signature.
    /// </summary>
    private void DrawTimeSignature()
    {
        double x = StaffMarginLeft + 55;
        double centerY = StaffMarginTop + 2 * EffectiveStaffLineSpacing;

        // Numerator
        var numText = new TextBlock
        {
            Text = _viewModel.TimeSignatureNumerator.ToString(),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 20 * _viewModel.Zoom,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White)
        };
        Canvas.SetLeft(numText, x);
        Canvas.SetTop(numText, centerY - 25 * _viewModel.Zoom);
        ScoreCanvas.Children.Add(numText);

        // Denominator
        var denomText = new TextBlock
        {
            Text = _viewModel.TimeSignatureDenominator.ToString(),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 20 * _viewModel.Zoom,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White)
        };
        Canvas.SetLeft(denomText, x);
        Canvas.SetTop(denomText, centerY + 5 * _viewModel.Zoom);
        ScoreCanvas.Children.Add(denomText);
    }

    /// <summary>
    /// Draws all notes on the score.
    /// </summary>
    private void DrawNotes()
    {
        foreach (var note in _viewModel.Notes)
        {
            DrawNote(note);
        }
    }

    /// <summary>
    /// Draws a single note on the score.
    /// </summary>
    private void DrawNote(ScoreNote note)
    {
        // Calculate position
        double x = GetNoteXPosition(note.Measure, note.BeatPosition);
        double y = GetNoteYPosition(note.MidiPitch, _viewModel.Clef);

        // Determine stem direction (notes above middle line have stems down)
        int staffLineOffset = ScoreNote.GetStaffLineOffset(note.MidiPitch, _viewModel.Clef);
        bool stemUp = staffLineOffset <= 0;

        // Create note visual
        var noteVisual = CreateNoteVisual(note, x, y, stemUp);

        ScoreCanvas.Children.Add(noteVisual);
        _noteVisuals.Add(noteVisual);

        // Draw accidental if needed
        if (note.Accidental != AccidentalType.None)
        {
            DrawAccidental(note.Accidental, x - 15 * _viewModel.Zoom, y);
        }

        // Draw dot if dotted
        if (note.IsDotted)
        {
            DrawDot(x + 10 * _viewModel.Zoom, y);
        }

        // Draw ledger lines if needed
        DrawLedgerLinesForNote(note.MidiPitch, x);

        // Draw note name if enabled
        if (_viewModel.ShowNoteNames)
        {
            DrawNoteName(note, x, y);
        }

        // Draw articulation if present
        if (note.Articulation != ArticulationType.None)
        {
            DrawArticulation(note.Articulation, x, y, stemUp);
        }
    }

    /// <summary>
    /// Creates a visual element for a note.
    /// </summary>
    private UIElement CreateNoteVisual(ScoreNote note, double x, double y, bool stemUp)
    {
        var container = new Canvas();

        var brush = note.IsSelected
            ? new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF))
            : new SolidColorBrush(Colors.White);

        bool isFilled = note.Duration != NoteDuration.Whole && note.Duration != NoteDuration.Half;
        bool needsStem = note.Duration != NoteDuration.Whole;

        // Note head
        double noteWidth = 12 * _viewModel.Zoom;
        double noteHeight = 8 * _viewModel.Zoom;

        if (note.Duration == NoteDuration.Whole)
        {
            noteWidth *= 1.3;
        }

        var noteHead = new Shapes.Ellipse
        {
            Width = noteWidth,
            Height = noteHeight,
            Fill = isFilled ? brush : Brushes.Transparent,
            Stroke = brush,
            StrokeThickness = isFilled ? 0 : 1.5,
            RenderTransform = new RotateTransform(-15, noteWidth / 2, noteHeight / 2)
        };

        Canvas.SetLeft(noteHead, -noteWidth / 2);
        Canvas.SetTop(noteHead, -noteHeight / 2);
        container.Children.Add(noteHead);

        // Stem
        if (needsStem)
        {
            double stemHeight = EffectiveStaffLineSpacing * 3.5;
            double stemX = stemUp ? noteWidth / 2 - 1 : -noteWidth / 2 + 1;
            double stemY1 = 0;
            double stemY2 = stemUp ? -stemHeight : stemHeight;

            var stem = new Shapes.Line
            {
                X1 = stemX,
                Y1 = stemY1,
                X2 = stemX,
                Y2 = stemY2,
                Stroke = brush,
                StrokeThickness = 1.5
            };
            container.Children.Add(stem);

            // Flags for eighth notes and smaller
            int flagCount = GetFlagCount(note.Duration);
            if (flagCount > 0)
            {
                DrawFlags(container, stemX, stemY2, flagCount, stemUp, brush);
            }
        }

        // Store note reference for hit testing
        container.Tag = note;

        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, y);

        return container;
    }

    /// <summary>
    /// Draws note flags for eighth notes and smaller.
    /// </summary>
    private void DrawFlags(Canvas container, double stemX, double stemEndY, int count, bool stemUp, Brush brush)
    {
        for (int i = 0; i < count; i++)
        {
            double yOffset = i * 6 * _viewModel.Zoom * (stemUp ? 1 : -1);
            double startY = stemEndY + yOffset;

            var flag = new Shapes.Path
            {
                Stroke = brush,
                StrokeThickness = 1.5
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(stemX, startY) };

            double flagWidth = 8 * _viewModel.Zoom;
            double flagHeight = 12 * _viewModel.Zoom;

            if (stemUp)
            {
                figure.Segments.Add(new BezierSegment(
                    new Point(stemX + flagWidth * 0.8, startY + flagHeight * 0.3),
                    new Point(stemX + flagWidth, startY + flagHeight * 0.7),
                    new Point(stemX + flagWidth * 0.3, startY + flagHeight),
                    true));
            }
            else
            {
                figure.Segments.Add(new BezierSegment(
                    new Point(stemX + flagWidth * 0.8, startY - flagHeight * 0.3),
                    new Point(stemX + flagWidth, startY - flagHeight * 0.7),
                    new Point(stemX + flagWidth * 0.3, startY - flagHeight),
                    true));
            }

            geometry.Figures.Add(figure);
            flag.Data = geometry;
            container.Children.Add(flag);
        }
    }

    /// <summary>
    /// Draws an accidental symbol.
    /// </summary>
    private void DrawAccidental(AccidentalType accidental, double x, double y)
    {
        string symbol = accidental switch
        {
            AccidentalType.Sharp => "#",
            AccidentalType.Flat => "b",
            AccidentalType.Natural => "n",
            AccidentalType.DoubleSharp => "x",
            AccidentalType.DoubleFlat => "bb",
            _ => ""
        };

        var text = new TextBlock
        {
            Text = symbol,
            FontSize = 14 * _viewModel.Zoom,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White)
        };

        Canvas.SetLeft(text, x);
        Canvas.SetTop(text, y - 8 * _viewModel.Zoom);
        ScoreCanvas.Children.Add(text);
    }

    /// <summary>
    /// Draws a dot for dotted notes.
    /// </summary>
    private void DrawDot(double x, double y)
    {
        var dot = new Shapes.Ellipse
        {
            Width = 4 * _viewModel.Zoom,
            Height = 4 * _viewModel.Zoom,
            Fill = new SolidColorBrush(Colors.White)
        };

        Canvas.SetLeft(dot, x);
        Canvas.SetTop(dot, y - 2 * _viewModel.Zoom);
        ScoreCanvas.Children.Add(dot);
    }

    /// <summary>
    /// Draws ledger lines for notes above or below the staff.
    /// </summary>
    private void DrawLedgerLinesForNote(int midiPitch, double x)
    {
        int staffLineOffset = ScoreNote.GetStaffLineOffset(midiPitch, _viewModel.Clef);
        double staffTopY = StaffMarginTop;
        double staffBottomY = StaffMarginTop + 4 * EffectiveStaffLineSpacing;

        var ledgerBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double lineWidth = 20 * _viewModel.Zoom;

        // Ledger lines above staff (staffLineOffset > 4 means above top line)
        if (staffLineOffset > 4)
        {
            for (int line = 6; line <= staffLineOffset; line += 2)
            {
                double y = staffTopY - (line - 4) * (EffectiveStaffLineSpacing / 2);
                var ledgerLine = new Shapes.Line
                {
                    X1 = x - lineWidth / 2,
                    Y1 = y,
                    X2 = x + lineWidth / 2,
                    Y2 = y,
                    Stroke = ledgerBrush,
                    StrokeThickness = 1
                };
                ScoreCanvas.Children.Add(ledgerLine);
            }
        }

        // Ledger lines below staff (staffLineOffset < 0 means below bottom line)
        if (staffLineOffset < 0)
        {
            for (int line = -2; line >= staffLineOffset; line -= 2)
            {
                double y = staffBottomY - line * (EffectiveStaffLineSpacing / 2);
                var ledgerLine = new Shapes.Line
                {
                    X1 = x - lineWidth / 2,
                    Y1 = y,
                    X2 = x + lineWidth / 2,
                    Y2 = y,
                    Stroke = ledgerBrush,
                    StrokeThickness = 1
                };
                ScoreCanvas.Children.Add(ledgerLine);
            }
        }
    }

    /// <summary>
    /// Draws the note name below the note.
    /// </summary>
    private void DrawNoteName(ScoreNote note, double x, double y)
    {
        var text = new TextBlock
        {
            Text = note.NoteName,
            FontSize = 9 * _viewModel.Zoom,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A))
        };

        Canvas.SetLeft(text, x - 8 * _viewModel.Zoom);
        Canvas.SetTop(text, y + 15 * _viewModel.Zoom);
        ScoreCanvas.Children.Add(text);
    }

    /// <summary>
    /// Draws an articulation marking.
    /// </summary>
    private void DrawArticulation(ArticulationType articulation, double x, double y, bool aboveNote)
    {
        string symbol = articulation switch
        {
            ArticulationType.Staccato => ".",
            ArticulationType.Tenuto => "-",
            ArticulationType.Accent => ">",
            ArticulationType.Marcato => "^",
            ArticulationType.Fermata => "U",
            _ => ""
        };

        if (string.IsNullOrEmpty(symbol)) return;

        double offset = (aboveNote ? -20 : 20) * _viewModel.Zoom;

        var text = new TextBlock
        {
            Text = symbol,
            FontSize = 14 * _viewModel.Zoom,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White)
        };

        Canvas.SetLeft(text, x - 4 * _viewModel.Zoom);
        Canvas.SetTop(text, y + offset);
        ScoreCanvas.Children.Add(text);
    }

    /// <summary>
    /// Draws measure numbers above the staff.
    /// </summary>
    private void DrawMeasureNumbers()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));

        for (int measure = 1; measure <= _viewModel.MeasureCount; measure++)
        {
            double x = StaffMarginLeft + (measure - 1) * EffectiveMeasureWidth + 5;

            var text = new TextBlock
            {
                Text = measure.ToString(),
                FontSize = 10 * _viewModel.Zoom,
                Foreground = brush
            };

            Canvas.SetLeft(text, x);
            Canvas.SetTop(text, StaffMarginTop - 20 * _viewModel.Zoom);
            ScoreCanvas.Children.Add(text);
        }
    }

    /// <summary>
    /// Draws the brace for grand staff.
    /// </summary>
    private void DrawGrandStaffBrace()
    {
        // Simplified brace representation
        var brace = new TextBlock
        {
            Text = "{",
            FontSize = 80 * _viewModel.Zoom,
            Foreground = new SolidColorBrush(Colors.White),
            RenderTransform = new ScaleTransform(1, 1.5)
        };

        Canvas.SetLeft(brace, StaffMarginLeft - 30);
        Canvas.SetTop(brace, StaffMarginTop - 10 * _viewModel.Zoom);
        ScoreCanvas.Children.Add(brace);
    }

    /// <summary>
    /// Creates the playhead line element.
    /// </summary>
    private void CreatePlayhead()
    {
        double staffHeight = 4 * EffectiveStaffLineSpacing;
        if (_viewModel.UseGrandStaff)
        {
            staffHeight = StaffSpacing * _viewModel.Zoom + 4 * EffectiveStaffLineSpacing;
        }

        _playheadLine = new Shapes.Line
        {
            Y1 = StaffMarginTop,
            Y2 = StaffMarginTop + staffHeight,
            Stroke = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF)),
            StrokeThickness = PlayheadWidth,
            Visibility = Visibility.Visible
        };

        ScoreCanvas.Children.Add(_playheadLine);
    }

    /// <summary>
    /// Updates the playhead position.
    /// </summary>
    private void UpdatePlayhead()
    {
        if (_playheadLine == null) return;

        double x = StaffMarginLeft + (_viewModel.PlayheadPosition / _viewModel.BeatsPerMeasure) * EffectiveMeasureWidth;

        _playheadLine.X1 = x;
        _playheadLine.X2 = x;

        // Update current measure display
        int currentMeasure = (int)(_viewModel.PlayheadPosition / _viewModel.BeatsPerMeasure) + 1;
        CurrentMeasureText.Text = currentMeasure.ToString();
    }

    #endregion

    #region Position Calculations

    /// <summary>
    /// Gets the X position for a note given measure and beat position.
    /// </summary>
    private double GetNoteXPosition(int measure, double beatPosition)
    {
        double measureX = StaffMarginLeft + (measure - 1) * EffectiveMeasureWidth;
        double beatX = (beatPosition / _viewModel.BeatsPerMeasure) * EffectiveMeasureWidth;
        return measureX + beatX + 70 * _viewModel.Zoom; // Offset for clef/key/time signatures
    }

    /// <summary>
    /// Gets the Y position for a note given MIDI pitch and clef.
    /// </summary>
    private double GetNoteYPosition(int midiPitch, ClefType clef)
    {
        int staffLineOffset = ScoreNote.GetStaffLineOffset(midiPitch, clef);
        double staffCenterY = StaffMarginTop + 2 * EffectiveStaffLineSpacing; // Middle line (B in treble)
        return staffCenterY - (staffLineOffset * EffectiveStaffLineSpacing / 2);
    }

    /// <summary>
    /// Gets the measure and beat position from canvas coordinates.
    /// </summary>
    private (int measure, double beatPosition) GetPositionFromCanvas(Point canvasPoint)
    {
        double adjustedX = canvasPoint.X - StaffMarginLeft - 70 * _viewModel.Zoom;
        int measure = (int)(adjustedX / EffectiveMeasureWidth) + 1;
        double beatPosition = ((adjustedX % EffectiveMeasureWidth) / EffectiveMeasureWidth) * _viewModel.BeatsPerMeasure;

        measure = Math.Max(1, Math.Min(measure, _viewModel.MeasureCount));
        beatPosition = Math.Max(0, Math.Min(beatPosition, _viewModel.BeatsPerMeasure - 0.01));

        return (measure, beatPosition);
    }

    /// <summary>
    /// Gets the MIDI pitch from a Y position on the canvas.
    /// </summary>
    private int GetPitchFromYPosition(double y)
    {
        double staffCenterY = StaffMarginTop + 2 * EffectiveStaffLineSpacing;
        int staffLineOffset = (int)Math.Round((staffCenterY - y) / (EffectiveStaffLineSpacing / 2));

        // Convert staff line offset back to MIDI pitch
        // This is an approximation - proper implementation would use the clef
        int middleC = 60;
        int octaveOffset = staffLineOffset / 7;
        int[] diatonicToChromatic = { 0, 2, 4, 5, 7, 9, 11 };
        int noteInOctave = ((staffLineOffset % 7) + 7) % 7;

        return middleC + octaveOffset * 12 + diatonicToChromatic[noteInOctave];
    }

    /// <summary>
    /// Gets the number of flags for a note duration.
    /// </summary>
    private int GetFlagCount(NoteDuration duration)
    {
        return duration switch
        {
            NoteDuration.Eighth => 1,
            NoteDuration.Sixteenth => 2,
            NoteDuration.ThirtySecond => 3,
            _ => 0
        };
    }

    #endregion

    #region Mouse Event Handlers

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(ScoreCanvas);
        _dragStartPoint = position;
        _lastMousePosition = position;

        // Check if clicking on a note
        var hitNote = GetNoteAtCanvasPosition(position);

        if (hitNote != null)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Toggle selection
                hitNote.IsSelected = !hitNote.IsSelected;
                if (hitNote.IsSelected)
                {
                    if (!_viewModel.SelectedNotes.Contains(hitNote))
                        _viewModel.SelectedNotes.Add(hitNote);
                }
                else
                {
                    _viewModel.SelectedNotes.Remove(hitNote);
                }
            }
            else if (!hitNote.IsSelected)
            {
                // Select only this note
                _viewModel.DeselectAllCommand.Execute(null);
                hitNote.IsSelected = true;
                _viewModel.SelectedNotes.Add(hitNote);
            }

            // Start drag
            if (_viewModel.CurrentTool == ScoreEditorTool.Select)
            {
                _isDragging = true;
                _draggedNote = hitNote;
            }
            else if (_viewModel.CurrentTool == ScoreEditorTool.Erase)
            {
                _viewModel.DeleteNoteCommand.Execute(hitNote);
            }

            RenderScore();
        }
        else if (_viewModel.CurrentTool == ScoreEditorTool.Draw)
        {
            // Create new note
            var (measure, beatPosition) = GetPositionFromCanvas(position);
            int pitch = GetPitchFromYPosition(position.Y);

            var newNote = _viewModel.CreateNote(pitch, measure, beatPosition);
            RenderScore();

            _viewModel.StatusMessage = $"Added {newNote.NoteName} at measure {measure}, beat {beatPosition:F2}";
        }
        else if (_viewModel.CurrentTool == ScoreEditorTool.Select)
        {
            // Start selection rectangle
            _viewModel.DeselectAllCommand.Execute(null);
            _isSelecting = true;
            _preSelectionState = _viewModel.SelectedNotes.ToList();
            StartSelectionRectangle(position);
        }

        ScoreCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(ScoreCanvas);

        if (_isDragging && _draggedNote != null)
        {
            // Move note(s)
            var delta = position - _lastMousePosition;

            foreach (var note in _viewModel.SelectedNotes)
            {
                var (newMeasure, newBeat) = GetPositionFromCanvas(new Point(
                    GetNoteXPosition(note.Measure, note.BeatPosition) + delta.X,
                    position.Y));

                note.Measure = newMeasure;
                note.BeatPosition = newBeat;
                note.MidiPitch = GetPitchFromYPosition(position.Y);
            }

            RenderScore();
        }
        else if (_isSelecting && _selectionRectangle != null)
        {
            // Update selection rectangle
            UpdateSelectionRectangle(position);
            UpdateSelectionFromRectangle();
        }

        _lastMousePosition = position;
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _draggedNote = null;
        }

        if (_isSelecting)
        {
            _isSelecting = false;
            EndSelectionRectangle();
        }

        ScoreCanvas.ReleaseMouseCapture();
    }

    private void OnCanvasMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(ScoreCanvas);
        var hitNote = GetNoteAtCanvasPosition(position);

        if (hitNote != null && _viewModel.CurrentTool == ScoreEditorTool.Select)
        {
            // Show context menu for note
            // For now, just select the note
            if (!hitNote.IsSelected)
            {
                _viewModel.DeselectAllCommand.Execute(null);
                hitNote.IsSelected = true;
                _viewModel.SelectedNotes.Add(hitNote);
                RenderScore();
            }
        }
    }

    /// <summary>
    /// Gets the note at the specified canvas position.
    /// </summary>
    private ScoreNote? GetNoteAtCanvasPosition(Point position)
    {
        foreach (var visual in _noteVisuals)
        {
            if (visual is Canvas noteCanvas && noteCanvas.Tag is ScoreNote note)
            {
                double noteX = Canvas.GetLeft(noteCanvas);
                double noteY = Canvas.GetTop(noteCanvas);
                double hitRadius = 15 * _viewModel.Zoom;

                if (Math.Abs(position.X - noteX) <= hitRadius &&
                    Math.Abs(position.Y - noteY) <= hitRadius)
                {
                    return note;
                }
            }
        }

        return null;
    }

    #endregion

    #region Selection Rectangle

    private void StartSelectionRectangle(Point startPoint)
    {
        _selectionRectangle = new Shapes.Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(0x33, 0x4A, 0x9E, 0xFF))
        };

        Canvas.SetLeft(_selectionRectangle, startPoint.X);
        Canvas.SetTop(_selectionRectangle, startPoint.Y);
        _selectionRectangle.Width = 0;
        _selectionRectangle.Height = 0;

        ScoreCanvas.Children.Add(_selectionRectangle);
    }

    private void UpdateSelectionRectangle(Point currentPoint)
    {
        if (_selectionRectangle == null) return;

        double x = Math.Min(_dragStartPoint.X, currentPoint.X);
        double y = Math.Min(_dragStartPoint.Y, currentPoint.Y);
        double width = Math.Abs(currentPoint.X - _dragStartPoint.X);
        double height = Math.Abs(currentPoint.Y - _dragStartPoint.Y);

        Canvas.SetLeft(_selectionRectangle, x);
        Canvas.SetTop(_selectionRectangle, y);
        _selectionRectangle.Width = width;
        _selectionRectangle.Height = height;
    }

    private void UpdateSelectionFromRectangle()
    {
        if (_selectionRectangle == null) return;

        var rect = new Rect(
            Canvas.GetLeft(_selectionRectangle),
            Canvas.GetTop(_selectionRectangle),
            _selectionRectangle.Width,
            _selectionRectangle.Height);

        foreach (var note in _viewModel.Notes)
        {
            double noteX = GetNoteXPosition(note.Measure, note.BeatPosition);
            double noteY = GetNoteYPosition(note.MidiPitch, _viewModel.Clef);

            bool isInRect = rect.Contains(new Point(noteX, noteY));

            if (isInRect && !note.IsSelected)
            {
                note.IsSelected = true;
                _viewModel.SelectedNotes.Add(note);
            }
            else if (!isInRect && note.IsSelected && !_preSelectionState.Contains(note))
            {
                note.IsSelected = false;
                _viewModel.SelectedNotes.Remove(note);
            }
        }

        RenderScore();
    }

    private void EndSelectionRectangle()
    {
        if (_selectionRectangle != null)
        {
            ScoreCanvas.Children.Remove(_selectionRectangle);
            _selectionRectangle = null;
        }

        _preSelectionState.Clear();
    }

    #endregion

    #region Keyboard Event Handlers

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        switch (e.Key)
        {
            case Key.Delete:
                _viewModel.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.A when ctrlPressed:
                _viewModel.SelectAllCommand.Execute(null);
                RenderScore();
                e.Handled = true;
                break;

            case Key.C when ctrlPressed:
                _viewModel.CopyCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.V when ctrlPressed:
                _viewModel.PasteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.X when ctrlPressed:
                _viewModel.CutCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.D when ctrlPressed:
                _viewModel.DuplicateSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.V:
                SetTool(ScoreEditorTool.Select);
                e.Handled = true;
                break;

            case Key.D:
                SetTool(ScoreEditorTool.Draw);
                e.Handled = true;
                break;

            case Key.E:
                SetTool(ScoreEditorTool.Erase);
                e.Handled = true;
                break;

            case Key.R:
                _viewModel.ToggleRestModeCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemPeriod:
                _viewModel.ToggleDottedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up when ctrlPressed:
                _viewModel.TransposeCommand.Execute(12);
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.TransposeCommand.Execute(1);
                e.Handled = true;
                break;

            case Key.Down when ctrlPressed:
                _viewModel.TransposeCommand.Execute(-12);
                e.Handled = true;
                break;

            case Key.Down:
                _viewModel.TransposeCommand.Execute(-1);
                e.Handled = true;
                break;

            case Key.Escape:
                _viewModel.DeselectAllCommand.Execute(null);
                RenderScore();
                e.Handled = true;
                break;

            case Key.Space:
                _viewModel.TogglePlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            // Duration shortcuts
            case Key.D1 or Key.NumPad1:
                _viewModel.SetDurationCommand.Execute(NoteDuration.Whole);
                WholeNoteButton.IsChecked = true;
                e.Handled = true;
                break;

            case Key.D2 or Key.NumPad2:
                _viewModel.SetDurationCommand.Execute(NoteDuration.Half);
                HalfNoteButton.IsChecked = true;
                e.Handled = true;
                break;

            case Key.D3 or Key.NumPad3:
                _viewModel.SetDurationCommand.Execute(NoteDuration.Quarter);
                QuarterNoteButton.IsChecked = true;
                e.Handled = true;
                break;

            case Key.D4 or Key.NumPad4:
                _viewModel.SetDurationCommand.Execute(NoteDuration.Eighth);
                EighthNoteButton.IsChecked = true;
                e.Handled = true;
                break;

            case Key.D5 or Key.NumPad5:
                _viewModel.SetDurationCommand.Execute(NoteDuration.Sixteenth);
                SixteenthNoteButton.IsChecked = true;
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region UI Event Handlers

    private void OnSelectToolClick(object sender, RoutedEventArgs e)
    {
        SetTool(ScoreEditorTool.Select);
    }

    private void OnDrawToolClick(object sender, RoutedEventArgs e)
    {
        SetTool(ScoreEditorTool.Draw);
    }

    private void OnEraseToolClick(object sender, RoutedEventArgs e)
    {
        SetTool(ScoreEditorTool.Erase);
    }

    private void SetTool(ScoreEditorTool tool)
    {
        _viewModel.SetToolCommand.Execute(tool);
        UpdateToolSelection();
    }

    private void UpdateToolSelection()
    {
        SelectToolButton.IsChecked = _viewModel.CurrentTool == ScoreEditorTool.Select;
        DrawToolButton.IsChecked = _viewModel.CurrentTool == ScoreEditorTool.Draw;
        EraseToolButton.IsChecked = _viewModel.CurrentTool == ScoreEditorTool.Erase;
    }

    private void OnDurationChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radioButton && radioButton.Tag is NoteDuration duration)
        {
            _viewModel.SetDurationCommand.Execute(duration);
        }
    }

    private void OnClefChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClefComboBox.SelectedItem is ComboBoxItem item && item.Tag is ClefType clef)
        {
            _viewModel.Clef = clef;
        }
    }

    private void OnKeySignatureChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KeySignatureComboBox.SelectedItem is ComboBoxItem item)
        {
            _viewModel.KeySignature = item.Content?.ToString() ?? "C";
        }
    }

    private void OnTimeSignatureChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeSignatureComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var parts = tag.Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int numerator) &&
                int.TryParse(parts[1], out int denominator))
            {
                _viewModel.TimeSignatureNumerator = numerator;
                _viewModel.TimeSignatureDenominator = denominator;
            }
        }
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _viewModel.ScrollX = e.HorizontalOffset;
        _viewModel.ScrollY = e.VerticalOffset;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the score display.
    /// </summary>
    public void Refresh()
    {
        RenderScore();
    }

    /// <summary>
    /// Scrolls to show the specified measure.
    /// </summary>
    /// <param name="measure">The measure number to scroll to.</param>
    public void ScrollToMeasure(int measure)
    {
        double x = StaffMarginLeft + (measure - 1) * EffectiveMeasureWidth;
        ScoreScrollViewer.ScrollToHorizontalOffset(Math.Max(0, x - 100));
    }

    /// <summary>
    /// Clears all notes from the score.
    /// </summary>
    public void ClearScore()
    {
        _viewModel.ClearScoreCommand.Execute(null);
    }

    /// <summary>
    /// Loads notes from a collection of PianoRollNotes.
    /// </summary>
    /// <param name="midiNotes">The MIDI notes to load.</param>
    public void LoadFromMidi(IEnumerable<PianoRollNote> midiNotes)
    {
        _viewModel.ConvertFromMidiCommand.Execute(midiNotes);
    }

    /// <summary>
    /// Gets all notes as PianoRollNotes for MIDI export.
    /// </summary>
    /// <returns>Collection of PianoRollNotes.</returns>
    public IEnumerable<PianoRollNote> ExportToMidi()
    {
        return _viewModel.GetMidiNotes();
    }

    #endregion
}
