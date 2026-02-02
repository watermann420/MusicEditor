// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Panel for editing audio effects parameters.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a registered effect in the editor.
/// </summary>
public class RegisteredEffect
{
    /// <summary>
    /// The effect instance.
    /// </summary>
    public object Effect { get; set; } = null!;

    /// <summary>
    /// Display name of the effect.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Type identifier (Compressor, Reverb, Delay, etc.).
    /// </summary>
    public string TypeName { get; set; } = "";

    /// <summary>
    /// Category of the effect.
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    /// Whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed { get; set; }

    /// <summary>
    /// Wet/Dry mix value (0-100).
    /// </summary>
    public double WetDryMix { get; set; } = 100;

    /// <summary>
    /// Display name for the effect chain ListBox.
    /// </summary>
    public string DisplayName => IsBypassed ? $"{Name} ({TypeName}) [BYPASSED]" : $"{Name} ({TypeName})";
}

/// <summary>
/// Panel that displays audio effect controls based on the selected effect type.
/// </summary>
public partial class EffectsEditorPanel : UserControl
{
    private string? _currentCategory;
    private string? _currentEffectType;
    private object? _currentEffect;
    private bool _isUpdatingSelection;

    /// <summary>
    /// Effect types organized by category.
    /// </summary>
    private static readonly Dictionary<string, string[]> EffectsByCategory = new()
    {
        ["Dynamics"] = new[] { "Compressor", "Limiter", "Gate", "Expander", "Transient Shaper", "Multiband Compressor" },
        ["Time-Based"] = new[] { "Reverb", "Delay", "Echo", "Convolution Reverb", "Ping Pong Delay", "Tape Delay" },
        ["Modulation"] = new[] { "Chorus", "Flanger", "Phaser", "Tremolo", "Vibrato", "Ring Modulator" },
        ["Distortion"] = new[] { "Overdrive", "Distortion", "Fuzz", "Bitcrusher", "Saturation", "Waveshaper" },
        ["Filters"] = new[] { "Low Pass", "High Pass", "Band Pass", "Notch", "Parametric EQ", "Graphic EQ" },
        ["Special"] = new[] { "Vocoder", "Pitch Shifter", "Auto-Tune", "Harmonizer", "Stereo Widener", "Lo-Fi" }
    };

    /// <summary>
    /// Collection of registered effects available for editing.
    /// </summary>
    public ObservableCollection<RegisteredEffect> RegisteredEffects { get; } = new();

    /// <summary>
    /// Collection of effects in the current effect chain.
    /// </summary>
    public ObservableCollection<RegisteredEffect> EffectChain { get; } = new();

    /// <summary>
    /// Event raised when the close button is clicked.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when effect parameters change.
    /// </summary>
    public event EventHandler<EffectParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Event raised when bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Creates a new EffectsEditorPanel.
    /// </summary>
    public EffectsEditorPanel()
    {
        InitializeComponent();
        EffectChainListBox.ItemsSource = EffectChain;

        // Initialize with first category
        UpdateEffectTypes("Dynamics");
    }

    /// <summary>
    /// Gets or sets the current effect being edited.
    /// </summary>
    public object? CurrentEffect
    {
        get => _currentEffect;
        set
        {
            _currentEffect = value;
            UpdateEffectDisplay();
        }
    }

    /// <summary>
    /// Gets the current effect type.
    /// </summary>
    public string? CurrentEffectType => _currentEffectType;

    /// <summary>
    /// Gets the current category.
    /// </summary>
    public string? CurrentCategory => _currentCategory;

    /// <summary>
    /// Gets or sets the wet/dry mix value (0-100).
    /// </summary>
    public double WetDryMix
    {
        get => WetDrySlider.Value;
        set
        {
            WetDrySlider.Value = Math.Clamp(value, 0, 100);
            WetDryValueText.Text = $"{WetDrySlider.Value:F0}%";
        }
    }

