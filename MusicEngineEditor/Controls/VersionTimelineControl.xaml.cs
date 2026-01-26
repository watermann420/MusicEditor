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
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Visual timeline control for project version history with branching visualization.
/// </summary>
public partial class VersionTimelineControl : UserControl
{
    private readonly ObservableCollection<ProjectVersion> _versions = new();
    private readonly List<VersionBranch> _branches = new();
    private ProjectVersion? _selectedVersion;
    private ProjectVersion? _compareVersion;
    private bool _isCompareMode;

    private const double NodeRadius = 8;
    private const double NodeSpacing = 60;
    private const double BranchSpacing = 40;
    private const double LeftMargin = 40;
    private const double TopMargin = 30;

    public event EventHandler<ProjectVersion>? VersionSelected;
    public event EventHandler<ProjectVersion>? RestoreRequested;
    public event EventHandler<(ProjectVersion, ProjectVersion)>? CompareRequested;
    public event EventHandler<string>? BranchCreated;

    public VersionTimelineControl()
    {
        InitializeComponent();

        LoadSampleData();
        DrawTimeline();
    }

    private void LoadSampleData()
    {
        // Create main branch
        var mainBranch = new VersionBranch { Name = "main", Color = "#4B6EAF" };
        _branches.Add(mainBranch);

        // Add sample versions
        var versions = new[]
        {
            new ProjectVersion
            {
                Id = "1",
                Name = "Initial commit",
                Description = "Project started",
                CreatedAt = DateTime.Now.AddDays(-7),
                IsAutoSave = false,
                BranchName = "main"
            },
            new ProjectVersion
            {
                Id = "2",
                Name = "Added drums",
                Description = "Drum pattern added",
                CreatedAt = DateTime.Now.AddDays(-6),
                IsAutoSave = false,
                BranchName = "main",
                ParentId = "1"
            },
            new ProjectVersion
            {
                Id = "3",
                Name = "Auto-save",
                CreatedAt = DateTime.Now.AddDays(-5).AddHours(-2),
                IsAutoSave = true,
                BranchName = "main",
                ParentId = "2"
            },
            new ProjectVersion
            {
                Id = "4",
                Name = "Bass line v1",
                Description = "First bass line attempt",
                CreatedAt = DateTime.Now.AddDays(-5),
                IsAutoSave = false,
                BranchName = "main",
                ParentId = "3"
            },
            new ProjectVersion
            {
                Id = "5",
                Name = "Mix adjustments",
                Description = "Level balancing",
                CreatedAt = DateTime.Now.AddDays(-4),
                IsAutoSave = false,
                BranchName = "main",
                ParentId = "4",
                IsCurrent = true
            }
        };

        foreach (var v in versions)
        {
            _versions.Add(v);
        }

        // Create alternative branch
        var altBranch = new VersionBranch { Name = "alt-mix", Color = "#6AAB73" };
        _branches.Add(altBranch);

        _versions.Add(new ProjectVersion
        {
            Id = "6",
            Name = "Alternative mix",
            Description = "Different EQ approach",
            CreatedAt = DateTime.Now.AddDays(-3),
            IsAutoSave = false,
            BranchName = "alt-mix",
            ParentId = "4"
        });

        UpdateVersionCount();
    }

    #region Public Methods

    public void AddVersion(ProjectVersion version)
    {
        _versions.Add(version);
        DrawTimeline();
        UpdateVersionCount();
    }

    public void SetVersions(IEnumerable<ProjectVersion> versions)
    {
        _versions.Clear();
        foreach (var v in versions)
        {
            _versions.Add(v);
        }
        DrawTimeline();
        UpdateVersionCount();
    }

    public void SelectVersion(string versionId)
    {
        var version = _versions.FirstOrDefault(v => v.Id == versionId);
        if (version != null)
        {
            SelectVersion(version);
        }
    }

    #endregion

    #region Private Methods

