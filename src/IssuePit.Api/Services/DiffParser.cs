using System.Text.RegularExpressions;

namespace IssuePit.Api.Services;

public static partial class DiffParser
{
    [GeneratedRegex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@(.*)$")]
    private static partial Regex HunkHeaderRegex();

    public static List<DiffHunkDto> ParsePatch(string? patch)
    {
        var hunks = new List<DiffHunkDto>();
        if (string.IsNullOrEmpty(patch)) return hunks;

        var lines = patch.Split('\n');
        DiffHunkDto? current = null;
        int oldLine = 0, newLine = 0;

        foreach (var raw in lines)
        {
            var m = HunkHeaderRegex().Match(raw);
            if (m.Success)
            {
                if (current is not null) hunks.Add(current);

                oldLine = int.Parse(m.Groups[1].Value);
                newLine = int.Parse(m.Groups[3].Value);

                current = new DiffHunkDto
                {
                    Header = raw,
                    OldStart = oldLine,
                    OldCount = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1,
                    NewStart = newLine,
                    NewCount = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 1,
                    Lines = []
                };
            }
            else if (current is not null)
            {
                if (raw.StartsWith('-'))
                {
                    current.Lines.Add(new DiffLineDto { Type = "deletion", OldLineNo = oldLine++, Content = raw });
                }
                else if (raw.StartsWith('+'))
                {
                    current.Lines.Add(new DiffLineDto { Type = "addition", NewLineNo = newLine++, Content = raw });
                }
                else if (!raw.StartsWith('\\'))
                {
                    // context line (skip "\ No newline at end of file")
                    current.Lines.Add(new DiffLineDto { Type = "context", OldLineNo = oldLine++, NewLineNo = newLine++, Content = raw });
                }
            }
        }

        if (current is not null) hunks.Add(current);
        return hunks;
    }
}

public record DiffLineDto(string Type = "", string Content = "", int? OldLineNo = null, int? NewLineNo = null)
{
    public string Type { get; init; } = Type;
    public string Content { get; init; } = Content;
    public int? OldLineNo { get; init; } = OldLineNo;
    public int? NewLineNo { get; init; } = NewLineNo;
}

public class DiffHunkDto
{
    public string Header { get; set; } = string.Empty;
    public int OldStart { get; set; }
    public int OldCount { get; set; }
    public int NewStart { get; set; }
    public int NewCount { get; set; }
    public List<DiffLineDto> Lines { get; set; } = [];
}
