// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Organ Synthesizer Editor control.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for OrganSynthControl.xaml.
/// Provides a visual editor for tonewheel organ synthesis with
/// Hammond-style drawbars, percussion, vibrato/chorus, and Leslie rotary speaker.
/// </summary>
public partial class OrganSynthControl : UserControl
{
    #region Fields

    private readonly DispatcherTimer _rotaryAnimationTimer;
    private double _hornAngle;
    private double _drumAngle;
    private double _targetHornSpeed;
    private double _targetDrumSpeed;
    private double _currentHornSpeed;
    private double _currentDrumSpeed;

    // Animation constants
    private const double SlowHornRpm = 48.0;
    private const double FastHornRpm = 400.0;
    private const double SlowDrumRpm = 40.0;
    private const double FastDrumRpm = 340.0;
    private const double AccelerationRate = 2.0;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new OrganSynthControl.
    /// </summary>
    public OrganSynthControl()
    {
        InitializeComponent();

        // Setup rotary speaker animation timer
        _rotaryAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _rotaryAnimationTimer.Tick += RotaryAnimationTimer_Tick;

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Properties

    private OrganSynthViewModel? ViewModel => DataContext as OrganSynthViewModel;

    #endregion

    #region Event Handlers

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is OrganSynthViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is OrganSynthViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            UpdateRotaryTargetSpeeds();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _rotaryAnimationTimer.Start();
        DrawRotarySpeaker();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _rotaryAnimationTimer.Stop();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OrganSynthViewModel.RotaryEnabled):
            case nameof(OrganSynthViewModel.IsRotaryStop):
            case nameof(OrganSynthViewModel.IsRotarySlow):
            case nameof(OrganSynthViewModel.IsRotaryFast):
                UpdateRotaryTargetSpeeds();
                break;
        }
    }

    private void RotaryAnimationTimer_Tick(object? sender, EventArgs e)
    {
        UpdateRotarySpeeds();
        UpdateRotaryAngles();
        DrawRotarySpeaker();
    }

    #endregion

    #region Rotary Speaker Animation

    private void UpdateRotaryTargetSpeeds()
    {
        if (ViewModel == null || !ViewModel.RotaryEnabled)
        {
            _targetHornSpeed = 0;
            _targetDrumSpeed = 0;
            return;
        }

        if (ViewModel.IsRotaryStop)
        {
            _targetHornSpeed = 0;
            _targetDrumSpeed = 0;
        }
        else if (ViewModel.IsRotarySlow)
        {
            _targetHornSpeed = SlowHornRpm;
            _targetDrumSpeed = SlowDrumRpm;
        }
        else if (ViewModel.IsRotaryFast)
        {
            _targetHornSpeed = FastHornRpm;
            _targetDrumSpeed = FastDrumRpm;
        }
    }

    private void UpdateRotarySpeeds()
    {
        double deltaTime = 0.016; // ~16ms per frame
        double accelAmount = AccelerationRate * deltaTime * 100;

        // Accelerate/decelerate horn
        if (_currentHornSpeed < _targetHornSpeed)
        {
            _currentHornSpeed = Math.Min(_currentHornSpeed + accelAmount, _targetHornSpeed);
        }
        else if (_currentHornSpeed > _targetHornSpeed)
        {
            _currentHornSpeed = Math.Max(_currentHornSpeed - accelAmount * 0.5, _targetHornSpeed);
        }

        // Accelerate/decelerate drum (slower acceleration)
        if (_currentDrumSpeed < _targetDrumSpeed)
        {
            _currentDrumSpeed = Math.Min(_currentDrumSpeed + accelAmount * 0.7, _targetDrumSpeed);
        }
        else if (_currentDrumSpeed > _targetDrumSpeed)
        {
            _currentDrumSpeed = Math.Max(_currentDrumSpeed - accelAmount * 0.3, _targetDrumSpeed);
        }
    }

    private void UpdateRotaryAngles()
    {
        double deltaTime = 0.016;

        // Convert RPM to radians per frame
        _hornAngle += _currentHornSpeed / 60.0 * 2.0 * Math.PI * deltaTime;
        _drumAngle += _currentDrumSpeed / 60.0 * 2.0 * Math.PI * deltaTime;

        // Keep angles in reasonable range
        if (_hornAngle > 2.0 * Math.PI) _hornAngle -= 2.0 * Math.PI;
        if (_drumAngle > 2.0 * Math.PI) _drumAngle -= 2.0 * Math.PI;
    }

    private void DrawRotarySpeaker()
    {
        if (RotaryCanvas == null) return;

        RotaryCanvas.Children.Clear();

        double width = RotaryCanvas.ActualWidth > 0 ? RotaryCanvas.ActualWidth : 260;
        double height = RotaryCanvas.ActualHeight > 0 ? RotaryCanvas.ActualHeight : 140;
        double centerX = width / 2;

        // Draw cabinet outline
        var cabinet = new Rectangle
        {
            Width = width - 20,
            Height = height - 10,
            Stroke = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            RadiusX = 4,
            RadiusY = 4
        };
        Canvas.SetLeft(cabinet, 10);
        Canvas.SetTop(cabinet, 5);
        RotaryCanvas.Children.Add(cabinet);

        // Draw horn rotor (top section)
        double hornCenterY = height * 0.3;
        DrawRotor(centerX, hornCenterY, 35, _hornAngle, true);

        // Draw drum rotor (bottom section)
        double drumCenterY = height * 0.7;
        DrawRotor(centerX, drumCenterY, 30, _drumAngle, false);

        // Draw speed indicator
        string speedText = _currentHornSpeed < 10 ? "STOP" :
                          _currentHornSpeed < 100 ? "SLOW" : "FAST";
        var speedLabel = new TextBlock
        {
            Text = speedText,
            Foreground = new SolidColorBrush(
                _currentHornSpeed < 10 ? Color.FromRgb(0x80, 0x80, 0x80) :
                _currentHornSpeed < 100 ? Color.FromRgb(0x00, 0xFF, 0x88) :
                Color.FromRgb(0xFF, 0xB8, 0x00)),
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };
        speedLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(speedLabel, width - speedLabel.DesiredSize.Width - 15);
        Canvas.SetTop(speedLabel, 10);
        RotaryCanvas.Children.Add(speedLabel);

        // Draw RPM indicators
        var hornRpm = new TextBlock
        {
            Text = $"Horn: {_currentHornSpeed:F0} RPM",
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
            FontSize = 9
        };
        Canvas.SetLeft(hornRpm, 15);
        Canvas.SetTop(hornRpm, hornCenterY - 5);
        RotaryCanvas.Children.Add(hornRpm);

        var drumRpm = new TextBlock
        {
            Text = $"Drum: {_currentDrumSpeed:F0} RPM",
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
            FontSize = 9
        };
        Canvas.SetLeft(drumRpm, 15);
        Canvas.SetTop(drumRpm, drumCenterY - 5);
        RotaryCanvas.Children.Add(drumRpm);
    }

    private void DrawRotor(double centerX, double centerY, double radius, double angle, bool isHorn)
    {
        // Rotor background circle
        var rotorBg = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(rotorBg, centerX - radius);
        Canvas.SetTop(rotorBg, centerY - radius);
        RotaryCanvas.Children.Add(rotorBg);

        // Rotor blade/arm
        double bladeLength = radius * 0.85;
        double bladeWidth = isHorn ? 8 : 12;

        var blade = new Rectangle
        {
            Width = bladeWidth,
            Height = bladeLength * 2,
            Fill = new SolidColorBrush(isHorn ?
                Color.FromRgb(0x00, 0xD9, 0xFF) :
                Color.FromRgb(0x00, 0xFF, 0x88)),
            RadiusX = 2,
            RadiusY = 2,
            RenderTransform = new RotateTransform(angle * 180.0 / Math.PI, bladeWidth / 2, bladeLength),
            Opacity = 0.8
        };
        Canvas.SetLeft(blade, centerX - bladeWidth / 2);
        Canvas.SetTop(blade, centerY - bladeLength);
        RotaryCanvas.Children.Add(blade);

        // Center hub
        var hub = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(hub, centerX - 5);
        Canvas.SetTop(hub, centerY - 5);
        RotaryCanvas.Children.Add(hub);

        // Motion blur effect (when spinning fast)
        if ((isHorn ? _currentHornSpeed : _currentDrumSpeed) > 100)
        {
            var blur = new Ellipse
            {
                Width = radius * 1.8,
                Height = radius * 1.8,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(0x20, isHorn ? (byte)0x00 : (byte)0x00,
                                        isHorn ? (byte)0xD9 : (byte)0xFF,
                                        isHorn ? (byte)0xFF : (byte)0x88),
                    Colors.Transparent)
            };
            Canvas.SetLeft(blur, centerX - radius * 0.9);
            Canvas.SetTop(blur, centerY - radius * 0.9);
            RotaryCanvas.Children.Add(blur);
        }
    }

    #endregion
}
