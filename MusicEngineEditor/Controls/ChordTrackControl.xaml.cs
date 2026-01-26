// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A control for displaying and editing chord progressions on a timeline.
/// Shows chord symbols (Cmaj7, Am, G/B, etc.) with drag-to-resize functionality.
/// </summary>
public partial class ChordTrackControl : UserControl
{
    #region Constants

    private const double DefaultBeatWidth = 40.0;
    private const int BeatsPerBar = 4;
    private const double MinChordDuration = 0.5;

    #endregion

    #region Private Fields

    private readonly Dictionary<Guid, Border> _chordElements = [];
    private ChordMarker? _selectedChord;
    private ChordMarker? _draggedChord;
    private ChordMarker? _resizingChord;
    private Point _dragStartPoint;
    private double _dragStartPosition;
    private double _dragStartDuration;
    private bool _isDragging;
    private bool _isResizing;
    private double _contextMenuPosition;

    // Colors for chord qualities
    private static readonly Color MajorColor = Color.FromRgb(0x4C, 0xAF, 0x50);
    private static readonly Color MinorColor = Color.FromRgb(0x21, 0x96, 0xF3);
    private static readonly Color DominantColor = Color.FromRgb(0xFF, 0x98, 0x00);
    private static readonly Color DiminishedColor = Color.FromRgb(0x9C, 0x27, 0xB0);
    private static readonly Color AugmentedColor = Color.FromRgb(0xE9, 0x1E, 0x63);
    private static readonly Color SuspendedColor = Color.FromRgb(0x00, 0xBC, 0xD4);
    private static readonly Color DefaultColor = Color.FromRgb(0x60, 0x7D, 0x8B);

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ChordsProperty =
        DependencyProperty.Register(nameof(Chords), typeof(ObservableCollection<ChordMarker>), typeof(ChordTrackControl),
            new PropertyMetadata(null, OnChordsChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(ChordTrackControl),
            new PropertyMetadata(DefaultBeatWidth, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TotalBeatsProperty =
        DependencyProperty.Register(nameof(TotalBeats), typeof(double), typeof(ChordTrackControl),
            new PropertyMetadata(32.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(ChordTrackControl),
            new PropertyMetadata(0.0, OnScrollChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(ChordTrackControl),
            new PropertyMetadata(0.0, OnPlayheadChanged));

    public static readonly DependencyProperty ZoomXProperty =
        DependencyProperty.Register(nameof(ZoomX), typeof(double), typeof(ChordTrackControl),
            new PropertyMetadata(1.0, OnLayoutPropertyChanged));

    /// <summary>
    /// Gets or sets the collection of chord markers.
    /// </summary>
    public ObservableCollection<ChordMarker>? Chords
    {
        get => (ObservableCollection<ChordMarker>?)GetValue(ChordsProperty);
        set => SetValue(ChordsProperty, value);
    }

    /// <summary>
    /// Gets or sets the pixels per beat (horizontal zoom).
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the total number of beats in the track.
    /// </summary>
    public double TotalBeats
    {
        get => (double)GetValue(TotalBeatsProperty);
        set => SetValue(TotalBeatsProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal scroll offset.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the playhead position in beats.
    /// </summary>
    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal zoom factor.
    /// </summary>
    public double ZoomX
    {
        get => (double)GetValue(ZoomXProperty);
        set => SetValue(ZoomXProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a chord is added.
    /// </summary>
    public event EventHandler<ChordMarker>? ChordAdded;

    /// <summary>
    /// Occurs when a chord is removed.
    /// </summary>
    public event EventHandler<ChordMarker>? ChordRemoved;

    /// <summary>
    /// Occurs when a chord is changed.
    /// </summary>
    public event EventHandler<ChordMarker>? ChordChanged;

    /// <summary>
    /// Occurs when a chord is selected.
    /// </summary>
    public event EventHandler<ChordMarker?>? ChordSelected;

    /// <summary>
    /// Occurs when chord detection is requested.
    /// </summary>
    public event EventHandler? DetectChordsRequested;

    #endregion

    #region Constructor

    public ChordTrackControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeFromCollectionChanges();
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnChordsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordTrackControl control)
        {
            control.OnChordsCollectionChanged(
                e.OldValue as ObservableCollection<ChordMarker>,
                e.NewValue as ObservableCollection<ChordMarker>);
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordTrackControl control)
        {
            control.RenderAll();
        }
    }

    private static void OnScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordTrackControl control)
        {
            control.ApplyScrollTransform();
        }
    }

    private static void OnPlayheadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordTrackControl control)
        {
            control.UpdatePlayhead();
        }
    }

    private void OnChordsCollectionChanged(
        ObservableCollection<ChordMarker>? oldCollection,
        ObservableCollection<ChordMarker>? newCollection)
    {
        if (oldCollection != null)
        {
            oldCollection.CollectionChanged -= OnChordsItemsChanged;
        }

        if (newCollection != null)
        {
            newCollection.CollectionChanged += OnChordsItemsChanged;
        }

        RenderChords();
        UpdateChordCount();
    }

    private void OnChordsItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderChords();
        UpdateChordCount();
    }

    private void UnsubscribeFromCollectionChanges()
    {
        if (Chords != null)
        {
            Chords.CollectionChanged -= OnChordsItemsChanged;
        }
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        if (!IsLoaded) return;

        RenderGrid();
        RenderChords();
        UpdatePlayhead();
        ApplyScrollTransform();
    }

    private void RenderGrid()
    {
        GridCanvas.Children.Clear();

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var totalWidth = TotalBeats * effectiveBeatWidth;
        var height = ChordsCanvas.ActualHeight > 0 ? ChordsCanvas.ActualHeight : ActualHeight - 30;

        GridCanvas.Width = totalWidth;
        GridCanvas.Height = height;

        // Draw beat and bar lines
        int totalBeatsInt = (int)Math.Ceiling(TotalBeats);
        for (int beat = 0; beat <= totalBeatsInt; beat++)
        {
            var x = beat * effectiveBeatWidth;
            var isBarLine = beat % BeatsPerBar == 0;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = new SolidColorBrush(isBarLine ? Color.FromRgb(0x3A, 0x3A, 0x3A) : Color.FromRgb(0x2A, 0x2A, 0x2A)),
                StrokeThickness = isBarLine ? 1.5 : 0.5
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void RenderChords()
    {
        ChordsCanvas.Children.Clear();
        _chordElements.Clear();

        if (Chords == null) return;

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = ChordsCanvas.ActualHeight > 0 ? ChordsCanvas.ActualHeight : ActualHeight - 30;

        ChordsCanvas.Width = TotalBeats * effectiveBeatWidth;
        ChordsCanvas.Height = height;

        foreach (var chord in Chords.OrderBy(c => c.Position))
        {
            var element = CreateChordElement(chord, effectiveBeatWidth, height);
            _chordElements[chord.Id] = element;
            ChordsCanvas.Children.Add(element);
        }
    }

    private Border CreateChordElement(ChordMarker chord, double effectiveBeatWidth, double height)
    {
        var x = chord.Position * effectiveBeatWidth;
        var width = chord.Duration * effectiveBeatWidth;
        var color = GetChordColor(chord);

        var border = new Border
        {
            Width = Math.Max(width - 2, 20),
            Height = height - 8,
            Background = new SolidColorBrush(color),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
            BorderThickness = chord.IsSelected ? new Thickness(2) : new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand,
            Tag = chord,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 3,
                ShadowDepth = 1,
                Opacity = 0.3
            }
        };

        // Content: Chord name
        var grid = new Grid();
        grid.Children.Add(new TextBlock
        {
            Text = chord.ChordName,
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        // Resize handle
        var resizeHandle = new Border
        {
            Width = 6,
            Height = height - 16,
            Background = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0),
            Cursor = Cursors.SizeWE,
            Tag = "ResizeHandle"
        };

        grid.Children.Add(resizeHandle);
        border.Child = grid;

        Canvas.SetLeft(border, x + 1);
        Canvas.SetTop(border, 4);

        // Event handlers
        border.MouseLeftButtonDown += ChordElement_MouseLeftButtonDown;
        border.MouseEnter += ChordElement_MouseEnter;
        border.MouseLeave += ChordElement_MouseLeave;

        return border;
    }

    private void UpdatePlayhead()
    {
        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = ChordsCanvas.ActualHeight > 0 ? ChordsCanvas.ActualHeight : ActualHeight - 30;

        var x = PlayheadPosition * effectiveBeatWidth - ScrollOffset;

        if (x >= 0 && x <= ActualWidth)
        {
            Playhead.Visibility = Visibility.Visible;
            Playhead.Height = height;
            Canvas.SetLeft(Playhead, x);
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyScrollTransform()
    {
        var transform = new TranslateTransform(-ScrollOffset, 0);
        GridCanvas.RenderTransform = transform;
        ChordsCanvas.RenderTransform = transform;
    }

    private void UpdateChordCount()
    {
        ChordCountText.Text = $" ({Chords?.Count ?? 0})";
    }

    private static Color GetChordColor(ChordMarker chord)
    {
        if (!string.IsNullOrEmpty(chord.Color))
        {
            try
            {
                return (Color)System.Windows.Media.ColorConverter.ConvertFromString(chord.Color);
            }
            catch { /* Fall through to quality-based color */ }
        }

        // Determine color by chord quality
        var name = chord.ChordName.ToLowerInvariant();
        if (name.Contains("dim") || name.Contains("o"))
            return DiminishedColor;
        if (name.Contains("aug") || name.Contains("+"))
            return AugmentedColor;
        if (name.Contains("sus"))
            return SuspendedColor;
        if (name.Contains("7") || name.Contains("9") || name.Contains("11") || name.Contains("13"))
            return DominantColor;
        if (name.Contains("m") && !name.Contains("maj"))
            return MinorColor;
        if (name.Contains("maj") || (!name.Contains("m") && !name.Contains("dim") && !name.Contains("aug")))
            return MajorColor;

        return DefaultColor;
    }

    #endregion

    #region Mouse Event Handlers

    private void ChordElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ChordMarker chord)
        {
            var position = e.GetPosition(border);

            // Check if clicking on resize handle
            if (position.X > border.Width - 10)
            {
                _resizingChord = chord;
                _dragStartDuration = chord.Duration;
                _dragStartPoint = e.GetPosition(ChordsCanvas);
                _isResizing = true;
            }
            else
            {
                _draggedChord = chord;
                _dragStartPosition = chord.Position;
                _dragStartPoint = e.GetPosition(ChordsCanvas);
                _isDragging = false;
            }

            SelectChord(chord);
            border.CaptureMouse();
            e.Handled = true;
        }
    }

    private void ChordElement_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border && border.Tag is ChordMarker chord)
        {
            ShowChordTooltip(chord, border);
        }
    }

    private void ChordElement_MouseLeave(object sender, MouseEventArgs e)
    {
        HoverTooltip.Visibility = Visibility.Collapsed;
    }

    private void ChordsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to add chord
            var position = e.GetPosition(ChordsCanvas);
            var beat = PositionToBeats(position.X + ScrollOffset);
            AddChordAtBeat(beat);
        }
        else
        {
            // Deselect
            SelectChord(null);
        }
    }