    private void DrawTimeline()
    {
        TimelineCanvas.Children.Clear();

        if (_versions.Count == 0) return;

        // Group versions by branch
        var branchVersions = _versions.GroupBy(v => v.BranchName).ToList();

        // Calculate branch Y positions
        var branchYPositions = new Dictionary<string, double>();
        double currentY = TopMargin;
        foreach (var branchGroup in branchVersions)
        {
            branchYPositions[branchGroup.Key] = currentY;
            currentY += BranchSpacing;
        }

        // Draw branch labels
        foreach (var branch in _branches)
        {
            if (branchYPositions.TryGetValue(branch.Name, out var y))
            {
                DrawBranchLabel(branch, LeftMargin - 35, y);
            }
        }

        // Sort versions by date within each branch
        var sortedVersions = _versions.OrderBy(v => v.CreatedAt).ToList();

        // Calculate X positions based on chronological order
        var versionXPositions = new Dictionary<string, double>();
        for (int i = 0; i < sortedVersions.Count; i++)
        {
            versionXPositions[sortedVersions[i].Id] = LeftMargin + i * NodeSpacing;
        }

        // Draw connections first (so nodes appear on top)
        foreach (var version in _versions)
        {
            if (!string.IsNullOrEmpty(version.ParentId))
            {
                DrawConnection(version, versionXPositions, branchYPositions);
            }
        }

        // Draw version nodes
        foreach (var version in _versions)
        {
            if (versionXPositions.TryGetValue(version.Id, out var x) &&
                branchYPositions.TryGetValue(version.BranchName, out var y))
            {
                DrawVersionNode(version, x, y);
            }
        }

        // Set canvas size
        TimelineCanvas.Width = LeftMargin + (_versions.Count + 1) * NodeSpacing;
        TimelineCanvas.Height = TopMargin + _branches.Count * BranchSpacing + 20;
    }

