# MusicEngineEditor - Claude Code Guidelines

## Project Overview
MusicEngineEditor is a WPF-based Digital Audio Workstation (DAW) code editor for the MusicEngine scripting environment. It provides a modern, dark-themed IDE for creating music through C# scripting.

## Tech Stack
- **Framework**: .NET 10.0 Windows (WPF)
- **Language**: C# 13
- **UI Framework**: WPF with AvalonDock for docking panels
- **Code Editor**: AvalonEdit with custom syntax highlighting
- **MVVM**: CommunityToolkit.Mvvm
- **Audio**: NAudio, NAudio.Midi, NAudio.Asio
- **Scripting**: Microsoft.CodeAnalysis (Roslyn) for C# scripting
- **Logging**: Serilog

## Project Structure
```
MusicEngineEditor/
├── Controls/           # Custom WPF UserControls
│   ├── Analysis/       # Audio analysis visualizations
│   ├── Effects/        # Effect controls (reverb, compressor, etc.)
│   ├── InlineVisuals/  # Inline editor visualizations
│   ├── MIDI/           # MIDI-related controls
│   └── Performance/    # Live performance controls
├── Commands/           # ICommand implementations
├── Converters/         # WPF value converters
├── Editor/             # Code editor setup and syntax highlighting
├── Models/             # Data models
├── Resources/          # Icons, sound packs, assets
├── Services/           # Business logic services
├── Themes/             # XAML theme resources
│   └── DarkTheme.xaml  # Main dark theme (DAW-inspired)
├── ViewModels/         # MVVM ViewModels
├── Views/              # Additional views/windows
├── MainWindow.xaml     # Main application window
└── App.xaml            # Application entry point
```

## Coding Conventions

