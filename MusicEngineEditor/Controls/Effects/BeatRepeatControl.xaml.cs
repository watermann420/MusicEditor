// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Beat Repeat effect control with gate pattern editor, decay, pitch shift, and probability.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Beat repeat effect control with grid size selection, repeat count, decay, pitch shift,
/// gate pattern editor, probability, mix, sync to tempo, and stutter mode.
/// </summary>
public partial class BeatRepeatControl : UserControl
{
    #region Constants

    private const int PatternRows = 8;
    private const int PatternColumns = 16;
    private const double CellPadding = 2.0;

    #endregion

    #region Private Fields

    private readonly bool[,] _gatePattern = new bool[PatternRows, PatternColumns];
    private Shapes.Rectangle[,]? _patternCells;
    private Shapes.Rectangle[]? _repeatVisualizationBars;

    private bool _isDrawing;
    private bool _drawState;
    private bool _isInitialized;
    private bool _isUpdating;
    private bool _isBypassed;

    private int _currentRepeatIndex;
    private int _activeRepeatCount;
    private readonly Random _random = new();

    private DispatcherTimer? _visualizationTimer;
    private DispatcherTimer? _repeatActivityTimer;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty GridSizeProperty =
        DependencyProperty.Register(nameof(GridSize), typeof(BeatGridSize), typeof(BeatRepeatControl),
            new PropertyMetadata(BeatGridSize.Eighth, OnGridSizeChanged));

    public static readonly DependencyProperty RepeatCountProperty =
        DependencyProperty.Register(nameof(RepeatCount), typeof(int), typeof(BeatRepeatControl),
            new PropertyMetadata(4, OnRepeatCountChanged));

    public static readonly DependencyProperty DecayProperty =
        DependencyProperty.Register(nameof(Decay), typeof(double), typeof(BeatRepeatControl),
            new PropertyMetadata(0.0, OnDecayChanged));

    public static readonly DependencyProperty PitchShiftProperty =
        DependencyProperty.Register(nameof(PitchShift), typeof(int), typeof(BeatRepeatControl),
            new PropertyMetadata(0, OnPitchShiftChanged));

    public static readonly DependencyProperty ProbabilityProperty =
        DependencyProperty.Register(nameof(Probability), typeof(double), typeof(BeatRepeatControl),
            new PropertyMetadata(100.0, OnProbabilityChanged));

    public static readonly DependencyProperty MixProperty =
        DependencyProperty.Register(nameof(Mix), typeof(double), typeof(BeatRepeatControl),
            new PropertyMetadata(100.0, OnMixChanged));

    public static readonly DependencyProperty SyncToTempoProperty =
        DependencyProperty.Register(nameof(SyncToTempo), typeof(bool), typeof(BeatRepeatControl),
            new PropertyMetadata(true, OnSyncToTempoChanged));

    public static readonly DependencyProperty StutterModeProperty =
        DependencyProperty.Register(nameof(StutterMode), typeof(bool), typeof(BeatRepeatControl),
            new PropertyMetadata(false, OnStutterModeChanged));

    public static readonly DependencyProperty IsBypassedProperty =
        DependencyProperty.Register(nameof(IsBypassed), typeof(bool), typeof(BeatRepeatControl),
            new PropertyMetadata(false, OnIsBypassedChanged));

