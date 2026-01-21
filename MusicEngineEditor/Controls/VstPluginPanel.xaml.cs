using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// An enhanced VST plugin panel with rich visual display
/// </summary>
public partial class VstPluginPanel : UserControl
{
    #region Events

    /// <summary>
    /// Occurs when a plugin is selected for opening its editor
    /// </summary>
    public event EventHandler<VstPluginEventArgs>? OnOpenPluginEditor;

    /// <summary>
    /// Occurs when a plugin is double-clicked
    /// </summary>
    public event EventHandler<VstPluginEventArgs>? OnPluginDoubleClick;

    /// <summary>
    /// Occurs when plugins have been scanned
    /// </summary>
    public event EventHandler<VstScanCompletedEventArgs>? OnScanCompleted;

    /// <summary>
    /// Occurs when the bypass state of a plugin is toggled
    /// </summary>
    public event EventHandler<VstBypassEventArgs>? OnBypassToggled;

    /// <summary>
    /// Occurs when a preset is selected from the quick selector
    /// </summary>
    public event EventHandler<VstPresetSelectedEventArgs>? OnPresetSelected;

    #endregion

    #region Private Fields

    private readonly ObservableCollection<VstPluginDisplayInfo> _allPlugins = new();
    private readonly ObservableCollection<VstPluginDisplayInfo> _filteredPlugins = new();
    private readonly HashSet<string> _loadedPlugins = new();

    #endregion

    #region Constructor

