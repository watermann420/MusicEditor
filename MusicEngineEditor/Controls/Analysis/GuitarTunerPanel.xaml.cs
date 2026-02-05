// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Guitar Tuner panel control with pitch detection and visual feedback.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngineEditor.ViewModels.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Guitar Tuner panel providing pitch detection visualization with arc display,
/// string selection, tuning presets, and strobe tuner mode.
/// </summary>
public partial class GuitarTunerPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty DetectedFrequencyProperty =
        DependencyProperty.Register(nameof(DetectedFrequency), typeof(double), typeof(GuitarTunerPanel),
            new PropertyMetadata(0.0, OnDetectedFrequencyChanged));

    public static readonly DependencyProperty InputLevelProperty =
        DependencyProperty.Register(nameof(InputLevel), typeof(double), typeof(GuitarTunerPanel),
            new PropertyMetadata(0.0, OnInputLevelChanged));

    public static readonly DependencyProperty IsListeningProperty =
        DependencyProperty.Register(nameof(IsListening), typeof(bool), typeof(GuitarTunerPanel),
            new PropertyMetadata(false, OnIsListeningChanged));

    public static readonly DependencyProperty IsStrobeModeProperty =
        DependencyProperty.Register(nameof(IsStrobeMode), typeof(bool), typeof(GuitarTunerPanel),
            new PropertyMetadata(false, OnIsStrobeModeChanged));

    public static readonly DependencyProperty ReferencePitchProperty =
        DependencyProperty.Register(nameof(ReferencePitch), typeof(double), typeof(GuitarTunerPanel),
            new PropertyMetadata(440.0, OnReferencePitchChanged));

    /// <summary>
    /// Gets or sets the detected frequency in Hz.
    /// </summary>
    public double DetectedFrequency
    {
        get => (double)GetValue(DetectedFrequencyProperty);
        set => SetValue(DetectedFrequencyProperty, value);
    }

    /// <summary>
    /// Gets or sets the input signal level (0.0 to 1.0).
    /// </summary>
    public double InputLevel
    {
        get => (double)GetValue(InputLevelProperty);
        set => SetValue(InputLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the tuner is listening.
    /// </summary>
    public bool IsListening
    {
        get => (bool)GetValue(IsListeningProperty);
        set => SetValue(IsListeningProperty, value);
    }

    /// <summary>
    /// Gets or sets whether strobe mode is enabled.
    /// </summary>
    public bool IsStrobeMode
    {
        get => (bool)GetValue(IsStrobeModeProperty);
        set => SetValue(IsStrobeModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the reference pitch for A4 in Hz.
    /// </summary>
    public double ReferencePitch
    {
        get => (double)GetValue(ReferencePitchProperty);
        set => SetValue(ReferencePitchProperty, value);
    }

    #endregion

    #region Cached Frozen Brushes

    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(0x00, 0xD9, 0xFF));
    private static readonly SolidColorBrush SuccessBrush = new(Color.FromRgb(0x00, 0xFF, 0x88));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xFF, 0xB8, 0x00));
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(0xFF, 0x47, 0x57));
    private static readonly SolidColorBrush DimBrush = new(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly SolidColorBrush WhiteBrush = new(Colors.White);
    private static readonly SolidColorBrush BlackBrush = new(Colors.Black);
    private static readonly SolidColorBrush PanelBackgroundBrush = new(Color.FromRgb(0x1A, 0x1A, 0x1A));
    private static readonly SolidColorBrush CachedBorderBrush = new(Color.FromRgb(0x2A, 0x2A, 0x2A));
    private static readonly SolidColorBrush SecondaryForegroundBrush = new(Color.FromRgb(0x90, 0x90, 0x90));
    private static readonly SolidColorBrush SubtleBorderBrush = new(Color.FromRgb(0x3A, 0x3A, 0x3A));
    private static readonly SolidColorBrush DisabledForegroundBrush = new(Color.FromRgb(0x40, 0x40, 0x40));

    static GuitarTunerPanel()
    {
        AccentBrush.Freeze();
        SuccessBrush.Freeze();
        WarningBrush.Freeze();
        ErrorBrush.Freeze();
        DimBrush.Freeze();
        WhiteBrush.Freeze();
        BlackBrush.Freeze();
        PanelBackgroundBrush.Freeze();
        CachedBorderBrush.Freeze();
        SecondaryForegroundBrush.Freeze();
        SubtleBorderBrush.Freeze();
        DisabledForegroundBrush.Freeze();
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private readonly GuitarTunerViewModel _viewModel;
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _strobeTimer;

    // Arc display elements
    private Shapes.Path? _arcBackground;
#pragma warning disable CS0169
    private Shapes.Path? _arcIndicator;
#pragma warning restore CS0169
    private Shapes.Ellipse? _needleIndicator;
    private readonly List<Shapes.Line> _arcTicks = new();
    private readonly List<TextBlock> _arcLabels = new();

    // String visualization elements
    private readonly List<Shapes.Line> _stringLines = new();
    private readonly List<Button> _stringButtons = new();

    // Strobe elements
    private readonly List<Shapes.Rectangle> _strobeLines = new();
    private double _strobeOffset;

    // In-tune glow effect
    private Shapes.Ellipse? _inTuneGlow;

    // Theme colors with lazy initialization
    private Color? _accentColor;
    private Color? _successColor;
    private Color? _warningColor;
    private Color? _errorColor;
    private Color? _dimColor;

    private Color AccentColor => _accentColor ??= GetThemeColor("AccentColor", Color.FromRgb(0x00, 0xD9, 0xFF));
    private Color SuccessColor => _successColor ??= GetThemeColor("SuccessColor", Color.FromRgb(0x00, 0xFF, 0x88));
    private Color WarningColor => _warningColor ??= GetThemeColor("WarningColor", Color.FromRgb(0xFF, 0xB8, 0x00));
    private Color ErrorColor => _errorColor ??= GetThemeColor("ErrorColor", Color.FromRgb(0xFF, 0x47, 0x57));
    private Color DimColor => _dimColor ??= GetThemeColor("SecondaryForegroundColor", Color.FromRgb(0x80, 0x80, 0x80));

    private static Color GetThemeColor(string resourceKey, Color fallback)
    {
        if (Application.Current?.TryFindResource(resourceKey) is Color color)
            return color;
        return fallback;
    }

    #endregion

    #region Constructor

    public GuitarTunerPanel()
    {
        InitializeComponent();

        _viewModel = new GuitarTunerViewModel();
        DataContext = _viewModel;

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _animationTimer.Tick += AnimationTimer_Tick;

        _strobeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS for smooth strobe
        };
        _strobeTimer.Tick += StrobeTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        InitializeTunerArc();
        InitializeStringVisualization();
        InitializeStrobeDisplay();
        UpdateStringButtons();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _animationTimer.Stop();
        _strobeTimer.Stop();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GuitarTunerViewModel.NoteName):
                NoteNameText.Text = _viewModel.NoteName;
                break;
            case nameof(GuitarTunerViewModel.Octave):
                OctaveText.Text = _viewModel.Octave.ToString();
                break;
            case nameof(GuitarTunerViewModel.CentsDeviation):
                UpdateCentsDisplay();
                UpdateNeedlePosition();
                break;
            case nameof(GuitarTunerViewModel.FrequencyDisplay):
                FrequencyText.Text = _viewModel.FrequencyDisplay;
                break;
            case nameof(GuitarTunerViewModel.IsInTune):
                UpdateInTuneVisual();
                break;
            case nameof(GuitarTunerViewModel.SelectedStringIndex):
                UpdateSelectedString();
                break;
            case nameof(GuitarTunerViewModel.ReferencePitch):
                ReferencePitchText.Text = $"{_viewModel.ReferencePitch:F0} Hz";
                break;
            case nameof(GuitarTunerViewModel.InputLevel):
                UpdateInputLevel();
                break;
            case nameof(GuitarTunerViewModel.SelectedTuningPreset):
                UpdateStringButtons();
                break;
        }
    }

    private static void OnDetectedFrequencyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GuitarTunerPanel panel && panel._isInitialized)
        {
            panel._viewModel.UpdatePitchData((double)e.NewValue, panel.InputLevel);
        }
    }

    private static void OnInputLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GuitarTunerPanel panel && panel._isInitialized)
        {
            panel._viewModel.UpdatePitchData(panel.DetectedFrequency, (double)e.NewValue);
        }
    }

    private static void OnIsListeningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GuitarTunerPanel panel)
        {
            panel._viewModel.IsListening = (bool)e.NewValue;
            if ((bool)e.NewValue)
            {
                panel._animationTimer.Start();
            }
            else
            {
                panel._animationTimer.Stop();
            }
        }
    }

    private static void OnIsStrobeModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GuitarTunerPanel panel)
        {
            panel._viewModel.IsStrobeMode = (bool)e.NewValue;
            panel.StrobePanel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;

            if ((bool)e.NewValue && panel.IsListening)
            {
                panel._strobeTimer.Start();
            }
            else
            {
                panel._strobeTimer.Stop();
            }
        }
    }

    private static void OnReferencePitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GuitarTunerPanel panel)
        {
            panel._viewModel.ReferencePitch = (double)e.NewValue;
        }
    }

    private void ListenToggle_Click(object sender, RoutedEventArgs e)
    {
        IsListening = ListenToggle.IsChecked ?? false;
    }

    private void AutoDetectToggle_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleAutoDetectCommand.Execute(null);
        UpdateSelectedString();
    }

    private void TuningPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStringButtons();
    }

    private void IncrementPitch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IncrementReferencePitchCommand.Execute(null);
    }

    private void DecrementPitch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DecrementReferencePitchCommand.Execute(null);
    }

    private void ResetPitch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetReferencePitchCommand.Execute(null);
    }

    private void ReferencePitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ReferencePitchText.Text = $"{e.NewValue:F0} Hz";
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        // Smooth animation updates
        UpdateNeedlePosition();
        UpdateInputLevel();
    }

    private void StrobeTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel.IsStrobeMode && _viewModel.HasSignal)
        {
            UpdateStrobeDisplay();
        }
    }

    #endregion

    #region Initialization

    private void InitializeTunerArc()
    {
        TunerArcCanvas.Children.Clear();
        _arcTicks.Clear();
        _arcLabels.Clear();

        double centerX = TunerArcCanvas.Width / 2;
        double centerY = TunerArcCanvas.Height - 20;
        double radius = 120;
        double startAngle = 180; // Left side
        double endAngle = 0; // Right side (arc goes from 180 to 0 degrees)

        // In-tune glow (behind everything)
        _inTuneGlow = new Shapes.Ellipse
        {
            Width = 80,
            Height = 80,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(80, 0x00, 0xFF, 0x88), 0),
                    new GradientStop(Color.FromArgb(0, 0x00, 0xFF, 0x88), 1)
                }
            },
            Opacity = 0
        };
        Canvas.SetLeft(_inTuneGlow, centerX - 40);
        Canvas.SetTop(_inTuneGlow, centerY - 60);
        TunerArcCanvas.Children.Add(_inTuneGlow);

        // Arc background (gradient from red to green to red)
        var arcGeometry = CreateArcGeometry(centerX, centerY, radius, startAngle, endAngle, 12);
        _arcBackground = new Shapes.Path
        {
            Data = arcGeometry,
            Stroke = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(ErrorColor, 0.0),
                    new GradientStop(WarningColor, 0.25),
                    new GradientStop(SuccessColor, 0.5),
                    new GradientStop(WarningColor, 0.75),
                    new GradientStop(ErrorColor, 1.0)
                }
            },
            StrokeThickness = 12,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0.3
        };
        TunerArcCanvas.Children.Add(_arcBackground);

        // Tick marks
        int[] tickValues = { -50, -40, -30, -20, -10, 0, 10, 20, 30, 40, 50 };
        foreach (int cents in tickValues)
        {
            double angle = 180 - (cents + 50) * 180 / 100; // Map -50..+50 to 180..0
            double radians = angle * Math.PI / 180;

            double innerRadius = cents == 0 ? radius - 25 : radius - 18;
            double outerRadius = radius + 5;

            double x1 = centerX + innerRadius * Math.Cos(radians);
            double y1 = centerY - innerRadius * Math.Sin(radians);
            double x2 = centerX + outerRadius * Math.Cos(radians);
            double y2 = centerY - outerRadius * Math.Sin(radians);

            var tick = new Shapes.Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = cents == 0 ? SuccessBrush : DimBrush,
                StrokeThickness = cents == 0 ? 2 : 1
            };
            TunerArcCanvas.Children.Add(tick);
            _arcTicks.Add(tick);

            // Labels for major ticks
            if (cents % 10 == 0)
            {
                double labelRadius = radius + 20;
                double labelX = centerX + labelRadius * Math.Cos(radians);
                double labelY = centerY - labelRadius * Math.Sin(radians);

                var label = new TextBlock
                {
                    Text = cents == 0 ? "0" : (cents > 0 ? $"+{cents}" : cents.ToString()),
                    FontSize = 9,
                    Foreground = cents == 0 ? SuccessBrush : DimBrush
                };
                Canvas.SetLeft(label, labelX - 12);
                Canvas.SetTop(label, labelY - 6);
                TunerArcCanvas.Children.Add(label);
                _arcLabels.Add(label);
            }
        }

        // Needle indicator
        _needleIndicator = new Shapes.Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = AccentBrush,
            Stroke = WhiteBrush,
            StrokeThickness = 2
        };
        Canvas.SetLeft(_needleIndicator, centerX - 8);
        Canvas.SetTop(_needleIndicator, centerY - radius - 8);
        TunerArcCanvas.Children.Add(_needleIndicator);

        // Center point
        var centerDot = new Shapes.Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = DimBrush
        };
        Canvas.SetLeft(centerDot, centerX - 4);
        Canvas.SetTop(centerDot, centerY - 4);
        TunerArcCanvas.Children.Add(centerDot);
    }

    private void InitializeStringVisualization()
    {
        StringVisualizationCanvas.Children.Clear();
        _stringLines.Clear();

        double width = 350;
        double height = 70;
        double stringSpacing = width / 7;
        double startY = 10;
        double endY = height - 10;

        // Guitar neck background
        var neckBackground = new Shapes.Rectangle
        {
            Width = width - 40,
            Height = height - 20,
            Fill = PanelBackgroundBrush,
            RadiusX = 4,
            RadiusY = 4
        };
        Canvas.SetLeft(neckBackground, 20);
        Canvas.SetTop(neckBackground, 10);
        StringVisualizationCanvas.Children.Add(neckBackground);

        // Frets
        for (int i = 0; i <= 4; i++)
        {
            double fretX = 30 + i * (width - 60) / 4;
            var fret = new Shapes.Line
            {
                X1 = fretX,
                Y1 = startY + 5,
                X2 = fretX,
                Y2 = endY - 5,
                Stroke = SubtleBorderBrush,
                StrokeThickness = 2
            };
            StringVisualizationCanvas.Children.Add(fret);
        }

        // Strings (6 strings, from thickest E2 to thinnest E4)
        double[] stringThicknesses = { 4, 3.5, 3, 2.5, 2, 1.5 };

        for (int i = 0; i < 6; i++)
        {
            double stringY = startY + 10 + i * (endY - startY - 20) / 5;

            var stringLine = new Shapes.Line
            {
                X1 = 25,
                Y1 = stringY,
                X2 = width - 25,
                Y2 = stringY,
                Stroke = SecondaryForegroundBrush,
                StrokeThickness = stringThicknesses[i]
            };
            StringVisualizationCanvas.Children.Add(stringLine);
            _stringLines.Add(stringLine);
        }
    }

    private void InitializeStrobeDisplay()
    {
        StrobeCanvas.Children.Clear();
        _strobeLines.Clear();

        // Create strobe pattern bars
        int barCount = 24;
        double barWidth = 8;
        double gap = 6;

        for (int i = 0; i < barCount; i++)
        {
            var bar = new Shapes.Rectangle
            {
                Width = barWidth,
                Height = 40,
                Fill = AccentBrush,
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(bar, i * (barWidth + gap));
            Canvas.SetTop(bar, 2);
            StrobeCanvas.Children.Add(bar);
            _strobeLines.Add(bar);
        }
    }

    private void UpdateStringButtons()
    {
        StringButtonsPanel.Items.Clear();
        _stringButtons.Clear();

        for (int i = 0; i < _viewModel.GuitarStrings.Count; i++)
        {
            var guitarString = _viewModel.GuitarStrings[i];
            int index = i;

            var button = new Button
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = guitarString.NoteName.Replace("2", "").Replace("3", "").Replace("4", ""),
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            Foreground = WhiteBrush
                        },
                        new TextBlock
                        {
                            Text = guitarString.StringNumber.ToString(),
                            FontSize = 9,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            Foreground = DimBrush
                        }
                    }
                },
                Width = 48,
                Height = 48,
                Margin = new Thickness(4),
                Background = PanelBackgroundBrush,
                BorderBrush = CachedBorderBrush,
                BorderThickness = new Thickness(2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = index
            };

            button.Click += StringButton_Click;

            // Apply rounded style
            button.Template = CreateStringButtonTemplate();

            StringButtonsPanel.Items.Add(button);
            _stringButtons.Add(button);
        }

        UpdateSelectedString();
    }

    private ControlTemplate CreateStringButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "border";
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(24));

        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(contentPresenter);

        template.VisualTree = border;

        // Mouse over trigger
        var mouseOverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, CachedBorderBrush, "border"));
        mouseOverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, AccentBrush, "border"));
        template.Triggers.Add(mouseOverTrigger);

        return template;
    }

    private void StringButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index)
        {
            _viewModel.SelectStringCommand.Execute(index);
        }
    }

    #endregion

    #region Update Methods

    private static SolidColorBrush GetTuningBrush(double cents)
    {
        if (Math.Abs(cents) <= 5) return SuccessBrush;
        if (Math.Abs(cents) <= 15) return WarningBrush;
        return ErrorBrush;
    }

    private void UpdateCentsDisplay()
    {
        double cents = _viewModel.CentsDeviation;
        string sign = cents >= 0 ? "+" : "";
        CentsText.Text = $"{sign}{cents:F0} cents";
        CentsText.Foreground = GetTuningBrush(cents);
    }

    private void UpdateNeedlePosition()
    {
        if (_needleIndicator == null) return;

        double centerX = TunerArcCanvas.Width / 2;
        double centerY = TunerArcCanvas.Height - 20;
        double radius = 120;

        // Clamp cents to -50..+50
        double cents = Math.Clamp(_viewModel.CentsDeviation, -50, 50);

        // Map cents to angle (180 = -50 cents, 0 = +50 cents)
        double angle = 180 - (cents + 50) * 180 / 100;
        double radians = angle * Math.PI / 180;

        double needleX = centerX + radius * Math.Cos(radians) - 8;
        double needleY = centerY - radius * Math.Sin(radians) - 8;

        Canvas.SetLeft(_needleIndicator, needleX);
        Canvas.SetTop(_needleIndicator, needleY);

        _needleIndicator.Fill = GetTuningBrush(cents);
    }

    private void UpdateInTuneVisual()
    {
        if (_inTuneGlow == null) return;

        // Animate glow opacity
        double targetOpacity = _viewModel.IsInTune ? 1.0 : 0.0;
        _inTuneGlow.Opacity = targetOpacity;

        NoteNameText.Foreground = _viewModel.IsInTune ? SuccessBrush : WhiteBrush;
    }

    private void UpdateSelectedString()
    {
        int selectedIndex = _viewModel.SelectedStringIndex;

        for (int i = 0; i < _stringButtons.Count; i++)
        {
            var button = _stringButtons[i];
            bool isSelected = i == selectedIndex;

            button.Background = isSelected ? AccentBrush : PanelBackgroundBrush;
            button.BorderBrush = isSelected ? AccentBrush : CachedBorderBrush;

            if (button.Content is StackPanel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is TextBlock textBlock)
                    {
                        if (textBlock.FontSize == 14)
                        {
                            textBlock.Foreground = isSelected ? BlackBrush : WhiteBrush;
                        }
                        else
                        {
                            textBlock.Foreground = isSelected ? DisabledForegroundBrush : DimBrush;
                        }
                    }
                }
            }
        }

        for (int i = 0; i < _stringLines.Count && i < 6; i++)
        {
            bool isSelected = i == selectedIndex;
            _stringLines[i].Stroke = isSelected ? AccentBrush : SecondaryForegroundBrush;
        }
    }

    private void UpdateInputLevel()
    {
        double level = _viewModel.InputLevel;
        double maxWidth = InputLevelBar.Parent is Grid grid ? grid.ActualWidth : 200;

        InputLevelBar.Width = level * maxWidth;

        if (level > 0.9)
        {
            InputLevelBar.Background = ErrorBrush;
            InputPeakIndicator.Visibility = Visibility.Visible;
            Canvas.SetLeft(InputPeakIndicator, level * maxWidth - 3);
        }
        else if (level > 0.7)
        {
            InputLevelBar.Background = WarningBrush;
            InputPeakIndicator.Visibility = Visibility.Collapsed;
        }
        else
        {
            InputLevelBar.Background = AccentBrush;
            InputPeakIndicator.Visibility = Visibility.Collapsed;
        }

        // Update text
        if (level < 0.01)
        {
            InputLevelText.Text = "No Signal";
        }
        else
        {
            double db = 20 * Math.Log10(Math.Max(level, 0.0001));
            InputLevelText.Text = $"{db:F1} dB";
        }
    }

    private void UpdateStrobeDisplay()
    {
        double cents = _viewModel.CentsDeviation;

        // Strobe speed based on cents deviation (stationary when in tune)
        double speed = cents / 10.0; // Scale for visual effect
        _strobeOffset += speed * 0.5;

        // Wrap around
        double totalWidth = _strobeLines.Count > 0 ? (_strobeLines[0].Width + 6) * _strobeLines.Count : 1;
        _strobeOffset %= totalWidth;
        if (_strobeOffset < 0) _strobeOffset += totalWidth;

        // Update bar positions and opacity
        for (int i = 0; i < _strobeLines.Count; i++)
        {
            double baseX = i * 14; // barWidth + gap
            double newX = baseX - _strobeOffset;

            // Wrap around
            if (newX < -8) newX += totalWidth;
            if (newX > StrobeCanvas.ActualWidth) newX -= totalWidth;

            Canvas.SetLeft(_strobeLines[i], newX);

            // Fade bars at edges
            double edgeFade = Math.Min(1, Math.Min(newX / 30, (StrobeCanvas.ActualWidth - newX) / 30));
            _strobeLines[i].Opacity = Math.Max(0.2, edgeFade);
        }

        var strobeBrush = GetTuningBrush(cents);
        foreach (var bar in _strobeLines)
        {
            bar.Fill = strobeBrush;
        }
    }

    #endregion

    #region Helper Methods

    private static PathGeometry CreateArcGeometry(double centerX, double centerY, double radius, double startAngle, double endAngle, double strokeThickness)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure();

        // Convert angles to radians
        double startRad = startAngle * Math.PI / 180;
        double endRad = endAngle * Math.PI / 180;

        // Start point
        double startX = centerX + radius * Math.Cos(startRad);
        double startY = centerY - radius * Math.Sin(startRad);
        figure.StartPoint = new Point(startX, startY);

        // End point
        double endX = centerX + radius * Math.Cos(endRad);
        double endY = centerY - radius * Math.Sin(endRad);

        // Arc segment
        var arc = new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = Math.Abs(endAngle - startAngle) > 180
        };

        figure.Segments.Add(arc);
        geometry.Figures.Add(figure);

        return geometry;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the tuner with new pitch detection data.
    /// </summary>
    /// <param name="frequency">Detected frequency in Hz.</param>
    /// <param name="level">Input signal level (0.0 to 1.0).</param>
    public void UpdatePitchData(double frequency, double level)
    {
        DetectedFrequency = frequency;
        InputLevel = level;
    }

    /// <summary>
    /// Resets the tuner display.
    /// </summary>
    public void Reset()
    {
        DetectedFrequency = 0;
        InputLevel = 0;
        _strobeOffset = 0;

        NoteNameText.Text = "--";
        OctaveText.Text = "4";
        CentsText.Text = "0 cents";
        FrequencyText.Text = "--- Hz";
        InputLevelText.Text = "No Signal";

        if (_inTuneGlow != null)
        {
            _inTuneGlow.Opacity = 0;
        }
    }

    #endregion
}