### XAML Styling
- **Theme file**: `Themes/DarkTheme.xaml` - All styles and colors defined here
- **Color scheme**: Deep blacks (#0D0D0D - #181818) with cyan accent (#00D9FF)
- **Use DynamicResource** for colors that may change at runtime
- **Use StaticResource** for static styles and templates
- **Tab headers**: Use StackPanel with Path (icon) + TextBlock (label)
- **Animations**: Use Storyboards in ControlTemplate.Triggers for hover effects

### Key Style Names
```xaml
<!-- Buttons -->
ToolbarButtonStyle          - Standard toolbar buttons
IconButtonStyle             - Small icon-only buttons
SidebarToolButtonStyle      - Sidebar buttons with glow effect
TransportPlayButtonStyle    - Green play/run button
TransportStopButtonStyle    - Red stop button
PrimaryButtonStyle          - Accent-colored action buttons
SecondaryButtonStyle        - Outline buttons
RunButtonStyle              - Script run button with glow

<!-- Panels & Layout -->
ModernPanelStyle            - Panels with shadow
CardStyle                   - Card-like containers
PanelHeaderStyle            - Panel header bars
FloatingPanelStyle          - Modal/popup panels

<!-- Input -->
InputTextBoxStyle           - Text input fields
OutputTextBoxStyle          - Read-only output areas
BpmDisplayStyle             - BPM input with cyan accent
```

### C# Code Style
- Use `static` helper methods for UI element traversal (e.g., `GetTextBlockFromTabHeader`)
- Tab header children may be `TextBlock` or `StackPanel` - always check type
- Use `FindResource()` for runtime resource lookup
- Null-check with `?.` when accessing optional UI elements

### Syntax Highlighting Colors (EditorSetup.cs)
```
Keywords:    #00D9FF (Cyan)
Strings:     #FFAB40 (Orange)
Comments:    #6A737D (Gray)
Numbers:     #D19AFF (Purple)
Types:       #4FC3F7 (Light Cyan)
Methods:     #B9F6CA (Mint Green)
```

## Common Patterns

### Adding New Tab Headers with Icons
```xaml
<Border x:Name="MyTabHeader" Padding="12,8" Cursor="Hand" MouseDown="MyTab_Click">
    <StackPanel Orientation="Horizontal">
        <Path Data="M 2,2 H 12 V 12 H 2 Z"
              Stroke="{StaticResource SecondaryForegroundBrush}"
              StrokeThickness="1" Width="14" Height="12" Margin="0,0,6,0"/>
        <TextBlock Text="My Tab" FontSize="11"/>
    </StackPanel>
</Border>
```

### Handling Tab Switching in Code-Behind
```csharp
// Use helper methods to handle both TextBlock and StackPanel structures
SetTabHeaderActive(MyTabHeader);    // Activates tab (white text/icon)
SetTabHeaderInactive(MyTabHeader);  // Deactivates tab (gray text/icon)
```

### Adding Glow Effects
```xaml
<Border.Effect>
    <DropShadowEffect x:Name="glowEffect" Color="#00D9FF"
                      BlurRadius="8" ShadowDepth="0" Opacity="0.4"/>
</Border.Effect>
```

## Audio Reactive Lighting

The application features audio-reactive UI effects that respond to music playback:

### AudioReactiveService (Services/AudioReactiveService.cs)
- Subscribes to AnalysisService for spectrum and peak data
- Processes audio into frequency bands: Bass (20-200Hz), Mid (200-2000Hz), High (2000Hz+)
- Provides smoothed values with attack/release envelope
- Beat detection for pulsing effects

### Reactive UI Elements
- **Run Button Glow**: Pulses with bass + beat intensity (BlurRadius 8-24, Opacity 0.35-0.9)
- **Sidebar Buttons**: Wave-like effect based on mid frequencies
- **Status Indicator**: Brightness varies with overall level

### Usage in Code-Behind
```csharp
// Start/stop with playback
StartAudioReactiveLighting();
StopAudioReactiveLighting();

// Access reactive values
float bass = AudioReactiveService.Instance.BassLevel;
float beat = AudioReactiveService.Instance.BeatIntensity;
```

### Configuration
```csharp
AudioReactiveService.Instance.Sensitivity = 1.5f;      // Level amplification
AudioReactiveService.Instance.MinGlowOpacity = 0.1f;   // Minimum glow
AudioReactiveService.Instance.MaxGlowOpacity = 0.9f;   // Maximum glow
```

## Audio Visualizer Background

The editor features a subtle audio-reactive background that responds to music playback.

### Visualizer Elements (MainWindow.xaml)
- **BassGlow**: Bottom gradient (Purple/Blue), reacts to bass (20-200Hz)
- **MidGlowLeft/Right**: Side edge gradients (Cyan), react to mids (200-2kHz)
- **HighGlow**: Top gradient (White/Cyan), reacts to highs (2kHz+)
- **AmbientPulse**: Center radial pulse, reacts to overall level + beat

### Configuration in MainWindow.xaml.cs
```csharp
// Enable/disable visualizer
SetAudioVisualizerEnabled(true);

// Set intensity (0.0 - 0.3 max to keep it subtle)
SetAudioVisualizerIntensity(0.12f);  // 12% opacity
```

### Design Principles
- **Subtil**: Max 12% opacity, nicht ablenkend
- **Professionell**: Wie FL Studio, Ableton, Bitwig
- **Deaktivierbar**: Kann ausgeschaltet werden
- **Performant**: Nur UI-Updates bei Änderungen

## Important Notes

1. **DropShadowEffect Targeting**: Cannot use `Setter TargetName` to target effects directly. Instead, replace the entire Effect property:
```xaml
<Setter TargetName="border" Property="Effect">
    <Setter.Value>
        <DropShadowEffect BlurRadius="0" Opacity="0"/>
    </Setter.Value>
</Setter>
```

2. **No CSS Properties**: WPF doesn't support CSS properties like `TextTransform`. Use `Typography.Capitals` for small caps or set text directly.

3. **Path Icons**: Use SVG-like Path Data for icons instead of emoji/unicode characters for better rendering.

## Build & Run
```bash
dotnet build MusicEngineEditor.csproj
dotnet run
```

## Testing UI Changes
After XAML changes, always rebuild and test:
1. Tab switching (Output, Console, Errors tabs)
2. Right panel tabs (MIDI, VST, Audio)
3. Sidebar button hover effects
4. Run/Stop button animations
