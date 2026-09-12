using ObjectStorage.Infrastructure.Services;

namespace ObjectStorage.UnitTests.Services;

public sealed class HierarchicalObjectKeyGeneratorTests
{
    private readonly HierarchicalObjectKeyGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsHierarchicalKey()
    {
        var key = _generator.Generate("myservice", "documents", "01ABC123", "report.pdf");

        Assert.Equal("myservice/documents/01ABC123/report.pdf", key);
    }

    [Fact]
    public void Generate_SanitizesPathSeparators()
    {
        var key = _generator.Generate("svc", "docs", "id1", "sub/dir/file.txt");

        Assert.Equal("svc/docs/id1/subdirfile.txt", key);
    }

    [Fact]
    public void Generate_SanitizesBackslashes()
    {
        var key = _generator.Generate("svc", "docs", "id1", @"path\to\file.txt");

        Assert.Equal("svc/docs/id1/pathtofile.txt", key);
    }

    [Fact]
    public void Generate_SanitizesControlCharacters()
    {
        var key = _generator.Generate("svc", "docs", "id1", "file\x00\x1F.txt");

        Assert.Equal("svc/docs/id1/file.txt", key);
    }

    [Fact]
    public void Generate_EmptyFileName_DefaultsToFile()
    {
        var key = _generator.Generate("svc", "docs", "id1", "");

        Assert.Equal("svc/docs/id1/file", key);
    }

    [Fact]
    public void Generate_WhitespaceFileName_DefaultsToFile()
    {
        var key = _generator.Generate("svc", "docs", "id1", "   ");

        Assert.Equal("svc/docs/id1/file", key);
    }

    [Fact]
    public void Generate_LeadingTrailingSpaces_Trimmed()
    {
        var key = _generator.Generate("svc", "docs", "id1", "  report.pdf  ");

        Assert.Equal("svc/docs/id1/report.pdf", key);
    }

    [Fact]
    public void Generate_OnlyIllegalChars_DefaultsToFile()
    {
        var key = _generator.Generate("svc", "docs", "id1", "/\\//");

        Assert.Equal("svc/docs/id1/file", key);
    }

    [Fact]
    public void Generate_LowercasesServiceAndCategory()
    {
        var key = _generator.Generate("MyService", "Documents", "id1", "file.pdf");

        Assert.Equal("myservice/documents/id1/file.pdf", key);
    }

    [Fact]
    public void Generate_ServiceAndCategoryNormalized()
    {
        var key = _generator.Generate("  My Service  ", "  Documents  ", "id1", "file.pdf");

        Assert.Equal("my service/documents/id1/file.pdf", key);
    }
}