    public VstPluginPanel()
    {
        InitializeComponent();
        PluginList.ItemsSource = _filteredPlugins;
        UpdateEmptyState();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets all scanned plugins
    /// </summary>
    public IReadOnlyCollection<VstPluginDisplayInfo> Plugins => _allPlugins;

    /// <summary>
    /// Gets or sets the set of loaded plugin names (for status display)
    /// </summary>
    public HashSet<string> LoadedPlugins
    {
        get => _loadedPlugins;
        set
        {
            _loadedPlugins.Clear();
            foreach (var name in value)
            {
                _loadedPlugins.Add(name);
            }
            UpdatePluginStatuses();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Scans for VST plugins in common directories
    /// </summary>
    public async Task ScanPluginsAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        _allPlugins.Clear();
        _filteredPlugins.Clear();

        await Task.Run(() =>
        {
            var vstPaths = new[]
            {
                @"C:\Program Files\Common Files\VST3",
                @"C:\Program Files\VSTPlugins",
                @"C:\Program Files\Steinberg\VSTPlugins",
                @"C:\Program Files (x86)\Common Files\VST3",
                @"C:\Program Files (x86)\VSTPlugins",
                @"C:\Program Files (x86)\Steinberg\VSTPlugins",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Common Files\VST3",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Common Files\VST3"
            };

            var foundPlugins = new List<VstPluginDisplayInfo>();

            foreach (var path in vstPaths.Where(Directory.Exists).Distinct())
            {
                try
                {
                    // Scan for VST3 plugins
                    foreach (var file in Directory.GetFiles(path, "*.vst3", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (!foundPlugins.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.Type == "VST3"))
                        {
                            foundPlugins.Add(new VstPluginDisplayInfo
                            {
                                Name = name,
                                Type = "VST3",
                                FullPath = file,
                                IsLoaded = _loadedPlugins.Contains(name)
                            });
                        }
                    }

                    // Scan for VST2 plugins (DLLs in VST directories)
                    foreach (var file in Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        // Skip obvious non-VST DLLs
                        if (name.StartsWith("api-", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("msvcp", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("vcruntime", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("ucrtbase", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!foundPlugins.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            foundPlugins.Add(new VstPluginDisplayInfo
                            {
                                Name = name,
                                Type = "VST2",
                                FullPath = file,
                                IsLoaded = _loadedPlugins.Contains(name)
                            });
                        }
                    }
                }
                catch
                {
                    // Skip inaccessible directories
                }
            }

            // Sort by name
            foundPlugins.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            Dispatcher.Invoke(() =>
            {
                foreach (var plugin in foundPlugins)
                {
                    _allPlugins.Add(plugin);
                    _filteredPlugins.Add(plugin);
                }
            });
        });

        LoadingOverlay.Visibility = Visibility.Collapsed;
        UpdatePluginCount();
        UpdateEmptyState();

        OnScanCompleted?.Invoke(this, new VstScanCompletedEventArgs(_allPlugins.Count));
    }

    /// <summary>
    /// Marks a plugin as loaded
    /// </summary>
    public void MarkPluginLoaded(string pluginName)
    {
        _loadedPlugins.Add(pluginName);
        UpdatePluginStatuses();
    }

    /// <summary>
    /// Marks a plugin as unloaded
    /// </summary>
    public void MarkPluginUnloaded(string pluginName)
    {
        _loadedPlugins.Remove(pluginName);
        UpdatePluginStatuses();
    }

    /// <summary>
    /// Clears all loaded plugin statuses
    /// </summary>
    public void ClearLoadedPlugins()
    {
        _loadedPlugins.Clear();
        UpdatePluginStatuses();
    }

    /// <summary>
    /// Associates a plugin instance with a displayed plugin for bypass/preset sync
    /// </summary>
    /// <param name="pluginName">Name of the plugin to associate</param>
    /// <param name="pluginInstance">The IVstPlugin instance to associate</param>
    public void AssociatePluginInstance(string pluginName, IVstPlugin pluginInstance)
    {
        var plugin = _allPlugins.FirstOrDefault(p =>
            p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

        if (plugin != null)
        {
            plugin.PluginInstance = pluginInstance;
            plugin.IsLoaded = true;
        }
    }

    /// <summary>
    /// Updates the bypass state of a plugin by name
    /// </summary>
    /// <param name="pluginName">Name of the plugin</param>
    /// <param name="isBypassed">New bypass state</param>
    public void SetPluginBypassState(string pluginName, bool isBypassed)
    {
        var plugin = _allPlugins.FirstOrDefault(p =>
            p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

        if (plugin != null)
        {
            plugin.IsBypassed = isBypassed;
        }
    }

    /// <summary>
    /// Updates the preset name of a plugin by name
    /// </summary>
    /// <param name="pluginName">Name of the plugin</param>
    /// <param name="presetName">New preset name</param>
    public void SetPluginPresetName(string pluginName, string presetName)
    {
        var plugin = _allPlugins.FirstOrDefault(p =>
            p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

        if (plugin != null)
        {
            plugin.CurrentPresetName = presetName;
        }
    }

    #endregion

    #region Event Handlers

    private async void ScanPlugins_Click(object sender, RoutedEventArgs e)
    {
        await ScanPluginsAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.Trim();

        // Update placeholder and clear button visibility
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(searchText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ClearSearchButton.Visibility = string.IsNullOrEmpty(searchText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Filter plugins
        FilterPlugins(searchText);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VstPluginDisplayInfo plugin)
        {
            OnOpenPluginEditor?.Invoke(this, new VstPluginEventArgs(plugin));
        }
    }

    private void PluginCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Border border && border.Tag is VstPluginDisplayInfo plugin)
        {
            OnPluginDoubleClick?.Invoke(this, new VstPluginEventArgs(plugin));
        }
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VstPluginDisplayInfo plugin)
        {
            plugin.IsBypassed = !plugin.IsBypassed;
            OnBypassToggled?.Invoke(this, new VstBypassEventArgs(plugin, plugin.IsBypassed));
        }
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.Tag is VstPluginDisplayInfo plugin && e.AddedItems.Count > 0)
        {
            var selectedPreset = e.AddedItems[0] as string;
            if (!string.IsNullOrEmpty(selectedPreset))
            {
                OnPresetSelected?.Invoke(this, new VstPresetSelectedEventArgs(plugin, selectedPreset));
            }
        }
    }

    #endregion

    #region Private Methods

    private void FilterPlugins(string searchText)
    {
        _filteredPlugins.Clear();

        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? _allPlugins
            : _allPlugins.Where(p =>
                p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.Type.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.FullPath.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        foreach (var plugin in filtered)
        {
            _filteredPlugins.Add(plugin);
        }

        UpdatePluginCount();
        UpdateEmptyState();
    }

    private void UpdatePluginCount()
    {
        var total = _allPlugins.Count;
        var filtered = _filteredPlugins.Count;

        if (total == 0)
        {
            PluginCountText.Text = "0 plugins found";
        }
        else if (filtered == total)
        {
            PluginCountText.Text = $"{total} plugin{(total == 1 ? "" : "s")} found";
        }
        else
        {
            PluginCountText.Text = $"Showing {filtered} of {total} plugins";
        }
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _filteredPlugins.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdatePluginStatuses()
    {
        foreach (var plugin in _allPlugins)
        {
            plugin.IsLoaded = _loadedPlugins.Contains(plugin.Name);
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Extended VST plugin info with display properties for the enhanced panel
/// </summary>
public class VstPluginDisplayInfo : INotifyPropertyChanged
{
    private string _name = "";
    private string _type = "";
    private string _fullPath = "";
    private bool _isLoaded;
    private bool _isBypassed;
    private string _currentPresetName = "";
    private IVstPlugin? _pluginInstance;
    private ObservableCollection<string> _presetNames = new();
    private string _selectedPresetName = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Type
    {
        get => _type;
        set
        {
            _type = value;
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(TypeIcon));
            OnPropertyChanged(nameof(TypeBadgeBackground));
            OnPropertyChanged(nameof(TypeGradientStart));
            OnPropertyChanged(nameof(TypeGradientEnd));
        }
    }

    public string FullPath
    {
        get => _fullPath;
        set
        {
            _fullPath = value;
            OnPropertyChanged(nameof(FullPath));
            OnPropertyChanged(nameof(TruncatedPath));
        }
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            _isLoaded = value;
            OnPropertyChanged(nameof(IsLoaded));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBackground));
            OnPropertyChanged(nameof(StatusForeground));
            OnPropertyChanged(nameof(StatusDotColor));
            OnPropertyChanged(nameof(StatusVisibility));
            OnPropertyChanged(nameof(LoadedVisibility));
            OnPropertyChanged(nameof(PresetSelectorVisibility));
        }
    }

    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            _isBypassed = value;
            OnPropertyChanged(nameof(IsBypassed));
            OnPropertyChanged(nameof(BypassVisibility));
            OnPropertyChanged(nameof(BypassButtonText));
            OnPropertyChanged(nameof(BypassButtonBackground));
            OnPropertyChanged(nameof(BypassButtonForeground));
            OnPropertyChanged(nameof(BypassedOpacity));

            // Sync with plugin instance if available
            if (_pluginInstance != null && _pluginInstance.IsBypassed != value)
            {
                _pluginInstance.IsBypassed = value;
            }
        }
    }

    public string CurrentPresetName
    {
        get => _currentPresetName;
        set
        {
            _currentPresetName = value;
            OnPropertyChanged(nameof(CurrentPresetName));
            OnPropertyChanged(nameof(CurrentPresetDisplay));
            OnPropertyChanged(nameof(HasPreset));
            OnPropertyChanged(nameof(PresetVisibility));
        }
    }

    public IVstPlugin? PluginInstance
    {
        get => _pluginInstance;
        set
        {
            _pluginInstance = value;
            if (value != null)
            {
                // Load presets from the plugin
                _presetNames.Clear();
                var presets = value.GetPresetNames();
                foreach (var preset in presets)
                {
                    _presetNames.Add(preset);
                }

                CurrentPresetName = value.CurrentPresetName;
                IsBypassed = value.IsBypassed;

                // Subscribe to bypass changes
                value.BypassChanged += OnPluginBypassChanged;
            }
            OnPropertyChanged(nameof(PluginInstance));
            OnPropertyChanged(nameof(PresetNames));
            OnPropertyChanged(nameof(PresetSelectorVisibility));
        }
    }

    private void OnPluginBypassChanged(object? sender, bool isBypassed)
    {
        if (_isBypassed != isBypassed)
        {
            _isBypassed = isBypassed;
            OnPropertyChanged(nameof(IsBypassed));
            OnPropertyChanged(nameof(BypassVisibility));
            OnPropertyChanged(nameof(BypassButtonText));
            OnPropertyChanged(nameof(BypassButtonBackground));
            OnPropertyChanged(nameof(BypassButtonForeground));
            OnPropertyChanged(nameof(BypassedOpacity));
        }
    }

    public ObservableCollection<string> PresetNames => _presetNames;

    public string SelectedPresetName
    {
        get => _selectedPresetName;
        set
        {
            if (_selectedPresetName != value)
            {
                _selectedPresetName = value;
                OnPropertyChanged(nameof(SelectedPresetName));

                // Apply preset if plugin is available
                if (_pluginInstance != null && !string.IsNullOrEmpty(value))
                {
                    var index = _presetNames.IndexOf(value);
                    if (index >= 0)
                    {
                        _pluginInstance.SetPreset(index);
                        CurrentPresetName = _pluginInstance.CurrentPresetName;
                    }
                }
            }
        }
    }

    // Display properties

    /// <summary>
    /// Gets the type icon text (V3 or V2)
    /// </summary>
    public string TypeIcon => Type == "VST3" ? "V3" : "V2";

    /// <summary>
    /// Gets the badge background color based on type
    /// </summary>
    public Brush TypeBadgeBackground => Type == "VST3"
        ? new SolidColorBrush(Color.FromRgb(0x9C, 0x7C, 0xE8))
        : new SolidColorBrush(Color.FromRgb(0xE8, 0x9C, 0x4B));

    /// <summary>
    /// Gets the gradient start color for the icon
    /// </summary>
    public Color TypeGradientStart => Type == "VST3"
        ? Color.FromRgb(0xAA, 0x8C, 0xF8)
        : Color.FromRgb(0xF8, 0xAC, 0x5B);

    /// <summary>
    /// Gets the gradient end color for the icon
    /// </summary>
    public Color TypeGradientEnd => Type == "VST3"
        ? Color.FromRgb(0x7C, 0x5C, 0xC8)
        : Color.FromRgb(0xC8, 0x7C, 0x3B);

    /// <summary>
    /// Gets the truncated path for display
    /// </summary>
    public string TruncatedPath
    {
        get
        {
            var directory = Path.GetDirectoryName(FullPath) ?? "";

            // Try to show a meaningful truncated path
            if (directory.Length > 35)
            {
                // Find a good break point
                var parts = directory.Split(Path.DirectorySeparatorChar);
                if (parts.Length >= 3)
                {
                    return $"...{Path.DirectorySeparatorChar}{parts[^2]}{Path.DirectorySeparatorChar}{parts[^1]}";
                }
                return "..." + directory[^30..];
            }

            return directory;
        }
    }

    /// <summary>
    /// Gets the status text
    /// </summary>
    public string StatusText => IsLoaded ? "Active" : "";

    /// <summary>
    /// Gets the status background brush
    /// </summary>
    public Brush StatusBackground => IsLoaded
        ? new SolidColorBrush(Color.FromArgb(0x40, 0x6A, 0xAB, 0x73))
        : Brushes.Transparent;

    /// <summary>
    /// Gets the status foreground brush
    /// </summary>
    public Brush StatusForeground => IsLoaded
        ? new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73))
        : new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));

    /// <summary>
    /// Gets the status dot color
    /// </summary>
    public Brush StatusDotColor => IsLoaded
        ? new SolidColorBrush(Color.FromRgb(0x6A, 0xAB, 0x73))
        : new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));

    /// <summary>
    /// Gets the visibility for the status indicator
    /// </summary>
    public Visibility StatusVisibility => IsLoaded && !IsBypassed
        ? Visibility.Visible
        : Visibility.Collapsed;

    /// <summary>
    /// Gets the visibility for loaded state
    /// </summary>
    public Visibility LoadedVisibility => IsLoaded
        ? Visibility.Visible
        : Visibility.Collapsed;

    // Bypass-related display properties

    /// <summary>
    /// Gets the bypass indicator visibility
    /// </summary>
    public Visibility BypassVisibility => IsLoaded && IsBypassed
        ? Visibility.Visible
        : Visibility.Collapsed;

    /// <summary>
    /// Gets the bypass button text
    /// </summary>
    public string BypassButtonText => IsBypassed ? "Enable" : "Bypass";

    /// <summary>
    /// Gets the bypass button background color
    /// </summary>
    public Brush BypassButtonBackground => IsBypassed
        ? new SolidColorBrush(Color.FromRgb(0xE8, 0x9C, 0x4B))
        : new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41));

    /// <summary>
    /// Gets the bypass button foreground color
    /// </summary>
    public Brush BypassButtonForeground => IsBypassed
        ? Brushes.White
        : new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4));

    /// <summary>
    /// Gets the opacity when bypassed
    /// </summary>
    public double BypassedOpacity => IsBypassed ? 0.7 : 1.0;

    // Preset-related display properties

    /// <summary>
    /// Gets whether a preset name is available
    /// </summary>
    public bool HasPreset => !string.IsNullOrEmpty(CurrentPresetName);

    /// <summary>
    /// Gets the display text for the current preset
    /// </summary>
    public string CurrentPresetDisplay => HasPreset ? $"- {CurrentPresetName}" : "";

    /// <summary>
    /// Gets the visibility for preset display
    /// </summary>
    public Visibility PresetVisibility => HasPreset
        ? Visibility.Visible
        : Visibility.Collapsed;

    /// <summary>
    /// Gets whether to show the preset selector dropdown
    /// </summary>
    public bool ShowPresetSelector => IsLoaded && _presetNames.Count > 0;

    /// <summary>
    /// Gets the visibility for preset selector
    /// </summary>
    public Visibility PresetSelectorVisibility => ShowPresetSelector
        ? Visibility.Visible
        : Visibility.Collapsed;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Event args for VST plugin events
