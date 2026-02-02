// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Scripting;
using NAudio.Midi;

namespace MusicEngineEditor.Services;

public class EngineService : IDisposable
{
    private AudioEngine? _engine;
    private Sequencer? _sequencer;
    private ScriptHost? _scriptHost;
    private bool _disposed;

    public double Bpm => _sequencer?.Bpm ?? 120;
    public double CurrentBeat => _sequencer?.CurrentBeat ?? 0;
    public int PatternCount { get; private set; }
    public bool IsInitialized { get; private set; }

    // Surface engine events for UI (logs, activity, parameter changes)
    public event Action<int>? MidiActivity;
    public event Action<int, bool>? MidiNoteActivity;
    public event Action<string, float>? ParameterChanged;
    public event Action<string>? MidiLog;

    /// <summary>
    /// Event fired when a synth is created via script.
    /// Parameters: synth instance (ISynth), synth name, synth type name.
    /// </summary>
    public event Action<object, string, string>? SynthCreated;

    /// <summary>Gets the sequencer for visualization integration.</summary>
    public Sequencer? Sequencer => _sequencer;

    public string? InitializationOutput { get; private set; }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        // Reuse the singleton AudioEngineService so MIDI devices are only opened once
        var aes = AudioEngineService.Instance;

        if (!aes.IsInitialized)
        {
            // Keep the original console capture behaviour for device listing
            var outputCapture = new StringWriter();
            var originalOut = Console.Out;
            try
            {
                Console.SetOut(outputCapture);
                await aes.InitializeAsync();
                InitializationOutput = outputCapture.ToString();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        _engine = aes.AudioEngine;
        _sequencer = aes.Sequencer;

        if (_engine == null || _sequencer == null)
        {
            throw new InvalidOperationException("AudioEngineService failed to provide engine or sequencer.");
        }

        // Forward engine events so UI can bind to this service uniformly
        _engine.MidiActivity += idx => MidiActivity?.Invoke(idx);
        _engine.MidiNoteActivity += (idx, on) => MidiNoteActivity?.Invoke(idx, on);
        _engine.ParameterChanged += (name, val) => ParameterChanged?.Invoke(name, val);
        _engine.MidiLog += msg => MidiLog?.Invoke(msg);

        // Script host uses the shared engine/sequencer
        _scriptHost = new ScriptHost(_engine, _sequencer);

        // Forward synth created events
        _scriptHost.OnSynthCreated += (synth, name, typeName) => SynthCreated?.Invoke(synth, name, typeName);

        IsInitialized = true;
    }

    public async Task<ScriptResult> ExecuteScriptAsync(string code)
    {
        if (!IsInitialized || _scriptHost == null || _engine == null || _sequencer == null)
        {
            return new ScriptResult
            {
                Success = false,
                ErrorMessage = "Engine not initialized"
            };
        }

        var result = new ScriptResult();
        var outputCapture = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            // Clear previous state (like /S command)
            _scriptHost.ClearState();

            // Capture console output
            Console.SetOut(outputCapture);

            await _scriptHost.ExecuteScriptAsync(code);

            result.Success = true;
            result.Output = outputCapture.ToString();
        }
        catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException compEx)
        {
            result.Success = false;
            result.ErrorMessage = "Compilation errors";
            result.Errors = new List<ScriptError>();

            foreach (var diagnostic in compEx.Diagnostics)
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                result.Errors.Add(new ScriptError
                {
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    Message = diagnostic.GetMessage(),
                    Severity = diagnostic.Severity.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Output = outputCapture.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return result;
    }

    public void AllNotesOff()
    {
        if (_scriptHost != null)
        {
            _scriptHost.ClearState();
        }

        // We deliberately do not dispose the shared AudioEngineService here
    }

    public void SetBpm(double bpm)
    {
        if (_sequencer != null)
        {
            _sequencer.Bpm = bpm;
        }
    }

    public int GetMidiInputCount()
    {
        return MidiIn.NumberOfDevices;
    }

    public int GetMidiOutputCount()
    {
        return MidiOut.NumberOfDevices;
    }

    public string GetMidiInputName(int index)
    {
        try
        {
            return MidiIn.DeviceInfo(index).ProductName;
        }
        catch
        {
            return $"MIDI Input {index}";
        }
    }

    public string GetMidiOutputName(int index)
    {
        try
        {
            return MidiOut.DeviceInfo(index).ProductName;
        }
        catch
        {
            return $"MIDI Output {index}";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _sequencer?.Stop();
        _engine?.Dispose();

        _disposed = true;
    }
}

public class ScriptResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ScriptError> Errors { get; set; } = new();
}

public class ScriptError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
}
