// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System;
using System.Windows.Controls;
using System.Windows.Threading;
using MusicEngineEditor.Models;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Code-behind for the MixerView user control.
/// Manages the mixer interface with channel strips, volume faders, and level meters.
/// </summary>
public partial class MixerView : UserControl
{
    private readonly MixerViewModel _viewModel;
    private readonly DispatcherTimer? _meterTimer;
    private readonly Random _random = new();

    // Simulated LUFS values for demo
    private double _simulatedIntegratedLufs = -14.0;
    private double _simulatedShortTermLufs = -14.0;
    private double _simulatedMomentaryLufs = -14.0;
    private double _simulatedTruePeak = -1.0;

    /// <summary>
    /// Creates a new MixerView and initializes the MixerViewModel.
    /// </summary>
    public MixerView()
    {
        InitializeComponent();

        // Initialize ViewModel and set as DataContext
        _viewModel = new MixerViewModel();
        DataContext = _viewModel;

        // Optional: Start a DispatcherTimer to simulate meter levels for demo
        _meterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // ~20 FPS for smooth meter animation
        };
        _meterTimer.Tick += OnMeterTimerTick;
        _meterTimer.Start();

        // Clean up timer when unloaded
        Unloaded += (s, e) => StopMeterSimulation();
    }

    /// <summary>
    /// Timer tick handler that simulates meter levels for demonstration purposes.
    /// </summary>
    private void OnMeterTimerTick(object? sender, EventArgs e)
    {
        // Simulate meter levels for each channel
        for (int i = 0; i < _viewModel.Channels.Count; i++)
        {
            var channel = _viewModel.Channels[i];

            // Skip muted channels
            if (channel.IsEffectivelyMuted)
            {
                channel.UpdateMeters(0f, 0f);
                continue;
            }

            // Generate simulated audio levels based on volume
            float baseLevel = channel.Volume * 0.7f;
            float variation = (float)(_random.NextDouble() * 0.3);

            // Apply pan to left/right distribution
            float panLeft = Math.Max(0, -channel.Pan + 1) / 2;
            float panRight = Math.Max(0, channel.Pan + 1) / 2;

            float left = Math.Min(1.1f, (baseLevel + variation) * panLeft);
            float right = Math.Min(1.1f, (baseLevel + variation) * panRight);

            channel.UpdateMeters(left, right);
        }

        // Simulate master meter levels (sum of all channels, simplified)
        float masterLeft = 0f;
        float masterRight = 0f;

        foreach (var channel in _viewModel.Channels)
        {
            if (!channel.IsEffectivelyMuted)
            {
                masterLeft += channel.MeterLeft * 0.15f;
                masterRight += channel.MeterRight * 0.15f;
            }
        }

        // Apply master volume and clamp
        masterLeft = Math.Min(1.2f, masterLeft * _viewModel.MasterChannel.Volume);
        masterRight = Math.Min(1.2f, masterRight * _viewModel.MasterChannel.Volume);

        _viewModel.UpdateMasterMeters(masterLeft, masterRight);

        // Update loudness meter display with simulated values
        UpdateLoudnessMeterSimulation(masterLeft, masterRight);
    }

    /// <summary>
    /// Updates the loudness meter display with simulated LUFS values based on master levels.
    /// In a real implementation, this would be connected to MusicEngine.Core.LoudnessMeter.
    /// </summary>
    private void UpdateLoudnessMeterSimulation(float masterLeft, float masterRight)
    {
        // Calculate simulated momentary loudness from master levels
        // Convert linear level to approximate LUFS (simplified simulation)
        float combinedLevel = (masterLeft + masterRight) / 2f;
        double targetMomentary = combinedLevel > 0.001f
            ? 20.0 * Math.Log10(combinedLevel) - 14.0 // Offset to center around -14 LUFS
            : double.NegativeInfinity;

        // Clamp to reasonable LUFS range
        if (!double.IsNegativeInfinity(targetMomentary))
        {
            targetMomentary = Math.Clamp(targetMomentary, -60.0, 0.0);
        }

        // Smooth momentary loudness (400ms window simulation)
        if (double.IsNegativeInfinity(targetMomentary))
        {
            _simulatedMomentaryLufs = Math.Max(_simulatedMomentaryLufs - 2.0, -60.0);
        }
        else
        {
            _simulatedMomentaryLufs = _simulatedMomentaryLufs * 0.7 + targetMomentary * 0.3;
        }

        // Short-term loudness (3 second window - slower response)
        _simulatedShortTermLufs = _simulatedShortTermLufs * 0.95 + _simulatedMomentaryLufs * 0.05;

        // Integrated loudness (running average - very slow response)
        if (_simulatedMomentaryLufs > -60.0)
        {
            _simulatedIntegratedLufs = _simulatedIntegratedLufs * 0.99 + _simulatedMomentaryLufs * 0.01;
        }

        // True peak (track maximum with slow decay)
        double instantPeak = combinedLevel > 0.001f
            ? 20.0 * Math.Log10(Math.Max(masterLeft, masterRight))
            : -60.0;
        if (instantPeak > _simulatedTruePeak)
        {
            _simulatedTruePeak = instantPeak;
        }
        else
        {
            _simulatedTruePeak = Math.Max(_simulatedTruePeak - 0.1, instantPeak);
        }

        // Update the display control
        LoudnessMeterDisplay.IntegratedLoudness = _simulatedIntegratedLufs;
        LoudnessMeterDisplay.ShortTermLoudness = _simulatedShortTermLufs;
        LoudnessMeterDisplay.MomentaryLoudness = _simulatedMomentaryLufs;
        LoudnessMeterDisplay.TruePeak = _simulatedTruePeak;
    }

    /// <summary>
    /// Starts the meter level simulation.
    /// </summary>
    public void StartMeterSimulation()
    {
        _meterTimer?.Start();
    }

    /// <summary>
    /// Stops the meter level simulation.
    /// </summary>
    public void StopMeterSimulation()
    {
        _meterTimer?.Stop();
    }

    /// <summary>
    /// Gets the MixerViewModel associated with this view.
    /// </summary>
    public MixerViewModel ViewModel => _viewModel;

    /// <summary>
    /// Handles layout selector selection changes.
    /// </summary>
    private void LayoutSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayoutSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var layout = tag switch
            {
                "LargeFaders" => MixerLayoutType.LargeFaders,
                "SmallFaders" => MixerLayoutType.SmallFaders,
                "MetersOnly" => MixerLayoutType.MetersOnly,
                _ => MixerLayoutType.LargeFaders
            };

            _viewModel.CurrentLayout = layout;
        }
    }

    /// <summary>
    /// Syncs the layout selector with the viewmodel state.
    /// </summary>
    private void SyncLayoutSelector()
    {
        int index = _viewModel.CurrentLayout switch
        {
            MixerLayoutType.LargeFaders => 0,
            MixerLayoutType.SmallFaders => 1,
            MixerLayoutType.MetersOnly => 2,
            _ => 0
        };

        if (LayoutSelector.SelectedIndex != index)
        {
            LayoutSelector.SelectedIndex = index;
        }
    }
}
