// MusicEngineEditor - Automation Thinning Command
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Linq;
using MusicEngine.Core.Automation;
using MusicEngine.Core.UndoRedo;

namespace MusicEngineEditor.Commands;

/// <summary>
/// Command to reduce automation point density while preserving the overall shape.
/// Uses the Ramer-Douglas-Peucker algorithm for line simplification.
/// </summary>
public sealed class AutomationThinCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly List<PointSnapshot> _originalPoints;
    private readonly float _threshold;
    private List<PointSnapshot>? _thinnedPoints;

    /// <inheritdoc/>
    public string Description => $"Thin Automation ({_originalPoints.Count} -> {_thinnedPoints?.Count ?? 0} points)";

    /// <summary>
    /// Creates a new AutomationThinCommand.
    /// </summary>
    /// <param name="lane">The automation lane to thin.</param>
    /// <param name="threshold">The perpendicular distance threshold for point reduction.
    /// Higher values remove more points. Typical range: 0.001 to 0.1.</param>
    public AutomationThinCommand(AutomationLane lane, float threshold = 0.01f)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        _threshold = Math.Max(0.0001f, threshold);

        // Capture original points
        _originalPoints = _lane.Curve.Points
            .Select(p => new PointSnapshot(p.Time, p.Value, p.CurveType, p.Tension,
                p.BezierX1, p.BezierY1, p.BezierX2, p.BezierY2, p.IsLocked, p.Label))
            .ToList();
    }

    /// <inheritdoc/>
    public void Execute()
    {
        if (_originalPoints.Count <= 2)
        {
            // Cannot thin fewer than 3 points
            _thinnedPoints = new List<PointSnapshot>(_originalPoints);
            return;
        }

        // Compute thinned points if not already done
        _thinnedPoints ??= ThinPoints(_originalPoints, _threshold);

        // Clear current curve and add thinned points
        _lane.ClearPoints();
        foreach (var snapshot in _thinnedPoints)
        {
            var point = _lane.Curve.AddPoint(snapshot.Time, snapshot.Value, snapshot.CurveType);
            point.Tension = snapshot.Tension;
            point.BezierX1 = snapshot.BezierX1;
            point.BezierY1 = snapshot.BezierY1;
            point.BezierX2 = snapshot.BezierX2;
            point.BezierY2 = snapshot.BezierY2;
            point.IsLocked = snapshot.IsLocked;
            point.Label = snapshot.Label;
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        // Restore original points
        _lane.ClearPoints();
        foreach (var snapshot in _originalPoints)
        {
            var point = _lane.Curve.AddPoint(snapshot.Time, snapshot.Value, snapshot.CurveType);
            point.Tension = snapshot.Tension;
            point.BezierX1 = snapshot.BezierX1;
            point.BezierY1 = snapshot.BezierY1;
            point.BezierX2 = snapshot.BezierX2;
            point.BezierY2 = snapshot.BezierY2;
            point.IsLocked = snapshot.IsLocked;
            point.Label = snapshot.Label;
        }
    }

    /// <summary>
    /// Reduces points using the Ramer-Douglas-Peucker algorithm.
    /// This algorithm recursively simplifies a curve by removing points that are
    /// within the threshold distance from the line between neighboring kept points.
    /// </summary>
    /// <param name="points">The input points sorted by time.</param>
    /// <param name="threshold">The perpendicular distance threshold.</param>
    /// <returns>A list of points representing the simplified curve.</returns>
    private static List<PointSnapshot> ThinPoints(List<PointSnapshot> points, float threshold)
    {
        if (points.Count <= 2)
            return new List<PointSnapshot>(points);

        // Normalize the coordinates to [0,1] range for consistent threshold behavior
        double minTime = points[0].Time;
        double maxTime = points[^1].Time;
        float minValue = points.Min(p => p.Value);
        float maxValue = points.Max(p => p.Value);

        double timeRange = maxTime - minTime;
        float valueRange = maxValue - minValue;

        // Avoid division by zero
        if (timeRange < double.Epsilon) timeRange = 1.0;
        if (valueRange < float.Epsilon) valueRange = 1f;

        // Convert to normalized 2D points for the algorithm
        var normalized = points.Select(p => new Point2D(
            (p.Time - minTime) / timeRange,
            (p.Value - minValue) / valueRange
        )).ToList();

        // Run RDP algorithm on indices
        var keepIndices = new List<int>();
        RamerDouglasPeucker(normalized, 0, normalized.Count - 1, threshold, keepIndices);

        // Always include first and last points
        var resultIndices = new HashSet<int>(keepIndices) { 0, points.Count - 1 };

        // Return the kept points in original order
        return resultIndices.OrderBy(i => i).Select(i => points[i]).ToList();
    }

    /// <summary>
    /// Recursive implementation of the Ramer-Douglas-Peucker algorithm.
    /// </summary>
    private static void RamerDouglasPeucker(List<Point2D> points, int startIdx, int endIdx, double epsilon, List<int> keepIndices)
    {
        if (endIdx <= startIdx + 1)
            return;

        double maxDistance = 0;
        int maxIndex = startIdx;

        var startPoint = points[startIdx];
        var endPoint = points[endIdx];

        // Find the point with maximum perpendicular distance to the line
        for (int i = startIdx + 1; i < endIdx; i++)
        {
            double distance = PerpendicularDistance(points[i], startPoint, endPoint);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        // If the max distance is greater than epsilon, recursively simplify
        if (maxDistance > epsilon)
        {
            keepIndices.Add(maxIndex);
            RamerDouglasPeucker(points, startIdx, maxIndex, epsilon, keepIndices);
            RamerDouglasPeucker(points, maxIndex, endIdx, epsilon, keepIndices);
        }
    }

    /// <summary>
    /// Calculates the perpendicular distance from a point to a line segment.
    /// </summary>
    private static double PerpendicularDistance(Point2D point, Point2D lineStart, Point2D lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;

        // Length of the line segment
        double lineLengthSquared = dx * dx + dy * dy;

        if (lineLengthSquared < double.Epsilon)
        {
            // Line segment is a point
            double pdx = point.X - lineStart.X;
            double pdy = point.Y - lineStart.Y;
            return Math.Sqrt(pdx * pdx + pdy * pdy);
        }

        // Calculate perpendicular distance using the cross product formula
        double numerator = Math.Abs(dy * point.X - dx * point.Y + lineEnd.X * lineStart.Y - lineEnd.Y * lineStart.X);
        double denominator = Math.Sqrt(lineLengthSquared);

        return numerator / denominator;
    }

    /// <summary>
    /// Internal 2D point structure for the RDP algorithm.
    /// </summary>
    private readonly struct Point2D
    {
        public double X { get; }
        public double Y { get; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Snapshot of an automation point for undo/redo.
    /// </summary>
    private readonly record struct PointSnapshot(
        double Time,
        float Value,
        AutomationCurveType CurveType,
        float Tension,
        float BezierX1,
        float BezierY1,
        float BezierX2,
        float BezierY2,
        bool IsLocked,
        string? Label);
}
