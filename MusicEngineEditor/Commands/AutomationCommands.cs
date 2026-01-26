// MusicEngineEditor - Automation Commands
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Linq;
using MusicEngine.Core.Automation;
using MusicEngine.Core.UndoRedo;

namespace MusicEngineEditor.Commands;

/// <summary>
/// Command for adding an automation point.
/// </summary>
public sealed class AutomationPointAddCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly double _time;
    private readonly float _value;
    private readonly AutomationCurveType _curveType;
    private AutomationPoint? _addedPoint;

    /// <inheritdoc/>
    public string Description => $"Add Automation Point at {_time:F2}s";

    /// <summary>
    /// Creates a new AutomationPointAddCommand.
    /// </summary>
    /// <param name="lane">The automation lane to add the point to.</param>
    /// <param name="time">The time position of the point.</param>
    /// <param name="value">The value at this point.</param>
    /// <param name="curveType">The curve type for interpolation.</param>
    public AutomationPointAddCommand(AutomationLane lane, double time, float value, AutomationCurveType curveType = AutomationCurveType.Linear)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        _time = time;
        _value = value;
        _curveType = curveType;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _addedPoint = _lane.Curve.AddPoint(_time, _value, _curveType);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (_addedPoint != null)
        {
            _lane.Curve.RemovePoint(_addedPoint);
            _addedPoint = null;
        }
    }
}

/// <summary>
/// Command for deleting one or more automation points.
/// </summary>
public sealed class AutomationPointDeleteCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly List<PointSnapshot> _deletedPoints;

    /// <inheritdoc/>
    public string Description => _deletedPoints.Count == 1
        ? $"Delete Automation Point"
        : $"Delete {_deletedPoints.Count} Automation Points";

    /// <summary>
    /// Creates a new AutomationPointDeleteCommand for a single point.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    /// <param name="point">The point to delete.</param>
    public AutomationPointDeleteCommand(AutomationLane lane, AutomationPoint point)
        : this(lane, new[] { point })
    {
    }

    /// <summary>
    /// Creates a new AutomationPointDeleteCommand for multiple points.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    /// <param name="points">The points to delete.</param>
    public AutomationPointDeleteCommand(AutomationLane lane, IEnumerable<AutomationPoint> points)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        _deletedPoints = points.Select(p => new PointSnapshot(p.Time, p.Value, p.CurveType)).ToList();
    }

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var snapshot in _deletedPoints)
        {
            var point = _lane.Curve.Points.FirstOrDefault(p => Math.Abs(p.Time - snapshot.Time) < 0.001);
            if (point != null)
            {
                _lane.Curve.RemovePoint(point);
            }
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        foreach (var snapshot in _deletedPoints)
        {
            _lane.Curve.AddPoint(snapshot.Time, snapshot.Value, snapshot.CurveType);
        }
    }

    private readonly record struct PointSnapshot(double Time, float Value, AutomationCurveType CurveType);
}

/// <summary>
/// Command for moving an automation point.
/// </summary>
public sealed class AutomationPointMoveCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly double _oldTime;
    private readonly float _oldValue;
    private readonly double _newTime;
    private readonly float _newValue;
    private readonly AutomationCurveType _curveType;

    /// <inheritdoc/>
    public string Description => "Move Automation Point";

    /// <summary>
    /// Creates a new AutomationPointMoveCommand.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    /// <param name="point">The point to move.</param>
    /// <param name="newTime">The new time position.</param>
    /// <param name="newValue">The new value.</param>
    public AutomationPointMoveCommand(AutomationLane lane, AutomationPoint point, double newTime, float newValue)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        ArgumentNullException.ThrowIfNull(point);
        _oldTime = point.Time;
        _oldValue = point.Value;
        _curveType = point.CurveType;
        _newTime = newTime;
        _newValue = newValue;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        var point = _lane.Curve.Points.FirstOrDefault(p => Math.Abs(p.Time - _oldTime) < 0.001);
        if (point != null)
        {
            _lane.Curve.RemovePoint(point);
        }
        _lane.Curve.AddPoint(_newTime, _newValue, _curveType);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        var point = _lane.Curve.Points.FirstOrDefault(p => Math.Abs(p.Time - _newTime) < 0.001);
        if (point != null)
        {
            _lane.Curve.RemovePoint(point);
        }
        _lane.Curve.AddPoint(_oldTime, _oldValue, _curveType);
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        // Allow merging moves on the same point (same original time)
        return other is AutomationPointMoveCommand otherMove &&
               otherMove._lane == _lane &&
               Math.Abs(otherMove._oldTime - _newTime) < 0.0001;
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is AutomationPointMoveCommand otherMove)
        {
            // Keep our original position, use their final position
            return new AutomationPointMoveCommand(
                _lane,
                new AutomationPoint { Time = _oldTime, Value = _oldValue, CurveType = _curveType },
                otherMove._newTime,
                otherMove._newValue);
        }
        return this;
    }
}

