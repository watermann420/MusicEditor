// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the ZoomToolbar control.

using System;
using System.Windows;
using System.Windows.Controls;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A reusable zoom toolbar control providing zoom presets, sliders, and navigation features
/// for use in Arrangement and Piano Roll views.
/// </summary>
public partial class ZoomToolbar : UserControl
{
    private ZoomToolbarViewModel? _viewModel;

    #region Dependency Properties

    /// <summary>
    /// Identifies the HorizontalZoom dependency property.
    /// </summary>
    public static readonly DependencyProperty HorizontalZoomProperty =
        DependencyProperty.Register(
            nameof(HorizontalZoom),
            typeof(double),
            typeof(ZoomToolbar),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHorizontalZoomChanged));

    /// <summary>
    /// Identifies the VerticalZoom dependency property.
    /// </summary>
    public static readonly DependencyProperty VerticalZoomProperty =
        DependencyProperty.Register(
            nameof(VerticalZoom),
            typeof(double),
            typeof(ZoomToolbar),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnVerticalZoomChanged));

    /// <summary>
    /// Identifies the ScrollOffset dependency property.
    /// </summary>
    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(
            nameof(ScrollOffset),
            typeof(double),
            typeof(ZoomToolbar),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnScrollOffsetChanged));

    /// <summary>
    /// Identifies the VisibleBeats dependency property.
    /// </summary>
    public static readonly DependencyProperty VisibleBeatsProperty =
        DependencyProperty.Register(
            nameof(VisibleBeats),
            typeof(double),
            typeof(ZoomToolbar),
            new FrameworkPropertyMetadata(64.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnVisibleBeatsChanged));

    /// <summary>
    /// Identifies the TotalProjectLength dependency property.
    /// </summary>
    public static readonly DependencyProperty TotalProjectLengthProperty =
        DependencyProperty.Register(
            nameof(TotalProjectLength),
            typeof(double),
            typeof(ZoomToolbar),
            new PropertyMetadata(256.0, OnTotalProjectLengthChanged));

    /// <summary>
    /// Identifies the FollowPlayhead dependency property.
    /// </summary>
    public static readonly DependencyProperty FollowPlayheadProperty =
        DependencyProperty.Register(
            nameof(FollowPlayhead),
            typeof(bool),
            typeof(ZoomToolbar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnFollowPlayheadChanged));

    /// <summary>
    /// Identifies the BeatsPerBar dependency property.
    /// </summary>
    public static readonly DependencyProperty BeatsPerBarProperty =
        DependencyProperty.Register(
            nameof(BeatsPerBar),
            typeof(int),
            typeof(ZoomToolbar),
            new PropertyMetadata(4, OnBeatsPerBarChanged));

    /// <summary>
    /// Identifies the PlayheadPosition dependency property.
    /// </summary>
    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(
            nameof(PlayheadPosition),
            typeof(double),
            typeof(ZoomToolbar),
            new PropertyMetadata(0.0, OnPlayheadPositionChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the horizontal zoom level (1.0 = 100%).
    /// </summary>
    public double HorizontalZoom
    {
        get => (double)GetValue(HorizontalZoomProperty);
        set => SetValue(HorizontalZoomProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical zoom level (1.0 = 100%).
    /// </summary>
    public double VerticalZoom
    {
        get => (double)GetValue(VerticalZoomProperty);
        set => SetValue(VerticalZoomProperty, value);
    }

    /// <summary>
    /// Gets or sets the scroll offset in beats.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of visible beats.
    /// </summary>
    public double VisibleBeats
    {
        get => (double)GetValue(VisibleBeatsProperty);
        set => SetValue(VisibleBeatsProperty, value);
    }

    /// <summary>
    /// Gets or sets the total project length in beats.
    /// </summary>
    public double TotalProjectLength
    {
        get => (double)GetValue(TotalProjectLengthProperty);
        set => SetValue(TotalProjectLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the view follows the playhead.
    /// </summary>
    public bool FollowPlayhead
    {
        get => (bool)GetValue(FollowPlayheadProperty);
        set => SetValue(FollowPlayheadProperty, value);
    }

    /// <summary>
    /// Gets or sets the beats per bar (time signature).
    /// </summary>
    public int BeatsPerBar
    {
        get => (int)GetValue(BeatsPerBarProperty);
        set => SetValue(BeatsPerBarProperty, value);
    }

    /// <summary>
    /// Gets or sets the current playhead position in beats.
    /// </summary>
    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    /// <summary>
    /// Gets the ViewModel.
    /// </summary>
    public ZoomToolbarViewModel? ViewModel => _viewModel;

    #endregion

    #region Events

    /// <summary>
    /// Raised when the horizontal zoom changes.
    /// </summary>
    public event EventHandler<double>? HorizontalZoomChanged;

    /// <summary>
    /// Raised when the vertical zoom changes.
    /// </summary>
    public event EventHandler<double>? VerticalZoomChanged;

    /// <summary>
    /// Raised when the scroll offset should change.
    /// </summary>
    public event EventHandler<double>? ScrollOffsetChanged;

    /// <summary>
    /// Raised when the visible beats should change.
    /// </summary>
    public event EventHandler<double>? VisibleBeatsChanged;

    /// <summary>
    /// Raised when a zoom preset is applied.
    /// </summary>
    public event EventHandler<ZoomPreset>? PresetApplied;

    /// <summary>
    /// Raised when Go to Playhead is requested.
    /// </summary>
    public event EventHandler? GoToPlayheadRequested;

    #endregion

    #region Constructor

    public ZoomToolbar()
    {
        InitializeComponent();

        _viewModel = new ZoomToolbarViewModel();
        DataContext = _viewModel;

        // Subscribe to ViewModel events
        _viewModel.HorizontalZoomChanged += (s, e) =>
        {
            HorizontalZoom = e;
            HorizontalZoomChanged?.Invoke(this, e);
        };

        _viewModel.VerticalZoomChanged += (s, e) =>
        {
            VerticalZoom = e;
            VerticalZoomChanged?.Invoke(this, e);
        };

        _viewModel.ScrollOffsetChanged += (s, e) =>
        {
            ScrollOffset = e;
            ScrollOffsetChanged?.Invoke(this, e);
        };

        _viewModel.VisibleBeatsChanged += (s, e) =>
        {
            VisibleBeats = e;
            VisibleBeatsChanged?.Invoke(this, e);
        };

        _viewModel.PresetApplied += (s, e) =>
        {
            PresetApplied?.Invoke(this, e);
        };

        _viewModel.GoToPlayheadRequested += (s, e) =>
        {
            GoToPlayheadRequested?.Invoke(this, e);
        };

        Loaded += ZoomToolbar_Loaded;
        Unloaded += ZoomToolbar_Unloaded;
    }

    #endregion

    #region Lifecycle

    private void ZoomToolbar_Loaded(object sender, RoutedEventArgs e)
    {
        // Sync initial values
        if (_viewModel != null)
        {
            _viewModel.HorizontalZoom = HorizontalZoom;
            _viewModel.VerticalZoom = VerticalZoom;
            _viewModel.ScrollOffset = ScrollOffset;
            _viewModel.VisibleBeats = VisibleBeats;
            _viewModel.TotalProjectLength = TotalProjectLength;
            _viewModel.FollowPlayhead = FollowPlayhead;
            _viewModel.BeatsPerBar = BeatsPerBar;
            _viewModel.PlayheadPosition = PlayheadPosition;
        }
    }

    private void ZoomToolbar_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel?.Dispose();
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnHorizontalZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomToolbar toolbar && toolbar._viewModel != null)
        {
            toolbar._viewModel.HorizontalZoom = (double)e.NewValue;
        }
    }

    private static void OnVerticalZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomToolbar toolbar && toolbar._viewModel != null)
        {
            toolbar._viewModel.VerticalZoom = (double)e.NewValue;
        }
    }

    private static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomToolbar toolbar && toolbar._viewModel != null)
        {
            toolbar._viewModel.ScrollOffset = (double)e.NewValue;
        }
    }

    private static void OnVisibleBeatsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomToolbar toolbar && toolbar._viewModel != null)
        {
            toolbar._viewModel.VisibleBeats = (double)e.NewValue;
        }
    }

    private static void OnTotalProjectLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomToolbar toolbar && toolbar._viewModel != null)
        {
            toolbar._viewModel.TotalProjectLength = (double)e.NewValue;
        }
    }

    private static void OnFollowPlayheadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomToolbar toolbar && toolbar._viewModel != null)
        {
            toolbar._viewModel.FollowPlayhead = (bool)e.NewValue;
        }
    }

    private static void OnBeatsPerBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomToolbar toolbar && toolbar._viewModel != null)
        {
            toolbar._viewModel.BeatsPerBar = (int)e.NewValue;
        }
    }

    private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomToolbar toolbar && toolbar._viewModel != null)
        {
            toolbar._viewModel.PlayheadPosition = (double)e.NewValue;
        }
    }

    #endregion

    #region Event Handlers

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _viewModel == null) return;

        if (PresetComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagValue)
        {
            if (Enum.TryParse<ZoomPreset>(tagValue, out var preset))
            {
                _viewModel.ApplyPresetCommand.Execute(preset);
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the selection range for Fit Selection preset.
    /// </summary>
    /// <param name="start">Selection start in beats.</param>
    /// <param name="end">Selection end in beats.</param>
    public void SetSelection(double start, double end)
    {
        _viewModel?.SetSelection(start, end);
    }

    /// <summary>
    /// Clears the selection.
    /// </summary>
    public void ClearSelection()
    {
        _viewModel?.ClearSelection();
    }

    /// <summary>
    /// Sets the view width for zoom calculations.
    /// </summary>
    /// <param name="width">View width in pixels.</param>
    public void SetViewWidth(double width)
    {
        _viewModel?.SetViewWidth(width);
    }

    /// <summary>
    /// Applies a zoom preset programmatically.
    /// </summary>
    /// <param name="preset">The preset to apply.</param>
    public void ApplyPreset(ZoomPreset preset)
    {
        _viewModel?.ApplyPresetCommand.Execute(preset);

        // Update ComboBox selection
        for (int i = 0; i < PresetComboBox.Items.Count; i++)
        {
            if (PresetComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is string tagValue &&
                Enum.TryParse<ZoomPreset>(tagValue, out var itemPreset) &&
                itemPreset == preset)
            {
                PresetComboBox.SelectedIndex = i;
                break;
            }
        }
    }

    /// <summary>
    /// Scrolls to show the playhead.
    /// </summary>
    public void GoToPlayhead()
    {
        _viewModel?.GoToPlayheadCommand.Execute(null);
    }

    #endregion
}
