// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Control for selecting and comparing track versions with quick preview.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicEngineEditor.Services;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for selecting and comparing track versions with a quick preview.
/// </summary>
public partial class TrackVersionCompareControl : UserControl
{
    #region Fields

    private ITrackVersioningService? _versioningService;
    private string? _trackId;
    private string _trackName = "Track";
    private List<VersionDisplayItem> _versions = new();

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty VersioningServiceProperty =
        DependencyProperty.Register(
            nameof(VersioningService),
            typeof(ITrackVersioningService),
            typeof(TrackVersionCompareControl),
            new PropertyMetadata(null, OnVersioningServiceChanged));

    public ITrackVersioningService? VersioningService
    {
        get => (ITrackVersioningService?)GetValue(VersioningServiceProperty);
        set => SetValue(VersioningServiceProperty, value);
    }

    public static readonly DependencyProperty TrackIdProperty =
        DependencyProperty.Register(
            nameof(TrackId),
            typeof(string),
            typeof(TrackVersionCompareControl),
            new PropertyMetadata(null, OnTrackIdChanged));

    public string? TrackId
    {
        get => (string?)GetValue(TrackIdProperty);
        set => SetValue(TrackIdProperty, value);
    }

    public static readonly DependencyProperty TrackNameProperty =
        DependencyProperty.Register(
            nameof(TrackName),
            typeof(string),
            typeof(TrackVersionCompareControl),
            new PropertyMetadata("Track", OnTrackNameChanged));

