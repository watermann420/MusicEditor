// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Harmonizer effect control with multi-voice harmony generation and keyboard visualization.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Harmonizer effect editor control with up to 4 harmony voices,
/// scale locking, formant correction, and keyboard visualization.
/// </summary>
public partial class HarmonizerControl : UserControl
{
    #region Constants

    private const int MaxVoices = 4;
    private const int KeyboardOctaves = 3;
    private const int StartOctave = 3; // Start at C3
    private const int KeysPerOctave = 12;
    private const double WhiteKeyWidth = 18;
    private const double BlackKeyWidth = 12;
    private const double WhiteKeyHeight = 50;
    private const double BlackKeyHeight = 32;

    private static readonly bool[] IsBlackKey = { false, true, false, true, false, false, true, false, true, false, true, false };
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    // Voice colors for visualization
    private static readonly Color[] VoiceColors =
    {
        Color.FromRgb(0x00, 0xD9, 0xFF), // Cyan
        Color.FromRgb(0x00, 0xFF, 0x88), // Green
        Color.FromRgb(0xFF, 0xB8, 0x00), // Orange
        Color.FromRgb(0xFF, 0x6B, 0x9D)  // Pink
    };

    #endregion

    #region Private Fields

    private readonly List<HarmonyVoice> _voices = new();
    private readonly Dictionary<int, Shapes.Rectangle> _whiteKeys = new();
    private readonly Dictionary<int, Shapes.Rectangle> _blackKeys = new();
    private readonly List<Border> _voicePanels = new();

    private bool _isInitialized;
    private bool _isBypassed;
    private bool _scaleLockEnabled;
    private bool _formantCorrectionEnabled;
    private bool _midiInputEnabled;
    private int _currentKey;
    private string _currentScale = "Major";
    private int _currentInputNote = -1;
    private readonly DispatcherTimer _updateTimer;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty InputNoteProperty =
        DependencyProperty.Register(nameof(InputNote), typeof(int), typeof(HarmonizerControl),
            new PropertyMetadata(-1, OnInputNoteChanged));

    public static readonly DependencyProperty MixProperty =
        DependencyProperty.Register(nameof(Mix), typeof(double), typeof(HarmonizerControl),
            new PropertyMetadata(0.5, OnMixChanged));

    /// <summary>
    /// Gets or sets the current input note (MIDI note number, -1 for none).
    /// </summary>
    public int InputNote
    {
        get => (int)GetValue(InputNoteProperty);
        set => SetValue(InputNoteProperty, value);
    }

    /// <summary>
    /// Gets or sets the dry/wet mix (0.0 to 1.0).
    /// </summary>
    public double Mix
    {
        get => (double)GetValue(MixProperty);
        set => SetValue(MixProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a harmonizer parameter changes.
    /// </summary>
    public event EventHandler<HarmonizerParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Raised when the bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Raised when a harmony voice is added or removed.
    /// </summary>
    public event EventHandler<HarmonyVoiceChangedEventArgs>? VoiceChanged;

    /// <summary>
    /// Raised when scale lock settings change.
    /// </summary>
    public event EventHandler<ScaleLockChangedEventArgs>? ScaleLockChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            _isBypassed = value;
            BypassToggle.IsChecked = value;
            UpdateActiveIndicator();
        }
    }

    /// <summary>
    /// Gets or sets whether scale lock is enabled.
    /// </summary>
    public bool ScaleLockEnabled
    {
        get => _scaleLockEnabled;
        set
        {
            _scaleLockEnabled = value;
            ScaleLockToggle.IsChecked = value;
            KeyComboBox.IsEnabled = value;
            ScaleComboBox.IsEnabled = value;
        }
    }

    /// <summary>
    /// Gets or sets whether formant correction is enabled.
    /// </summary>
    public bool FormantCorrectionEnabled
    {
        get => _formantCorrectionEnabled;
        set
        {
            _formantCorrectionEnabled = value;
            FormantCorrectionToggle.IsChecked = value;
        }
    }

