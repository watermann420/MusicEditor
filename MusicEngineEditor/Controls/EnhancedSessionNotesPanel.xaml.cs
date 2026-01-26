// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Enhanced panel for managing timestamped production notes with rich formatting and tagging.
/// </summary>
public partial class EnhancedSessionNotesPanel : UserControl
{
    private readonly ObservableCollection<EnhancedSessionNote> _allNotes = new();
    private readonly ObservableCollection<EnhancedSessionNote> _filteredNotes = new();
    private readonly ObservableCollection<NoteTag> _allTags = new();
    private readonly HashSet<string> _selectedTags = new();

    private string _searchText = string.Empty;
    private string _categoryFilter = string.Empty;

    #region Dependency Properties

    public static readonly DependencyProperty CurrentPositionProperty =
        DependencyProperty.Register(nameof(CurrentPosition), typeof(TimeSpan), typeof(EnhancedSessionNotesPanel),
            new PropertyMetadata(TimeSpan.Zero));

    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(double), typeof(EnhancedSessionNotesPanel),
            new PropertyMetadata(120.0));

    public TimeSpan CurrentPosition
    {
        get => (TimeSpan)GetValue(CurrentPositionProperty);
        set => SetValue(CurrentPositionProperty, value);
    }

    public double Bpm
    {
        get => (double)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<EnhancedSessionNote>? NoteAdded;
    public event EventHandler<EnhancedSessionNote>? NoteDeleted;
    public event EventHandler<EnhancedSessionNote>? NoteEdited;
    public event EventHandler<TimeSpan>? JumpToPositionRequested;

    #endregion

    public EnhancedSessionNotesPanel()
    {
        InitializeComponent();

        NotesItemsControl.ItemsSource = _filteredNotes;
        TagFilterPanel.ItemsSource = _allTags;

        InitializeDefaultTags();
        UpdateEmptyState();
    }

    private void InitializeDefaultTags()
    {
        var defaultTags = new[] { "Vocals", "Drums", "Bass", "Mix", "Master", "Arrangement", "Automation", "FX" };
        foreach (var tag in defaultTags)
        {
            _allTags.Add(new NoteTag { Name = tag });
        }
    }

    #region Public Methods

    public void AddNote(string title, string content, string category, IEnumerable<string>? tags = null)
    {
        var note = new EnhancedSessionNote
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Content = content,
            Category = category,
            Timestamp = CurrentPosition,
            Tags = tags?.ToList() ?? new List<string>(),
            CreatedAt = DateTime.Now
        };

        _allNotes.Insert(0, note);

        // Add any new tags to the tag list
        foreach (var tag in note.Tags)
        {
            if (!_allTags.Any(t => t.Name == tag))
            {
                _allTags.Add(new NoteTag { Name = tag });
            }
        }

        ApplyFilters();
        NoteAdded?.Invoke(this, note);
        UpdateNoteCount();
    }

    public void AddNoteAtPosition(TimeSpan position, string content, string category = "General")
    {
        var note = new EnhancedSessionNote
        {
            Id = Guid.NewGuid().ToString(),
            Content = content,
            Category = category,
            Timestamp = position,
            CreatedAt = DateTime.Now
        };

        _allNotes.Insert(0, note);
        ApplyFilters();
        NoteAdded?.Invoke(this, note);
        UpdateNoteCount();
    }

    public IReadOnlyList<EnhancedSessionNote> GetAllNotes() => _allNotes.ToList().AsReadOnly();

    public IReadOnlyList<EnhancedSessionNote> GetNotesByCategory(string category) =>
        _allNotes.Where(n => n.Category == category).ToList().AsReadOnly();

    public IReadOnlyList<EnhancedSessionNote> GetNotesInRange(TimeSpan start, TimeSpan end) =>
        _allNotes.Where(n => n.Timestamp >= start && n.Timestamp <= end).ToList().AsReadOnly();

    public void ClearAllNotes()
    {
        _allNotes.Clear();
        ApplyFilters();
        UpdateNoteCount();
    }

    #endregion

    #region Private Methods

    private void ApplyFilters()
    {
        _filteredNotes.Clear();

        var filtered = _allNotes.AsEnumerable();

        // Category filter
        if (!string.IsNullOrEmpty(_categoryFilter))
        {
            filtered = filtered.Where(n => n.Category == _categoryFilter);
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var search = _searchText.ToLowerInvariant();
            filtered = filtered.Where(n =>
                (n.Title?.ToLowerInvariant().Contains(search) ?? false) ||
                n.Content.ToLowerInvariant().Contains(search) ||
                n.Tags.Any(t => t.ToLowerInvariant().Contains(search)));
        }

        // Tag filter
        if (_selectedTags.Count > 0)
        {
            filtered = filtered.Where(n => n.Tags.Any(t => _selectedTags.Contains(t)));
        }

        // Sort by creation date (most recent first)
        foreach (var note in filtered.OrderByDescending(n => n.CreatedAt))
        {
            _filteredNotes.Add(note);
        }

        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = _allNotes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateNoteCount()
    {
        NoteCountText.Text = $" ({_allNotes.Count})";
    }

    private static List<string> ParseTags(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();

        return input
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();
    }

    private void ExportNotes(string filePath)
    {
        var sb = new StringBuilder();
        var isMarkdown = filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

        if (isMarkdown)
        {
            sb.AppendLine("# Session Notes");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Session Notes");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('-', 50));
            sb.AppendLine();
        }

        foreach (var categoryGroup in _allNotes.OrderBy(n => n.Timestamp).GroupBy(n => n.Category))
        {
            if (isMarkdown)
            {
                sb.AppendLine($"## {categoryGroup.Key}");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"=== {categoryGroup.Key} ===");
                sb.AppendLine();
            }

            foreach (var note in categoryGroup)
            {
                var timeStr = FormatTimeSpan(note.Timestamp);

                if (isMarkdown)
                {
                    sb.AppendLine($"### [{timeStr}] {note.Title}");
                    sb.AppendLine(note.Content);
                    if (note.Tags.Count > 0)
                    {
                        sb.AppendLine($"*Tags: {string.Join(", ", note.Tags)}*");
                    }
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"[{timeStr}] {note.Title}");
                    sb.AppendLine(note.Content);
                    if (note.Tags.Count > 0)
                    {
                        sb.AppendLine($"Tags: {string.Join(", ", note.Tags)}");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds / 100:D1}";
    }

    private static EnhancedSessionNote? GetNoteFromMenuItem(object sender)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem &&
            menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement element)
        {
            return element.DataContext as EnhancedSessionNote;
        }
        return null;
    }

    #endregion

    #region Event Handlers

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text ?? string.Empty;
        ApplyFilters();
    }

    private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryFilterCombo.SelectedItem is ComboBoxItem item)
        {
            _categoryFilter = item.Tag?.ToString() ?? string.Empty;
            ApplyFilters();
        }
    }

    private void TagFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.DataContext is NoteTag tag)
        {
            if (tag.IsSelected)
                _selectedTags.Add(tag.Name);
            else
                _selectedTags.Remove(tag.Name);

            ApplyFilters();
        }
    }

    private void NoteItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is EnhancedSessionNote note)
        {
            JumpToPositionRequested?.Invoke(this, note.Timestamp);
            e.Handled = true;
        }
    }

    private void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text?.Trim() ?? string.Empty;
        var content = ContentTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(content)) return;

        var category = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "General";
        var tags = ParseTags(TagsTextBox.Text);

        AddNote(title, content, category, tags);

        TitleTextBox.Text = string.Empty;
        ContentTextBox.Text = string.Empty;
        TagsTextBox.Text = string.Empty;
    }

    private void JumpToPositionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            JumpToPositionRequested?.Invoke(this, note.Timestamp);
        }
    }

    private void EditNoteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            var newContent = Microsoft.VisualBasic.Interaction.InputBox(
                "Edit note content:",
                "Edit Note",
                note.Content);

            if (!string.IsNullOrEmpty(newContent))
            {
                note.Content = newContent;
                note.ModifiedAt = DateTime.Now;
                NoteEdited?.Invoke(this, note);
            }
        }
    }

    private void DeleteNoteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            if (MessageBox.Show("Delete this note?", "Delete Note", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _allNotes.Remove(note);
                ApplyFilters();
                NoteDeleted?.Invoke(this, note);
                UpdateNoteCount();
            }
        }
    }

    private void CopyContentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            Clipboard.SetText(note.Content);
        }
    }

    private void AddTagMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            var newTag = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter tag name:",
                "Add Tag",
                string.Empty);

            if (!string.IsNullOrWhiteSpace(newTag) && !note.Tags.Contains(newTag))
            {
                note.Tags.Add(newTag);

                if (!_allTags.Any(t => t.Name == newTag))
                {
                    _allTags.Add(new NoteTag { Name = newTag });
                }
            }
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown (*.md)|*.md|Text File (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = $"SessionNotes_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            ExportNotes(dialog.FileName);
            MessageBox.Show($"Notes exported to:\n{dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to clear all notes?", "Clear Notes", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ClearAllNotes();
        }
    }

    #endregion
}

