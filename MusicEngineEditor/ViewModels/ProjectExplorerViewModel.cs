using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Project Explorer panel
/// </summary>
public partial class ProjectExplorerViewModel : ViewModelBase
{
    [ObservableProperty]
    private MusicProject? _project;

    [ObservableProperty]
    private FileTreeNode? _selectedNode;

    public ObservableCollection<FileTreeNode> RootNodes { get; } = new();

    public event System.EventHandler<MusicScript>? ScriptDoubleClicked;

    public void LoadProject(MusicProject project)
    {
        Project = project;
        RootNodes.Clear();

        if (project == null) return;

        // Create root node for project
        var projectNode = new FileTreeNode
        {
            Name = project.Name,
            FullPath = project.FilePath,
            NodeType = FileTreeNodeType.Project,
            IsExpanded = true
        };

        // Add Scripts folder
        var scriptsNode = new FileTreeNode
        {
            Name = "Scripts",
            FullPath = Path.Combine(project.ProjectDirectory, "Scripts"),
            NodeType = FileTreeNodeType.Folder,
            IsExpanded = true
        };

        foreach (var script in project.Scripts)
        {
            scriptsNode.Children.Add(new FileTreeNode
            {
                Name = script.FileName,
                FullPath = script.FilePath,
                NodeType = FileTreeNodeType.Script,
                Script = script,
                IsEntryPoint = script.IsEntryPoint
            });
        }

        projectNode.Children.Add(scriptsNode);

        // Add Audio folder
        var audioNode = new FileTreeNode
        {
            Name = "Audio",
            FullPath = Path.Combine(project.ProjectDirectory, "Audio"),
            NodeType = FileTreeNodeType.Folder,
            IsExpanded = true
        };

        // Group by category
        var categories = new Dictionary<string, FileTreeNode>();
        foreach (var asset in project.AudioAssets)
        {
            if (!categories.TryGetValue(asset.Category, out var categoryNode))
            {
                categoryNode = new FileTreeNode
                {
                    Name = asset.Category,
                    FullPath = Path.Combine(project.ProjectDirectory, "Audio", asset.Category),
                    NodeType = FileTreeNodeType.Folder
                };
                categories[asset.Category] = categoryNode;
                audioNode.Children.Add(categoryNode);
            }

            categoryNode.Children.Add(new FileTreeNode
            {
                Name = $"{asset.Alias} ({asset.FileName})",
                FullPath = asset.FilePath,
                NodeType = FileTreeNodeType.Audio,
                AudioAsset = asset
            });
        }

        projectNode.Children.Add(audioNode);

        // Add References folder
        if (project.References.Count > 0)
        {
            var referencesNode = new FileTreeNode
            {
                Name = "References",
                FullPath = string.Empty,
                NodeType = FileTreeNodeType.Folder
            };

            foreach (var reference in project.References)
            {
                referencesNode.Children.Add(new FileTreeNode
                {
                    Name = reference.Alias,
                    FullPath = reference.Path,
                    NodeType = FileTreeNodeType.Reference,
                    Reference = reference
                });
            }

            projectNode.Children.Add(referencesNode);
        }

        RootNodes.Add(projectNode);
    }

    [RelayCommand]
    private void NodeDoubleClick(FileTreeNode? node)
    {
        if (node?.Script != null)
        {
            ScriptDoubleClicked?.Invoke(this, node.Script);
        }
    }