/// <summary>
/// Command for changing the curve type of an automation point.
/// </summary>
public sealed class AutomationPointCurveTypeCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly double _time;
    private readonly float _value;
    private readonly AutomationCurveType _oldCurveType;
    private readonly AutomationCurveType _newCurveType;

    /// <inheritdoc/>
    public string Description => $"Change Curve Type to {_newCurveType}";

    /// <summary>
    /// Creates a new AutomationPointCurveTypeCommand.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    /// <param name="point">The point to modify.</param>
    /// <param name="newCurveType">The new curve type.</param>
    public AutomationPointCurveTypeCommand(AutomationLane lane, AutomationPoint point, AutomationCurveType newCurveType)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        ArgumentNullException.ThrowIfNull(point);
        _time = point.Time;
        _value = point.Value;
        _oldCurveType = point.CurveType;
        _newCurveType = newCurveType;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        var point = _lane.Curve.Points.FirstOrDefault(p => Math.Abs(p.Time - _time) < 0.001);
        if (point != null)
        {
            _lane.Curve.RemovePoint(point);
            _lane.Curve.AddPoint(_time, _value, _newCurveType);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        var point = _lane.Curve.Points.FirstOrDefault(p => Math.Abs(p.Time - _time) < 0.001);
        if (point != null)
        {
            _lane.Curve.RemovePoint(point);
            _lane.Curve.AddPoint(_time, _value, _oldCurveType);
        }
    }
}

/// <summary>
/// Command for clearing all points in an automation lane.
/// </summary>
public sealed class AutomationClearCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly List<PointSnapshot> _clearedPoints;

    /// <inheritdoc/>
    public string Description => $"Clear Automation Lane";

    /// <summary>
    /// Creates a new AutomationClearCommand.
    /// </summary>
    /// <param name="lane">The automation lane to clear.</param>
    public AutomationClearCommand(AutomationLane lane)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        _clearedPoints = _lane.Curve.Points.Select(p => new PointSnapshot(p.Time, p.Value, p.CurveType)).ToList();
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _lane.ClearPoints();
    }

    /// <inheritdoc/>
    public void Undo()
    {
        foreach (var snapshot in _clearedPoints)
        {
            _lane.Curve.AddPoint(snapshot.Time, snapshot.Value, snapshot.CurveType);
        }
    }

    private readonly record struct PointSnapshot(double Time, float Value, AutomationCurveType CurveType);
}

