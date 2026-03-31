using CafeMaestro.Services;
using FluentAssertions;

namespace CafeMaestro.Tests;

public sealed class CsvParserServiceTests : IDisposable
{
    private readonly string _testFilesDirectory;
    private readonly CsvParserService _csvParserService = new();

    public CsvParserServiceTests()
    {
        _testFilesDirectory = Path.Combine(AppContext.BaseDirectory, "CsvParserServiceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testFilesDirectory);
    }

    [Fact]
    public async Task GetCsvHeadersAsync_ReturnsHeadersAfterSkippingCommentsAndEmptyLines()
    {
        string filePath = await CreateCsvFileAsync(
            """

            // generated comment
            Name,Origin,Notes
            Ethiopian,Ethiopia,Fruity
            """);

        var headers = await _csvParserService.GetCsvHeadersAsync(filePath);

        headers.Should().Equal("Name", "Origin", "Notes");
    }

    [Fact]
    public async Task ReadCsvContentAsync_ReadsSimpleRows()
    {
        string filePath = await CreateCsvFileAsync(
            """
            Name,Origin
            Ethiopian,Ethiopia
            Colombia,Colombia
            """);

        var rows = await _csvParserService.ReadCsvContentAsync(filePath);

        rows.Should().HaveCount(2);
        rows[0]["Name"].Should().Be("Ethiopian");
        rows[1]["Origin"].Should().Be("Colombia");
    }

    [Fact]
    public async Task ReadCsvContentAsync_HandlesQuotedValuesAndEscapedQuotes()
    {
        string filePath = await CreateCsvFileAsync(
            "Name,Notes\n" +
            "\"House Blend\",\"Sweet, balanced\"\n" +
            "\"Special\"\"Reserve\",\"Quoted \"\"note\"\"\"");

        var rows = await _csvParserService.ReadCsvContentAsync(filePath);

        rows.Should().HaveCount(2);
        rows[0]["Notes"].Should().Be("Sweet, balanced");
        rows[1]["Name"].Should().Be("Special\"Reserve");
        rows[1]["Notes"].Should().Be("Quoted \"note\"");
    }

    [Fact]
    public async Task ReadCsvContentAsync_SkipsEmptyLinesAndLeadingCommentLines()
    {
        string filePath = await CreateCsvFileAsync(
            """
            // comment

            Name,Origin

            Ethiopian,Ethiopia

            Kenya,Kenya
            """);

        var rows = await _csvParserService.ReadCsvContentAsync(filePath);

        rows.Should().HaveCount(2);
        rows.Select(row => row["Name"]).Should().Equal("Ethiopian", "Kenya");
    }

    [Fact]
    public async Task ReadCsvContentAsync_HonorsMaxRowsLimit()
    {
        string filePath = await CreateCsvFileAsync(
            """
            Name,Origin
            Ethiopian,Ethiopia
            Kenya,Kenya
            Colombia,Colombia
            """);

        var rows = await _csvParserService.ReadCsvContentAsync(filePath, 2);

        rows.Should().HaveCount(2);
        rows.Select(row => row["Name"]).Should().Equal("Ethiopian", "Kenya");
    }

    [Fact]
    public async Task CsvMethods_ThrowWhenFileDoesNotExist()
    {
        string missingFile = Path.Combine(_testFilesDirectory, "missing.csv");

        Func<Task> getHeaders = () => _csvParserService.GetCsvHeadersAsync(missingFile);
        Func<Task> readContent = () => _csvParserService.ReadCsvContentAsync(missingFile);

        await getHeaders.Should().ThrowAsync<FileNotFoundException>();
        await readContent.Should().ThrowAsync<FileNotFoundException>();
    }

    private async Task<string> CreateCsvFileAsync(string content)
    {
        string filePath = Path.Combine(_testFilesDirectory, $"{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(filePath, content.ReplaceLineEndings(Environment.NewLine));
        return filePath;
    }

    public void Dispose()
    {
        if (!Directory.Exists(_testFilesDirectory))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(_testFilesDirectory, "*.csv"))
        {
            File.Delete(file);
        }

        Directory.Delete(_testFilesDirectory, false);
    }
}
