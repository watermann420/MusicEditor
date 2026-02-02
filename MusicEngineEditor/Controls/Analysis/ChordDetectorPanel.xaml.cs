// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Chord detector panel for real-time chord detection and analysis.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MusicEngineEditor.ViewModels.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Chord Detector Panel providing real-time chord detection and analysis visualization.
/// Features large chord name display, piano keyboard visualization, guitar diagrams,
/// Roman numeral analysis, confidence meter, and chord history.
/// </summary>
public partial class ChordDetectorPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty DetectedNotesProperty =
        DependencyProperty.Register(nameof(DetectedNotes), typeof(int[]), typeof(ChordDetectorPanel),
            new PropertyMetadata(Array.Empty<int>(), OnDetectedNotesChanged));

    public static readonly DependencyProperty IsDetectionActiveProperty =
        DependencyProperty.Register(nameof(IsDetectionActive), typeof(bool), typeof(ChordDetectorPanel),
            new PropertyMetadata(true, OnIsDetectionActiveChanged));

    public static readonly DependencyProperty SensitivityProperty =
        DependencyProperty.Register(nameof(Sensitivity), typeof(double), typeof(ChordDetectorPanel),
            new PropertyMetadata(0.5, OnSensitivityChanged));

    public static readonly DependencyProperty IsMidiOutputEnabledProperty =
        DependencyProperty.Register(nameof(IsMidiOutputEnabled), typeof(bool), typeof(ChordDetectorPanel),
            new PropertyMetadata(false, OnIsMidiOutputEnabledChanged));

    public static readonly DependencyProperty CurrentKeyProperty =
        DependencyProperty.Register(nameof(CurrentKey), typeof(string), typeof(ChordDetectorPanel),
            new PropertyMetadata("C Major", OnCurrentKeyChanged));

    /// <summary>
    /// Gets or sets the array of detected MIDI note numbers.
    /// </summary>
    public int[] DetectedNotes
    {
        get => (int[])GetValue(DetectedNotesProperty);
        set => SetValue(DetectedNotesProperty, value);
    }

    /// <summary>
    /// Gets or sets whether chord detection is active.
    /// </summary>
    public bool IsDetectionActive
    {
        get => (bool)GetValue(IsDetectionActiveProperty);
        set => SetValue(IsDetectionActiveProperty, value);
    }

    /// <summary>
    /// Gets or sets the detection sensitivity (0.0 to 1.0).
    /// </summary>
    public double Sensitivity
    {
        get => (double)GetValue(SensitivityProperty);
        set => SetValue(SensitivityProperty, value);
    }

    /// <summary>
    /// Gets or sets whether MIDI output is enabled.
    /// </summary>
    public bool IsMidiOutputEnabled
    {
        get => (bool)GetValue(IsMidiOutputEnabledProperty);
        set => SetValue(IsMidiOutputEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the current key for Roman numeral analysis.
    /// </summary>
    public string CurrentKey
    {
        get => (string)GetValue(CurrentKeyProperty);
        set => SetValue(CurrentKeyProperty, value);
    }

    #endregion

    #region Routed Events

    public static readonly RoutedEvent ChordDetectedEvent =
        EventManager.RegisterRoutedEvent(nameof(ChordDetected), RoutingStrategy.Bubble,
            typeof(EventHandler<ChordDetectedEventArgs>), typeof(ChordDetectorPanel));

    public static readonly RoutedEvent MidiOutputRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(MidiOutputRequested), RoutingStrategy.Bubble,
            typeof(EventHandler<MidiOutputEventArgs>), typeof(ChordDetectorPanel));

    /// <summary>
    /// Occurs when a chord is detected.
    /// </summary>
    public event EventHandler<ChordDetectedEventArgs> ChordDetected
    {
        add => AddHandler(ChordDetectedEvent, value);
        remove => RemoveHandler(ChordDetectedEvent, value);
    }

    /// <summary>
    /// Occurs when MIDI output is requested for the detected chord.
    /// </summary>
    public event EventHandler<MidiOutputEventArgs> MidiOutputRequested
    {
        add => AddHandler(MidiOutputRequestedEvent, value);
        remove => RemoveHandler(MidiOutputRequestedEvent, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private readonly ChordDetectorViewModel _viewModel;

    // Piano keyboard constants
    private const int PianoOctaves = 2;
    private const int PianoStartOctave = 4; // C4 to B5
    private const int TotalPianoKeys = PianoOctaves * 12;
    private static readonly bool[] IsBlackKey = { false, true, false, true, false, false, true, false, true, false, true, false };
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    // Piano key rectangles
    private readonly List<Shapes.Rectangle> _whiteKeys = new();
    private readonly List<Shapes.Rectangle> _blackKeys = new();
    private readonly Dictionary<int, Shapes.Rectangle> _noteToKeyMap = new();

    // Guitar diagram elements
    private readonly List<UIElement> _guitarElements = new();

    // Colors
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color WhiteKeyColor = Color.FromRgb(0xE8, 0xE8, 0xE8);
    private static readonly Color BlackKeyColor = Color.FromRgb(0x2D, 0x2D, 0x2D);
    private static readonly Color BorderColor = Color.FromRgb(0x2A, 0x2A, 0x2A);

    #endregion

    #region Constructor

    public ChordDetectorPanel()
    {
        InitializeComponent();

        _viewModel = new ChordDetectorViewModel();
        DataContext = _viewModel;

        // Bind ViewModel properties to dependency properties
        SetupBindings();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        DrawPianoKeyboard();
        DrawGuitarDiagram();
        UpdateConfidenceMeter();
        UpdateDetectedNotesText();

        // Subscribe to ViewModel property changes
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized) return;

        switch (e.PropertyName)
        {
            case nameof(ChordDetectorViewModel.DetectedNotes):
                Dispatcher.Invoke(() =>
                {
                    HighlightDetectedNotes(_viewModel.DetectedNotes);
                    UpdateDetectedNotesText();
                });
                break;

            case nameof(ChordDetectorViewModel.Confidence):
                Dispatcher.Invoke(UpdateConfidenceMeter);
                break;

            case nameof(ChordDetectorViewModel.GuitarFrets):
            case nameof(ChordDetectorViewModel.HasGuitarDiagram):
                Dispatcher.Invoke(DrawGuitarDiagram);
                break;

            case nameof(ChordDetectorViewModel.ChordName):
                if (_viewModel.IsMidiOutputEnabled && _viewModel.DetectedNotes.Length > 0)
                {
                    RaiseMidiOutputRequested(_viewModel.DetectedNotes);
                }
                RaiseChordDetected();
                break;
        }
    }

    private static void OnDetectedNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordDetectorPanel panel && panel._isInitialized)
        {
            var notes = (int[])e.NewValue;
            panel._viewModel.AnalyzeNotes(notes);
        }
    }

    private static void OnIsDetectionActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordDetectorPanel panel)
        {
            panel._viewModel.IsDetectionActive = (bool)e.NewValue;
        }
    }

    private static void OnSensitivityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordDetectorPanel panel)
        {
            panel._viewModel.Sensitivity = (double)e.NewValue;
        }
    }

    private static void OnIsMidiOutputEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordDetectorPanel panel)
        {
            panel._viewModel.IsMidiOutputEnabled = (bool)e.NewValue;
        }
    }

    private static void OnCurrentKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChordDetectorPanel panel)
        {
            panel._viewModel.CurrentKey = (string)e.NewValue;
        }
    }

    private void PianoKeyboardCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawPianoKeyboard();
            HighlightDetectedNotes(_viewModel.DetectedNotes);
        }
    }

    private void GuitarDiagramCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawGuitarDiagram();
        }
    }

    #endregion

    #region Piano Keyboard Drawing

    private void DrawPianoKeyboard()
    {
        PianoKeyboardCanvas.Children.Clear();
        _whiteKeys.Clear();
        _blackKeys.Clear();
        _noteToKeyMap.Clear();

        double canvasWidth = PianoKeyboardCanvas.ActualWidth;
        double canvasHeight = PianoKeyboardCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        // Count white keys
        int whiteKeyCount = 0;
        for (int i = 0; i < TotalPianoKeys; i++)
        {
            if (!IsBlackKey[i % 12]) whiteKeyCount++;
        }

        double whiteKeyWidth = canvasWidth / whiteKeyCount;
        double blackKeyWidth = whiteKeyWidth * 0.6;
        double blackKeyHeight = canvasHeight * 0.6;

        // Draw white keys first
        double x = 0;
        for (int i = 0; i < TotalPianoKeys; i++)
        {
            int noteIndex = i % 12;
            int midiNote = (PianoStartOctave + 1) * 12 + i; // C4 = 60

            if (!IsBlackKey[noteIndex])
            {
                var whiteKey = new Shapes.Rectangle
                {
                    Width = whiteKeyWidth - 1,
                    Height = canvasHeight - 2,
                    Fill = new SolidColorBrush(WhiteKeyColor),
                    Stroke = new SolidColorBrush(BorderColor),
                    StrokeThickness = 1,
                    RadiusX = 2,
                    RadiusY = 2
                };

                Canvas.SetLeft(whiteKey, x);
                Canvas.SetTop(whiteKey, 1);
                PianoKeyboardCanvas.Children.Add(whiteKey);
                _whiteKeys.Add(whiteKey);
                _noteToKeyMap[midiNote] = whiteKey;

                x += whiteKeyWidth;
            }
        }

        // Draw black keys
        x = 0;
        for (int i = 0; i < TotalPianoKeys; i++)
        {
            int noteIndex = i % 12;
            int midiNote = (PianoStartOctave + 1) * 12 + i;

            if (!IsBlackKey[noteIndex])
            {
                x += whiteKeyWidth;
            }
            else
            {
                var blackKey = new Shapes.Rectangle
                {
                    Width = blackKeyWidth,
                    Height = blackKeyHeight,
                    Fill = new SolidColorBrush(BlackKeyColor),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                    StrokeThickness = 1,
                    RadiusX = 2,
                    RadiusY = 2
                };

                Canvas.SetLeft(blackKey, x - blackKeyWidth / 2 - whiteKeyWidth);
                Canvas.SetTop(blackKey, 1);
                Canvas.SetZIndex(blackKey, 1);
                PianoKeyboardCanvas.Children.Add(blackKey);
                _blackKeys.Add(blackKey);
                _noteToKeyMap[midiNote] = blackKey;
            }
        }
    }

    private void HighlightDetectedNotes(int[] notes)
    {
        // Reset all keys
        foreach (var key in _whiteKeys)
        {
            key.Fill = new SolidColorBrush(WhiteKeyColor);
        }
        foreach (var key in _blackKeys)
        {
            key.Fill = new SolidColorBrush(BlackKeyColor);
        }

        if (notes == null || notes.Length == 0) return;

        // Highlight detected notes
        foreach (int note in notes)
        {
            // Map to display range
            int displayNote = note;
            while (displayNote < (PianoStartOctave + 1) * 12)
            {
                displayNote += 12;
            }
            while (displayNote >= (PianoStartOctave + 1) * 12 + TotalPianoKeys)
            {
                displayNote -= 12;
            }

            if (_noteToKeyMap.TryGetValue(displayNote, out var key))
            {
                key.Fill = new SolidColorBrush(AccentColor);
            }
        }
    }

    #endregion

    #region Guitar Diagram Drawing

    private void DrawGuitarDiagram()
    {
        GuitarDiagramCanvas.Children.Clear();
        _guitarElements.Clear();

        if (!_viewModel.HasGuitarDiagram) return;

        double canvasWidth = GuitarDiagramCanvas.ActualWidth;
        double canvasHeight = GuitarDiagramCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        double margin = 10;
        double fretboardWidth = canvasWidth - margin * 2;
        double fretboardHeight = canvasHeight - margin * 2;
        double stringSpacing = fretboardWidth / 5;
        double fretSpacing = fretboardHeight / 5;

        var lineBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));
        var nutBrush = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
        var dotBrush = new SolidColorBrush(AccentColor);
        var mutedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));
        var openBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));

        // Draw nut (thick line at top)
        var nut = new Shapes.Rectangle
        {
            Width = fretboardWidth,
            Height = 4,
            Fill = nutBrush
        };
        Canvas.SetLeft(nut, margin);
        Canvas.SetTop(nut, margin);
        GuitarDiagramCanvas.Children.Add(nut);

        // Draw frets (horizontal lines)
        for (int i = 1; i <= 5; i++)
        {
            var fret = new Shapes.Line
            {
                X1 = margin,
                Y1 = margin + i * fretSpacing,
                X2 = margin + fretboardWidth,
                Y2 = margin + i * fretSpacing,
                Stroke = lineBrush,
                StrokeThickness = 1
            };
            GuitarDiagramCanvas.Children.Add(fret);
        }

        // Draw strings (vertical lines)
        for (int i = 0; i < 6; i++)
        {
            var guitarString = new Shapes.Line
            {
                X1 = margin + i * stringSpacing,
                Y1 = margin,
                X2 = margin + i * stringSpacing,
                Y2 = margin + fretboardHeight,
                Stroke = lineBrush,
                StrokeThickness = i < 3 ? 2 : 1 // Bass strings thicker
            };
            GuitarDiagramCanvas.Children.Add(guitarString);
        }

        // Draw finger positions
        var frets = _viewModel.GuitarFrets;
        int startFret = _viewModel.GuitarStartFret;

        for (int stringIndex = 0; stringIndex < 6 && stringIndex < frets.Length; stringIndex++)
        {
            int fret = frets[stringIndex];
            double x = margin + stringIndex * stringSpacing;

            if (fret == -1)
            {
                // Muted string - draw X above nut
                var mutedX = new TextBlock
                {
                    Text = "X",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = mutedBrush
                };
                Canvas.SetLeft(mutedX, x - 4);
                Canvas.SetTop(mutedX, 0);
                GuitarDiagramCanvas.Children.Add(mutedX);
            }
            else if (fret == 0)
            {
                // Open string - draw O above nut
                var openO = new Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Stroke = openBrush,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(openO, x - 4);
                Canvas.SetTop(openO, 0);
                GuitarDiagramCanvas.Children.Add(openO);
            }
            else
            {
                // Fretted position - draw filled circle
                int displayFret = fret - startFret + 1;
                if (displayFret >= 1 && displayFret <= 5)
                {
                    double y = margin + (displayFret - 0.5) * fretSpacing;
                    var dot = new Shapes.Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Fill = dotBrush
                    };
                    Canvas.SetLeft(dot, x - 6);
                    Canvas.SetTop(dot, y - 6);
                    GuitarDiagramCanvas.Children.Add(dot);
                }
            }
        }

        // Draw starting fret number if not 1
        if (startFret > 1)
        {
            var fretNumber = new TextBlock
            {
                Text = startFret.ToString(),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
            };
            Canvas.SetLeft(fretNumber, margin + fretboardWidth + 4);
            Canvas.SetTop(fretNumber, margin + fretSpacing / 2 - 6);
            GuitarDiagramCanvas.Children.Add(fretNumber);
        }
    }

    #endregion

    #region UI Update Methods

    private void SetupBindings()
    {
        // Create a BoolToVisibilityConverter resource if not already in Resources
        if (!Resources.Contains("BoolToVisibilityConverter"))
        {
            Resources.Add("BoolToVisibilityConverter", new BooleanToVisibilityConverter());
        }
    }

    private void UpdateConfidenceMeter()
    {
        double confidence = _viewModel.Confidence;
        double maxWidth = ((Grid)ConfidenceFill.Parent).ActualWidth;

        if (maxWidth > 0)
        {
            ConfidenceFill.Width = maxWidth * confidence;
        }

        // Update confidence color
        Color confColor;
        if (confidence >= 0.8)
        {
            confColor = Color.FromRgb(0x00, 0xD9, 0xFF); // Accent
        }
        else if (confidence >= 0.6)
        {
            confColor = Color.FromRgb(0x00, 0xFF, 0x88); // Success
        }
        else if (confidence >= 0.4)
        {
            confColor = Color.FromRgb(0xFF, 0xB8, 0x00); // Warning
        }
        else
        {
            confColor = Color.FromRgb(0xFF, 0x47, 0x57); // Error
        }

        ConfidenceText.Foreground = new SolidColorBrush(confColor);
    }

    private void UpdateDetectedNotesText()
    {
        var notes = _viewModel.DetectedNotes;
        if (notes == null || notes.Length == 0)
        {
            DetectedNotesText.Text = "";
            return;
        }

        var noteStrings = notes.Select(n =>
        {
            int noteIndex = n % 12;
            int octave = (n / 12) - 1;
            return $"{NoteNames[noteIndex]}{octave}";
        });

        DetectedNotesText.Text = string.Join(", ", noteStrings);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Analyzes a set of MIDI notes.
    /// </summary>
    /// <param name="midiNotes">Array of MIDI note numbers (0-127).</param>
    public void AnalyzeNotes(int[] midiNotes)
    {
        DetectedNotes = midiNotes;
    }

    /// <summary>
    /// Analyzes detected frequencies.
    /// </summary>
    /// <param name="frequencies">Fundamental frequencies in Hz.</param>
    /// <param name="amplitudes">Corresponding amplitudes (0-1).</param>
    public void AnalyzeFrequencies(float[] frequencies, float[] amplitudes)
    {
        _viewModel.AnalyzeFrequencies(frequencies, amplitudes);
    }

    /// <summary>
    /// Resets the chord detection.
    /// </summary>
    public void Reset()
    {
        _viewModel.ResetDetectionCommand.Execute(null);
    }

    /// <summary>
    /// Clears the chord history.
    /// </summary>
    public void ClearHistory()
    {
        _viewModel.ClearHistoryCommand.Execute(null);
    }

    /// <summary>
    /// Gets the current chord name.
    /// </summary>
    public string GetChordName()
    {
        return _viewModel.ChordName;
    }

    /// <summary>
    /// Gets the current chord quality.
    /// </summary>
    public ChordQuality GetChordQuality()
    {
        return _viewModel.Quality;
    }

    /// <summary>
    /// Gets the Roman numeral analysis.
    /// </summary>
    public string GetRomanNumeral()
    {
        return _viewModel.RomanNumeral;
    }

    #endregion

    #region Private Helper Methods

    private void RaiseChordDetected()
    {
        var args = new ChordDetectedEventArgs(ChordDetectedEvent, this)
        {
            ChordName = _viewModel.ChordName,
            RootNote = _viewModel.RootNote,
            Quality = _viewModel.Quality,
            BassNote = _viewModel.BassNote,
            IsSlashChord = _viewModel.IsSlashChord,
            Confidence = _viewModel.Confidence,
            RomanNumeral = _viewModel.RomanNumeral,
            DetectedNotes = _viewModel.DetectedNotes
        };

        RaiseEvent(args);
    }

    private void RaiseMidiOutputRequested(int[] notes)
    {
        var args = new MidiOutputEventArgs(MidiOutputRequestedEvent, this)
        {
            Notes = notes,
            ChordName = _viewModel.ChordName
        };

        RaiseEvent(args);
    }

    #endregion
}

