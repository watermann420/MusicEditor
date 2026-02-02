// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Track header control for arrangement view with mute/solo and collapse functionality.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MusicEngineEditor.Controls.Arrangement;

/// <summary>
/// Control for displaying track header with name, color indicator, and mute/solo controls.
/// </summary>
public partial class TrackHeaderControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty TrackNameProperty =
        DependencyProperty.Register(nameof(TrackName), typeof(string), typeof(TrackHeaderControl),
            new PropertyMetadata("Track", OnTrackNameChanged));

    public static readonly DependencyProperty TrackColorProperty =
        DependencyProperty.Register(nameof(TrackColor), typeof(Color), typeof(TrackHeaderControl),
            new PropertyMetadata(Color.FromRgb(0x00, 0xD9, 0xFF), OnTrackColorChanged));

    public static readonly DependencyProperty IsMutedProperty =
        DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(TrackHeaderControl),
            new PropertyMetadata(false, OnIsMutedChanged));

    public static readonly DependencyProperty IsSoloProperty =
        DependencyProperty.Register(nameof(IsSolo), typeof(bool), typeof(TrackHeaderControl),
            new PropertyMetadata(false, OnIsSoloChanged));

    public static readonly DependencyProperty IsCollapsedProperty =
        DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(TrackHeaderControl),
            new PropertyMetadata(false, OnIsCollapsedChanged));

    public static readonly DependencyProperty TrackIndexProperty =
        DependencyProperty.Register(nameof(TrackIndex), typeof(int), typeof(TrackHeaderControl),
            new PropertyMetadata(0));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the track name.
    /// </summary>
    public string TrackName
    {
        get => (string)GetValue(TrackNameProperty);
        set => SetValue(TrackNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the track color.
    /// </summary>
    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the track is muted.
    /// </summary>
    public bool IsMuted
    {
        get => (bool)GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the track is soloed.
    /// </summary>
    public bool IsSolo
    {
        get => (bool)GetValue(IsSoloProperty);
        set => SetValue(IsSoloProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the track is collapsed.
    /// </summary>
    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>
    /// Gets or sets the track index.
    /// </summary>
    public int TrackIndex
    {
        get => (int)GetValue(TrackIndexProperty);
        set => SetValue(TrackIndexProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the mute state changes.
    /// </summary>
    public event EventHandler<bool>? MuteChanged;

    /// <summary>
    /// Event raised when the solo state changes.
    /// </summary>
    public event EventHandler<bool>? SoloChanged;

    /// <summary>
    /// Event raised when the collapsed state changes.
    /// </summary>
    public event EventHandler<bool>? CollapsedChanged;

    /// <summary>
    /// Event raised when the track is selected.
    /// </summary>
    public event EventHandler? TrackSelected;

    #endregion

    #region Fields

    private readonly SolidColorBrush _defaultButtonBackground = new(Color.FromRgb(0x25, 0x25, 0x25));
    private readonly SolidColorBrush _muteActiveBrush = new(Color.FromRgb(0xFF, 0x47, 0x57));
    private readonly SolidColorBrush _soloActiveBrush = new(Color.FromRgb(0xFF, 0xB8, 0x00));
    private readonly SolidColorBrush _activeTextBrush = new(Colors.White);
    private readonly SolidColorBrush _inactiveTextBrush = new(Color.FromRgb(0x80, 0x80, 0x80));

    #endregion

    public TrackHeaderControl()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    #region Property Changed Callbacks

    private static void OnTrackNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackHeaderControl control)
        {
            control.TrackNameText.Text = e.NewValue as string ?? "Track";
        }
    }

    private static void OnTrackColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackHeaderControl control && e.NewValue is Color color)
        {
            control.TrackColorRect.Fill = new SolidColorBrush(color);
        }
    }

    private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackHeaderControl control)
        {
            control.UpdateMuteButton();
        }
    }

    private static void OnIsSoloChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackHeaderControl control)
        {
            control.UpdateSoloButton();
        }
    }

    private static void OnIsCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackHeaderControl control && e.NewValue is bool isCollapsed)
        {
            control.CollapseToggle.IsChecked = isCollapsed;
        }
    }

    #endregion

    #region Event Handlers

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TrackSelected?.Invoke(this, EventArgs.Empty);
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        IsMuted = !IsMuted;
        MuteChanged?.Invoke(this, IsMuted);
    }

    private void SoloButton_Click(object sender, RoutedEventArgs e)
    {
        IsSolo = !IsSolo;
        SoloChanged?.Invoke(this, IsSolo);
    }

    private void CollapseToggle_Checked(object sender, RoutedEventArgs e)
    {
        IsCollapsed = true;
        CollapsedChanged?.Invoke(this, true);
    }

    private void CollapseToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        IsCollapsed = false;
        CollapsedChanged?.Invoke(this, false);
    }

    #endregion

    #region Visual Updates

    private void UpdateMuteButton()
    {
        if (IsMuted)
        {
            MuteButton.Background = _muteActiveBrush;
            MuteButton.Foreground = _activeTextBrush;
        }
        else
        {
            MuteButton.Background = _defaultButtonBackground;
            MuteButton.Foreground = _inactiveTextBrush;
        }
    }

    private void UpdateSoloButton()
    {
        if (IsSolo)
        {
            SoloButton.Background = _soloActiveBrush;
            SoloButton.Foreground = _activeTextBrush;
        }
        else
        {
            SoloButton.Background = _defaultButtonBackground;
            SoloButton.Foreground = _inactiveTextBrush;
        }
    }

    #endregion
}
