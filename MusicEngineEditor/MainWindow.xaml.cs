// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Main application window.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MusicEngineEditor.Editor;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.Controls;
using MusicEngineEditor.ViewModels;
using MusicEngineEditor.Views;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor;

public partial class MainWindow : Window
{
    // Cached frozen brushes for hot-path methods (timer callbacks, audio reactive, log output)
    private static readonly SolidColorBrush s_statusGreenBrush;
    private static readonly SolidColorBrush s_statusGrayBrush;
    private static readonly SolidColorBrush s_runButtonRedBrush;
    private static readonly SolidColorBrush s_runButtonGreenBrush;
    private static readonly SolidColorBrush s_statusTextGreenBrush;
    private static readonly SolidColorBrush s_statusTextGrayBrush;
    private static readonly SolidColorBrush s_logErrorBrush;
    private static readonly SolidColorBrush s_logWarningBrush;
    private static readonly SolidColorBrush s_logDebugBrush;
    private static readonly SolidColorBrush s_logDefaultBrush;
    private static readonly SolidColorBrush s_timestampBrush;

    static MainWindow()
    {
        s_statusGreenBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));
        s_statusGreenBrush.Freeze();

        s_statusGrayBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        s_statusGrayBrush.Freeze();

        s_runButtonRedBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x2D, 0x2D));
        s_runButtonRedBrush.Freeze();

        s_runButtonGreenBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x5A, 0x2D));
        s_runButtonGreenBrush.Freeze();

        s_statusTextGreenBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x66));
        s_statusTextGreenBrush.Freeze();

        s_statusTextGrayBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
        s_statusTextGrayBrush.Freeze();

        s_logErrorBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));
        s_logErrorBrush.Freeze();

        s_logWarningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00));
        s_logWarningBrush.Freeze();

        s_logDebugBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
        s_logDebugBrush.Freeze();

        s_logDefaultBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        s_logDefaultBrush.Freeze();

        s_timestampBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));
        s_timestampBrush.Freeze();
    }

    private readonly EngineService _engineService;
    private readonly IProjectService _projectService;
    private readonly DispatcherTimer _statusTimer;
    private MusicProject? _currentProject;
    private readonly Dictionary<string, TabItem> _openTabs = new();
    private readonly Dictionary<TabItem, MusicScript> _tabScripts = new();
    private bool _hasUnsavedChanges;
    private bool _outputVisible = true;
    private bool _isRunning = false;
    private bool _isLiveMode = false;
    private CompletionProvider? _completionProvider;
    private InlineSliderService? _inlineSliderService;
    private MinimapControl? _minimap;
    private InlineVisualEngine? _inlineVisuals;
    private VisualizationIntegration? _visualization;
    private readonly PerformanceOptions _perfOptions = PerformanceConfig.Options;

    // VST Plugin Windows
    private readonly Dictionary<string, VstPluginWindow> _vstWindows = new();

    // DAW Windows
    private Views.PatternEditorWindow? _patternEditorWindow;
    private Views.ArrangementWindow? _arrangementWindow;
    private SessionViewWindow? _sessionViewWindow;

    // Transport ViewModel
    private TransportViewModel? _transportViewModel;

    // Performance Monitoring
    private readonly PerformanceMonitorService _performanceMonitorService;
    private readonly PerformanceViewModel _performanceViewModel;

    // Problems/Errors
    public ObservableCollection<ProblemItem> Problems { get; } = new();

    // State for tabs
    private enum OutputTab { Output, Console, Errors }
    private OutputTab _activeTab = OutputTab.Output;

    // Console buffer
    private readonly List<string> _consoleHistory = new();
    private int _consoleHistoryIndex = -1;

    // Active Instruments Display
    public ObservableCollection<ActiveInstrumentInfo> ActiveInstruments { get; } = new();
    private readonly DispatcherTimer _animationTimer;

    // Audio Reactive Lighting
    private AudioReactiveService? _audioReactiveService;
    private DropShadowEffect? _runButtonGlow;
    private readonly List<DropShadowEffect?> _sidebarGlows = new();
    private readonly SolidColorBrush _audioReactiveStatusBrush = new(Color.FromRgb(0x6F, 0x73, 0x7A));

    // Audio Visualizer Background Settings
    private bool _audioVisualizerEnabled = true;
    private float _audioVisualizerIntensity = 0.12f; // 12% max opacity (subtle)

    // Data for right panel lists
    public ObservableCollection<MidiDeviceInfo> MidiDevices { get; } = new();
    public ObservableCollection<AudioFileInfo> AudioFiles { get; } = new();

    // Track Management
    public ObservableCollection<TrackInfo> Tracks { get; } = new();
    private readonly Dictionary<int, FreezeTrackData> _frozenTrackData = new();

    public MainWindow()
    {
        InitializeComponent();

        // Get services from DI
        _engineService = App.Services.GetRequiredService<EngineService>();
        _projectService = App.Services.GetRequiredService<IProjectService>();

        // Load syntax highlighting and configure editor
        EditorSetup.Configure(CodeEditor);

        // Setup autocomplete using the new CompletionProvider
        // Triggers on Ctrl+Space and automatically on dot (.)
        _completionProvider = EditorSetup.SetupCompletion(CodeEditor);

        // Inline visuals (Strudel-style overlays under code lines)
        if (_perfOptions.EnableInlineVisuals)
        {
            _inlineVisuals = new InlineVisualEngine(CodeEditor);
        }

        // Handle Ctrl+Enter for run and other keyboard shortcuts
        CodeEditor.PreviewKeyDown += CodeEditor_PreviewKeyDown;

        // Start status update timer
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _statusTimer.Tick += StatusTimer_Tick;

        // Initialize engine async
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        // Track changes
        CodeEditor.TextChanged += (s, e) => _hasUnsavedChanges = true;
        CodeEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

        // Bind data to lists
        MidiDevicesList.ItemsSource = MidiDevices;
        AudioFilesList.ItemsSource = AudioFiles;
        ProblemsListView.ItemsSource = Problems;

        // Wire up VstPluginPanel events
        VstPluginsPanel.OnOpenPluginEditor += VstPluginsPanel_OnOpenPluginEditor;
        VstPluginsPanel.OnPluginDoubleClick += VstPluginsPanel_OnPluginDoubleClick;
        VstPluginsPanel.OnScanCompleted += VstPluginsPanel_OnScanCompleted;

        // Pipe MIDI log to output console and mirror to console tab
        _engineService.MidiLog += OnMidiLog;

        // Open synth editor when a synth is created via script
        _engineService.SynthCreated += OnSynthCreated;

        // Hook user console keydown
        UserConsoleBox.KeyDown += UserConsoleBox_KeyDown;

        // Attach Find/Replace control to editor
        FindReplaceBar.AttachToEditor(CodeEditor);

        // Setup hover tooltips for code
        var tooltipService = new Editor.CodeTooltipService(CodeEditor);

        // Setup inline sliders for numeric literals (like Strudel.cc)
        // Hover over a number to see a slider popup
        _inlineSliderService = EditorSetup.SetupInlineSliders(CodeEditor);

        // Setup parameter tooltips for function calls
        EditorSetup.SetupParameterTooltips(CodeEditor);

        // Setup color picker (Ctrl+Click on color values)
        EditorSetup.SetupColorPicker(CodeEditor);

        // Setup minimap (VS Code-style code overview)
        _minimap = MinimapHelper.CreateMinimap(CodeEditor);
        MinimapContainer.Child = _minimap;

        // Warm up audio engine on startup (non-blocking)
        _ = Task.Run(async () =>
        {
            try
            {
                await _engineService.InitializeAsync();
            }
            catch
            {
                // Ignore warmup errors; they will surface on actual run
            }
        });
        _inlineSliderService.ValueChanged += InlineSlider_ValueChanged;
        _inlineSliderService.ValueChangeCompleted += InlineSlider_ValueChangeCompleted;

        // Setup visualization integration for real-time playback highlighting
        _visualization = this.CreateVisualizationIntegration(CodeEditor);
        _visualization.VisualizationError += OnVisualizationError;

        // Setup context menu for code editor
        SetupEditorContextMenu();

        // Animation timer for pulsing active instruments
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _animationTimer.Tick += AnimationTimer_Tick;

        // Set initial content
        CodeEditor.Text = GetDefaultScript();
        _hasUnsavedChanges = false;

        // Wire up WorkshopPanel events
        WorkshopPanel.OnRunCode += WorkshopPanel_OnRunCode;
        WorkshopPanel.OnCopyCode += WorkshopPanel_OnCopyCode;
        WorkshopPanel.OnInsertCode += WorkshopPanel_OnInsertCode;

        // Initialize Performance Monitoring
        _performanceMonitorService = new PerformanceMonitorService();
        _performanceViewModel = new PerformanceViewModel(_performanceMonitorService);
        PerformanceMeterControl.ConnectToViewModel(_performanceViewModel);
    }

    private void CodeEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Ctrl+Enter to run script
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _ = ExecuteScript();
        }
        // Handle Escape to stop or close find/replace
        else if (e.Key == Key.Escape)
        {
            if (FindReplaceBar.Visibility == Visibility.Visible)
            {
                FindReplaceBar.Hide();
            }
            else
            {
                _engineService.AllNotesOff();
                _isRunning = false;
                _visualization?.OnPlaybackStopped();
                StatusText.Text = "Stopped";
                OutputLine("Stopped (Escape pressed)");
            }
            e.Handled = true;
        }
        // Handle Ctrl+F to find
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            FindReplaceBar.ShowFind();
        }
        // Handle Ctrl+H to find and replace
        else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            FindReplaceBar.ShowReplace();
        }
        // Handle F3 for AI Assistant toggle (or find next if FindReplaceBar is visible)
        else if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (FindReplaceBar.Visibility == Visibility.Visible)
            {
                // Find next is handled inside the control
            }
          
        }
        // Handle Ctrl+P for command palette
        else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowCommandPalette();
        }
        // Handle Alt+Up to move line(s) up
        else if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            e.Handled = true;
            MoveSelectedLinesUp();
        }
        // Handle Alt+Down to move line(s) down
        else if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            e.Handled = true;
            MoveSelectedLinesDown();
        }
        // Handle Ctrl+/ to toggle comment
        else if (e.Key == Key.Oem2 && Keyboard.Modifiers == ModifierKeys.Control) // Oem2 is /
        {
            e.Handled = true;
            ToggleLineComment();
        }
        // Handle Ctrl+Shift+D to duplicate line
        else if (e.Key == Key.D && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            DuplicateLine();
        }
        // Handle Ctrl+G to goto line
        else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowGotoLineDialog();
        }
        // Handle Ctrl+L to select current line
        else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            SelectCurrentLine();
        }
    }

    private void ToggleLineComment()
    {
        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        int startLine, endLine;
        if (selection.IsEmpty)
        {
            startLine = endLine = textArea.Caret.Line;
        }
        else
        {
            startLine = document.GetLineByOffset(selection.SurroundingSegment.Offset).LineNumber;
            endLine = document.GetLineByOffset(selection.SurroundingSegment.EndOffset).LineNumber;
            // If selection ends at start of line, don't include that line
            if (selection.SurroundingSegment.EndOffset == document.GetLineByNumber(endLine).Offset && endLine > startLine)
                endLine--;
        }

        document.BeginUpdate();
        try
        {
            // Check if all lines are already commented
            bool allCommented = true;
            for (int i = startLine; i <= endLine; i++)
            {
                var line = document.GetLineByNumber(i);
                var lineText = document.GetText(line.Offset, line.Length).TrimStart();
                if (!lineText.StartsWith("//"))
                {
                    allCommented = false;
                    break;
                }
            }

            // Toggle comments
            for (int i = startLine; i <= endLine; i++)
            {
                var line = document.GetLineByNumber(i);
                var lineText = document.GetText(line.Offset, line.Length);

                if (allCommented)
                {
                    // Remove comment
                    int commentIndex = lineText.IndexOf("//");
                    if (commentIndex >= 0)
                    {
                        // Remove "// " or "//"
                        int removeLength = (commentIndex + 2 < lineText.Length && lineText[commentIndex + 2] == ' ') ? 3 : 2;
                        document.Remove(line.Offset + commentIndex, removeLength);
                    }
                }
                else
                {
                    // Add comment at start of line (after leading whitespace)
                    int insertPos = 0;
                    while (insertPos < lineText.Length && char.IsWhiteSpace(lineText[insertPos]))
                        insertPos++;
                    document.Insert(line.Offset + insertPos, "// ");
                }
            }
        }
        finally
        {
            document.EndUpdate();
        }
    }

    private void DuplicateLine()
    {
        var document = CodeEditor.Document;
        var caret = CodeEditor.TextArea.Caret;
        var line = document.GetLineByNumber(caret.Line);
        var lineText = document.GetText(line.Offset, line.Length);

        document.Insert(line.EndOffset, Environment.NewLine + lineText);
        caret.Line = caret.Line + 1;
    }

    private void ShowGotoLineDialog()
    {
        var dialog = new System.Windows.Window
        {
            Title = "Go to Line",
            Width = 280,
            Height = 120,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18))
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        var label = new TextBlock
        {
            Text = $"Line number (1-{CodeEditor.Document.LineCount}):",
            Foreground = new SolidColorBrush(Colors.White),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var textBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
            Padding = new Thickness(8, 4, 8, 4)
        };
        textBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (int.TryParse(textBox.Text, out int lineNum) && lineNum >= 1 && lineNum <= CodeEditor.Document.LineCount)
                {
                    var targetLine = CodeEditor.Document.GetLineByNumber(lineNum);
                    CodeEditor.CaretOffset = targetLine.Offset;
                    CodeEditor.ScrollToLine(lineNum);
                    dialog.Close();
                }
            }
            else if (e.Key == Key.Escape)
            {
                dialog.Close();
            }
        };

        stack.Children.Add(label);
        stack.Children.Add(textBox);
        dialog.Content = stack;
        dialog.Loaded += (s, e) => textBox.Focus();
        dialog.ShowDialog();
    }

    private void SelectCurrentLine()
    {
        var document = CodeEditor.Document;
        var caret = CodeEditor.TextArea.Caret;
        var line = document.GetLineByNumber(caret.Line);
        CodeEditor.Select(line.Offset, line.TotalLength);
    }

    /// <summary>
    /// Window-level keyboard handler for global shortcuts like Panic (Alt+Space).
    /// This works regardless of which control has focus - critical for live performance.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Alt+Space for Panic (All Notes Off) - critical for live performance
        // This silences all audio immediately without stopping the script
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            e.Handled = true;
            _engineService.AllNotesOff();
            StatusText.Text = "Panic! All Notes Off";
            OutputLine("Panic! All Notes Off (Alt+Space)");

            // Reset status text after a brief delay
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                if (StatusText.Text == "Panic! All Notes Off")
                {
                    SetStatusText(_isRunning ? "Running" : "Ready");
                }
            };
            timer.Start();
        }

        // F3: AI Assistant Panel
      

        // Handle F4 for Synth Editor toggle
        if (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            ToggleSynthEditor_Click(this, new RoutedEventArgs());
        }

        // F5: Toggle Effects Editor
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            ToggleEffectsEditor_Click(this, new RoutedEventArgs());
        }

        // F6: Open Pattern Editor
        if (e.Key == Key.F6 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            OpenPatternEditor_Click(this, new RoutedEventArgs());
        }

        // F7: Toggle Mixer
        if (e.Key == Key.F7 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            ToggleMixer_Click(this, new RoutedEventArgs());
        }

        // F8: Open Arrangement
        if (e.Key == Key.F8 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            OpenArrangement_Click(this, new RoutedEventArgs());
        }

        // Ctrl+1 through Ctrl+5: Quick workspace presets
        if (HandleWorkspacePresetShortcut(e))
        {
            return;
        }

        // Ctrl+Shift+W: Open workspace presets dialog
        if (e.Key == Key.W && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            WorkspacePresets_Click(this, new RoutedEventArgs());
        }

        // F9: Performance Monitor
        if (e.Key == Key.F9 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            var dialog = new PerformanceDialog { Owner = this };
            dialog.ShowDialog();
        }

        // F10: Audio Statistics
        if (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            var dialog = new AudioStatisticsDialog { Owner = this };
            dialog.ShowDialog();
        }

        // F11: Toggle Fullscreen (maximize/restore)
        if (e.Key == Key.F11 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (WindowState == System.Windows.WindowState.Maximized)
            {
                WindowState = System.Windows.WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = System.Windows.WindowState.Maximized;
            }
        }

        // F12: Plugin Manager - open via command palette (requires VstHost service)
        if (e.Key == Key.F12 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            ShowCommandPalette(); // Opens command palette where user can search for plugin manager
        }

        // Ctrl+Shift+M: Toggle Mixer
        if (e.Key == Key.M && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            ToggleMixer_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Shift+P: Toggle Pattern Editor (piano roll)
        if (e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            OpenPatternEditor_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Shift+A: Toggle Arrangement
        if (e.Key == Key.A && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            OpenArrangement_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Alt+S: Open Session View
        if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            OpenSessionView_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Shift+E: Toggle Effects Editor
        if (e.Key == Key.E && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            ToggleEffectsEditor_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Shift+Y: Toggle Synth Editor
        if (e.Key == Key.Y && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            ToggleSynthEditor_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Shift+T: Track Template
        if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            var dialog = new TrackTemplateDialog { Owner = this };
            dialog.ShowDialog();
        }

        // Ctrl+Shift+R: Render Queue
        if (e.Key == Key.R && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            var dialog = new RenderQueueDialog { Owner = this };
            dialog.ShowDialog();
        }

        // Ctrl+Shift+L: Loudness Report
        if (e.Key == Key.L && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            var dialog = new LoudnessReportDialog { Owner = this };
            dialog.ShowDialog();
        }

        // Ctrl+Shift+G: Groove Template
        if (e.Key == Key.G && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            var dialog = new GrooveTemplateDialog { Owner = this };
            dialog.ShowDialog();
        }

        // Ctrl+Alt+T: Guitar Tuner
        if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            OpenGuitarTuner_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Alt+C: Chord Detector
        if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            OpenChordDetector_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Alt+K: Key Detector
        if (e.Key == Key.K && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            OpenKeyDetector_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Alt+B: Tempo Detector (BPM)
        if (e.Key == Key.B && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            OpenTempoDetector_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Alt+L: Loop Finder
        if (e.Key == Key.L && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            OpenLoopFinder_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Alt+P: Spatial Audio Panel
        if (e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            OpenSpatialPanel_Click(this, new RoutedEventArgs());
        }

        // Ctrl+Alt+A: Analysis Panel
        if (e.Key == Key.A && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            SwitchRightPanelTab("analysis");
        }

        // Ctrl+Alt+N: Network Sync Panel
        if (e.Key == Key.N && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            e.Handled = true;
            SwitchRightPanelTab("network");
        }
    }

    private void MoveSelectedLinesUp()
    {
        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        // Determine the range of lines to move
        int startLine, endLine;
        if (selection.IsEmpty)
        {
            // No selection - move current line
            startLine = endLine = textArea.Caret.Line;
        }
        else
        {
            // Selection exists - get the line range
            var startOffset = selection.SurroundingSegment.Offset;
            var endOffset = selection.SurroundingSegment.EndOffset;
            startLine = document.GetLineByOffset(startOffset).LineNumber;
            endLine = document.GetLineByOffset(endOffset).LineNumber;

            // If selection ends at start of a line, don't include that line
            var endLineObj = document.GetLineByOffset(endOffset);
            if (endOffset == endLineObj.Offset && endLine > startLine)
            {
                endLine--;
            }
        }

        // Can't move up if already at first line
        if (startLine <= 1)
            return;

        // Get the line above (the one we'll swap with)
        var lineAbove = document.GetLineByNumber(startLine - 1);
        var lineAboveText = document.GetText(lineAbove.Offset, lineAbove.Length);

        // Get the lines to move
        var firstLine = document.GetLineByNumber(startLine);
        var lastLine = document.GetLineByNumber(endLine);
        var movingLinesStart = firstLine.Offset;
        var movingLinesEnd = lastLine.EndOffset;
        var movingLinesText = document.GetText(movingLinesStart, movingLinesEnd - movingLinesStart);

        // Calculate cursor/selection positions relative to the moving text
        var caretOffsetInMovingText = textArea.Caret.Offset - movingLinesStart;
        var selectionStartOffset = selection.IsEmpty ? -1 : selection.SurroundingSegment.Offset - movingLinesStart;
        var selectionLength = selection.IsEmpty ? 0 : selection.SurroundingSegment.Length;

        // Perform the swap using a single document update
        document.BeginUpdate();
        try
        {
            // Remove the line above and moving lines, then insert in swapped order
            var fullStart = lineAbove.Offset;
            var fullEnd = lastLine.EndOffset;
            var fullLength = fullEnd - fullStart;

            // Build the new text: moving lines first, then line above
            string newText;
            if (lastLine.DelimiterLength > 0)
            {
                // Moving lines have a line ending
                newText = movingLinesText + lineAboveText;
            }
            else
            {
                // Last line has no line ending (end of document) - add one after moving text
                var lineEnding = document.GetText(lineAbove.EndOffset, lineAbove.DelimiterLength);
                if (string.IsNullOrEmpty(lineEnding))
                    lineEnding = "\n";
                newText = movingLinesText + lineEnding + lineAboveText;
            }

            document.Replace(fullStart, fullLength, newText);

            // Restore caret position (moved up by the length of the line above + its delimiter)
            var newCaretOffset = fullStart + caretOffsetInMovingText;
            textArea.Caret.Offset = Math.Max(0, Math.Min(newCaretOffset, document.TextLength));

            // Restore selection if there was one
            if (!selection.IsEmpty && selectionStartOffset >= 0)
            {
                var newSelectionStart = fullStart + selectionStartOffset;
                textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(
                    textArea,
                    newSelectionStart,
                    newSelectionStart + selectionLength);
            }
        }
        finally
        {
            document.EndUpdate();
        }
    }

    private void MoveSelectedLinesDown()
    {
        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        // Determine the range of lines to move
        int startLine, endLine;
        if (selection.IsEmpty)
        {
            // No selection - move current line
            startLine = endLine = textArea.Caret.Line;
        }
        else
        {
            // Selection exists - get the line range
            var startOffset = selection.SurroundingSegment.Offset;
            var endOffset = selection.SurroundingSegment.EndOffset;
            startLine = document.GetLineByOffset(startOffset).LineNumber;
            endLine = document.GetLineByOffset(endOffset).LineNumber;

            // If selection ends at start of a line, don't include that line
            var endLineObj = document.GetLineByOffset(endOffset);
            if (endOffset == endLineObj.Offset && endLine > startLine)
            {
                endLine--;
            }
        }

        // Can't move down if already at last line
        if (endLine >= document.LineCount)
            return;

        // Get the line below (the one we'll swap with)
        var lineBelow = document.GetLineByNumber(endLine + 1);
        var lineBelowText = document.GetText(lineBelow.Offset, lineBelow.Length);

        // Get the lines to move
        var firstLine = document.GetLineByNumber(startLine);
        var lastLine = document.GetLineByNumber(endLine);
        var movingLinesStart = firstLine.Offset;
        var movingLinesEnd = lastLine.EndOffset;
        var movingLinesText = document.GetText(movingLinesStart, movingLinesEnd - movingLinesStart);

        // Calculate cursor/selection positions relative to the moving text
        var caretOffsetInMovingText = textArea.Caret.Offset - movingLinesStart;
        var selectionStartOffset = selection.IsEmpty ? -1 : selection.SurroundingSegment.Offset - movingLinesStart;
        var selectionLength = selection.IsEmpty ? 0 : selection.SurroundingSegment.Length;

        // Calculate how far down the text will move
        var lineBelowLength = lineBelow.Length + lineBelow.DelimiterLength;

        // Perform the swap using a single document update
        document.BeginUpdate();
        try
        {
            // Remove moving lines and line below, then insert in swapped order
            var fullStart = firstLine.Offset;
            var fullEnd = lineBelow.EndOffset;
            var fullLength = fullEnd - fullStart;

            // Build the new text: line below first, then moving lines
            string newText;
            if (lineBelow.DelimiterLength > 0)
            {
                // Line below has a line ending - use it after line below text
                var lineEnding = document.GetText(lastLine.EndOffset, lastLine.DelimiterLength);
                if (string.IsNullOrEmpty(lineEnding))
                    lineEnding = "\n";
                newText = lineBelowText + lineEnding + movingLinesText;
            }
            else
            {
                // Line below is last line (no line ending) - add ending after line below, remove from moving text
                var lineEnding = document.GetText(lastLine.EndOffset, lastLine.DelimiterLength);
                if (string.IsNullOrEmpty(lineEnding))
                    lineEnding = "\n";
                // Remove trailing line ending from moving text if present
                var trimmedMovingText = movingLinesText;
                if (trimmedMovingText.EndsWith("\r\n"))
                    trimmedMovingText = trimmedMovingText.Substring(0, trimmedMovingText.Length - 2);
                else if (trimmedMovingText.EndsWith("\n") || trimmedMovingText.EndsWith("\r"))
                    trimmedMovingText = trimmedMovingText.Substring(0, trimmedMovingText.Length - 1);
                newText = lineBelowText + lineEnding + trimmedMovingText;
            }

            document.Replace(fullStart, fullLength, newText);

            // Restore caret position (moved down by the length of the line below)
            var newCaretOffset = fullStart + lineBelowLength + caretOffsetInMovingText;
            // Adjust if we're at end of document
            if (lineBelow.DelimiterLength == 0)
            {
                // Line below had no delimiter, we added one
                newCaretOffset = fullStart + lineBelow.Length + 1 + caretOffsetInMovingText;
                // Account for removed delimiter from moving text
                if (movingLinesText.EndsWith("\r\n"))
                    newCaretOffset = Math.Min(newCaretOffset, document.TextLength);
            }
            textArea.Caret.Offset = Math.Max(0, Math.Min(newCaretOffset, document.TextLength));

            // Restore selection if there was one
            if (!selection.IsEmpty && selectionStartOffset >= 0)
            {
                var newSelectionStart = fullStart + lineBelowLength + selectionStartOffset;
                if (lineBelow.DelimiterLength == 0)
                {
                    newSelectionStart = fullStart + lineBelow.Length + 1 + selectionStartOffset;
                }
                newSelectionStart = Math.Max(0, Math.Min(newSelectionStart, document.TextLength));
                var newSelectionEnd = Math.Min(newSelectionStart + selectionLength, document.TextLength);
                textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(
                    textArea,
                    newSelectionStart,
                    newSelectionEnd);
            }
        }
        finally
        {
            document.EndUpdate();
        }
    }

    #region Context Menu

    private void SetupEditorContextMenu()
    {
        var contextMenu = new System.Windows.Controls.ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2B, 0x2D, 0x30)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3C, 0x3F, 0x41)),
        };

        // Standard edit commands
        var cutItem = new System.Windows.Controls.MenuItem { Header = "Cut", InputGestureText = "Ctrl+X" };
        cutItem.Click += (s, e) => CodeEditor.Cut();
        contextMenu.Items.Add(cutItem);

        var copyItem = new System.Windows.Controls.MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
        copyItem.Click += (s, e) => CodeEditor.Copy();
        contextMenu.Items.Add(copyItem);

        var pasteItem = new System.Windows.Controls.MenuItem { Header = "Paste", InputGestureText = "Ctrl+V" };
        pasteItem.Click += (s, e) => CodeEditor.Paste();
        contextMenu.Items.Add(pasteItem);

        contextMenu.Items.Add(new Separator());

        // Find/Replace
        var findItem = new System.Windows.Controls.MenuItem { Header = "Find...", InputGestureText = "Ctrl+F" };
        findItem.Click += (s, e) => FindReplaceBar.ShowFind();
        contextMenu.Items.Add(findItem);

        var replaceItem = new System.Windows.Controls.MenuItem { Header = "Replace...", InputGestureText = "Ctrl+H" };
        replaceItem.Click += (s, e) => FindReplaceBar.ShowReplace();
        contextMenu.Items.Add(replaceItem);

        contextMenu.Items.Add(new Separator());

        // VST specific option (dynamically enabled)
        var openVstItem = new System.Windows.Controls.MenuItem { Header = "Open VST Editor", IsEnabled = false };
        openVstItem.Click += ContextMenu_OpenVstEditor;
        contextMenu.Items.Add(openVstItem);

        // Run selection
        var runItem = new System.Windows.Controls.MenuItem { Header = "Run Script", InputGestureText = "Ctrl+Enter" };
        runItem.Click += (s, e) => _ = ExecuteScript();
        contextMenu.Items.Add(runItem);

        contextMenu.Opened += (s, e) =>
        {
            // Check if we're on a VST variable
            var vstName = GetVstNameAtCursor();
            openVstItem.IsEnabled = vstName != null;
            openVstItem.Tag = vstName;
        };

        CodeEditor.ContextMenu = contextMenu;

        // Double-click handler for VST names
        CodeEditor.TextArea.TextView.MouseLeftButtonDown += TextView_MouseLeftButtonDown;
    }

    private string? GetVstNameAtCursor()
    {
        var position = CodeEditor.TextArea.Caret.Position;
        var line = CodeEditor.Document.GetLineByNumber(position.Line);
        var lineText = CodeEditor.Document.GetText(line.Offset, line.Length);

        // Find word at cursor
        var column = position.Column - 1;
        if (column < 0 || column >= lineText.Length) return null;

        int start = column;
        int end = column;

        while (start > 0 && (char.IsLetterOrDigit(lineText[start - 1]) || lineText[start - 1] == '_'))
            start--;

        while (end < lineText.Length && (char.IsLetterOrDigit(lineText[end]) || lineText[end] == '_'))
            end++;

        if (start >= end) return null;

        var word = lineText.Substring(start, end - start);

        // Check if this word is a VST variable by looking for vst.load patterns
        var vstPattern = new System.Text.RegularExpressions.Regex($@"var\s+{word}\s*=\s*vst\.load\s*\([""']([^""']+)[""']\)");
        var match = vstPattern.Match(CodeEditor.Text);
        if (match.Success)
        {
            return word;
        }

        // Also check if the word itself matches a known VST plugin
        foreach (var plugin in VstPluginsPanel.Plugins)
        {
            if (plugin.Name.Equals(word, StringComparison.OrdinalIgnoreCase))
            {
                return word;
            }
        }

        return null;
    }

    private void ContextMenu_OpenVstEditor(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && item.Tag is string vstName)
        {
            OpenVstWindowByName(vstName);
        }
    }

    private void OpenVstWindowByName(string name)
    {
        // Try to find or create VST window
        if (_vstWindows.TryGetValue(name, out var existingWindow))
        {
            existingWindow.Show();
            existingWindow.WindowState = System.Windows.WindowState.Normal;
            existingWindow.Activate();
        }
        else
        {
            // Find the VST plugin in the panel's list
            var plugin = VstPluginsPanel.Plugins.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (plugin != null)
            {
                var window = new VstPluginWindow(plugin.Name, plugin.FullPath);
                _vstWindows[name] = window;
                window.Show();
                OutputLine($"Opened VST window: {name}");
            }
            else
            {
                // Plugin not found in panel, open with just the name
                OpenVstPluginWindow(name, name);
            }
        }
    }

    private DateTime _lastClickTime = DateTime.MinValue;
    private int _lastClickOffset = -1;

    private void TextView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Check for double-click
        var now = DateTime.Now;
        var position = CodeEditor.GetPositionFromPoint(e.GetPosition(CodeEditor));

        if (position == null) return;

        var offset = CodeEditor.Document.GetOffset(position.Value.Location);

        if ((now - _lastClickTime).TotalMilliseconds < 300 && Math.Abs(offset - _lastClickOffset) < 5)
        {
            // Double-click detected - check if on VST name
            var vstName = GetVstNameAtCursor();
            if (vstName != null)
            {
                OpenVstWindowByName(vstName);
                e.Handled = true;
            }
        }

        _lastClickTime = now;
        _lastClickOffset = offset;
    }

    #endregion

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Initializing engine...";
        OutputLine("MusicEngine Editor starting...");
        OutputLine("");

        try
        {
            await _engineService.InitializeAsync();
            _statusTimer.Start();

            // Show device enumeration output from engine initialization
            if (!string.IsNullOrEmpty(_engineService.InitializationOutput))
            {
                OutputLine("=== Audio/MIDI Devices ===");
                OutputLine(_engineService.InitializationOutput);
                OutputLine("==========================");
                OutputLine("");
            }

            // Populate MIDI devices list
            if (_perfOptions.EnableMidi)
            {
                RefreshMidiDevices();
            }
            else
            {
                OutputLine("MIDI disabled (ME_PERF_PROFILE/ME_DISABLE_MIDI). Skipping device scan.");
            }

            // Connect visualization and inline visuals to the sequencer
            if (_engineService.Sequencer != null)
            {
                _visualization?.ConnectToSequencer(_engineService.Sequencer);
                if (_inlineVisuals != null)
                {
                    _inlineVisuals.Sequencer = _engineService.Sequencer;
                }
            }
            else if (!_perfOptions.StartSequencer)
            {
                OutputLine("Sequencer kept idle by performance profile.");
            }

            // Initialize Transport ViewModel
            _transportViewModel = new TransportViewModel();

            // Start Performance Monitoring
            if (_perfOptions.EnablePerfMonitor)
            {
                _performanceMonitorService.Start();
            }
            else
            {
                StatusText.Text = "Perf monitor disabled for low-power mode";
            }

            // Initialize Audio Reactive Lighting
            InitializeAudioReactiveLighting();

            SetStatusText("Ready");
            OutputLine("Engine initialized successfully!");
            OutputLine("Press Ctrl+Enter to run the script, Escape to stop.");
            OutputLine("");

            // Warm-up compile/run to make first Run snappier
            if (_perfOptions.StartSequencer)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _engineService.ExecuteScriptAsync("Sequencer.Bpm = 120;");
                    }
                    catch { /* ignore warmup errors */ }
                });
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Engine initialization failed";
            OutputLine($"ERROR: Failed to initialize engine: {ex.Message}");
            OutputLine("Check if audio devices are available and not in use by another application.");
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                SaveAll_Click(this, new RoutedEventArgs());
            }
        }

        // Unsubscribe from service events to prevent leaks
        _engineService.MidiLog -= OnMidiLog;
        _engineService.SynthCreated -= OnSynthCreated;
        if (_visualization != null)
            _visualization.VisualizationError -= OnVisualizationError;
        if (_audioReactiveService != null)
            _audioReactiveService.ValuesUpdated -= OnAudioReactiveValuesUpdated;

        _statusTimer.Stop();
        _sliderHotReloadTimer?.Stop();
        _inlineSliderService?.Dispose();
        _inlineVisuals?.Dispose();
        _visualization?.Dispose();
        _transportViewModel?.Dispose();
        _performanceMonitorService.Dispose();
        CloseAllVstWindows();
        _engineService.Dispose();

        // Mark session as cleanly closed (no crash recovery needed)
        try
        {
            RecoveryService.Instance.MarkSessionClosed();
            AutoSaveService.Instance.Dispose();
        }
        catch
        {
            // Ignore errors during shutdown
        }
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        // Adapt polling rate: tighter when running, relaxed when idle
        var targetMs = _isRunning ? 50 : 180;
        if (Math.Abs(_statusTimer.Interval.TotalMilliseconds - targetMs) > 1)
        {
            _statusTimer.Interval = TimeSpan.FromMilliseconds(targetMs);
        }


        // Update status indicator based on running state
        if (_isRunning)
        {
            StatusIndicator.Fill = s_statusGreenBrush; // Green
        }
        else
        {
            StatusIndicator.Fill = s_statusGrayBrush; // Gray
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        CaretPositionDisplay.Text = $"Ln {CodeEditor.TextArea.Caret.Line}, Col {CodeEditor.TextArea.Caret.Column}";
    }

    private void OnMidiLog(string msg)
    {
        Dispatcher.BeginInvoke(() =>
        {
            OutputLine(msg);
            if (_activeTab == OutputTab.Console)
            {
                AppendConsole(msg);
            }
        });
    }

    private void OnSynthCreated(object synth, string name, string typeName)
    {
        Dispatcher.BeginInvoke(() =>
        {
            SynthEditorPanel.RegisterSynth(synth, name, typeName);
            OpenSynthEditor(synth, name, typeName);
        });
    }

    private void OnVisualizationError(object? sender, string msg)
    {
        OutputLine($"[Visualization] {msg}");
    }

    #region Inline Slider Events

    private void InlineSlider_ValueChanged(object? sender, SliderValueChangedEventArgs e)
    {
        // Mark as having unsaved changes
        _hasUnsavedChanges = true;

        // If script is running, trigger hot-reload
        if (_isRunning)
        {
            // Debounce hot-reload to avoid too many re-evaluations
            if (_sliderHotReloadTimer != null)
            {
                _sliderHotReloadTimer.Stop();
                _sliderHotReloadTimer = null;
            }
            _sliderHotReloadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _sliderHotReloadTimer.Tick += async (s, args) =>
            {
                _sliderHotReloadTimer.Stop();
                await TriggerHotReload();
            };
            _sliderHotReloadTimer.Start();
        }
    }

    private void InlineSlider_ValueChangeCompleted(object? sender, SliderValueChangedEventArgs e)
    {
        // Final update when slider is released
        _hasUnsavedChanges = true;

        // If script is running, do a final hot-reload
        if (_isRunning)
        {
            _sliderHotReloadTimer?.Stop();
            _ = TriggerHotReload();
        }

        // Show feedback in status
        var context = e.Number.Context ?? e.Number.SliderConfig?.Label ?? "value";
        StatusText.Text = $"Changed {context}: {e.OldValue:F2} -> {e.NewValue:F2}";
    }

    private DispatcherTimer? _sliderHotReloadTimer;

    private async Task TriggerHotReload()
    {
        try
        {
            var code = CodeEditor.Text;
            var result = await _engineService.ExecuteScriptAsync(code);

            if (!result.Success)
            {
                // Collect errors quietly in Errors tab
                foreach (var error in result.Errors)
                {
                    Problems.Add(new ProblemItem
                    {
                        Severity = error.Severity == "Error" ? ProblemSeverity.Error : ProblemSeverity.Warning,
                        Message = error.Message,
                        FileName = GetCurrentFileName(),
                        FilePath = GetCurrentFilePath(),
                        Line = error.Line,
                        Column = error.Column,
                        Suggestion = SuggestFor(error.Message)
                    });
                }
                UpdateErrorBadge();
            }
        }
        catch (Exception ex)
        {
            Problems.Add(new ProblemItem
            {
                Severity = ProblemSeverity.Error,
                Message = ex.Message,
                FileName = GetCurrentFileName(),
                FilePath = GetCurrentFilePath(),
                Line = 1,
                Column = 1,
                Suggestion = SuggestFor(ex.Message)
            });
            UpdateErrorBadge();
        }
    }

    #endregion

    #region Project Management

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProjectDialog();
        if (IsLoaded) dialog.Owner = this;

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // Validate dialog inputs before proceeding
                if (string.IsNullOrWhiteSpace(dialog.ProjectName))
                {
                    MessageBox.Show("Project name cannot be empty.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(dialog.ProjectLocation))
                {
                    MessageBox.Show("Project location cannot be empty.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Creating project...";
                _currentProject = await _projectService.CreateProjectAsync(dialog.ProjectName, dialog.ProjectLocation);

                // Verify project was created successfully
                if (_currentProject == null)
                {
                    StatusText.Text = "Failed to create project";
                    OutputLine("ERROR: Project creation returned null");
                    MessageBox.Show("Failed to create project: Project creation returned null.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                UpdateProjectExplorer();
                UpdateAudioFilesList();
                ProjectNameDisplay.Text = _currentProject.Name;
                StatusText.Text = $"Created project: {_currentProject.Name}";
                OutputLine($"Created new project: {_currentProject.Name}");

                // Mark session as active for crash recovery
                RecoveryService.Instance.MarkSessionActive(_currentProject);

                // Initialize auto-save for this project
                AutoSaveService.Instance.Initialize(_projectService);

                // Add to recent projects
                var recentProjectsService = App.Services.GetService<IRecentProjectsService>();
                if (recentProjectsService != null && !string.IsNullOrEmpty(_currentProject.FilePath))
                {
                    await recentProjectsService.AddProjectAsync(_currentProject.FilePath, _currentProject.Name);
                }

                // Open entry point script (with null checks)
                if (_currentProject.Scripts != null && _currentProject.Scripts.Count > 0)
                {
                    var entryScript = _currentProject.Scripts[0];
                    if (entryScript != null)
                    {
                        OpenScriptInTab(entryScript);
                    }
                    else
                    {
                        OutputLine("Warning: Entry script is null.");
                    }
                }
                else
                {
                    OutputLine("Warning: No scripts were created with the project.");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Project creation failed";
                OutputLine($"ERROR: Failed to create project: {ex.Message}");
                MessageBox.Show($"Failed to create project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MusicEngine Projects (*.meproj)|*.meproj|All Files (*.*)|*.*",
            DefaultExt = ".meproj"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                StatusText.Text = "Loading project...";
                _currentProject = await _projectService.OpenProjectAsync(dialog.FileName);

                // Verify project was loaded successfully
                if (_currentProject == null)
                {
                    StatusText.Text = "Failed to load project";
                    OutputLine("ERROR: Project loading returned null");
                    MessageBox.Show("Failed to load project: Project file could not be parsed.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                UpdateProjectExplorer();
                UpdateAudioFilesList();
                ProjectNameDisplay.Text = _currentProject.Name;
                StatusText.Text = $"Loaded: {_currentProject.Name}";
                OutputLine($"Loaded project: {_currentProject.Name}");

                // Mark session as active for crash recovery
                RecoveryService.Instance.MarkSessionActive(_currentProject);

                // Initialize auto-save for this project
                AutoSaveService.Instance.Initialize(_projectService);

                // Add to recent projects
                var recentProjectsService = App.Services.GetService<IRecentProjectsService>();
                if (recentProjectsService != null)
                {
                    await recentProjectsService.AddProjectAsync(dialog.FileName, _currentProject.Name);
                }

                // Open entry point script (with null checks)
                if (_currentProject.Scripts != null && _currentProject.Scripts.Count > 0)
                {
                    foreach (var script in _currentProject.Scripts)
                    {
                        if (script != null && script.IsEntryPoint)
                        {
                            OpenScriptInTab(script);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Project loading failed";
                OutputLine($"ERROR: Failed to open project: {ex.Message}");
                MessageBox.Show($"Failed to open project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void UpdateProjectExplorer()
    {
        ProjectTree.Items.Clear();

        if (_currentProject == null) return;

        // Project root
        var projectItem = new TreeViewItem
        {
            Header = _currentProject.Name,
            IsExpanded = true
        };

        // Scripts folder
        var scriptsFolder = new TreeViewItem
        {
            Header = "Scripts",
            IsExpanded = true
        };

        foreach (var script in _currentProject.Scripts)
        {
            var scriptItem = new TreeViewItem
            {
                Header = script.IsEntryPoint ? $"{script.FileName} (Entry)" : script.FileName,
                Tag = script
            };
            scriptsFolder.Items.Add(scriptItem);
        }

        projectItem.Items.Add(scriptsFolder);

        // Audio folder
        var audioFolder = new TreeViewItem
        {
            Header = "Audio",
            IsExpanded = true
        };

        foreach (var asset in _currentProject.AudioAssets)
        {
            var assetItem = new TreeViewItem
            {
                Header = $"{asset.Alias} ({asset.FileName})",
                Tag = asset
            };
            audioFolder.Items.Add(assetItem);
        }

        projectItem.Items.Add(audioFolder);

        // References folder (if any)
        if (_currentProject.References.Count > 0)
        {
            var refsFolder = new TreeViewItem
            {
                Header = "References",
                IsExpanded = true
            };

            foreach (var reference in _currentProject.References)
            {
                var refItem = new TreeViewItem
                {
                    Header = reference.Alias,
                    Tag = reference
                };
                refsFolder.Items.Add(refItem);
            }

            projectItem.Items.Add(refsFolder);
        }

        ProjectTree.Items.Add(projectItem);
    }

    private void ProjectTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProjectTree.SelectedItem is TreeViewItem item && item.Tag is MusicScript script)
        {
            OpenScriptInTab(script);
        }
    }

    #endregion

    #region Tab Management

    private void OpenScriptInTab(MusicScript script)
    {
        // Null check for script parameter
        if (script == null)
        {
            OutputLine("Warning: Attempted to open a null script.");
            return;
        }

        // Validate script has required properties
        if (string.IsNullOrEmpty(script.FilePath))
        {
            OutputLine("Warning: Script has no file path.");
            return;
        }

        // Check if already open
        if (_openTabs.TryGetValue(script.FilePath, out var existingTab))
        {
            // Save current tab's content before switching
            SaveCurrentEditorContent();
            EditorTabs.SelectedItem = existingTab;
            // Note: Content will be loaded by SelectionChanged event
            return;
        }

        // Save current tab's content before opening new tab
        SaveCurrentEditorContent();

        // Create new tab
        var tab = new TabItem
        {
            Header = script.FileName ?? "Untitled",
            Tag = script
        };

        _openTabs[script.FilePath] = tab;
        _tabScripts[tab] = script;
        EditorTabs.Items.Add(tab);
        EditorTabs.SelectedItem = tab;

        CodeEditor.Text = script.Content ?? string.Empty;
        
    }

    private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // First, save content of the PREVIOUS tab (the one being switched away from)
        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem previousTab)
        {
            if (_tabScripts.TryGetValue(previousTab, out var previousScript))
            {
                if (previousScript.Content != CodeEditor.Text)
                {
                    previousScript.Content = CodeEditor.Text;
                    previousScript.IsDirty = true;
                }
            }
        }

        // Then load the NEW tab's content
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem newTab)
        {
            if (_tabScripts.TryGetValue(newTab, out var script))
            {
                CodeEditor.Text = script.Content ?? string.Empty;
               
            }
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TabItem tab)
        {
            if (_tabScripts.TryGetValue(tab, out var script))
            {
                if (script.IsDirty)
                {
                    var result = MessageBox.Show(
                        $"Save changes to {script.FileName}?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) return;
                    if (result == MessageBoxResult.Yes)
                    {
                        script.Content = CodeEditor.Text;
                        _ = _projectService.SaveScriptAsync(script);
                    }
                }

                _openTabs.Remove(script.FilePath);
                _tabScripts.Remove(tab);
            }

            EditorTabs.Items.Remove(tab);

            if (EditorTabs.Items.Count == 0)
            {
                CodeEditor.Text = "";
               
            }
        }
    }

    private void SaveCurrentEditorContent()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            if (script.Content != CodeEditor.Text)
            {
                script.Content = CodeEditor.Text;
                script.IsDirty = true;
            }
        }
    }

    #endregion

    #region File Operations

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Simple input dialog for script name
        var name = InputDialog.Show("Enter script name:", "New Script", "NewScript", this);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var script = _projectService.CreateScript(_currentProject, name);
            _currentProject.Scripts.Add(script);
            UpdateProjectExplorer();
            OpenScriptInTab(script);
        }
    }

    private async void SaveScript_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEditorContent();

        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            await _projectService.SaveScriptAsync(script);
            SetStatusText($"Saved: {script.FileName}");
        }
        else if (_currentProject == null)
        {
            // Legacy mode - save as single file
            var dialog = new SaveFileDialog
            {
                Filter = "C# Script Files (*.csx)|*.csx|MusicEngine Scripts (*.me)|*.me|All Files (*.*)|*.*",
                DefaultExt = ".csx"
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, CodeEditor.Text);
                SetStatusText($"Saved: {Path.GetFileName(dialog.FileName)}");
            }
        }

        _hasUnsavedChanges = false;

        // Live Mode: Auto-reload script on save
        if (_isLiveMode && _isRunning)
        {
            await TriggerHotReload();
            SetStatusText("Live reload complete");
        }
    }

    private void LiveModeToggle_Click(object sender, RoutedEventArgs e)
    {
       
    }

    private async void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEditorContent();

        foreach (var kvp in _tabScripts)
        {
            if (kvp.Value.IsDirty)
            {
                await _projectService.SaveScriptAsync(kvp.Value);
            }
        }

        if (_currentProject != null)
        {
            await _projectService.SaveProjectAsync(_currentProject);
        }

        _hasUnsavedChanges = false;
        StatusText.Text = "All files saved";
    }

    #endregion

    #region Script Execution

    private async void RunScript_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteScript();
    }

    private async void RunStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            StopExecution();
        }
        else
        {
            await ExecuteScript();
        }
    }

    private async Task ExecuteScript()
    {
        SaveCurrentEditorContent();
        var code = CodeEditor.Text;

        if (string.IsNullOrWhiteSpace(code))
        {
            OutputLine("No code to execute.");
            return;
        }

        _isRunning = true;
        StatusText.Text = "Executing...";
        UpdateRunStopButton();

        // Clear previous problems
        Problems.Clear();
        UpdateErrorBadge();

        OutputLine("----------------------------------------");
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Executing script...");

        // Notify visualization system before execution
        _visualization?.OnBeforeExecute(code);

        var stopwatch = Stopwatch.StartNew();
        var currentFileName = GetCurrentFileName();

        // Clear synth registry before new script execution
        SynthEditorPanel.ClearRegisteredSynths();

        try
        {
            var result = await _engineService.ExecuteScriptAsync(code);
            stopwatch.Stop();

        if (result.Success)
        {
            SetStatusText($"Running ({stopwatch.ElapsedMilliseconds}ms)");
            OutputLine($"Script executed successfully ({stopwatch.ElapsedMilliseconds}ms)");

            if (!string.IsNullOrEmpty(result.Output))
            {
                OutputLine(result.Output);
                AppendConsole(result.Output);
            }

                // Notify visualization system after successful execution
                _visualization?.OnAfterExecute(true);
                _visualization?.OnPlaybackStarted();

                // Start audio reactive lighting
                StartAudioReactiveLighting();

                // Start playback time tracking
                StartPlaybackTimeTracking();

                // Parse code to extract instruments and start animation
                ExtractInstrumentsFromCode(code);
                _animationTimer.Start();
            }
            else
            {
                _isRunning = false;
                UpdateRunStopButton();
                StatusText.Text = "Script error";

                foreach (var error in result.Errors)
                {
                    // Add to Problems panel
                    Problems.Add(new ProblemItem
                    {
                        Severity = error.Severity == "Error" ? ProblemSeverity.Error : ProblemSeverity.Warning,
                        Message = error.Message,
                        FileName = currentFileName,
                        FilePath = GetCurrentFilePath(),
                        Line = error.Line,
                        Column = error.Column,
                        Suggestion = SuggestFor(error.Message)
                    });
                }

                UpdateErrorBadge();

                // Switch to Problems tab if there are errors
                if (Problems.Count > 0)
                {
                    SwitchOutputTab(false);
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _isRunning = false;
            StatusText.Text = "Execution failed";

            // Add exception to Problems
            Problems.Add(new ProblemItem
            {
                Severity = ProblemSeverity.Error,
                Message = ex.Message,
                FileName = currentFileName,
                FilePath = GetCurrentFilePath(),
                Line = 1,
                Column = 1,
                Suggestion = SuggestFor(ex.Message)
            });
            UpdateErrorBadge();
        }

        UpdateRunStopButton();
    }

    private string GetCurrentFileName()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            return script.FileName;
        }
        return "Script";
    }

    private string GetCurrentFilePath()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            return script.FilePath;
        }
        return "";
    }

    private void UpdateErrorBadge()
    {
        var errorCount = Problems.Count(p => p.Severity == ProblemSeverity.Error);
        if (errorCount > 0)
        {
            
        }
        else
        {
           
        }
    }

    // Suggestion helper for error hints
    private static string SuggestFor(string message)
    {
        var msg = message.ToLowerInvariant();
        if (msg.Contains("synth")) return "Did you mean 'synth' or check routing?";
        if (msg.Contains("device")) return "Check midi.device(index) and routing.";
        if (msg.Contains("note") && msg.Contains("range")) return "Verify Note(...) arguments (pitch/beat/duration/velocity).";
        if (msg.Contains("vst")) return "Is the plugin installed and scanned?";
        return string.Empty;
    }

    private void ExtractInstrumentsFromCode(string code)
    {
        ClearActiveInstruments();

        // Find synth declarations: var name = CreateSynth();
        var synthPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*CreateSynth\s*\(");
        foreach (System.Text.RegularExpressions.Match match in synthPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "synth");
        }

        // Find VST declarations: var name = vst.load("...")
        var vstPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*vst\.load\s*\(");
        foreach (System.Text.RegularExpressions.Match match in vstPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "vst");
        }

        // Find pattern declarations: var name = CreatePattern(...)
        var patternPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*CreatePattern\s*\(");
        foreach (System.Text.RegularExpressions.Match match in patternPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "pattern");
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopExecution();
    }

    private void StopExecution()
    {
        _engineService.AllNotesOff();
        _isRunning = false;
        _animationTimer.Stop();

        // Stop audio reactive lighting
        StopAudioReactiveLighting();

        // Stop playback time tracking
        StopPlaybackTimeTracking();

        // Notify visualization system that playback stopped
        _visualization?.OnPlaybackStopped();

        // Update button to show Run state
        UpdateRunStopButton();

        ClearActiveInstruments();
        SetStatusText("Stopped");
        OutputLine("Stopped");
    }

    private void UpdateRunStopButton()
    {
        if (_isRunning)
        {
            // Show Stop state (red)
            RunStopButton.Background = s_runButtonRedBrush;
            RunStopIcon.Text = "\u25A0"; // Square (stop icon)
            RunStopText.Text = "Stop";

            // Update glow color for hover effect
            if (RunStopButton.Template.FindName("glowEffect", RunStopButton) is DropShadowEffect glow)
            {
                glow.Color = Color.FromRgb(0xD3, 0x2F, 0x2F);
            }
        }
        else
        {
            // Show Run state (green)
            RunStopButton.Background = s_runButtonGreenBrush;
            RunStopIcon.Text = "\u25B6"; // Triangle (play icon)
            RunStopText.Text = "Run";

            // Update glow color for hover effect
            if (RunStopButton.Template.FindName("glowEffect", RunStopButton) is DropShadowEffect glow)
            {
                glow.Color = Color.FromRgb(0x00, 0xCC, 0x66);
            }
        }
    }

    #endregion

    #region Audio Reactive Lighting

    private void InitializeAudioReactiveLighting()
    {
        try
        {
            _audioReactiveService = AudioReactiveService.Instance;
            _audioReactiveService.ValuesUpdated += OnAudioReactiveValuesUpdated;

            // Cache the RunStopButton glow effect for fast access
            if (RunStopButton.Template.FindName("glowEffect", RunStopButton) is DropShadowEffect runGlow)
            {
                _runButtonGlow = runGlow;
            }

            // Cache sidebar button glow effects
            CacheSidebarGlowEffects();

            OutputLine("[Audio Reactive] Lighting system initialized");
        }
        catch (Exception ex)
        {
            OutputLine($"[Audio Reactive] Failed to initialize: {ex.Message}");
        }
    }

    private void CacheSidebarGlowEffects()
    {
        // List of sidebar buttons to make reactive
        var sidebarButtons = new Button[]
        {
            MidiToolButton, VstToolButton, AudioToolButton,
            TrackPropertiesToolButton, OutputToolButton
        };

        _sidebarGlows.Clear();
        foreach (var button in sidebarButtons)
        {
            if (button?.Template?.FindName("glowEffect", button) is DropShadowEffect glow)
            {
                _sidebarGlows.Add(glow);
            }
            else
            {
                _sidebarGlows.Add(null);
            }
        }
    }

    private void StartAudioReactiveLighting()
    {
        try
        {
            // Start analysis service if not already running
            AnalysisService.Instance.Start();
            _audioReactiveService?.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Audio Reactive] Start failed: {ex.Message}");
        }
    }

    private void StopAudioReactiveLighting()
    {
        try
        {
            _audioReactiveService?.Stop();

            // Reset glow effects to default state
            ResetGlowEffectsToDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Audio Reactive] Stop failed: {ex.Message}");
        }
    }

    private void ResetGlowEffectsToDefault()
    {
        // Reset run button glow
        if (_runButtonGlow != null)
        {
            _runButtonGlow.BlurRadius = 8;
            _runButtonGlow.Opacity = 0.35;
        }

        // Reset sidebar glows
        foreach (var glow in _sidebarGlows)
        {
            if (glow != null)
            {
                glow.BlurRadius = 0;
                glow.Opacity = 0;
            }
        }

        // Reset visualizer background
        ResetAudioVisualizerBackground();
    }

    private void ResetAudioVisualizerBackground()
    {
        if (BassGlow != null) BassGlow.Opacity = 0;
        if (MidGlowLeft != null) MidGlowLeft.Opacity = 0;
        if (MidGlowRight != null) MidGlowRight.Opacity = 0;
        if (HighGlow != null) HighGlow.Opacity = 0;
        if (AmbientPulse != null) AmbientPulse.Opacity = 0;
    }

    /// <summary>
    /// Enables or disables the audio visualizer background.
    /// </summary>
    public void SetAudioVisualizerEnabled(bool enabled)
    {
        _audioVisualizerEnabled = enabled;
        if (!enabled)
        {
            ResetAudioVisualizerBackground();
        }
    }

    /// <summary>
    /// Sets the intensity of the audio visualizer (0.0 - 1.0).
    /// </summary>
    public void SetAudioVisualizerIntensity(float intensity)
    {
        _audioVisualizerIntensity = Math.Clamp(intensity, 0f, 0.3f); // Max 30% to keep it subtle
    }

    private void OnAudioReactiveValuesUpdated(object? sender, AudioReactiveEventArgs e)
    {
        if (!_isRunning) return;

        // Update Run Button glow based on bass + beat (pulsing effect)
        if (_runButtonGlow != null)
        {
            // Combine bass level with beat transients for punchy effect
            float intensity = Math.Max(e.Bass, e.Beat);
            _runButtonGlow.BlurRadius = 8 + (intensity * 16); // 8-24 range
            _runButtonGlow.Opacity = 0.35 + (intensity * 0.55); // 0.35-0.9 range
        }

        // Update sidebar button glows based on mid frequencies
        for (int i = 0; i < _sidebarGlows.Count; i++)
        {
            var glow = _sidebarGlows[i];
            if (glow == null) continue;

            // Stagger the sidebar buttons for wave-like effect
            float offset = i * 0.1f;
            float level = Math.Max(0, e.Mid - offset);
            level = Math.Min(1f, level * 1.5f); // Amplify slightly

            glow.BlurRadius = level * 10; // 0-10 range
            glow.Opacity = level * 0.6; // 0-0.6 range
        }

        // Update status indicator brightness based on overall level
        if (StatusIndicator != null)
        {
            var baseColor = _isRunning
                ? Color.FromRgb(0x00, 0xFF, 0x88)
                : Color.FromRgb(0x6F, 0x73, 0x7A);

            // Brighten color based on audio level
            byte r = (byte)Math.Min(255, baseColor.R + (int)(e.Overall * 50));
            byte g = (byte)Math.Min(255, baseColor.G + (int)(e.Overall * 30));
            byte b = (byte)Math.Min(255, baseColor.B + (int)(e.Overall * 30));

            _audioReactiveStatusBrush.Color = Color.FromRgb(r, g, b);
            if (StatusIndicator.Fill != _audioReactiveStatusBrush)
                StatusIndicator.Fill = _audioReactiveStatusBrush;
        }

        // Update Audio Visualizer Background
        UpdateAudioVisualizerBackground(e);
    }

    private void UpdateAudioVisualizerBackground(AudioReactiveEventArgs e)
    {
        if (!_audioVisualizerEnabled) return;

        float maxOpacity = _audioVisualizerIntensity;

        // Bass Glow (Bottom) - Purple/Blue, reacts to bass frequencies
        if (BassGlow != null)
        {
            float bassIntensity = Math.Max(e.Bass, e.Beat * 0.8f);
            BassGlow.Opacity = bassIntensity * maxOpacity;
        }

        // Mid Glow (Left/Right edges) - Cyan, reacts to mid frequencies
        if (MidGlowLeft != null)
        {
            MidGlowLeft.Opacity = e.Mid * maxOpacity * 0.8f;
        }
        if (MidGlowRight != null)
        {
            MidGlowRight.Opacity = e.Mid * maxOpacity * 0.8f;
        }

        // High Glow (Top) - White/Cyan sparkle, reacts to high frequencies
        if (HighGlow != null)
        {
            HighGlow.Opacity = e.High * maxOpacity * 0.6f;
        }

        // Ambient Pulse (Center) - Overall level pulse
        if (AmbientPulse != null)
        {
            float pulseIntensity = Math.Max(e.Overall, e.Beat * 0.5f);
            AmbientPulse.Opacity = pulseIntensity * maxOpacity;

            // Scale pulse size based on beat
            double baseSize = 600;
            double pulseSize = baseSize + (e.Beat * 400);
            AmbientPulse.Width = pulseSize;
            AmbientPulse.Height = pulseSize;

            // Center the pulse
            double canvasWidth = AudioVisualizerCanvas.ActualWidth;
            double canvasHeight = AudioVisualizerCanvas.ActualHeight;
            Canvas.SetLeft(AmbientPulse, (canvasWidth - pulseSize) / 2);
            Canvas.SetTop(AmbientPulse, (canvasHeight - pulseSize) / 2);
        }
    }

    #endregion

    #region Panel Toggle Methods

    private void ToggleProjectExplorer_Click(object sender, RoutedEventArgs e)
    {
        ProjectExplorerPanel.Visibility = ProjectExplorerMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
        LeftPanelColumn.Width = ProjectExplorerMenuItem.IsChecked
            ? new GridLength(240)
            : new GridLength(0);

        // Hide workshop panel when showing project explorer
        if (ProjectExplorerMenuItem.IsChecked)
        {
            WorkshopPanel.Visibility = Visibility.Collapsed;
            WorkshopMenuItem.IsChecked = false;
        }
    }

    private void ToggleWorkshop_Click(object sender, RoutedEventArgs e)
    {
        var showWorkshop = !WorkshopMenuItem.IsChecked;
        WorkshopMenuItem.IsChecked = showWorkshop;

        if (showWorkshop)
        {
            // Show workshop panel, hide project explorer
            WorkshopPanel.Visibility = Visibility.Visible;
            ProjectExplorerPanel.Visibility = Visibility.Collapsed;
            ProjectExplorerMenuItem.IsChecked = false;
            LeftPanelColumn.Width = new GridLength(500);
            LeftPanelColumn.MinWidth = 400;
        }
        else
        {
            // Hide workshop panel, restore project explorer
            WorkshopPanel.Visibility = Visibility.Collapsed;
            ProjectExplorerPanel.Visibility = Visibility.Visible;
            ProjectExplorerMenuItem.IsChecked = true;
            LeftPanelColumn.Width = new GridLength(240);
            LeftPanelColumn.MinWidth = 180;
        }
    }

    private void ToggleOutput_Click(object sender, RoutedEventArgs e)
    {
        _outputVisible = !_outputVisible;
        OutputMenuItem.IsChecked = _outputVisible;
        OutputPanel.Visibility = _outputVisible ? Visibility.Visible : Visibility.Collapsed;
        OutputSplitter.Visibility = _outputVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FoldAll_Click(object sender, RoutedEventArgs e)
    {
        EditorSetup.FoldAll();
    }

    private void UnfoldAll_Click(object sender, RoutedEventArgs e)
    {
        EditorSetup.UnfoldAll();
    }

    private void ToggleMinimap_Click(object sender, RoutedEventArgs e)
    {
        MinimapContainer.Visibility = MinimapMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ToggleMidiPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("midi");
        MidiPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private void ToggleVstPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("vst");
        VstPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private void ToggleAudioPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("audio");
        AudioPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private string? _currentRightPanelTab = null;

    private void ShowRightPanel(string tab)
    {
        // If panel is visible and same tab is requested, toggle it off
        if (RightPanel.Visibility == Visibility.Visible && _currentRightPanelTab == tab)
        {
            HideRightPanel();
            return;
        }

        // If panel is hidden, show it
        if (RightPanel.Visibility == Visibility.Collapsed)
        {
            RightPanel.Visibility = Visibility.Visible;
            RightSplitter.Visibility = Visibility.Visible;
            RightPanelColumn.Width = new GridLength(280);
            RightPanelColumn.MinWidth = 200;
        }

        // Switch to the requested tab
        _currentRightPanelTab = tab;
        SwitchRightPanelTab(tab);
    }

    private void HideRightPanel()
    {
        RightPanel.Visibility = Visibility.Collapsed;
        RightSplitter.Visibility = Visibility.Collapsed;
        RightPanelColumn.Width = new GridLength(0);
        RightPanelColumn.MinWidth = 0;
        _currentRightPanelTab = null;

        // Update menu checkboxes
        MidiPanelMenuItem.IsChecked = false;
        VstPanelMenuItem.IsChecked = false;
        AudioPanelMenuItem.IsChecked = false;
        UndoHistoryMenuItem.IsChecked = false;
        SynthEditorMenuItem.IsChecked = false;
        EffectsEditorMenuItem.IsChecked = false;
    }

    private static TextBlock? GetTextBlockFromTabHeader(Border header)
    {
        if (header.Child is TextBlock tb) return tb;
        if (header.Child is StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is TextBlock textBlock) return textBlock;
            }
        }
        return null;
    }

    private static System.Windows.Shapes.Path? GetPathFromTabHeader(Border header)
    {
        if (header.Child is StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is System.Windows.Shapes.Path path) return path;
            }
        }
        return null;
    }

    private void SetTabHeaderInactive(Border header)
    {
        header.Background = System.Windows.Media.Brushes.Transparent;
        var tb = GetTextBlockFromTabHeader(header);
        var path = GetPathFromTabHeader(header);
        if (tb != null) tb.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
        if (path != null) path.Stroke = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
    }

    private void SetTabHeaderActive(Border header)
    {
        header.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
        var tb = GetTextBlockFromTabHeader(header);
        var path = GetPathFromTabHeader(header);
        if (tb != null)
        {
            tb.Foreground = System.Windows.Media.Brushes.White;
            tb.FontWeight = FontWeights.SemiBold;
        }
        if (path != null) path.Stroke = System.Windows.Media.Brushes.White;
    }

    private void SwitchRightPanelTab(string tab)
    {
        // Reset all tab headers
        SetTabHeaderInactive(MidiTabHeader);
        SetTabHeaderInactive(VstTabHeader);
        SetTabHeaderInactive(AudioTabHeader);
        SetTabHeaderInactive(SynthTabHeader);
        SetTabHeaderInactive(EffectsTabHeader);
       
      

        // Hide all panels
        MidiDevicesPanel.Visibility = Visibility.Collapsed;
        VstPluginsPanel.Visibility = Visibility.Collapsed;
        AudioFilesPanel.Visibility = Visibility.Collapsed;
        TrackPropertiesPanel.Visibility = Visibility.Collapsed;
        UndoHistoryPanel.Visibility = Visibility.Collapsed;
        SynthEditorPanel.Visibility = Visibility.Collapsed;
        EffectsEditorPanel.Visibility = Visibility.Collapsed;
      
    
       

        // Show selected tab
        switch (tab)
        {
            case "midi":
                SetTabHeaderActive(MidiTabHeader);
                MidiDevicesPanel.Visibility = Visibility.Visible;
                break;
            case "vst":
                SetTabHeaderActive(VstTabHeader);
                VstPluginsPanel.Visibility = Visibility.Visible;
                break;
            case "audio":
                SetTabHeaderActive(AudioTabHeader);
                AudioFilesPanel.Visibility = Visibility.Visible;
                break;
            case "trackproperties":
                // Track properties panel is standalone (no tab header in the tabbed area)
                TrackPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "undohistory":
                // Undo history panel is standalone (no tab header in the tabbed area)
                UndoHistoryPanel.Visibility = Visibility.Visible;
                break;
            case "synth":
                SetTabHeaderActive(SynthTabHeader);
                SynthEditorPanel.Visibility = Visibility.Visible;
                break;
            case "effects":
                SetTabHeaderActive(EffectsTabHeader);
                EffectsEditorPanel.Visibility = Visibility.Visible;
                break;
           
           
        }
    }

    private void CloseRightPanel_Click(object sender, RoutedEventArgs e)
    {
        RightPanel.Visibility = Visibility.Collapsed;
        RightSplitter.Visibility = Visibility.Collapsed;
        RightPanelColumn.Width = new GridLength(0);
        RightPanelColumn.MinWidth = 0;

        MidiPanelMenuItem.IsChecked = false;
        VstPanelMenuItem.IsChecked = false;
        AudioPanelMenuItem.IsChecked = false;
        SynthEditorMenuItem.IsChecked = false;
        EffectsEditorMenuItem.IsChecked = false;
      
    }

    private void MidiTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("midi");
    }

    private void VstTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("vst");
    }

    private void AudioTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("audio");
    }

    private void SynthTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("synth");
    }

    private void ToggleSynthEditor_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("synth");
        SynthEditorMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible && _currentRightPanelTab == "synth";
    }

    private void SynthEditorPanel_CloseRequested(object? sender, EventArgs e)
    {
        HideRightPanel();
    }

    private void EffectsTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("effects");
    }

    private void ToggleEffectsEditor_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("effects");
        EffectsEditorMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible && _currentRightPanelTab == "effects";
    }

    private void EffectsEditorPanel_CloseRequested(object? sender, EventArgs e)
    {
        HideRightPanel();
    }

    private void SpatialTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("spatial");
    }

    private void PresetsTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("presets");
    }

    private void TogglePresetsPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("presets");
    }

    private void PresetBrowserPanel_PresetLoadRequested(object? sender, Models.PresetInfo preset)
    {
        // Handle preset loading - open the appropriate editor for the preset type
        if (preset.TargetType == MusicEngine.Core.PresetTargetType.Synth)
        {
            // Open synth editor and load the preset
            ShowRightPanel("synth");
            OutputLine($"Loading synth preset: {preset.Name}");
        }
        else if (preset.TargetType == MusicEngine.Core.PresetTargetType.Effect)
        {
            // Open effects editor and load the preset
            ShowRightPanel("effects");
            OutputLine($"Loading effect preset: {preset.Name}");
        }
    }



    private void AIAssistantPanel_CloseRequested(object? sender, EventArgs e)
    {
        HideRightPanel();
    }

    private void OpenPatternEditor_Click(object sender, RoutedEventArgs e)
    {
        if (_patternEditorWindow == null || !_patternEditorWindow.IsLoaded)
        {
            _patternEditorWindow = new Views.PatternEditorWindow();
            _patternEditorWindow.Owner = this;
            _patternEditorWindow.Closed += (s, args) => _patternEditorWindow = null;
        }
        _patternEditorWindow.Show();
        _patternEditorWindow.Activate();
    }

    private void ToggleMixer_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement mixer panel/window
        MessageBox.Show("Mixer feature coming soon!", "Mixer", MessageBoxButton.OK, MessageBoxImage.Information);
        MixerMenuItem.IsChecked = false;
    }

    private void OpenArrangement_Click(object sender, RoutedEventArgs e)
    {
        if (_arrangementWindow == null || !_arrangementWindow.IsLoaded)
        {
            _arrangementWindow = new Views.ArrangementWindow();
            _arrangementWindow.Owner = this;
            _arrangementWindow.Closed += (s, args) => _arrangementWindow = null;
        }
        _arrangementWindow.Show();
        _arrangementWindow.Activate();
    }

    private void OpenSessionView_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionViewWindow == null || !_sessionViewWindow.IsLoaded)
        {
            _sessionViewWindow = new SessionViewWindow();
            _sessionViewWindow.Owner = this;
            _sessionViewWindow.Closed += (s, args) => _sessionViewWindow = null;
        }
        _sessionViewWindow.Show();
        _sessionViewWindow.Activate();
    }

    #region Spatial Audio Menu Handlers

    private void OpenSpatialPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("spatial");
    }

    private void OpenSurroundPanner_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("spatial");
        // Could switch to surround tab if SpatialAudioPanel supports it
    }

    private void OpenBinauralRenderer_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("spatial");
        // Could switch to binaural tab if SpatialAudioPanel supports it
    }

    private void OpenAmbisonics_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("spatial");
        // Could switch to ambisonics tab if SpatialAudioPanel supports it
    }

    #endregion

    #region Analysis Menu Handlers

    private void OpenAnalysisPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("analysis");
    }

    private void OpenGuitarTuner_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("analysis");
        // Switch to tuner tab in AnalysisPanel
    }

    private void OpenChordDetector_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("analysis");
        // Switch to chord tab in AnalysisPanel
    }

    private void OpenKeyDetector_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("analysis");
        // Switch to key tab in AnalysisPanel
    }

    private void OpenTempoDetector_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("analysis");
        // Switch to tempo tab in AnalysisPanel
    }

    private void OpenLoopFinder_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("analysis");
        // Switch to loop tab in AnalysisPanel
    }

    #endregion

    #region Network Menu Handlers

    private void OpenNetworkPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("network");
    }

    private void OpenLinkSync_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("network");
        // Switch to Link tab in NetworkSyncPanel
    }

    private void OpenOSCControl_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("network");
        // Switch to OSC tab in NetworkSyncPanel
    }

    private void OpenNetworkMIDI_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("network");
        // Switch to Network MIDI tab in NetworkSyncPanel
    }

    private void OpenMachineControl_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("network");
        // Switch to Machine Control tab in NetworkSyncPanel
    }

    #endregion

    /// <summary>
    /// Opens the synth editor panel for a specific synth instance.
    /// </summary>
    /// <param name="synth">The synth object to edit</param>
    /// <param name="synthName">Display name of the synth</param>
    /// <param name="synthType">Type identifier (Simple, Poly, FM, etc.)</param>
    public void OpenSynthEditor(object? synth, string synthName, string synthType)
    {
        ShowRightPanel("synth");
        SynthEditorPanel.OpenSynth(synth, synthName, synthType);
        SynthEditorMenuItem.IsChecked = true;
    }

    /// <summary>
    /// Opens the synth editor panel for a synth type without a specific instance.
    /// </summary>
    /// <param name="synthType">Type identifier (Simple, Poly, FM, etc.)</param>
    public void OpenSynthEditorByType(string synthType)
    {
        ShowRightPanel("synth");
        SynthEditorPanel.OpenSynthByType(synthType);
        SynthEditorMenuItem.IsChecked = true;
    }

    private void ToggleTrackPropertiesPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("trackproperties");
    }

    private void ToggleUndoHistory_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("undohistory");
        UndoHistoryMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible && _currentRightPanelTab == "undohistory";
    }



   

    private void TrackPropertiesPanel_CloseRequested(object? sender, EventArgs e)
    {
        HideRightPanel();
    }

    private void TrackPropertiesPanel_TrackPropertyChanged(object? sender, TrackPropertyChangedEventArgs e)
    {
        // Handle track property changes - update any connected views
        OutputLine($"Track '{e.Track.Name}' property '{e.PropertyName}' changed: {e.OldValue} -> {e.NewValue}");
    }

    private void TrackPropertiesPanel_TrackDuplicateRequested(object? sender, TrackEventArgs e)
    {
        // Create a duplicate of the track
        var duplicate = e.Track.Duplicate();

        // Find the index of the original track and insert the duplicate after it
        int originalIndex = -1;
        for (int i = 0; i < Tracks.Count; i++)
        {
            if (Tracks[i].Id == e.Track.Id)
            {
                originalIndex = i;
                break;
            }
        }

        // Add the duplicate track to the track list
        if (originalIndex >= 0 && originalIndex < Tracks.Count - 1)
        {
            Tracks.Insert(originalIndex + 1, duplicate);
        }
        else
        {
            Tracks.Add(duplicate);
        }

        // Update the duplicate's order property
        duplicate.Order = originalIndex + 1;

        // Update order for all subsequent tracks
        for (int i = duplicate.Order + 1; i < Tracks.Count; i++)
        {
            Tracks[i].Order = i;
        }

        OutputLine($"Duplicated track '{e.Track.Name}' -> '{duplicate.Name}'");

        // Select the duplicate in the properties panel
        TrackPropertiesPanel.SelectedTrack = duplicate;
        StatusText.Text = $"Track '{e.Track.Name}' duplicated";
    }

    private void TrackPropertiesPanel_TrackDeleteRequested(object? sender, TrackEventArgs e)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete track '{e.Track.Name}'?",
            "Delete Track",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Check if the track is frozen and clean up frozen data
            if (e.Track.IsFrozen && _frozenTrackData.TryGetValue(e.Track.Id, out var freezeData))
            {
                // Delete the frozen audio file if it exists
                if (!string.IsNullOrEmpty(freezeData.FrozenAudioFilePath) && File.Exists(freezeData.FrozenAudioFilePath))
                {
                    try
                    {
                        File.Delete(freezeData.FrozenAudioFilePath);
                        OutputLine($"Deleted frozen audio file: {freezeData.FrozenAudioFilePath}");
                    }
                    catch (Exception ex)
                    {
                        OutputLine($"Warning: Could not delete frozen audio file: {ex.Message}");
                    }
                }

                _frozenTrackData.Remove(e.Track.Id);
            }

            // Remove the track from the tracks collection
            TrackInfo? trackToRemove = null;
            foreach (var track in Tracks)
            {
                if (track.Id == e.Track.Id)
                {
                    trackToRemove = track;
                    break;
                }
            }

            if (trackToRemove != null)
            {
                Tracks.Remove(trackToRemove);

                // Update order for remaining tracks
                for (int i = 0; i < Tracks.Count; i++)
                {
                    Tracks[i].Order = i;
                }
            }

            OutputLine($"Deleted track: {e.Track.Name}");
            TrackPropertiesPanel.ClearSelection();
            StatusText.Text = $"Track '{e.Track.Name}' deleted";
        }
    }

    private async void TrackPropertiesPanel_TrackFreezeRequested(object? sender, TrackEventArgs e)
    {
        // Note: IsFrozen is toggled before this event is raised, so:
        // - IsFrozen == true means we need to freeze (track was just set to frozen)
        // - IsFrozen == false means we need to unfreeze (track was just set to unfrozen)
        if (e.Track.IsFrozen)
        {
            await FreezeTrackAsync(e.Track);
        }
        else
        {
            UnfreezeTrack(e.Track);
        }
    }

    /// <summary>
    /// Freezes a track by rendering it to an audio file and storing the original state.
    /// </summary>
    /// <param name="track">The track to freeze.</param>
    private async Task FreezeTrackAsync(TrackInfo track)
    {
        OutputLine($"Freezing track: {track.Name}...");
        StatusText.Text = $"Freezing track '{track.Name}'...";

        try
        {
            // Create freeze data directory if it doesn't exist
            var frozenTracksDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicEngineEditor",
                "FrozenTracks");

            if (!Directory.Exists(frozenTracksDir))
            {
                Directory.CreateDirectory(frozenTracksDir);
            }

            // Generate a unique filename for the frozen audio
            var frozenFileName = $"frozen_{track.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            var frozenFilePath = Path.Combine(frozenTracksDir, frozenFileName);

            // Store the original track data for unfreezing
            var freezeData = new FreezeTrackData
            {
                TrackId = track.Id,
                OriginalName = track.Name,
                OriginalInstrumentName = track.InstrumentName,
                OriginalInstrumentPath = track.InstrumentPath,
                OriginalTrackType = track.TrackType,
                FrozenAudioFilePath = frozenFilePath,
                FrozenAt = DateTime.Now
            };

            // Simulate freeze operation (render track to audio)
            // In a full implementation, this would use FreezeManager from MusicEngine.Core.Freeze
            await Task.Run(async () =>
            {
                // Simulate rendering time
                await Task.Delay(500);

                // In a real implementation, we would:
                // 1. Get the track's pattern from the sequencer
                // 2. Use TrackRenderer to render the pattern to audio
                // 3. Save the rendered audio to the file path
                // 4. Store the freeze data

                // For now, create an empty placeholder file to indicate the track is frozen
                File.WriteAllText(frozenFilePath + ".freeze", $"Frozen track: {track.Name}\nFrozen at: {freezeData.FrozenAt}");
            });

            // Calculate duration (placeholder - would come from actual rendered audio)
            freezeData.DurationSeconds = 30.0; // Placeholder duration

            // Store the freeze data
            _frozenTrackData[track.Id] = freezeData;

            // Update the track display to indicate it's frozen
            track.Name = $"[Frozen] {freezeData.OriginalName}";

            OutputLine($"Track '{freezeData.OriginalName}' frozen successfully");
            OutputLine($"  Frozen audio path: {frozenFilePath}");
            StatusText.Text = $"Track '{freezeData.OriginalName}' frozen";
        }
        catch (Exception ex)
        {
            OutputLine($"Error freezing track: {ex.Message}");
            StatusText.Text = $"Failed to freeze track '{track.Name}'";

            // Revert the frozen state on error
            track.IsFrozen = false;
        }
    }

    /// <summary>
    /// Unfreezes a track by restoring its original state.
    /// </summary>
    /// <param name="track">The track to unfreeze.</param>
    private void UnfreezeTrack(TrackInfo track)
    {
        OutputLine($"Unfreezing track: {track.Name}...");
        StatusText.Text = $"Unfreezing track '{track.Name}'...";

        try
        {
            // Check if we have freeze data for this track
            if (!_frozenTrackData.TryGetValue(track.Id, out var freezeData))
            {
                OutputLine($"Warning: No freeze data found for track {track.Id}. Resetting frozen state.");
                track.IsFrozen = false;
                StatusText.Text = $"Track unfrozen (no previous state to restore)";
                return;
            }

            // Delete the frozen audio file if it exists
            if (!string.IsNullOrEmpty(freezeData.FrozenAudioFilePath))
            {
                // Delete the actual audio file
                if (File.Exists(freezeData.FrozenAudioFilePath))
                {
                    File.Delete(freezeData.FrozenAudioFilePath);
                }

                // Delete the freeze metadata file
                var freezeMetaFile = freezeData.FrozenAudioFilePath + ".freeze";
                if (File.Exists(freezeMetaFile))
                {
                    File.Delete(freezeMetaFile);
                }
            }

            // Restore original track name
            track.Name = freezeData.OriginalName;

            // Restore original instrument info
            track.InstrumentName = freezeData.OriginalInstrumentName;
            track.InstrumentPath = freezeData.OriginalInstrumentPath;

            // Remove the freeze data
            _frozenTrackData.Remove(track.Id);

            OutputLine($"Track '{freezeData.OriginalName}' unfrozen successfully");
            StatusText.Text = $"Track '{freezeData.OriginalName}' unfrozen";
        }
        catch (Exception ex)
        {
            OutputLine($"Error unfreezing track: {ex.Message}");
            StatusText.Text = $"Failed to unfreeze track '{track.Name}'";

            // Keep the track frozen on error
            track.IsFrozen = true;
        }
    }

    #endregion

    #region Right Panel Data Methods

    private void RefreshMidiDevices_Click(object sender, RoutedEventArgs e)
    {
        RefreshMidiDevices();
    }

    private void RefreshMidiDevices()
    {
        MidiDevices.Clear();

        try
        {
            // Get MIDI input devices from engine
            var inputCount = _engineService.GetMidiInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                var deviceName = _engineService.GetMidiInputName(i);
                MidiDevices.Add(new MidiDeviceInfo
                {
                    Name = deviceName,
                    Type = "Input",
                    DeviceIndex = i,
                    ChannelInfo = "Ch 1-16"  // MIDI inputs typically receive on all channels
                });
            }

            // Get MIDI output devices from engine
            var outputCount = _engineService.GetMidiOutputCount();
            for (int i = 0; i < outputCount; i++)
            {
                var deviceName = _engineService.GetMidiOutputName(i);
                MidiDevices.Add(new MidiDeviceInfo
                {
                    Name = deviceName,
                    Type = "Output",
                    DeviceIndex = i,
                    ChannelInfo = "Ch 1-16"  // MIDI outputs can send on all channels
                });
            }

            // Show message if no devices found
            if (MidiDevices.Count == 0)
            {
                OutputLine("No MIDI devices found. Connect a MIDI device and click Refresh.");
            }
            else
            {
                OutputLine($"Found {inputCount} MIDI input(s) and {outputCount} MIDI output(s).");
            }
        }
        catch (Exception ex)
        {
            // If engine methods don't exist yet or error occurs, add placeholder
            MidiDevices.Add(new MidiDeviceInfo
            {
                Name = "No devices found",
                Type = "-",
                DeviceIndex = -1,
                ChannelInfo = "-"
            });
            OutputLine($"Error enumerating MIDI devices: {ex.Message}");
        }
    }

    private async void ScanVstPlugins_Click(object sender, RoutedEventArgs e)
    {
        // The new VstPluginPanel handles scanning internally
        await VstPluginsPanel.ScanPluginsAsync();
    }

    // VstPluginPanel Event Handlers
    private void VstPluginsPanel_OnOpenPluginEditor(object? sender, VstPluginEventArgs e)
    {
        OpenVstPluginWindow(e.Plugin.Name, e.Plugin.Name);
    }

    private void VstPluginsPanel_OnPluginDoubleClick(object? sender, VstPluginEventArgs e)
    {
        OpenVstPluginWindow(e.Plugin.Name, e.Plugin.Name);
    }

    private void VstPluginsPanel_OnScanCompleted(object? sender, VstScanCompletedEventArgs e)
    {
        OutputLine($"VST scan completed: Found {e.PluginCount} plugins");
    }

    private void UpdateAudioFilesList()
    {
        AudioFiles.Clear();

        if (_currentProject == null) return;

        foreach (var asset in _currentProject.AudioAssets)
        {
            AudioFiles.Add(new AudioFileInfo
            {
                Alias = asset.Alias,
                Duration = "0:00", // Would need to read from file
                Format = Path.GetExtension(asset.FileName).TrimStart('.')
            });
        }
    }

    private void AudioFile_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AudioFilesList.SelectedItem is AudioFileInfo audio)
        {
            // Insert code to load this audio file
            var code = $"var {audio.Alias} = LoadAudio(\"{audio.Alias}\");";
            CodeEditor.Document.Insert(CodeEditor.CaretOffset, code);
        }
    }

    private void AudioFilesPanel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var audioExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac", ".aiff" };

            bool hasAudioFile = files.Any(f =>
                audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            e.Effects = hasAudioFile ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void AudioFilesPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var audioExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac", ".aiff" };

            foreach (var file in files)
            {
                if (audioExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    var alias = Path.GetFileNameWithoutExtension(file);
                    _ = _projectService.ImportAudioAsync(_currentProject, file, alias);
                    OutputLine($"Imported audio: {alias}");
                }
            }

            UpdateProjectExplorer();
            UpdateAudioFilesList();
        }
    }

    #endregion

    #region Menu Handlers

    private void AddScript_Click(object sender, RoutedEventArgs e)
    {
        NewFile_Click(sender, e);
    }

    private void ImportAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.wav;*.mp3;*.ogg;*.flac)|*.wav;*.mp3;*.ogg;*.flac|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                var alias = Path.GetFileNameWithoutExtension(file);
                _ = _projectService.ImportAudioAsync(_currentProject, file, alias);
                OutputLine($"Imported audio: {alias}");
            }

            UpdateProjectExplorer();
            UpdateAudioFilesList();
        }
    }

    private void AddReference_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Project references are not yet implemented.", "Coming Soon",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ProjectSettings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Project settings dialog is not yet implemented.", "Coming Soon",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsService = App.Services.GetRequiredService<ISettingsService>();
        var dialog = new SettingsDialog(settingsService)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            StatusText.Text = "Settings saved";
            OutputLine("Settings saved successfully.");
        }
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        FindReplaceBar.ShowFind();
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        FindReplaceBar.ShowReplace();
    }

    private void ClearOutput_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Document.Blocks.Clear();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Documentation_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/watermann420/MusicEngine",
            UseShellExecute = true
        });
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "MusicEngine Editor v1.0\n\n" +
            "A professional IDE for MusicEngine live coding.\n\n" +
            "Shortcuts:\n" +
            "  Ctrl+Enter - Run script\n" +
            "  Escape - Stop / All notes off\n" +
            "  Ctrl+S - Save\n" +
            "  Ctrl+Shift+S - Save All\n" +
            "  Ctrl+Shift+N - New Project\n" +
            "  Ctrl+Shift+O - Open Project",
            "About MusicEngine Editor",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    #endregion

    #region Toolbar Handlers

    // Undo/Redo
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (CodeEditor.CanUndo)
            CodeEditor.Undo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (CodeEditor.CanRedo)
            CodeEditor.Redo();
    }

    // Transport Controls
    private void Transport_Stop_Click(object sender, RoutedEventArgs e)
    {
        StopExecution();
    }

    #endregion

    #region Project Browser Tabs

    private enum ProjectBrowserTab { Files, Presets, Samples }
    private ProjectBrowserTab _activeBrowserTab = ProjectBrowserTab.Files;

    private void FilesTab_Click(object sender, MouseButtonEventArgs e)
    {
        SetProjectBrowserTab(ProjectBrowserTab.Files);
    }

    private void LeftPresetsTab_Click(object sender, MouseButtonEventArgs e)
    {
        SetProjectBrowserTab(ProjectBrowserTab.Presets);
    }

    private void SamplesTab_Click(object sender, MouseButtonEventArgs e)
    {
        SetProjectBrowserTab(ProjectBrowserTab.Samples);
    }

    private void SetProjectBrowserTab(ProjectBrowserTab tab)
    {
        _activeBrowserTab = tab;

        // Update tab visuals
        SetBrowserTabActive(FilesTabHeader, tab == ProjectBrowserTab.Files);
       

        // Update panel visibility
        FilesPanel.Visibility = tab == ProjectBrowserTab.Files ? Visibility.Visible : Visibility.Collapsed;
        PresetsPanel.Visibility = tab == ProjectBrowserTab.Presets ? Visibility.Visible : Visibility.Collapsed;
        SamplesPanel.Visibility = tab == ProjectBrowserTab.Samples ? Visibility.Visible : Visibility.Collapsed;

        // Load content if needed
        if (tab == ProjectBrowserTab.Presets && PresetsTree.Items.Count == 0)
            LoadPresetsTree();
        else if (tab == ProjectBrowserTab.Samples && SamplesTree.Items.Count == 0)
            LoadSamplesTree();
    }

    private void SetBrowserTabActive(Border header, bool active)
    {
        header.BorderBrush = active ? FindResource("AccentBrush") as Brush ?? Brushes.Transparent : Brushes.Transparent;
        var stack = header.Child as StackPanel;
        if (stack != null)
        {
            foreach (var child in stack.Children)
            {
                if (child is System.Windows.Shapes.Path path)
                    path.Stroke = active ? FindResource("AccentBrush") as Brush ?? Brushes.Transparent : FindResource("SecondaryForegroundBrush") as Brush ?? Brushes.Transparent;
                else if (child is TextBlock text)
                    text.Foreground = active ? FindResource("BrightForegroundBrush") as Brush ?? Brushes.Transparent : FindResource("SecondaryForegroundBrush") as Brush ?? Brushes.Transparent;
            }
        }
    }

    private void LoadPresetsTree()
    {
        PresetsTree.Items.Clear();
        var root = new TreeViewItem { Header = "Presets", IsExpanded = true };

        var synths = new TreeViewItem { Header = "Synths", IsExpanded = true };
        synths.Items.Add(new TreeViewItem { Header = "Bass Synth.preset" });
        synths.Items.Add(new TreeViewItem { Header = "Lead Pad.preset" });
        synths.Items.Add(new TreeViewItem { Header = "Pluck.preset" });
        root.Items.Add(synths);

        var effects = new TreeViewItem { Header = "Effects", IsExpanded = true };
        effects.Items.Add(new TreeViewItem { Header = "Reverb Hall.preset" });
        effects.Items.Add(new TreeViewItem { Header = "Delay Ping Pong.preset" });
        root.Items.Add(effects);

        PresetsTree.Items.Add(root);
    }

    private void LoadSamplesTree()
    {
        SamplesTree.Items.Clear();
        var root = new TreeViewItem { Header = "Samples", IsExpanded = true };

        var drums = new TreeViewItem { Header = "Drums", IsExpanded = true };
        drums.Items.Add(new TreeViewItem { Header = "kick_808.wav" });
        drums.Items.Add(new TreeViewItem { Header = "snare_punchy.wav" });
        drums.Items.Add(new TreeViewItem { Header = "hihat_closed.wav" });
        root.Items.Add(drums);

        var loops = new TreeViewItem { Header = "Loops", IsExpanded = true };
        loops.Items.Add(new TreeViewItem { Header = "bass_loop_120bpm.wav" });
        loops.Items.Add(new TreeViewItem { Header = "pad_ambient.wav" });
        root.Items.Add(loops);

        SamplesTree.Items.Add(root);
    }

    private void PresetsTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PresetsTree.SelectedItem is TreeViewItem item && item.Items.Count == 0)
        {
            OutputLine($"[Preset] Loading: {item.Header}");
        }
    }

    private void SamplesTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SamplesTree.SelectedItem is TreeViewItem item && item.Items.Count == 0)
        {
            OutputLine($"[Sample] Loading: {item.Header}");
        }
    }

    private void RefreshProjectTree_Click(object sender, RoutedEventArgs e)
    {
        UpdateProjectExplorer();
        OutputLine("[Project] Refreshed");
    }

    #endregion

    #region Output Filters

    private bool _filterInfo = true;
    private bool _filterWarning = true;
    private bool _filterError = true;
    private string _outputSearchText = "";

    private void FilterInfo_Click(object sender, RoutedEventArgs e)
    {
    
    }

    private void FilterWarning_Click(object sender, RoutedEventArgs e)
    {
        
    }

    private void FilterError_Click(object sender, RoutedEventArgs e)
    {
        
    }

    private void OutputSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _outputSearchText = OutputSearchBox.Text;
        ApplyOutputFilters();
    }

    private void ApplyOutputFilters()
    {
        // Note: Full filter implementation would require storing messages in a list
        // and re-rendering. For now, this logs the filter state.
        var filters = new List<string>();
        if (_filterInfo) filters.Add("Info");
        if (_filterWarning) filters.Add("Warning");
        if (_filterError) filters.Add("Error");

        // Apply search filter visual feedback
        if (!string.IsNullOrEmpty(_outputSearchText))
        {
            StatusText.Text = $"Filtering: {_outputSearchText}";
        }
    }

    #endregion

    #region Playback Time and MIDI Activity

    private DateTime _playbackStartTime;
    private DispatcherTimer? _playbackTimeTimer;

    private void StartPlaybackTimeTracking()
    {
        _playbackStartTime = DateTime.Now;
        _playbackTimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _playbackTimeTimer.Tick += PlaybackTimeTimer_Tick;
        _playbackTimeTimer.Start();
    }

    private void StopPlaybackTimeTracking()
    {
        _playbackTimeTimer?.Stop();
        _playbackTimeTimer = null;
    }

    private void PlaybackTimeTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _playbackStartTime;
        PlaybackTimeDisplay.Text = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
    }

    private DateTime _lastMidiActivity = DateTime.MinValue;

    
    #endregion

    #region Custom Title Bar

    private bool _isMaximized = true; // Start maximized
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private Point _windowStartPosition;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to toggle maximize
            ToggleMaximize();
        }
        else
        {
            // Start drag
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            _windowStartPosition = new Point(Left, Top);
            ((UIElement)sender).CaptureMouse();
        }
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            // If maximized and dragging, restore first
            if (_isMaximized)
            {
                // Calculate proportional position
                var mousePos = e.GetPosition(this);
                var screenPos = PointToScreen(mousePos);

                _isMaximized = false;
                WindowState = System.Windows.WindowState.Normal;

                // Position window so the mouse is still over it proportionally
                Left = screenPos.X - (Width / 2);
                Top = screenPos.Y - 16; // Center of title bar

                _dragStartPoint = new Point(Width / 2, 16);
            }
            else
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _dragStartPoint;
                Left = _windowStartPosition.X + delta.X;
                Top = _windowStartPosition.Y + delta.Y;
                _windowStartPosition = new Point(Left, Top);
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = System.Windows.WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        if (_isMaximized)
        {
            WindowState = System.Windows.WindowState.Normal;
            _isMaximized = false;
        }
        else
        {
            WindowState = System.Windows.WindowState.Maximized;
            _isMaximized = true;
        }
    }

    // Update maximize button icon when window state changes
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        _isMaximized = WindowState == System.Windows.WindowState.Maximized;
    }

    #endregion

    #region Helpers

    private void OutputLine(string text)
    {
        Dispatcher.Invoke(() =>
        {
            AppendColoredOutput(text);
        });
    }

    /// <summary>
    /// Sets the status text with appropriate color based on state.
    /// Running = green (#00CC66), Stopped = gray (#808080), Ready = default
    /// </summary>
    private void SetStatusText(string text)
    {
        StatusText.Text = text;

        if (text == "Running" || text.StartsWith("Running ("))
        {
            StatusText.Foreground = s_statusTextGreenBrush; // Green
        }
        else if (text == "Stopped")
        {
            StatusText.Foreground = s_statusTextGrayBrush; // Gray
        }
        else if (text == "Ready")
        {
            StatusText.Foreground = FindResource("ForegroundBrush") as Brush ?? Brushes.Transparent; // Default
        }
        else
        {
            StatusText.Foreground = FindResource("ForegroundBrush") as Brush ?? Brushes.Transparent; // Default for other messages
        }
    }

    /// <summary>
    /// Determines the log level from the text and returns the appropriate brush.
    /// </summary>
    private SolidColorBrush GetLogLevelBrush(string text)
    {
        var lowerText = text.ToLowerInvariant();

        // Check for error indicators
        if (lowerText.Contains("[error]") ||
            lowerText.Contains("error:") ||
            lowerText.Contains("exception") ||
            lowerText.Contains("failed") ||
            lowerText.StartsWith("error"))
        {
            return s_logErrorBrush; // #FF4757 - Red
        }

        // Check for warning indicators
        if (lowerText.Contains("[warning]") ||
            lowerText.Contains("[warn]") ||
            lowerText.Contains("warning:") ||
            lowerText.Contains("warn:"))
        {
            return s_logWarningBrush; // #FFB800 - Yellow
        }

        // Check for debug/trace indicators
        if (lowerText.Contains("[debug]") ||
            lowerText.Contains("[trace]") ||
            lowerText.Contains("debug:") ||
            lowerText.Contains("trace:"))
        {
            return s_logDebugBrush; // #808080 - Gray
        }

        // Default to white/info color
        return s_logDefaultBrush; // #E0E0E0 - White/Light gray
    }

    /// <summary>
    /// Appends colored text to the output RichTextBox based on log level.
    /// </summary>
    private void AppendColoredOutput(string text)
    {
        var brush = GetLogLevelBrush(text);
        var timestamp = DateTime.Now.ToString("[HH:mm:ss] ");

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 1
        };

        // Add timestamp in gray
        var timestampRun = new Run(timestamp)
        {
            Foreground = s_timestampBrush // Dark gray timestamp
        };
        paragraph.Inlines.Add(timestampRun);

        // Add main message with appropriate color
        var run = new Run(text)
        {
            Foreground = brush
        };
        paragraph.Inlines.Add(run);

        OutputBox.Document.Blocks.Add(paragraph);
        OutputBox.ScrollToEnd();
    }

    private static string GetDefaultScript()
    {
        var path = ResolveTestScriptPath();
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        // Fallback to built-in sample
        return """
// MusicEngine Script
// Press Ctrl+Enter to execute, Escape to stop

// Set BPM and start the sequencer
Sequencer.Bpm = 120;
Sequencer.Start();

// Create a simple synth with sawtooth waveform
var synth = CreateSynth();
synth.SetParameter("waveform", 2);  // 0=Sine, 1=Square, 2=Sawtooth, 3=Triangle, 4=Noise
synth.SetParameter("cutoff", 0.6f);

// === TEST: Play a chord directly (no MIDI keyboard needed) ===
Print("Playing test chord...");
synth.NoteOn(60, 100);  // C4 (Middle C)
synth.NoteOn(64, 100);  // E4
synth.NoteOn(67, 100);  // G4

Print("You should hear a C major chord now!");
Print("Press Escape to stop all notes.");
Print("");

// === MIDI Setup (only works if you have a MIDI keyboard connected) ===
// Check the MIDI panel on the right for available devices
// Engine.RouteMidiInput(0, synth);
// Engine.MapRange(0, 21, 108, synth, false);
// Print("MIDI keyboard routed to synth.");

// Or load a VST plugin:
// var vital = vst.load("Vital");
// vital?.from(0);
""";
    }

    private static string ResolveTestScriptPath()
    {
        // 1) Direct relative to solution layout (MusicEditor/../MusicEngine/test_script.csx)
        var candidateDirect = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "MusicEngine", "test_script.csx"));
        if (File.Exists(candidateDirect)) return candidateDirect;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "MusicEngine", "test_script.csx");
            if (File.Exists(candidate)) return candidate;

            var sibling = Path.Combine(dir.FullName, "..", "MusicEngine", "test_script.csx");
            if (File.Exists(sibling)) return Path.GetFullPath(sibling);

            dir = dir.Parent;
        }

        // fallback: typical dev path
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fallback = Path.Combine(userProfile, "RiderProjects", "MusicEngine", "test_script.csx");
        return File.Exists(fallback) ? fallback : string.Empty;
    }

    #endregion

    #region Output/Problems Panel

    private void OutputTab_Click(object sender, MouseButtonEventArgs e)
    {
        SetTab(OutputTab.Output);
    }

    private void ProblemsTab_Click(object sender, MouseButtonEventArgs e)
    {
        SetTab(OutputTab.Errors);
    }

    private void ConsoleTab_Click(object sender, MouseButtonEventArgs e)
    {
        SetTab(OutputTab.Console);
    }

    private async void UserConsoleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            var text = UserConsoleBox.Text;
            var lastLine = text.Split('\n').LastOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(lastLine)) return;

            AppendConsole($"> {lastLine}");
            _consoleHistory.Add(lastLine);
            _consoleHistoryIndex = _consoleHistory.Count;

            try
            {
                var result = await _engineService.ExecuteScriptAsync(lastLine);
                if (!string.IsNullOrEmpty(result.Output))
                {
                    AppendConsole(result.Output.Trim());
                }
                if (result.Errors != null && result.Errors.Count > 0)
                {
                    foreach (var err in result.Errors)
                    {
                        Problems.Add(new ProblemItem
                        {
                            Severity = ProblemSeverity.Error,
                            Message = err.Message,
                            FileName = "Console",
                            FilePath = "",
                            Line = err.Line,
                            Column = err.Column,
                            Suggestion = SuggestFor(err.Message)
                        });
                    }
                    UpdateErrorBadge();
                    SetTab(OutputTab.Errors);
                }
            }
            catch (Exception ex)
            {
                Problems.Add(new ProblemItem
                {
                    Severity = ProblemSeverity.Error,
                    Message = ex.Message,
                    FileName = "Console",
                    FilePath = "",
                    Line = 1,
                    Column = 1,
                    Suggestion = SuggestFor(ex.Message)
                });
                UpdateErrorBadge();
                SetTab(OutputTab.Errors);
            }

            UserConsoleBox.AppendText(Environment.NewLine);
            UserConsoleBox.ScrollToEnd();
        }
        else if (e.Key == Key.Up)
        {
            if (_consoleHistory.Count == 0) return;
            _consoleHistoryIndex = Math.Max(0, _consoleHistoryIndex - 1);
            ReplaceCurrentConsoleLine(_consoleHistory[_consoleHistoryIndex]);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (_consoleHistory.Count == 0) return;
            _consoleHistoryIndex = Math.Min(_consoleHistory.Count, _consoleHistoryIndex + 1);
            ReplaceCurrentConsoleLine(_consoleHistoryIndex == _consoleHistory.Count ? "" : _consoleHistory[_consoleHistoryIndex]);
            e.Handled = true;
        }
    }

    private void ReplaceCurrentConsoleLine(string newText)
    {
        var text = UserConsoleBox.Text;
        var lines = text.Split('\n').ToList();
        if (lines.Count == 0) { lines.Add(""); }
        lines[lines.Count - 1] = newText;
        UserConsoleBox.Text = string.Join("\n", lines);
        UserConsoleBox.CaretIndex = UserConsoleBox.Text.Length;
    }

    private void AppendConsole(string line)
    {
        UserConsoleBox.AppendText(line + Environment.NewLine);
        UserConsoleBox.ScrollToEnd();
    }

    private void SetTab(OutputTab tab)
    {
        _activeTab = tab;
        // reset styles
        SetTabHeaderInactive(OutputTabHeader);
        SetTabHeaderInactive(ConsoleTabHeader);
    

        OutputBox.Visibility = Visibility.Collapsed;
        UserConsoleBox.Visibility = Visibility.Collapsed;
        ProblemsListView.Visibility = Visibility.Collapsed;

        switch (tab)
        {
            case OutputTab.Output:
                SetTabHeaderActive(OutputTabHeader);
                OutputBox.Visibility = Visibility.Visible;
                break;
            case OutputTab.Console:
                SetTabHeaderActive(ConsoleTabHeader);
                UserConsoleBox.Visibility = Visibility.Visible;
                break;
            
        }
    }

    private void SwitchOutputTab(bool showOutput)
    {
        SetTab(showOutput ? OutputTab.Output : OutputTab.Errors);
    }

    private void ProblemsListView_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProblemsListView.SelectedItem is ProblemItem problem)
        {
            // Navigate to the error location
            NavigateToError(problem);
        }
    }

    private void NavigateToError(ProblemItem problem)
    {
        // If the file is open in a tab, switch to it
        if (!string.IsNullOrEmpty(problem.FilePath) && _openTabs.TryGetValue(problem.FilePath, out var tab))
        {
            EditorTabs.SelectedItem = tab;
        }

        // Navigate to the line and column
        try
        {
            var line = Math.Max(1, problem.Line);
            var column = Math.Max(1, problem.Column);

            if (line <= CodeEditor.Document.LineCount)
            {
                var offset = CodeEditor.Document.GetOffset(line, column);
                CodeEditor.CaretOffset = offset;
                CodeEditor.ScrollToLine(line);
                CodeEditor.TextArea.Focus();

                // Select the line for visibility
                var lineInfo = CodeEditor.Document.GetLineByNumber(line);
                CodeEditor.Select(lineInfo.Offset, lineInfo.Length);
            }
        }
        catch (Exception ex)
        {
            OutputLine($"Could not navigate to error: {ex.Message}");
        }
    }

    #endregion

    #region VST Plugin Windows

    public void OpenVstPluginWindow(string pluginName, string variableName)
    {
        var key = $"{variableName}_{pluginName}";

        if (_vstWindows.TryGetValue(key, out var existingWindow))
        {
            // Window already exists, show it
            existingWindow.ShowWindow();
        }
        else
        {
            // Create new VST window
            var window = new VstPluginWindow(pluginName, variableName, null)
            {
                Owner = this
            };

            _vstWindows[key] = window;

            // Remove from dictionary when force-closed
            window.Closed += (s, e) =>
            {
                if (!window.KeepRunning)
                {
                    _vstWindows.Remove(key);
                }
            };

            window.Show();
            OutputLine($"Opened VST plugin window: {pluginName} (variable: {variableName})");
        }
    }

    public void CloseAllVstWindows()
    {
        foreach (var window in _vstWindows.Values.ToList())
        {
            window.ForceClose();
        }
        _vstWindows.Clear();
    }

    #endregion

    #region Active Instruments Animation

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        foreach (var instrument in ActiveInstruments)
        {
            instrument.UpdateAnimation();
        }
    }

    public void AddActiveInstrument(string name, string type)
    {
        Dispatcher.Invoke(() =>
        {
            // Check if already exists
            var existing = ActiveInstruments.FirstOrDefault(i => i.Name == name);
            if (existing == null)
            {
                ActiveInstruments.Add(new ActiveInstrumentInfo
                {
                    Name = name,
                    InstrumentType = type,
                    IsActive = true
                });
                UpdateNoInstrumentsVisibility();
            }
        });
    }

    public void RemoveActiveInstrument(string name)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == name);
            if (instrument != null)
            {
                ActiveInstruments.Remove(instrument);
                UpdateNoInstrumentsVisibility();
            }
        });
    }

    public void TriggerNoteOn(string instrumentName, int note, int velocity)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == instrumentName);
            if (instrument != null)
            {
                instrument.TriggerNote(note, velocity);
            }
        });
    }

    public void TriggerNoteOff(string instrumentName, int note)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == instrumentName);
            if (instrument != null)
            {
                instrument.ReleaseNote(note);
            }
        });
    }

    public void ClearActiveInstruments()
    {
        Dispatcher.Invoke(() =>
        {
            ActiveInstruments.Clear();
            UpdateNoInstrumentsVisibility();
        });
    }

    private void UpdateNoInstrumentsVisibility()
    {
        // Active instruments display has been removed from toolbar
    }

    #endregion

    #region Workshop Panel Event Handlers

    private async void WorkshopPanel_OnRunCode(object? sender, WorkshopCodeEventArgs e)
    {
        // Execute the code example from the workshop via the EngineService
        if (string.IsNullOrWhiteSpace(e.Code))
        {
            OutputLine("No code to execute.");
            return;
        }

        _isRunning = true;
        StatusText.Text = "Executing workshop example...";
        UpdateRunStopButton();

        // Clear previous problems
        Problems.Clear();
        UpdateErrorBadge();

        OutputLine("----------------------------------------");
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Running workshop example...");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _engineService.ExecuteScriptAsync(e.Code);
            stopwatch.Stop();

            if (result.Success)
            {
                StatusText.Text = $"Running ({stopwatch.ElapsedMilliseconds}ms)";
                OutputLine($"Workshop example executed successfully ({stopwatch.ElapsedMilliseconds}ms)");

                if (!string.IsNullOrEmpty(result.Output))
                {
                    OutputLine(result.Output);
                }

                // Parse code to extract instruments and start animation
                ExtractInstrumentsFromCode(e.Code);
                _animationTimer.Start();
            }
            else
            {
                _isRunning = false;
                UpdateRunStopButton();
                StatusText.Text = "Workshop example error";

                foreach (var error in result.Errors)
                {
                    // Add to Problems panel (no console spam)
                    Problems.Add(new ProblemItem
                    {
                        Severity = error.Severity == "Error" ? ProblemSeverity.Error : ProblemSeverity.Warning,
                        Message = error.Message,
                        FileName = "Workshop Example",
                        FilePath = "",
                        Line = error.Line,
                        Column = error.Column,
                        Suggestion = SuggestFor(error.Message)
                    });
                }

                UpdateErrorBadge();

                // Switch to Problems tab if there are errors
                if (Problems.Count > 0)
                {
                    SwitchOutputTab(false);
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _isRunning = false;
            StatusText.Text = "Workshop execution failed";

            // Add exception to Problems
            Problems.Add(new ProblemItem
            {
                Severity = ProblemSeverity.Error,
                Message = ex.Message,
                FileName = "Workshop Example",
                FilePath = "",
                Line = 1,
                Column = 1,
                Suggestion = SuggestFor(ex.Message)
            });
            UpdateErrorBadge();
        }

        UpdateRunStopButton();
    }

    private void WorkshopPanel_OnCopyCode(object? sender, WorkshopCodeEventArgs e)
    {
        // Code is already copied to clipboard by the WorkshopPanel itself
        // Just show feedback in the output
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Code copied to clipboard.");
        StatusText.Text = "Code copied to clipboard";
    }

    private void WorkshopPanel_OnInsertCode(object? sender, WorkshopCodeEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Code))
        {
            return;
        }

        // Insert code at the current cursor position in the editor
        // If there's a selection, replace it; otherwise, insert at cursor
        var textArea = CodeEditor.TextArea;
        var document = CodeEditor.Document;

        if (textArea.Selection.Length > 0)
        {
            // Replace selection with the code
            var selectionStart = textArea.Selection.SurroundingSegment.Offset;
            var selectionLength = textArea.Selection.SurroundingSegment.Length;
            document.Replace(selectionStart, selectionLength, e.Code);
            CodeEditor.CaretOffset = selectionStart + e.Code.Length;
        }
        else
        {
            // Insert at cursor position
            var insertPosition = CodeEditor.CaretOffset;

            // Check if we should add a newline before the inserted code
            if (insertPosition > 0)
            {
                var charBefore = document.GetCharAt(insertPosition - 1);
                if (charBefore != '\n' && charBefore != '\r')
                {
                    // Add newline before the code if not at the start of a line
                    document.Insert(insertPosition, Environment.NewLine);
                    insertPosition += Environment.NewLine.Length;
                }
            }

            document.Insert(insertPosition, e.Code);
            CodeEditor.CaretOffset = insertPosition + e.Code.Length;
        }

        // Mark as having unsaved changes
        _hasUnsavedChanges = true;

        // Show feedback
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Code inserted into editor.");
        StatusText.Text = "Code inserted into editor";

        // Focus the editor
        CodeEditor.Focus();
    }

    #endregion

    #region Cloud, Collaboration, and Network MIDI

    /// <summary>
    /// Opens the Cloud Storage dialog.
    /// </summary>
    private void CloudStorage_Click(object sender, RoutedEventArgs e)
    {
        CloudStorageDialog.ShowDialog(this);
    }

    /// <summary>
    /// Opens the Collaboration dialog.
    /// </summary>
    private void Collaboration_Click(object sender, RoutedEventArgs e)
    {
        CollaborationDialog.ShowDialog(this);
    }

    /// <summary>
    /// Opens the Network MIDI dialog.
    /// </summary>
    private void NetworkMidi_Click(object sender, RoutedEventArgs e)
    {
        NetworkMidiDialog.ShowDialog(this);
    }

    #endregion

    #region Command Palette

    /// <summary>
    /// Shows the command palette dialog.
    /// </summary>
    private void ShowCommandPalette()
    {
        // Register commands if not already done
        RegisterCommandPaletteCommands();

        // Show the palette
        var selectedCommand = CommandPaletteDialog.ShowPalette(this);

        if (selectedCommand != null)
        {
            OutputLine($"Executed: {selectedCommand.Category}: {selectedCommand.Name}");
        }
    }

    /// <summary>
    /// Registers commands with the command palette service.
    /// </summary>
    private void RegisterCommandPaletteCommands()
    {
        var service = CommandPaletteService.Instance;

        // Only register once
        if (service.Commands.Count > 0)
            return;

        // File commands
        service.RegisterCommand("New Project", "File", () => NewProject_Click(this, new RoutedEventArgs()), "Ctrl+Shift+N", "Create a new project");
        service.RegisterCommand("Open Project", "File", () => OpenProject_Click(this, new RoutedEventArgs()), "Ctrl+Shift+O", "Open an existing project");
        service.RegisterCommand("New File", "File", () => NewFile_Click(this, new RoutedEventArgs()), "Ctrl+N", "Create a new script file");
        service.RegisterCommand("Save", "File", () => SaveScript_Click(this, new RoutedEventArgs()), "Ctrl+S", "Save current file");
        service.RegisterCommand("Save All", "File", () => SaveAll_Click(this, new RoutedEventArgs()), "Ctrl+Shift+S", "Save all open files");
        service.RegisterCommand("Settings", "File", () => Settings_Click(this, new RoutedEventArgs()), "Ctrl+,", "Open settings");
        service.RegisterCommand("Exit", "File", () => Exit_Click(this, new RoutedEventArgs()), null, "Exit the application");

        // Edit commands
        service.RegisterCommand("Undo", "Edit", () => CodeEditor.Undo(), "Ctrl+Z", "Undo last action");
        service.RegisterCommand("Redo", "Edit", () => CodeEditor.Redo(), "Ctrl+Y", "Redo last undone action");
        service.RegisterCommand("Cut", "Edit", () => CodeEditor.Cut(), "Ctrl+X", "Cut selection");
        service.RegisterCommand("Copy", "Edit", () => CodeEditor.Copy(), "Ctrl+C", "Copy selection");
        service.RegisterCommand("Paste", "Edit", () => CodeEditor.Paste(), "Ctrl+V", "Paste from clipboard");
        service.RegisterCommand("Select All", "Edit", () => CodeEditor.SelectAll(), "Ctrl+A", "Select all text");
        service.RegisterCommand("Find", "Edit", () => FindReplaceBar.ShowFind(), "Ctrl+F", "Find text");
        service.RegisterCommand("Replace", "Edit", () => FindReplaceBar.ShowReplace(), "Ctrl+H", "Find and replace text");

        // Transport commands
        service.RegisterCommand("Run Script", "Transport", () => _ = ExecuteScript(), "Ctrl+Enter", "Run the current script", ["play", "execute", "start"]);
        service.RegisterCommand("Stop", "Transport", () =>
        {
            _engineService.AllNotesOff();
            _isRunning = false;
            _visualization?.OnPlaybackStopped();
            SetStatusText("Stopped");
            OutputLine("Stopped");
        }, "Escape", "Stop playback", ["pause", "halt"]);
        service.RegisterCommand("Panic (All Notes Off)", "Transport", () =>
        {
            _engineService.AllNotesOff();
            StatusText.Text = "Panic! All Notes Off";
            OutputLine("Panic! All Notes Off");

            // Reset status text after a brief delay
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                if (StatusText.Text == "Panic! All Notes Off")
                {
                    SetStatusText(_isRunning ? "Running" : "Ready");
                }
            };
            timer.Start();
        }, "Alt+Space", "Emergency stop - silence all audio without stopping the script", ["panic", "silence", "mute", "emergency", "all notes off"]);

        // View commands
        service.RegisterCommand("Toggle Output", "View", () =>
        {
            if (_outputVisible)
            {
                OutputPanel.Visibility = Visibility.Collapsed;
                OutputSplitter.Visibility = Visibility.Collapsed;
                _outputVisible = false;
            }
            else
            {
                OutputPanel.Visibility = Visibility.Visible;
                OutputSplitter.Visibility = Visibility.Visible;
                _outputVisible = true;
            }
        }, null, "Toggle output panel visibility");

        service.RegisterCommand("Clear Output", "View", () => OutputBox.Document.Blocks.Clear(), null, "Clear the output panel");

        // View - Panels
        service.RegisterCommand("Toggle Project Explorer", "View", () => ToggleProjectExplorer_Click(this, new RoutedEventArgs()), null, "Show/hide project explorer", ["sidebar", "files", "tree"]);
        service.RegisterCommand("Toggle Workshop", "View", () => ToggleWorkshop_Click(this, new RoutedEventArgs()), null, "Show/hide workshop panel", ["examples", "snippets", "templates"]);
        service.RegisterCommand("Toggle Minimap", "View", () => ToggleMinimap_Click(this, new RoutedEventArgs()), null, "Show/hide code minimap", ["overview", "scroll"]);
        service.RegisterCommand("Toggle MIDI Panel", "View", () => ToggleMidiPanel_Click(this, new RoutedEventArgs()), null, "Show/hide MIDI devices panel", ["devices", "controller"]);
        service.RegisterCommand("Toggle VST Panel", "View", () => ToggleVstPanel_Click(this, new RoutedEventArgs()), null, "Show/hide VST plugins panel", ["plugins", "instruments", "effects"]);
        service.RegisterCommand("Toggle Audio Panel", "View", () => ToggleAudioPanel_Click(this, new RoutedEventArgs()), null, "Show/hide audio files panel", ["samples", "wav", "files"]);
        service.RegisterCommand("Toggle Track Properties", "View", () => ToggleTrackPropertiesPanel_Click(this, new RoutedEventArgs()), null, "Show/hide track properties", ["track", "properties", "inspector"]);
        service.RegisterCommand("Toggle Undo History", "View", () => ToggleUndoHistory_Click(this, new RoutedEventArgs()), null, "Show/hide undo history panel", ["history", "changes"]);

        // View - Editors & Windows

        service.RegisterCommand("Toggle Synth Editor", "View", () => ToggleSynthEditor_Click(this, new RoutedEventArgs()), "F4 / Ctrl+Shift+Y", "Open synthesizer parameter editor", ["synth", "instrument", "parameters"]);
        service.RegisterCommand("Toggle Effects Editor", "View", () => ToggleEffectsEditor_Click(this, new RoutedEventArgs()), "F5 / Ctrl+Shift+E", "Open audio effects editor", ["effects", "fx", "processing"]);
        service.RegisterCommand("Open Pattern Editor", "View", () => OpenPatternEditor_Click(this, new RoutedEventArgs()), "F6 / Ctrl+Shift+P", "Open piano roll pattern editor", ["piano roll", "notes", "midi", "sequence"]);
        service.RegisterCommand("Toggle Mixer", "View", () => ToggleMixer_Click(this, new RoutedEventArgs()), "F7 / Ctrl+Shift+M", "Open mixing console", ["mix", "fader", "channel", "levels"]);
        service.RegisterCommand("Open Arrangement", "View", () => OpenArrangement_Click(this, new RoutedEventArgs()), "F8 / Ctrl+Shift+A", "Open arrangement timeline view", ["timeline", "arrangement", "structure", "song"]);
        service.RegisterCommand("Open Session View", "View", () => OpenSessionView_Click(this, new RoutedEventArgs()), "Ctrl+Alt+S", "Open session view with clip launcher grid", ["session", "clips", "launcher", "live", "ableton"]);

        // View - Code folding
        service.RegisterCommand("Fold All", "View", () => FoldAll_Click(this, new RoutedEventArgs()), null, "Collapse all code regions", ["collapse", "fold", "regions"]);
        service.RegisterCommand("Unfold All", "View", () => UnfoldAll_Click(this, new RoutedEventArgs()), null, "Expand all code regions", ["expand", "unfold", "regions"]);

        // Additional Edit commands
        service.RegisterCommand("Toggle Comment", "Edit", () => ToggleLineComment(), "Ctrl+/", "Comment or uncomment selected lines", ["comment", "uncomment"]);
        service.RegisterCommand("Duplicate Line", "Edit", () => DuplicateLine(), "Ctrl+Shift+D", "Duplicate current line", ["copy line", "clone"]);
        service.RegisterCommand("Move Line Up", "Edit", () => MoveSelectedLinesUp(), "Alt+Up", "Move selected lines up", ["swap", "reorder"]);
        service.RegisterCommand("Move Line Down", "Edit", () => MoveSelectedLinesDown(), "Alt+Down", "Move selected lines down", ["swap", "reorder"]);
        service.RegisterCommand("Go to Line", "Edit", () => ShowGotoLineDialog(), "Ctrl+G", "Jump to a specific line number", ["goto", "jump", "line number"]);
        service.RegisterCommand("Select Line", "Edit", () => SelectCurrentLine(), "Ctrl+L", "Select entire current line", ["selection"]);

        // Project commands
        service.RegisterCommand("Add Script", "Project", () => AddScript_Click(this, new RoutedEventArgs()), null, "Add a new script to the project", ["new", "create"]);
        service.RegisterCommand("Import Audio", "Project", () => ImportAudio_Click(this, new RoutedEventArgs()), null, "Import audio file into project", ["sample", "wav", "mp3", "audio"]);
        service.RegisterCommand("Add Reference", "Project", () => AddReference_Click(this, new RoutedEventArgs()), null, "Add external reference", ["reference", "library"]);
        service.RegisterCommand("Project Settings", "Project", () => ProjectSettings_Click(this, new RoutedEventArgs()), null, "Open project settings dialog", ["settings", "config"]);
        service.RegisterCommand("Refresh Project Tree", "Project", () => RefreshProjectTree_Click(this, new RoutedEventArgs()), null, "Refresh the project file tree", ["reload", "update"]);

        // Help commands
        service.RegisterCommand("About", "Help", () =>
        {
            var dialog = new AboutDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "About MusicEngine Editor");

        service.RegisterCommand("Keyboard Shortcuts", "Help", () =>
        {
            var dialog = new ShortcutsDialog(App.Services.GetRequiredService<IShortcutService>()) { Owner = this };
            dialog.ShowDialog();
        }, null, "Show keyboard shortcuts");

        // Tools
        service.RegisterCommand("Quantize", "Tools", () =>
        {
            var dialog = new QuantizeDialog { Owner = this };
            dialog.ShowDialog();
        }, "Q", "Quantize selected notes", ["snap", "grid"]);

        service.RegisterCommand("Export Audio", "Tools", () =>
        {
            var dialog = new ExportDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Export project to audio file", ["render", "bounce"]);

        service.RegisterCommand("Metronome Settings", "Tools", () =>
        {
            var dialog = new MetronomeSettingsDialog(App.Services.GetRequiredService<MetronomeService>()) { Owner = this };
            dialog.ShowDialog();
        }, null, "Configure metronome");

        service.RegisterCommand("Recording Setup", "Tools", () =>
        {
            var dialog = new RecordingSetupDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Configure recording settings");

        service.RegisterCommand("Stem Export", "Tools", () =>
        {
            var dialog = new StemExportDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Export individual stems");

        service.RegisterCommand("Render Queue", "Tools", () =>
        {
            var dialog = new RenderQueueDialog { Owner = this };
            dialog.ShowDialog();
        }, "Ctrl+Shift+R", "Manage render queue for batch exports", ["batch", "export", "queue"]);

        service.RegisterCommand("Groove Template", "Tools", () =>
        {
            var dialog = new GrooveTemplateDialog { Owner = this };
            dialog.ShowDialog();
        }, "Ctrl+Shift+G", "Apply groove templates to timing", ["swing", "feel", "humanize"]);

        service.RegisterCommand("MIDI Transform", "Tools", () =>
        {
            var dialog = new MIDITransformDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Transform MIDI data with operations", ["transpose", "velocity", "length"]);

        service.RegisterCommand("Logical Editor", "Tools", () =>
        {
            var dialog = new LogicalEditorDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Advanced MIDI editing with logic conditions", ["filter", "select", "process"]);

        service.RegisterCommand("Batch Processor", "Tools", () =>
        {
            var dialog = new BatchProcessorDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Process multiple files at once", ["batch", "convert", "process"]);

        service.RegisterCommand("Batch Fade", "Tools", () =>
        {
            var dialog = new BatchFadeDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Apply fades to multiple clips", ["fade in", "fade out", "crossfade"]);

        service.RegisterCommand("Macro Recorder", "Tools", () =>
        {
            var dialog = new MacroRecorderDialog(App.Services.GetRequiredService<MacroRecorderService>()) { Owner = this };
            dialog.ShowDialog();
        }, null, "Record and playback macros", ["automate", "repeat", "script"]);

        service.RegisterCommand("Crossfade Editor", "Tools", () =>
        {
            var dialog = new CrossfadeEditorDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Edit crossfade curves between clips", ["transition", "blend"]);

        // Analysis commands
        service.RegisterCommand("Audio Statistics", "Analysis", () =>
        {
            var dialog = new AudioStatisticsDialog { Owner = this };
            dialog.ShowDialog();
        }, "F10", "View audio file statistics", ["peak", "rms", "loudness", "dynamic range"]);

        service.RegisterCommand("Loudness Report", "Analysis", () =>
        {
            var dialog = new LoudnessReportDialog { Owner = this };
            dialog.ShowDialog();
        }, "Ctrl+Shift+L", "Generate loudness analysis report", ["lufs", "true peak", "mastering"]);

        service.RegisterCommand("Audio Suite", "Analysis", () =>
        {
            var dialog = new AudioSuiteDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Advanced audio analysis suite", ["spectrum", "phase", "correlation"]);

        service.RegisterCommand("Project Statistics", "Analysis", () =>
        {
            var dialog = new ProjectStatisticsDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "View project statistics and metrics", ["info", "summary"]);

        service.RegisterCommand("Project Compare", "Analysis", () =>
        {
            var dialog = new ProjectCompareDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Compare two project versions", ["diff", "changes"]);

        // Note: Track Version Compare requires a specific track to be selected first
        // It's available through the track context menu when a track is selected

        // Plugin commands
        service.RegisterCommand("Plugin Manager", "Plugins", () =>
        {
            // Plugin Manager is accessible via the VST panel - select a plugin and right-click
            OutputLine("Open the VST panel (View > VST Panel) to manage plugins.");
        }, "F12", "Manage VST plugins - opens VST panel", ["vst", "organize", "scan"]);

        service.RegisterCommand("Plugin Blacklist", "Plugins", () =>
        {
            var dialog = new PluginBlacklistDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Manage blacklisted plugins", ["disable", "block"]);

        service.RegisterCommand("Plugin Preset Browser", "Plugins", () =>
        {
            var dialog = new PluginPresetBrowserDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Browse plugin presets", ["presets", "sounds"]);

        service.RegisterCommand("VST Effect Selector", "Plugins", () =>
        {
            var dialog = new VstEffectSelectorDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Select VST effect to add", ["insert", "add"]);

        // Note: VST Preset Browser requires a specific plugin to be selected first
        // It's available through the VST panel when a plugin is selected

        // Track commands
        service.RegisterCommand("Track Template", "Tracks", () =>
        {
            var dialog = new TrackTemplateDialog { Owner = this };
            dialog.ShowDialog();
        }, "Ctrl+Shift+T", "Create or apply track templates", ["preset", "save", "load"]);

        service.RegisterCommand("Track Import", "Tracks", () =>
        {
            var dialog = new TrackImportDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Import tracks from another project", ["copy", "move"]);

        service.RegisterCommand("Channel Settings", "Tracks", () =>
        {
            var dialog = new ChannelSettingsDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Configure channel strip settings", ["routing", "sends", "inserts"]);

        // Note: Marker Editor requires a specific marker to be selected first
        // It's available through the timeline when a marker is selected

        // Workspace & Settings
        service.RegisterCommand("Workspace Manager", "Window", () =>
        {
            var dialog = new WorkspaceDialog(App.Services.GetRequiredService<WorkspaceService>()) { Owner = this };
            dialog.ShowDialog();
        }, "Ctrl+Shift+W", "Manage window layouts", ["layout", "save", "restore"]);

        service.RegisterCommand("Theme Settings", "Window", () =>
        {
            var dialog = new ThemeSettingsDialog(App.Services.GetRequiredService<IThemeService>()) { Owner = this };
            dialog.ShowDialog();
        }, null, "Customize application theme", ["dark", "colors", "appearance"]);

        service.RegisterCommand("Color Palette", "Window", () =>
        {
            var dialog = new ColorPaletteDialog(App.Services.GetRequiredService<ColorPaletteService>()) { Owner = this };
            dialog.ShowDialog();
        }, null, "Customize color palette", ["colors", "scheme"]);

        service.RegisterCommand("Performance Monitor", "Window", () =>
        {
            var dialog = new PerformanceDialog { Owner = this };
            dialog.ShowDialog();
        }, "F9", "Open performance monitoring window", ["cpu", "memory", "disk", "latency"]);

        service.RegisterCommand("Toggle Fullscreen", "Window", () =>
        {
            if (WindowState == System.Windows.WindowState.Maximized && WindowStyle == WindowStyle.None)
            {
                WindowState = System.Windows.WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = System.Windows.WindowState.Maximized;
            }
        }, "F11", "Toggle fullscreen mode", ["maximize", "full screen"]);

        // Recording commands
        service.RegisterCommand("Recording Dialog", "Recording", () =>
        {
            var dialog = new RecordingDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Open recording panel", ["arm", "input"]);

        service.RegisterCommand("Recovery Manager", "File", () =>
        {
            var dialog = new RecoveryDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Recover unsaved projects", ["backup", "restore", "crash"]);

        // Cloud & Collaboration commands
        service.RegisterCommand("Cloud Storage", "Cloud", () =>
        {
            CloudStorageDialog.ShowDialog(this);
        }, null, "Open cloud storage manager", ["sync", "upload", "download"]);

        service.RegisterCommand("Collaboration", "Cloud", () =>
        {
            CollaborationDialog.ShowDialog(this);
        }, null, "Start or join collaboration session", ["collab", "share", "realtime"]);

        service.RegisterCommand("Network MIDI", "Cloud", () =>
        {
            NetworkMidiDialog.ShowDialog(this);
        }, null, "Configure network MIDI (RTP-MIDI)", ["rtpmidi", "network", "remote"]);
    }

    #endregion

    #region Welcome Screen Integration

    /// <summary>
    /// Triggers the new project dialog (for welcome screen integration).
    /// </summary>
    public void TriggerNewProject()
    {
        // Defer until window is fully rendered
        Dispatcher.BeginInvoke(new Action(() => NewProject_Click(this, new RoutedEventArgs())),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Triggers the open project dialog (for welcome screen integration).
    /// </summary>
    public void TriggerOpenProject()
    {
        // Defer until window is fully rendered
        Dispatcher.BeginInvoke(new Action(() => OpenProject_Click(this, new RoutedEventArgs())),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Opens a project file directly (for welcome screen integration).
    /// </summary>
    /// <param name="filePath">The full path to the project file.</param>
    public async System.Threading.Tasks.Task OpenProjectFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            OutputLine($"ERROR: Project file not found: {filePath}");
            return;
        }

        try
        {
            StatusText.Text = "Loading project...";
            _currentProject = await _projectService.OpenProjectAsync(filePath);

            // Verify project was loaded successfully
            if (_currentProject == null)
            {
                StatusText.Text = "Failed to load project";
                OutputLine("ERROR: Project loading returned null");
                MessageBox.Show("Failed to load project: Project file could not be parsed.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            UpdateProjectExplorer();
            UpdateAudioFilesList();
            ProjectNameDisplay.Text = _currentProject.Name;
            StatusText.Text = $"Loaded: {_currentProject.Name}";
            OutputLine($"Loaded project: {_currentProject.Name}");

            // Mark session as active for crash recovery
            RecoveryService.Instance.MarkSessionActive(_currentProject);

            // Initialize auto-save for this project
            AutoSaveService.Instance.Initialize(_projectService);

            // Add to recent projects
            var recentProjectsService = App.Services.GetService<IRecentProjectsService>();
            if (recentProjectsService != null)
            {
                await recentProjectsService.AddProjectAsync(filePath, _currentProject.Name);
            }

            // Open entry point script (with null checks)
            if (_currentProject.Scripts != null && _currentProject.Scripts.Count > 0)
            {
                foreach (var script in _currentProject.Scripts)
                {
                    if (script != null && script.IsEntryPoint)
                    {
                        OpenScriptInTab(script);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Project loading failed";
            OutputLine($"ERROR: Failed to open project: {ex.Message}");
            MessageBox.Show($"Failed to open project: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Workspace Presets

    private WorkspacePresetService? _workspacePresetService;

    /// <summary>
    /// Opens the workspace presets dialog.
    /// </summary>
    private void WorkspacePresets_Click(object sender, RoutedEventArgs e)
    {
        EnsureWorkspacePresetServiceInitialized();

        var dialog = new WorkspacePresetDialog(_workspacePresetService!, CaptureCurrentLayout)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.PresetApplied && dialog.SelectedPreset != null)
        {
            ApplyWorkspacePreset(dialog.SelectedPreset);
        }
    }

    /// <summary>
    /// Applies a workspace preset to the current window layout.
    /// </summary>
    private void ApplyWorkspacePreset(WorkspacePresetData preset)
    {
        try
        {
            // Apply window state
            if (preset.MainWindow.WindowState == "Maximized")
            {
                WindowState = System.Windows.WindowState.Maximized;
            }
            else if (preset.MainWindow.WindowState == "Normal")
            {
                WindowState = System.Windows.WindowState.Normal;
                Left = preset.MainWindow.Left;
                Top = preset.MainWindow.Top;
                Width = preset.MainWindow.Width;
                Height = preset.MainWindow.Height;
            }

            // Apply panel visibility
            ProjectExplorerMenuItem.IsChecked = preset.Panels.ProjectExplorerVisible;
            ProjectExplorerPanel.Visibility = preset.Panels.ProjectExplorerVisible ? Visibility.Visible : Visibility.Collapsed;
            LeftPanelColumn.Width = preset.Panels.ProjectExplorerVisible ? new GridLength(preset.PanelSizes.LeftPanelWidth) : new GridLength(0);

            OutputMenuItem.IsChecked = preset.Panels.OutputVisible;
            OutputPanel.Visibility = preset.Panels.OutputVisible ? Visibility.Visible : Visibility.Collapsed;
            _outputVisible = preset.Panels.OutputVisible;

            // Apply mixer visibility if applicable
            MixerMenuItem.IsChecked = preset.Panels.MixerVisible;

            // Apply synth editor visibility
            SynthEditorMenuItem.IsChecked = preset.Panels.SynthEditorVisible;

            // Apply effects editor visibility
            EffectsEditorMenuItem.IsChecked = preset.Panels.EffectsEditorVisible;

            // Update status
            StatusText.Text = $"Workspace preset '{preset.Name}' applied";
            OutputLine($"Applied workspace preset: {preset.Name}");

          
        }
        catch (Exception ex)
        {
            OutputLine($"Error applying workspace preset: {ex.Message}");
            StatusText.Text = "Failed to apply workspace preset";
        }
    }

    /// <summary>
    /// Captures the current window layout for saving to a preset.
    /// </summary>
    private WorkspaceLayoutCapture CaptureCurrentLayout()
    {
        return new WorkspaceLayoutCapture
        {
            MainWindow = new WindowLayoutState
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height,
                WindowState = WindowState == System.Windows.WindowState.Maximized ? "Maximized" : "Normal"
            },
            Panels = new PanelVisibilityState
            {
                ProjectExplorerVisible = ProjectExplorerMenuItem.IsChecked,
                OutputVisible = _outputVisible,
                MixerVisible = MixerMenuItem.IsChecked,
                SynthEditorVisible = SynthEditorMenuItem.IsChecked,
                EffectsEditorVisible = EffectsEditorMenuItem.IsChecked,
                TransportVisible = true // Always visible
            },
            PanelSizes = new PanelSizeState
            {
                LeftPanelWidth = LeftPanelColumn.Width.Value,
                RightPanelWidth = RightPanelColumn.Width.Value,
                OutputHeight = OutputPanel.ActualHeight
            }
        };
    }

    /// <summary>
    /// Ensures the workspace preset service is initialized.
    /// </summary>
    private void EnsureWorkspacePresetServiceInitialized()
    {
        if (_workspacePresetService == null)
        {
            _workspacePresetService = new WorkspacePresetService();
            _ = _workspacePresetService.InitializeAsync();
            _workspacePresetService.PresetLoaded += (s, preset) => ApplyWorkspacePreset(preset);
        }
    }



    /// <summary>
    /// Switches to a workspace preset by name.
    /// </summary>
    private void SwitchToWorkspacePreset(string presetName)
    {
        EnsureWorkspacePresetServiceInitialized();

        var preset = _workspacePresetService!.Presets.FirstOrDefault(p =>
            string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));

        if (preset != null)
        {
            ApplyWorkspacePreset(preset);
        }
    }

   

    // Quick workspace menu item handlers
    private void WorkspaceRecording_Click(object sender, RoutedEventArgs e) => SwitchToWorkspacePreset("Recording");
    private void WorkspaceMixing_Click(object sender, RoutedEventArgs e) => SwitchToWorkspacePreset("Mixing");
    private void WorkspaceMastering_Click(object sender, RoutedEventArgs e) => SwitchToWorkspacePreset("Mastering");
    private void WorkspaceEditing_Click(object sender, RoutedEventArgs e) => SwitchToWorkspacePreset("Editing");
    private void WorkspacePerformance_Click(object sender, RoutedEventArgs e) => SwitchToWorkspacePreset("Performance");

    /// <summary>
    /// Handles workspace preset shortcuts (Ctrl+1 through Ctrl+5).
    /// </summary>
    private bool HandleWorkspacePresetShortcut(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;

        string? presetName = e.Key switch
        {
            Key.D1 => "Recording",
            Key.D2 => "Mixing",
            Key.D3 => "Mastering",
            Key.D4 => "Editing",
            Key.D5 => "Performance",
            _ => null
        };

        if (presetName != null)
        {
            SwitchToWorkspacePreset(presetName);
            e.Handled = true;
            return true;
        }

        return false;
    }

    #endregion
}

// Data classes for the right panel lists
public class MidiDeviceInfo
{
    private static readonly System.Windows.Media.SolidColorBrush s_inputBrush;
    private static readonly System.Windows.Media.SolidColorBrush s_outputBrush;

    static MidiDeviceInfo()
    {
        s_inputBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x88));
        s_inputBrush.Freeze();
        s_outputBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xD9, 0xFF));
        s_outputBrush.Freeze();
    }

    public string Name { get; set; } = "";
    public string Type { get; set; } = "";  // "Input" or "Output"
    public int DeviceIndex { get; set; } = -1;
    public string ChannelInfo { get; set; } = "";  // e.g., "Ch 1-16" or "Omni"

    // Display string combining all info for the UI
    public string DisplayName => $"{Name} ({Type})";
    public string DisplayChannel => string.IsNullOrEmpty(ChannelInfo) ? "Ch 1-16" : ChannelInfo;

    // Icon based on type
    public string TypeIcon => Type == "Input" ? "\u2B05" : "\u27A1";  // Left arrow for input, right arrow for output

    // Color for the type indicator
    public System.Windows.Media.Brush TypeColor => Type == "Input"
        ? s_inputBrush   // Green for input
        : s_outputBrush; // Blue for output
}

public class VstPluginInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
}

public class AudioFileInfo
{
    public string Alias { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Format { get; set; } = "";
}

// Problem/Error item for the Problems panel
public enum ProblemSeverity
{
    Error,
    Warning,
    Info
}

public class ProblemItem
{
    private static readonly System.Windows.Media.SolidColorBrush s_errorBrush;
    private static readonly System.Windows.Media.SolidColorBrush s_warningBrush;
    private static readonly System.Windows.Media.SolidColorBrush s_infoBrush;

    static ProblemItem()
    {
        s_errorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x47, 0x57));
        s_errorBrush.Freeze();
        s_warningBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xB8, 0x00));
        s_warningBrush.Freeze();
        s_infoBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xD9, 0xFF));
        s_infoBrush.Freeze();
    }

    public ProblemSeverity Severity { get; set; } = ProblemSeverity.Error;
    public string Message { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string Suggestion { get; set; } = "";

    // For display in ListView
    public string Icon => Severity switch
    {
        ProblemSeverity.Error => "\u26A0",   // Warning sign (using this since error icon is not standard)
        ProblemSeverity.Warning => "\u26A0",
        ProblemSeverity.Info => "\u2139",    // Info icon
        _ => "\u26A0"
    };

    public System.Windows.Media.Brush IconColor => Severity switch
    {
        ProblemSeverity.Error => s_errorBrush,
        ProblemSeverity.Warning => s_warningBrush,
        ProblemSeverity.Info => s_infoBrush,
        _ => System.Windows.Media.Brushes.White
    };
}

// Active Instrument display item with animation support
public class ActiveInstrumentInfo : System.ComponentModel.INotifyPropertyChanged
{
    // Cached frozen brushes for static (non-animated) states
    private static readonly System.Windows.Media.SolidColorBrush s_activeIconBrush;
    private static readonly System.Windows.Media.SolidColorBrush s_inactiveGrayBrush;
    private static readonly System.Windows.Media.SolidColorBrush s_activeTextBrush;
    private static readonly System.Windows.Media.SolidColorBrush s_activeBackgroundBrush;
    private static readonly System.Windows.Media.SolidColorBrush s_activeBorderBrush;
    private static readonly System.Windows.Media.SolidColorBrush s_inactiveBorderBrush;

    static ActiveInstrumentInfo()
    {
        s_activeIconBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x88));
        s_activeIconBrush.Freeze();
        s_inactiveGrayBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6F, 0x73, 0x7A));
        s_inactiveGrayBrush.Freeze();
        s_activeTextBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDF, 0xE1, 0xE5));
        s_activeTextBrush.Freeze();
        s_activeBackgroundBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0x00, 0xD9, 0xFF));
        s_activeBackgroundBrush.Freeze();
        s_activeBorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xD9, 0xFF));
        s_activeBorderBrush.Freeze();
        s_inactiveBorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x2A));
        s_inactiveBorderBrush.Freeze();
    }

    private string _name = "";
    private string _instrumentType = "synth";
    private bool _isActive;
    private bool _isPlaying;
    private int _currentNoteValue;
    private int _velocity;
    private double _pulsePhase;
    private DateTime _lastNoteTime;
    private readonly HashSet<int> _activeNotes = new();

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public string InstrumentType
    {
        get => _instrumentType;
        set { _instrumentType = value; OnPropertyChanged(nameof(Icon)); OnPropertyChanged(nameof(IconColor)); }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(FontWeight));
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            _isPlaying = value;
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(NoteVisibility));
            OnPropertyChanged(nameof(NoteColor));
        }
    }

    // Icon based on instrument type
    public string Icon => InstrumentType.ToLower() switch
    {
        "synth" => "\u266B",      // Musical note
        "vst" => "\u2699",        // Gear for VST
        "sampler" => "\u25B6",    // Play triangle for sampler
        "pattern" => "\u2630",    // Trigram for pattern
        _ => "\u266A"             // Default music note
    };

    public System.Windows.Media.Brush IconColor
    {
        get
        {
            if (IsPlaying)
            {
                // Pulsing bright color when playing
                var intensity = (byte)(180 + 75 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, 0xFF, intensity));
            }
            if (IsActive)
            {
                return s_activeIconBrush; // Green
            }
            return s_inactiveGrayBrush; // Gray
        }
    }

    public System.Windows.Media.Brush TextColor
    {
        get
        {
            if (IsPlaying)
            {
                // Bright pulsing white when playing
                var intensity = (byte)(220 + 35 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, intensity, intensity));
            }
            if (IsActive)
            {
                return s_activeTextBrush; // Bright
            }
            return s_inactiveGrayBrush; // Dim
        }
    }

    public System.Windows.Media.Brush BackgroundBrush
    {
        get
        {
            if (IsPlaying)
            {
                // Glowing background when playing
                var alpha = (byte)(40 + 30 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x00, 0xFF, 0x88));
            }
            if (IsActive)
            {
                return s_activeBackgroundBrush;
            }
            return System.Windows.Media.Brushes.Transparent;
        }
    }

    public System.Windows.Media.Brush BorderBrush
    {
        get
        {
            if (IsPlaying)
            {
                // Bright green border when playing
                var intensity = (byte)(100 + 55 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, 0xAB, intensity));
            }
            if (IsActive)
            {
                return s_activeBorderBrush;
            }
            return s_inactiveBorderBrush;
        }
    }

    public System.Windows.FontWeight FontWeight => IsPlaying ? System.Windows.FontWeights.Bold : (IsActive ? System.Windows.FontWeights.SemiBold : System.Windows.FontWeights.Normal);

    // Currently playing note display
    public string CurrentNote
    {
        get
        {
            if (!IsPlaying || _activeNotes.Count == 0) return "";
            var note = _activeNotes.First();
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = (note / 12) - 1;
            var noteName = noteNames[note % 12];
            return $"{noteName}{octave}";
        }
    }

    public System.Windows.Media.Brush NoteColor
    {
        get
        {
            if (!IsPlaying) return System.Windows.Media.Brushes.Transparent;
            // Velocity-based color intensity
            var intensity = (byte)(150 + (_velocity * 105 / 127));
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, intensity, 0x50));
        }
    }

    public System.Windows.Visibility NoteVisibility => IsPlaying && _activeNotes.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public void TriggerNote(int note, int velocity)
    {
        _activeNotes.Add(note);
        _currentNoteValue = note;
        _velocity = velocity;
        _lastNoteTime = DateTime.Now;
        IsPlaying = true;
        OnPropertyChanged(nameof(CurrentNote));
    }

    public void ReleaseNote(int note)
    {
        _activeNotes.Remove(note);
        if (_activeNotes.Count == 0)
        {
            IsPlaying = false;
        }
        OnPropertyChanged(nameof(CurrentNote));
        OnPropertyChanged(nameof(NoteVisibility));
    }

    public void UpdateAnimation()
    {
        if (IsPlaying)
        {
            _pulsePhase += 0.3; // Speed of pulse
            if (_pulsePhase > Math.PI * 2) _pulsePhase -= Math.PI * 2;

            // Notify all visual properties to update
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(NoteColor));
        }
        else if (_pulsePhase > 0)
        {
            // Fade out animation
            _pulsePhase = 0;
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Stores data for a frozen track to enable unfreezing.
/// </summary>
public class FreezeTrackData
{
    /// <summary>
    /// Gets or sets the track ID.
    /// </summary>
    public int TrackId { get; set; }

    /// <summary>
    /// Gets or sets the original track name.
    /// </summary>
    public string OriginalName { get; set; } = "";

    /// <summary>
    /// Gets or sets the original instrument name.
    /// </summary>
    public string? OriginalInstrumentName { get; set; }

    /// <summary>
    /// Gets or sets the original instrument path (for VST plugins).
    /// </summary>
    public string? OriginalInstrumentPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the frozen audio file.
    /// </summary>
    public string? FrozenAudioFilePath { get; set; }

    /// <summary>
    /// Gets or sets the original track type.
    /// </summary>
    public Models.TrackType OriginalTrackType { get; set; }

    /// <summary>
    /// Gets or sets when the track was frozen.
    /// </summary>
    public DateTime FrozenAt { get; set; }

    /// <summary>
    /// Gets or sets the duration of the frozen audio in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }
}
