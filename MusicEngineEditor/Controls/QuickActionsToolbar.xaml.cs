// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Quick Actions Toolbar - Provides one-click access to common DAW operations.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a quick action button configuration.
/// </summary>
public class QuickActionItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public int Order { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Quick Actions Toolbar control for common DAW operations.
/// </summary>
public partial class QuickActionsToolbar : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty TransposeValueProperty =
        DependencyProperty.Register(nameof(TransposeValue), typeof(int), typeof(QuickActionsToolbar),
            new PropertyMetadata(0, OnTransposeValueChanged));

    /// <summary>
    /// Gets or sets the current transpose value in semitones.
    /// </summary>
    public int TransposeValue
    {
        get => (int)GetValue(TransposeValueProperty);
        set => SetValue(TransposeValueProperty, value);
    }

    private static void OnTransposeValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuickActionsToolbar toolbar)
        {
            toolbar.UpdateTransposeDisplay();
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when AI Master button is clicked.
    /// </summary>
    public event EventHandler? AIMasterRequested;

    /// <summary>
    /// Raised when Stem Split button is clicked.
    /// </summary>
    public event EventHandler? StemSplitRequested;

    /// <summary>
    /// Raised when a quantize operation is requested.
    /// </summary>
    public event EventHandler<double>? QuantizeRequested;

    /// <summary>
    /// Raised when quantize dialog is requested.
    /// </summary>
    public event EventHandler? QuantizeDialogRequested;

    /// <summary>
    /// Raised when transpose is applied.
    /// </summary>
    public event EventHandler<int>? TransposeRequested;

    /// <summary>
    /// Raised when duplicate is requested.
    /// </summary>
    public event EventHandler? DuplicateRequested;

    /// <summary>
    /// Raised when split at playhead is requested.
    /// </summary>
    public event EventHandler? SplitRequested;

    /// <summary>
    /// Raised when toolbar customization is requested.
    /// </summary>
    public event EventHandler? CustomizeRequested;

    /// <summary>
    /// Raised when toolbar visibility should be toggled.
    /// </summary>
    public event EventHandler? HideRequested;

    #endregion

    #region Private Fields

    private readonly ObservableCollection<QuickActionItem> _actions = new();
    private readonly List<QuickActionItem> _defaultActions;

    #endregion

    #region Constructor

    public QuickActionsToolbar()
    {
        InitializeComponent();

        // Define default actions
        _defaultActions = new List<QuickActionItem>
        {
            new() { Id = "ai_master", Name = "AI Master", Icon = "brain", Category = "AI", Order = 0 },
            new() { Id = "stem_split", Name = "Stem Split", Icon = "split", Category = "AI", Order = 1 },
            new() { Id = "quantize", Name = "Quantize", Icon = "grid", Category = "Edit", Order = 2 },
            new() { Id = "transpose", Name = "Transpose", Icon = "arrows", Category = "Edit", Order = 3 },
            new() { Id = "duplicate", Name = "Duplicate", Icon = "copy", Category = "Edit", Order = 4 },
            new() { Id = "split", Name = "Split", Icon = "scissors", Category = "Edit", Order = 5 }
        };

        LoadConfiguration();
    }

    #endregion

    #region Configuration

    private void LoadConfiguration()
    {
        // Load saved configuration or use defaults
        _actions.Clear();
        foreach (var action in _defaultActions)
        {
            _actions.Add(new QuickActionItem
            {
                Id = action.Id,
                Name = action.Name,
                Icon = action.Icon,
                Category = action.Category,
                Order = action.Order,
                IsVisible = true
            });
        }
    }

    /// <summary>
    /// Resets the toolbar to default configuration.
    /// </summary>
    public void ResetToDefaults()
    {
        TransposeValue = 0;
        LoadConfiguration();
    }

    /// <summary>
    /// Gets the collection of available quick actions.
    /// </summary>
    public ObservableCollection<QuickActionItem> Actions => _actions;

    #endregion

    #region UI Event Handlers

    private void AIMaster_Click(object sender, RoutedEventArgs e)
    {
        AIMasterRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StemSplit_Click(object sender, RoutedEventArgs e)
    {
        StemSplitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuantizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagValue)
        {
            if (double.TryParse(tagValue, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double strength))
            {
                QuantizeDropdownButton.IsChecked = false;
                QuantizeRequested?.Invoke(this, strength);
            }
        }
    }

    private void QuantizeDialog_Click(object sender, RoutedEventArgs e)
    {
        QuantizeDropdownButton.IsChecked = false;
        QuantizeDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TransposeDown_Click(object sender, RoutedEventArgs e)
    {
        TransposeValue = Math.Max(-24, TransposeValue - 1);
    }

    private void TransposeUp_Click(object sender, RoutedEventArgs e)
    {
        TransposeValue = Math.Min(24, TransposeValue + 1);
    }

    private void TransposeApply_Click(object sender, RoutedEventArgs e)
    {
        if (TransposeValue != 0)
        {
            TransposeRequested?.Invoke(this, TransposeValue);
            // Optionally reset after applying
            // TransposeValue = 0;
        }
    }

    private void UpdateTransposeDisplay()
    {
        if (TransposeValueText != null)
        {
            var value = TransposeValue;
            TransposeValueText.Text = value > 0 ? $"+{value}" : value.ToString();
        }
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        DuplicateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Split_Click(object sender, RoutedEventArgs e)
    {
        SplitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CustomizeToolbar_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomizeQuickActionsDialog(_actions)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            // Apply changes from dialog
            ApplyCustomization(dialog.GetConfiguration());
        }

        CustomizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Reset Quick Actions toolbar to default configuration?",
            "Reset to Defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ResetToDefaults();
        }
    }

    private void HideToolbar_Click(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Customization

    private void ApplyCustomization(List<QuickActionItem> configuration)
    {
        _actions.Clear();
        foreach (var item in configuration)
        {
            _actions.Add(item);
        }

        // Update visibility of buttons based on configuration
        UpdateButtonVisibility();
    }

    private void UpdateButtonVisibility()
    {
        // This would update the visibility of individual buttons
        // based on the _actions collection
        // For now, all buttons are visible by default
    }

    /// <summary>
    /// Shows or hides a specific action by ID.
    /// </summary>
    public void SetActionVisibility(string actionId, bool isVisible)
    {
        foreach (var action in _actions)
        {
            if (action.Id == actionId)
            {
                action.IsVisible = isVisible;
                break;
            }
        }
        UpdateButtonVisibility();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Programmatically triggers the AI Master action.
    /// </summary>
    public void TriggerAIMaster()
    {
        AIMasterRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Programmatically triggers quantize with specified strength.
    /// </summary>
    public void TriggerQuantize(double strength)
    {
        QuantizeRequested?.Invoke(this, strength);
    }

    /// <summary>
    /// Programmatically triggers transpose with specified semitones.
    /// </summary>
    public void TriggerTranspose(int semitones)
    {
        TransposeRequested?.Invoke(this, semitones);
    }

    /// <summary>
    /// Programmatically triggers duplicate action.
    /// </summary>
    public void TriggerDuplicate()
    {
        DuplicateRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Programmatically triggers split at playhead action.
    /// </summary>
    public void TriggerSplit()
    {
        SplitRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