    /// <summary>
    /// Gets or sets whether MIDI input mode is enabled.
    /// </summary>
    public bool MidiInputEnabled
    {
        get => _midiInputEnabled;
        set
        {
            _midiInputEnabled = value;
            MidiInputToggle.IsChecked = value;
        }
    }

    /// <summary>
    /// Gets the current harmony voices.
    /// </summary>
    public IReadOnlyList<HarmonyVoice> Voices => _voices.AsReadOnly();

    /// <summary>
    /// Gets the number of active voices.
    /// </summary>
    public int VoiceCount => _voices.Count;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new harmonizer control.
    /// </summary>
    public HarmonizerControl()
    {
        InitializeComponent();

        // Setup update timer for visualization
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildKeyboardVisualization();

        // Add one default voice
        if (_voices.Count == 0)
        {
            AddVoice(5); // Perfect 4th above
        }

        _isInitialized = true;
        _updateTimer.Start();
        UpdateVoiceCount();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _updateTimer.Stop();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            BuildKeyboardVisualization();
            UpdateKeyboardDisplay();
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateKeyboardDisplay();
    }

    #endregion

    #region Keyboard Visualization

    private void BuildKeyboardVisualization()
    {
        KeyboardCanvas.Children.Clear();
        _whiteKeys.Clear();
        _blackKeys.Clear();

        double canvasWidth = KeyboardCanvas.ActualWidth;
        double canvasHeight = KeyboardCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        int totalKeys = KeyboardOctaves * KeysPerOctave + 1; // Include final C
        int whiteKeyCount = 0;
        for (int i = 0; i < totalKeys; i++)
        {
            if (!IsBlackKey[i % 12]) whiteKeyCount++;
        }

        double actualWhiteKeyWidth = canvasWidth / whiteKeyCount;
        double actualBlackKeyWidth = actualWhiteKeyWidth * 0.65;
        double actualWhiteKeyHeight = canvasHeight;
        double actualBlackKeyHeight = canvasHeight * 0.6;

        Brush whiteKeyBrush = FindResource("HarmonizerWhiteKeyBrush") as Brush ?? Brushes.WhiteSmoke;
        Brush blackKeyBrush = FindResource("HarmonizerBlackKeyBrush") as Brush ?? Brushes.DarkGray;
        Brush borderBrush = FindResource("HarmonizerBorderBrush") as Brush ?? Brushes.Gray;

        // First pass: Draw white keys
        double whiteKeyX = 0;
        int startNote = StartOctave * 12; // C3 = 48

        for (int i = 0; i < totalKeys; i++)
        {
            int noteIndex = i % 12;
            int midiNote = startNote + i;

            if (!IsBlackKey[noteIndex])
            {
                var key = new Shapes.Rectangle
                {
                    Width = actualWhiteKeyWidth - 1,
                    Height = actualWhiteKeyHeight,
                    Fill = whiteKeyBrush,
                    Stroke = borderBrush,
                    StrokeThickness = 1,
                    RadiusX = 2,
                    RadiusY = 2,
                    Tag = midiNote
                };

                Canvas.SetLeft(key, whiteKeyX);
                Canvas.SetTop(key, 0);
                KeyboardCanvas.Children.Add(key);
                _whiteKeys[midiNote] = key;

                whiteKeyX += actualWhiteKeyWidth;
            }
        }

        // Second pass: Draw black keys on top
        whiteKeyX = 0;
        for (int i = 0; i < totalKeys; i++)
        {
            int noteIndex = i % 12;
            int midiNote = startNote + i;

            if (!IsBlackKey[noteIndex])
            {
                // Check if next note is a black key
                if (i + 1 < totalKeys && IsBlackKey[(i + 1) % 12])
                {
                    int blackMidiNote = midiNote + 1;
                    var blackKey = new Shapes.Rectangle
                    {
                        Width = actualBlackKeyWidth,
                        Height = actualBlackKeyHeight,
                        Fill = blackKeyBrush,
                        Stroke = borderBrush,
                        StrokeThickness = 1,
                        RadiusX = 2,
                        RadiusY = 2,
                        Tag = blackMidiNote
                    };

                    double blackKeyX = whiteKeyX + actualWhiteKeyWidth - (actualBlackKeyWidth / 2);
                    Canvas.SetLeft(blackKey, blackKeyX);
                    Canvas.SetTop(blackKey, 0);
                    Canvas.SetZIndex(blackKey, 1);
                    KeyboardCanvas.Children.Add(blackKey);
                    _blackKeys[blackMidiNote] = blackKey;
                }

                whiteKeyX += actualWhiteKeyWidth;
            }
        }
    }

