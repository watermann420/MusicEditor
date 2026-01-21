using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Event arguments for shortcut recorded event
/// </summary>
public class ShortcutRecordedEventArgs : EventArgs
{
    public Key Key { get; }
    public ModifierKeys Modifiers { get; }

    public ShortcutRecordedEventArgs(Key key, ModifierKeys modifiers)
    {
        Key = key;
        Modifiers = modifiers;
    }
}

/// <summary>
/// A control for recording keyboard shortcuts
/// </summary>
public partial class ShortcutRecorder : UserControl
{
    private bool _isRecording;
    private Storyboard? _recordingAnimation;

    /// <summary>
    /// Event raised when a shortcut has been recorded
    /// </summary>
    public event EventHandler<ShortcutRecordedEventArgs>? ShortcutRecorded;

    /// <summary>
    /// Event raised when the shortcut is cleared
    /// </summary>
    public event EventHandler? ShortcutCleared;

    /// <summary>
    /// Dependency property for the current key
    /// </summary>
    public static readonly DependencyProperty CurrentKeyProperty =
        DependencyProperty.Register(
            nameof(CurrentKey),
            typeof(Key),
            typeof(ShortcutRecorder),
            new FrameworkPropertyMetadata(Key.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentKeyChanged));

    /// <summary>
    /// Dependency property for the current modifiers
    /// </summary>
    public static readonly DependencyProperty CurrentModifiersProperty =
        DependencyProperty.Register(
            nameof(CurrentModifiers),
            typeof(ModifierKeys),
            typeof(ShortcutRecorder),
            new FrameworkPropertyMetadata(ModifierKeys.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentModifiersChanged));

    /// <summary>
    /// Dependency property for whether the shortcut is customizable
    /// </summary>
    public static readonly DependencyProperty IsCustomizableProperty =
        DependencyProperty.Register(
            nameof(IsCustomizable),
            typeof(bool),
            typeof(ShortcutRecorder),
            new PropertyMetadata(true, OnIsCustomizableChanged));

    /// <summary>
    /// Dependency property for the placeholder text
    /// </summary>
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(ShortcutRecorder),
            new PropertyMetadata("Click to record shortcut", OnPlaceholderTextChanged));

    /// <summary>
    /// Gets or sets the current key
    /// </summary>
    public Key CurrentKey
    {
        get => (Key)GetValue(CurrentKeyProperty);
        set => SetValue(CurrentKeyProperty, value);
    }

    /// <summary>
    /// Gets or sets the current modifiers
    /// </summary>
    public ModifierKeys CurrentModifiers
    {
        get => (ModifierKeys)GetValue(CurrentModifiersProperty);
        set => SetValue(CurrentModifiersProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the shortcut can be customized
    /// </summary>
    public bool IsCustomizable
    {
        get => (bool)GetValue(IsCustomizableProperty);
        set => SetValue(IsCustomizableProperty, value);
    }

    /// <summary>
    /// Gets or sets the placeholder text shown when no shortcut is set
    /// </summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>
    /// Gets whether the control is currently recording
    /// </summary>
    public bool IsRecording => _isRecording;

    public ShortcutRecorder()
    {
        InitializeComponent();

        _recordingAnimation = FindResource("RecordingAnimation") as Storyboard;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopRecording();
    }

    private static void OnCurrentKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShortcutRecorder recorder)
        {
            recorder.UpdateDisplay();
        }
    }

    private static void OnCurrentModifiersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShortcutRecorder recorder)
        {
            recorder.UpdateDisplay();
        }
    }

    private static void OnIsCustomizableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShortcutRecorder recorder)
        {
            recorder.UpdateCustomizableState();
        }
    }

    private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShortcutRecorder recorder)
        {
            recorder.UpdateDisplay();
        }
    }

    private void UpdateCustomizableState()
    {
        IsEnabled = IsCustomizable;
        Opacity = IsCustomizable ? 1.0 : 0.6;

        if (!IsCustomizable)
        {
            StopRecording();
        }
    }

    private void UpdateDisplay()
    {
        if (_isRecording)
        {
            ShortcutText.Text = "Press a key combination...";
            ShortcutText.Foreground = (Brush)FindResource("AccentBrush");
            ClearButton.Visibility = Visibility.Collapsed;
        }
        else if (CurrentKey == Key.None)
        {
            ShortcutText.Text = PlaceholderText;
            ShortcutText.Foreground = (Brush)FindResource("SecondaryForegroundBrush");
            ClearButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShortcutText.Text = GetDisplayString(CurrentKey, CurrentModifiers);
            ShortcutText.Foreground = (Brush)FindResource("ForegroundBrush");
            ClearButton.Visibility = IsCustomizable ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static string GetDisplayString(Key key, ModifierKeys modifiers)
    {
        if (key == Key.None)
            return "Not Set";

        var sb = new StringBuilder();

        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            sb.Append("Ctrl+");
        if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            sb.Append("Alt+");
        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            sb.Append("Shift+");

        sb.Append(GetKeyDisplayName(key));

        return sb.ToString();
    }

    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemBackslash => "\\",
            Key.OemTilde => "`",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            Key.NumPad0 => "Num 0",
            Key.NumPad1 => "Num 1",
            Key.NumPad2 => "Num 2",
            Key.NumPad3 => "Num 3",
            Key.NumPad4 => "Num 4",
            Key.NumPad5 => "Num 5",
            Key.NumPad6 => "Num 6",
            Key.NumPad7 => "Num 7",
            Key.NumPad8 => "Num 8",
            Key.NumPad9 => "Num 9",
            Key.Multiply => "Num *",
            Key.Add => "Num +",
            Key.Subtract => "Num -",
            Key.Divide => "Num /",
            Key.Decimal => "Num .",
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Escape => "Esc",
            Key.Prior => "Page Up",
            Key.Next => "Page Down",
            _ => key.ToString()
        };
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsCustomizable)
            return;

        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        if (!IsCustomizable || _isRecording)
            return;

        _isRecording = true;

        // Visual feedback
        MainBorder.BorderBrush = (Brush)FindResource("AccentBrush");
        MainBorder.BorderThickness = new Thickness(2);
        RecordingIndicator.Visibility = Visibility.Visible;

        // Start animation
        _recordingAnimation?.Begin(RecordingIndicator, true);

        UpdateDisplay();

        // Capture keyboard
        Focus();
        Keyboard.Focus(this);
        CaptureMouse();

        // Subscribe to key events
        PreviewKeyDown += OnPreviewKeyDown;
        LostFocus += OnLostFocus;
        LostMouseCapture += OnLostMouseCapture;
    }

    private void StopRecording()
    {
        if (!_isRecording)
            return;

        _isRecording = false;

        // Visual feedback
        MainBorder.BorderBrush = (Brush)FindResource("BorderBrush");
        MainBorder.BorderThickness = new Thickness(1);
        RecordingIndicator.Visibility = Visibility.Collapsed;

        // Stop animation
        _recordingAnimation?.Stop(RecordingIndicator);

        UpdateDisplay();

        // Release capture
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        // Unsubscribe from events
        PreviewKeyDown -= OnPreviewKeyDown;
        LostFocus -= OnLostFocus;
        LostMouseCapture -= OnLostMouseCapture;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecording)
            return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape cancels recording
        if (key == Key.Escape)
        {
            StopRecording();
            return;
        }

        // Ignore modifier keys alone
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        // Get modifiers
        var modifiers = Keyboard.Modifiers;

        // Record the shortcut
        CurrentKey = key;
        CurrentModifiers = modifiers;

        StopRecording();

        // Raise event
        ShortcutRecorded?.Invoke(this, new ShortcutRecordedEventArgs(key, modifiers));
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        StopRecording();
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        // Only stop if we lost capture unexpectedly
        if (_isRecording && !IsMouseOver)
        {
            StopRecording();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (!IsCustomizable)
            return;

        CurrentKey = Key.None;
        CurrentModifiers = ModifierKeys.None;

        UpdateDisplay();

        ShortcutCleared?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        // Release mouse capture on click outside
        if (_isRecording && IsMouseCaptured && !IsMouseOver)
        {
            StopRecording();
        }
    }
}
