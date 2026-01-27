// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service for managing collaboration session persistence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing collaboration session history with persistence.
/// Stores recent sessions for quick reconnection.
/// </summary>
public class CollaborationSessionService
{
    private static readonly string SessionsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor",
        "Collaboration");

    private static readonly string SessionsFile = Path.Combine(SessionsFolder, "session-history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Maximum number of sessions to store in history.
    /// </summary>
    public const int MaxSessionHistory = 10;

    private readonly List<CollaborationSessionInfo> _sessions = [];
    private bool _isLoaded;

    /// <summary>
    /// Gets the collection of saved sessions, ordered by last connected time.
    /// </summary>
    public ReadOnlyCollection<CollaborationSessionInfo> Sessions =>
        _sessions.OrderByDescending(s => s.LastConnected).ToList().AsReadOnly();

    /// <summary>
    /// Event raised when the session list changes.
    /// </summary>
    public event EventHandler? SessionsChanged;

    /// <summary>
    /// Loads session history from disk.
    /// </summary>
    public async Task LoadSessionsAsync()
    {
        if (_isLoaded) return;

        try
        {
            if (File.Exists(SessionsFile))
            {
                var json = await File.ReadAllTextAsync(SessionsFile);
                var loaded = JsonSerializer.Deserialize<SessionHistoryData>(json, JsonOptions);

                if (loaded?.Sessions != null)
                {
                    _sessions.Clear();
                    _sessions.AddRange(loaded.Sessions);
                }
            }

            _isLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load collaboration sessions: {ex.Message}");
            _isLoaded = true;
        }
    }

    /// <summary>
    /// Saves session history to disk.
    /// </summary>
    public async Task SaveSessionsAsync()
    {
        try
        {
            Directory.CreateDirectory(SessionsFolder);

            var data = new SessionHistoryData
            {
                Sessions = _sessions.ToList(),
                LastSaved = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(SessionsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save collaboration sessions: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds or updates a session in the history.
    /// </summary>
    /// <param name="session">The session info to add or update.</param>
    public async Task AddOrUpdateSessionAsync(CollaborationSessionInfo session)
    {
        await LoadSessionsAsync();

        // Check if session with same code already exists
        var existing = _sessions.FirstOrDefault(s =>
            s.SessionCode.Equals(session.SessionCode, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            // Update existing session
            existing.SessionName = session.SessionName;
            existing.ServerAddress = session.ServerAddress;
            existing.Port = session.Port;
            existing.Username = session.Username;
            existing.RoomName = session.RoomName;
            existing.WasHost = session.WasHost;
            existing.LastConnected = DateTime.UtcNow;
            existing.ConnectionCount++;
            existing.HasPassword = session.HasPassword;
        }
        else
        {
            // Add new session
            session.Created = DateTime.UtcNow;
            session.LastConnected = DateTime.UtcNow;
            session.ConnectionCount = 1;
            _sessions.Add(session);
        }

        // Trim to max history size
        while (_sessions.Count > MaxSessionHistory)
        {
            var oldest = _sessions.OrderBy(s => s.LastConnected).First();
            _sessions.Remove(oldest);
        }

        await SaveSessionsAsync();
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes a session from history.
    /// </summary>
    /// <param name="sessionId">The session ID to remove.</param>
    public async Task RemoveSessionAsync(string sessionId)
    {
        await LoadSessionsAsync();

        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null)
        {
            _sessions.Remove(session);
            await SaveSessionsAsync();
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Removes a session from history by session code.
    /// </summary>
    /// <param name="sessionCode">The session code to remove.</param>
    public async Task RemoveSessionByCodeAsync(string sessionCode)
    {
        await LoadSessionsAsync();

        var session = _sessions.FirstOrDefault(s =>
            s.SessionCode.Equals(sessionCode, StringComparison.OrdinalIgnoreCase));
        if (session != null)
        {
            _sessions.Remove(session);
            await SaveSessionsAsync();
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Clears all session history.
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        _sessions.Clear();
        await SaveSessionsAsync();
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets a session by its code.
    /// </summary>
    /// <param name="sessionCode">The session code.</param>
    /// <returns>The session info if found, null otherwise.</returns>
    public async Task<CollaborationSessionInfo?> GetSessionByCodeAsync(string sessionCode)
    {
        await LoadSessionsAsync();
        return _sessions.FirstOrDefault(s =>
            s.SessionCode.Equals(sessionCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the most recently connected session.
    /// </summary>
    /// <returns>The most recent session, or null if no sessions exist.</returns>
    public async Task<CollaborationSessionInfo?> GetMostRecentSessionAsync()
    {
        await LoadSessionsAsync();
        return _sessions.OrderByDescending(s => s.LastConnected).FirstOrDefault();
    }

    /// <summary>
    /// Gets sessions where the user was the host.
    /// </summary>
    /// <returns>Collection of hosted sessions.</returns>
    public async Task<IReadOnlyList<CollaborationSessionInfo>> GetHostedSessionsAsync()
    {
        await LoadSessionsAsync();
        return _sessions.Where(s => s.WasHost)
            .OrderByDescending(s => s.LastConnected)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets sessions where the user joined as a guest.
    /// </summary>
    /// <returns>Collection of joined sessions.</returns>
    public async Task<IReadOnlyList<CollaborationSessionInfo>> GetJoinedSessionsAsync()
    {
        await LoadSessionsAsync();
        return _sessions.Where(s => !s.WasHost)
            .OrderByDescending(s => s.LastConnected)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets sessions marked for auto-reconnect.
    /// </summary>
    /// <returns>Collection of auto-reconnect sessions.</returns>
    public async Task<IReadOnlyList<CollaborationSessionInfo>> GetAutoReconnectSessionsAsync()
    {
        await LoadSessionsAsync();
        return _sessions.Where(s => s.AutoReconnect)
            .OrderByDescending(s => s.LastConnected)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Sets the auto-reconnect flag for a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="autoReconnect">Whether to auto-reconnect.</param>
    public async Task SetAutoReconnectAsync(string sessionId, bool autoReconnect)
    {
        await LoadSessionsAsync();

        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null)
        {
            session.AutoReconnect = autoReconnect;
            await SaveSessionsAsync();
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Updates the last connected time for a session.
    /// </summary>
    /// <param name="sessionCode">The session code.</param>
    public async Task UpdateLastConnectedAsync(string sessionCode)
    {
        await LoadSessionsAsync();

        var session = _sessions.FirstOrDefault(s =>
            s.SessionCode.Equals(sessionCode, StringComparison.OrdinalIgnoreCase));
        if (session != null)
        {
            session.LastConnected = DateTime.UtcNow;
            session.ConnectionCount++;
            await SaveSessionsAsync();
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Data container for session history persistence.
    /// </summary>
    private class SessionHistoryData
    {
        [JsonPropertyName("sessions")]
        public List<CollaborationSessionInfo> Sessions { get; set; } = [];

        [JsonPropertyName("lastSaved")]
        public DateTime LastSaved { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;
    }
}
