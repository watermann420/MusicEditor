# PowerShell script to add license headers to all C# files
# Run from the MusicEditor directory

$licenseHeader = @"
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
"@

# File descriptions based on folder/filename patterns
$descriptions = @{
    # Commands
    "AudioCommands" = "Audio-related commands for the editor."
    "AutomationCommands" = "Automation editing commands."
    "AutomationThinCommand" = "Command for thinning automation data."
    "BounceInPlaceCommand" = "Bounce in place functionality."
    "MixerCommands" = "Mixer-related commands."
    "NoteCommands" = "Note editing commands for piano roll."
    "SectionCommands" = "Section/arrangement commands."

    # Controls
    "AnalysisPanel" = "Audio analysis visualization panel."
    "ArrangementView" = "Main arrangement/timeline view."
    "BusChannelControl" = "Bus channel mixer control."
    "EffectSlotControl" = "Effect slot for effect chain."
    "FindReplaceControl" = "Find and replace functionality."
    "LearnPanel" = "MIDI/parameter learn panel."
    "LevelMeter" = "Audio level meter visualization."
    "MidiCCLane" = "MIDI CC automation lane."
    "PianoKeyboard" = "Virtual piano keyboard control."
    "PresetCard" = "Preset display card control."
    "PunchcardVisualization" = "Pattern punchcard visualization."
    "SampleBrowser" = "Sample/audio file browser."
    "SendControl" = "Send/return routing control."
    "ShortcutRecorder" = "Keyboard shortcut recorder."
    "TrackPropertiesPanel" = "Track properties editor panel."
    "TransportToolbar" = "Transport controls toolbar."
    "VstPluginPanel" = "VST plugin host panel."
    "WaveformDisplay" = "Audio waveform visualization."
    "WorkshopPanel" = "Workshop/community panel."
    "PerformanceDetailPanel" = "Detailed performance monitoring panel."
    "PerformanceMeter" = "Performance meter visualization."

    # Editor
    "CSharpFoldingStrategy" = "Code folding strategy for C# scripts."
    "CodeSourceAnalyzer" = "Source code analysis for live features."
    "CodeTooltipService" = "Code tooltip/hover information service."
    "CompletionProvider" = "Autocomplete/intellisense provider."
    "InlineSliderAdorner" = "Popup-based inline slider for numbers."
    "LiveParameterSystem" = "Real-time parameter binding system."
    "MusicEngineCompletionData" = "Completion data for MusicEngine API."
    "NumberDetector" = "Numeric literal detection in code."
    "PlaybackHighlightRenderer" = "Highlights active code during playback."
    "VisualizationIntegration" = "Integration for live code visualization."
    "StrudelInlineSlider" = "Strudel.cc-style inline sliders in code."
    "EditorSetup" = "Code editor initialization and setup."

    # Models
    "AppSettings" = "Application settings model."
    "AudioAsset" = "Audio asset/file model."
    "AudioSample" = "Audio sample data model."
    "BusChannel" = "Bus channel routing model."
    "CodeSnippet" = "Code snippet template model."
    "EffectSlot" = "Effect slot configuration model."
    "KeyboardShortcut" = "Keyboard shortcut mapping model."
    "MidiCCEvent" = "MIDI CC event data model."
    "MusicProject" = "Music project container model."
    "MusicScript" = "Script file model."
    "PianoRollNote" = "Piano roll note data model."
    "ProjectInfo" = "Project information/metadata model."
    "ProjectManifest" = "Project manifest for serialization."
    "ProjectReference" = "Project reference model."
    "RecordingClip" = "Audio recording clip model."
    "SendReturn" = "Send/return routing model."
    "SoundPack" = "Sound pack container model."
    "Theme" = "UI theme configuration model."
    "TrackInfo" = "Track information model."
    "WaveformData" = "Waveform visualization data model."

    # Services
    "AudioEngineService" = "Audio engine integration service."
    "EditorUndoService" = "Undo/redo management service."
    "EventBus" = "Event bus for decoupled communication."
    "MetronomeService" = "Metronome/click track service."
    "MixerEffectService" = "Mixer effects management service."
    "PerformanceMonitorService" = "System performance monitoring service."
    "PlaybackService" = "Audio playback control service."
    "RecordingService" = "Audio/MIDI recording service."
    "ScriptExecutionService" = "Script compilation and execution service."
    "ScrubService" = "Timeline scrubbing service."
    "SettingsService" = "Application settings persistence service."
    "ShortcutService" = "Keyboard shortcut management service."
    "SnippetService" = "Code snippet management service."
    "SoundPackService" = "Sound pack loading service."
    "VisualizationBridge" = "Bridge between audio and visualization."
    "WaveformService" = "Waveform generation service."

    # ViewModels
    "AutomationViewModel" = "ViewModel for automation editing."
    "EditorTabViewModel" = "ViewModel for editor tabs."
    "ExportViewModel" = "ViewModel for export dialog."
    "MainViewModel" = "Main window ViewModel."
    "MetronomeViewModel" = "ViewModel for metronome settings."
    "MidiCCLaneViewModel" = "ViewModel for MIDI CC lanes."
    "OutputViewModel" = "ViewModel for output/console panel."
    "PerformanceViewModel" = "ViewModel for performance monitor."
    "ProjectBrowserViewModel" = "ViewModel for project browser."
    "ProjectExplorerViewModel" = "ViewModel for project explorer."
    "SampleBrowserViewModel" = "ViewModel for sample browser."
    "SettingsViewModel" = "ViewModel for settings dialog."
    "ShortcutsViewModel" = "ViewModel for shortcuts editor."
    "TrackPropertiesViewModel" = "ViewModel for track properties."
    "TransportViewModel" = "ViewModel for transport controls."
    "ViewModelBase" = "Base class for all ViewModels."
    "VstPresetBrowserViewModel" = "ViewModel for VST preset browser."

    # Converters
    "StringToVisibilityConverter" = "Converts string to Visibility."

    # Views
    "MainWindow" = "Main application window."
    "AboutDialog" = "About dialog window."
    "AddScriptDialog" = "Add new script dialog."
    "ExportDialog" = "Export settings dialog."
    "ImportAudioDialog" = "Audio import dialog."
    "InputDialog" = "Generic input dialog."
    "MetronomeSettingsDialog" = "Metronome settings dialog."
    "NewProjectDialog" = "New project creation dialog."

    # App
    "App" = "Application entry point and configuration."
}