/// <summary>
/// Command for copying automation points to a clipboard.
/// </summary>
public sealed class CopyAutomationCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly double? _startTime;
    private readonly double? _endTime;
    private static List<PointSnapshot>? _clipboard;

    /// <inheritdoc/>
    public string Description => "Copy Automation";

    /// <summary>
    /// Gets the copied points from the clipboard.
    /// </summary>
    public static IReadOnlyList<PointSnapshot>? ClipboardPoints => _clipboard?.AsReadOnly();

    /// <summary>
    /// Creates a new CopyAutomationCommand to copy all points.
    /// </summary>
    /// <param name="lane">The automation lane to copy from.</param>
    public CopyAutomationCommand(AutomationLane lane)
        : this(lane, null, null)
    {
    }

    /// <summary>
    /// Creates a new CopyAutomationCommand to copy points in a time range.
    /// </summary>
    /// <param name="lane">The automation lane to copy from.</param>
    /// <param name="startTime">The start time of the range (inclusive).</param>
    /// <param name="endTime">The end time of the range (inclusive).</param>
    public CopyAutomationCommand(AutomationLane lane, double? startTime, double? endTime)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        _startTime = startTime;
        _endTime = endTime;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        var points = _lane.Curve.Points;

        // Filter by time range if specified
        IEnumerable<AutomationPoint> selectedPoints = points;
        if (_startTime.HasValue)
        {
            selectedPoints = selectedPoints.Where(p => p.Time >= _startTime.Value);
        }
        if (_endTime.HasValue)
        {
            selectedPoints = selectedPoints.Where(p => p.Time <= _endTime.Value);
        }

        // Store in clipboard with full point data
        _clipboard = selectedPoints.Select(p => new PointSnapshot(
            p.Time, p.Value, p.CurveType, p.Tension,
            p.BezierX1, p.BezierY1, p.BezierX2, p.BezierY2,
            p.IsLocked, p.Label)).ToList();

        // Normalize times relative to first point
        if (_clipboard.Count > 0)
        {
            double baseTime = _clipboard[0].Time;
            _clipboard = _clipboard.Select(p => p with { Time = p.Time - baseTime }).ToList();
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        // Copy is a read-only operation on the lane, but we could clear clipboard
        // For simplicity, we don't undo clipboard changes
    }

    /// <summary>
    /// Extended snapshot for copy/paste operations.
    /// </summary>
    public readonly record struct PointSnapshot(
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

/// <summary>
/// Command for pasting automation points from clipboard.
/// </summary>
public sealed class PasteAutomationCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly double _pasteTime;
    private readonly bool _replaceExisting;
    private List<AutomationPoint>? _pastedPoints;
    private List<PointSnapshot>? _replacedPoints;

    /// <inheritdoc/>
    public string Description => "Paste Automation";

    /// <summary>
    /// Creates a new PasteAutomationCommand.
    /// </summary>
    /// <param name="lane">The automation lane to paste into.</param>
    /// <param name="pasteTime">The time position to paste at.</param>
    /// <param name="replaceExisting">If true, removes existing points in the paste range.</param>
    public PasteAutomationCommand(AutomationLane lane, double pasteTime, bool replaceExisting = false)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        _pasteTime = pasteTime;
        _replaceExisting = replaceExisting;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        var clipboard = CopyAutomationCommand.ClipboardPoints;
        if (clipboard == null || clipboard.Count == 0)
            return;

        _pastedPoints = new List<AutomationPoint>();

        // Calculate paste range
        double pasteEnd = _pasteTime + clipboard[^1].Time;

        // Remove existing points in range if requested
        if (_replaceExisting)
        {
            var existingInRange = _lane.Curve.Points
                .Where(p => p.Time >= _pasteTime && p.Time <= pasteEnd)
                .ToList();

            _replacedPoints = existingInRange.Select(p => new PointSnapshot(
                p.Time, p.Value, p.CurveType, p.Tension,
                p.BezierX1, p.BezierY1, p.BezierX2, p.BezierY2,
                p.IsLocked, p.Label)).ToList();

            foreach (var point in existingInRange)
            {
                _lane.Curve.RemovePoint(point);
            }
        }

        // Paste points at the specified time
        foreach (var snapshot in clipboard)
        {
            var newPoint = _lane.Curve.AddPoint(_pasteTime + snapshot.Time, snapshot.Value, snapshot.CurveType);
            newPoint.Tension = snapshot.Tension;
            newPoint.BezierX1 = snapshot.BezierX1;
            newPoint.BezierY1 = snapshot.BezierY1;
            newPoint.BezierX2 = snapshot.BezierX2;
            newPoint.BezierY2 = snapshot.BezierY2;
            newPoint.IsLocked = snapshot.IsLocked;
            newPoint.Label = snapshot.Label;
            _pastedPoints.Add(newPoint);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        // Remove pasted points
        if (_pastedPoints != null)
        {
            foreach (var point in _pastedPoints)
            {
                _lane.Curve.RemovePoint(point);
            }
            _pastedPoints = null;
        }

        // Restore replaced points
        if (_replacedPoints != null)
        {
            foreach (var snapshot in _replacedPoints)
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
            _replacedPoints = null;
        }
    }

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

/// <summary>
/// Command for scaling automation point values by a factor.
/// </summary>
public sealed class ScaleAutomationCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly float _scaleFactor;
    private readonly float _pivotValue;
    private readonly double? _startTime;
    private readonly double? _endTime;
    private List<PointValueSnapshot>? _originalValues;

    /// <inheritdoc/>
    public string Description => $"Scale Automation by {_scaleFactor:F2}x";

    /// <summary>
    /// Creates a new ScaleAutomationCommand to scale all points.
    /// </summary>
    /// <param name="lane">The automation lane to scale.</param>
    /// <param name="scaleFactor">The scale factor (1.0 = no change, 2.0 = double values).</param>
    /// <param name="pivotValue">The pivot value around which to scale (default 0).</param>
    public ScaleAutomationCommand(AutomationLane lane, float scaleFactor, float pivotValue = 0f)
        : this(lane, scaleFactor, pivotValue, null, null)
    {
    }

    /// <summary>
    /// Creates a new ScaleAutomationCommand to scale points in a time range.
    /// </summary>
    /// <param name="lane">The automation lane to scale.</param>
    /// <param name="scaleFactor">The scale factor.</param>
    /// <param name="pivotValue">The pivot value around which to scale.</param>
    /// <param name="startTime">The start time of the range (inclusive).</param>
    /// <param name="endTime">The end time of the range (inclusive).</param>
    public ScaleAutomationCommand(AutomationLane lane, float scaleFactor, float pivotValue, double? startTime, double? endTime)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        _scaleFactor = scaleFactor;
        _pivotValue = pivotValue;
        _startTime = startTime;
        _endTime = endTime;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        var points = GetAffectedPoints();
        _originalValues = points.Select(p => new PointValueSnapshot(p.Time, p.Value)).ToList();

        foreach (var point in points)
        {
            // Scale around pivot: newValue = pivot + (oldValue - pivot) * factor
            float scaledValue = _pivotValue + (point.Value - _pivotValue) * _scaleFactor;
            point.Value = Math.Clamp(scaledValue, _lane.MinValue, _lane.MaxValue);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (_originalValues == null)
            return;

        var points = _lane.Curve.Points;
        foreach (var snapshot in _originalValues)
        {
            var point = points.FirstOrDefault(p => Math.Abs(p.Time - snapshot.Time) < 0.0001);
            if (point != null)
            {
                point.Value = snapshot.Value;
            }
        }
        _originalValues = null;
    }

    private List<AutomationPoint> GetAffectedPoints()
    {
        IEnumerable<AutomationPoint> points = _lane.Curve.Points;

        if (_startTime.HasValue)
            points = points.Where(p => p.Time >= _startTime.Value);
        if (_endTime.HasValue)
            points = points.Where(p => p.Time <= _endTime.Value);

        return points.ToList();
    }

    private readonly record struct PointValueSnapshot(double Time, float Value);
}

