// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog for comparing track versions with visual diff view.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for comparing two track versions and displaying detailed differences.
/// </summary>
public partial class TrackVersionCompareDialog : Window
{
    #region Fields

    private readonly ITrackVersioningService _versioningService;
    private readonly string _trackId;
    private readonly List<TrackVersion> _versions;
    private VersionComparison? _currentComparison;

    // Color definitions
    private static readonly SolidColorBrush AddedColor = new(Color.FromRgb(0x00, 0xCC, 0x66));
    private static readonly SolidColorBrush RemovedColor = new(Color.FromRgb(0xFF, 0x47, 0x57));
    private static readonly SolidColorBrush ModifiedColor = new(Color.FromRgb(0xFF, 0x98, 0x00));
    private static readonly SolidColorBrush UnchangedColor = new(Color.FromRgb(0x6F, 0x73, 0x7A));

    private static readonly SolidColorBrush AddedBackground = new(Color.FromArgb(0x20, 0x00, 0xCC, 0x66));
    private static readonly SolidColorBrush RemovedBackground = new(Color.FromArgb(0x20, 0xFF, 0x47, 0x57));
    private static readonly SolidColorBrush ModifiedBackground = new(Color.FromArgb(0x20, 0xFF, 0x98, 0x00));

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new TrackVersionCompareDialog for the specified track.
    /// </summary>
    /// <param name="versioningService">The versioning service.</param>
    /// <param name="trackId">The track ID to compare versions for.</param>
    /// <param name="trackName">The display name of the track.</param>
    public TrackVersionCompareDialog(ITrackVersioningService versioningService, string trackId, string trackName)
    {
        _versioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
        _trackId = trackId ?? throw new ArgumentNullException(nameof(trackId));
        _versions = _versioningService.GetVersions(trackId).ToList();

        InitializeComponent();

        TrackNameText.Text = $"Track: {trackName}";
        LoadVersions();
    }

    /// <summary>
    /// Creates a new TrackVersionCompareDialog with preselected versions.
    /// </summary>
    public TrackVersionCompareDialog(ITrackVersioningService versioningService, string trackId, string trackName,
        string? versionAId, string? versionBId) : this(versioningService, trackId, trackName)
    {
        if (!string.IsNullOrEmpty(versionAId))
        {
            var versionA = _versions.FirstOrDefault(v => v.Id == versionAId);
            if (versionA != null)
                VersionAComboBox.SelectedItem = versionA;
        }

        if (!string.IsNullOrEmpty(versionBId))
        {
            var versionB = _versions.FirstOrDefault(v => v.Id == versionBId);
            if (versionB != null)
                VersionBComboBox.SelectedItem = versionB;
        }

        if (VersionAComboBox.SelectedItem != null && VersionBComboBox.SelectedItem != null)
        {
            PerformComparison();
        }
    }

    #endregion

    #region Private Methods

    private void LoadVersions()
    {
        VersionAComboBox.ItemsSource = _versions;
        VersionBComboBox.ItemsSource = _versions;

        // Pre-select if we have at least 2 versions
        if (_versions.Count >= 2)
        {
            VersionAComboBox.SelectedIndex = 0;
            VersionBComboBox.SelectedIndex = 1;
        }
        else if (_versions.Count == 1)
        {
            VersionAComboBox.SelectedIndex = 0;
        }
    }

    private void UpdateCompareButtonState()
    {
        var versionA = VersionAComboBox.SelectedItem as TrackVersion;
        var versionB = VersionBComboBox.SelectedItem as TrackVersion;

        CompareButton.IsEnabled = versionA != null && versionB != null && versionA.Id != versionB.Id;
    }

