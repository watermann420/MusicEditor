// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service for managing collaborator cursor visualization.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Infrastructure.Collaboration;
using MusicEngineEditor.Controls;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents cursor data for a collaborator.
/// </summary>
public class CollaboratorCursorData
{
    /// <summary>
    /// Gets the peer ID.
    /// </summary>
    public Guid PeerId { get; init; }

    /// <summary>
    /// Gets or sets the peer name.
    /// </summary>
    public string PeerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the peer color (ARGB).
    /// </summary>
    public uint Color { get; set; }

    /// <summary>
    /// Gets or sets the current view type.
    /// </summary>
    public string ViewType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the X position (beats or pixels depending on view).
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y position (track index or note number).
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets the track ID if applicable.
    /// </summary>
    public Guid? TrackId { get; set; }

    /// <summary>
    /// Gets or sets the selection start position.
    /// </summary>
    public (double X, double Y)? SelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the selection end position.
    /// </summary>
    public (double X, double Y)? SelectionEnd { get; set; }

    /// <summary>
    /// Gets or sets whether the peer is actively editing.
    /// </summary>
    public bool IsActivelyEditing { get; set; }

    /// <summary>
    /// Gets or sets the last update time.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets whether this cursor data is stale (older than 10 seconds).
    /// </summary>
    public bool IsStale => (DateTime.UtcNow - LastUpdated).TotalSeconds > 10;
}

/// <summary>
/// Event arguments for cursor updates.
/// </summary>
public class CursorUpdateEventArgs : EventArgs
{
    /// <summary>
    /// Gets the cursor data.
    /// </summary>
    public CollaboratorCursorData CursorData { get; }

    /// <summary>
    /// Creates cursor update event arguments.
    /// </summary>
    /// <param name="cursorData">The cursor data.</param>
    public CursorUpdateEventArgs(CollaboratorCursorData cursorData)
    {
        CursorData = cursorData;
    }
}

/// <summary>
/// Service for managing collaborator cursor positions and visualization.
/// </summary>
public class CollaboratorCursorService : IDisposable
{
    private static CollaboratorCursorService? _instance;
    private static readonly object _instanceLock = new();

    private readonly ConcurrentDictionary<Guid, CollaboratorCursorData> _cursors = new();
    private readonly ConcurrentDictionary<Guid, CollaboratorCursor> _cursorControls = new();
    private readonly List<Canvas> _registeredCanvases = [];
    private readonly object _canvasLock = new();

    private CollaborationSession? _session;
    private DispatcherTimer? _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Gets the singleton instance of the service.
    /// </summary>
    public static CollaboratorCursorService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new CollaboratorCursorService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Raised when a cursor is updated.
    /// </summary>
    public event EventHandler<CursorUpdateEventArgs>? CursorUpdated;

    /// <summary>
    /// Raised when a cursor is removed.
    /// </summary>
    public event EventHandler<Guid>? CursorRemoved;

    /// <summary>
    /// Gets all current cursor data.
    /// </summary>
    public IReadOnlyCollection<CollaboratorCursorData> Cursors => _cursors.Values.ToList().AsReadOnly();

    /// <summary>
    /// Gets whether collaboration is active.
    /// </summary>
    public bool IsCollaborationActive => _session?.State == SessionState.Active;

    /// <summary>
    /// Creates a new collaborator cursor service.
    /// </summary>
    private CollaboratorCursorService()
    {
        // Start cleanup timer to remove stale cursors
        _cleanupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _cleanupTimer.Tick += CleanupTimer_Tick;
        _cleanupTimer.Start();
    }

    /// <summary>
    /// Attaches the service to a collaboration session.
    /// </summary>
    /// <param name="session">The collaboration session.</param>
    public void AttachSession(CollaborationSession session)
    {
        AttachSession(session, null, null);
    }

    /// <summary>
    /// Attaches the service to a collaboration session with optional client/server.
    /// Also attaches the TransportSyncService for transport synchronization.
    /// </summary>
    /// <param name="session">The collaboration session.</param>
    /// <param name="client">Optional collaboration client.</param>
    /// <param name="server">Optional collaboration server.</param>
    public void AttachSession(CollaborationSession session, CollaborationClient? client, CollaborationServer? server)
    {
        if (_session != null)
        {
            DetachSession();
        }

        _session = session;
        _session.CursorUpdated += Session_CursorUpdated;
        _session.PeerLeft += Session_PeerLeft;
        _session.StateChanged += Session_StateChanged;

        // Initialize cursors for existing peers
        foreach (var peer in _session.Peers)
        {
            if (peer.CursorPosition != null)
            {
                UpdateCursor(peer);
            }
        }

        // Also attach the TransportSyncService for transport synchronization
        TransportSyncService.Instance.AttachSession(session, client, server);
    }