    /// <summary>
    /// Gets or sets whether the current effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => BypassToggle.IsChecked == true;
        set => BypassToggle.IsChecked = value;
    }

    /// <summary>
    /// Registers an effect so it appears in the effect chain.
    /// </summary>
    /// <param name="effect">The effect object</param>
    /// <param name="name">Display name of the effect</param>
    /// <param name="typeName">Type identifier (Compressor, Reverb, etc.)</param>
    /// <param name="category">Category of the effect</param>
    public void RegisterEffect(object effect, string name, string typeName, string category = "")
    {
        // Check if already registered
        foreach (var existing in RegisteredEffects)
        {
            if (ReferenceEquals(existing.Effect, effect))
            {
                // Update existing entry
                existing.Name = name;
                existing.TypeName = typeName;
                existing.Category = category;
                return;
            }
        }

        // Add new entry
        var registeredEffect = new RegisteredEffect
        {
            Effect = effect,
            Name = name,
            TypeName = typeName,
            Category = category
        };

        RegisteredEffects.Add(registeredEffect);
        EffectChain.Add(registeredEffect);
    }

    /// <summary>
    /// Clears all registered effects. Call this when scripts are reloaded.
    /// </summary>
    public void ClearEffects()
    {
        RegisteredEffects.Clear();
        EffectChain.Clear();
        CloseEffect();
    }

    /// <summary>
    /// Opens the effect editor for a specific effect instance.
    /// </summary>
    /// <param name="effect">The effect object to edit</param>
    /// <param name="effectName">Display name of the effect</param>
    /// <param name="effectType">Type identifier (Compressor, Reverb, etc.)</param>
    public void OpenEffect(object? effect, string effectName, string effectType)
    {
        _currentEffect = effect;
        _currentEffectType = effectType;

        EffectSubtitle.Text = $"Editing: {effectName}";
        ShowEffectEditor(effectType);

        // Set the Content of the EffectControlContainer
        var effectControl = GetEffectControl(effectType);
        if (effectControl != null)
        {
            EffectControlContainer.Content = effectControl;
            effectControl.DataContext = effect;
        }
    }

    /// <summary>
    /// Closes the effect editor and shows the empty state.
    /// </summary>
    public void CloseEffect()
    {
        _currentEffect = null;
        _currentEffectType = null;

        EffectSubtitle.Text = "Audio Effect Processing";

        HideEffectEditor();
        NoEffectPanel.Visibility = Visibility.Visible;
        EffectControlContainer.Content = null;
    }

    /// <summary>
    /// Gets the appropriate control for the given effect type.
    /// </summary>
    /// <param name="effectType">The effect type identifier</param>
    /// <returns>A FrameworkElement control for editing the effect, or null if not found</returns>
    public FrameworkElement? GetEffectControl(string effectType)
    {
        // Return a placeholder control for now - specific effect controls would be implemented separately
        var placeholder = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#181818")!),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(12)
        };

        var stack = new StackPanel();

        var titleText = new TextBlock
        {
            Text = effectType,
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E0E0E0")!),
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(titleText);

        var infoText = new TextBlock
        {
            Text = $"Effect control panel for {effectType}.\nImplement specific controls based on effect parameters.",
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#808080")!),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(infoText);

        placeholder.Child = stack;
        return placeholder;
    }

    private void UpdateEffectTypes(string category)
    {
        if (EffectTypeComboBox == null) return;

        _isUpdatingSelection = true;
        try
        {
            EffectTypeComboBox.Items.Clear();

            if (EffectsByCategory.TryGetValue(category, out var effects))
            {
                foreach (var effect in effects)
                {
                    EffectTypeComboBox.Items.Add(new ComboBoxItem { Content = effect });
                }

                if (EffectTypeComboBox.Items.Count > 0)
                {
                    EffectTypeComboBox.SelectedIndex = 0;
                }
            }

            _currentCategory = category;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void ShowEffectEditor(string effectType)
    {
        NoEffectPanel.Visibility = Visibility.Collapsed;
        EffectControlContainer.Visibility = Visibility.Visible;
    }

    private void HideEffectEditor()
    {
        EffectControlContainer.Visibility = Visibility.Collapsed;
    }

    private void UpdateEffectDisplay()
    {
        if (_currentEffect == null)
        {
            CloseEffect();
        }
    }

    private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection || !IsLoaded) return;

        if (CategoryComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var category = selectedItem.Content?.ToString() ?? "Dynamics";
            UpdateEffectTypes(category);
        }
    }

    private void EffectTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        if (EffectTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var effectType = selectedItem.Content?.ToString();
            if (!string.IsNullOrEmpty(effectType))
            {
                _currentEffectType = effectType;
                OpenEffect(null, effectType, effectType);
            }
        }
    }

    private void WetDrySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WetDryValueText != null)
        {
            WetDryValueText.Text = $"{e.NewValue:F0}%";
        }

        ParameterChanged?.Invoke(this, new EffectParameterChangedEventArgs("WetDryMix", e.NewValue));
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        var isBypassed = BypassToggle.IsChecked == true;
        BypassChanged?.Invoke(this, isBypassed);

        // Update subtitle to reflect bypass state
        if (isBypassed)
        {
            EffectSubtitle.Text = "BYPASSED - Audio Effect Processing";
        }
        else if (_currentEffectType != null)
        {
            EffectSubtitle.Text = $"Editing: {_currentEffectType}";
        }
        else
        {
            EffectSubtitle.Text = "Audio Effect Processing";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Event arguments for effect parameter changes.
/// </summary>
public class EffectParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Name of the parameter that changed.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// New value of the parameter.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public EffectParameterChangedEventArgs(string parameterName, object? newValue)
    {
        ParameterName = parameterName;
        NewValue = newValue;
    }
}
