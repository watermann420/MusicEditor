// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Renders standard music notation elements using WPF DrawingContext.
/// </summary>
public class ScoreRenderer
{
    #region Constants

    private const int StaffLineCount = 5;
    private const int LedgerLineExtension = 6;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the spacing between staff lines in pixels.
    /// </summary>
    public double StaffLineSpacing { get; set; } = 10;

    /// <summary>
    /// Gets or sets the width of a note head.
    /// </summary>
    public double NoteHeadWidth { get; set; } = 12;

    /// <summary>
    /// Gets or sets the height of a note head.
    /// </summary>
    public double NoteHeadHeight { get; set; } = 8;

    /// <summary>
    /// Gets or sets the width of a single measure.
    /// </summary>
    public double MeasureWidth { get; set; } = 200;

    /// <summary>
    /// Gets or sets the stem height multiplier.
    /// </summary>
    public double StemHeightMultiplier { get; set; } = 3.5;

    /// <summary>
    /// Gets or sets the stem thickness.
    /// </summary>
    public double StemThickness { get; set; } = 1.5;

    /// <summary>
    /// Gets or sets the flag width for eighth notes and smaller.
    /// </summary>
    public double FlagWidth { get; set; } = 8;

    /// <summary>
    /// Gets or sets the flag height.
    /// </summary>
    public double FlagHeight { get; set; } = 12;

    /// <summary>
    /// Gets or sets the note color.
    /// </summary>
    public Color NoteColor { get; set; } = Colors.White;

    /// <summary>
    /// Gets or sets the staff line color.
    /// </summary>
    public Color StaffLineColor { get; set; } = Color.FromRgb(0x6F, 0x73, 0x7A);

    /// <summary>
    /// Gets or sets the bar line color.
    /// </summary>
    public Color BarLineColor { get; set; } = Color.FromRgb(0xBC, 0xBE, 0xC4);

    /// <summary>
    /// Gets or sets the selected note color.
    /// </summary>
    public Color SelectedNoteColor { get; set; } = Color.FromRgb(0x4B, 0x6E, 0xAF);

    /// <summary>
    /// Gets or sets the ledger line color.
    /// </summary>
    public Color LedgerLineColor { get; set; } = Color.FromRgb(0x6F, 0x73, 0x7A);

    /// <summary>
    /// Gets or sets the accidental color.
    /// </summary>
    public Color AccidentalColor { get; set; } = Colors.White;

    #endregion

    #region Cached Resources

    private Pen? _staffLinePen;
    private Pen? _barLinePen;
    private Pen? _stemPen;
    private Pen? _ledgerLinePen;
    private SolidColorBrush? _noteBrush;
    private SolidColorBrush? _selectedNoteBrush;
    private SolidColorBrush? _hollowNoteBrush;
    private SolidColorBrush? _accidentalBrush;
    private Typeface? _musicTypeface;
    private Typeface? _textTypeface;

