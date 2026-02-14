using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.Win32;
using EditorApp.CodeStyling;
using MusicEngine.Scripting;

namespace EditorApp;

public partial class MainWindow : Window
{
    private const string FoldPlaceholder = "    ...";
    private const double EditorLineHeight = 18.0;
    private const int HighlightDelayMs = 50;
    private static readonly Regex VstCallRegex = new(
        @"(?:[A-Za-z_]\w*\.)*(?:CreateVstEffect|VstEffect|VstFx|CreateVst|Vst)\s*\(\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private bool _isRunning;
    private bool _isPseudoMaximized;
    private Rect _restoreBounds;
    private bool _consoleOpen;
    private bool _filesOpen;
    private EngineScriptInterface? _engineInterface;
    private bool _engineStarting;
    private Task? _engineWarmupTask;
    private bool _scriptsPrimed;
    private string? _projectRoot;
    private string? _currentFile;
    private bool _isDirty;
    private ScrollViewer? _editorScroll;
    private readonly TranslateTransform _lineNumbersTransform = new();
    private readonly List<FoldRegion> _foldRegions = new();
    private readonly HashSet<int> _collapsedStarts = new();
    private readonly List<string[]> _collapsedBlocks = new();
    private readonly CodeStylingApi _stylingApi = new();
    private bool _suppressTextChange;
    private bool _isApplyingHighlight;
    private readonly DispatcherTimer _highlightTimer;
    private readonly DispatcherTimer _refreshStatusTimer;
    private readonly Stopwatch _refreshStopwatch = new();
    private int _refreshUiDepth;
    private string? _pendingContextVstToken;
    private bool _editorHoverShowsOpenCursor;
    private string _pendingHighlightText = string.Empty;
    private bool _pendingPreserveCaret;
    private int _pendingCaretCharOffset;
    private int _pendingSelectionStartCharOffset;
    private int _pendingSelectionEndCharOffset;
    private double _pendingVerticalOffset;
    private double _pendingHorizontalOffset;
    private string _fullText = string.Empty;

    public CodeStylingApi StylingApi => _stylingApi;

    public void RefreshHighlight()
    {
        ScheduleHighlight(preserveCaret: true);
    }

    public MainWindow()
    {
        InitializeComponent();
        LineNumbers.RenderTransform = _lineNumbersTransform;
        _highlightTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(HighlightDelayMs)
        };
        _highlightTimer.Tick += OnHighlightTimerTick;
        _refreshStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _refreshStatusTimer.Tick += OnRefreshStatusTimerTick;
        InitializeStyling();
        _fullText = GetEditorText();
        UpdateCursor();
        UpdateFoldRegions();
        UpdateLineNumbers();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        SetPseudoMaximized();
        _filesOpen = true;
        FilesColumn.Width = new GridLength(120);
        LoadProjectTree();
        StartStopButton.IsEnabled = false;
        _engineWarmupTask = WarmupEngineAsync();
    }

    private async void OnStartStopClicked(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            StopScriptExecution();
            UpdateStartStopUi(false);
            return;
        }