    private void DrawBranchLabel(VersionBranch branch, double x, double y)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2)
        };

        var text = new TextBlock
        {
            Text = branch.Name,
            FontSize = 10,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(branch.Color))
        };

        border.Child = text;
        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y - 8);
        TimelineCanvas.Children.Add(border);
    }

    private void DrawConnection(ProjectVersion version, Dictionary<string, double> xPositions, Dictionary<string, double> yPositions)
    {
        if (!xPositions.TryGetValue(version.Id, out var x) ||
            !yPositions.TryGetValue(version.BranchName, out var y) ||
            !xPositions.TryGetValue(version.ParentId!, out var parentX) ||
            version.ParentId == null)
            return;

        var parent = _versions.FirstOrDefault(v => v.Id == version.ParentId);
        if (parent == null || !yPositions.TryGetValue(parent.BranchName, out var parentY))
            return;

        // Get branch color
        var branch = _branches.FirstOrDefault(b => b.Name == version.BranchName);
        var color = branch != null
            ? (Color)ColorConverter.ConvertFromString(branch.Color)
            : Color.FromRgb(0x60, 0x60, 0x60);

        if (Math.Abs(y - parentY) < 0.1)
        {
            // Same branch - horizontal line
            var line = new Line
            {
                X1 = parentX + NodeRadius,
                Y1 = parentY,
                X2 = x - NodeRadius,
                Y2 = y,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2
            };
            TimelineCanvas.Children.Add(line);
        }
        else
        {
            // Different branch - curved line
            var path = new Path
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(parentX + NodeRadius, parentY) };

            var bezier = new BezierSegment
            {
                Point1 = new Point(parentX + NodeSpacing / 2, parentY),
                Point2 = new Point(x - NodeSpacing / 2, y),
                Point3 = new Point(x - NodeRadius, y)
            };

            figure.Segments.Add(bezier);
            geometry.Figures.Add(figure);
            path.Data = geometry;

            TimelineCanvas.Children.Add(path);
        }
    }

    private void DrawVersionNode(ProjectVersion version, double x, double y)
    {
        var branch = _branches.FirstOrDefault(b => b.Name == version.BranchName);
        var color = branch != null
            ? (Color)ColorConverter.ConvertFromString(branch.Color)
            : Color.FromRgb(0x4B, 0x6E, 0xAF);

        // Node circle
        var node = new Ellipse
        {
            Width = NodeRadius * 2,
            Height = NodeRadius * 2,
            Fill = version.IsAutoSave
                ? new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37))
                : new SolidColorBrush(color),
            Stroke = version.IsCurrent
                ? Brushes.White
                : (version.IsAutoSave ? new SolidColorBrush(color) : null),
            StrokeThickness = version.IsCurrent ? 3 : (version.IsAutoSave ? 2 : 0),
            Cursor = Cursors.Hand,
            Tag = version
        };

        node.MouseLeftButtonDown += Node_MouseLeftButtonDown;
        node.ToolTip = CreateVersionTooltip(version);

        Canvas.SetLeft(node, x - NodeRadius);
        Canvas.SetTop(node, y - NodeRadius);
        TimelineCanvas.Children.Add(node);

        // Label for non-auto-save versions
        if (!version.IsAutoSave)
        {
            var label = new TextBlock
            {
                Text = version.Name.Length > 15 ? version.Name.Substring(0, 12) + "..." : version.Name,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4)),
                TextAlignment = TextAlignment.Center
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, y + NodeRadius + 4);
            TimelineCanvas.Children.Add(label);
        }
    }

    private object CreateVersionTooltip(ProjectVersion version)
    {
        var panel = new StackPanel { MaxWidth = 250 };

        panel.Children.Add(new TextBlock
        {
            Text = version.Name,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });

        if (!string.IsNullOrEmpty(version.Description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = version.Description,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = version.CreatedAt.ToString("MMM d, yyyy HH:mm"),
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        });

        if (version.IsAutoSave)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Auto-save",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xA7, 0x3C)),
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        return new System.Windows.Controls.ToolTip
        {
            Content = panel,
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41)),
            Padding = new Thickness(10)
        };
    }

    private void SelectVersion(ProjectVersion version)
    {
        if (_isCompareMode && _selectedVersion != null)
        {
            _compareVersion = version;
            _isCompareMode = false;
            CompareRequested?.Invoke(this, (_selectedVersion, _compareVersion));
            return;
        }

        _selectedVersion = version;
        UpdateSelectedVersionInfo();
        VersionSelected?.Invoke(this, version);
    }

    private void UpdateSelectedVersionInfo()
    {
        SelectedVersionInfo.Children.Clear();

        if (_selectedVersion == null)
        {
            SelectedVersionInfo.Children.Add(new TextBlock
            {
                Text = "Select a version to view details",
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
                FontSize = 12
            });

            RestoreButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            return;
        }

        SelectedVersionInfo.Children.Add(new TextBlock
        {
            Text = _selectedVersion.Name,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });

        SelectedVersionInfo.Children.Add(new TextBlock
        {
            Text = $" - {_selectedVersion.CreatedAt:g}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (_selectedVersion.IsCurrent)
        {
            SelectedVersionInfo.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = "Current",
                    FontSize = 10,
                    Foreground = Brushes.White
                }
            });
        }

        RestoreButton.IsEnabled = !_selectedVersion.IsCurrent;
        DeleteButton.IsEnabled = !_selectedVersion.IsCurrent && _versions.Count > 1;
    }

    private void UpdateVersionCount()
    {
        VersionCountText.Text = $" ({_versions.Count} versions)";
    }

    #endregion

    #region Event Handlers

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.Tag is ProjectVersion version)
        {
            SelectVersion(version);
        }
    }

    private void CreateBranchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedVersion == null)
        {
            MessageBox.Show("Please select a version to branch from.", "No Version Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var branchName = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter branch name:",
            "Create Branch",
            $"branch-{_branches.Count + 1}");

        if (!string.IsNullOrWhiteSpace(branchName))
        {
            var colors = new[] { "#6AAB73", "#E8A73C", "#9C7CE8", "#E85C5C", "#5CBFE8" };
            var newBranch = new VersionBranch
            {
                Name = branchName,
                Color = colors[_branches.Count % colors.Length]
            };
            _branches.Add(newBranch);

            BranchCreated?.Invoke(this, branchName);
            DrawTimeline();
        }
    }

    private void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedVersion == null)
        {
            MessageBox.Show("Please select a version first, then click Compare and select another version.", "No Version Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isCompareMode = true;
        MessageBox.Show("Now click on another version to compare.", "Compare Mode", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedVersion != null && !_selectedVersion.IsCurrent)
        {
            if (MessageBox.Show($"Restore to version '{_selectedVersion.Name}'?\n\nThis will create a new version from the restored state.",
                "Restore Version", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                RestoreRequested?.Invoke(this, _selectedVersion);
            }
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedVersion != null && !_selectedVersion.IsCurrent)
        {
            if (MessageBox.Show($"Delete version '{_selectedVersion.Name}'?\n\nThis cannot be undone.",
                "Delete Version", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _versions.Remove(_selectedVersion);
                _selectedVersion = null;
                UpdateSelectedVersionInfo();
                DrawTimeline();
                UpdateVersionCount();
            }
        }
    }

    #endregion
}

#region Models

/// <summary>
/// Represents a project version in the timeline.
/// </summary>
public class ProjectVersion : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private DateTime _createdAt;
    private bool _isAutoSave;
    private bool _isCurrent;
    private string _branchName = "main";
    private string? _parentId;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
    public bool IsAutoSave { get => _isAutoSave; set { _isAutoSave = value; OnPropertyChanged(); } }
    public bool IsCurrent { get => _isCurrent; set { _isCurrent = value; OnPropertyChanged(); } }
    public string BranchName { get => _branchName; set { _branchName = value; OnPropertyChanged(); } }
    public string? ParentId { get => _parentId; set { _parentId = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a branch in the version timeline.
/// </summary>
public class VersionBranch
{
    public string Name { get; set; } = "main";
    public string Color { get; set; } = "#4B6EAF";
}

#endregion
