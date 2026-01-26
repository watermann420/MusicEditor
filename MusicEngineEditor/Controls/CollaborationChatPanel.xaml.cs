// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace MusicEngineEditor.Controls;

/// <summary>
/// In-app chat panel for real-time collaboration.
/// </summary>
public partial class CollaborationChatPanel : UserControl
{
    private readonly ObservableCollection<ChatMessage> _messages = new();
    private readonly ObservableCollection<ChatUser> _onlineUsers = new();
    private string _currentUserId = Guid.NewGuid().ToString();
    private string _currentUserName = "You";

    public event EventHandler<ChatMessage>? MessageSent;
    public event EventHandler<string>? FileShareRequested;

    public CollaborationChatPanel()
    {
        InitializeComponent();

        MessagesPanel.ItemsSource = _messages;
        OnlineUsersPanel.ItemsSource = _onlineUsers;

        LoadSampleData();
    }

    private void LoadSampleData()
    {
        // Add sample users
        _onlineUsers.Add(new ChatUser
        {
            Id = _currentUserId,
            DisplayName = _currentUserName,
            Color = "#4B6EAF",
            IsOnline = true
        });

        _onlineUsers.Add(new ChatUser
        {
            Id = "2",
            DisplayName = "Alice",
            Color = "#6AAB73",
            IsOnline = true
        });

        _onlineUsers.Add(new ChatUser
        {
            Id = "3",
            DisplayName = "Bob",
            Color = "#E8A73C",
            IsOnline = true
        });

        UpdateOnlineCount();

        // Add sample messages
        AddSystemMessage("Session started");

        AddMessage(new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "2",
            SenderName = "Alice",
            Content = "Hey, I just uploaded the new drum pattern!",
            Timestamp = DateTime.Now.AddMinutes(-5),
            IsOwnMessage = false,
            SenderColorHex = "#6AAB73"
        });

        AddMessage(new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = "3",
            SenderName = "Bob",
            Content = "Nice! Let me check it out. The bass line is also ready.",
            Timestamp = DateTime.Now.AddMinutes(-3),
            IsOwnMessage = false,
            SenderColorHex = "#E8A73C"
        });
    }

    #region Public Methods

    public void SetCurrentUser(string userId, string userName)
    {
        _currentUserId = userId;
        _currentUserName = userName;
    }

    public void AddMessage(ChatMessage message)
    {
        message.IsOwnMessage = message.SenderId == _currentUserId;
        _messages.Add(message);
        ScrollToBottom();
    }

    public void AddSystemMessage(string content)
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Content = content,
            Timestamp = DateTime.Now,
            IsSystemMessage = true
        };
        _messages.Add(message);
        ScrollToBottom();
    }

    public void AddUser(ChatUser user)
    {
        if (!_onlineUsers.Any(u => u.Id == user.Id))
        {
            _onlineUsers.Add(user);
            UpdateOnlineCount();
            AddSystemMessage($"{user.DisplayName} joined");
        }
    }

    public void RemoveUser(string userId)
    {
        var user = _onlineUsers.FirstOrDefault(u => u.Id == userId);
        if (user != null)
        {
            _onlineUsers.Remove(user);
            UpdateOnlineCount();
            AddSystemMessage($"{user.DisplayName} left");
        }
    }

    public void UpdateUserStatus(string userId, bool isOnline)
    {
        var user = _onlineUsers.FirstOrDefault(u => u.Id == userId);
        if (user != null)
        {
            user.IsOnline = isOnline;
        }
    }

    public void ClearMessages()
    {
        _messages.Clear();
    }

    #endregion

    #region Private Methods

    private void SendMessage()
    {
        var content = MessageTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(content)) return;

        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = _currentUserId,
            SenderName = _currentUserName,
            Content = content,
            Timestamp = DateTime.Now,
            IsOwnMessage = true,
            SenderColorHex = "#4B6EAF"
        };

        AddMessage(message);
        MessageSent?.Invoke(this, message);

        MessageTextBox.Text = string.Empty;
    }

    private void SendFileMessage(string filePath)
    {
        var fileName = System.IO.Path.GetFileName(filePath);

        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = _currentUserId,
            SenderName = _currentUserName,
            Content = $"Shared a file",
            Timestamp = DateTime.Now,
            IsOwnMessage = true,
            SenderColorHex = "#4B6EAF",
            HasAttachment = true,
            AttachmentName = fileName,
            AttachmentPath = filePath
        };

        AddMessage(message);
        FileShareRequested?.Invoke(this, filePath);
        MessageSent?.Invoke(this, message);
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            MessageScrollViewer.ScrollToEnd();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void UpdateOnlineCount()
    {
        var count = _onlineUsers.Count(u => u.IsOnline);
        OnlineCountText.Text = $"{count} online";
    }

    #endregion

    #region Event Handlers

    private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            SendMessage();
            e.Handled = true;
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.flac;*.ogg;*.aiff|Project Files|*.meproj|All Files|*.*",
            Title = "Select file to share"
        };

        if (dialog.ShowDialog() == true)
        {
            SendFileMessage(dialog.FileName);
        }
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var clearItem = new System.Windows.Controls.MenuItem { Header = "Clear Chat" };
        clearItem.Click += (s, args) => ClearMessages();

        var exportItem = new System.Windows.Controls.MenuItem { Header = "Export Chat" };
        exportItem.Click += (s, args) => ExportChat();

        menu.Items.Add(clearItem);
        menu.Items.Add(exportItem);

        menu.IsOpen = true;
    }

    private void ExportChat()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text File (*.txt)|*.txt",
            FileName = $"Chat_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            var lines = _messages.Select(m =>
            {
                if (m.IsSystemMessage)
                    return $"[{m.Timestamp:HH:mm}] --- {m.Content} ---";
                return $"[{m.Timestamp:HH:mm}] {m.SenderName}: {m.Content}";
            });

            System.IO.File.WriteAllLines(dialog.FileName, lines);
        }
    }

    #endregion
}