    /// <summary>
    /// Detaches from the current collaboration session.
    /// </summary>
    public void DetachSession()
    {
        if (_session != null)
        {
            _session.CursorUpdated -= Session_CursorUpdated;
            _session.PeerLeft -= Session_PeerLeft;
            _session.StateChanged -= Session_StateChanged;
            _session = null;
        }

        // Also detach the TransportSyncService
        TransportSyncService.Instance.DetachSession();

        ClearAllCursors();
    }

    /// <summary>
    /// Registers a canvas for cursor rendering.
    /// </summary>
    /// <param name="canvas">The canvas to register.</param>
    /// <param name="viewType">The view type this canvas represents.</param>
    public void RegisterCanvas(Canvas canvas, string viewType)
    {
        lock (_canvasLock)
        {
            if (!_registeredCanvases.Contains(canvas))
            {
                _registeredCanvases.Add(canvas);
                canvas.Tag = viewType;

                // Add existing cursors for this view type
                foreach (var cursorData in _cursors.Values.Where(c => c.ViewType == viewType))
                {
                    AddCursorToCanvas(canvas, cursorData);
                }
            }
        }
    }

    /// <summary>
    /// Unregisters a canvas from cursor rendering.
    /// </summary>
    /// <param name="canvas">The canvas to unregister.</param>
    public void UnregisterCanvas(Canvas canvas)
    {
        lock (_canvasLock)
        {
            // Remove cursor controls from this canvas
            foreach (var control in _cursorControls.Values)
            {
                if (control.Parent == canvas)
                {
                    canvas.Children.Remove(control);
                }
            }

            _registeredCanvases.Remove(canvas);
        }
    }

