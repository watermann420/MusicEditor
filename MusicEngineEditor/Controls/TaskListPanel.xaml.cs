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

namespace MusicEngineEditor.Controls;

/// <summary>
/// Built-in to-do list panel for tracking project tasks.
/// </summary>
public partial class TaskListPanel : UserControl
{
    private readonly ObservableCollection<ProjectTask> _allTasks = new();
    private readonly ObservableCollection<ProjectTask> _filteredTasks = new();
    private readonly ObservableCollection<TaskCategory> _categories = new();
    private readonly HashSet<string> _selectedCategories = new();

    private string _statusFilter = "All";
    private string _sortBy = "Priority";

    public event EventHandler<ProjectTask>? TaskAdded;
    public event EventHandler<ProjectTask>? TaskCompleted;
    public event EventHandler<ProjectTask>? TaskDeleted;

    public TaskListPanel()
    {
        InitializeComponent();

        TasksItemsControl.ItemsSource = _filteredTasks;
        CategoryFilterPanel.ItemsSource = _categories;

        InitializeCategories();
        LoadSampleData();
        ApplyFilters();
    }

    private void InitializeCategories()
    {
        _categories.Add(new TaskCategory { Name = "Mix", Color = "#4B6EAF" });
        _categories.Add(new TaskCategory { Name = "Arrangement", Color = "#9C7CE8" });
        _categories.Add(new TaskCategory { Name = "Recording", Color = "#E85CAF" });
        _categories.Add(new TaskCategory { Name = "Mastering", Color = "#5CBFE8" });
        _categories.Add(new TaskCategory { Name = "General", Color = "#808080" });
    }

    private void LoadSampleData()
    {
        _allTasks.Add(new ProjectTask
        {
            Id = "1",
            Title = "EQ the vocals",
            Category = "Mix",
            Priority = TaskPriority.High,
            CreatedAt = DateTime.Now.AddDays(-2)
        });

        _allTasks.Add(new ProjectTask
        {
            Id = "2",
            Title = "Add automation to chorus",
            Category = "Mix",
            Priority = TaskPriority.Medium,
            DueDate = DateTime.Now.AddDays(1),
            CreatedAt = DateTime.Now.AddDays(-1)
        });

        _allTasks.Add(new ProjectTask
        {
            Id = "3",
            Title = "Record guitar solo",
            Category = "Recording",
            Priority = TaskPriority.High,
            DueDate = DateTime.Now.AddDays(3),
            CreatedAt = DateTime.Now
        });

        _allTasks.Add(new ProjectTask
        {
            Id = "4",
            Title = "Review mix notes",
            Category = "General",
            Priority = TaskPriority.Low,
            IsCompleted = true,
            CreatedAt = DateTime.Now.AddDays(-3)
        });
    }

    #region Public Methods

    public void AddTask(string title, string category, TaskPriority priority, DateTime? dueDate = null)
    {
        var task = new ProjectTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Category = category,
            Priority = priority,
            DueDate = dueDate,
            CreatedAt = DateTime.Now
        };

