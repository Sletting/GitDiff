using PrDiffViewer.Shared.Models;

namespace PrDiffViewer.Shared.Diff;

/// <summary>One row of a side-by-side diff: an optional original (left) line and an optional modified (right) line.</summary>
public sealed class SideBySideRow
{
    public DiffLine? Left { get; set; }
    public DiffLine? Right { get; set; }

    public bool IsContext => Left is { Kind: DiffLineKind.Context } || Right is { Kind: DiffLineKind.Context };
    /// <summary>A delete paired with an add on the same row (a true in-place modification).</summary>
    public bool IsModifiedPair => Left is { Kind: DiffLineKind.Deleted } && Right is { Kind: DiffLineKind.Added };
}

/// <summary>
/// Pairs the inline-ordered lines of a hunk into side-by-side rows. This is the single
/// source of truth for how deletes line up with adds, used both to render the
/// side-by-side view and to decide which line pairs get word-level highlighting.
/// </summary>
public static class SideBySide
{
    public static List<SideBySideRow> Build(IReadOnlyList<DiffLine> lines)
    {
        var rows = new List<SideBySideRow>(lines.Count);
        var dels = new List<DiffLine>();
        var adds = new List<DiffLine>();

        void Flush()
        {
            int max = Math.Max(dels.Count, adds.Count);
            for (int i = 0; i < max; i++)
            {
                var left = i < dels.Count ? dels[i] : null;
                var right = i < adds.Count ? adds[i] : null;
                rows.Add(new SideBySideRow { Left = left, Right = right });
            }
            dels.Clear();
            adds.Clear();
        }

        foreach (var line in lines)
        {
            switch (line.Kind)
            {
                case DiffLineKind.Deleted:
                    dels.Add(line);
                    break;
                case DiffLineKind.Added:
                    adds.Add(line);
                    break;
                default: // Context
                    Flush();
                    rows.Add(new SideBySideRow { Left = line, Right = line });
                    break;
            }
        }

        Flush();
        return rows;
    }
}
