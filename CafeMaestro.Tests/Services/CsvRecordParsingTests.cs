using CafeMaestro.Services;
using FluentAssertions;

namespace CafeMaestro.Tests.Services;

/// <summary>
/// CSV reading is record-aware, not line-aware. CafeMaestro quotes every note it exports, so a note
/// containing a line break produces a logical record that spans physical lines.
/// </summary>
public sealed class CsvRecordParsingTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public CsvRecordParsingTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task ReadCsvContentAsync_KeepsAMultilineQuotedFieldAsOneRow()
    {
        string path = await WriteAsync(
            "multiline.csv",
            """
            Date,Bean Type,Notes
            2026-03-01,Kenya AA,"First line
            second line"
            2026-03-02,Colombia,"Single line"
            """);

        List<Dictionary<string, string>> rows = await new CsvParserService().ReadCsvContentAsync(path, int.MaxValue);

        rows.Should().HaveCount(2);
        rows[0]["Notes"].Should().Be($"First line{Environment.NewLine}second line".Replace("\r\n", "\n"));
        rows[0]["Bean Type"].Should().Be("Kenya AA");
        rows[1]["Notes"].Should().Be("Single line");
    }

    [Fact]
    public async Task ReadCsvContentAsync_UnescapesDoubledQuotesAndKeepsQuotedCommas()
    {
        string path = await WriteAsync(
            "quotes.csv",
            """
            Bean Type,Notes
            "Ethiopia ""Guji"" lot","Sweet, floral, clean"
            """);

        List<Dictionary<string, string>> rows = await new CsvParserService().ReadCsvContentAsync(path, int.MaxValue);

        rows.Should().ContainSingle();
        rows[0]["Bean Type"].Should().Be("Ethiopia \"Guji\" lot");
        rows[0]["Notes"].Should().Be("Sweet, floral, clean");
    }

    [Fact]
    public async Task GetCsvHeadersAsync_IsQuoteAware()
    {
        string path = await WriteAsync(
            "quotedheaders.csv",
            """
            "Bean Type","Weight, g",Notes
            Kenya AA,220,ok
            """);

        List<string> headers = await new CsvParserService().GetCsvHeadersAsync(path);

        headers.Should().Equal("Bean Type", "Weight, g", "Notes");
    }

    [Fact]
    public async Task ReadCsvContentAsync_StillSkipsBlankAndCommentLines()
    {
        string path = await WriteAsync(
            "comments.csv",
            """
            // exported by CafeMaestro

            Bean Type,Notes
            Kenya AA,ok

            """);

        CsvParserService parser = new();

        (await parser.GetCsvHeadersAsync(path)).Should().Equal("Bean Type", "Notes");
        (await parser.ReadCsvContentAsync(path, int.MaxValue)).Should().ContainSingle();
    }

    [Fact]
    public void ParseRecords_WithAnUnterminatedQuote_EndsCleanlyInsteadOfThrowing()
    {
        // A malformed record must not abort the file: everything before it stays reviewable.
        List<List<string>> records = CsvParserService.ParseRecords(
            "Bean Type,Notes\nKenya AA,ok\nColombia,\"never closed");

        records.Should().HaveCount(3);
        records[1].Should().Equal("Kenya AA", "ok");
        records[2].Should().Equal("Colombia", "never closed");
    }

    [Fact]
    public void ParseRecords_HandlesCrLfAndALastRowWithoutATrailingNewline()
    {
        List<List<string>> records = CsvParserService.ParseRecords("A,B\r\n1,2\r\n3,4");

        records.Should().HaveCount(3);
        records[2].Should().Equal("3", "4");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private async Task<string> WriteAsync(string fileName, string content)
    {
        string path = Path.Combine(_testDirectory, fileName);
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