    /// <summary>
    /// Gets or sets the beat grid size for repeats.
    /// </summary>
    public BeatGridSize GridSize
    {
        get => (BeatGridSize)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of times to repeat (1-16).
    /// </summary>
    public int RepeatCount
    {
        get => (int)GetValue(RepeatCountProperty);
        set => SetValue(RepeatCountProperty, value);
    }

    /// <summary>
    /// Gets or sets the volume decay per repeat (0-100%).
    /// </summary>
    public double Decay
    {
        get => (double)GetValue(DecayProperty);
        set => SetValue(DecayProperty, value);
    }

    /// <summary>
    /// Gets or sets the pitch shift per repeat in semitones (-12 to +12).
    /// </summary>
    public int PitchShift
    {
        get => (int)GetValue(PitchShiftProperty);
        set => SetValue(PitchShiftProperty, value);
    }

    /// <summary>
    /// Gets or sets the probability of repeat triggering (0-100%).
    /// </summary>
    public double Probability
    {
        get => (double)GetValue(ProbabilityProperty);
        set => SetValue(ProbabilityProperty, value);
    }

    /// <summary>
    /// Gets or sets the dry/wet mix (0-100%).
    /// </summary>
    public double Mix
    {
        get => (double)GetValue(MixProperty);
        set => SetValue(MixProperty, value);
    }

    /// <summary>
    /// Gets or sets whether repeats are synced to tempo.
    /// </summary>
    public bool SyncToTempo
    {
        get => (bool)GetValue(SyncToTempoProperty);
        set => SetValue(SyncToTempoProperty, value);
    }

    /// <summary>
    /// Gets or sets whether stutter mode is enabled for glitchy effects.
    /// </summary>
    public bool StutterMode
    {
        get => (bool)GetValue(StutterModeProperty);
        set => SetValue(StutterModeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => (bool)GetValue(IsBypassedProperty);
        set => SetValue(IsBypassedProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a parameter value changes.
    /// </summary>
    public event EventHandler<BeatRepeatParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Raised when the bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Raised when the gate pattern changes.
    /// </summary>
    public event EventHandler<bool[,]>? GatePatternChanged;

    #endregion

    #region Constructor

    public BeatRepeatControl()
    {
        InitializeComponent();

        // Initialize pattern to all enabled
        for (int row = 0; row < PatternRows; row++)
        {
            for (int col = 0; col < PatternColumns; col++)
            {
                _gatePattern[row, col] = true;
            }
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildPatternGrid();
        BuildRepeatVisualization();
        BuildRowLabels();
        _isInitialized = true;

        // Start visualization timer
        _visualizationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _visualizationTimer.Tick += VisualizationTimer_Tick;
        _visualizationTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _visualizationTimer?.Stop();
        _visualizationTimer = null;
        _repeatActivityTimer?.Stop();
        _repeatActivityTimer = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdatePatternGridLayout();
            UpdateRepeatVisualizationLayout();
        }
    }

    private void VisualizationTimer_Tick(object? sender, EventArgs e)
    {
        if (_isInitialized)
        {
            UpdateRepeatVisualization();
        }
    }

    #endregion

    #region Visual Tree Building

    private void BuildPatternGrid()
    {
        GatePatternCanvas.Children.Clear();
        _patternCells = new Shapes.Rectangle[PatternRows, PatternColumns];

        var activeBrush = FindResource("BeatRepeatAccentBrush") as Brush ?? Brushes.Cyan;
        var inactiveBrush = FindResource("BeatRepeatInactiveStepBrush") as Brush ?? Brushes.DarkGray;
        var borderBrush = FindResource("BeatRepeatBorderBrush") as Brush ?? Brushes.Gray;

        for (int row = 0; row < PatternRows; row++)
        {
            for (int col = 0; col < PatternColumns; col++)
            {
                var cell = new Shapes.Rectangle
                {
                    Fill = _gatePattern[row, col] ? activeBrush : inactiveBrush,
                    Stroke = borderBrush,
                    StrokeThickness = 1,
                    RadiusX = 2,
                    RadiusY = 2,
                    Tag = new Point(row, col)
                };

                _patternCells[row, col] = cell;
                GatePatternCanvas.Children.Add(cell);
            }
        }

        UpdatePatternGridLayout();
    }

    private void BuildRepeatVisualization()
    {
        RepeatVisualizationCanvas.Children.Clear();
        _repeatVisualizationBars = new Shapes.Rectangle[16];

        var accentBrush = FindResource("BeatRepeatAccentBrush") as Brush ?? Brushes.Cyan;

        for (int i = 0; i < 16; i++)
        {
            var bar = new Shapes.Rectangle
            {
                Fill = accentBrush,
                RadiusX = 2,
                RadiusY = 2,
                Opacity = 0.2
            };

            _repeatVisualizationBars[i] = bar;
            RepeatVisualizationCanvas.Children.Add(bar);
        }

        UpdateRepeatVisualizationLayout();
    }

    private void BuildRowLabels()
    {
        RowLabelsControl.Items.Clear();
        var textBrush = FindResource("BeatRepeatSecondaryTextBrush") as Brush ?? Brushes.Gray;

        for (int row = 0; row < PatternRows; row++)
        {
            var label = new TextBlock
            {
                Text = $"{row + 1}",
                Foreground = textBrush,
                FontSize = 10,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RowLabelsControl.Items.Add(label);
        }
    }

    private void UpdatePatternGridLayout()
    {
        if (_patternCells == null) return;

        double width = GatePatternCanvas.ActualWidth;
        double height = GatePatternCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double cellWidth = (width - (PatternColumns + 1) * CellPadding) / PatternColumns;
        double cellHeight = (height - (PatternRows + 1) * CellPadding) / PatternRows;

        for (int row = 0; row < PatternRows; row++)
        {
            for (int col = 0; col < PatternColumns; col++)
            {
                var cell = _patternCells[row, col];
                cell.Width = Math.Max(1, cellWidth);
                cell.Height = Math.Max(1, cellHeight);

                Canvas.SetLeft(cell, CellPadding + col * (cellWidth + CellPadding));
                Canvas.SetTop(cell, CellPadding + row * (cellHeight + CellPadding));
            }
        }
    }

    private void UpdateRepeatVisualizationLayout()
    {
        if (_repeatVisualizationBars == null) return;

        double width = RepeatVisualizationCanvas.ActualWidth;
        double height = RepeatVisualizationCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double barWidth = (width - 17 * 2) / 16;
        double barHeight = height - 8;

        for (int i = 0; i < 16; i++)
        {
            var bar = _repeatVisualizationBars[i];
            bar.Width = Math.Max(1, barWidth);
            bar.Height = Math.Max(1, barHeight);

            Canvas.SetLeft(bar, 2 + i * (barWidth + 2));
            Canvas.SetTop(bar, 4);
        }
    }

    private void UpdateRepeatVisualization()
    {
        if (_repeatVisualizationBars == null) return;

        var accentBrush = FindResource("BeatRepeatAccentBrush") as Brush ?? Brushes.Cyan;
        var successBrush = FindResource("BeatRepeatSuccessBrush") as Brush ?? Brushes.Green;

        int repeatCount = (int)RepeatCountSlider.Value;

        for (int i = 0; i < 16; i++)
        {
            var bar = _repeatVisualizationBars[i];

            if (i < repeatCount)
            {
                bar.Opacity = i == _currentRepeatIndex && _activeRepeatCount > 0 ? 1.0 : 0.4;
                bar.Fill = i == _currentRepeatIndex && _activeRepeatCount > 0 ? successBrush : accentBrush;
            }
            else
            {
                bar.Opacity = 0.1;
                bar.Fill = accentBrush;
            }
        }

        // Update LED indicator
        if (_activeRepeatCount > 0)
        {
            RepeatActivityLed.Fill = FindResource("BeatRepeatSuccessBrush") as Brush ?? Brushes.Green;
            RepeatCountDisplay.Text = $"{_currentRepeatIndex + 1} / {repeatCount}";
        }
        else
        {
            RepeatActivityLed.Fill = FindResource("BeatRepeatBorderBrush") as Brush ?? Brushes.Gray;
            RepeatCountDisplay.Text = "-- / --";
        }
    }

    private void RefreshPatternDisplay()
    {
        if (_patternCells == null) return;

        var activeBrush = FindResource("BeatRepeatAccentBrush") as Brush ?? Brushes.Cyan;
        var inactiveBrush = FindResource("BeatRepeatInactiveStepBrush") as Brush ?? Brushes.DarkGray;

        for (int row = 0; row < PatternRows; row++)
        {
            for (int col = 0; col < PatternColumns; col++)
            {
                _patternCells[row, col].Fill = _gatePattern[row, col] ? activeBrush : inactiveBrush;
            }
        }
    }

    #endregion

    #region Pattern Interaction

    private void GatePatternCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Point pos = e.GetPosition(GatePatternCanvas);
        var (row, col) = GetCellAtPoint(pos);

        if (row >= 0 && row < PatternRows && col >= 0 && col < PatternColumns)
        {
            _isDrawing = true;
            _drawState = !_gatePattern[row, col];
            _gatePattern[row, col] = _drawState;
            RefreshPatternDisplay();
            GatePatternCanvas.CaptureMouse();
        }
    }

    private void GatePatternCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;

        Point pos = e.GetPosition(GatePatternCanvas);
        var (row, col) = GetCellAtPoint(pos);

        if (row >= 0 && row < PatternRows && col >= 0 && col < PatternColumns)
        {
            if (_gatePattern[row, col] != _drawState)
            {
                _gatePattern[row, col] = _drawState;
                RefreshPatternDisplay();
            }
        }
    }

    private void GatePatternCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawing)
        {
            _isDrawing = false;
            GatePatternCanvas.ReleaseMouseCapture();
            RaiseGatePatternChanged();
            StatusText.Text = "Pattern updated";
        }
    }

    private (int row, int col) GetCellAtPoint(Point point)
    {
        double width = GatePatternCanvas.ActualWidth;
        double height = GatePatternCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return (-1, -1);

        double cellWidth = (width - (PatternColumns + 1) * CellPadding) / PatternColumns;
        double cellHeight = (height - (PatternRows + 1) * CellPadding) / PatternRows;

        int col = (int)((point.X - CellPadding) / (cellWidth + CellPadding));
        int row = (int)((point.Y - CellPadding) / (cellHeight + CellPadding));

        return (row, col);
    }

    #endregion

    #region Parameter Event Handlers

    private void GridSize_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        BeatGridSize gridSize = BeatGridSize.Eighth;

        if (Grid1_4?.IsChecked == true) gridSize = BeatGridSize.Quarter;
        else if (Grid1_8?.IsChecked == true) gridSize = BeatGridSize.Eighth;
        else if (Grid1_16?.IsChecked == true) gridSize = BeatGridSize.Sixteenth;
        else if (Grid1_32?.IsChecked == true) gridSize = BeatGridSize.ThirtySecond;

        GridSize = gridSize;
        RaiseParameterChanged("GridSize", (int)gridSize);
        StatusText.Text = $"Grid: {GetGridSizeName(gridSize)}";
    }

