// MusicEngineEditor - Chord Stamp Panel Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: UI control for selecting and stamping chords in the Piano Roll.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A control panel for selecting chord types, root notes, inversions, and voicings
/// for stamping chords into the Piano Roll editor.
/// </summary>
public partial class ChordStampPanel : UserControl
{
    #region Private Fields

    private readonly ChordStampService _chordService;
    private readonly Dictionary<string, ChordDefinition> _chordLookup;
    private readonly ToggleButton[] _rootNoteButtons;
    private bool _isUpdating;

    #endregion

    #region Dependency Properties

    /// <summary>
    /// Identifies the NotesCollection dependency property.
    /// </summary>
    public static readonly DependencyProperty NotesCollectionProperty =
        DependencyProperty.Register(nameof(NotesCollection), typeof(ObservableCollection<PianoRollNote>),
            typeof(ChordStampPanel), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the StampPosition dependency property.
    /// </summary>
    public static readonly DependencyProperty StampPositionProperty =
        DependencyProperty.Register(nameof(StampPosition), typeof(double), typeof(ChordStampPanel),
            new PropertyMetadata(0.0));

    /// <summary>
    /// Identifies the StampDuration dependency property.
    /// </summary>
    public static readonly DependencyProperty StampDurationProperty =
        DependencyProperty.Register(nameof(StampDuration), typeof(double), typeof(ChordStampPanel),
            new PropertyMetadata(1.0));

    /// <summary>
    /// Gets or sets the notes collection to stamp chords into.
    /// </summary>
    public ObservableCollection<PianoRollNote>? NotesCollection
    {
        get => (ObservableCollection<PianoRollNote>?)GetValue(NotesCollectionProperty);
        set => SetValue(NotesCollectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the beat position where chords will be stamped.
    /// </summary>
    public double StampPosition
    {
        get => (double)GetValue(StampPositionProperty);
        set => SetValue(StampPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the duration for stamped chord notes.
    /// </summary>
    public double StampDuration
    {
        get => (double)GetValue(StampDurationProperty);
        set => SetValue(StampDurationProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a chord is stamped.
    /// </summary>
    public event EventHandler<ChordStampedEventArgs>? ChordStamped;

    /// <summary>
    /// Occurs when a chord stamp is requested (for preview before placing).
    /// </summary>
    public event EventHandler<ChordPreviewEventArgs>? ChordPreviewRequested;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new ChordStampPanel.
    /// </summary>
    public ChordStampPanel()
    {
        InitializeComponent();

        _chordService = ChordStampService.Instance;

        // Build chord lookup
        _chordLookup = ChordDefinition.AllChords.ToDictionary(c => c.Symbol, c => c);

        // Collect root note buttons
        _rootNoteButtons = new[]
        {
            RootC, RootCs, RootD, RootDs, RootE, RootF,
            RootFs, RootG, RootGs, RootA, RootAs, RootB
        };

        // Initialize UI
        Loaded += OnLoaded;
        DataContext = _chordService;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set initial values
        UpdateRootNoteButtons();
        UpdateInversionComboBox();
        UpdateVoicingComboBox();
        UpdateChordDisplay();

        // Subscribe to service events
        _chordService.PropertyChanged += OnChordServicePropertyChanged;
        _chordService.ChordPreviewRequested += OnServiceChordPreviewRequested;
    }

    private void OnChordServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isUpdating) return;

        switch (e.PropertyName)
        {
            case nameof(ChordStampService.CurrentRootPitchClass):
                UpdateRootNoteButtons();
                UpdateChordDisplay();
                break;
            case nameof(ChordStampService.CurrentChord):
                UpdateInversionComboBox();
                UpdateChordDisplay();
                break;
            case nameof(ChordStampService.CurrentInversion):
            case nameof(ChordStampService.CurrentVoicing):
            case nameof(ChordStampService.CurrentChordNotes):
                UpdateChordDisplay();
                break;
        }
    }

    private void OnServiceChordPreviewRequested(object? sender, ChordPreviewEventArgs e)
    {
        ChordPreviewRequested?.Invoke(this, e);
    }

    private void OnRootNoteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        if (button.Tag is not string tagStr || !int.TryParse(tagStr, out int pitchClass)) return;

        _isUpdating = true;
        try
        {
            _chordService.CurrentRootPitchClass = pitchClass;
            UpdateRootNoteButtons();

            if (_chordService.PreviewOnSelect)
            {
                _chordService.PreviewCurrentChord();
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnChordTypeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Tag is not string symbol) return;

        if (_chordLookup.TryGetValue(symbol, out var chord))
        {
            _isUpdating = true;
            try
            {
                _chordService.CurrentChord = chord;
                UpdateInversionComboBox();
                UpdateChordDisplay();

                if (_chordService.PreviewOnSelect)
                {
                    _chordService.PreviewCurrentChord();
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    private void OnInversionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (InversionComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string tagStr || !int.TryParse(tagStr, out int inversion)) return;

        _isUpdating = true;
        try
        {
            _chordService.CurrentInversion = inversion;
            UpdateChordDisplay();

            if (_chordService.PreviewOnSelect)
            {
                _chordService.PreviewCurrentChord();
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnVoicingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (VoicingComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string voicingName) return;

        _isUpdating = true;
        try
        {
            _chordService.CurrentVoicing = voicingName switch
            {
                "Open" => ChordVoicing.Open,
                "Drop2" => ChordVoicing.Drop2,
                "Drop3" => ChordVoicing.Drop3,
                _ => ChordVoicing.Close
            };
            UpdateChordDisplay();

            if (_chordService.PreviewOnSelect)
            {
                _chordService.PreviewCurrentChord();
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnStampClick(object sender, RoutedEventArgs e)
    {
        StampCurrentChord();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Stamps the current chord at the current stamp position.
    /// </summary>
    public void StampCurrentChord()
    {
        if (NotesCollection == null) return;

        var notes = _chordService.StampChord(NotesCollection, StampPosition, StampDuration);
        ChordStamped?.Invoke(this, new ChordStampedEventArgs(
            notes.ToList(),
            _chordService.CurrentChord,
            _chordService.CurrentRoot));
    }

    /// <summary>
    /// Stamps a specific chord.
    /// </summary>
    /// <param name="chord">The chord definition.</param>
    /// <param name="rootNote">The root MIDI note.</param>
    /// <param name="startBeat">Start position.</param>
    /// <param name="duration">Duration.</param>
    public void StampChord(ChordDefinition chord, int rootNote, double startBeat, double duration)
    {
        if (NotesCollection == null) return;

        var notes = _chordService.StampChord(
            NotesCollection, chord, rootNote, startBeat, duration,
            _chordService.DefaultVelocity,
            _chordService.CurrentInversion,
            _chordService.CurrentVoicing);

        ChordStamped?.Invoke(this, new ChordStampedEventArgs(notes.ToList(), chord, rootNote));
    }

    /// <summary>
    /// Gets preview notes for the current chord at a position.
    /// </summary>
    /// <param name="startBeat">Start position.</param>
    /// <param name="duration">Duration.</param>
    /// <returns>Preview notes.</returns>
    public IReadOnlyList<PianoRollNote> GetPreviewNotes(double startBeat, double duration)
    {
        return _chordService.GetPreviewNotes(startBeat, duration);
    }

    /// <summary>
    /// Previews the current chord.
    /// </summary>
    public void PreviewChord()
    {
        _chordService.PreviewCurrentChord();
    }

    /// <summary>
    /// Stops the chord preview.
    /// </summary>
    public void StopPreview()
    {
        _chordService.StopPreview();
    }

    #endregion

    #region Private Methods

    private void UpdateRootNoteButtons()
    {
        int currentRoot = _chordService.CurrentRootPitchClass;

        for (int i = 0; i < _rootNoteButtons.Length; i++)
        {
            if (_rootNoteButtons[i] != null)
            {
                _rootNoteButtons[i].IsChecked = (i == currentRoot);
            }
        }
    }

    private void UpdateInversionComboBox()
    {
        int maxInversion = _chordService.CurrentChord.MaxInversion;

        // Enable/disable items based on chord note count
        for (int i = 0; i < InversionComboBox.Items.Count; i++)
        {
            if (InversionComboBox.Items[i] is ComboBoxItem item)
            {
                item.IsEnabled = i <= maxInversion;
            }
        }

        // Clamp current inversion
        int currentInversion = Math.Min(_chordService.CurrentInversion, maxInversion);
        if (InversionComboBox.SelectedIndex != currentInversion)
        {
            InversionComboBox.SelectedIndex = currentInversion;
        }
    }

    private void UpdateVoicingComboBox()
    {
        int voicingIndex = _chordService.CurrentVoicing switch
        {
            ChordVoicing.Open => 1,
            ChordVoicing.Drop2 => 2,
            ChordVoicing.Drop3 => 3,
            _ => 0
        };

        if (VoicingComboBox.SelectedIndex != voicingIndex)
        {
            VoicingComboBox.SelectedIndex = voicingIndex;
        }
    }

    private void UpdateChordDisplay()
    {
        CurrentChordText.Text = _chordService.CurrentChordDisplay;

        // Format notes as note names
        var notes = _chordService.CurrentChordNotes;
        var noteNames = notes.Select(n => PianoRollNote.GetNoteName(n));
        CurrentNotesText.Text = string.Join(", ", noteNames);
    }

    #endregion
}