    /// <summary>
    /// Updates a cursor's pixel position for a specific view.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="viewType">The view type.</param>
    /// <param name="pixelX">X position in pixels.</param>
    /// <param name="pixelY">Y position in pixels.</param>
    public void UpdateCursorPixelPosition(Guid peerId, string viewType, double pixelX, double pixelY)
    {
        if (!_cursorControls.TryGetValue(peerId, out var control))
            return;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            control.SetPosition(pixelX, pixelY);
        });
    }

    /// <summary>
    /// Gets cursor data for a specific peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>The cursor data, or null if not found.</returns>
    public CollaboratorCursorData? GetCursor(Guid peerId)
    {
        _cursors.TryGetValue(peerId, out var cursor);
        return cursor;
    }

    /// <summary>
    /// Gets all cursors for a specific view type.
    /// </summary>
    /// <param name="viewType">The view type.</param>
    /// <returns>Collection of cursor data for the view.</returns>
    public IReadOnlyCollection<CollaboratorCursorData> GetCursorsForView(string viewType)
    {
        return _cursors.Values
            .Where(c => c.ViewType == viewType && !c.IsStale)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Sends local cursor update to the session.
    /// </summary>
    /// <param name="viewType">Current view type.</param>
    /// <param name="x">X position.</param>
    /// <param name="y">Y position.</param>
    /// <param name="trackId">Optional track ID.</param>
    /// <param name="selectionStart">Optional selection start.</param>
    /// <param name="selectionEnd">Optional selection end.</param>
    public void SendLocalCursorUpdate(string viewType, double x, double y, Guid? trackId = null,
        (double X, double Y)? selectionStart = null, (double X, double Y)? selectionEnd = null)
    {
        if (_session == null || _session.State != SessionState.Active)
            return;

        try
        {
            var message = _session.SendCursorUpdate(viewType, x, y, trackId, selectionStart, selectionEnd);
            // The message would be sent through the collaboration client/server
            // This is handled by the collaboration infrastructure
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send cursor update: {ex.Message}");
        }
    }

    private void Session_CursorUpdated(object? sender, PeerEventArgs e)
    {
        UpdateCursor(e.Peer);
    }

    private void Session_PeerLeft(object? sender, PeerEventArgs e)
    {
        RemoveCursor(e.Peer.Id);
    }

    private void Session_StateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        if (e.NewState == SessionState.Inactive || e.NewState == SessionState.Closing)
        {
            ClearAllCursors();
        }
    }

    private void UpdateCursor(CollaborationPeer peer)
    {
        if (peer.CursorPosition == null)
            return;

        var cursorData = _cursors.GetOrAdd(peer.Id, _ => new CollaboratorCursorData { PeerId = peer.Id });

        cursorData.PeerName = peer.Name;
        cursorData.Color = peer.Color;
        cursorData.ViewType = peer.CursorPosition.ViewType;
        cursorData.X = peer.CursorPosition.X;
        cursorData.Y = peer.CursorPosition.Y;
        cursorData.TrackId = peer.CursorPosition.TrackId;
        cursorData.LastUpdated = DateTime.UtcNow;

        if (peer.CurrentSelection != null)
        {
            cursorData.SelectionStart = peer.CurrentSelection.Start;
            cursorData.SelectionEnd = peer.CurrentSelection.End;
        }
        else
        {
            cursorData.SelectionStart = null;
            cursorData.SelectionEnd = null;
        }

        // Update or create cursor control on UI thread
        Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateCursorControl(cursorData);
        });

        CursorUpdated?.Invoke(this, new CursorUpdateEventArgs(cursorData));
    }

    private void UpdateCursorControl(CollaboratorCursorData cursorData)
    {
        if (!_cursorControls.TryGetValue(cursorData.PeerId, out var control))
        {
            // Create new cursor control
            control = CollaboratorCursor.Create(cursorData.PeerId, cursorData.PeerName, cursorData.Color);
            _cursorControls[cursorData.PeerId] = control;

            // Add to appropriate canvas
            AddCursorToCanvas(cursorData);
        }

        // Update control properties
        control.PeerName = cursorData.PeerName;
        control.CursorColor = Color.FromArgb(
            (byte)((cursorData.Color >> 24) & 0xFF),
            (byte)((cursorData.Color >> 16) & 0xFF),
            (byte)((cursorData.Color >> 8) & 0xFF),
            (byte)(cursorData.Color & 0xFF)
        );
        control.IsActivelyEditing = cursorData.IsActivelyEditing;

        // Update selection if present
        if (cursorData.SelectionStart.HasValue && cursorData.SelectionEnd.HasValue)
        {
            var start = cursorData.SelectionStart.Value;
            var end = cursorData.SelectionEnd.Value;
            control.SetSelectionBounds(
                Math.Min(start.X, end.X) - cursorData.X,
                Math.Min(start.Y, end.Y) - cursorData.Y,
                Math.Abs(end.X - start.X),
                Math.Abs(end.Y - start.Y)
            );
        }
        else
        {
            control.HideSelection();
        }
    }

    private void AddCursorToCanvas(CollaboratorCursorData cursorData)
    {
        if (!_cursorControls.TryGetValue(cursorData.PeerId, out var control))
            return;

        lock (_canvasLock)
        {
            var canvas = _registeredCanvases.FirstOrDefault(c => c.Tag?.ToString() == cursorData.ViewType);
            if (canvas != null && control.Parent != canvas)
            {
                // Remove from previous parent if any
                if (control.Parent is Canvas oldCanvas)
                {
                    oldCanvas.Children.Remove(control);
                }

                canvas.Children.Add(control);
                System.Windows.Controls.Panel.SetZIndex(control, 1000); // Ensure cursors are on top
            }
        }
    }

    private void AddCursorToCanvas(Canvas canvas, CollaboratorCursorData cursorData)
    {
        if (!_cursorControls.TryGetValue(cursorData.PeerId, out var control))
        {
            control = CollaboratorCursor.Create(cursorData.PeerId, cursorData.PeerName, cursorData.Color);
            _cursorControls[cursorData.PeerId] = control;
        }

        if (control.Parent != canvas)
        {
            if (control.Parent is Canvas oldCanvas)
            {
                oldCanvas.Children.Remove(control);
            }

            canvas.Children.Add(control);
            System.Windows.Controls.Panel.SetZIndex(control, 1000);
        }
    }

    private void RemoveCursor(Guid peerId)
    {
        _cursors.TryRemove(peerId, out _);

        if (_cursorControls.TryRemove(peerId, out var control))
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (control.Parent is Canvas canvas)
                {
                    canvas.Children.Remove(control);
                }
            });
        }

        CursorRemoved?.Invoke(this, peerId);
    }

    private void ClearAllCursors()
    {
        var peerIds = _cursors.Keys.ToList();
        foreach (var peerId in peerIds)
        {
            RemoveCursor(peerId);
        }
    }

    private void CleanupTimer_Tick(object? sender, EventArgs e)
    {
        // Remove stale cursors
        var staleCursors = _cursors.Values
            .Where(c => c.IsStale)
            .Select(c => c.PeerId)
            .ToList();

        foreach (var peerId in staleCursors)
        {
            RemoveCursor(peerId);
        }
    }

    /// <summary>
    /// Disposes the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer?.Stop();
        _cleanupTimer = null;

        DetachSession();
        ClearAllCursors();

        lock (_canvasLock)
        {
            _registeredCanvases.Clear();
        }

        _disposed = true;
    }
}