/// <summary>
/// Command for shifting all automation points in time.
/// </summary>
public sealed class ShiftAutomationCommand : IUndoableCommand
{
    private readonly AutomationLane _lane;
    private readonly double _timeOffset;
    private readonly double? _startTime;
    private readonly double? _endTime;
    private List<PointTimeSnapshot>? _originalTimes;

    /// <inheritdoc/>
    public string Description => _timeOffset >= 0
        ? $"Shift Automation +{_timeOffset:F2}"
        : $"Shift Automation {_timeOffset:F2}";

    /// <summary>
    /// Creates a new ShiftAutomationCommand to shift all points.
    /// </summary>
    /// <param name="lane">The automation lane to shift.</param>
    /// <param name="timeOffset">The time offset in beats (positive = later, negative = earlier).</param>
    public ShiftAutomationCommand(AutomationLane lane, double timeOffset)
        : this(lane, timeOffset, null, null)
    {
    }

    /// <summary>
    /// Creates a new ShiftAutomationCommand to shift points in a time range.
    /// </summary>
    /// <param name="lane">The automation lane to shift.</param>
    /// <param name="timeOffset">The time offset in beats.</param>
    /// <param name="startTime">The start time of the range (inclusive).</param>
    /// <param name="endTime">The end time of the range (inclusive).</param>
    public ShiftAutomationCommand(AutomationLane lane, double timeOffset, double? startTime, double? endTime)
    {
        _lane = lane ?? throw new ArgumentNullException(nameof(lane));
        _timeOffset = timeOffset;
        _startTime = startTime;
        _endTime = endTime;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        var points = GetAffectedPoints();
        _originalTimes = points.Select(p => new PointTimeSnapshot(p.Id, p.Time)).ToList();

        foreach (var point in points)
        {
            double newTime = point.Time + _timeOffset;
            // Prevent negative times
            point.Time = Math.Max(0, newTime);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (_originalTimes == null)
            return;

        foreach (var snapshot in _originalTimes)
        {
            var point = _lane.Curve.Points.FirstOrDefault(p => p.Id == snapshot.Id);
            if (point != null)
            {
                point.Time = snapshot.Time;
            }
        }
        _originalTimes = null;
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        // Allow merging consecutive shifts on the same lane and range
        return other is ShiftAutomationCommand otherShift &&
               otherShift._lane == _lane &&
               otherShift._startTime == _startTime &&
               otherShift._endTime == _endTime;
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is ShiftAutomationCommand otherShift)
        {
            // Combine the offsets
            return new ShiftAutomationCommand(_lane, _timeOffset + otherShift._timeOffset, _startTime, _endTime);
        }
        return this;
    }

    private List<AutomationPoint> GetAffectedPoints()
    {
        IEnumerable<AutomationPoint> points = _lane.Curve.Points;

        if (_startTime.HasValue)
            points = points.Where(p => p.Time >= _startTime.Value);
        if (_endTime.HasValue)
            points = points.Where(p => p.Time <= _endTime.Value);

        return points.ToList();
    }

    private readonly record struct PointTimeSnapshot(long Id, double Time);
}
