// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Section/arrangement commands.

using System;
using MusicEngine.Core;
using MusicEngine.Core.UndoRedo;

namespace MusicEngineEditor.Commands;

/// <summary>
/// Command for adding a section to the arrangement.
/// </summary>
public sealed class SectionAddCommand : IUndoableCommand
{
    private readonly Arrangement _arrangement;
    private readonly ArrangementSection _section;
    private readonly double _startPosition;
    private readonly double _endPosition;
    private readonly SectionType _sectionType;
    private readonly string? _customName;

    /// <inheritdoc/>
    public string Description => $"Add {_section?.Name ?? _sectionType.ToString()} Section";

    /// <summary>
    /// Creates a new SectionAddCommand with a section type.
    /// </summary>
    /// <param name="arrangement">The arrangement to add to.</param>
    /// <param name="startPosition">The start position in beats.</param>
    /// <param name="endPosition">The end position in beats.</param>
    /// <param name="sectionType">The type of section to add.</param>
    public SectionAddCommand(Arrangement arrangement, double startPosition, double endPosition, SectionType sectionType)
    {
        _arrangement = arrangement ?? throw new ArgumentNullException(nameof(arrangement));
        _startPosition = startPosition;
        _endPosition = endPosition;
        _sectionType = sectionType;
        _customName = null;
        _section = null!; // Will be set in Execute
    }

    /// <summary>
    /// Creates a new SectionAddCommand with a custom name.
    /// </summary>
    /// <param name="arrangement">The arrangement to add to.</param>
    /// <param name="startPosition">The start position in beats.</param>
    /// <param name="endPosition">The end position in beats.</param>
    /// <param name="customName">The custom name for the section.</param>
    public SectionAddCommand(Arrangement arrangement, double startPosition, double endPosition, string customName)
    {
        _arrangement = arrangement ?? throw new ArgumentNullException(nameof(arrangement));
        _startPosition = startPosition;
        _endPosition = endPosition;
        _sectionType = SectionType.Custom;
        _customName = customName;
        _section = null!; // Will be set in Execute
    }

    /// <summary>
    /// Creates a new SectionAddCommand with an existing section.
    /// </summary>
    /// <param name="arrangement">The arrangement to add to.</param>
    /// <param name="section">The section to add.</param>
    public SectionAddCommand(Arrangement arrangement, ArrangementSection section)
    {
        _arrangement = arrangement ?? throw new ArgumentNullException(nameof(arrangement));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _startPosition = section.StartPosition;
        _endPosition = section.EndPosition;
        _sectionType = section.Type;
        _customName = section.Name;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        if (_customName != null)
        {
            _arrangement.AddSection(_startPosition, _endPosition, _customName);
        }
        else
        {
            _arrangement.AddSection(_startPosition, _endPosition, _sectionType);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        var section = _arrangement.GetSectionAt(_startPosition);
        if (section != null)
        {
            _arrangement.RemoveSection(section);
        }
    }
}

/// <summary>
/// Command for deleting a section from the arrangement.
/// </summary>
public sealed class SectionDeleteCommand : IUndoableCommand
{
    private readonly Arrangement _arrangement;
    private readonly SectionSnapshot _snapshot;

    /// <inheritdoc/>
    public string Description => $"Delete {_snapshot.Name} Section";

    /// <summary>
    /// Creates a new SectionDeleteCommand.
    /// </summary>
    /// <param name="arrangement">The arrangement to remove from.</param>
    /// <param name="section">The section to delete.</param>
    public SectionDeleteCommand(Arrangement arrangement, ArrangementSection section)
    {
        _arrangement = arrangement ?? throw new ArgumentNullException(nameof(arrangement));
        ArgumentNullException.ThrowIfNull(section);

        _snapshot = new SectionSnapshot(
            section.Name,
            section.StartPosition,
            section.EndPosition,
            section.Type,
            section.RepeatCount,
            section.IsMuted,
            section.IsLocked,
            section.Color
        );
    }

    /// <inheritdoc/>
    public void Execute()
    {
        var section = _arrangement.GetSectionAt(_snapshot.StartPosition);
        if (section != null)
        {
            _arrangement.RemoveSection(section);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        var section = _arrangement.AddSection(_snapshot.StartPosition, _snapshot.EndPosition, _snapshot.Name);
        if (section != null)
        {
            section.RepeatCount = _snapshot.RepeatCount;
            section.IsMuted = _snapshot.IsMuted;
            section.IsLocked = _snapshot.IsLocked;
            section.Color = _snapshot.Color;
        }
    }

    private readonly record struct SectionSnapshot(
        string Name,
        double StartPosition,
        double EndPosition,
        SectionType SectionType,
        int RepeatCount,
        bool IsMuted,
        bool IsLocked,
        string Color);
}

/// <summary>
/// Command for moving or resizing a section in the arrangement.
/// </summary>
public sealed class SectionMoveCommand : IUndoableCommand
{
    private readonly Arrangement _arrangement;
    private readonly ArrangementSection _section;
    private readonly double _oldStartPosition;
    private readonly double _oldEndPosition;
    private readonly double _newStartPosition;
    private readonly double _newEndPosition;

