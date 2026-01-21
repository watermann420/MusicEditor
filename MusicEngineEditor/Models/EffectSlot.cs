//MusicEngineEditor - Effect Slot Model
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a single effect slot in an effect chain.
/// Contains effect type, parameters, and bypass state.
/// </summary>
public partial class EffectSlot : ObservableObject
{
    private readonly int _index;

    /// <summary>
    /// Gets the slot index in the chain.
    /// </summary>
    public int Index => _index;

    /// <summary>
    /// Gets or sets the effect type identifier.
    /// </summary>
    [ObservableProperty]
    private string _effectType = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the effect.
    /// </summary>
    [ObservableProperty]
    private string _displayName = "Empty";

    /// <summary>
    /// Gets or sets whether the effect is bypassed.
    /// </summary>
    [ObservableProperty]
    private bool _isBypassed;

    /// <summary>
    /// Gets or sets whether this slot is expanded in the UI.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Gets or sets whether this slot is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets or sets whether this slot is empty (no effect loaded).
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>
    /// Gets or sets the dry/wet mix (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _mix = 1.0f;

    /// <summary>
    /// Gets or sets the effect color for visual representation.
    /// </summary>
    [ObservableProperty]
    private string _effectColor = "#6B7280";

    /// <summary>
    /// Gets or sets the effect category (e.g., "Dynamics", "Time-Based", "Modulation").
    /// </summary>
    [ObservableProperty]
    private string _category = string.Empty;

    /// <summary>
    /// Gets the collection of effect parameters.
    /// </summary>
    public ObservableCollectionEx<EffectParameter> Parameters { get; } = [];

    /// <summary>
    /// Creates a new effect slot.
    /// </summary>
    /// <param name="index">The slot index.</param>
    public EffectSlot(int index)
    {
        _index = index;
    }

    /// <summary>
    /// Creates a new effect slot with an effect loaded.
    /// </summary>
    /// <param name="index">The slot index.</param>
    /// <param name="effectType">The effect type identifier.</param>
    /// <param name="displayName">The display name.</param>
    public EffectSlot(int index, string effectType, string displayName) : this(index)
    {
        _effectType = effectType;
        _displayName = displayName;
        _isEmpty = false;
        SetEffectColorByCategory(effectType);
    }

    /// <summary>
    /// Loads an effect into this slot.
    /// </summary>
    /// <param name="effectType">The effect type identifier.</param>
    /// <param name="displayName">The display name.</param>
    public void LoadEffect(string effectType, string displayName)
    {
        EffectType = effectType;
        DisplayName = displayName;
        IsEmpty = false;
        IsBypassed = false;
        Mix = 1.0f;
        Parameters.Clear();
        SetEffectColorByCategory(effectType);
    }

    /// <summary>
    /// Clears the effect from this slot.
    /// </summary>
    public void ClearEffect()
    {
        EffectType = string.Empty;
        DisplayName = "Empty";
        IsEmpty = true;
        IsBypassed = false;
        Mix = 1.0f;
        Parameters.Clear();
        EffectColor = "#6B7280";
        Category = string.Empty;
    }

    /// <summary>
    /// Adds a parameter to this effect.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The initial value.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    public void AddParameter(string name, float value, float min = 0f, float max = 1f)
    {
        Parameters.Add(new EffectParameter(name, value, min, max));
    }

    private void SetEffectColorByCategory(string effectType)
    {
        var typeLower = effectType.ToLowerInvariant();

        if (typeLower.Contains("reverb") || typeLower.Contains("delay") || typeLower.Contains("echo"))
        {
            EffectColor = "#3B82F6"; // Blue for time-based
            Category = "Time-Based";
        }
        else if (typeLower.Contains("compressor") || typeLower.Contains("limiter") || typeLower.Contains("gate"))
        {
            EffectColor = "#EF4444"; // Red for dynamics
            Category = "Dynamics";
        }
        else if (typeLower.Contains("eq") || typeLower.Contains("filter"))
        {
            EffectColor = "#10B981"; // Green for EQ/Filter
            Category = "EQ/Filter";
        }
        else if (typeLower.Contains("chorus") || typeLower.Contains("flanger") || typeLower.Contains("phaser") ||
                 typeLower.Contains("tremolo") || typeLower.Contains("vibrato"))
        {
            EffectColor = "#8B5CF6"; // Purple for modulation
            Category = "Modulation";
        }
        else if (typeLower.Contains("distortion") || typeLower.Contains("overdrive") || typeLower.Contains("saturation"))
        {
            EffectColor = "#F59E0B"; // Orange for distortion
            Category = "Distortion";
        }
        else
        {
            EffectColor = "#6B7280"; // Gray for other
            Category = "Other";
        }
    }
}

/// <summary>
/// Represents a parameter of an effect.
/// </summary>
public partial class EffectParameter : ObservableObject
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// Gets or sets the parameter value.
    /// </summary>
    [ObservableProperty]
    private float _value;

    /// <summary>
    /// Gets the minimum value.
    /// </summary>
    [ObservableProperty]
    private float _minimum;

    /// <summary>
    /// Gets the maximum value.
    /// </summary>
    [ObservableProperty]
    private float _maximum;

    /// <summary>
    /// Gets or sets the display format string.
    /// </summary>
    [ObservableProperty]
    private string _displayFormat = "{0:F2}";

    /// <summary>
    /// Gets or sets the unit suffix (e.g., "dB", "ms", "Hz").
    /// </summary>
    [ObservableProperty]
    private string _unit = string.Empty;

    /// <summary>
    /// Creates a new effect parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The initial value.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    public EffectParameter(string name, float value, float min = 0f, float max = 1f)
    {
        _name = name;
        _value = value;
        _minimum = min;
        _maximum = max;
    }

    /// <summary>
    /// Gets the normalized value (0.0 to 1.0).
    /// </summary>
    public float NormalizedValue
    {
        get => (Maximum - Minimum) > 0 ? (Value - Minimum) / (Maximum - Minimum) : 0f;
        set => Value = Minimum + (value * (Maximum - Minimum));
    }

    /// <summary>
    /// Gets the formatted display value.
    /// </summary>
    public string DisplayValue => string.Format(DisplayFormat, Value) + (string.IsNullOrEmpty(Unit) ? "" : $" {Unit}");
}

/// <summary>
/// Observable collection with AddRange support.
/// </summary>
public class ObservableCollectionEx<T> : System.Collections.ObjectModel.ObservableCollection<T>
{
    /// <summary>
    /// Adds multiple items to the collection.
    /// </summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }
}
