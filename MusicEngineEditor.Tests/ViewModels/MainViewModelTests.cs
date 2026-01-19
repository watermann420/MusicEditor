using System.Collections.ObjectModel;
using System.ComponentModel;
using FluentAssertions;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;
using NSubstitute;
using Xunit;

namespace MusicEngineEditor.Tests.ViewModels;

/// <summary>
/// Unit tests for MainViewModel
/// </summary>
public class MainViewModelTests
{
    private readonly IProjectService _projectService;
    private readonly IScriptExecutionService _executionService;
    private readonly ISettingsService _settingsService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _executionService = Substitute.For<IScriptExecutionService>();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Settings.Returns(new AppSettings());

        _viewModel = new MainViewModel(_projectService, _executionService, _settingsService);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeProjectExplorer()
    {
        // Assert
        _viewModel.ProjectExplorer.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldInitializeOutput()
    {
        // Assert
        _viewModel.Output.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldInitializeOpenDocumentsCollection()
    {
        // Assert
        _viewModel.OpenDocuments.Should().NotBeNull();
        _viewModel.OpenDocuments.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ShouldLoadSettings()
    {
        // Assert
        _settingsService.Received(1).LoadSettingsAsync();
    }

    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Assert
        _viewModel.IsProjectExplorerVisible.Should().BeTrue();
        _viewModel.IsOutputVisible.Should().BeTrue();
        _viewModel.IsPropertiesVisible.Should().BeTrue();
        _viewModel.CurrentBpm.Should().Be(120);
        _viewModel.PlaybackStatus.Should().Be("Stopped");
        _viewModel.CaretPosition.Should().Be("1:1");
        _viewModel.Encoding.Should().Be("UTF-8");
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void CurrentProject_ShouldBeNullInitially()
    {
        // Assert
        _viewModel.CurrentProject.Should().BeNull();
    }

    [Fact]
    public void ActiveDocument_ShouldBeNullInitially()
    {
        // Assert
        _viewModel.ActiveDocument.Should().BeNull();
    }

    [Fact]
    public void OpenScript_ShouldAddDocumentToOpenDocuments()
    {
        // Arrange
        var script = CreateTestScript("Test.me");

        // Act
        _viewModel.OpenScript(script);

        // Assert
        _viewModel.OpenDocuments.Should().HaveCount(1);
        _viewModel.OpenDocuments[0].Script.Should().Be(script);
    }

    [Fact]
    public void OpenScript_ShouldSetAsActiveDocument()
    {
        // Arrange
        var script = CreateTestScript("Test.me");

        // Act
        _viewModel.OpenScript(script);

        // Assert
        _viewModel.ActiveDocument.Should().NotBeNull();
        _viewModel.ActiveDocument!.Script.Should().Be(script);
    }

    [Fact]
    public void OpenScript_ShouldNotDuplicateIfAlreadyOpen()
    {
        // Arrange
        var script = CreateTestScript("Test.me");

        // Act
        _viewModel.OpenScript(script);
        _viewModel.OpenScript(script);

        // Assert
        _viewModel.OpenDocuments.Should().HaveCount(1);
    }

    [Fact]
    public void OpenScript_ShouldSwitchToExistingDocumentIfAlreadyOpen()
    {
        // Arrange
        var script1 = CreateTestScript("Test1.me");
        var script2 = CreateTestScript("Test2.me");
        _viewModel.OpenScript(script1);
        _viewModel.OpenScript(script2);

        // Act
        _viewModel.OpenScript(script1);

        // Assert
        _viewModel.ActiveDocument!.Script.Should().Be(script1);
    }

    [Fact]
    public void OpenScript_ShouldSupportMultipleDocuments()
    {
        // Arrange
        var script1 = CreateTestScript("Test1.me");
        var script2 = CreateTestScript("Test2.me");
        var script3 = CreateTestScript("Test3.me");

        // Act
        _viewModel.OpenScript(script1);
        _viewModel.OpenScript(script2);
        _viewModel.OpenScript(script3);

        // Assert
        _viewModel.OpenDocuments.Should().HaveCount(3);
    }

    #endregion

    #region Property Change Notification Tests

    [Fact]
    public void StatusMessage_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.StatusMessage))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.StatusMessage = "Test Status";

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void IsBusy_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsBusy))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.IsBusy = true;

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void PlaybackStatus_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.PlaybackStatus))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.PlaybackStatus = "Running";

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void ActiveDocument_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.ActiveDocument))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.OpenScript(CreateTestScript("Test.me"));

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void CurrentProject_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.CurrentProject))
                propertyChangedRaised = true;
        };
        var project = CreateTestProject("TestProject");

        // Act - simulate project loaded event
        _projectService.ProjectLoaded += Raise.Event<EventHandler<MusicProject>>(
            _projectService, project);

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    #endregion

    #region Command Availability Tests

    [Fact]
    public void NewProjectCommand_ShouldExist()
    {
        // Assert
        _viewModel.NewProjectCommand.Should().NotBeNull();
    }

    [Fact]
    public void OpenProjectCommand_ShouldExist()
    {
        // Assert
        _viewModel.OpenProjectCommand.Should().NotBeNull();
    }

    [Fact]
    public void SaveCommand_ShouldExist()
    {
        // Assert
        _viewModel.SaveCommand.Should().NotBeNull();
    }

    [Fact]
    public void SaveAllCommand_ShouldExist()
    {
        // Assert
        _viewModel.SaveAllCommand.Should().NotBeNull();
    }

    [Fact]
    public void RunCommand_ShouldExist()
    {
        // Assert
        _viewModel.RunCommand.Should().NotBeNull();
    }

    [Fact]
    public void StopCommand_ShouldExist()
    {
        // Assert
        _viewModel.StopCommand.Should().NotBeNull();
    }

    [Fact]
    public void FindCommand_ShouldExist()
    {
        // Assert
        _viewModel.FindCommand.Should().NotBeNull();
    }

    [Fact]
    public void ReplaceCommand_ShouldExist()
    {
        // Assert
        _viewModel.ReplaceCommand.Should().NotBeNull();
    }

    [Fact]
    public void NewFileCommand_ShouldExist()
    {
        // Assert
        _viewModel.NewFileCommand.Should().NotBeNull();
    }

    [Fact]
    public void AddScriptCommand_ShouldExist()
    {
        // Assert
        _viewModel.AddScriptCommand.Should().NotBeNull();
    }

    [Fact]
    public void ImportAudioCommand_ShouldExist()
    {
        // Assert
        _viewModel.ImportAudioCommand.Should().NotBeNull();
    }

    [Fact]
    public void OpenSettingsCommand_ShouldExist()
    {
        // Assert
        _viewModel.OpenSettingsCommand.Should().NotBeNull();
    }

    [Fact]
    public void ExitCommand_ShouldExist()
    {
        // Assert
        _viewModel.ExitCommand.Should().NotBeNull();
    }

    #endregion

    #region Event Handler Tests

    [Fact]
    public void OnProjectLoaded_ShouldSetCurrentProject()
    {
        // Arrange
        var project = CreateTestProject("TestProject");

        // Act
        _projectService.ProjectLoaded += Raise.Event<EventHandler<MusicProject>>(
            _projectService, project);

        // Assert
        _viewModel.CurrentProject.Should().Be(project);
    }

    [Fact]
    public void OnProjectLoaded_ShouldLoadProjectInExplorer()
    {
        // Arrange
        var project = CreateTestProject("TestProject");

        // Act
        _projectService.ProjectLoaded += Raise.Event<EventHandler<MusicProject>>(
            _projectService, project);

        // Assert
        _viewModel.ProjectExplorer!.Project.Should().Be(project);
    }

    [Fact]
    public void OnProjectLoaded_ShouldOpenEntryPointScript()
    {
        // Arrange
        var project = CreateTestProject("TestProject");
        var entryScript = CreateTestScript("Main.me", isEntryPoint: true);
        project.Scripts.Add(entryScript);

        // Act
        _projectService.ProjectLoaded += Raise.Event<EventHandler<MusicProject>>(
            _projectService, project);

        // Assert
        _viewModel.OpenDocuments.Should().HaveCount(1);
        _viewModel.OpenDocuments[0].Script.Should().Be(entryScript);
    }

    [Fact]
    public void OnOutputReceived_ShouldAppendToOutput()
    {
        // Arrange
        var testMessage = "Test output message";

        // Act
        _executionService.OutputReceived += Raise.Event<EventHandler<string>>(
            _executionService, testMessage);

        // Assert
        _viewModel.Output!.OutputText.Should().Contain(testMessage);
    }

    [Fact]
    public void OnExecutionStarted_ShouldSetPlaybackStatusToRunning()
    {
        // Act
        _executionService.ExecutionStarted += Raise.Event<EventHandler>(
            _executionService, EventArgs.Empty);

        // Assert
        _viewModel.PlaybackStatus.Should().Be("Running");
    }

    [Fact]
    public void OnExecutionStopped_ShouldSetPlaybackStatusToStopped()
    {
        // Arrange
        _viewModel.PlaybackStatus = "Running";

        // Act
        _executionService.ExecutionStopped += Raise.Event<EventHandler>(
            _executionService, EventArgs.Empty);

        // Assert
        _viewModel.PlaybackStatus.Should().Be("Stopped");
    }

    #endregion

    #region CloseDocument Tests

    [Fact]
    public async Task CloseDocumentAsync_ShouldRemoveFromOpenDocuments()
    {
        // Arrange
        var script = CreateTestScript("Test.me");
        _viewModel.OpenScript(script);
        var document = _viewModel.OpenDocuments[0];
        document.IsDirty = false;

        // Act
        await _viewModel.CloseDocumentAsync(document);

        // Assert
        _viewModel.OpenDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task CloseDocumentAsync_ShouldSetActiveDocumentToNull_WhenLastDocumentClosed()
    {
        // Arrange
        var script = CreateTestScript("Test.me");
        _viewModel.OpenScript(script);
        var document = _viewModel.OpenDocuments[0];
        document.IsDirty = false;

        // Act
        await _viewModel.CloseDocumentAsync(document);

        // Assert
        _viewModel.ActiveDocument.Should().BeNull();
    }

    [Fact]
    public async Task CloseDocumentAsync_ShouldSwitchToFirstDocument_WhenActiveDocumentClosed()
    {
        // Arrange
        var script1 = CreateTestScript("Test1.me");
        var script2 = CreateTestScript("Test2.me");
        _viewModel.OpenScript(script1);
        _viewModel.OpenScript(script2);
        var document2 = _viewModel.OpenDocuments[1];
        document2.IsDirty = false;

        // Act
        await _viewModel.CloseDocumentAsync(document2);

        // Assert
        _viewModel.ActiveDocument.Should().NotBeNull();
        _viewModel.ActiveDocument!.Script.Should().Be(script1);
    }

    #endregion

    #region Helper Methods

    private static MusicScript CreateTestScript(string fileName, bool isEntryPoint = false)
    {
        return new MusicScript
        {
            FilePath = $"C:\\Test\\Scripts\\{fileName}",
            Namespace = "Test.Scripts",
            Content = "// Test content",
            IsEntryPoint = isEntryPoint
        };
    }

    private static MusicProject CreateTestProject(string name)
    {
        return new MusicProject
        {
            Name = name,
            Guid = Guid.NewGuid(),
            Namespace = name,
            FilePath = $"C:\\Test\\{name}\\{name}.meproj",
            MusicEngineVersion = "1.0.0"
        };
    }

    #endregion
}
