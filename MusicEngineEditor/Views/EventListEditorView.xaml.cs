// MusicEngineEditor - Event List Editor View
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Views;

/// <summary>
/// Converter to display MIDI channel as 1-based.
/// </summary>
public class ChannelDisplayConverter : IValueConverter
{
    public static readonly ChannelDisplayConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int channel)
        {
            return (channel + 1).ToString();
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && int.TryParse(str, out int channel))
        {
            return Math.Clamp(channel - 1, 0, 15);
        }
        return 0;
    }
}

/// <summary>
/// Spreadsheet-style MIDI event editor with filtering and sorting.
/// </summary>
public partial class EventListEditorView : UserControl
{
    private readonly CollectionViewSource _eventsViewSource;
    private string _typeFilter = "All";
    private int _channelFilter = -1;
    private string _searchText = string.Empty;
    private ListSortDirection _lastSortDirection = ListSortDirection.Ascending;
    private string _lastSortColumn = "Position";

    /// <summary>
    /// Gets the collection of all MIDI events.
    /// </summary>
    public ObservableCollection<MidiEventViewModel> Events { get; } = new();

    /// <summary>
    /// Gets the filtered view of events.
    /// </summary>
    public ICollectionView FilteredEvents => _eventsViewSource.View;

    /// <summary>
    /// Event raised when an event is added.
    /// </summary>
    public event EventHandler<MidiEventViewModel>? EventAdded;

    /// <summary>
    /// Event raised when an event is removed.
    /// </summary>
    public event EventHandler<MidiEventViewModel>? EventRemoved;

    /// <summary>
    /// Event raised when an event is modified.
    /// </summary>
    public event EventHandler<MidiEventViewModel>? EventModified;

    public EventListEditorView()
    {
        _eventsViewSource = new CollectionViewSource { Source = Events };
        _eventsViewSource.Filter += EventsViewSource_Filter;

        InitializeComponent();

        DataContext = this;

        Events.CollectionChanged += (_, _) => UpdateStatusBar();
        Loaded += (_, _) => UpdateStatusBar();
    }