        _allTasks.Insert(0, task);
        ApplyFilters();
        TaskAdded?.Invoke(this, task);
    }

    public void RemoveTask(string taskId)
    {
        var task = _allTasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            _allTasks.Remove(task);
            ApplyFilters();
            TaskDeleted?.Invoke(this, task);
        }
    }

    public void CompleteTask(string taskId)
    {
        var task = _allTasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            task.IsCompleted = true;
            ApplyFilters();
            TaskCompleted?.Invoke(this, task);
        }
    }

    public IReadOnlyList<ProjectTask> GetAllTasks() => _allTasks.ToList().AsReadOnly();

    public IReadOnlyList<ProjectTask> GetActiveTasks() => _allTasks.Where(t => !t.IsCompleted).ToList().AsReadOnly();

    public IReadOnlyList<ProjectTask> GetCompletedTasks() => _allTasks.Where(t => t.IsCompleted).ToList().AsReadOnly();

    public void ClearCompletedTasks()
    {
        var completed = _allTasks.Where(t => t.IsCompleted).ToList();
        foreach (var task in completed)
        {
            _allTasks.Remove(task);
        }
        ApplyFilters();
    }

    #endregion

    #region Private Methods

    private void ApplyFilters()
    {
        _filteredTasks.Clear();

        var filtered = _allTasks.AsEnumerable();

        // Status filter
        switch (_statusFilter)
        {
            case "Active":
                filtered = filtered.Where(t => !t.IsCompleted);
                break;
            case "Done":
                filtered = filtered.Where(t => t.IsCompleted);
                break;
        }

        // Category filter
        if (_selectedCategories.Count > 0)
        {
            filtered = filtered.Where(t => _selectedCategories.Contains(t.Category));
        }

        // Sort
        filtered = _sortBy switch
        {
            "Priority" => filtered.OrderByDescending(t => t.Priority).ThenBy(t => t.DueDate ?? DateTime.MaxValue),
            "Due Date" => filtered.OrderBy(t => t.DueDate ?? DateTime.MaxValue).ThenByDescending(t => t.Priority),
            "Category" => filtered.OrderBy(t => t.Category).ThenByDescending(t => t.Priority),
            "Created" => filtered.OrderByDescending(t => t.CreatedAt),
            _ => filtered.OrderByDescending(t => t.Priority)
        };

        foreach (var task in filtered)
        {
            _filteredTasks.Add(task);
        }

        UpdateCounts();
        UpdateEmptyState();
    }

    private void UpdateCounts()
    {
        var completed = _allTasks.Count(t => t.IsCompleted);
        var total = _allTasks.Count;
        TaskCountText.Text = $" ({completed}/{total})";
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = _allTasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static ProjectTask? GetTaskFromMenuItem(object sender)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem &&
            menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement element)
        {
            return element.DataContext as ProjectTask;
        }
        return null;
    }

    #endregion

    #region Event Handlers

    private void TaskTitleTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddTaskFromInput();
        }
    }

    private void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        AddTaskFromInput();
    }

    private void AddTaskFromInput()
    {
        var title = TaskTitleTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(title)) return;

        var priority = (PriorityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Medium";
        var category = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "General";
        var dueDate = DueDatePicker.SelectedDate;

        AddTask(title, category, Enum.Parse<TaskPriority>(priority), dueDate);

        TaskTitleTextBox.Text = string.Empty;
        DueDatePicker.SelectedDate = null;
    }

    private void TaskCheckbox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkbox && checkbox.DataContext is ProjectTask task)
        {
            if (task.IsCompleted)
            {
                TaskCompleted?.Invoke(this, task);
            }
            ApplyFilters();
        }
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        FilterAll.IsChecked = sender == FilterAll;
        FilterActive.IsChecked = sender == FilterActive;
        FilterDone.IsChecked = sender == FilterDone;

        _statusFilter = sender == FilterActive ? "Active" : (sender == FilterDone ? "Done" : "All");
        ApplyFilters();
    }

    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.DataContext is TaskCategory cat)
        {
            if (cat.IsSelected)
                _selectedCategories.Add(cat.Name);
            else
                _selectedCategories.Remove(cat.Name);

            ApplyFilters();
        }
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.SelectedItem is ComboBoxItem item)
        {
            _sortBy = item.Content?.ToString() ?? "Priority";
            ApplyFilters();
        }
    }

    private void ClearDoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Remove all completed tasks?", "Clear Completed", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ClearCompletedTasks();
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        ProjectTask? task = null;

        if (sender is Button btn && btn.Tag is ProjectTask t)
        {
            task = t;
        }
        else
        {
            task = GetTaskFromMenuItem(sender);
        }

        if (task != null)
        {
            _allTasks.Remove(task);
            ApplyFilters();
            TaskDeleted?.Invoke(this, task);
        }
    }

    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        var task = GetTaskFromMenuItem(sender);
        if (task != null)
        {
            var newTitle = Microsoft.VisualBasic.Interaction.InputBox(
                "Edit task title:",
                "Edit Task",
                task.Title);

            if (!string.IsNullOrWhiteSpace(newTitle))
            {
                task.Title = newTitle;
            }
        }
    }

    private void SetPriority_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Tag is string priorityStr)
        {
            var task = GetTaskFromMenuItem(menuItem);
            if (task != null && Enum.TryParse<TaskPriority>(priorityStr, out var priority))
            {
                task.Priority = priority;
                ApplyFilters();
            }
        }
    }

    private void SetCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Tag is string category)
        {
            var task = GetTaskFromMenuItem(menuItem);
            if (task != null)
            {
                task.Category = category;
                ApplyFilters();
            }
        }
    }

    #endregion
}

