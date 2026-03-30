using System.Text;
using System.Text.Json;

namespace IssuePit.Notes.Core.Services;

/// <summary>
/// Operational Transformation engine for plain-text CRDT.
/// Implements the standard OT algorithm for sequential, concurrent text operations.
/// </summary>
/// <remarks>
/// Operations use Quill delta format: a list of retain/insert/delete ops.
/// - retain(N): keep N characters unchanged
/// - insert(text): insert text at the current position
/// - delete(N): remove N characters at the current position
/// The trailing portion of the document is always implicitly retained.
///
/// Transform guarantees convergence:
///   apply(apply(s, A), transform(B, A)) == apply(apply(s, B), transform(A, B))
/// </remarks>
public static class OtEngine
{
    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Apply a sequence of OT operations to a text document and return the result.
    /// </summary>
    public static string Apply(string doc, IReadOnlyList<TextOp> ops)
    {
        var sb = new StringBuilder(doc.Length + 256);
        int pos = 0;
        foreach (var op in ops)
        {
            switch (op)
            {
                case RetainOp r:
                    if (pos + r.Count > doc.Length)
                        throw new OtException($"Retain operation exceeds document bounds: attempting to retain {r.Count} characters at position {pos}, but document length is {doc.Length}");
                    sb.Append(doc, pos, r.Count);
                    pos += r.Count;
                    break;
                case InsertOp ins:
                    sb.Append(ins.Text);
                    break;
                case DeleteOp del:
                    if (pos + del.Count > doc.Length)
                        throw new OtException($"Delete operation exceeds document bounds: attempting to delete {del.Count} characters at position {pos}, but document length is {doc.Length}");
                    pos += del.Count;
                    break;
            }
        }
        // implicitly retain the tail
        if (pos < doc.Length)
            sb.Append(doc, pos, doc.Length - pos);
        return sb.ToString();
    }

    /// <summary>
    /// Transform <paramref name="op"/> so it can be applied to a document that already
    /// has <paramref name="concurrent"/> applied.
    /// </summary>
    /// <remarks>
    /// Returns <c>op'</c> such that:
    ///   apply(apply(doc, concurrent), op') == apply(apply(doc, op), concurrent')
    ///
    /// When both operations insert at the same position, <paramref name="concurrent"/>
    /// (the server's previously applied operation) wins (its content comes first).
    /// </remarks>
    public static List<TextOp> Transform(IReadOnlyList<TextOp> op, IReadOnlyList<TextOp> concurrent)
    {
        var result = new List<TextOp>(op.Count + 4);

        // Use index + remainder to allow partial consumption of each op
        int ia = 0, ib = 0;
        int remA = GetLen(ia < op.Count ? op[ia] : null);
        int remB = GetLen(ib < concurrent.Count ? concurrent[ib] : null);

        while (ia < op.Count)
        {
            var curA = op[ia];
            var curB = ib < concurrent.Count ? concurrent[ib] : null;

            // --- Consume all b-inserts before processing the current a-op ---
            // An insert in b means b added chars here; a must skip over them.
            while (curB is InsertOp insB && curA is not InsertOp)
            {
                AppendOrMerge(result, new RetainOp(insB.Text.Length));
                ib++;
                remB = GetLen(ib < concurrent.Count ? concurrent[ib] : null);
                curB = ib < concurrent.Count ? concurrent[ib] : null;
            }

            if (curA is InsertOp insA)
            {
                // a inserts text — always included as-is (b has already been accounted for above)
                AppendOrMerge(result, insA);
                ia++;
                remA = GetLen(ia < op.Count ? op[ia] : null);
                continue;
            }

            if (curB == null)
            {
                // No more b ops — include a as-is for the rest
                AppendOrMerge(result, curA);
                ia++;
                continue;
            }

            // Both curA and curB are retain or delete — consume min(remA, remB) characters
            int take = Math.Min(remA, remB);

            switch (curA, curB)
            {
                case (RetainOp, RetainOp):
                    AppendOrMerge(result, new RetainOp(take));
                    break;
                case (RetainOp, DeleteOp):
                    // b deleted chars that a wanted to retain — they're gone, nothing to output
                    break;
                case (DeleteOp, RetainOp):
                    AppendOrMerge(result, new DeleteOp(take));
                    break;
                case (DeleteOp, DeleteOp):
                    // Both deleted the same characters — cancel a's delete (b already did it)
                    break;
            }

            remA -= take;
            remB -= take;

            if (remA == 0)
            {
                ia++;
                remA = GetLen(ia < op.Count ? op[ia] : null);
            }

            if (remB == 0)
            {
                ib++;
                remB = GetLen(ib < concurrent.Count ? concurrent[ib] : null);
            }
        }

        // Drain any remaining b-inserts (b added chars after a's last position)
        while (ib < concurrent.Count)
        {
            if (concurrent[ib] is InsertOp trailIns)
                AppendOrMerge(result, new RetainOp(trailIns.Text.Length));
            ib++;
        }

        return result;
    }

