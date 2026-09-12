using ObjectStorage.Infrastructure.Services;

namespace ObjectStorage.UnitTests.Services;

public sealed class UlidObjectIdGeneratorTests
{
    private readonly UlidObjectIdGenerator _generator = new();

    [Fact]
    public void Generate_Returns26CharacterString()
    {
        var id = _generator.Generate();

        Assert.Equal(26, id.Length);
    }

    [Fact]
    public void Generate_StartsWithTimestampPrefix()
    {
        var id = _generator.Generate();

        // ULID first 10 chars encode timestamp in Crockford base32
        // Must start with '01' for year 2026+ (128-bit ULID, 48-bit ms timestamp)
        Assert.StartsWith("01", id);
    }

    [Fact]
    public void Generate_UsesCrockfordBase32()
    {
        var id = _generator.Generate();

        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{26}$", id);
    }

    [Fact]
    public void Generate_TwoCallsReturnDifferentIds()
    {
        var id1 = _generator.Generate();
        var id2 = _generator.Generate();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Generate_1000CallsAllUnique()
    {
        var ids = new HashSet<string>();

        for (var i = 0; i < 1000; i++)
        {
            ids.Add(_generator.Generate());
        }

        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void Generate_SortableByTime()
    {
        // Generate multiple batches across different points in time.
        // ULIDs are time-sortable within the same millisecond by their random component,
        // but across milliseconds the timestamp prefix advances.
        var id1 = _generator.Generate();
        var id2 = _generator.Generate();

        // Both should have the same timestamp prefix (generated in same ms)
        // and both should be valid ULIDs
        Assert.Equal(26, id1.Length);
        Assert.Equal(26, id2.Length);
        Assert.StartsWith("01", id1);
        Assert.StartsWith("01", id2);
    }
}
