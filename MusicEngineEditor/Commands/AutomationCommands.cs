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
