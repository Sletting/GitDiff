using PrDiffViewer.Shared.Models;

namespace PrDiffViewer.Client.Diff;

/// <summary>A block in a file's rendered diff: either real lines, or a collapsed (expandable) gap.</summary>
public abstract class DiffBlock { }

public sealed class LinesBlock : DiffBlock
{
    public List<DiffLine> Lines { get; } = new();
    public LinesBlock() { }
    public LinesBlock(IEnumerable<DiffLine> lines) => Lines.AddRange(lines);
}

/// <summary>A run of hidden, unchanged context lines that can be expanded.</summary>
public sealed class GapBlock : DiffBlock
{
    /// <summary>1-based first hidden line number on the old side.</summary>
    public int OldStart { get; set; }
    /// <summary>1-based first hidden line number on the new side.</summary>
    public int NewStart { get; set; }
    /// <summary>Number of hidden lines, or null when open-ended (gap to end of file).</summary>
    public int? Count { get; set; }
    public bool IsTop { get; set; }
    public bool IsBottom { get; set; }

    /// <summary>new-line minus old-line offset, constant within an unchanged region.</summary>
    public int Offset => NewStart - OldStart;
}

public static class DiffBlocks
{
    /// <summary>Builds the initial block list for a file: gaps before/between hunks (and after, for edits).</summary>
    public static List<DiffBlock> Build(FileDiff file)
    {
        var blocks = new List<DiffBlock>();
        if (file.Hunks.Count == 0)
            return blocks;

        bool canExpandTail = file.ChangeKind is ChangeKind.Modified or ChangeKind.Renamed
            or ChangeKind.Copied or ChangeKind.TypeChanged;

        var first = file.Hunks[0];
        if (first.NewStart > 1 && first.OldStart > 1)
            blocks.Add(new GapBlock { IsTop = true, OldStart = 1, NewStart = 1, Count = first.NewStart - 1 });

        for (int i = 0; i < file.Hunks.Count; i++)
        {
            var h = file.Hunks[i];
            blocks.Add(new LinesBlock(h.Lines));

            int newEnd = h.NewStart + h.NewLines - 1;
            int oldEnd = h.OldStart + h.OldLines - 1;

            if (i + 1 < file.Hunks.Count)
            {
                var next = file.Hunks[i + 1];
                int gap = next.NewStart - (newEnd + 1);
                if (gap > 0)
                    blocks.Add(new GapBlock { OldStart = oldEnd + 1, NewStart = newEnd + 1, Count = gap });
            }
            else if (canExpandTail && h.NewLines > 0)
            {
                blocks.Add(new GapBlock { IsBottom = true, OldStart = oldEnd + 1, NewStart = newEnd + 1, Count = null });
            }
        }

        return blocks;
    }

    /// <summary>Turns fetched file text into context lines with correctly mapped old/new numbers.</summary>
    public static List<DiffLine> ToContextLines(IReadOnlyList<string> texts, int firstNewLine, int offset)
    {
        var lines = new List<DiffLine>(texts.Count);
        for (int i = 0; i < texts.Count; i++)
        {
            int newNo = firstNewLine + i;
            lines.Add(new DiffLine
            {
                Kind = DiffLineKind.Context,
                NewLineNumber = newNo,
                OldLineNumber = newNo - offset,
                Content = texts[i]
            });
        }
        return lines;
    }
}