/// </summary>
public class VstPluginEventArgs : EventArgs
{
    public VstPluginDisplayInfo Plugin { get; }

    public VstPluginEventArgs(VstPluginDisplayInfo plugin)
    {
        Plugin = plugin;
    }
}

/// <summary>
/// Event args for scan completed event
/// </summary>
public class VstScanCompletedEventArgs : EventArgs
{
    public int PluginCount { get; }

    public VstScanCompletedEventArgs(int pluginCount)
    {
        PluginCount = pluginCount;
    }
}

/// <summary>
/// Event args for bypass toggle events
/// </summary>
public class VstBypassEventArgs : EventArgs
{
    public VstPluginDisplayInfo Plugin { get; }
    public bool IsBypassed { get; }

    public VstBypassEventArgs(VstPluginDisplayInfo plugin, bool isBypassed)
    {
        Plugin = plugin;
        IsBypassed = isBypassed;
    }
}

/// <summary>
/// Event args for preset selection events
/// </summary>
public class VstPresetSelectedEventArgs : EventArgs
{
    public VstPluginDisplayInfo Plugin { get; }
    public string PresetName { get; }

    public VstPresetSelectedEventArgs(VstPluginDisplayInfo plugin, string presetName)
    {
        Plugin = plugin;
        PresetName = presetName;
    }
}

#endregion