    public string TrackName
    {
        get => (string)GetValue(TrackNameProperty);
        set => SetValue(TrackNameProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a detailed comparison is requested.
    /// </summary>
    public event EventHandler<VersionComparisonRequestedEventArgs>? DetailedComparisonRequested;

    /// <summary>
    /// Raised when a quick comparison is performed.
    /// </summary>
    public event EventHandler<VersionComparison>? QuickComparisonCompleted;

    #endregion

    #region Constructor

    public TrackVersionCompareControl()
    {
        InitializeComponent();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the control with a versioning service and track ID.
    /// </summary>
    public void Initialize(ITrackVersioningService service, string trackId, string trackName)
    {
        _versioningService = service;
        _trackId = trackId;
        _trackName = trackName;

        TrackNameText.Text = $"Track: {trackName}";
        LoadVersions();
    }

    /// <summary>
    /// Refreshes the version list.
    /// </summary>
    public void RefreshVersions()
    {
        LoadVersions();
    }

    /// <summary>
    /// Pre-selects specific versions for comparison.
    /// </summary>
    public void SelectVersions(string? versionAId, string? versionBId)
    {
        if (versionAId != null)
        {
            var versionA = _versions.FirstOrDefault(v => v.Id == versionAId);
            if (versionA != null)
                VersionACombo.SelectedItem = versionA;
        }

        if (versionBId != null)
        {
            var versionB = _versions.FirstOrDefault(v => v.Id == versionBId);
            if (versionB != null)
                VersionBCombo.SelectedItem = versionB;
        }
    }

    #endregion

    #region Private Methods

    private static void OnVersioningServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackVersionCompareControl control)
        {
            control._versioningService = e.NewValue as ITrackVersioningService;
            control.LoadVersions();
        }
    }

    private static void OnTrackIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackVersionCompareControl control)
        {
            control._trackId = e.NewValue as string;
            control.LoadVersions();
        }
    }

    private static void OnTrackNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackVersionCompareControl control)
        {
            control._trackName = e.NewValue as string ?? "Track";
            control.TrackNameText.Text = $"Track: {control._trackName}";
        }
    }

    private void LoadVersions()
    {
        _versions.Clear();
        VersionACombo.ItemsSource = null;
        VersionBCombo.ItemsSource = null;

        if (_versioningService == null || string.IsNullOrEmpty(_trackId))
        {
            QuickDiffText.Text = "No track selected";
            return;
        }

        var versions = _versioningService.GetVersions(_trackId);
        _versions = versions.Select(v => new VersionDisplayItem
        {
            Id = v.Id,
            Name = v.Name,
            CreatedAt = v.CreatedAt,
            DisplayName = $"{v.Name} ({v.CreatedAt:g})"
        }).ToList();

        VersionACombo.ItemsSource = _versions;
        VersionBCombo.ItemsSource = _versions;

        if (_versions.Count >= 2)
        {
            VersionACombo.SelectedIndex = 0;
            VersionBCombo.SelectedIndex = 1;
        }
        else if (_versions.Count == 1)
        {
            VersionACombo.SelectedIndex = 0;
            QuickDiffText.Text = "Only one version exists. Create more versions to compare.";
        }
        else
        {
            QuickDiffText.Text = "No versions available. Create versions to compare.";
        }
    }

    private void UpdateButtonStates()
    {
        var versionA = VersionACombo.SelectedItem as VersionDisplayItem;
        var versionB = VersionBCombo.SelectedItem as VersionDisplayItem;

        bool canCompare = versionA != null && versionB != null && versionA.Id != versionB.Id;
        CompareButton.IsEnabled = canCompare;
        QuickCompareButton.IsEnabled = canCompare;
    }

    private void PerformQuickCompare()
    {
        if (_versioningService == null || string.IsNullOrEmpty(_trackId))
            return;

        var versionA = VersionACombo.SelectedItem as VersionDisplayItem;
        var versionB = VersionBCombo.SelectedItem as VersionDisplayItem;

        if (versionA == null || versionB == null || versionA.Id == versionB.Id)
        {
            QuickDiffText.Text = "Select two different versions to compare.";
            return;
        }

        try
        {
            var comparison = _versioningService.CompareVersions(_trackId, versionA.Id, versionB.Id);
            DisplayQuickDiff(comparison);
            QuickComparisonCompleted?.Invoke(this, comparison);
        }
        catch (Exception ex)
        {
            QuickDiffText.Text = $"Error comparing versions: {ex.Message}";
        }
    }

    private void DisplayQuickDiff(VersionComparison comparison)
    {
        QuickDiffPanel.Children.Clear();

        // Summary header
        var summaryPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        // Add summary counts with colors
        var countsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };

        countsPanel.Children.Add(CreateCountBadge($"+{comparison.TotalAdded}", "#4CAF50"));
        countsPanel.Children.Add(CreateCountBadge($"-{comparison.TotalRemoved}", "#F44336"));
        countsPanel.Children.Add(CreateCountBadge($"~{comparison.TotalModified}", "#FF9800"));

        summaryPanel.Children.Add(countsPanel);

        // Check for no differences
        if (!comparison.HasDifferences && comparison.TotalAdded == 0 &&
            comparison.TotalRemoved == 0 && comparison.TotalModified == 0)
        {
            summaryPanel.Children.Add(new TextBlock
            {
                Text = "Versions are identical",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x66)),
                FontWeight = FontWeights.SemiBold
            });
            QuickDiffPanel.Children.Add(summaryPanel);
            return;
        }

        // Summary text
        var summaryText = new TextBlock
        {
            Text = $"{comparison.TotalAdded + comparison.TotalRemoved + comparison.TotalModified} total changes found",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            TextWrapping = TextWrapping.Wrap
        };
        summaryPanel.Children.Add(summaryText);

        QuickDiffPanel.Children.Add(summaryPanel);

        // Add category summaries
        if (comparison.NoteDiffs != null && HasChanges(comparison.NoteDiffs.Added.Count, comparison.NoteDiffs.Removed.Count, comparison.NoteDiffs.Modified.Count))
        {
            QuickDiffPanel.Children.Add(CreateCategorySummary("Notes",
                comparison.NoteDiffs.Added.Count,
                comparison.NoteDiffs.Removed.Count,
                comparison.NoteDiffs.Modified.Count));
        }

        if (comparison.ClipDiffs != null && HasChanges(comparison.ClipDiffs.Added.Count, comparison.ClipDiffs.Removed.Count, comparison.ClipDiffs.Modified.Count))
        {
            QuickDiffPanel.Children.Add(CreateCategorySummary("Clips",
                comparison.ClipDiffs.Added.Count,
                comparison.ClipDiffs.Removed.Count,
                comparison.ClipDiffs.Modified.Count));
        }

        if (comparison.EffectDiffs != null && HasChanges(comparison.EffectDiffs.Added.Count, comparison.EffectDiffs.Removed.Count, comparison.EffectDiffs.Modified.Count))
        {
            QuickDiffPanel.Children.Add(CreateCategorySummary("Effects",
                comparison.EffectDiffs.Added.Count,
                comparison.EffectDiffs.Removed.Count,
                comparison.EffectDiffs.Modified.Count));
        }

        if (comparison.AutomationDiffs != null && HasChanges(comparison.AutomationDiffs.Added.Count, comparison.AutomationDiffs.Removed.Count, comparison.AutomationDiffs.Modified.Count))
        {
            QuickDiffPanel.Children.Add(CreateCategorySummary("Automation",
                comparison.AutomationDiffs.Added.Count,
                comparison.AutomationDiffs.Removed.Count,
                comparison.AutomationDiffs.Modified.Count));
        }

        if (comparison.ParameterDiffs != null && HasChanges(comparison.ParameterDiffs.Added.Count, comparison.ParameterDiffs.Removed.Count, comparison.ParameterDiffs.Modified.Count))
        {
            QuickDiffPanel.Children.Add(CreateCategorySummary("Parameters",
                comparison.ParameterDiffs.Added.Count,
                comparison.ParameterDiffs.Removed.Count,
                comparison.ParameterDiffs.Modified.Count));
        }
    }

    private static bool HasChanges(int added, int removed, int modified)
    {
        return added > 0 || removed > 0 || modified > 0;
    }

    private static Border CreateCountBadge(string text, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x30, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(color),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            }
        };
    }

    private static Border CreateCategorySummary(string category, int added, int removed, int modified)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new TextBlock
        {
            Text = category,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            Width = 80
        });

        if (added > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"+{added}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x66)),
                Margin = new Thickness(0, 0, 8, 0)
            });
        }

        if (removed > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"-{removed}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)),
                Margin = new Thickness(0, 0, 8, 0)
            });
        }

        if (modified > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"~{modified}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00))
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x28)),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(4),
            Child = panel
        };
    }

    #endregion

    #region Event Handlers

    private void VersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        if (_versioningService == null || string.IsNullOrEmpty(_trackId))
            return;

        var versionA = VersionACombo.SelectedItem as VersionDisplayItem;
        var versionB = VersionBCombo.SelectedItem as VersionDisplayItem;

        if (versionA == null || versionB == null)
            return;

        // Raise event for external handling if subscribed
        var args = new VersionComparisonRequestedEventArgs(_trackId, _trackName, versionA.Id, versionB.Id);
        DetailedComparisonRequested?.Invoke(this, args);

        // If not handled externally, open the dialog
        if (!args.Handled)
        {
            var dialog = new TrackVersionCompareDialog(_versioningService, _trackId, _trackName, versionA.Id, versionB.Id);
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }

    private void QuickCompareButton_Click(object sender, RoutedEventArgs e)
    {
        PerformQuickCompare();
    }

    #endregion
}

#region Support Classes

/// <summary>
/// Display item for version combo boxes.
/// </summary>
public class VersionDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Event args for requesting a detailed version comparison.
/// </summary>
public class VersionComparisonRequestedEventArgs : EventArgs
{
    public string TrackId { get; }
    public string TrackName { get; }
    public string VersionAId { get; }
    public string VersionBId { get; }
    public bool Handled { get; set; }

    public VersionComparisonRequestedEventArgs(string trackId, string trackName, string versionAId, string versionBId)
    {
        TrackId = trackId;
        TrackName = trackName;
        VersionAId = versionAId;
        VersionBId = versionBId;
    }
}

#endregion
