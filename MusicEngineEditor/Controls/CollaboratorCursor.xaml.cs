// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Visual cursor control for displaying collaborator positions.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying a collaborator's cursor position with name label.
/// </summary>
public partial class CollaboratorCursor : UserControl
{
    private Storyboard? _pulseStoryboard;
    private bool _isPulsing;

    /// <summary>
    /// Dependency property for the peer ID.
    /// </summary>
    public static readonly DependencyProperty PeerIdProperty =
        DependencyProperty.Register(nameof(PeerId), typeof(Guid), typeof(CollaboratorCursor),
            new PropertyMetadata(Guid.Empty));

    /// <summary>
    /// Dependency property for the peer name.
    /// </summary>
    public static readonly DependencyProperty PeerNameProperty =
        DependencyProperty.Register(nameof(PeerName), typeof(string), typeof(CollaboratorCursor),
            new PropertyMetadata("Collaborator", OnPeerNameChanged));

    /// <summary>
    /// Dependency property for the cursor color.
    /// </summary>
    public static readonly DependencyProperty CursorColorProperty =
        DependencyProperty.Register(nameof(CursorColor), typeof(Color), typeof(CollaboratorCursor),
            new PropertyMetadata(Color.FromRgb(0x00, 0xD9, 0xFF), OnCursorColorChanged));

    /// <summary>
    /// Dependency property for showing the selection rectangle.
    /// </summary>
    public static readonly DependencyProperty ShowSelectionProperty =
        DependencyProperty.Register(nameof(ShowSelection), typeof(bool), typeof(CollaboratorCursor),
            new PropertyMetadata(false, OnShowSelectionChanged));

    /// <summary>
    /// Dependency property for the selection width.
    /// </summary>
    public static readonly DependencyProperty SelectionWidthProperty =
        DependencyProperty.Register(nameof(SelectionWidth), typeof(double), typeof(CollaboratorCursor),
            new PropertyMetadata(100.0, OnSelectionSizeChanged));

    /// <summary>
    /// Dependency property for the selection height.
    /// </summary>
    public static readonly DependencyProperty SelectionHeightProperty =
        DependencyProperty.Register(nameof(SelectionHeight), typeof(double), typeof(CollaboratorCursor),
            new PropertyMetadata(50.0, OnSelectionSizeChanged));

    /// <summary>
    /// Dependency property for the selection offset X.
    /// </summary>
    public static readonly DependencyProperty SelectionOffsetXProperty =
        DependencyProperty.Register(nameof(SelectionOffsetX), typeof(double), typeof(CollaboratorCursor),
            new PropertyMetadata(0.0, OnSelectionPositionChanged));

    /// <summary>
    /// Dependency property for the selection offset Y.
    /// </summary>
    public static readonly DependencyProperty SelectionOffsetYProperty =
        DependencyProperty.Register(nameof(SelectionOffsetY), typeof(double), typeof(CollaboratorCursor),
            new PropertyMetadata(20.0, OnSelectionPositionChanged));

    /// <summary>
    /// Dependency property for the active editing state.
    /// </summary>
    public static readonly DependencyProperty IsActivelyEditingProperty =
        DependencyProperty.Register(nameof(IsActivelyEditing), typeof(bool), typeof(CollaboratorCursor),
            new PropertyMetadata(false, OnIsActivelyEditingChanged));

    /// <summary>
    /// Gets or sets the peer ID.
    /// </summary>
    public Guid PeerId
    {
        get => (Guid)GetValue(PeerIdProperty);
        set => SetValue(PeerIdProperty, value);
    }