    private void EventsViewSource_Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is not MidiEventViewModel evt)
        {
            e.Accepted = false;
            return;
        }

        // Type filter
        if (_typeFilter != "All")
        {
            if (!Enum.TryParse<MidiEventType>(_typeFilter, out var type) || evt.EventType != type)
            {
                e.Accepted = false;
                return;
            }
        }

        // Channel filter
        if (_channelFilter >= 0 && evt.Channel != _channelFilter)
        {
            e.Accepted = false;
            return;
        }

        // Search filter
        if (!string.IsNullOrEmpty(_searchText))
        {
            var searchLower = _searchText.ToLower();
            var matchesSearch =
                evt.NoteOrCCDisplay.ToLower().Contains(searchLower) ||
                evt.EventType.ToString().ToLower().Contains(searchLower) ||
                evt.Position.ToString(CultureInfo.InvariantCulture).Contains(searchLower);

            if (!matchesSearch)
            {
                e.Accepted = false;
                return;
            }
        }

        e.Accepted = true;
    }

    private void RefreshFilter()
    {
        _eventsViewSource.View.Refresh();
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        var totalCount = Events.Count;
        var filteredCount = FilteredEvents.Cast<MidiEventViewModel>().Count();
        var selectedCount = EventsDataGrid.SelectedItems.Count;

        EventCountText.Text = totalCount == filteredCount
            ? $"{totalCount} events"
            : $"{filteredCount} of {totalCount} events";

        SelectionInfoText.Text = $"{selectedCount} selected";

        var filters = new System.Collections.Generic.List<string>();
        if (_typeFilter != "All") filters.Add(_typeFilter);
        if (_channelFilter >= 0) filters.Add($"Ch {_channelFilter + 1}");
        if (!string.IsNullOrEmpty(_searchText)) filters.Add($"Search: {_searchText}");

        FilterStatusText.Text = filters.Count > 0 ? string.Join(" | ", filters) : "";
    }

    /// <summary>
    /// Adds a new MIDI event to the list.
    /// </summary>
    public void AddEvent(MidiEventViewModel evt)
    {
        Events.Add(evt);
        EventAdded?.Invoke(this, evt);
    }

    /// <summary>
    /// Removes an event from the list.
    /// </summary>
    public void RemoveEvent(MidiEventViewModel evt)
    {
        Events.Remove(evt);
        EventRemoved?.Invoke(this, evt);
    }

    /// <summary>
    /// Clears all events.
    /// </summary>
    public void ClearAllEvents()
    {
        Events.Clear();
    }

    /// <summary>
    /// Gets all selected events.
    /// </summary>
    public MidiEventViewModel[] GetSelectedEvents()
    {
        return EventsDataGrid.SelectedItems.Cast<MidiEventViewModel>().ToArray();
    }

    #region Event Handlers

    private void TypeFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeFilterCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _typeFilter = tag;
            RefreshFilter();
        }
    }

    private void ChannelFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelFilterCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _channelFilter = int.TryParse(tag, out var ch) ? ch : -1;
            RefreshFilter();
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchTextBox.Text;
        RefreshFilter();
    }

    private void AddEvent_Click(object sender, RoutedEventArgs e)
    {
        // Create new note event at position 0
        var evt = MidiEventViewModel.CreateNoteOn(0, 60, 100, 1.0);
        AddEvent(evt);

        // Select the new event
        EventsDataGrid.SelectedItem = evt;
        EventsDataGrid.ScrollIntoView(evt);
    }

    private void DuplicateEvent_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedEvents();
        foreach (var evt in selected)
        {
            var duplicate = evt.Clone();
            duplicate.Position += 1.0; // Offset position
            AddEvent(duplicate);
        }
    }

    private void DeleteEvent_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedEvents().ToList();
        if (selected.Count == 0) return;

        var result = MessageBox.Show(
            $"Delete {selected.Count} event(s)?",
            "Delete Events",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            foreach (var evt in selected)
            {
                RemoveEvent(evt);
            }
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        EventsDataGrid.SelectAll();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        EventsDataGrid.UnselectAll();
    }

    private void EventsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (MidiEventViewModel evt in e.RemovedItems)
        {
            evt.IsSelected = false;
        }
        foreach (MidiEventViewModel evt in e.AddedItems)
        {
            evt.IsSelected = true;
        }
        UpdateStatusBar();
    }

    private void EventsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is MidiEventViewModel evt)
        {
            EventModified?.Invoke(this, evt);
        }
    }

    private void EventsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        var column = e.Column.SortMemberPath ?? e.Column.Header?.ToString() ?? "Position";
        var direction = _lastSortColumn == column && _lastSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        _lastSortColumn = column;
        _lastSortDirection = direction;

        e.Column.SortDirection = direction;

        var view = CollectionViewSource.GetDefaultView(EventsDataGrid.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(column, direction));
    }

    private void EditEvent_Click(object sender, RoutedEventArgs e)
    {
        if (EventsDataGrid.SelectedItem is MidiEventViewModel evt)
        {
            ShowEditDialog(evt);
        }
    }

    private void MuteSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var evt in GetSelectedEvents())
        {
            evt.IsMuted = true;
        }
    }

    private void UnmuteSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var evt in GetSelectedEvents())
        {
            evt.IsMuted = false;
        }
    }

    private void ShowEditDialog(MidiEventViewModel evt)
    {
        // Simple edit dialog - in production, use a proper dialog
        var dialog = new Window
        {
            Title = "Edit Event",
            Width = 350,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x2B, 0x2D, 0x30))
        };

        var stack = new StackPanel { Margin = new Thickness(16) };

        // Position
        stack.Children.Add(CreateLabeledTextBox("Position:", evt.Position.ToString("F3"), out var posBox));

        // Channel
        stack.Children.Add(CreateLabeledTextBox("Channel (1-16):", (evt.Channel + 1).ToString(), out var chBox));

        // Note/CC
        stack.Children.Add(CreateLabeledTextBox("Note/CC (0-127):", evt.NoteOrCC.ToString(), out var noteBox));

        // Velocity/Value
        stack.Children.Add(CreateLabeledTextBox("Velocity/Value (0-127):", evt.VelocityOrValue.ToString(), out var velBox));

        // Length
        stack.Children.Add(CreateLabeledTextBox("Length (beats):", evt.Length.ToString("F3"), out var lenBox));

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var okButton = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 70 };

        okButton.Click += (_, _) =>
        {
            if (double.TryParse(posBox.Text, out var pos))
                evt.Position = pos;
            if (int.TryParse(chBox.Text, out var ch))
                evt.Channel = Math.Clamp(ch - 1, 0, 15);
            if (int.TryParse(noteBox.Text, out var note))
                evt.NoteOrCC = Math.Clamp(note, 0, 127);
            if (int.TryParse(velBox.Text, out var vel))
                evt.VelocityOrValue = Math.Clamp(vel, 0, 127);
            if (double.TryParse(lenBox.Text, out var len))
                evt.Length = len;

            EventModified?.Invoke(this, evt);
            dialog.DialogResult = true;
        };
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        stack.Children.Add(buttonPanel);
        dialog.Content = stack;

        dialog.ShowDialog();
    }

    private static StackPanel CreateLabeledTextBox(string label, string value, out TextBox textBox)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 4)
        });
        textBox = new TextBox
        {
            Text = value,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x1E, 0x1F, 0x22)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x43, 0x45, 0x4A)),
            Padding = new Thickness(8, 4, 8, 4)
        };
        stack.Children.Add(textBox);
        return stack;
    }

    #endregion
}
