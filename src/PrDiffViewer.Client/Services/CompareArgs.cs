using PrDiffViewer.Shared.Models;

namespace PrDiffViewer.Client.Services;

/// <summary>The user's choices on the setup screen, passed up to load a diff.</summary>
public record CompareArgs(string RepoPath, string Source, string Target, DiffMode Mode);
