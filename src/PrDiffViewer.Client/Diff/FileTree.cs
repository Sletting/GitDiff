using PrDiffViewer.Shared.Models;

namespace PrDiffViewer.Client.Diff;

/// <summary>A node in the changed-files tree: either a folder (with children) or a file leaf.</summary>
public sealed class TreeNode
{
    public string Name { get; set; } = "";
    /// <summary>Full path: the file path for leaves, the folder path for folders.</summary>
    public string Path { get; set; } = "";
    public bool IsFolder { get; set; }
    public FileDiff? File { get; set; }
    public List<TreeNode> Children { get; } = new();
    public bool Expanded { get; set; } = true;

    /// <summary>DOM id of the diff card this file maps to (leaves only).</summary>
    public string CardId => FileTree.CardId(Path);
}

/// <summary>Builds the changed-files tree from a flat diff, the way the ADO file panel presents it.</summary>
public static class FileTree
{
    /// <summary>Stable DOM id for a file's diff card, used to scroll to it from the tree.</summary>
    public static string CardId(string path) => "file-" + path.Replace('/', '-').Replace('.', '_');

    public static List<TreeNode> Build(IEnumerable<FileDiff> files)
    {
        var root = new TreeNode { IsFolder = true };

        foreach (var file in files)
        {
            var parts = file.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                bool isLeaf = i == parts.Length - 1;
                if (isLeaf)
                {
                    current.Children.Add(new TreeNode
                    {
                        IsFolder = false,
                        Name = parts[i],
                        Path = file.Path,
                        File = file
                    });
                }
                else
                {
                    var folderPath = string.Join('/', parts.Take(i + 1));
                    var existing = current.Children.FirstOrDefault(c => c.IsFolder && c.Path == folderPath);
                    if (existing is null)
                    {
                        existing = new TreeNode { IsFolder = true, Name = parts[i], Path = folderPath };
                        current.Children.Add(existing);
                    }
                    current = existing;
                }
            }
        }

        Compress(root);
        Sort(root);
        return root.Children;
    }

    /// <summary>Collapse chains of single-child folders into one row, e.g. "src" + "Utils" -> "src/Utils".</summary>
    private static void Compress(TreeNode node)
    {
        foreach (var child in node.Children)
            Compress(child);

        if (!node.IsFolder)
            return;

        while (node.Children.Count == 1 && node.Children[0].IsFolder)
        {
            var only = node.Children[0];
            node.Name = node.Name.Length == 0 ? only.Name : $"{node.Name}/{only.Name}";
            node.Path = only.Path;
            node.Children.Clear();
            node.Children.AddRange(only.Children);
        }
    }

    /// <summary>Folders first, then files; alphabetical within each group (recursive).</summary>
    private static void Sort(TreeNode node)
    {
        node.Children.Sort((a, b) =>
        {
            if (a.IsFolder != b.IsFolder) return a.IsFolder ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        foreach (var child in node.Children)
            Sort(child);
    }
}