    private void ChordsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedChord != null)
        {
            if (_isDragging)
            {
                ChordChanged?.Invoke(this, _draggedChord);
            }

            if (_chordElements.TryGetValue(_draggedChord.Id, out var element))
            {
                element.ReleaseMouseCapture();
            }

            _draggedChord = null;
            _isDragging = false;
        }

        if (_resizingChord != null)
        {
            ChordChanged?.Invoke(this, _resizingChord);

            if (_chordElements.TryGetValue(_resizingChord.Id, out var element))
            {
                element.ReleaseMouseCapture();
            }

            _resizingChord = null;
            _isResizing = false;
        }
    }

    private void ChordsCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(ChordsCanvas);
        _contextMenuPosition = PositionToBeats(position.X + ScrollOffset);

        // Find chord at position
        var hitChord = GetChordAtPosition(position.X + ScrollOffset);
        SelectChord(hitChord);

        EditChordMenuItem.IsEnabled = hitChord != null;
        DeleteChordMenuItem.IsEnabled = hitChord != null;
        TransposeUpMenuItem.IsEnabled = hitChord != null;
        TransposeDownMenuItem.IsEnabled = hitChord != null;
    }

    private void ChordsCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(ChordsCanvas);
        var effectiveBeatWidth = PixelsPerBeat * ZoomX;

        if (_isDragging && _draggedChord != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var delta = position.X - _dragStartPoint.X;
            var beatDelta = delta / effectiveBeatWidth;
            var newPosition = Math.Max(0, _dragStartPosition + beatDelta);

            _draggedChord.Position = SnapToBeat(newPosition);
            UpdateChordPosition(_draggedChord);
        }
        else if (_isResizing && _resizingChord != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var delta = position.X - _dragStartPoint.X;
            var beatDelta = delta / effectiveBeatWidth;
            var newDuration = Math.Max(MinChordDuration, _dragStartDuration + beatDelta);

            _resizingChord.Duration = SnapToBeat(newDuration);
            RenderChords();
        }
        else if (_draggedChord != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var delta = position - _dragStartPoint;
            if (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5)
            {
                _isDragging = true;
            }
        }
    }

    private void ChordsCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        HoverTooltip.Visibility = Visibility.Collapsed;

        if (_draggedChord != null && e.LeftButton != MouseButtonState.Pressed)
        {
            _draggedChord = null;
            _isDragging = false;
        }
    }

    #endregion

    #region Button Event Handlers

    private void AddChordButton_Click(object sender, RoutedEventArgs e)
    {
        AddChordAtBeat(PlayheadPosition);
    }

    private void DetectChordsButton_Click(object sender, RoutedEventArgs e)
    {
        DetectChordsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearChordsButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear all chords?", "Clear Chords",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Chords?.Clear();
        }
    }

    private void EditChord_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChord != null)
        {
            ShowChordEditDialog(_selectedChord);
        }
    }

    private void DeleteChord_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChord != null)
        {
            RemoveChord(_selectedChord);
        }
    }

    private void AddChordHere_Click(object sender, RoutedEventArgs e)
    {
        AddChordAtBeat(_contextMenuPosition);
    }

    private void TransposeUp_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChord != null)
        {
            TransposeChord(_selectedChord, 1);
        }
    }

    private void TransposeDown_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChord != null)
        {
            TransposeChord(_selectedChord, -1);
        }
    }

    #endregion

    #region Helper Methods

    private double PositionToBeats(double x)
    {
        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        return x / effectiveBeatWidth;
    }

    private double SnapToBeat(double beat, double grid = 0.5)
    {
        return Math.Round(beat / grid) * grid;
    }

    private ChordMarker? GetChordAtPosition(double x)
    {
        if (Chords == null) return null;

        var beat = PositionToBeats(x);

        return Chords.FirstOrDefault(c =>
            beat >= c.Position && beat < c.Position + c.Duration);
    }

    private void SelectChord(ChordMarker? chord)
    {
        // Deselect previous
        if (_selectedChord != null)
        {
            _selectedChord.IsSelected = false;
            if (_chordElements.TryGetValue(_selectedChord.Id, out var prevElement))
            {
                prevElement.BorderThickness = new Thickness(0);
            }
        }

        _selectedChord = chord;

        // Select new
        if (_selectedChord != null)
        {
            _selectedChord.IsSelected = true;
            if (_chordElements.TryGetValue(_selectedChord.Id, out var element))
            {
                element.BorderThickness = new Thickness(2);
            }
        }

        ChordSelected?.Invoke(this, chord);
    }

    private void UpdateChordPosition(ChordMarker chord)
    {
        if (_chordElements.TryGetValue(chord.Id, out var element))
        {
            var effectiveBeatWidth = PixelsPerBeat * ZoomX;
            var x = chord.Position * effectiveBeatWidth;
            Canvas.SetLeft(element, x + 1);
        }
    }

    private void AddChordAtBeat(double beat)
    {
        var newChord = new ChordMarker
        {
            Position = SnapToBeat(beat),
            Duration = 4.0, // Default 4 beats
            ChordName = "C",
            ChordNotes = [60, 64, 67] // C major
        };

        Chords ??= [];
        Chords.Add(newChord);
        SelectChord(newChord);
        ChordAdded?.Invoke(this, newChord);
    }

    private void RemoveChord(ChordMarker chord)
    {
        Chords?.Remove(chord);
        if (_selectedChord == chord)
        {
            _selectedChord = null;
        }
        ChordRemoved?.Invoke(this, chord);
    }

    private void TransposeChord(ChordMarker chord, int semitones)
    {
        // Transpose notes
        for (int i = 0; i < chord.ChordNotes.Count; i++)
        {
            chord.ChordNotes[i] = Math.Clamp(chord.ChordNotes[i] + semitones, 0, 127);
        }

        // Update chord name based on new root
        var rootNote = chord.ChordNotes.Min();
        var rootName = GetNoteName(rootNote);
        var quality = GetChordQuality(chord.ChordName);
        chord.ChordName = rootName + quality;

        RenderChords();
        ChordChanged?.Invoke(this, chord);
    }

    private void ShowChordEditDialog(ChordMarker chord)
    {
        // Create a simple input dialog using WPF Window
        var dialog = new Window
        {
            Title = "Edit Chord",
            Width = 300,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30)),
            WindowStyle = WindowStyle.ToolWindow
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = "Enter chord name (e.g., Cmaj7, Am, G/B):",
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(label, 0);

        var textBox = new TextBox
        {
            Text = chord.ChordName,
            Margin = new Thickness(0, 0, 0, 16),
            Padding = new Thickness(4),
            FontSize = 14
        };
        Grid.SetRow(textBox, 1);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 2);

        var okButton = new Button
        {
            Content = "OK",
            Width = 70,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        okButton.Click += (s, e) => { dialog.DialogResult = true; };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 70,
            IsCancel = true
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(label);
        grid.Children.Add(textBox);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            chord.ChordName = textBox.Text;
            RenderChords();
            ChordChanged?.Invoke(this, chord);
        }
    }

    private void ShowChordTooltip(ChordMarker chord, Border element)
    {
        HoverChordName.Text = chord.ChordName;
        HoverChordNotes.Text = string.Join(" ", chord.ChordNotes.Select(GetNoteName));

        var pos = element.TranslatePoint(new Point(0, 0), this);
        HoverTooltip.Margin = new Thickness(pos.X, pos.Y - 30, 0, 0);
        HoverTooltip.Visibility = Visibility.Visible;
    }

    private static string GetNoteName(int midiNote)
    {
        string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        return noteNames[midiNote % 12];
    }

    private static string GetChordQuality(string chordName)
    {
        // Extract quality from chord name (everything after the root)
        var root = chordName.Length > 1 && (chordName[1] == '#' || chordName[1] == 'b')
            ? chordName[..2]
            : chordName[..1];
        return chordName[root.Length..];
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the chord track display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    /// <summary>
    /// Gets the chord at a specific beat position.
    /// </summary>
    public ChordMarker? GetChordAtBeat(double beat)
    {
        return Chords?.FirstOrDefault(c =>
            beat >= c.Position && beat < c.Position + c.Duration);
    }

    /// <summary>
    /// Adds a chord with the specified properties.
    /// </summary>
    public ChordMarker AddChord(double position, double duration, string name, List<int>? notes = null)
    {
        var chord = new ChordMarker
        {
            Position = position,
            Duration = duration,
            ChordName = name,
            ChordNotes = notes ?? []
        };

        Chords ??= [];
        Chords.Add(chord);
        ChordAdded?.Invoke(this, chord);

        return chord;
    }

    #endregion
}

/// <summary>
/// Represents a chord marker on the chord track.
/// </summary>
public partial class ChordMarker : ObservableObject
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Position in beats.
    /// </summary>
    [ObservableProperty]
    private double _position;

    /// <summary>
    /// Duration in beats.
    /// </summary>
    [ObservableProperty]
    private double _duration = 4.0;

    /// <summary>
    /// Chord name/symbol (e.g., "Cmaj7", "Am", "G/B").
    /// </summary>
    [ObservableProperty]
    private string _chordName = "C";

    /// <summary>
    /// MIDI note numbers that make up the chord.
    /// </summary>
    [ObservableProperty]
    private List<int> _chordNotes = [];

    /// <summary>
    /// Custom color for the chord block (hex string).
    /// </summary>
    [ObservableProperty]
    private string _color = string.Empty;

    /// <summary>
    /// Indicates whether this chord is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Optional: Bass note for slash chords (e.g., the "B" in "G/B").
    /// </summary>
    [ObservableProperty]
    private int? _bassNote;

    /// <summary>
    /// Gets the end position in beats.
    /// </summary>
    public double EndPosition => Position + Duration;
}