    /// <summary>
    /// Gets or sets the peer name displayed on the label.
    /// </summary>
    public string PeerName
    {
        get => (string)GetValue(PeerNameProperty);
        set => SetValue(PeerNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the cursor color.
    /// </summary>
    public Color CursorColor
    {
        get => (Color)GetValue(CursorColorProperty);
        set => SetValue(CursorColorProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the selection rectangle.
    /// </summary>
    public bool ShowSelection
    {
        get => (bool)GetValue(ShowSelectionProperty);
        set => SetValue(ShowSelectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection rectangle width.
    /// </summary>
    public double SelectionWidth
    {
        get => (double)GetValue(SelectionWidthProperty);
        set => SetValue(SelectionWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection rectangle height.
    /// </summary>
    public double SelectionHeight
    {
        get => (double)GetValue(SelectionHeightProperty);
        set => SetValue(SelectionHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection X offset from cursor.
    /// </summary>
    public double SelectionOffsetX
    {
        get => (double)GetValue(SelectionOffsetXProperty);
        set => SetValue(SelectionOffsetXProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection Y offset from cursor.
    /// </summary>
    public double SelectionOffsetY
    {
        get => (double)GetValue(SelectionOffsetYProperty);
        set => SetValue(SelectionOffsetYProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the peer is actively editing.
    /// </summary>
    public bool IsActivelyEditing
    {
        get => (bool)GetValue(IsActivelyEditingProperty);
        set => SetValue(IsActivelyEditingProperty, value);
    }

    /// <summary>
    /// Creates a new collaborator cursor control.
    /// </summary>
    public CollaboratorCursor()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Play fade-in animation
        if (TryFindResource("FadeInAnimation") is Storyboard fadeIn)
        {
            fadeIn.Begin(this);
        }
    }

    private static void OnPeerNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollaboratorCursor cursor && e.NewValue is string name)
        {
            cursor.NameText.Text = name;
        }
    }

    private static void OnCursorColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollaboratorCursor cursor && e.NewValue is Color color)
        {
            cursor.UpdateColors(color);
        }
    }

    private static void OnShowSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollaboratorCursor cursor && e.NewValue is bool show)
        {
            cursor.SelectionRect.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnSelectionSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollaboratorCursor cursor)
        {
            cursor.SelectionRect.Width = cursor.SelectionWidth;
            cursor.SelectionRect.Height = cursor.SelectionHeight;
        }
    }

    private static void OnSelectionPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollaboratorCursor cursor)
        {
            Canvas.SetLeft(cursor.SelectionRect, cursor.SelectionOffsetX);
            Canvas.SetTop(cursor.SelectionRect, cursor.SelectionOffsetY);
        }
    }

    private static void OnIsActivelyEditingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollaboratorCursor cursor && e.NewValue is bool isEditing)
        {
            if (isEditing)
            {
                cursor.StartPulseAnimation();
            }
            else
            {
                cursor.StopPulseAnimation();
            }
        }
    }

    /// <summary>
    /// Updates all color-dependent elements.
    /// </summary>
    /// <param name="color">The new color.</param>
    private void UpdateColors(Color color)
    {
        CursorFill.Color = color;
        NameBackground.Color = color;
        SelectionFill.Color = color;
        SelectionStroke.Color = color;
    }

    /// <summary>
    /// Sets the cursor position on the parent canvas.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    public void SetPosition(double x, double y)
    {
        Canvas.SetLeft(this, x);
        Canvas.SetTop(this, y);
    }

    /// <summary>
    /// Sets the selection rectangle bounds relative to the cursor.
    /// </summary>
    /// <param name="startX">Selection start X (relative to cursor).</param>
    /// <param name="startY">Selection start Y (relative to cursor).</param>
    /// <param name="width">Selection width.</param>
    /// <param name="height">Selection height.</param>
    public void SetSelectionBounds(double startX, double startY, double width, double height)
    {
        SelectionOffsetX = startX;
        SelectionOffsetY = startY;
        SelectionWidth = Math.Abs(width);
        SelectionHeight = Math.Abs(height);
        ShowSelection = true;
    }

    /// <summary>
    /// Hides the selection rectangle.
    /// </summary>
    public void HideSelection()
    {
        ShowSelection = false;
    }

    /// <summary>
    /// Starts the pulse animation for active editing.
    /// </summary>
    private void StartPulseAnimation()
    {
        if (_isPulsing) return;

        _pulseStoryboard = TryFindResource("PulseAnimation") as Storyboard;
        if (_pulseStoryboard != null)
        {
            _pulseStoryboard.Begin(this, true);
            _isPulsing = true;
        }
    }

    /// <summary>
    /// Stops the pulse animation.
    /// </summary>
    private void StopPulseAnimation()
    {
        if (!_isPulsing) return;

        _pulseStoryboard?.Stop(this);
        CursorTriangle.Opacity = 1.0;
        _isPulsing = false;
    }

    /// <summary>
    /// Creates a collaborator cursor from peer data.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="peerName">The peer name.</param>
    /// <param name="colorArgb">The color in ARGB format.</param>
    /// <returns>A new CollaboratorCursor instance.</returns>
    public static CollaboratorCursor Create(Guid peerId, string peerName, uint colorArgb)
    {
        var color = Color.FromArgb(
            (byte)((colorArgb >> 24) & 0xFF),
            (byte)((colorArgb >> 16) & 0xFF),
            (byte)((colorArgb >> 8) & 0xFF),
            (byte)(colorArgb & 0xFF)
        );

        return new CollaboratorCursor
        {
            PeerId = peerId,
            PeerName = peerName,
            CursorColor = color
        };
    }
}
