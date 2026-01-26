// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents an effect slot specific to a clip.
/// </summary>
public class ClipEffectSlot : INotifyPropertyChanged
{
    private int _index;
    private string _effectType = string.Empty;
    private string _displayName = "Empty";
    private string _category = string.Empty;
    private string _effectColor = "#6B7280";
    private bool _isEmpty = true;
    private bool _isBypassed;
    private float _mix = 1.0f;
    private bool _isSelected;

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    public string EffectType
    {
        get => _effectType;
        set { _effectType = value; OnPropertyChanged(); }
    }

    public string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); }
    }

    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); }
    }

    public string EffectColor
    {
        get => _effectColor;
        set { _effectColor = value; OnPropertyChanged(); }
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set { _isEmpty = value; OnPropertyChanged(); }
    }

    public bool IsBypassed
    {
        get => _isBypassed;
        set { _isBypassed = value; OnPropertyChanged(); }
    }

    public float Mix
    {
        get => _mix;
        set { _mix = Math.Clamp(value, 0f, 1f); OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the effect parameters.
    /// </summary>
    public ObservableCollection<EffectParameter> Parameters { get; } = new();

    public ClipEffectSlot(int index)
    {
        _index = index;
    }

    public ClipEffectSlot(int index, string effectType, string displayName) : this(index)
    {
        LoadEffect(effectType, displayName);
    }

    public void LoadEffect(string effectType, string displayName)
    {
        EffectType = effectType;
        DisplayName = displayName;
        IsEmpty = false;
        IsBypassed = false;
        Mix = 1.0f;
        Parameters.Clear();
        SetColorByCategory(effectType);
    }

    public void ClearEffect()
    {
        EffectType = string.Empty;
        DisplayName = "Empty";
        Category = string.Empty;
        EffectColor = "#6B7280";
        IsEmpty = true;
        IsBypassed = false;
        Mix = 1.0f;
        Parameters.Clear();
    }

    public ClipEffectSlot Clone(int newIndex)
    {
        var clone = new ClipEffectSlot(newIndex)
        {
            EffectType = EffectType,
            DisplayName = DisplayName,
            Category = Category,
            EffectColor = EffectColor,
            IsEmpty = IsEmpty,
            IsBypassed = IsBypassed,
            Mix = Mix
        };

        foreach (var param in Parameters)
        {
            clone.Parameters.Add(new EffectParameter(param.Name, param.Value, param.Minimum, param.Maximum)
            {
                DisplayFormat = param.DisplayFormat,
                Unit = param.Unit
            });
        }

        return clone;
    }

    private void SetColorByCategory(string effectType)
    {
        var typeLower = effectType.ToLowerInvariant();

        if (typeLower.Contains("reverb") || typeLower.Contains("delay"))
        {
            EffectColor = "#3B82F6";
            Category = "Time-Based";
        }
        else if (typeLower.Contains("compressor") || typeLower.Contains("limiter") || typeLower.Contains("gate"))
        {
            EffectColor = "#EF4444";
            Category = "Dynamics";
        }
        else if (typeLower.Contains("eq") || typeLower.Contains("filter"))
        {
            EffectColor = "#10B981";
            Category = "EQ/Filter";
        }
        else if (typeLower.Contains("chorus") || typeLower.Contains("flanger") || typeLower.Contains("phaser"))
        {
            EffectColor = "#8B5CF6";
            Category = "Modulation";
        }
        else if (typeLower.Contains("distortion") || typeLower.Contains("saturation"))
        {
            EffectColor = "#F59E0B";
            Category = "Distortion";
        }
        else
        {
            EffectColor = "#6B7280";
            Category = "Other";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Control for displaying and managing per-clip effect chains.
/// Supports adding, removing, reordering effects, parameter editing, and bypass toggle.
/// </summary>
public partial class ClipEffectsPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ClipIdProperty =
        DependencyProperty.Register(nameof(ClipId), typeof(string), typeof(ClipEffectsPanel),
            new PropertyMetadata(null, OnClipIdChanged));

    public static readonly DependencyProperty EffectSlotsProperty =
        DependencyProperty.Register(nameof(EffectSlots), typeof(ObservableCollection<ClipEffectSlot>),
            typeof(ClipEffectsPanel), new PropertyMetadata(null, OnEffectSlotsChanged));

    public static readonly DependencyProperty IsChainBypassedProperty =
        DependencyProperty.Register(nameof(IsChainBypassed), typeof(bool), typeof(ClipEffectsPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the clip ID this panel is editing.
    /// </summary>
    public string? ClipId
    {
        get => (string?)GetValue(ClipIdProperty);
        set => SetValue(ClipIdProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of effect slots.
    /// </summary>
    public ObservableCollection<ClipEffectSlot> EffectSlots
    {
        get => (ObservableCollection<ClipEffectSlot>)GetValue(EffectSlotsProperty);
        set => SetValue(EffectSlotsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the entire effect chain is bypassed.
    /// </summary>
    public bool IsChainBypassed
    {
        get => (bool)GetValue(IsChainBypassedProperty);
        set => SetValue(IsChainBypassedProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when add effect is requested.
    /// </summary>
    public event EventHandler<ClipEffectRequestedEventArgs>? AddEffectRequested;

    /// <summary>
    /// Raised when an effect is removed.
    /// </summary>
    public event EventHandler<ClipEffectSlotEventArgs>? EffectRemoved;

    /// <summary>
    /// Raised when an effect is selected for editing.
    /// </summary>
    public event EventHandler<ClipEffectSlotEventArgs>? EffectEditRequested;

    /// <summary>
    /// Raised when effects are copied.
    /// </summary>
    public event EventHandler<ClipEffectsCopiedEventArgs>? EffectsCopied;

    /// <summary>
    /// Raised when effects are pasted.
    /// </summary>
    public event EventHandler<ClipEffectsPastedEventArgs>? EffectsPasted;

    /// <summary>
    /// Raised when the effect chain is modified.
    /// </summary>
    public event EventHandler<ClipEffectsChangedEventArgs>? EffectsChanged;

    #endregion

    #region Fields

    private static ObservableCollection<ClipEffectSlot>? _clipboard;
    private ClipEffectSlot? _draggedSlot;

    #endregion

    public ClipEffectsPanel()
    {
        InitializeComponent();

        EffectSlots = new ObservableCollection<ClipEffectSlot>();
        InitializeDefaultSlots();
        EffectSlotsControl.ItemsSource = EffectSlots;

        EffectSlots.CollectionChanged += (_, _) => UpdateEffectCount();
    }

    #region Property Changed Callbacks

    private static void OnClipIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClipEffectsPanel panel)
        {
            panel.LoadEffectsForClip((string?)e.NewValue);
        }
    }

    private static void OnEffectSlotsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClipEffectsPanel panel)
        {
            panel.EffectSlotsControl.ItemsSource = panel.EffectSlots;
            panel.UpdateEffectCount();

            if (e.OldValue is ObservableCollection<ClipEffectSlot> oldCollection)
            {
                oldCollection.CollectionChanged -= panel.EffectSlots_CollectionChanged;
            }

            if (e.NewValue is ObservableCollection<ClipEffectSlot> newCollection)
            {
                newCollection.CollectionChanged += panel.EffectSlots_CollectionChanged;
            }
        }
    }

    private void EffectSlots_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateEffectCount();
    }

    #endregion

    #region Event Handlers

    private void AddEffect_Click(object sender, RoutedEventArgs e)
    {
        // Find first empty slot or create new one
        var targetSlot = EffectSlots.FirstOrDefault(s => s.IsEmpty);

        if (targetSlot == null)
        {
            targetSlot = new ClipEffectSlot(EffectSlots.Count);
            EffectSlots.Add(targetSlot);
        }

        AddEffectRequested?.Invoke(this, new ClipEffectRequestedEventArgs(ClipId, targetSlot));
    }

    private void RemoveEffect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ClipEffectSlot slot)
        {
            slot.ClearEffect();
            UpdateEffectCount();
            EffectRemoved?.Invoke(this, new ClipEffectSlotEventArgs(ClipId, slot));
            NotifyEffectsChanged();
        }
    }

    private void EditEffect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ClipEffectSlot slot && !slot.IsEmpty)
        {
            EffectEditRequested?.Invoke(this, new ClipEffectSlotEventArgs(ClipId, slot));
        }
    }

    private void EffectSlot_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ClipEffectSlot slot)
        {
            // Deselect all
            foreach (var s in EffectSlots)
            {
                s.IsSelected = false;
            }

            slot.IsSelected = true;

            // If empty, trigger add
            if (slot.IsEmpty)
            {
                AddEffectRequested?.Invoke(this, new ClipEffectRequestedEventArgs(ClipId, slot));
            }
        }
    }

    private void BypassAllToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsChainBypassed = BypassAllToggle.IsChecked == true;
        NotifyEffectsChanged();
    }

    private void CopyEffects_Click(object sender, RoutedEventArgs e)
    {
        // Copy non-empty effects to clipboard
        _clipboard = new ObservableCollection<ClipEffectSlot>();
        int index = 0;
        foreach (var slot in EffectSlots.Where(s => !s.IsEmpty))
        {
            _clipboard.Add(slot.Clone(index++));
        }

        EffectsCopied?.Invoke(this, new ClipEffectsCopiedEventArgs(ClipId, _clipboard.ToList()));
    }

    private void PasteEffects_Click(object sender, RoutedEventArgs e)
    {
        if (_clipboard == null || _clipboard.Count == 0) return;

        // Clear existing effects
        foreach (var slot in EffectSlots)
        {
            slot.ClearEffect();
        }

        // Paste from clipboard
        for (int i = 0; i < _clipboard.Count; i++)
        {
            if (i >= EffectSlots.Count)
            {
                EffectSlots.Add(new ClipEffectSlot(i));
            }

            var source = _clipboard[i];
            var target = EffectSlots[i];
            target.EffectType = source.EffectType;
            target.DisplayName = source.DisplayName;
            target.Category = source.Category;
            target.EffectColor = source.EffectColor;
            target.IsEmpty = source.IsEmpty;
            target.IsBypassed = source.IsBypassed;
            target.Mix = source.Mix;

            target.Parameters.Clear();
            foreach (var param in source.Parameters)
            {
                target.Parameters.Add(new EffectParameter(param.Name, param.Value, param.Minimum, param.Maximum)
                {
                    DisplayFormat = param.DisplayFormat,
                    Unit = param.Unit
                });
            }
        }

        UpdateEffectCount();
        EffectsPasted?.Invoke(this, new ClipEffectsPastedEventArgs(ClipId, EffectSlots.ToList()));
        NotifyEffectsChanged();
    }

    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ClipEffectSlot slot && !slot.IsEmpty)
        {
            _draggedSlot = slot;
            DragDrop.DoDragDrop(element, slot, DragDropEffects.Move);
            _draggedSlot = null;
        }
    }

    private void EffectSlot_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ClipEffectSlot)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void EffectSlot_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element &&
            element.DataContext is ClipEffectSlot targetSlot &&
            e.Data.GetData(typeof(ClipEffectSlot)) is ClipEffectSlot sourceSlot)
        {
            if (sourceSlot.Index != targetSlot.Index)
            {
                SwapSlots(sourceSlot, targetSlot);
                NotifyEffectsChanged();
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds an effect to the chain.
    /// </summary>
    public bool AddEffect(string effectType, string displayName)
    {
        var emptySlot = EffectSlots.FirstOrDefault(s => s.IsEmpty);
        if (emptySlot != null)
        {
            emptySlot.LoadEffect(effectType, displayName);
        }
        else
        {
            var newSlot = new ClipEffectSlot(EffectSlots.Count, effectType, displayName);
            EffectSlots.Add(newSlot);
        }

        UpdateEffectCount();
        NotifyEffectsChanged();
        return true;
    }

    /// <summary>
    /// Removes all effects from the chain.
    /// </summary>
    public void ClearAllEffects()
    {
        foreach (var slot in EffectSlots)
        {
            slot.ClearEffect();
        }
        UpdateEffectCount();
        NotifyEffectsChanged();
    }

    /// <summary>
    /// Copies effects to another clip.
    /// </summary>
    public void CopyEffectsTo(ClipEffectsPanel targetPanel)
    {
        targetPanel.ClearAllEffects();

        for (int i = 0; i < EffectSlots.Count; i++)
        {
            var source = EffectSlots[i];
            if (source.IsEmpty) continue;

            if (i >= targetPanel.EffectSlots.Count)
            {
                targetPanel.EffectSlots.Add(new ClipEffectSlot(i));
            }

            var target = targetPanel.EffectSlots[i];
            target.EffectType = source.EffectType;
            target.DisplayName = source.DisplayName;
            target.Category = source.Category;
            target.EffectColor = source.EffectColor;
            target.IsEmpty = source.IsEmpty;
            target.IsBypassed = source.IsBypassed;
            target.Mix = source.Mix;

            target.Parameters.Clear();
            foreach (var param in source.Parameters)
            {
                target.Parameters.Add(new EffectParameter(param.Name, param.Value, param.Minimum, param.Maximum)
                {
                    DisplayFormat = param.DisplayFormat,
                    Unit = param.Unit
                });
            }
        }

        targetPanel.UpdateEffectCount();
        targetPanel.NotifyEffectsChanged();
    }

    #endregion

    #region Private Methods

    private void InitializeDefaultSlots()
    {
        for (int i = 0; i < 4; i++)
        {
            EffectSlots.Add(new ClipEffectSlot(i));
        }
    }

    private void LoadEffectsForClip(string? clipId)
    {
        // Clear current effects when clip changes
        foreach (var slot in EffectSlots)
        {
            slot.ClearEffect();
        }
        UpdateEffectCount();
    }

    private void UpdateEffectCount()
    {
        var count = EffectSlots.Count(s => !s.IsEmpty);
        EffectCountText.Text = count.ToString();
        EffectCountBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SwapSlots(ClipEffectSlot source, ClipEffectSlot target)
    {
        // Swap effect data
        var tempType = target.EffectType;
        var tempName = target.DisplayName;
        var tempCategory = target.Category;
        var tempColor = target.EffectColor;
        var tempEmpty = target.IsEmpty;
        var tempBypassed = target.IsBypassed;
        var tempMix = target.Mix;

        if (source.IsEmpty)
        {
            target.ClearEffect();
        }
        else
        {
            target.EffectType = source.EffectType;
            target.DisplayName = source.DisplayName;
            target.Category = source.Category;
            target.EffectColor = source.EffectColor;
            target.IsEmpty = source.IsEmpty;
            target.IsBypassed = source.IsBypassed;
            target.Mix = source.Mix;
        }

        if (tempEmpty)
        {
            source.ClearEffect();
        }
        else
        {
            source.EffectType = tempType;
            source.DisplayName = tempName;
            source.Category = tempCategory;
            source.EffectColor = tempColor;
            source.IsEmpty = tempEmpty;
            source.IsBypassed = tempBypassed;
            source.Mix = tempMix;
        }
    }

    private void NotifyEffectsChanged()
    {
        EffectsChanged?.Invoke(this, new ClipEffectsChangedEventArgs(ClipId, EffectSlots.ToList()));
    }

    #endregion
}

#region Event Args

public class ClipEffectRequestedEventArgs : EventArgs
{
    public string? ClipId { get; }
    public ClipEffectSlot Slot { get; }

    public ClipEffectRequestedEventArgs(string? clipId, ClipEffectSlot slot)
    {
        ClipId = clipId;
        Slot = slot;
    }
}

public class ClipEffectSlotEventArgs : EventArgs
{
    public string? ClipId { get; }
    public ClipEffectSlot Slot { get; }

    public ClipEffectSlotEventArgs(string? clipId, ClipEffectSlot slot)
    {
        ClipId = clipId;
        Slot = slot;
    }
}

public class ClipEffectsCopiedEventArgs : EventArgs
{
    public string? ClipId { get; }
    public IReadOnlyList<ClipEffectSlot> Effects { get; }

    public ClipEffectsCopiedEventArgs(string? clipId, IReadOnlyList<ClipEffectSlot> effects)
    {
        ClipId = clipId;
        Effects = effects;
    }
}

public class ClipEffectsPastedEventArgs : EventArgs
{
    public string? ClipId { get; }
    public IReadOnlyList<ClipEffectSlot> Effects { get; }

    public ClipEffectsPastedEventArgs(string? clipId, IReadOnlyList<ClipEffectSlot> effects)
    {
        ClipId = clipId;
        Effects = effects;
    }
}

public class ClipEffectsChangedEventArgs : EventArgs
{
    public string? ClipId { get; }
    public IReadOnlyList<ClipEffectSlot> Effects { get; }

    public ClipEffectsChangedEventArgs(string? clipId, IReadOnlyList<ClipEffectSlot> effects)
    {
        ClipId = clipId;
        Effects = effects;
    }
}

#endregion