    /// <inheritdoc/>
    public string Description => _oldEndPosition - _oldStartPosition != _newEndPosition - _newStartPosition
        ? $"Resize {_section.Name} Section"
        : $"Move {_section.Name} Section";

    /// <summary>
    /// Creates a new SectionMoveCommand for moving a section.
    /// </summary>
    /// <param name="arrangement">The arrangement.</param>
    /// <param name="section">The section to move.</param>
    /// <param name="newStartPosition">The new start position in beats.</param>
    public SectionMoveCommand(Arrangement arrangement, ArrangementSection section, double newStartPosition)
    {
        _arrangement = arrangement ?? throw new ArgumentNullException(nameof(arrangement));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _oldStartPosition = section.StartPosition;
        _oldEndPosition = section.EndPosition;
        _newStartPosition = newStartPosition;
        _newEndPosition = newStartPosition + (section.EndPosition - section.StartPosition);
    }

    /// <summary>
    /// Creates a new SectionMoveCommand for moving and/or resizing a section.
    /// </summary>
    /// <param name="arrangement">The arrangement.</param>
    /// <param name="section">The section to modify.</param>
    /// <param name="newStartPosition">The new start position in beats.</param>
    /// <param name="newEndPosition">The new end position in beats.</param>
    public SectionMoveCommand(Arrangement arrangement, ArrangementSection section, double newStartPosition, double newEndPosition)
    {
        _arrangement = arrangement ?? throw new ArgumentNullException(nameof(arrangement));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _oldStartPosition = section.StartPosition;
        _oldEndPosition = section.EndPosition;
        _newStartPosition = newStartPosition;
        _newEndPosition = newEndPosition;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _arrangement.MoveSection(_section, _newStartPosition);
        if (Math.Abs(_newEndPosition - _newStartPosition - (_oldEndPosition - _oldStartPosition)) > 0.001)
        {
            _section.EndPosition = _newEndPosition;
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _arrangement.MoveSection(_section, _oldStartPosition);
        _section.EndPosition = _oldEndPosition;
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        return other is SectionMoveCommand otherMove &&
               otherMove._section == _section &&
               Math.Abs(otherMove._oldStartPosition - _newStartPosition) < 0.001;
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is SectionMoveCommand otherMove)
        {
            return new SectionMoveCommand(_arrangement, _section, _oldStartPosition, _oldEndPosition)
            {
                // This creates a command from our original position to their final position
            };
        }
        return this;
    }
}

/// <summary>
/// Command for changing section properties (mute, lock, repeat count, etc.).
/// </summary>
public sealed class SectionPropertyCommand : IUndoableCommand
{
    private readonly ArrangementSection _section;
    private readonly string _propertyName;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    /// <inheritdoc/>
    public string Description => $"Change {_propertyName} on {_section.Name}";

    /// <summary>
    /// Creates a new SectionPropertyCommand.
    /// </summary>
    /// <param name="section">The section to modify.</param>
    /// <param name="propertyName">The name of the property being changed.</param>
    /// <param name="oldValue">The old value.</param>
    /// <param name="newValue">The new value.</param>
    public SectionPropertyCommand(ArrangementSection section, string propertyName, object? oldValue, object? newValue)
    {
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _oldValue = oldValue;
        _newValue = newValue;
    }

    /// <inheritdoc/>
    public void Execute() => SetProperty(_newValue);

    /// <inheritdoc/>
    public void Undo() => SetProperty(_oldValue);

    private void SetProperty(object? value)
    {
        switch (_propertyName)
        {
            case nameof(ArrangementSection.IsMuted):
                _section.IsMuted = (bool)(value ?? false);
                break;
            case nameof(ArrangementSection.IsLocked):
                _section.IsLocked = (bool)(value ?? false);
                break;
            case nameof(ArrangementSection.RepeatCount):
                _section.RepeatCount = (int)(value ?? 1);
                break;
            case nameof(ArrangementSection.Color):
                _section.Color = (string)(value ?? "#4A9EFF");
                break;
            case nameof(ArrangementSection.Name):
                // Note: Name change may need special handling via Arrangement.RenameSection if available
                break;
        }
        _section.Touch();
    }
}
