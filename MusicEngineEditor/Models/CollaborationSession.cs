// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Collaboration session model for persistence.

using System;
using System.Text.Json.Serialization;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a saved collaboration session for quick reconnection.
/// </summary>
public class CollaborationSessionInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for this session record.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the session name.
    /// </summary>
    [JsonPropertyName("sessionName")]
    public string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session code used for joining.
    /// </summary>
    [JsonPropertyName("sessionCode")]
    public string SessionCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the server address for the collaboration server.
    /// </summary>
    [JsonPropertyName("serverAddress")]
    public string ServerAddress { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the server port.
    /// </summary>
    [JsonPropertyName("port")]
    public int Port { get; set; } = 7890;

    /// <summary>
    /// Gets or sets the username used in the session.
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the room name (for multi-room servers).
    /// </summary>
    [JsonPropertyName("roomName")]
    public string RoomName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the user was the host in this session.
    /// </summary>
    [JsonPropertyName("wasHost")]
    public bool WasHost { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this session was last connected.
    /// </summary>
    [JsonPropertyName("lastConnected")]
    public DateTime LastConnected { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when this session was created.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the total number of times this session was joined.
    /// </summary>
    [JsonPropertyName("connectionCount")]
    public int ConnectionCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to auto-reconnect to this session on startup.
    /// </summary>
    [JsonPropertyName("autoReconnect")]
    public bool AutoReconnect { get; set; }

    /// <summary>
    /// Gets or sets whether the session password should be remembered.
    /// Note: Password is not stored, only the flag indicating if one was used.
    /// </summary>
    [JsonPropertyName("hasPassword")]
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets a display-friendly description of the session.
    /// </summary>
    [JsonIgnore]
    public string DisplayDescription => WasHost
        ? $"Hosted session - Last used {GetRelativeTime()}"
        : $"Joined session - Last used {GetRelativeTime()}";

    /// <summary>
    /// Gets a display-friendly connection string.
    /// </summary>
    [JsonIgnore]
    public string ConnectionString => $"{ServerAddress}:{Port}";

    /// <summary>
    /// Gets a relative time string for display.
    /// </summary>
    private string GetRelativeTime()
    {
        var diff = DateTime.UtcNow - LastConnected;

        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)} weeks ago";

        return LastConnected.ToLocalTime().ToString("MMM dd, yyyy");
    }

    /// <summary>
    /// Creates a deep copy of this session info.
    /// </summary>
    public CollaborationSessionInfo Clone()
    {
        return new CollaborationSessionInfo
        {
            Id = Id,
            SessionName = SessionName,
            SessionCode = SessionCode,
            ServerAddress = ServerAddress,
            Port = Port,
            Username = Username,
            RoomName = RoomName,
            WasHost = WasHost,
            LastConnected = LastConnected,
            Created = Created,
            ConnectionCount = ConnectionCount,
            AutoReconnect = AutoReconnect,
            HasPassword = HasPassword
        };
    }
}