        bool started = await StartScriptExecutionAsync();
        UpdateStartStopUi(started);
    }

    private void OnMinimizeClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnMaximizeClicked(object sender, RoutedEventArgs e)
    {
        if (_isPseudoMaximized)
        {
            RestoreFromPseudoMaximized();
        }
        else
        {
            SetPseudoMaximized();
        }
    }

    private void SetPseudoMaximized()
    {
        if (!_isPseudoMaximized)
        {
            _restoreBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        }

        Rect workArea = SystemParameters.WorkArea;
        WindowState = WindowState.Normal;
        Left = workArea.Left;
        Top = workArea.Top;
        Width = workArea.Width;
        Height = workArea.Height;
        _isPseudoMaximized = true;
    }

    private void RestoreFromPseudoMaximized()
    {
        if (_restoreBounds.Width > 0 && _restoreBounds.Height > 0)
        {
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
        }

        _isPseudoMaximized = false;
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnConsoleToggleClicked(object sender, RoutedEventArgs e)
    {
        _consoleOpen = !_consoleOpen;
        ConsoleRow.Height = _consoleOpen ? new GridLength(160) : new GridLength(0);
    }

    private void OnFilesToggleClicked(object sender, RoutedEventArgs e)
    {
        _filesOpen = !_filesOpen;
        FilesColumn.Width = _filesOpen ? new GridLength(120) : new GridLength(0);
    }

    protected override void OnClosed(EventArgs e)
    {
        System.Windows.Input.Mouse.OverrideCursor = null;
        _engineInterface?.Dispose();
        base.OnClosed(e);
    }

    private async Task EnsureEngineAsync()
    {
        if (_engineInterface != null || _engineStarting)
        {
            return;
        }

        _engineStarting = true;
        try
        {
            string projectRoot = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "MusicEngine");
            projectRoot = System.IO.Path.GetFullPath(projectRoot);
            _projectRoot = projectRoot;
            Environment.SetEnvironmentVariable("MUSICENGINE_PROJECT_DIR", projectRoot);

            var options = new EngineScriptInterfaceOptions
            {
                EnableVstScanning = true,
                StartSequencerOnStartup = false,
                ScriptFilePath = null
            };

            _engineInterface = new EngineScriptInterface(options);
            await _engineInterface.StartupAsync();
            _engineInterface.SetEditorMode(true);
            AppendConsole("Music engine ready. Project: " + projectRoot);
        }
        catch (Exception ex)
        {
            AppendConsole("Engine startup failed: " + ex.Message);
        }
        finally
        {
            _engineStarting = false;
        }
    }

    private async Task<bool> StartScriptExecutionAsync()
    {
        try
        {
            if (_engineWarmupTask != null)
            {
                await _engineWarmupTask;
            }

            await EnsureEngineAsync();
            if (_engineInterface == null)
            {
                return false;
            }

            if (_engineInterface.IsSleeping)
            {
                _engineInterface.Wake();
            }

            if (!_scriptsPrimed)
            {
                bool ran = await _engineInterface.Host.RefreshMainScriptsAsync();
                if (!ran)
                {
                    AppendConsole("No main scripts found.");
                    return false;
                }

                _scriptsPrimed = true;
            }

            _engineInterface.Host.StartSequencer();
            _isRunning = true;
            AppendConsole("Project scripts started.");
            return true;
        }
        catch (Exception ex)
        {
            AppendConsole("Script start failed: " + ex.Message);
            return false;
        }
    }

    private void StopScriptExecution()
    {
        if (_engineInterface == null)
        {
            return;
        }

        _engineInterface.Host.StopSequencer();
        _engineInterface.Sequencer.Stop();
        _engineInterface.Host.AllNotesOff();
        if (!_engineInterface.IsSleeping)
        {
            _engineInterface.Sleep();
        }

        _isRunning = false;
        AppendConsole("Script stopped.");
    }

    private async Task RefreshProjectScriptsAsync()
    {
        BeginRefreshUi();
        try
        {
            SaveCurrentEditorFileIfNeeded();
            await EnsureEngineAsync();
            if (_engineInterface == null)
            {
                return;
            }

            bool ran = await _engineInterface.Host.RefreshMainScriptsAsync();
            if (!ran)
            {
                _scriptsPrimed = false;
                AppendConsole("No main scripts found.");
                return;
            }

            _scriptsPrimed = true;
            if (_isRunning)
            {
                _engineInterface.Host.StartSequencer();
            }

            AppendConsole("Project scripts refreshed.");
        }
        catch (Exception ex)
        {
            AppendConsole("Script refresh failed: " + ex.Message);
        }
        finally
        {
            EndRefreshUi();
        }
    }

    private void SaveCurrentEditorFileIfNeeded()
    {
        if (!_isDirty || string.IsNullOrWhiteSpace(_currentFile))
        {
            return;
        }

        File.WriteAllText(_currentFile, _fullText);
        _isDirty = false;
        AppendConsole("Saved current file before refresh.");
    }

    private async Task WarmupEngineAsync()
    {
        try
        {
            await EnsureEngineAsync();
            if (_engineInterface == null)
            {
                return;
            }

            bool ran = await _engineInterface.Host.RefreshMainScriptsAsync();
            _scriptsPrimed = ran;
            if (ran)
            {
                AppendConsole("Project scripts preloaded.");
            }
            else
            {
                AppendConsole("No main scripts found for preload.");
            }
        }
        catch (Exception ex)
        {
            AppendConsole("Engine warmup failed: " + ex.Message);
        }
        finally
        {
            StartStopButton.IsEnabled = true;
        }
    }

    private void UpdateStartStopUi(bool isRunning)
    {
        if (_refreshUiDepth > 0)
        {
            return;
        }

        _isRunning = isRunning;
        StartStopButton.Content = _isRunning ? "Stop" : "Start";
        StartStopButton.Background = _isRunning
            ? new SolidColorBrush(Color.FromRgb(31, 107, 61))
            : new SolidColorBrush(Color.FromRgb(107, 31, 31));
        StartStopButton.BorderBrush = _isRunning
            ? new SolidColorBrush(Color.FromRgb(42, 138, 78))
            : new SolidColorBrush(Color.FromRgb(138, 42, 42));
    }

    private void BeginRefreshUi()
    {
        _refreshUiDepth++;
        if (_refreshUiDepth > 1)
        {
            return;
        }

        _refreshStopwatch.Restart();
        StartStopButton.Content = (_isRunning ? "Stop" : "Start") + " | Refresh 0.0s";
        StartStopButton.Background = new SolidColorBrush(Color.FromRgb(140, 28, 28));
        StartStopButton.BorderBrush = new SolidColorBrush(Color.FromRgb(186, 45, 45));
        _refreshStatusTimer.Start();
    }

    private void EndRefreshUi()
    {
        if (_refreshUiDepth <= 0)
        {
            return;
        }

        _refreshUiDepth--;
        if (_refreshUiDepth > 0)
        {
            return;
        }

        _refreshStatusTimer.Stop();
        _refreshStopwatch.Stop();
        UpdateStartStopUi(_isRunning);
    }

    private void OnRefreshStatusTimerTick(object? sender, EventArgs e)
    {
        string baseLabel = _isRunning ? "Stop" : "Start";
        StartStopButton.Content = $"{baseLabel} | Refresh {_refreshStopwatch.Elapsed.TotalSeconds:0.0}s";
    }

    private void AppendConsole(string message)
    {
        if (ConsoleText == null)
        {
            return;
        }

        ConsoleText.AppendText($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        ConsoleText.ScrollToEnd();
    }

    private void LoadProjectTree()
    {
        string projectRoot = _projectRoot ?? System.IO.Path.GetFullPath(System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "MusicEngine"));
        string testProjectDir = System.IO.Path.Combine(projectRoot, "Test Project");

        FileTree.Items.Clear();

        if (!Directory.Exists(testProjectDir))
        {
            FileTree.Items.Add(new TreeViewItem { Header = "Test Project (missing)" });
            return;
        }

        string[] files = Directory.GetFiles(testProjectDir, "*.cs", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            FileTree.Items.Add(new TreeViewItem { Header = Path.GetFileName(file), Tag = file });
        }
    }


    private void OnEditorChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChange)
        {
            return;
        }

        _isDirty = true;
        string displayText = GetEditorText();
        _fullText = _collapsedStarts.Count > 0
            ? ExpandCollapsedText(displayText)
            : displayText;
        UpdateFoldRegions();
        UpdateLineNumbers();
        UpdateCursor();
        ScheduleHighlight(preserveCaret: true);
    }

    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        _editorScroll = FindScrollViewer(CodeEditor);
        if (_editorScroll != null)
        {
            _editorScroll.ScrollChanged += OnEditorScrollChanged;
            SyncLineNumbersScroll();
        }
    }

    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateCursor();
    }

    private void OnFilesMenuMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        FilesMenu.ContextMenu.PlacementTarget = FilesMenu;
        FilesMenu.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OnViewMenuMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ViewMenu.ContextMenu.PlacementTarget = ViewMenu;
        ViewMenu.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OnNewClicked(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        _suppressTextChange = true;
        _currentFile = null;
        _fullText = string.Empty;
        _collapsedStarts.Clear();
        _collapsedBlocks.Clear();
        ScheduleHighlight(preserveCaret: false);
        _suppressTextChange = false;

        _isDirty = false;
        UpdateFoldRegions();
        UpdateLineNumbers();
        UpdateCursor();
    }

    private void OnOpenClicked(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Filter = "Code Files|*.txt;*.cs;*.json;*.me|All Files|*.*",
            Title = "Open File"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _suppressTextChange = true;
        _currentFile = dialog.FileName;
        _fullText = File.ReadAllText(dialog.FileName);
        _collapsedStarts.Clear();
        _collapsedBlocks.Clear();
        ScheduleHighlight(preserveCaret: false);
        _suppressTextChange = false;

        _isDirty = false;
        UpdateFoldRegions();
        UpdateLineNumbers();
        UpdateCursor();
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFile))
        {
            OnSaveAsClicked(sender, e);
            return;
        }

        File.WriteAllText(_currentFile, _fullText);
        _isDirty = false;
    }

    private void OnSaveAsClicked(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Filter = "Code Files|*.txt;*.cs;*.json;*.me|All Files|*.*",
            Title = "Save File"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _currentFile = dialog.FileName;
        File.WriteAllText(_currentFile, _fullText);
        _isDirty = false;
    }

    private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        SyncLineNumbersScroll();
    }

    private void OnFileTreeDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FileTree.SelectedItem is not TreeViewItem item)
        {
            return;
        }

        if (item.Tag is not string path || !File.Exists(path))
        {
            return;
        }

        _suppressTextChange = true;
        _currentFile = path;
        _fullText = File.ReadAllText(path);
        _collapsedStarts.Clear();
        _collapsedBlocks.Clear();
        ScheduleHighlight(preserveCaret: false);
        _suppressTextChange = false;

        _isDirty = false;
        UpdateFoldRegions();
        UpdateLineNumbers();
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        (int line, int column) = GetCaretLineColumn();
        CursorText.Text = $"Line {line + 1}, Col {column + 1}";
    }

    private void UpdateLineNumbers()
    {
        List<int> displayMap = BuildDisplayMap();
        int lineCount = Math.Max(1, displayMap.Count);
        StringBuilder numberBuilder = new();
        for (int i = 0; i < lineCount; i++)
        {
            int fullLine = displayMap[i];
            if (fullLine < 0)
            {
                numberBuilder.Append('\n');
                continue;
            }

            char marker = GetMarkerForLine(fullLine);
            if (marker == ' ')
            {
                numberBuilder.Append(fullLine + 1).Append('\n');
            }
            else
            {
                numberBuilder.Append(fullLine + 1).Append(marker).Append('\n');
            }
        }
        LineNumbers.Text = numberBuilder.ToString();
        UpdateLineNumberColumnWidth(lineCount);
    }

    private void SyncLineNumbersScroll()
    {
        if (_editorScroll == null)
        {
            return;
        }

        _lineNumbersTransform.Y = -_editorScroll.VerticalOffset;
    }

    private void OnLineNumbersMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_editorScroll == null)
        {
            return;
        }

        Point position = e.GetPosition(LineNumbers);
        int lineIndex = GetLineIndexFromPoint(position);
        if (lineIndex < 0)
        {
            return;
        }

        List<int> displayMap = BuildDisplayMap();
        if (lineIndex < 0 || lineIndex >= displayMap.Count)
        {
            return;
        }

        int fullLine = displayMap[lineIndex];
        if (fullLine < 0)
        {
            return;
        }

        FoldRegion? region = FindRegionByStartLine(fullLine);
        if (region == null)
        {
            return;
        }

        if (_collapsedStarts.Contains(region.StartLine))
        {
            _collapsedStarts.Remove(region.StartLine);
        }
        else
        {
            _collapsedStarts.Add(region.StartLine);
        }

        ApplyFoldView();
    }

    private void UpdateLineNumberColumnWidth(int lineCount)
    {
        int digits = Math.Max(1, lineCount.ToString(CultureInfo.InvariantCulture).Length);
        string sample = new string('8', digits) + ">";
        Typeface typeface = new(LineNumbers.FontFamily, LineNumbers.FontStyle, LineNumbers.FontWeight, LineNumbers.FontStretch);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        FormattedText text = new(sample, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, LineNumbers.FontSize, Brushes.Transparent, dpi);
        double width = Math.Ceiling(text.Width) + 2;
        LineNumbers.MinWidth = width;
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_isDirty)
        {
            return true;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            "Discard unsaved changes?",
            "Unsaved Changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private void ApplyFoldView()
    {
        _suppressTextChange = true;
        if (_collapsedStarts.Count == 0)
        {
            ScheduleHighlight(preserveCaret: false);
        }
        else
        {
            ScheduleHighlight(preserveCaret: false, textOverride: BuildCollapsedText());
        }
        _suppressTextChange = false;

        UpdateFoldRegions();
        UpdateLineNumbers();
        UpdateCursor();
    }

    private void OnEditorPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (TryHandleRefreshShortcut(e))
        {
            return;
        }

        if (_collapsedStarts.Count == 0)
        {
            return;
        }

        int caretIndex = GetCaretOffset();
        int lineIndex = GetLineIndexFromOffset(GetEditorText(), caretIndex);
        string lineText = GetLineText(GetEditorText(), lineIndex);

        if (lineText == FoldPlaceholder)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Back && caretIndex == GetCharIndexFromLineStart(lineIndex))
        {
            if (lineIndex > 0 && GetLineText(GetEditorText(), lineIndex - 1) == FoldPlaceholder)
            {
                e.Handled = true;
            }
        }

        if (e.Key == System.Windows.Input.Key.Delete)
        {
            int lineStart = GetCharIndexFromLineStart(lineIndex);
            int lineLength = GetLineText(GetEditorText(), lineIndex).Length;
            if (caretIndex >= lineStart + lineLength && lineIndex + 1 < CountLines(GetEditorText()))
            {
                if (GetLineText(GetEditorText(), lineIndex + 1) == FoldPlaceholder)
                {
                    e.Handled = true;
                }
            }
        }
    }

    private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _ = TryHandleRefreshShortcut(e);
    }

    private bool TryHandleRefreshShortcut(System.Windows.Input.KeyEventArgs e)
    {
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == 0 ||
            e.Key != System.Windows.Input.Key.Enter)
        {
            return false;
        }

        e.Handled = true;
        _ = RefreshProjectScriptsAsync();
        return true;
    }

    private void OnEditorPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Point point = e.GetPosition(CodeEditor);
        TextPointer? pointer = CodeEditor.GetPositionFromPoint(point, false);
        if (pointer == null)
        {
            SetEditorHoverOpenCursor(false);
            return;
        }

        string text = GetEditorText();
        int offset = GetCharOffsetFromTextPointer(pointer);
        string? token = GetVstTokenAtOffset(text, offset, strictAtOffset: true);
        SetEditorHoverOpenCursor(!string.IsNullOrWhiteSpace(token));
    }

    private void OnEditorMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        SetEditorHoverOpenCursor(false);
    }

    private void SetEditorHoverOpenCursor(bool showOpenCursor)
    {
        if (_editorHoverShowsOpenCursor == showOpenCursor)
        {
            return;
        }

        _editorHoverShowsOpenCursor = showOpenCursor;
        System.Windows.Input.Mouse.OverrideCursor = showOpenCursor
            ? System.Windows.Input.Cursors.Arrow
            : null;
    }

    private async void OnEditorMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_engineWarmupTask != null)
        {
            await _engineWarmupTask;
        }

        if (_engineInterface == null)
        {
            AppendConsole("Engine not ready.");
            return;
        }

        Point point = e.GetPosition(CodeEditor);
        TextPointer? pointer = CodeEditor.GetPositionFromPoint(point, false) ??
                               CodeEditor.GetPositionFromPoint(point, true);
        string text = GetEditorText();
        int offset = pointer == null ? GetCaretOffset() : GetCharOffsetFromTextPointer(pointer);
        string? token = GetVstTokenForDoubleClick(text, offset);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        bool opened = false;
        try
        {
            opened = _engineInterface.Host.TryOpenVstEditor(token);
        }
        catch (Exception ex)
        {
            AppendConsole("Open VST editor failed: " + ex.Message);
            return;
        }

        if (opened)
        {
            AppendConsole("Opened VST editor: " + token);
            e.Handled = true;
        }
    }

    private void OnEditorContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _pendingContextVstToken = ResolveContextVstToken();
        if (OpenVstMenuItem == null)
        {
            return;
        }

        OpenVstMenuItem.IsEnabled = !string.IsNullOrWhiteSpace(_pendingContextVstToken);
        OpenVstMenuItem.Header = string.IsNullOrWhiteSpace(_pendingContextVstToken)
            ? "Open VST"
            : $"Open VST \"{_pendingContextVstToken}\"";
    }

    private async void OnOpenVstMenuItemClicked(object sender, RoutedEventArgs e)
    {
        string? token = _pendingContextVstToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (_engineWarmupTask != null)
        {
            await _engineWarmupTask;
        }

        if (_engineInterface == null)
        {
            AppendConsole("Engine not ready.");
            return;
        }

        bool opened = false;
        try
        {
            opened = _engineInterface.Host.TryOpenVstEditor(token);
        }
        catch (Exception ex)
        {
            AppendConsole("Open VST editor failed: " + ex.Message);
            return;
        }

        if (opened)
        {
            AppendConsole("Opened VST editor: " + token);
        }
        else
        {
            AppendConsole("VST not loaded in current script: " + token);
        }
    }

    private string? ResolveContextVstToken()
    {
        string selected = CodeEditor.Selection.Text?.Trim() ?? string.Empty;
        if (selected.Length > 0)
        {
            if (selected.Length >= 2 && selected[0] == '"' && selected[^1] == '"')
            {
                string unquoted = selected[1..^1].Trim();
                if (unquoted.Length > 0)
                {
                    return unquoted;
                }
            }

            return selected.Trim('"');
        }

        string text = GetEditorText();
        int offset = GetCaretOffset();
        return GetVstTokenAtOffset(text, offset, strictAtOffset: false);
    }

    private static string? GetVstTokenAtOffset(string text, int offset, bool strictAtOffset)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        int clamped = Math.Clamp(offset, 0, Math.Max(0, text.Length - 1));

        if (TryGetQuotedTokenAtOffset(text, clamped, out string? quoted))
        {
            return quoted;
        }

        if (!IsVstTokenChar(text[clamped]))
        {
            if (strictAtOffset)
            {
                return null;
            }

            if (clamped > 0 && IsVstTokenChar(text[clamped - 1]))
            {
                clamped--;
            }
            else
            {
                return null;
            }
        }

        int start = clamped;
        while (start > 0 && IsVstTokenChar(text[start - 1]))
        {
            start--;
        }

        int end = clamped;
        while (end < text.Length && IsVstTokenChar(text[end]))
        {
            end++;
        }

        if (end <= start)
        {
            return null;
        }

        return text[start..end];
    }

    private static string? GetVstTokenForDoubleClick(string text, int offset)
    {
        string? direct = GetVstTokenAtOffset(text, offset, strictAtOffset: true);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        if (!TryGetLineAtOffset(text, offset, out string? line))
        {
            return null;
        }

        Match match = VstCallRegex.Match(line);
        if (!match.Success)
        {
            return null;
        }

        string name = match.Groups[1].Value.Trim();
        return name.Length == 0 ? null : name;
    }

    private static bool TryGetLineAtOffset(string text, int offset, out string line)
    {
        line = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        int clamped = Math.Clamp(offset, 0, text.Length);
        int lineStart = clamped > 0 ? text.LastIndexOf('\n', clamped - 1) + 1 : 0;
        int lineEnd = text.IndexOf('\n', clamped);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        if (lineEnd <= lineStart)
        {
            return false;
        }

        line = text[lineStart..lineEnd];
        return true;
    }

    private static bool TryGetQuotedTokenAtOffset(string text, int offset, out string? token)
    {
        token = null;
        int lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        int nextNewLine = text.IndexOf('\n', offset);
        int lineEnd = nextNewLine < 0 ? text.Length : nextNewLine;
        if (lineStart >= lineEnd)
        {
            return false;
        }

        int quoteStart = -1;
        for (int i = lineStart; i <= offset && i < lineEnd; i++)
        {
            if (text[i] == '"')
            {
                quoteStart = i;
            }
        }

        if (quoteStart < 0)
        {
            return false;
        }

        int quoteEnd = -1;
        for (int i = quoteStart + 1; i < lineEnd; i++)
        {
            if (text[i] == '"')
            {
                quoteEnd = i;
                break;
            }
        }

        if (quoteEnd < 0 || offset < quoteStart || offset > quoteEnd)
        {
            return false;
        }

        string value = text[(quoteStart + 1)..quoteEnd].Trim();
        if (value.Length == 0)
        {
            return false;
        }

        token = value;
        return true;
    }

    private static bool IsVstTokenChar(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_' || ch == '-';
    }

    private string BuildCollapsedText()
    {
        _collapsedBlocks.Clear();
        string[] lines = _fullText.Replace("\r\n", "\n").Split('\n');
        List<FoldRegion> collapsed = GetTopLevelCollapsedRegions();
        collapsed.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));

        List<string> output = new();
        int regionIndex = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (regionIndex < collapsed.Count && collapsed[regionIndex].StartLine == i)
            {
                FoldRegion region = collapsed[regionIndex];
                output.Add(lines[i]);
                output.Add(FoldPlaceholder);
                if (region.EndLine > region.StartLine + 1)
                {
                    int count = region.EndLine - region.StartLine - 1;
                    string[] block = new string[count];
                    Array.Copy(lines, region.StartLine + 1, block, 0, count);
                    _collapsedBlocks.Add(block);
                }
                else
                {
                    _collapsedBlocks.Add(Array.Empty<string>());
                }

                i = region.EndLine - 1;
                regionIndex++;
                continue;
            }

            output.Add(lines[i]);
        }

        return string.Join("\n", output);
    }

    private List<int> BuildDisplayMap()
    {
        List<int> map = new();
        if (_collapsedStarts.Count == 0)
        {
            int count = Math.Max(1, CountLines(GetEditorText()));
            for (int i = 0; i < count; i++)
            {
                map.Add(i);
            }
            return map;
        }

        int lineCount = _fullText.Replace("\r\n", "\n").Split('\n').Length;
        Dictionary<int, FoldRegion> regionLookup = new();
        foreach (FoldRegion region in GetTopLevelCollapsedRegions())
        {
            regionLookup[region.StartLine] = region;
        }

        int line = 0;
        while (line < lineCount)
        {
            map.Add(line);
            if (regionLookup.TryGetValue(line, out FoldRegion? region) && _collapsedStarts.Contains(line))
            {
                map.Add(-1);
                line = region.EndLine + 1;
                continue;
            }

            line++;
        }

        return map;
    }

    private List<FoldRegion> GetTopLevelCollapsedRegions()
    {
        List<FoldRegion> collapsed = new();
        foreach (FoldRegion region in _foldRegions)
        {
            if (_collapsedStarts.Contains(region.StartLine))
            {
                collapsed.Add(region);
            }
        }

        collapsed.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));
        List<FoldRegion> topLevel = new();
        foreach (FoldRegion region in collapsed)
        {
            bool isNested = false;
            foreach (FoldRegion existing in topLevel)
            {
                if (region.StartLine > existing.StartLine && region.EndLine <= existing.EndLine)
                {
                    isNested = true;
                    break;
                }
            }

            if (!isNested)
            {
                topLevel.Add(region);
            }
        }

        return topLevel;
    }

    private string ExpandCollapsedText(string displayText)
    {
        if (_collapsedBlocks.Count == 0)
        {
            return displayText;
        }

        string[] lines = displayText.Replace("\r\n", "\n").Split('\n');
        List<string> output = new();
        int blockIndex = 0;

        foreach (string line in lines)
        {
            if (line == FoldPlaceholder && blockIndex < _collapsedBlocks.Count)
            {
                output.AddRange(_collapsedBlocks[blockIndex]);
                blockIndex++;
            }
            else
            {
                output.Add(line);
            }
        }

        while (blockIndex < _collapsedBlocks.Count)
        {
            output.AddRange(_collapsedBlocks[blockIndex]);
            blockIndex++;
        }

        return string.Join("\n", output);
    }

    private static string GetLineText(string text, int lineIndex)
    {
        if (lineIndex < 0)
        {
            return string.Empty;
        }

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        if (lineIndex >= lines.Length)
        {
            return string.Empty;
        }

        return lines[lineIndex];
    }

    private void UpdateFoldRegions()
    {
        _foldRegions.Clear();
        string[] lines = _fullText.Replace("\r\n", "\n").Split('\n');
        Stack<int> openBraces = new();

        for (int i = 0; i < lines.Length; i++)
        {
            foreach (char ch in lines[i])
            {
                if (ch == '{')
                {
                    openBraces.Push(i);
                }
                else if (ch == '}')
                {
                    if (openBraces.Count == 0)
                    {
                        continue;
                    }

                    int startLine = openBraces.Pop();
                    if (i > startLine)
                    {
                        _foldRegions.Add(new FoldRegion(startLine, i));
                    }
                }
            }
        }

        _collapsedStarts.RemoveWhere(start => _foldRegions.Find(region => region.StartLine == start) == null);
    }

    private FoldRegion? FindRegionByStartLine(int line)
    {
        foreach (FoldRegion region in _foldRegions)
        {
            if (region.StartLine == line)
            {
                return region;
            }
        }

        return null;
    }

    private char GetMarkerForLine(int line)
    {
        foreach (FoldRegion region in _foldRegions)
        {
            if (region.StartLine == line)
            {
                return _collapsedStarts.Contains(line) ? '>' : 'v';
            }
        }

        return ' ';
    }

    private void InitializeStyling()
    {
        _stylingApi.EnableGlow(true);
        _stylingApi.SetDefaultColor(Color.FromRgb(252, 252, 252));
        _stylingApi.SetKeywordColor(Color.FromRgb(70, 170, 255));
        _stylingApi.SetTypeColor(Color.FromRgb(54, 232, 204));
        _stylingApi.SetStringColor(Color.FromRgb(255, 184, 92));
        _stylingApi.SetCommentColor(Color.FromRgb(186, 193, 204));
        _stylingApi.SetNumberColor(Color.FromRgb(255, 121, 198));
    }

    private void ScheduleHighlight(bool preserveCaret, string? textOverride = null)
    {
        _pendingPreserveCaret = preserveCaret;
        if (preserveCaret)
        {
            _pendingCaretCharOffset = GetCaretOffset();
            _pendingSelectionStartCharOffset = GetSelectionStartCharOffset();
            _pendingSelectionEndCharOffset = GetSelectionEndCharOffset();
            if (_editorScroll != null)
            {
                _pendingVerticalOffset = _editorScroll.VerticalOffset;
                _pendingHorizontalOffset = _editorScroll.HorizontalOffset;
            }
        }
        _pendingHighlightText = textOverride ?? _fullText;
        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    private void OnHighlightTimerTick(object? sender, EventArgs e)
    {
        if (_isApplyingHighlight)
        {
            return;
        }

        _highlightTimer.Stop();
        _isApplyingHighlight = true;
        _suppressTextChange = true;
            CodeEditor.Document = _stylingApi.BuildDocument(_pendingHighlightText, EditorLineHeight, Brushes.Gainsboro);
        _suppressTextChange = false;
        _isApplyingHighlight = false;

        if (_pendingPreserveCaret)
        {
            TextPointer? caretPointer = GetTextPointerAtCharOffset(_pendingCaretCharOffset);
            if (caretPointer != null)
            {
                CodeEditor.CaretPosition = caretPointer;
            }

            if (_pendingSelectionStartCharOffset != _pendingSelectionEndCharOffset)
            {
                TextPointer? selectionStart = GetTextPointerAtCharOffset(_pendingSelectionStartCharOffset);
                TextPointer? selectionEnd = GetTextPointerAtCharOffset(_pendingSelectionEndCharOffset);
                if (selectionStart != null && selectionEnd != null)
                {
                    CodeEditor.Selection.Select(selectionStart, selectionEnd);
                }
            }

            if (_editorScroll != null)
            {
                _editorScroll.ScrollToHorizontalOffset(_pendingHorizontalOffset);
                _editorScroll.ScrollToVerticalOffset(_pendingVerticalOffset);
            }
        }

        // Ensure UI state (line numbers/cursor/folds) matches freshly applied document text
        // even when loading/opening files with suppressed text change events.
        UpdateFoldRegions();
        UpdateLineNumbers();
        UpdateCursor();

    }

    private int GetSelectionStartCharOffset()
    {
        return GetNormalizedCharOffset(CodeEditor.Selection.Start);
    }

    private int GetSelectionEndCharOffset()
    {
        return GetNormalizedCharOffset(CodeEditor.Selection.End);
    }

    private TextPointer? GetTextPointerAtCharOffset(int offset)
    {
        TextPointer start = CodeEditor.Document.ContentStart;
        TextPointer end = CodeEditor.Document.ContentEnd;
        int maxSymbolOffset = start.GetOffsetToPosition(end);
        int targetCharOffset = Math.Clamp(offset, 0, GetNormalizedCharOffset(end));

        int low = 0;
        int high = maxSymbolOffset;
        TextPointer? best = start;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            TextPointer? candidate = start.GetPositionAtOffset(mid, LogicalDirection.Forward);
            if (candidate == null)
            {
                high = mid - 1;
                continue;
            }

            int candidateCharOffset = GetNormalizedCharOffset(candidate);
            if (candidateCharOffset < targetCharOffset)
            {
                best = candidate;
                low = mid + 1;
            }
            else
            {
                best = candidate;
                high = mid - 1;
            }
        }

        if (best == null)
        {
            return start;
        }

        int bestOffset = GetNormalizedCharOffset(best);
        if (bestOffset > targetCharOffset)
        {
            TextPointer? previous = best.GetPositionAtOffset(-1, LogicalDirection.Backward);
            while (previous != null)
            {
                int previousOffset = GetNormalizedCharOffset(previous);
                if (previousOffset <= targetCharOffset)
                {
                    return previous;
                }

                previous = previous.GetPositionAtOffset(-1, LogicalDirection.Backward);
            }

            return start;
        }

        TextPointer? nextPos = best;
        while (nextPos != null)
        {
            int nextOffset = GetNormalizedCharOffset(nextPos);
            if (nextOffset >= targetCharOffset)
            {
                return nextPos;
            }

            nextPos = nextPos.GetNextContextPosition(LogicalDirection.Forward);
        }

        return end;
    }

    private string GetEditorText()
    {
        TextRange range = new(CodeEditor.Document.ContentStart, CodeEditor.Document.ContentEnd);
        return NormalizeText(range.Text);
    }

    private static string NormalizeText(string text)
    {
        return text.Replace("\r\n", "\n");
    }

    private int GetCaretOffset()
    {
        return GetNormalizedCharOffset(CodeEditor.CaretPosition);
    }

    private int GetNormalizedCharOffset(TextPointer pointer)
    {
        TextRange range = new(CodeEditor.Document.ContentStart, pointer);
        return NormalizeText(range.Text).Length;
    }

    private (int line, int column) GetCaretLineColumn()
    {
        string text = GetEditorText();
        int offset = GetCaretOffset();
        int line = 0;
        int column = 0;
        for (int i = 0; i < text.Length && i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 0;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }

    private int GetLineIndexFromPoint(Point point)
    {
        TextPointer? pointer = CodeEditor.GetPositionFromPoint(point, true);
        if (pointer == null)
        {
            return -1;
        }

        TextRange range = new(CodeEditor.Document.ContentStart, pointer);
        string text = NormalizeText(range.Text);
        return CountLines(text) - 1;
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        int count = 1;
        foreach (char ch in text)
        {
            if (ch == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static int GetLineIndexFromOffset(string text, int offset)
    {
        int line = 0;
        for (int i = 0; i < text.Length && i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private int GetCharIndexFromLineStart(int lineIndex)
    {
        string text = GetEditorText();
        if (lineIndex <= 0)
        {
            return 0;
        }

        int line = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (line == lineIndex)
            {
                return i;
            }

            if (text[i] == '\n')
            {
                line++;
            }
        }

        return text.Length;
    }

    private int GetCharOffsetFromTextPointer(TextPointer pointer)
    {
        return GetNormalizedCharOffset(pointer);
    }


    private sealed record FoldRegion(int StartLine, int EndLine);

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }

            ScrollViewer? nested = FindScrollViewer(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