    private void UpdateKeyboardDisplay()
    {
        if (!_isInitialized) return;

        Brush whiteKeyBrush = FindResource("HarmonizerWhiteKeyBrush") as Brush ?? Brushes.WhiteSmoke;
        Brush blackKeyBrush = FindResource("HarmonizerBlackKeyBrush") as Brush ?? Brushes.DarkGray;
        Brush activeKeyBrush = FindResource("HarmonizerActiveKeyBrush") as Brush ?? Brushes.Cyan;

        // Reset all keys
        foreach (var kvp in _whiteKeys)
        {
            kvp.Value.Fill = whiteKeyBrush;
        }
        foreach (var kvp in _blackKeys)
        {
            kvp.Value.Fill = blackKeyBrush;
        }

        // Highlight input note
        if (_currentInputNote >= 0)
        {
            HighlightKey(_currentInputNote, activeKeyBrush);

            // Highlight harmony notes
            for (int i = 0; i < _voices.Count; i++)
            {
                var voice = _voices[i];
                if (voice.IsEnabled && !voice.IsMuted)
                {
                    int harmonyNote = CalculateHarmonyNote(_currentInputNote, voice.Interval);
                    if (harmonyNote >= 0 && harmonyNote <= 127)
                    {
                        var voiceBrush = new SolidColorBrush(VoiceColors[i % VoiceColors.Length]);
                        HighlightKey(harmonyNote, voiceBrush);
                    }
                }
            }
        }
    }

    private void HighlightKey(int midiNote, Brush brush)
    {
        if (_whiteKeys.TryGetValue(midiNote, out var whiteKey))
        {
            whiteKey.Fill = brush;
        }
        else if (_blackKeys.TryGetValue(midiNote, out var blackKey))
        {
            blackKey.Fill = brush;
        }
    }

    private int CalculateHarmonyNote(int inputNote, int interval)
    {
        int harmonyNote = inputNote + interval;

        if (_scaleLockEnabled)
        {
            harmonyNote = SnapToScale(harmonyNote, _currentKey, _currentScale);
        }

        return harmonyNote;
    }

    private int SnapToScale(int note, int key, string scale)
    {
        int[] scaleIntervals = GetScaleIntervals(scale);
        if (scaleIntervals.Length == 0) return note;

        int noteInOctave = ((note - key) % 12 + 12) % 12;
        int octave = (note - key) / 12;

        // Find closest scale degree
        int closestInterval = 0;
        int minDistance = 12;

        foreach (int interval in scaleIntervals)
        {
            int distance = Math.Abs(noteInOctave - interval);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestInterval = interval;
            }
        }