#region Models

public enum TaskPriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>
/// Represents a project task.
/// </summary>
public class ProjectTask : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _title = string.Empty;
    private string _category = "General";
    private TaskPriority _priority = TaskPriority.Medium;
    private DateTime? _dueDate;
    private DateTime _createdAt;
    private bool _isCompleted;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
    public string Category { get => _category; set { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(CategoryBrush)); } }
    public TaskPriority Priority { get => _priority; set { _priority = value; OnPropertyChanged(); OnPropertyChanged(nameof(PriorityBrush)); } }
    public DateTime? DueDate { get => _dueDate; set { _dueDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDueDate)); OnPropertyChanged(nameof(DueDateDisplay)); OnPropertyChanged(nameof(DueDateForeground)); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
    public bool IsCompleted { get => _isCompleted; set { _isCompleted = value; OnPropertyChanged(); OnPropertyChanged(nameof(TitleForeground)); OnPropertyChanged(nameof(TitleDecoration)); } }

    public bool HasDueDate => DueDate.HasValue;

    public string DueDateDisplay
    {
        get
        {
            if (!DueDate.HasValue) return "";
            var date = DueDate.Value.Date;
            if (date == DateTime.Today) return "Today";
            if (date == DateTime.Today.AddDays(1)) return "Tomorrow";
            if (date < DateTime.Today) return $"Overdue ({date:MMM d})";
            return date.ToString("MMM d");
        }
    }

    public SolidColorBrush PriorityBrush => Priority switch
    {
        TaskPriority.High => new SolidColorBrush(Color.FromRgb(0xE8, 0x5C, 0x5C)),
        TaskPriority.Medium => new SolidColorBrush(Color.FromRgb(0xE8, 0xA7, 0x3C)),
        TaskPriority.Low => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),
        _ => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
    };

    public SolidColorBrush CategoryBrush => Category switch
    {
        "Mix" => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
        "Arrangement" => new SolidColorBrush(Color.FromRgb(0x9C, 0x7C, 0xE8)),
        "Recording" => new SolidColorBrush(Color.FromRgb(0xE8, 0x5C, 0xAF)),
        "Mastering" => new SolidColorBrush(Color.FromRgb(0x5C, 0xBF, 0xE8)),
        _ => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
    };

    public SolidColorBrush TitleForeground => IsCompleted
        ? new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))
        : new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

    public TextDecorationCollection? TitleDecoration => IsCompleted
        ? TextDecorations.Strikethrough
        : null;

    public SolidColorBrush DueDateForeground
    {
        get
        {
            if (!DueDate.HasValue) return new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
            if (DueDate.Value.Date < DateTime.Today) return new SolidColorBrush(Color.FromRgb(0xE8, 0x5C, 0x5C));
            if (DueDate.Value.Date == DateTime.Today) return new SolidColorBrush(Color.FromRgb(0xE8, 0xA7, 0x3C));
            return new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a task category for filtering.
/// </summary>
public class TaskCategory : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _color = "#808080";
    private bool _isSelected;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Color { get => _color; set { _color = value; OnPropertyChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion
