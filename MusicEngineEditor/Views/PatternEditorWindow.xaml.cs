// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Floating pattern editor window with piano roll control.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngineEditor.Controls.PatternEditor;

namespace MusicEngineEditor.Views;

/// <summary>
/// Floating window for editing patterns with a piano roll interface.
/// Features custom title bar with drag support and transport controls.
/// </summary>
public partial class PatternEditorWindow : Window
{
    #region Private Fields

    private readonly List<object> _patterns = new();
    private object? _currentPattern;
    private bool _isPlaying;
    private bool _keepRunning = true;

    #endregion

    #region Events

    /// <summary>
    /// Raised when playback is requested.
    /// </summary>
    public event EventHandler? PlayRequested;

    /// <summary>
    /// Raised when stop is requested.
    /// </summary>
    public event EventHandler? StopRequested;

    /// <summary>
    /// Raised when the selected pattern changes.
    /// </summary>
    public event EventHandler<object?>? PatternChanged;

    /// <summary>
    /// Raised when loop toggle state changes.
    /// </summary>
#pragma warning disable CS0067
    public event EventHandler<bool>? LoopToggled;
#pragma warning restore CS0067

    #endregion

    #region Properties

    /// <summary>
    /// Gets the currently selected pattern.
    /// </summary>
    public object? CurrentPattern => _currentPattern;

    /// <summary>
    /// Gets whether playback is active.
    /// </summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// Gets whether loop is enabled.
    /// </summary>
    public bool IsLoopEnabled => LoopToggle.IsChecked ?? false;

    #endregion

    #region Constructor

    public PatternEditorWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    #endregion

    #region Window Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set owner to MainWindow if available
        if (Owner == null && Application.Current.MainWindow != this)
        {
            Owner = Application.Current.MainWindow;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_keepRunning)
        {
            // Hide instead of close to allow re-showing
            e.Cancel = true;
            Hide();
        }
    }

    #endregion

    #region Title Bar Event Handlers

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to toggle maximize
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    #endregion

    #region Toolbar Event Handlers

    private void PatternSelectorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PatternSelectorCombo.SelectedIndex >= 0 && PatternSelectorCombo.SelectedIndex < _patterns.Count)
        {
            _currentPattern = _patterns[PatternSelectorCombo.SelectedIndex];
        }
        else
        {
            _currentPattern = null;
        }

        UpdatePatternDisplay();
        PatternChanged?.Invoke(this, _currentPattern);
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        _isPlaying = true;
        UpdateTransportState();
        PlayRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _isPlaying = false;
        UpdateTransportState();
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Registers patterns for editing.
    /// </summary>
    /// <param name="patterns">The patterns to register.</param>
    public void RegisterPatterns(IEnumerable<object> patterns)
    {
        _patterns.Clear();
        PatternSelectorCombo.Items.Clear();

        int index = 1;
        foreach (var pattern in patterns)
        {
            _patterns.Add(pattern);

            string name = GetPatternName(pattern) ?? $"Pattern {index}";
            PatternSelectorCombo.Items.Add(new ComboBoxItem { Content = name, Tag = pattern });
            index++;
        }

        if (PatternSelectorCombo.Items.Count > 0)
        {
            PatternSelectorCombo.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Registers a single pattern for editing.
    /// </summary>
    /// <param name="pattern">The pattern to register.</param>
    /// <param name="name">Optional display name for the pattern.</param>
    public void RegisterPattern(object pattern, string? name = null)
    {
        _patterns.Add(pattern);

        string displayName = name ?? GetPatternName(pattern) ?? $"Pattern {_patterns.Count}";
        PatternSelectorCombo.Items.Add(new ComboBoxItem { Content = displayName, Tag = pattern });

        if (PatternSelectorCombo.SelectedIndex < 0)
        {
            PatternSelectorCombo.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Clears all registered patterns.
    /// </summary>
    public void ClearPatterns()
    {
        _patterns.Clear();
        PatternSelectorCombo.Items.Clear();
        _currentPattern = null;
        UpdatePatternDisplay();
    }

    /// <summary>
    /// Selects a pattern by index.
    /// </summary>
    /// <param name="index">The index of the pattern to select.</param>
    public void SelectPattern(int index)
    {
        if (index >= 0 && index < PatternSelectorCombo.Items.Count)
        {
            PatternSelectorCombo.SelectedIndex = index;
        }
    }

    /// <summary>
    /// Sets the playback state.
    /// </summary>
    /// <param name="isPlaying">Whether playback is active.</param>
    public void SetPlaybackState(bool isPlaying)
    {
        _isPlaying = isPlaying;
        UpdateTransportState();
    }

    /// <summary>
    /// Sets the loop state.
    /// </summary>
    /// <param name="isLooping">Whether loop is enabled.</param>
    public void SetLoopState(bool isLooping)
    {
        LoopToggle.IsChecked = isLooping;
    }

    /// <summary>
    /// Shows the window.
    /// </summary>
    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    /// <summary>
    /// Forces the window to close permanently.
    /// </summary>
    public void ForceClose()
    {
        _keepRunning = false;
        Close();
    }

    /// <summary>
    /// Updates the status text.
    /// </summary>
    /// <param name="status">The status message to display.</param>
    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    #endregion

    #region Private Methods

    private void UpdatePatternDisplay()
    {
        if (_currentPattern != null)
        {
            string name = GetPatternName(_currentPattern) ?? "Unknown";
            PatternNameText.Text = $"- {name}";
        }
        else
        {
            PatternNameText.Text = "";
        }
    }

    private void UpdateTransportState()
    {
        // Visual feedback could be added here
        // For example, changing button appearance when playing
    }

    private string? GetPatternName(object pattern)
    {
        // Try to get Name property via reflection
        var nameProperty = pattern.GetType().GetProperty("Name");
        if (nameProperty != null)
        {
            return nameProperty.GetValue(pattern)?.ToString();
        }
        return null;
    }

    #endregion
}
