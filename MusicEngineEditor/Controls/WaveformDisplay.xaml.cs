using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying audio waveforms with zoom, scroll, and selection support.
/// </summary>
public partial class WaveformDisplay : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty WaveformDataProperty =
        DependencyProperty.Register(nameof(WaveformData), typeof(WaveformData), typeof(WaveformDisplay),
            new PropertyMetadata(null, OnWaveformDataChanged));

    public static readonly DependencyProperty SamplesPerPixelProperty =
        DependencyProperty.Register(nameof(SamplesPerPixel), typeof(int), typeof(WaveformDisplay),
            new PropertyMetadata(256, OnRenderPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(long), typeof(WaveformDisplay),
            new PropertyMetadata(0L, OnRenderPropertyChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(long), typeof(WaveformDisplay),
            new PropertyMetadata(-1L, OnPlayheadPositionChanged));

    public static readonly DependencyProperty SelectionStartProperty =
        DependencyProperty.Register(nameof(SelectionStart), typeof(long), typeof(WaveformDisplay),
            new PropertyMetadata(-1L, OnSelectionChanged));

    public static readonly DependencyProperty SelectionEndProperty =
        DependencyProperty.Register(nameof(SelectionEnd), typeof(long), typeof(WaveformDisplay),
            new PropertyMetadata(-1L, OnSelectionChanged));

    public static readonly DependencyProperty WaveformColorProperty =
        DependencyProperty.Register(nameof(WaveformColor), typeof(Color), typeof(WaveformDisplay),
            new PropertyMetadata(Color.FromRgb(0x4C, 0xAF, 0x50), OnColorPropertyChanged));

    public static readonly DependencyProperty WaveformColorRightProperty =
        DependencyProperty.Register(nameof(WaveformColorRight), typeof(Color), typeof(WaveformDisplay),
            new PropertyMetadata(Color.FromRgb(0x21, 0x96, 0xF3), OnColorPropertyChanged));

    public static readonly DependencyProperty PlayheadColorProperty =
        DependencyProperty.Register(nameof(PlayheadColor), typeof(Color), typeof(WaveformDisplay),
            new PropertyMetadata(Colors.White, OnColorPropertyChanged));

    public static readonly DependencyProperty SelectionColorProperty =
        DependencyProperty.Register(nameof(SelectionColor), typeof(Color), typeof(WaveformDisplay),
            new PropertyMetadata(Color.FromArgb(0x40, 0x4B, 0x6E, 0xAF), OnColorPropertyChanged));

    public static readonly DependencyProperty DisplayModeProperty =
        DependencyProperty.Register(nameof(DisplayMode), typeof(WaveformDisplayMode), typeof(WaveformDisplay),
            new PropertyMetadata(WaveformDisplayMode.Mixed, OnRenderPropertyChanged));

    public static readonly DependencyProperty ShowPlayheadProperty =
        DependencyProperty.Register(nameof(ShowPlayhead), typeof(bool), typeof(WaveformDisplay),
            new PropertyMetadata(true, OnPlayheadVisibilityChanged));

    public static readonly DependencyProperty ShowCenterLineProperty =
        DependencyProperty.Register(nameof(ShowCenterLine), typeof(bool), typeof(WaveformDisplay),
            new PropertyMetadata(true, OnCenterLineVisibilityChanged));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(WaveformDisplay),
            new PropertyMetadata(false, OnIsLoadingChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the waveform data to display.
    /// </summary>
    public WaveformData? WaveformData
    {
        get => (WaveformData?)GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of samples per pixel (zoom level).
    /// </summary>
    public int SamplesPerPixel
    {
        get => (int)GetValue(SamplesPerPixelProperty);
        set => SetValue(SamplesPerPixelProperty, Math.Max(1, value));
    }

    /// <summary>
    /// Gets or sets the scroll offset in samples.
    /// </summary>
    public long ScrollOffset
    {
        get => (long)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets the playhead position in samples (-1 to hide).
    /// </summary>
    public long PlayheadPosition
    {
        get => (long)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection start in samples (-1 for no selection).
    /// </summary>
    public long SelectionStart
    {
        get => (long)GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection end in samples (-1 for no selection).
    /// </summary>
    public long SelectionEnd
    {
        get => (long)GetValue(SelectionEndProperty);
        set => SetValue(SelectionEndProperty, value);
    }

    /// <summary>
    /// Gets or sets the waveform fill color for the left/mono channel.
    /// </summary>
    public Color WaveformColor
    {
        get => (Color)GetValue(WaveformColorProperty);
        set => SetValue(WaveformColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the waveform fill color for the right channel.
    /// </summary>
    public Color WaveformColorRight
    {
        get => (Color)GetValue(WaveformColorRightProperty);
        set => SetValue(WaveformColorRightProperty, value);
    }

    /// <summary>
    /// Gets or sets the playhead color.
    /// </summary>
    public Color PlayheadColor
    {
        get => (Color)GetValue(PlayheadColorProperty);
        set => SetValue(PlayheadColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection highlight color.
    /// </summary>
    public Color SelectionColor
    {
        get => (Color)GetValue(SelectionColorProperty);
        set => SetValue(SelectionColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the display mode for stereo waveforms.
    /// </summary>
    public WaveformDisplayMode DisplayMode
    {
        get => (WaveformDisplayMode)GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the playhead.
    /// </summary>
    public bool ShowPlayhead
    {
        get => (bool)GetValue(ShowPlayheadProperty);
        set => SetValue(ShowPlayheadProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the center line.
    /// </summary>
    public bool ShowCenterLine
    {
        get => (bool)GetValue(ShowCenterLineProperty);
        set => SetValue(ShowCenterLineProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the control is currently loading data.
    /// </summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the user clicks to set the playhead position.
    /// </summary>
    public event EventHandler<WaveformPositionEventArgs>? PlayheadRequested;

    /// <summary>
    /// Event raised when the selection changes.
    /// </summary>
    public event EventHandler<WaveformSelectionEventArgs>? SelectionChanged;

    /// <summary>
    /// Event raised when zoom changes.
    /// </summary>
    public event EventHandler<WaveformZoomEventArgs>? ZoomChanged;

    #endregion

    #region Fields

    private bool _isDragging;
    private bool _isSelecting;
    private Point _dragStartPoint;
    private long _selectionStartSample;
    private WaveformPeak[][]? _cachedPeaks;
    private int _cachedSamplesPerPixel;
    private bool _needsRender;

    #endregion

    public WaveformDisplay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateColorBrushes();
        InvalidateWaveform();
    }

    #region Property Changed Callbacks

    private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformDisplay control)
        {
            control._cachedPeaks = null;
            control.InvalidateWaveform();
        }
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformDisplay control)
        {
            control.InvalidateWaveform();
        }
    }

    private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformDisplay control)
        {
            control.UpdatePlayhead();
        }
    }

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformDisplay control)
        {
            control.UpdateSelection();
        }
    }

    private static void OnColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformDisplay control)
        {
            control.UpdateColorBrushes();
            control.InvalidateWaveform();
        }
    }

    private static void OnPlayheadVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformDisplay control)
        {
            control.UpdatePlayhead();
        }
    }

    private static void OnCenterLineVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformDisplay control)
        {
            control.CenterLine.Visibility = control.ShowCenterLine ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformDisplay control)
        {
            control.LoadingOverlay.Visibility = control.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Invalidates and re-renders the waveform.
    /// </summary>
    public void InvalidateWaveform()
    {
        if (_needsRender) return;
        _needsRender = true;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            _needsRender = false;
            RenderWaveform();
        }));
    }

    private void RenderWaveform()
    {
        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        // Update center line
        CenterLine.X1 = 0;
        CenterLine.X2 = width;
        CenterLine.Y1 = height / 2;
        CenterLine.Y2 = height / 2;

        // Check for data
        if (WaveformData == null || !WaveformData.IsLoaded)
        {
            WaveformPath.Data = null;
            WaveformPathRight.Data = null;
            NoDataText.Visibility = IsLoading ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        NoDataText.Visibility = Visibility.Collapsed;

        // Get peaks for current zoom level
        var peaks = GetPeaksForDisplay();
        if (peaks == null || peaks.Length == 0 || peaks[0].Length == 0)
        {
            WaveformPath.Data = null;
            WaveformPathRight.Data = null;
            return;
        }

        // Render based on display mode
        switch (DisplayMode)
        {
            case WaveformDisplayMode.Mixed:
                RenderMixedWaveform(peaks, width, height);
                break;
            case WaveformDisplayMode.StereoOverlay:
                RenderStereoOverlay(peaks, width, height);
                break;
            case WaveformDisplayMode.StereoStacked:
                RenderStereoStacked(peaks, width, height);
                break;
            case WaveformDisplayMode.LeftOnly:
                RenderSingleChannel(peaks, 0, WaveformPath, width, height);
                WaveformPathRight.Data = null;
                WaveformPathRight.Visibility = Visibility.Collapsed;
                break;
            case WaveformDisplayMode.RightOnly:
                if (peaks.Length > 1)
                {
                    RenderSingleChannel(peaks, 1, WaveformPath, width, height);
                }
                WaveformPathRight.Data = null;
                WaveformPathRight.Visibility = Visibility.Collapsed;
                break;
        }

        // Update playhead and selection
        UpdatePlayhead();
        UpdateSelection();
    }

    private WaveformPeak[][]? GetPeaksForDisplay()
    {
        if (WaveformData == null || !WaveformData.IsLoaded)
            return null;

        var width = (int)WaveformCanvas.ActualWidth;
        if (width <= 0)
            return null;

        // Check cache
        if (_cachedPeaks != null && _cachedSamplesPerPixel == SamplesPerPixel)
            return _cachedPeaks;

        // Calculate visible range
        var startSample = (int)ScrollOffset;
        var visibleSamples = width * SamplesPerPixel;
        var endSample = Math.Min(startSample + visibleSamples, WaveformData.SamplesPerChannel);

        // Get peaks for visible range
        _cachedPeaks = WaveformData.GetPeaksForRange(startSample, endSample, width);
        _cachedSamplesPerPixel = SamplesPerPixel;

        return _cachedPeaks;
    }

    private void RenderMixedWaveform(WaveformPeak[][] peaks, double width, double height)
    {
        var mixedPeaks = peaks.Length == 1 ? peaks[0] : MixPeaks(peaks);
        RenderPeaksToPath(mixedPeaks, WaveformPath, width, height, 0, height);
        WaveformPathRight.Visibility = Visibility.Collapsed;
    }

    private void RenderStereoOverlay(WaveformPeak[][] peaks, double width, double height)
    {
        if (peaks.Length >= 1)
        {
            RenderPeaksToPath(peaks[0], WaveformPath, width, height, 0, height);
        }

        if (peaks.Length >= 2)
        {
            RenderPeaksToPath(peaks[1], WaveformPathRight, width, height, 0, height);
            WaveformPathRight.Visibility = Visibility.Visible;
        }
        else
        {
            WaveformPathRight.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderStereoStacked(WaveformPeak[][] peaks, double width, double height)
    {
        var halfHeight = height / 2;

        if (peaks.Length >= 1)
        {
            RenderPeaksToPath(peaks[0], WaveformPath, width, halfHeight, 0, halfHeight);
        }

        if (peaks.Length >= 2)
        {
            RenderPeaksToPath(peaks[1], WaveformPathRight, width, halfHeight, halfHeight, halfHeight);
            WaveformPathRight.Visibility = Visibility.Visible;
            Canvas.SetTop(WaveformPathRight, halfHeight);
        }
        else
        {
            WaveformPathRight.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderSingleChannel(WaveformPeak[][] peaks, int channel, Path path, double width, double height)
    {
        if (channel < peaks.Length)
        {
            RenderPeaksToPath(peaks[channel], path, width, height, 0, height);
        }
    }

    private void RenderPeaksToPath(WaveformPeak[] peaks, Path path, double width, double height, double yOffset, double availableHeight)
    {
        if (peaks.Length == 0)
        {
            path.Data = null;
            return;
        }

        var centerY = yOffset + availableHeight / 2;
        var halfHeight = availableHeight / 2 * 0.95; // 95% to leave some margin

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            // Draw filled polygon: top line forward, bottom line backward
            var topPoints = new Point[peaks.Length];
            var bottomPoints = new Point[peaks.Length];

            for (var i = 0; i < peaks.Length; i++)
            {
                var x = (double)i * width / peaks.Length;
                var peak = peaks[i];

                var topY = centerY - peak.Max * halfHeight;
                var bottomY = centerY - peak.Min * halfHeight;

                topPoints[i] = new Point(x, topY);
                bottomPoints[i] = new Point(x, bottomY);
            }

            // Start at first top point
            context.BeginFigure(topPoints[0], true, true);

            // Draw top line
            for (var i = 1; i < topPoints.Length; i++)
            {
                context.LineTo(topPoints[i], true, false);
            }

            // Draw bottom line (reversed)
            for (var i = bottomPoints.Length - 1; i >= 0; i--)
            {
                context.LineTo(bottomPoints[i], true, false);
            }
        }

        geometry.Freeze();
        path.Data = geometry;
    }

    private static WaveformPeak[] MixPeaks(WaveformPeak[][] peaks)
    {
        if (peaks.Length == 0)
            return [];

        if (peaks.Length == 1)
            return peaks[0];

        var length = peaks[0].Length;
        var mixed = new WaveformPeak[length];

        for (var i = 0; i < length; i++)
        {
            var min = float.MaxValue;
            var max = float.MinValue;

            foreach (var channelPeaks in peaks)
            {
                if (i < channelPeaks.Length)
                {
                    min = Math.Min(min, channelPeaks[i].Min);
                    max = Math.Max(max, channelPeaks[i].Max);
                }
            }

            mixed[i] = new WaveformPeak(min, max);
        }

        return mixed;
    }

    private void UpdatePlayhead()
    {
        if (!ShowPlayhead || PlayheadPosition < 0 || WaveformData == null || !WaveformData.IsLoaded)
        {
            Playhead.Visibility = Visibility.Collapsed;
            return;
        }

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            Playhead.Visibility = Visibility.Collapsed;
            return;
        }

        // Calculate playhead position in pixels
        var relativeSample = PlayheadPosition - ScrollOffset;
        var x = (double)relativeSample / SamplesPerPixel;

        if (x >= 0 && x <= width)
        {
            Canvas.SetLeft(Playhead, x - 1);
            Canvas.SetTop(Playhead, 0);
            Playhead.Height = height;
            Playhead.Visibility = Visibility.Visible;
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateSelection()
    {
        if (SelectionStart < 0 || SelectionEnd < 0 || SelectionStart >= SelectionEnd)
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            return;
        }

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            return;
        }

        // Calculate selection rectangle
        var startX = (double)(SelectionStart - ScrollOffset) / SamplesPerPixel;
        var endX = (double)(SelectionEnd - ScrollOffset) / SamplesPerPixel;

        startX = Math.Max(0, startX);
        endX = Math.Min(width, endX);

        if (endX > startX)
        {
            Canvas.SetLeft(SelectionRect, startX);
            Canvas.SetTop(SelectionRect, 0);
            SelectionRect.Width = endX - startX;
            SelectionRect.Height = height;
            SelectionRect.Visibility = Visibility.Visible;
        }
        else
        {
            SelectionRect.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateColorBrushes()
    {
        WaveformPath.Fill = new SolidColorBrush(WaveformColor);
        WaveformPath.Stroke = new SolidColorBrush(LightenColor(WaveformColor, 0.2f));

        WaveformPathRight.Fill = new SolidColorBrush(WaveformColorRight);
        WaveformPathRight.Stroke = new SolidColorBrush(LightenColor(WaveformColorRight, 0.2f));

        Playhead.Fill = new SolidColorBrush(PlayheadColor);

        SelectionRect.Fill = new SolidColorBrush(SelectionColor);
        SelectionRect.Stroke = new SolidColorBrush(Color.FromArgb(255, SelectionColor.R, SelectionColor.G, SelectionColor.B));
    }

    private static Color LightenColor(Color color, float amount)
    {
        return Color.FromArgb(
            color.A,
            (byte)Math.Min(255, color.R + 255 * amount),
            (byte)Math.Min(255, color.G + 255 * amount),
            (byte)Math.Min(255, color.B + 255 * amount));
    }

    #endregion

    #region Mouse Interaction

    private void WaveformCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WaveformData == null || !WaveformData.IsLoaded)
            return;

        var position = e.GetPosition(WaveformCanvas);
        _dragStartPoint = position;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Start selection
            _isSelecting = true;
            _selectionStartSample = PixelToSample(position.X);
            SelectionStart = _selectionStartSample;
            SelectionEnd = _selectionStartSample;
        }
        else
        {
            // Request playhead position
            var sample = PixelToSample(position.X);
            PlayheadRequested?.Invoke(this, new WaveformPositionEventArgs(sample));
        }

        _isDragging = true;
        WaveformCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void WaveformCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting)
        {
            var position = e.GetPosition(WaveformCanvas);
            var endSample = PixelToSample(position.X);

            SelectionStart = Math.Min(_selectionStartSample, endSample);
            SelectionEnd = Math.Max(_selectionStartSample, endSample);

            if (SelectionEnd - SelectionStart < 10)
            {
                // Selection too small, clear it
                SelectionStart = -1;
                SelectionEnd = -1;
            }

            SelectionChanged?.Invoke(this, new WaveformSelectionEventArgs(SelectionStart, SelectionEnd));
        }

        _isDragging = false;
        _isSelecting = false;
        WaveformCanvas.ReleaseMouseCapture();
    }

    private void WaveformCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;

        var position = e.GetPosition(WaveformCanvas);

        if (_isSelecting)
        {
            var endSample = PixelToSample(position.X);
            SelectionStart = Math.Min(_selectionStartSample, endSample);
            SelectionEnd = Math.Max(_selectionStartSample, endSample);
        }
    }

    private void WaveformCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var position = e.GetPosition(WaveformCanvas);
        var sampleAtMouse = PixelToSample(position.X);

        // Zoom
        var oldSamplesPerPixel = SamplesPerPixel;
        var newSamplesPerPixel = e.Delta > 0
            ? Math.Max(1, SamplesPerPixel / 2)
            : Math.Min(8192, SamplesPerPixel * 2);

        if (newSamplesPerPixel != oldSamplesPerPixel)
        {
            SamplesPerPixel = newSamplesPerPixel;

            // Adjust scroll to keep the mouse position stable
            var newScrollOffset = sampleAtMouse - (long)(position.X * newSamplesPerPixel);
            ScrollOffset = Math.Max(0, newScrollOffset);

            ZoomChanged?.Invoke(this, new WaveformZoomEventArgs(oldSamplesPerPixel, newSamplesPerPixel));
            _cachedPeaks = null; // Invalidate cache
        }

        e.Handled = true;
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _cachedPeaks = null; // Invalidate cache on resize
        InvalidateWaveform();
    }

    private long PixelToSample(double x)
    {
        return ScrollOffset + (long)(x * SamplesPerPixel);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Loads waveform data from a file asynchronously.
    /// </summary>
    public async Task LoadFromFileAsync(string filePath, IWaveformService waveformService, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        LoadingText.Text = "Loading waveform...";

        try
        {
            waveformService.LoadProgress += OnLoadProgress;
            var data = await waveformService.LoadFromFileAsync(filePath, cancellationToken);
            WaveformData = data;
        }
        finally
        {
            waveformService.LoadProgress -= OnLoadProgress;
            IsLoading = false;
        }
    }

    private void OnLoadProgress(object? sender, WaveformProgressEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LoadingProgress.IsIndeterminate = false;
            LoadingProgress.Value = e.Progress;
            LoadingText.Text = e.StatusMessage;
        });
    }

    /// <summary>
    /// Scrolls to show the specified sample position.
    /// </summary>
    public void ScrollToSample(long samplePosition)
    {
        var width = WaveformCanvas.ActualWidth;
        if (width <= 0)
            return;

        var visibleSamples = (long)(width * SamplesPerPixel);
        var newOffset = samplePosition - visibleSamples / 2;
        ScrollOffset = Math.Max(0, newOffset);
    }

    /// <summary>
    /// Zooms to fit the entire waveform in the view.
    /// </summary>
    public void ZoomToFit()
    {
        if (WaveformData == null || !WaveformData.IsLoaded)
            return;

        var width = WaveformCanvas.ActualWidth;
        if (width <= 0)
            return;

        var totalSamples = WaveformData.SamplesPerChannel;
        SamplesPerPixel = Math.Max(1, totalSamples / (int)width);
        ScrollOffset = 0;
        _cachedPeaks = null;
        InvalidateWaveform();
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        SelectionStart = -1;
        SelectionEnd = -1;
    }

    #endregion
}