        return key + octave * 12 + closestInterval;
    }

    private int[] GetScaleIntervals(string scale)
    {
        return scale switch
        {
            "Major" => new[] { 0, 2, 4, 5, 7, 9, 11 },
            "Minor" => new[] { 0, 2, 3, 5, 7, 8, 10 },
            "HarmonicMinor" => new[] { 0, 2, 3, 5, 7, 8, 11 },
            "MelodicMinor" => new[] { 0, 2, 3, 5, 7, 9, 11 },
            "Dorian" => new[] { 0, 2, 3, 5, 7, 9, 10 },
            "Phrygian" => new[] { 0, 1, 3, 5, 7, 8, 10 },
            "Lydian" => new[] { 0, 2, 4, 6, 7, 9, 11 },
            "Mixolydian" => new[] { 0, 2, 4, 5, 7, 9, 10 },
            "Chromatic" => new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            _ => new[] { 0, 2, 4, 5, 7, 9, 11 } // Default to Major
        };
    }

    #endregion

    #region Voice Management

    /// <summary>
    /// Adds a new harmony voice with the specified interval.
    /// </summary>
    public HarmonyVoice? AddVoice(int interval = 0)
    {
        if (_voices.Count >= MaxVoices)
        {
            StatusText.Text = "Maximum 4 voices";
            return null;
        }

        var voice = new HarmonyVoice
        {
            Index = _voices.Count,
            Interval = interval,
            Detune = 0,
            Level = 0.8,
            Pan = 0,
            Delay = 0,
            IsEnabled = true,
            IsMuted = false
        };

        _voices.Add(voice);
        CreateVoicePanel(voice);
        UpdateVoiceCount();

        VoiceChanged?.Invoke(this, new HarmonyVoiceChangedEventArgs(voice, true));
        StatusText.Text = $"Voice {voice.Index + 1} added";

        return voice;
    }

    /// <summary>
    /// Removes a harmony voice at the specified index.
    /// </summary>
    public void RemoveVoice(int index)
    {
        if (index < 0 || index >= _voices.Count) return;

        var voice = _voices[index];
        _voices.RemoveAt(index);

        if (index < _voicePanels.Count)
        {
            VoiceSlotsPanel.Children.Remove(_voicePanels[index]);
            _voicePanels.RemoveAt(index);
        }

        // Update indices
        for (int i = 0; i < _voices.Count; i++)
        {
            _voices[i].Index = i;
        }

        RebuildVoicePanels();
        UpdateVoiceCount();

        VoiceChanged?.Invoke(this, new HarmonyVoiceChangedEventArgs(voice, false));
        StatusText.Text = $"Voice removed";
    }

    private void CreateVoicePanel(HarmonyVoice voice)
    {
        var voiceColor = VoiceColors[voice.Index % VoiceColors.Length];
        var voiceBrush = new SolidColorBrush(voiceColor);

        var panel = new Border
        {
            Style = FindResource("HarmonizerVoicePanelStyle") as Style,
            BorderBrush = voiceBrush,
            BorderThickness = new Thickness(2, 2, 2, 2),
            Tag = voice.Index
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header row with voice number and enable/mute buttons
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(headerGrid, 0);

        var voiceLabel = new TextBlock
        {
            Text = $"Voice {voice.Index + 1}",
            FontWeight = FontWeights.SemiBold,
            Foreground = voiceBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(voiceLabel, 0);
        headerGrid.Children.Add(voiceLabel);

        var enableToggle = new ToggleButton
        {
            Content = "ON",
            IsChecked = voice.IsEnabled,
            Style = FindResource("HarmonizerSmallToggleStyle") as Style,
            Tag = voice.Index,
            Margin = new Thickness(0, 0, 4, 0)
        };
        enableToggle.Click += VoiceEnableToggle_Click;
        Grid.SetColumn(enableToggle, 1);
        headerGrid.Children.Add(enableToggle);

        var muteToggle = new ToggleButton
        {
            Content = "M",
            IsChecked = voice.IsMuted,
            Style = FindResource("HarmonizerSmallToggleStyle") as Style,
            Tag = voice.Index,
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = "Mute voice"
        };
        muteToggle.Click += VoiceMuteToggle_Click;
        Grid.SetColumn(muteToggle, 2);
        headerGrid.Children.Add(muteToggle);

        var removeButton = new Button
        {
            Content = "X",
            Tag = voice.Index,
            Padding = new Thickness(6, 4, 6, 4),
            FontSize = 10,
            ToolTip = "Remove voice"
        };
        removeButton.Click += RemoveVoiceButton_Click;
        Grid.SetColumn(removeButton, 3);
        headerGrid.Children.Add(removeButton);

        grid.Children.Add(headerGrid);

        // Row 1: Interval and Detune
        var row1 = CreateParameterRow(
            "Interval", $"{FormatInterval(voice.Interval)}",
            -24, 24, voice.Interval, 1,
            (s, e) => OnVoiceIntervalChanged(voice.Index, (int)e.NewValue),
            "Detune", $"{voice.Detune:+0;-0;0} ct",
            -100, 100, voice.Detune, 1,
            (s, e) => OnVoiceDetuneChanged(voice.Index, (int)e.NewValue));
        Grid.SetRow(row1, 1);
        grid.Children.Add(row1);

        // Row 2: Level and Pan
        var row2 = CreateParameterRow(
            "Level", $"{voice.Level * 100:0}%",
            0, 100, voice.Level * 100, 1,
            (s, e) => OnVoiceLevelChanged(voice.Index, e.NewValue / 100),
            "Pan", FormatPan(voice.Pan),
            -100, 100, voice.Pan * 100, 1,
            (s, e) => OnVoicePanChanged(voice.Index, e.NewValue / 100));
        Grid.SetRow(row2, 2);
        grid.Children.Add(row2);

        // Row 3: Delay only
        var row3 = CreateSingleParameterRow(
            "Delay", $"{voice.Delay:0} ms",
            0, 100, voice.Delay, 1,
            (s, e) => OnVoiceDelayChanged(voice.Index, e.NewValue));
        Grid.SetRow(row3, 3);
        grid.Children.Add(row3);

        panel.Child = grid;
        VoiceSlotsPanel.Children.Add(panel);
        _voicePanels.Add(panel);
    }

    private Grid CreateParameterRow(
        string label1, string value1, double min1, double max1, double current1, double tick1,
        RoutedPropertyChangedEventHandler<double> handler1,
        string label2, string value2, double min2, double max2, double current2, double tick2,
        RoutedPropertyChangedEventHandler<double> handler2)
    {
        var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(CreateParameterPanel(label1, value1, min1, max1, current1, tick1, handler1, 0));
        grid.Children.Add(CreateParameterPanel(label2, value2, min2, max2, current2, tick2, handler2, 2));

        return grid;
    }

    private Grid CreateSingleParameterRow(
        string label, string value, double min, double max, double current, double tick,
        RoutedPropertyChangedEventHandler<double> handler)
    {
        var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(CreateParameterPanel(label, value, min, max, current, tick, handler, 0));

        return grid;
    }

    private StackPanel CreateParameterPanel(
        string label, string value, double min, double max, double current, double tick,
        RoutedPropertyChangedEventHandler<double> handler, int column)
    {
        var panel = new StackPanel();
        Grid.SetColumn(panel, column);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            Style = FindResource("HarmonizerParameterLabelStyle") as Style
        };
        Grid.SetColumn(labelBlock, 0);
        headerGrid.Children.Add(labelBlock);

        var valueBlock = new TextBlock
        {
            Text = value,
            Style = FindResource("HarmonizerParameterValueStyle") as Style,
            Tag = label
        };
        Grid.SetColumn(valueBlock, 1);
        headerGrid.Children.Add(valueBlock);

        panel.Children.Add(headerGrid);

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = current,
            TickFrequency = tick,
            IsSnapToTickEnabled = tick >= 1,
            Style = FindResource("HarmonizerSliderStyle") as Style,
            Tag = label
        };
        slider.ValueChanged += handler;
        panel.Children.Add(slider);

        return panel;
    }

    private void RebuildVoicePanels()
    {
        VoiceSlotsPanel.Children.Clear();
        _voicePanels.Clear();

        foreach (var voice in _voices)
        {
            CreateVoicePanel(voice);
        }
    }

    private void UpdateVoiceCount()
    {
        AddVoiceButton.IsEnabled = _voices.Count < MaxVoices;
    }

    #endregion

    #region Event Handlers

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = BypassToggle.IsChecked == true;
        UpdateActiveIndicator();
        BypassChanged?.Invoke(this, _isBypassed);
        StatusText.Text = _isBypassed ? "Bypassed" : "Active";
    }

    private void MidiInputToggle_Click(object sender, RoutedEventArgs e)
    {
        _midiInputEnabled = MidiInputToggle.IsChecked == true;
        RaiseParameterChanged("MidiInput", _midiInputEnabled ? 1 : 0);
        StatusText.Text = _midiInputEnabled ? "MIDI input enabled" : "MIDI input disabled";
    }

    private void ScaleLockToggle_Click(object sender, RoutedEventArgs e)
    {
        _scaleLockEnabled = ScaleLockToggle.IsChecked == true;
        KeyComboBox.IsEnabled = _scaleLockEnabled;
        ScaleComboBox.IsEnabled = _scaleLockEnabled;

        ScaleLockChanged?.Invoke(this, new ScaleLockChangedEventArgs(_scaleLockEnabled, _currentKey, _currentScale));
        RaiseParameterChanged("ScaleLock", _scaleLockEnabled ? 1 : 0);
        StatusText.Text = _scaleLockEnabled ? "Scale lock enabled" : "Scale lock disabled";
    }

    private void KeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KeyComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int key))
        {
            _currentKey = key;
            ScaleLockChanged?.Invoke(this, new ScaleLockChangedEventArgs(_scaleLockEnabled, _currentKey, _currentScale));
            RaiseParameterChanged("Key", _currentKey);
        }
    }

    private void ScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScaleComboBox.SelectedItem is ComboBoxItem item && item.Tag is string scale)
        {
            _currentScale = scale;
            ScaleLockChanged?.Invoke(this, new ScaleLockChangedEventArgs(_scaleLockEnabled, _currentKey, _currentScale));
            RaiseParameterChanged("Scale", GetScaleIndex(_currentScale));
        }
    }

    private void FormantCorrectionToggle_Click(object sender, RoutedEventArgs e)
    {
        _formantCorrectionEnabled = FormantCorrectionToggle.IsChecked == true;
        RaiseParameterChanged("FormantCorrection", _formantCorrectionEnabled ? 1 : 0);
        StatusText.Text = _formantCorrectionEnabled ? "Formant correction on" : "Formant correction off";
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null) return;
        MixValue.Text = $"{e.NewValue:0}%";
        SetValue(MixProperty, e.NewValue / 100.0);
        RaiseParameterChanged("Mix", e.NewValue / 100.0);
    }

    private void AddVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        AddVoice(0);
    }

    private void RemoveVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index)
        {
            RemoveVoice(index);
        }
    }

    private void VoiceEnableToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggle && toggle.Tag is int index && index < _voices.Count)
        {
            _voices[index].IsEnabled = toggle.IsChecked == true;
            RaiseParameterChanged($"Voice{index}_Enable", _voices[index].IsEnabled ? 1 : 0);
        }
    }

    private void VoiceMuteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggle && toggle.Tag is int index && index < _voices.Count)
        {
            _voices[index].IsMuted = toggle.IsChecked == true;
            RaiseParameterChanged($"Voice{index}_Mute", _voices[index].IsMuted ? 1 : 0);
        }
    }

    private void OnVoiceIntervalChanged(int index, int value)
    {
        if (index >= _voices.Count) return;
        _voices[index].Interval = value;
        UpdateVoiceValueDisplay(index, "Interval", FormatInterval(value));
        RaiseParameterChanged($"Voice{index}_Interval", value);
    }

    private void OnVoiceDetuneChanged(int index, int value)
    {
        if (index >= _voices.Count) return;
        _voices[index].Detune = value;
        UpdateVoiceValueDisplay(index, "Detune", $"{value:+0;-0;0} ct");
        RaiseParameterChanged($"Voice{index}_Detune", value);
    }

    private void OnVoiceLevelChanged(int index, double value)
    {
        if (index >= _voices.Count) return;
        _voices[index].Level = value;
        UpdateVoiceValueDisplay(index, "Level", $"{value * 100:0}%");
        RaiseParameterChanged($"Voice{index}_Level", value);
    }

    private void OnVoicePanChanged(int index, double value)
    {
        if (index >= _voices.Count) return;
        _voices[index].Pan = value;
        UpdateVoiceValueDisplay(index, "Pan", FormatPan(value));
        RaiseParameterChanged($"Voice{index}_Pan", value);
    }

    private void OnVoiceDelayChanged(int index, double value)
    {
        if (index >= _voices.Count) return;
        _voices[index].Delay = value;
        UpdateVoiceValueDisplay(index, "Delay", $"{value:0} ms");
        RaiseParameterChanged($"Voice{index}_Delay", value);
    }

    private void UpdateVoiceValueDisplay(int voiceIndex, string paramName, string value)
    {
        if (voiceIndex >= _voicePanels.Count) return;

        var panel = _voicePanels[voiceIndex];
        var textBlocks = FindVisualChildren<TextBlock>(panel);
        foreach (var tb in textBlocks)
        {
            if (tb.Tag is string tag && tag == paramName)
            {
                tb.Text = value;
                break;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject obj) where T : DependencyObject
    {
        if (obj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is T t) yield return t;

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnInputNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HarmonizerControl control)
        {
            control._currentInputNote = (int)e.NewValue;
            control.UpdateKeyboardDisplay();
        }
    }

    private static void OnMixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HarmonizerControl control)
        {
            control.MixSlider.Value = (double)e.NewValue * 100;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the input note for visualization.
    /// </summary>
    public void SetInputNote(int midiNote)
    {
        InputNote = midiNote;
    }

    /// <summary>
    /// Gets the harmony notes for a given input note.
    /// </summary>
    public int[] GetHarmonyNotes(int inputNote)
    {
        var notes = new List<int>();
        foreach (var voice in _voices)
        {
            if (voice.IsEnabled && !voice.IsMuted)
            {
                int harmonyNote = CalculateHarmonyNote(inputNote, voice.Interval);
                if (harmonyNote >= 0 && harmonyNote <= 127)
                {
                    notes.Add(harmonyNote);
                }
            }
        }
        return notes.ToArray();
    }

    /// <summary>
    /// Resets all parameters to default values.
    /// </summary>
    public void Reset()
    {
        _voices.Clear();
        RebuildVoicePanels();

        AddVoice(5); // Perfect 4th

        _scaleLockEnabled = false;
        _formantCorrectionEnabled = false;
        _midiInputEnabled = false;
        _isBypassed = false;
        _currentKey = 0;
        _currentScale = "Major";

        ScaleLockToggle.IsChecked = false;
        FormantCorrectionToggle.IsChecked = false;
        MidiInputToggle.IsChecked = false;
        BypassToggle.IsChecked = false;
        KeyComboBox.SelectedIndex = 0;
        ScaleComboBox.SelectedIndex = 0;
        KeyComboBox.IsEnabled = false;
        ScaleComboBox.IsEnabled = false;
        MixSlider.Value = 50;

        UpdateActiveIndicator();
        StatusText.Text = "Reset to defaults";
    }

    #endregion

    #region Helper Methods

    private void UpdateActiveIndicator()
    {
        ActiveIndicator.Fill = _isBypassed
            ? new SolidColorBrush(Color.FromRgb(128, 128, 128))
            : FindResource("HarmonizerSuccessBrush") as Brush ?? Brushes.Green;
    }

    private void RaiseParameterChanged(string name, double value)
    {
        ParameterChanged?.Invoke(this, new HarmonizerParameterChangedEventArgs(name, value));
    }

    private static string FormatInterval(int semitones)
    {
        if (semitones == 0) return "Unison";
        string sign = semitones > 0 ? "+" : "";
        return $"{sign}{semitones} st";
    }

    private static string FormatPan(double pan)
    {
        if (Math.Abs(pan) < 0.01) return "C";
        if (pan < 0) return $"L{(int)(-pan * 100)}";
        return $"R{(int)(pan * 100)}";
    }

    private static int GetScaleIndex(string scale)
    {
        return scale switch
        {
            "Major" => 0,
            "Minor" => 1,
            "HarmonicMinor" => 2,
            "MelodicMinor" => 3,
            "Dorian" => 4,
            "Phrygian" => 5,
            "Lydian" => 6,
            "Mixolydian" => 7,
            "Chromatic" => 8,
            _ => 0
        };
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Represents a harmony voice with all its parameters.
/// </summary>
public class HarmonyVoice
{
    /// <summary>
    /// Gets or sets the voice index (0-3).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the interval in semitones (-24 to +24).
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Gets or sets the fine detune in cents (-100 to +100).
    /// </summary>
    public int Detune { get; set; }

    /// <summary>
    /// Gets or sets the voice level (0.0 to 1.0).
    /// </summary>
    public double Level { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the stereo pan position (-1.0 to +1.0).
    /// </summary>
    public double Pan { get; set; }

    /// <summary>
    /// Gets or sets the timing delay in milliseconds (0 to 100).
    /// </summary>
    public double Delay { get; set; }

    /// <summary>
    /// Gets or sets whether the voice is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the voice is muted.
    /// </summary>
    public bool IsMuted { get; set; }
}

/// <summary>
/// Event arguments for harmonizer parameter changes.
/// </summary>
public class HarmonizerParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    public HarmonizerParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

/// <summary>
/// Event arguments for harmony voice changes.
/// </summary>
public class HarmonyVoiceChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the affected voice.
    /// </summary>
    public HarmonyVoice Voice { get; }

    /// <summary>
    /// Gets whether the voice was added (true) or removed (false).
    /// </summary>
    public bool WasAdded { get; }

    public HarmonyVoiceChangedEventArgs(HarmonyVoice voice, bool wasAdded)
    {
        Voice = voice;
        WasAdded = wasAdded;
    }
}

/// <summary>
/// Event arguments for scale lock changes.
/// </summary>
public class ScaleLockChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets whether scale lock is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets the root key (0-11, where 0=C).
    /// </summary>
    public int Key { get; }

    /// <summary>
    /// Gets the scale type name.
    /// </summary>
    public string Scale { get; }

    public ScaleLockChangedEventArgs(bool isEnabled, int key, string scale)
    {
        IsEnabled = isEnabled;
        Key = key;
        Scale = scale;
    }
}

#endregion

#region Converters

/// <summary>
/// Converter for interval display.
/// </summary>
public class HarmonizerIntervalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int semitones)
        {
            if (semitones == 0) return "Unison";
            string sign = semitones > 0 ? "+" : "";
            return $"{sign}{semitones} st";
        }
        return "0 st";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for detune display in cents.
/// </summary>
public class HarmonizerDetuneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int cents)
        {
            return $"{cents:+0;-0;0} ct";
        }
        return "0 ct";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for pan display.
/// </summary>
public class HarmonizerPanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pan)
        {
            if (Math.Abs(pan) < 0.01) return "C";
            if (pan < 0) return $"L{(int)(-pan * 100)}";
            return $"R{(int)(pan * 100)}";
        }
        return "C";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for percentage display.
/// </summary>
public class HarmonizerPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return $"{percent * 100:0}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