#region Models

/// <summary>
/// Represents a timestamped session note with enhanced features.
/// </summary>
public class EnhancedSessionNote : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _title = string.Empty;
    private string _content = string.Empty;
    private string _category = "General";
    private TimeSpan _timestamp;
    private List<string> _tags = new();
    private DateTime _createdAt;
    private DateTime? _modifiedAt;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTitle)); } }
    public string Content { get => _content; set { _content = value; OnPropertyChanged(); } }
    public string Category { get => _category; set { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(CategoryBrush)); } }
    public TimeSpan Timestamp { get => _timestamp; set { _timestamp = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimestampDisplay)); } }
    public List<string> Tags { get => _tags; set { _tags = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTags)); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(DateDisplay)); } }
    public DateTime? ModifiedAt { get => _modifiedAt; set { _modifiedAt = value; OnPropertyChanged(); } }

    public bool HasTitle => !string.IsNullOrEmpty(Title);
    public bool HasTags => Tags.Count > 0;

    public string TimestampDisplay
    {
        get
        {
            if (Timestamp.TotalHours >= 1)
            {
                return $"{(int)Timestamp.TotalHours}:{Timestamp.Minutes:D2}:{Timestamp.Seconds:D2}";
            }
            return $"{(int)Timestamp.TotalMinutes}:{Timestamp.Seconds:D2}.{Timestamp.Milliseconds / 100:D1}";
        }
    }

    public string DateDisplay => CreatedAt.ToString("MMM d, HH:mm");

    public SolidColorBrush CategoryBrush => Category switch
    {
        "Idea" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        "Todo" => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
        "Issue" => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        "Mix" => new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
        "Arrangement" => new SolidColorBrush(Color.FromRgb(0x9C, 0x27, 0xB0)),
        _ => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E))
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a tag used for filtering notes.
/// </summary>
public class NoteTag : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isSelected;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion
