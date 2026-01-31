using System;
using System.IO;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.UIA3;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace MusicEngineEditor.Tests.UI;

/// <summary>
/// Lightweight UI smoke tests; run only when ENABLE_UI_TESTS=1 or -UiSmoke is passed to build.ps1.
/// </summary>
public class UISmokeTests : IDisposable
{
    private Application? _app;

    [Fact]
    [Trait("Category", "UI")]
    public void MainWindow_ShouldLaunchAndShowTitle()
    {
        if (!IsUiEnabled())
        {
            // Allow test suite to pass when UI smoke is disabled (e.g., headless CI).
            return;
        }

        var exePath = ResolveEditorExe();
        File.Exists(exePath).Should().BeTrue($"Editor executable should exist at {exePath}");

        _app = Application.Launch(exePath);

        using var automation = new UIA3Automation();
        var mainWindow = _app.GetMainWindow(automation, TimeSpan.FromSeconds(15));
        mainWindow.Should().NotBeNull("Main window should appear");
        mainWindow.Title.Should().Contain("Music", "window title should be visible for sanity check");
    }

    private static bool IsUiEnabled()
    {
        var env = Environment.GetEnvironmentVariable("ENABLE_UI_TESTS");
        return string.Equals(env, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveEditorExe()
    {
        var baseDir = AppContext.BaseDirectory; // .../bin/{Config}/net10.0-windows/
        var configDir = Directory.GetParent(baseDir)!.Parent?.Name ?? "Release";
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var candidate = Path.Combine(repoRoot, "MusicEngineEditor", "bin", configDir, "net10.0-windows", "MusicEngineEditor.exe");
        return candidate;
    }

    public void Dispose()
    {
        try
        {
            if (_app != null && !_app.HasExited)
            {
                _app.Close();
                _app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(5));
                _app.Kill();
            }
        }
        catch
        {
            // ignore best-effort cleanup
        }
    }
}
