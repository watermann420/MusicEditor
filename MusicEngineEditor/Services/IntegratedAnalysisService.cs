// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Singleton service for integrated audio analysis including tuner, chord, key, tempo, and loop detection.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicEngineEditor.Controls.Analysis;

namespace MusicEngineEditor.Services;

/// <summary>
/// Singleton service providing integrated audio analysis capabilities including:
/// - Guitar/instrument tuning with pitch detection
/// - Chord detection and recognition
/// - Key/scale detection
/// - Tempo/BPM detection
/// - Loop point finding
/// </summary>
public sealed class IntegratedAnalysisService : IDisposable
{
    #region Singleton

    private static readonly Lazy<IntegratedAnalysisService> _instance = new(
        () => new IntegratedAnalysisService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the IntegratedAnalysisService.
    /// </summary>
    public static IntegratedAnalysisService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private readonly Dispatcher _dispatcher;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isRunning;

    // Analysis state
    private AnalysisInputSource _inputSource = AnalysisInputSource.MasterOutput;
    private AnalysisQualityLevel _quality = AnalysisQualityLevel.Normal;
    private AnalysisType _activeAnalysisType = AnalysisType.Tuner;

    // Tuner state
    private double _referencePitch = 440.0;
    private string? _currentNote;
    private int _currentOctave;
    private double _currentFrequency;
    private double _currentCentsDeviation;

    // Chord detection state
    private string? _currentChordRoot;
    private string? _currentChordType;
    private string[]? _currentChordNotes;
    private double _chordConfidence;

    // Key detection state
    private string? _currentKeyRoot;
    private string? _currentKeyMode;
    private string? _relativeKey;
    private string[]? _scaleNotes;
    private double _keyConfidence;
    private float[]? _chromagram;

    // Tempo detection state
    private double _currentBpm;
    private double _tempoConfidence;
    private string _timeSignature = "4/4";
    private int _currentBeat;

    // Tap tempo state
    private readonly List<DateTime> _tapTimes = new();
    private const int MaxTapHistory = 8;
    private const double TapTimeoutSeconds = 2.0;
    private double _tapTempoBpm;
    private int _tapCount;

    // Loop detection state
    private int _minLoopBars = 1;
    private int _maxLoopBars = 16;
    private List<LoopPoint> _detectedLoops = new();

    // Refresh rate control
    private int _refreshRateMs = 33; // ~30 fps default
    private DateTime _lastTunerUpdate;
    private DateTime _lastChordUpdate;
    private DateTime _lastKeyUpdate;
    private DateTime _lastTempoUpdate;
    private DateTime _lastChromagramUpdate;

    // Analysis timer
    private Timer? _analysisTimer;

    // Note names for pitch detection
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the analysis service is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets or sets the input source for analysis.
    /// </summary>
    public AnalysisInputSource InputSource
    {
        get => _inputSource;
        set
        {
            lock (_lock)
            {
                if (_inputSource != value)
                {
                    _inputSource = value;
                    ConfigureInputRouting();
                    InputSourceChanged?.Invoke(this, value);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the analysis quality level.
    /// </summary>
    public AnalysisQualityLevel Quality
    {
        get => _quality;
        set
        {
            lock (_lock)
            {
                if (_quality != value)
                {
                    _quality = value;
                    ConfigureQuality();
                    QualityChanged?.Invoke(this, value);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the reference pitch for tuning (A4 frequency).
    /// </summary>
    public double ReferencePitch
    {
        get => _referencePitch;
        set
        {
            lock (_lock)
            {
                _referencePitch = Math.Clamp(value, 400, 480);
            }
        }
    }

    /// <summary>
    /// Gets the current detected note name.
    /// </summary>
    public string? CurrentNote => _currentNote;

    /// <summary>
    /// Gets the current detected octave.
    /// </summary>
    public int CurrentOctave => _currentOctave;

    /// <summary>
    /// Gets the current detected frequency in Hz.
    /// </summary>
    public double CurrentFrequency => _currentFrequency;

    /// <summary>
    /// Gets the current cents deviation from the target pitch.
    /// </summary>
    public double CurrentCentsDeviation => _currentCentsDeviation;

    /// <summary>
    /// Gets the current detected chord root.
    /// </summary>
    public string? CurrentChordRoot => _currentChordRoot;

    /// <summary>
    /// Gets the current detected chord type.
    /// </summary>
    public string? CurrentChordType => _currentChordType;

    /// <summary>
    /// Gets the current detected key root.
    /// </summary>
    public string? CurrentKeyRoot => _currentKeyRoot;

    /// <summary>
    /// Gets the current detected key mode (Major/Minor).
    /// </summary>
    public string? CurrentKeyMode => _currentKeyMode;

    /// <summary>
    /// Gets the current detected BPM.
    /// </summary>
    public double CurrentBpm => _currentBpm;

    /// <summary>
    /// Gets the tap tempo BPM.
    /// </summary>
    public double TapTempoBpm => _tapTempoBpm;

    /// <summary>
    /// Gets the tap count.
    /// </summary>
    public int TapCount => _tapCount;

    /// <summary>
    /// Gets or sets the minimum loop length in bars.
    /// </summary>
    public int MinLoopBars
    {
        get => _minLoopBars;
        set => _minLoopBars = Math.Clamp(value, 1, 64);
    }

    /// <summary>
    /// Gets or sets the maximum loop length in bars.
    /// </summary>
    public int MaxLoopBars
    {
        get => _maxLoopBars;
        set => _maxLoopBars = Math.Clamp(value, 1, 128);
    }

    /// <summary>
    /// Gets or sets the UI refresh rate in milliseconds.
    /// </summary>
    public int RefreshRateMs
    {
        get => _refreshRateMs;
        set => _refreshRateMs = Math.Clamp(value, 16, 200);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when tuner data is updated.
    /// </summary>
    public event EventHandler<TunerEventArgs>? TunerUpdated;

    /// <summary>
    /// Raised when a chord is detected.
    /// </summary>
    public event EventHandler<ChordEventArgs>? ChordDetected;

    /// <summary>
    /// Raised when a key is detected.
    /// </summary>
    public event EventHandler<MusicalKeyEventArgs>? KeyDetected;

    /// <summary>
    /// Raised when tempo is detected.
    /// </summary>
    public event EventHandler<TempoEventArgs>? TempoDetected;

    /// <summary>
    /// Raised when a beat is detected.
    /// </summary>
    public event EventHandler<BeatEventArgs>? BeatDetected;

    /// <summary>
    /// Raised when loop points are detected.
    /// </summary>
    public event EventHandler<LoopDetectionEventArgs>? LoopsDetected;

    /// <summary>
    /// Raised when chromagram data is updated.
    /// </summary>
    public event EventHandler<ChromagramEventArgs>? ChromagramUpdated;

    /// <summary>
    /// Raised when the input source changes.
    /// </summary>
    public event EventHandler<AnalysisInputSource>? InputSourceChanged;

    /// <summary>
    /// Raised when the quality level changes.
    /// </summary>
    public event EventHandler<AnalysisQualityLevel>? QualityChanged;

    /// <summary>
    /// Raised when analysis starts.
    /// </summary>
    public event EventHandler? Started;

    /// <summary>
    /// Raised when analysis stops.
    /// </summary>
    public event EventHandler? Stopped;

    #endregion

    #region Constructor

    private IntegratedAnalysisService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts the analysis service.
    /// </summary>
    public void StartAnalysis()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;

            // Start analysis timer (simulates audio processing for demo)
            _analysisTimer = new Timer(OnAnalysisTimerTick, null, 0, _refreshRateMs);

            _dispatcher.BeginInvoke(() => Started?.Invoke(this, EventArgs.Empty));
        }
    }

    /// <summary>
    /// Stops the analysis service.
    /// </summary>
    public void StopAnalysis()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;

            _analysisTimer?.Dispose();
            _analysisTimer = null;

            _dispatcher.BeginInvoke(() => Stopped?.Invoke(this, EventArgs.Empty));
        }
    }

    /// <summary>
    /// Sets the active analysis type for optimization.
    /// </summary>
    /// <param name="type">The analysis type that is currently visible.</param>
    public void SetActiveAnalysisType(AnalysisType type)
    {
        lock (_lock)
        {
            _activeAnalysisType = type;
        }
    }

    /// <summary>
    /// Processes audio samples for analysis.
    /// </summary>
    /// <param name="samples">Audio samples (interleaved if stereo).</param>
    /// <param name="count">Number of samples.</param>
    /// <param name="channels">Number of channels.</param>
    public void ProcessSamples(float[] samples, int count, int channels = 2)
    {
        if (!_isRunning) return;

        lock (_lock)
        {
            // Convert to mono if stereo
            float[] monoSamples;
            int monoCount;

            if (channels == 2)
            {
                monoCount = count / 2;
                monoSamples = new float[monoCount];
                for (int i = 0; i < monoCount; i++)
                {
                    monoSamples[i] = (samples[i * 2] + samples[i * 2 + 1]) * 0.5f;
                }
            }
            else
            {
                monoSamples = samples;
                monoCount = count;
            }

            // Process based on active analysis type
            switch (_activeAnalysisType)
            {
                case AnalysisType.Tuner:
                    ProcessTuner(monoSamples, monoCount);
                    break;
                case AnalysisType.Chord:
                    ProcessChordDetection(monoSamples, monoCount);
                    break;
                case AnalysisType.Key:
                    ProcessKeyDetection(monoSamples, monoCount);
                    break;
                case AnalysisType.Tempo:
                    ProcessTempoDetection(monoSamples, monoCount);
                    break;
                case AnalysisType.Loop:
                    // Loop detection is done on-demand, not continuously
                    break;
            }
        }
    }

    /// <summary>
    /// Records a tap for tap tempo calculation.
    /// </summary>
    /// <returns>Current calculated BPM from taps, or 0 if not enough taps.</returns>
    public double Tap()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            // Clear old taps if timeout exceeded
            if (_tapTimes.Count > 0)
            {
                var lastTap = _tapTimes[^1];
                if ((now - lastTap).TotalSeconds > TapTimeoutSeconds)
                {
                    _tapTimes.Clear();
                }
            }

            // Add new tap
            _tapTimes.Add(now);
            _tapCount = _tapTimes.Count;

            // Keep only recent taps
            while (_tapTimes.Count > MaxTapHistory)
            {
                _tapTimes.RemoveAt(0);
            }

            // Calculate BPM if we have at least 2 taps
            if (_tapTimes.Count >= 2)
            {
                var intervals = new List<double>();
                for (int i = 1; i < _tapTimes.Count; i++)
                {
                    intervals.Add((_tapTimes[i] - _tapTimes[i - 1]).TotalSeconds);
                }

                // Calculate average interval
                double avgInterval = 0;
                foreach (var interval in intervals)
                {
                    avgInterval += interval;
                }
                avgInterval /= intervals.Count;

                // Convert to BPM
                _tapTempoBpm = avgInterval > 0 ? 60.0 / avgInterval : 0;
                return _tapTempoBpm;
            }

            return 0;
        }
    }

    /// <summary>
    /// Resets tap tempo tracking.
    /// </summary>
    public void ResetTapTempo()
    {
        lock (_lock)
        {
            _tapTimes.Clear();
            _tapTempoBpm = 0;
            _tapCount = 0;
        }
    }

    /// <summary>
    /// Initiates loop point detection on the current audio.
    /// </summary>
    public void FindLoops()
    {
        Task.Run(() =>
        {
            // Simulate loop finding (would use actual audio analysis in production)
            Thread.Sleep(500); // Simulate processing time

            lock (_lock)
            {
                _detectedLoops.Clear();

                // Generate sample loop points (would be detected from audio in production)
                if (_currentBpm > 0)
                {
                    double beatDuration = 60.0 / _currentBpm;

                    _detectedLoops.Add(new LoopPoint
                    {
                        Name = "Loop 1 (4 bars)",
                        StartTime = 0,
                        EndTime = beatDuration * 16,
                        Bars = 4,
                        Score = 0.92
                    });

                    _detectedLoops.Add(new LoopPoint
                    {
                        Name = "Loop 2 (8 bars)",
                        StartTime = beatDuration * 16,
                        EndTime = beatDuration * 48,
                        Bars = 8,
                        Score = 0.85
                    });

                    _detectedLoops.Add(new LoopPoint
                    {
                        Name = "Loop 3 (2 bars)",
                        StartTime = beatDuration * 48,
                        EndTime = beatDuration * 56,
                        Bars = 2,
                        Score = 0.78
                    });
                }
            }

            _dispatcher.BeginInvoke(() =>
            {
                LoopsDetected?.Invoke(this, new LoopDetectionEventArgs
                {
                    IsComplete = true,
                    Loops = _detectedLoops.ToArray()
                });
            });
        });
    }

    /// <summary>
    /// Applies a detected loop point to the current project.
    /// </summary>
    /// <param name="startTime">Loop start time in seconds.</param>
    /// <param name="endTime">Loop end time in seconds.</param>
    public void ApplyLoopPoint(double startTime, double endTime)
    {
        // This would integrate with the project/transport service in production
        // For now, just log the action
        System.Diagnostics.Debug.WriteLine($"Applying loop: {startTime:F2}s - {endTime:F2}s");
    }

    /// <summary>
    /// Resets all analysis state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentNote = null;
            _currentOctave = -1;
            _currentFrequency = 0;
            _currentCentsDeviation = 0;

            _currentChordRoot = null;
            _currentChordType = null;
            _currentChordNotes = null;
            _chordConfidence = 0;

            _currentKeyRoot = null;
            _currentKeyMode = null;
            _relativeKey = null;
            _scaleNotes = null;
            _keyConfidence = 0;
            _chromagram = null;

            _currentBpm = 0;
            _tempoConfidence = 0;
            _currentBeat = 0;

            ResetTapTempo();
            _detectedLoops.Clear();
        }
    }

    #endregion

    #region Private Methods

    private void ConfigureInputRouting()
    {
        // Configure audio routing based on input source
        // This would connect to the appropriate audio stream in production
        switch (_inputSource)
        {
            case AnalysisInputSource.MasterOutput:
                // Connect to master bus output
                break;
            case AnalysisInputSource.SelectedTrack:
                // Connect to selected track output
                break;
            case AnalysisInputSource.ExternalInput:
                // Connect to external audio input
                break;
        }
    }

    private void ConfigureQuality()
    {
        // Configure analysis parameters based on quality level
        switch (_quality)
        {
            case AnalysisQualityLevel.Low:
                _refreshRateMs = 50; // 20 fps
                break;
            case AnalysisQualityLevel.Normal:
                _refreshRateMs = 33; // 30 fps
                break;
            case AnalysisQualityLevel.High:
                _refreshRateMs = 16; // 60 fps
                break;
        }
    }

    private void OnAnalysisTimerTick(object? state)
    {
        if (!_isRunning) return;

        // Simulate analysis updates for demonstration
        // In production, this would process actual audio data
        SimulateAnalysisUpdate();
    }

    private void SimulateAnalysisUpdate()
    {
        var now = DateTime.UtcNow;

        switch (_activeAnalysisType)
        {
            case AnalysisType.Tuner:
                if ((now - _lastTunerUpdate).TotalMilliseconds >= _refreshRateMs)
                {
                    _lastTunerUpdate = now;
                    SimulateTunerUpdate();
                }
                break;

            case AnalysisType.Chord:
                if ((now - _lastChordUpdate).TotalMilliseconds >= _refreshRateMs * 3)
                {
                    _lastChordUpdate = now;
                    SimulateChordUpdate();
                }
                break;

            case AnalysisType.Key:
                if ((now - _lastKeyUpdate).TotalMilliseconds >= _refreshRateMs * 5)
                {
                    _lastKeyUpdate = now;
                    SimulateKeyUpdate();
                }
                if ((now - _lastChromagramUpdate).TotalMilliseconds >= _refreshRateMs)
                {
                    _lastChromagramUpdate = now;
                    SimulateChromagramUpdate();
                }
                break;

            case AnalysisType.Tempo:
                if ((now - _lastTempoUpdate).TotalMilliseconds >= _refreshRateMs * 2)
                {
                    _lastTempoUpdate = now;
                    SimulateTempoUpdate();
                }
                break;
        }
    }

    private void SimulateTunerUpdate()
    {
        // Simulate pitch detection (would use actual FFT/autocorrelation in production)
        var random = new Random();

        // Simulate detecting a note around A4
        _currentFrequency = _referencePitch + (random.NextDouble() * 10 - 5);
        _currentCentsDeviation = (random.NextDouble() * 20 - 10);

        // Calculate note from frequency
        var semitones = 12 * Math.Log2(_currentFrequency / _referencePitch) + 9; // A4 = 9 semitones above C
        var noteIndex = ((int)Math.Round(semitones) % 12 + 12) % 12;
        _currentOctave = 4 + (int)Math.Floor((semitones + 3) / 12);
        _currentNote = NoteNames[noteIndex];

        _dispatcher.BeginInvoke(() =>
        {
            TunerUpdated?.Invoke(this, new TunerEventArgs
            {
                NoteName = _currentNote,
                Octave = _currentOctave,
                Frequency = _currentFrequency,
                CentsDeviation = _currentCentsDeviation
            });
        }, DispatcherPriority.Render);
    }

    private void SimulateChordUpdate()
    {
        // Simulate chord detection
        var chords = new[] { ("C", "maj"), ("Am", "min"), ("F", "maj"), ("G", "maj"), ("Dm", "min"), ("Em", "min") };
        var random = new Random();
        var chord = chords[random.Next(chords.Length)];

        _currentChordRoot = chord.Item1;
        _currentChordType = chord.Item2 == "maj" ? "Major" : "Minor";
        _chordConfidence = 0.7 + random.NextDouble() * 0.3;

        // Generate chord notes
        if (chord.Item2 == "maj")
        {
            _currentChordNotes = new[] { chord.Item1.Replace("m", ""), GetNoteAtInterval(chord.Item1, 4), GetNoteAtInterval(chord.Item1, 7) };
        }
        else
        {
            _currentChordNotes = new[] { chord.Item1.Replace("m", ""), GetNoteAtInterval(chord.Item1, 3), GetNoteAtInterval(chord.Item1, 7) };
        }

        _dispatcher.BeginInvoke(() =>
        {
            ChordDetected?.Invoke(this, new ChordEventArgs
            {
                RootNote = _currentChordRoot,
                ChordType = _currentChordType,
                Notes = _currentChordNotes,
                Confidence = _chordConfidence
            });
        }, DispatcherPriority.Render);
    }

    private void SimulateKeyUpdate()
    {
        // Simulate key detection
        var keys = new[] { ("C", "Major", "A Minor"), ("G", "Major", "E Minor"), ("D", "Major", "B Minor"), ("A", "Minor", "C Major") };
        var random = new Random();
        var key = keys[random.Next(keys.Length)];

        _currentKeyRoot = key.Item1;
        _currentKeyMode = key.Item2;
        _relativeKey = key.Item3;
        _keyConfidence = 0.75 + random.NextDouble() * 0.25;

        // Generate scale notes
        if (key.Item2 == "Major")
        {
            _scaleNotes = GetMajorScale(key.Item1);
        }
        else
        {
            _scaleNotes = GetMinorScale(key.Item1);
        }

        _dispatcher.BeginInvoke(() =>
        {
            KeyDetected?.Invoke(this, new MusicalKeyEventArgs
            {
                RootNote = _currentKeyRoot,
                Mode = _currentKeyMode,
                RelativeKey = _relativeKey,
                ScaleNotes = _scaleNotes,
                Confidence = _keyConfidence
            });
        }, DispatcherPriority.Render);
    }

    private void SimulateChromagramUpdate()
    {
        // Simulate chromagram values
        var random = new Random();
        _chromagram = new float[12];

        for (int i = 0; i < 12; i++)
        {
            _chromagram[i] = (float)(random.NextDouble() * 0.8);
        }

        // Boost the notes in the current scale
        if (_scaleNotes != null)
        {
            foreach (var note in _scaleNotes)
            {
                int index = Array.IndexOf(NoteNames, note.Replace("#", "#"));
                if (index >= 0)
                {
                    _chromagram[index] = Math.Min(1.0f, _chromagram[index] + 0.4f);
                }
            }
        }

        _dispatcher.BeginInvoke(() =>
        {
            ChromagramUpdated?.Invoke(this, new ChromagramEventArgs
            {
                Values = _chromagram
            });
        }, DispatcherPriority.Render);
    }

    private void SimulateTempoUpdate()
    {
        // Simulate tempo detection
        var random = new Random();

        if (_currentBpm == 0)
        {
            _currentBpm = 100 + random.NextDouble() * 40; // 100-140 BPM
        }
        else
        {
            // Small fluctuation
            _currentBpm += (random.NextDouble() * 2 - 1) * 0.5;
            _currentBpm = Math.Clamp(_currentBpm, 60, 200);
        }

        _tempoConfidence = 0.8 + random.NextDouble() * 0.2;

        // Simulate beat
        _currentBeat = (_currentBeat % 4) + 1;

        _dispatcher.BeginInvoke(() =>
        {
            TempoDetected?.Invoke(this, new TempoEventArgs
            {
                Bpm = _currentBpm,
                Confidence = _tempoConfidence,
                TimeSignature = _timeSignature
            });

            BeatDetected?.Invoke(this, new BeatEventArgs
            {
                BeatNumber = _currentBeat,
                Timestamp = DateTime.UtcNow.Ticks / 10000000.0
            });
        }, DispatcherPriority.Render);
    }

    private void ProcessTuner(float[] samples, int count)
    {
        // Actual pitch detection would go here
        // Using autocorrelation or FFT-based methods
    }

    private void ProcessChordDetection(float[] samples, int count)
    {
        // Actual chord detection would go here
        // Using chromagram analysis and chord template matching
    }

    private void ProcessKeyDetection(float[] samples, int count)
    {
        // Actual key detection would go here
        // Using chromagram analysis and key profile matching
    }

    private void ProcessTempoDetection(float[] samples, int count)
    {
        // Actual tempo detection would go here
        // Using onset detection and autocorrelation
    }

    private static string GetNoteAtInterval(string root, int semitones)
    {
        var rootIndex = Array.IndexOf(NoteNames, root.Replace("m", "").Replace("#", "#"));
        if (rootIndex < 0) rootIndex = 0;
        var targetIndex = (rootIndex + semitones) % 12;
        return NoteNames[targetIndex];
    }

    private static string[] GetMajorScale(string root)
    {
        var rootIndex = Array.IndexOf(NoteNames, root);
        if (rootIndex < 0) rootIndex = 0;

        // Major scale intervals: W-W-H-W-W-W-H (2-2-1-2-2-2-1)
        var intervals = new[] { 0, 2, 4, 5, 7, 9, 11 };
        var scale = new string[7];

        for (int i = 0; i < 7; i++)
        {
            scale[i] = NoteNames[(rootIndex + intervals[i]) % 12];
        }

        return scale;
    }

    private static string[] GetMinorScale(string root)
    {
        var rootIndex = Array.IndexOf(NoteNames, root);
        if (rootIndex < 0) rootIndex = 0;

        // Natural minor scale intervals: W-H-W-W-H-W-W (2-1-2-2-1-2-2)
        var intervals = new[] { 0, 2, 3, 5, 7, 8, 10 };
        var scale = new string[7];

        for (int i = 0; i < 7; i++)
        {
            scale[i] = NoteNames[(rootIndex + intervals[i]) % 12];
        }

        return scale;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            StopAnalysis();
        }
    }

    #endregion
}
