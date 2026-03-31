using IssuePit.Notes.Core.Services;

namespace IssuePit.Notes.Tests.Integration;

[Trait("Category", "Unit")]
public class OtEngineComposeTests
{
    // ── Compose ──────────────────────────────────────────────────────────

    [Fact]
    public void Compose_TwoInserts_ProducesEquivalentSingleDelta()
    {
        // Start: "hello"
        // A: insert " world" at end → "hello world"
        // B: insert "!" at end → "hello world!"
        // compose(A, B) should be equivalent to applying both
        var a = OtEngine.Deserialize("[{\"retain\":5},{\"insert\":\" world\"}]");
        var b = OtEngine.Deserialize("[{\"retain\":11},{\"insert\":\"!\"}]");

        var composed = OtEngine.Compose(a, b);
        var doc = "hello";
        var viaCompose = OtEngine.Apply(doc, composed);
        var viaSequential = OtEngine.Apply(OtEngine.Apply(doc, a), b);

        Assert.Equal(viaSequential, viaCompose);
        Assert.Equal("hello world!", viaCompose);
    }

    [Fact]
    public void Compose_InsertThenDelete_Cancels()
    {
        // A: insert "X" at position 0 → "Xhello"
        // B: delete 1 char at position 0 (removing the "X") → "hello"
        // compose(A, B) should yield the identity (no net change)
        var a = OtEngine.Deserialize("[{\"insert\":\"X\"}]");
        var b = OtEngine.Deserialize("[{\"delete\":1}]");

        var composed = OtEngine.Compose(a, b);
        var doc = "hello";
        var result = OtEngine.Apply(doc, composed);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Compose_DeleteThenInsert_Replacement()
    {
        // A: delete "hello" → ""
        // B: insert "world" → "world"
        var a = OtEngine.Deserialize("[{\"delete\":5}]");
        var b = OtEngine.Deserialize("[{\"insert\":\"world\"}]");

        var composed = OtEngine.Compose(a, b);
        var doc = "hello";
        var result = OtEngine.Apply(doc, composed);

        Assert.Equal("world", result);
    }

    [Fact]
    public void Compose_RetainRetain_CombinesCorrectly()
    {
        // Both ops just retain → result retains the same chars
        var a = OtEngine.Deserialize("[{\"retain\":5}]");
        var b = OtEngine.Deserialize("[{\"retain\":5}]");

        var composed = OtEngine.Compose(a, b);
        var doc = "hello";
        var result = OtEngine.Apply(doc, composed);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Compose_MultipleOps_EquivalentToSequential()
    {
        // More complex scenario: A replaces middle, B appends
        // doc = "the quick brown fox"
        // A: retain 4, delete 5, insert "slow", retain 10 → "the slow brown fox"
        // B: retain 18, insert "!" → "the slow brown fox!"
        var doc = "the quick brown fox";
        var a = OtEngine.Deserialize("[{\"retain\":4},{\"delete\":5},{\"insert\":\"slow\"},{\"retain\":10}]");
        var b = OtEngine.Deserialize("[{\"retain\":18},{\"insert\":\"!\"}]");

        var composed = OtEngine.Compose(a, b);
        var viaCompose = OtEngine.Apply(doc, composed);
        var viaSequential = OtEngine.Apply(OtEngine.Apply(doc, a), b);

        Assert.Equal(viaSequential, viaCompose);
    }
}