#region Event Args

/// <summary>
/// Event arguments for chord detection events.
/// </summary>
public class ChordDetectedEventArgs : RoutedEventArgs
{
    public string ChordName { get; set; } = string.Empty;
    public string RootNote { get; set; } = string.Empty;
    public ChordQuality Quality { get; set; }
    public string BassNote { get; set; } = string.Empty;
    public bool IsSlashChord { get; set; }
    public double Confidence { get; set; }
    public string RomanNumeral { get; set; } = string.Empty;
    public int[] DetectedNotes { get; set; } = Array.Empty<int>();

    public ChordDetectedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source)
    {
    }
}

/// <summary>
/// Event arguments for MIDI output requests.
/// </summary>
public class MidiOutputEventArgs : RoutedEventArgs
{
    public int[] Notes { get; set; } = Array.Empty<int>();
    public string ChordName { get; set; } = string.Empty;

    public MidiOutputEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source)
    {
    }
}

#endregion

#region Converters

/// <summary>
/// Converts chord quality to a display color.
/// </summary>
public class ChordQualityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is ChordQuality quality)
        {
            return quality switch
            {
                ChordQuality.Major or ChordQuality.Major7 or ChordQuality.Major9 or ChordQuality.Major6
                    => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)), // Accent
                ChordQuality.Minor or ChordQuality.Minor7 or ChordQuality.Minor9 or ChordQuality.Minor6
                    => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)), // Success (green)
                ChordQuality.Diminished or ChordQuality.Diminished7 or ChordQuality.HalfDiminished7
                    => new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)), // Error (red)
                ChordQuality.Augmented or ChordQuality.Augmented7
                    => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)), // Warning (yellow)
                ChordQuality.Sus2 or ChordQuality.Sus4
                    => new SolidColorBrush(Color.FromRgb(0xA0, 0x80, 0xFF)), // Purple
                ChordQuality.Dominant7 or ChordQuality.Dominant9
                    => new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x00)), // Orange
                _ => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)) // Gray
            };
        }

        return new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts confidence value to a color.
/// </summary>
public class ConfidenceToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double confidence)
        {
            if (confidence >= 0.8)
                return new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)); // Accent
            if (confidence >= 0.6)
                return new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)); // Success
            if (confidence >= 0.4)
                return new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)); // Warning
            return new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)); // Error
        }

        return new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
