// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Data model class.

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Defines the curve type for tempo transitions.
/// </summary>
public enum TempoCurveType
{
    /// <summary>
    /// Instant step change.
    /// </summary>
    Step,

    /// <summary>
    /// Linear interpolation.
    /// </summary>
    Linear,

    /// <summary>
    /// Smooth S-curve interpolation.
    /// </summary>
    SCurve,

    /// <summary>
    /// Exponential ease-in.
    /// </summary>
    ExponentialIn,

    /// <summary>
    /// Exponential ease-out.
    /// </summary>
    ExponentialOut
}

/// <summary>
/// Represents a tempo point in the tempo curve editor.
/// </summary>
public partial class TempoPoint : ObservableObject
{
    /// <summary>
    /// Unique identifier for the tempo point.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Position in beats.
    /// </summary>
    [ObservableProperty]
    private double _position;

    /// <summary>
    /// Tempo value in BPM.
    /// </summary>
    [ObservableProperty]
    private double _tempo = 120.0;

    /// <summary>
    /// Curve type for transition to next point.
    /// </summary>
    [ObservableProperty]
    private TempoCurveType _curveType = TempoCurveType.Linear;

    /// <summary>
    /// Indicates whether this point is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Indicates whether this point is locked.
    /// </summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>
    /// Bezier handle X offset for curve control (used for S-Curve type).
    /// </summary>
    [ObservableProperty]
    private double _handleX;

    /// <summary>
    /// Bezier handle Y offset for curve control (used for S-Curve type).
    /// </summary>
    [ObservableProperty]
    private double _handleY;

    /// <summary>
    /// Creates a new tempo point with default values.
    /// </summary>
    public TempoPoint()
    {
    }

    /// <summary>
    /// Creates a new tempo point with specified values.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <param name="tempo">Tempo in BPM.</param>
    /// <param name="curveType">Curve type for transition.</param>
    public TempoPoint(double position, double tempo, TempoCurveType curveType = TempoCurveType.Linear)
    {
        Position = position;
        Tempo = Math.Clamp(tempo, MinTempo, MaxTempo);
        CurveType = curveType;
    }

    /// <summary>
    /// Minimum allowed tempo (BPM).
    /// </summary>
    public const double MinTempo = 20.0;

    /// <summary>
    /// Maximum allowed tempo (BPM).
    /// </summary>
    public const double MaxTempo = 300.0;

    /// <summary>
    /// Common tempo snap values.
    /// </summary>
    public static readonly double[] CommonTempos =
    {
        60, 70, 80, 90, 100, 110, 120, 125, 128, 130,
        135, 140, 145, 150, 160, 170, 175, 180, 200
    };

    /// <summary>
    /// Creates a deep copy of this tempo point.
    /// </summary>
    /// <returns>A new TempoPoint with the same values but a new Id.</returns>
    public TempoPoint Clone()
    {
        return new TempoPoint
        {
            Id = Guid.NewGuid(),
            Position = Position,
            Tempo = Tempo,
            CurveType = CurveType,
            IsSelected = false,
            IsLocked = IsLocked,
            HandleX = HandleX,
            HandleY = HandleY
        };
    }

    /// <summary>
    /// Snaps the tempo to the nearest common value.
    /// </summary>
    /// <param name="tolerance">Snap tolerance in BPM.</param>
    /// <returns>The snapped tempo value.</returns>
    public double GetSnappedTempo(double tolerance = 2.0)
    {
        foreach (var common in CommonTempos)
        {
            if (Math.Abs(Tempo - common) <= tolerance)
            {
                return common;
            }
        }
        return Tempo;
    }

    /// <summary>
    /// Calculates the interpolated tempo between this point and another.
    /// </summary>
    /// <param name="next">The next tempo point.</param>
    /// <param name="position">Position to interpolate at.</param>
    /// <returns>Interpolated tempo value.</returns>
    public double InterpolateTo(TempoPoint? next, double position)
    {
        if (next == null || position <= Position)
            return Tempo;

        if (position >= next.Position)
            return next.Tempo;

        double t = (position - Position) / (next.Position - Position);

        return CurveType switch
        {
            TempoCurveType.Step => Tempo,
            TempoCurveType.Linear => Lerp(Tempo, next.Tempo, t),
            TempoCurveType.SCurve => Lerp(Tempo, next.Tempo, SmoothStep(t)),
            TempoCurveType.ExponentialIn => Lerp(Tempo, next.Tempo, t * t),
            TempoCurveType.ExponentialOut => Lerp(Tempo, next.Tempo, 1 - (1 - t) * (1 - t)),
            _ => Lerp(Tempo, next.Tempo, t)
        };
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double SmoothStep(double t) => t * t * (3 - 2 * t);

    partial void OnTempoChanged(double value)
    {
        // Clamp tempo to valid range
        if (value < MinTempo)
            Tempo = MinTempo;
        else if (value > MaxTempo)
            Tempo = MaxTempo;
    }
}