    private Pen StaffLinePen => _staffLinePen ??= new Pen(new SolidColorBrush(StaffLineColor), 1) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
    private Pen BarLinePen => _barLinePen ??= new Pen(new SolidColorBrush(BarLineColor), 1.5) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
    private Pen StemPen => _stemPen ??= new Pen(new SolidColorBrush(NoteColor), StemThickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
    private Pen LedgerLinePen => _ledgerLinePen ??= new Pen(new SolidColorBrush(LedgerLineColor), 1) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
    private SolidColorBrush NoteBrush => _noteBrush ??= new SolidColorBrush(NoteColor);
    private SolidColorBrush SelectedNoteBrush => _selectedNoteBrush ??= new SolidColorBrush(SelectedNoteColor);
    private SolidColorBrush HollowNoteBrush => _hollowNoteBrush ??= new SolidColorBrush(Colors.Transparent);
    private SolidColorBrush AccidentalBrush => _accidentalBrush ??= new SolidColorBrush(AccidentalColor);
    private Typeface MusicTypeface => _musicTypeface ??= new Typeface(new FontFamily("Segoe UI Symbol"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private Typeface TextTypeface => _textTypeface ??= new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    #endregion

    #region Public Methods

    /// <summary>
    /// Renders a five-line staff within the given bounds.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="bounds">The bounding rectangle for the staff.</param>
    /// <param name="clef">The clef type to display.</param>
    public void RenderStaff(DrawingContext dc, Rect bounds, ClefType clef)
    {
        double staffHeight = (StaffLineCount - 1) * StaffLineSpacing;
        double startY = bounds.Y + (bounds.Height - staffHeight) / 2;

        // Draw the five staff lines
        for (int i = 0; i < StaffLineCount; i++)
        {
            double y = startY + i * StaffLineSpacing;
            dc.DrawLine(StaffLinePen, new Point(bounds.X, y), new Point(bounds.X + bounds.Width, y));
        }

        // Draw the clef
        RenderClef(dc, new Point(bounds.X + 10, startY), clef);
    }

    /// <summary>
    /// Renders a clef symbol at the specified position.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The position for the clef.</param>
    /// <param name="clef">The clef type.</param>
    public void RenderClef(DrawingContext dc, Point position, ClefType clef)
    {
        // Use Unicode music symbols or draw geometrically
        string clefSymbol = clef switch
        {
            ClefType.Treble => "\U0001D11E", // Musical Symbol G Clef
            ClefType.Bass => "\U0001D122",   // Musical Symbol F Clef
            ClefType.Alto => "\U0001D121",   // Musical Symbol C Clef
            ClefType.Tenor => "\U0001D121",  // Musical Symbol C Clef
            _ => "\U0001D11E"
        };

        // Fallback to geometric drawing for better compatibility
        DrawClefGeometric(dc, position, clef);
    }

    /// <summary>
    /// Renders a note at the specified position.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The center position for the note head.</param>
    /// <param name="duration">The note duration.</param>
    /// <param name="isRest">Whether to render a rest instead of a note.</param>
    /// <param name="isSelected">Whether the note is selected.</param>
    /// <param name="stemUp">Whether the stem points upward.</param>
    public void RenderNote(DrawingContext dc, Point position, NoteDuration duration, bool isRest, bool isSelected = false, bool stemUp = true)
    {
        if (isRest)
        {
            RenderRest(dc, position, duration);
            return;
        }

        var brush = isSelected ? SelectedNoteBrush : NoteBrush;
        var pen = new Pen(brush, 1.5);

        // Draw note head based on duration
        bool isFilled = duration != NoteDuration.Whole && duration != NoteDuration.Half;
        bool needsStem = duration != NoteDuration.Whole;
        int flagCount = GetFlagCount(duration);

        // Draw the note head (ellipse)
        double noteHeadWidthActual = NoteHeadWidth;
        double noteHeadHeightActual = NoteHeadHeight;

        if (duration == NoteDuration.Whole)
        {
            // Whole notes are wider
            noteHeadWidthActual *= 1.3;
        }

        var noteHeadRect = new Rect(
            position.X - noteHeadWidthActual / 2,
            position.Y - noteHeadHeightActual / 2,
            noteHeadWidthActual,
            noteHeadHeightActual
        );

        // Rotate the note head slightly for aesthetic
        var rotatedGeometry = new EllipseGeometry(noteHeadRect);
        var transform = new RotateTransform(-15, position.X, position.Y);

        dc.PushTransform(transform);
        if (isFilled)
        {
            dc.DrawGeometry(brush, null, rotatedGeometry);
        }
        else
        {
            dc.DrawGeometry(HollowNoteBrush, pen, rotatedGeometry);
        }
        dc.Pop();

        // Draw stem
        if (needsStem)
        {
            double stemHeight = StaffLineSpacing * StemHeightMultiplier;
            double stemX = stemUp ? position.X + noteHeadWidthActual / 2 - 1 : position.X - noteHeadWidthActual / 2 + 1;
            double stemStartY = position.Y;
            double stemEndY = stemUp ? position.Y - stemHeight : position.Y + stemHeight;

            var stemPen = new Pen(brush, StemThickness);
            dc.DrawLine(stemPen, new Point(stemX, stemStartY), new Point(stemX, stemEndY));

            // Draw flags for eighth notes and smaller
            if (flagCount > 0)
            {
                RenderFlags(dc, new Point(stemX, stemEndY), flagCount, stemUp, brush);
            }
        }
    }

    /// <summary>
    /// Renders a rest symbol at the specified position.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The center position for the rest.</param>
    /// <param name="duration">The rest duration.</param>
    public void RenderRest(DrawingContext dc, Point position, NoteDuration duration)
    {
        var brush = NoteBrush;
        var pen = new Pen(brush, 1.5);

        switch (duration)
        {
            case NoteDuration.Whole:
                // Whole rest: rectangle below line
                var wholeRestRect = new Rect(position.X - 6, position.Y - 4, 12, 4);
                dc.DrawRectangle(brush, null, wholeRestRect);
                break;

            case NoteDuration.Half:
                // Half rest: rectangle above line
                var halfRestRect = new Rect(position.X - 6, position.Y, 12, 4);
                dc.DrawRectangle(brush, null, halfRestRect);
                break;

            case NoteDuration.Quarter:
                // Quarter rest: zigzag shape (simplified)
                DrawQuarterRest(dc, position, brush);
                break;

            case NoteDuration.Eighth:
                // Eighth rest: flag with stem
                DrawEighthRest(dc, position, brush, 1);
                break;

            case NoteDuration.Sixteenth:
                // Sixteenth rest: two flags
                DrawEighthRest(dc, position, brush, 2);
                break;

            case NoteDuration.ThirtySecond:
                // Thirty-second rest: three flags
                DrawEighthRest(dc, position, brush, 3);
                break;
        }
    }

    /// <summary>
    /// Renders an accidental symbol at the specified position.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The position for the accidental.</param>
    /// <param name="accidental">The accidental type.</param>
    public void RenderAccidental(DrawingContext dc, Point position, AccidentalType accidental)
    {
        if (accidental == AccidentalType.None)
            return;

        var brush = AccidentalBrush;
        var pen = new Pen(brush, 1.2);

        switch (accidental)
        {
            case AccidentalType.Sharp:
                DrawSharp(dc, position, brush, pen);
                break;

            case AccidentalType.Flat:
                DrawFlat(dc, position, brush, pen);
                break;

            case AccidentalType.Natural:
                DrawNatural(dc, position, brush, pen);
                break;

            case AccidentalType.DoubleSharp:
                DrawDoubleSharp(dc, position, brush);
                break;

            case AccidentalType.DoubleFlat:
                DrawDoubleFlat(dc, position, brush, pen);
                break;
        }
    }

    /// <summary>
    /// Renders a time signature at the specified position.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The position for the time signature.</param>
    /// <param name="numerator">The top number (beats per measure).</param>
    /// <param name="denominator">The bottom number (beat unit).</param>
    public void RenderTimeSignature(DrawingContext dc, Point position, int numerator, int denominator)
    {
        double fontSize = StaffLineSpacing * 2.5;
        var brush = NoteBrush;

        // Draw numerator (centered above middle)
        var numeratorText = new FormattedText(
            numerator.ToString(),
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            TextTypeface,
            fontSize,
            brush,
            1.0);

        dc.DrawText(numeratorText, new Point(
            position.X - numeratorText.Width / 2,
            position.Y - StaffLineSpacing * 2 - numeratorText.Height / 2));

        // Draw denominator (centered below middle)
        var denominatorText = new FormattedText(
            denominator.ToString(),
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            TextTypeface,
            fontSize,
            brush,
            1.0);

        dc.DrawText(denominatorText, new Point(
            position.X - denominatorText.Width / 2,
            position.Y + StaffLineSpacing - denominatorText.Height / 2));
    }

    /// <summary>
    /// Renders a key signature at the specified position.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The position for the key signature.</param>
    /// <param name="key">The key signature string (e.g., "G", "F", "Bb").</param>
    /// <param name="clef">The clef type for proper accidental placement.</param>
    public void RenderKeySignature(DrawingContext dc, Point position, string key, ClefType clef)
    {
        var sharps = GetKeySignatureSharps(key);
        var flats = GetKeySignatureFlats(key);

        double x = position.X;
        double spacing = 10;

        if (sharps.Length > 0)
        {
            // Draw sharps
            int[] sharpPositions = clef switch
            {
                ClefType.Treble => [0, 3, -1, 2, 5, 1, 4], // F#, C#, G#, D#, A#, E#, B#
                ClefType.Bass => [2, 5, 1, 4, 7, 3, 6],
                _ => [0, 3, -1, 2, 5, 1, 4]
            };

            for (int i = 0; i < sharps.Length && i < sharpPositions.Length; i++)
            {
                double y = position.Y - sharpPositions[i] * (StaffLineSpacing / 2);
                RenderAccidental(dc, new Point(x, y), AccidentalType.Sharp);
                x += spacing;
            }
        }
        else if (flats.Length > 0)
        {
            // Draw flats
            int[] flatPositions = clef switch
            {
                ClefType.Treble => [4, 1, 5, 2, 6, 3, 7], // Bb, Eb, Ab, Db, Gb, Cb, Fb
                ClefType.Bass => [6, 3, 7, 4, 8, 5, 9],
                _ => [4, 1, 5, 2, 6, 3, 7]
            };

            for (int i = 0; i < flats.Length && i < flatPositions.Length; i++)
            {
                double y = position.Y - flatPositions[i] * (StaffLineSpacing / 2);
                RenderAccidental(dc, new Point(x, y), AccidentalType.Flat);
                x += spacing;
            }
        }
    }

    /// <summary>
    /// Renders a bar line from start to end points.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="start">The start point of the bar line.</param>
    /// <param name="end">The end point of the bar line.</param>
    /// <param name="isDouble">Whether to render a double bar line.</param>
    public void RenderBarLine(DrawingContext dc, Point start, Point end, bool isDouble = false)
    {
        dc.DrawLine(BarLinePen, start, end);

        if (isDouble)
        {
            var offset = new Vector(4, 0);
            dc.DrawLine(BarLinePen, start + offset, end + offset);
        }
    }

    /// <summary>
    /// Renders ledger lines for notes above or below the staff.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The center position of the note.</param>
    /// <param name="staffTopY">The Y position of the top staff line.</param>
    /// <param name="staffBottomY">The Y position of the bottom staff line.</param>
    public void RenderLedgerLines(DrawingContext dc, Point position, double staffTopY, double staffBottomY)
    {
        double halfSpacing = StaffLineSpacing / 2;
        double lineWidth = NoteHeadWidth + LedgerLineExtension * 2;

        // Check if ledger lines are needed above the staff
        if (position.Y < staffTopY - halfSpacing)
        {
            double y = staffTopY - StaffLineSpacing;
            while (y >= position.Y - halfSpacing)
            {
                dc.DrawLine(LedgerLinePen,
                    new Point(position.X - lineWidth / 2, y),
                    new Point(position.X + lineWidth / 2, y));
                y -= StaffLineSpacing;
            }
        }

        // Check if ledger lines are needed below the staff
        if (position.Y > staffBottomY + halfSpacing)
        {
            double y = staffBottomY + StaffLineSpacing;
            while (y <= position.Y + halfSpacing)
            {
                dc.DrawLine(LedgerLinePen,
                    new Point(position.X - lineWidth / 2, y),
                    new Point(position.X + lineWidth / 2, y));
                y += StaffLineSpacing;
            }
        }
    }

    /// <summary>
    /// Renders a dot for dotted notes.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The position after the note head.</param>
    /// <param name="isSelected">Whether the note is selected.</param>
    public void RenderDot(DrawingContext dc, Point position, bool isSelected = false)
    {
        var brush = isSelected ? SelectedNoteBrush : NoteBrush;
        double dotRadius = 2;
        dc.DrawEllipse(brush, null, position, dotRadius, dotRadius);
    }

    /// <summary>
    /// Renders an articulation marking.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="position">The position for the articulation.</param>
    /// <param name="articulation">The articulation type.</param>
    /// <param name="aboveNote">Whether to place above the note.</param>
    public void RenderArticulation(DrawingContext dc, Point position, ArticulationType articulation, bool aboveNote = true)
    {
        if (articulation == ArticulationType.None)
            return;

        var brush = NoteBrush;
        double offset = aboveNote ? -StaffLineSpacing : StaffLineSpacing;
        var articulationPosition = new Point(position.X, position.Y + offset);

        switch (articulation)
        {
            case ArticulationType.Staccato:
                dc.DrawEllipse(brush, null, articulationPosition, 2, 2);
                break;

            case ArticulationType.Staccatissimo:
                DrawTriangle(dc, articulationPosition, 4, aboveNote, brush);
                break;

            case ArticulationType.Tenuto:
                dc.DrawLine(new Pen(brush, 2),
                    new Point(articulationPosition.X - 4, articulationPosition.Y),
                    new Point(articulationPosition.X + 4, articulationPosition.Y));
                break;

            case ArticulationType.Accent:
                DrawAccent(dc, articulationPosition, aboveNote, brush);
                break;

            case ArticulationType.Marcato:
                DrawMarcato(dc, articulationPosition, aboveNote, brush);
                break;

            case ArticulationType.Fermata:
                DrawFermata(dc, articulationPosition, aboveNote, brush);
                break;
        }
    }

    /// <summary>
    /// Renders a tie arc between two note positions.
    /// </summary>
    /// <param name="dc">The DrawingContext to render to.</param>
    /// <param name="startPosition">The position of the first note.</param>
    /// <param name="endPosition">The position of the second note.</param>
    /// <param name="above">Whether the tie curves above the notes.</param>
    public void RenderTie(DrawingContext dc, Point startPosition, Point endPosition, bool above = true)
    {
        var brush = NoteBrush;
        var pen = new Pen(brush, 1.5);

        double controlY = above
            ? Math.Min(startPosition.Y, endPosition.Y) - StaffLineSpacing * 1.5
            : Math.Max(startPosition.Y, endPosition.Y) + StaffLineSpacing * 1.5;

        var controlPoint1 = new Point(startPosition.X + (endPosition.X - startPosition.X) * 0.3, controlY);
        var controlPoint2 = new Point(startPosition.X + (endPosition.X - startPosition.X) * 0.7, controlY);

        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = startPosition,
            IsClosed = false
        };
        figure.Segments.Add(new BezierSegment(controlPoint1, controlPoint2, endPosition, true));
        geometry.Figures.Add(figure);

        dc.DrawGeometry(null, pen, geometry);
    }

    /// <summary>
    /// Gets the staff line position for a given MIDI note and clef.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <param name="clef">The clef type.</param>
    /// <returns>The offset from the middle line of the staff.</returns>
    public int GetStaffLineForPitch(int midiNote, ClefType clef)
    {
        return ScoreNote.GetStaffLineOffset(midiNote, clef);
    }

    /// <summary>
    /// Calculates the Y position for a note on the staff.
    /// </summary>
    /// <param name="staffLineOffset">The offset from the middle line.</param>
    /// <param name="staffCenterY">The Y position of the middle staff line.</param>
    /// <returns>The Y position for the note.</returns>
    public double GetYPositionForStaffLine(int staffLineOffset, double staffCenterY)
    {
        // Staff lines are numbered from bottom (0) to top (4)
        // Middle line is at index 2
        // Positive offset = higher pitch = lower Y value
        return staffCenterY - (staffLineOffset * StaffLineSpacing / 2);
    }

    #endregion

    #region Private Helper Methods

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

    private void RenderFlags(DrawingContext dc, Point stemEnd, int count, bool stemUp, Brush brush)
    {
        var pen = new Pen(brush, 1.5);

        for (int i = 0; i < count; i++)
        {
            double yOffset = i * 6 * (stemUp ? 1 : -1);
            double startY = stemEnd.Y + yOffset;

            var flagGeometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(stemEnd.X, startY) };

            if (stemUp)
            {
                figure.Segments.Add(new BezierSegment(
                    new Point(stemEnd.X + FlagWidth * 0.8, startY + FlagHeight * 0.3),
                    new Point(stemEnd.X + FlagWidth, startY + FlagHeight * 0.7),
                    new Point(stemEnd.X + FlagWidth * 0.3, startY + FlagHeight),
                    true));
            }
            else
            {
                figure.Segments.Add(new BezierSegment(
                    new Point(stemEnd.X + FlagWidth * 0.8, startY - FlagHeight * 0.3),
                    new Point(stemEnd.X + FlagWidth, startY - FlagHeight * 0.7),
                    new Point(stemEnd.X + FlagWidth * 0.3, startY - FlagHeight),
                    true));
            }

            flagGeometry.Figures.Add(figure);
            dc.DrawGeometry(null, pen, flagGeometry);
        }
    }

    private void DrawClefGeometric(DrawingContext dc, Point position, ClefType clef)
    {
        var brush = NoteBrush;
        var pen = new Pen(brush, 2);

        switch (clef)
        {
            case ClefType.Treble:
                // Simplified treble clef drawing
                DrawTrebleClef(dc, position, brush, pen);
                break;

            case ClefType.Bass:
                // Simplified bass clef drawing
                DrawBassClef(dc, position, brush, pen);
                break;

            case ClefType.Alto:
            case ClefType.Tenor:
                // Simplified C clef drawing
                DrawCClef(dc, position, brush, pen);
                break;
        }
    }

    private void DrawTrebleClef(DrawingContext dc, Point position, Brush brush, Pen pen)
    {
        // Draw a simplified treble clef (G clef)
        double scale = StaffLineSpacing / 10.0;
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(position.X + 15 * scale, position.Y + 30 * scale),
            IsFilled = false
        };

        // Main spiral
        figure.Segments.Add(new BezierSegment(
            new Point(position.X + 10 * scale, position.Y + 35 * scale),
            new Point(position.X + 5 * scale, position.Y + 30 * scale),
            new Point(position.X + 5 * scale, position.Y + 20 * scale), true));
        figure.Segments.Add(new BezierSegment(
            new Point(position.X + 5 * scale, position.Y + 10 * scale),
            new Point(position.X + 15 * scale, position.Y + 5 * scale),
            new Point(position.X + 20 * scale, position.Y + 15 * scale), true));
        figure.Segments.Add(new BezierSegment(
            new Point(position.X + 25 * scale, position.Y + 25 * scale),
            new Point(position.X + 15 * scale, position.Y + 35 * scale),
            new Point(position.X + 10 * scale, position.Y + 25 * scale), true));

        geometry.Figures.Add(figure);
        dc.DrawGeometry(null, pen, geometry);

        // Add G line indicator
        dc.DrawEllipse(brush, null, new Point(position.X + 12 * scale, position.Y + 20 * scale), 2, 2);
    }

