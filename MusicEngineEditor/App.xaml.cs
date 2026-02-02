// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Application entry point and configuration.

using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;
using MusicEngineEditor.Views;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load performance profile from environment before services are built
        PerformanceConfig.LoadFromEnvironment();

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Load settings and apply saved theme
        await ApplyStartupThemeAsync();

        // Load recent projects service
        var recentProjectsService = Services.GetRequiredService<IRecentProjectsService>();
        await recentProjectsService.LoadAsync();

        // Create and show main window first (prevents app shutdown issues)
        var mainWindow = new MainWindow();
        mainWindow.Show();

        // Show welcome screen if enabled (after main window exists)
        if (recentProjectsService.ShowWelcomeOnStartup)
        {
            // Defer welcome screen to after main window is fully loaded
            mainWindow.Dispatcher.BeginInvoke(new Action(async () =>
            {
                var welcomeScreen = new WelcomeScreen(recentProjectsService);
                welcomeScreen.Owner = mainWindow;
                var result = welcomeScreen.ShowDialog();

                if (result == true)
                {
                    switch (welcomeScreen.Result.Action)
                    {
                        case WelcomeScreenAction.NewProject:
                            mainWindow.TriggerNewProject();
                            break;
                        case WelcomeScreenAction.OpenProject:
                            mainWindow.TriggerOpenProject();
                            break;
                        case WelcomeScreenAction.OpenRecentProject:
                            if (!string.IsNullOrEmpty(welcomeScreen.Result.SelectedProjectPath))
                            {
                                await mainWindow.OpenProjectFileAsync(welcomeScreen.Result.SelectedProjectPath);
                            }
                            break;
                        case WelcomeScreenAction.Skip:
                        case WelcomeScreenAction.Close:
                            // Just continue
                            break;
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // Check for crash recovery after main window is shown
        CheckForCrashRecovery(mainWindow);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Mark session as cleanly closed
        try
        {
            RecoveryService.Instance.MarkSessionClosed();
        }
        catch
        {
            // Ignore errors during exit
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Checks for crash recovery and shows the recovery dialog if needed.
    /// </summary>
    private static void CheckForCrashRecovery(Window owner)
    {
        try
        {
            var recoveryService = RecoveryService.Instance;
            var autoSaveService = AutoSaveService.Instance;

            // Check if there's a recoverable session from a crash
            if (recoveryService.HasRecoverableSession() || autoSaveService.HasAutoSaves())
            {
                // Show recovery dialog
                var result = RecoveryDialog.ShowRecoveryDialogWithInfo(owner);

                if (result.WasRecovered && result.RecoveryFilePath != null)
                {
                    // Recovery was requested - the MainWindow will handle loading the recovered project
                    System.Diagnostics.Debug.WriteLine($"Recovery requested for: {result.RecoveryFilePath}");
                }
            }

            // Clean up old recovery data (older than 7 days)
            recoveryService.CleanupOldRecoveries(7);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Crash recovery check failed: {ex.Message}");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISoundPackService, SoundPackService>();
        services.AddSingleton<PresetBrowserService>();
        services.AddSingleton<EngineService>();

        // Playback services (singletons accessed via Instance property)
        services.AddSingleton(_ => PlaybackService.Instance);
        services.AddSingleton(_ => AudioEngineService.Instance);

        // Auto-save and recovery services
        services.AddSingleton(_ => AutoSaveService.Instance);
        services.AddSingleton(_ => RecoveryService.Instance);

        // Recent projects service
        services.AddSingleton<IRecentProjectsService, RecentProjectsService>();

        // Spatial audio service
        services.AddSingleton<SpatialAudioService>();

        // Analysis service
        services.AddSingleton(_ => IntegratedAnalysisService.Instance);

        // Network sync service
        services.AddSingleton(_ => NetworkSyncService.Instance);

        // Workspace and UI services
        services.AddSingleton<WorkspacePresetService>();
        services.AddSingleton<TrackColorService>();
        services.AddSingleton(_ => AudioReactiveService.Instance);

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ProjectExplorerViewModel>();
        services.AddTransient<OutputViewModel>();
        services.AddTransient<EditorTabViewModel>();
        services.AddTransient<SampleBrowserViewModel>();
        services.AddTransient<TransportViewModel>();
    }

    /// <summary>
    /// Loads settings and applies the saved theme on startup
    /// </summary>
    private static async System.Threading.Tasks.Task ApplyStartupThemeAsync()
    {
        try
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            var themeService = Services.GetRequiredService<IThemeService>();

            // Load settings to get the saved theme
            var settings = await settingsService.LoadSettingsAsync();
            var savedTheme = settings.Editor.Theme;

            // Apply the saved theme (or default to Dark if not set)
            if (!string.IsNullOrWhiteSpace(savedTheme))
            {
                themeService.ApplyTheme(savedTheme);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply startup theme: {ex.Message}");
            // Fall back to default theme (Dark) which is already loaded in App.xaml
        }
    }
}