    private void PerformComparison()
    {
        var versionA = VersionAComboBox.SelectedItem as TrackVersion;
        var versionB = VersionBComboBox.SelectedItem as TrackVersion;

        if (versionA == null || versionB == null)
            return;

        try
        {
            _currentComparison = _versioningService.CompareVersions(_trackId, versionA.Id, versionB.Id);
            UpdateSummary();
            ApplyFilter();
            UpdateButtonStates();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to compare versions:\n{ex.Message}", "Comparison Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateSummary()
    {
        if (_currentComparison == null)
        {
            AddedCountText.Text = "0";
            RemovedCountText.Text = "0";
            ModifiedCountText.Text = "0";
            UnchangedCountText.Text = "0";
            return;
        }

        AddedCountText.Text = _currentComparison.TotalAdded.ToString();
        RemovedCountText.Text = _currentComparison.TotalRemoved.ToString();
        ModifiedCountText.Text = _currentComparison.TotalModified.ToString();
        UnchangedCountText.Text = _currentComparison.TotalUnchanged.ToString();
    }

    private void UpdateButtonStates()
    {
        bool hasComparison = _currentComparison != null;
        ExportDiffButton.IsEnabled = hasComparison;
        CopyToClipboardButton.IsEnabled = hasComparison;
        RestoreVersionAButton.IsEnabled = hasComparison;
    }

    private void ApplyFilter()
    {
        DiffContentPanel.Children.Clear();

        if (_currentComparison == null)
        {
            EmptyStatePanel.Visibility = Visibility.Visible;
            NoDifferencesPanel.Visibility = Visibility.Collapsed;
            DiffScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyStatePanel.Visibility = Visibility.Collapsed;

        // Check if there are any differences
        if (!_currentComparison.HasDifferences &&
            _currentComparison.TotalAdded == 0 &&
            _currentComparison.TotalRemoved == 0 &&
            _currentComparison.TotalModified == 0)
        {
            NoDifferencesPanel.Visibility = Visibility.Visible;
            DiffScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        NoDifferencesPanel.Visibility = Visibility.Collapsed;
        DiffScrollViewer.Visibility = Visibility.Visible;

        // Determine which categories to show based on filter
        bool showAll = FilterAllRadio.IsChecked == true;
        bool showNotes = FilterNotesRadio.IsChecked == true || showAll;
        bool showClips = FilterClipsRadio.IsChecked == true || showAll;
        bool showEffects = FilterEffectsRadio.IsChecked == true || showAll;
        bool showAutomation = FilterAutomationRadio.IsChecked == true || showAll;
        bool showParameters = FilterParametersRadio.IsChecked == true || showAll;

        // Add sections for each category
        if (showNotes && _currentComparison.NoteDiffs != null)
        {
            AddNotesDiffSection(_currentComparison.NoteDiffs);
        }

        if (showClips && _currentComparison.ClipDiffs != null)
        {
            AddClipsDiffSection(_currentComparison.ClipDiffs);
        }

        if (showEffects && _currentComparison.EffectDiffs != null)
        {
            AddEffectsDiffSection(_currentComparison.EffectDiffs);
        }

        if (showAutomation && _currentComparison.AutomationDiffs != null)
        {
            AddAutomationDiffSection(_currentComparison.AutomationDiffs);
        }

        if (showParameters && _currentComparison.ParameterDiffs != null)
        {
            AddParametersDiffSection(_currentComparison.ParameterDiffs);
        }
    }

    private void AddNotesDiffSection(NoteComparison notes)
    {
        if (notes.Added.Count == 0 && notes.Removed.Count == 0 && notes.Modified.Count == 0)
            return;

        var expander = CreateCategoryExpander("MIDI Notes",
            notes.Added.Count, notes.Removed.Count, notes.Modified.Count);

        var contentPanel = new StackPanel();

        // Added notes
        foreach (var note in notes.Added)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Added,
                $"{note.NoteName} (Note {note.NoteNumber})",
                $"Added at beat {note.NewStartBeat:F2}, duration {note.NewDuration:F2}, velocity {note.NewVelocity}"));
        }

        // Removed notes
        foreach (var note in notes.Removed)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Removed,
                $"{note.NoteName} (Note {note.NoteNumber})",
                $"Removed from beat {note.OldStartBeat:F2}, was duration {note.OldDuration:F2}, velocity {note.OldVelocity}"));
        }

        // Modified notes
        foreach (var note in notes.Modified)
        {
            var changes = string.Join(", ", note.Changes);
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Modified,
                $"{note.NoteName} (Note {note.NoteNumber}) at beat {note.OldStartBeat:F2}",
                changes));
        }

        expander.Content = contentPanel;
        DiffContentPanel.Children.Add(expander);
    }

    private void AddClipsDiffSection(ClipComparison clips)
    {
        if (clips.Added.Count == 0 && clips.Removed.Count == 0 && clips.Modified.Count == 0)
            return;

        var expander = CreateCategoryExpander("Clips / Regions",
            clips.Added.Count, clips.Removed.Count, clips.Modified.Count);

        var contentPanel = new StackPanel();

        // Added clips
        foreach (var clip in clips.Added)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Added,
                clip.ClipName,
                $"Added at beat {clip.NewStartBeat:F2}, duration {clip.NewDuration:F2}"));
        }

        // Removed clips
        foreach (var clip in clips.Removed)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Removed,
                clip.ClipName,
                $"Removed from beat {clip.OldStartBeat:F2}, was duration {clip.OldDuration:F2}"));
        }

        // Modified clips
        foreach (var clip in clips.Modified)
        {
            var changes = string.Join(", ", clip.Changes);
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Modified,
                clip.ClipName,
                changes));
        }

        expander.Content = contentPanel;
        DiffContentPanel.Children.Add(expander);
    }

    private void AddEffectsDiffSection(EffectComparison effects)
    {
        if (effects.Added.Count == 0 && effects.Removed.Count == 0 && effects.Modified.Count == 0)
            return;

        var expander = CreateCategoryExpander("Effects",
            effects.Added.Count, effects.Removed.Count, effects.Modified.Count);

        var contentPanel = new StackPanel();

        // Added effects
        foreach (var effect in effects.Added)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Added,
                effect.EffectName,
                $"New effect added (Plugin: {effect.PluginId})"));
        }

        // Removed effects
        foreach (var effect in effects.Removed)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Removed,
                effect.EffectName,
                $"Effect removed (Plugin: {effect.PluginId})"));
        }

        // Modified effects
        foreach (var effect in effects.Modified)
        {
            var paramChangesText = new StringBuilder();
            foreach (var paramChange in effect.ParameterChanges.Take(5)) // Limit to first 5
            {
                paramChangesText.Append($"{paramChange.ParameterName}: {paramChange.OldValue ?? "N/A"} -> {paramChange.NewValue ?? "N/A"}, ");
            }
            if (effect.ParameterChanges.Count > 5)
            {
                paramChangesText.Append($"and {effect.ParameterChanges.Count - 5} more...");
            }

            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Modified,
                effect.EffectName,
                paramChangesText.ToString().TrimEnd(',', ' ')));

            // Add nested parameter changes
            if (effect.ParameterChanges.Count > 0)
            {
                var nestedPanel = new StackPanel { Margin = new Thickness(24, 0, 0, 8) };
                foreach (var param in effect.ParameterChanges)
                {
                    nestedPanel.Children.Add(CreateParameterChangeItem(param));
                }
                contentPanel.Children.Add(nestedPanel);
            }
        }

        expander.Content = contentPanel;
        DiffContentPanel.Children.Add(expander);
    }

    private void AddAutomationDiffSection(AutomationComparison automation)
    {
        if (automation.Added.Count == 0 && automation.Removed.Count == 0 && automation.Modified.Count == 0)
            return;

        var expander = CreateCategoryExpander("Automation",
            automation.Added.Count, automation.Removed.Count, automation.Modified.Count);

        var contentPanel = new StackPanel();

        // Added lanes
        foreach (var lane in automation.Added)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Added,
                lane.ParameterName,
                $"New automation lane with {lane.NewPointCount} points"));
        }

        // Removed lanes
        foreach (var lane in automation.Removed)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Removed,
                lane.ParameterName,
                $"Automation lane removed (had {lane.OldPointCount} points)"));
        }

        // Modified lanes
        foreach (var lane in automation.Modified)
        {
            var addedPoints = lane.PointChanges.Count(p => p.ChangeType == PointChangeType.Added);
            var removedPoints = lane.PointChanges.Count(p => p.ChangeType == PointChangeType.Removed);
            var modifiedPoints = lane.PointChanges.Count(p => p.ChangeType == PointChangeType.Modified);

            var description = $"Points: {lane.OldPointCount} -> {lane.NewPointCount} ";
            if (addedPoints > 0) description += $"(+{addedPoints} added) ";
            if (removedPoints > 0) description += $"(-{removedPoints} removed) ";
            if (modifiedPoints > 0) description += $"(~{modifiedPoints} modified)";

            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Modified,
                lane.ParameterName,
                description.Trim()));
        }

        expander.Content = contentPanel;
        DiffContentPanel.Children.Add(expander);
    }

    private void AddParametersDiffSection(ParameterComparison parameters)
    {
        if (parameters.Added.Count == 0 && parameters.Removed.Count == 0 && parameters.Modified.Count == 0)
            return;

        var expander = CreateCategoryExpander("Track Parameters",
            parameters.Added.Count, parameters.Removed.Count, parameters.Modified.Count);

        var contentPanel = new StackPanel();

        // Added parameters
        foreach (var param in parameters.Added)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Added,
                param.ParameterName,
                $"New value: {param.NewValue}"));
        }

        // Removed parameters
        foreach (var param in parameters.Removed)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Removed,
                param.ParameterName,
                $"Was: {param.OldValue}"));
        }

        // Modified parameters
        foreach (var param in parameters.Modified)
        {
            contentPanel.Children.Add(CreateDiffItem(
                DiffType.Modified,
                param.ParameterName,
                $"{param.OldValue} -> {param.NewValue}"));
        }

        expander.Content = contentPanel;
        DiffContentPanel.Children.Add(expander);
    }

    private Expander CreateCategoryExpander(string title, int added, int removed, int modified)
    {
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

        headerPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        headerPanel.Children.Add(new TextBlock
        {
            Text = " - ",
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            VerticalAlignment = VerticalAlignment.Center
        });

        if (added > 0)
        {
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"+{added}",
                Foreground = AddedColor,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (removed > 0)
        {
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"-{removed}",
                Foreground = RemovedColor,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (modified > 0)
        {
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"~{modified}",
                Foreground = ModifiedColor,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return new Expander
        {
            Header = headerPanel,
            IsExpanded = true,
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41)),
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(8)
        };
    }

    private Border CreateDiffItem(DiffType type, string title, string description)
    {
        var (indicator, indicatorColor, background) = type switch
        {
            DiffType.Added => ("+", AddedColor, AddedBackground),
            DiffType.Removed => ("-", RemovedColor, RemovedBackground),
            DiffType.Modified => ("~", ModifiedColor, ModifiedBackground),
            _ => (" ", UnchangedColor, Brushes.Transparent)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Indicator
        var indicatorText = new TextBlock
        {
            Text = indicator,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = indicatorColor,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(indicatorText, 0);
        grid.Children.Add(indicatorText);

        // Content
        var contentPanel = new StackPanel();
        contentPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = Brushes.White
        });

        if (!string.IsNullOrEmpty(description))
        {
            contentPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        Grid.SetColumn(contentPanel, 1);
        grid.Children.Add(contentPanel);

        return new Border
        {
            Background = background,
            BorderBrush = indicatorColor,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 1),
            Child = grid
        };
    }

    private Border CreateParameterChangeItem(ParameterDiffItem param)
    {
        var text = $"{param.ParameterName}: ";
        if (param.OldValue != null && param.NewValue != null)
        {
            text += $"{param.OldValue} -> {param.NewValue}";
        }
        else if (param.NewValue != null)
        {
            text += $"(new) {param.NewValue}";
        }
        else if (param.OldValue != null)
        {
            text += $"{param.OldValue} (removed)";
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0x98, 0x00)),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 2),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0))
            }
        };
    }

    private string GenerateDiffText()
    {
        if (_currentComparison == null)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"Track Version Comparison");
        sb.AppendLine($"========================");
        sb.AppendLine($"Version A: {_currentComparison.Version1?.Name} ({_currentComparison.Version1?.CreatedAt:g})");
        sb.AppendLine($"Version B: {_currentComparison.Version2?.Name} ({_currentComparison.Version2?.CreatedAt:g})");
        sb.AppendLine($"Compared at: {_currentComparison.ComparedAt:g}");
        sb.AppendLine();
        sb.AppendLine($"Summary: +{_currentComparison.TotalAdded} added, -{_currentComparison.TotalRemoved} removed, ~{_currentComparison.TotalModified} modified");
        sb.AppendLine();

        // Notes
        if (_currentComparison.NoteDiffs != null)
        {
            AppendNoteDiffs(sb, _currentComparison.NoteDiffs);
        }

        // Clips
        if (_currentComparison.ClipDiffs != null)
        {
            AppendClipDiffs(sb, _currentComparison.ClipDiffs);
        }

        // Effects
        if (_currentComparison.EffectDiffs != null)
        {
            AppendEffectDiffs(sb, _currentComparison.EffectDiffs);
        }

        // Automation
        if (_currentComparison.AutomationDiffs != null)
        {
            AppendAutomationDiffs(sb, _currentComparison.AutomationDiffs);
        }

        // Parameters
        if (_currentComparison.ParameterDiffs != null)
        {
            AppendParameterDiffs(sb, _currentComparison.ParameterDiffs);
        }

        return sb.ToString();
    }

    private static void AppendNoteDiffs(StringBuilder sb, NoteComparison notes)
    {
        if (notes.Added.Count == 0 && notes.Removed.Count == 0 && notes.Modified.Count == 0)
            return;

        sb.AppendLine("MIDI Notes:");
        sb.AppendLine("-----------");
        foreach (var note in notes.Added)
            sb.AppendLine($"  + {note.NoteName} at beat {note.NewStartBeat:F2} (velocity {note.NewVelocity})");
        foreach (var note in notes.Removed)
            sb.AppendLine($"  - {note.NoteName} at beat {note.OldStartBeat:F2}");
        foreach (var note in notes.Modified)
            sb.AppendLine($"  ~ {note.NoteName} at beat {note.OldStartBeat:F2}: {string.Join(", ", note.Changes)}");
        sb.AppendLine();
    }

    private static void AppendClipDiffs(StringBuilder sb, ClipComparison clips)
    {
        if (clips.Added.Count == 0 && clips.Removed.Count == 0 && clips.Modified.Count == 0)
            return;

        sb.AppendLine("Clips / Regions:");
        sb.AppendLine("----------------");
        foreach (var clip in clips.Added)
            sb.AppendLine($"  + {clip.ClipName} at beat {clip.NewStartBeat:F2}");
        foreach (var clip in clips.Removed)
            sb.AppendLine($"  - {clip.ClipName} at beat {clip.OldStartBeat:F2}");
        foreach (var clip in clips.Modified)
            sb.AppendLine($"  ~ {clip.ClipName}: {string.Join(", ", clip.Changes)}");
        sb.AppendLine();
    }

    private static void AppendEffectDiffs(StringBuilder sb, EffectComparison effects)
    {
        if (effects.Added.Count == 0 && effects.Removed.Count == 0 && effects.Modified.Count == 0)
            return;

        sb.AppendLine("Effects:");
        sb.AppendLine("--------");
        foreach (var effect in effects.Added)
            sb.AppendLine($"  + {effect.EffectName}");
        foreach (var effect in effects.Removed)
            sb.AppendLine($"  - {effect.EffectName}");
        foreach (var effect in effects.Modified)
        {
            sb.AppendLine($"  ~ {effect.EffectName}:");
            foreach (var param in effect.ParameterChanges)
                sb.AppendLine($"      {param.ParameterName}: {param.OldValue ?? "N/A"} -> {param.NewValue ?? "N/A"}");
        }
        sb.AppendLine();
    }

    private static void AppendAutomationDiffs(StringBuilder sb, AutomationComparison automation)
    {
        if (automation.Added.Count == 0 && automation.Removed.Count == 0 && automation.Modified.Count == 0)
            return;

        sb.AppendLine("Automation:");
        sb.AppendLine("-----------");
        foreach (var lane in automation.Added)
            sb.AppendLine($"  + {lane.ParameterName} ({lane.NewPointCount} points)");
        foreach (var lane in automation.Removed)
            sb.AppendLine($"  - {lane.ParameterName} ({lane.OldPointCount} points)");
        foreach (var lane in automation.Modified)
            sb.AppendLine($"  ~ {lane.ParameterName}: {lane.OldPointCount} -> {lane.NewPointCount} points");
        sb.AppendLine();
    }

    private static void AppendParameterDiffs(StringBuilder sb, ParameterComparison parameters)
    {
        if (parameters.Added.Count == 0 && parameters.Removed.Count == 0 && parameters.Modified.Count == 0)
            return;

        sb.AppendLine("Track Parameters:");
        sb.AppendLine("-----------------");
        foreach (var param in parameters.Added)
            sb.AppendLine($"  + {param.ParameterName}: {param.NewValue}");
        foreach (var param in parameters.Removed)
            sb.AppendLine($"  - {param.ParameterName}: {param.OldValue}");
        foreach (var param in parameters.Modified)
            sb.AppendLine($"  ~ {param.ParameterName}: {param.OldValue} -> {param.NewValue}");
        sb.AppendLine();
    }

    #endregion

    #region Event Handlers

    private void VersionAComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCompareButtonState();
    }

    private void VersionBComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCompareButtonState();
    }

    private void SwapVersions_Click(object sender, RoutedEventArgs e)
    {
        var tempA = VersionAComboBox.SelectedItem;
        VersionAComboBox.SelectedItem = VersionBComboBox.SelectedItem;
        VersionBComboBox.SelectedItem = tempA;

        if (_currentComparison != null)
        {
            PerformComparison();
        }
    }

    private void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        PerformComparison();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void ExportDiff_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new SaveFileDialog
        {
            Title = "Export Version Diff",
            Filter = "Text Files|*.txt|All Files|*.*",
            FileName = $"version_diff_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(saveDialog.FileName, GenerateDiffText());
                MessageBox.Show($"Diff exported to:\n{saveDialog.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export diff:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(GenerateDiffText());
            MessageBox.Show("Diff copied to clipboard.", "Copy Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard:\n{ex.Message}", "Copy Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreVersionA_Click(object sender, RoutedEventArgs e)
    {
        var versionA = VersionAComboBox.SelectedItem as TrackVersion;
        if (versionA == null)
            return;

        var result = MessageBox.Show(
            $"Restore track to version '{versionA.Name}'?\n\nThis will switch the active version to this version.",
            "Restore Version",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _versioningService.SwitchVersion(_trackId, versionA.Id);
                MessageBox.Show($"Restored to version '{versionA.Name}'.", "Restore Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore version:\n{ex.Message}", "Restore Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion
}

/// <summary>
/// Type of difference for visual styling.
/// </summary>
internal enum DiffType
{
    Added,
    Removed,
    Modified,
    Unchanged
}
