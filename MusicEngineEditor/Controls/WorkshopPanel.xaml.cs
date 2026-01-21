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
                    Type = WorkshopSectionType.Text,
                    Content = "MusicEngine is a live coding environment for creating music using C# scripts. " +
                              "You can create synthesizers, load VST plugins, sequence patterns, and control everything with MIDI."
                },
                new()
                {
                    Type = WorkshopSectionType.Heading,
                    Content = "What you'll learn"
                },
                new()
                {
                    Type = WorkshopSectionType.BulletList,
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
                    Type = WorkshopSectionType.Info,
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
                    Type = WorkshopSectionType.Text,
                    Content = "Let's create your first sound! The simplest way to make noise in MusicEngine is to create a synthesizer."
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = "// Create a simple synth\nvar synth = CreateSynth();",
                    Description = "This creates a basic synthesizer with default settings."
                },
                new()
                {
                    Type = WorkshopSectionType.Text,
                    Content = "Now let's play a note! MIDI notes go from 0 to 127, where 60 is middle C."
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = "// Create synth and play middle C\nvar synth = CreateSynth();\nsynth.NoteOn(60, 100);  // Note 60 = C4, velocity 100",
                    Description = "Click Run to hear your first note!"
                },
                new()
                {
                    Type = WorkshopSectionType.Tip,
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
                    Type = WorkshopSectionType.Text,
                    Content = "To play melodies, we need to turn notes on and off with timing. Use await Task.Delay() to wait between notes."
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
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
                    Type = WorkshopSectionType.Heading,
                    Content = "Common MIDI Notes"
                },
                new()
                {
                    Type = WorkshopSectionType.Table,
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
                    Type = WorkshopSectionType.Text,
                    Content = "MIDI (Musical Instrument Digital Interface) lets you connect keyboards, controllers, and other devices. " +
                              "MusicEngine automatically detects connected MIDI devices."
                },
                new()
                {
                    Type = WorkshopSectionType.Heading,
                    Content = "Listing MIDI Devices"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
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
                    Type = WorkshopSectionType.Info,
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
                    Type = WorkshopSectionType.Text,
                    Content = "Now let's connect your MIDI keyboard to control a synthesizer!"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Route MIDI keyboard to synth
var synth = CreateSynth();

// Route MIDI device 0 to the synth
midi.device(0).route(synth);

Print(""Play your keyboard!"");",
                    Description = "Your keyboard now controls the synth"
                },
                new()
                {
                    Type = WorkshopSectionType.Heading,
                    Content = "Mapping MIDI CC"
                },
                new()
                {
                    Type = WorkshopSectionType.Text,
                    Content = "You can also map MIDI control change (CC) messages to synth parameters:"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
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
                    Type = WorkshopSectionType.Text,
                    Content = "MusicEngine can load VST2 and VST3 plugins. This lets you use any professional synthesizer like Serum, Vital, Massive, or any other plugin you have installed."
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Load a VST plugin by name
var synth = vst.load(""MyPluginName"");
synth.ShowEditor();
synth.NoteOn(60, 100);",
                    Description = "Replace 'MyPluginName' with your plugin name (e.g. Serum, Vital, Massive)"
                },
                new()
                {
                    Type = WorkshopSectionType.Heading,
                    Content = "Connect MIDI Keyboard"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Load plugin and connect your keyboard
var synth = vst.load(""MyPluginName"");
synth.ShowEditor();
synth.from(0);  // MIDI device 0",
                    Description = "Route MIDI keyboard to VST plugin"
                },
                new()
                {
                    Type = WorkshopSectionType.Tip,
                    Content = "Common VST locations: C:\\Program Files\\Common Files\\VST3 or C:\\Program Files\\VSTPlugins"
                },
                new()
                {
                    Type = WorkshopSectionType.Heading,
                    Content = "Popular Free VST Plugins"
                },
                new()
                {
                    Type = WorkshopSectionType.BulletList,
                    Items = new List<string>
                    {
                        "Vital - Free wavetable synth (vital.audio)",
                        "Surge XT - Open source hybrid synth (surge-synth-team.org)",
                        "Dexed - DX7 FM synth emulator",
                        "OB-Xd - Oberheim emulation",
                        "Helm - Another great free synth"
                    }
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
                    Type = WorkshopSectionType.Text,
                    Content = "Patterns let you create repeating musical sequences. Use pattern.Note(note, beat, duration, velocity) to add notes."
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Simple 4-beat pattern
var synth = CreateSynth();
var p = CreatePattern(synth);

p.Note(60, 0, 0.5, 100);  // C on beat 1
p.Note(64, 1, 0.5, 100);  // E on beat 2
p.Note(67, 2, 0.5, 100);  // G on beat 3
p.Note(72, 3, 0.5, 100);  // C on beat 4

p.Loop = true;
p.Play();",
                    Description = "A simple melody loop"
                },
                new()
                {
                    Type = WorkshopSectionType.Heading,
                    Content = "Drum Pattern Example"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Basic drum beat
var drums = CreateSynth();
var p = CreatePattern(drums);

p.Note(36, 0, 0.25, 100);  // Kick
p.Note(38, 1, 0.25, 100);  // Snare
p.Note(36, 2, 0.25, 100);  // Kick
p.Note(38, 3, 0.25, 100);  // Snare

p.Loop = true;
p.Play();",
                    Description = "Note 36 = Kick, Note 38 = Snare"
                },
                new()
                {
                    Type = WorkshopSectionType.Tip,
                    Content = "The white box around code shows what's currently playing!"
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
                    Type = WorkshopSectionType.Text,
                    Content = "The Sequencer controls the tempo. Use sequencer.Bpm to set the speed."
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Set tempo to 120 BPM
sequencer.Bpm = 120;

var synth = CreateSynth();
var p = CreatePattern(synth);

p.Note(60, 0, 0.5, 100);
p.Note(67, 2, 0.5, 100);

p.Loop = true;
p.Play();",
                    Description = "Simple example with tempo"
                },
                new()
                {
                    Type = WorkshopSectionType.Tip,
                    Content = "Use lowercase 'sequencer' or uppercase 'Sequencer' - both work!"
                }
            }
        });

        // New lesson: SampleInstrument
        _lessons.Add(new WorkshopLesson
        {
            Id = "samples",
            Title = "Sample Instrument",
            Subtitle = "Play audio files as instruments",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = WorkshopSectionType.Text,
                    Content = "SampleInstrument lets you load audio files (WAV, MP3, etc.) and play them like an instrument. Map different sounds to different MIDI notes!"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Load a single sample
var drums = CreateSampler();
drums.LoadSample(36, ""kick.wav"");
drums.LoadSample(38, ""snare.wav"");

drums.NoteOn(36, 100);  // Play kick",
                    Description = "Load samples to specific MIDI notes"
                },
                new()
                {
                    Type = WorkshopSectionType.Heading,
                    Content = "Load from a folder"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Load all samples from a folder
var sampler = CreateSamplerFromDirectory(""C:/Samples/DrumKit"");

// Files are mapped by name:
// kick.wav -> note 36, snare.wav -> note 38, etc.
sampler.NoteOn(36, 100);",
                    Description = "Auto-map samples from a folder"
                },
                new()
                {
                    Type = WorkshopSectionType.Heading,
                    Content = "Use in Patterns"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Create drum loop with samples
var drums = CreateSampler();
drums.LoadSample(36, ""kick.wav"");
drums.LoadSample(38, ""snare.wav"");

var p = CreatePattern(drums);
p.Note(36, 0, 0.25, 100);  // Kick
p.Note(38, 1, 0.25, 100);  // Snare
p.Note(36, 2, 0.25, 100);  // Kick
p.Note(38, 3, 0.25, 100);  // Snare

p.Loop = true;
p.Play();",
                    Description = "Combine samples with patterns"
                },
                new()
                {
                    Type = WorkshopSectionType.Tip,
                    Content = "Samples are automatically pitch-shifted when played on different notes!"
                }
            }
        });

        _lessons.Add(new WorkshopLesson
        {
            Id = "complete",
            Title = "Putting It All Together",
            Subtitle = "Complete music example",
            Sections = new List<LessonSection>
            {
                new()
                {
                    Type = WorkshopSectionType.Text,
                    Content = "Let's combine everything you've learned into a complete track!"
                },
                new()
                {
                    Type = WorkshopSectionType.Code,
                    Content = @"// Set tempo
sequencer.Bpm = 128;

// Bass synth
var bass = CreateSynth();
var bassP = CreatePattern(bass);
bassP.Note(36, 0, 0.5, 100);
bassP.Note(36, 1, 0.5, 100);
bassP.Note(43, 2, 0.5, 100);
bassP.Note(41, 3, 0.5, 100);
bassP.Loop = true;

// Lead synth
var lead = CreateSynth();
var leadP = CreatePattern(lead);
leadP.Note(60, 0, 1, 80);
leadP.Note(67, 2, 1, 80);
leadP.Loop = true;

// Start everything
bassP.Play();
leadP.Play();",
                    Description = "Bass + Lead pattern"
                },
                new()
                {
                    Type = WorkshopSectionType.Success,
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
        _navButtons["patterns"] = Nav_Patterns;
        _navButtons["sequencing"] = Nav_Sequencing;
        _navButtons["samples"] = Nav_Samples;
        _navButtons["complete"] = Nav_Complete;
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
                "patterns" => "patterns",
                "sequencing" => "sequencing",
                "samples" => "samples",
                "complete" => "complete",
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
            case WorkshopSectionType.Text:
                RenderText(section.Content);
                break;
            case WorkshopSectionType.Heading:
                RenderHeading(section.Content);
                break;
            case WorkshopSectionType.Code:
                RenderCode(section.Content, section.Description);
                break;
            case WorkshopSectionType.BulletList:
                RenderBulletList(section.Items!);
                break;
            case WorkshopSectionType.Table:
                RenderTable(section.TableData!);
                break;
            case WorkshopSectionType.Tip:
                RenderCallout(section.Content, "Tip", "#4B6EAF");
                break;
            case WorkshopSectionType.Warning:
                RenderCallout(section.Content, "Warning", "#E9B85B");
                break;
            case WorkshopSectionType.Info:
                RenderCallout(section.Content, "Info", "#6F737A");
                break;
            case WorkshopSectionType.Success:
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
    public WorkshopSectionType Type { get; set; }
    public string Content { get; set; } = "";
    public string? Description { get; set; }
    public List<string>? Items { get; set; }
    public List<string[]>? TableData { get; set; }
}

public enum WorkshopSectionType
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