    private void RepeatCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RepeatCountValue == null || _isUpdating) return;

        int value = (int)e.NewValue;
        RepeatCountValue.Text = value.ToString();
        RepeatCount = value;
        RaiseParameterChanged("RepeatCount", value);
        UpdateRepeatVisualization();
    }

    private void DecaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DecayValue == null || _isUpdating) return;

        DecayValue.Text = $"{e.NewValue:F0}%";
        Decay = e.NewValue;
        RaiseParameterChanged("Decay", e.NewValue);
    }

    private void PitchShiftSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PitchShiftValue == null || _isUpdating) return;

        int value = (int)e.NewValue;
        string sign = value > 0 ? "+" : "";
        PitchShiftValue.Text = $"{sign}{value} st";
        PitchShift = value;
        RaiseParameterChanged("PitchShift", value);
    }

    private void ProbabilitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProbabilityValue == null || _isUpdating) return;

        ProbabilityValue.Text = $"{e.NewValue:F0}%";
        Probability = e.NewValue;
        RaiseParameterChanged("Probability", e.NewValue);
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null || _isUpdating) return;

        MixValue.Text = $"{e.NewValue:F0}%";
        Mix = e.NewValue;
        RaiseParameterChanged("Mix", e.NewValue);
    }

    private void SyncToTempoToggle_Click(object sender, RoutedEventArgs e)
    {
        SyncToTempo = SyncToTempoToggle.IsChecked == true;
        RaiseParameterChanged("SyncToTempo", SyncToTempo ? 1 : 0);
        StatusText.Text = SyncToTempo ? "Sync to tempo enabled" : "Sync to tempo disabled";
    }

    private void StutterModeToggle_Click(object sender, RoutedEventArgs e)
    {
        StutterMode = StutterModeToggle.IsChecked == true;
        RaiseParameterChanged("StutterMode", StutterMode ? 1 : 0);
        StatusText.Text = StutterMode ? "Stutter mode enabled" : "Stutter mode disabled";
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = BypassToggle.IsChecked == true;
        IsBypassed = _isBypassed;
        StatusText.Text = _isBypassed ? "Effect bypassed" : "Effect active";
        BypassChanged?.Invoke(this, _isBypassed);
    }

    #endregion

    #region Pattern Buttons

    private void ClearPattern_Click(object sender, RoutedEventArgs e)
    {
        for (int row = 0; row < PatternRows; row++)
        {
            for (int col = 0; col < PatternColumns; col++)
            {
                _gatePattern[row, col] = false;
            }
        }

        RefreshPatternDisplay();
        RaiseGatePatternChanged();
        StatusText.Text = "Pattern cleared";
    }

    private void RandomPattern_Click(object sender, RoutedEventArgs e)
    {
        for (int row = 0; row < PatternRows; row++)
        {
            for (int col = 0; col < PatternColumns; col++)
            {
                _gatePattern[row, col] = _random.NextDouble() > 0.5;
            }
        }

        RefreshPatternDisplay();
        RaiseGatePatternChanged();
        StatusText.Text = "Random pattern generated";
    }

    private void EuclideanPattern_Click(object sender, RoutedEventArgs e)
    {
        // Generate Euclidean rhythm pattern
        for (int row = 0; row < PatternRows; row++)
        {
            int hits = row + 3; // 3-10 hits per row
            GenerateEuclideanPattern(row, hits, PatternColumns);
        }

        RefreshPatternDisplay();
        RaiseGatePatternChanged();
        StatusText.Text = "Euclidean pattern generated";
    }

    private void GenerateEuclideanPattern(int row, int hits, int steps)
    {
        hits = Math.Min(hits, steps);

        if (hits == 0)
        {
            for (int col = 0; col < steps; col++)
            {
                _gatePattern[row, col] = false;
            }
            return;
        }

        if (hits == steps)
        {
            for (int col = 0; col < steps; col++)
            {
                _gatePattern[row, col] = true;
            }
            return;
        }

        // Bresenham-based Euclidean rhythm
        int[] pattern = new int[steps];
        int prev = -1;

        for (int i = 0; i < hits; i++)
        {
            int current = (int)Math.Floor((double)(i * steps) / hits);
            if (current != prev)
            {
                pattern[current] = 1;
                prev = current;
            }
        }

        for (int col = 0; col < steps; col++)
        {
            _gatePattern[row, col] = pattern[col] == 1;
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        StatusText.Text = "Reset to defaults";
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedIndex <= 0 || _isUpdating) return;

        var item = PresetComboBox.SelectedItem as ComboBoxItem;
        if (item == null) return;

        string preset = item.Content?.ToString() ?? "";
        ApplyPreset(preset);

        PresetComboBox.SelectedIndex = 0;
    }

    #endregion

    #region Presets

    private void ApplyPreset(string preset)
    {
        _isUpdating = true;

        try
        {
            switch (preset)
            {
                case "Subtle":
                    Grid1_8.IsChecked = true;
                    RepeatCountSlider.Value = 2;
                    DecaySlider.Value = 30;
                    PitchShiftSlider.Value = 0;
                    ProbabilitySlider.Value = 50;
                    MixSlider.Value = 60;
                    SyncToTempoToggle.IsChecked = true;
                    StutterModeToggle.IsChecked = false;
                    break;

                case "Classic":
                    Grid1_8.IsChecked = true;
                    RepeatCountSlider.Value = 4;
                    DecaySlider.Value = 15;
                    PitchShiftSlider.Value = 0;
                    ProbabilitySlider.Value = 100;
                    MixSlider.Value = 100;
                    SyncToTempoToggle.IsChecked = true;
                    StutterModeToggle.IsChecked = false;
                    break;

                case "Glitch":
                    Grid1_16.IsChecked = true;
                    RepeatCountSlider.Value = 8;
                    DecaySlider.Value = 0;
                    PitchShiftSlider.Value = 0;
                    ProbabilitySlider.Value = 70;
                    MixSlider.Value = 100;
                    SyncToTempoToggle.IsChecked = false;
                    StutterModeToggle.IsChecked = true;
                    break;

                case "Tape Stop":
                    Grid1_8.IsChecked = true;
                    RepeatCountSlider.Value = 6;
                    DecaySlider.Value = 50;
                    PitchShiftSlider.Value = -2;
                    ProbabilitySlider.Value = 100;
                    MixSlider.Value = 100;
                    SyncToTempoToggle.IsChecked = true;
                    StutterModeToggle.IsChecked = false;
                    break;

                case "Riser":
                    Grid1_16.IsChecked = true;
                    RepeatCountSlider.Value = 12;
                    DecaySlider.Value = 0;
                    PitchShiftSlider.Value = 1;
                    ProbabilitySlider.Value = 100;
                    MixSlider.Value = 80;
                    SyncToTempoToggle.IsChecked = true;
                    StutterModeToggle.IsChecked = false;
                    break;

                case "Breakdown":
                    Grid1_32.IsChecked = true;
                    RepeatCountSlider.Value = 16;
                    DecaySlider.Value = 5;
                    PitchShiftSlider.Value = 0;
                    ProbabilitySlider.Value = 100;
                    MixSlider.Value = 100;
                    SyncToTempoToggle.IsChecked = true;
                    StutterModeToggle.IsChecked = true;
                    break;
            }

            UpdateValueDisplays();
            StatusText.Text = $"Preset applied: {preset}";
        }
        finally
        {
            _isUpdating = false;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the current gate pattern.
    /// </summary>
    public bool[,] GetGatePattern()
    {
        var pattern = new bool[PatternRows, PatternColumns];
        Array.Copy(_gatePattern, pattern, _gatePattern.Length);
        return pattern;
    }

    /// <summary>
    /// Sets the gate pattern.
    /// </summary>
    public void SetGatePattern(bool[,] pattern)
    {
        if (pattern.GetLength(0) != PatternRows || pattern.GetLength(1) != PatternColumns)
        {
            throw new ArgumentException($"Pattern must be {PatternRows}x{PatternColumns}");
        }

        Array.Copy(pattern, _gatePattern, _gatePattern.Length);
        RefreshPatternDisplay();
    }

    /// <summary>
    /// Triggers a repeat visualization (called from audio engine).
    /// </summary>
    public void TriggerRepeat(int repeatIndex)
    {
        _currentRepeatIndex = repeatIndex;
        _activeRepeatCount = (int)RepeatCountSlider.Value;

        _repeatActivityTimer?.Stop();
        _repeatActivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _repeatActivityTimer.Tick += (s, e) =>
        {
            _activeRepeatCount = 0;
            _repeatActivityTimer?.Stop();
            UpdateRepeatVisualization();
        };
        _repeatActivityTimer.Start();

        UpdateRepeatVisualization();
    }

    /// <summary>
    /// Resets all parameters to default values.
    /// </summary>
    public void Reset()
    {
        _isUpdating = true;

        try
        {
            Grid1_8.IsChecked = true;
            RepeatCountSlider.Value = 4;
            DecaySlider.Value = 0;
            PitchShiftSlider.Value = 0;
            ProbabilitySlider.Value = 100;
            MixSlider.Value = 100;
            SyncToTempoToggle.IsChecked = true;
            StutterModeToggle.IsChecked = false;
            BypassToggle.IsChecked = false;

            // Reset pattern to all enabled
            for (int row = 0; row < PatternRows; row++)
            {
                for (int col = 0; col < PatternColumns; col++)
                {
                    _gatePattern[row, col] = true;
                }
            }

            RefreshPatternDisplay();
            UpdateValueDisplays();
        }
        finally
        {
            _isUpdating = false;
        }

        _isBypassed = false;
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(BeatGridSize gridSize, int repeatCount, double decay,
        int pitchShift, double probability, double mix, bool syncToTempo, bool stutterMode)
    {
        _isUpdating = true;

        try
        {
            switch (gridSize)
            {
                case BeatGridSize.Quarter: Grid1_4.IsChecked = true; break;
                case BeatGridSize.Eighth: Grid1_8.IsChecked = true; break;
                case BeatGridSize.Sixteenth: Grid1_16.IsChecked = true; break;
                case BeatGridSize.ThirtySecond: Grid1_32.IsChecked = true; break;
            }

            RepeatCountSlider.Value = Math.Clamp(repeatCount, 1, 16);
            DecaySlider.Value = Math.Clamp(decay, 0, 100);
            PitchShiftSlider.Value = Math.Clamp(pitchShift, -12, 12);
            ProbabilitySlider.Value = Math.Clamp(probability, 0, 100);
            MixSlider.Value = Math.Clamp(mix, 0, 100);
            SyncToTempoToggle.IsChecked = syncToTempo;
            StutterModeToggle.IsChecked = stutterMode;

            UpdateValueDisplays();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    #endregion

    #region Helper Methods

    private void UpdateValueDisplays()
    {
        RepeatCountValue.Text = $"{(int)RepeatCountSlider.Value}";
        DecayValue.Text = $"{DecaySlider.Value:F0}%";

        int pitchShift = (int)PitchShiftSlider.Value;
        string sign = pitchShift > 0 ? "+" : "";
        PitchShiftValue.Text = $"{sign}{pitchShift} st";

        ProbabilityValue.Text = $"{ProbabilitySlider.Value:F0}%";
        MixValue.Text = $"{MixSlider.Value:F0}%";
    }

    private void RaiseParameterChanged(string name, double value)
    {
        ParameterChanged?.Invoke(this, new BeatRepeatParameterChangedEventArgs(name, value));
    }

    private void RaiseGatePatternChanged()
    {
        GatePatternChanged?.Invoke(this, GetGatePattern());
    }

    private static string GetGridSizeName(BeatGridSize gridSize)
    {
        return gridSize switch
        {
            BeatGridSize.Quarter => "1/4",
            BeatGridSize.Eighth => "1/8",
            BeatGridSize.Sixteenth => "1/16",
            BeatGridSize.ThirtySecond => "1/32",
            _ => "1/8"
        };
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnGridSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control._isUpdating = true;
            try
            {
                var gridSize = (BeatGridSize)e.NewValue;
                switch (gridSize)
                {
                    case BeatGridSize.Quarter: control.Grid1_4.IsChecked = true; break;
                    case BeatGridSize.Eighth: control.Grid1_8.IsChecked = true; break;
                    case BeatGridSize.Sixteenth: control.Grid1_16.IsChecked = true; break;
                    case BeatGridSize.ThirtySecond: control.Grid1_32.IsChecked = true; break;
                }
            }
            finally
            {
                control._isUpdating = false;
            }
        }
    }

    private static void OnRepeatCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control.RepeatCountSlider.Value = (int)e.NewValue;
        }
    }

    private static void OnDecayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control.DecaySlider.Value = (double)e.NewValue;
        }
    }

    private static void OnPitchShiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control.PitchShiftSlider.Value = (int)e.NewValue;
        }
    }

    private static void OnProbabilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control.ProbabilitySlider.Value = (double)e.NewValue;
        }
    }

    private static void OnMixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control.MixSlider.Value = (double)e.NewValue;
        }
    }

    private static void OnSyncToTempoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control.SyncToTempoToggle.IsChecked = (bool)e.NewValue;
        }
    }

    private static void OnStutterModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control.StutterModeToggle.IsChecked = (bool)e.NewValue;
        }
    }

    private static void OnIsBypassedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BeatRepeatControl control && !control._isUpdating)
        {
            control.BypassToggle.IsChecked = (bool)e.NewValue;
            control._isBypassed = (bool)e.NewValue;
        }
    }

    #endregion
}

/// <summary>
/// Beat grid size divisions.
/// </summary>
public enum BeatGridSize
{
    /// <summary>1/4 beat division (quarter notes)</summary>
    Quarter = 4,
    /// <summary>1/8 beat division (eighth notes)</summary>
    Eighth = 8,
    /// <summary>1/16 beat division (sixteenth notes)</summary>
    Sixteenth = 16,
    /// <summary>1/32 beat division (thirty-second notes)</summary>
    ThirtySecond = 32
}

/// <summary>
/// Event arguments for beat repeat parameter changes.
/// </summary>
public class BeatRepeatParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public BeatRepeatParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}