    // ── Serialization ─────────────────────────────────────────────────────

    /// <summary>Serialize ops to JSON: [{"retain":5},{"insert":"hi"},{"delete":3}]</summary>
    public static string Serialize(IReadOnlyList<TextOp> ops)
    {
        var list = ops.Select(op => op switch
        {
            RetainOp r => (object)new { retain = r.Count },
            InsertOp i => new { insert = i.Text },
            DeleteOp d => new { delete = d.Count },
            _ => throw new OtException("Unknown op type")
        });
        return JsonSerializer.Serialize(list);
    }

    /// <summary>Deserialize ops from JSON.</summary>
    public static List<TextOp> Deserialize(string json)
    {
        List<JsonElement>? elements;
        try
        {
            elements = JsonSerializer.Deserialize<List<JsonElement>>(json);
        }
        catch (JsonException ex)
        {
            throw new OtException($"Invalid delta JSON: {ex.Message}");
        }

        if (elements is null) throw new OtException("Null delta JSON");

        return elements.Select(el =>
        {
            if (el.TryGetProperty("retain", out var r))
                return (TextOp)new RetainOp(r.GetInt32());
            if (el.TryGetProperty("insert", out var ins))
                return new InsertOp(ins.GetString() ?? "");
            if (el.TryGetProperty("delete", out var d))
                return new DeleteOp(d.GetInt32());
            throw new OtException($"Unknown op element: {el}");
        }).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static int GetLen(TextOp? op) => op switch
    {
        RetainOp r => r.Count,
        DeleteOp d => d.Count,
        InsertOp ins => ins.Text.Length,
        _ => 0
    };

    /// <summary>Merge consecutive same-type ops for a more compact result.</summary>
    private static void AppendOrMerge(List<TextOp> ops, TextOp op)
    {
        if (ops.Count > 0)
        {
            var last = ops[^1];
            if (last is RetainOp lr && op is RetainOp nr)
            {
                ops[^1] = new RetainOp(lr.Count + nr.Count);
                return;
            }
            if (last is InsertOp li && op is InsertOp ni)
            {
                ops[^1] = new InsertOp(li.Text + ni.Text);
                return;
            }
            if (last is DeleteOp ld && op is DeleteOp nd)
            {
                ops[^1] = new DeleteOp(ld.Count + nd.Count);
                return;
            }
        }
        ops.Add(op);
    }
}

// ── Operation types ───────────────────────────────────────────────────────

public abstract record TextOp;

/// <summary>Keep N characters unchanged and advance the cursor.</summary>
public record RetainOp(int Count) : TextOp;

/// <summary>Insert text at the current cursor position.</summary>
public record InsertOp(string Text) : TextOp;

/// <summary>Delete N characters at the current cursor position.</summary>
public record DeleteOp(int Count) : TextOp;

public sealed class OtException(string message) : Exception(message);
