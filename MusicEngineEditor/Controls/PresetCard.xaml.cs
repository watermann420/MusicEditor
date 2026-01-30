// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Preset display card control.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A card control for displaying a single preset in the Preset Browser.
/// </summary>
public partial class PresetCard : UserControl
{
    /// <summary>
    /// Identifies the Preset dependency property.
    /// </summary>
    public static readonly DependencyProperty PresetProperty =
        DependencyProperty.Register(
            nameof(Preset),
            typeof(PresetInfo),
            typeof(PresetCard),
            new PropertyMetadata(null));

    /// <summary>
    /// Identifies the IsSelected dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(PresetCard),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets the preset displayed by this card.
    /// </summary>
    public PresetInfo? Preset
    {
        get => (PresetInfo?)GetValue(PresetProperty);
        set => SetValue(PresetProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this card is selected.
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Event raised when the favorite status changes.
    /// </summary>
    public event EventHandler<bool>? FavoriteChanged;

    /// <summary>
    /// Event raised when the rating changes.
    /// </summary>
    public event EventHandler<int>? RatingChanged;

    public PresetCard()
    {
        InitializeComponent();
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Preset != null)
        {
            Preset.UpdateFavoriteStatus();
            FavoriteChanged?.Invoke(this, Preset.IsFavorite);
        }
    }

    private void RatingStar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string tagStr } && Preset != null)
        {
            if (int.TryParse(tagStr, out var rating))
            {
                // If clicking the same star that's already the rating, clear it
                if (Preset.Rating == rating)
                {
                    Preset.Rating = 0;
                }
                else
                {
                    Preset.Rating = rating;
                }

                Preset.UpdateRating();
                RatingChanged?.Invoke(this, Preset.Rating);
            }
        }
    }
}

/// <summary>
/// Converts a rating value to a boolean for star display.
/// </summary>
public class RatingConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int rating && parameter is string paramStr && int.TryParse(paramStr, out var starNumber))
        {
            return rating >= starNumber;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Not used for this converter
        return null;
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