#region Models

/// <summary>
/// Represents a chat message.
/// </summary>
public class ChatMessage : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _senderId = string.Empty;
    private string _senderName = string.Empty;
    private string _content = string.Empty;
    private DateTime _timestamp;
    private bool _isOwnMessage;
    private bool _isSystemMessage;
    private string _senderColorHex = "#808080";
    private bool _hasAttachment;
    private string _attachmentName = string.Empty;
    private string _attachmentPath = string.Empty;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string SenderId { get => _senderId; set { _senderId = value; OnPropertyChanged(); } }
    public string SenderName { get => _senderName; set { _senderName = value; OnPropertyChanged(); } }
    public string Content { get => _content; set { _content = value; OnPropertyChanged(); } }
    public DateTime Timestamp { get => _timestamp; set { _timestamp = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimestampDisplay)); } }
    public bool IsOwnMessage { get => _isOwnMessage; set { _isOwnMessage = value; OnPropertyChanged(); UpdateDisplayProperties(); } }
    public bool IsSystemMessage { get => _isSystemMessage; set { _isSystemMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRegularMessage)); } }
    public string SenderColorHex { get => _senderColorHex; set { _senderColorHex = value; OnPropertyChanged(); OnPropertyChanged(nameof(SenderColor)); } }
    public bool HasAttachment { get => _hasAttachment; set { _hasAttachment = value; OnPropertyChanged(); } }
    public string AttachmentName { get => _attachmentName; set { _attachmentName = value; OnPropertyChanged(); } }
    public string AttachmentPath { get => _attachmentPath; set { _attachmentPath = value; OnPropertyChanged(); } }

    public bool IsRegularMessage => !IsSystemMessage;
    public bool ShowSenderName => !IsOwnMessage && !IsSystemMessage;
    public string TimestampDisplay => Timestamp.ToString("HH:mm");

    public SolidColorBrush SenderColor
    {
        get
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(SenderColorHex));
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
            }
        }
    }

    public SolidColorBrush BubbleBackground => IsOwnMessage
        ? new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF))
        : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37));

    public SolidColorBrush TextColor => IsOwnMessage
        ? Brushes.White
        : new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4));

    public SolidColorBrush TimestampColor => IsOwnMessage
        ? new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF))
        : new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));

    public CornerRadius BubbleCornerRadius => IsOwnMessage
        ? new CornerRadius(12, 12, 4, 12)
        : new CornerRadius(12, 12, 12, 4);

    public Thickness BubbleMargin => IsOwnMessage
        ? new Thickness(40, 4, 8, 4)
        : new Thickness(8, 4, 40, 4);

    public System.Windows.HorizontalAlignment BubbleAlignment => IsOwnMessage
        ? System.Windows.HorizontalAlignment.Right
        : System.Windows.HorizontalAlignment.Left;

    private void UpdateDisplayProperties()
    {
        OnPropertyChanged(nameof(ShowSenderName));
        OnPropertyChanged(nameof(BubbleBackground));
        OnPropertyChanged(nameof(TextColor));
        OnPropertyChanged(nameof(TimestampColor));
        OnPropertyChanged(nameof(BubbleCornerRadius));
        OnPropertyChanged(nameof(BubbleMargin));
        OnPropertyChanged(nameof(BubbleAlignment));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a user in the chat.
/// </summary>
public class ChatUser : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private string _color = "#808080";
    private bool _isOnline;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Initials)); } }
    public string Color { get => _color; set { _color = value; OnPropertyChanged(); OnPropertyChanged(nameof(UserColor)); } }
    public bool IsOnline { get => _isOnline; set { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); } }

    public string Initials => string.IsNullOrEmpty(DisplayName)
        ? "?"
        : string.Concat(DisplayName.Split(' ').Take(2).Select(s => s.Length > 0 ? s[0].ToString() : ""));

    public SolidColorBrush UserColor
    {
        get
        {
            try
            {
                return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(Color));
            }
            catch
            {
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80));
            }
        }
    }

    public SolidColorBrush StatusColor => IsOnline
        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xAB, 0x73))
        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60));

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion
