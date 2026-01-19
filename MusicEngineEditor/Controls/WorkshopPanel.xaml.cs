using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Interactive workshop panel with step-by-step tutorials like Strudel.cc
/// </summary>
public partial class WorkshopPanel : UserControl
{
    #region Events

    /// <summary>
    /// Occurs when the user clicks Run on a code example
    /// </summary>
    public event EventHandler<WorkshopCodeEventArgs>? OnRunCode;

    /// <summary>
    /// Occurs when the user clicks Copy on a code example
    /// </summary>
    public event EventHandler<WorkshopCodeEventArgs>? OnCopyCode;

    /// <summary>
    /// Occurs when the user clicks Insert on a code example
    /// </summary>
    public event EventHandler<WorkshopCodeEventArgs>? OnInsertCode;

    #endregion

    #region Private Fields

    private readonly List<WorkshopLesson> _lessons = new();
    private readonly Dictionary<string, Button> _navButtons = new();
    private int _currentLessonIndex = 0;

    #endregion

    #region Constructor

    public WorkshopPanel()
    {
        InitializeComponent();
        InitializeLessons();
        InitializeNavButtons();
        ShowLesson(0);
    }

    #endregion

    #region Lesson Initialization

    private void InitializeLessons()
    {
        _lessons.Add(new WorkshopLesson
        {
            Id = "welcome",
            Title = "Welcome to MusicEngine",
            Subtitle = "Learn live coding music with C#",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "MusicEngine is a live coding environment for creating music using C# scripts. " +
                              "You can create synthesizers, load VST plugins, sequence patterns, and control everything with MIDI."
                },
                new()
                {
                    Type = SectionType.Heading,
                    Content = "What you'll learn"
                },
                new()
                {
                    Type = SectionType.BulletList,
                    Items = new List<string>
                    {
                        "Create and control synthesizers",
                        "Connect MIDI keyboards and controllers",
                        "Load VST plugins like Vital and Serum",
                        "Build patterns and sequences",
                        "Live code music in real-time"
                    }
                },
                new()
                {
                    Type = SectionType.Info,
                    Content = "Press Ctrl+Enter to run code, Escape to stop. Let's get started!"
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "first-sound",
            Title = "First Sounds",
            Subtitle = "Create your first synthesizer",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "Let's create your first sound! The simplest way to make noise in MusicEngine is to create a synthesizer."
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = "// Create a simple synth\nvar synth = CreateSynth();",
                    Description = "This creates a basic synthesizer with default settings."
                },
                new()
                {
                    Type = SectionType.Text,
                    Content = "Now let's play a note! MIDI notes go from 0 to 127, where 60 is middle C."
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = "// Create synth and play middle C\nvar synth = CreateSynth();\nsynth.NoteOn(60, 100);  // Note 60 = C4, velocity 100",
                    Description = "Click Run to hear your first note!"
                },
                new()
                {
                    Type = SectionType.Tip,
                    Content = "The velocity (0-127) controls how loud the note is. Try different values!"
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "notes",
            Title = "Playing Notes",
            Subtitle = "Melodies and timing",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "To play melodies, we need to turn notes on and off with timing. Use await Task.Delay() to wait between notes."
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Play a simple melody
var synth = CreateSynth();

synth.NoteOn(60, 100);  // C
await Task.Delay(300);
synth.NoteOff(60);

synth.NoteOn(64, 100);  // E
await Task.Delay(300);
synth.NoteOff(64);

synth.NoteOn(67, 100);  // G
await Task.Delay(500);
synth.NoteOff(67);",
                    Description = "A C major arpeggio"
                },
                new()
                {
                    Type = SectionType.Heading,
                    Content = "Common MIDI Notes"
                },
                new()
                {
                    Type = SectionType.Table,
                    TableData = new List<string[]>
                    {
                        new[] { "Note", "MIDI Number" },
                        new[] { "C4 (Middle C)", "60" },
                        new[] { "D4", "62" },
                        new[] { "E4", "64" },
                        new[] { "F4", "65" },
                        new[] { "G4", "67" },
                        new[] { "A4", "69" },
                        new[] { "B4", "71" },
                        new[] { "C5", "72" }
                    }
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "midi-basics",
            Title = "MIDI Basics",
            Subtitle = "Understanding MIDI devices",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "MIDI (Musical Instrument Digital Interface) lets you connect keyboards, controllers, and other devices. " +
                              "MusicEngine automatically detects connected MIDI devices."
                },
                new()
                {
                    Type = SectionType.Heading,
                    Content = "Listing MIDI Devices"
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// List all MIDI input devices
var inputCount = Engine.GetMidiInputCount();
Print($""Found {inputCount} MIDI inputs"");

for (int i = 0; i < inputCount; i++)
{
    Print($""  Device {i}: {Engine.GetMidiInputName(i)}"");
}",
                    Description = "See what MIDI devices are connected"
                },
                new()
                {
                    Type = SectionType.Info,
                    Content = "Check the MIDI panel on the right side of the editor to see your devices with their channel numbers."
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "midi-routing",
            Title = "MIDI Routing",
            Subtitle = "Connect your keyboard",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "Now let's connect your MIDI keyboard to control a synthesizer!"
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Route MIDI keyboard to synth
var synth = CreateSynth();

// Route MIDI device 0 to the synth
midi.device(0).route(synth);

Print(""Play your keyboard!"");",
                    Description = "Your keyboard now controls the synth"
                },
                new()
                {
                    Type = SectionType.Heading,
                    Content = "Mapping MIDI CC"
                },
                new()
                {
                    Type = SectionType.Text,
                    Content = "You can also map MIDI control change (CC) messages to synth parameters:"
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Map CC to filter cutoff
var synth = CreateSynth();
midi.device(0).route(synth);

// Map CC 1 (mod wheel) to cutoff
midi.device(0).cc(1).to(synth, ""cutoff"");",
                    Description = "Control the filter with your mod wheel"
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "vst-basics",
            Title = "Loading VST Plugins",
            Subtitle = "Use your favorite synths",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "MusicEngine can load VST2 and VST3 plugins. This lets you use professional synthesizers like Vital, Serum, or any other VST instrument."
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Load a VST plugin by name
var plugin = vst.load(""Vital"");

if (plugin != null)
{
    Print(""Plugin loaded!"");
    plugin.ShowEditor();  // Open the plugin window
}
else
{
    Print(""Plugin not found"");
}",
                    Description = "Load and open a VST plugin"
                },
                new()
                {
                    Type = SectionType.Warning,
                    Content = "Make sure your VST plugins are installed in a standard location like C:\\Program Files\\Common Files\\VST3"
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "vst-vital",
            Title = "Using Vital",
            Subtitle = "Free wavetable synthesizer",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "Vital is a free, powerful wavetable synthesizer. Download it from vital.audio if you haven't already."
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Load Vital and connect MIDI
var vital = vst.load(""Vital"");

if (vital != null)
{
    // Show the Vital interface
    vital.ShowEditor();

    // Route your MIDI keyboard
    vital.from(0);

    Print(""Vital ready! Play your keyboard."");
}
else
{
    Print(""Install Vital from vital.audio"");
}",
                    Description = "Complete Vital setup"
                },
                new()
                {
                    Type = SectionType.Heading,
                    Content = "Playing Notes Programmatically"
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Play notes through Vital
var vital = vst.load(""Vital"");

if (vital != null)
{
    vital.ShowEditor();

    // Play a chord
    vital.NoteOn(60, 100);  // C
    vital.NoteOn(64, 100);  // E
    vital.NoteOn(67, 100);  // G

    await Task.Delay(1000);

    vital.AllNotesOff();
}",
                    Description = "Play chords through Vital"
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "patterns",
            Title = "Creating Patterns",
            Subtitle = "Build drum loops and melodies",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "Patterns let you create repeating musical sequences. You can program drum beats, bass lines, or melodies."
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Create a simple drum pattern
var synth = CreateSynth();
var pattern = CreatePattern(synth);

// Add notes: (note, beat, duration, velocity)
pattern.Note(36, 0, 0.25, 100);     // Kick on beat 1
pattern.Note(38, 1, 0.25, 100);     // Snare on beat 2
pattern.Note(36, 2, 0.25, 100);     // Kick on beat 3
pattern.Note(38, 3, 0.25, 100);     // Snare on beat 4

pattern.Loop = true;
pattern.Play();",
                    Description = "A basic 4-on-the-floor beat"
                },
                new()
                {
                    Type = SectionType.Tip,
                    Content = "Note 36 = Kick drum, Note 38 = Snare in General MIDI"
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "sequencing",
            Title = "Sequencing",
            Subtitle = "Timing and the sequencer",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = SectionType.Text,
                    Content = "The Sequencer controls the global tempo and timing. You can schedule events to happen at specific beats."
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Set the tempo
Sequencer.Bpm = 120;

// Schedule events
Sequencer.Schedule(0, () => Print(""Beat 1""));
Sequencer.Schedule(1, () => Print(""Beat 2""));
Sequencer.Schedule(2, () => Print(""Beat 3""));
Sequencer.Schedule(3, () => Print(""Beat 4""));

Sequencer.Start();",
                    Description = "Schedule events at specific beats"
                },
                new()
                {
                    Type = SectionType.Heading,
                    Content = "Combining Everything"
                },
                new()
                {
                    Type = SectionType.Code,
                    Content = @"// Full example: synth + pattern + tempo
Sequencer.Bpm = 140;

var synth = CreateSynth();
synth.SetParameter(""waveform"", 2);  // Sawtooth

var pattern = CreatePattern(synth);
pattern.Note(48, 0, 0.5, 100);    // C
pattern.Note(48, 1, 0.5, 100);    // C
pattern.Note(55, 2, 0.5, 100);    // G
pattern.Note(53, 3, 0.5, 100);    // F

pattern.Loop = true;
pattern.Play();
Sequencer.Start();",
                    Description = "A complete musical example"
                },
                new()
                {
                    Type = SectionType.Success,
                    Content = "Congratulations! You've completed the workshop. Keep experimenting and have fun making music!"
                }
            }
        });
    }

    private void InitializeNavButtons()
    {
        _navButtons["welcome"] = Nav_Welcome;
        _navButtons["first-sound"] = Nav_FirstSound;
        _navButtons["notes"] = Nav_Notes;
        _navButtons["midi-basics"] = Nav_MidiBasics;
        _navButtons["midi-routing"] = Nav_MidiRouting;
        _navButtons["vst-basics"] = Nav_VstBasics;
        _navButtons["vst-vital"] = Nav_VstVital;
        _navButtons["patterns"] = Nav_Patterns;
        _navButtons["sequencing"] = Nav_Sequencing;
    }

    #endregion

    #region Navigation

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var lessonId = button.Name.Replace("Nav_", "").ToLower() switch
            {
                "welcome" => "welcome",
                "firstsound" => "first-sound",
                "notes" => "notes",
                "midibasics" => "midi-basics",
                "midirouting" => "midi-routing",
                "vstbasics" => "vst-basics",
                "vstvital" => "vst-vital",
                "patterns" => "patterns",
                "sequencing" => "sequencing",
                _ => "welcome"
            };

            var index = _lessons.FindIndex(l => l.Id == lessonId);
            if (index >= 0)
            {
                ShowLesson(index);
            }
        }
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLessonIndex > 0)
        {
            ShowLesson(_currentLessonIndex - 1);
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLessonIndex < _lessons.Count - 1)
        {
            ShowLesson(_currentLessonIndex + 1);
        }
    }

    private void ShowLesson(int index)
    {
        if (index < 0 || index >= _lessons.Count) return;

        _currentLessonIndex = index;
        var lesson = _lessons[index];

        // Update navigation highlighting
        foreach (var kvp in _navButtons)
        {
            kvp.Value.Tag = kvp.Key == lesson.Id ? "Active" : null;
        }

        // Update navigation buttons
        PrevButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index < _lessons.Count - 1;
        ProgressText.Text = $"{index + 1} / {_lessons.Count}";

        // Build content
        ContentPanel.Children.Clear();

        // Title
        var titleBlock = new TextBlock
        {
            Text = lesson.Title,
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = FindResource("BrightForegroundBrush") as WpfBrush,
            Margin = new Thickness(0, 0, 0, 4)
        };
        ContentPanel.Children.Add(titleBlock);

        // Subtitle
        if (!string.IsNullOrEmpty(lesson.Subtitle))
        {
            var subtitleBlock = new TextBlock
            {
                Text = lesson.Subtitle,
                FontSize = 14,
                Foreground = FindResource("SecondaryForegroundBrush") as WpfBrush,
                Margin = new Thickness(0, 0, 0, 24)
            };
            ContentPanel.Children.Add(subtitleBlock);
        }

        // Sections
        foreach (var section in lesson.Sections)
        {
            RenderSection(section);
        }
    }

    private void RenderSection(LessonSection section)
    {
        switch (section.Type)
        {
            case SectionType.Text:
                RenderText(section.Content);
                break;
            case SectionType.Heading:
                RenderHeading(section.Content);
                break;
            case SectionType.Code:
                RenderCode(section.Content, section.Description);
                break;
            case SectionType.BulletList:
                RenderBulletList(section.Items!);
                break;
            case SectionType.Table:
                RenderTable(section.TableData!);
                break;
            case SectionType.Tip:
                RenderCallout(section.Content, "Tip", "#4B6EAF");
                break;
            case SectionType.Warning:
                RenderCallout(section.Content, "Warning", "#E9B85B");
                break;
            case SectionType.Info:
                RenderCallout(section.Content, "Info", "#6F737A");
                break;
            case SectionType.Success:
                RenderCallout(section.Content, "Success", "#6AAB73");
                break;
        }
    }

    private void RenderText(string content)
    {
        var textBlock = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 22,
            Foreground = FindResource("ForegroundBrush") as WpfBrush,
            Margin = new Thickness(0, 0, 0, 16)
        };
        ContentPanel.Children.Add(textBlock);
    }

    private void RenderHeading(string content)
    {
        var heading = new TextBlock
        {
            Text = content,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindResource("BrightForegroundBrush") as WpfBrush,
            Margin = new Thickness(0, 16, 0, 12)
        };
        ContentPanel.Children.Add(heading);
    }

    private void RenderCode(string code, string? description)
    {
        var card = new Border
        {
            Background = FindResource("CardBackgroundBrush") as WpfBrush,
            BorderBrush = FindResource("BorderBrush") as WpfBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 16),
            Padding = new Thickness(16)
        };

        var stack = new StackPanel();

        // Description
        if (!string.IsNullOrEmpty(description))
        {
            var descBlock = new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = FindResource("SecondaryForegroundBrush") as WpfBrush,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(descBlock);
        }

        // Code block
        var codeBackground = new Border
        {
            Background = FindResource("CodeBackgroundBrush") as WpfBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12)
        };

        var codeBlock = new TextBlock
        {
            Text = code,
            FontFamily = new WpfFontFamily("JetBrains Mono, Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            Foreground = FindResource("ForegroundBrush") as WpfBrush,
            TextWrapping = TextWrapping.Wrap
        };
        codeBackground.Child = codeBlock;
        stack.Children.Add(codeBackground);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var runButton = new Button
        {
            Content = "Run",
            Style = FindResource("WorkshopRunButtonStyle") as Style,
            Tag = code,
            Margin = new Thickness(0, 0, 8, 0)
        };
        runButton.Click += (s, e) => OnRunCode?.Invoke(this, new WorkshopCodeEventArgs(code));
        buttonPanel.Children.Add(runButton);

        var copyButton = new Button
        {
            Content = "Copy",
            Style = FindResource("WorkshopCopyButtonStyle") as Style,
            Tag = code,
            Margin = new Thickness(0, 0, 8, 0)
        };
        copyButton.Click += (s, e) =>
        {
            try
            {
                System.Windows.Clipboard.SetText(code);
                OnCopyCode?.Invoke(this, new WorkshopCodeEventArgs(code));
            }
            catch { }
        };
        buttonPanel.Children.Add(copyButton);

        var insertButton = new Button
        {
            Content = "Insert",
            Style = FindResource("WorkshopCopyButtonStyle") as Style,
            Tag = code
        };
        insertButton.Click += (s, e) => OnInsertCode?.Invoke(this, new WorkshopCodeEventArgs(code));
        buttonPanel.Children.Add(insertButton);

        stack.Children.Add(buttonPanel);
        card.Child = stack;
        ContentPanel.Children.Add(card);
    }

    private void RenderBulletList(List<string> items)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

        foreach (var item in items)
        {
            var itemPanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

            var bullet = new TextBlock
            {
                Text = "\u2022",
                FontSize = 14,
                Foreground = FindResource("AccentBrush") as WpfBrush,
                Width = 20
            };
            itemPanel.Children.Add(bullet);

            var text = new TextBlock
            {
                Text = item,
                FontSize = 14,
                Foreground = FindResource("ForegroundBrush") as WpfBrush,
                TextWrapping = TextWrapping.Wrap
            };
            itemPanel.Children.Add(text);

            stack.Children.Add(itemPanel);
        }

        ContentPanel.Children.Add(stack);
    }

    private void RenderTable(List<string[]> rows)
    {
        var border = new Border
        {
            BorderBrush = FindResource("BorderBrush") as WpfBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var grid = new Grid();

        // Create columns
        if (rows.Count > 0)
        {
            for (int c = 0; c < rows[0].Length; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
        }

        // Create rows
        for (int r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int c = 0; c < rows[r].Length; c++)
            {
                var cellBorder = new Border
                {
                    Background = r == 0 ? FindResource("CardBackgroundBrush") as WpfBrush : WpfBrushes.Transparent,
                    BorderBrush = FindResource("BorderBrush") as WpfBrush,
                    BorderThickness = new Thickness(0, 0, c < rows[r].Length - 1 ? 1 : 0, r < rows.Count - 1 ? 1 : 0),
                    Padding = new Thickness(12, 8, 12, 8)
                };

                var text = new TextBlock
                {
                    Text = rows[r][c],
                    FontWeight = r == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = FindResource(r == 0 ? "BrightForegroundBrush" : "ForegroundBrush") as WpfBrush,
                    FontSize = 12
                };

                cellBorder.Child = text;
                Grid.SetRow(cellBorder, r);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        border.Child = grid;
        ContentPanel.Children.Add(border);
    }

    private void RenderCallout(string content, string title, string colorHex)
    {
        var color = (WpfColor)WpfColorConverter.ConvertFromString(colorHex);
        var brush = new SolidColorBrush(color);

        var border = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(30, color.R, color.G, color.B)),
            BorderBrush = brush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var stack = new StackPanel();

        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = brush,
            Margin = new Thickness(0, 0, 0, 4)
        };
        stack.Children.Add(titleBlock);

        var contentBlock = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = FindResource("ForegroundBrush") as WpfBrush
        };
        stack.Children.Add(contentBlock);

        border.Child = stack;
        ContentPanel.Children.Add(border);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Navigate to a specific lesson by ID
    /// </summary>
    public void GoToLesson(string lessonId)
    {
        var index = _lessons.FindIndex(l => l.Id == lessonId);
        if (index >= 0)
        {
            ShowLesson(index);
        }
    }

    /// <summary>
    /// Navigate to a specific lesson by index
    /// </summary>
    public void GoToLesson(int index)
    {
        ShowLesson(index);
    }

    /// <summary>
    /// Get the current lesson index
    /// </summary>
    public int CurrentLessonIndex => _currentLessonIndex;

    /// <summary>
    /// Get the total number of lessons
    /// </summary>
    public int LessonCount => _lessons.Count;

    #endregion
}

#region Data Models

public class WorkshopLesson
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public List<LessonSection> Sections { get; set; } = new();
}

public class LessonSection
{
    public SectionType Type { get; set; }
    public string Content { get; set; } = "";
    public string? Description { get; set; }
    public List<string>? Items { get; set; }
    public List<string[]>? TableData { get; set; }
}

public enum SectionType
{
    Text,
    Heading,
    Code,
    BulletList,
    Table,
    Tip,
    Warning,
    Info,
    Success
}

public class WorkshopCodeEventArgs : EventArgs
{
    public string Code { get; }

    public WorkshopCodeEventArgs(string code)
    {
        Code = code;
    }
}

#endregion