function Get-Description($filename) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($filename)

    # Check exact match
    if ($descriptions.ContainsKey($baseName)) {
        return $descriptions[$baseName]
    }

    # Check partial matches
    foreach ($key in $descriptions.Keys) {
        if ($baseName -like "*$key*") {
            return $descriptions[$key]
        }
    }

    # Default based on folder
    $folder = Split-Path (Split-Path $filename -Parent) -Leaf
    switch ($folder) {
        "Commands" { return "Command implementation." }
        "Controls" { return "UI control implementation." }
        "Editor" { return "Code editor component." }
        "Models" { return "Data model class." }
        "Services" { return "Service implementation." }
        "ViewModels" { return "ViewModel implementation." }
        "Views" { return "View implementation." }
        "Dialogs" { return "Dialog window implementation." }
        "Converters" { return "Value converter implementation." }
        default { return "MusicEngineEditor component." }
    }
}

function Add-Header($filePath) {
    $content = Get-Content $filePath -Raw -Encoding UTF8

    # Skip if already has our header
    if ($content -match "MusicEngine License \(MEL\)") {
        Write-Host "Skipping (already has header): $filePath" -ForegroundColor Yellow
        return
    }

    # Get description
    $desc = Get-Description $filePath

    # Build full header
    $fullHeader = "$licenseHeader`n// Description: $desc`n`n"

    # Remove existing header comments if present (old style)
    $content = $content -replace "^(//.*\r?\n)+", ""
    $content = $content.TrimStart()

    # Add new header
    $newContent = $fullHeader + $content

    Set-Content $filePath -Value $newContent -Encoding UTF8 -NoNewline
    Write-Host "Updated: $filePath" -ForegroundColor Green
}

# Process all C# files
$files = Get-ChildItem -Path "MusicEngineEditor" -Filter "*.cs" -Recurse
$count = 0

foreach ($file in $files) {
    # Skip obj/bin folders
    if ($file.FullName -match "\\obj\\" -or $file.FullName -match "\\bin\\") {
        continue
    }

    Add-Header $file.FullName
    $count++
}

Write-Host "`nProcessed $count files." -ForegroundColor Cyan