    private void DrawBassClef(DrawingContext dc, Point position, Brush brush, Pen pen)
    {
        // Draw a simplified bass clef (F clef)
        double scale = StaffLineSpacing / 10.0;

        // Main curve
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(position.X + 5 * scale, position.Y + 5 * scale),
            IsFilled = false
        };

        figure.Segments.Add(new BezierSegment(
            new Point(position.X + 20 * scale, position.Y + 5 * scale),
            new Point(position.X + 25 * scale, position.Y + 15 * scale),
            new Point(position.X + 15 * scale, position.Y + 25 * scale), true));
        figure.Segments.Add(new BezierSegment(
            new Point(position.X + 5 * scale, position.Y + 30 * scale),
            new Point(position.X, position.Y + 25 * scale),
            new Point(position.X + 5 * scale, position.Y + 20 * scale), true));

        geometry.Figures.Add(figure);
        dc.DrawGeometry(null, pen, geometry);

        // Two dots
        dc.DrawEllipse(brush, null, new Point(position.X + 22 * scale, position.Y + 8 * scale), 2, 2);
        dc.DrawEllipse(brush, null, new Point(position.X + 22 * scale, position.Y + 18 * scale), 2, 2);
    }

    private void DrawCClef(DrawingContext dc, Point position, Brush brush, Pen pen)
    {
        // Draw a simplified C clef (alto/tenor)
        double scale = StaffLineSpacing / 10.0;

        // Left vertical line
        dc.DrawLine(pen, new Point(position.X, position.Y), new Point(position.X, position.Y + 40 * scale));

        // Second vertical line
        dc.DrawLine(pen, new Point(position.X + 5 * scale, position.Y), new Point(position.X + 5 * scale, position.Y + 40 * scale));

        // Right brackets
        var thickness = 3 * scale;
        dc.DrawRectangle(brush, null, new Rect(position.X + 8 * scale, position.Y, thickness, 20 * scale));
        dc.DrawRectangle(brush, null, new Rect(position.X + 8 * scale, position.Y + 20 * scale, thickness, 20 * scale));
    }

    private void DrawQuarterRest(DrawingContext dc, Point position, Brush brush)
    {
        // Simplified quarter rest (zigzag)
        var pen = new Pen(brush, 2);
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(position.X, position.Y - 10),
            IsFilled = false
        };

        figure.Segments.Add(new LineSegment(new Point(position.X + 5, position.Y - 5), true));
        figure.Segments.Add(new LineSegment(new Point(position.X - 3, position.Y), true));
        figure.Segments.Add(new LineSegment(new Point(position.X + 5, position.Y + 5), true));
        figure.Segments.Add(new LineSegment(new Point(position.X, position.Y + 10), true));

        geometry.Figures.Add(figure);
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawEighthRest(DrawingContext dc, Point position, Brush brush, int flags)
    {
        var pen = new Pen(brush, 1.5);

        // Stem
        double stemHeight = 10 + (flags - 1) * 5;
        dc.DrawLine(pen, position, new Point(position.X + 3, position.Y + stemHeight));

        // Flag dots
        for (int i = 0; i < flags; i++)
        {
            double y = position.Y + i * 5;
            dc.DrawEllipse(brush, null, new Point(position.X - 2, y), 2, 2);
        }
    }

    private void DrawSharp(DrawingContext dc, Point position, Brush brush, Pen pen)
    {
        // Draw sharp symbol (#)
        double size = StaffLineSpacing * 0.8;

        // Vertical lines
        dc.DrawLine(pen, new Point(position.X - 2, position.Y - size), new Point(position.X - 2, position.Y + size));
        dc.DrawLine(pen, new Point(position.X + 2, position.Y - size), new Point(position.X + 2, position.Y + size));

        // Horizontal lines (slightly angled)
        var thickPen = new Pen(brush, 2);
        dc.DrawLine(thickPen, new Point(position.X - 5, position.Y - 3), new Point(position.X + 5, position.Y - 5));
        dc.DrawLine(thickPen, new Point(position.X - 5, position.Y + 5), new Point(position.X + 5, position.Y + 3));
    }

    private void DrawFlat(DrawingContext dc, Point position, Brush brush, Pen pen)
    {
        // Draw flat symbol (b)
        double size = StaffLineSpacing * 0.8;

        // Vertical stem
        dc.DrawLine(pen, new Point(position.X - 2, position.Y - size * 1.5), new Point(position.X - 2, position.Y + size * 0.5));

        // Curved bottom
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(position.X - 2, position.Y - size * 0.3),
            IsFilled = false
        };

        figure.Segments.Add(new BezierSegment(
            new Point(position.X + 5, position.Y - size * 0.2),
            new Point(position.X + 5, position.Y + size * 0.5),
            new Point(position.X - 2, position.Y + size * 0.5), true));

        geometry.Figures.Add(figure);
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawNatural(DrawingContext dc, Point position, Brush brush, Pen pen)
    {
        // Draw natural symbol
        double size = StaffLineSpacing * 0.8;

        // Left vertical (short, bottom)
        dc.DrawLine(pen, new Point(position.X - 3, position.Y), new Point(position.X - 3, position.Y + size));

        // Right vertical (short, top)
        dc.DrawLine(pen, new Point(position.X + 3, position.Y - size), new Point(position.X + 3, position.Y));

        // Horizontal connectors
        var thickPen = new Pen(brush, 2);
        dc.DrawLine(thickPen, new Point(position.X - 3, position.Y - 2), new Point(position.X + 3, position.Y - 4));
        dc.DrawLine(thickPen, new Point(position.X - 3, position.Y + 4), new Point(position.X + 3, position.Y + 2));
    }

    private void DrawDoubleSharp(DrawingContext dc, Point position, Brush brush)
    {
        // Draw double sharp (X)
        double size = 4;
        var pen = new Pen(brush, 2);

        dc.DrawLine(pen, new Point(position.X - size, position.Y - size), new Point(position.X + size, position.Y + size));
        dc.DrawLine(pen, new Point(position.X + size, position.Y - size), new Point(position.X - size, position.Y + size));
    }

    private void DrawDoubleFlat(DrawingContext dc, Point position, Brush brush, Pen pen)
    {
        // Draw two flats side by side
        DrawFlat(dc, new Point(position.X - 4, position.Y), brush, pen);
        DrawFlat(dc, new Point(position.X + 4, position.Y), brush, pen);
    }

    private void DrawTriangle(DrawingContext dc, Point position, double size, bool pointUp, Brush brush)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = pointUp ? new Point(position.X, position.Y - size) : new Point(position.X, position.Y + size),
            IsFilled = true
        };

        figure.Segments.Add(new LineSegment(new Point(position.X - size / 2, pointUp ? position.Y : position.Y), true));
        figure.Segments.Add(new LineSegment(new Point(position.X + size / 2, pointUp ? position.Y : position.Y), true));
        figure.IsClosed = true;

        geometry.Figures.Add(figure);
        dc.DrawGeometry(brush, null, geometry);
    }

    private void DrawAccent(DrawingContext dc, Point position, bool above, Brush brush)
    {
        var pen = new Pen(brush, 1.5);
        double size = 5;
        double direction = above ? 1 : -1;

        dc.DrawLine(pen, new Point(position.X - size, position.Y), new Point(position.X, position.Y - size * direction));
        dc.DrawLine(pen, new Point(position.X, position.Y - size * direction), new Point(position.X + size, position.Y));
    }

    private void DrawMarcato(DrawingContext dc, Point position, bool above, Brush brush)
    {
        // Marcato is like accent but filled
        DrawAccent(dc, position, above, brush);
        // Add vertical line
        var pen = new Pen(brush, 1.5);
        double size = 5;
        dc.DrawLine(pen, new Point(position.X, position.Y), new Point(position.X, position.Y - size * (above ? 1 : -1)));
    }

    private void DrawFermata(DrawingContext dc, Point position, bool above, Brush brush)
    {
        var pen = new Pen(brush, 1.5);
        double width = 10;
        double height = 6;
        double direction = above ? -1 : 1;

        // Arc
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(position.X - width / 2, position.Y),
            IsFilled = false
        };

        figure.Segments.Add(new ArcSegment(
            new Point(position.X + width / 2, position.Y),
            new Size(width / 2, height),
            0,
            false,
            above ? SweepDirection.Counterclockwise : SweepDirection.Clockwise,
            true));

        geometry.Figures.Add(figure);
        dc.DrawGeometry(null, pen, geometry);

        // Dot
        dc.DrawEllipse(brush, null, new Point(position.X, position.Y + height * direction / 2), 2, 2);
    }

    private int[] GetKeySignatureSharps(string key)
    {
        return key.ToUpperInvariant() switch
        {
            "G" or "EM" => [6],
            "D" or "BM" => [6, 1],
            "A" or "F#M" => [6, 1, 8],
            "E" or "C#M" => [6, 1, 8, 3],
            "B" or "G#M" => [6, 1, 8, 3, 10],
            "F#" or "D#M" => [6, 1, 8, 3, 10, 5],
            "C#" or "A#M" => [6, 1, 8, 3, 10, 5, 0],
            _ => []
        };
    }

    private int[] GetKeySignatureFlats(string key)
    {
        return key.ToUpperInvariant() switch
        {
            "F" or "DM" => [10],
            "BB" or "GM" => [10, 3],
            "EB" or "CM" => [10, 3, 8],
            "AB" or "FM" => [10, 3, 8, 1],
            "DB" or "BBM" => [10, 3, 8, 1, 6],
            "GB" or "EBM" => [10, 3, 8, 1, 6, 11],
            "CB" or "ABM" => [10, 3, 8, 1, 6, 11, 4],
            _ => []
        };
    }

    /// <summary>
    /// Invalidates cached resources so they are recreated on next use.
    /// Call this when colors or styling properties change.
    /// </summary>
    public void InvalidateResources()
    {
        _staffLinePen = null;
        _barLinePen = null;
        _stemPen = null;
        _ledgerLinePen = null;
        _noteBrush = null;
        _selectedNoteBrush = null;
        _hollowNoteBrush = null;
        _accidentalBrush = null;
    }

    #endregion
}