/// <summary>
/// Display modes for stereo waveforms.
/// </summary>
public enum WaveformDisplayMode
{
    /// <summary>
    /// Mix both channels into a single waveform.
    /// </summary>
    Mixed,

    /// <summary>
    /// Overlay both channels on top of each other.
    /// </summary>
    StereoOverlay,

    /// <summary>
    /// Stack channels vertically (left on top, right on bottom).
    /// </summary>
    StereoStacked,

    /// <summary>
    /// Display only the left channel.
    /// </summary>
    LeftOnly,

    /// <summary>
    /// Display only the right channel.
    /// </summary>
    RightOnly
}

/// <summary>
/// Event arguments for waveform position events.
/// </summary>
public class WaveformPositionEventArgs : EventArgs
{
    /// <summary>
    /// Gets the sample position.
    /// </summary>
    public long SamplePosition { get; }

    public WaveformPositionEventArgs(long samplePosition)
    {
        SamplePosition = samplePosition;
    }
}

/// <summary>
/// Event arguments for waveform selection events.
/// </summary>
public class WaveformSelectionEventArgs : EventArgs
{
    /// <summary>
    /// Gets the selection start in samples.
    /// </summary>
    public long Start { get; }

    /// <summary>
    /// Gets the selection end in samples.
    /// </summary>
    public long End { get; }

    /// <summary>
    /// Gets whether there is a valid selection.
    /// </summary>
    public bool HasSelection => Start >= 0 && End > Start;

    public WaveformSelectionEventArgs(long start, long end)
    {
        Start = start;
        End = end;
    }
}

/// <summary>
/// Event arguments for waveform zoom events.
/// </summary>
public class WaveformZoomEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous samples per pixel value.
    /// </summary>
    public int OldSamplesPerPixel { get; }

    /// <summary>
    /// Gets the new samples per pixel value.
    /// </summary>
    public int NewSamplesPerPixel { get; }

    public WaveformZoomEventArgs(int oldSamplesPerPixel, int newSamplesPerPixel)
    {
        OldSamplesPerPixel = oldSamplesPerPixel;
        NewSamplesPerPixel = newSamplesPerPixel;
    }
}