    [RelayCommand]
    private void AddNewScript()
    {
        if (Project == null) return;

        // Get script name from user
        var scriptName = InputDialog.Show(
            "Enter script name (without extension):",
            "New Script",
            "NewScript");

        if (string.IsNullOrWhiteSpace(scriptName)) return;

        // Sanitize the script name
        scriptName = SanitizeFileName(scriptName);
        if (string.IsNullOrEmpty(scriptName))
        {
            MessageBox.Show("Invalid script name.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Ensure .me extension
        if (!scriptName.EndsWith(".me", StringComparison.OrdinalIgnoreCase))
            scriptName += ".me";

        try
        {
            // Determine target folder
            var scriptsFolder = Path.Combine(Project.ProjectDirectory, "Scripts");
            if (!Directory.Exists(scriptsFolder))
                Directory.CreateDirectory(scriptsFolder);

            var filePath = Path.Combine(scriptsFolder, scriptName);

            // Check if file already exists
            if (File.Exists(filePath))
            {
                MessageBox.Show($"A script named '{scriptName}' already exists.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create the script file with default content
            var defaultContent = $"// {Path.GetFileNameWithoutExtension(scriptName)}\n// Created: {DateTime.Now:yyyy-MM-dd}\n\n";
            File.WriteAllText(filePath, defaultContent);

            // Create the MusicScript object
            var newScript = new MusicScript
            {
                FilePath = filePath,
                Content = defaultContent,
                Project = Project,
                IsEntryPoint = false,
                LastModified = DateTime.UtcNow
            };

            // Add to project
            Project.Scripts.Add(newScript);
            Project.IsDirty = true;

            // Add to tree view
            var scriptsNode = FindScriptsNode();
            if (scriptsNode != null)
            {
                scriptsNode.Children.Add(new FileTreeNode
                {
                    Name = newScript.FileName,
                    FullPath = newScript.FilePath,
                    NodeType = FileTreeNodeType.Script,
                    Script = newScript,
                    IsEntryPoint = false
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create script: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FileTreeNode? FindScriptsNode()
    {
        if (RootNodes.Count == 0) return null;
        var projectNode = RootNodes[0];
        return projectNode.Children.FirstOrDefault(n => n.Name == "Scripts" && n.NodeType == FileTreeNodeType.Folder);
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
    }

    [RelayCommand]
    private void AddNewFolder()
    {
        if (Project == null) return;

        // Determine parent node - use selected node if it's a folder, otherwise use Scripts folder
        var parentNode = SelectedNode;
        if (parentNode == null || (parentNode.NodeType != FileTreeNodeType.Folder && parentNode.NodeType != FileTreeNodeType.Project))
        {
            // Default to Scripts folder if no valid folder is selected
            parentNode = FindScriptsNode();
        }

        if (parentNode == null)
        {
            MessageBox.Show("Please select a folder to add the new folder to.", "No Folder Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Get folder name from user
        var folderName = InputDialog.Show(
            "Enter folder name:",
            "New Folder",
            "NewFolder");

        if (string.IsNullOrWhiteSpace(folderName)) return;

        // Sanitize the folder name
        folderName = SanitizeFileName(folderName);
        if (string.IsNullOrEmpty(folderName))
        {
            MessageBox.Show("Invalid folder name.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var parentPath = parentNode.NodeType == FileTreeNodeType.Project
                ? Project.ProjectDirectory
                : parentNode.FullPath;

            var folderPath = Path.Combine(parentPath, folderName);

            // Check if folder already exists
            if (Directory.Exists(folderPath))
            {
                MessageBox.Show($"A folder named '{folderName}' already exists.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create the folder on disk
            Directory.CreateDirectory(folderPath);

            // Add to tree view
            var newNode = new FileTreeNode
            {
                Name = folderName,
                FullPath = folderPath,
                NodeType = FileTreeNodeType.Folder,
                IsExpanded = true
            };

            parentNode.Children.Add(newNode);
            parentNode.IsExpanded = true;
            Project.IsDirty = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create folder: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DeleteNode()
    {
        if (Project == null || SelectedNode == null) return;

        var node = SelectedNode;

        // Prevent deletion of protected nodes
        if (node.NodeType == FileTreeNodeType.Project)
        {
            MessageBox.Show("Cannot delete the project node.", "Delete Not Allowed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (node.NodeType == FileTreeNodeType.Reference)
        {
            MessageBox.Show("Use Project > Remove Reference to remove references.", "Delete Not Allowed",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Build confirmation message
        var itemType = node.NodeType == FileTreeNodeType.Folder ? "folder" : "file";
        var hasChildren = node.Children.Count > 0;
        var warningMessage = hasChildren
            ? $"Are you sure you want to delete the {itemType} '{node.Name}' and all its contents?\n\nThis action cannot be undone."
            : $"Are you sure you want to delete '{node.Name}'?\n\nThis action cannot be undone.";

        var result = MessageBox.Show(warningMessage, "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            // Delete from file system
            if (node.NodeType == FileTreeNodeType.Folder)
            {
                if (Directory.Exists(node.FullPath))
                    Directory.Delete(node.FullPath, true);
            }
            else if (node.NodeType == FileTreeNodeType.Script || node.NodeType == FileTreeNodeType.Audio)
            {
                if (File.Exists(node.FullPath))
                    File.Delete(node.FullPath);
            }

            // Remove from project model
            if (node.NodeType == FileTreeNodeType.Script && node.Script != null)
            {
                Project.Scripts.Remove(node.Script);
            }
            else if (node.NodeType == FileTreeNodeType.Audio && node.AudioAsset != null)
            {
                Project.AudioAssets.Remove(node.AudioAsset);
            }
            else if (node.NodeType == FileTreeNodeType.Folder)
            {
                // Remove any scripts/assets that were in this folder
                RemoveChildrenFromProject(node);
            }

            // Remove from tree view
            RemoveNodeFromTree(node);

            Project.IsDirty = true;
            SelectedNode = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveChildrenFromProject(FileTreeNode folderNode)
    {
        foreach (var child in folderNode.Children)
        {
            if (child.NodeType == FileTreeNodeType.Script && child.Script != null)
            {
                Project?.Scripts.Remove(child.Script);
            }
            else if (child.NodeType == FileTreeNodeType.Audio && child.AudioAsset != null)
            {
                Project?.AudioAssets.Remove(child.AudioAsset);
            }
            else if (child.NodeType == FileTreeNodeType.Folder)
            {
                RemoveChildrenFromProject(child);
            }
        }
    }

    private void RemoveNodeFromTree(FileTreeNode nodeToRemove)
    {
        // Search through all nodes to find and remove the target
        foreach (var rootNode in RootNodes)
        {
            if (RemoveNodeFromChildren(rootNode.Children, nodeToRemove))
                return;
        }
    }

    private bool RemoveNodeFromChildren(ObservableCollection<FileTreeNode> children, FileTreeNode nodeToRemove)
    {
        if (children.Remove(nodeToRemove))
            return true;

        foreach (var child in children)
        {
            if (RemoveNodeFromChildren(child.Children, nodeToRemove))
                return true;
        }

        return false;
    }

    [RelayCommand]
    private void RenameNode()
    {
        if (Project == null || SelectedNode == null) return;

        var node = SelectedNode;

        // Prevent renaming of protected nodes
        if (node.NodeType == FileTreeNodeType.Project)
        {
            MessageBox.Show("Use Project > Properties to rename the project.", "Rename Not Allowed",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (node.NodeType == FileTreeNodeType.Reference)
        {
            MessageBox.Show("References cannot be renamed.", "Rename Not Allowed",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Get current name without extension for scripts
        var currentName = node.NodeType == FileTreeNodeType.Script
            ? Path.GetFileNameWithoutExtension(node.Name)
            : node.Name;

        // Get new name from user
        var promptText = node.NodeType == FileTreeNodeType.Folder
            ? "Enter new folder name:"
            : "Enter new name (without extension):";

        var newName = InputDialog.Show(promptText, "Rename", currentName);

        if (string.IsNullOrWhiteSpace(newName) || newName == currentName) return;

        // Sanitize the name
        newName = SanitizeFileName(newName);
        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("Invalid name.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var parentDir = Path.GetDirectoryName(node.FullPath) ?? string.Empty;
            string newPath;
            string newDisplayName;

            if (node.NodeType == FileTreeNodeType.Folder)
            {
                newPath = Path.Combine(parentDir, newName);
                newDisplayName = newName;

                // Check if folder already exists
                if (Directory.Exists(newPath))
                {
                    MessageBox.Show($"A folder named '{newName}' already exists.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Rename folder on disk
                Directory.Move(node.FullPath, newPath);

                // Update paths for all children
                UpdateChildPaths(node, node.FullPath, newPath);
            }
            else if (node.NodeType == FileTreeNodeType.Script)
            {
                // Preserve extension for scripts
                var extension = Path.GetExtension(node.FullPath);
                newPath = Path.Combine(parentDir, newName + extension);
                newDisplayName = newName + extension;

                // Check if file already exists
                if (File.Exists(newPath))
                {
                    MessageBox.Show($"A file named '{newDisplayName}' already exists.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Rename file on disk
                File.Move(node.FullPath, newPath);

                // Update the MusicScript model
                if (node.Script != null)
                {
                    node.Script.FilePath = newPath;
                }
            }
            else if (node.NodeType == FileTreeNodeType.Audio)
            {
                // Preserve extension for audio files
                var extension = Path.GetExtension(node.FullPath);
                newPath = Path.Combine(parentDir, newName + extension);
                newDisplayName = newName + extension;

                // Check if file already exists
                if (File.Exists(newPath))
                {
                    MessageBox.Show($"A file named '{newDisplayName}' already exists.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Rename file on disk
                File.Move(node.FullPath, newPath);

                // Update the AudioAsset model
                if (node.AudioAsset != null)
                {
                    node.AudioAsset.FilePath = newPath;
                    // Update display name to include alias
                    newDisplayName = $"{node.AudioAsset.Alias} ({newName}{extension})";
                }
            }
            else
            {
                return; // Unsupported node type
            }

            // Update tree node
            node.FullPath = newPath;
            node.Name = newDisplayName;

            Project.IsDirty = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to rename: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateChildPaths(FileTreeNode folderNode, string oldBasePath, string newBasePath)
    {
        foreach (var child in folderNode.Children)
        {
            // Update the child's path
            var relativePath = child.FullPath.Substring(oldBasePath.Length).TrimStart(Path.DirectorySeparatorChar);
            child.FullPath = Path.Combine(newBasePath, relativePath);

            // Update associated model paths
            if (child.Script != null)
            {
                child.Script.FilePath = child.FullPath;
            }
            else if (child.AudioAsset != null)
            {
                child.AudioAsset.FilePath = child.FullPath;
            }

            // Recursively update children
            if (child.NodeType == FileTreeNodeType.Folder)
            {
                UpdateChildPaths(child, oldBasePath, newBasePath);
            }
        }
    }
}

/// <summary>
/// Represents a node in the file tree
/// </summary>
public partial class FileTreeNode : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private FileTreeNodeType _nodeType;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEntryPoint;

    public MusicScript? Script { get; set; }
    public AudioAsset? AudioAsset { get; set; }
    public ProjectReference? Reference { get; set; }

    public ObservableCollection<FileTreeNode> Children { get; } = new();
}

public enum FileTreeNodeType
{
    Project,
    Folder,
    Script,
    Audio,
    Reference
}
