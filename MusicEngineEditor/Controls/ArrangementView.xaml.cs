using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngine.Core;
using MusicEngineEditor.ViewModels;
using Rectangle = System.Windows.Shapes.Rectangle;
using Line = System.Windows.Shapes.Line;
using MenuItem = System.Windows.Controls.MenuItem;
using ColorConverter = System.Windows.Media.ColorConverter;
using SectionType = MusicEngine.Core.SectionType;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and editing song arrangement with sections.
/// </summary>
public partial class ArrangementView : UserControl
{
    private ArrangementViewModel? _viewModel;
    private readonly Dictionary<Guid, UIElement> _sectionElements = [];
    private ArrangementSection? _selectedSection;
    private ArrangementSection? _draggingSection;
    private Point _dragStartPoint;
    private double _dragStartPosition;
    private bool _isDragging;
    private bool _isResizing;
    private bool _isResizingLeft;
    private double _contextMenuPosition;

    /// <summary>
    /// Gets or sets the view model.
    /// </summary>
    public ArrangementViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = value;
            DataContext = _viewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                MarkerTrackControl.MarkerTrack = _viewModel.Arrangement?.MarkerTrack;
            }

            RefreshSections();
        }
    }

    /// <summary>
    /// Event raised when the playback position should change.
    /// </summary>
    public event EventHandler<double>? SeekRequested;

    /// <summary>
    /// Event raised when a section is selected.
    /// </summary>
    public event EventHandler<ArrangementSection>? SectionSelected;

    public ArrangementView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RefreshView();
        Loaded += ArrangementView_Loaded;
    }

    private void ArrangementView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            // Create default view model if not set
            ViewModel = new ArrangementViewModel();
        }

        RefreshView();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ArrangementViewModel.Arrangement):
                MarkerTrackControl.MarkerTrack = _viewModel?.Arrangement?.MarkerTrack;
                RefreshSections();
                break;
            case nameof(ArrangementViewModel.PlaybackPosition):
                UpdatePlayhead();
                MarkerTrackControl.PlaybackPosition = _viewModel?.PlaybackPosition ?? 0;
                break;
            case nameof(ArrangementViewModel.VisibleBeats):
            case nameof(ArrangementViewModel.ScrollOffset):
                RefreshView();
                MarkerTrackControl.VisibleBeats = _viewModel?.VisibleBeats ?? 64;
                MarkerTrackControl.ScrollOffset = _viewModel?.ScrollOffset ?? 0;
                break;
        }
    }

    /// <summary>
    /// Refreshes the entire view.
    /// </summary>
    public void RefreshView()
    {
        RefreshTimeline();
        RefreshSections();
        UpdatePlayhead();
    }

    private void RefreshTimeline()
    {
        TimelineRuler.Children.Clear();

        if (_viewModel == null || SectionCanvas.ActualWidth <= 0)
            return;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        var beatsPerBar = _viewModel.Arrangement?.TimeSignatureNumerator ?? 4;

        // Draw bar lines and numbers
        var startBeat = _viewModel.ScrollOffset;
        var endBeat = startBeat + _viewModel.VisibleBeats;

        // Determine grid spacing based on zoom
        var gridSpacing = beatsPerBar;
        if (pixelsPerBeat * beatsPerBar < 30) gridSpacing *= 2;
        if (pixelsPerBeat * beatsPerBar < 15) gridSpacing *= 2;

        for (var beat = Math.Floor(startBeat / gridSpacing) * gridSpacing; beat <= endBeat; beat += gridSpacing)
        {
            var x = (beat - startBeat) * pixelsPerBeat;
            if (x < 0) continue;

            // Bar line
            var line = new Line
            {
                X1 = x,
                Y1 = 16,
                X2 = x,
                Y2 = 24,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            TimelineRuler.Children.Add(line);

            // Bar number
            var barNumber = (int)(beat / beatsPerBar) + 1;
            var text = new TextBlock
            {
                Text = barNumber.ToString(),
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray)
            };
            Canvas.SetLeft(text, x + 2);
            Canvas.SetTop(text, 2);
            TimelineRuler.Children.Add(text);
        }
    }

    /// <summary>
    /// Refreshes the section display.
    /// </summary>
    public void RefreshSections()
    {
        // Clear existing section elements
        foreach (var element in _sectionElements.Values)
        {
            SectionCanvas.Children.Remove(element);
        }
        _sectionElements.Clear();

        if (_viewModel?.Arrangement == null)
            return;

        // Update canvas width based on total length
        var pixelsPerBeat = Math.Max(1, SectionCanvas.ActualWidth / _viewModel.VisibleBeats);
        SectionCanvas.Width = Math.Max(SectionCanvas.ActualWidth, _viewModel.Arrangement.TotalLength * pixelsPerBeat + 100);

        // Draw grid lines
        DrawGridLines(pixelsPerBeat);

        // Add section elements
        foreach (var section in _viewModel.Arrangement.Sections)
        {
            var element = CreateSectionElement(section);
            _sectionElements[section.Id] = element;
            SectionCanvas.Children.Add(element);
            PositionSectionElement(section, element, pixelsPerBeat);
        }

        // Ensure playhead is on top
        if (SectionCanvas.Children.Contains(Playhead))
        {
            SectionCanvas.Children.Remove(Playhead);
            SectionCanvas.Children.Add(Playhead);
        }
    }

    private void DrawGridLines(double pixelsPerBeat)
    {
        var beatsPerBar = _viewModel?.Arrangement?.TimeSignatureNumerator ?? 4;
        var totalBeats = _viewModel?.Arrangement?.TotalLength ?? 64;

        // Determine grid spacing based on zoom
        var gridSpacing = beatsPerBar;
        if (pixelsPerBeat * beatsPerBar < 30) gridSpacing *= 2;
        if (pixelsPerBeat * beatsPerBar < 15) gridSpacing *= 2;

        for (double beat = 0; beat <= totalBeats + gridSpacing; beat += gridSpacing)
        {
            var x = beat * pixelsPerBeat;
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = SectionCanvas.ActualHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                StrokeThickness = beat % (beatsPerBar * 4) == 0 ? 1 : 0.5,
                IsHitTestVisible = false
            };
            SectionCanvas.Children.Add(line);
        }
    }

    private UIElement CreateSectionElement(ArrangementSection section)
    {
        var color = ParseColor(section.Color);
        var brush = new SolidColorBrush(color);
        var lightBrush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));

        var container = new Grid
        {
            Tag = section,
            Cursor = Cursors.Hand,
            Opacity = section.IsMuted ? 0.5 : 1.0
        };

        // Main section rectangle
        var rect = new Border
        {
            Background = lightBrush,
            BorderBrush = brush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(1, 4, 1, 4)
        };

        // Section content
        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header with name
        var header = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(2, 2, 0, 0),
            Padding = new Thickness(6, 2, 6, 2)
        };

        var headerContent = new StackPanel { Orientation = Orientation.Horizontal };
        headerContent.Children.Add(new TextBlock
        {
            Text = section.Name,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });

        if (section.RepeatCount > 1)
        {
            headerContent.Children.Add(new TextBlock
            {
                Text = $" x{section.RepeatCount}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (section.IsLocked)
        {
            headerContent.Children.Add(new TextBlock
            {
                Text = " [Locked]",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        header.Child = headerContent;
        Grid.SetRow(header, 0);
        contentGrid.Children.Add(header);

        // Info area
        var info = new StackPanel
        {
            Margin = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        info.Children.Add(new TextBlock
        {
            Text = $"{section.StartPosition:F1} - {section.EndPosition:F1} ({section.Length:F1} beats)",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
        });

        Grid.SetRow(info, 1);
        contentGrid.Children.Add(info);

        rect.Child = contentGrid;
        container.Children.Add(rect);

        // Resize handles
        var leftHandle = new Rectangle
        {
            Width = 6,
            Fill = Brushes.Transparent,
            Cursor = Cursors.SizeWE,
            HorizontalAlignment = HorizontalAlignment.Left,
            Tag = "LeftHandle"
        };

        var rightHandle = new Rectangle
        {
            Width = 6,
            Fill = Brushes.Transparent,
            Cursor = Cursors.SizeWE,
            HorizontalAlignment = HorizontalAlignment.Right,
            Tag = "RightHandle"
        };

        container.Children.Add(leftHandle);
        container.Children.Add(rightHandle);

        // Event handlers
        container.MouseLeftButtonDown += SectionElement_MouseLeftButtonDown;
        container.MouseEnter += SectionElement_MouseEnter;
        container.MouseLeave += SectionElement_MouseLeave;
        leftHandle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
        rightHandle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;

        return container;
    }

    private void PositionSectionElement(ArrangementSection section, UIElement element, double pixelsPerBeat)
    {
        if (element is not Grid container)
            return;

        var x = (section.StartPosition - (_viewModel?.ScrollOffset ?? 0)) * pixelsPerBeat;
        var width = section.Length * pixelsPerBeat;

        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, 0);
        container.Width = Math.Max(20, width);
        container.Height = SectionCanvas.ActualHeight > 0 ? SectionCanvas.ActualHeight : 80;
    }

    private void UpdatePlayhead()
    {
        if (_viewModel == null || SectionCanvas.ActualWidth <= 0)
            return;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        var x = (_viewModel.PlaybackPosition - _viewModel.ScrollOffset) * pixelsPerBeat;

        if (x >= 0 && x <= SectionCanvas.Width)
        {
            Playhead.Visibility = Visibility.Visible;
            Canvas.SetLeft(Playhead, x);
            Playhead.Height = SectionCanvas.ActualHeight;
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private double PositionToBeats(double x)
    {
        if (_viewModel == null || SectionCanvas.ActualWidth <= 0)
            return 0;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        return (x / pixelsPerBeat) + _viewModel.ScrollOffset;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Blue;
        }
    }

    #region Event Handlers

    private void SectionElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Grid container && container.Tag is ArrangementSection section)
        {
            _selectedSection = section;
            _draggingSection = section;
            _dragStartPoint = e.GetPosition(SectionCanvas);
            _dragStartPosition = section.StartPosition;
            _isDragging = false;
            _isResizing = false;

            SectionSelected?.Invoke(this, section);
            container.CaptureMouse();
            e.Handled = true;
        }
    }

    private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle handle && handle.Parent is Grid container && container.Tag is ArrangementSection section)
        {
            _selectedSection = section;
            _draggingSection = section;
            _dragStartPoint = e.GetPosition(SectionCanvas);
            _dragStartPosition = handle.Tag?.ToString() == "LeftHandle" ? section.StartPosition : section.EndPosition;
            _isResizing = true;
            _isResizingLeft = handle.Tag?.ToString() == "LeftHandle";
            _isDragging = false;

            container.CaptureMouse();
            e.Handled = true;
        }
    }

    private void SectionElement_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Grid container)
        {
            container.Opacity = container.Tag is ArrangementSection section && section.IsMuted ? 0.6 : 0.9;
        }
    }

    private void SectionElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Grid container)
        {
            container.Opacity = container.Tag is ArrangementSection section && section.IsMuted ? 0.5 : 1.0;
        }
    }

    private void SectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click to seek
        if (e.ClickCount == 2)
        {
            var position = PositionToBeats(e.GetPosition(SectionCanvas).X);
            SeekRequested?.Invoke(this, position);
        }
    }

    private void SectionCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuPosition = PositionToBeats(e.GetPosition(SectionCanvas).X);

        // Check if clicking on a section
        var hitSection = GetSectionAtPosition(e.GetPosition(SectionCanvas));
        _selectedSection = hitSection;

        // Enable/disable context menu items
        EditSectionMenuItem.IsEnabled = hitSection != null;
        DuplicateSectionMenuItem.IsEnabled = hitSection != null;
        DeleteSectionMenuItem.IsEnabled = hitSection != null && !hitSection.IsLocked;
        SetRepeatMenuItem.IsEnabled = hitSection != null && !hitSection.IsLocked;
        MuteSectionMenuItem.IsEnabled = hitSection != null;
        MuteSectionMenuItem.Header = hitSection?.IsMuted == true ? "Unmute Section" : "Mute Section";
        LockSectionMenuItem.IsEnabled = hitSection != null;
        LockSectionMenuItem.Header = hitSection?.IsLocked == true ? "Unlock Section" : "Lock Section";
    }

    private void SectionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingSection != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(SectionCanvas);
            var deltaX = currentPoint.X - _dragStartPoint.X;

            if (!_isDragging && Math.Abs(deltaX) > 5)
            {
                _isDragging = true;
            }

            if (_isDragging && !_draggingSection.IsLocked)
            {
                var newPosition = PositionToBeats(currentPoint.X);
                newPosition = Math.Max(0, newPosition);

                // Snap to grid (quarter note)
                newPosition = Math.Round(newPosition * 4) / 4;

                if (_isResizing)
                {
                    if (_isResizingLeft)
                    {
                        var newStart = Math.Min(newPosition, _draggingSection.EndPosition - 1);
                        _draggingSection.StartPosition = newStart;
                    }
                    else
                    {
                        var newEnd = Math.Max(newPosition, _draggingSection.StartPosition + 1);
                        _draggingSection.EndPosition = newEnd;
                    }
                }
                else
                {
                    _viewModel?.Arrangement?.MoveSection(_draggingSection, newPosition);
                }

                _draggingSection.Touch();
                RefreshSections();
            }
        }
    }

    private void SectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingSection != null)
        {
            // Find and release the container
            if (_sectionElements.TryGetValue(_draggingSection.Id, out var element) && element is Grid container)
            {
                container.ReleaseMouseCapture();
            }

            _draggingSection = null;
            _isDragging = false;
            _isResizing = false;
        }
    }

    private ArrangementSection? GetSectionAtPosition(Point point)
    {
        var beats = PositionToBeats(point.X);
        return _viewModel?.Arrangement?.GetSectionAt(beats);
    }

    private void SectionScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_viewModel == null) return;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        if (pixelsPerBeat > 0)
        {
            _viewModel.ScrollOffset = e.HorizontalOffset / pixelsPerBeat;
        }
    }

    private void AddSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && _viewModel?.Arrangement != null)
        {
            var typeName = button.Content?.ToString() ?? "Section";
            var type = GetSectionTypeFromName(typeName);

            // Add at end of arrangement
            var startPosition = _viewModel.Arrangement.TotalLength;
            var length = 16.0; // Default 4 bars at 4/4

            _viewModel.Arrangement.AddSection(startPosition, startPosition + length, type);
            RefreshSections();
        }
    }

    private void AddSectionHere_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.Arrangement == null) return;

        var startPosition = Math.Round(_contextMenuPosition * 4) / 4; // Snap to quarter note
        var length = 16.0;

        _viewModel.Arrangement.AddSection(startPosition, startPosition + length, "New Section");
        RefreshSections();
    }

    private void AddSectionType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && _viewModel?.Arrangement != null)
        {
            var typeTag = menuItem.Tag?.ToString() ?? "Custom";
            var type = Enum.TryParse<SectionType>(typeTag, out var parsed) ? parsed : SectionType.Custom;

            var startPosition = Math.Round(_contextMenuPosition * 4) / 4;
            var length = 16.0;

            _viewModel.Arrangement.AddSection(startPosition, startPosition + length, type);
            RefreshSections();
        }
    }

    private void CreateStandardStructure_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will replace the current arrangement with a standard song structure. Continue?",
            "Create Standard Structure",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes && _viewModel != null)
        {
            _viewModel.Arrangement = Arrangement.CreateStandardStructure();
            MarkerTrackControl.MarkerTrack = _viewModel.Arrangement.MarkerTrack;
            RefreshSections();
        }
    }

    private void EditSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null) return;

        MessageBox.Show(
            $"Edit section: {_selectedSection.Name}\n" +
            $"Position: {_selectedSection.StartPosition:F2} - {_selectedSection.EndPosition:F2}\n" +
            $"Type: {_selectedSection.Type}\n" +
            $"Repeat: {_selectedSection.RepeatCount}x",
            "Edit Section",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void DuplicateSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null || _viewModel?.Arrangement == null) return;

        _viewModel.Arrangement.DuplicateSection(_selectedSection);
        RefreshSections();
    }

    private void DeleteSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null || _viewModel?.Arrangement == null) return;

        if (_selectedSection.IsLocked)
        {
            MessageBox.Show("Cannot delete a locked section.", "Section Locked",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Delete section '{_selectedSection.Name}'?",
            "Delete Section",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.Arrangement.RemoveSection(_selectedSection);
            _selectedSection = null;
            RefreshSections();
        }
    }

    private void SetRepeat_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null) return;

        // In a full implementation, show a dialog to set repeat count
        _selectedSection.RepeatCount = _selectedSection.RepeatCount >= 4 ? 1 : _selectedSection.RepeatCount + 1;
        _selectedSection.Touch();
        RefreshSections();
    }

    private void MuteSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null) return;

        _selectedSection.IsMuted = !_selectedSection.IsMuted;
        _selectedSection.Touch();
        RefreshSections();
    }

    private void LockSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null) return;

        _selectedSection.IsLocked = !_selectedSection.IsLocked;
        _selectedSection.Touch();
        RefreshSections();
    }

    private void MarkerTrackControl_MarkerSelected(object? sender, Marker marker)
    {
        // Handle marker selection if needed
    }

    private void MarkerTrackControl_JumpRequested(object? sender, double position)
    {
        SeekRequested?.Invoke(this, position);
    }

    private static SectionType GetSectionTypeFromName(string name)
    {
        return name.Replace("-", "").Replace(" ", "") switch
        {
            "Intro" => SectionType.Intro,
            "Verse" => SectionType.Verse,
            "PreChorus" => SectionType.PreChorus,
            "Chorus" => SectionType.Chorus,
            "PostChorus" => SectionType.PostChorus,
            "Bridge" => SectionType.Bridge,
            "Breakdown" => SectionType.Breakdown,
            "Buildup" or "BuildUp" => SectionType.Buildup,
            "Drop" => SectionType.Drop,
            "Solo" => SectionType.Solo,
            "Interlude" => SectionType.Interlude,
            "Outro" => SectionType.Outro,
            _ => SectionType.Custom
        };
    }

    #endregion
}
